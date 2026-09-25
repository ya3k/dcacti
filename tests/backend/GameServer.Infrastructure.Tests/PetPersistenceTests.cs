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
/// PetDefinition field set, the documented check constraints, the
/// <c>petLevelMultiplier &gt; 0</c> constraint, the single
/// <c>Pet(PlayerId)</c> index, and the absence of any XP or Evolution
/// column (ADR-012 items 5–6).
///
/// <c>SignatureSkillCardId</c> is deliberately absent: CardDefinition does
/// not exist until TASK-028 (TASK-024 Scope; <c>AGENTS.md</c> §7).
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
        // PetDefinitionId (FK), Tier, Star, Level, and AcquiredAt. The field
        // list is closed: no XP column, no Evolution field, no combat stats.
        var fields = typeof(Pet)
            .GetProperties()
            .Select(p => p.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[] { "AcquiredAt", "Level", "PetDefinitionId", "PetInstanceId", "PlayerId", "Star", "Tier" },
            fields);
    }

    [Fact]
    public void Pet_ShouldExposeNoXpOrEvolutionField()
    {
        // ADR-012 items 5–6: no XP column, no Evolution field, no Tier/Star
        // derivation from Player Level. DATABASE.md §1 defines none.
        var fields = typeof(Pet).GetProperties().Select(p => p.Name).ToArray();

        foreach (var forbidden in new[] { "XP", "Xp", "Experience", "Evolution", "Evolves", "NextLevelXP" })
        {
            Assert.DoesNotContain(forbidden, fields);
        }
    }

    [Fact]
    public void Pet_ShouldExposeNoCombatAuthorityFields()
    {
        // DATABASE.md §3 / ADR-011 item 5: no combat-stat columns on Pet —
        // HP/ATK/DEF/Crit/Power are battle-time PetState values
        // (GAME_STATE.md §2.3).
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
        // PetLevelMultiplier, PassiveDefinition (threshold/effect reference),
        // SignatureSkillCardId (FK → CardDefinition).
        //
        // SignatureSkillCardId was intentionally absent while CardDefinition
        // did not exist (TASK-024 Scope); TASK-028 completes that deferral, so
        // the field is now part of the documented set and is asserted here.
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
                "PetLevelMultiplier",
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

        Assert.Contains(Player.MinLevel.ToString(), check.Sql);
        Assert.Contains(Player.MaxLevel.ToString(), check.Sql);
    }

    [Fact]
    public void Model_ShouldConstrainPetLevelMultiplierToBePositive()
    {
        // DATABASE.md §3: PetDefinition.PetLevelMultiplier > 0 (decimal)
        // (PET_RULES.md §5). Zero and negative multipliers are illegal.
        var model = CreateDesignTimeModel(nameof(Model_ShouldConstrainPetLevelMultiplierToBePositive));

        var entity = model.FindEntityType(typeof(PetDefinition))!;
        var check = entity.GetCheckConstraints()
            .Single(c => c.Name == "CK_PetDefinition_PetLevelMultiplier_Positive");

        Assert.Contains(">", check.Sql);
        Assert.Contains("0", check.Sql);
    }

    [Fact]
    public void Model_ShouldStorePetLevelMultiplierAsADecimal()
    {
        // DATABASE.md §1/§3: PetLevelMultiplier is a decimal > 0 — the type
        // is part of the documented contract, not a float approximation.
        var model = CreateDesignTimeModel(nameof(Model_ShouldStorePetLevelMultiplierAsADecimal));

        var entity = model.FindEntityType(typeof(PetDefinition))!;
        var property = entity.FindProperty(nameof(PetDefinition.PetLevelMultiplier))!;

        Assert.Equal(typeof(decimal), property.ClrType);
        Assert.True(property.IsNullable == false);
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
    // PET_RULES.md §5 — Level is a denormalized snapshot
    // -----------------------------------------------------------------------

    [Fact]
    public void PetLevel_ShouldBeWritableForRecompute()
    {
        // ADR-012 Consequences: Pet.Level is a denormalized snapshot that is
        // rewritten when Player Level or the multiplier changes. The setter
        // exists solely for that recompute path — it is not an XP API.
        var pet = new Pet
        {
            PetInstanceId = "pet_1",
            PlayerId = "player_1",
            PetDefinitionId = "def_1",
            Tier = PetTier.Common,
            Star = 1,
            Level = 1,
            AcquiredAt = DateTimeOffset.UtcNow,
        };

        pet.Level = 10;

        Assert.Equal(10, pet.Level);
    }
}
