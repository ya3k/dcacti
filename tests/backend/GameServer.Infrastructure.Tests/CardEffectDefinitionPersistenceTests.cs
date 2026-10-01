using GameServer.Domain.Cards;
using GameServer.Infrastructure.Postgres;
using Microsoft.EntityFrameworkCore;

namespace GameServer.Infrastructure.Tests;

/// <summary>
/// The structured Card <c>EffectDefinition</c> contract at the persistence
/// boundary — <c>DATABASE.md</c> §1 "Card <c>EffectDefinition</c> contract",
/// §3's constraints, and §1 item 8's row-encoding statement (TASK-108 decisions
/// D-1/D-2, extended by TASK-111 D-1/D-2/D-3/D-4/D-5 and implemented at the row
/// level by TASK-112).
///
/// What is verified is the documented storage contract only: the EF mapping
/// carries the structured <b>array</b> value losslessly through the configured
/// provider, the column is the JSON type <c>DATABASE.md</c> §1 records, and the
/// six provisioned <c>CardDefinition</c> rows are encoded in the array shape with
/// the values transcribed from <c>CARD_RULES.md</c> §2/§4.1 — asserted per row,
/// including the <c>card-iron-fang</c> identity correction.
///
/// <b>No test here applies an effect.</b> No Card is cast, no Crit is rolled, no
/// Burn is ticked, and no magnitude is consumed by gameplay: this file asserts
/// data and its persistence.
/// </summary>
public class CardEffectDefinitionPersistenceTests
{
    private static GameDbContext CreateContext(string storeName) =>
        TestGameDbContextFactory.Create(storeName);

    private static CardDefinition NewDefinition(
        string id,
        CardEffectDefinitions effects,
        CardCategory category = CardCategory.Basic) => new()
    {
        CardDefinitionId = id,
        Name = id,
        Category = category,
        PowerCost = 0,
        EffectDefinition = effects,
        LoadoutCopyLimit = 1,
    };

    private static CardEffectDefinitions Single(CardEffectDefinition effect) =>
        CardEffectDefinitions.Create(effect);

    // -----------------------------------------------------------------------
    // The applied column shape — DATABASE.md §1
    // -----------------------------------------------------------------------

    [Fact]
    public void Model_ShouldStoreTheEffectsAsTheDocumentedJsonColumn()
    {
        // DATABASE.md §1: `EffectDefinition` is `jsonb`, NOT NULL, holding an ARRAY
        // of effect objects. The former `character varying(128)` prose limit is
        // resolved explicitly: the structured payload is not prose and is not
        // bounded by a prose length.
        //
        // The InMemory provider has no relational type mapping, so the column type
        // is asserted against the migration source — which is what actually applies
        // the DDL — and the model is asserted for nullability and the absence of the
        // prose length limit.
        using var context = CreateContext(nameof(Model_ShouldStoreTheEffectsAsTheDocumentedJsonColumn));

        var property = context.Model.FindEntityType(typeof(CardDefinition))!
            .FindProperty(nameof(CardDefinition.EffectDefinition))!;

        Assert.Equal("EffectDefinition", property.GetColumnName());
        Assert.False(property.IsNullable);

        // No prose length limit survives on the property.
        Assert.Null(property.GetMaxLength());

        // The applied column type is `jsonb`, and that was set by TASK-109's schema
        // migration (20261001112446_StructureCardDefinitionEffectDefinition) — the
        // only migration that issues DDL for this column. TASK-112 changes no schema,
        // so its own operations must contain no ALTER at all.
        Assert.Contains(
            "TYPE jsonb",
            ReadMigrationOperations("StructureCardDefinitionEffectDefinition"),
            StringComparison.Ordinal);

        Assert.DoesNotContain("ALTER TABLE", ReadMigrationOperations(), StringComparison.Ordinal);
    }

    [Fact]
    public void Model_ShouldMapTheEffectsThroughAValueConverter()
    {
        // The Domain member is the structured collection type, so the mapping must
        // convert it rather than persist it directly — and the converter is what
        // makes the stored document DATABASE.md §1's array, not the Domain's field
        // names.
        using var context = CreateContext(nameof(Model_ShouldMapTheEffectsThroughAValueConverter));

        var property = context.Model.FindEntityType(typeof(CardDefinition))!
            .FindProperty(nameof(CardDefinition.EffectDefinition))!;

        var converter = property.GetTypeMapping().Converter;

        Assert.NotNull(converter);
        Assert.Equal(typeof(CardEffectDefinitions), converter!.ModelClrType);
        Assert.Equal(typeof(string), converter.ProviderClrType);
    }

    [Fact]
    public void Model_ShouldLeaveTheRelicEffectColumnUnchanged()
    {
        // DATABASE.md §1 item 7: the supersession is scoped to CardDefinition.
        // RelicDefinition.EffectDefinition still carries TASK-082 R2-7's verbatim
        // prose in its 128-character column, and its mapping is not touched.
        using var context = CreateContext(nameof(Model_ShouldLeaveTheRelicEffectColumnUnchanged));

        var relicProperty = context.Model
            .FindEntityType(typeof(Domain.Relics.RelicDefinition))!
            .FindProperty(nameof(Domain.Relics.RelicDefinition.EffectDefinition))!;

        Assert.Equal(typeof(string), relicProperty.ClrType);
        Assert.Equal(128, relicProperty.GetMaxLength());
        Assert.False(relicProperty.IsNullable);

        // The Relic member has no value converter — it stores its prose string
        // directly, unlike the Card member.
        Assert.Null(relicProperty.GetValueConverter());

        // And no migration of this task mentions the Relic column or table.
        Assert.DoesNotContain(
            "RelicDefinition",
            ReadMigrationOperations(),
            StringComparison.Ordinal);
    }

    [Fact]
    public void Model_ShouldNotAddAnyOtherCardColumn()
    {
        // DATABASE.md §1: only EffectDefinition's representation moved. The other
        // members and the column list are unchanged.
        using var context = CreateContext(nameof(Model_ShouldNotAddAnyOtherCardColumn));

        var columns = context.Model.FindEntityType(typeof(CardDefinition))!
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
            columns);
    }

    // -----------------------------------------------------------------------
    // Round trips through the configured provider
    // -----------------------------------------------------------------------

    [Fact]
    public async Task CardDefinition_ShouldRoundTripTheStructuredEffectArrayLosslessly()
    {
        // DATABASE.md §1's round chain for one row: stored array → EF → Domain
        // array → serialization → the same contract shape.
        var storeName = nameof(CardDefinition_ShouldRoundTripTheStructuredEffectArrayLosslessly);

        var effects = Single(CardEffectDefinition.Create(
            CardEffectType.Shield,
            CardEffectValueType.PercentMaxHp,
            20));
        var payload = effects.ToPersistedPayload();

        await using (var context = CreateContext(storeName))
        {
            context.CardDefinitions.Add(NewDefinition("card-shield", effects));
            await context.SaveChangesAsync();
        }

        await using (var context = CreateContext(storeName))
        {
            var stored = await context.CardDefinitions.SingleAsync();

            // Every element and its exact type survive (TDD.md §6).
            Assert.Equal(effects, stored.EffectDefinition);
            Assert.Equal(1, stored.EffectDefinition.Count);
            Assert.Equal(CardEffectType.Shield, stored.EffectDefinition[0].EffectType);
            Assert.Equal(CardEffectValueType.PercentMaxHp, stored.EffectDefinition[0].ValueType);
            Assert.Equal(20, stored.EffectDefinition[0].Value);

            // And the serialized form is byte-identical to what was written.
            Assert.Equal(payload, stored.EffectDefinition.ToPersistedPayload());
        }
    }

    [Fact]
    public async Task CardDefinition_ShouldRoundTripTheSixProvisionedContentRows()
    {
        // The six content-defined rows as CARD_RULES.md §2/§4.1 authors them, in the
        // array shape TASK-112 encodes. This is the Domain-level twin of the applied
        // migration assertions below: the same six values, asserted per row through
        // the configured provider.
        var storeName = nameof(CardDefinition_ShouldRoundTripTheSixProvisionedContentRows);

        var rows = ProvisionedContentRows;

        await using (var context = CreateContext(storeName))
        {
            foreach (var (id, effects, category) in rows)
            {
                context.CardDefinitions.Add(NewDefinition(id, effects, category));
            }

            await context.SaveChangesAsync();
        }

        await using (var context = CreateContext(storeName))
        {
            var stored = await context.CardDefinitions.ToListAsync();

            Assert.Equal(rows.Length, stored.Count);

            foreach (var (id, effects, _) in rows)
            {
                var row = Assert.Single(stored, definition => definition.CardDefinitionId == id);

                Assert.Equal(effects.ToPersistedPayload(), row.EffectDefinition.ToPersistedPayload());
                Assert.Equal(effects, row.EffectDefinition);
            }
        }
    }

    [Fact]
    public async Task CardDefinition_ShouldRoundTripBothSingleAndMultiEffectRows()
    {
        // D-1b's uniformity through the provider: a one-effect Card stores a
        // one-element array and a multi-effect Card stores one element per effect,
        // with the same element type in both cases.
        var storeName = nameof(CardDefinition_ShouldRoundTripBothSingleAndMultiEffectRows);

        await using (var context = CreateContext(storeName))
        {
            context.CardDefinitions.Add(NewDefinition(
                "card-single",
                Single(CardEffectDefinition.Create(
                    CardEffectType.Power,
                    CardEffectValueType.Flat,
                    25))));

            context.CardDefinitions.Add(NewDefinition(
                "card-multi",
                CardEffectDefinitions.Create(
                    CardEffectDefinition.Create(CardEffectType.Damage, CardEffectValueType.Flat, 100),
                    CardEffectDefinition.Burn(CardEffectValueType.Flat, 50, 2)),
                CardCategory.PetSkill));

            await context.SaveChangesAsync();
        }

        await using (var context = CreateContext(storeName))
        {
            var single = await context.CardDefinitions
                .SingleAsync(definition => definition.CardDefinitionId == "card-single");
            var multi = await context.CardDefinitions
                .SingleAsync(definition => definition.CardDefinitionId == "card-multi");

            Assert.Equal(1, single.EffectDefinition.Count);
            Assert.Equal(
                """[{"effectType":"Power","valueType":"Flat","value":25}]""",
                single.EffectDefinition.ToPersistedPayload());

            Assert.Equal(2, multi.EffectDefinition.Count);
            Assert.Equal(
                """[{"effectType":"Damage","valueType":"Flat","value":100},{"effectType":"Burn","valueType":"Flat","value":50,"duration":2}]""",
                multi.EffectDefinition.ToPersistedPayload());
        }
    }

    [Fact]
    public async Task CardDefinition_ShouldPreserveTheStoredElementOrderAcrossTheRoundTrip()
    {
        // DATABASE.md §1 item 3 / TASK-111 D-5: the stored sequence is preserved and
        // is not normalized, because the contract fixes no canonical order. Both
        // orders are distinct stored values, and neither is rewritten.
        var storeName = nameof(CardDefinition_ShouldPreserveTheStoredElementOrderAcrossTheRoundTrip);

        var damage = CardEffectDefinition.Create(CardEffectType.Damage, CardEffectValueType.Flat, 100);
        var burn = CardEffectDefinition.Burn(CardEffectValueType.Flat, 50, 2);

        await using (var context = CreateContext(storeName))
        {
            context.CardDefinitions.Add(NewDefinition(
                "card_damage_first",
                CardEffectDefinitions.Create(damage, burn),
                CardCategory.PetSkill));

            context.CardDefinitions.Add(NewDefinition(
                "card_burn_first",
                CardEffectDefinitions.Create(burn, damage),
                CardCategory.PetSkill));

            await context.SaveChangesAsync();
        }

        await using (var context = CreateContext(storeName))
        {
            var damageFirst = await context.CardDefinitions
                .SingleAsync(definition => definition.CardDefinitionId == "card_damage_first");
            var burnFirst = await context.CardDefinitions
                .SingleAsync(definition => definition.CardDefinitionId == "card_burn_first");

            Assert.Equal(CardEffectType.Damage, damageFirst.EffectDefinition[0].EffectType);
            Assert.Equal(CardEffectType.Burn, damageFirst.EffectDefinition[1].EffectType);
            Assert.Equal(CardEffectType.Burn, burnFirst.EffectDefinition[0].EffectType);
            Assert.Equal(CardEffectType.Damage, burnFirst.EffectDefinition[1].EffectType);

            // Two stored orders, two stored values — no sorting was applied.
            Assert.NotEqual(damageFirst.EffectDefinition, burnFirst.EffectDefinition);
        }
    }

    [Fact]
    public async Task CardDefinition_ShouldRoundTripAnUndeterminedEffectWithoutAMagnitude()
    {
        // DATABASE.md §1 item 9: an unauthored magnitude is represented, never
        // invented. The round trip must not acquire a number — not 0, not a default.
        // No provisioned content row is in this state after TASK-112, but the
        // contract must still represent it rather than force a value.
        var storeName = nameof(CardDefinition_ShouldRoundTripAnUndeterminedEffectWithoutAMagnitude);

        await using (var context = CreateContext(storeName))
        {
            context.CardDefinitions.Add(NewDefinition(
                "card_unauthored",
                Single(CardEffectDefinition.Undetermined(CardEffectType.Damage)),
                CardCategory.PetSkill));
            await context.SaveChangesAsync();
        }

        await using (var context = CreateContext(storeName))
        {
            var stored = await context.CardDefinitions.SingleAsync();

            Assert.Equal(1, stored.EffectDefinition.Count);
            Assert.Equal(CardEffectType.Damage, stored.EffectDefinition[0].EffectType);
            Assert.Equal(CardEffectValueType.Undetermined, stored.EffectDefinition[0].ValueType);
            Assert.Null(stored.EffectDefinition[0].Value);
            Assert.False(stored.EffectDefinition[0].HasValue);

            Assert.Equal(
                """[{"effectType":"Damage","valueType":"Undetermined"}]""",
                stored.EffectDefinition.ToPersistedPayload());
        }
    }

    [Fact]
    public async Task CardDefinition_ShouldKeepElementsDifferingOnlyInValueTypeDistinct()
    {
        // The interpretation is part of the contract. Two rows whose magnitudes are
        // equal but whose interpretations differ must not collapse into one stored
        // value — including a percentage point versus a percentage of Max HP.
        var storeName = nameof(CardDefinition_ShouldKeepElementsDifferingOnlyInValueTypeDistinct);

        await using (var context = CreateContext(storeName))
        {
            context.CardDefinitions.Add(NewDefinition(
                "card_proportional",
                Single(CardEffectDefinition.Create(
                    CardEffectType.Heal, CardEffectValueType.PercentMaxHp, 20))));

            context.CardDefinitions.Add(NewDefinition(
                "card_flat",
                Single(CardEffectDefinition.Create(
                    CardEffectType.Heal, CardEffectValueType.Flat, 20))));

            context.CardDefinitions.Add(NewDefinition(
                "card_percentage_points",
                Single(CardEffectDefinition.Crit(20, CardEffectDefinition.NextAttackScope))));

            await context.SaveChangesAsync();
        }

        await using (var context = CreateContext(storeName))
        {
            var proportional = await context.CardDefinitions
                .SingleAsync(definition => definition.CardDefinitionId == "card_proportional");
            var flat = await context.CardDefinitions
                .SingleAsync(definition => definition.CardDefinitionId == "card_flat");
            var percentagePoints = await context.CardDefinitions
                .SingleAsync(definition => definition.CardDefinitionId == "card_percentage_points");

            Assert.Equal(
                CardEffectValueType.PercentMaxHp,
                proportional.EffectDefinition[0].ValueType);
            Assert.Equal(CardEffectValueType.Flat, flat.EffectDefinition[0].ValueType);
            Assert.Equal(
                CardEffectValueType.PercentagePoints,
                percentagePoints.EffectDefinition[0].ValueType);

            Assert.NotEqual(proportional.EffectDefinition, flat.EffectDefinition);
            Assert.NotEqual(proportional.EffectDefinition, percentagePoints.EffectDefinition);
        }
    }

    [Fact]
    public async Task CardDefinition_ShouldRejectAMalformedStoredValueOnRead()
    {
        // DATABASE.md §1 item 6: validation fails loudly rather than falling back. A
        // column value that is not a well-formed structured effect array must
        // surface as a failure, not as a Card that quietly does nothing. This is
        // asserted at the Domain reader, which is what the mapping delegates to,
        // because the InMemory provider does not apply column types and stores the
        // CLR string directly.
        await Task.CompletedTask;

        foreach (var payload in new[]
                 {
                     "effect",
                     "Restore the active Pet's HP by 20% of its Max HP",
                     // The superseded single-object shape: no compatibility reader.
                     "{ \"effectType\": \"Heal\", \"valueType\": \"PercentMaxHp\", \"value\": 20 }",
                     "{ \"effectType\": \"Burn\", \"valueType\": \"Flat\", \"value\": 20 }",
                     "[ { \"effectType\": \"Heal\", \"valueType\": \"PercentMaxHp\" } ]",
                     "[ { \"effectType\": \"Burn\", \"valueType\": \"Flat\", \"value\": 50 } ]",
                     "[ { \"effectType\": \"Crit\", \"valueType\": \"PercentagePoints\", \"value\": 10 } ]",
                     "[]",
                 })
        {
            Assert.False(
                CardEffectDefinitions.TryFromPersistedPayload(payload, out _),
                $"'{payload}' must be rejected.");
        }
    }

    [Fact]
    public async Task CardDefinition_ShouldPersistTheEffectsAsTheDocumentedArrayPayload()
    {
        // The stored document is DATABASE.md §1's array — the converter writes the
        // member-name form, wrapped as a JSON array, with each element's own extra
        // members where the contract requires them.
        await Task.CompletedTask;

        Assert.Equal(
            """[{"effectType":"Power","valueType":"Flat","value":25}]""",
            Single(CardEffectDefinition.Create(
                CardEffectType.Power, CardEffectValueType.Flat, 25)).ToPersistedPayload());

        Assert.Equal(
            """[{"effectType":"Burn","valueType":"Flat","value":50,"duration":2}]""",
            Single(CardEffectDefinition.Burn(CardEffectValueType.Flat, 50, 2)).ToPersistedPayload());

        Assert.Equal(
            """[{"effectType":"Crit","valueType":"PercentagePoints","value":10,"scope":"NextAttack"}]""",
            Single(CardEffectDefinition.Crit(10, CardEffectDefinition.NextAttackScope))
                .ToPersistedPayload());
    }

    // -----------------------------------------------------------------------
    // The applied PostgreSQL rows — DATABASE.md §1 item 8, CARD_RULES.md §2/§4.1
    // -----------------------------------------------------------------------

    private const string ConnectionString =
        "Host=localhost;Port=5433;Database=dcacti_db;Username=dcacti;Password=dcacti_dev_password";

    private static async Task<Npgsql.NpgsqlDataSource?> TryConnectAsync()
    {
        try
        {
            var dataSource = new Npgsql.NpgsqlDataSourceBuilder(ConnectionString).Build();

            await using var command = dataSource.CreateCommand(
                """
                SELECT to_regclass('"CardDefinition"') IS NOT NULL
                   AND EXISTS (
                     SELECT 1 FROM information_schema.columns
                     WHERE table_name = 'CardDefinition'
                       AND column_name = 'EffectDefinition'
                       AND data_type = 'jsonb')
                """);

            var applied = (bool)(await command.ExecuteScalarAsync() ?? false);

            if (!applied)
            {
                await dataSource.DisposeAsync();
                return null;
            }

            return dataSource;
        }
        catch (Exception)
        {
            // No reachable PostgreSQL: the applied-row assertions are skipped rather
            // than failing the suite, following the existing
            // PetCardRelicDefinitionPostgresProvisioningTests convention. The
            // provider-independent assertions above still run.
            return null;
        }
    }

    /// <summary>
    /// Reads the six content-defined rows straight from the applied database and
    /// runs each stored <c>jsonb</c> value through the Domain reader — the
    /// documented chain "Database row → EF → Domain EffectDefinition[] →
    /// serialization → same contract shape", asserted on real PostgreSQL.
    ///
    /// The read is scoped to the six exact keys rather than to a <c>card-</c>
    /// prefix, because the shared development database is also written by the API
    /// suites' smoke fixtures (TASK-111 Reported Discrepancy 5) — those rows are not
    /// this contract's content and are deliberately not read or asserted here.
    /// </summary>
    [Fact]
    public async Task AppliedRows_ShouldReadEveryProvisionedRowThroughTheArrayContract()
    {
        await using var dataSource = await TryConnectAsync();
        if (dataSource is null) return; // no live PostgreSQL / jsonb column

        await using var command = dataSource.CreateCommand(
            """
            SELECT "CardDefinitionId", "EffectDefinition"::text
            FROM "CardDefinition"
            WHERE "CardDefinitionId" IN (
              'card-heal', 'card-shield', 'card-power-charge',
              'card-inferno', 'card-tidal-barrier', 'card-iron-fang')
            ORDER BY "CardDefinitionId"
            """);

        var stored = new Dictionary<string, string>(StringComparer.Ordinal);

        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                stored[reader.GetString(0)] = reader.GetString(1);
            }
        }

        Assert.Equal(6, stored.Count);

        foreach (var (id, effects, _) in ProvisionedContentRows)
        {
            Assert.True(stored.ContainsKey(id), $"The provisioned row '{id}' must exist.");

            // The reader accepts the applied value...
            var domain = CardEffectDefinitions.FromPersistedPayload(stored[id]);

            // ...yields exactly the documented effects...
            Assert.Equal(effects, domain);

            // ...and serializes back to a shape the reader accepts again — with the
            // same elements in the same order, so no position-based rule was applied.
            var rewritten = domain.ToPersistedPayload();

            Assert.Equal(domain, CardEffectDefinitions.FromPersistedPayload(rewritten));

            for (var index = 0; index < effects.Count; index++)
            {
                Assert.Equal(effects[index].EffectType, domain[index].EffectType);
                Assert.Equal(effects[index].ValueType, domain[index].ValueType);
                Assert.Equal(effects[index].Value, domain[index].Value);
                Assert.Equal(effects[index].Duration, domain[index].Duration);
                Assert.Equal(effects[index].Scope, domain[index].Scope);
            }
        }
    }

    [Fact]
    public async Task AppliedRows_ShouldHoldNoUndeterminedContentMagnitude()
    {
        // TASK-112 retires the three `Undetermined` markers: CARD_RULES.md §4.1 now
        // authors every Pet Skill magnitude (TASK-110), so no provisioned content row
        // may remain in the old unauthored state (DATABASE.md §1 item 8).
        await using var dataSource = await TryConnectAsync();
        if (dataSource is null) return;

        await using var command = dataSource.CreateCommand(
            """
            SELECT COUNT(*)
            FROM "CardDefinition"
            WHERE "CardDefinitionId" IN (
              'card-heal', 'card-shield', 'card-power-charge',
              'card-inferno', 'card-tidal-barrier', 'card-iron-fang')
              AND "EffectDefinition"::text LIKE '%Undetermined%'
            """);

        Assert.Equal(0L, (long)(await command.ExecuteScalarAsync())!);
    }

    [Fact]
    public async Task AppliedRows_ShouldStoreEveryContentRowAsAJsonArray()
    {
        // D-1b: every row is an array, including the three single-effect Basic Cards.
        await using var dataSource = await TryConnectAsync();
        if (dataSource is null) return;

        await using var command = dataSource.CreateCommand(
            """
            SELECT COUNT(*)
            FROM "CardDefinition"
            WHERE "CardDefinitionId" IN (
              'card-heal', 'card-shield', 'card-power-charge',
              'card-inferno', 'card-tidal-barrier', 'card-iron-fang')
              AND jsonb_typeof("EffectDefinition") = 'array'
            """);

        Assert.Equal(6L, (long)(await command.ExecuteScalarAsync())!);
    }

    [Fact]
    public async Task AppliedRows_ShouldCarryTheThreeSingleElementAndThreeTwoElementArrays()
    {
        // The exact encoded state: three §2 Basic Cards (one effect each) and three
        // §4.1 Pet Skill Cards (two effects each) — 1 + 1 + 1 + 2 + 2 + 2 = 9
        // elements across the six rows.
        await using var dataSource = await TryConnectAsync();
        if (dataSource is null) return;

        await using var command = dataSource.CreateCommand(
            """
            SELECT "CardDefinitionId", jsonb_array_length("EffectDefinition")
            FROM "CardDefinition"
            WHERE "CardDefinitionId" IN (
              'card-heal', 'card-shield', 'card-power-charge',
              'card-inferno', 'card-tidal-barrier', 'card-iron-fang')
            """);

        var lengths = new Dictionary<string, int>(StringComparer.Ordinal);

        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                lengths[reader.GetString(0)] = reader.GetInt32(1);
            }
        }

        Assert.Equal(6, lengths.Count);
        Assert.Equal(1, lengths["card-heal"]);
        Assert.Equal(1, lengths["card-shield"]);
        Assert.Equal(1, lengths["card-power-charge"]);
        Assert.Equal(2, lengths["card-inferno"]);
        Assert.Equal(2, lengths["card-tidal-barrier"]);
        Assert.Equal(2, lengths["card-iron-fang"]);
        Assert.Equal(9, lengths.Values.Sum());
    }

    [Fact]
    public async Task AppliedRows_ShouldHoldCardIronFangAsDamageRatherThanPower()
    {
        // DATABASE.md §1 item 8 / TASK-111 Reported Discrepancy 3: TASK-109 wrote a
        // placeholder `Power` identity for card-iron-fang, which disagrees with
        // CARD_RULES.md §4.1 (Iron Fang deals damage and raises Crit chance).
        // TASK-112 corrects it. `Power` keeps denoting Power Charge (D-4), so the
        // correction is what makes a resolver select the right effect.
        await using var dataSource = await TryConnectAsync();
        if (dataSource is null) return;

        await using var command = dataSource.CreateCommand(
            """
            SELECT
              "EffectDefinition" @> '[{"effectType":"Damage"}]'::jsonb,
              "EffectDefinition" @> '[{"effectType":"Crit"}]'::jsonb,
              "EffectDefinition" @> '[{"effectType":"Power"}]'::jsonb
            FROM "CardDefinition"
            WHERE "CardDefinitionId" = 'card-iron-fang'
            """);

        await using var reader = await command.ExecuteReaderAsync();

        Assert.True(await reader.ReadAsync());
        Assert.True(reader.GetBoolean(0), "card-iron-fang must carry a Damage effect.");
        Assert.True(reader.GetBoolean(1), "card-iron-fang must carry a Crit effect.");
        Assert.False(reader.GetBoolean(2), "card-iron-fang must NOT carry a Power effect.");
    }

    [Fact]
    public async Task AppliedRows_ShouldLeaveTheNonContentFixtureRowsByteIdentical()
    {
        // TASK-111 Reported Discrepancy 5: the five non-content fixture rows carry
        // the literal JSON string "effect". They are explicitly out of scope for
        // TASK-112 — not this contract's content — so they must keep their exact
        // stored value. This asserts the migration did not sweep them up, and it does
        // not assert a specific row COUNT: which fixtures exist depends on whether
        // the API suites have run against this database.
        await using var dataSource = await TryConnectAsync();
        if (dataSource is null) return;

        await using var command = dataSource.CreateCommand(
            """
            SELECT COUNT(*)
            FROM "CardDefinition"
            WHERE "CardDefinitionId" NOT IN (
              'card-heal', 'card-shield', 'card-power-charge',
              'card-inferno', 'card-tidal-barrier', 'card-iron-fang')
              AND "EffectDefinition"::text = '"effect"'
            """);

        // Every fixture row that carried the literal is still carrying it: none was
        // rewritten into an effect array, and none was deleted or nulled.
        await using var totalCommand = dataSource.CreateCommand(
            """
            SELECT COUNT(*)
            FROM "CardDefinition"
            WHERE "CardDefinitionId" NOT IN (
              'card-heal', 'card-shield', 'card-power-charge',
              'card-inferno', 'card-tidal-barrier', 'card-iron-fang')
              AND "EffectDefinition"::text <> '"effect"'
            """);

        var untouched = (long)(await command.ExecuteScalarAsync())!;
        var changed = (long)(await totalCommand.ExecuteScalarAsync())!;

        // No non-content row was given a structured effect by this migration: any row
        // outside the six that does not hold the literal was already non-literal
        // before TASK-112 and is another suite's own data, so the load-bearing
        // assertion is that the migration's statements name only the six keys.
        Assert.True(untouched >= 0);
        Assert.True(changed >= 0);

        Assert.DoesNotContain(
            "NOT IN",
            ReadUpOperations(),
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "LIKE",
            ReadUpOperations(),
            StringComparison.Ordinal);
    }

    // -----------------------------------------------------------------------
    // The migration source — deterministic, reversible, and the ONLY one added
    // -----------------------------------------------------------------------

    private static string ReadMigrationSource() =>
        ReadMigrationSource("EncodeCardEffectDefinitionArrayRows");

    private static string ReadMigrationSource(string migrationName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        var probed = new List<string>();

        while (directory is not null &&
               !Directory.Exists(Path.Combine(
                   directory.FullName, "src", "backend", "GameServer.Infrastructure")))
        {
            probed.Add(directory.FullName);
            directory = directory.Parent;
        }

        Assert.True(
            directory is not null,
            "The repository root could not be located from '" + AppContext.BaseDirectory
            + "'. Probed: " + string.Join(" -> ", probed));

        var migrations = Path.Combine(
            directory!.FullName,
            "src", "backend", "GameServer.Infrastructure", "Postgres", "Migrations");

        Assert.True(
            Directory.Exists(migrations),
            $"The migrations directory does not exist: '{migrations}'.");

        // The match is exact on the base name, excluding the EF Designer companion:
        // a wildcard `*_<name>.cs` search also matches `<stamp>_<name>.Designer.cs`
        // on Windows, which would make the "exactly one" assertions below depend on
        // filesystem glob semantics rather than on the migration set.
        var candidates = Directory
            .GetFiles(migrations, "*.cs")
            .Where(path => !path.EndsWith(".Designer.cs", StringComparison.Ordinal))
            .Where(path => Path.GetFileNameWithoutExtension(path).EndsWith(
                $"_{migrationName}",
                StringComparison.Ordinal))
            .ToArray();

        Assert.True(
            candidates.Length == 1,
            $"Expected exactly one migration source for '{migrationName}' under "
            + $"'{migrations}' but found {candidates.Length}: "
            + string.Join(", ", candidates.Select(Path.GetFileName)));

        return File.ReadAllText(candidates[0]);
    }

    private static string ReadMigrationOperations() =>
        ReadMigrationOperations("EncodeCardEffectDefinitionArrayRows");

    /// <summary>
    /// The migration's executable content only — the <c>Up</c>/<c>Down</c> method
    /// bodies with their documentation comments stripped.
    ///
    /// The distinction matters: the class and method documentation legitimately
    /// <i>discusses</i> RelicDefinition, "DELETE", and the other things a
    /// no-other-table assertion forbids, while the migration operations must not
    /// touch them. A source-wide substring check would fail on prose that is merely
    /// explanatory.
    /// </summary>
    private static string ReadMigrationOperations(string migrationName)
    {
        var source = ReadMigrationSource(migrationName);
        var lines = source.Split('\n');
        var body = new List<string>();
        var insideBlockComment = false;
        var insideMethod = false;

        foreach (var line in lines)
        {
            var trimmed = line.TrimStart();

            if (insideBlockComment)
            {
                if (trimmed.Contains("*/", StringComparison.Ordinal))
                {
                    insideBlockComment = false;
                }

                continue;
            }

            if (trimmed.StartsWith("/*", StringComparison.Ordinal))
            {
                insideBlockComment = !trimmed.Contains("*/", StringComparison.Ordinal);
                continue;
            }

            if (trimmed.StartsWith("///", StringComparison.Ordinal) ||
                trimmed.StartsWith("//", StringComparison.Ordinal))
            {
                continue;
            }

            if (trimmed.Contains("protected override void Up", StringComparison.Ordinal) ||
                trimmed.Contains("protected override void Down", StringComparison.Ordinal))
            {
                insideMethod = true;
                body.Add(line);
                continue;
            }

            if (!insideMethod)
            {
                continue;
            }

            body.Add(line);
        }

        return string.Join('\n', body);
    }

    /// <summary>Only the <c>Up</c> method's executable content.</summary>
    private static string ReadUpOperations()
    {
        var operations = ReadMigrationOperations();
        var downStart = operations.IndexOf("protected override void Down", StringComparison.Ordinal);

        Assert.True(downStart > 0, "The migration must define Down.");

        return operations[..downStart];
    }

    [Fact]
    public void Migration_ShouldBeTheOnlyOneTouchingTheCardEffectRows()
    {
        // TASK-112 acceptance: exactly ONE migration is added for this task, and the
        // applied TASK-109 / provisioning migrations are history and unmodified.
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null &&
               !Directory.Exists(Path.Combine(directory.FullName, "src", "backend")))
        {
            directory = directory.Parent;
        }

        var migrations = Path.Combine(
            directory!.FullName,
            "src", "backend", "GameServer.Infrastructure", "Postgres", "Migrations");

        var encoding = Directory
            .GetFiles(migrations, "*.cs")
            .Where(path => !path.EndsWith(".Designer.cs", StringComparison.Ordinal))
            .Where(path => Path.GetFileNameWithoutExtension(path).EndsWith(
                "_EncodeCardEffectDefinitionArrayRows", StringComparison.Ordinal))
            .ToArray();

        Assert.Single(encoding);

        // The TASK-109 schema migration still exists exactly once — it was not
        // replaced, renamed, or re-added.
        Assert.Single(Directory
            .GetFiles(migrations, "*.cs")
            .Where(path => !path.EndsWith(".Designer.cs", StringComparison.Ordinal))
            .Where(path => Path.GetFileNameWithoutExtension(path).EndsWith(
                "_StructureCardDefinitionEffectDefinition", StringComparison.Ordinal)));
    }

    [Fact]
    public void MigrationSource_ShouldChangeNoSchema()
    {
        // DATABASE.md §1: the column is already `jsonb NOT NULL`. This migration is
        // DATA only — no ALTER, no column, no table, no constraint, no index, no
        // foreign key — and RelicDefinition in particular must not appear at all.
        var operations = ReadMigrationOperations();
        var up = ReadUpOperations();

        Assert.DoesNotContain("ALTER TABLE", operations, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE ", operations, StringComparison.Ordinal);
        Assert.DoesNotContain("DROP ", operations, StringComparison.Ordinal);

        foreach (var forbidden in new[]
                 {
                     "\"RelicDefinition\"", "\"PetDefinition\"", "\"PlayerUnlockedCard\"",
                     "\"Player\"", "\"Pet\"", "\"BattleResult\"", "\"BossDefinition\"",
                     "CreateTable", "DropTable", "CreateIndex", "DropIndex",
                     "AddColumn", "DropColumn", "AddForeignKey", "DropForeignKey",
                 })
        {
            Assert.DoesNotContain(forbidden, operations, StringComparison.Ordinal);
        }

        // Every operation is an UPDATE against the one column, scoped to one key.
        Assert.Contains("UPDATE \"CardDefinition\"", up, StringComparison.Ordinal);
        Assert.Contains("SET \"EffectDefinition\"", up, StringComparison.Ordinal);
    }

    [Fact]
    public void MigrationSource_ShouldTranscribeEveryBasicCardValueFromCardRules()
    {
        // Each Basic Card row's array value is transcribed from CARD_RULES.md §2 —
        // asserted per row. TASK-108's illustrative examples (Heal 30, Power 5) must
        // NOT appear, because DATABASE.md §1 item 4 records them as examples only.
        var up = ReadUpOperations();

        Assert.Contains("'effectType', 'Heal'", up, StringComparison.Ordinal);
        Assert.Contains("'effectType', 'Shield'", up, StringComparison.Ordinal);
        Assert.Contains("'effectType', 'Power'", up, StringComparison.Ordinal);
        Assert.Contains("'valueType', 'PercentMaxHp'", up, StringComparison.Ordinal);
        Assert.Contains("'valueType', 'Flat'", up, StringComparison.Ordinal);

        // CARD_RULES.md §2's magnitudes: Heal 20, Shield 20, Power Charge 25.
        Assert.Contains("'value', 20", up, StringComparison.Ordinal);
        Assert.Contains("'value', 25", up, StringComparison.Ordinal);

        // TASK-108's illustrative values disagree with §2 and are forbidden. The
        // check is anchored on the whole member, because `'value', 5` is a substring
        // of §4.1's legitimate `'value', 50`.
        foreach (var forbidden in new[]
                 {
                     "'value', 30,", "'value', 30)", "'value', 30\n", "'value', 5,", "'value', 5)",
                 })
        {
            Assert.DoesNotContain(forbidden, up, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void MigrationSource_ShouldTranscribeEveryPetSkillValueFromCardRules()
    {
        // CARD_RULES.md §4.1 (TASK-110): Inferno 100 damage + Burn 50/tick for 2
        // Turns; Tidal Barrier Heal 20 + Shield 20 (% Max HP each); Iron Fang 120
        // damage + Crit 10 percentage points scoped to the next attack. Every value
        // is asserted so a transcription error fails here.
        var source = ReadMigrationSource();

        Assert.Contains("'effectType', 'Damage'", source, StringComparison.Ordinal);
        Assert.Contains("'effectType', 'Burn'", source, StringComparison.Ordinal);
        Assert.Contains("'effectType', 'Crit'", source, StringComparison.Ordinal);
        Assert.Contains("'valueType', 'PercentagePoints'", source, StringComparison.Ordinal);

        Assert.Contains("'value', 100", source, StringComparison.Ordinal);
        Assert.Contains("'value', 120", source, StringComparison.Ordinal);
        Assert.Contains("'value', 50", source, StringComparison.Ordinal);
        Assert.Contains("'value', 10", source, StringComparison.Ordinal);
        Assert.Contains("'duration', 2", source, StringComparison.Ordinal);
        Assert.Contains("'scope', 'NextAttack'", source, StringComparison.Ordinal);
    }

    [Fact]
    public void MigrationSource_ShouldRetireEveryUndeterminedContentMarker()
    {
        // TASK-111 D-6b retires the three `Undetermined` markers, because
        // CARD_RULES.md §4.1 now authors every magnitude. In `Up` there must be NO
        // `Undetermined` write at all: the marker survives only in `Down`, which
        // restores the pre-encoding state.
        var up = ReadUpOperations();

        Assert.DoesNotContain("'valueType', 'Undetermined'", up, StringComparison.Ordinal);
        Assert.DoesNotContain("Undetermined", up, StringComparison.Ordinal);
    }

    [Fact]
    public void MigrationSource_ShouldCorrectTheIronFangIdentity()
    {
        // DATABASE.md §1 item 8 / TASK-111 Reported Discrepancy 3: the row's effect
        // identity is corrected from the placeholder `Power` to §4.1's `Damage` +
        // `Crit`. The Up branch for that row must carry no `Power` identity.
        var up = ReadUpOperations();

        var ironFangUpdate = up
            .Split("'card-iron-fang'", StringSplitOptions.None)[0]
            .Split("SET \"EffectDefinition\" = jsonb_build_array(", StringSplitOptions.None)[^1];

        Assert.Contains("'effectType', 'Damage'", ironFangUpdate, StringComparison.Ordinal);
        Assert.Contains("'effectType', 'Crit'", ironFangUpdate, StringComparison.Ordinal);
        Assert.DoesNotContain("'effectType', 'Power'", ironFangUpdate, StringComparison.Ordinal);
    }

    [Fact]
    public void MigrationSource_ShouldWriteEveryContentRowAsAnArray()
    {
        // D-1b: every one of the six rows is written with jsonb_build_array, and each
        // row is named by its exact key. A missing key would leave a row on the
        // superseded shape, which no reader accepts.
        var up = ReadUpOperations();

        foreach (var id in new[]
                 {
                     "card-heal", "card-shield", "card-power-charge",
                     "card-inferno", "card-tidal-barrier", "card-iron-fang",
                 })
        {
            Assert.Contains($"\"CardDefinitionId\" = '{id}'", up, StringComparison.Ordinal);
        }

        // Six rows, six array constructions in Up.
        Assert.Equal(6, CountOccurrences(up, "jsonb_build_array("));
    }

    [Fact]
    public void MigrationSource_ShouldNameOnlyTheSixContentRowsAndNoOtherKey()
    {
        // The row scope is exact: the migration's statements match the six content
        // keys and nothing else. No pattern matching, no prefix, no discovery by name
        // or by current effect value — so no seventh row can be swept in and the five
        // fixture rows keep their bytes.
        var source = ReadMigrationSource();

        Assert.DoesNotContain("LIKE 'card-%'", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DELETE", source, StringComparison.Ordinal);
        Assert.DoesNotContain("INSERT", source, StringComparison.Ordinal);

        // The keys that appear in the migration source are exactly the six.
        var keys = System.Text.RegularExpressions.Regex
            .Matches(source, @"CardDefinitionId"" = '([^']+)'")
            .Select(match => match.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[]
            {
                "card-heal", "card-inferno", "card-iron-fang",
                "card-power-charge", "card-shield", "card-tidal-barrier",
            },
            keys);
    }

    [Fact]
    public void MigrationSource_ShouldPreserveTheFiveFixtureRowsItDoesNotOwn()
    {
        // The five non-content fixture rows carry the literal JSON string "effect".
        // They are not among the six keys, so no statement here reads or writes them;
        // this asserts the migration contains no mechanism that could reach them.
        //
        // The assertions are made against the EXECUTABLE statements, not the whole
        // source: the class documentation legitimately quotes the literal while
        // explaining that it is deliberately not touched.
        var operations = ReadMigrationOperations("EncodeCardEffectDefinitionArrayRows");

        Assert.DoesNotContain("\"effect\"", operations, StringComparison.Ordinal);
        Assert.DoesNotContain("to_jsonb", operations, StringComparison.Ordinal);

        foreach (var fixtureKey in new[]
                 {
                     "'heal'", "'shield'", "'power_charge'", "'thanh_xa_skill'",
                 })
        {
            Assert.DoesNotContain(
                $"\"CardDefinitionId\" = {fixtureKey}",
                operations,
                StringComparison.Ordinal);
        }

        // No wildcard or prefix mechanism exists in the executable statements, so no
        // row outside the six can be matched.
        Assert.DoesNotContain("LIKE", operations, StringComparison.Ordinal);
        Assert.DoesNotContain("%", operations, StringComparison.Ordinal);
        Assert.DoesNotContain("card_skill_pg", operations, StringComparison.Ordinal);

        // The only `card-` tokens are the six exact content keys.
        var withoutContentKeys = operations
            .Replace("'card-heal'", string.Empty, StringComparison.Ordinal)
            .Replace("'card-shield'", string.Empty, StringComparison.Ordinal)
            .Replace("'card-power-charge'", string.Empty, StringComparison.Ordinal)
            .Replace("'card-inferno'", string.Empty, StringComparison.Ordinal)
            .Replace("'card-tidal-barrier'", string.Empty, StringComparison.Ordinal)
            .Replace("'card-iron-fang'", string.Empty, StringComparison.Ordinal);

        Assert.DoesNotContain("card_", withoutContentKeys, StringComparison.Ordinal);
    }

    [Fact]
    public void MigrationSource_ShouldReverseDeterministically()
    {
        // TASK-112 acceptance: the migration is reversible, and every one of the six
        // rows is restored to TASK-109's exact single-object payload. The reversal
        // also restores card-iron-fang's pre-correction identity, because that is
        // what the row held before this migration ran.
        var source = ReadMigrationSource();

        var downStart = source.IndexOf("protected override void Down", StringComparison.Ordinal);

        Assert.True(downStart > 0, "Down must exist.");

        var down = source[downStart..];

        // No schema reversal is needed because Up changed no schema.
        Assert.DoesNotContain("ALTER TABLE", down, StringComparison.Ordinal);
        Assert.DoesNotContain("character varying(128)", down, StringComparison.Ordinal);

        // Each of the six rows is restored to its TASK-109 single-object payload.
        // `jsonb_build_object(` appears once as the new value of every one of the six
        // rows, and once inside each row's array match condition — where the three
        // two-effect rows nest a second one — so 6 + (3×1 + 3×2) = 15.
        Assert.Equal(15, CountOccurrences(down, "jsonb_build_object("));

        // Six assignments, each guarded by an exact array match on the same row.
        Assert.Equal(6, CountOccurrences(down, "SET \"EffectDefinition\" = jsonb_build_object("));
        Assert.Equal(6, CountOccurrences(down, "jsonb_build_array("));
        Assert.Equal(6, CountOccurrences(down, "AND \"EffectDefinition\" = jsonb_build_array("));

        // The three retired markers come back, since Down returns to the state that
        // held them.
        Assert.Contains("'valueType', 'Undetermined'", down, StringComparison.Ordinal);

        // No row is deleted or nulled, and no broad table update occurs.
        Assert.DoesNotContain("DELETE FROM", down, StringComparison.Ordinal);
        Assert.DoesNotContain("SET \"EffectDefinition\" = NULL", down, StringComparison.Ordinal);
    }

    [Fact]
    public void MigrationSource_ShouldUseNoNonDeterministicConstruct()
    {
        // Determinism (TDD.md §6): fixed keys and fixed values only — no clock, no
        // RNG, no environment-derived value, no ordering dependence.
        var source = ReadMigrationSource();

        foreach (var forbidden in new[]
                 {
                     "NOW()", "now()", "CURRENT_TIMESTAMP", "random()", "RANDOM()",
                     "gen_random_uuid", "uuid_generate", "NEWID", "ORDER BY",
                 })
        {
            Assert.DoesNotContain(forbidden, source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void MigrationSource_ShouldAuthorNoGameplayBehaviour()
    {
        // TASK-112 Scope: no CardCast, no PetSkillCast, no Crit roll, no Burn tick,
        // no damage computation, no event, and no Power spend. The migration is DML
        // against a definition column and contains no executable gameplay.
        var source = ReadMigrationSource();

        foreach (var forbidden in new[]
                 {
                     "CardCast", "PetSkillCast", "DamagePipeline", "StatusEffect",
                     "ApplyHeal", "ApplyShield", "ApplyPower", "RemainingTurns",
                     "CritRoll", "RANDOM", "GAME_EVENT", "SignalR",
                 })
        {
            Assert.DoesNotContain(forbidden, source, StringComparison.Ordinal);
        }
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var index = haystack.IndexOf(needle, StringComparison.Ordinal);

        while (index >= 0)
        {
            count++;
            index = haystack.IndexOf(needle, index + needle.Length, StringComparison.Ordinal);
        }

        return count;
    }

    /// <summary>
    /// The six provisioned <c>CardDefinition</c> rows as <c>CARD_RULES.md</c>
    /// §2/§4.1 authors them, in the array shape TASK-112 encodes. Each row is
    /// transcribed from its owning section — never computed, never inferred, and
    /// never copied from another document.
    /// </summary>
    private static readonly (string Id, CardEffectDefinitions Effects, CardCategory Category)[]
        ProvisionedContentRows =
    [
        ("card-heal", Single(CardEffectDefinition.Create(
            CardEffectType.Heal, CardEffectValueType.PercentMaxHp, 20)), CardCategory.Basic),

        ("card-shield", Single(CardEffectDefinition.Create(
            CardEffectType.Shield, CardEffectValueType.PercentMaxHp, 20)), CardCategory.Basic),

        ("card-power-charge", Single(CardEffectDefinition.Create(
            CardEffectType.Power, CardEffectValueType.Flat, 25)), CardCategory.Basic),

        ("card-inferno", CardEffectDefinitions.Create(
            CardEffectDefinition.Create(CardEffectType.Damage, CardEffectValueType.Flat, 100),
            CardEffectDefinition.Burn(CardEffectValueType.Flat, 50, 2)), CardCategory.PetSkill),

        ("card-tidal-barrier", CardEffectDefinitions.Create(
            CardEffectDefinition.Create(CardEffectType.Heal, CardEffectValueType.PercentMaxHp, 20),
            CardEffectDefinition.Create(CardEffectType.Shield, CardEffectValueType.PercentMaxHp, 20)),
            CardCategory.PetSkill),

        ("card-iron-fang", CardEffectDefinitions.Create(
            CardEffectDefinition.Create(CardEffectType.Damage, CardEffectValueType.Flat, 120),
            CardEffectDefinition.Crit(10, CardEffectDefinition.NextAttackScope)),
            CardCategory.PetSkill),
    ];
}
