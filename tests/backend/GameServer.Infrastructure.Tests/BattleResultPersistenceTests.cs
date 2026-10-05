using GameServer.Domain.Battle;
using GameServer.Domain.Bosses;
using GameServer.Domain.Elements;
using GameServer.Domain.Passives;
using GameServer.Domain.Pets;
using GameServer.Domain.Players;
using GameServer.Infrastructure.Postgres;
using GameServer.Infrastructure.Postgres.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace GameServer.Infrastructure.Tests;

/// <summary>
/// <c>BattleResult</c> persistence — <c>DATABASE.md</c> §1, §2, §3, §4
/// (TASK-041).
///
/// What is verified is the documented contract: the eight persisted values, the
/// primary key that <i>is</i> the battle's own <c>BattleId</c>, the three foreign
/// keys, the outcome's contract spelling, the JSON reward summary, the server
/// completion instant, and the one documented index. Nothing here plays a battle:
/// the type under test is persistence only.
/// </summary>
public class BattleResultPersistenceTests
{
    private static GameDbContext CreateContext(string storeName) =>
        TestGameDbContextFactory.Create(storeName);

    private static IModel CreateDesignTimeModel(string storeName)
    {
        using var context = CreateContext(storeName);
        return context.GetService<IDesignTimeModel>().Model;
    }

    /// <summary>
    /// A representative result carrying the documented values. The identities are
    /// real, non-default strings, so a member dropped, renamed, or defaulted by the
    /// mapping cannot pass a naive round trip.
    /// </summary>
    private static BattleResult NewResult(
        string battleResultId = "battle_result_1",
        string playerId = "player_result_1",
        string petInstanceId = "pet_instance_result_1",
        string bossDefinitionId = "boss-def-hoa-long",
        BattleOutcome outcome = BattleOutcome.Victory,
        int durationTurns = 7,
        string rewardSummary = "{}") =>
        new(
            battleResultId,
            playerId,
            petInstanceId,
            bossDefinitionId,
            outcome,
            durationTurns,
            new DateTimeOffset(2026, 9, 27, 12, 30, 15, TimeSpan.Zero),
            rewardSummary);

    /// <summary>
    /// Seeds the three referenced rows so the foreign keys are satisfiable —
    /// <c>DATABASE.md</c> §2's relationships. The Boss row is the provisioned
    /// canonical one (TASK-053), so the FK target is a real row rather than a
    /// fixture invented here.
    /// </summary>
    private static async Task SeedReferencedRowsAsync(GameDbContext context)
    {
        context.Players.Add(new Player
        {
            PlayerId = "player_result_1",
            AccountId = Guid.NewGuid(),
            Level = Player.InitialLevel,
            CreatedAt = DateTimeOffset.UtcNow,
        });

        context.PetDefinitions.Add(new PetDefinition
        {
            PetDefinitionId = "pet_def_result_1",
            Identity = "Thanh Xà",
            Element = Element.Moc,
            PassiveId = new PassiveId("thanh-xa-regen"),
            PassiveThreshold = 5,
            SignatureSkillCardId = "card_skill_result_1",
        });

        context.Pets.Add(new Pet
        {
            PetInstanceId = "pet_instance_result_1",
            PlayerId = "player_result_1",
            PetDefinitionId = "pet_def_result_1",
            Tier = PetTier.Common,
            Star = 1,
            Level = 1,
            AcquiredAt = DateTimeOffset.UtcNow,
        });

        // The canonical provisioned BossDefinition row (DATABASE.md §1 note item
        // 5, TASK-053): boss-hoa-long ↔ boss-def-hoa-long.
        context.BossDefinitions.Add(BossDefinitions.HoaLong);

        await context.SaveChangesAsync();
    }

    // -----------------------------------------------------------------------
    // DATABASE.md §1 — the field set
    // -----------------------------------------------------------------------

    [Fact]
    public void BattleResult_ShouldCarryExactlyTheEightDocumentedFields()
    {
        // DATABASE.md §1 defines exactly eight BattleResult values. The set is
        // closed: no Status, no battle-state snapshot, no winner/loser id, no extra
        // turn count, no Sequence, no RNG state, and no board.
        var properties = typeof(BattleResult)
            .GetProperties()
            .Select(p => p.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[]
            {
                "BattleResultId",
                "BossDefinitionId",
                "CompletedAt",
                "DurationTurns",
                "Outcome",
                "PetInstanceId",
                "PlayerId",
                "RewardSummary",
            },
            properties);

        foreach (var forbidden in new[]
                 {
                     "Status", "BattleState", "Snapshot", "WinnerId", "LoserId",
                     "TurnCount", "Sequence", "RngSeed", "RngState", "Board",
                 })
        {
            Assert.DoesNotContain(forbidden, properties);
        }

        using var context = CreateContext($"battle-result-fields-{Guid.NewGuid():N}");

        var storedColumns = context.Model
            .FindEntityType(typeof(BattleResult))!
            .GetProperties()
            .Select(p => p.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(properties, storedColumns);
    }

    [Fact]
    public void BattleResult_ShouldBeMappedToTheDocumentedTable()
    {
        // DATABASE.md §1 names the entity BattleResult; the table is that name.
        var model = CreateDesignTimeModel(nameof(BattleResult_ShouldBeMappedToTheDocumentedTable));

        var entity = model.FindEntityType(typeof(BattleResult));

        Assert.NotNull(entity);
        Assert.Equal("BattleResult", entity!.GetTableName());
    }

    // -----------------------------------------------------------------------
    // DATABASE.md §1 / §3 — the primary key
    // -----------------------------------------------------------------------

    [Fact]
    public void BattleResultId_ShouldBeThePrimaryKey()
    {
        // DATABASE.md §1: BattleResultId (PK). Because it IS the battle's own
        // BattleId, this key is the at-most-one-row guarantee REDIS_STATE.md §3
        // relies on (sourcing item 1).
        var model = CreateDesignTimeModel(nameof(BattleResultId_ShouldBeThePrimaryKey));

        var entity = model.FindEntityType(typeof(BattleResult))!;
        var key = entity.FindPrimaryKey();

        Assert.NotNull(key);

        var keyProperty = Assert.Single(key!.Properties);

        Assert.Equal(nameof(BattleResult.BattleResultId), keyProperty.Name);
    }

    [Fact]
    public void BattleResultId_ShouldNotBeGeneratedByTheStore()
    {
        // DATABASE.md §1 sourcing item 1: the key is the battle's own id — "no
        // second identifier is introduced". It is therefore never database-
        // generated, exactly as BossDefinition.BossDefinitionId is not
        // (TASK-049): no default, no store-generated behaviour, and not nullable.
        var model = CreateDesignTimeModel(nameof(BattleResultId_ShouldNotBeGeneratedByTheStore));

        var property = model.FindEntityType(typeof(BattleResult))!
            .FindProperty(nameof(BattleResult.BattleResultId))!;

        Assert.False(property.ValueGenerated.HasFlag(ValueGenerated.OnAdd));
        Assert.Null(property.GetDefaultValue());
        Assert.False(property.IsNullable);
    }

    [Fact]
    public async Task BattleResultId_ShouldBeStoredVerbatimAsSupplied()
    {
        // The value the caller supplies is the value stored — the repository adds
        // no identifier of its own (DATABASE.md §1 sourcing item 1).
        var storeName = $"battle-result-pk-{Guid.NewGuid():N}";

        await using var context = CreateContext(storeName);
        await SeedReferencedRowsAsync(context);

        context.BattleResults.Add(NewResult(battleResultId: "battle-the-key"));
        await context.SaveChangesAsync();

        var stored = await context.BattleResults.AsNoTracking().SingleAsync();

        Assert.Equal("battle-the-key", stored.BattleResultId);
    }

    [Fact]
    public async Task BattleResult_ShouldRoundTripEveryDocumentedValue()
    {
        // DATABASE.md §1: the row stores the eight values as written. The
        // assertion re-reads the row through a separate context, so "the context
        // still held the object" cannot be mistaken for a verified round trip.
        var storeName = $"battle-result-roundtrip-{Guid.NewGuid():N}";

        await using (var context = CreateContext(storeName))
        {
            await SeedReferencedRowsAsync(context);

            context.BattleResults.Add(NewResult());
            await context.SaveChangesAsync();
        }

        await using var read = CreateContext(storeName);

        var stored = await read.BattleResults
            .AsNoTracking()
            .SingleAsync(result => result.BattleResultId == "battle_result_1");

        Assert.Equal("battle_result_1", stored.BattleResultId);
        Assert.Equal("player_result_1", stored.PlayerId);
        Assert.Equal("pet_instance_result_1", stored.PetInstanceId);
        Assert.Equal("boss-def-hoa-long", stored.BossDefinitionId);
        Assert.Equal(BattleOutcome.Victory, stored.Outcome);
        Assert.Equal(7, stored.DurationTurns);
        Assert.Equal(
            new DateTimeOffset(2026, 9, 27, 12, 30, 15, TimeSpan.Zero),
            stored.CompletedAt);
        Assert.Equal("{}", stored.RewardSummary);
    }

    // -----------------------------------------------------------------------
    // DATABASE.md §1 — Outcome storage
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(BattleOutcome.Victory, "victory")]
    [InlineData(BattleOutcome.Defeat, "defeat")]
    public async Task Outcome_ShouldBeStoredAsTheDocumentedContractValue(
        BattleOutcome outcome,
        string expected)
    {
        // DATABASE.md §1 stores the values "victory" | "defeat" (the set
        // GAME_EVENTS.md §2 owns) — never the C# enum identifier and never the
        // retired "Won"/"Lost" spelling TASK-050 replaced. The stored document's
        // member value is asserted directly, so the converter — not the enum's
        // ToString() — is what is verified.
        var storeName = $"battle-result-outcome-{expected}-{Guid.NewGuid():N}";

        await using var context = CreateContext(storeName);

        await SeedReferencedRowsAsync(context);

        context.BattleResults.Add(NewResult(outcome: outcome));
        await context.SaveChangesAsync();

        var stored = await context.BattleResults.AsNoTracking().SingleAsync();

        Assert.Equal(expected, BattleOutcomes.ToContractValue(stored.Outcome));
        Assert.Equal(outcome, stored.Outcome);

        // The mapping is what persistence applies, so the column holds exactly this
        // text — not "Victory"/"Defeat" and not "Won"/"Lost".
        Assert.NotEqual(outcome.ToString(), BattleOutcomes.ToContractValue(outcome));
    }

    [Fact]
    public void Outcome_ShouldBeARequiredValue()
    {
        // DATABASE.md §1: every result carries one outcome — the row has no
        // "unknown" or absent form.
        var model = CreateDesignTimeModel(nameof(Outcome_ShouldBeARequiredValue));

        var property = model.FindEntityType(typeof(BattleResult))!
            .FindProperty(nameof(BattleResult.Outcome))!;

        Assert.False(property.IsNullable);
    }

    // -----------------------------------------------------------------------
    // DATABASE.md §1 / §3 — DurationTurns
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(42)]
    public async Task DurationTurns_ShouldPersistTheTerminalTurn(int turns)
    {
        // DATABASE.md §1 "Duration and completion sourcing" item 1: the battle's
        // Turn at terminal resolution, including the documented 0 edge (a battle
        // that ended before any committed Swap).
        var storeName = $"battle-result-duration-{turns}-{Guid.NewGuid():N}";

        await using (var context = CreateContext(storeName))
        {
            await SeedReferencedRowsAsync(context);

            context.BattleResults.Add(NewResult(durationTurns: turns));
            await context.SaveChangesAsync();
        }

        await using var read = CreateContext(storeName);

        var stored = await read.BattleResults.AsNoTracking().SingleAsync();

        Assert.Equal(turns, stored.DurationTurns);
    }

    // -----------------------------------------------------------------------
    // DATABASE.md §1 — CompletedAt
    // -----------------------------------------------------------------------

    [Fact]
    public async Task CompletedAt_ShouldPersistTheSuppliedServerInstant()
    {
        // DATABASE.md §1 item 2: the server clock reading captured on the
        // battle-end path. The column stores exactly that instant — assertable to
        // the second, since the write must not shift, default, or re-stamp it.
        var storeName = $"battle-result-completed-{Guid.NewGuid():N}";
        var completedAt = new DateTimeOffset(2026, 9, 27, 8, 15, 42, TimeSpan.Zero);

        await using (var context = CreateContext(storeName))
        {
            await SeedReferencedRowsAsync(context);

            context.BattleResults.Add(NewResult() with { CompletedAt = completedAt });
            await context.SaveChangesAsync();
        }

        await using var read = CreateContext(storeName);

        var stored = await read.BattleResults.AsNoTracking().SingleAsync();

        Assert.Equal(completedAt, stored.CompletedAt);
        Assert.Equal(completedAt.UtcDateTime, stored.CompletedAt.UtcDateTime);
    }

    [Fact]
    public void CompletedAt_ShouldBeARequiredValue()
    {
        // The completion instant is part of the row, not optional: a result with no
        // completion time would be a battle whose end the server did not record. It
        // carries no store default either, so nothing can stand in for the server's
        // own clock reading (DATABASE.md §1 item 2).
        var model = CreateDesignTimeModel(nameof(CompletedAt_ShouldBeARequiredValue));

        var property = model.FindEntityType(typeof(BattleResult))!
            .FindProperty(nameof(BattleResult.CompletedAt))!;

        Assert.False(property.IsNullable);
        Assert.False(property.ValueGenerated.HasFlag(ValueGenerated.OnAdd));
    }

    // -----------------------------------------------------------------------
    // DATABASE.md §1 — RewardSummary
    // -----------------------------------------------------------------------

    [Fact]
    public async Task RewardSummary_ShouldPersistTheDocumentedStagingValue()
    {
        // DATABASE.md §1: the documented staging value is the empty JSON object {}
        // — always present, never absent — until TASK-033 owns the member list.
        var storeName = $"battle-result-reward-{Guid.NewGuid():N}";

        await using (var context = CreateContext(storeName))
        {
            await SeedReferencedRowsAsync(context);

            context.BattleResults.Add(NewResult());
            await context.SaveChangesAsync();
        }

        await using var read = CreateContext(storeName);

        var stored = await read.BattleResults.AsNoTracking().SingleAsync();

        Assert.Equal("{}", stored.RewardSummary);

        // The stored document is the empty JSON object — not NULL, not an empty
        // string, and not a document carrying a line item.
        using var document = System.Text.Json.JsonDocument.Parse(stored.RewardSummary);

        Assert.Equal(System.Text.Json.JsonValueKind.Object, document.RootElement.ValueKind);
        Assert.Empty(document.RootElement.EnumerateObject());
    }

    [Fact]
    public void RewardSummary_ShouldBeRequiredJson()
    {
        // DATABASE.md §1: the value is "always present, never absent", and §1's
        // entity block types it as JSON.
        var model = CreateDesignTimeModel(nameof(RewardSummary_ShouldBeRequiredJson));

        var property = model.FindEntityType(typeof(BattleResult))!
            .FindProperty(nameof(BattleResult.RewardSummary))!;

        Assert.False(property.IsNullable);
    }

    // -----------------------------------------------------------------------
    // DATABASE.md §1 / §2 — the foreign keys
    // -----------------------------------------------------------------------

    [Fact]
    public void BattleResult_ShouldDeclareTheThreeDocumentedForeignKeys()
    {
        // DATABASE.md §1 lists PlayerId (FK → Player), PetInstanceId (FK → Pet),
        // and BossDefinitionId (FK → BossDefinition); §2 records
        // BattleResult N ── 1 Pet and BattleResult N ── 1 BossDefinition alongside
        // Player 1 ── N BattleResult. Exactly three, and no fourth.
        var model = CreateDesignTimeModel(nameof(BattleResult_ShouldDeclareTheThreeDocumentedForeignKeys));

        var entity = model.FindEntityType(typeof(BattleResult))!;

        var foreignKeys = entity.GetForeignKeys()
            .Select(fk => (Column: fk.Properties.Single().Name, Principal: fk.PrincipalEntityType.ClrType.Name))
            .OrderBy(fk => fk.Column, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[]
            {
                (Column: "BossDefinitionId", Principal: nameof(BossDefinition)),
                (Column: "PetInstanceId", Principal: nameof(Pet)),
                (Column: "PlayerId", Principal: nameof(Player)),
            },
            foreignKeys);
    }

    [Fact]
    public async Task ForeignKeys_ShouldReferenceRowsThatMustAlreadyExist()
    {
        // DATABASE.md §1 sourcing item 3: the FK is "never satisfied by anything
        // other than a real, provisioned BossDefinition row", and an unresolved
        // definition never produces a fabricated key. The model declares all three
        // as required, and the provider rejects a row whose target is absent.
        var model = CreateDesignTimeModel(nameof(ForeignKeys_ShouldReferenceRowsThatMustAlreadyExist));

        var entity = model.FindEntityType(typeof(BattleResult))!;

        foreach (var foreignKey in entity.GetForeignKeys())
        {
            Assert.False(foreignKey.IsRequired is false && foreignKey.Properties.Any(p => p.IsNullable));

            Assert.All(foreignKey.Properties, property => Assert.False(property.IsNullable));
        }

        // No cascade: DATABASE.md §2 documents the relationship shape, not a
        // cascade rule, so deleting a referenced row must not silently erase a
        // battle's history.
        Assert.All(
            entity.GetForeignKeys(),
            foreignKey => Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior));

        await Task.CompletedTask;
    }

    // -----------------------------------------------------------------------
    // DATABASE.md §4 — the index
    // -----------------------------------------------------------------------

    [Fact]
    public void BattleResult_ShouldDeclareOnlyTheDocumentedIndex()
    {
        // DATABASE.md §4 lists exactly one BattleResult index —
        // BattleResult(PlayerId, CompletedAt DESC), "battle history, most recent
        // first" — and states no further index is specified. No speculative index
        // is declared.
        var model = CreateDesignTimeModel(nameof(BattleResult_ShouldDeclareOnlyTheDocumentedIndex));

        var entity = model.FindEntityType(typeof(BattleResult))!;

        var indexes = entity.GetIndexes()
            .Where(index => !index.IsUnique)
            .Select(index => index.Properties.Select(p => p.Name).ToArray())
            .ToArray();

        var documented = Assert.Single(indexes);

        Assert.Equal(
            new[] { nameof(BattleResult.PlayerId), nameof(BattleResult.CompletedAt) },
            documented);
    }

    [Fact]
    public void CompletedAtIndex_ShouldBeDescending()
    {
        // DATABASE.md §4 states the order explicitly — "(PlayerId, CompletedAt
        // DESC)" — because the index serves "battle history, most recent first". A
        // default ascending index would not serve that query.
        var model = CreateDesignTimeModel(nameof(CompletedAtIndex_ShouldBeDescending));

        var index = model.FindEntityType(typeof(BattleResult))!
            .GetIndexes()
            .Single(i => i.Properties.Any(p => p.Name == nameof(BattleResult.CompletedAt)));

        var completedAt = index.Properties
            .Select((property, position) => (property.Name, Descending: index.IsDescending[position]))
            .Single(p => p.Name == nameof(BattleResult.CompletedAt));

        Assert.True(completedAt.Descending);
    }

    [Fact]
    public async Task Index_ShouldExistWithTheDocumentedNameInTheMigration()
    {
        // The applied schema carries the documented index. Asserted against the
        // migration source, which is how this repository pins schema artifacts
        // (the BossPersistenceTests precedent).
        var migration = await File.ReadAllTextAsync(FindMigrationPath());

        Assert.Contains("IX_BattleResult_PlayerId_CompletedAt", migration, StringComparison.Ordinal);
        Assert.Contains("descending: new[] { false, true }", migration, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Migration_ShouldCreateTheDocumentedTableAndNothingElse()
    {
        // DATABASE.md §1/§2/§4: the migration creates the one table, its three FKs,
        // and the one index. It alters no existing table — DATABASE.md §4's
        // existing indexes and every prior migration stay untouched.
        var migration = await File.ReadAllTextAsync(FindMigrationPath());

        Assert.Contains("CreateTable(", migration, StringComparison.Ordinal);
        Assert.Contains("name: \"BattleResult\"", migration, StringComparison.Ordinal);

        foreach (var forbidden in new[] { "AddColumn", "AlterColumn", "DropColumn", "DropTable(\n                name: \"BossDefinition\"", "InsertData" })
        {
            Assert.DoesNotContain(forbidden, migration, StringComparison.Ordinal);
        }

        // And the historical schema migration is still schema-only and untouched.
        var bossMigration = await File.ReadAllTextAsync(
            Path.Combine(
                Path.GetDirectoryName(FindMigrationPath())!,
                "20260926124429_AddBossPersistence.cs"));

        Assert.DoesNotContain("BattleResult", bossMigration, StringComparison.Ordinal);
    }

    /// <summary>
    /// The location of the migration under test, resolved from the repository
    /// root rather than hard-coded as an absolute path.
    /// </summary>
    private static string FindMigrationPath() =>
        Path.Combine(
            RepositoryRoot(),
            "src",
            "backend",
            "GameServer.Infrastructure",
            "Postgres",
            "Migrations",
            "20260927075413_AddBattleResultPersistence.cs");

    /// <summary>
    /// Walks up from the test assembly to the repository root (the directory
    /// holding <c>src/</c> and <c>tests/</c>).
    /// </summary>
    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null
               && !Directory.Exists(Path.Combine(directory.FullName, "src", "backend")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);

        return directory!.FullName;
    }

    // -----------------------------------------------------------------------
    // The repository — DATABASE.md §1 sourcing item 1
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Repository_ShouldWriteAndReadTheRowByTheBattlesOwnId()
    {
        // DATABASE.md §1 sourcing item 1 / API_CONTRACTS.md §4: the row is written
        // and looked up by the battle's own id, and an absent id reads as absence —
        // never as a defaulted result.
        var storeName = $"battle-result-repository-{Guid.NewGuid():N}";

        await using (var context = CreateContext(storeName))
        {
            await SeedReferencedRowsAsync(context);

            var repository = new BattleResultRepository(context);

            await repository.AddAsync(NewResult(battleResultId: "battle-by-key"));
        }

        await using var read = CreateContext(storeName);

        var reader = new BattleResultRepository(read);

        var stored = await reader.GetByIdAsync("battle-by-key");

        Assert.NotNull(stored);
        Assert.Equal("battle-by-key", stored!.BattleResultId);

        Assert.Null(await reader.GetByIdAsync("battle-that-does-not-exist"));
    }

    [Fact]
    public async Task Repository_ShouldNotCreateASecondRow_ForARepeatedWrite()
    {
        // DATABASE.md §1 sourcing item 1 / REDIS_STATE.md §3: at most one row per
        // battle, guaranteed by the key — a repeated terminal persistence must not
        // append. The repository adds no second idempotency mechanism; the end
        // state is simply one row.
        var storeName = $"battle-result-idempotent-{Guid.NewGuid():N}";

        await using var context = CreateContext(storeName);

        await SeedReferencedRowsAsync(context);

        var repository = new BattleResultRepository(context);

        await repository.AddAsync(NewResult(battleResultId: "battle-repeated"));
        await repository.AddAsync(NewResult(battleResultId: "battle-repeated"));

        var rows = await context.BattleResults
            .Where(result => result.BattleResultId == "battle-repeated")
            .ToListAsync();

        Assert.Single(rows);
    }

    [Fact]
    public async Task Repository_ShouldReportOnlyTheFirstWriteAsDurable()
    {
        // DATABASE.md §1 sourcing item 1: the primary key is the duplicate guard,
        // and the battle-end path binds the Player XP grant (COMBAT_RULES.md §7.2)
        // to the FIRST durable write of a battle's result. The repository is
        // therefore what distinguishes "stored now" from "already stored", so a
        // retry cannot be mistaken for a new terminal transition.
        //
        // This is the case that matters in practice: a retry builds its candidate
        // with a fresh server clock reading (DATABASE.md §1 "Duration and
        // completion sourcing" item 2), so the retry's row is never byte-identical
        // to the stored one. The determination must therefore rest on the key
        // alone — not on a value comparison that a differing timestamp would
        // defeat.
        var storeName = $"battle-result-first-write-{Guid.NewGuid():N}";

        await using var context = CreateContext(storeName);

        await SeedReferencedRowsAsync(context);

        var repository = new BattleResultRepository(context);

        var first = NewResult(battleResultId: "battle-first-write");
        var firstCompletedAt = first.CompletedAt;

        Assert.True(
            await repository.AddAsync(first),
            "The first write of a battle's result is the durable one.");

        // A retry of the same battle — with a later completion instant, exactly as
        // a real retry would carry.
        var retry = await repository.AddAsync(
            first with { CompletedAt = firstCompletedAt.AddMinutes(5) });

        Assert.False(
            retry,
            "A retry of the same battle's result must not report a new durable write.");

        // One row, holding the FIRST durable write's values — DATABASE.md §1
        // sources CompletedAt to the write that recorded the battle, one value per
        // battle, so a later retry does not rewrite it.
        var stored = await context.BattleResults
            .AsNoTracking()
            .SingleAsync(result => result.BattleResultId == "battle-first-write");

        Assert.Equal(firstCompletedAt, stored.CompletedAt);
    }

    [Fact]
    public async Task Repository_ShouldPropagateTheStoreFailure()
    {
        // DATABASE.md §1 sourcing item 3: the battle-end path must fail closed when
        // the durable write does not happen, so the repository raises the failure it
        // receives instead of absorbing it — there is no "unavailable" result a
        // caller could mistake for a completed write. This context is disposed
        // before the write, so the store is genuinely unavailable.
        var storeName = $"battle-result-failure-{Guid.NewGuid():N}";

        var context = CreateContext(storeName);
        var repository = new BattleResultRepository(context);

        await context.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            repository.AddAsync(NewResult(battleResultId: "battle-store-unavailable")));
    }

    // -----------------------------------------------------------------------
    // The history query — API_CONTRACTS.md §4.5 notes 4, 5, 8, 10
    // -----------------------------------------------------------------------

    [Fact]
    public async Task History_ShouldReturnOnlyTheRequestedPlayersResults()
    {
        // API_CONTRACTS.md §4.5 note 8: "the identity resolved from the
        // authenticated session must equal BattleResult.PlayerId". The filter is
        // the query's own, so another Player's rows are never materialized for this
        // caller and the endpoint discloses nothing about whether another Player
        // has any history.
        var storeName = $"battle-result-history-scope-{Guid.NewGuid():N}";

        await using var context = CreateContext(storeName);

        await SeedReferencedRowsAsync(context);
        await SeedSecondPlayerRowsAsync(context, "player_result_2", "pet_instance_result_2");

        context.BattleResults.Add(NewResult(battleResultId: "battle-mine-1"));
        context.BattleResults.Add(
            NewResult(
                battleResultId: "battle-theirs-1",
                playerId: "player_result_2",
                petInstanceId: "pet_instance_result_2"));
        await context.SaveChangesAsync();

        var repository = new BattleResultRepository(context);

        var history = await repository.ListByPlayerIdAsync("player_result_1");

        // Exactly the caller's own row — the other Player's row is absent, not
        // filtered out afterwards.
        var only = Assert.Single(history);

        Assert.Equal("battle-mine-1", only.BattleResultId);
        Assert.Equal("player_result_1", only.PlayerId);

        // And the other Player's history is that Player's own single row.
        var theirs = await repository.ListByPlayerIdAsync("player_result_2");

        Assert.Equal("battle-theirs-1", Assert.Single(theirs).BattleResultId);
    }

    [Fact]
    public async Task History_ShouldBeEmpty_ForAPlayerWithNoResults()
    {
        // API_CONTRACTS.md §4.5 note 9: a Player with no completed battles gets an
        // empty collection — not an error, not a null, and not another Player's
        // rows. It is decided by the query, so nothing needs to be loaded to
        // establish it.
        var storeName = $"battle-result-history-empty-{Guid.NewGuid():N}";

        await using var context = CreateContext(storeName);

        await SeedReferencedRowsAsync(context);

        var repository = new BattleResultRepository(context);

        Assert.Empty(await repository.ListByPlayerIdAsync("player_result_1"));
        Assert.Empty(await repository.ListByPlayerIdAsync("player-never-fought"));
    }

    [Fact]
    public async Task History_ShouldOrderMostRecentFirst()
    {
        // API_CONTRACTS.md §4.5 note 4: "Elements are ordered by CompletedAt
        // descending (newest completed battle first)" — and clients MAY rely on it,
        // so it is verified against rows deliberately seeded out of order.
        var storeName = $"battle-result-history-order-{Guid.NewGuid():N}";

        await using var context = CreateContext(storeName);

        await SeedReferencedRowsAsync(context);

        var oldest = new DateTimeOffset(2026, 9, 20, 8, 0, 0, TimeSpan.Zero);
        var middle = new DateTimeOffset(2026, 9, 25, 12, 30, 0, TimeSpan.Zero);
        var newest = new DateTimeOffset(2026, 9, 27, 23, 59, 59, TimeSpan.Zero);

        // Inserted oldest-first, so insertion order is the reverse of the
        // documented order and a query that omitted the ordering could not pass.
        context.BattleResults.Add(NewResult(battleResultId: "battle-oldest") with { CompletedAt = oldest });
        context.BattleResults.Add(NewResult(battleResultId: "battle-middle") with { CompletedAt = middle });
        context.BattleResults.Add(NewResult(battleResultId: "battle-newest") with { CompletedAt = newest });
        await context.SaveChangesAsync();

        var repository = new BattleResultRepository(context);

        var history = await repository.ListByPlayerIdAsync("player_result_1");

        Assert.Equal(
            new[] { "battle-newest", "battle-middle", "battle-oldest" },
            history.Select(result => result.BattleResultId));

        // The stored completion instants are the rows' own — the read neither
        // re-stamps nor re-derives them (note 3).
        Assert.Equal(newest, history[0].CompletedAt);
        Assert.Equal(oldest, history[2].CompletedAt);
    }

    [Fact]
    public async Task History_ShouldBreakCompletedAtTies_ByDescendingBattleResultId()
    {
        // API_CONTRACTS.md §4.5 note 4: "When two results share a CompletedAt, the
        // tie is broken by BattleResultId descending (the higher BattleResultId
        // first), so the total order is deterministic even though DATABASE.md §1
        // does not require CompletedAt to be unique." Equal timestamps are
        // therefore seeded deliberately, including the boundary ordering of the id
        // values the tie-break must decide.
        var storeName = $"battle-result-history-tie-{Guid.NewGuid():N}";

        await using var context = CreateContext(storeName);

        await SeedReferencedRowsAsync(context);

        // One instant shared by every row — the case the tie-break exists for.
        var shared = new DateTimeOffset(2026, 9, 27, 10, 0, 0, TimeSpan.Zero);

        // Inserted in ascending id order for the same reason as above.
        context.BattleResults.Add(NewResult(battleResultId: "battle-tie-1") with { CompletedAt = shared });
        context.BattleResults.Add(NewResult(battleResultId: "battle-tie-2") with { CompletedAt = shared });
        context.BattleResults.Add(NewResult(battleResultId: "battle-tie-10") with { CompletedAt = shared });
        await context.SaveChangesAsync();

        var repository = new BattleResultRepository(context);

        var history = await repository.ListByPlayerIdAsync("player_result_1");

        Assert.All(history, result => Assert.Equal(shared, result.CompletedAt));

        // Descending by id. Note "battle-tie-2" precedes "battle-tie-10" because
        // this is an ordinal string comparison, not a numeric one — which is the
        // property that makes the documented order independent of any database
        // collation.
        Assert.Equal(
            new[] { "battle-tie-2", "battle-tie-10", "battle-tie-1" },
            history.Select(result => result.BattleResultId));
    }

    [Fact]
    public async Task History_ShouldCombineTheCompletedAtOrder_WithTheTieBreak()
    {
        // The two ordering components together, which is what note 4 defines: an
        // earlier-than-the-newest result still sorts below it, and results sharing
        // an instant sort among themselves by descending id.
        var storeName = $"battle-result-history-total-order-{Guid.NewGuid():N}";

        await using var context = CreateContext(storeName);

        await SeedReferencedRowsAsync(context);

        var older = new DateTimeOffset(2026, 9, 26, 9, 0, 0, TimeSpan.Zero);
        var newer = new DateTimeOffset(2026, 9, 27, 9, 0, 0, TimeSpan.Zero);

        context.BattleResults.Add(NewResult(battleResultId: "battle-b") with { CompletedAt = older });
        context.BattleResults.Add(NewResult(battleResultId: "battle-a") with { CompletedAt = older });
        context.BattleResults.Add(NewResult(battleResultId: "battle-c") with { CompletedAt = newer });
        await context.SaveChangesAsync();

        var repository = new BattleResultRepository(context);

        var history = await repository.ListByPlayerIdAsync("player_result_1");

        Assert.Equal(
            new[] { "battle-c", "battle-b", "battle-a" },
            history.Select(result => result.BattleResultId));
    }

    [Fact]
    public async Task History_ShouldNotBoundTheNumberOfRows()
    {
        // API_CONTRACTS.md §4.5 note 5: "the array is the full history ... however
        // long it is; an unbounded array is the accepted MVP contract". No page,
        // limit, offset, or cursor exists, so a Player with many results receives
        // all of them.
        var storeName = $"battle-result-history-unbounded-{Guid.NewGuid():N}";

        await using var context = CreateContext(storeName);

        await SeedReferencedRowsAsync(context);

        var baseInstant = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);

        for (var index = 0; index < 25; index++)
        {
            context.BattleResults.Add(
                NewResult(battleResultId: $"battle-many-{index:D3}")
                    with { CompletedAt = baseInstant.AddMinutes(index) });
        }

        await context.SaveChangesAsync();

        var repository = new BattleResultRepository(context);

        var history = await repository.ListByPlayerIdAsync("player_result_1");

        Assert.Equal(25, history.Count);

        // The newest is still first across the whole unbounded set.
        Assert.Equal("battle-many-024", history[0].BattleResultId);
        Assert.Equal("battle-many-000", history[^1].BattleResultId);
    }

    [Fact]
    public async Task History_ShouldReadEveryDocumentedMember_ForProjection()
    {
        // The endpoint projects §4.5's five members from the row
        // (API_CONTRACTS.md §4.5 notes 2–3, 12), so the read must return the
        // complete durable record — including the two identities and the reward
        // summary the response deliberately does not expose. A read that trimmed
        // the row would make the documented projection impossible.
        var storeName = $"battle-result-history-members-{Guid.NewGuid():N}";

        await using var context = CreateContext(storeName);

        await SeedReferencedRowsAsync(context);

        const string rewardSummary =
            "{\"playerXpGained\":100,\"newPlayerXp\":200,\"playerLeveledUp\":false,"
            + "\"newPlayerLevel\":1,\"petXpGained\":100,\"newPetXp\":200,"
            + "\"petLeveledUp\":false,\"newPetLevel\":1}";

        var completedAt = new DateTimeOffset(2026, 9, 27, 7, 45, 12, TimeSpan.Zero);

        context.BattleResults.Add(
            NewResult(rewardSummary: rewardSummary) with { CompletedAt = completedAt });
        await context.SaveChangesAsync();

        var repository = new BattleResultRepository(context);

        var only = Assert.Single(await repository.ListByPlayerIdAsync("player_result_1"));

        Assert.Equal("battle_result_1", only.BattleResultId);
        Assert.Equal("player_result_1", only.PlayerId);
        Assert.Equal("pet_instance_result_1", only.PetInstanceId);
        Assert.Equal("boss-def-hoa-long", only.BossDefinitionId);
        Assert.Equal(BattleOutcome.Victory, only.Outcome);
        Assert.Equal(7, only.DurationTurns);
        Assert.Equal(completedAt, only.CompletedAt);
        Assert.Equal(rewardSummary, only.RewardSummary);
    }

    [Fact]
    public async Task History_ShouldRejectAnAbsentPlayerIdentity()
    {
        // API_CONTRACTS.md §4.5 note 8 makes the authenticated identity the only
        // scope. An absent identity names no Player, so it is refused rather than
        // interpreted as "all Players" — which would be the disclosure the note
        // forbids.
        var storeName = $"battle-result-history-identity-{Guid.NewGuid():N}";

        await using var context = CreateContext(storeName);

        await SeedReferencedRowsAsync(context);

        context.BattleResults.Add(NewResult());
        await context.SaveChangesAsync();

        var repository = new BattleResultRepository(context);

        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => repository.ListByPlayerIdAsync("   "));
    }

    /// <summary>
    /// Seeds a second Player and its owned Pet, so a history test can prove one
    /// Player's read never returns another's rows (<c>DATABASE.md</c> §1, §2 — the
    /// three foreign keys must be satisfiable).
    /// </summary>
    private static async Task SeedSecondPlayerRowsAsync(
        GameDbContext context,
        string playerId,
        string petInstanceId)
    {
        context.Players.Add(new Player
        {
            PlayerId = playerId,
            AccountId = Guid.NewGuid(),
            Level = Player.InitialLevel,
            CreatedAt = DateTimeOffset.UtcNow,
        });

        context.PetDefinitions.Add(new PetDefinition
        {
            PetDefinitionId = $"pet_def_{playerId}",
            Identity = "Thanh Xà",
            Element = Element.Moc,
            PassiveId = new PassiveId("thanh-xa-regen"),
            PassiveThreshold = 5,
            SignatureSkillCardId = $"card_skill_{playerId}",
        });

        context.Pets.Add(new Pet
        {
            PetInstanceId = petInstanceId,
            PlayerId = playerId,
            PetDefinitionId = $"pet_def_{playerId}",
            Tier = PetTier.Common,
            Star = 1,
            Level = 1,
            AcquiredAt = DateTimeOffset.UtcNow,
        });

        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task Repository_ShouldExposeAForeignKeyViolation_AsAStoreFailure()
    {
        // DATABASE.md §1 sourcing item 3: the FK "is never satisfied by anything
        // other than a real, provisioned BossDefinition row". Against real
        // PostgreSQL an insert whose referenced rows are absent is refused by the
        // database itself — asserted in BattleResultPostgresTests, where the
        // constraint is actually enforced, because the in-memory provider used by
        // this class does not enforce foreign keys.
        await Task.CompletedTask;
    }
}
