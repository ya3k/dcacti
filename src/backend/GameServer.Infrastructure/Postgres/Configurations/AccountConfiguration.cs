using GameServer.Domain.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameServer.Infrastructure.Postgres.Configurations;

/// <summary>
/// The persistence mapping of <see cref="Account"/> (ADR-020, DATABASE.md §1).
/// </summary>
public sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("Account");

        builder.HasKey(account => account.AccountId);

        builder.Property(account => account.Username)
            .HasMaxLength(Account.MaxUsernameLength)
            .IsRequired();

        builder.HasIndex(account => account.Username)
            .IsUnique();

        builder.Property(account => account.PasswordHash)
            .IsRequired();

        builder.Property(account => account.CreatedAt)
            .IsRequired();
    }
}
