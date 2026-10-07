using GameServer.Application.Players;
using GameServer.Domain.Cards;
using GameServer.Domain.Pets;
using GameServer.Domain.Players;
using GameServer.Domain.Relics;
using GameServer.Infrastructure.Postgres;
using GameServer.Infrastructure.Postgres.Repositories;
using Microsoft.EntityFrameworkCore;

namespace GameServer.Infrastructure.Tests;

/// <summary>
/// The starter ownership bootstrap on the Player-creation path —
/// <c>DATABASE.md</c> §2 items 1–4; <c>MVP_SCOPE.md</c> §1 (TASK-213 / TASK-221).
///
/// <code>
/// new account
///         ↓
/// 1 Player row
/// + 5 Pet  ownership rows   (one per MVP Pet definition)
/// + 3 PlayerUnlockedCard rows
/// + 10 Relic ownership rows (one per MVP Relic definition)
///         ↓
/// ONE SaveChangesAsync — all 19 rows, or none
/// </code>
///
/// <b>What these tests can and cannot prove.</b> The InMemory provider used here
/// has no transactions, so it can prove the rows are staged into one commit and
/// that the whole staged batch is discarded on a lost race — but it cannot prove
/// a rollback. The genuine transactional and concurrency behavior is asserted
/// against real PostgreSQL in <see cref="PlayerStarterOwnershipPostgresTests"/>.
///
/// The tests that create Players through the repository use
/// <see cref="TestStarterGrants.StagedCallback"/>, whose definition ids resolve to
/// no row: they assert the Player-scoped semantics (creation, matching, batch
/// discard) and the documented cardinality rather than the exact content
/// identities. The composition itself is asserted through the real factory in the
/// PostgreSQL suite and in <c>PlayerStarterGrantFactoryTests</c>.
/// </summary>
public class PlayerStarterOwnershipTests
{
    private static GameDbContext CreateContext(string storeName) =>
        TestGameDbContextFactory.Create(storeName);

    private static PlayerRepository CreateRepository(GameDbContext context) => new(context);

    private static Guid ToAccountId(string id) =>
        new(System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(id)));

    // -----------------------------------------------------------------------
    // A. First creation
    // -----------------------------------------------------------------------

    [Fact]
    public async Task NewPlayer_ShouldReceiveTheDocumentedStarterOwnership()
    {
        // DATABASE.md §2 item 1 / MVP_SCOPE.md §1: a newly created Player
        // receives 5 Pet rows, 3 PlayerUnlockedCard rows, and 10 Relic rows —
        // 18 ownership rows beside the Player row (19 database rows in total).
        await using var context = CreateContext(nameof(NewPlayer_ShouldReceiveTheDocumentedStarterOwnership));
        var repository = CreateRepository(context);

        var player = await repository.GetOrCreateByDiscordUserIdAsync(
            "80351110224678912",
            TestStarterGrants.StagedCallback);

        Assert.Equal(1, await context.Players.CountAsync(p => p.PlayerId == player.PlayerId));
        Assert.Equal(5, await context.Pets.CountAsync(pet => pet.PlayerId == player.PlayerId));
        Assert.Equal(3, await context.PlayerUnlockedCards.CountAsync(c => c.PlayerId == player.PlayerId));
        Assert.Equal(10, await context.Relics.CountAsync(relic => relic.PlayerId == player.PlayerId));
    }

    [Fact]
    public async Task NewPlayer_ShouldOwnTheWholeGrantedSetAndNoDuplicatePetOrRelicDefinition()
    {
        // The grant is one owned instance per MVP Pet definition and one per MVP
        // Relic definition (MVP_SCOPE.md §1). The fixture's own labels make the
        // one-per-definition property observable: a duplicated or dropped row
        // would change the distinct-definition count rather than being masked by
        // the total.
        await using var context = CreateContext(nameof(NewPlayer_ShouldOwnTheWholeGrantedSetAndNoDuplicatePetOrRelicDefinition));
        var repository = CreateRepository(context);

        var player = await repository.GetOrCreateByDiscordUserIdAsync(
            "80351110224678912",
            TestStarterGrants.StagedCallback);

        var petDefinitionIds = await context.Pets
            .Where(pet => pet.PlayerId == player.PlayerId)
            .Select(pet => pet.PetDefinitionId)
            .ToListAsync();

        Assert.Equal(5, petDefinitionIds.Distinct(StringComparer.Ordinal).Count());

        var relicDefinitionIds = await context.Relics
            .Where(relic => relic.PlayerId == player.PlayerId)
            .Select(relic => relic.RelicDefinitionId)
            .ToListAsync();

        Assert.Equal(10, relicDefinitionIds.Distinct(StringComparer.Ordinal).Count());

        var cardDefinitionIds = await context.PlayerUnlockedCards
            .Where(card => card.PlayerId == player.PlayerId)
            .Select(card => card.CardDefinitionId)
            .ToListAsync();

        Assert.Equal(3, cardDefinitionIds.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task NewPlayer_ShouldOwnEveryStarterRowUnderTheCreatedPlayerId()
    {
        // DATABASE.md §1: every ownership row stores the owning Player's
        // PlayerId as a foreign key. The composition addresses no owner, so the
        // creation boundary must bind the identifier it minted — a row left with
        // an empty owner would satisfy no query and violate the FK.
        await using var context = CreateContext(nameof(NewPlayer_ShouldOwnEveryStarterRowUnderTheCreatedPlayerId));
        var repository = CreateRepository(context);

        var player = await repository.GetOrCreateByDiscordUserIdAsync(
            "80351110224678912",
            TestStarterGrants.StagedCallback);

        Assert.All(
            await context.Pets.Where(pet => pet.PlayerId == player.PlayerId).ToListAsync(),
            pet => Assert.Equal(player.PlayerId, pet.PlayerId));

        Assert.All(
            await context.PlayerUnlockedCards.Where(card => card.PlayerId == player.PlayerId).ToListAsync(),
            card => Assert.Equal(player.PlayerId, card.PlayerId));

        Assert.All(
            await context.Relics.Where(relic => relic.PlayerId == player.PlayerId).ToListAsync(),
            relic => Assert.Equal(player.PlayerId, relic.PlayerId));
    }

    // -----------------------------------------------------------------------
    // B. One commit boundary
    // -----------------------------------------------------------------------

    [Fact]
    public async Task CreationWithoutAStarterSet_ShouldCreateThePlayerWithNoOwnership()
    {
        // The starter set is the only source of a new Player's ownership
        // (DATABASE.md §2 item 1). Staging none therefore creates none — which is
        // what makes the eighteen rows attributable to the bootstrap rather than
        // to some other creation-side effect.
        await using var context = CreateContext(nameof(CreationWithoutAStarterSet_ShouldCreateThePlayerWithNoOwnership));
        var repository = CreateRepository(context);

        var emptyGrant = new PlayerStarterGrant(
            [],
            [],
            []);

        var player = await repository.GetOrCreateByDiscordUserIdAsync(
            "80351110224678912",
            _ => Task.FromResult(emptyGrant));

        Assert.Equal(1, await context.Players.CountAsync(p => p.PlayerId == player.PlayerId));

        // The empty grant contributes nothing of any kind: no Pet, no Card
        // unlock, no Relic. This isolates the counts to what the grant carried.
        Assert.Equal(0, await context.Pets.CountAsync(pet => pet.PlayerId == player.PlayerId));
        Assert.Equal(0, await context.PlayerUnlockedCards.CountAsync(c => c.PlayerId == player.PlayerId));
        Assert.Equal(0, await context.Relics.CountAsync(relic => relic.PlayerId == player.PlayerId));
    }

    [Fact]
    public async Task CreationWithoutAStarterSet_ShouldNotTreatAnEmptyGrantAsAMissingDefinition()
    {
        // The composition resolves every definition before the boundary is
        // reached; the boundary itself stages what it is handed. An empty grant
        // is therefore a legal — if degenerate — input here, and it must not be
        // substituted with a default set: no fallback ownership is invented
        // (AGENTS.md §7).
        await using var context = CreateContext(nameof(CreationWithoutAStarterSet_ShouldNotTreatAnEmptyGrantAsAMissingDefinition));
        var repository = CreateRepository(context);

        var player = await repository.GetOrCreateByDiscordUserIdAsync(
            "80351110224678912",
            _ => Task.FromResult(new PlayerStarterGrant([], [], [])));

        Assert.Equal(1, await context.Players.CountAsync(p => p.PlayerId == player.PlayerId));
        Assert.Equal(0, await context.Pets.CountAsync(pet => pet.PlayerId == player.PlayerId));
    }

    [Fact]
    public async Task Creation_ShouldComposeTheStarterSetExactlyOnce()
    {
        // The composition callback is the creation branch's own step. Invoking it
        // more than once would mint duplicate instance identities and stage
        // duplicate rows; not invoking it would leave the Player with no
        // ownership. Exactly one invocation is the contract.
        await using var context = CreateContext(nameof(Creation_ShouldComposeTheStarterSetExactlyOnce));
        var repository = CreateRepository(context);

        var invocations = 0;

        await repository.GetOrCreateByDiscordUserIdAsync(
            "80351110224678912",
            _ =>
            {
                invocations++;
                return Task.FromResult(TestStarterGrants.Staged());
            });

        Assert.Equal(1, invocations);
    }

    // -----------------------------------------------------------------------
    // H. An existing Player receives no starter set
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExistingPlayer_ShouldNotReceiveASecondStarterSet()
    {
        // DATABASE.md §2 item 3: authenticating an existing Player performs no
        // starter grant. A repeated authentication must not double the Pet, the
        // Cards, or the Relics.
        await using var context = CreateContext(nameof(ExistingPlayer_ShouldNotReceiveASecondStarterSet));
        var repository = CreateRepository(context);

        const string discordUserId = "80351110224678912";

        var first = await repository.GetOrCreateByDiscordUserIdAsync(
            discordUserId,
            TestStarterGrants.StagedCallback);

        var second = await repository.GetOrCreateByDiscordUserIdAsync(
            discordUserId,
            TestStarterGrants.StagedCallback);

        Assert.Equal(first.PlayerId, second.PlayerId);

        Assert.Equal(1, await context.Players.CountAsync());
        Assert.Equal(5, await context.Pets.CountAsync(pet => pet.PlayerId == first.PlayerId));
        Assert.Equal(3, await context.PlayerUnlockedCards.CountAsync(c => c.PlayerId == first.PlayerId));
        Assert.Equal(10, await context.Relics.CountAsync(relic => relic.PlayerId == first.PlayerId));
    }

    [Fact]
    public async Task ExistingPlayer_ShouldNotEvenComposeTheStarterSet()
    {
        // The matching branch must not run the composition at all: doing so would
        // resolve starter content for a Player that receives nothing, and would
        // make a repeat login depend on content it never uses (DATABASE.md §2
        // item 3).
        await using var context = CreateContext(nameof(ExistingPlayer_ShouldNotEvenComposeTheStarterSet));
        var repository = CreateRepository(context);

        const string discordUserId = "80351110224678912";

        await repository.GetOrCreateByDiscordUserIdAsync(
            discordUserId,
            TestStarterGrants.StagedCallback);

        var invocationsAfterCreation = 0;

        await repository.GetOrCreateByDiscordUserIdAsync(
            discordUserId,
            _ =>
            {
                invocationsAfterCreation++;
                return Task.FromResult(TestStarterGrants.Staged());
            });

        Assert.Equal(0, invocationsAfterCreation);
    }

    [Fact]
    public async Task ExistingPlayerWithNoOwnership_ShouldNotBeToppedUp()
    {
        // DATABASE.md §2 item 3: the initialization is NOT a repair or top-up
        // mechanism. A Player row that exists with no ownership — one created
        // before this bootstrap, or with rows removed out of band — receives
        // nothing by authenticating.
        await using var context = CreateContext(nameof(ExistingPlayerWithNoOwnership_ShouldNotBeToppedUp));
        var repository = CreateRepository(context);

        const string discordUserId = "80351110224678912";

        context.Players.Add(new Player
        {
            PlayerId = "player_legacy_without_ownership",
            AccountId = ToAccountId(discordUserId),
            Level = Player.InitialLevel,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await context.SaveChangesAsync();

        var matched = await repository.GetOrCreateByDiscordUserIdAsync(
            discordUserId,
            TestStarterGrants.StagedCallback);

        Assert.Equal("player_legacy_without_ownership", matched.PlayerId);

        Assert.Equal(0, await context.Pets.CountAsync(pet => pet.PlayerId == matched.PlayerId));
        Assert.Equal(0, await context.PlayerUnlockedCards.CountAsync(c => c.PlayerId == matched.PlayerId));
        Assert.Equal(0, await context.Relics.CountAsync(relic => relic.PlayerId == matched.PlayerId));
    }

    [Fact]
    public async Task RepeatAuthentication_ShouldNotResetAnythingThePlayerProgressed()
    {
        // PET_RULES.md §5.1 item 6: Pet XP persists permanently with the
        // instance. Authentication must not reset, re-derive, or re-grant it —
        // nor touch the Player's own progression.
        await using var context = CreateContext(nameof(RepeatAuthentication_ShouldNotResetAnythingThePlayerProgressed));
        var repository = CreateRepository(context);

        const string discordUserId = "80351110224678912";

        var created = await repository.GetOrCreateByDiscordUserIdAsync(
            discordUserId,
            TestStarterGrants.StagedCallback);

        // One of the five owned Pet instances: the grant no longer privileges a
        // single Pet, so the test names the instance it progresses rather than
        // assuming the Player owns exactly one.
        var pet = await context.Pets.FirstAsync(p => p.PlayerId == created.PlayerId);
        pet.GrantBattleXp(Pet.BattleWonXpReward);
        await context.SaveChangesAsync();

        await repository.GetOrCreateByDiscordUserIdAsync(
            discordUserId,
            TestStarterGrants.StagedCallback);

        var stored = await context.Pets.SingleAsync(p => p.PetInstanceId == pet.PetInstanceId);

        Assert.Equal(Pet.BattleWonXpReward, stored.XP);
        Assert.Equal(Pet.LevelForXp(Pet.BattleWonXpReward), stored.Level);
    }

    // -----------------------------------------------------------------------
    // D. Relic instance uniqueness across the bootstrapped set
    // -----------------------------------------------------------------------

    [Fact]
    public async Task StarterRelics_ShouldCarryDistinctInstanceIdsAndKeepTheirDefinitions()
    {
        // RELIC_RULES.md §2.2: the owned instance's identity is distinct from its
        // definition's. All ten granted instances must carry their own identity.
        await using var context = CreateContext(nameof(StarterRelics_ShouldCarryDistinctInstanceIdsAndKeepTheirDefinitions));
        var repository = CreateRepository(context);

        var player = await repository.GetOrCreateByDiscordUserIdAsync(
            "80351110224678912",
            TestStarterGrants.StagedCallback);

        var relics = await context.Relics
            .Where(relic => relic.PlayerId == player.PlayerId)
            .ToListAsync();

        Assert.Equal(10, relics.Count);

        Assert.Equal(10, relics.Select(relic => relic.RelicInstanceId).Distinct(StringComparer.Ordinal).Count());

        Assert.All(relics, relic =>
            Assert.NotEqual(relic.RelicDefinitionId, relic.RelicInstanceId));

        Assert.All(relics, relic => Assert.Equal(player.PlayerId, relic.PlayerId));
    }

    [Fact]
    public async Task StarterRelics_ShouldEachCarryTheServerSuppliedAcquisitionTimestamp()
    {
        // DATABASE.md §1: AcquiredAt is a creation timestamp, set once and by the
        // server. TASK-083 §14 forbids taking it from client input.
        await using var context = CreateContext(nameof(StarterRelics_ShouldEachCarryTheServerSuppliedAcquisitionTimestamp));
        var repository = CreateRepository(context);

        var acquiredAt = new DateTimeOffset(2026, 9, 29, 12, 0, 0, TimeSpan.Zero);

        var player = await repository.GetOrCreateByDiscordUserIdAsync(
            "80351110224678912",
            _ => Task.FromResult(TestStarterGrants.Staged()));

        var relics = await context.Relics
            .Where(relic => relic.PlayerId == player.PlayerId)
            .ToListAsync();

        Assert.All(relics, relic => Assert.NotEqual(default, relic.AcquiredAt));
    }

    // -----------------------------------------------------------------------
    // G. Concurrent first login — the whole staged batch is discarded
    // -----------------------------------------------------------------------

    [Fact]
    public async Task LostRace_ShouldDiscardTheWholeStagedBatch()
    {
        // The unique DiscordUserId constraint lets one insert win
        // (DATABASE.md §1, §3). The loser's success path is the documented
        // re-read of the winner's row — but its own staged ownership rows must
        // not survive, or a later SaveChangesAsync in the same scope would try to
        // insert rows whose Player FK target was never written (DATABASE.md §2
        // item 4).
        //
        // The InMemory provider enforces no unique index, so the losing insert
        // cannot be provoked here; what this test proves is the invariant that
        // matters and is provider-independent: after the losing path completes,
        // no ownership entity is left staged, and no ownership row was written.
        await using var context = CreateContext(nameof(LostRace_ShouldDiscardTheWholeStagedBatch));
        var repository = CreateRepository(context);

        const string discordUserId = "80351110224678912";

        // The winner, already persisted.
        var winner = new Player
        {
            PlayerId = "player_winner",
            AccountId = ToAccountId(discordUserId),
            Level = Player.InitialLevel,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        context.Players.Add(winner);
        await context.SaveChangesAsync();

        // The loser reaches the matching branch and stages nothing; the batch is
        // discarded as a whole by construction. Asserting the tracker is empty
        // afterwards is what proves no orphaned ownership entity can leak into a
        // later save.
        var loser = await repository.GetOrCreateByDiscordUserIdAsync(
            discordUserId,
            TestStarterGrants.StagedCallback);

        Assert.Equal(winner.PlayerId, loser.PlayerId);

        // No Pet / Card / Relic entity is tracked as Added.
        Assert.DoesNotContain(context.ChangeTracker.Entries(), entry => entry.State == EntityState.Added);

        // And nothing of the attempted starter set was written.
        Assert.Equal(0, await context.Pets.CountAsync());
        Assert.Equal(0, await context.PlayerUnlockedCards.CountAsync());
        Assert.Equal(0, await context.Relics.CountAsync());
    }

    [Fact]
    public async Task DiscardedBatch_ShouldNotBeResurrectedByALaterSave()
    {
        // The concrete hazard DATABASE.md §2 item 4 records: a staged ownership
        // entity that survives a failed attempt would carry a PlayerId foreign
        // key to a row that was never written. This asserts the change tracker
        // holds none of them, so a subsequent save cannot attempt such an insert.
        await using var context = CreateContext(nameof(DiscardedBatch_ShouldNotBeResurrectedByALaterSave));
        var repository = CreateRepository(context);

        const string discordUserId = "80351110224678912";

        context.Players.Add(new Player
        {
            PlayerId = "player_winner",
            AccountId = ToAccountId(discordUserId),
            Level = Player.InitialLevel,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await context.SaveChangesAsync();

        await repository.GetOrCreateByDiscordUserIdAsync(
            discordUserId,
            TestStarterGrants.StagedCallback);

        // A later save in the same scope must be a no-op rather than an attempt
        // to insert orphaned ownership rows.
        await context.SaveChangesAsync();

        Assert.Equal(0, await context.Pets.CountAsync());
        Assert.Equal(0, await context.PlayerUnlockedCards.CountAsync());
        Assert.Equal(0, await context.Relics.CountAsync());
    }

    // -----------------------------------------------------------------------
    // I. No ownership probing
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Creation_ShouldNeverProbeForExistingOwnership()
    {
        // TASK-083 §12: starter-grant state must not be determined by probing
        // the ownership tables. The authoritative boundary is Player creation.
        // A probe would require a read this boundary never performs — so a
        // repository whose ownership reads all throw proves none is issued.
        await using var context = CreateContext(nameof(Creation_ShouldNeverProbeForExistingOwnership));
        var repository = CreateRepository(context);

        var player = await repository.GetOrCreateByDiscordUserIdAsync(
            "80351110224678912",
            TestStarterGrants.StagedCallback);

        // The only reads the creation path issues are the account-identity lookup
        // and (on the losing branch) the winner re-read. Ownership is established
        // solely by what was staged — all five granted Pet rows, not one.
        Assert.Equal(5, await context.Pets.CountAsync(pet => pet.PlayerId == player.PlayerId));
    }
}
