using GameServer.Domain.Cards;
using GameServer.Domain.Pets;
using GameServer.Domain.Players;
using GameServer.Domain.Relics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

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

    /// <summary>
    /// The persistent owned Pet instance table (<c>DATABASE.md</c> §1).
    /// </summary>
    public DbSet<Pet> Pets => Set<Pet>();

    /// <summary>
    /// The persistent Pet definition table (<c>DATABASE.md</c> §1).
    /// </summary>
    public DbSet<PetDefinition> PetDefinitions => Set<PetDefinition>();

    /// <summary>
    /// The persistent owned Relic instance table (<c>DATABASE.md</c> §1) —
    /// collection ownership only. There is no persistent equip table
    /// (<c>DATABASE.md</c> §2, ADR-012 item 7).
    /// </summary>
    public DbSet<Relic> Relics => Set<Relic>();

    /// <summary>
    /// The persistent Relic definition table (<c>DATABASE.md</c> §1).
    /// </summary>
    public DbSet<RelicDefinition> RelicDefinitions => Set<RelicDefinition>();

    /// <summary>
    /// The persistent Card definition table (<c>DATABASE.md</c> §1) — static
    /// content only. MVP Cards have no Tier/Star/Level, so there is no instance
    /// table (ADR-012 item 9).
    /// </summary>
    public DbSet<CardDefinition> CardDefinitions => Set<CardDefinition>();

    /// <summary>
    /// The persistent Player Card unlock table (<c>DATABASE.md</c> §1–§2) —
    /// unlock flags only. There is no persistent Card-equip table and no
    /// quantity column (<c>DATABASE.md</c> §2, ADR-012 items 9–10).
    /// </summary>
    public DbSet<PlayerUnlockedCard> PlayerUnlockedCards => Set<PlayerUnlockedCard>();

    /// <summary>
    /// EF Core 7+ documented API for removing conventions.
    ///
    /// <c>ForeignKeyIndexConvention</c> is removed so the model matches
    /// <c>DATABASE.md</c> §4, which lists exactly one Pet index —
    /// <c>Pet(PlayerId)</c> — and states that further indexes should be
    /// added only when a real query pattern requires them
    /// (anti-overengineering, <c>AGENTS.md</c> §9).
    ///
    /// Explicit indexes declared in entity configurations
    /// (<c>HasIndex</c>) are unaffected: only the automatic FK-index
    /// creation is disabled. This runs as part of model building on the
    /// <see cref="DbContext"/> itself, so it applies at design time
    /// (<c>dotnet ef migrations</c>), at runtime, and in tests alike —
    /// without depending on where <c>IConventionSetPlugin</c> is
    /// registered in DI.
    /// </summary>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Conventions.Remove(typeof(ForeignKeyIndexConvention));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Entity configurations live beside this context
        // (Postgres/Configurations/), one per persisted entity. The Player
        // mapping is DATABASE.md §1's four fields plus §3's constraints;
        // Pet maps its instance fields and PetDefinition its configuration;
        // Relic maps collection ownership and RelicDefinition its static
        // content; CardDefinition maps static Card content and
        // PlayerUnlockedCard the Player's unlock flags (no equip table —
        // DATABASE.md §2).
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GameDbContext).Assembly);
    }
}
