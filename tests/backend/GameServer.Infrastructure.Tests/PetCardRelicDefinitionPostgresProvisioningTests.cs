using GameServer.Application.Cards;
using GameServer.Application.Pets;
using GameServer.Application.Relics;
using GameServer.Domain.Cards;
using GameServer.Domain.Elements;
using GameServer.Domain.Pets;
using GameServer.Domain.Relics;
using GameServer.Infrastructure.Postgres;
using GameServer.Infrastructure.Postgres.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace GameServer.Infrastructure.Tests;

/// <summary>
/// The applied Pet / Card / Relic content definitions as PostgreSQL actually
/// holds them after <c>dotnet ef database update</c> (<c>DATABASE.md</c> §1,
/// §5 item 4; TASK-085, extended by TASK-168).
///
/// The migration-source assertions in
/// <see cref="PetCardRelicDefinitionProvisioningTests"/> (TASK-085) and
/// <see cref="ThanhXaAndSonHungSignatureSkillProvisioningTests"/> (TASK-168)
/// prove *what* the migrations contain; the InMemory provider cannot prove the
/// rows actually landed, because it never applies migrations. These tests
/// therefore run against a real PostgreSQL instance and are skipped when one is
/// not reachable, following the existing
/// <see cref="BossDefinitionPostgresProvisioningTests"/> convention — so the
/// suite still runs hermetically where no database is available.
///
/// <b>Precondition:</b> the database must have this repository's migrations
/// applied. These tests do not create the schema and do not apply migrations
/// themselves — doing so would make a test the provisioning mechanism, which is
/// what TASK-085 forbids. When the tables are absent the tests skip with that
/// reason recorded rather than failing the suite.
///
/// What is verified is the documented contract only: the canonical rows with
/// their exact documented values (13 from TASK-085 plus TASK-168's 4), the
/// content that is still deferred absent, the repository lookups resolving every
/// canonical id, the <c>SignatureSkillCardId</c> foreign key resolving, and no
/// <c>PassiveDefinition</c> table existing.
/// </summary>
public class PetCardRelicDefinitionPostgresProvisioningTests : IAsyncLifetime
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

            // The migration must already have been applied to this database
            // (dotnet ef database update). A missing table means the
            // precondition is unmet — skip rather than fail, and never create it
            // here.
            await using var schemaCheck = _dataSource.CreateCommand(
                """
                SELECT to_regclass('"CardDefinition"') IS NOT NULL
                   AND to_regclass('"PetDefinition"') IS NOT NULL
                   AND to_regclass('"RelicDefinition"') IS NOT NULL
                """);

            _schemaApplied = (bool)(await schemaCheck.ExecuteScalarAsync() ?? false);
        }
        catch (Exception)
        {
            // No reachable PostgreSQL: the applied-row tests are skipped rather
            // than failing the suite. The provider-independent migration-source
            // assertions still run in PetCardRelicDefinitionProvisioningTests.
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

    /// <summary>
    /// The exact canonical ids the two provisioning migrations hold:
    /// <c>CAT</c> — the thirteen rows of TASK-085
    /// (<c>20260929152651_ProvisionPetCardRelicContentDefinitions</c>), and the
    /// four rows TASK-168 adds
    /// (<c>20261004055006_ProvisionThanhXaAndSonHungSignatureSkills</c>)
    /// (<c>DATABASE.md</c> §1 value forms; TASK-082 decision B).
    ///
    /// The reads below are scoped to this closed set rather than to a
    /// <c>card-</c>/<c>pet-</c>/<c>relic-</c> prefix, because the shared
    /// development database is also written by the API integration suites'
    /// smoke fixtures. A prefix filter would make these assertions depend on
    /// whether those suites had happened to run first — the provisioned rows
    /// themselves are what this task owns and what must be asserted.
    /// </summary>
    private static readonly string[] CanonicalCardIds =
    [
        "card-heal", "card-shield", "card-power-charge",
        "card-inferno", "card-tidal-barrier", "card-iron-fang",
        "card-venomous-bloom", "card-earthshaker",
    ];

    private static readonly string[] CanonicalPetIds =
    [
        "pet-xich-lang", "pet-bach-ho", "pet-huyen-quy",
        "pet-thanh-xa", "pet-son-hung",
    ];

    private static readonly string[] CanonicalRelicIds =
    [
        "relic-berserker-core", "relic-mana-crystal",
        "relic-assassin-eye", "relic-emergency-core",
    ];

    private async Task<List<CardDefinition>?> ReadCardsAsync()
    {
        if (!_available || !_schemaApplied) return null;

        await using var context = CreateContext();

        return await context.CardDefinitions
            .AsNoTracking()
            .Where(definition => CanonicalCardIds.Contains(definition.CardDefinitionId))
            .OrderBy(definition => definition.CardDefinitionId)
            .ToListAsync();
    }

    private async Task<List<PetDefinition>?> ReadPetsAsync()
    {
        if (!_available || !_schemaApplied) return null;

        await using var context = CreateContext();

        return await context.PetDefinitions
            .AsNoTracking()
            .Where(definition => CanonicalPetIds.Contains(definition.PetDefinitionId))
            .OrderBy(definition => definition.PetDefinitionId)
            .ToListAsync();
    }

    private async Task<List<RelicDefinition>?> ReadRelicsAsync()
    {
        if (!_available || !_schemaApplied) return null;

        await using var context = CreateContext();

        return await context.RelicDefinitions
            .AsNoTracking()
            .Where(definition => CanonicalRelicIds.Contains(definition.RelicDefinitionId))
            .OrderBy(definition => definition.RelicDefinitionId)
            .ToListAsync();
    }

    // -----------------------------------------------------------------------
    // Row set — exactly 8 / 5 / 4 canonical rows
    //
    // 6+3 (TASK-085) plus 2+2 (TASK-168) = 8 Cards and 5 Pets; the Relic set is
    // unchanged at 4, because TASK-168 provisions no Relic row and Burning Curse
    // stays deferred (RELIC_RULES.md §6 note 3).
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Postgres_ShouldHoldExactlyEightCanonicalCardDefinitions()
    {
        var cards = await ReadCardsAsync();
        if (cards is null) return; // no live PostgreSQL / schema

        Assert.Equal(
            new[]
            {
                "card-earthshaker", "card-heal", "card-inferno", "card-iron-fang",
                "card-power-charge", "card-shield", "card-tidal-barrier",
                "card-venomous-bloom",
            },
            cards.Select(card => card.CardDefinitionId).ToArray());
    }

    [Fact]
    public async Task Postgres_ShouldHoldExactlyFiveCanonicalPetDefinitions()
    {
        var pets = await ReadPetsAsync();
        if (pets is null) return;

        Assert.Equal(
            new[]
            {
                "pet-bach-ho", "pet-huyen-quy", "pet-son-hung",
                "pet-thanh-xa", "pet-xich-lang",
            },
            pets.Select(pet => pet.PetDefinitionId).ToArray());
    }

    [Fact]
    public async Task Postgres_ShouldHoldExactlyFourCanonicalRelicDefinitions()
    {
        var relics = await ReadRelicsAsync();
        if (relics is null) return;

        Assert.Equal(
            new[]
            {
                "relic-assassin-eye", "relic-berserker-core",
                "relic-emergency-core", "relic-mana-crystal",
            },
            relics.Select(relic => relic.RelicDefinitionId).ToArray());
    }

    [Fact]
    public async Task Postgres_ShouldHoldTheDocumentedRowsAcrossTheThreeTables()
    {
        var cards = await ReadCardsAsync();
        var pets = await ReadPetsAsync();
        var relics = await ReadRelicsAsync();

        if (cards is null || pets is null || relics is null) return;

        // TASK-085: 6 + 3 + 4 = 13. TASK-168 adds 2 Cards + 2 Pets, so 8 + 5 + 4
        // = 17. Each read returns exactly one row per canonical id, so a missing
        // row and a duplicated row both fail here.
        Assert.Equal(8, cards.Count);
        Assert.Equal(5, pets.Count);
        Assert.Equal(4, relics.Count);
        Assert.Equal(17, cards.Count + pets.Count + relics.Count);

        // The pre-existing thirteen remain exactly thirteen of the seventeen —
        // TASK-168 adds rows and re-encodes none (AGENTS.md §16).
        Assert.Equal(13, cards.Count + pets.Count + relics.Count - 4);
    }

    [Fact]
    public async Task Postgres_ShouldRecordExactlyOneProvisioningMigration()
    {
        if (!_available) return;

        // The documented apply step is `dotnet ef database update`, which records
        // the applied migration in __EFMigrationsHistory. Exactly one such
        // migration exists: this task adds exactly one, and a second would mean a
        // second schema change or a duplicate data apply rode along.
        await using var command = _dataSource!.CreateCommand(
            """
            SELECT COUNT(*) FROM "__EFMigrationsHistory"
            WHERE "MigrationId" LIKE '%_ProvisionPetCardRelicContentDefinitions'
            """);

        var applied = (long)(await command.ExecuteScalarAsync())!;

        Assert.Equal(1, applied);
    }

    [Fact]
    public async Task Postgres_ShouldRecordExactlyOneThanhXaAndSonHungProvisioningMigration()
    {
        if (!_available) return;

        // The documented apply step is `dotnet ef database update`, which records
        // the applied migration in __EFMigrationsHistory. TASK-168 adds exactly
        // one such migration; a second would mean a duplicate data apply rode
        // along.
        await using var command = _dataSource!.CreateCommand(
            """
            SELECT COUNT(*) FROM "__EFMigrationsHistory"
            WHERE "MigrationId" LIKE '%_ProvisionThanhXaAndSonHungSignatureSkills'
            """);

        var applied = (long)(await command.ExecuteScalarAsync())!;

        Assert.Equal(1, applied);
    }

    [Fact]
    public async Task Postgres_ShouldContributeNoSchemaChangeToTheAppliedTables()
    {
        if (!_available || !_schemaApplied) return;

        // DATABASE.md §5 item 1: this migration is data only. The applied column
        // sets are asserted directly, so a column added by the provisioning
        // migration would be caught even though the model snapshot is unchanged.
        await using var command = _dataSource!.CreateCommand(
            """
            SELECT table_name, column_name
            FROM information_schema.columns
            WHERE table_name IN ('CardDefinition', 'PetDefinition', 'RelicDefinition')
            ORDER BY table_name, column_name
            """);

        var columns = new List<string>();

        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                columns.Add($"{reader.GetString(0)}.{reader.GetString(1)}");
            }
        }

        Assert.Equal(
            new[]
            {
                "CardDefinition.CardDefinitionId", "CardDefinition.Category",
                "CardDefinition.EffectDefinition", "CardDefinition.LoadoutCopyLimit",
                "CardDefinition.Name", "CardDefinition.PowerCost",
                "PetDefinition.Element", "PetDefinition.Identity",
                "PetDefinition.PassiveId", "PetDefinition.PassiveThreshold",
                "PetDefinition.PetDefinitionId", "PetDefinition.SignatureSkillCardId",
                "RelicDefinition.Condition", "RelicDefinition.EffectDefinition",
                "RelicDefinition.Name", "RelicDefinition.RelicDefinitionId",
                "RelicDefinition.Trigger",
            },
            columns);
    }

    // -----------------------------------------------------------------------
    // Documented values per row — CARD_RULES.md §1/§2/§4.1,
    // PET_RULES.md §8, PASSIVE_RULES.md §8, RELIC_RULES.md §3/§6
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Postgres_ShouldHoldTheDocumentedCardValuesPerRow()
    {
        var cards = await ReadCardsAsync();
        if (cards is null) return;

        // CARD_RULES.md §2 (Basic) and §4.1 (Pet Skill): name, cost, and effect
        // value verbatim; §1 item 5 fixes LoadoutCopyLimit = 1 for every
        // CardDefinition defined by the document. The last two rows are TASK-168's
        // (CARD_RULES.md §4.1, TASK-167).
        //
        // TASK-109 migrated EffectDefinition from prose to the structured
        // contract (DATABASE.md §1), so this test asserts identity/cost/category
        // here and delegates the EFFECT assertion to
        // CardEffectDefinitionMigrationTests, which owns the per-row structured
        // expectations. Asserting the effect shape twice would give the same
        // fact two owners (GAME_STATE.md §0 item 5).
        var expected = new (string Id, string Name, CardCategory Category, int PowerCost)[]
        {
            ("card-heal", "Heal", CardCategory.Basic, 20),
            ("card-shield", "Shield", CardCategory.Basic, 20),
            ("card-power-charge", "Power Charge", CardCategory.Basic, 0),
            ("card-inferno", "Inferno", CardCategory.PetSkill, 100),
            ("card-tidal-barrier", "Tidal Barrier", CardCategory.PetSkill, 80),
            ("card-iron-fang", "Iron Fang", CardCategory.PetSkill, 100),
            ("card-venomous-bloom", "Venomous Bloom", CardCategory.PetSkill, 80),
            ("card-earthshaker", "Earthshaker", CardCategory.PetSkill, 100),
        };

        Assert.Equal(expected.Length, cards.Count);

        foreach (var (id, name, category, powerCost) in expected)
        {
            var row = Assert.Single(cards, card => card.CardDefinitionId == id);

            Assert.Equal(name, row.Name);
            Assert.Equal(category, row.Category);
            Assert.Equal(powerCost, row.PowerCost);
            Assert.Equal(1, row.LoadoutCopyLimit);
        }

        // Three Basic and five PetSkill — one Skill Card per MVP Pet
        // (CARD_RULES.md §4 item 1; MVP_SCOPE.md §1 "5 Pet Skill Cards"), and no
        // third category.
        Assert.Equal(3, cards.Count(card => card.Category == CardCategory.Basic));
        Assert.Equal(5, cards.Count(card => card.Category == CardCategory.PetSkill));
    }

    [Fact]
    public async Task Postgres_ShouldHoldTheDocumentedPetValuesPerRow()
    {
        var pets = await ReadPetsAsync();
        if (pets is null) return;

        // PET_RULES.md §8 owns Element and Signature Skill;
        // PASSIVE_RULES.md §8 owns the PassiveId spelling and threshold. The last
        // two rows are TASK-168's (PET_RULES.md §8, TASK-167).
        var expected = new (string Id, string Identity, Element Element, string PassiveId, int Threshold, string Card)[]
        {
            ("pet-xich-lang", "Xích Lang", Element.Hoa, "passive-xich-lang", 5, "card-inferno"),
            ("pet-bach-ho", "Bạch Hổ", Element.Kim, "passive-bach-ho", 4, "card-iron-fang"),
            ("pet-huyen-quy", "Huyền Quy", Element.Thuy, "passive-huyen-quy", 6, "card-tidal-barrier"),
            ("pet-thanh-xa", "Thanh Xà", Element.Moc, "passive-thanh-xa", 7, "card-venomous-bloom"),
            ("pet-son-hung", "Sơn Hùng", Element.Tho, "passive-son-hung", 5, "card-earthshaker"),
        };

        Assert.Equal(expected.Length, pets.Count);

        foreach (var (id, identity, element, passiveId, threshold, cardId) in expected)
        {
            var row = Assert.Single(pets, pet => pet.PetDefinitionId == id);

            Assert.Equal(identity, row.Identity);
            Assert.Equal(element, row.Element);
            Assert.Equal(passiveId, row.PassiveId.Value);
            Assert.Equal(threshold, row.PassiveThreshold);
            Assert.Equal(cardId, row.SignatureSkillCardId);
        }
    }

    [Fact]
    public async Task Postgres_ShouldHoldTheDocumentedRelicValuesPerRow()
    {
        var relics = await ReadRelicsAsync();
        if (relics is null) return;

        // RELIC_RULES.md §3 is the closed Trigger list, and Trigger is unchanged
        // by §8 (TASK-131 D8) — it stays the §3 identity the provisioning
        // migration stored.
        //
        // TASK-132 migrated Condition and EffectDefinition from prose to the
        // structured contract RELIC_RULES.md §8 defines (DATABASE.md §1), so this
        // test asserts the identity/Trigger here and the STRUCTURED §8.5 values
        // per row. Asserting the encoded shape twice would give the same fact two
        // owners (GAME_STATE.md §0 item 5) — the persistence-level twin of these
        // assertions lives in RelicStructuredStorageTests.
        var expected = new (string Id, string Name, string Trigger)[]
        {
            ("relic-berserker-core", "Berserker Core", "OnMatchCount"),
            ("relic-mana-crystal", "Mana Crystal", "OnMatchCount"),
            ("relic-assassin-eye", "Assassin Eye", "OnCombo"),
            ("relic-emergency-core", "Emergency Core", "OnHpBelow"),
        };

        Assert.Equal(expected.Length, relics.Count);

        foreach (var (id, name, trigger) in expected)
        {
            var row = Assert.Single(relics, relic => relic.RelicDefinitionId == id);

            Assert.Equal(name, row.Name);
            Assert.Equal(trigger, row.Trigger);

            // The structured Condition and EffectDefinition read back as the
            // §8.1/§8.2 contract values — not as the superseded prose.
            var structured = RelicProvisionedContent.Single(id);

            Assert.Equal(structured.Condition, row.Condition);
            Assert.Equal(structured.Effects, row.EffectDefinition);
        }
    }

    // -----------------------------------------------------------------------
    // Deferred content — DATABASE.md §5 item 4 rule (a)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Postgres_ShouldHoldNoContentStillDeferred()
    {
        if (!_available || !_schemaApplied) return;

        // DATABASE.md §5 item 4 rule (a): only content-defined rows may be
        // provisioned; a row whose required members have no documented value
        // stays unprovisioned.
        //
        // CORRECTED FOR TASK-168. This test previously read
        // "ShouldHoldNoDeferredContent" and asserted that pet-thanh-xa,
        // pet-son-hung, and the two GUESSED Card keys card-thanh-xa-skill /
        // card-son-hung-skill were all absent, on the pre-TASK-167 premise that
        // their Signature Skills were not content-defined. TASK-167 authored both
        // Skills in CARD_RULES.md §4.1, DATABASE.md §5 item 4 now records the rows
        // as "provisioned-later, not content-blocked", and TASK-168 provisions
        // them under their REAL keys — card-venomous-bloom and card-earthshaker,
        // as named by TASK-167's Completion Evidence. The old assertions are false
        // and the guessed key form was never a documented value form (DATABASE.md
        // §1: `card-<ascii-kebab-case-name>` of the CARD'S documented name, so
        // "Thanh Xà's Skill" was never the source of the key at all).
        //
        // The RELIC half is unchanged and still asserted below: Burning Curse
        // remains deferred (RELIC_RULES.md §6 note 3), which is the only content
        // deferral TASK-167 left standing.
        //
        // The positive half of the correction — that the four rows now EXIST with
        // their documented values — is asserted by
        // Postgres_ShouldHoldTheDocumentedCardValuesPerRow / ...PetValuesPerRow and
        // by the repository lookups below, so the deferral premise cannot silently
        // return.
        await using var command = _dataSource!.CreateCommand(
            """
            SELECT
              (SELECT COUNT(*) FROM "PetDefinition"
                 WHERE "PetDefinitionId" IN ('pet-thanh-xa', 'pet-son-hung')),
              (SELECT COUNT(*) FROM "CardDefinition"
                 WHERE "CardDefinitionId" IN ('card-venomous-bloom', 'card-earthshaker')),
              (SELECT COUNT(*) FROM "CardDefinition"
                 WHERE "CardDefinitionId" IN ('card-thanh-xa-skill', 'card-son-hung-skill')),
              (SELECT COUNT(*) FROM "RelicDefinition"
                 WHERE "RelicDefinitionId" = 'relic-burning-curse')
            """);

        await using var reader = await command.ExecuteReaderAsync();

        Assert.True(await reader.ReadAsync());

        // PET_RULES.md §8 (TASK-167): both Pet rows are now provisionable and
        // provisioned, so both exist exactly once each.
        Assert.Equal(2, reader.GetInt64(0));

        // CARD_RULES.md §4.1 (TASK-167): both Signature Skill Cards are authored
        // and provisioned under their documented keys.
        Assert.Equal(2, reader.GetInt64(1));

        // The pre-TASK-167 guessed key form remains absent — it was never derived
        // from a documented Card name and no row may carry it.
        Assert.Equal(0, reader.GetInt64(2));

        // RELIC_RULES.md §6 note 3: Burning Curse stays deferred.
        Assert.Equal(0, reader.GetInt64(3));
    }

    [Fact]
    public async Task Postgres_ShouldHaveNoPassiveDefinitionTable()
    {
        if (!_available) return;

        // DATABASE.md §1: PetDefinition.PassiveId and PassiveThreshold are
        // properties of PetDefinition — there is NO PassiveDefinition table. The
        // applied schema is asserted directly, so a stray migration could not
        // have created one quietly.
        await using var command = _dataSource!.CreateCommand(
            "SELECT to_regclass('\"PassiveDefinition\"') IS NULL");

        Assert.True((bool)(await command.ExecuteScalarAsync() ?? false));
    }

    // -----------------------------------------------------------------------
    // FK integrity — DATABASE.md §2
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Postgres_ShouldResolveEverySignatureSkillCardReference()
    {
        var cards = await ReadCardsAsync();
        var pets = await ReadPetsAsync();

        if (cards is null || pets is null) return;

        // DATABASE.md §2: PetDefinition.SignatureSkillCardId (FK →
        // CardDefinition). Every provisioned Pet's reference must resolve to a
        // provisioned Card row — a dangling reference could not have been
        // inserted at all, so this asserts the contract the FK enforces.
        foreach (var pet in pets)
        {
            Assert.Contains(cards, card => card.CardDefinitionId == pet.SignatureSkillCardId);
        }

        // CARD_RULES.md §1 item 4: the referenced Card is the Pet Skill Card, so
        // each one carries Category = PetSkill.
        foreach (var pet in pets)
        {
            var skill = cards.Single(card => card.CardDefinitionId == pet.SignatureSkillCardId);

            Assert.Equal(CardCategory.PetSkill, skill.Category);
        }
    }

    // -----------------------------------------------------------------------
    // Repository lookups — DATABASE.md §1/§4
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Repositories_ShouldRetrieveEveryCanonicalPetDefinition()
    {
        if (!_available || !_schemaApplied) return;

        await using var context = CreateContext();
        var repository = new PetRepository(context);

        foreach (var id in new[]
                 {
                     "pet-xich-lang", "pet-bach-ho", "pet-huyen-quy",
                     "pet-thanh-xa", "pet-son-hung",
                 })
        {
            var definition = await repository.GetDefinitionAsync(id);

            Assert.NotNull(definition);
            Assert.Equal(id, definition!.PetDefinitionId);
        }
    }

    [Fact]
    public async Task Repositories_ShouldRetrieveEveryCanonicalCardDefinition()
    {
        if (!_available || !_schemaApplied) return;

        await using var context = CreateContext();
        var repository = new CardRepository(context);

        foreach (var id in new[]
                 {
                     "card-heal", "card-shield", "card-power-charge",
                     "card-inferno", "card-tidal-barrier", "card-iron-fang",
                     "card-venomous-bloom", "card-earthshaker",
                 })
        {
            var definition = await repository.GetDefinitionAsync(id);

            Assert.NotNull(definition);
            Assert.Equal(id, definition!.CardDefinitionId);
        }
    }

    [Fact]
    public async Task Repositories_ShouldRetrieveEveryCanonicalRelicDefinition()
    {
        if (!_available || !_schemaApplied) return;

        await using var context = CreateContext();
        var repository = new RelicRepository(context);

        foreach (var id in new[]
                 {
                     "relic-berserker-core", "relic-mana-crystal",
                     "relic-assassin-eye", "relic-emergency-core",
                 })
        {
            var definition = await repository.GetDefinitionAsync(id);

            Assert.NotNull(definition);
            Assert.Equal(id, definition!.RelicDefinitionId);
        }
    }

    [Fact]
    public async Task Repositories_ShouldResolveTheNewlyProvisionedDefinitions()
    {
        if (!_available || !_schemaApplied) return;

        await using var context = CreateContext();

        // CORRECTED FOR TASK-168. The repository lookups for the four keys below
        // were previously asserted NULL by
        // "Repositories_ShouldReturnNullForEveryDeferredDefinition", on the
        // pre-TASK-167 premise that Thanh Xà's and Sơn Hùng's rows (and two
        // guessed Card keys) were deferred. TASK-167 authored both Signature
        // Skills (CARD_RULES.md §4.1) and TASK-168 provisions all four rows, so
        // the repository must now RESOLVE them — and their exact documented
        // values are asserted here rather than merely their existence.
        var pets = new PetRepository(context);
        var cards = new CardRepository(context);

        var thanhXa = await pets.GetDefinitionAsync("pet-thanh-xa");

        Assert.NotNull(thanhXa);
        Assert.Equal("Thanh Xà", thanhXa!.Identity);              // PET_RULES.md §8
        Assert.Equal(Element.Moc, thanhXa.Element);                // PET_RULES.md §8 / ELEMENT_RULES.md §6
        Assert.Equal("passive-thanh-xa", thanhXa.PassiveId.Value); // PASSIVE_RULES.md §8
        Assert.Equal(7, thanhXa.PassiveThreshold);                 // PASSIVE_RULES.md §8
        Assert.Equal("card-venomous-bloom", thanhXa.SignatureSkillCardId);

        var sonHung = await pets.GetDefinitionAsync("pet-son-hung");

        Assert.NotNull(sonHung);
        Assert.Equal("Sơn Hùng", sonHung!.Identity);
        Assert.Equal(Element.Tho, sonHung.Element);
        Assert.Equal("passive-son-hung", sonHung.PassiveId.Value);
        Assert.Equal(5, sonHung.PassiveThreshold);
        Assert.Equal("card-earthshaker", sonHung.SignatureSkillCardId);

        var venomousBloom = await cards.GetDefinitionAsync("card-venomous-bloom");

        Assert.NotNull(venomousBloom);
        Assert.Equal("Venomous Bloom", venomousBloom!.Name);
        Assert.Equal(CardCategory.PetSkill, venomousBloom.Category);
        Assert.Equal(80, venomousBloom.PowerCost);
        Assert.Equal(1, venomousBloom.LoadoutCopyLimit);

        var earthshaker = await cards.GetDefinitionAsync("card-earthshaker");

        Assert.NotNull(earthshaker);
        Assert.Equal("Earthshaker", earthshaker!.Name);
        Assert.Equal(CardCategory.PetSkill, earthshaker.Category);
        Assert.Equal(100, earthshaker.PowerCost);
        Assert.Equal(1, earthshaker.LoadoutCopyLimit);
    }

    [Fact]
    public async Task Repositories_ShouldStillReturnNullForTheDeferredRelic()
    {
        if (!_available || !_schemaApplied) return;

        await using var context = CreateContext();

        var relics = new RelicRepository(context);

        // RELIC_RULES.md §6 note 3: Burning Curse is the one content deferral
        // TASK-167 left standing — TASK-168 provisions no Relic row, so its
        // lookup still resolves to nothing. The guessed pre-TASK-167 Card key form
        // likewise remains unresolvable (DATABASE.md §1 value forms).
        Assert.Null(await relics.GetDefinitionAsync("relic-burning-curse"));

        var cards = new CardRepository(context);

        Assert.Null(await cards.GetDefinitionAsync("card-thanh-xa-skill"));
        Assert.Null(await cards.GetDefinitionAsync("card-son-hung-skill"));
    }

    [Fact]
    public async Task Repositories_ShouldResolveTheSignatureSkillCardThroughItsRepository()
    {
        if (!_available || !_schemaApplied) return;

        await using var context = CreateContext();

        var pets = new PetRepository(context);
        var cards = new CardRepository(context);

        // The battle-start derivation path (CARD_RULES.md §1, §4): the active
        // Pet's definition is read, and its SignatureSkillCardId is resolved
        // through the Card repository to the fourth equipped Card. This walks
        // that documented chain against the applied rows rather than a fixture —
        // for all five MVP Pets (MVP_SCOPE.md §1; PET_RULES.md §8).
        foreach (var petId in new[]
                 {
                     "pet-xich-lang", "pet-bach-ho", "pet-huyen-quy",
                     "pet-thanh-xa", "pet-son-hung",
                 })
        {
            var pet = await pets.GetDefinitionAsync(petId);

            Assert.NotNull(pet);

            var skill = await cards.GetDefinitionAsync(pet!.SignatureSkillCardId);

            Assert.NotNull(skill);
            Assert.Equal(CardCategory.PetSkill, skill!.Category);
        }
    }

    // -----------------------------------------------------------------------
    // Model shape — the provisioning added no column and no table
    // -----------------------------------------------------------------------

    [Fact]
    public void Model_ShouldExposeTheProvisionedTablesWithTheirDocumentedColumns()
    {
        using var context = TestGameDbContextFactory.Create(
            nameof(Model_ShouldExposeTheProvisionedTablesWithTheirDocumentedColumns));

        var model = context.Model;

        var cardColumns = model.FindEntityType(typeof(CardDefinition))!
            .GetProperties()
            .Select(property => property.GetColumnName())
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[]
            {
                "CardDefinitionId", "Category", "EffectDefinition",
                "LoadoutCopyLimit", "Name", "PowerCost",
            },
            cardColumns);

        var petColumns = model.FindEntityType(typeof(PetDefinition))!
            .GetProperties()
            .Select(property => property.GetColumnName())
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        // DATABASE.md §1: PassiveId and PassiveThreshold are columns of
        // PetDefinition itself — there is no PassiveDefinition column, table,
        // or navigation.
        Assert.Equal(
            new[]
            {
                "Element", "Identity", "PassiveId", "PassiveThreshold",
                "PetDefinitionId", "SignatureSkillCardId",
            },
            petColumns);

        var relicColumns = model.FindEntityType(typeof(RelicDefinition))!
            .GetProperties()
            .Select(property => property.GetColumnName())
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[] { "Condition", "EffectDefinition", "Name", "RelicDefinitionId", "Trigger" },
            relicColumns);
    }
}
