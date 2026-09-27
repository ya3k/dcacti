using GameServer.Domain.Pets;
using GameServer.Domain.Players;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameServer.Infrastructure.Postgres.Configurations;

/// <summary>
/// The persistence mapping of <see cref="Pet"/> (<c>DATABASE.md</c> §1, §3, §4).
///
/// <code>
/// Pet
/// ├── PetInstanceId     (PK)
/// ├── PlayerId          (FK → Player)
/// ├── PetDefinitionId   (FK → PetDefinition)
/// ├── Tier              (Common / Rare / Epic / Legendary / Mythic)
/// ├── Star              (1–5)
/// ├── XP                (0–4900 — per-instance Pet XP; hard-capped)
/// ├── Level             (1–50 — derived from this instance's own XP)
/// └── AcquiredAt
/// </code>
///
/// <b>The constraints are the documented ones, not chosen here.</b>
/// <c>DATABASE.md</c> §3 states <c>Pet.Tier ∈ the five documented members</c>,
/// <c>Pet.Star ∈ [1, 5]</c>, <c>Pet.XP ∈ [0, 4900]</c>, and
/// <c>Pet.Level ∈ [1, 50]</c>; §4 lists the single <c>Pet(PlayerId)</c> index.
/// Ranges are read from the Domain constants that own them so each bound has
/// exactly one spelling.
///
/// <b>XP and Level are stored per-instance progression values.</b>
/// <c>DATABASE.md</c> §1 documents <c>XP</c> as this Pet instance's own Pet XP
/// and <c>Level</c> as its documented function (<c>PET_RULES.md</c> §5.4); the
/// check constraints enforce the documented <c>[0, 4900]</c> and <c>[1, 50]</c>
/// ranges. Level is no longer derived from Player Level — the retired
/// <c>Player.Level × PetLevelMultiplier</c> derivation was removed
/// (<c>PET_RULES.md</c> §5.6 item 1, ADR-016 item 13) — and is re-derived from
/// this Pet's own XP by <see cref="Pet.GrantBattleXp"/>.
///
/// <b>The XP cap is the Pet track's, never the Player's.</b>
/// <c>PET_RULES.md</c> §5.5 makes <c>Pet.XP</c> hard-capped at <c>4900</c> with
/// no overflow, while <c>COMBAT_RULES.md</c> §7.5 item 1 leaves
/// <c>Player.XP</c> uncapped; neither cap rule is applied to the other track.
/// Both bounds are declared here because §5.5 item 4 keeps the XP cap and the
/// Level cap as separate, equally documented facts.
///
/// <b>No index beyond Pet(PlayerId) is declared.</b> <c>DATABASE.md</c> §4
/// lists exactly that one Pet index and states further indexes should be
/// added only when a real query pattern requires them.
/// </summary>
public sealed class PetConfiguration : IEntityTypeConfiguration<Pet>
{
    public void Configure(EntityTypeBuilder<Pet> builder)
    {
        builder.ToTable("Pet");

        builder.HasKey(pet => pet.PetInstanceId);

        // DATABASE.md §1: PetInstanceId (PK). Opaque domain string, stored
        // as a bounded string rather than a database-generated value.
        builder.Property(pet => pet.PetInstanceId)
            .HasMaxLength(64)
            .IsRequired();

        // DATABASE.md §1/§2: PlayerId FK → Player (collection ownership —
        // ADR-011 item 5, ADR-012). Restrict keeps an owned Pet from being
        // deleted as a side effect of a Player delete without an explicit
        // cascade decision (DATABASE.md §2 documents the relationship shape,
        // not a cascade rule).
        builder.Property(pet => pet.PlayerId)
            .HasMaxLength(64)
            .IsRequired();

        builder.HasOne<Player>()
            .WithMany()
            .HasForeignKey(pet => pet.PlayerId)
            .OnDelete(DeleteBehavior.Restrict);

        // DATABASE.md §1/§2: PetDefinitionId FK → PetDefinition
        // (Pet N ── 1 PetDefinition). Restrict: an instance cannot outlive
        // the static content it references without an explicit cascade
        // decision.
        builder.Property(pet => pet.PetDefinitionId)
            .HasMaxLength(64)
            .IsRequired();

        builder.HasOne<PetDefinition>()
            .WithMany()
            .HasForeignKey(pet => pet.PetDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);

        // No index is declared on this FK: DATABASE.md §4 lists exactly one
        // Pet index — Pet(PlayerId). EF Core's conventional FK index on
        // PetDefinitionId is disabled by GameDbContext.ConfigureConventions
        // (ForeignKeyIndexConvention removed), so no implicit
        // IX_Pet_PetDefinitionId is scaffolded.

        // DATABASE.md §3: Pet.Tier ∈ {Common, Rare, Epic, Legendary,
        // Mythic} (PET_RULES.md §3). Stored as the enum's numeric value;
        // the closed set lives in the Domain enum, not in a database CHECK
        // (mirroring Element's storage on PetDefinition).
        builder.Property(pet => pet.Tier)
            .IsRequired();

        // DATABASE.md §3: Pet.Star ∈ [1, 5] (PET_RULES.md §4). Check
        // constraint reads the Domain constants so the bounds cannot drift.
        builder.Property(pet => pet.Star)
            .IsRequired();

        // DATABASE.md §1/§3: Pet.XP — int, NOT NULL, default 0, in [0, 4900]
        // (PET_RULES.md §5.2 initial value, §5.5 hard cap). The initial value is
        // the column default, read from the domain constant that owns it, so an
        // existing row receives the documented 0 rather than a value derived
        // from its Level — no Level → XP conversion exists in any document.
        builder.Property(pet => pet.XP)
            .IsRequired()
            .HasDefaultValue(Pet.InitialXp);

        // §5.5 item 1: the hard cap. Both bounds are declared, because §5.5
        // item 4 keeps the XP cap (4900) and the Level cap (50) as separate
        // documented facts, and this track's cap is deliberately NOT the
        // Player track's uncapped policy (COMBAT_RULES.md §7.5 item 1).
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_Pet_XP_Range",
            $"\"XP\" >= {Pet.InitialXp} AND \"XP\" <= {Pet.MaxXp}"));

        // DATABASE.md §3: Pet.Level ∈ [1, 50] (PET_RULES.md §5.5 item 4).
        // Check constraint reads this track's own Domain constants so the
        // bounds cannot drift — and so the Pet range is not silently sourced
        // from Player's constants (ADR-016 item 12: independent tracks).
        builder.Property(pet => pet.Level)
            .IsRequired();

        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "CK_Pet_Star_Range",
                $"\"Star\" >= {Pet.MinStar} AND \"Star\" <= {Pet.MaxStar}");

            table.HasCheckConstraint(
                "CK_Pet_Level_Range",
                $"\"Level\" >= {Pet.MinLevel} AND \"Level\" <= {Pet.MaxLevel}");
        });

        // DATABASE.md §1: AcquiredAt — a creation timestamp, set once.
        builder.Property(pet => pet.AcquiredAt)
            .IsRequired();

        // DATABASE.md §4: Pet(PlayerId) — list a player's Pets. The only
        // documented Pet index; no speculative extras are added.
        builder.HasIndex(pet => pet.PlayerId);
    }
}
