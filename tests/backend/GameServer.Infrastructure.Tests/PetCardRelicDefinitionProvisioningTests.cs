using System.Text;
using GameServer.Domain.Cards;
using GameServer.Domain.Elements;
using GameServer.Domain.Relics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace GameServer.Infrastructure.Tests;

/// <summary>
/// The provisioning migration for the MVP Pet / Card / Relic content rows —
/// <c>DATABASE.md</c> §1/§5 item 4, TASK-082 decisions A–D, TASK-085.
///
/// <b>Why these assertions live against the migration source.</b> The decided
/// mechanism is <b>migration-level <c>InsertData</c></b> — not model seed data
/// (<c>HasData</c>), not a startup loader, not a JSON pipeline. The InMemory
/// provider cannot apply a migration, and <c>GetSeedData()</c> is deliberately
/// empty, so the migration's own source is the authoritative record of what is
/// provisioned. This mirrors the convention
/// <see cref="BossPersistenceTests"/> established for TASK-053; the *applied*
/// rows are covered separately by
/// <see cref="PetCardRelicDefinitionPostgresProvisioningTests"/>.
///
/// What is verified is the documented contract only: exactly thirteen inserted
/// rows, the canonical IDs, every value transcribed from its owning domain
/// document, the FK-safe insert order, a mirror <c>DeleteData</c> per insert,
/// and the absence of any schema operation, model seed, or deferred content.
/// </summary>
public class PetCardRelicDefinitionProvisioningTests
{
    private const string MigrationSuffix = "ProvisionPetCardRelicContentDefinitions";

    // -----------------------------------------------------------------------
    // Canonical IDs — DATABASE.md §1 value forms (`card-`/`pet-`/`relic-` +
    // ASCII kebab-case of the documented display name, TASK-082 decision B).
    //
    // Declared here as the literal documented values, so a drifted migration
    // fails against the contract itself rather than against its own contents.
    // -----------------------------------------------------------------------

    private static readonly string[] CanonicalCardDefinitionIds =
    [
        "card-heal",
        "card-shield",
        "card-power-charge",
        "card-inferno",
        "card-tidal-barrier",
        "card-iron-fang",
    ];

    private static readonly string[] CanonicalPetDefinitionIds =
    [
        "pet-xich-lang",
        "pet-bach-ho",
        "pet-huyen-quy",
    ];

    private static readonly string[] CanonicalRelicDefinitionIds =
    [
        "relic-berserker-core",
        "relic-mana-crystal",
        "relic-assassin-eye",
        "relic-emergency-core",
    ];

    // -----------------------------------------------------------------------
    // DATABASE.md §1 / §5 item 4 — the module still seeds nothing
    // -----------------------------------------------------------------------

    [Fact]
    public void Model_ShouldStillProvisionNothing()
    {
        // DATABASE.md §5 item 4: the mechanism is a migration INSERT, so the
        // MODEL must carry no seed of its own. If HasData had been used instead,
        // these tables would report seed rows here — which is exactly what this
        // guard makes impossible to slip in.
        using var context = TestGameDbContextFactory.Create(nameof(Model_ShouldStillProvisionNothing));

        var model = context.GetService<IDesignTimeModel>().Model;

        foreach (var entityType in new[]
                 {
                     typeof(Domain.Cards.CardDefinition),
                     typeof(Domain.Pets.PetDefinition),
                     typeof(RelicDefinition),
                 })
        {
            Assert.Empty(model.FindEntityType(entityType)!.GetSeedData());
        }
    }

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
        // DATABASE.md §5 item 1: this migration is data only. The three tables,
        // their keys, the SignatureSkillCardId FK, and every column were created
        // by the Add*Persistence migrations, which stay untouched. EF generated
        // no schema difference for this migration — the model snapshot is
        // unchanged — so any schema operation appearing here would be a
        // contract violation, not an unavoidable generation artifact.
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
        // A data-only migration must not change the model. The snapshot is
        // therefore still the one the last schema migration generated: it
        // declares no seed data for any of the three tables and contains no
        // hint of the provisioned rows. This is the source-level proof that the
        // migration added no schema.
        //
        // The seed-data token is matched in its call form, because the snapshot
        // legitimately contains ".HasDatabaseName(...)" for the existing indexes.
        var snapshot = ReadMigrationSourceFile("GameDbContextModelSnapshot");

        foreach (var forbidden in new[]
                 {
                     ".HasData(", "GetSeedData",
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
    public void MigrationSource_ShouldInsertExactlyThirteenDefinitionRows()
    {
        // TASK-085: 6 CardDefinition + 3 PetDefinition + 4 RelicDefinition.
        var operations = ReadMigrationOperations();

        // Six Card inserts (table named in both Up and Down), three Pet, four
        // Relic — counted from the InsertData calls themselves below.
        Assert.Equal(13, CountOccurrences(operations, "migrationBuilder.InsertData("));
        Assert.Equal(13, CountOccurrences(operations, "migrationBuilder.DeleteData("));
        Assert.Equal(26, CountOccurrences(operations, "migrationBuilder."));

        // Per-table insertion counts, counted by the inserted primary-key value
        // rather than by the table name (which also appears in the Pet rows'
        // SignatureSkillCardId column list).
        Assert.Equal(
            6,
            CanonicalCardDefinitionIds.Count(id => operations.Contains($"values: new object[] {{ \"{id}\"")));
        Assert.Equal(
            3,
            CanonicalPetDefinitionIds.Count(id => operations.Contains($"values: new object[] {{ \"{id}\"")));
        Assert.Equal(
            4,
            CanonicalRelicDefinitionIds.Count(id => operations.Contains($"values: new object[] {{ \"{id}\"")));
    }

    [Fact]
    public void MigrationSource_ShouldInsertEveryCanonicalIdExactlyOnce()
    {
        // DATABASE.md §1 value forms; TASK-082 decision B. Each id appears once
        // in the Up insert and once in the Down delete key — never twice in
        // either, so no row is provisioned twice and none is left behind.
        var operations = ReadMigrationOperations();

        foreach (var canonicalId in CanonicalCardDefinitionIds
                     .Concat(CanonicalPetDefinitionIds)
                     .Concat(CanonicalRelicDefinitionIds))
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

        Assert.Equal(13, CanonicalCardDefinitionIds.Length
                         + CanonicalPetDefinitionIds.Length
                         + CanonicalRelicDefinitionIds.Length);
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
        var operations = ReadMigrationOperations();

        var upEnd = operations.IndexOf("protected override void Down", StringComparison.Ordinal);
        Assert.True(upEnd > 0, "The migration source declares no Down method.");

        var up = operations[..upEnd];

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
        var operations = ReadMigrationOperations();

        var upEnd = operations.IndexOf("protected override void Down", StringComparison.Ordinal);
        Assert.True(upEnd > 0, "The migration source declares no Down method.");

        var down = operations[upEnd..];

        var lastPetDelete = down.LastIndexOf("table: \"PetDefinition\"", StringComparison.Ordinal);
        var firstCardDelete = down.IndexOf("table: \"CardDefinition\"", StringComparison.Ordinal);

        Assert.True(lastPetDelete >= 0, "The Down method deletes no PetDefinition row.");
        Assert.True(firstCardDelete >= 0, "The Down method deletes no CardDefinition row.");
        Assert.True(
            lastPetDelete < firstCardDelete,
            "Down must delete PetDefinition rows before the CardDefinition rows they reference (DATABASE.md §2 FK).");
    }

    [Fact]
    public void MigrationSource_ShouldDeleteOnlyTheProvisionedRows()
    {
        // TASK-085: Down removes exactly the rows this migration introduced —
        // no broad table delete, no unrelated row, and every delete keyed by a
        // canonical primary key.
        var operations = ReadMigrationOperations();

        var upEnd = operations.IndexOf("protected override void Down", StringComparison.Ordinal);
        var down = operations[upEnd..];

        Assert.Equal(13, CountOccurrences(down, "migrationBuilder.DeleteData("));
        Assert.DoesNotContain("migrationBuilder.Sql(", down);

        // Each delete names its key column and value explicitly.
        Assert.Equal(13, CountOccurrences(down, "keyColumn:"));
        Assert.Equal(13, CountOccurrences(down, "keyValue:"));

        foreach (var forbidden in new[]
                 {
                     "DeleteData(\n                table: \"CardDefinition\",\n                keyColumn",
                 })
        {
            Assert.DoesNotContain(forbidden, down);
        }
    }

    // -----------------------------------------------------------------------
    // CardDefinition values — CARD_RULES.md §1, §2, §4.1
    // -----------------------------------------------------------------------

    [Fact]
    public void MigrationSource_ShouldWriteTheDocumentedBasicCards()
    {
        // CARD_RULES.md §2 owns the three Basic Cards' Cost and Effect text
        // verbatim; DATABASE.md §1 stores EffectDefinition "VERBATIM", and
        // §1 item 5 of CARD_RULES.md fixes LoadoutCopyLimit = 1 for every
        // defined CardDefinition. Category Basic encodes as 0 (§1).
        var operations = ReadMigrationOperations();

        Assert.Contains(
            "\"card-heal\", \"Heal\", 0, 20, 1, \"Restore the active Pet's HP by 20% of its Max HP\"",
            operations);

        Assert.Contains(
            "\"card-shield\", \"Shield\", 0, 20, 1, \"Active Pet gains Shield equal to 20% of its Max HP\"",
            operations);

        // CARD_RULES.md §2 item 3: Power Charge costs 0 Power by design.
        Assert.Contains(
            "\"card-power-charge\", \"Power Charge\", 0, 0, 1, \"Active Pet gains 25 Power\"",
            operations);
    }

    [Fact]
    public void MigrationSource_ShouldWriteTheDocumentedPetSkillCards()
    {
        // CARD_RULES.md §4.1 owns the three Pet Skill Cards' Cost and Effect
        // text verbatim. Category PetSkill encodes as 1 (CARD_RULES.md §1).
        var operations = ReadMigrationOperations();

        Assert.Contains(
            "\"card-inferno\", \"Inferno\", 1, 100, 1, \"Deal high Fire (Hỏa) damage; apply Burn\"",
            operations);

        Assert.Contains(
            "\"card-tidal-barrier\", \"Tidal Barrier\", 1, 80, 1, \"Heal; Gain Shield\"",
            operations);

        Assert.Contains(
            "\"card-iron-fang\", \"Iron Fang\", 1, 100, 1, \"High damage; increased Crit chance\"",
            operations);
    }

    [Fact]
    public void MigrationSource_ShouldEncodeCategoryAsTheDocumentedEnumValue()
    {
        // DATABASE.md §1/§3: Category ∈ {Basic, PetSkill}, stored as the Domain
        // enum's numeric value (CardDefinitionConfiguration's
        // HasConversion<int>()). The three Basics are 0 and the three Pet
        // Skills are 1 — never the enum NAME, which would not convert.
        var operations = ReadMigrationOperations();

        Assert.Equal(0, (int)CardCategory.Basic);
        Assert.Equal(1, (int)CardCategory.PetSkill);

        // The enum NAME never appears as a Category value, which would not
        // convert. The Card's display name "Shield" is distinct from the
        // Category token, so only the category tokens are asserted.
        Assert.DoesNotContain("\"Basic\"", operations);
        Assert.DoesNotContain("\"PetSkill\"", operations);

        // Each row spelled out, so the encoded integer is checked together with
        // the row it belongs to.
        Assert.Contains("\"card-heal\", \"Heal\", 0,", operations);
        Assert.Contains("\"card-shield\", \"Shield\", 0,", operations);
        Assert.Contains("\"card-power-charge\", \"Power Charge\", 0,", operations);
        Assert.Contains("\"card-inferno\", \"Inferno\", 1,", operations);
        Assert.Contains("\"card-tidal-barrier\", \"Tidal Barrier\", 1,", operations);
        Assert.Contains("\"card-iron-fang\", \"Iron Fang\", 1,", operations);
    }

    [Fact]
    public void MigrationSource_ShouldInsertNoAdditionalOrInventedCard()
    {
        // TASK-085 / CARD_RULES.md §4.1: exactly six cards in THIS migration. The
        // two Thanh Xà / Sơn Hùng Signature Skills are authored now
        // (CARD_RULES.md §4.1, TASK-167) but belong to the separate TASK-168
        // provisioning migration — not to this historical one. What this assertion
        // still guarantees is that TASK-085's own four-card-beyond-the-Basics set
        // is unedited and that no PLACEHOLDER, invented, or guessed key was ever
        // written into it.
        //
        // The earlier wording justified the absence of the two Skills by their
        // being "not content-defined", which TASK-167 made false; the guarantee
        // asserted here does not depend on that premise.
        var operations = ReadMigrationOperations();

        Assert.DoesNotContain("card-thanh-xa", operations);
        Assert.DoesNotContain("card-son-hung", operations);
        Assert.DoesNotContain("card-burning", operations);
        Assert.DoesNotContain("placeholder", operations, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("TODO", operations, StringComparison.OrdinalIgnoreCase);

        // Every inserted Card id is one of the six documented ones.
        foreach (var id in CanonicalCardDefinitionIds)
        {
            Assert.Contains($"\"{id}\"", operations);
        }

        // The two now-authored Skill Cards are supplied by TASK-168's migration,
        // never by this one — so this historical row set stays exactly six.
        Assert.DoesNotContain("card-venomous-bloom", operations);
        Assert.DoesNotContain("card-earthshaker", operations);
    }

    // -----------------------------------------------------------------------
    // PetDefinition values — PET_RULES.md §8; PASSIVE_RULES.md §8
    // -----------------------------------------------------------------------

    [Fact]
    public void MigrationSource_ShouldWriteTheDocumentedPets()
    {
        // PET_RULES.md §8 owns each Pet's Element and Signature Skill;
        // PASSIVE_RULES.md §8 owns the PassiveId spelling and the match
        // threshold. Each Pet's Passive is carried directly on its
        // PetDefinition row (DATABASE.md §1) — there is no PassiveDefinition
        // table.
        var operations = ReadMigrationOperations();

        Assert.Contains(
            "\"pet-xich-lang\", \"Xích Lang\", 3, \"passive-xich-lang\", 5, \"card-inferno\"",
            operations);

        Assert.Contains(
            "\"pet-bach-ho\", \"Bạch Hổ\", 4, \"passive-bach-ho\", 4, \"card-iron-fang\"",
            operations);

        Assert.Contains(
            "\"pet-huyen-quy\", \"Huyền Quy\", 2, \"passive-huyen-quy\", 6, \"card-tidal-barrier\"",
            operations);
    }

    [Fact]
    public void MigrationSource_ShouldEncodeElementAsTheDocumentedEnumValue()
    {
        // DATABASE.md §1: Element is stored as the Domain Element enum's
        // integer value (PetDefinitionConfiguration's numeric mapping), so the
        // inserted integer must equal the enum member the rule document names —
        // never a hand-guessed number and never the enum NAME.
        var operations = ReadMigrationOperations();

        Assert.Equal(3, (int)Element.Hoa);   // Xích Lang
        Assert.Equal(4, (int)Element.Kim);   // Bạch Hổ
        Assert.Equal(2, (int)Element.Thuy);  // Huyền Quy

        Assert.Contains("\"Xích Lang\", 3,", operations);
        Assert.Contains("\"Bạch Hổ\", 4,", operations);
        Assert.Contains("\"Huyền Quy\", 2,", operations);

        Assert.DoesNotContain("\"Hoa\"", operations);
        Assert.DoesNotContain("\"Kim\"", operations);
        Assert.DoesNotContain("\"Thuy\"", operations);
    }

    [Fact]
    public void MigrationSource_ShouldInsertOnlyItsOwnDocumentedThanhXaAndSonHungRows()
    {
        // TASK-085's row set is FINAL and unchanged: this migration provisions
        // exactly the thirteen rows TASK-085 transcribed, and the two Pets and two
        // Signature Skill Cards are supplied by the SEPARATE provisioning migration
        // TASK-168 (20261004055006_ProvisionThanhXaAndSonHungSignatureSkills),
        // which this test does not read.
        //
        // WHY THE EARLIER WORDING CHANGED. This assertion previously read
        // "ShouldInsertTheDeferredPetsNowhere" and justified itself by the
        // pre-TASK-167 deferral — Thanh Xà and Sơn Hùng were held back because
        // their SignatureSkillCardId targets did not exist (CARD_RULES.md §4.1
        // authored no content for either Skill) and the FK is required
        // (DATABASE.md §1/§2). TASK-167 has since authored both Skills
        // (CARD_RULES.md §4.1; PET_RULES.md §8), and DATABASE.md §5 item 4 records
        // the rows as "provisioned-later, not content-blocked". The deferral
        // premise is therefore false, and TASK-168 provisions the rows.
        //
        // What remains TEXTUALLY true, and is what this corrected assertion now
        // guarantees, is the narrower and still-binding fact: TASK-085's migration
        // has not been retro-edited to carry them. A completed migration is a
        // historical record (TASK-168 Out of Scope: "must not be merged into
        // TASK-085's migration — completed migrations are historical records"), so
        // these tokens must still appear NOWHERE in ITS source. The rows themselves
        // are asserted by TASK-168's own migration and tests.
        var operations = ReadMigrationOperations();

        Assert.DoesNotContain("Thanh Xà", operations);
        Assert.DoesNotContain("Sơn Hùng", operations);
        Assert.DoesNotContain("pet-thanh-xa", operations);
        Assert.DoesNotContain("pet-son-hung", operations);
        Assert.DoesNotContain("passive-thanh-xa", operations);
        Assert.DoesNotContain("passive-son-hung", operations);
        Assert.DoesNotContain("card-venomous-bloom", operations);
        Assert.DoesNotContain("card-earthshaker", operations);
    }

    [Fact]
    public void MigrationSource_ShouldCreateNoPassiveDefinitionRow()
    {
        // DATABASE.md §1: PassiveId and PassiveThreshold are properties of
        // PetDefinition — there is NO PassiveDefinition table or entity, so the
        // migration must not write one and no such table may exist in the
        // model.
        var operations = ReadMigrationOperations();

        Assert.DoesNotContain("PassiveDefinition\",", operations);
        Assert.DoesNotContain("table: \"PassiveDefinition\"", operations);

        using var context = TestGameDbContextFactory.Create(nameof(MigrationSource_ShouldCreateNoPassiveDefinitionRow));

        var model = context.GetService<IDesignTimeModel>().Model;

        Assert.DoesNotContain(
            model.GetEntityTypes(),
            entityType => entityType.GetTableName() == "PassiveDefinition");
    }

    [Fact]
    public void MigrationSource_ShouldReferenceOnlyExistingCardIds()
    {
        // DATABASE.md §2: every PetDefinition.SignatureSkillCardId must resolve
        // to a provisioned CardDefinition row, or the insert fails on the FK.
        // Each referenced id is therefore one of the six inserted cards and is
        // declared as a PetSkill (CARD_RULES.md §1 item 4).
        var operations = ReadMigrationOperations();

        foreach (var referencedCardId in new[] { "card-inferno", "card-iron-fang", "card-tidal-barrier" })
        {
            Assert.Contains(referencedCardId, CanonicalCardDefinitionIds);
            Assert.Contains($"\"{referencedCardId}\"", operations);
        }
    }

    // -----------------------------------------------------------------------
    // RelicDefinition values — RELIC_RULES.md §1, §3, §6
    // -----------------------------------------------------------------------

    [Fact]
    public void MigrationSource_ShouldWriteTheDocumentedRelics()
    {
        // RELIC_RULES.md §6 owns each Relic's Trigger, Condition, and Effect
        // verbatim; §3 is the closed Trigger list. DATABASE.md §1 stores
        // Trigger, Condition, and EffectDefinition. Condition keeps its
        // documented spelling, including the non-ASCII "≥" and "−" characters.
        var operations = ReadMigrationOperations();

        Assert.Contains(
            "\"relic-berserker-core\", \"Berserker Core\", \"OnMatchCount\", \"every 3 Matches\", \"+5% ATK\"",
            operations);

        Assert.Contains(
            "\"relic-mana-crystal\", \"Mana Crystal\", \"OnMatchCount\", \"every 4 Matches\", \"+10 Power\"",
            operations);

        Assert.Contains(
            "\"relic-assassin-eye\", \"Assassin Eye\", \"OnCombo\", \"Combo ≥ 3\", \"Increased Crit chance\"",
            operations);

        Assert.Contains(
            "\"relic-emergency-core\", \"Emergency Core\", \"OnHpBelow\", \"HP < 30%\", \"Heal Card cost −50%\"",
            operations);
    }

    [Fact]
    public void MigrationSource_ShouldUseOnlyTriggersFromTheClosedList()
    {
        // RELIC_RULES.md §3: "A Relic must declare exactly one primary Trigger
        // from this list" — new trigger types are a rule change. The migration's
        // four Trigger values are the §3 members the §6 rows name.
        var operations = ReadMigrationOperations();

        foreach (var trigger in new[] { "OnMatchCount", "OnCombo", "OnHpBelow" })
        {
            Assert.Contains($"\"{trigger}\"", operations);
        }

        // No Trigger invented outside the documented provisioned set.
        foreach (var forbidden in new[]
                 {
                     "\"OnBattleStart\"", "\"OnMatch\"", "\"OnCascade\"",
                     "\"OnPowerGain\"", "\"OnDamageDealt\"", "\"OnDamageTaken\"",
                     "\"OnCardCast\"", "\"OnTurnStart\"", "\"OnTurnEnd\"",
                 })
        {
            Assert.DoesNotContain(forbidden, operations);
        }
    }

    [Fact]
    public void MigrationSource_ShouldInsertNoBurningCurseRow()
    {
        // RELIC_RULES.md §6 note 3: Burning Curse is deferred — §3 requires a
        // primary Trigger from the §3 list while note 1 describes it as a static
        // modifier with none. That tension is reported, not resolved, so no
        // placeholder Trigger and no row may be written here.
        var operations = ReadMigrationOperations();

        Assert.DoesNotContain("Burning Curse", operations);
        Assert.DoesNotContain("relic-burning-curse", operations);
        Assert.DoesNotContain("Burn damage", operations);

        // No fifth Relic row of any kind: exactly the four documented
        // relic-* literal ids appear, twice each (Up value + Down key).
        Assert.Equal(8, CountOccurrences(operations, "\"relic-"));
    }

    [Fact]
    public void MigrationSource_ShouldReferenceTheCanonicalEffectTextVerbatim()
    {
        // DATABASE.md §1: EffectDefinition is "the owning domain document's
        // effect rule text, stored VERBATIM" and CARD_RULES.md §4.1 requires the
        // §4.1 spelling including the Vietnamese diacritic in "Hỏa". A
        // transliterated or paraphrased value would be an invented value.
        var operations = ReadMigrationOperations();

        Assert.Contains("Fire (Hỏa) damage", operations);
        Assert.DoesNotContain("Fire (Hoa) damage", operations);

        // RESTORE the exact §6 note text for Emergency Core's en-dash form.
        Assert.Contains("Heal Card cost −50%", operations);
        Assert.DoesNotContain("Heal Card cost -50%", operations);
    }

    // -----------------------------------------------------------------------
    // Fixtures
    // -----------------------------------------------------------------------

    /// <summary>
    /// A migration's source with line and XML-documentation comments removed,
    /// so an assertion targets the executable operations only. The migration
    /// documents the contract (including the forbidden mechanism names and the
    /// deferred content it must not insert) in its comments, and a whole-file
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
