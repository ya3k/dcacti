using GameServer.Domain.Players;
using Microsoft.EntityFrameworkCore;

namespace GameServer.Infrastructure.Postgres;

public class GameDbContext : DbContext
{
    public GameDbContext(DbContextOptions<GameDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// The persistent Player account table (<c>DATABASE.md</c> §1).
    /// </summary>
    public DbSet<Player> Players => Set<Player>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Entity configurations live beside this context
        // (Postgres/Configurations/), one per persisted entity. The Player
        // mapping is DATABASE.md §1's four fields plus §3's constraints;
        // Pet/Card/Relic mappings belong to their own tasks and are not
        // registered here.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GameDbContext).Assembly);
    }
}
