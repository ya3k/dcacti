using GameServer.Application;
using GameServer.Application.Battle;
using GameServer.Application.Pets;
using GameServer.Application.Players;
using GameServer.Domain.Battle;
using GameServer.Domain.Bosses;
using GameServer.Domain.Cards;
using GameServer.Domain.Elements;
using GameServer.Domain.Passives;
using GameServer.Domain.Pets;
using GameServer.Domain.Players;
using GameServer.Infrastructure.Postgres;
using GameServer.Infrastructure.Postgres.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace GameServer.Infrastructure.Tests;

/// <summary>
/// The battle-end unit of work as PostgreSQL actually enforces it —
/// <c>DATABASE.md</c> §1, <c>ARCHITECTURE.md</c> §4 item 4,
/// <c>REDIS_STATE.md</c> §3.
///
/// <code>
/// Rule (DATABASE.md §1 records one battle end as one durable result plus the
///       documented reward on each track)
///  ↓
/// Scenario (Given the production persistence boundaries over one shared
///           GameDbContext,
///           When a participating progression write fails,
///           Then PostgreSQL rolls the result row and both progression writes
///                back together, and the active state is not cleared)
///  ↓
/// Test
/// </code>
///
/// <b>Why this suite must run against real PostgreSQL.</b> The invariant is a
/// property of a database transaction: the result-row insert, the Player update,
/// and the Pet update have to be issued on the transaction's own connection and
/// undone by its rollback. The in-memory provider used by
/// <c>BattleEndAtomicityTests</c> (Application) cannot prove that — it does not
/// support transactions at all — so it proves the boundary's control flow, and
/// these tests prove the database behaviour those tests assume.
///
/// <b>It uses the production boundaries, not stand-ins for them.</b>
/// <see cref="BattleResultRepository"/>, <see cref="PlayerRepository"/>,
/// <see cref="PetRepository"/>, <see cref="BossDefinitionLookup"/>, and
/// <see cref="BattleEndTransaction"/> are the real Infrastructure
/// implementations, constructed over the same <see cref="GameDbContext"/> the
/// production scope would give them. Only the two failures are injected, and only
/// through the Application boundaries themselves
/// (<see cref="FailingPetProgressionRepository"/> /
/// <see cref="FailingPlayerProgressionRepository"/>), because no production flag
/// exists for failing a persistence write (<c>AGENTS.md</c> §9).
///
/// <b>It follows the PostgreSQL suites' convention</b>
/// (<see cref="BattleResultPostgresTests"/>, <see cref="PlayerPostgresConstraintTests"/>):
/// a hard-coded local connection string, skipped when unreachable, with the rows a
/// test created removed afterwards.
/// </summary>
public class BattleEndAtomicityPostgresTests : IAsyncLifetime
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
            // No reachable PostgreSQL: this suite verifies database transaction
            // behaviour, so it is skipped rather than failed. The boundary's control
            // flow is verified without a database by the Application suite.
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

    /// <summary>
    /// One context, exactly as one scope resolves one context in production. It is
    /// the shared context that makes the three writes one transaction and not
    /// three, so the suites below deliberately do not give the boundaries separate
    /// instances to write through.
    /// </summary>
    private GameDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<GameDbContext>().UseNpgsql(_dataSource!).Options);

    private static string NewId(string prefix) => $"{prefix}_{Guid.NewGuid():N}";

    /// <summary>
    /// A battle the documented factory created, naming the seeded Player and Pet
    /// (<c>GAME_STATE.md</c> §2.8, §2.3) and the canonical <c>BossDefinitions.HoaLong</c>
    /// whose provisioned row the foreign key resolves to (<c>DATABASE.md</c> §1 note
    /// item 2, TASK-053).
    /// </summary>
    private static BattleState NewBattle(string battleId, string playerId, string petInstanceId) =>
        BattleState.Create(
            battleId,
            rngSeed: 20260927UL,
            new PlayerId(playerId),
            new PetId(petInstanceId),
            Element.Hoa,
            new PassiveId("xich-lang"),
            passiveThreshold: 5,
            BossDefinitions.HoaLong);

    /// <summary>
    /// Seeds the rows a battle end references — the owning Player at the documented
    /// initial values and the Pet instance the battle will name
    /// (<c>DATABASE.md</c> §1–§2, <c>COMBAT_RULES.md</c> §7.5 item 3,
    /// <c>PET_RULES.md</c> §5.2) — through a context of its own, so the context the
    /// battle-end step then uses did not create them. That is the production shape:
    /// the terminal step's scope reads rows an earlier request wrote.
    /// </summary>
    private async Task<(string PlayerId, string PetInstanceId)> SeedOwnerAsync()
    {
        await using var context = CreateContext();

        var playerId = NewId("player_atomic_pg");
        var petDefinitionId = NewId("pet_def_atomic_pg");
        var petInstanceId = NewId("pet_instance_atomic_pg");
        var signatureSkillCardId = NewId("card_skill_atomic_pg");

        var accountId = Guid.NewGuid();

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

        // DATABASE.md §2 makes PetDefinition 1 ── 1 CardDefinition a real foreign
        // key, so the Signature Skill Card must exist before the definition that
        // references it.
        context.CardDefinitions.Add(new CardDefinition
        {
            CardDefinitionId = signatureSkillCardId,
            Name = signatureSkillCardId,
            Category = CardCategory.PetSkill,
            PowerCost = 0,
            EffectDefinition = TestCardEffects.FlatPower,
            LoadoutCopyLimit = 1,
        });

        context.PetDefinitions.Add(new PetDefinition
        {
            PetDefinitionId = petDefinitionId,
            Identity = "Thanh Xà",
            Element = Element.Moc,
            PassiveId = new PassiveId("thanh-xa-regen"),
            PassiveThreshold = 5,
            SignatureSkillCardId = signatureSkillCardId,
        });

        context.Pets.Add(new Pet
        {
            PetInstanceId = petInstanceId,
            PlayerId = playerId,
            PetDefinitionId = petDefinitionId,
            Tier = PetTier.Common,
            Star = Pet.MinStar,
            XP = Pet.InitialXp,
            Level = Pet.InitialLevel,
            AcquiredAt = DateTimeOffset.UtcNow,
        });

        await context.SaveChangesAsync();

        return (playerId, petInstanceId);
    }

    /// <summary>
    /// Removes the rows a test created, in foreign-key order, so the shared
    /// development database is left as it was found.
    /// </summary>
    private async Task CleanupAsync(string battleId, string playerId, string petInstanceId)
    {
        await using var context = CreateContext();

        await context.Database.ExecuteSqlRawAsync(
            "DELETE FROM \"BattleResult\" WHERE \"BattleResultId\" = {0}",
            battleId);

        var petDefinitionId = await context.Pets
            .Where(pet => pet.PetInstanceId == petInstanceId)
            .Select(pet => pet.PetDefinitionId)
            .FirstOrDefaultAsync();

        var signatureSkillCardId = petDefinitionId is null
            ? null
            : await context.PetDefinitions
                .Where(definition => definition.PetDefinitionId == petDefinitionId)
                .Select(definition => definition.SignatureSkillCardId)
                .FirstOrDefaultAsync();

        await context.Database.ExecuteSqlRawAsync(
            "DELETE FROM \"Pet\" WHERE \"PetInstanceId\" = {0}",
            petInstanceId);

        if (petDefinitionId is not null)
        {
            await context.Database.ExecuteSqlRawAsync(
                "DELETE FROM \"PetDefinition\" WHERE \"PetDefinitionId\" = {0}",
                petDefinitionId);
        }

        if (signatureSkillCardId is not null)
        {
            await context.Database.ExecuteSqlRawAsync(
                "DELETE FROM \"CardDefinition\" WHERE \"CardDefinitionId\" = {0}",
                signatureSkillCardId);
        }

        var accountId = await context.Players
            .Where(player => player.PlayerId == playerId)
            .Select(player => (Guid?)player.AccountId)
            .FirstOrDefaultAsync();

        await context.Database.ExecuteSqlRawAsync(
            "DELETE FROM \"Player\" WHERE \"PlayerId\" = {0}",
            playerId);

        if (accountId is not null)
        {
            await context.Database.ExecuteSqlRawAsync(
                "DELETE FROM \"Account\" WHERE \"AccountId\" = {0}",
                accountId.Value);
        }
    }

    /// <summary>
    /// Reads the three durable values of a battle end through a context of its own —
    /// a second connection, so it sees only what PostgreSQL committed. That is what
    /// makes the rollback assertions statements about the database rather than about
    /// in-memory state.
    /// </summary>
    private async Task<(BattleResult? Result, int PlayerXp, int PlayerLevel, int PetXp, int PetLevel)>
        ReadDurableStateAsync(string battleId, string playerId, string petInstanceId)
    {
        await using var context = CreateContext();

        var result = await context.BattleResults
            .AsNoTracking()
            .SingleOrDefaultAsync(row => row.BattleResultId == battleId);

        var player = await context.Players
            .AsNoTracking()
            .SingleAsync(candidate => candidate.PlayerId == playerId);

        var pet = await context.Pets
            .AsNoTracking()
            .SingleAsync(candidate => candidate.PetInstanceId == petInstanceId);

        return (result, player.XP, player.Level, pet.XP, pet.Level);
    }

    /// <summary>
    /// The battle-end boundary over the production boundaries sharing one context —
    /// the composition the scoped production registration produces, without the DI
    /// container (which the composed-graph test below covers).
    /// </summary>
    private static BattleResultService CreateService(
        GameDbContext context,
        IBattleStateRepository battleStates,
        IPetRepository? pets = null,
        IPlayerRepository? players = null) =>
        new(
            new BattleResultRepository(context),
            new BossDefinitionLookup(context),
            battleStates,
            players ?? new PlayerRepository(context),
            pets ?? new PetRepository(context),
            new BattleEndTransaction(context),
            TimeProvider.System);

    // =======================================================================
    // The happy path — one commit, then the delete
    // =======================================================================

    [Fact]
    public async Task BattleEnd_ShouldCommitTheResultAndBothProgressions_AndClearTheActiveStateOnlyAfterTheCommit()
    {
        if (!_available) return;

        var battleId = NewId("battle_atomic_ok");
        var (playerId, petInstanceId) = await SeedOwnerAsync();
        var state = NewBattle(battleId, playerId, petInstanceId);

        var battleStates = new RecordingBattleStateRepository(CreateContext);

        try
        {
            await using (var context = CreateContext())
            {
                await battleStates.CreateAsync(state);

                var written = await CreateService(context, battleStates)
                    .PersistTerminalResultAsync(state, BattleOutcome.Victory);

                Assert.True(written);
            }

            // REDIS_STATE.md §3: the active battle is cleared once, and the probe
            // read the durable row on a separate connection at that moment — so the
            // COMMIT preceded the delete rather than following it.
            Assert.Equal(1, battleStates.DeleteCount);
            Assert.True(battleStates.ResultWasDurableAtDelete);

            // DATABASE.md §1 / COMBAT_RULES.md §7.2 / PET_RULES.md §5.3: the result
            // row and both tracks' documented grants are durable afterwards.
            var (result, playerXp, playerLevel, petXp, petLevel) =
                await ReadDurableStateAsync(battleId, playerId, petInstanceId);

            Assert.NotNull(result);
            Assert.Equal(battleId, result!.BattleResultId);

            Assert.Equal(100, playerXp);
            Assert.Equal(2, playerLevel);

            Assert.Equal(100, petXp);
            Assert.Equal(2, petLevel);
        }
        finally
        {
            await CleanupAsync(battleId, playerId, petInstanceId);
        }
    }

    // =======================================================================
    // Pet progression failure — PostgreSQL rolls the whole unit of work back
    // =======================================================================

    [Fact]
    public async Task PetProgressionFailure_ShouldRollBackTheResultRowAndThePlayerProgression_InPostgres()
    {
        if (!_available) return;

        var battleId = NewId("battle_atomic_pet_fail");
        var (playerId, petInstanceId) = await SeedOwnerAsync();
        var state = NewBattle(battleId, playerId, petInstanceId);

        var battleStates = new RecordingBattleStateRepository(CreateContext);

        try
        {
            await using (var context = CreateContext())
            {
                var service = CreateService(
                    context,
                    battleStates,
                    pets: new FailingPetProgressionRepository(
                        new PetRepository(context),
                        ProgressionFailure.Throws));

                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => service.PersistTerminalResultAsync(state, BattleOutcome.Victory));
            }

            // REDIS_STATE.md §3: the delete is conditioned on the durable write, so
            // no durable battle end means no delete — the battle stays recoverable.
            Assert.Equal(0, battleStates.DeleteCount);

            // The result row was inserted and the Player progression was updated
            // inside the transaction before the Pet write failed, and PostgreSQL
            // undid both: no durable result, and no progression on either track.
            var (result, playerXp, playerLevel, petXp, petLevel) =
                await ReadDurableStateAsync(battleId, playerId, petInstanceId);

            Assert.Null(result);
            Assert.Equal(Player.InitialXp, playerXp);
            Assert.Equal(Player.InitialLevel, playerLevel);
            Assert.Equal(Pet.InitialXp, petXp);
            Assert.Equal(Pet.InitialLevel, petLevel);
        }
        finally
        {
            await CleanupAsync(battleId, playerId, petInstanceId);
        }
    }

    [Fact]
    public async Task PetProgressionReportedAsAbsent_ShouldRollBackTheUnitOfWork_InPostgres()
    {
        if (!_available) return;

        // DATABASE.md §1's "an absent row is reported as absence": the boundary's
        // own report of a write that did not happen must fail the battle end, so the
        // result row cannot survive it. This is the case the battle-end path used to
        // ignore — and the case that lost the Pet XP permanently, because the
        // surviving row then refused the retry.
        var battleId = NewId("battle_atomic_pet_absent");
        var (playerId, petInstanceId) = await SeedOwnerAsync();
        var state = NewBattle(battleId, playerId, petInstanceId);

        var battleStates = new RecordingBattleStateRepository(CreateContext);

        try
        {
            await using (var context = CreateContext())
            {
                var service = CreateService(
                    context,
                    battleStates,
                    pets: new FailingPetProgressionRepository(
                        new PetRepository(context),
                        ProgressionFailure.ReportsAbsentRow));

                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => service.PersistTerminalResultAsync(state, BattleOutcome.Victory));
            }

            Assert.Equal(0, battleStates.DeleteCount);

            var (result, playerXp, _, petXp, _) =
                await ReadDurableStateAsync(battleId, playerId, petInstanceId);

            Assert.Null(result);
            Assert.Equal(Player.InitialXp, playerXp);
            Assert.Equal(Pet.InitialXp, petXp);
        }
        finally
        {
            await CleanupAsync(battleId, playerId, petInstanceId);
        }
    }

    // =======================================================================
    // Player progression failure — the same invariant on the other track
    // =======================================================================

    [Fact]
    public async Task PlayerProgressionFailure_ShouldRollBackTheResultRow_AndWriteNoProgression_InPostgres()
    {
        if (!_available) return;

        var battleId = NewId("battle_atomic_player_fail");
        var (playerId, petInstanceId) = await SeedOwnerAsync();
        var state = NewBattle(battleId, playerId, petInstanceId);

        var battleStates = new RecordingBattleStateRepository(CreateContext);

        try
        {
            await using (var context = CreateContext())
            {
                var service = CreateService(
                    context,
                    battleStates,
                    players: new FailingPlayerProgressionRepository(
                        new PlayerRepository(context),
                        ProgressionFailure.Throws));

                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => service.PersistTerminalResultAsync(state, BattleOutcome.Victory));
            }

            Assert.Equal(0, battleStates.DeleteCount);

            var (result, playerXp, playerLevel, petXp, petLevel) =
                await ReadDurableStateAsync(battleId, playerId, petInstanceId);

            Assert.Null(result);
            Assert.Equal(Player.InitialXp, playerXp);
            Assert.Equal(Player.InitialLevel, playerLevel);
            Assert.Equal(Pet.InitialXp, petXp);
            Assert.Equal(Pet.InitialLevel, petLevel);
        }
        finally
        {
            await CleanupAsync(battleId, playerId, petInstanceId);
        }
    }

    // =======================================================================
    // Retry after a rolled-back attempt — the whole battle end is recorded once
    // =======================================================================

    [Fact]
    public async Task RetryAfterARolledBackAttempt_ShouldRecordTheWholeBattleEndOnce_InPostgres()
    {
        if (!_available) return;

        // DATABASE.md §1 sourcing item 3: a failed battle end leaves the battle
        // recoverable. The retry is only possible because the rollback left no
        // durable row — sourcing item 1's primary-key guard would otherwise find one
        // and refuse to apply the reward, which is exactly how the Pet XP used to be
        // lost permanently.
        var battleId = NewId("battle_atomic_retry");
        var (playerId, petInstanceId) = await SeedOwnerAsync();
        var state = NewBattle(battleId, playerId, petInstanceId);

        var battleStates = new RecordingBattleStateRepository(CreateContext);

        try
        {
            // Attempt 1 → the Pet progression fails → PostgreSQL rolls the result
            // row and the Player's grant back with it.
            await using (var context = CreateContext())
            {
                var failing = CreateService(
                    context,
                    battleStates,
                    pets: new FailingPetProgressionRepository(
                        new PetRepository(context),
                        ProgressionFailure.Throws));

                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => failing.PersistTerminalResultAsync(state, BattleOutcome.Victory));
            }

            Assert.Equal(0, battleStates.DeleteCount);

            // Attempt 2 → the cause is resolved, and the same terminal persistence is
            // re-run against the same authoritative state.
            await using (var context = CreateContext())
            {
                var written = await CreateService(context, battleStates)
                    .PersistTerminalResultAsync(state, BattleOutcome.Victory);

                Assert.True(written);
            }

            Assert.Equal(1, battleStates.DeleteCount);
            Assert.True(battleStates.ResultWasDurableAtDelete);

            // Exactly one durable result, and each track granted exactly once: the XP
            // is 100 rather than 200, and the levels are the documented ones for those
            // values (COMBAT_RULES.md §7.4, PET_RULES.md §5.4).
            var (result, playerXp, playerLevel, petXp, petLevel) =
                await ReadDurableStateAsync(battleId, playerId, petInstanceId);

            Assert.NotNull(result);
            Assert.Equal(100, playerXp);
            Assert.Equal(2, playerLevel);
            Assert.Equal(100, petXp);
            Assert.Equal(2, petLevel);

            await using var verification = CreateContext();

            Assert.Equal(
                1,
                await verification.BattleResults
                    .AsNoTracking()
                    .CountAsync(row => row.BattleResultId == battleId));
        }
        finally
        {
            await CleanupAsync(battleId, playerId, petInstanceId);
        }
    }

    // =======================================================================
    // The composed graph — the DI-registered boundaries share one transaction
    // =======================================================================

    [Fact]
    public async Task ComposedBattleEndPath_ShouldRollBack_WhenAParticipatingProgressionWriteFails()
    {
        if (!_available) return;

        // The tests above construct the boundaries over one context by hand, which
        // is what the scope does in production. This one proves the composition
        // itself: the context the registered repositories write through and the
        // context the registered IBattleEndTransaction begins its transaction on are
        // the same instance, so a failure anywhere in the graph undoes all of it.
        var battleId = NewId("battle_atomic_composed");
        var (playerId, petInstanceId) = await SeedOwnerAsync();
        var state = NewBattle(battleId, playerId, petInstanceId);

        var battleStates = new RecordingBattleStateRepository(CreateContext);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = ConnectionString,
            })
            .Build();

        var services = new ServiceCollection();

        services.AddLogging();
        services.AddInfrastructureServices(configuration);
        services.AddApplicationServices();

        // Redis is deliberately unconfigured here: the active-state store is the
        // recording double, so the delete the graph performs can be observed.
        services.AddSingleton<IBattleStateRepository>(battleStates);

        // The one injected failure, on the Application boundary the battle-end step
        // consumes — decorated over the registered real repository, so every other
        // call reaches PostgreSQL through the scoped context.
        services.AddScoped<IPetRepository>(provider => new FailingPetProgressionRepository(
            new PetRepository(provider.GetRequiredService<GameDbContext>()),
            ProgressionFailure.Throws));

        try
        {
            await using (var provider = services.BuildServiceProvider())
            using (var scope = provider.CreateScope())
            {
                var service = scope.ServiceProvider.GetRequiredService<BattleResultService>();

                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => service.PersistTerminalResultAsync(state, BattleOutcome.Victory));
            }

            Assert.Equal(0, battleStates.DeleteCount);

            var (result, playerXp, _, petXp, _) =
                await ReadDurableStateAsync(battleId, playerId, petInstanceId);

            Assert.Null(result);
            Assert.Equal(Player.InitialXp, playerXp);
            Assert.Equal(Pet.InitialXp, petXp);
        }
        finally
        {
            await CleanupAsync(battleId, playerId, petInstanceId);
        }
    }

    // =======================================================================
    // Test-only failure injection (no production flag exists — AGENTS.md §9)
    // =======================================================================

    /// <summary>
    /// How a participating progression write is made to fail. Both are real
    /// outcomes of the documented boundary: a write that raises, and a write that
    /// reports the row as absent (<c>DATABASE.md</c> §1).
    /// </summary>
    private enum ProgressionFailure
    {
        /// <summary>The write raises, as a failing PostgreSQL update does.</summary>
        Throws,

        /// <summary>The boundary reports that there was no row to update.</summary>
        ReportsAbsentRow,
    }

    /// <summary>
    /// The Pet boundary with the progression write made to fail, delegating every
    /// other operation to the real repository over the same context — so the reads
    /// and the writes that precede it are the production ones and participate in the
    /// production transaction.
    /// </summary>
    private sealed class FailingPetProgressionRepository : IPetRepository
    {
        private readonly PetRepository _inner;
        private readonly ProgressionFailure _failure;

        public FailingPetProgressionRepository(PetRepository inner, ProgressionFailure failure)
        {
            _inner = inner;
            _failure = failure;
        }

        public Task AddAsync(Pet pet, CancellationToken cancellationToken = default) =>
            _inner.AddAsync(pet, cancellationToken);

        public Task<PetDefinition?> GetDefinitionAsync(
            string petDefinitionId,
            CancellationToken cancellationToken = default) =>
            _inner.GetDefinitionAsync(petDefinitionId, cancellationToken);

        public Task<IReadOnlyList<Pet>> ListByPlayerIdAsync(
            string playerId,
            CancellationToken cancellationToken = default) =>
            _inner.ListByPlayerIdAsync(playerId, cancellationToken);

        public Task<IReadOnlyList<PetDefinition>> ListDefinitionsAsync(
            IReadOnlyCollection<string> petDefinitionIds,
            CancellationToken cancellationToken = default) =>
            _inner.ListDefinitionsAsync(petDefinitionIds, cancellationToken);

        public Task<Pet?> GetByIdAsync(string petInstanceId, CancellationToken cancellationToken = default) =>
            _inner.GetByIdAsync(petInstanceId, cancellationToken);

        public Task<bool> SaveProgressionAsync(Pet pet, CancellationToken cancellationToken = default) =>
            _failure == ProgressionFailure.Throws
                ? throw new InvalidOperationException(
                    "DATABASE.md §1: this integration test's Pet boundary is configured to fail the "
                    + "progression write.")
                : Task.FromResult(false);
    }

    /// <summary>
    /// The Player boundary with the progression write made to fail, delegating
    /// every other operation to the real repository for the same reason.
    /// </summary>
    private sealed class FailingPlayerProgressionRepository : IPlayerRepository
    {
        private readonly PlayerRepository _inner;
        private readonly ProgressionFailure _failure;

        public FailingPlayerProgressionRepository(PlayerRepository inner, ProgressionFailure failure)
        {
            _inner = inner;
            _failure = failure;
        }

        public Task<Player> GetOrCreateForAccountAsync(
            Guid accountId,
            Func<CancellationToken, Task<PlayerStarterGrant>> composeStarterGrant,
            CancellationToken cancellationToken = default) =>
            _inner.GetOrCreateForAccountAsync(accountId, composeStarterGrant, cancellationToken);

        public Task<Player?> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default) =>
            _inner.GetByAccountIdAsync(accountId, cancellationToken);

        public Task<Player?> GetByIdAsync(string playerId, CancellationToken cancellationToken = default) =>
            _inner.GetByIdAsync(playerId, cancellationToken);

        public Task<bool> SaveProgressionAsync(Player player, CancellationToken cancellationToken = default) =>
            _failure == ProgressionFailure.Throws
                ? throw new InvalidOperationException(
                    "DATABASE.md §1: this integration test's Player boundary is configured to fail the "
                    + "progression write.")
                : Task.FromResult(false);
    }

    /// <summary>
    /// The active battle state store for this suite, recording the one documented
    /// battle-end operation and probing the database when it happens.
    ///
    /// <b>The probe is the ordering proof.</b> <c>REDIS_STATE.md</c> §3 conditions
    /// the delete on the durable result having been written, so at the moment
    /// <see cref="DeleteAsync"/> is called the <c>BattleResult</c> row must already be
    /// visible — and it is read here on a connection of its own, which sees only what
    /// PostgreSQL committed. A delete that ran before the commit, or without one,
    /// cannot satisfy that.
    ///
    /// It is not a substitute for the real store in any other respect: it is a test
    /// double for the terminal path only, and no production composition registers it
    /// (<c>REDIS_STATE.md</c> §2 item 2, §7 item 5).
    /// </summary>
    private sealed class RecordingBattleStateRepository : IBattleStateRepository
    {
        private readonly Func<GameDbContext> _createContext;
        private readonly Dictionary<string, BattleState> _records = new(StringComparer.Ordinal);

        public RecordingBattleStateRepository(Func<GameDbContext> createContext)
        {
            _createContext = createContext;
        }

        /// <summary>How many deletes this double has received.</summary>
        public int DeleteCount { get; private set; }

        /// <summary>
        /// Whether the battle's <c>BattleResult</c> row was already durably visible,
        /// on a separate connection, when the delete was attempted.
        /// </summary>
        public bool ResultWasDurableAtDelete { get; private set; }

        public Task CreateAsync(BattleState state, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(state);

            _records[state.BattleId] = state;

            return Task.CompletedTask;
        }

        public Task<BattleState?> GetAsync(string battleId, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(battleId);

            return Task.FromResult(
                _records.TryGetValue(battleId, out var state) ? state : null);
        }

        public Task<bool> TryUpdateAsync(
            BattleState state,
            int expectedSequence,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(state);

            if (!_records.TryGetValue(state.BattleId, out var stored) || stored.Sequence != expectedSequence)
            {
                return Task.FromResult(false);
            }

            _records[state.BattleId] = state;

            return Task.FromResult(true);
        }

        public async Task DeleteAsync(string battleId, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(battleId);

            DeleteCount++;

            await using (var context = _createContext())
            {
                ResultWasDurableAtDelete = await context.BattleResults
                    .AsNoTracking()
                    .AnyAsync(row => row.BattleResultId == battleId, cancellationToken);
            }

            _records.Remove(battleId);
        }
    }
}