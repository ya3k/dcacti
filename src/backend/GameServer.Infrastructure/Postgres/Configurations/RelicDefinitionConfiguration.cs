using GameServer.Domain.Relics;
using Microsoft.EntityFrameworkCore;
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
/// ├── Trigger
/// ├── Condition          (optional)
/// └── EffectDefinition
/// </code>
///
/// <b>The field set is <c>DATABASE.md</c> §1's</b> — static content only. No
/// per-instance state (charges, stacks, cooldowns, duration) is mapped,
/// because no MVP Relic in <c>RELIC_RULES.md</c> §6 carries any and §1 places
/// <c>Reset/Cooldown</c> on the Relic's definition rather than on an owned
/// copy.
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
        // human-readable name; no uniqueness constraint is documented.
        builder.Property(definition => definition.Name)
            .HasMaxLength(64)
            .IsRequired();

        // DATABASE.md §1 / RELIC_RULES.md §3: Trigger — the identity of the
        // Relic's one primary Trigger, drawn from §3's closed list. Stored as
        // an identity string, not as evaluated trigger state; the closed set
        // is content, and §3 states new trigger types are a rule change
        // requiring GAME_RULES.md §20 — so no enum is invented here.
        builder.Property(definition => definition.Trigger)
            .HasMaxLength(64)
            .IsRequired();

        // DATABASE.md §1 / RELIC_RULES.md §1: Condition — the optional extra
        // condition ("Combo ≥ 3", "HP < 30%"). Optional per the rule, so the
        // column is nullable rather than defaulted to a sentinel string that
        // would read as a real condition.
        builder.Property(definition => definition.Condition)
            .HasMaxLength(128);

        // DATABASE.md §1 / RELIC_RULES.md §1: EffectDefinition — the passive
        // modification reference. Stored as a reference, matching how
        // PetDefinition carries PassiveId rather than inlining effect content;
        // resolution is the Relic system's concern and is not implemented.
        builder.Property(definition => definition.EffectDefinition)
            .HasMaxLength(128)
            .IsRequired();
    }
}
