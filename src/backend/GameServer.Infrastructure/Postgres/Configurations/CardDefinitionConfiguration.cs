using GameServer.Domain.Cards;
using Microsoft.EntityFrameworkCore;
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
/// └── EffectDefinition
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

        // DATABASE.md §1: EffectDefinition — the effect reference, stored as a
        // reference matching how RelicDefinition.EffectDefinition and
        // PetDefinition.PassiveId carry theirs rather than inlining effect
        // content. Nothing executes it (TASK-028 Scope: no Card gameplay).
        builder.Property(definition => definition.EffectDefinition)
            .HasMaxLength(128)
            .IsRequired();

        // DATABASE.md §1 / CARD_RULES.md §1 item 2: LoadoutCopyLimit —
        // required, explicit, NO default. Non-nullable with no HasDefaultValue
        // so a definition row must state its own limit; a missing value is
        // invalid definition data rather than a value silently supplied by the
        // database. No CHECK bound is added: §1 defines no range for it.
        builder.Property(definition => definition.LoadoutCopyLimit)
            .IsRequired();
    }
}
