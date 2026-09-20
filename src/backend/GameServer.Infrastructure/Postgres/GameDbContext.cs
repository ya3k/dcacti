using Microsoft.EntityFrameworkCore;

namespace GameServer.Infrastructure.Postgres;

public class GameDbContext : DbContext
{
    public GameDbContext(DbContextOptions<GameDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // Entity configurations are added during domain/database implementation tasks
    }
}
