using System.Text.Json;
using GameServer.Api.Hubs;
using GameServer.Application.Battle;
using GameServer.Domain.Battle;
using GameServer.Domain.Bosses;
using GameServer.Domain.Elements;
using GameServer.Domain.Match3;
using GameServer.Domain.Passives;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GameServer.Api.Tests;

/// <summary>
/// End-to-end transport tests for the TASK-022 events — the Boss Response's
/// <c>BossSkillCast</c>, the shared Passive events' <c>source</c>/<c>sourceId</c>,
/// and the terminal <c>BattleWon</c>/<c>BattleLost</c> — over the real
/// <c>Swap</c> → <c>ReceiveEvents</c> path.
///
/// <code>
/// Rule (SIGNALR_PROTOCOL.md §3.2.16, §3.2.18, §3.2.19; BOSS_RULES.md §7)
///  ↓
/// Scenario (Given a battle whose Boss reaches the documented condition, When the
///           client commits a Swap, Then the batch carries the documented event)
///  ↓
/// Test
/// </code>
///
/// <b>These exercise the real boundary.</b> The batch is produced by the
/// Application layer, serialized by the SignalR client's own JSON, and read back
/// as JSON — so the discriminator, the camelCase member names, the string values,
/// and the omission rules are asserted on what a client actually receives, not on
/// an in-memory DTO.
/// </summary>
public class BossResponseWireTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public BossResponseWireTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private static readonly BattleStateService.PetConfiguration Pet =
        new(
            new GameServer.Domain.Pets.PetId("pet_instance_wire_boss_1"),
            Element.Hoa,
            new PassiveId("xich-lang"),
            PassiveThreshold: 5);

    /// <summary>
    /// The owning Player of the battles this suite creates
    /// (<c>GAME_STATE.md</c> §2.8) — recorded at creation and, per §2.8 item 3,
    /// excluded from every payload this suite asserts.
    /// </summary>
    private static readonly GameServer.Domain.Players.PlayerId Owner =
        new("player_boss_response_wire_owner");

    private HubConnection BuildHubConnection() =>
        new HubConnectionBuilder()
            .WithUrl("http://localhost/hubs/battle", options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
            })
            .Build();

    /// <summary>
    /// Creates a battle server-side against <paramref name="bossDefinition"/> and
    /// returns an adjacent pair of its generated board that produces a Match.
    /// </summary>
    private async Task<SwapRequest> CreateBattleWithPairAsync(string battleId, BossDefinition bossDefinition)
    {
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<BattleStateService>();

        var created = await service.CreateBattleAsync(battleId, Owner, Pet, bossDefinition);

        return FindMatchProducingPair(created.BoardState);
    }

    /// <summary>Commits one Swap and returns the received <c>ReceiveEvents</c> batch.</summary>
    private async Task<JsonElement> SwapOnceAndReadTheBatch(string battleId, SwapRequest pair)
    {
        var hubConnection = BuildHubConnection();
        var batch = new TaskCompletionSource<JsonElement>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        hubConnection.On<JsonElement>("ReceiveEvents", payload => batch.TrySetResult(payload));

        await hubConnection.StartAsync();
        await hubConnection.InvokeAsync("JoinBattle", battleId);

        var result = await hubConnection.InvokeAsync<SwapResponse>(
            "Swap", battleId, pair.From, pair.To, "client-seq-boss-response");

        Assert.True(result.Accepted);

        var payload = await batch.Task.WaitAsync(TimeSpan.FromSeconds(10));

        await hubConnection.StopAsync();

        return payload;
    }

    // =======================================================================
    // source / sourceId — SIGNALR_PROTOCOL.md §3.2.16, §3.2.17
    // =======================================================================

    [Fact]
    public async Task ReceiveEvents_PassiveCharged_ShouldCarrySourceAndSourceId()
    {
        // §3.2.16: `source` and `sourceId` are ALWAYS present on both shared Passive
        // events, alongside the identity, the progress, and the Threshold.
        const string battleId = "wire-boss-passive-source";
        var pair = await CreateBattleWithPairAsync(battleId, BossDefinitions.HoaLong);

        var payload = await SwapOnceAndReadTheBatch(battleId, pair);

        var charges = payload.GetProperty("events").EnumerateArray()
            .Where(e => e.GetProperty("type").GetString() == "PassiveCharged")
            .ToArray();

        Assert.NotEmpty(charges);

        // Both Passive systems charged on this Swap, and each reports its own source
        // (BOSS_RULES.md §7: the shared event is how the two are distinguished).
        Assert.Contains(charges, c => c.GetProperty("source").GetString() == "pet");
        Assert.Contains(charges, c => c.GetProperty("source").GetString() == "boss");

        // Every charge carries the member, and it is one of exactly the two
        // documented values.
        Assert.All(
            charges,
            c => Assert.Contains(c.GetProperty("source").GetString(), new[] { "pet", "boss" }));

        // The Boss's charges name the Boss by its canonical technical Identity
        // BossId — BOSS_RULES.md §6.4 / §3.2.16 item 2 — and its Passive identity
        // is the defined one.
        var bossCharges = charges
            .Where(c => c.GetProperty("source").GetString() == "boss")
            .ToArray();

        Assert.NotEmpty(bossCharges);

        Assert.All(
            bossCharges,
            c =>
            {
                Assert.Equal("boss-hoa-long", c.GetProperty("sourceId").GetString());
                Assert.Equal("boss-hoa-long-rage", c.GetProperty("passiveId").GetString());
                Assert.Equal(5, c.GetProperty("threshold").GetInt32());
            });

        // The Pet's charges name the Pet's own Passive.
        Assert.All(
            charges.Where(c => c.GetProperty("source").GetString() == "pet"),
            c => Assert.Equal("xich-lang", c.GetProperty("passiveId").GetString()));
    }

    [Fact]
    public async Task ReceiveEvents_BossPassive_ShouldNotAppearForTheAlwaysActiveBoss()
    {
        // BOSS_RULES.md §6.2: Thủy Ma's Passive is "always active" — never charged on
        // Player Matches, and emitting no PassiveCharged/PassiveTriggered from match
        // progress. The Pet's own charging is unaffected.
        const string battleId = "wire-thuy-ma-no-charge";
        var pair = await CreateBattleWithPairAsync(battleId, BossDefinitions.ThuyMa);

        var payload = await SwapOnceAndReadTheBatch(battleId, pair);

        var charges = payload.GetProperty("events").EnumerateArray()
            .Where(e => e.GetProperty("type").GetString() == "PassiveCharged")
            .ToArray();

        Assert.NotEmpty(charges);
        Assert.DoesNotContain(charges, c => c.GetProperty("source").GetString() == "boss");
        Assert.All(charges, c => Assert.Equal("pet", c.GetProperty("source").GetString()));
    }

    // =======================================================================
    // BossSkillCast — SIGNALR_PROTOCOL.md §3.2.18
    // =======================================================================

    [Fact]
    public async Task ReceiveEvents_BossSkillCast_ShouldCarrySkillIdAndSourceId()
    {
        // §3.2.18: `skillId` and `sourceId`, both always present. The Skill is driven
        // to fire through the documented condition — SkillCharge reaching the
        // requirement with SkillCooldown at 0 (GAME_STATE.md §2.4.3) — by using a
        // definition whose requirement one Swap's Matches satisfy.
        const string battleId = "wire-boss-skill-cast";

        var boss = BossDefinitions.HoaLong with
        {
            SkillDefinition = BossDefinitions.HoaLong.SkillDefinition
                with { ChargeRequirement = 1 },
        };
        var pair = await CreateBattleWithPairAsync(battleId, boss);

        var payload = await SwapOnceAndReadTheBatch(battleId, pair);

        var casts = payload.GetProperty("events").EnumerateArray()
            .Where(e => e.GetProperty("type").GetString() == "BossSkillCast")
            .ToArray();

        Assert.Single(casts);

        Assert.Equal("flame-burst", casts[0].GetProperty("skillId").GetString());
        Assert.Equal("boss-hoa-long", casts[0].GetProperty("sourceId").GetString());

        // §3.2.18: the cast carries exactly the discriminator and its two members.
        Assert.Equal(
            new[] { "skillId", "sourceId", "type" },
            casts[0].EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));

        // §3.2.18 item 3: the Skill's damage is carried by the damage events in the
        // same batch, with source="boss" and target="player".
        var bossDamage = payload.GetProperty("events").EnumerateArray()
            .Where(e => e.GetProperty("type").GetString() == "DamageDealt"
                && e.GetProperty("source").GetString() == "boss")
            .ToArray();

        Assert.Single(bossDamage);
        Assert.Equal("player", bossDamage[0].GetProperty("target").GetString());
    }

    [Fact]
    public async Task ReceiveEvents_BossDamage_ShouldReportBossToPlayer()
    {
        // COMBAT_RULES.md §3.4 / SIGNALR_PROTOCOL.md §3.2.14 item 3: the Boss's
        // instance reports source="boss", target="player" — the widening §3.2.14
        // records for Boss→Player damage. Both damage directions now travel in one
        // batch, so the two pairs are distinguished by these members.
        const string battleId = "wire-boss-damage-direction";
        var pair = await CreateBattleWithPairAsync(battleId, BossDefinitions.HoaLong);

        var payload = await SwapOnceAndReadTheBatch(battleId, pair);

        var dealt = payload.GetProperty("events").EnumerateArray()
            .Where(e => e.GetProperty("type").GetString() == "DamageDealt")
            .ToArray();

        Assert.Equal(2, dealt.Length);

        Assert.Equal("player", dealt[0].GetProperty("source").GetString());
        Assert.Equal("boss", dealt[0].GetProperty("target").GetString());
        Assert.Equal("boss", dealt[1].GetProperty("source").GetString());
        Assert.Equal("player", dealt[1].GetProperty("target").GetString());

        // Each instance reports its own amount, and both are positive at the MVP
        // stats — an instance of 0 would make the direction assertion vacuous.
        Assert.True(dealt[0].GetProperty("amount").GetInt32() > 0);
        Assert.True(dealt[1].GetProperty("amount").GetInt32() > 0);
    }

    // =======================================================================
    // BattleWon / BattleLost — SIGNALR_PROTOCOL.md §3.2.19
    // =======================================================================

    [Fact]
    public async Task ReceiveEvents_BattleWon_ShouldCarryTheOutcomeAndTerminalHp()
    {
        // §3.2.19: `outcome` is the string "victory" (item 1), and both terminal HP
        // values are always present (item 2). A 1-HP Boss cannot survive one Swap's
        // damage, so the Boss terminal check fires.
        const string battleId = "wire-battle-won";

        var boss = BossDefinitions.HoaLong with { MaxHP = 1 };
        var pair = await CreateBattleWithPairAsync(battleId, boss);

        var payload = await SwapOnceAndReadTheBatch(battleId, pair);

        var events = payload.GetProperty("events").EnumerateArray().ToArray();
        var won = events.Where(e => e.GetProperty("type").GetString() == "BattleWon").ToArray();

        Assert.Single(won);
        Assert.Equal("victory", won[0].GetProperty("outcome").GetString());
        Assert.Equal(0, won[0].GetProperty("finalBossHp").GetInt32());

        // The player was untouched — the Boss died before it could respond
        // (BOSS_RULES.md §5 item 4).
        Assert.Equal(1000, won[0].GetProperty("finalPlayerHp").GetInt32());

        // §3.2.19 item 3: no `reward summary` — deferred, not a wire member.
        Assert.False(won[0].TryGetProperty("reward", out _));
        Assert.False(won[0].TryGetProperty("rewardSummary", out _));

        // The outcome is the batch's LAST event: nothing follows the battle's end.
        Assert.Equal("BattleWon", events[^1].GetProperty("type").GetString());

        // And no Boss Response occurred at all.
        Assert.DoesNotContain(events, e => e.GetProperty("type").GetString() == "BossSkillCast");
        Assert.DoesNotContain(
            events,
            e => e.GetProperty("type").GetString() == "DamageDealt"
                && e.GetProperty("source").GetString() == "boss");
    }

    [Fact]
    public async Task ReceiveEvents_BattleLost_ShouldCarryTheOutcomeAndTerminalHp()
    {
        // §3.2.19's defeat shape. A Boss whose Basic Attack exceeds the player's
        // whole HP pool ends the battle at the post-response check.
        const string battleId = "wire-battle-lost";

        var boss = BossDefinitions.HoaLong with
        {
            ATK = 100_000,
            // Keep the Skill out of the way and the Passive inert so the
            // instance under test is the Basic Attack.
            SkillDefinition = BossDefinitions.HoaLong.SkillDefinition
                with { ChargeRequirement = int.MaxValue },
            PassiveDefinition = BossDefinitions.HoaLong.PassiveDefinition
                with { Threshold = null },
        };

        var pair = await CreateBattleWithPairAsync(battleId, boss);
        var payload = await SwapOnceAndReadTheBatch(battleId, pair);

        var events = payload.GetProperty("events").EnumerateArray().ToArray();
        var lost = events.Where(e => e.GetProperty("type").GetString() == "BattleLost").ToArray();

        Assert.Single(lost);
        Assert.Equal("defeat", lost[0].GetProperty("outcome").GetString());
        Assert.Equal(0, lost[0].GetProperty("finalPlayerHp").GetInt32());

        // The Boss survived, so its terminal HP is above 0.
        Assert.True(lost[0].GetProperty("finalBossHp").GetInt32() > 0);

        Assert.Equal("BattleLost", events[^1].GetProperty("type").GetString());
    }

    [Fact]
    public async Task ReceiveEvents_ShouldCarryNoOutcomeWhenBothSurvive()
    {
        // GAME_RULES.md §1.4: the battle ends only when a side reaches 0. At the MVP
        // stats neither does in one Swap, so neither outcome event is emitted — the
        // absence is the documented statement that the action was not terminal.
        const string battleId = "wire-no-outcome";
        var pair = await CreateBattleWithPairAsync(battleId, BossDefinitions.HoaLong);

        var payload = await SwapOnceAndReadTheBatch(battleId, pair);

        var types = payload.GetProperty("events").EnumerateArray()
            .Select(e => e.GetProperty("type").GetString())
            .ToArray();

        Assert.DoesNotContain("BattleWon", types);
        Assert.DoesNotContain("BattleLost", types);

        // The full response still ran: two damage instances, the player's first.
        Assert.Equal(2, types.Count(t => t == "DamageDealt"));
    }

    [Fact]
    public async Task ReceiveEvents_ShouldNotCarryAForbiddenBossEventName()
    {
        // BOSS_RULES.md §7: Boss Passive triggers use the shared Passive events, a
        // Boss Basic Attack is reported by its damage instance, and Boss state changes
        // are inferred from the sequence — so none of these names exists on the wire.
        const string battleId = "wire-no-forbidden-boss-events";
        var pair = await CreateBattleWithPairAsync(battleId, BossDefinitions.HoaLong);

        var payload = await SwapOnceAndReadTheBatch(battleId, pair);

        var types = payload.GetProperty("events").EnumerateArray()
            .Select(e => e.GetProperty("type").GetString())
            .ToArray();

        foreach (var forbidden in new[]
                 {
                     "BossPassiveCharged", "BossPassiveTriggered", "BossBasicAttack",
                     "BossEnraged", "BossStateChanged", "Enrage",
                 })
        {
            Assert.DoesNotContain(forbidden, types);
        }
    }

    // =======================================================================
    // Helpers
    // =======================================================================

    private static SwapRequest FindMatchProducingPair(BoardState board)
    {
        foreach (var (from, to) in AllAdjacentPairs())
        {
            if (MatchDetector.Detect(board.WithSwapped(from, to)).Count > 0)
            {
                return new SwapRequest(from, to);
            }
        }

        throw new InvalidOperationException(
            "A generated board has at least one valid Swap (MATCH3_RULES.md §1.4).");
    }

    private static IEnumerable<(int From, int To)> AllAdjacentPairs()
    {
        for (var index = 0; index < BoardState.CellCount; index++)
        {
            var right = BoardState.ToColumn(index) + 1 < BoardState.Columns ? index + 1 : -1;
            var down = index + BoardState.Width < BoardState.CellCount ? index + BoardState.Width : -1;

            if (right >= 0)
            {
                yield return (index, right);
            }

            if (down >= 0)
            {
                yield return (index, down);
            }
        }
    }
}
