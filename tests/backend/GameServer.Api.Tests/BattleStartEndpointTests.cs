using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GameServer.Api.Controllers;
using GameServer.Application.Battle;
using GameServer.Application.Pets;
using GameServer.Application.Players;
using GameServer.Domain.Cards;
using GameServer.Domain.Elements;
using GameServer.Domain.Passives;
using GameServer.Domain.Pets;
using GameServer.Domain.Players;
using GameServer.Domain.Relics;
using GameServer.Infrastructure.Postgres;
using GameServer.Domain.Bosses;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace GameServer.Api.Tests;

/// <summary>
/// The <c>POST /api/battle/start</c> endpoint — <c>API_CONTRACTS.md</c> §3, §6
/// (TASK-030).
///
/// <code>
/// Client request
///       ↓
/// POST /api/battle/start
///       ↓
/// BattleStartService
///       ↓
/// Pet / Boss resolution
/// CardLoadoutService + RelicLoadoutService
///       ↓
/// BattleStateService
///       ↓
/// { battleId, signalrHub, initialState }
/// </code>
///
/// These tests exercise the endpoint end to end against real persistence
/// (an isolated in-memory store) and the real loadout services, so the
/// orchestration is proven wired — not merely unit-tested in isolation.
/// </summary>
public class BattleStartEndpointTests
{
    private const string DiscordUserId = "80351110224678912";
    private const string PetInstanceId = "pet_instance_1";
    private const string PetDefinitionId = "pet_def_1";
    private const string SignatureSkillCardId = "card_skill_1";
    private const string BasicA = "card_basic_a";
    private const string BasicB = "card_basic_b";
    private const string BasicC = "card_basic_c";

    // -----------------------------------------------------------------------
    // Success path — API_CONTRACTS.md §3
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Start_WithAValidSelection_ShouldReturnTheDocumentedResponseShape()
    {
        using var factory = new BattleStartFactory();
        var client = factory.CreateClient();
        var playerId = await factory.SeedPlayerAsync(client);

        var response = await factory.PostStartAsync(client, new { petId = PetInstanceId, bossId = "boss-hoa-long", cardLoadout = new[] { BasicA, BasicB, BasicC }, relicLoadout = new[] { "relic_1", "relic_2", "relic_3" } }, playerId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        // §3 response: exactly battleId, signalrHub, initialState.
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("battleId").GetString()));
        Assert.Equal("/hubs/battle", body.GetProperty("signalrHub").GetString());
        Assert.True(body.TryGetProperty("initialState", out var initialState));
        Assert.Equal(JsonValueKind.Object, initialState.ValueKind);
    }

    [Fact]
    public async Task Start_ShouldSnapshotFourEquippedCards_InTheResponseState()
    {
        // API_CONTRACTS.md §3 / CARD_RULES.md §1: 4 entries — 3 submitted Basics
        // plus the derived Signature Skill.
        using var factory = new BattleStartFactory();
        var client = factory.CreateClient();
        var playerId = await factory.SeedPlayerAsync(client);

        var response = await factory.PostStartAsync(client, new { petId = PetInstanceId, bossId = "boss-hoa-long", cardLoadout = new[] { BasicA, BasicB, BasicC }, relicLoadout = new[] { "relic_1", "relic_2", "relic_3" } }, playerId);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var equippedCards = body
            .GetProperty("initialState")
            .GetProperty("petState")
            .GetProperty("equippedCards")
            .EnumerateArray()
            .Select(card => card.GetString())
            .ToArray();

        Assert.Equal(4, equippedCards.Length);
        Assert.Equal([BasicA, BasicB, BasicC, SignatureSkillCardId], equippedCards);
    }

    [Fact]
    public async Task Start_ShouldSnapshotTheRequestedRelics_InSubmittedOrder()
    {
        // RELIC_RULES.md §2.3: element i is equip slot i + 1 — never re-sorted.
        // The submitted order is deliberately not the sorted order, so a
        // re-sorting implementation cannot pass.
        string[] submitted = ["relic_z", "relic_a", "relic_m"];

        using var factory = new BattleStartFactory { OwnedRelicInstanceIds = submitted };
        var client = factory.CreateClient();
        var playerId = await factory.SeedPlayerAsync(client);

        var response = await factory.PostStartAsync(client, new { petId = PetInstanceId, bossId = "boss-hoa-long", cardLoadout = new[] { BasicA, BasicB, BasicC }, relicLoadout = submitted }, playerId);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var equippedRelics = body
            .GetProperty("initialState")
            .GetProperty("petState")
            .GetProperty("equippedRelics")
            .EnumerateArray()
            .Select(relic => relic.GetString())
            .ToArray();

        Assert.Equal(submitted, equippedRelics);
    }

    [Fact]
    public async Task Start_ShouldReturnAStateWithAuthoritativeCreationValues()
    {
        // GAME_STATE.md §2.0.2 / §2.0.5.2 item 1: starting a battle resolves no
        // action, so Turn and Sequence are 0; the board is the server's 64 cells.
        using var factory = new BattleStartFactory();
        var client = factory.CreateClient();
        var playerId = await factory.SeedPlayerAsync(client);

        var response = await factory.PostStartAsync(client, new { petId = PetInstanceId, bossId = "boss-hoa-long", cardLoadout = new[] { BasicA, BasicB, BasicC }, relicLoadout = new[] { "relic_1", "relic_2", "relic_3" } }, playerId);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var state = body.GetProperty("initialState");

        Assert.Equal(0, state.GetProperty("turn").GetInt32());
        Assert.Equal(0, state.GetProperty("sequence").GetInt32());
        Assert.Equal(0, state.GetProperty("combo").GetInt32());
        Assert.Equal(0, state.GetProperty("matchCount").GetInt32());
        Assert.Equal(64, state.GetProperty("board").GetProperty("cells").GetArrayLength());

        // The server authored the battle identity and the seed.
        Assert.Equal(body.GetProperty("battleId").GetString(), state.GetProperty("battleId").GetString());
        Assert.True(state.GetProperty("rngSeed").GetUInt64() > 0);
    }

    [Fact]
    public async Task Start_ShouldEstablishTheSelectedBoss_AtFullHealthInItsInitialState()
    {
        using var factory = new BattleStartFactory();
        var client = factory.CreateClient();
        var playerId = await factory.SeedPlayerAsync(client);

        var response = await factory.PostStartAsync(client, new { petId = PetInstanceId, bossId = "boss-moc-yeu", cardLoadout = new[] { BasicA, BasicB, BasicC }, relicLoadout = new[] { "relic_1", "relic_2", "relic_3" } }, playerId);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var boss = body.GetProperty("initialState").GetProperty("bossState");

        // BOSS_RULES.md §6.1 / GAME_STATE.md §2.4.
        Assert.Equal("boss-moc-yeu", boss.GetProperty("bossId").GetString());
        Assert.Equal(5000, boss.GetProperty("hp").GetInt32());
        Assert.Equal(5000, boss.GetProperty("maxHP").GetInt32());
        Assert.Equal("Idle", boss.GetProperty("state").GetString());
    }

    [Fact]
    public async Task Start_ShouldProduceABattleRetrievableThroughTheExistingStatePath()
    {
        // SIGNALR_PROTOCOL.md §1 items 1–2, §4.1: the returned battleId feeds the
        // existing state retrieval path — the same Application boundary the hub
        // pushes from on group join. No new transport is introduced.
        using var factory = new BattleStartFactory();
        var client = factory.CreateClient();
        var playerId = await factory.SeedPlayerAsync(client);

        var response = await factory.PostStartAsync(client, new { petId = PetInstanceId, bossId = "boss-hoa-long", cardLoadout = new[] { BasicA, BasicB, BasicC }, relicLoadout = new[] { "relic_1", "relic_2", "relic_3" } }, playerId);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var battleId = body.GetProperty("battleId").GetString()!;

        var battles = factory.Services.GetRequiredService<BattleStateService>();
        var state = await battles.GetInitialStateForGroupAsync(battleId);

        Assert.NotNull(state);
        Assert.Equal(battleId, state!.BattleId);
        Assert.Equal(4, state.PetState.EquippedCards!.Length);
        Assert.Equal(3, state.PetState.EquippedRelics!.Length);
    }

    // -----------------------------------------------------------------------
    // Card validation — API_CONTRACTS.md §3, §6 (INVALID_LOADOUT)
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    public async Task Start_WithAnInvalidCardCount_ShouldReturnInvalidLoadout(int count)
    {
        using var factory = new BattleStartFactory();
        var client = factory.CreateClient();
        var playerId = await factory.SeedPlayerAsync(client);

        var cardLoadout = Enumerable.Range(0, count).Select(i => $"card_basic_{i}").ToArray();

        var response = await factory.PostStartAsync(client, new { petId = PetInstanceId, bossId = "boss-hoa-long", cardLoadout, relicLoadout = new[] { "relic_1", "relic_2", "relic_3" } }, playerId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("INVALID_LOADOUT", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Start_WhenACardIsNotUnlocked_ShouldReturnInvalidLoadout()
    {
        // API_CONTRACTS.md §3 step 2 (ownership). The Player owns A and B only.
        using var factory = new BattleStartFactory();
        var client = factory.CreateClient();
        var playerId = await factory.SeedPlayerAsync(client);

        var response = await factory.PostStartAsync(client, new { petId = PetInstanceId, bossId = "boss-hoa-long", cardLoadout = new[] { BasicA, BasicB, "card_not_unlocked" }, relicLoadout = new[] { "relic_1", "relic_2", "relic_3" } }, playerId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("INVALID_LOADOUT", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Start_WhenACardIsNotBasic_ShouldReturnInvalidLoadout()
    {
        // API_CONTRACTS.md §3 step 3 (category): a PetSkill Card cannot fill a
        // submitted Basic slot (CARD_RULES.md §1 item 4).
        using var factory = new BattleStartFactory();
        var client = factory.CreateClient();
        var playerId = await factory.SeedPlayerAsync(client);

        // The Signature Skill Card is unlocked but is a PetSkill Card.
        var response = await factory.PostStartAsync(client, new { petId = PetInstanceId, bossId = "boss-hoa-long", cardLoadout = new[] { BasicA, BasicB, SignatureSkillCardId }, relicLoadout = new[] { "relic_1", "relic_2", "relic_3" } }, playerId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("INVALID_LOADOUT", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Start_WhenTheCopyLimitIsExceeded_ShouldReturnInvalidLoadout()
    {
        // API_CONTRACTS.md §3 step 4 (copy limit). BasicA's limit is 1 here.
        using var factory = new BattleStartFactory { CopyLimitOfBasicA = 1 };
        var client = factory.CreateClient();
        var playerId = await factory.SeedPlayerAsync(client);

        var response = await factory.PostStartAsync(client, new { petId = PetInstanceId, bossId = "boss-hoa-long", cardLoadout = new[] { BasicA, BasicA, BasicB }, relicLoadout = new[] { "relic_1", "relic_2", "relic_3" } }, playerId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("INVALID_LOADOUT", body.GetProperty("error").GetString());
    }

    // -----------------------------------------------------------------------
    // Relic validation — API_CONTRACTS.md §3, §6 (INVALID_LOADOUT)
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(2)]
    [InlineData(6)]
    public async Task Start_WithAnOutOfRangeRelicCount_ShouldReturnInvalidLoadout(int count)
    {
        using var factory = new BattleStartFactory();
        var client = factory.CreateClient();
        var playerId = await factory.SeedPlayerAsync(client);

        var relicLoadout = Enumerable.Range(1, count).Select(i => $"relic_{i}").ToArray();

        var response = await factory.PostStartAsync(client, new { petId = PetInstanceId, bossId = "boss-hoa-long", cardLoadout = new[] { BasicA, BasicB, BasicC }, relicLoadout }, playerId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("INVALID_LOADOUT", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Start_WithADuplicateRelicInstance_ShouldReturnInvalidLoadout()
    {
        // RELIC_RULES.md §2.4 items 1–2.
        using var factory = new BattleStartFactory();
        var client = factory.CreateClient();
        var playerId = await factory.SeedPlayerAsync(client);

        var response = await factory.PostStartAsync(client, new { petId = PetInstanceId, bossId = "boss-hoa-long", cardLoadout = new[] { BasicA, BasicB, BasicC }, relicLoadout = new[] { "relic_1", "relic_1", "relic_2" } }, playerId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("INVALID_LOADOUT", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Start_WithAnUnownedRelicInstance_ShouldReturnInvalidLoadout()
    {
        // RELIC_RULES.md §2.1 item 2.
        using var factory = new BattleStartFactory();
        var client = factory.CreateClient();
        var playerId = await factory.SeedPlayerAsync(client);

        var response = await factory.PostStartAsync(client, new { petId = PetInstanceId, bossId = "boss-hoa-long", cardLoadout = new[] { BasicA, BasicB, BasicC }, relicLoadout = new[] { "relic_1", "relic_2", "relic_99" } }, playerId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("INVALID_LOADOUT", body.GetProperty("error").GetString());
    }

    // -----------------------------------------------------------------------
    // Pet / Boss validation — API_CONTRACTS.md §3, §6
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Start_WhenThePetBelongsToAnotherPlayer_ShouldReturnPetNotOwned()
    {
        using var factory = new BattleStartFactory { PetOwnerIsAnotherPlayer = true };
        var client = factory.CreateClient();
        var playerId = await factory.SeedPlayerAsync(client);

        var response = await factory.PostStartAsync(client, new { petId = PetInstanceId, bossId = "boss-hoa-long", cardLoadout = new[] { BasicA, BasicB, BasicC }, relicLoadout = new[] { "relic_1", "relic_2", "relic_3" } }, playerId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("PET_NOT_OWNED", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Start_WhenThePetDoesNotExist_ShouldReturnPetNotOwned()
    {
        using var factory = new BattleStartFactory();
        var client = factory.CreateClient();
        var playerId = await factory.SeedPlayerAsync(client);

        var response = await factory.PostStartAsync(client, new { petId = "pet_does_not_exist", bossId = "boss-hoa-long", cardLoadout = new[] { BasicA, BasicB, BasicC }, relicLoadout = new[] { "relic_1", "relic_2", "relic_3" } }, playerId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("PET_NOT_OWNED", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Start_WithAnUnknownBoss_ShouldReturnBossNotFound()
    {
        using var factory = new BattleStartFactory();
        var client = factory.CreateClient();
        var playerId = await factory.SeedPlayerAsync(client);

        var response = await factory.PostStartAsync(client, new { petId = PetInstanceId, bossId = "Not A Real Boss", cardLoadout = new[] { BasicA, BasicB, BasicC }, relicLoadout = new[] { "relic_1", "relic_2", "relic_3" } }, playerId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("BOSS_NOT_FOUND", body.GetProperty("error").GetString());
    }

    // -----------------------------------------------------------------------
    // Failure atomicity — API_CONTRACTS.md §3
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Start_OnEveryRejection_ShouldCreateNoBattle()
    {
        // §3: "A rejected request equips nothing and writes no battle state."
        using var factory = new BattleStartFactory();
        var client = factory.CreateClient();
        var playerId = await factory.SeedPlayerAsync(client);

        var battles = factory.Services.GetRequiredService<BattleStateService>();

        // The active-state store's records are what "a battle exists" means
        // (REDIS_STATE.md §3: the record is written as part of creation), so the
        // count is taken from the store itself rather than from the service.
        var store = (ApiTestBattleStateRepository)
            factory.Services.GetRequiredService<IBattleStateRepository>();
        var before = store.RecordCount;

        var rejections = new[]
        {
            new { petId = "pet_does_not_exist", bossId = "boss-hoa-long", cardLoadout = new[] { BasicA, BasicB, BasicC }, relicLoadout = new[] { "relic_1", "relic_2", "relic_3" } },
            new { petId = PetInstanceId, bossId = "nope", cardLoadout = new[] { BasicA, BasicB, BasicC }, relicLoadout = new[] { "relic_1", "relic_2", "relic_3" } },
            new { petId = PetInstanceId, bossId = "boss-hoa-long", cardLoadout = new[] { BasicA }, relicLoadout = new[] { "relic_1", "relic_2", "relic_3" } },
            new { petId = PetInstanceId, bossId = "boss-hoa-long", cardLoadout = new[] { BasicA, BasicB, BasicC }, relicLoadout = new[] { "relic_1" } },
        };

        foreach (var rejection in rejections)
        {
            var response = await factory.PostStartAsync(client, rejection, playerId);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

            // The response carries no battleId, so no partial battle is reported…
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.False(body.TryGetProperty("battleId", out _));
        }

        // …and no record was written to the active-state store.
        Assert.Equal(before, store.RecordCount);
    }

    // -----------------------------------------------------------------------
    // Server authority — GAME_RULES.md §18, ADR-001
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Start_ShouldNotLetTheClientSupplyAuthoritativeValues()
    {
        // API_CONTRACTS.md §1: no endpoint accepts Damage, HP, Power, Match, or
        // Combo values, and the battleId/Turn/Sequence/RngState/BoardState are
        // the server's. Extra members in the request body are therefore ignored,
        // and the created state carries the server's own values regardless.
        using var factory = new BattleStartFactory();
        var client = factory.CreateClient();
        var playerId = await factory.SeedPlayerAsync(client);

        var response = await factory.PostStartAsync(client, new
            {
                petId = PetInstanceId,
                bossId = "boss-hoa-long",
                cardLoadout = new[] { BasicA, BasicB, BasicC },
                relicLoadout = new[] { "relic_1", "relic_2", "relic_3" },

                // Every one of these is an authoritative value the client may not
                // author. A client that submits them must change nothing.
                battleId = "client_chosen_battle",
                turn = 99,
                sequence = 99,
                rngSeed = 1UL,
                rngState = new { state = 1UL, increment = 1UL },
                hp = 1,
                atk = 9999,
                def = 9999,
                crit = 100,
                power = 100,
                combo = 99,
                matchCount = 99,
                board = new { cells = Array.Empty<object>() },
            }, playerId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var state = body.GetProperty("initialState");

        // The server's identity and values, not the submitted ones.
        Assert.NotEqual("client_chosen_battle", body.GetProperty("battleId").GetString());
        Assert.Equal(0, state.GetProperty("turn").GetInt32());
        Assert.Equal(0, state.GetProperty("sequence").GetInt32());
        Assert.Equal(0, state.GetProperty("combo").GetInt32());
        Assert.Equal(0, state.GetProperty("matchCount").GetInt32());
        Assert.Equal(64, state.GetProperty("board").GetProperty("cells").GetArrayLength());

        var pet = state.GetProperty("petState");
        Assert.Equal(1000, pet.GetProperty("hp").GetInt32());
        Assert.Equal(50, pet.GetProperty("atk").GetInt32());
        Assert.Equal(25, pet.GetProperty("def").GetInt32());
        Assert.Equal(5, pet.GetProperty("crit").GetInt32());
        Assert.Equal(0, pet.GetProperty("power").GetInt32());
    }

    [Fact]
    public async Task Start_WithoutAnAuthenticatedIdentity_ShouldNotCreateABattle()
    {
        // API_CONTRACTS.md §1: all endpoints require an authenticated session.
        // Until the session mechanism (ADR-007 item 4, TASK-034) exists and
        // populates the request's Player identity, no caller can be identified —
        // so the request is rejected rather than attributed to a default Player.
        using var factory = new BattleStartFactory();
        var client = factory.CreateClient();

        var battleCountBefore = ((ApiTestBattleStateRepository)factory.Services
            .GetRequiredService<IBattleStateRepository>())
            .RecordCount;

        var response = await factory.PostStartAsync(client, new { petId = PetInstanceId, bossId = "boss-hoa-long", cardLoadout = new[] { BasicA, BasicB, BasicC }, relicLoadout = new[] { "relic_1", "relic_2", "relic_3" } }, playerId: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("UNAUTHENTICATED", body.GetProperty("error").GetString());

        Assert.Equal(
            battleCountBefore,
            ((ApiTestBattleStateRepository)factory.Services
                .GetRequiredService<IBattleStateRepository>())
            .RecordCount);
    }

    // -----------------------------------------------------------------------
    // Snapshot isolation — ADR-012 items 8 and 10
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Start_ShouldProduceSnapshotsThatSurviveLaterInventoryChanges()
    {
        // The created battle's loadouts are battle snapshots, not live inventory
        // views: changing the Player's unlocks and relic ownership afterwards
        // must not alter the created state, and no live re-read may occur.
        using var factory = new BattleStartFactory();
        var client = factory.CreateClient();
        var playerId = await factory.SeedPlayerAsync(client);

        var response = await factory.PostStartAsync(client, new { petId = PetInstanceId, bossId = "boss-hoa-long", cardLoadout = new[] { BasicA, BasicB, BasicC }, relicLoadout = new[] { "relic_1", "relic_2", "relic_3" } }, playerId);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var battleId = body.GetProperty("battleId").GetString()!;

        var battles = factory.Services.GetRequiredService<BattleStateService>();
        var before = (await battles.GetBattleAsync(battleId))!.PetState;

        // Revoke every unlock and delete every owned relic.
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<GameDbContext>();

            context.PlayerUnlockedCards.RemoveRange(context.PlayerUnlockedCards);
            context.Relics.RemoveRange(context.Relics);
            await context.SaveChangesAsync();
        }

        var after = (await battles.GetBattleAsync(battleId))!.PetState;

        Assert.Equal(
            before.EquippedCards!.Select(c => c.Value).ToArray(),
            after.EquippedCards!.Select(c => c.Value).ToArray());

        Assert.Equal(
            before.EquippedRelics!.Select(r => r.Value).ToArray(),
            after.EquippedRelics!.Select(r => r.Value).ToArray());
    }

    [Fact]
    public async Task Start_ShouldNotWriteToTheCardOrRelicOwnershipTables()
    {
        // Both loadout services are read-only passes: creation writes no unlock,
        // no equip row, and no relic ownership row (ADR-012 items 8 and 10).
        using var factory = new BattleStartFactory();
        var client = factory.CreateClient();
        var playerId = await factory.SeedPlayerAsync(client);

        int unlocksBefore, relicsBefore;

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<GameDbContext>();
            unlocksBefore = await context.PlayerUnlockedCards.CountAsync();
            relicsBefore = await context.Relics.CountAsync();
        }

        await factory.PostStartAsync(client, new { petId = PetInstanceId, bossId = "boss-hoa-long", cardLoadout = new[] { BasicA, BasicB, BasicC }, relicLoadout = new[] { "relic_1", "relic_2", "relic_3" } }, playerId);

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<GameDbContext>();
            Assert.Equal(unlocksBefore, await context.PlayerUnlockedCards.CountAsync());
            Assert.Equal(relicsBefore, await context.Relics.CountAsync());
        }
    }

    // -----------------------------------------------------------------------
    // Test host
    // -----------------------------------------------------------------------

    /// <summary>
    /// A host with the real endpoint, real loadout services, and real
    /// persistence wired to an isolated in-memory store, so the whole
    /// battle-start flow is exercised without a live PostgreSQL instance.
    /// </summary>
    private sealed class BattleStartFactory : WebApplicationFactory<Program>
    {
        private readonly string _storeName = $"battle-start-{Guid.NewGuid():N}";

        /// <summary>The <c>LoadoutCopyLimit</c> of the <see cref="BasicA"/> definition.</summary>
        public int CopyLimitOfBasicA { get; init; } = 3;

        /// <summary>When set, the seeded Pet belongs to a different Player.</summary>
        public bool PetOwnerIsAnotherPlayer { get; init; }

        /// <summary>
        /// The Relic instance identities the seeded Player owns. Defaults to the
        /// three the success-path tests submit.
        /// </summary>
        public string[] OwnedRelicInstanceIds { get; init; } = ["relic_1", "relic_2", "relic_3"];

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // Blank the connection strings so AddInfrastructureServices does not
            // register the Npgsql provider; this host supplies the isolated store
            // below instead.
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
                // registers no IBattleStateRepository at all, so the same isolated
                // in-memory substitution the host makes for GameDbContext is made
                // for it here — the real BattleStateService pipeline runs over it.
                services.AddSingleton<IBattleStateRepository, ApiTestBattleStateRepository>();

                // Stand in for the session mechanism ADR-007 item 4 leaves to
                // TASK-034: translate the test's identity header into the same
                // request-context item BattleController reads. It is registered
                // as a startup filter so the host's own pipeline (routing, MVC,
                // the hub) is left intact — the identity is the only thing this
                // host adds. It defines no session format and validates nothing.
                services.AddSingleton<IStartupFilter>(new TestIdentityStartupFilter());
            });
        }

        /// <summary>
        /// Inserts the test identity middleware ahead of the host's pipeline.
        /// </summary>
        private sealed class TestIdentityStartupFilter : IStartupFilter
        {
            public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
                app =>
                {
                    app.UseMiddleware<TestIdentityMiddleware>();
                    next(app);
                };
        }

        /// <summary>
        /// Copies the test identity header into the request context. It defines
        /// no session format and validates nothing — it stands in for the
        /// TASK-034 mechanism so the endpoint's own behaviour is testable.
        /// </summary>
        private sealed class TestIdentityMiddleware
        {
            private readonly RequestDelegate _next;

            public TestIdentityMiddleware(RequestDelegate next)
            {
                _next = next;
            }

            public Task InvokeAsync(HttpContext context)
            {
                if (context.Request.Headers.TryGetValue("X-Test-PlayerId", out var playerId))
                {
                    context.Items[BattleController.AuthenticatedPlayerItemKey] = playerId.ToString();
                }

                return _next(context);
            }
        }

        /// <summary>
        /// Posts a battle-start request with the authenticated Player's identity
        /// attached, standing in for the session mechanism ADR-007 item 4 leaves
        /// to TASK-034. A <c>null</c> identity posts the request with no
        /// authenticated caller at all.
        /// </summary>
        public async Task<HttpResponseMessage> PostStartAsync<T>(
            HttpClient client,
            T request,
            string? playerId)
        {
            var message = new HttpRequestMessage(HttpMethod.Post, "/api/battle/start")
            {
                Content = JsonContent.Create(request),
            };

            if (playerId is not null)
            {
                message.Headers.Add("X-Test-PlayerId", playerId);
            }

            return await client.SendAsync(message);
        }

        /// <summary>
        /// Seeds one Player with a Pet, its definition, three unlocked Basic
        /// Cards, the Pet's Signature Skill Card, and three owned Relics — the
        /// documented inputs <c>POST /api/battle/start</c> validates against.
        /// </summary>
        public async Task<string> SeedPlayerAsync(HttpClient client)
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameDbContext>();

            var playerId = $"player_{Guid.NewGuid():N}";

            context.Players.Add(new Player
            {
                PlayerId = playerId,
                DiscordUserId = $"{DiscordUserId}{Random.Shared.Next(1000, 9999)}",
                Level = Player.InitialLevel,
                CreatedAt = DateTimeOffset.UtcNow,
            });

            context.PetDefinitions.Add(new PetDefinition
            {
                PetDefinitionId = PetDefinitionId,
                Identity = "Thanh Xà",
                Element = Element.Moc,
                PetLevelMultiplier = 1.0m,
                PassiveId = new PassiveId("pet-passive-1"),
                PassiveThreshold = 5,
                SignatureSkillCardId = SignatureSkillCardId,
            });

            context.Pets.Add(new Pet
            {
                PetInstanceId = PetInstanceId,
                PlayerId = PetOwnerIsAnotherPlayer ? "player_someone_else" : playerId,
                PetDefinitionId = PetDefinitionId,
                Tier = PetTier.Common,
                Star = 1,
                Level = 1,
                AcquiredAt = DateTimeOffset.UtcNow,
            });

            // The three Basic Cards, plus the Pet's Signature Skill Card.
            foreach (var (id, category) in new[]
            {
                (BasicA, CardCategory.Basic),
                (BasicB, CardCategory.Basic),
                (BasicC, CardCategory.Basic),
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
                    LoadoutCopyLimit = id == BasicA ? CopyLimitOfBasicA : 3,
                });

                context.PlayerUnlockedCards.Add(new PlayerUnlockedCard
                {
                    PlayerId = playerId,
                    CardDefinitionId = id,
                });
            }

            foreach (var relicInstanceId in OwnedRelicInstanceIds)
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
