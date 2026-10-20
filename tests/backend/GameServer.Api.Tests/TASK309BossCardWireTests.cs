using System.Text.Json;
using GameServer.Api.Hubs;
using GameServer.Application.Battle;
using GameServer.Domain.Battle;
using GameServer.Domain.Bosses;
using GameServer.Domain.Cards;
using GameServer.Domain.Elements;
using GameServer.Domain.Match3;
using GameServer.Domain.Passives;
using GameServer.Domain.Pets;
using GameServer.Domain.Players;
using GameServer.Infrastructure.Postgres;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace GameServer.Api.Tests;

/// <summary>
/// TASK-309 (ADR-029 Slice 4) wire coverage for the provisioned Boss Cards — the
/// four abilities this task migrated (<c>flame-burst</c>, <c>drain-power</c>,
/// <c>root</c>, <c>thunder-strike</c>) plus the TASK-308 <c>earthquake</c> case,
/// driven through the real <c>Swap</c> → <c>ReceiveEvents</c> hub path.
///
/// <code>
/// Rule (SIGNALR_PROTOCOL.md §3.2.24, §3.2.30; GAME_EVENTS.md §1, §2;
///       BOSS_RULES.md §4.2–§4.4)
///  ↓
/// Scenario (Given a provisioned boss card, enough Boss Power and a committed
///           Swap, When the Boss Card wins the one-action selection, Then the
///           batch carries the documented items in the documented order)
///  ↓
/// Test
/// </code>
///
/// <b>The provisioned content is the real fixture.</b> The host's isolated store
/// receives the same 13 <c>CardDefinition</c> rows the production migrations
/// create (<see cref="TestProvisionedContent"/>), so the Boss's active form is
/// resolved by the production content read rather than by a test-only branch
/// (<c>BOSS_RULES.md</c> §4.4 item 6). Only the balance values that make one
/// committed round deterministic are overridden on the <see cref="BossDefinition"/>
/// fixture — the cast-priority score that forces the cast to win selection and
/// the HP/ATK values that keep the round non-terminal — exactly as the TASK-308
/// Api case does; the authored 85/board-score boundary is Domain- and
/// Application-tested, not re-tested here.
///
/// <b>Exact JSON is asserted, not just member presence.</b> Each Boss
/// <c>BossCardCast</c> and the drain's <c>PowerChanged</c> are compared as raw
/// JSON strings, which proves the item carries exactly the documented members and
/// no cost/Power/cooldown/effect member (<c>§3.2.30</c> items 4–5,
/// <c>§3.2.24</c>) — the member order in the expected string is the transport
/// projection's declared serialization order, not a second contract claim.
/// </summary>
public class TASK309BossCardWireTests
    : IClassFixture<TASK309BossCardWireTests.BossCardWireFactory>
{
    private readonly BossCardWireFactory _factory;

    public TASK309BossCardWireTests(BossCardWireFactory factory)
    {
        _factory = factory;
        EnsureProvisionedContent();
    }

    /// <summary>
    /// The suite's host: the real application pipeline with both connection strings
    /// blanked (no live PostgreSQL, no live Redis) and the isolated in-memory pair
    /// substituted, and the application session's signing key supplied through
    /// configuration (<c>ADR-015</c> D10) so the hub connection authenticates with a
    /// real session token exactly as <c>SIGNALR_PROTOCOL.md</c> §1 item 3 documents.
    /// </summary>
    public sealed class BossCardWireFactory : WebApplicationFactory<Program>
    {
        private readonly string _storeName = $"task309-boss-card-wire-{Guid.NewGuid():N}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("ConnectionStrings:DefaultConnection", "");
            builder.UseSetting("ConnectionStrings:Redis", "");

            foreach (var (key, value) in TestApplicationSession.CurrentKeyConfiguration)
            {
                builder.UseSetting(key, value);
            }

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<GameDbContext>>();
                services.RemoveAll<GameDbContext>();
                services.AddDbContext<GameDbContext>(options =>
                    options.UseInMemoryDatabase(_storeName));

                services.AddSingleton<IBattleStateRepository, ApiTestBattleStateRepository>();
            });
        }
    }

    private static readonly object SeedGate = new();
    private static bool _seeded;

    /// <summary>
    /// Seeds the 13 canonical provisioned definitions once. The rows are the
    /// production content — the Boss Card rows are what make each Boss resolve the
    /// migrated form at battle creation.
    /// </summary>
    private void EnsureProvisionedContent()
    {
        lock (SeedGate)
        {
            if (_seeded)
            {
                return;
            }

            TestProvisionedContent.Seed(_factory.Services);
            _seeded = true;
        }
    }

    private static readonly BattleStateService.PetConfiguration Pet =
        new(
            new PetId("pet_instance_task309_wire_1"),
            Element.Hoa,
            new PassiveId("xich-lang"),
            PassiveThreshold: 5);

    /// <summary>
    /// A Pet configuration whose committed loadout makes a Player Card cast and a
    /// Pet Skill cast deliverable through the hub — the two Player events whose
    /// payloads this task must leave byte-identical (§3.2.20, §3.2.21).
    /// </summary>
    private static BattleStateService.PetConfiguration LoadoutPet(params string[] equippedCards) =>
        new(
            new PetId("pet_instance_task309_wire_loadout"),
            Element.Hoa,
            new PassiveId("xich-lang"),
            PassiveThreshold: 5,
            EquippedCards: equippedCards
                .Select(cardId => new EquippedCardIdentity(cardId))
                .ToArray());

    private static readonly PlayerId Owner = new("player_task309_boss_card_wire_owner");

    private static readonly string OwnerSessionToken =
        TestApplicationSession.Mint(Owner.Value);

    private HubConnection BuildHubConnection() =>
        new HubConnectionBuilder()
            .WithUrl("http://localhost/hubs/battle", options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.AccessTokenProvider = () => Task.FromResult<string?>(OwnerSessionToken);
            })
            .Build();

    /// <summary>
    /// Creates a battle against <paramref name="bossDefinition"/> through the real
    /// Application creation path, using the provisioned content (Boss Card rows).
    /// </summary>
    private async Task<BattleState> CreateBattleAsync(
        string battleId,
        BossDefinition bossDefinition,
        BattleStateService.PetConfiguration? petConfiguration = null)
    {
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<BattleStateService>();

        return await service.CreateBattleAsync(
            battleId,
            Owner,
            petConfiguration ?? Pet,
            bossDefinition);
    }

    /// <summary>
    /// The battle's current authoritative state, as the host's store holds it.
    /// </summary>
    private async Task<BattleState> ReadStateAsync(string battleId)
    {
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<BattleStateService>();

        return await service.GetBattleAsync(battleId)
            ?? throw new InvalidOperationException($"battle '{battleId}' has no active-state record.");
    }

    /// <summary>
    /// Pins the Boss content the documented cast gates read — its Power and its
    /// post-decrement cooldown (<c>BOSS_RULES.md</c> §4.3 step 1) — through the
    /// store's own Sequence compare-and-set, so the scenario starts from a state
    /// the game genuinely produces rather than from an invented one
    /// (<c>REDIS_STATE.md</c> §4 items 1–3).
    /// </summary>
    private async Task PinBossPowerAndCooldownAsync(string battleId, int power)
    {
        var state = await ReadStateAsync(battleId);

        var repository = _factory.Services.GetRequiredService<IBattleStateRepository>();
        var written = await repository.TryUpdateAsync(
            state with
            {
                BossState = state.BossState with { Power = power, SkillCooldown = 0 },
            },
            state.Sequence);

        Assert.True(written, "the scenario's Boss state could not be pinned: the stored Sequence moved on.");
    }

    /// <summary>
    /// Pins the active Pet's Power, so the drain's signed delta is deterministic
    /// against the documented floor-at-0 rule (<c>BOSS_RULES.md</c> §6.3.1 item 2;
    /// <c>GAME_RULES.md</c> §12).
    /// </summary>
    private async Task PinPetPowerAsync(string battleId, int power)
    {
        var state = await ReadStateAsync(battleId);

        var repository = _factory.Services.GetRequiredService<IBattleStateRepository>();
        var written = await repository.TryUpdateAsync(
            state with { PetState = state.PetState with { Power = power } },
            state.Sequence);

        Assert.True(written, "the scenario's Pet state could not be pinned: the stored Sequence moved on.");
    }

    /// <summary>
    /// The provisioned <c>CardDefinition</c> row the production migrations create,
    /// read from the host's store — the real content the Boss's active form and its
    /// authored cost/effect are resolved from.
    /// </summary>
    private CardDefinition ReadProvisionedBossCard(string cardId)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GameDbContext>();

        return context.CardDefinitions.Single(definition => definition.CardDefinitionId == cardId);
    }

    /// <summary>
    /// Commits one Swap through the real hub and returns the received
    /// <c>ReceiveEvents</c> batch.
    /// </summary>
    private async Task<JsonElement> SwapOnceAndReadTheBatchAsync(
        string battleId,
        SwapRequest pair,
        string actionId = "client-seq-task309")
    {
        var hubConnection = BuildHubConnection();
        var batch = new TaskCompletionSource<JsonElement>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        hubConnection.On<JsonElement>("ReceiveEvents", payload => batch.TrySetResult(payload));

        await hubConnection.StartAsync();
        await hubConnection.InvokeAsync("JoinBattle", battleId);

        var result = await hubConnection.InvokeAsync<SwapResponse>(
            "Swap", battleId, pair.From, pair.To, actionId);

        Assert.True(result.Accepted);

        var payload = await batch.Task.WaitAsync(TimeSpan.FromSeconds(10));

        await hubConnection.StopAsync();

        return payload;
    }

    /// <summary>
    /// Invokes one Player cast through the real hub and returns the received
    /// <c>ReceiveEvents</c> batch.
    ///
    /// <b>Arguments are passed individually, not as one array.</b> The SignalR client
    /// has both a fixed-arity <c>InvokeAsync(args...)</c> form and a
    /// <c>params object?[]</c> form; handing it a single array value binds the array
    /// as one argument, which the hub cannot bind to its parameter list — so each
    /// documented argument is named here.
    /// </summary>
    private async Task<JsonElement> InvokeCastAndReadTheBatchAsync(
        string battleId,
        bool isPetSkillCast,
        string? cardId,
        string clientSequence)
    {
        var hubConnection = BuildHubConnection();
        var batch = new TaskCompletionSource<JsonElement>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        hubConnection.On<JsonElement>("ReceiveEvents", payload => batch.TrySetResult(payload));

        await hubConnection.StartAsync();
        await hubConnection.InvokeAsync("JoinBattle", battleId);

        if (isPetSkillCast)
        {
            // PetSkillCast(battleId, clientSequence) — SIGNALR_PROTOCOL.md §2.
            var result = await hubConnection.InvokeAsync<PetSkillCastResponse>(
                "PetSkillCast", battleId, clientSequence);
            Assert.True(result.Accepted);
        }
        else
        {
            // CardCast(battleId, cardId, clientSequence) — SIGNALR_PROTOCOL.md §2.
            var result = await hubConnection.InvokeAsync<CardCastResponse>(
                "CardCast", battleId, cardId, clientSequence);
            Assert.True(result.Accepted);
        }

        var payload = await batch.Task.WaitAsync(TimeSpan.FromSeconds(10));

        await hubConnection.StopAsync();

        return payload;
    }

    private static JsonElement[] Events(JsonElement payload) =>
        payload.GetProperty("events").EnumerateArray().ToArray();

    private static string Type(JsonElement item) => item.GetProperty("type").GetString()!;

    private static int IndexOfType(JsonElement[] events, string type) =>
        Array.FindIndex(events, item => Type(item) == type);

    private static SwapRequest FindMatchProducingPair(BoardState board)
    {
        for (var index = 0; index < BoardState.CellCount; index++)
        {
            var right = BoardState.ToColumn(index) + 1 < BoardState.Columns ? index + 1 : -1;
            var down = index + BoardState.Width < BoardState.CellCount ? index + BoardState.Width : -1;

            if (right >= 0 && MatchDetector.Detect(board.WithSwapped(index, right)).Count > 0)
            {
                return new SwapRequest(index, right);
            }

            if (down >= 0 && MatchDetector.Detect(board.WithSwapped(index, down)).Count > 0)
            {
                return new SwapRequest(index, down);
            }
        }

        throw new InvalidOperationException(
            "A generated board has at least one valid Swap (MATCH3_RULES.md §1.4).");
    }

    // =======================================================================
    // BossCardCast — SIGNALR_PROTOCOL.md §3.2.30, GAME_EVENTS.md §1/§2
    // =======================================================================

    [Theory]
    // The four abilities TASK-309 migrated (BOSS_RULES.md §4.2, §4.4 item 5;
    // TASK-309 §1's Power Cost table), plus the TASK-308 `earthquake` case that
    // proved the migrated form end-to-end before this slice. All five MVP Bosses
    // have a provisioned BossCard row after the TASK-308/TASK-309 migrations.
    [InlineData("boss-hoa-long", "flame-burst", 40, 2)]
    [InlineData("boss-thuy-ma", "drain-power", 25, 3)]
    [InlineData("boss-moc-yeu", "root", 35, 2)]
    [InlineData("boss-kim-loi-vuong", "thunder-strike", 40, 0)]
    [InlineData("boss-son-thach-ve", "earthquake", 30, 0)]
    public async Task BossCardCast_ShouldBeTheOnlyBossCastEvent_WithExactlyItsTwoIdentities(
        string bossId,
        string cardId,
        int authoredPowerCost,
        int authoredCooldown)
    {
        // Given a provisioned Boss Card whose ability identity equals the Boss's
        // SkillId, and a Boss the documented selection policy selects for a cast
        // (its cast-priority score out-scores the board; §4.3 steps 5–6), with
        // enough Power and a zero cooldown (§4.3 step 1),
        // When one Swap commits,
        // Then the batch carries exactly one BossCardCast — after the step-18b
        // Passive events and after the Player board cycle's summary — carrying only
        // its two identities, and no other Boss cast form, no Boss board cycle.
        const int StartingBossPower = 100;

        var authored = BossDefinitions.All.Single(definition => definition.BossId.Value == bossId);

        Assert.Equal(cardId, authored.SkillId);

        // The real provisioned row: the Boss Card identity, category, and authored
        // cost the migration stores (DATABASE.md §1; CARD_RULES.md §1.1 item 3;
        // TASK-309 §1's Power Cost table).
        var provisioned = ReadProvisionedBossCard(cardId);
        Assert.Equal(CardCategory.BossCard, provisioned.Category);
        Assert.Equal(authoredPowerCost, provisioned.PowerCost);

        var boss = authored with
        {
            // The cast is forced to win selection so the scenario does not depend on
            // the generated board's own score; the authored 85/board-score boundary
            // is Domain/Application-tested. The HP/ATK overrides keep the round
            // non-terminal, so the batch under test is an ordinary committed round.
            CastPriorityScore = int.MaxValue,
            MaxHP = 1_000_000,
            ATK = 0,
        };

        var battleId = $"task309-wire-cast-{cardId}";
        var created = await CreateBattleAsync(battleId, boss);

        await PinBossPowerAndCooldownAsync(battleId, StartingBossPower);

        // A Pet Power above the drain's authored 20 keeps the drain-power case's
        // signed delta deterministic (the floor at 0 would otherwise clamp it when
        // the round's own Power generation is empty). The other three abilities do
        // not read Pet Power.
        await PinPetPowerAsync(battleId, 50);

        var payload = await SwapOnceAndReadTheBatchAsync(
            battleId,
            FindMatchProducingPair(created.BoardState));

        var events = Events(payload);

        // ---- exactly one BossCardCast, carrying exactly the two identities ----
        var casts = events.Where(item => Type(item) == "BossCardCast").ToArray();
        Assert.Single(casts);

        var cast = casts[0];

        // §3.2.30 items 4–5: the payload is the discriminator, `cardId` (the Boss
        // Card definition identity, equal to the ability SkillId) and `sourceId`
        // (the Boss's canonical technical Identity) — and nothing else. Exact JSON
        // is compared so an added cost/Power/cooldown/effect member cannot slip
        // through a member-name check. The member order is the transport
        // projection's declared serialization order.
        Assert.Equal(
            $"{{\"type\":\"BossCardCast\",\"sourceId\":\"{bossId}\",\"cardId\":\"{cardId}\"}}",
            cast.GetRawText());

        Assert.Equal(
            ["cardId", "sourceId", "type"],
            cast.EnumerateObject().Select(property => property.Name).OrderBy(name => name, StringComparer.Ordinal));

        Assert.Equal(cardId, cast.GetProperty("cardId").GetString());
        Assert.Equal(bossId, cast.GetProperty("sourceId").GetString());

        // ---- the cast follows the step-18b Passive reports ----
        // GAME_EVENTS.md §1 fixes the order: 18b Passive events, then the cast at
        // 18c. For the match-charged Boss Passives the round genuinely reports (Hỏa
        // Long's Rage every 5 Matches, Mộc Yêu's regeneration every 8), the cast is
        // after every Boss-sourced Passive item; for the non-match-charged ones
        // (Thủy Ma, Sơn Thạch Vệ, Kim Lôi Vương) no such item exists and the
        // assertion states only the absence of a reversal.
        var castIndex = IndexOfType(events, "BossCardCast");

        var bossPassiveIndexes = events
            .Select((item, index) => (Item: item, Index: index))
            .Where(entry => Type(entry.Item) is "PassiveCharged" or "PassiveTriggered"
                && entry.Item.GetProperty("source").GetString() == "boss")
            .Select(entry => entry.Index)
            .ToArray();

        Assert.All(bossPassiveIndexes, index => Assert.True(index < castIndex));

        if (cardId is "flame-burst" or "root")
        {
            Assert.NotEmpty(bossPassiveIndexes);
        }

        // The Player board cycle's summary closes that cycle before the following
        // stage's items (§1.1 item 9), so it precedes the cast too.
        var summaryIndex = IndexOfType(events, "MatchedGemSummary");
        Assert.True(summaryIndex >= 0 && summaryIndex < castIndex);

        // ---- no Boss board cycle on a cast phase ----
        // A cast-selected phase performs no Boss board action: one boundary pair
        // only (the Player's), one board-cycle summary only, and no Boss-sourced
        // damage (§4.3 item 4, ADR-029 §2.7, GAME_EVENTS.md §1).
        var swapsStarted = events.Where(item => Type(item) == "SwapStarted").ToArray();
        Assert.Single(swapsStarted);
        Assert.Equal("player", swapsStarted[0].GetProperty("actor").GetString());
        Assert.Single(events, item => Type(item) == "SwapResolved");
        Assert.Single(events, item => Type(item) == "MatchedGemSummary");
        Assert.DoesNotContain(
            events,
            item => Type(item) == "DamageDealt" && item.GetProperty("source").GetString() == "boss");

        // ---- no other cast form for the same ability ----
        // BOSS_RULES.md §4.4: a Boss carries one form of an ability. The migrated
        // form therefore never emits the transitional BossSkillCast, and the Player
        // cast events are never used for a Boss cast (GAME_EVENTS.md §2;
        // SIGNALR_PROTOCOL.md §3.2.30 item 7).
        Assert.DoesNotContain(events, item => Type(item) == "BossSkillCast");
        Assert.DoesNotContain(events, item => Type(item) is "CardCast" or "PetSkillCast");

        // ---- the Boss's own Power spend emits no PowerChanged ----
        // §3.2.24 / GAME_EVENTS.md §2 item 5: the event reports the Pet-side
        // PetState.Power only. The spend is authoritative in `bossState`.
        var bossSourcedPowerChanges = events
            .Where(item => Type(item) == "PowerChanged"
                && item.GetProperty("source").GetString() == "boss")
            .ToArray();

        if (cardId == "drain-power")
        {
            // The one Boss-owned Power mutation this cast performs is the Pet-side
            // drain; that is the single "boss" item — never a second one for the
            // Boss's own cost (asserted in the dedicated drain test as well).
            Assert.Single(bossSourcedPowerChanges);
            Assert.Equal(-20, bossSourcedPowerChanges[0].GetProperty("delta").GetInt32());
        }
        else
        {
            Assert.Empty(bossSourcedPowerChanges);
        }

        // ---- the authoritative state carries the spend and the cooldown ----
        var after = await ReadStateAsync(battleId);

        Assert.Equal(StartingBossPower - authoredPowerCost, after.BossState.Power);
        Assert.Equal(authoredCooldown, after.BossState.SkillCooldown);
    }

    // =======================================================================
    // drain-power's Pet PowerChanged — SIGNALR_PROTOCOL.md §3.2.24,
    // GAME_EVENTS.md §2 items 4–5
    // =======================================================================

    [Fact]
    public async Task DrainPowerBossCardCast_ShouldCarryThePetPowerChangedInTheSameBatch_BeforeTheCast()
    {
        // GAME_EVENTS.md §2 item 5: a Boss Card cast's PowerDrain mutates the active
        // Pet's PetState.Power, so it emits the existing authoritative PowerChanged
        // for that mutation — inside the round's single ordered batch and before
        // that cast's BossCardCast — while the Boss's own cost spend (a
        // BossState.Power mutation, a different resource) emits none.
        const int StartingPetPower = 50;
        const int StartingBossPower = 100;
        const int DrainAmount = 20;

        var authored = BossDefinitions.ThuyMa;
        var boss = authored with
        {
            CastPriorityScore = int.MaxValue,
            MaxHP = 1_000_000,
            ATK = 0,
        };

        // The provisioned row's authored effect and cost — the values the documented
        // effect resolution and the cost gate/spend read (CARD_RULES.md §3.6;
        // BOSS_RULES.md §6.3.1 item 2; TASK-309 §1: drain-power = 25, -20 flat).
        var provisioned = ReadProvisionedBossCard("drain-power");
        Assert.Equal(CardEffectType.PowerDrain, provisioned.EffectDefinition[0].EffectType);
        Assert.Equal<int?>(DrainAmount, provisioned.EffectDefinition[0].Value);
        Assert.Equal(25, provisioned.PowerCost);

        const string battleId = "task309-wire-drain-power";
        var created = await CreateBattleAsync(battleId, boss);

        await PinBossPowerAndCooldownAsync(battleId, StartingBossPower);
        await PinPetPowerAsync(battleId, StartingPetPower);

        var payload = await SwapOnceAndReadTheBatchAsync(
            battleId,
            FindMatchProducingPair(created.BoardState));

        var events = Events(payload);

        // ---- exactly one Boss-sourced PowerChanged, and it is the drain ----
        var bossPowerChanges = events
            .Where(item => Type(item) == "PowerChanged"
                && item.GetProperty("source").GetString() == "boss")
            .ToArray();

        Assert.Single(bossPowerChanges);

        var drainEvent = bossPowerChanges[0];

        // The round's Player phase may itself have generated Pet Power from POWER
        // Gems (§1/§2, source="match"), so the resulting Power is the pinned value
        // plus those authoritative deltas minus the drain. Recomputing it from the
        // delivered deltas is what proves the event reports the mutation rather than
        // a guessed value (§3.2.24 items 1–2, item 6).
        var matchPowerDelta = events
            .Where(item => Type(item) == "PowerChanged"
                && item.GetProperty("source").GetString() == "match")
            .Sum(item => item.GetProperty("delta").GetInt32());

        var expectedPower = StartingPetPower + matchPowerDelta - DrainAmount;

        Assert.Equal(-DrainAmount, drainEvent.GetProperty("delta").GetInt32());
        Assert.Equal(expectedPower, drainEvent.GetProperty("power").GetInt32());

        // Exact JSON: the signed delta, the resulting Power, and the stage that owns
        // the mutation — exactly four members (§3.2.24).
        Assert.Equal(
            $"{{\"type\":\"PowerChanged\",\"source\":\"boss\",\"delta\":{-DrainAmount},\"power\":{expectedPower}}}",
            drainEvent.GetRawText());

        Assert.Equal(
            ["delta", "power", "source", "type"],
            drainEvent.EnumerateObject().Select(property => property.Name).OrderBy(name => name, StringComparer.Ordinal));

        // ---- inside the same batch, before BossCardCast ----
        var castIndex = IndexOfType(events, "BossCardCast");
        var drainIndex = Array.FindIndex(
            events,
            item => Type(item) == "PowerChanged"
                && item.GetProperty("source").GetString() == "boss");

        Assert.True(castIndex >= 0);
        Assert.True(
            drainIndex >= 0 && drainIndex < castIndex,
            "the drain's PowerChanged must precede BossCardCast (GAME_EVENTS.md §2 item 5).");

        // ---- the Boss's own cost spend emits none ----
        // Exactly one "boss" PowerChanged exists, and its delta is the drain — a
        // cost spend of 25 would have been a different delta and a second item. The
        // authoritative Boss Power records the spend instead.
        var after = await ReadStateAsync(battleId);

        Assert.Equal(StartingBossPower - provisioned.PowerCost, after.BossState.Power);
        Assert.Equal(expectedPower, after.PetState.Power);
        Assert.Equal(authored.SkillCooldownTurns, after.BossState.SkillCooldown);

        // No Boss PowerChanged exists at any other point in the batch.
        Assert.DoesNotContain(
            events,
            item => Type(item) == "PowerChanged"
                && item.GetProperty("source").GetString() == "boss"
                && item.GetProperty("delta").GetInt32() != -DrainAmount);
    }

    // =======================================================================
    // The Player cast contracts stay byte-identical — §3.2.20, §3.2.21
    // =======================================================================

    [Fact]
    public async Task PlayerCardCast_ShouldStillCarryExactlyTheDocumentedShape()
    {
        // CARD_RULES.md §6 / SIGNALR_PROTOCOL.md §3.2.20: a Basic Card cast reports
        // CardCast (type + cardId) and the authoritative PowerChanged for the cost it
        // pays. TASK-309 adds no member, no cost member, and no actor/source member.
        const string battleId = "task309-wire-player-cardcast";

        await CreateBattleAsync(
            battleId,
            BossDefinitions.HoaLong,
            LoadoutPet("card-heal", "card-shield", "card-power-charge", "card-inferno"));

        await PinPetPowerAsync(battleId, 100);

        var payload = await InvokeCastAndReadTheBatchAsync(
            battleId,
            isPetSkillCast: false,
            cardId: "card-heal",
            clientSequence: "client-seq-player-cardcast");

        var events = Events(payload);

        var cardCasts = events.Where(item => Type(item) == "CardCast").ToArray();
        Assert.Single(cardCasts);

        // Exact JSON, from the documented §3.2.20 shape: exactly `type` + `cardId`.
        Assert.Equal(
            "{\"type\":\"CardCast\",\"cardId\":\"card-heal\"}",
            cardCasts[0].GetRawText());

        // A Basic Card cast never emits the Signature Skill confirmation.
        Assert.DoesNotContain(events, item => Type(item) == "PetSkillCast");

        // And no Boss cast form is ever used for a Player cast.
        Assert.DoesNotContain(events, item => Type(item) is "BossCardCast" or "BossSkillCast");

        // The cost's Power mutation is still the Card stage's PowerChanged
        // (GAME_EVENTS.md §2 item 2: "card" covers the cost and any Power effect).
        var cost = events.Single(item => Type(item) == "PowerChanged");
        Assert.Equal("card", cost.GetProperty("source").GetString());
        Assert.Equal(-20, cost.GetProperty("delta").GetInt32());
        Assert.Equal(80, cost.GetProperty("power").GetInt32());
    }

    [Fact]
    public async Task PlayerPetSkillCast_ShouldStillCarryExactlyTheDocumentedShapes_InOrder()
    {
        // CARD_RULES.md §6 / SIGNALR_PROTOCOL.md §3.2.21, §3.2.22: a Pet Skill Card
        // cast emits CardCast and, in addition, PetSkillCast — each carrying exactly
        // `type` + `cardId`, in that order. TASK-309 changes neither payload.
        const string battleId = "task309-wire-player-petskillcast";

        await CreateBattleAsync(
            battleId,
            BossDefinitions.HoaLong,
            LoadoutPet("card-heal", "card-shield", "card-power-charge", "card-inferno"));

        await PinPetPowerAsync(battleId, 100);

        var payload = await InvokeCastAndReadTheBatchAsync(
            battleId,
            isPetSkillCast: true,
            cardId: null,
            clientSequence: "client-seq-player-petskillcast");

        var events = Events(payload);

        var cardCasts = events.Where(item => Type(item) == "CardCast").ToArray();
        var petSkillCasts = events.Where(item => Type(item) == "PetSkillCast").ToArray();

        Assert.Single(cardCasts);
        Assert.Single(petSkillCasts);

        // Exact JSON, from the documented §3.2.20/§3.2.21 shapes.
        Assert.Equal(
            "{\"type\":\"CardCast\",\"cardId\":\"card-inferno\"}",
            cardCasts[0].GetRawText());

        Assert.Equal(
            "{\"type\":\"PetSkillCast\",\"cardId\":\"card-inferno\"}",
            petSkillCasts[0].GetRawText());

        // §3.2.22: CardCast precedes PetSkillCast for the same cast.
        Assert.True(
            IndexOfType(events, "CardCast") < IndexOfType(events, "PetSkillCast"),
            "CardCast must precede PetSkillCast (SIGNALR_PROTOCOL.md §3.2.22).");

        // No new member anywhere on the two Player cast payloads, and no Boss form.
        Assert.Equal(
            ["cardId", "type"],
            cardCasts[0].EnumerateObject().Select(property => property.Name).OrderBy(name => name, StringComparer.Ordinal));
        Assert.Equal(
            ["cardId", "type"],
            petSkillCasts[0].EnumerateObject().Select(property => property.Name).OrderBy(name => name, StringComparer.Ordinal));
        Assert.DoesNotContain(events, item => Type(item) is "BossCardCast" or "BossSkillCast");
    }
}
