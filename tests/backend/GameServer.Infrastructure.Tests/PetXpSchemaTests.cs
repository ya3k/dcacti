using GameServer.Domain.Cards;
using GameServer.Domain.Elements;
using GameServer.Domain.Passives;
using GameServer.Domain.Pets;
using GameServer.Domain.Players;
using GameServer.Infrastructure.Postgres;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace GameServer.Infrastructure.Tests;

/// <summary>
/// The Pet XP schema as the applied migration actually produced it —
/// <c>DATABASE.md</c> §1/§3, <c>PET_RULES.md</c> §5.1/§5.4/§5.5.
///
/// <b>Why this suite exists.</b> The model-level tests in
/// <see cref="PetPersistenceTests"/> assert the <i>declared</i> model, which is
/// what the migration is generated from. These assert the <i>applied schema</i>
/// in a real PostgreSQL database, so the Migrations/ directory is proven to have
/// actually been applied rather than merely written — the documented
/// <c>dotnet ef database update</c> step.
///
/// Following the <see cref="PlayerPostgresConstraintTests"/> convention, it is
/// skipped when no database is reachable.
/// </summary>
public class PetXpSchemaTests : IAsyncLifetime
{
    private const string ConnectionString =
        "Host=localhost;Port=5433;Database=dcacti_db;Username=dcacti;Password=dcacti_dev_password";

    private NpgsqlDataSource? _dataSource;
    private bool _available;

    public async Task InitializeAsync()
    {
        try
        {
            var builder = new NpgsqlDataSourceBuilder(ConnectionString);
            _dataSource = builder.Build();

            await using var command = _dataSource.CreateCommand("SELECT 1");
            await command.ExecuteScalarAsync();

            _available = true;
        }
        catch (Exception)
        {
            _available = false;
        }
    }

    public async Task DisposeAsync()
    {
        if (_dataSource is not null)
        {
            await _dataSource.DisposeAsync();
        }
    }

    [Fact]
    public async Task AppliedSchema_ShouldHaveThePetXpColumn_NonNullable_DefaultingToZero()
    {
        if (!_available) return;

        // DATABASE.md §1/§3: Pet.XP is int, NOT NULL, default 0 (PET_RULES.md
        // §5.2). Asserted against the database's own catalogue, so a migration that
        // was written but never applied fails here.
        await using var command = _dataSource!.CreateCommand(
            """
            SELECT data_type, is_nullable, column_default
            FROM information_schema.columns
            WHERE table_name = 'Pet' AND column_name = 'XP'
            """);

        await using var reader = await command.ExecuteReaderAsync();

        Assert.True(await reader.ReadAsync(), "The applied schema has no Pet.XP column.");

        Assert.Equal("integer", reader.GetString(0));
        Assert.Equal("NO", reader.GetString(1));
        Assert.Contains("0", reader.GetString(2));
    }

    [Fact]
    public async Task AppliedSchema_ShouldCarryTheDocumentedPetXpAndLevelConstraints()
    {
        if (!_available) return;

        // DATABASE.md §3: Pet.XP ∈ [0, 4900] — a HARD cap (PET_RULES.md §5.5 item
        // 1) — and Pet.Level ∈ [1, 50]. Both check constraints are asserted by name
        // and definition, so neither can be dropped or widened silently.
        await using var command = _dataSource!.CreateCommand(
            """
            SELECT conname, pg_get_constraintdef(oid)
            FROM pg_constraint
            WHERE conrelid = '"Pet"'::regclass AND contype = 'c'
            """);

        var constraints = new Dictionary<string, string>(StringComparer.Ordinal);

        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                constraints[reader.GetString(0)] = reader.GetString(1);
            }
        }

        Assert.True(constraints.ContainsKey("CK_Pet_XP_Range"));
        Assert.True(constraints.ContainsKey("CK_Pet_Level_Range"));
        Assert.True(constraints.ContainsKey("CK_Pet_Star_Range"));

        // Both bounds of the XP hard cap, and explicitly the documented 4900.
        Assert.Contains("\"XP\" >= 0", constraints["CK_Pet_XP_Range"]);
        Assert.Contains("\"XP\" <= 4900", constraints["CK_Pet_XP_Range"]);

        Assert.Contains("\"Level\" >= 1", constraints["CK_Pet_Level_Range"]);
        Assert.Contains("\"Level\" <= 50", constraints["CK_Pet_Level_Range"]);
    }

    [Fact]
    public async Task AppliedSchema_ShouldNotApplyThePetCapToThePlayerXpColumn()
    {
        if (!_available) return;

        // PET_RULES.md §5.5 "Deliberate Divergence From the Player Track": the Pet
        // XP cap is the Pet track's alone and "neither track's cap rule may be
        // applied to the other". Player.XP stays uncapped (COMBAT_RULES.md §7.5
        // item 1), so its applied constraint must carry no ceiling.
        await using var command = _dataSource!.CreateCommand(
            """
            SELECT pg_get_constraintdef(oid)
            FROM pg_constraint
            WHERE conrelid = '"Player"'::regclass
              AND conname = 'CK_Player_XP_NonNegative'
            """);

        var definition = (string?)await command.ExecuteScalarAsync();

        Assert.NotNull(definition);
        Assert.Contains("\"XP\" >= 0", definition);
        Assert.DoesNotContain("4900", definition);
    }

    [Fact]
    public async Task AppliedSchema_ShouldHaveReceivedTheAddPetXpMigration()
    {
        if (!_available) return;

        // The documented apply step is `dotnet ef database update`, which records
        // the applied migration in __EFMigrationsHistory. Its presence is what
        // proves the shipped migration — not a hand-built schema — produced the
        // column above. Exactly one such migration exists: the task adds exactly
        // one, and a second would mean a second schema change rode along.
        await using var command = _dataSource!.CreateCommand(
            """
            SELECT COUNT(*) FROM "__EFMigrationsHistory"
            WHERE "MigrationId" LIKE '%_AddPetXp'
            """);

        var applied = (long)(await command.ExecuteScalarAsync())!;

        Assert.Equal(1, applied);
    }

    [Fact]
    public async Task AppliedSchema_ShouldHaveNoRetiredPetLevelMultiplierColumn()
    {
        if (!_available) return;

        // TASK-064 retired PetLevelMultiplier and PET_RULES.md §5.6 item 1 /
        // ADR-016 item 13 keep it retired: it is not an XP curve multiplier, not an
        // XP reward multiplier, and not a Pet Level input. Asserted against the
        // applied schema rather than the source alone, so a stray migration could
        // not quietly restore it.
        await using var command = _dataSource!.CreateCommand(
            """
            SELECT COUNT(*) FROM information_schema.columns
            WHERE table_name = 'PetDefinition' AND column_name = 'PetLevelMultiplier'
            """);

        var present = (long)(await command.ExecuteScalarAsync())!;

        Assert.Equal(0, present);
    }

    [Fact]
    public async Task AppliedSchema_ShouldCarryPetXpOnThePetTableOnly()
    {
        if (!_available) return;

        // PET_RULES.md §5.1 items 1-2 / DATABASE.md §3: Pet XP is a per-INSTANCE
        // column on the owned Pet row — not on PetDefinition and not on Player.
        // The only other XP column in the schema is the Player track's own, which
        // is a separate documented pool (ADR-016).
        await using var command = _dataSource!.CreateCommand(
            """
            SELECT table_name FROM information_schema.columns
            WHERE column_name = 'XP' AND table_name <> 'Player'
            ORDER BY table_name
            """);

        var tables = new List<string>();

        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                tables.Add(reader.GetString(0));
            }
        }

        Assert.Equal(new[] { "Pet" }, tables);
    }

    [Fact]
    public async Task AppliedSchema_ShouldStoreAndReloadXPAtTheDocumentedCap()
    {
        if (!_available) return;

        // The end-to-end round trip through the applied schema: PET_RULES.md §5.5
        // makes 4900 the hard maximum, so it is storable, while a value one past it
        // is refused by the constraint rather than retained.
        var options = new DbContextOptionsBuilder<GameDbContext>()
            .UseNpgsql(_dataSource!)
            .Options;

        var suffix = Guid.NewGuid().ToString("N");
        var playerId = $"player_pet_xp_schema_{suffix}";
        var cardDefinitionId = $"card-skill-xp-schema-{suffix}";
        var definitionId = $"pet-def-xp-schema-{suffix}";
        var petInstanceId = $"pet_xp_schema_{suffix}";

        var accountId = Guid.NewGuid();

        await using (var context = new GameDbContext(options))
        {
            context.Accounts.Add(new Domain.Accounts.Account
            {
                AccountId = accountId,
                Username = $"u_{accountId:N}"[..20],
                PasswordHash = "hash",
                CreatedAt = DateTimeOffset.UtcNow,
            });

            context.Players.Add(new Player
            {
                PlayerId = playerId,
                AccountId = accountId,
                XP = Player.InitialXp,
                Level = Player.InitialLevel,
                CreatedAt = DateTimeOffset.UtcNow,
            });

            // The applied schema enforces PetDefinition.SignatureSkillCardId as a
            // real FK (DATABASE.md §1/§2), so the CardDefinition row is seeded
            // rather than a dangling id being written.
            context.CardDefinitions.Add(new CardDefinition
            {
                CardDefinitionId = cardDefinitionId,
                Name = "Pet XP Schema Signature Skill",
                Category = CardCategory.PetSkill,
                PowerCost = 0,
                EffectDefinition = TestCardEffects.FlatPower,
                LoadoutCopyLimit = 1,
            });

            context.PetDefinitions.Add(new PetDefinition
            {
                PetDefinitionId = definitionId,
                Identity = $"pet-xp-schema-{suffix}",
                Element = Element.Hoa,
                PassiveId = new PassiveId("xich-lang"),
                PassiveThreshold = 5,
                SignatureSkillCardId = cardDefinitionId,
            });

            context.Pets.Add(new Pet
            {
                PetInstanceId = petInstanceId,
                PlayerId = playerId,
                PetDefinitionId = definitionId,
                Tier = PetTier.Common,
                Star = Pet.MinStar,
                XP = Pet.MaxXp,
                Level = Pet.LevelForXp(Pet.MaxXp),
                AcquiredAt = DateTimeOffset.UtcNow,
            });

            await context.SaveChangesAsync();
        }

        try
        {
            await using var reload = new GameDbContext(options);

            var stored = await reload.Pets.AsNoTracking()
                .SingleAsync(pet => pet.PetInstanceId == petInstanceId);

            Assert.Equal(4900, stored.XP);
            Assert.Equal(Pet.MaxLevel, stored.Level);

            // The other side of the same boundary: a value past the hard cap is
            // refused by the applied constraint, so no overflow can be stored.
            var petAboveCap = await reload.Pets
                .SingleAsync(pet => pet.PetInstanceId == petInstanceId);

            petAboveCap.XP = Pet.MaxXp + Pet.BattleWonXpReward;

            await Assert.ThrowsAsync<DbUpdateException>(() => reload.SaveChangesAsync());
        }
        finally
        {
            await using var cleanup = new GameDbContext(options);

            var pet = await cleanup.Pets.SingleOrDefaultAsync(p => p.PetInstanceId == petInstanceId);

            if (pet is not null)
            {
                cleanup.Pets.Remove(pet);
                await cleanup.SaveChangesAsync();
            }

            var definition = await cleanup.PetDefinitions.SingleOrDefaultAsync(d => d.PetDefinitionId == definitionId);

            if (definition is not null)
            {
                cleanup.PetDefinitions.Remove(definition);
                await cleanup.SaveChangesAsync();
            }

            var card = await cleanup.CardDefinitions.SingleOrDefaultAsync(c => c.CardDefinitionId == cardDefinitionId);

            if (card is not null)
            {
                cleanup.CardDefinitions.Remove(card);
                await cleanup.SaveChangesAsync();
            }

            var player = await cleanup.Players.SingleOrDefaultAsync(p => p.PlayerId == playerId);

            if (player is not null)
            {
                cleanup.Players.Remove(player);
            }

            var account = await cleanup.Accounts.SingleOrDefaultAsync(a => a.AccountId == accountId);

            if (account is not null)
            {
                cleanup.Accounts.Remove(account);
            }

            await cleanup.SaveChangesAsync();
        }
    }
}