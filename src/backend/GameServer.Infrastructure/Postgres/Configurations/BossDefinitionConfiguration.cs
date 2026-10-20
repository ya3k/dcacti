using System.Text.Json;
using GameServer.Domain.Bosses;
using GameServer.Domain.Passives;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace GameServer.Infrastructure.Postgres.Configurations;

/// <summary>
/// The persistence mapping of <see cref="BossDefinition"/>
/// (<c>DATABASE.md</c> §1, §3).
///
/// <code>
/// BossDefinition
/// ├── BossDefinitionId   (PK)
/// ├── Identity           (unique)
/// ├── Element
/// ├── MaxHP              (int, NOT NULL, > 0)
/// ├── ATK                (int, NOT NULL, >= 0)
/// ├── DEF                (int, NOT NULL, >= 0)
/// ├── EnrageThreshold    (double precision, NOT NULL, (0, 1))
/// ├── CastPriorityScore  (int, NULL, NULL OR >= 0)
/// ├── PassiveDefinition  (jsonb, NOT NULL)
/// └── SkillDefinition    (jsonb, NOT NULL)
/// </code>
///
/// <b>The field set is <c>DATABASE.md</c> §1's</b> (<c>ADR-024</c> §2.2, as
/// amended in place by TASK-308). The row stores the static content — the
/// persistence key, the canonical Identity, the Element, and the two JSON
/// configuration objects — alongside the five Domain-Overlay values
/// <c>MaxHP</c>, <c>ATK</c>, <c>DEF</c>, <c>EnrageThreshold</c>, and the
/// nullable <c>CastPriorityScore</c>, which are authoritative at battle
/// creation and overlaid onto the structural Domain baseline. No display-name
/// column exists on this table (§1 note item 1).
///
/// <b>The Skill's secondary effect stays on the Domain type.</b> §1 note item 4
/// fixes the stored <c>SkillDefinition</c> document to its four members, so
/// <c>BossSkillDefinition.SecondaryEffect</c> is not persisted; the overlay
/// preserves it from the Domain baseline (<c>ADR-024</c> §2.2 item 4).
///
/// <b>The primary key is content-supplied, never generated.</b> §1 note item 2
/// (TASK-049) fixes <c>BossDefinitionId</c> as an independent stable
/// persistence key supplied by content/Domain: "No GUID, integer,
/// provider-generated, or database-generated key is used". It is mapped as a
/// bounded required string with no <c>HasDefaultValue</c> and no
/// store-generated behavior.
///
/// <b><c>Identity</c> is required and unique.</b> §3 documents
/// <c>BossDefinition.Identity NOT NULL, UNIQUE</c> — "the unique target of the
/// FK lookup in §1". The Domain carries it in the <see cref="BossId"/> wrapper,
/// so the column stores its <c>Value</c>.
///
/// <b>The two JSON objects are <c>jsonb</c>, NOT NULL, and are mapped as value
/// objects.</b> §1 note items 3–4 fix the member lists exactly, and §1 note
/// item 3 states the governing rule: "the JSON/storage names are the contract;
/// internal representation maps to them, not vice versa". The Domain is a flat
/// configuration record whose Passive and Skill members are individual values,
/// so each documented object is expressed here as a small <b>value object</b>
/// equal to those flat members, with a converter that writes exactly §1's JSON
/// and reads it back onto the record's members. That keeps the Domain a single
/// flat type (and its <c>with</c> overrides working) while storage holds the
/// documented shape.
///
/// <b>No index beyond the documented uniqueness is declared.</b>
/// <c>DATABASE.md</c> §4 lists no <c>BossDefinition</c> index, and §4 states
/// further indexes should be added only when a real query pattern requires
/// them.
/// </summary>
public sealed class BossDefinitionConfiguration : IEntityTypeConfiguration<BossDefinition>
{
    private static readonly JsonSerializerOptions JsonOptions = new();

    public void Configure(EntityTypeBuilder<BossDefinition> builder)
    {
        builder.ToTable("BossDefinition");

        builder.HasKey(definition => definition.BossDefinitionId);

        // DATABASE.md §1: BossDefinitionId (PK) — content-supplied, never
        // database-generated (note item 2, TASK-049).
        builder.Property(definition => definition.BossDefinitionId)
            .HasMaxLength(64)
            .IsRequired();

        // DATABASE.md §1/§3: Identity — the canonical technical Boss ID from
        // the BossId value wrapper; NOT NULL, UNIQUE (the §1 FK-lookup target).
        //
        // The converter is bound to the property so a query can compare the
        // column directly: the battle-end lookup filters on `Identity`, and a
        // value-typed wrapper whose `.Value` is only reachable inside the
        // expression tree would not translate to SQL. With the conversion
        // declared here, `definition.BossId == new BossId(identity)` — and the
        // equivalent comparison written as a projection over the wrapper — is a
        // plain column comparison at the provider.
        builder.Property(definition => definition.BossId)
            .HasConversion(
                id => id.Value,
                value => new BossId(value),
                new ValueComparer<BossId>(
                    (left, right) => left.Equals(right),
                    id => id.GetHashCode(),
                    id => new BossId(id.Value)))
            .HasColumnName("Identity")
            .HasMaxLength(64)
            .IsRequired();

        builder.HasIndex(definition => definition.BossId)
            .HasDatabaseName("IX_BossDefinition_Identity")
            .IsUnique();

        // DATABASE.md §1: Element — the closed Domain enum as its numeric value.
        builder.Property(definition => definition.Element)
            .HasConversion<int>()
            .IsRequired();

        // DATABASE.md §1/§3 and ADR-024 §2.2/§2.5: the four runtime combat-balance
        // columns. PostgreSQL is authoritative for these values at battle
        // creation; the Domain baseline supplies the structural mechanics and the
        // canonical seed values (BossRules), never a runtime fallback.
        //
        // No HasDefaultValue is configured (ADR-024 §2.5): the schema columns'
        // transitional defaults exist only inside the migration that adds them, so
        // an insert that omits a balance value fails the NOT NULL / CHECK
        // constraints instead of silently taking a default.
        builder.Property(definition => definition.MaxHP)
            .IsRequired();

        builder.Property(definition => definition.ATK)
            .IsRequired();

        builder.Property(definition => definition.DEF)
            .IsRequired();

        // Stored as double precision: the value is the documented fraction of
        // MaxHP (BOSS_RULES.md §5 item 4, §6.1), not an absolute HP figure.
        builder.Property(definition => definition.EnrageThreshold)
            .IsRequired();

        // DATABASE.md §1/§3, ADR-024 §2.2 as amended by TASK-308 (OD-3a): the
        // FIFTH Domain-Overlay value. It is nullable by contract — `NULL` is the
        // documented "no cast-priority score authored" marker, legal only while the
        // Boss has no Boss Card — so no default and no store-generated value is
        // configured here either (ADR-024 §2.5's no-transitional-default rule is
        // applied to it identically). The closed "a Boss Card form requires a
        // non-null value" rule is a content/creation-time rule owned by the
        // Application layer, not a database constraint: §1/§3 permit NULL on the
        // column.
        builder.Property(definition => definition.CastPriorityScore)
            .IsRequired(false);

        // DATABASE.md §3 and ADR-024 §2.4/§2.5: the four documented range
        // invariants, enforced by the database so a corrupt balance row cannot be
        // written at all. The runtime overlay validates the same ranges before it
        // uses a value (BossDefinitionLookup), so a row that predates the
        // constraint still fails closed rather than being applied.
        //
        // The fifth overlay value's constraint (TASK-308 / OD-3a) is the
        // nullable-range form DATABASE.md §1/§3 fixes exactly:
        // `CastPriorityScore IS NULL OR CastPriorityScore >= 0`.
        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "CK_BossDefinition_MaxHP_Positive",
                "\"MaxHP\" > 0");

            table.HasCheckConstraint(
                "CK_BossDefinition_ATK_NonNegative",
                "\"ATK\" >= 0");

            table.HasCheckConstraint(
                "CK_BossDefinition_DEF_NonNegative",
                "\"DEF\" >= 0");

            table.HasCheckConstraint(
                "CK_BossDefinition_EnrageThreshold_Range",
                "\"EnrageThreshold\" > 0.0 AND \"EnrageThreshold\" < 1.0");

            table.HasCheckConstraint(
                "CK_BossDefinition_CastPriorityScore_NonNegative",
                "\"CastPriorityScore\" IS NULL OR \"CastPriorityScore\" >= 0");
        });

        // DATABASE.md §1 note item 3 / §3: PassiveDefinition — jsonb, NOT NULL.
        // Mapped as a scalar with a value converter: EF Core binds immutable
        // records through their constructor, so a scalar conversion on a
        // constructor parameter is supported where an owned navigation is not.
        // The converter writes exactly §1's document.
        builder.Property(definition => definition.PassiveDefinition)
            .HasConversion(new PassiveDefinitionConverter())
            .HasColumnType("jsonb")
            .IsRequired();

        // DATABASE.md §1 note item 4 / §3: SkillDefinition — jsonb, NOT NULL.
        builder.Property(definition => definition.SkillDefinition)
            .HasConversion(new SkillDefinitionConverter())
            .HasColumnType("jsonb")
            .IsRequired();
    }

    /// <summary>
    /// Writes and reads <c>DATABASE.md</c> §1 note item 3's persisted
    /// <c>PassiveDefinition</c> document.
    ///
    /// <code>
    /// { "passiveId": "…", "threshold": 5, "resetBehavior": "Default" }
    /// </code>
    ///
    /// The member names come from the storage contract, not the Domain's field
    /// names (§1 note item 3: "the JSON/storage names are the contract;
    /// internal representation maps to them, not vice versa").
    /// </summary>
    private sealed class PassiveDefinitionConverter
        : ValueConverter<BossPassiveDefinition, string>
    {
        public PassiveDefinitionConverter()
            : base(
                passive => Write(passive),
                json => Read(json))
        {
        }

        private static string Write(BossPassiveDefinition passive) =>
            BossDefinitionJson.WritePassive(passive);

        private static BossPassiveDefinition Read(string json) =>
            BossDefinitionJson.ReadPassive(json);
    }

    /// <summary>
    /// Writes and reads <c>DATABASE.md</c> §1 note item 4's persisted
    /// <c>SkillDefinition</c> document.
    ///
    /// <code>
    /// { "skillId": "…", "baseDamage": 0, "chargeRequirement": 0, "cooldownTurns": 0 }
    /// </code>
    ///
    /// Exactly four members, all required; the battle-state member names
    /// (<c>GAME_STATE.md</c> §2.4) are a separate contract and are not used.
    ///
    /// <b>The Skill's secondary effect is deliberately not written.</b> The
    /// stored document is fixed to exactly these four members by §1 note item 4
    /// ("fixed by TASK-045 … unchanged"), so
    /// <c>BossSkillDefinition.SecondaryEffect</c> — the Domain configuration
    /// carrying <c>BOSS_RULES.md</c> §6.3.1's Skill effects — has no column and
    /// is excluded by this converter, which is the persistence boundary. The
    /// Domain Overlay (<c>ADR-024</c> §2.2 item 4) is what keeps it intact at
    /// runtime: the overlay takes the structural definition, including this
    /// member, from <c>BossDefinitions.All</c> and only the four balance values
    /// from the row.
    /// </summary>
    private sealed class SkillDefinitionConverter
        : ValueConverter<BossSkillDefinition, string>
    {
        public SkillDefinitionConverter()
            : base(
                skill => Write(skill),
                json => Read(json))
        {
        }

        private static string Write(BossSkillDefinition skill) =>
            BossDefinitionJson.WriteSkill(skill);

        private static BossSkillDefinition Read(string json) =>
            BossDefinitionJson.ReadSkill(json);
    }
}

/// <summary>
/// The persisted JSON documents of <c>BossDefinition</c>
/// (<c>DATABASE.md</c> §1 note items 3–4), and the mapping between them and the
/// Domain's two configuration groups.
///
/// <code>
/// PassiveDefinition  { "passiveId": "…", "threshold": 5, "resetBehavior": "Default" }
/// SkillDefinition    { "skillId": "…", "baseDamage": 0, "chargeRequirement": 0,
///                      "cooldownTurns": 0 }
/// </code>
///
/// <b>The member names are the storage contract.</b> §1 note item 3 states "the
/// JSON/storage names are the contract; internal representation maps to them,
/// not vice versa", so the document properties are named exactly as §1 spells
/// them and are not the Domain's names.
///
/// <b><c>threshold</c> is <c>int?</c> and <c>null</c> means always-active.</b>
/// §1 note item 3: "<c>null</c> means the Passive has <b>no threshold and is
/// always active</b>; <c>0</c> is never used as a 'no threshold' sentinel"
/// (<c>BOSS_RULES.md</c> §6.2's Thủy Ma).
///
/// It is public alongside <c>BossDefinitionConfiguration</c> so the persistence
/// tests can assert the exact stored document without a relational provider.
/// </summary>
public static class BossDefinitionJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        // DATABASE.md §1 note item 3: "the JSON/storage names are the contract".
        // The documented members are camelCase (passiveId, resetBehavior,
        // skillId, baseDamage, chargeRequirement, cooldownTurns), so the
        // convention is pinned here rather than left to a serializer default.
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static string WritePassive(BossPassiveDefinition passive) =>
        JsonSerializer.Serialize(
            new PassiveDocument
            {
                PassiveId = passive.PassiveId.Value,
                Threshold = passive.Threshold,
                ResetBehavior = passive.ResetBehavior,
            },
            Options);

    public static BossPassiveDefinition ReadPassive(string json)
    {
        var document = JsonSerializer.Deserialize<PassiveDocument>(json, Options)!;

        return new BossPassiveDefinition(
            new PassiveId(document.PassiveId),
            document.Threshold,
            document.ResetBehavior);
    }

    public static string WriteSkill(BossSkillDefinition skill) =>
        JsonSerializer.Serialize(
            new SkillDocument
            {
                SkillId = skill.SkillId,
                // OD-311-1 = B: the two legacy members stay part of the stored
                // document's shape, but they are no longer Domain configuration.
                // The application never writes a BossDefinition row —
                // provisioning is migration-only — so these placeholders only
                // keep the document contract stable for a caller that does.
                BaseDamage = 0,
                ChargeRequirement = 0,
                CooldownTurns = skill.CooldownTurns,
            },
            Options);

    public static BossSkillDefinition ReadSkill(string json)
    {
        var document = JsonSerializer.Deserialize<SkillDocument>(json, Options)!;

        // OD-311-1 = B: `baseDamage` and `chargeRequirement` are read off the
        // document and deliberately discarded. The Domain has no runtime or
        // Domain responsibility for them (ADR-029 Slice 5 / TASK-313), and a
        // stored value — including the historical 150/180 baseline drift — must
        // never reactivate the removed legacy Boss Skill behaviour.
        return new BossSkillDefinition(
            document.SkillId,
            document.CooldownTurns);
    }

    /// <summary>
    /// The stored members of the <c>PassiveDefinition</c> document — exactly
    /// <c>passiveId</c>, <c>threshold</c>, <c>resetBehavior</c>.
    /// </summary>
    private sealed record PassiveDocument
    {
        public required string PassiveId { get; init; }

        /// <summary><c>null</c> ⇔ always-active, no threshold; never <c>0</c>.</summary>
        public int? Threshold { get; init; }

        public required string ResetBehavior { get; init; }
    }

    /// <summary>
    /// The stored members of the <c>SkillDefinition</c> document —
    /// <c>skillId</c>, <c>baseDamage</c>, <c>chargeRequirement</c>,
    /// <c>cooldownTurns</c>.
    ///
    /// <b><c>baseDamage</c> and <c>chargeRequirement</c> are storage compatibility
    /// only (OD-311-1 = B, ADR-029 Slice 5 / TASK-313).</b> They are optional on
    /// read so both the historical documents and any newer document without them
    /// deserialize, and they are never mapped into the Domain — the Domain's
    /// <c>BossSkillDefinition</c> carries only <c>skillId</c> and
    /// <c>cooldownTurns</c>. No cleanup migration exists for them and no
    /// historical migration was rewritten.
    /// </summary>
    private sealed record SkillDocument
    {
        public required string SkillId { get; init; }

        /// <summary>
        /// Retained storage-compatibility member. Not read by the Domain.
        /// </summary>
        public int BaseDamage { get; init; }

        /// <summary>
        /// Retained storage-compatibility member. Not read by the Domain.
        /// </summary>
        public int ChargeRequirement { get; init; }

        public required int CooldownTurns { get; init; }
    }
}
