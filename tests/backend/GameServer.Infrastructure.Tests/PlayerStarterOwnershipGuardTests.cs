using GameServer.Application.Players;
using GameServer.Domain.Cards;
using GameServer.Domain.Pets;
using GameServer.Domain.Players;
using GameServer.Domain.Relics;
using GameServer.Infrastructure.Postgres;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace GameServer.Infrastructure.Tests;

/// <summary>
/// TASK-083's negative acceptance criteria, asserted against the applied EF Core
/// model and the source that produced it.
///
/// <code>
/// No new table, column, index, or constraint   (DATABASE.md §2, §4)
/// No starter/granted flag                       (TASK-083 §"Out of Scope")
/// No new endpoint or request/response member    (API_CONTRACTS.md §1, §2.5)
/// No new persistence abstraction                (DATABASE.md §2 item 4)
/// </code>
///
/// <b>Why these are model assertions rather than review notes.</b> Each of them
/// is a thing the implementation could plausibly have grown — an
/// <c>IsInitialized</c> column, a uniqueness constraint on
/// <c>(PlayerId, RelicDefinitionId)</c>, an <c>IUnitOfWork</c> — and each would be
/// a contract change rather than an implementation detail. Asserting them here
/// means such a change fails a test instead of passing review by looking
/// reasonable.
/// </summary>
public class PlayerStarterOwnershipGuardTests
{
    private static GameDbContext CreateContext(string storeName) =>
        TestGameDbContextFactory.Create(storeName);

    // -----------------------------------------------------------------------
    // No schema change
    // -----------------------------------------------------------------------

    [Fact]
    public void AppliedModel_ShouldContainExactlyTheDocumentedPersistentEntities()
    {
        // DATABASE.md §1 lists the persistent tables. The starter bootstrap adds
        // none of them: it writes existing tables only.
        using var context = CreateContext(nameof(AppliedModel_ShouldContainExactlyTheDocumentedPersistentEntities));

        var tables = context.Model.GetEntityTypes()
            .Select(entityType => entityType.GetTableName())
            .Where(name => name is not null)
            .Select(name => name!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            [
                "Account",
                "BattleResult",
                "BossDefinition",
                "CardDefinition",
                "Pet",
                "PetDefinition",
                "Player",
                "PlayerUnlockedCard",
                "Relic",
                "RelicDefinition",
            ],
            tables);
    }

    [Theory]
    [InlineData("HasReceivedStarter")]
    [InlineData("StarterGranted")]
    [InlineData("IsInitialized")]
    [InlineData("HasReceivedStarterOwnership")]
    [InlineData("StarterOwnershipGrantedAt")]
    public void AppliedModel_ShouldDeclareNoStarterGrantFlag(string forbiddenMember)
    {
        // TASK-083 §12 / §"Out of Scope": the bootstrap boundary is the Player
        // row's own creation step, so the Player table gains no flag and no
        // timestamp recording "this Player received the starter set". A probe or
        // flag would introduce a second, weaker idempotency rule.
        using var context = CreateContext($"{nameof(AppliedModel_ShouldDeclareNoStarterGrantFlag)}-{forbiddenMember}");

        var playerProperties = context.Model
            .FindEntityType(typeof(Player))!
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();

        Assert.DoesNotContain(forbiddenMember, playerProperties);
    }

    [Fact]
    public void AppliedModel_ShouldKeepTheDocumentedOwnershipColumnsOnly()
    {
        // DATABASE.md §1 fixes each ownership table's member set. The bootstrap
        // adds no column to any of them — in particular no acquisition timestamp
        // on PlayerUnlockedCard, which has exactly two members.
        using var context = CreateContext(nameof(AppliedModel_ShouldKeepTheDocumentedOwnershipColumnsOnly));

        Assert.Equal(
            ["AcquiredAt", "Level", "PetDefinitionId", "PetInstanceId", "PlayerId", "Star", "Tier", "XP"],
            PropertyNames<Pet>(context));

        Assert.Equal(
            ["CardDefinitionId", "PlayerId"],
            PropertyNames<PlayerUnlockedCard>(context));

        Assert.Equal(
            ["AcquiredAt", "PlayerId", "RelicDefinitionId", "RelicInstanceId"],
            PropertyNames<Relic>(context));
    }

    [Fact]
    public void AppliedModel_ShouldDeclareNoUniquenessConstraintOnRelicOwnership()
    {
        // RELIC_RULES.md §2.4 item 3: two distinct owned instances may reference
        // the same definition and be equipped together, so ownership carries no
        // uniqueness rule. A constraint added to make the starter grant
        // idempotent would forbid a documented legal state.
        using var context = CreateContext(nameof(AppliedModel_ShouldDeclareNoUniquenessConstraintOnRelicOwnership));

        var relic = context.Model.FindEntityType(typeof(Relic))!;

        // The only key is the instance identity (DATABASE.md §1).
        var keys = relic.GetKeys().Select(key => key.Properties.Select(p => p.Name).ToArray()).ToArray();

        Assert.Single(keys);
        Assert.Equal(["RelicInstanceId"], keys[0]);

        Assert.DoesNotContain(relic.GetIndexes(), index => index.IsUnique);
    }

    [Fact]
    public void AppliedModel_ShouldRetainTheDocumentedPlayerUnlockedCardCompositeKey()
    {
        // DATABASE.md §1: PlayerUnlockedCard's key is (PlayerId,
        // CardDefinitionId) — a database-level guard against a duplicated Card
        // unlock. It is retained, not weakened, and not replaced.
        using var context = CreateContext(nameof(AppliedModel_ShouldRetainTheDocumentedPlayerUnlockedCardCompositeKey));

        var unlockedCard = context.Model.FindEntityType(typeof(PlayerUnlockedCard))!;

        var key = Assert.Single(unlockedCard.GetKeys());

        var keyMembers = key.Properties
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["CardDefinitionId", "PlayerId"], keyMembers);
    }

    // -----------------------------------------------------------------------
    // No new persistence abstraction
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("IUnitOfWork")]
    [InlineData("UnitOfWork")]
    [InlineData("TransactionManager")]
    [InlineData("StarterOwnershipManager")]
    [InlineData("StarterOwnershipService")]
    [InlineData("UniversalRepository")]
    [InlineData("EventSourcingEngine")]
    public void InfrastructureAssembly_ShouldDeclareNoForbiddenAbstraction(string forbiddenTypeName)
    {
        // TASK-083 §9 / DATABASE.md §2 item 4: atomicity is achieved on the
        // existing scoped GameDbContext through one SaveChangesAsync. No
        // unit-of-work, transaction manager, universal repository, or event
        // sourcing engine is introduced — the one combined commit-scope surface
        // is a parameter on the existing Player-creation boundary.
        var declared = typeof(GameDbContext).Assembly
            .GetTypes()
            .Select(type => type.Name)
            .ToArray();

        Assert.DoesNotContain(forbiddenTypeName, declared);
    }

    [Fact]
    public void ApplicationAssembly_ShouldDeclareNoForbiddenAbstraction()
    {
        // The composition lives in the Application layer as one factory; it
        // introduces no manager, coordinator, or unit-of-work type of its own.
        var declared = typeof(PlayerStarterGrantFactory).Assembly
            .GetTypes()
            .Select(type => type.Name)
            .ToArray();

        foreach (var forbidden in new[]
        {
            "IUnitOfWork",
            "UnitOfWork",
            "TransactionManager",
            "StarterOwnershipManager",
            "StarterOwnershipService",
            "UniversalRepository",
        })
        {
            Assert.DoesNotContain(forbidden, declared);
        }
    }

    // -----------------------------------------------------------------------
    // The composition carries no duplicated content
    // -----------------------------------------------------------------------

    [Fact]
    public void StarterGrant_ShouldCarryNoDuplicatedContentValue()
    {
        // AGENTS.md §7 / GAME_STATE.md §0 item 5: an ownership row references its
        // definition; it never carries a second copy of the definition's content.
        // The composition therefore exposes definition identities and the two
        // documented creation values only.
        var ownershipTypes = new[]
        {
            typeof(Pet),
            typeof(PlayerUnlockedCard),
            typeof(Relic),
        };

        foreach (var ownershipType in ownershipTypes)
        {
            var members = ownershipType.GetProperties().Select(property => property.Name).ToArray();

            // No definition content member (name, cost, effect, trigger,
            // condition, element, category, identity) may appear on an ownership
            // row — those are read through the definition reference.
            foreach (var forbidden in new[]
            {
                "Name",
                "Identity",
                "Element",
                "Category",
                "PowerCost",
                "EffectDefinition",
                "Trigger",
                "Condition",
                "LoadoutCopyLimit",
                "PassiveId",
                "SignatureSkillCardId",
            })
            {
                Assert.DoesNotContain(forbidden, members);
            }
        }
    }

    private static string[] PropertyNames<TEntity>(GameDbContext context) =>
        context.Model
            .FindEntityType(typeof(TEntity))!
            .GetProperties()
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
}
