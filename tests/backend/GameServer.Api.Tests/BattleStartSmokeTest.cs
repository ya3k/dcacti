using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GameServer.Application.Battle;
using GameServer.Domain.Cards;
using GameServer.Domain.Elements;
using GameServer.Domain.Passives;
using GameServer.Domain.Pets;
using GameServer.Domain.Players;
using GameServer.Domain.Relics;
using GameServer.Infrastructure.Postgres;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
using Xunit.Abstractions;

namespace GameServer.Api.Tests;

/// <summary>
/// The documented <c>POST /api/battle/start</c> smoke test (TASK-030 §22).
///
/// <code>
/// Client request
///       ↓
/// POST /api/battle/start
///       ↓
/// server validation
///       ↓
/// CardLoadoutService
///       ↓
/// RelicLoadoutService
///       ↓
/// Pet/Boss resolution
///       ↓
/// BattleState composition
///       ↓
/// authoritative BattleState
/// </code>
///
/// It walks one real request through the unmodified production pipeline — the
/// same controller, services, and persistence wiring the application composes —
/// and then inspects the resulting authoritative state through the existing
/// retrieval path. It is a scenario test, not a unit test: its value is showing
/// the whole chain is connected and that the snapshots survive as state.
/// </summary>
public class BattleStartSmokeTest
{
    private const string PetInstanceId = "smoke_pet";
    private const string PetDefinitionId = "smoke_pet_def";
    private const string HealCardId = "heal";
    private const string ShieldCardId = "shield";
    private const string PowerChargeCardId = "power_charge";
    private const string SignatureSkillCardId = "thanh_xa_skill";

    private readonly ITestOutputHelper _output;

    public BattleStartSmokeTest(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task SmokeTest_PostBattleStart_ShouldComposeAnAuthoritativeBattleWithBothLoadoutSnapshots()
    {
        using var factory = new SmokeFactory();
        var client = factory.CreateClient();
        var playerId = await factory.SeedAsync();

        // ---- The documented API_CONTRACTS.md §3 request ----------------
        // Exactly the four documented members: petId, bossId, cardLoadout,
        // relicLoadout. No identity, no battleId, no combat value.
        var request = new
        {
            petId = PetInstanceId,
            bossId = "boss-hoa-long",
            cardLoadout = new[] { HealCardId, ShieldCardId, PowerChargeCardId },
            relicLoadout = new[] { "relic_a", "relic_b", "relic_c" },
        };

        _output.WriteLine("POST /api/battle/start");
        _output.WriteLine($"  petId        = {request.petId}");
        _output.WriteLine($"  bossId       = {request.bossId}");
        _output.WriteLine($"  cardLoadout  = [{string.Join(", ", request.cardLoadout)}]");
        _output.WriteLine($"  relicLoadout = [{string.Join(", ", request.relicLoadout)}]");

        var message = new HttpRequestMessage(HttpMethod.Post, "/api/battle/start")
        {
            Content = JsonContent.Create(request),
        };
        message.Headers.Add("X-Smoke-PlayerId", playerId);

        var response = await client.SendAsync(message);

        // ---- §3: the documented success response -----------------------
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var battleId = body.GetProperty("battleId").GetString()!;
        var host = body.GetProperty("signalrHub").GetString()!;
        var state = body.GetProperty("initialState");

        _output.WriteLine($"← 200 battleId={battleId} signalrHub={host}");

        Assert.False(string.IsNullOrWhiteSpace(battleId));
        Assert.Equal("/hubs/battle", host);

        // ---- The authoritative state the endpoint created --------------
        var battles = factory.Services.GetRequiredService<BattleStateService>();
        var authoritative = await battles.GetInitialStateForGroupAsync(battleId);

        Assert.NotNull(authoritative);

        // ---- Snapshot: EquippedCards = 3 Basic + 1 derived Signature Skill
        var equippedCards = authoritative!.PetState.EquippedCards;

        Assert.NotNull(equippedCards);
        Assert.Equal(4, equippedCards!.Length);

        Assert.Equal(
            [HealCardId, ShieldCardId, PowerChargeCardId, SignatureSkillCardId],
            equippedCards.Select(c => c.Value).ToArray());

        _output.WriteLine("PetState.EquippedCards[] (4):");
        foreach (var card in equippedCards)
        {
            _output.WriteLine($"  - {card.Value}");
        }

        // ---- Snapshot: EquippedRelics = the requested 3, in order -------
        var equippedRelics = authoritative.PetState.EquippedRelics;

        Assert.NotNull(equippedRelics);
        Assert.Equal(3, equippedRelics!.Length);

        Assert.Equal(
            request.relicLoadout,
            equippedRelics.Select(r => r.Value).ToArray());

        _output.WriteLine("PetState.EquippedRelics[] (3, equip-slot order):");
        for (var slot = 0; slot < equippedRelics.Length; slot++)
        {
            _output.WriteLine($"  slot {slot + 1}: {equippedRelics[slot].Value}");
        }

        // ---- Both snapshots are also on the response's initialState ----
        var responsePet = state.GetProperty("petState");

        Assert.Equal(
            equippedCards.Select(c => c.Value).ToArray(),
            responsePet.GetProperty("equippedCards").EnumerateArray()
                .Select(c => c.GetString()!).ToArray());

        Assert.Equal(
            equippedRelics.Select(r => r.Value).ToArray(),
            responsePet.GetProperty("equippedRelics").EnumerateArray()
                .Select(r => r.GetString()!).ToArray());

        // ---- Server authority: creation values are the server's --------
        Assert.Equal(0, authoritative.Turn);
        Assert.Equal(0, authoritative.Sequence);
        Assert.Equal(64, authoritative.BoardState.Cells.Count);
        Assert.Equal("boss-hoa-long", authoritative.BossState.BossId.Value);
        Assert.Equal(5000, authoritative.BossState.HP);
        Assert.Equal(Element.Moc, authoritative.PetState.Element);

        _output.WriteLine(
            $"authoritative: turn={authoritative.Turn} sequence={authoritative.Sequence} "
            + $"cells={authoritative.BoardState.Cells.Count} "
            + $"boss={authoritative.BossState.BossId.Value}/{authoritative.BossState.HP}hp "
            + $"petElement={authoritative.PetState.Element}");

        // ---- Snapshot independence: later inventory change is invisible --
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<GameDbContext>();

            context.PlayerUnlockedCards.RemoveRange(context.PlayerUnlockedCards);
            context.Relics.RemoveRange(context.Relics);
            await context.SaveChangesAsync();
        }

        var afterMutation = (await battles.GetInitialStateForGroupAsync(battleId))!;

        Assert.Equal(
            equippedCards.Select(c => c.Value).ToArray(),
            afterMutation.PetState.EquippedCards!.Select(c => c.Value).ToArray());

        Assert.Equal(
            equippedRelics.Select(r => r.Value).ToArray(),
            afterMutation.PetState.EquippedRelics!.Select(r => r.Value).ToArray());

        _output.WriteLine(
            "snapshot independence: after revoking every unlock and deleting every Relic, "
            + "the battle's loadouts are unchanged.");
    }

    /// <summary>
    /// A host composed exactly like the application's, with an isolated store.
    /// </summary>
    private sealed class SmokeFactory : WebApplicationFactory<Program>
    {
        private readonly string _storeName = $"smoke-{Guid.NewGuid():N}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("ConnectionStrings:DefaultConnection", "");
            builder.UseSetting("ConnectionStrings:Redis", "");

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<GameDbContext>>();
                services.RemoveAll<GameDbContext>();
                services.AddDbContext<GameDbContext>(options =>
                    options.UseInMemoryDatabase(_storeName));

                // The active-state store (REDIS_STATE.md §1–§4). Blanking
                // ConnectionStrings:Redis above means the production composition
                // registers no IBattleStateRepository at all, so this host supplies
                // the isolated in-memory substitute, exactly as it does for
                // GameDbContext.
                services.AddSingleton<IBattleStateRepository, ApiTestBattleStateRepository>();

                services.AddSingleton<IStartupFilter>(new SmokeIdentityStartupFilter());
            });
        }

        /// <summary>
        /// Supplies the authenticated Player identity the endpoint requires,
        /// standing in for the TASK-034 session mechanism so the documented flow
        /// can be walked end to end.
        /// </summary>
        private sealed class SmokeIdentityStartupFilter : IStartupFilter
        {
            public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
                app =>
                {
                    app.Use(async (context, inner) =>
                    {
                        if (context.Request.Headers.TryGetValue("X-Smoke-PlayerId", out var playerId))
                        {
                            context.Items[Controllers.BattleController.AuthenticatedPlayerItemKey] =
                                playerId.ToString();
                        }

                        await inner();
                    });

                    next(app);
                };
        }

        /// <summary>
        /// Seeds the documented battle-start inputs: one Player owning one Pet
        /// (with its definition), three unlocked Basic Cards plus the Pet's
        /// Signature Skill Card, and three owned Relic instances.
        /// </summary>
        public async Task<string> SeedAsync()
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameDbContext>();

            var playerId = $"player_smoke_{Guid.NewGuid():N}";

            context.Players.Add(new Player
            {
                PlayerId = playerId,
                DiscordUserId = $"8035{Random.Shared.NextInt64(1_000_000_000_000_000L):D16}",
                Level = Player.InitialLevel,
                CreatedAt = DateTimeOffset.UtcNow,
            });

            context.PetDefinitions.Add(new PetDefinition
            {
                PetDefinitionId = PetDefinitionId,
                Identity = "Thanh Xà",
                Element = Element.Moc,
                PetLevelMultiplier = 1.0m,
                PassiveId = new PassiveId("thanh-xa-regen"),
                PassiveThreshold = 5,
                SignatureSkillCardId = SignatureSkillCardId,
            });

            context.Pets.Add(new Pet
            {
                PetInstanceId = PetInstanceId,
                PlayerId = playerId,
                PetDefinitionId = PetDefinitionId,
                Tier = PetTier.Common,
                Star = 1,
                Level = 1,
                AcquiredAt = DateTimeOffset.UtcNow,
            });

            foreach (var (id, category) in new[]
            {
                (HealCardId, CardCategory.Basic),
                (ShieldCardId, CardCategory.Basic),
                (PowerChargeCardId, CardCategory.Basic),
                (SignatureSkillCardId, CardCategory.PetSkill),
            })
            {
                context.CardDefinitions.Add(new CardDefinition
                {
                    CardDefinitionId = id,
                    Name = id,
                    Category = category,
                    PowerCost = 0,
                    EffectDefinition = "effect",
                    LoadoutCopyLimit = id == HealCardId ? 1 : 3,
                });

                context.PlayerUnlockedCards.Add(new PlayerUnlockedCard
                {
                    PlayerId = playerId,
                    CardDefinitionId = id,
                });
            }

            foreach (var relicInstanceId in new[] { "relic_a", "relic_b", "relic_c" })
            {
                context.Relics.Add(new Relic
                {
                    RelicInstanceId = relicInstanceId,
                    PlayerId = playerId,
                    RelicDefinitionId = "relic_def_1",
                    AcquiredAt = DateTimeOffset.UtcNow,
                });
            }

            await context.SaveChangesAsync();

            return playerId;
        }
    }
}
