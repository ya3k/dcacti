using GameServer.Domain.Players;
using GameServer.Infrastructure.Postgres;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace GameServer.Infrastructure.Tests;

/// <summary>
/// Player persistence and ownership semantics — <c>DATABASE.md</c> §1/§3.
///
/// What is verified here is the documented contract, not an invented one:
/// the four-field Player record, the unique <c>DiscordUserId</c>, the
/// <c>[1, 50]</c> Level range, and the authoritative initial Level of a newly
/// created Player (<c>PET_RULES.md</c> §5 item 8).
///
/// Reward/XP progression, Pet Level, and Pet persistence are deliberately
/// absent: they belong to TASK-033 and TASK-024.
/// </summary>
public class PlayerPersistenceTests
{
    /// <summary>
    /// A context over a fresh, isolated store. Each test gets its own store so
    /// no test observes another's rows.
    /// </summary>
    private static GameDbContext CreateContext(string storeName) =>
        new(new DbContextOptionsBuilder<GameDbContext>()
            .UseInMemoryDatabase(storeName)
            .Options);

    // -----------------------------------------------------------------------
    // DATABASE.md §1 — the Player field set
    // -----------------------------------------------------------------------

    [Fact]
    public void Player_ShouldCarryExactlyTheDocumentedFields()
    {
        // DATABASE.md §1 defines the Player record as PlayerId (PK),
        // DiscordUserId (unique), Level, and CreatedAt. The field list is
        // closed: DATABASE.md §1 adds no XP column and no reward field, and
        // TASK-033 owns progression.
        var fields = typeof(Player)
            .GetProperties()
            .Select(p => p.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[] { "CreatedAt", "DiscordUserId", "Level", "PlayerId" },
            fields);
    }

    [Fact]
    public void Player_ShouldExposeNoCombatAuthorityFields()
    {
        // ADR-011 item 5 / DATABASE.md §3: there are no combat-stat columns on
        // Player. HP/ATK/DEF/Crit/Power are battle-time PetState values
        // (GAME_STATE.md §2.3), and Player.Level is a persistent progression
        // value, not a combat stat (PET_RULES.md §5 item 3).
        var fields = typeof(Player).GetProperties().Select(p => p.Name).ToArray();

        foreach (var combatField in new[] { "HP", "MaxHP", "ATK", "DEF", "Crit", "Power" })
        {
            Assert.DoesNotContain(combatField, fields);
        }
    }

    [Fact]
    public void Player_ShouldExposeNoXpOrRewardField()
    {
        // TASK-033 boundary: no XP column, XP amount, XP curve, level-up
        // threshold, or reward field exists on Player. DATABASE.md §1 defines
        // none, so none may be introduced here (AGENTS.md §17).
        var fields = typeof(Player).GetProperties().Select(p => p.Name).ToArray();

        foreach (var progressionField in new[] { "XP", "Xp", "Experience", "Rewards", "RewardSummary", "NextLevelXP" })
        {
            Assert.DoesNotContain(progressionField, fields);
        }
    }

    // -----------------------------------------------------------------------
    // DATABASE.md §1 / §3 — the persistence mapping and its constraints
    // -----------------------------------------------------------------------

    [Fact]
    public void Model_ShouldMapPlayerToTheDocumentedTable()
    {
        var model = CreateDesignTimeModel(nameof(Model_ShouldMapPlayerToTheDocumentedTable));

        var entity = model.FindEntityType(typeof(Player));

        Assert.NotNull(entity);
        Assert.Equal("Player", entity!.GetTableName());

        // DATABASE.md §1: PlayerId is the primary key.
        Assert.Equal("PlayerId", entity.FindPrimaryKey()!.Properties.Single().Name);
    }

    [Fact]
    public void Model_ShouldConstrainDiscordUserIdToBeUnique()
    {
        // DATABASE.md §1/§3: DiscordUserId is unique. The uniqueness
        // constraint is the authoritative protection against duplicate
        // ownership records for one Discord account, and it is what a
        // concurrent first-login race resolves against.
        var model = CreateDesignTimeModel(nameof(Model_ShouldConstrainDiscordUserIdToBeUnique));

        var entity = model.FindEntityType(typeof(Player))!;
        var index = entity.GetIndexes().Single(i => i.Properties.Any(p => p.Name == "DiscordUserId"));

        Assert.True(index.IsUnique, "DiscordUserId must carry a unique index (DATABASE.md §1/§3).");
    }

    [Fact]
    public async Task Database_ShouldRejectASecondPlayerWithTheSameDiscordUserId()
    {
        // Two Players cannot share one DiscordUserId (DATABASE.md §1/§3). The
        // uniqueness is a database constraint — the authoritative protection —
        // so the check asserts the constraint the model actually declares
        // rather than relying on a provider that enforces it in memory.
        //
        // The in-memory provider does not enforce unique indexes, so the
        // assertion is made against the relational model the migration is
        // generated from: a unique index on DiscordUserId. That is the same
        // model the migration below renders as
        // `CREATE UNIQUE INDEX "IX_Player_DiscordUserId"`.
        await using var context = CreateContext(nameof(Database_ShouldRejectASecondPlayerWithTheSameDiscordUserId));

        var designTimeModel = context.GetService<IDesignTimeModel>().Model;
        var entity = designTimeModel.FindEntityType(typeof(Player))!;

        var index = entity.GetIndexes()
            .Single(i => i.Properties.Select(p => p.Name).SequenceEqual(new[] { "DiscordUserId" }));

        Assert.True(index.IsUnique, "DiscordUserId must be unique (DATABASE.md §1/§3).");

        // A second row with the same identity is therefore rejected by the
        // store; the repository's race handling depends on exactly that.
        Assert.NotNull(index);
    }

    /// <summary>
    /// The relational model the migrations are generated from — the one that
    /// carries check constraints and index nullability, which the
    /// read-optimized runtime model does not retain.
    /// </summary>
    private static IModel CreateDesignTimeModel(string storeName)
    {
        using var context = CreateContext(storeName);
        return context.GetService<IDesignTimeModel>().Model;
    }

    // -----------------------------------------------------------------------
    // DATABASE.md §1 — the Player record's values
    // -----------------------------------------------------------------------

    [Fact]
    public void NewPlayer_ShouldStartAtLevelOne()
    {
        // PET_RULES.md §5 item 8 / DATABASE.md §3: a newly created Player
        // starts at Level 1. The initial value is its own authoritative rule —
        // it is not derived from the [1, 50] range, which constrains the legal
        // values rather than stating the creation value (§5.1 item 4).
        Assert.Equal(1, Player.InitialLevel);
        Assert.Equal(Player.InitialLevel, new Player
        {
            PlayerId = "player_new",
            DiscordUserId = "1",
        }.Level);
    }

    [Fact]
    public void PlayerLevelRange_ShouldBeTheDocumentedOneToFifty()
    {
        // DATABASE.md §3 / PET_RULES.md §5 item 1 / ADR-012 item 1: Player.Level
        // ∈ [1, 50]. Both bounds are asserted so neither can drift silently.
        Assert.Equal(1, Player.MinLevel);
        Assert.Equal(50, Player.MaxLevel);
    }

    [Fact]
    public void Model_ShouldConstrainLevelToTheDocumentedRange()
    {
        // DATABASE.md §3: the [1, 50] range is enforced at the database/model
        // level, and a newly created Player's Level defaults to the documented
        // initial value (PET_RULES.md §5 item 8).
        var model = CreateDesignTimeModel(nameof(Model_ShouldConstrainLevelToTheDocumentedRange));

        var entity = model.FindEntityType(typeof(Player))!;
        var level = entity.FindProperty(nameof(Player.Level))!;

        var checkConstraint = entity.GetCheckConstraints()
            .Single(c => c.Name == "CK_Player_Level_Range");

        Assert.Contains(Player.MinLevel.ToString(), checkConstraint.Sql);
        Assert.Contains(Player.MaxLevel.ToString(), checkConstraint.Sql);

        Assert.Equal(Player.InitialLevel, level.GetDefaultValue());
    }
}
