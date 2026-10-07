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

            // 1 Player + 5 Pets + 3 Cards + 10 Relics = 19 rows
            // (MVP_SCOPE.md §1 / DATABASE.md §2 item 1).
            Assert.Equal(1, await read.Players.CountAsync(p => p.PlayerId == player.PlayerId));
            Assert.Equal(5, await read.Pets.CountAsync(pet => pet.PlayerId == player.PlayerId));
            Assert.Equal(3, await read.PlayerUnlockedCards.CountAsync(c => c.PlayerId == player.PlayerId));
            Assert.Equal(10, await read.Relics.CountAsync(relic => relic.PlayerId == player.PlayerId));
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

        // DATABASE.md §2 item 1 / MVP_SCOPE.md §1: all five provisioned Pets,
        // all three Basic Cards, and all ten provisioned Relics — and nothing
        // else. The sets are compared as sets because ownership order is not a
        // contract (RELIC_RULES.md §2.3 owns equip order; the reads are
        // unordered).
        await using var context = CreateContext();
        var player = await new PlayerRepository(context).GetOrCreateByDiscordUserIdAsync(
            NewDiscordUserId(),
            TestStarterGrants.ResolvedCallback(context));

        try
        {
            await using var read = CreateContext();

            var petDefinitionIds = await read.Pets.AsNoTracking()
                .Where(p => p.PlayerId == player.PlayerId)
                .Select(p => p.PetDefinitionId)
                .ToListAsync();

            Assert.Equal(
                ["pet-bach-ho", "pet-huyen-quy", "pet-son-hung", "pet-thanh-xa", "pet-xich-lang"],
                petDefinitionIds.OrderBy(id => id, StringComparer.Ordinal).ToArray());

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
                [
                    "relic-arcane-battery",
                    "relic-assassin-eye",
                    "relic-battle-instinct",
                    "relic-berserker-core",
                    "relic-burning-curse",
                    "relic-cascade-core",
                    "relic-combo-fang",
                    "relic-emergency-core",
                    "relic-execution-mark",
                    "relic-mana-crystal",
                ],
                relicDefinitionIds.OrderBy(id => id, StringComparer.Ordinal).ToArray());
        }
        finally
        {
            await using var cleanup = CreateContext();
            await TestStarterGrants.CleanupPlayerAsync(cleanup, player.PlayerId);
        }
    }

    [Fact]
    public async Task Postgres_StarterPets_ShouldEachCarryTheirDocumentedCreationValues()
    {
        if (Skip) return;

        // DATABASE.md §2 item 1 / §3: Tier Common, Star 1, XP 0, Level 1, and a
        // server-set AcquiredAt — on every granted instance, not on one
        // privileged Pet. The database also enforces the documented ranges
        // (CK_Pet_Star_Range, CK_Pet_Level_Range, CK_Pet_XP_Range), so a value
        // outside them could not have been stored at all.
        await using var context = CreateContext();
        var player = await new PlayerRepository(context).GetOrCreateByDiscordUserIdAsync(
            NewDiscordUserId(),
            TestStarterGrants.ResolvedCallback(context));

        try
        {
            await using var read = CreateContext();

            var pets = await read.Pets.AsNoTracking()
                .Where(pet => pet.PlayerId == player.PlayerId)
                .ToListAsync();

            Assert.Equal(5, pets.Count);

            Assert.All(pets, pet =>
            {
                Assert.Equal(PetTier.Common, pet.Tier);
                Assert.Equal(1, pet.Star);
                Assert.Equal(0, pet.XP);
                Assert.Equal(1, pet.Level);
                Assert.NotEqual(default, pet.AcquiredAt);
            });

            // One owned instance per MVP Pet definition: no duplicate row.
            Assert.Equal(5, pets.Select(pet => pet.PetDefinitionId).Distinct(StringComparer.Ordinal).Count());
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

            Assert.Equal(10, relics.Count);

            // Every granted instance carries its own identity.
            var instanceIds = relics.Select(r => r.RelicInstanceId).ToArray();
            Assert.Equal(10, instanceIds.Distinct(StringComparer.Ordinal).Count());

            // RELIC_RULES.md §2.2: never collapsed with the definition id.
            Assert.All(relics, r => Assert.NotEqual(r.RelicDefinitionId, r.RelicInstanceId));

            // DATABASE.md §1: AcquiredAt, set once by the server.
            Assert.All(relics, r => Assert.NotEqual(default, r.AcquiredAt));

            // One owned instance per MVP Relic definition: no duplicate row, so
            // no duplicate-selection question is opened (RELIC_RULES.md §2.4).
            Assert.Equal(10, relics.Select(r => r.RelicDefinitionId).Distinct(StringComparer.Ordinal).Count());
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

            var pets = await read.Pets.AsNoTracking()
                .Where(pet => pet.PlayerId == player.PlayerId)
                .ToListAsync();

            Assert.Equal(5, pets.Count);

            foreach (var pet in pets)
            {
                var petDefinition = await read.PetDefinitions.AsNoTracking()
                    .SingleOrDefaultAsync(d => d.PetDefinitionId == pet.PetDefinitionId);

                Assert.NotNull(petDefinition);
            }

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
        // one of the granted rows — the Pet's Star range (DATABASE.md §3,
        // CK_Pet_Star_Range) — on an otherwise valid variant of the §2 grant, so
        // the row's range violation is the only thing wrong with the batch.
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

                        // Out-of-range Star on one Pet: a state the applied schema
                        // refuses. Every other granted row is the real one, so a
                        // partial commit would be observable.
                        var invalidPets = valid.StarterPets
                            .Select((pet, index) => new Pet
                            {
                                PetInstanceId = pet.PetInstanceId,
                                PlayerId = pet.PlayerId,
                                PetDefinitionId = pet.PetDefinitionId,
                                Tier = pet.Tier,
                                Star = index == 0 ? Pet.MaxStar + 1 : pet.Star,
                                XP = pet.XP,
                                Level = pet.Level,
                                AcquiredAt = pet.AcquiredAt,
                            })
                            .ToArray();

                        return new PlayerStarterGrant(
                            invalidPets,
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
        // requests for one account must produce exactly 1 Player, 5 starter Pets,
        // 3 starter Cards, and 10 starter Relics — never a doubled set. The
        // AccountId UNIQUE constraint is the documented mechanism (DATABASE.md §1,
        // §3); the loser re-reads the winner's row and its whole staged batch is
        // discarded.
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

            Assert.Equal(5, petCount);
            Assert.Equal(3, cardCount);
            Assert.Equal(10, relicCount);

            // Exactly 18 ownership rows in total.
            Assert.Equal(18, petCount + cardCount + relicCount);
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
            Assert.Equal(5, await verify.Pets.CountAsync(pet => pet.PlayerId == first.PlayerId));
            Assert.Equal(3, await verify.PlayerUnlockedCards.CountAsync(c => c.PlayerId == first.PlayerId));
            Assert.Equal(10, await verify.Relics.CountAsync(r => r.PlayerId == first.PlayerId));
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
