using GameServer.Domain.Players;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameServer.Infrastructure.Postgres.Configurations;

/// <summary>
/// The persistence mapping of <see cref="Player"/> (<c>DATABASE.md</c> §1, §3).
///
/// <code>
/// Player
/// ├── PlayerId        (PK)
/// ├── DiscordUserId   (unique)
/// ├── Level           (1–50; = 1 for a newly created Player)
/// └── CreatedAt
/// </code>
///
/// <b>The constraints are the documented ones, not chosen here.</b>
/// <c>DATABASE.md</c> §3 states <c>Player.Level ∈ [1, 50]</c>,
/// <c>Player.Level = 1 for a newly created Player</c> (<c>PET_RULES.md</c> §5
/// item 8), and <c>DiscordUserId (unique)</c> (<c>DATABASE.md</c> §1). Each
/// value is read from the domain constants those sections define, so the
/// documented range and initial value have exactly one spelling.
///
/// <b>No further column is mapped.</b> <c>DATABASE.md</c> §1 defines four
/// fields; §3 records that there are no combat-stat columns on Player
/// (HP/ATK/DEF/Crit/Power are battle-time <c>PetState</c>,
/// <c>GAME_STATE.md</c> §2.3, <c>ADR-011</c> item 5). No XP column, XP amount,
/// or reward field exists (<c>DATABASE.md</c> §1 defines none; TASK-033 owns
/// progression).
///
/// <b>No index beyond the unique constraint is declared.</b> <c>DATABASE.md</c>
/// §4 lists no Player index and states further indexes should be added only
/// when a real query pattern requires them. Matching a Player is by the
/// unique <c>DiscordUserId</c>, whose unique index already serves that lookup.
/// </summary>
public sealed class PlayerConfiguration : IEntityTypeConfiguration<Player>
{
    public void Configure(EntityTypeBuilder<Player> builder)
    {
        builder.ToTable("Player");

        builder.HasKey(player => player.PlayerId);

        // DATABASE.md §1: PlayerId (PK). The identifier is the domain
        // string the auth boundary returns (API_CONTRACTS.md §2.5), so it is
        // stored as a bounded string rather than a database-generated value.
        builder.Property(player => player.PlayerId)
            .HasMaxLength(64)
            .IsRequired();

        // DATABASE.md §1/§3: DiscordUserId (unique). The uniqueness constraint
        // is the authoritative protection against duplicate ownership records
        // for one Discord account, and it is what a concurrent first-login
        // race resolves against — a second insert of the same identity fails
        // rather than creating a second Player.
        //
        // The value is a Discord snowflake returned as a string
        // (API_CONTRACTS.md §2.3 item 3), so it is stored as a string and is
        // never parsed into a numeric type.
        builder.Property(player => player.DiscordUserId)
            .HasMaxLength(32)
            .IsRequired();

        builder.HasIndex(player => player.DiscordUserId)
            .IsUnique();

        // DATABASE.md §3: Player.Level ∈ [1, 50] and = 1 for a newly created
        // Player (PET_RULES.md §5 item 8). The range is enforced by a check
        // constraint and the initial value by the column default, both read
        // from the domain constants that own them.
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
