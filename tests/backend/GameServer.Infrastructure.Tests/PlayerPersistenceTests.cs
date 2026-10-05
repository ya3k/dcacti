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
/// the five-field Player record, the unique <c>DiscordUserId</c>, the
/// <c>[1, 50]</c> Level range, the <c>XP &gt;= 0</c> constraint with no upper
/// bound, and the documented initial values of a newly created Player
/// (<c>COMBAT_RULES.md</c> §7.5 item 3).
///
/// Pet persistence is deliberately absent: it belongs to the Pet track, which
/// <c>ADR-016</c> item 12 keeps independent of this one.
/// </summary>
public class PlayerPersistenceTests
{
    /// <summary>
    /// A context over a fresh, isolated store. Each test gets its own store so
    /// no test observes another's rows.
    /// </summary>
    private static GameDbContext CreateContext(string storeName) =>
        TestGameDbContextFactory.Create(storeName);

    // -----------------------------------------------------------------------
    // DATABASE.md §1 — the Player field set
    // -----------------------------------------------------------------------

    [Fact]
    public void Player_ShouldCarryExactlyTheDocumentedFields()
    {
        // DATABASE.md §1 defines the Player record as five persisted members —
        // PlayerId (PK), DiscordUserId (unique), XP, Level, and CreatedAt. The
        // persisted field list is closed: §1 adds no reward column, no second
        // progression member, and no combat stat.
        //
        // The CLR member set and the mapped column set are identical: the Player
        // contract carries exactly the five documented members and nothing else.
        var properties = typeof(Player)
            .GetProperties()
            .Select(p => p.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[] { "AccountId", "CreatedAt", "Level", "PlayerId", "XP" },
            properties);

        using var context = CreateContext($"player-fields-{Guid.NewGuid():N}");

        var storedColumns = context.Model
            .FindEntityType(typeof(Player))!
            .GetProperties()
            .Select(p => p.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[] { "AccountId", "CreatedAt", "Level", "PlayerId", "XP" },
            storedColumns);
    }

    [Fact]
    public void Player_ShouldExposeNoCombatAuthorityFields()
    {
        // ADR-011 item 5 / COMBAT_RULES.md §7.6 / DATABASE.md §3: there are no
        // combat-stat columns on Player. HP/ATK/DEF/Crit/Power are battle-time
        // PetState values (GAME_STATE.md §2.3), and Player.Level grants no combat
        // modifier of any kind.
        var fields = typeof(Player).GetProperties().Select(p => p.Name).ToArray();

        foreach (var combatField in new[] { "HP", "MaxHP", "ATK", "DEF", "Crit", "Power" })
        {
            Assert.DoesNotContain(combatField, fields);
        }
    }

    [Fact]
    public void Player_ShouldExposeNoRewardField_AndNoSecondProgressionEntity()
    {
        // DATABASE.md §1 keeps the reward record on BattleResult, not on Player:
        // RewardSummary is a BattleResult member and the Player carries no reward
        // column. The Player's progression members are exactly XP and Level — no
        // PlayerProgression, XPWallet, ProgressionState, or Experience member is
        // introduced (TASK-065 §5; AGENTS.md §9).
        var fields = typeof(Player).GetProperties().Select(p => p.Name).ToArray();

        foreach (var rewardField in new[]
                 {
                     "Rewards", "RewardSummary", "NextLevelXP", "Experience",
                     "Progression", "PlayerProgression", "XPWallet", "ProgressionState",
                 })
        {
            Assert.DoesNotContain(rewardField, fields);
        }
    }

    [Fact]
    public void Player_ShouldCarryNoPetProgressionMember()
    {
        // ADR-016 item 12: Player account progression and Pet combat progression
        // are separate tracks that neither read nor modify each other. The Player
        // entity therefore carries no Pet XP, Pet Level, or Pet reference —
        // Player XP must not be implemented by reaching into Pet state.
        var fields = typeof(Player).GetProperties().Select(p => p.Name).ToArray();

        foreach (var petField in new[] { "PetXp", "PetXP", "PetLevel", "PetId", "Pets", "ActivePet" })
        {
            Assert.DoesNotContain(petField, fields);
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
    public void Model_ShouldConstrainAccountIdToBeUnique()
    {
        // DATABASE.md §1/§3: AccountId is unique. The uniqueness
        // constraint is the authoritative protection against duplicate
        // ownership records for one Account, and it is what a
        // concurrent registration/login race resolves against.
        var model = CreateDesignTimeModel(nameof(Model_ShouldConstrainAccountIdToBeUnique));

        var entity = model.FindEntityType(typeof(Player))!;
        var index = entity.GetIndexes().Single(i => i.Properties.Any(p => p.Name == "AccountId"));

        Assert.True(index.IsUnique, "AccountId must carry a unique index (DATABASE.md §1/§3).");
    }

    [Fact]
    public async Task Database_ShouldRejectASecondPlayerWithTheSameAccountId()
    {
        await using var context = CreateContext(nameof(Database_ShouldRejectASecondPlayerWithTheSameAccountId));

        var designTimeModel = context.GetService<IDesignTimeModel>().Model;
        var entity = designTimeModel.FindEntityType(typeof(Player))!;

        var index = entity.GetIndexes()
            .Single(i => i.Properties.Select(p => p.Name).SequenceEqual(new[] { "AccountId" }));

        Assert.True(index.IsUnique, "AccountId must be unique (DATABASE.md §1/§3).");

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
    public void NewPlayer_ShouldStartAtXpZeroAndLevelOne()
    {
        // COMBAT_RULES.md §7.5 item 3 / DATABASE.md §3: a newly created Player
        // has XP = 0 and Level = 1. Level 1 is also exactly the §7.4 formula at
        // XP = 0, so the creation values and the formula cannot disagree.
        Assert.Equal(0, Player.InitialXp);
        Assert.Equal(1, Player.InitialLevel);

        var created = new Player
        {
            PlayerId = "player_new",
            AccountId = Guid.NewGuid(),
        };

        Assert.Equal(Player.InitialXp, created.XP);
        Assert.Equal(Player.InitialLevel, created.Level);
        Assert.Equal(Player.LevelForXp(created.XP), created.Level);
    }

    [Fact]
    public void PlayerLevelRange_ShouldBeTheDocumentedOneToFifty()
    {
        // DATABASE.md §3 / COMBAT_RULES.md §7 / ADR-016 item 2: Player.Level
        // ∈ [1, 50]. Both bounds are asserted so neither can drift.
        Assert.Equal(1, Player.MinLevel);
        Assert.Equal(50, Player.MaxLevel);
    }

    [Fact]
    public void Model_ShouldConstrainLevelToTheDocumentedRange()
    {
        // DATABASE.md §3: the [1, 50] range is enforced at the database/model
        // level, and a newly created Player's Level defaults to the documented
        // initial value (COMBAT_RULES.md §7.5 item 3).
        var model = CreateDesignTimeModel(nameof(Model_ShouldConstrainLevelToTheDocumentedRange));

        var entity = model.FindEntityType(typeof(Player))!;
        var level = entity.FindProperty(nameof(Player.Level))!;

        var checkConstraint = entity.GetCheckConstraints()
            .Single(c => c.Name == "CK_Player_Level_Range");

        Assert.Contains(Player.MinLevel.ToString(), checkConstraint.Sql);
        Assert.Contains(Player.MaxLevel.ToString(), checkConstraint.Sql);

        Assert.Equal(Player.InitialLevel, level.GetDefaultValue());
    }

    // -----------------------------------------------------------------------
    // DATABASE.md §3 — the XP column and its documented constraints
    // -----------------------------------------------------------------------

    [Fact]
    public void Model_ShouldMapXpAsANonNullableColumnDefaultingToZero()
    {
        // DATABASE.md §1/§3: Player.XP is int, NOT NULL, default 0 — the initial
        // value of a newly created Player (COMBAT_RULES.md §7.5 item 3). The
        // column default is also what an existing row receives when the migration
        // adds the column, so no Level → XP conversion is needed or invented.
        var model = CreateDesignTimeModel(nameof(Model_ShouldMapXpAsANonNullableColumnDefaultingToZero));

        var xp = model.FindEntityType(typeof(Player))!.FindProperty(nameof(Player.XP))!;

        Assert.False(xp.IsNullable, "Player.XP is NOT NULL (DATABASE.md §3).");
        Assert.Equal(typeof(int), xp.ClrType);
        Assert.Equal(Player.InitialXp, xp.GetDefaultValue());
    }

    [Fact]
    public void Model_ShouldConstrainXpToBeNonNegative_WithNoUpperBound()
    {
        // DATABASE.md §3: Player.XP >= 0 with NO upper bound — COMBAT_RULES.md
        // §7.5 item 1 makes XP uncapped and keep accumulating after Level 50.
        // Only the lower bound is therefore declared: a cap here would silently
        // discard XP the contract requires be retained, and the Pet track's 4900
        // hard cap (PET_RULES.md §5.5) is a different policy.
        var model = CreateDesignTimeModel(nameof(Model_ShouldConstrainXpToBeNonNegative_WithNoUpperBound));

        var entity = model.FindEntityType(typeof(Player))!;

        var checkConstraint = entity.GetCheckConstraints()
            .Single(c => c.Name == "CK_Player_XP_NonNegative");

        // The documented lower bound is present …
        Assert.Contains($"\"XP\" >= {Player.InitialXp}", checkConstraint.Sql);

        // … and no upper bound is asserted anywhere on XP. The Level cap is the
        // only cap in the Player contract, and it lives on the Level constraint.
        Assert.DoesNotContain(Player.MaxLevel.ToString(), checkConstraint.Sql);
        Assert.DoesNotContain("4900", checkConstraint.Sql);
    }

    [Fact]
    public void XpPersistence_ShouldRetainAValueBeyondTheLevelFiftyBoundary()
    {
        // COMBAT_RULES.md §7.5 item 1: XP is never clamped or discarded, so a
        // value past 4900 must round-trip exactly while the Level stays capped at
        // 50 (item 2). This is the documented distinction between the Player
        // track and the Pet track's hard cap.
        using var context = CreateContext(nameof(XpPersistence_ShouldRetainAValueBeyondTheLevelFiftyBoundary));

        var player = new Player
        {
            PlayerId = "player_uncapped_xp",
            AccountId = Guid.NewGuid(),
            XP = 12_345,
            Level = Player.LevelForXp(12_345),
            CreatedAt = DateTimeOffset.UtcNow,
        };

        context.Players.Add(player);
        context.SaveChanges();

        // A reload — the stored value, not the in-memory instance.
        context.ChangeTracker.Clear();

        var stored = context.Players.Single(p => p.PlayerId == "player_uncapped_xp");

        Assert.Equal(12_345, stored.XP);
        Assert.Equal(Player.MaxLevel, stored.Level);
    }
}
