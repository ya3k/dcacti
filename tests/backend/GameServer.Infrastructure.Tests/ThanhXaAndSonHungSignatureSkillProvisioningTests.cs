using System.Text;
using GameServer.Domain.Cards;
using GameServer.Domain.Elements;

namespace GameServer.Infrastructure.Tests;

/// <summary>
/// The TASK-168 provisioning migration for the two newly content-defined MVP Pet
/// Signature Skill Cards and their two owning Pets —
/// <c>DATABASE.md</c> §1/§5 item 4, <c>CARD_RULES.md</c> §1/§4.1,
/// <c>PET_RULES.md</c> §8, <c>PASSIVE_RULES.md</c> §8.
///
/// <b>Why these assertions live against the migration source.</b> The decided
/// mechanism is <b>migration-level <c>InsertData</c></b> — not model seed data
/// (<c>HasData</c>), not a startup loader, not a JSON pipeline. The InMemory
/// provider cannot apply a migration, so the migration's own source is the
/// authoritative record of what is provisioned. This mirrors the convention
/// <see cref="PetCardRelicDefinitionProvisioningTests"/> established for TASK-085
/// and <see cref="BossPersistenceTests"/> for TASK-053; the *applied* rows are
/// covered separately by
/// <see cref="PetCardRelicDefinitionPostgresProvisioningTests"/>.
///
/// What is verified is the documented contract only: exactly four inserted rows,
/// the canonical IDs, every value transcribed from its owning domain document, the
/// FK-safe insert order, a mirror <c>DeleteData</c> per insert, the absence of any
/// schema operation or seed mechanism, and the untouched pre-existing row set.
/// </summary>
public class ThanhXaAndSonHungSignatureSkillProvisioningTests
{
    private const string MigrationSuffix = "ProvisionThanhXaAndSonHungSignatureSkills";

    // -----------------------------------------------------------------------
    // Canonical IDs — DATABASE.md §1 value forms (`card-`/`pet-` + ASCII
    // kebab-case of the documented display name, TASK-082 decision B).
    //
    // Declared here as the literal documented values, so a drifted migration
    // fails against the contract itself rather than against its own contents.
    // TASK-167's Completion Evidence names both Card keys; PASSIVE_RULES.md §8's
    // derivation rule yields both PassiveIds from the Pets' documented names.
    // -----------------------------------------------------------------------

    private static readonly string[] CanonicalCardDefinitionIds =
    [
        "card-venomous-bloom",
        "card-earthshaker",
    ];

    private static readonly string[] CanonicalPetDefinitionIds =
    [
        "pet-thanh-xa",
        "pet-son-hung",
    ];

    // -----------------------------------------------------------------------
    // The pre-existing row set this migration must not touch
    // (TASK-085: 6 Cards / 3 Pets / 4 Relics).
    // -----------------------------------------------------------------------

    private static readonly string[] PreExistingCardDefinitionIds =
    [
        "card-heal", "card-shield", "card-power-charge",
        "card-inferno", "card-tidal-barrier", "card-iron-fang",
    ];

    private static readonly string[] PreExistingPetDefinitionIds =
    [
        "pet-xich-lang", "pet-bach-ho", "pet-huyen-quy",
    ];

    // -----------------------------------------------------------------------
    // DATABASE.md §5 item 4 — the module still seeds nothing
    // -----------------------------------------------------------------------

    [Fact]
    public void MigrationSource_ShouldContainNoHasDataOrRuntimeSeeder()
    {
        // DATABASE.md §5 item 4 forbids `HasData`, a seed runner, a startup
        // loader, a JSON content pipeline, and an external content service. The
        // migration is the only provisioning mechanism, and it must stay a
        // migration.
        var operations = ReadMigrationOperations();

        foreach (var forbidden in new[]
                 {
                     "HasData", "UseSeeding", "UseAsyncSeeding", "IHostedService",
                     "BackgroundService", "Seeder", "SeedAsync", "JsonSerializer",
                     ".json",
                 })
        {
            Assert.DoesNotContain(forbidden, operations, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void MigrationSource_ShouldContainNoSchemaOperation()
    {
        // TASK-168 is data only. The two tables, their keys, the
        // SignatureSkillCardId FK, and every column already exist (the
        // Add*Persistence migrations, which stay untouched). EF generated no
        // schema difference for this migration — it scaffolded with EMPTY Up/Down
        // bodies and GameDbContextModelSnapshot.cs is unchanged — so any schema
        // operation appearing here would be a contract violation rather than an
        // unavoidable generation artifact.
        //
        // `migrationBuilder.Sql(` is included because TASK-112 used SQL for a
        // re-encode; an InsertData-only migration has no reason to issue one.
        var operations = ReadMigrationOperations();

        foreach (var schemaOperation in new[]
                 {
                     "CreateTable", "DropTable", "AddColumn", "DropColumn",
                     "AlterColumn", "CreateIndex", "DropIndex", "AddForeignKey",
                     "DropForeignKey", "AddPrimaryKey", "DropPrimaryKey",
                     "CreateSequence", "DropSequence", "RenameColumn",
                     "RenameTable", "AddCheckConstraint", "DropCheckConstraint",
                     "migrationBuilder.Sql(",
                 })
        {
            Assert.DoesNotContain(schemaOperation, operations);
        }
    }

    [Fact]
    public void ModelSnapshot_ShouldBeUnchangedByTheDataMigration()
    {
        // A data-only migration must not change the model. The snapshot therefore
        // declares no seed data for the content tables and contains no hint of the
        // provisioned rows — the source-level proof that the migration added no
        // schema.
        //
        // The seed-data token is matched in its call form, because the snapshot
        // legitimately contains ".HasDatabaseName(...)" for the existing indexes.
        var snapshot = ReadMigrationSourceFile("GameDbContextModelSnapshot");

        foreach (var forbidden in new[]
                 {
                     ".HasData(", "GetSeedData",
                     "card-venomous-bloom", "card-earthshaker",
                     "pet-thanh-xa", "pet-son-hung",
                     "card-heal", "card-inferno", "pet-xich-lang",
                     "pet-bach-ho", "pet-huyen-quy", "relic-berserker-core",
                     "relic-mana-crystal",
                     "relic-assassin-eye", "relic-emergency-core",
                 })
        {
            Assert.DoesNotContain(forbidden, snapshot);
        }
    }

    // -----------------------------------------------------------------------
    // Row set and order — DATABASE.md §1/§2/§5 item 4
    // -----------------------------------------------------------------------

    [Fact]
    public void MigrationSource_ShouldInsertExactlyFourDefinitionRows()
    {
        // TASK-168: 2 CardDefinition + 2 PetDefinition, and nothing else — no
        // Relic row, no Boss row, no PassiveDefinition row.
        var operations = ReadMigrationOperations();

        Assert.Equal(4, CountOccurrences(operations, "migrationBuilder.InsertData("));
        Assert.Equal(4, CountOccurrences(operations, "migrationBuilder.DeleteData("));
        Assert.Equal(8, CountOccurrences(operations, "migrationBuilder."));

        // Per-table insertion counts, counted by the inserted primary-key value
        // rather than by the table name (which also appears in the Pet rows'
        // SignatureSkillCardId column list).
        Assert.Equal(
            2,
            CanonicalCardDefinitionIds.Count(id => operations.Contains($"values: new object[] {{ \"{id}\"")));
        Assert.Equal(
            2,
            CanonicalPetDefinitionIds.Count(id => operations.Contains($"values: new object[] {{ \"{id}\"")));

        // No third table of any kind is written.
        Assert.DoesNotContain("table: \"RelicDefinition\"", operations);
        Assert.DoesNotContain("table: \"BossDefinition\"", operations);
        Assert.DoesNotContain("table: \"PassiveDefinition\"", operations);
    }

    [Fact]
    public void MigrationSource_ShouldInsertEveryCanonicalIdExactlyOnce()
    {
        // DATABASE.md §1 value forms. Each id appears once in the Up insert and
        // once in the Down delete key — never twice in either, so no row is
        // provisioned twice and none is left behind.
        var operations = ReadMigrationOperations();

        foreach (var canonicalId in CanonicalCardDefinitionIds.Concat(CanonicalPetDefinitionIds))
        {
            // Each id appears once as an inserted value and once as a Down key.
            // The value form is matched with its closing quote, so an id that is
            // a prefix of another cannot inflate the count.
            Assert.Equal(
                1,
                CountOccurrences(operations, $"values: new object[] {{ \"{canonicalId}\""));
            Assert.Equal(
                1,
                CountOccurrences(operations, $"keyValue: \"{canonicalId}\")"));
        }

        Assert.Equal(4, CanonicalCardDefinitionIds.Length + CanonicalPetDefinitionIds.Length);
    }

    [Fact]
    public void MigrationSource_ShouldInsertCardsBeforeThePetsThatReferenceThem()
    {
        // DATABASE.md §2: PetDefinition.SignatureSkillCardId is an FK to
        // CardDefinition with the existing Restrict delete behavior. The
        // SignatureSkillCardId values are required and non-nullable, so the
        // referenced Card rows must exist first or the insert fails on the
        // foreign key.
        //
        // The comparison is positional over the Up body only, because the Down
        // body legitimately mentions both tables in the reverse order.
        var up = UpBody();

        var lastCardInsert = up.LastIndexOf("table: \"CardDefinition\"", StringComparison.Ordinal);
        var firstPetInsert = up.IndexOf("table: \"PetDefinition\"", StringComparison.Ordinal);

        Assert.True(lastCardInsert >= 0, "The migration inserts no CardDefinition row.");
        Assert.True(firstPetInsert >= 0, "The migration inserts no PetDefinition row.");
        Assert.True(
            lastCardInsert < firstPetInsert,
            "Every CardDefinition row must be inserted before any PetDefinition row (DATABASE.md §2 FK).");
    }

    [Fact]
    public void MigrationSource_ShouldDeletePetsBeforeTheCardsTheyReference()
    {
        // The mirror of the FK constraint: Down must remove the referencing Pet
        // rows before the referenced Card rows, or the Restrict FK rejects it.
        var down = DownBody();

        var lastPetDelete = down.LastIndexOf("table: \"PetDefinition\"", StringComparison.Ordinal);
        var firstCardDelete = down.IndexOf("table: \"CardDefinition\"", StringComparison.Ordinal);

        Assert.True(lastPetDelete >= 0, "The Down method deletes no PetDefinition row.");
        Assert.True(firstCardDelete >= 0, "The Down method deletes no CardDefinition row.");
        Assert.True(
            lastPetDelete < firstCardDelete,
            "Down must delete PetDefinition rows before the CardDefinition rows they reference (DATABASE.md §2 FK).");
    }

    [Fact]
    public void MigrationSource_ShouldDeleteOnlyItsOwnProvisionedRows()
    {
        // TASK-168: Down removes exactly the four rows this migration introduced —
        // no broad table delete, no unrelated row, and every delete keyed by a
        // canonical primary key. No pre-existing row is named anywhere in the
        // file, so the migration cannot update, re-encode, or delete the 13 rows
        // TASK-085 provisioned (AGENTS.md §16).
        var operations = ReadMigrationOperations();
        var down = DownBody();

        Assert.Equal(4, CountOccurrences(down, "migrationBuilder.DeleteData("));
        Assert.DoesNotContain("migrationBuilder.Sql(", down);
        Assert.DoesNotContain("Update", operations);
        Assert.DoesNotContain("Delete(", operations);

        // Each delete names its key column and value explicitly.
        Assert.Equal(4, CountOccurrences(down, "keyColumn:"));
        Assert.Equal(4, CountOccurrences(down, "keyValue:"));

        // No pre-existing content row is named by this migration at all.
        foreach (var existingId in PreExistingCardDefinitionIds.Concat(PreExistingPetDefinitionIds))
        {
            Assert.DoesNotContain(existingId, operations);
        }

        Assert.DoesNotContain("relic-", operations);
        Assert.DoesNotContain("boss-def-", operations);
    }

    // -----------------------------------------------------------------------
    // CardDefinition values — CARD_RULES.md §1, §4.1; DATABASE.md §1 items 1–9
    // -----------------------------------------------------------------------

    [Fact]
    public void MigrationSource_ShouldWriteTheDocumentedPetSkillCards()
    {
        // CARD_RULES.md §4.1 owns both Signature Skills' Cost, damage value, Burn
        // value, and Burn duration. Category PetSkill encodes as 1 (§1) and
        // LoadoutCopyLimit = 1 per §1 item 5, which fixes that value for every
        // CardDefinition the document defines. The display names carry no
        // diacritics; §4.1 spells them "Venomous Bloom" and "Earthshaker".
        var operations = ReadMigrationOperations();

        Assert.Contains(
            "\"card-venomous-bloom\", \"Venomous Bloom\", 1, 80, 1, ",
            operations);

        Assert.Contains(
            "\"card-earthshaker\", \"Earthshaker\", 1, 100, 1, ",
            operations);
    }

    [Fact]
    public void MigrationSource_ShouldEncodeCategoryAsTheDocumentedEnumValue()
    {
        // DATABASE.md §1/§3: Category ∈ {Basic, PetSkill}, stored as the Domain
        // enum's numeric value (CardDefinitionConfiguration's
        // HasConversion<int>()). Both new rows are PetSkill = 1 — never the enum
        // NAME, which would not convert.
        var operations = ReadMigrationOperations();

        Assert.Equal(0, (int)CardCategory.Basic);
        Assert.Equal(1, (int)CardCategory.PetSkill);

        Assert.DoesNotContain("\"Basic\"", operations);
        Assert.DoesNotContain("\"PetSkill\"", operations);

        Assert.Contains("\"card-venomous-bloom\", \"Venomous Bloom\", 1,", operations);
        Assert.Contains("\"card-earthshaker\", \"Earthshaker\", 1,", operations);
    }

    [Fact]
    public void MigrationSource_ShouldEncodeVenomousBloomAsATwoElementEffectArray()
    {
        // CARD_RULES.md §4.1 — Venomous Bloom (Thanh Xà's Signature Skill):
        //   Cost:   80 Power
        //   Effect: Deal 80 Mộc (Wood) damage ...; apply Burn 25 damage per tick
        //           for 2 Turns, Element Hỏa (Fire)
        //
        // DATABASE.md §1 item 1: the stored value is an ARRAY, one element per
        // effect, each carrying its own effectType/valueType/value triple (D-1,
        // D-1a). §4.1 states TWO effects for this Skill, so the row stores a
        // TWO-ELEMENT array in §4.1's effect order (damage, then Burn).
        //
        // `duration` is the Burn element's REQUIRED extra member (DATABASE.md §1
        // item 1 / §3 — present iff effectType = Burn). Omitting it is a contract
        // violation the strict reader rejects loudly (§1 item 6), so its presence
        // is asserted explicitly rather than left to the value comparison.
        var operations = ReadMigrationOperations();

        Assert.Contains(
            "[{\\\"effectType\\\":\\\"Damage\\\",\\\"valueType\\\":\\\"Flat\\\",\\\"value\\\":80},"
            + "{\\\"effectType\\\":\\\"Burn\\\",\\\"valueType\\\":\\\"Flat\\\",\\\"value\\\":25,\\\"duration\\\":2}]",
            operations);

        // The Burn element's duration is 2 Turns and is not omitted or defaulted.
        Assert.Contains("\\\"duration\\\":2", operations);
    }

    [Fact]
    public void MigrationSource_ShouldEncodeEarthshakerAsAOneElementEffectArrayWithoutBurn()
    {
        // CARD_RULES.md §4.1 — Earthshaker (Sơn Hùng's Signature Skill):
        //   Cost:   100 Power
        //   Effect: Deal 150 Thổ (Earth) damage (flat base value ...)
        //
        // §4.1 states exactly ONE effect for this Skill and NO Burn. Per TASK-111
        // D-1b a one-effect Card stores a ONE-ELEMENT array — the shape is uniform
        // for every Card — so this row has no element 2 and therefore no
        // `duration` member anywhere (DATABASE.md §3: present iff
        // effectType = Burn).
        var operations = ReadMigrationOperations();

        Assert.Contains(
            "[{\\\"effectType\\\":\\\"Damage\\\",\\\"valueType\\\":\\\"Flat\\\",\\\"value\\\":150}]",
            operations);

        // Exactly one `duration` member exists in the whole migration, and it
        // belongs to Venomous Bloom's Burn element — Earthshaker contributes none.
        Assert.Equal(1, CountOccurrences(operations, "\\\"duration\\\":"));
    }

    [Fact]
    public void MigrationSource_ShouldUseOnlyTheClosedEffectAndValueTypeSets()
    {
        // DATABASE.md §1 item 1 / §3: effectType ∈ {Heal, Shield, Power, Damage,
        // Burn, Crit} and valueType ∈ {Flat, PercentMaxHp, PercentagePoints,
        // Undetermined}. Both Skills use only Damage, Burn, and Flat — no new
        // EffectType, no new ValueType, and no Prose/Undetermined element (both
        // §4.1 magnitudes are authored, so nothing needs the unauthored marker,
        // §1 item 9).
        var operations = ReadMigrationOperations();

        foreach (var effectType in new[] { "\\\"effectType\\\":\\\"Damage\\\"", "\\\"effectType\\\":\\\"Burn\\\"" })
        {
            Assert.Contains(effectType, operations);
        }

        foreach (var forbidden in new[]
                 {
                     "\\\"effectType\\\":\\\"Heal\\\"", "\\\"effectType\\\":\\\"Shield\\\"",
                     "\\\"effectType\\\":\\\"Power\\\"", "\\\"effectType\\\":\\\"Crit\\\"",
                     "Heal", "Shield", "PercentagePoints", "PercentMaxHp",
                     "Undetermined", "Prose",
                 })
        {
            Assert.DoesNotContain(forbidden, operations);
        }

        Assert.Equal(3, CountOccurrences(operations, "\\\"valueType\\\":\\\"Flat\\\""));
        Assert.Equal(0, CountOccurrences(operations, "\\\"scope\\\":"));
    }

    [Fact]
    public void MigrationSource_ShouldStoreNoElementMemberOnEitherCardRow()
    {
        // CARD_RULES.md §4.1 states each effect's Element in PROSE — Venomous
        // Bloom's damage is Mộc and its Burn is Hỏa; Earthshaker's single effect
        // is Thổ — and ELEMENT_RULES.md §1.1/§5 establish that a Skill carries an
        // Element while an Effect (Burn) carries its own. That is exactly how
        // card-inferno's Hỏa Element already reaches the Damage Pipeline today,
        // which is why DATABASE.md §1's EffectDefinition member set is CLOSED and
        // contains no Element member and CardDefinition has no Element column
        // (§3).
        //
        // This is the storage-contract guard for the task's most tempting
        // violation: adding an Element/DamageElement/BurnElement member. The two
        // effects of Venomous Bloom are recorded as carrying DISTINCT Elements by
        // their prose owner, not by a storage member that does not exist.
        //
        // The guard is scoped to the two CardDefinition INSERT statements, because
        // `Element` IS a legitimate column of `PetDefinition` (DATABASE.md §1) —
        // the Pet rows below name it in their column array, which is required and
        // is not a Card-side member.
        var operations = ReadMigrationOperations();
        var cardStatements = CardInsertStatements(operations);

        Assert.Equal(2, CountOccurrences(cardStatements, "values: new object[] {"));

        foreach (var forbiddenMember in new[]
                 {
                     "element", "Element", "damageElement", "DamageElement",
                     "burnElement", "BurnElement", "\\\"element\\\"",
                 })
        {
            Assert.DoesNotContain(forbiddenMember, cardStatements);
        }

        // Each Card row's column array is exactly the documented six columns
        // (DATABASE.md §1) — no seventh, Element-carrier column was introduced.
        Assert.Equal(2, CountOccurrences(
            cardStatements,
            "columns: new[] { \"CardDefinitionId\", \"Name\", \"Category\", \"PowerCost\", \"LoadoutCopyLimit\", \"EffectDefinition\" }"));

        // The Element enum NAMES never appear as a stored Card value either. (An
        // EditData/Element-column token would also fail the no-schema-operation
        // test above.)
        foreach (var elementName in new[] { "Moc", "Tho", "Thuy", "Hoa", "Kim" })
        {
            Assert.DoesNotContain($"\\\"{elementName}\\\"", operations);
        }
    }

    // -----------------------------------------------------------------------
    // PetDefinition values — PET_RULES.md §8; PASSIVE_RULES.md §8
    // -----------------------------------------------------------------------

    [Fact]
    public void MigrationSource_ShouldWriteTheDocumentedPets()
    {
        // PET_RULES.md §8 owns each Pet's Element and Signature Skill;
        // PASSIVE_RULES.md §8 owns the PassiveId spelling and the match
        // threshold. Each Pet's Passive is carried directly on its PetDefinition
        // row (DATABASE.md §1) — there is no PassiveDefinition table.
        //
        // Identity carries the display text with its diacritics verbatim
        // (PET_RULES.md §1: PetDefinitionId is the technical identity, Identity is
        // the display text). A transliterated value would be an invented value.
        //
        //   Thanh Xà  — Mộc (0), "Every 7 Matches → Restore 8% HP",
        //               threshold 7, Venomous Bloom
        //   Sơn Hùng  — Thổ (1), "Every 5 Matches → Temp Defense",
        //               threshold 5, Earthshaker
        var operations = ReadMigrationOperations();

        Assert.Contains(
            "\"pet-thanh-xa\", \"Thanh Xà\", 0, \"passive-thanh-xa\", 7, \"card-venomous-bloom\"",
            operations);

        Assert.Contains(
            "\"pet-son-hung\", \"Sơn Hùng\", 1, \"passive-son-hung\", 5, \"card-earthshaker\"",
            operations);
    }

    [Fact]
    public void MigrationSource_ShouldEncodeElementAsTheDocumentedEnumValue()
    {
        // DATABASE.md §1 / PET_RULES.md §8 / ELEMENT_RULES.md §6: Element is stored
        // as the Domain Element enum's integer value
        // (PetDefinitionConfiguration's numeric mapping), so the inserted integer
        // must equal the enum member the rule document names — never a
        // hand-guessed number and never the enum NAME, which would not convert.
        var operations = ReadMigrationOperations();

        Assert.Equal(0, (int)Element.Moc);   // Thanh Xà
        Assert.Equal(1, (int)Element.Tho);   // Sơn Hùng

        Assert.Contains("\"Thanh Xà\", 0,", operations);
        Assert.Contains("\"Sơn Hùng\", 1,", operations);

        Assert.DoesNotContain("\"Moc\"", operations);
        Assert.DoesNotContain("\"Tho\"", operations);
        Assert.DoesNotContain("\"Hoa\"", operations);
        Assert.DoesNotContain("\"Kim\"", operations);
        Assert.DoesNotContain("\"Thuy\"", operations);
    }

    [Fact]
    public void MigrationSource_ShouldDeriveEachPassiveIdFromItsPetsDocumentedName()
    {
        // PASSIVE_RULES.md §8: a Pet passive's PassiveId is
        // `passive-<ascii-kebab-case-name>` of the owning Pet's documented name —
        // ASCII, so the diacritics are folded ("Thanh Xà" → thanh-xa,
        // "Sơn Hùng" → son-hung) and the technical key is never the display text.
        var operations = ReadMigrationOperations();

        Assert.Contains("\"passive-thanh-xa\"", operations);
        Assert.Contains("\"passive-son-hung\"", operations);

        // The display form is never used as the PassiveId.
        Assert.DoesNotContain("\"passive-Thanh", operations);
        Assert.DoesNotContain("\"passive-Sơn", operations);
        Assert.DoesNotContain("passive-thanh-xà", operations);
        Assert.DoesNotContain("passive-sơn-hùng", operations);
    }

    [Fact]
    public void MigrationSource_ShouldCreateNoPassiveDefinitionRow()
    {
        // DATABASE.md §1: PassiveId and PassiveThreshold are properties of
        // PetDefinition — there is NO PassiveDefinition table or entity, so the
        // migration must not write one.
        var operations = ReadMigrationOperations();

        Assert.DoesNotContain("table: \"PassiveDefinition\"", operations);
    }

    [Fact]
    public void MigrationSource_ShouldReferenceOnlyCardsItInserts()
    {
        // DATABASE.md §2: every PetDefinition.SignatureSkillCardId must resolve to
        // a provisioned CardDefinition row, or the insert fails on the FK. Each
        // referenced id is therefore one of the two cards inserted ABOVE it in
        // this same migration and is declared as a PetSkill (CARD_RULES.md §1
        // item 4).
        var operations = ReadMigrationOperations();

        var references = new (string CardId, string PetId)[]
        {
            ("card-venomous-bloom", "pet-thanh-xa"),
            ("card-earthshaker", "pet-son-hung"),
        };

        foreach (var (cardId, petId) in references)
        {
            Assert.Contains(cardId, CanonicalCardDefinitionIds);
            Assert.Contains($"\"{cardId}\"", operations);

            // The reference is emitted as the Pet row's final value, so no Pet row
            // points at a Card this migration does not insert.
            Assert.Contains($", \"{cardId}\" }});", operations);
            Assert.Contains(petId, CanonicalPetDefinitionIds);
        }
    }

    // -----------------------------------------------------------------------
    // Fixtures
    // -----------------------------------------------------------------------

    /// <summary>
    /// The two <c>CardDefinition</c> insert statements only, so a Card-side
    /// member guard is not tripped by the legitimate <c>PetDefinition.Element</c>
    /// column (<c>DATABASE.md</c> §1).
    /// </summary>
    private static string CardInsertStatements(string operations)
    {
        var builder = new StringBuilder();
        var lines = operations.Split('\n');

        for (var index = 0; index < lines.Length; index++)
        {
            if (!lines[index].Contains("table: \"CardDefinition\"", StringComparison.Ordinal))
            {
                continue;
            }

            // Each insert spans the table/columns/values triple.
            for (var offset = 0; offset < 3 && index + offset < lines.Length; offset++)
            {
                builder.AppendLine(lines[index + offset]);
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// The migration's <c>Up</c> body only, so a positional assertion is not
    /// disturbed by the <c>Down</c> body legitimately naming both tables.
    /// </summary>
    private static string UpBody()
    {
        var operations = ReadMigrationOperations();
        var upEnd = operations.IndexOf("protected override void Down", StringComparison.Ordinal);

        Assert.True(upEnd > 0, "The migration source declares no Down method.");

        return operations[..upEnd];
    }

    /// <summary>The migration's <c>Down</c> body only.</summary>
    private static string DownBody()
    {
        var operations = ReadMigrationOperations();
        var upEnd = operations.IndexOf("protected override void Down", StringComparison.Ordinal);

        Assert.True(upEnd > 0, "The migration source declares no Down method.");

        return operations[upEnd..];
    }

    /// <summary>
    /// A migration's source with line and XML-documentation comments removed,
    /// so an assertion targets the executable operations only. The migration
    /// documents the contract (including the forbidden mechanism names and the
    /// elements it deliberately does not store) in its comments, and a whole-file
    /// substring scan would otherwise flag that documentation.
    /// </summary>
    private static string ReadMigrationOperations()
    {
        var source = ReadMigrationSourceFile(MigrationSuffix);
        var builder = new StringBuilder();

        foreach (var rawLine in source.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');

            // Drop XML-documentation and ordinary comment lines. The migration
            // body contains no string literal that begins with "///" or "//",
            // so this cannot hide an operation.
            if (line.TrimStart().StartsWith("///", StringComparison.Ordinal) ||
                line.TrimStart().StartsWith("//", StringComparison.Ordinal))
            {
                continue;
            }

            builder.AppendLine(line);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Reads a file from the Infrastructure project's Migrations folder so its
    /// generated source can be asserted without a relational provider.
    ///
    /// Migrations are named <c>&lt;timestamp&gt;_&lt;Name&gt;.cs</c>; the model
    /// snapshot is a fixed file name with no timestamp.
    /// </summary>
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
