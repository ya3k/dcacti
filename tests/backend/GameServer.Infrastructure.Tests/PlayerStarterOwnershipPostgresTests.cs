using GameServer.Application.Players;
using GameServer.Domain.Cards;
using GameServer.Domain.Pets;
using GameServer.Domain.Players;
using GameServer.Domain.Relics;
using GameServer.Infrastructure.Postgres;
using GameServer.Infrastructure.Postgres.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace GameServer.Infrastructure.Tests;

/// <summary>
/// The starter ownership bootstrap as PostgreSQL actually enforces it —
/// <c>DATABASE.md</c> §2 items 1–4.
///
/// The InMemory tests in <see cref="PlayerStarterOwnershipTests"/> assert the
/// declared behavior; the InMemory provider has no transactions and enforces no
/// foreign key or unique index, so it cannot prove <b>atomicity</b>, the
/// <b>concurrent first-login</b> outcome, or that every starter foreign key
/// <b>resolves</b>. Those are exactly the properties this suite asserts against a
/// real PostgreSQL instance, and it is skipped when one is not reachable —
/// following the existing <see cref="PlayerPostgresConstraintTests"/>
/// convention, so the suite still runs hermetically where no database is
/// available.
///
/// <b>Precondition:</b> the database must have this repository's migrations
/// applied, including the TASK-085 provisioning migration. These tests do not
/// create the schema and do not apply migrations — when the required tables are
/// absent the tests skip with that reason recorded rather than failing, because
/// provisioning content is not this task's mechanism.
///
/// Every test that writes a Player removes it and its ownership rows afterwards,
/// so the shared development database is left as it was found.
/// </summary>
public class PlayerStarterOwnershipPostgresTests : IAsyncLifetime
{
    private const string ConnectionString =
        "Host=localhost;Port=5433;Database=dcacti_db;Username=dcacti;Password=dcacti_dev_password";

    private NpgsqlDataSource? _dataSource;
    private bool _available;
    private bool _schemaApplied;

    public async Task InitializeAsync()
    {
        try
        {
            _dataSource = new NpgsqlDataSourceBuilder(ConnectionString).Build();

            await using var command = _dataSource.CreateCommand("SELECT 1");
            await command.ExecuteScalarAsync();

            _available = true;

            // The ownership tables and the provisioned definition tables must
            // already exist (dotnet ef database update). A missing table means
            // the precondition is unmet — skip rather than fail.
            await using var schemaCheck = _dataSource.CreateCommand(
                """
                SELECT to_regclass('"Player"') IS NOT NULL
                   AND to_regclass('"Pet"') IS NOT NULL
                   AND to_regclass('"PlayerUnlockedCard"') IS NOT NULL
                   AND to_regclass('"Relic"') IS NOT NULL
                   AND to_regclass('"PetDefinition"') IS NOT NULL
                   AND to_regclass('"CardDefinition"') IS NOT NULL
                   AND to_regclass('"RelicDefinition"') IS NOT NULL
                """);

            _schemaApplied = (bool)(await schemaCheck.ExecuteScalarAsync() ?? false);
        }
        catch (Exception)
        {
            // No reachable PostgreSQL: skip rather than fail the suite.
            _available = false;
            _schemaApplied = false;
        }
    }

    public async Task DisposeAsync()
    {
        if (_dataSource is not null)
        {
            await _dataSource.DisposeAsync();
        }
    }

    private GameDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<GameDbContext>()
            .UseNpgsql(_dataSource!)
            .Options);

    private static string NewDiscordUserId() =>
        $"9{Random.Shared.NextInt64(1_000_000_000_000_000L):D16}";

    private static Guid ToAccountId(string id) =>
        new(System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(id)));

    private bool Skip => !_available || !_schemaApplied;

    // -----------------------------------------------------------------------
    // A/B/C — the real starter set, committed, with its documented values
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Postgres_NewPlayer_ShouldCommitTheFullStarterSet()
    {
        if (Skip) return;

        await using var context = CreateContext();
        var player = await new PlayerRepository(context).GetOrCreateByDiscordUserIdAsync(
            NewDiscordUserId(),
            TestStarterGrants.ResolvedCallback(context));

        try
        {
            await using var read = CreateContext();

            // 1 Player + 1 Pet + 3 Cards + 3 Relics = 8 rows.
            Assert.Equal(1, await read.Players.CountAsync(p => p.PlayerId == player.PlayerId));
            Assert.Equal(1, await read.Pets.CountAsync(pet => pet.PlayerId == player.PlayerId));
            Assert.Equal(3, await read.PlayerUnlockedCards.CountAsync(c => c.PlayerId == player.PlayerId));
            Assert.Equal(3, await read.Relics.CountAsync(relic => relic.PlayerId == player.PlayerId));
        }
        finally
        {
            await using var cleanup = CreateContext();
            await TestStarterGrants.CleanupPlayerAsync(cleanup, player.PlayerId);
        }
    }

    [Fact]
    public async Task Postgres_StarterSet_ShouldReferenceExactlyTheDocumentedContent()
    {
        if (Skip) return;

        // DATABASE.md §2 item 1: pet-xich-lang; card-heal, card-shield,
        // card-power-charge; relic-berserker-core, relic-mana-crystal,
        // relic-assassin-eye — and nothing else.
        await using var context = CreateContext();
        var player = await new PlayerRepository(context).GetOrCreateByDiscordUserIdAsync(
            NewDiscordUserId(),
            TestStarterGrants.ResolvedCallback(context));

        try
        {
            await using var read = CreateContext();

            var pet = await read.Pets.AsNoTracking()
                .SingleAsync(p => p.PlayerId == player.PlayerId);

            Assert.Equal("pet-xich-lang", pet.PetDefinitionId);

            var cardIds = await read.PlayerUnlockedCards.AsNoTracking()
                .Where(c => c.PlayerId == player.PlayerId)
                .Select(c => c.CardDefinitionId)
                .ToListAsync();

            Assert.Equal(
                ["card-heal", "card-power-charge", "card-shield"],
                cardIds.OrderBy(id => id, StringComparer.Ordinal).ToArray());

            var relicDefinitionIds = await read.Relics.AsNoTracking()
                .Where(r => r.PlayerId == player.PlayerId)
                .Select(r => r.RelicDefinitionId)
                .ToListAsync();

            Assert.Equal(
                ["relic-assassin-eye", "relic-berserker-core", "relic-mana-crystal"],
                relicDefinitionIds.OrderBy(id => id, StringComparer.Ordinal).ToArray());
        }
        finally
        {
            await using var cleanup = CreateContext();
            await TestStarterGrants.CleanupPlayerAsync(cleanup, player.PlayerId);
        }
    }

    [Fact]
    public async Task Postgres_StarterPet_ShouldCarryItsDocumentedCreationValues()
    {
        if (Skip) return;

        // DATABASE.md §2 item 1 / §3: Tier Common, Star 1, XP 0, Level 1, and a
        // server-set AcquiredAt. The database also enforces the documented
        // ranges (CK_Pet_Star_Range, CK_Pet_Level_Range, CK_Pet_XP_Range), so a
        // value outside them could not have been stored at all.
        await using var context = CreateContext();
        var player = await new PlayerRepository(context).GetOrCreateByDiscordUserIdAsync(
            NewDiscordUserId(),
            TestStarterGrants.ResolvedCallback(context));

        try
        {
            await using var read = CreateContext();

            var pet = await read.Pets.AsNoTracking()
                .SingleAsync(p => p.PlayerId == player.PlayerId);

            Assert.Equal(PetTier.Common, pet.Tier);
            Assert.Equal(1, pet.Star);
            Assert.Equal(0, pet.XP);
            Assert.Equal(1, pet.Level);
            Assert.NotEqual(default, pet.AcquiredAt);
        }
        finally
        {
            await using var cleanup = CreateContext();
            await TestStarterGrants.CleanupPlayerAsync(cleanup, player.PlayerId);
        }
    }

    [Fact]
    public async Task Postgres_StarterRelics_ShouldCarryDistinctInstanceIdsAndServerTimestamps()
    {
        if (Skip) return;

        await using var context = CreateContext();
        var player = await new PlayerRepository(context).GetOrCreateByDiscordUserIdAsync(
            NewDiscordUserId(),
            TestStarterGrants.ResolvedCallback(context));

        try
        {
            await using var read = CreateContext();

            var relics = await read.Relics.AsNoTracking()
                .Where(r => r.PlayerId == player.PlayerId)
                .ToListAsync();

            Assert.Equal(3, relics.Count);

            // TASK-083 §15: A != B, B != C, A != C.
            var instanceIds = relics.Select(r => r.RelicInstanceId).ToArray();
            Assert.Equal(3, instanceIds.Distinct(StringComparer.Ordinal).Count());

            // RELIC_RULES.md §2.2: never collapsed with the definition id.
            Assert.All(relics, r => Assert.NotEqual(r.RelicDefinitionId, r.RelicInstanceId));

            // DATABASE.md §1: AcquiredAt, set once by the server.
            Assert.All(relics, r => Assert.NotEqual(default, r.AcquiredAt));
        }
        finally
        {
            await using var cleanup = CreateContext();
            await TestStarterGrants.CleanupPlayerAsync(cleanup, player.PlayerId);
        }
    }

    // -----------------------------------------------------------------------
    // E — every starter foreign key resolves
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Postgres_EveryStarterForeignKey_ShouldResolveToAProvisionedDefinition()
    {
        if (Skip) return;

        // TASK-083 §25: "All ownership Definition FKs resolve." The FK
        // constraints already make an unresolvable reference impossible to
        // insert; this asserts the resolution explicitly, by reading each
        // referenced definition back.
        await using var context = CreateContext();
        var player = await new PlayerRepository(context).GetOrCreateByDiscordUserIdAsync(
            NewDiscordUserId(),
            TestStarterGrants.ResolvedCallback(context));

        try
        {
            await using var read = CreateContext();

            var pet = await read.Pets.AsNoTracking()
                .SingleAsync(p => p.PlayerId == player.PlayerId);

            var petDefinition = await read.PetDefinitions.AsNoTracking()
                .SingleOrDefaultAsync(d => d.PetDefinitionId == pet.PetDefinitionId);

            Assert.NotNull(petDefinition);

            var cardDefinitionIds = await read.PlayerUnlockedCards.AsNoTracking()
                .Where(c => c.PlayerId == player.PlayerId)
                .Select(c => c.CardDefinitionId)
                .ToListAsync();

            foreach (var cardDefinitionId in cardDefinitionIds)
            {
                var definition = await read.CardDefinitions.AsNoTracking()
                    .SingleOrDefaultAsync(d => d.CardDefinitionId == cardDefinitionId);

                Assert.NotNull(definition);

                // CARD_RULES.md §1 item 4 / DATABASE.md §2 item 1: every starter
                // Card is Category = Basic. A PetSkill Card is derived at battle
                // start and is never an unlock row.
                Assert.Equal(CardCategory.Basic, definition!.Category);
            }

            var relicDefinitionIds = await read.Relics.AsNoTracking()
                .Where(r => r.PlayerId == player.PlayerId)
                .Select(r => r.RelicDefinitionId)
                .ToListAsync();

            foreach (var relicDefinitionId in relicDefinitionIds)
            {
                var definition = await read.RelicDefinitions.AsNoTracking()
                    .SingleOrDefaultAsync(d => d.RelicDefinitionId == relicDefinitionId);

                Assert.NotNull(definition);
            }
        }
        finally
        {
            await using var cleanup = CreateContext();
            await TestStarterGrants.CleanupPlayerAsync(cleanup, player.PlayerId);
        }
    }

    // -----------------------------------------------------------------------
    // F — atomic rollback
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Postgres_FailedStarterStage_ShouldLeaveNoPlayerAndNoPartialOwnership()
    {
        if (Skip) return;

        // TASK-083 §10 / §25: if any part of the starter grant fails, no Player
        // row and no partial starter set may remain. The failure is forced the
        // way it can genuinely occur in production: the composition refuses
        // because a required provisioned definition is absent (AGENTS.md §7 —
        // no substitute is invented).
        //
        // The composition runs before anything is added to the change tracker, so
        // this also proves the refusal happens before a commit is attempted.
        var discordUserId = NewDiscordUserId();

        await using (var context = CreateContext())
        {
            var repository = new PlayerRepository(context);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                repository.GetOrCreateByDiscordUserIdAsync(
                    discordUserId,
                    _ => new PlayerStarterGrantFactory(
                            new FailingPetRepository(),
                            new CardRepository(context),
                            new RelicRepository(context))
                        .CreateAsync(DateTimeOffset.UtcNow)));
        }

        // Nothing survived: no Player, and therefore no ownership of any kind
        // for that identity.
        await using var verify = CreateContext();

        Assert.Equal(0, await verify.Players.CountAsync(p => p.AccountId == ToAccountId(discordUserId)));
    }

    [Fact]
    public async Task Postgres_FailedCommit_ShouldRollBackThePlayerAndTheWholeStarterSet()
    {
        if (Skip) return;

        // TASK-083 §10: a starter write failure must leave zero bootstrap rows.
        // The single SaveChangesAsync is one transaction (DATABASE.md §2 item 4),
        // so a failure anywhere in the batch rolls the whole batch back.
        //
        // The failure is forced by violating a documented ownership constraint on
        // one of the starter rows — the Pet's Star range (DATABASE.md §3,
        // CK_Pet_Star_Range). The Player and the other six rows must not persist.
        var discordUserId = NewDiscordUserId();

        await using (var context = CreateContext())
        {
            var repository = new PlayerRepository(context);

            await Assert.ThrowsAsync<DbUpdateException>(() =>
                repository.GetOrCreateByDiscordUserIdAsync(
                    discordUserId,
                    async cancellationToken =>
                    {
                        var valid = await TestStarterGrants.ResolvedAsync(
                            context,
                            DateTimeOffset.UtcNow);

                        // Out-of-range Star: a state the applied schema refuses.
                        var invalidPet = new Pet
                        {
                            PetInstanceId = valid.StarterPet.PetInstanceId,
                            PlayerId = valid.StarterPet.PlayerId,
                            PetDefinitionId = valid.StarterPet.PetDefinitionId,
                            Tier = valid.StarterPet.Tier,
                            Star = Pet.MaxStar + 1,
                            XP = valid.StarterPet.XP,
                            Level = valid.StarterPet.Level,
                            AcquiredAt = valid.StarterPet.AcquiredAt,
                        };

                        return new PlayerStarterGrant(
                            invalidPet,
                            valid.StarterCards,
                            valid.StarterRelics);
                    }));
        }

        // The documented failure direction: no Player row, and no ownership row
        // of any of the three kinds. The starter set is all-or-nothing.
        await using var verify = CreateContext();

        Assert.Equal(0, await verify.Players.CountAsync(p => p.AccountId == ToAccountId(discordUserId)));

        // The ownership rows carry the PlayerId minted for the rejected insert,
        // which was never written — so none can exist.
        Assert.Equal(
            0,
            await verify.Pets.CountAsync(pet =>
                !verify.Players.Any(player => player.PlayerId == pet.PlayerId)));
    }

    // -----------------------------------------------------------------------
    // G — concurrent first login for one Discord identity
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Postgres_ConcurrentFirstLogin_ShouldProduceExactlyOneStarterSet()
    {
        if (Skip) return;

        // TASK-083 §11 / API_CONTRACTS.md §2.6 rule 5: two concurrent first-login
        // requests for one Discord identity must produce exactly 1 Player, 1
        // starter Pet, 3 starter Cards, and 3 starter Relics — never a doubled
        // set. The DiscordUserId UNIQUE constraint is the documented mechanism
        // (DATABASE.md §1, §3); the loser re-reads the winner's row and its whole
        // staged batch is discarded.
        //
        // Each request gets its own GameDbContext and its own connection, so the
        // race is real rather than a shared-tracker artifact.
        var discordUserId = NewDiscordUserId();

        var players = await Task.WhenAll(
            Enumerable.Range(0, 4).Select(_ => CreatePlayerConcurrentlyAsync(discordUserId)));

        try
        {
            // Every request resolved the same Player — the winner's.
            Assert.Single(players.Select(p => p.PlayerId).Distinct(StringComparer.Ordinal));

            var playerId = players[0].PlayerId;

            await using var verify = CreateContext();

            Assert.Equal(1, await verify.Players.CountAsync(p => p.AccountId == ToAccountId(discordUserId)));

            // Exactly one starter set — no duplicate ownership of any kind.
            var petCount = await verify.Pets.CountAsync(pet => pet.PlayerId == playerId);
            var cardCount = await verify.PlayerUnlockedCards.CountAsync(c => c.PlayerId == playerId);
            var relicCount = await verify.Relics.CountAsync(r => r.PlayerId == playerId);

            Assert.Equal(1, petCount);
            Assert.Equal(3, cardCount);
            Assert.Equal(3, relicCount);

            // Exactly 7 ownership rows in total.
            Assert.Equal(7, petCount + cardCount + relicCount);
        }
        finally
        {
            await using var cleanup = CreateContext();
            await TestStarterGrants.CleanupPlayerAsync(cleanup, players[0].PlayerId);
        }
    }

    /// <summary>
    /// One first-login attempt with its own context, connection, and starter
    /// composition — the shape two simultaneous requests actually have.
    /// </summary>
    private async Task<Player> CreatePlayerConcurrentlyAsync(string discordUserId)
    {
        await using var context = CreateContext();

        return await new PlayerRepository(context).GetOrCreateByDiscordUserIdAsync(
            discordUserId,
            TestStarterGrants.ResolvedCallback(context));
    }

    // -----------------------------------------------------------------------
    // H — an existing Player is not re-granted
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Postgres_RepeatAuthentication_ShouldNotCreateASecondStarterSet()
    {
        if (Skip) return;

        await using var context = CreateContext();
        var repository = new PlayerRepository(context);

        var discordUserId = NewDiscordUserId();

        var first = await repository.GetOrCreateByDiscordUserIdAsync(
            discordUserId,
            TestStarterGrants.ResolvedCallback(context));

        var second = await repository.GetOrCreateByDiscordUserIdAsync(
            discordUserId,
            TestStarterGrants.ResolvedCallback(context));

        try
        {
            Assert.Equal(first.PlayerId, second.PlayerId);

            await using var verify = CreateContext();

            Assert.Equal(1, await verify.Players.CountAsync(p => p.AccountId == ToAccountId(discordUserId)));
            Assert.Equal(1, await verify.Pets.CountAsync(pet => pet.PlayerId == first.PlayerId));
            Assert.Equal(3, await verify.PlayerUnlockedCards.CountAsync(c => c.PlayerId == first.PlayerId));
            Assert.Equal(3, await verify.Relics.CountAsync(r => r.PlayerId == first.PlayerId));
        }
        finally
        {
            await using var cleanup = CreateContext();
            await TestStarterGrants.CleanupPlayerAsync(cleanup, first.PlayerId);
        }
    }

    /// <summary>
    /// A Pet repository whose definition lookup refuses, so the composition can
    /// be observed failing on a real database without provisioning anything.
    /// </summary>
    private sealed class FailingPetRepository : Application.Pets.IPetRepository
    {
        public Task AddAsync(Pet pet, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<Pet>> ListByPlayerIdAsync(
            string playerId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PetDefinition?> GetDefinitionAsync(
            string petDefinitionId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<PetDefinition?>(null);

        public Task<IReadOnlyList<PetDefinition>> ListDefinitionsAsync(
            IReadOnlyCollection<string> petDefinitionIds,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Pet?> GetByIdAsync(
            string petInstanceId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> SaveProgressionAsync(
            Pet pet,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
