using GameServer.Application;
using GameServer.Application.Relics;
using GameServer.Domain.Relics;
using GameServer.Infrastructure.Postgres;
using GameServer.Infrastructure.Postgres.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GameServer.Infrastructure.Tests;

/// <summary>
/// The Infrastructure side of the Relic definition lookup boundary —
/// <c>DATABASE.md</c> §1's <c>RelicDefinition</c> block and its
/// "Relic <c>EffectDefinition</c> and <c>Condition</c> contract" note, plus
/// <c>RELIC_RULES.md</c> §8.1–§8.5 as recorded by <c>ADR-018</c> and stored by
/// TASK-132's migration.
///
/// <b>The implementation is the existing persistence boundary.</b> The read is
/// <c>RelicRepository.GetDefinitionAsync</c> against the existing
/// <c>RelicDefinition</c> table through the existing <c>GameDbContext</c> — no
/// new table, no new column, no migration, no cache, and no second
/// representation is introduced. What these tests verify is that reading through
/// that boundary yields the authoritative <b>Domain</b> <c>RelicDefinition</c>
/// with its structured <c>Condition</c> and its complete, ordered
/// <c>EffectDefinition[]</c>, that an unknown identity yields <c>null</c>, and
/// that the Application adapter can consume the boundary from the existing
/// singleton/scoped composition.
///
/// The provider-independent assertions run against the in-memory provider; the
/// applied-row assertions run against a real PostgreSQL instance and are skipped
/// when one is not reachable, following the existing
/// <see cref="PetCardRelicDefinitionPostgresProvisioningTests"/> convention.
///
/// <b>No test here resolves a Relic.</b> No trigger is evaluated, no condition is
/// compared, no effect is applied, and no <c>RelicTriggered</c> event is
/// emitted — <c>RELIC_RULES.md</c> §8.7 records every one of those as NOT
/// IMPLEMENTED.
/// </summary>
public class RelicDefinitionLookupTests
{
    private static GameDbContext CreateContext(string storeName) =>
        TestGameDbContextFactory.Create(storeName);

    private static RelicDefinition NewDefinition(
        string id,
        string name,
        string trigger,
        RelicCondition? condition,
        RelicEffectDefinitions effects) => new()
    {
        RelicDefinitionId = id,
        Name = name,
        Trigger = trigger,
        Condition = condition,
        EffectDefinition = effects,
    };

    private static async Task SeedProvisionedRowsAsync(string storeName)
    {
        await using var context = CreateContext(storeName);

        foreach (var row in RelicProvisionedContent.Rows)
        {
            context.RelicDefinitions.Add(
                NewDefinition(row.Id, row.Name, row.Trigger, row.Condition, row.Effects));
        }

        await context.SaveChangesAsync();
    }

    // -----------------------------------------------------------------------
    // Existing definition — DATABASE.md §1
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Repository_ShouldReturnTheStructuredDomainDefinitionByItsIdentity()
    {
        // The documented chain "database row → EF → Domain": the read is
        // addressed by the definition's own identity (DATABASE.md §1:
        // RelicDefinitionId) and yields the Domain RelicDefinition — never an EF
        // entity, a DbContext, or a JSON payload (ARCHITECTURE.md §2 item 3).
        var storeName = nameof(Repository_ShouldReturnTheStructuredDomainDefinitionByItsIdentity);

        await SeedProvisionedRowsAsync(storeName);

        await using var context = CreateContext(storeName);
        var repository = new RelicRepository(context);

        var found = await repository.GetDefinitionAsync("relic-berserker-core");

        Assert.NotNull(found);
        Assert.IsType<RelicDefinition>(found);
        Assert.Equal("GameServer.Domain.Relics.RelicDefinition", found!.GetType().FullName);

        var expected = RelicProvisionedContent.Single("relic-berserker-core");

        Assert.Equal(expected.Id, found.RelicDefinitionId);
        Assert.Equal(expected.Name, found.Name);
        Assert.Equal(expected.Trigger, found.Trigger);
    }

    [Fact]
    public async Task Repository_ShouldReturnNullForAnUnknownIdentity()
    {
        // RELIC_RULES.md §8.5 / DATABASE.md §1: an identity with no row yields
        // null. No empty definition, no default threshold, and no fallback effect
        // array is fabricated (AGENTS.md §7).
        //
        // CORRECTED FOR TASK-184. This assertion previously probed
        // "relic-burning-curse" as its genuine-absence case, on RELIC_RULES.md §6
        // note 3's deferral. TASK-176 content-defined that row (§8.5 item 5 gives
        // it `OnBattleStart`) and TASK-184 provisions it, so it now RESOLVES and
        // can no longer serve as an absent identity — the probe was replaced with
        // a key no document defines.
        var storeName = nameof(Repository_ShouldReturnNullForAnUnknownIdentity);

        await SeedProvisionedRowsAsync(storeName);

        await using var context = CreateContext(storeName);
        var repository = new RelicRepository(context);

        Assert.Null(await repository.GetDefinitionAsync("relic-non-existent"));

        // Burning Curse is provisioned now, so the lookup must RESOLVE it rather
        // than return null (TASK-184 AC-02).
        Assert.NotNull(await repository.GetDefinitionAsync("relic-burning-curse"));
    }

    // -----------------------------------------------------------------------
    // Structured Condition — RELIC_RULES.md §8.1, DATABASE.md §1 item 7
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Repository_ShouldMaterializeTheStructuredConditionOfEveryProvisionedRow()
    {
        // Each condition crosses the boundary as its structured value — the form
        // plus the integer threshold §8.1 item 1 requires — for every one of
        // §8.5's four rows, read member by member from the row the migration
        // encoded. The expectations are RelicProvisionedContent's transcription of
        // §8.5, not a stored representation of the old prose.
        var storeName = nameof(Repository_ShouldMaterializeTheStructuredConditionOfEveryProvisionedRow);

        await SeedProvisionedRowsAsync(storeName);

        await using var context = CreateContext(storeName);
        var repository = new RelicRepository(context);

        foreach (var row in RelicProvisionedContent.Rows)
        {
            var found = await repository.GetDefinitionAsync(row.Id);

            Assert.NotNull(found);
            Assert.Equal(row.Condition, found!.Condition);

            // RELIC_RULES.md §8.1 item 4: a Relic whose Trigger alone is its
            // complete condition carries none, so the four §8.5 rows that record
            // `null` read back as an absent Condition — never a sentinel.
            if (row.Condition is null)
            {
                Assert.Null(found.Condition);
            }
            else
            {
                Assert.Equal(row.Condition.Value.ConditionType, found.Condition!.Value.ConditionType);
                Assert.Equal(row.Condition.Value.Threshold, found.Condition!.Value.Threshold);
            }
        }
    }

    [Fact]
    public async Task Repository_ShouldReturnANullConditionWhenTheRowDeclaresNone()
    {
        // RELIC_RULES.md §8.1 item 4: Condition is optional (DATABASE.md §1: the
        // column is NULLable), so the absent case arrives as null rather than as a
        // sentinel condition that would read as a real one.
        var storeName = nameof(Repository_ShouldReturnANullConditionWhenTheRowDeclaresNone);

        await using (var seed = CreateContext(storeName))
        {
            seed.RelicDefinitions.Add(NewDefinition(
                "relic-no-condition",
                "No Condition Relic",
                "OnMatchCount",
                condition: null,
                RelicProvisionedContent.Single("relic-berserker-core").Effects));

            await seed.SaveChangesAsync();
        }

        await using var context = CreateContext(storeName);
        var repository = new RelicRepository(context);

        var found = await repository.GetDefinitionAsync("relic-no-condition");

        Assert.NotNull(found);
        Assert.Null(found!.Condition);
    }

    // -----------------------------------------------------------------------
    // Structured EffectDefinition[] — RELIC_RULES.md §8.2/§8.3
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Repository_ShouldMaterializeTheCompleteOrderedEffectDefinitionArray()
    {
        // EffectDefinition is a structured ARRAY (RELIC_RULES.md §8.2), and §8.2
        // item 4 fixes no canonical order, so the read preserves the stored
        // sequence exactly and normalizes nothing: every element, in its stored
        // order, with every one of the five members §8.2/§8.3 define. Positions
        // are observed only as stored order; no position carries gameplay meaning.
        var storeName = nameof(Repository_ShouldMaterializeTheCompleteOrderedEffectDefinitionArray);

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

        await using (var seed = CreateContext(storeName))
        {
            seed.RelicDefinitions.Add(NewDefinition(
                "relic-two-effects",
                "Two Effect Relic",
                "OnMatchCount",
                RelicCondition.Create(RelicConditionType.MatchCountAtLeast, 3),
                effects));

            await seed.SaveChangesAsync();
        }

        await using var context = CreateContext(storeName);
        var repository = new RelicRepository(context);

        var found = await repository.GetDefinitionAsync("relic-two-effects");

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

    // -----------------------------------------------------------------------
    // The applied PostgreSQL rows — DATABASE.md §1 item 7, RELIC_RULES.md §8.5
    // -----------------------------------------------------------------------

    private const string ConnectionString =
        "Host=localhost;Port=5433;Database=dcacti_db;Username=dcacti;Password=dcacti_dev_password";

    private static async Task<Npgsql.NpgsqlDataSource?> TryConnectAsync()
    {
        try
        {
            var dataSource = new Npgsql.NpgsqlDataSourceBuilder(ConnectionString).Build();

            await using var command = dataSource.CreateCommand(
                """
                SELECT to_regclass('"RelicDefinition"') IS NOT NULL
                   AND EXISTS (
                     SELECT 1 FROM information_schema.columns
                     WHERE table_name = 'RelicDefinition'
                       AND column_name = 'EffectDefinition'
                       AND data_type = 'jsonb')
                   AND EXISTS (
                     SELECT 1 FROM information_schema.columns
                     WHERE table_name = 'RelicDefinition'
                       AND column_name = 'Condition'
                       AND data_type = 'jsonb')
                """);

            var applied = (bool)(await command.ExecuteScalarAsync() ?? false);

            if (!applied)
            {
                await dataSource.DisposeAsync();
                return null;
            }

            return dataSource;
        }
        catch (Exception)
        {
            // No reachable PostgreSQL (or the migration is not applied): the
            // applied-row assertions are skipped rather than failing the suite,
            // following the existing RelicStructuredStorageTests convention. The
            // provider-independent assertions above still run.
            return null;
        }
    }

    private static GameDbContext CreatePostgresContext(Npgsql.NpgsqlDataSource dataSource) =>
        new(new DbContextOptionsBuilder<GameDbContext>()
            .UseNpgsql(dataSource)
            .Options);

    [Fact]
    public async Task AppliedRows_ShouldReturnEveryProvisionedDefinitionWithItsStructuredMembers()
    {
        // The documented chain "applied database row → EF → Domain", read through
        // the same repository the Application lookup delegates to, for each of
        // §8.5's four rows. Every structured member is asserted against
        // RelicProvisionedContent's transcription of §8.5 — the stored value is
        // the contract's, not the test's and not the implementation's.
        await using var dataSource = await TryConnectAsync();
        if (dataSource is null) return;

        await using var context = CreatePostgresContext(dataSource);
        var repository = new RelicRepository(context);

        foreach (var row in RelicProvisionedContent.Rows)
        {
            var found = await repository.GetDefinitionAsync(row.Id);

            Assert.NotNull(found);
            Assert.Equal(row.Id, found!.RelicDefinitionId);
            Assert.Equal(row.Name, found.Name);
            Assert.Equal(row.Trigger, found.Trigger);

            // §8.1's form and its threshold, as the object DATABASE.md §1 item 7
            // stores rather than the superseded prose. A §8.5 row that records
            // `null` reads back as an absent Condition (§8.1 item 4).
            Assert.Equal(row.Condition, found.Condition);

            if (row.Condition is null)
            {
                Assert.Null(found.Condition);
            }
            else
            {
                Assert.Equal(row.Condition.Value.ConditionType, found.Condition!.Value.ConditionType);
                Assert.Equal(row.Condition.Value.Threshold, found.Condition!.Value.Threshold);
            }

            // §8.2's array, element for element and member for member, in stored
            // order.
            Assert.Equal(row.Effects, found.EffectDefinition);
            Assert.Equal(row.Effects.Count, found.EffectDefinition.Count);

            for (var index = 0; index < row.Effects.Count; index++)
            {
                Assert.Equal(row.Effects[index].EffectType, found.EffectDefinition[index].EffectType);
                Assert.Equal(row.Effects[index].ValueType, found.EffectDefinition[index].ValueType);
                Assert.Equal(row.Effects[index].Value, found.EffectDefinition[index].Value);
                Assert.Equal(row.Effects[index].Target, found.EffectDefinition[index].Target);
                Assert.Equal(row.Effects[index].Lifetime, found.EffectDefinition[index].Lifetime);
            }
        }
    }

    [Fact]
    public async Task AppliedRows_ShouldReturnNullForAnIdentityNoRowCarries()
    {
        await using var dataSource = await TryConnectAsync();
        if (dataSource is null) return;

        await using var context = CreatePostgresContext(dataSource);
        var repository = new RelicRepository(context);

        Assert.Null(await repository.GetDefinitionAsync("relic-non-existent"));

        // CORRECTED FOR TASK-184: Burning Curse was the "deferred row is a genuine
        // absence" probe; TASK-176 content-defined it and TASK-184 provisions it, so
        // it now resolves against the applied database (TASK-184 AC-02).
        var burningCurse = await repository.GetDefinitionAsync("relic-burning-curse");

        Assert.NotNull(burningCurse);
        Assert.Equal("OnBattleStart", burningCurse!.Trigger);
        Assert.Null(burningCurse.Condition);
    }

    [Fact]
    public async Task Boundary_ShouldResolveAProvisionedDefinitionEndToEndOverPostgreSql()
    {
        // The full documented chain for this task, with both real compositions in
        // place:
        //
        //   IRelicDefinitionLookup (Application registration)
        //        ↓  ScopedRelicDefinitionLookup (scope per call)
        //   IRelicRepository → GameDbContext → applied RelicDefinition row
        //        ↓
        //   Domain RelicDefinition
        //
        // ValidateScopes makes a captured scoped DbContext fail rather than pass,
        // so this also proves the singleton registration is safe to consume from
        // the root scope. Nothing here resolves a Relic: the assertion stops at
        // the definition's declared content (RELIC_RULES.md §8.7).
        await using var dataSource = await TryConnectAsync();
        if (dataSource is null) return;

        var services = new ServiceCollection();
        services.AddApplicationServices();
        services.AddDbContext<GameDbContext>(options => options.UseNpgsql(dataSource));
        services.AddScoped<IRelicRepository, RelicRepository>();

        await using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true });

        var lookup = provider.GetRequiredService<IRelicDefinitionLookup>();

        var found = await lookup.GetDefinitionAsync("relic-berserker-core");

        var expected = RelicProvisionedContent.Single("relic-berserker-core");

        Assert.NotNull(found);
        Assert.Equal(expected.Id, found!.RelicDefinitionId);
        Assert.Equal(expected.Name, found.Name);
        Assert.Equal(expected.Trigger, found.Trigger);
        Assert.Equal(expected.Condition, found.Condition);
        Assert.Equal(expected.Effects, found.EffectDefinition);
        Assert.Equal(
            RelicConditionType.MatchCountAtLeast,
            found.Condition!.Value.ConditionType);
        Assert.Equal(3, found.Condition!.Value.Threshold);
        Assert.Equal(1, found.EffectDefinition.Count);
        Assert.Equal(RelicEffectType.ATK, found.EffectDefinition[0].EffectType);
        Assert.Equal(RelicEffectValueType.Percentage, found.EffectDefinition[0].ValueType);
        Assert.Equal(5, found.EffectDefinition[0].Value);
        Assert.Equal(RelicEffectTarget.Pet, found.EffectDefinition[0].Target);
        Assert.Equal(RelicEffectLifetime.Battle, found.EffectDefinition[0].Lifetime);

        // The not-found convention holds through the whole composition too.
        Assert.Null(await lookup.GetDefinitionAsync("relic-non-existent"));
    }
}
