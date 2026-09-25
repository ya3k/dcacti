using GameServer.Domain.Players;
using GameServer.Domain.Relics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameServer.Infrastructure.Postgres.Configurations;

/// <summary>
/// The persistence mapping of <see cref="Relic"/> (<c>DATABASE.md</c> §1, §2,
/// §4).
///
/// <code>
/// Relic
/// ├── RelicInstanceId    (PK)
/// ├── PlayerId           (FK → Player)
/// ├── RelicDefinitionId  (FK → RelicDefinition)
/// └── AcquiredAt
/// </code>
///
/// <b>Ownership only — no equip column and no equip table.</b>
/// <c>DATABASE.md</c> §2 states "There is likewise no persistent Relic-equip
/// table: Player owns Relic instances; which 3–5 are equipped for a given
/// battle is request-time loadout only" (<c>RELIC_RULES.md</c> §2 item 1,
/// ADR-012 item 7). This mapping therefore adds no loadout, slot, or
/// "equipped" column, and no <c>PetRelicLoadout</c>/<c>EquippedRelic</c>
/// table is introduced.
///
/// <b>No uniqueness constraint on <c>PlayerId + RelicDefinitionId</c>.</b>
/// <c>RELIC_RULES.md</c> §2.4 item 3 permits two distinct owned instances that
/// reference the same definition to be equipped simultaneously, so several
/// rows may legitimately share a definition. The rule constrains instance
/// identity (item 1), never definition identity — a definition-level
/// uniqueness rule would contradict it.
///
/// <b>The declared index is the documented one.</b> <c>DATABASE.md</c> §4
/// lists exactly one Relic index — <c>Relic(PlayerId)</c>, "list a player's
/// Relics" — and states further indexes should be added only when a real query
/// pattern requires them. No index is declared on the
/// <c>RelicDefinitionId</c> FK: <c>GameDbContext.ConfigureConventions</c>
/// removes <c>ForeignKeyIndexConvention</c> (the TASK-024 behavior), so no
/// implicit <c>IX_Relic_RelicDefinitionId</c> is scaffolded.
/// </summary>
public sealed class RelicConfiguration : IEntityTypeConfiguration<Relic>
{
    public void Configure(EntityTypeBuilder<Relic> builder)
    {
        builder.ToTable("Relic");

        builder.HasKey(relic => relic.RelicInstanceId);

        // DATABASE.md §1: RelicInstanceId (PK). Opaque domain string, stored
        // as a bounded string rather than a database-generated value. This is
        // the identity the battle snapshot carries (RELIC_RULES.md §2.2).
        builder.Property(relic => relic.RelicInstanceId)
            .HasMaxLength(64)
            .IsRequired();

        // DATABASE.md §1/§2: PlayerId FK → Player (collection ownership —
        // ADR-011 item 4, ADR-012 item 7). Restrict keeps an owned Relic from
        // being deleted as a side effect of a Player delete without an
        // explicit cascade decision, matching the Pet mapping.
        builder.Property(relic => relic.PlayerId)
            .HasMaxLength(64)
            .IsRequired();

        builder.HasOne<Player>()
            .WithMany()
            .HasForeignKey(relic => relic.PlayerId)
            .OnDelete(DeleteBehavior.Restrict);

        // DATABASE.md §1/§2: RelicDefinitionId FK → RelicDefinition
        // (Relic N ── 1 RelicDefinition). Trigger, Condition, and Effect are
        // read through this reference, not duplicated on the instance
        // (GAME_STATE.md §0 item 5).
        builder.Property(relic => relic.RelicDefinitionId)
            .HasMaxLength(64)
            .IsRequired();

        builder.HasOne<RelicDefinition>()
            .WithMany()
            .HasForeignKey(relic => relic.RelicDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);

        // No index is declared on the RelicDefinitionId FK: DATABASE.md §4
        // lists exactly one Relic index — Relic(PlayerId) — and
        // ForeignKeyIndexConvention is removed by GameDbContext, so no
        // implicit FK index is scaffolded.

        // DATABASE.md §1: AcquiredAt — a creation timestamp, set once. It is
        // not an ordering key: equip slot order is the submitted loadout
        // array position (RELIC_RULES.md §2.3 item 2).
        builder.Property(relic => relic.AcquiredAt)
            .IsRequired();

        // DATABASE.md §4: Relic(PlayerId) — list a player's Relics and serve
        // the loadout ownership check. The only documented Relic index; no
        // speculative extras are added.
        builder.HasIndex(relic => relic.PlayerId);
    }
}
