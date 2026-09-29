using GameServer.Application.Players;
using GameServer.Domain.Cards;
using GameServer.Domain.Pets;
using GameServer.Domain.Relics;
using Microsoft.EntityFrameworkCore;

namespace GameServer.Infrastructure.Tests;

/// <summary>
/// Starter-grant fixtures for the Player-creation tests.
///
/// <b>Why a helper exists.</b> <c>IPlayerRepository.GetOrCreateByDiscordUserIdAsync</c>
/// takes the starter ownership set a newly created Player receives
/// (<c>DATABASE.md</c> §2 item 1), so every test that exercises the
/// match-or-create path must state what starter set it is creating the Player
/// with. The two builders below keep that statement explicit and one line long.
///
/// <b>These fixtures assert no documented content value.</b> They build the
/// ownership rows directly, so they must never be read as a second definition of
/// the starter set — the composition (<c>pet-xich-lang</c>, the three Basic
/// Cards, the three selected Relics) is owned by
/// <see cref="PlayerStarterGrantFactory"/> and <c>DATABASE.md</c> §2 item 1, and
/// the tests that assert it use the real factory.
/// </summary>
internal static class TestStarterGrants
{
    /// <summary>
    /// A starter set whose rows carry a distinct, recognisable definition each,
    /// so a test can assert the whole set was staged, committed, or discarded.
    ///
    /// <b>For the InMemory provider only.</b> Those definition ids are this
    /// fixture's own labels and resolve to no row: InMemory enforces no foreign
    /// key, so a test that only cares about the Player row can use this. A test
    /// running against real PostgreSQL must use
    /// <see cref="ResolvedAsync"/> instead, because there the FK is enforced and
    /// the fixture's labels would violate it — which is itself the proof that the
    /// real starter set's definition references are constrained.
    /// </summary>
    public static PlayerStarterGrant Staged(
        string petDefinitionId = "pet-fixture",
        string cardDefinitionPrefix = "card-fixture",
        string relicDefinitionPrefix = "relic-fixture")
    {
        var pet = new Pet
        {
            PetInstanceId = $"petinst_{Guid.NewGuid():N}",
            PlayerId = string.Empty,
            PetDefinitionId = petDefinitionId,
            Tier = PetTier.Common,
            Star = Pet.MinStar,
            XP = Pet.InitialXp,
            Level = Pet.InitialLevel,
            AcquiredAt = DateTimeOffset.UtcNow,
        };

        var cards = new List<PlayerUnlockedCard>();
        var relics = new List<Relic>();

        for (var index = 0; index < 3; index++)
        {
            cards.Add(new PlayerUnlockedCard
            {
                PlayerId = string.Empty,
                CardDefinitionId = $"{cardDefinitionPrefix}-{index}",
            });

            relics.Add(new Relic
            {
                RelicInstanceId = $"relicinst_{Guid.NewGuid():N}",
                PlayerId = string.Empty,
                RelicDefinitionId = $"{relicDefinitionPrefix}-{index}",
                AcquiredAt = DateTimeOffset.UtcNow,
            });
        }

        return new PlayerStarterGrant(pet, cards, relics);
    }

    /// <summary>
    /// The starter set the real composition produces, for the tests that assert
    /// the documented contents end to end. It resolves the provisioned
    /// definitions through the same factory the auth path uses, so those tests
    /// cannot drift from production.
    /// </summary>
    public static Task<PlayerStarterGrant> ResolvedAsync(
        Postgres.GameDbContext context,
        DateTimeOffset acquiredAt) =>
        new PlayerStarterGrantFactory(
                new Postgres.Repositories.PetRepository(context),
                new Postgres.Repositories.CardRepository(context),
                new Postgres.Repositories.RelicRepository(context))
            .CreateAsync(acquiredAt);

    /// <summary>
    /// <see cref="Staged"/> as the boundary expects it. The production call
    /// signature takes a composition callback so the starter set is resolved on
    /// the creation branch only; these fixtures satisfy it with a value that is
    /// already built, which is the same shape a test wants when it asserts the
    /// Player row rather than the composition.
    /// </summary>
    public static Func<CancellationToken, Task<PlayerStarterGrant>> StagedCallback =>
        _ => Task.FromResult(Staged());

    /// <summary>
    /// <see cref="ResolvedAsync"/> as the boundary expects it, so a
    /// PostgreSQL-backed test creates a Player with the real starter set.
    /// </summary>
    public static Func<CancellationToken, Task<PlayerStarterGrant>> ResolvedCallback(
        Postgres.GameDbContext context) =>
        cancellationToken => ResolvedAsync(context, DateTimeOffset.UtcNow);

    /// <summary>
    /// Removes <paramref name="playerId"/> and every ownership row created for
    /// it, so a test that ran against the shared development database leaves it
    /// as it found it.
    ///
    /// The ownership rows are deleted first because <c>DATABASE.md</c> §1's
    /// ownership FKs are <c>OnDelete Restrict</c>
    /// (<c>PetConfiguration</c> / <c>RelicConfiguration</c>): deleting the Player
    /// alone would be rejected while its collection still references it.
    /// </summary>
    public static async Task CleanupPlayerAsync(
        Postgres.GameDbContext context,
        string playerId)
    {
        await context.Pets
            .Where(pet => pet.PlayerId == playerId)
            .ExecuteDeleteAsync();

        await context.PlayerUnlockedCards
            .Where(unlocked => unlocked.PlayerId == playerId)
            .ExecuteDeleteAsync();

        await context.Relics
            .Where(relic => relic.PlayerId == playerId)
            .ExecuteDeleteAsync();

        await context.Players
            .Where(player => player.PlayerId == playerId)
            .ExecuteDeleteAsync();
    }
}
