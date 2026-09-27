using GameServer.Infrastructure.Postgres;
using Microsoft.EntityFrameworkCore;

namespace GameServer.Infrastructure.Tests;

/// <summary>
/// TASK-032 — the Infrastructure layer's share of the Player/Pet authority
/// regression suite.
///
/// <code>
/// ADR-011 / ADR-012 authority model
/// Player       = account / owner
/// Pet          = combat character
/// BattleState  = authoritative battle snapshot
/// PetState     = combat-state owner
/// PlayerState  = NOT a combat-stat owner (no such node)
/// </code>
///
/// Infrastructure implements the Application contracts, so a Player
/// combat-readiness source or a Player combat-profile persistence shape would
/// land here if it returned (<c>DATABASE.md</c> §1 — no Player
/// combat-readiness condition is part of the contract; <c>§3</c> /
/// <c>ADR-011</c> item 5 — no combat-stat columns on Player).
///
/// <b>Each layer asserts its own assembly.</b> The regression can reappear in any
/// of them, and a scan of Domain would not see an Infrastructure-layer
/// reintroduction. The same checks are applied per layer by
/// <c>Application.Tests.AuthorityRegressionSuiteTests</c>,
/// <c>Api.Tests.ApiAuthorityRegressionTests</c>, and this class — duplicated
/// deliberately rather than shared through a cross-project reference, which would
/// couple four independent test projects to gain fifteen lines
/// (<c>AGENTS.md</c> §9).
/// </summary>
public class InfrastructureAuthorityRegressionTests
{
    /// <summary>
    /// The type names TASK-056/TASK-057 removed from the contract — not renamed,
    /// not deprecated, and not replaced (<c>DATABASE.md</c> §1).
    /// </summary>
    private static readonly string[] RemovedCombatReadinessTypes =
    [
        "IsCombatReady",
        "ICombatStatsSource",
        "PlayerCombatProfile",
        "PlayerCombatProfileSource",
    ];

    private static GameDbContext CreateContext(string storeName) =>
        TestGameDbContextFactory.Create(storeName);

    [Fact]
    public void Infrastructure_ShouldDeclareNoPlayerCombatReadinessType()
    {
        var declared = typeof(GameDbContext).Assembly.GetTypes().Select(t => t.Name).ToArray();

        foreach (var forbidden in RemovedCombatReadinessTypes)
        {
            Assert.DoesNotContain(forbidden, declared);
        }

        // Any name that merely mentions the removed mechanism, so a renamed
        // reintroduction cannot slip past the exact-name check.
        foreach (var fragment in new[] { "CombatReady", "CombatReadiness", "CombatStatsSource" })
        {
            Assert.DoesNotContain(
                declared,
                name => name.Contains(fragment, StringComparison.Ordinal));
        }

        // The scan is a real scan: the layer's documented types are present, so an
        // empty or unloaded assembly cannot make the assertions above vacuous.
        Assert.NotEmpty(declared);
        Assert.Contains(nameof(GameDbContext), declared);
    }

    [Fact]
    public void PlayerModel_ShouldExposeNoCombatReadinessOrCombatProfileShape()
    {
        // The persistence model is the place a readiness column or a combat-profile
        // entity would be added, and CLR reflection on the entity does not cover the
        // mapped shape. PlayerPersistenceTests owns the exact four-column field set
        // (DATABASE.md §1); this states the same guarantee against the mapped model
        // in the negative direction the removed mechanism would need.
        using var context = CreateContext(
            nameof(PlayerModel_ShouldExposeNoCombatReadinessOrCombatProfileShape));

        var playerEntity = context.Model.FindEntityType(typeof(Domain.Players.Player))!;

        var columns = playerEntity.GetProperties().Select(p => p.Name).ToArray();

        foreach (var forbiddenFragment in new[] { "Ready", "Readiness", "Combat", "Profile", "Stats" })
        {
            Assert.DoesNotContain(
                columns,
                name => name.Contains(forbiddenFragment, StringComparison.Ordinal));
        }

        // And no second entity was introduced to carry a Player combat profile.
        // CardPersistenceTests/RelicPersistenceTests assert the documented table set
        // exhaustively; this is the same guarantee stated for the mechanism that was
        // removed, so a reintroduction under a new table name fails here too.
        var tables = context.Model.GetEntityTypes()
            .Select(e => e.GetTableName())
            .Where(name => name is not null)
            .ToArray();

        foreach (var forbiddenFragment in new[] { "Combat", "Profile", "Readiness", "Stats" })
        {
            Assert.DoesNotContain(
                tables,
                name => name!.Contains(forbiddenFragment, StringComparison.Ordinal));
        }
    }
}
