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
/// ├── PassiveDefinition  (jsonb, NOT NULL)
/// └── SkillDefinition    (jsonb, NOT NULL)
/// </code>
///
/// <b>The field set is <c>DATABASE.md</c> §1's</b> — static content only. The
/// combat-definition values <c>MaxHP</c>, <c>ATK</c>, <c>DEF</c>, and
/// <c>EnrageThreshold</c> are <b>not</b> columns: §1's persistence contract
/// states they "remain sourced from the authoritative Domain
/// <c>BossDefinition</c> content at battle creation". They stay on the Domain
/// record and are deliberately unmapped here. No display-name column exists on
/// this table either (§1 note item 1).
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
        builder.Property(definition => definition.BossId)
            .HasConversion(id => id.Value, value => new BossId(value))
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

        // DATABASE.md §1 note item 1: the combat-definition values MaxHP, ATK,
        // DEF, and EnrageThreshold are NOT columns of this table — they "remain
        // sourced from the authoritative Domain BossDefinition content at battle
        // creation". They are declared on the Domain record outside its
        // constructor (the persisted surface), and are excluded here explicitly
        // so the model holds exactly the five documented columns: by convention
        // EF would otherwise map any settable property.
        builder.Ignore(definition => definition.MaxHP);
        builder.Ignore(definition => definition.ATK);
        builder.Ignore(definition => definition.DEF);
        builder.Ignore(definition => definition.EnrageThreshold);

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
                BaseDamage = skill.BaseDamage,
                ChargeRequirement = skill.ChargeRequirement,
                CooldownTurns = skill.CooldownTurns,
            },
            Options);

    public static BossSkillDefinition ReadSkill(string json)
    {
        var document = JsonSerializer.Deserialize<SkillDocument>(json, Options)!;

        return new BossSkillDefinition(
            document.SkillId,
            document.BaseDamage,
            document.ChargeRequirement,
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
    /// The stored members of the <c>SkillDefinition</c> document — exactly
    /// <c>skillId</c>, <c>baseDamage</c>, <c>chargeRequirement</c>,
    /// <c>cooldownTurns</c>.
    /// </summary>
    private sealed record SkillDocument
    {
        public required string SkillId { get; init; }

        public required int BaseDamage { get; init; }

        public required int ChargeRequirement { get; init; }

        public required int CooldownTurns { get; init; }
    }
}
