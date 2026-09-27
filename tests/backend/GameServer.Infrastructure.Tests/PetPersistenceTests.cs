using GameServer.Domain.Elements;
using GameServer.Domain.Passives;
using GameServer.Domain.Pets;
using GameServer.Domain.Players;
using GameServer.Infrastructure.Postgres;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace GameServer.Infrastructure.Tests;

/// <summary>
/// Pet and PetDefinition persistence — <c>DATABASE.md</c> §1, §3, §4.
///
/// What is verified is the documented contract: the Pet field set, the
/// PetDefinition field set, the documented check constraints, the single
/// <c>Pet(PlayerId)</c> index, and the continuing absence of any Evolution
/// field (ADR-012 items 5–6).
///
/// <b>Pet XP is now part of the documented field set.</b> <c>DATABASE.md</c> §1
/// and <c>PET_RULES.md</c> §5.1 item 1 place <c>XP</c> on the owned Pet row, so
/// these assertions allow it. <b>Evolution and the retired
/// <c>PetLevelMultiplier</c> remain forbidden</b> — the field list is still
/// closed, and enabling XP must not have opened it to anything else.
///
/// <c>SignatureSkillCardId</c> is deliberately present on PetDefinition:
/// CardDefinition exists and TASK-028 completed that deferral (TASK-024 Scope;
/// <c>AGENTS.md</c> §7).
/// </summary>
public class PetPersistenceTests
{
    private static GameDbContext CreateContext(string storeName) =>
        TestGameDbContextFactory.Create(storeName);

    private static IModel CreateDesignTimeModel(string storeName)
    {
        using var context = CreateContext(storeName);
        return context.GetService<IDesignTimeModel>().Model;
    }

    // -----------------------------------------------------------------------
    // DATABASE.md §1 — the Pet field set
    // -----------------------------------------------------------------------

    [Fact]
    public void Pet_ShouldCarryExactlyTheDocumentedFields()
    {
        // DATABASE.md §1 defines Pet as PetInstanceId (PK), PlayerId (FK),
        // PetDefinitionId (FK), Tier, Star, XP, Level, and AcquiredAt. The field
        // list is closed: no Evolution field, no combat stats, and no retired
        // multiplier.
        var fields = typeof(Pet)
            .GetProperties()
            .Select(p => p.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[] { "AcquiredAt", "Level", "PetDefinitionId", "PetInstanceId", "PlayerId", "Star", "Tier", "XP" },
            fields);
    }

    [Fact]
    public void Pet_ShouldExposeXp_ButStillNoEvolutionOrRetiredMultiplierField()
    {
        // DATABASE.md §1 / PET_RULES.md §5.1 item 1: XP IS a documented Pet
        // member, on the owned instance rather than on PetDefinition or Player.
        var fields = typeof(Pet).GetProperties().Select(p => p.Name).ToArray();

        Assert.Contains("XP", fields);

        // The rest of the closed list is unchanged by that: ADR-012 items 5–6
        // forbid an Evolution field, and PET_RULES.md §5.6 item 1 / ADR-016 item
        // 13 retired PetLevelMultiplier. Enabling XP must not have opened the
        // field list to a second progression source or a resurrected one.
        foreach (var forbidden in new[]
                 {
                     "Experience", "Evolution", "Evolves", "NextLevelXP",
                     "PetLevelMultiplier", "LevelMultiplier", "PetLevelDerivation",
                 })
        {
            Assert.DoesNotContain(forbidden, fields);
        }
    }

    [Fact]
    public void Pet_ShouldExposeNoCombatAuthorityFields()
    {
        // DATABASE.md §3 / ADR-011 item 5: no combat-stat columns on Pet —
        // HP/ATK/DEF/Crit/Power are battle-time PetState values
        // (GAME_STATE.md §2.3). PET_RULES.md §5.7 item 1: Pet XP grants no combat
        // stats, so adding XP must not have added one either.
        var fields = typeof(Pet).GetProperties().Select(p => p.Name).ToArray();

        foreach (var combatField in new[] { "HP", "MaxHP", "ATK", "DEF", "Crit", "Power" })
        {
            Assert.DoesNotContain(combatField, fields);
        }
    }

    // -----------------------------------------------------------------------
    // DATABASE.md §1 — the PetDefinition field set
    // -----------------------------------------------------------------------

    [Fact]
    public void PetDefinition_ShouldCarryExactlyTheDocumentedFields()
    {
        // DATABASE.md §1: PetDefinitionId (PK), Identity, Element,
        // PassiveDefinition (threshold/effect reference),
        // SignatureSkillCardId (FK → CardDefinition).
        //
        // SignatureSkillCardId was intentionally absent while CardDefinition
        // did not exist (TASK-024 Scope); TASK-028 completes that deferral, so
        // the field is now part of the documented set and is asserted here.
        //
        // PetLevelMultiplier is absent because it was retired with the
        // Player.Level × PetLevelMultiplier derivation (PET_RULES.md §5.6
        // item 1, ADR-016 item 13) and removed from the documented field set
        // (DATABASE.md §1).
        var fields = typeof(PetDefinition)
            .GetProperties()
            .Select(p => p.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[]
            {
                "Element",
                "Identity",
                "PassiveId",
                "PassiveThreshold",
                "PetDefinitionId",
                "SignatureSkillCardId",
            },
            fields);
    }

    // -----------------------------------------------------------------------
    // DATABASE.md §1 / §3 — the persistence mapping and its constraints
    // -----------------------------------------------------------------------

    [Fact]
    public void Model_ShouldMapPetToTheDocumentedTable()
    {
        var model = CreateDesignTimeModel(nameof(Model_ShouldMapPetToTheDocumentedTable));

        var entity = model.FindEntityType(typeof(Pet));

        Assert.NotNull(entity);
        Assert.Equal("Pet", entity!.GetTableName());
        Assert.Equal("PetInstanceId", entity.FindPrimaryKey()!.Properties.Single().Name);
    }

    [Fact]
    public void Model_ShouldMapPetDefinitionToTheDocumentedTable()
    {
        var model = CreateDesignTimeModel(nameof(Model_ShouldMapPetDefinitionToTheDocumentedTable));

        var entity = model.FindEntityType(typeof(PetDefinition));

        Assert.NotNull(entity);
        Assert.Equal("PetDefinition", entity!.GetTableName());
        Assert.Equal(
            "PetDefinitionId",
            entity.FindPrimaryKey()!.Properties.Single().Name);
    }

    [Fact]
    public void Model_ShouldConstrainStarToTheDocumentedOneToFiveRange()
    {
        // DATABASE.md §3: Pet.Star ∈ [1, 5] (PET_RULES.md §4).
        var model = CreateDesignTimeModel(nameof(Model_ShouldConstrainStarToTheDocumentedOneToFiveRange));

        var entity = model.FindEntityType(typeof(Pet))!;
        var check = entity.GetCheckConstraints()
            .Single(c => c.Name == "CK_Pet_Star_Range");

        Assert.Contains(Pet.MinStar.ToString(), check.Sql);
        Assert.Contains(Pet.MaxStar.ToString(), check.Sql);
        Assert.Equal(1, Pet.MinStar);
        Assert.Equal(5, Pet.MaxStar);
    }

    [Fact]
    public void Model_ShouldConstrainLevelToTheDocumentedOneToFiftyRange()
    {
        // DATABASE.md §3: Pet.Level ∈ [1, 50] (PET_RULES.md §5).
        var model = CreateDesignTimeModel(nameof(Model_ShouldConstrainLevelToTheDocumentedOneToFiftyRange));

        var entity = model.FindEntityType(typeof(Pet))!;
        var check = entity.GetCheckConstraints()
            .Single(c => c.Name == "CK_Pet_Level_Range");

        // The Pet range is read from the Pet track's own constants
        // (PET_RULES.md §5.5 item 4, ADR-016 item 12: independent tracks).
        Assert.Contains(Pet.MinLevel.ToString(), check.Sql);
        Assert.Contains(Pet.MaxLevel.ToString(), check.Sql);
    }

    [Fact]
    public void Model_ShouldMapPetXp_RequiredWithTheDocumentedDefault()
    {
        // DATABASE.md §1/§3: Pet.XP is int, NOT NULL, default 0 (PET_RULES.md
        // §5.2's initial value). The column default is what gives an existing row
        // the documented 0 — no Level → XP conversion exists in any document.
        var model = CreateDesignTimeModel(nameof(Model_ShouldMapPetXp_RequiredWithTheDocumentedDefault));

        var entity = model.FindEntityType(typeof(Pet))!;
        var xp = entity.FindProperty(nameof(Pet.XP))!;

        Assert.False(xp.IsNullable);
        Assert.Equal(Pet.InitialXp, xp.GetDefaultValue());
        Assert.Equal(0, Pet.InitialXp);
    }

    [Fact]
    public void Model_ShouldConstrainXpToTheDocumentedZeroToFortyNineHundredRange()
    {
        // DATABASE.md §3: Pet.XP ∈ [0, 4900], a HARD cap (PET_RULES.md §5.5 item
        // 1 — Pet XP stops at 4900 and no overflow is retained). Both bounds are
        // asserted, because §5.5 item 4 keeps the XP cap and the Level cap as
        // separate documented facts.
        var model = CreateDesignTimeModel(nameof(Model_ShouldConstrainXpToTheDocumentedZeroToFortyNineHundredRange));

        var entity = model.FindEntityType(typeof(Pet))!;
        var check = entity.GetCheckConstraints()
            .Single(c => c.Name == "CK_Pet_XP_Range");

        Assert.Contains(Pet.InitialXp.ToString(), check.Sql);
        Assert.Contains(Pet.MaxXp.ToString(), check.Sql);
        Assert.Equal(4900, Pet.MaxXp);

        // The Pet cap is emphatically not the Player policy: the Player column is
        // uncapped (COMBAT_RULES.md §7.5 item 1), so its constraint must carry no
        // ceiling.
        var playerXp = model.FindEntityType(typeof(Player))!
            .GetCheckConstraints()
            .Single(c => c.Name == "CK_Player_XP_NonNegative");

        Assert.DoesNotContain(Pet.MaxXp.ToString(), playerXp.Sql);
    }

    [Fact]
    public void Model_ShouldRetainBothExistingPetCheckConstraints()
    {
        // DATABASE.md §3: adding the XP bound must not have dropped or widened the
        // two constraints the Pet table already carried.
        var model = CreateDesignTimeModel(nameof(Model_ShouldRetainBothExistingPetCheckConstraints));

        var entity = model.FindEntityType(typeof(Pet))!;
        var constraintNames = entity.GetCheckConstraints()
            .Select(constraint => constraint.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[] { "CK_Pet_Level_Range", "CK_Pet_Star_Range", "CK_Pet_XP_Range" },
            constraintNames);
    }

    [Fact]
    public void PetDefinition_ShouldStillCarryNoRetiredMultiplier()
    {
        // PET_RULES.md §5.6 items 1–2 / ADR-016 item 13: PetLevelMultiplier is
        // retired, and PetDefinition never owns instance XP or Level. Enabling
        // Pet.XP must not have moved progression onto the definition row.
        var model = CreateDesignTimeModel(nameof(PetDefinition_ShouldStillCarryNoRetiredMultiplier));

        var entity = model.FindEntityType(typeof(PetDefinition))!;

        Assert.Null(entity.FindProperty("PetLevelMultiplier"));
        Assert.Null(entity.FindProperty("XP"));
        Assert.Null(entity.FindProperty("Level"));
    }

    [Fact]
    public void Model_ShouldDeclareExactlyTheOneDocumentedPetIndex()
    {
        // DATABASE.md §4: Pet(PlayerId) is the only Pet index. No further
        // indexes are specified; additional indexes require a real query
        // pattern (anti-overengineering).
        var model = CreateDesignTimeModel(nameof(Model_ShouldDeclareExactlyTheOneDocumentedPetIndex));

        var entity = model.FindEntityType(typeof(Pet))!;
        var indexes = entity.GetIndexes().ToArray();

        var playerIdIndex = indexes
            .Single(i => i.Properties.Any(p => p.Name == nameof(Pet.PlayerId)));

        Assert.False(playerIdIndex.IsUnique, "Pet(PlayerId) is a non-unique collection index.");
        Assert.Equal(1, indexes.Length);
    }

    [Fact]
    public void Model_ShouldCreateForeignKeysForPlayerAndDefinition()
    {
        // DATABASE.md §2: Player 1 ── N Pet; Pet N ── 1 PetDefinition.
        var model = CreateDesignTimeModel(nameof(Model_ShouldCreateForeignKeysForPlayerAndDefinition));

        var entity = model.FindEntityType(typeof(Pet))!;
        var foreignKeys = entity.GetForeignKeys().ToArray();

        Assert.Equal(2, foreignKeys.Length);
        Assert.Contains(foreignKeys, fk =>
            fk.PrincipalEntityType?.ClrType == typeof(Player));
        Assert.Contains(foreignKeys, fk =>
            fk.PrincipalEntityType?.ClrType == typeof(PetDefinition));
    }

    // -----------------------------------------------------------------------
    // PET_RULES.md §5 — Level is derived from this Pet's own XP
    // -----------------------------------------------------------------------

    [Fact]
    public void PetLevel_ShouldBeWritableForTheProgressionPath()
    {
        // DATABASE.md §1/§3 make Pet.Level a PERSISTED column rather than a
        // computed member, so the progression path must be able to assign it
        // (PET_RULES.md §5.4). The retired Player.Level × PetLevelMultiplier
        // recompute pass is gone (ADR-016 item 13); the setter now serves the
        // XP-derived write, and the grant is the only production caller.
        var pet = new Pet
        {
            PetInstanceId = "pet_1",
            PlayerId = "player_1",
            PetDefinitionId = "def_1",
            Tier = PetTier.Common,
            Star = 1,
            XP = Pet.InitialXp,
            Level = Pet.InitialLevel,
            AcquiredAt = DateTimeOffset.UtcNow,
        };

        // A newly constructed Pet holds the documented initial pair, which the
        // §5.4 formula agrees with by construction (PET_RULES.md §5.2).
        Assert.Equal(Pet.InitialXp, pet.XP);
        Assert.Equal(Pet.LevelForXp(pet.XP), pet.Level);

        pet.Level = 10;

        Assert.Equal(10, pet.Level);
    }

    [Fact]
    public void GrantBattleXp_ShouldLeaveXpAndLevelConsistentOnTheEntity()
    {
        // PET_RULES.md §5.4: the grant re-derives Level from the resulting XP, so
        // the two persisted columns can never be left disagreeing by the one
        // production writer.
        var pet = new Pet
        {
            PetInstanceId = "pet_grant",
            PlayerId = "player_1",
            PetDefinitionId = "def_1",
            Tier = PetTier.Common,
            Star = 1,
            AcquiredAt = DateTimeOffset.UtcNow,
        };

        pet.GrantBattleXp(Pet.BattleWonXpReward);

        Assert.Equal(100, pet.XP);
        Assert.Equal(Pet.LevelForXp(pet.XP), pet.Level);
        Assert.Equal(2, pet.Level);
    }
}
