using System.Text.RegularExpressions;
using GameServer.Domain.Cards;
using Xunit;

namespace GameServer.Infrastructure.Tests;

/// <summary>
/// <b>TASK-309 W2 + V-3 — the four ordered, data-only Boss Card provisioning
/// migrations.</b> They migrate the four remaining MVP Boss abilities to their
/// <c>BossCard</c> form and author each Boss's approved cast-priority score
/// (<c>DATABASE.md</c> §1, §3, §5 item 4; <c>CARD_RULES.md</c> §1.1;
/// <c>BOSS_RULES.md</c> §4.2, §4.3 item 5, §4.4 item 3, §6.3, §6.3.1 items 1–3
/// and 5; ADR-029 Slice 4; TASK-309 §1 B-1/B-2/B-3/D-1 and the Power Cost
/// table):
///
/// <code>
/// order   migration stamp / name                         Card row      Boss row           score
/// -----   --------------------------------------------   -----------   ----------------   -----
/// 1       20261020111406_ProvisionFlameBurstBossCard      flame-burst   boss-def-hoa-long      85
/// 2       20261020111411_ProvisionRootBossCard            root          boss-def-moc-yeu       85
/// 3       20261020111415_ProvisionDrainPowerBossCard      drain-power   boss-def-thuy-ma       85
/// 4       20261020111419_ProvisionThunderStrikeBossCard   thunder-strike boss-def-kim-loi-vuong 85
/// </code>
///
/// <b>What is asserted here is the migration source, not a live database.</b>
/// Each <c>Up</c> is exactly one <c>InsertData</c> into <c>CardDefinition</c>
/// (category <c>BossCard</c> = 2, the approved <c>PowerCost</c>,
/// <c>LoadoutCopyLimit</c> = 1, and the approved effect array byte-equal to
/// <c>CardEffectDefinitions.ToPersistedPayload()</c>) plus one
/// <c>UpdateData</c> on <c>BossDefinition</c> setting that Boss's
/// <c>CastPriorityScore</c> to 85. Each <c>Down</c> is the exact mirror —
/// <c>DeleteData</c> keyed by that row's <c>CardDefinitionId</c> and
/// <c>UpdateData</c> restoring <c>NULL</c> — and depends on no later Slice 4
/// migration. No migration contains a schema operation, and
/// <c>GameDbContextModelSnapshot.cs</c> is unchanged.
///
/// <b>Why source assertions and not a live-data test here.</b> The repository's
/// PostgreSQL suites (<see cref="PetCardRelicDefinitionPostgresProvisioningTests"/>,
/// <see cref="BossDefinitionPostgresProvisioningTests"/>,
/// <see cref="BossDefinitionBalanceLookupTests"/>) own the applied-row
/// verification and skip when no database is reachable; this file must stay
/// hermetic and must not depend on a cluster it did not create. The migration's
/// executable source is the artifact this slice owns.
///
/// <b>V-3 gate — executed evidence (2026-10-20, disposable cluster only).</b>
/// The <c>Up</c>/<c>Down</c> chain below was actually applied and reverted
/// against a fresh local PostgreSQL 18.6.1 cluster; source inspection alone was
/// explicitly insufficient (<c>TASK-309</c> §1 V-3, §11). There was no Docker
/// and no production database; the distribution was the portable
/// <c>theseus-rs/postgresql-binaries</c> 18.6.1 x86_64 Windows zip, extracted
/// under %TEMP%, and every step ran on the disposable port 5544.
///
/// <code>
/// 1. Download (release asset postgresql-18.6.1-x86_64-pc-windows-msvc.zip,
///    48,730,935 bytes) and extract to
///    %TEMP%\dcacti-v3-pg\pgsql\postgresql-18.6.1-x86_64-pc-windows-msvc
///
/// 2. initdb -D %TEMP%\dcacti-v3-pg\data -U postgres -A trust -E UTF8 --no-locale
///       -> "Success. You can now start the database server ..."; exit 0
///
/// 3. pg_ctl -D %TEMP%\dcacti-v3-pg\data -l %TEMP%\dcacti-v3-pg\server.log
///           -o "-p 5544 -h 127.0.0.1" start
///       -> "server started"; pg_isready -> "127.0.0.1:5544 - accepting connections"
///
/// 4. psql -h 127.0.0.1 -p 5544 -U postgres -d postgres
///       -> CREATE ROLE dcacti_v3 LOGIN PASSWORD '...'; exit 0
///       -> CREATE DATABASE dcacti_v3 OWNER dcacti_v3; exit 0
///
/// 5. $env:ConnectionStrings__DefaultConnection =
///      'Host=127.0.0.1;Port=5544;Database=dcacti_v3;Username=dcacti_v3;Password=...;Include Error Detail=true'
///    dotnet ef database update --project src/backend/GameServer.Infrastructure \
///                              --startup-project src/backend/GameServer.Api
///       -> Applying '..._ProvisionFlameBurstBossCard'
///          Applying '..._ProvisionRootBossCard'
///          Applying '..._ProvisionDrainPowerBossCard'
///          Applying '..._ProvisionThunderStrikeBossCard'
///          Done.  (exit 0)
///
/// 6. psql verification after Up:
///       CardDefinition rows      = 13 (8 Player Cards + earthquake + the four)
///       flame-burst     Category 2  PowerCost 40  LoadoutCopyLimit 1
///                       EffectDefinition -> AtkBuff / PercentagePoints / 50 / duration 3
///       root            Category 2  PowerCost 35  LoadoutCopyLimit 1
///                       EffectDefinition -> AtkDebuff / PercentagePoints / 30 / duration 3
///       drain-power     Category 2  PowerCost 25  LoadoutCopyLimit 1
///                       EffectDefinition -> PowerDrain / Flat / 20 (no duration, no scope)
///       thunder-strike  Category 2  PowerCost 40  LoadoutCopyLimit 1
///                       EffectDefinition -> NextAtkBuff / PercentagePoints / 50 / scope NextAttack
///       earthquake      Category 2  PowerCost 30  EffectDefinition Shield / Flat / 150 (unchanged)
///       the eight Player Cards unchanged (same category/cost/effect values)
///       BossDefinition CastPriorityScore:
///         boss-def-hoa-long 85, boss-def-moc-yeu 85, boss-def-thuy-ma 85,
///         boss-def-kim-loi-vuong 85, boss-def-son-thach-ve 16 (unchanged)
///       information_schema columns identical before/after (no schema change)
///       __EFMigrationsHistory = 25 rows (the 21 pre-existing + the four)
///
/// 7. dotnet ef database update 20261019125446_ProvisionBossCardContent ...
///       -> Reverting '..._ProvisionThunderStrikeBossCard'
///          Reverting '..._ProvisionDrainPowerBossCard'
///          Reverting '..._ProvisionRootBossCard'
///          Reverting '..._ProvisionFlameBurstBossCard'
///          Done.  (exit 0)
///    psql verification after Down:
///       CardDefinition rows = 9; the four rows are gone
///       the four BossDefinition scores are NULL, Son Thach Ve still 16
///       earthquake, the eight Player Cards, every other row and the schema
///       are byte-identical to the pre-Up state
///       __EFMigrationsHistory = the 21 pre-existing rows
///
/// 8. Re-apply (dotnet ef database update) -> Done.; the psql dump was
///    byte-for-byte identical to step 6 (PowerShell -ceq on the raw capture).
///
/// 9. pg_ctl -D %TEMP%\dcacti-v3-pg\data -m fast stop -> "server stopped";
///    the disposable cluster/data directory was deleted. No production
///    database was touched.
///
/// Also verified (same run): `dotnet ef migrations has-pending-model-changes`
/// -> "No changes have been made to the model since the last migration.";
/// `dotnet ef migrations list` shows the four applied in the mandated order.
/// </code>
///
/// The assertions below are the hermetic half of that evidence: they pin the
/// executable operations, the exact row/score values, the mandated order, the
/// Down mirrors, and the unchanged model snapshot.
/// </summary>
public class TASK309BossCardProvisioningMigrationTests
{
    /// <summary>
    /// One provisioned Slice 4 row, transcribed from TASK-309 §1 (the approved
    /// row values) and <c>BOSS_RULES.md</c> §6.3/§6.3.1 — never computed here.
    /// The effect is built with the Domain's own factories, so the expected
    /// payload is the contract's writer output rather than a hand-built string.
    /// </summary>
    private sealed record ProvisionedRow(
        string Migration,
        string CardDefinitionId,
        string Name,
        int PowerCost,
        string BossDefinitionId,
        CardEffectDefinitions Effect)
    {
        public string Payload => Effect.ToPersistedPayload();
    }

    private static readonly ProvisionedRow[] ProvisionedRows =
    [
        new(
            "ProvisionFlameBurstBossCard",
            "flame-burst",
            "Flame Burst",
            40,
            "boss-def-hoa-long",
            // BOSS_RULES.md §6.3.1 item 1 / TASK-309 §1 B-2: Boss self ATK +50%
            // for 3 Turns.
            CardEffectDefinitions.Create(CardEffectDefinition.AtkBuff(50, 3))),
        new(
            "ProvisionRootBossCard",
            "root",
            "Root",
            35,
            "boss-def-moc-yeu",
            // BOSS_RULES.md §6.3.1 item 3 / TASK-309 §1 B-3: active Pet ATK -30%
            // for 3 Turns.
            CardEffectDefinitions.Create(CardEffectDefinition.AtkDebuff(30, 3))),
        new(
            "ProvisionDrainPowerBossCard",
            "drain-power",
            "Drain Power",
            25,
            "boss-def-thuy-ma",
            // BOSS_RULES.md §6.3.1 item 2 / TASK-309 §1 D-1: instant -20 flat Pet
            // Power, no duration and no scope.
            CardEffectDefinitions.Create(CardEffectDefinition.PowerDrain(20))),
        new(
            "ProvisionThunderStrikeBossCard",
            "thunder-strike",
            "Thunder Strike",
            40,
            "boss-def-kim-loi-vuong",
            // BOSS_RULES.md §6.3.1 item 5 / TASK-309 §1 D-1: +50% on the next
            // qualifying Boss attack, scope NextAttack.
            CardEffectDefinitions.Create(CardEffectDefinition.NextAtkBuff(50))),
    ];

    /// <summary>
    /// The exact JSON member order the contract's writer emits — asserted so the
    /// payload the migration stores is not merely equal-as-JSON but byte-equal to
    /// <c>CardEffectDefinitions.ToPersistedPayload()</c>, which is what the strict
    /// reader compares.
    /// </summary>
    private static readonly (string CardDefinitionId, string Payload)[] DocumentedPayloadLiterals =
    [
        ("flame-burst", "[{\"effectType\":\"AtkBuff\",\"valueType\":\"PercentagePoints\",\"value\":50,\"duration\":3}]"),
        ("root", "[{\"effectType\":\"AtkDebuff\",\"valueType\":\"PercentagePoints\",\"value\":30,\"duration\":3}]"),
        ("drain-power", "[{\"effectType\":\"PowerDrain\",\"valueType\":\"Flat\",\"value\":20}]"),
        ("thunder-strike", "[{\"effectType\":\"NextAtkBuff\",\"valueType\":\"PercentagePoints\",\"value\":50,\"scope\":\"NextAttack\"}]"),
    ];

    /// <summary>
    /// All thirteen provisioned <c>CardDefinition</c> ids after Slice 4
    /// (<c>DATABASE.md</c> §5 item 4): the eight Player Cards provisioned by
    /// TASK-085/TASK-168, Slice 3's <c>earthquake</c>, and these four Boss Cards.
    /// </summary>
    private static readonly string[] AllThirteenProvisionedCardIds =
    [
        "card-heal", "card-shield", "card-power-charge",
        "card-inferno", "card-tidal-barrier", "card-iron-fang",
        "card-venomous-bloom", "card-earthshaker",
        "earthquake",
        "flame-burst", "root", "drain-power", "thunder-strike",
    ];

    private const string CardInsertDataPattern =
        @"migrationBuilder\.InsertData\(\s*table: ""CardDefinition"",\s*columns: new\[\] \{ [^}]+ \},\s*values: new object\[\] \{ ""(?<id>[^""]+)"", ""(?<name>[^""]+)"", (?<category>\d+), (?<cost>\d+), (?<limit>\d+), ""(?<payload>(?:[^""\\]|\\.)*)"" \}\);";

    private const string CardDeleteDataPattern =
        @"migrationBuilder\.DeleteData\(\s*table: ""CardDefinition"",\s*keyColumn: ""CardDefinitionId"",\s*keyValue: ""(?<key>[^""]+)""\);";

    private const string BossUpdateDataPattern =
        @"migrationBuilder\.UpdateData\(\s*table: ""BossDefinition"",\s*keyColumn: ""BossDefinitionId"",\s*keyValue: ""(?<key>[^""]+)"",\s*column: ""(?<column>[^""]+)"",\s*value: (?<value>[^)]*)\);";

    // -----------------------------------------------------------------------
    // Mandated order — ADR-029 Slice 4 / BOSS_RULES.md §4.4 item 3
    // -----------------------------------------------------------------------

    [Fact]
    public void Migrations_ShouldExistInTheMandatedOrder()
    {
        // BOSS_RULES.md §4.4 item 3 / TASK-309 §6 W2: the sequence is
        // flame-burst -> root -> drain-power -> thunder-strike ("Thunder Strike
        // last"), because the trigger-based ability needs the closest old/new
        // comparison. The stamps must be strictly increasing in that exact order,
        // and all four must sort after Slice 3's content migration, so the chain
        // applies as mandated.
        var stamps = new[]
        {
            MigrationStamp("ProvisionFlameBurstBossCard"),
            MigrationStamp("ProvisionRootBossCard"),
            MigrationStamp("ProvisionDrainPowerBossCard"),
            MigrationStamp("ProvisionThunderStrikeBossCard"),
        };

        Assert.Equal(
            new[]
            {
                "20261020111406_ProvisionFlameBurstBossCard",
                "20261020111411_ProvisionRootBossCard",
                "20261020111415_ProvisionDrainPowerBossCard",
                "20261020111419_ProvisionThunderStrikeBossCard",
            },
            stamps);

        Assert.True(
            string.CompareOrdinal(stamps[0], "20261019125446_ProvisionBossCardContent") > 0,
            "Slice 4 must apply after Slice 3's Boss Card content migration.");

        for (var index = 1; index < stamps.Length; index++)
        {
            Assert.True(
                string.CompareOrdinal(stamps[index - 1], stamps[index]) < 0,
                "The Slice 4 migration stamps must be strictly increasing in the mandated order.");
        }
    }

    // -----------------------------------------------------------------------
    // Up — one CardDefinition insert + one BossDefinition score update
    // -----------------------------------------------------------------------

    [Fact]
    public void EachMigration_ShouldInsertItsOneBossCardRowWithTheApprovedValues()
    {
        // CARD_RULES.md §1.1 items 3 and 5 / DATABASE.md §1, §3, §5 item 4: exactly
        // one CardDefinition row per migration, keyed by the approved ability
        // SkillId (the BossCard value-form exception), with the approved display
        // name, category BossCard (ordinal 2), PowerCost, and the
        // required-but-never-read LoadoutCopyLimit = 1.
        Assert.Equal(2, (int)CardCategory.BossCard);

        foreach (var row in ProvisionedRows)
        {
            var operations = ReadMigrationOperations(row.Migration);

            var insert = Assert.Single(
                Regex.Matches(operations, CardInsertDataPattern, RegexOptions.Singleline));

            Assert.Equal(row.CardDefinitionId, insert.Groups["id"].Value);
            Assert.Equal(row.Name, insert.Groups["name"].Value);
            Assert.Equal((int)CardCategory.BossCard, int.Parse(insert.Groups["category"].Value));
            Assert.Equal(row.PowerCost, int.Parse(insert.Groups["cost"].Value));
            Assert.Equal(1, int.Parse(insert.Groups["limit"].Value));

            // The stored payload is byte-equal to the Domain writer's output for
            // the approved effect, i.e. the exact bytes the strict reader accepts.
            var storedPayload = insert.Groups["payload"].Value
                .Replace("\\\"", "\"", StringComparison.Ordinal);

            Assert.Equal(row.Payload, storedPayload);

            // ...and it is the documented literal, member order included.
            Assert.Equal(
                DocumentedPayloadLiterals.Single(literal => literal.CardDefinitionId == row.CardDefinitionId).Payload,
                storedPayload);

            // The migration source keeps the same shape the other provisioning
            // migrations use (column order and the C# literal form).
            Assert.Contains(
                "columns: new[] { \"CardDefinitionId\", \"Name\", \"Category\", \"PowerCost\", \"LoadoutCopyLimit\", \"EffectDefinition\" }",
                operations,
                StringComparison.Ordinal);

            Assert.Contains(
                $"\"{row.CardDefinitionId}\", \"{row.Name}\", {insert.Groups["category"].Value}, "
                + $"{insert.Groups["cost"].Value}, 1, ",
                operations,
                StringComparison.Ordinal);
        }
    }

    [Fact]
    public void EachMigration_ShouldSetItsBossCastPriorityScoreTo85()
    {
        // BOSS_RULES.md §4.3 item 5 / TASK-309 §1 B-1: every one of the four
        // Bosses receives the approved initial cast-priority score 85, in the same
        // migration as its row so no committed state has the BossCard form without
        // the non-null score that form requires (BOSS_RULES.md §4.4 item 6). The
        // update touches CastPriorityScore and no other BossDefinition column.
        foreach (var row in ProvisionedRows)
        {
            var up = UpOperations(row.Migration);

            var update = Assert.Single(
                Regex.Matches(up, BossUpdateDataPattern, RegexOptions.Singleline));

            Assert.Equal(row.BossDefinitionId, update.Groups["key"].Value);
            Assert.Equal("CastPriorityScore", update.Groups["column"].Value);
            Assert.Equal(85, int.Parse(update.Groups["value"].Value.Trim()));

            // No other overlay or structural column is written by this migration.
            foreach (var column in new[]
                     {
                         "MaxHP", "ATK", "DEF", "EnrageThreshold",
                         "Element", "Identity", "PassiveDefinition", "SkillDefinition",
                     })
            {
                Assert.DoesNotContain($"{column} =", up, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void EachMigration_ShouldBeDataOnlyWithExactlyTwoUpAndTwoDownOperations()
    {
        // DATABASE.md §5 item 1 and TASK-309 §9: data-only. The CardDefinition
        // table/columns and the nullable BossDefinition.CastPriorityScore column
        // already exist, so no schema operation belongs here — the migration was
        // scaffolded empty and GameDbContextModelSnapshot.cs is unchanged. Each
        // file has exactly two Up operations (insert + update) and two Down
        // operations (delete + update).
        foreach (var row in ProvisionedRows)
        {
            var operations = ReadMigrationOperations(row.Migration);

            Assert.Equal(4, CountOccurrences(operations, "migrationBuilder."));
            Assert.Equal(1, CountOccurrences(operations, "migrationBuilder.InsertData("));
            Assert.Equal(1, CountOccurrences(operations, "migrationBuilder.DeleteData("));
            Assert.Equal(2, CountOccurrences(operations, "migrationBuilder.UpdateData("));

            foreach (var schemaOperation in new[]
                     {
                         "CreateTable", "DropTable", "AddColumn", "DropColumn",
                         "AlterColumn", "CreateIndex", "DropIndex", "AddForeignKey",
                         "DropForeignKey", "AddPrimaryKey", "DropPrimaryKey",
                         "CreateSequence", "DropSequence", "RenameColumn",
                         "RenameTable", "AddCheckConstraint", "DropCheckConstraint",
                         "migrationBuilder.Sql(", "HasData",
                     })
            {
                Assert.DoesNotContain(schemaOperation, operations, StringComparison.Ordinal);
            }
        }
    }

    // -----------------------------------------------------------------------
    // Down — the exact mirror, independent of the other Slice 4 migrations
    // -----------------------------------------------------------------------

    [Fact]
    public void EachDown_ShouldDeleteOnlyItsOwnRowAndRestoreNull()
    {
        // TASK-309 §7 / §10: Down removes exactly the row its own Up inserted and
        // restores that Boss's pre-migration not-authored NULL. It must not depend
        // on a later migration, so it names only its own CardDefinitionId and
        // BossDefinitionId.
        foreach (var row in ProvisionedRows)
        {
            var down = DownOperations(row.Migration);

            var delete = Assert.Single(
                Regex.Matches(down, CardDeleteDataPattern, RegexOptions.Singleline));

            Assert.Equal(row.CardDefinitionId, delete.Groups["key"].Value);

            var updates = Regex.Matches(
                down, BossUpdateDataPattern, RegexOptions.Singleline);

            var restore = Assert.Single(updates);

            Assert.Equal(row.BossDefinitionId, restore.Groups["key"].Value);
            Assert.Equal("CastPriorityScore", restore.Groups["column"].Value);
            Assert.Equal("null", restore.Groups["value"].Value.Trim());

            // Exactly one row and exactly one Boss per file: no other card id and
            // no other BossDefinitionId appears in the Down operations.
            Assert.Equal(
                [row.CardDefinitionId],
                CardDefinitionIdLiterals(down));

            Assert.Equal(
                [row.BossDefinitionId],
                BossDefinitionIdLiterals(down));
        }
    }

    [Fact]
    public void EachMigration_ShouldNotDependOnAnyOtherSlice4Migration()
    {
        // TASK-309 §7: "Down of an earlier migration must not depend on a later
        // one." Each file's operations mention only its own row's ids, so any
        // subset can be reverted independently — proved here for every ordering of
        // the other three ids.
        foreach (var row in ProvisionedRows)
        {
            var operations = ReadMigrationOperations(row.Migration);

            foreach (var other in ProvisionedRows.Where(other => other != row))
            {
                Assert.DoesNotContain(other.CardDefinitionId, operations, StringComparison.Ordinal);
                Assert.DoesNotContain(other.BossDefinitionId, operations, StringComparison.Ordinal);
            }
        }
    }

    // -----------------------------------------------------------------------
    // The stored effect payloads — DATABASE.md §1 contract
    // -----------------------------------------------------------------------

    [Fact]
    public void Payloads_ShouldRoundTripThroughTheStrictReaderToTheSameBytes()
    {
        // DATABASE.md §1 items 1–9 / §1 item 6: the stored array is read by one
        // strict reader and written by one writer. Each provisioned payload must be
        // accepted, must expose exactly the approved members, and must serialize
        // back to the same bytes — with duration required on AtkBuff/AtkDebuff and
        // scope required on NextAtkBuff, and absent where the contract forbids them.
        foreach (var row in ProvisionedRows)
        {
            var parsed = CardEffectDefinitions.FromPersistedPayload(row.Payload);

            Assert.Equal(1, parsed.Count);
            Assert.Equal(row.Payload, parsed.ToPersistedPayload());

            var element = parsed[0];
            var expected = row.Effect[0];

            Assert.Equal(expected.EffectType, element.EffectType);
            Assert.Equal(expected.ValueType, element.ValueType);
            Assert.Equal(expected.Value, element.Value);
            Assert.Equal(expected.Duration, element.Duration);
            Assert.Equal(expected.Scope, element.Scope);
            Assert.NotNull(element.Value);
            Assert.True(element.Value > 0);
        }

        // Effect-specific members, exactly as TASK-309 §1 D-1 fixes them.
        var flameBurst = CardEffectDefinitions.FromPersistedPayload(
            DocumentedPayloadLiterals.Single(l => l.CardDefinitionId == "flame-burst").Payload)[0];

        Assert.Equal(CardEffectType.AtkBuff, flameBurst.EffectType);
        Assert.Equal(50, flameBurst.Value);
        Assert.Equal(3, flameBurst.Duration);
        Assert.Null(flameBurst.Scope);

        var root = CardEffectDefinitions.FromPersistedPayload(
            DocumentedPayloadLiterals.Single(l => l.CardDefinitionId == "root").Payload)[0];

        Assert.Equal(CardEffectType.AtkDebuff, root.EffectType);
        Assert.Equal(30, root.Value);
        Assert.Equal(3, root.Duration);
        Assert.Null(root.Scope);

        var drainPower = CardEffectDefinitions.FromPersistedPayload(
            DocumentedPayloadLiterals.Single(l => l.CardDefinitionId == "drain-power").Payload)[0];

        Assert.Equal(CardEffectType.PowerDrain, drainPower.EffectType);
        Assert.Equal(CardEffectValueType.Flat, drainPower.ValueType);
        Assert.Equal(20, drainPower.Value);
        Assert.Null(drainPower.Duration);
        Assert.Null(drainPower.Scope);

        var thunderStrike = CardEffectDefinitions.FromPersistedPayload(
            DocumentedPayloadLiterals.Single(l => l.CardDefinitionId == "thunder-strike").Payload)[0];

        Assert.Equal(CardEffectType.NextAtkBuff, thunderStrike.EffectType);
        Assert.Equal(50, thunderStrike.Value);
        Assert.Equal(CardEffectDefinition.NextAttackScope, thunderStrike.Scope);
        Assert.Null(thunderStrike.Duration);
    }

    // -----------------------------------------------------------------------
    // The model snapshot — no schema and no model seed
    // -----------------------------------------------------------------------

    [Fact]
    public void ModelSnapshot_ShouldBeUnchangedByTheDataMigrations()
    {
        // TASK-309 §9: no schema operation and no GameDbContextModelSnapshot.cs
        // change. The snapshot is the last schema migration's model and contains
        // no hint of the provisioned rows or of a model seed, so `dotnet ef
        // migrations has-pending-model-changes` reports no changes (V-3 evidence
        // above).
        var snapshot = ReadMigrationSourceFile("GameDbContextModelSnapshot");

        Assert.DoesNotContain(".HasData(", snapshot, StringComparison.Ordinal);
        Assert.DoesNotContain("GetSeedData", snapshot, StringComparison.Ordinal);

        foreach (var forbidden in new[]
                 {
                     "flame-burst", "root", "drain-power", "thunder-strike",
                     "\"Flame Burst\"", "\"Root\"", "\"Drain Power\"", "\"Thunder Strike\"",
                 })
        {
            Assert.DoesNotContain(forbidden, snapshot, StringComparison.Ordinal);
        }
    }

    // -----------------------------------------------------------------------
    // The provisioned set — DATABASE.md §5 item 4 (9 -> 13 rows)
    // -----------------------------------------------------------------------

    [Fact]
    public void ProvisionedSet_ShouldBeThirteenRows()
    {
        // DATABASE.md §5 item 4: the four Slice 4 rows take the provisioned
        // CardDefinition set from 9 to 13 — the eight Player Cards, Slice 3's
        // earthquake, and these four. Each is inserted by exactly one of the four
        // migrations and by no other, so the set is a partition with no duplicate.
        Assert.Equal(13, AllThirteenProvisionedCardIds.Length);
        Assert.Equal(
            13,
            AllThirteenProvisionedCardIds.Distinct(StringComparer.Ordinal).Count());

        // The nine pre-existing rows are not touched by any Slice 4 migration.
        foreach (var existing in AllThirteenProvisionedCardIds.Except(
                     ProvisionedRows.Select(row => row.CardDefinitionId), StringComparer.Ordinal))
        {
            foreach (var row in ProvisionedRows)
            {
                Assert.DoesNotContain(
                    $"\"{existing}\"",
                    ReadMigrationOperations(row.Migration),
                    StringComparison.Ordinal);
            }
        }

        // Exactly four new rows; `earthquake` (Slice 3) keeps its own migration.
        Assert.Equal(4, ProvisionedRows.Length);
        Assert.DoesNotContain("earthquake", ProvisionedRows.Select(row => row.CardDefinitionId));
        Assert.Contains("earthquake", AllThirteenProvisionedCardIds);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// The <c>Up</c> half of a migration's executable operations. Each file
    /// carries one Up and one mirrored Down, so a per-direction assertion must
    /// look at only its own half.
    /// </summary>
    private static string UpOperations(string fileNameSuffix) =>
        SplitDirections(ReadMigrationOperations(fileNameSuffix)).Up;

    /// <summary>
    /// The <c>Down</c> half of a migration's executable operations.
    /// </summary>
    private static string DownOperations(string fileNameSuffix) =>
        SplitDirections(ReadMigrationOperations(fileNameSuffix)).Down;

    private static (string Up, string Down) SplitDirections(string operations)
    {
        var downStart = operations.IndexOf(
            "protected override void Down", StringComparison.Ordinal);

        Assert.True(downStart > 0, "A migration must declare both Up and Down.");

        return (operations[..downStart], operations[downStart..]);
    }

    private static string ReadMigrationOperations(string fileNameSuffix)
    {        var source = ReadMigrationSourceFile(fileNameSuffix);
        var builder = new System.Text.StringBuilder();

        foreach (var rawLine in source.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');

            // Drop XML-documentation and ordinary comment lines so assertions
            // target the executable operations only; the migration documents the
            // contract, including forbidden mechanism names, in its comments.
            if (line.TrimStart().StartsWith("///", StringComparison.Ordinal) ||
                line.TrimStart().StartsWith("//", StringComparison.Ordinal))
            {
                continue;
            }

            builder.AppendLine(line);
        }

        return builder.ToString();
    }

    private static string MigrationStamp(string fileNameSuffix)
    {
        var file = Directory
            .EnumerateFiles(MigrationsDirectory(), $"*_{fileNameSuffix}.cs")
            .Single(path => !path.EndsWith(".Designer.cs", StringComparison.Ordinal));

        return Path.GetFileNameWithoutExtension(file);
    }

    private static string ReadMigrationSourceFile(string fileNameSuffix)
    {
        var migrations = MigrationsDirectory();

        var file = Directory
            .EnumerateFiles(migrations, $"*_{fileNameSuffix}.cs")
            .SingleOrDefault(path => !path.EndsWith(".Designer.cs", StringComparison.Ordinal))
            ?? Directory
                .EnumerateFiles(migrations, $"*_{fileNameSuffix}.cs")
                .SingleOrDefault()
            ?? Directory
                .EnumerateFiles(migrations, $"{fileNameSuffix}.cs")
                .Single();

        return File.ReadAllText(file);
    }

    private static string MigrationsDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null &&
               !Directory.Exists(Path.Combine(directory.FullName, "src", "backend")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);

        var migrations = Path.Combine(
            directory!.FullName,
            "src",
            "backend",
            "GameServer.Infrastructure",
            "Postgres",
            "Migrations");

        Assert.True(Directory.Exists(migrations), $"Migrations folder not found at {migrations}.");

        return migrations;
    }

    private static string[] CardDefinitionIdLiterals(string migration) =>
        Regex.Matches(migration, "\"(card-[a-z0-9-]+|earthquake|flame-burst|root|drain-power|thunder-strike)\"")
            .Select(match => match.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

    private static string[] BossDefinitionIdLiterals(string migration) =>
        Regex.Matches(migration, "\"(boss-def-[a-z0-9-]+)\"")
            .Select(match => match.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

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
}
