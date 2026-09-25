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
/// ├── Level             (1–50 — denormalized snapshot)
/// └── AcquiredAt
/// </code>
///
/// <b>The constraints are the documented ones, not chosen here.</b>
/// <c>DATABASE.md</c> §3 states <c>Pet.Tier ∈ the five documented members</c>,
/// <c>Pet.Star ∈ [1, 5]</c>, and <c>Pet.Level ∈ [1, 50]</c>; §4 lists the
/// single <c>Pet(PlayerId)</c> index. Ranges are read from the Domain
/// constants that own them so each bound has exactly one spelling.
///
/// <b>Level is a stored snapshot, not an independent store.</b>
/// <c>DATABASE.md</c> §1 documents it as the denormalized result of
/// <c>PET_RULES.md</c> §5's formula; the check constraint enforces the
/// range, while derivation itself lives in Domain
/// (<see cref="PetLevelDerivation"/>). There is no XP column
/// (ADR-012 item 6).
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

        // DATABASE.md §3: Pet.Level ∈ [1, 50] (PET_RULES.md §5). Check
        // constraint reads Player.MinLevel/MaxLevel — the same documented
        // 1–50 range both Player Level and Pet Level share (ADR-012 item 4).
        builder.Property(pet => pet.Level)
            .IsRequired();

        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "CK_Pet_Star_Range",
                $"\"Star\" >= {Pet.MinStar} AND \"Star\" <= {Pet.MaxStar}");

            table.HasCheckConstraint(
                "CK_Pet_Level_Range",
                $"\"Level\" >= {Player.MinLevel} AND \"Level\" <= {Player.MaxLevel}");
        });

        // DATABASE.md §1: AcquiredAt — a creation timestamp, set once.
        builder.Property(pet => pet.AcquiredAt)
            .IsRequired();

        // DATABASE.md §4: Pet(PlayerId) — list a player's Pets. The only
        // documented Pet index; no speculative extras are added.
        builder.HasIndex(pet => pet.PlayerId);
    }
}
