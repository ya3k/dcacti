using GameServer.Domain.Relics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameServer.Infrastructure.Postgres.Configurations;

/// <summary>
/// The persistence mapping of <see cref="RelicDefinition"/>
/// (<c>DATABASE.md</c> §1).
///
/// <code>
/// RelicDefinition
/// ├── RelicDefinitionId  (PK)
/// ├── Name
/// ├── Trigger            (a §3 Trigger identity — unchanged, prose)
/// ├── Condition          (jsonb, NULL — structured, optional)
/// └── EffectDefinition   (jsonb, NOT NULL — structured effect array)
/// </code>
///
/// <b>The field set is <c>DATABASE.md</c> §1's</b> — static content only. No
/// per-instance state (charges, stacks, cooldowns, duration) is mapped, because
/// no MVP Relic in <c>RELIC_RULES.md</c> §6 carries any and §1 places
/// <c>Reset/Cooldown</c> on the Relic's definition rather than on an owned copy.
///
/// <b><c>Condition</c> and <c>EffectDefinition</c> are stored as <c>jsonb</c>,
/// not as bounded prose.</b> <c>RELIC_RULES.md</c> §8.1/§8.2 define a structured
/// condition and a structured <c>EffectDefinition[]</c>, and §8.6 (TASK-131 D9)
/// records that the former <c>character varying(128)</c> columns are
/// <b>insufficient</b> for them — the same conclusion the <c>CardDefinition</c>
/// precedent reached for the identical contract shape. TASK-132's migration
/// <c>20261003090000_StructureRelicDefinitionStructuredColumns</c> performs the
/// type change and encodes the four provisioned rows.
///
/// <c>Condition</c> keeps its documented <b>optionality</b> (§8.1 item 4,
/// <c>RELIC_RULES.md</c> §1): it is nullable rather than defaulted to a sentinel
/// condition that would read as a real one. <c>EffectDefinition</c> is required
/// and NOT NULL, because every Relic states at least one effect.
///
/// <b><c>Trigger</c> is deliberately untouched.</b> <c>RELIC_RULES.md</c> §8.5
/// item 3 leaves §3's closed list unchanged, so it stays a bounded identity
/// string and no enum is invented for it.
///
/// <b>No index and no uniqueness constraint is declared.</b> <c>DATABASE.md</c>
/// §4 lists no <c>RelicDefinition</c> index — its index list is
/// <c>Pet(PlayerId)</c>, <c>Relic(PlayerId)</c>,
/// <c>PlayerUnlockedCard(PlayerId)</c>, and
/// <c>BattleResult(PlayerId, CompletedAt DESC)</c> — and §4 states further
/// indexes should be added only when a real query pattern requires them.
/// </summary>
public sealed class RelicDefinitionConfiguration : IEntityTypeConfiguration<RelicDefinition>
{
    public void Configure(EntityTypeBuilder<RelicDefinition> builder)
    {
        builder.ToTable("RelicDefinition");

        builder.HasKey(definition => definition.RelicDefinitionId);

        // DATABASE.md §1: RelicDefinitionId (PK). The identifier is the domain
        // string every owned Relic instance references as a FK (§2), stored as
        // a bounded string rather than a database-generated value.
        builder.Property(definition => definition.RelicDefinitionId)
            .HasMaxLength(64)
            .IsRequired();

        // DATABASE.md §1: Name — the Relic's display name ("Berserker Core",
        // "Mana Crystal", ...; RELIC_RULES.md §6). Bounded for a
        // human-readable name; no uniqueness constraint is documented. No rule
        // reads it: RELIC_RULES.md §8.2 item 1 forbids deriving an effect from
        // the Relic's Name.
        builder.Property(definition => definition.Name)
            .HasMaxLength(64)
            .IsRequired();

        // DATABASE.md §1 / RELIC_RULES.md §3: Trigger — the identity of the
        // Relic's one primary Trigger, drawn from §3's closed list. Stored as
        // an identity string, not as evaluated trigger state; the closed set
        // is content, and §3 states new trigger types are a rule change
        // requiring GAME_RULES.md §20 — so no enum is invented here.
        //
        // RELIC_RULES.md §8.5 item 3 (TASK-131 D8) leaves §3 unchanged: no
        // trigger value is added, removed, or reinterpreted, so this member is
        // deliberately the one member §8 does NOT restructure.
        builder.Property(definition => definition.Trigger)
            .HasMaxLength(64)
            .IsRequired();

        // DATABASE.md §1 / RELIC_RULES.md §8.1: Condition — the STRUCTURED form
        // plus its threshold. The column is `jsonb` and NULLABLE: an absent
        // condition is the documented "no extra condition" case (§8.1 item 4),
        // not a sentinel string that would read as a real condition.
        //
        // The conversion delegates to RelicCondition's own payload writer and
        // reader, so the storage contract lives in exactly one place and this
        // mapping defines no second encoding of it. The reader throws on a
        // malformed payload rather than substituting a default, which is what
        // makes a corrupt column value surface as a failure instead of a Relic
        // that silently does nothing (DATABASE.md §1's Relic note item 6).
        //
        // Nothing evaluates this value: condition evaluation is
        // GAME_RULES.md §17 step 11's stage, which RELIC_RULES.md §8.7 records
        // as NOT IMPLEMENTED.
        builder.Property(definition => definition.Condition)
            .HasConversion(new RelicConditionConverter())
            .HasColumnName("Condition")
            .HasColumnType("jsonb");

        // DATABASE.md §1 / RELIC_RULES.md §8.2: EffectDefinition — the Relic's
        // STRUCTURED effect rules: a `jsonb` ARRAY of effect objects, each
        // carrying its own `effectType`/`valueType`/`value` triple plus the
        // `target`/`lifetime` members §8.3 defines.
        //
        // This supersedes TASK-082 R2-7 for this member (TASK-131 D1); combined
        // with TASK-109's earlier Card-scoped supersession, R2-7 no longer
        // governs any member. The former `character varying(128)` was sized for
        // the verbatim prose R2-7 stored and is neither large enough nor the
        // right type for the structured payload (§8.6 / TASK-131 D9).
        //
        // Nothing executes this value (RELIC_RULES.md §8.7 — no trigger
        // evaluation, no condition evaluation, no effect application): it is
        // read and written as data only.
        builder.Property(definition => definition.EffectDefinition)
            .HasConversion(new RelicEffectDefinitionsConverter())
            .HasColumnName("EffectDefinition")
            .HasColumnType("jsonb")
            .IsRequired();
    }

    /// <summary>
    /// Writes and reads <c>DATABASE.md</c> §1's stored structured Relic condition
    /// — the optional <c>conditionType</c>/<c>threshold</c> object
    /// <c>RELIC_RULES.md</c> §8.1 defines.
    ///
    /// <code>
    /// { "conditionType": "MatchCountAtLeast", "threshold": 3 }
    /// </code>
    ///
    /// It delegates to <see cref="RelicCondition.ToPersistedPayload"/> and
    /// <see cref="RelicCondition.FromPersistedPayload"/>, so the storage contract
    /// lives in exactly one place and the mapping defines no second encoding of it
    /// (<c>GAME_STATE.md</c> §0 item 5). The reader throws on a malformed payload
    /// rather than substituting a default, which is what makes a corrupt column
    /// value surface as a failure rather than a silently ignored condition
    /// (<c>RELIC_RULES.md</c> §8.2 item 5's loud-rejection standard).
    /// </summary>
    private sealed class RelicConditionConverter
        : ValueConverter<RelicCondition, string>
    {
        public RelicConditionConverter()
            : base(
                condition => condition.ToPersistedPayload(),
                payload => RelicCondition.FromPersistedPayload(payload))
        {
        }
    }

    /// <summary>
    /// Writes and reads <c>DATABASE.md</c> §1's stored structured Relic effect
    /// document — the <b>array</b> of effect objects
    /// <c>RELIC_RULES.md</c> §8.2 defines.
    ///
    /// <code>
    /// [ { "effectType": "ATK", "valueType": "Percentage", "value": 5,
    ///     "target": "Pet", "lifetime": "Battle" } ]
    /// </code>
    ///
    /// It delegates to <see cref="RelicEffectDefinitions.ToPersistedPayload"/>
    /// and <see cref="RelicEffectDefinitions.FromPersistedPayload"/>, so the
    /// storage contract lives in exactly one place and the mapping defines no
    /// second encoding of it. The reader throws on a malformed payload — including
    /// the superseded prose form, for which no compatibility reader exists — rather
    /// than substituting a default, which is what makes a corrupt column value
    /// surface as a failure instead of a Relic that silently does nothing
    /// (<c>RELIC_RULES.md</c> §8.2 item 5).
    /// </summary>
    private sealed class RelicEffectDefinitionsConverter
        : ValueConverter<RelicEffectDefinitions, string>
    {
        public RelicEffectDefinitionsConverter()
            : base(
                effects => effects.ToPersistedPayload(),
                payload => RelicEffectDefinitions.FromPersistedPayload(payload))
        {
        }
    }
}
