using GameServer.Domain.Accounts;
using GameServer.Domain.Players;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameServer.Infrastructure.Postgres.Configurations;

/// <summary>
/// The persistence mapping of <see cref="Player"/> (<c>DATABASE.md</c> §1, §3, <c>ADR-020</c>).
///
/// <code>
/// Player
/// ├── PlayerId        (PK)
/// ├── AccountId       (unique, FK → Account)
/// ├── XP              (int, NOT NULL, default 0 — uncapped)
/// ├── Level           (1–50; = 1 for a newly created Player)
/// └── CreatedAt
/// </code>
/// </summary>
public sealed class PlayerConfiguration : IEntityTypeConfiguration<Player>
{
    public void Configure(EntityTypeBuilder<Player> builder)
    {
        builder.ToTable("Player");

        builder.HasKey(player => player.PlayerId);

        // DATABASE.md §1: PlayerId (PK).
        builder.Property(player => player.PlayerId)
            .HasMaxLength(64)
            .IsRequired();

        // DATABASE.md §1/§3: AccountId (unique, FK → Account; ADR-020).
        builder.Property(player => player.AccountId)
            .IsRequired();

        builder.HasIndex(player => player.AccountId)
            .IsUnique();

        builder.HasOne<Account>()
            .WithOne()
            .HasForeignKey<Player>(player => player.AccountId)
            .OnDelete(DeleteBehavior.Cascade);

        // DATABASE.md §1/§3: Player.XP — int, NOT NULL, default 0, and >= 0
        // with NO upper bound (COMBAT_RULES.md §7.5 item 1: XP is uncapped and
        // keeps accumulating after Level 50).
        builder.Property(player => player.XP)
            .IsRequired()
            .HasDefaultValue(Player.InitialXp);

        builder.ToTable(table => table.HasCheckConstraint(
            "CK_Player_XP_NonNegative",
            $"\"XP\" >= {Player.InitialXp}"));

        // DATABASE.md §3: Player.Level ∈ [1, 50] and = 1 for a newly created
        // Player (COMBAT_RULES.md §7.5 item 3).
        builder.Property(player => player.Level)
            .IsRequired()
            .HasDefaultValue(Player.InitialLevel);

        builder.ToTable(table => table.HasCheckConstraint(
            "CK_Player_Level_Range",
            $"\"Level\" >= {Player.MinLevel} AND \"Level\" <= {Player.MaxLevel}"));

        // DATABASE.md §1: CreatedAt — a creation timestamp, set once.
        builder.Property(player => player.CreatedAt)
            .IsRequired();
    }
}
