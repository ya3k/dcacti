using GameServer.Domain.Cards;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameServer.Infrastructure.Postgres.Configurations;

/// <summary>
/// The persistence mapping of <see cref="CardDefinition"/>
/// (<c>DATABASE.md</c> §1, §3).
///
/// <code>
/// CardDefinition
/// ├── CardDefinitionId  (PK)
/// ├── Name
/// ├── Category
/// ├── PowerCost
/// ├── LoadoutCopyLimit  (required — no default)
/// └── EffectDefinition  (jsonb, NOT NULL — structured effect rule)
/// </code>
///
/// <b>The field set is <c>DATABASE.md</c> §1's</b> — static content only. No
/// Tier/Star/Level, quantity, or per-instance column is mapped, because MVP
/// Cards have none (ADR-012 item 9: "MVP Cards have no Tier/Star/Level, so an
/// instance table is unnecessary").
///
/// <b><c>LoadoutCopyLimit</c> is required and has no default.</b>
/// <c>DATABASE.md</c> §1 marks it "required … explicit value required, no
/// default" and <c>CARD_RULES.md</c> §1 item 2 makes a missing value invalid
/// definition data. The column is therefore non-nullable with no
/// <c>HasDefaultValue</c> — substituting 1 or 3 here would be exactly the
/// invented default the contract forbids. No CHECK bounds it either: §1 defines
/// no range for the limit, and inventing one would add an undocumented
/// constraint.
///
/// <b>No index and no uniqueness constraint is declared.</b> <c>DATABASE.md</c>
/// §4 lists no <c>CardDefinition</c> index — its list is <c>Pet(PlayerId)</c>,
/// <c>Relic(PlayerId)</c>, <c>PlayerUnlockedCard(PlayerId)</c>, and
/// <c>BattleResult(PlayerId, CompletedAt DESC)</c> — and §4 states further
/// indexes should be added only when a real query pattern requires them.
/// </summary>
public sealed class CardDefinitionConfiguration : IEntityTypeConfiguration<CardDefinition>
{
    public void Configure(EntityTypeBuilder<CardDefinition> builder)
    {
        builder.ToTable("CardDefinition");

        builder.HasKey(definition => definition.CardDefinitionId);

        // DATABASE.md §1: CardDefinitionId (PK). The identifier is the domain
        // string an unlock row references as a FK (§2) and the identity the
        // battle snapshot carries (GAME_STATE.md §2.3), stored as a bounded
        // string rather than a database-generated value.
        builder.Property(definition => definition.CardDefinitionId)
            .HasMaxLength(64)
            .IsRequired();

        // DATABASE.md §1: Name — the Card's display name ("Heal", "Shield",
        // "Power Charge"; CARD_RULES.md §2). Bounded for a human-readable
        // name; no uniqueness constraint is documented.
        builder.Property(definition => definition.Name)
            .HasMaxLength(64)
            .IsRequired();

        // DATABASE.md §1/§3: Category ∈ {Basic, PetSkill} (CARD_RULES.md §1).
        // Stored as the enum's numeric value via the same conversion pattern as
        // PetDefinition.Element; the closed category set lives in the Domain
        // enum, not in a database CHECK. No default is declared: an unset value
        // must not silently become a valid category (DATABASE.md §1 lists
        // Category as required content).
        builder.Property(definition => definition.Category)
            .HasConversion<int>()
            .IsRequired();

        // DATABASE.md §1: PowerCost — the Card's cast cost (CARD_RULES.md
        // §2/§4.1 own the concrete values). Required content; no default and no
        // range is invented, because §1 declares none and the cast validation
        // that would consume it is a separate, unimplemented concern
        // (CARD_RULES.md §3).
        builder.Property(definition => definition.PowerCost)
            .IsRequired();

        // DATABASE.md §1: EffectDefinition — the Card's structured effect rules.
        //
        // DATABASE.md §1 stores a JSON ARRAY of effect objects, each carrying its
        // own `effectType`, `valueType`, and `value` (TASK-108 D-1/D-2, extended by
        // TASK-111 D-1/D-3 and superseding TASK-082 R2-7 for Cards ONLY). The column
        // is `jsonb`, NOT NULL, mapped with a value converter that writes exactly
        // §1's document — the same scalar-conversion shape BossDefinitionConfiguration
        // uses for its two JSON objects, which is what EF Core supports for an
        // immutable value type carried on a constructor-independent property.
        //
        // The former `character varying(128)` was sized for the verbatim prose
        // R2-7 stored and is neither large enough nor the right type for the
        // structured payload; TASK-109's migration
        // 20261001112446_StructureCardDefinitionEffectDefinition replaced it with
        // `jsonb`, and TASK-112's migration re-encoded the six content rows from
        // the single-object shape to this array shape.
        //
        // Nothing executes this value (TASK-112 Scope: no CardCast, no
        // PetSkillCast, no Crit roll, no Burn tick): it is read and written as data
        // only.
        builder.Property(definition => definition.EffectDefinition)
            .HasConversion(new CardEffectDefinitionsConverter())
            .HasColumnName("EffectDefinition")
            .HasColumnType("jsonb")
            .IsRequired();

        // DATABASE.md §1 / CARD_RULES.md §1 item 2: LoadoutCopyLimit —
        // required, explicit, NO default. Non-nullable with no HasDefaultValue
        // so a definition row must state its own limit; a missing value is
        // invalid definition data rather than a value silently supplied by the
        // database. No CHECK bound is added: §1 defines no range for it.
        builder.Property(definition => definition.LoadoutCopyLimit)
            .IsRequired();
    }

    /// <summary>
    /// Writes and reads <c>DATABASE.md</c> §1's persisted structured Card effect
    /// document — the <b>array</b> of effect objects.
    ///
    /// <code>
    /// [ { "effectType": "Shield", "valueType": "PercentMaxHp", "value": 20 } ]
    /// [ { "effectType": "Damage", "valueType": "Flat", "value": 100 },
    ///   { "effectType": "Burn", "valueType": "Flat", "value": 50, "duration": 2 } ]
    /// </code>
    ///
    /// It delegates to <see cref="CardEffectDefinitions.ToPersistedPayload"/> and
    /// <see cref="CardEffectDefinitions.FromPersistedPayload"/>, so the storage
    /// contract lives in exactly one place and the mapping defines no second
    /// encoding of it (<c>GAME_STATE.md</c> §0 item 5). The reader throws on a
    /// malformed payload — including the superseded single-object shape — rather
    /// than substituting a default, which is what makes a corrupt column value
    /// surface as a failure instead of a Card that silently does nothing
    /// (<c>DATABASE.md</c> §1 item 6). <c>RelicDefinition.EffectDefinition</c> is
    /// deliberately <b>not</b> mapped this way: TASK-082 R2-7 remains in force
    /// for Relics (<c>DATABASE.md</c> §1).
    /// </summary>
    private sealed class CardEffectDefinitionsConverter
        : ValueConverter<CardEffectDefinitions, string>
    {
        public CardEffectDefinitionsConverter()
            : base(
                effects => effects.ToPersistedPayload(),
                payload => CardEffectDefinitions.FromPersistedPayload(payload))
        {
        }
    }
}
