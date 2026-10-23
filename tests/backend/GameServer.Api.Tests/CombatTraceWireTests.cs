using System.Text.Json;
using System.Text.Json.Serialization;
using GameServer.Api.Hubs;
using GameServer.Application.Battle;
using GameServer.Domain.Battle;
using GameServer.Domain.Bosses;
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
/// TASK-321 end-to-end wire suite for the optional <c>ReceiveEvents.combatTrace</c>
/// member (<c>SIGNALR_PROTOCOL.md</c> §3 item 5, §3.3).
///
/// <code>
/// Rule (SIGNALR_PROTOCOL.md §3 item 5, §3.3)
///  ↓
/// Scenario (a real Swap through the real hub)
///  ↓
/// Test (the serialized JSON a client receives)
/// </code>
///
/// <b>The transport boundary is the subject.</b> The batch is produced by the
/// Application layer, serialized by SignalR's own JSON, and read back as JSON, so
/// the member names, the correlation values, the omission rules, and the concrete
/// numbers are asserted on what a client actually receives — not on an in-memory
/// DTO.
/// </summary>
public class CombatTraceWireTests : IClassFixture<CombatTraceWireTests.CombatTraceWireFactory>
{
    private readonly CombatTraceWireFactory _factory;

    public CombatTraceWireTests(CombatTraceWireFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// The suite's host: the real pipeline with blank connection strings (no live
    /// PostgreSQL/Redis) and the application-session signing key supplied through
    /// configuration (<c>ADR-015</c> D10).
    /// </summary>
    public sealed class CombatTraceWireFactory : WebApplicationFactory<Program>
    {
        private readonly string _storeName = $"combat-trace-wire-{Guid.NewGuid():N}";

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

    private static readonly BattleStateService.PetConfiguration Pet =
        new(new PetId("pet_instance_combat_trace_wire"), Element.Hoa, new PassiveId("xich-lang"), PassiveThreshold: 5);

    private static readonly PlayerId Owner = new("player_combat_trace_wire_owner");

    private static readonly string OwnerSessionToken = TestApplicationSession.Mint(Owner.Value);

    private HubConnection BuildHubConnection() =>
        new HubConnectionBuilder()
            .WithUrl("http://localhost/hubs/battle", options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.AccessTokenProvider = () => Task.FromResult<string?>(OwnerSessionToken);
            })
            .Build();

    private async Task<SwapRequest> CreateBattleWithPairAsync(string battleId, BossDefinition boss)
    {
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<BattleStateService>();

        var created = await service.CreateBattleAsync(battleId, Owner, Pet, boss);

        return FindMatchProducingPair(created.BoardState, created.LastCommittedSwapPair);
    }

    private async Task<BattleState> ReadStateAsync(string battleId)
    {
        var repository = _factory.Services.GetRequiredService<IBattleStateRepository>();

        return await repository.GetAsync(battleId)
            ?? throw new InvalidOperationException($"battle '{battleId}' has no active-state record.");
    }

    private async Task<JsonElement> SwapOnceAndReadTheBatch(
        string battleId,
        SwapRequest pair,
        string actionId)
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

    // =======================================================================
    // 1. Correlation and structure
    // =======================================================================

    [Fact]
    public async Task Swap_ReceiveEvents_ShouldCarryACorrelatedCombatTrace()
    {
        const string battleId = "wire-combat-trace-correlation";
        var pair = await CreateBattleWithPairAsync(battleId, BossDefinitions.HoaLong);

        var payload = await SwapOnceAndReadTheBatch(battleId, pair, "trace-correlation-1");

        Assert.True(payload.TryGetProperty("combatTrace", out var trace));
        Assert.Equal(JsonValueKind.Object, trace.ValueKind);

        // §3.3.2 item 2: the trace repeats the envelope's own correlation values.
        Assert.Equal(battleId, trace.GetProperty("battleId").GetString());
        Assert.Equal(
            payload.GetProperty("serverSequence").GetInt32(),
            trace.GetProperty("serverSequence").GetInt32());

        // The move identity is the committed Swap's own two cells.
        var move = trace.GetProperty("move");
        Assert.Equal("swap", move.GetProperty("kind").GetString());
        Assert.Equal(pair.From, move.GetProperty("fromCell").GetInt32());
        Assert.Equal(pair.To, move.GetProperty("toCell").GetInt32());

        // The cycles are ordered and actor-bearing; no actor has to be inferred.
        var cycles = trace.GetProperty("cycles").EnumerateArray().ToArray();
        Assert.NotEmpty(cycles);
        Assert.Equal("player", cycles[0].GetProperty("actor").GetString());
        Assert.Equal("board", cycles[0].GetProperty("action").GetString());
        Assert.Equal(1, cycles[0].GetProperty("ordinal").GetInt32());
        Assert.Equal(pair.From, cycles[0].GetProperty("fromCell").GetInt32());
        Assert.Equal(pair.To, cycles[0].GetProperty("toCell").GetInt32());

        // A card cycle's members are not applicable and are omitted, not null.
        Assert.False(cycles[0].TryGetProperty("cardId", out _));

        var matchedGems = cycles[0].GetProperty("matchedGems").EnumerateArray().ToArray();
        Assert.Equal(4, matchedGems.Length);
        Assert.Equal("ATK", matchedGems[0].GetProperty("gemType").GetString());

        // The cascades are the resolution's ordered passes.
        var cascades = cycles[0].GetProperty("cascades").EnumerateArray().ToArray();
        Assert.NotEmpty(cascades);
        Assert.Equal(1, cascades[0].GetProperty("depth").GetInt32());

        // Both snapshots are present and carry the authoritative stats.
        var pet = trace.GetProperty("pet");
        var boss = trace.GetProperty("boss");
        foreach (var snapshot in new[] { pet, boss })
        {
            Assert.True(snapshot.TryGetProperty("hpBefore", out _));
            Assert.True(snapshot.TryGetProperty("hpAfter", out _));
            Assert.True(snapshot.TryGetProperty("defBase", out _));
            Assert.True(snapshot.TryGetProperty("powerBefore", out _));
            Assert.True(snapshot.TryGetProperty("powerAfter", out _));
        }

        Assert.Equal(JsonValueKind.Array, trace.GetProperty("damage").ValueKind);
        Assert.Equal(JsonValueKind.Array, trace.GetProperty("healing").ValueKind);
    }

    // =======================================================================
    // 2. Damage instances carry the authoritative DEF and HP values
    // =======================================================================

    [Fact]
    public async Task Swap_ReceiveEvents_CombatTraceDamage_ShouldCarryAuthoritativeDefenseAndHpLoss()
    {
        const string battleId = "wire-combat-trace-damage";
        var pair = await CreateBattleWithPairAsync(battleId, BossDefinitions.HoaLong);

        JsonElement? sample = null;
        var stateBefore = await ReadStateAsync(battleId);

        for (var round = 0; sample is null && round < 40; round++)
        {
            var payload = await SwapOnceAndReadTheBatch(battleId, pair, $"{battleId}-round-{round}");
            var trace = payload.GetProperty("combatTrace");
            var attacks = trace.GetProperty("damage").EnumerateArray()
                .Where(d =>
                    d.GetProperty("source").GetString() == "player"
                    && d.GetProperty("target").GetString() == "boss")
                .ToArray();

            if (attacks.Length > 0)
            {
                sample = attacks[0];
            }

            stateBefore = await ReadStateAsync(battleId);
            pair = FindMatchProducingPair(stateBefore.BoardState, stateBefore.LastCommittedSwapPair);
        }

        Assert.NotNull(sample);
        var attack = sample!.Value;

        // The trace's own breakdown is the pipeline's: the DEF the attack step read is
        // the Boss's stored DEF (never a default), and the effective DEF is a real
        // integer at or above the stored base.
        Assert.Equal(stateBefore.BossState.DEF, attack.GetProperty("defenderDefBase").GetInt32());
        Assert.True(attack.GetProperty("defenderDefEffective").GetInt32() >= attack.GetProperty("defenderDefBase").GetInt32());

        // Shield absorption and HP loss are separate, and they add up to the instance
        // (COMBAT_RULES.md §4 items 2, 5; §3 steps 5–6).
        var finalDamage = attack.GetProperty("finalDamage").GetInt32();
        var absorbed = attack.GetProperty("shieldAbsorbed").GetInt32();
        var hpBefore = attack.GetProperty("hpBefore").GetInt32();
        var hpAfter = attack.GetProperty("hpAfter").GetInt32();
        var hpLoss = attack.GetProperty("hpLoss").GetInt32();

        Assert.Equal(hpBefore - hpAfter, hpLoss);
        Assert.Equal(finalDamage - absorbed, hpLoss);
        Assert.True(hpLoss >= 0);

        // A real zero is written as 0, not omitted.
        Assert.True(attack.TryGetProperty("defenderDefBase", out _));
        Assert.True(attack.TryGetProperty("defenderDefEffective", out _));
    }

    [Fact]
    public async Task Swap_ReceiveEvents_CombatTrace_ZeroDefBoss_ShouldWriteAnExplicitZero()
    {
        // Sơn Thạch Vệ stores DEF 0 (BOSS_RULES.md §6.4): an explicit 0, never a
        // default substituted by the transport or omitted as "not applicable".
        const string battleId = "wire-combat-trace-zero-def";
        var pair = await CreateBattleWithPairAsync(battleId, BossDefinitions.SonThachVe);
        var stateBefore = await ReadStateAsync(battleId);
        Assert.Equal(0, stateBefore.BossState.DEF);

        JsonElement? sample = null;
        for (var round = 0; sample is null && round < 40; round++)
        {
            var payload = await SwapOnceAndReadTheBatch(battleId, pair, $"{battleId}-round-{round}");
            sample = payload.GetProperty("combatTrace").GetProperty("damage").EnumerateArray()
                .Where(d =>
                    d.GetProperty("source").GetString() == "player"
                    && d.GetProperty("target").GetString() == "boss")
                .Cast<JsonElement?>()
                .FirstOrDefault();

            stateBefore = await ReadStateAsync(battleId);
            pair = FindMatchProducingPair(stateBefore.BoardState, stateBefore.LastCommittedSwapPair);
        }

        Assert.NotNull(sample);
        Assert.True(
            sample!.Value.TryGetProperty("defenderDefBase", out var baseDefense),
            "a zero DEF must be written explicitly, not omitted as 'not applicable'");
        Assert.Equal(0, baseDefense.GetInt32());

        // The effective DEF is 0 unless a Boss-owned DEF-Gem buff from an earlier
        // round is still active (COMBAT_RULES.md §5.5.6); either way it is at or
        // above the stored base, never a default.
        Assert.True(sample.Value.GetProperty("defenderDefEffective").GetInt32() >= 0);
    }

    // =======================================================================
    // 3. Serialization contract — camelCase names and omission, not nulls
    // =======================================================================

    [Fact]
    public void CombatTraceWireDto_ShouldSerializeCamelCaseAndOmitNotApplicableMembers()
    {
        // SIGNALR_PROTOCOL.md §3.2.3 fixes camelCase explicitly and §3.2.5 requires a
        // not-applicable member to be omitted rather than written as null.
        var trace = new CombatTraceWireDto(
            BattleId: "battle-serialization",
            ServerSequence: 7,
            Move: new CombatTraceMoveWireDto(Kind: "swap", FromCell: 1, ToCell: 2),
            Cycles:
            [
                new CombatTraceCycleWireDto(
                    Ordinal: 1,
                    Actor: "boss",
                    Action: "card",
                    FromCell: null,
                    ToCell: null,
                    CardId: "flame-burst",
                    MatchedGems: [new CombatTraceGemWireDto(GemType: "ATK", Count: 3)],
                    Cascades: []),
            ],
            Damage:
            [
                new CombatTraceDamageWireDto(
                    Ordinal: 1,
                    Phase: "statusTick",
                    CycleOrdinal: null,
                    Source: "player",
                    Target: "boss",
                    Base: 30,
                    ComboModifier: 1.0,
                    ElementModifier: 1.5,
                    OtherModifiers: 1.0,
                    MitigatedDamage: 20.0,
                    FinalDamage: 20,
                    DefenderDefBase: 0,
                    DefenderDefEffective: 0,
                    ShieldAbsorbed: 0,
                    HpBefore: 100,
                    HpAfter: 80,
                    HpLoss: 20),
            ],
            Healing:
            [
                new CombatTraceHealWireDto(
                    Ordinal: 1,
                    CycleOrdinal: 1,
                    Target: "player",
                    Amount: 0,
                    RawAmount: 15,
                    HpBefore: 100,
                    HpAfter: 100),
            ],
            Pet: new CombatTraceTargetWireDto(
                HpBefore: 100, HpAfter: 100, DefBase: 25, PowerBefore: 0, PowerAfter: 0),
            Boss: new CombatTraceTargetWireDto(
                HpBefore: 900, HpAfter: 880, DefBase: 0, PowerBefore: 0, PowerAfter: 0));

        var json = JsonSerializer.Serialize(trace);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        // The root's contract member names.
        Assert.Equal(
            new[] { "battleId", "boss", "cycles", "damage", "healing", "move", "pet", "serverSequence" },
            root.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));

        // A board action's cells and a card action's identity are each not
        // applicable on the other kind: omitted, never null.
        var cycle = root.GetProperty("cycles")[0];
        Assert.False(cycle.TryGetProperty("fromCell", out _));
        Assert.False(cycle.TryGetProperty("toCell", out _));
        Assert.Equal("flame-burst", cycle.GetProperty("cardId").GetString());

        // A status tick belongs to no cycle: cycleOrdinal is omitted.
        var damage = root.GetProperty("damage")[0];
        Assert.False(damage.TryGetProperty("cycleOrdinal", out _));
        Assert.Equal("statusTick", damage.GetProperty("phase").GetString());

        // The damage breakdown and DEF members keep their fixed names, and a real
        // zero DEF is written.
        Assert.Equal(30, damage.GetProperty("base").GetInt32());
        Assert.Equal(20.0, damage.GetProperty("mitigatedDamage").GetDouble());
        Assert.Equal(0, damage.GetProperty("defenderDefBase").GetInt32());
        Assert.Equal(0, damage.GetProperty("defenderDefEffective").GetInt32());
        Assert.Equal(20, damage.GetProperty("finalDamage").GetInt32());
        Assert.Equal(0, damage.GetProperty("shieldAbsorbed").GetInt32());
        Assert.Equal(20, damage.GetProperty("hpLoss").GetInt32());

        // The heal keeps its raw amount and its explicit zero applied amount.
        var heal = root.GetProperty("healing")[0];
        Assert.Equal(0, heal.GetProperty("amount").GetInt32());
        Assert.Equal(15, heal.GetProperty("rawAmount").GetInt32());

        // No member is written as null anywhere in the trace.
        Assert.DoesNotContain("null", json, StringComparison.Ordinal);

        // Every member survives a deserialization round trip: the wire names are
        // symmetric, and an omitted not-applicable member comes back as absent.
        var roundTripped = JsonSerializer.Deserialize<CombatTraceWireDto>(json);
        Assert.NotNull(roundTripped);
        var parsed = roundTripped!;

        Assert.Equal("battle-serialization", parsed.BattleId);
        Assert.Equal(7, parsed.ServerSequence);
        Assert.Equal("swap", parsed.Move.Kind);
        Assert.Equal(1, parsed.Move.FromCell);
        Assert.Equal(2, parsed.Move.ToCell);
        Assert.Equal("boss", parsed.Cycles[0].Actor);
        Assert.Equal("card", parsed.Cycles[0].Action);
        Assert.Null(parsed.Cycles[0].FromCell);
        Assert.Null(parsed.Cycles[0].ToCell);
        Assert.Equal("flame-burst", parsed.Cycles[0].CardId);
        Assert.Equal("ATK", parsed.Cycles[0].MatchedGems[0].GemType);
        Assert.Equal(3, parsed.Cycles[0].MatchedGems[0].Count);
        Assert.Empty(parsed.Cycles[0].Cascades);
        Assert.Equal("statusTick", parsed.Damage[0].Phase);
        Assert.Null(parsed.Damage[0].CycleOrdinal);
        Assert.Equal("player", parsed.Damage[0].Source);
        Assert.Equal("boss", parsed.Damage[0].Target);
        Assert.Equal(0, parsed.Damage[0].DefenderDefBase);
        Assert.Equal(0, parsed.Damage[0].DefenderDefEffective);
        Assert.Equal(20, parsed.Damage[0].HpLoss);
        Assert.Equal(0, parsed.Healing[0].Amount);
        Assert.Equal(15, parsed.Healing[0].RawAmount);
        Assert.Equal(100, parsed.Pet.HpBefore);
        Assert.Equal(0, parsed.Boss.DefBase);
    }

    // =======================================================================
    // 4. TASK-322 — the not-applicable omission rule, both directions
    // =======================================================================

    /// <summary>
    /// <c>SIGNALR_PROTOCOL.md</c> §3.3.1's presence rule and §3.3.2 item 7: an
    /// omitted member is a *not-applicable* omission — absent from the JSON, never
    /// written as <c>null</c> — while a value the entry really carries is written.
    ///
    /// <b>The two directions are asserted in one place on purpose.</b> The defect
    /// TASK-322 corrects is a presence statement that made a member simultaneously
    /// mandatory and omittable, so a test that could only show one direction would
    /// not pin the rule. A status tick belongs to no actor cycle, so its
    /// <c>cycleOrdinal</c> is omitted; the attack instance in the same array names
    /// its cycle and is written.
    /// </summary>
    [Fact]
    public void CombatTraceWireDto_DamageCycleOrdinal_ShouldBeOmittedForAStatusTickAndWrittenForAnAttack()
    {
        var trace = new CombatTraceWireDto(
            BattleId: "battle-cycle-ordinal",
            ServerSequence: 9,
            Move: new CombatTraceMoveWireDto(Kind: "swap", FromCell: 9, ToCell: 10),
            Cycles:
            [
                new CombatTraceCycleWireDto(
                    Ordinal: 1,
                    Actor: "player",
                    Action: "board",
                    FromCell: 9,
                    ToCell: 10,
                    CardId: null,
                    MatchedGems: [new CombatTraceGemWireDto(GemType: "ATK", Count: 3)],
                    Cascades: [new CombatTraceCascadeWireDto(Depth: 1)]),
            ],
            Damage:
            [
                AttackInstance(ordinal: 1, cycleOrdinal: 1),
                StatusTickInstance(ordinal: 2),
            ],
            Healing: [],
            Pet: new CombatTraceTargetWireDto(
                HpBefore: 100, HpAfter: 60, DefBase: 25, PowerBefore: 0, PowerAfter: 0),
            Boss: new CombatTraceTargetWireDto(
                HpBefore: 900, HpAfter: 880, DefBase: 50, PowerBefore: 0, PowerAfter: 0));

        var json = JsonSerializer.Serialize(trace);
        using var document = JsonDocument.Parse(json);
        var damage = document.RootElement.GetProperty("damage");

        // The attack instance names the cycle that produced it: the member is
        // present and carries the actual ordinal (0 is a real value, so it is
        // present too — this is not an omission).
        var attack = damage[0];
        Assert.True(attack.TryGetProperty("cycleOrdinal", out var attackCycle));
        Assert.Equal(1, attackCycle.GetInt32());
        Assert.Equal("attack", attack.GetProperty("phase").GetString());

        // The status tick belongs to no cycle: omitted entirely, and never null.
        var tick = damage[1];
        Assert.False(
            tick.TryGetProperty("cycleOrdinal", out _),
            "a status tick belongs to no actor cycle, so cycleOrdinal is a not-applicable omission (§3.3.1, §3.2.5)");
        Assert.Equal("statusTick", tick.GetProperty("phase").GetString());

        // No member of the payload is written as null anywhere, in either array.
        Assert.DoesNotContain("null", json, StringComparison.Ordinal);

        // An absent member stays absent across a wire round trip, and the written
        // one stays written (the omission is not a serializer artifact).
        var roundTripped = JsonSerializer.Deserialize<CombatTraceWireDto>(json);
        Assert.NotNull(roundTripped);
        Assert.Equal(1, roundTripped!.Damage[0].CycleOrdinal);
        Assert.Null(roundTripped.Damage[1].CycleOrdinal);
        Assert.Equal("attack", roundTripped.Damage[0].Phase);
        Assert.Equal("statusTick", roundTripped.Damage[1].Phase);
    }

    /// <summary>
    /// <c>SIGNALR_PROTOCOL.md</c> §3.3.1's Heal object: <c>cycleOrdinal</c> is a
    /// not-applicable omission when the heal belongs to no actor cycle, while a
    /// heal that restored <c>0</c> at full HP is still applicable — it names its
    /// cycle and its <c>amount</c> is written as a real zero.
    ///
    /// <b>Both halves are asserted on the serialized wire.</b> The rule is only
    /// realized if the transport can produce it: a heal naming a cycle writes the
    /// numeric ordinal, and a heal that belongs to no actor cycle omits the member
    /// entirely — it is not written as <c>null</c> and not as the <c>0</c> sentinel
    /// that would name no cycle while still being written (OD-322-1, as resolved by
    /// TASK-324). A DTO or serializer change that reintroduced either substitute
    /// fails here.
    /// </summary>
    [Fact]
    public void CombatTraceWireDto_HealCycleOrdinal_ShouldBeOmittedWhenTheHealBelongsToNoCycle()
    {
        // The documented §3.3.1 payload: an applicable heal, and a heal that
        // belongs to no actor cycle (its cycleOrdinal absent).
        const string documentedJson = """
            {
                "battleId": "battle-heal-cycle-ordinal",
                "serverSequence": 3,
                "move": { "kind": "swap", "fromCell": 0, "toCell": 1 },
                "cycles": [],
                "damage": [],
                "healing": [
                    { "ordinal": 1, "cycleOrdinal": 1, "target": "player", "amount": 0, "rawAmount": 15, "hpBefore": 100, "hpAfter": 100 },
                    { "ordinal": 2, "target": "boss", "amount": 12, "rawAmount": 12, "hpBefore": 40, "hpAfter": 52 }
                ],
                "pet": { "hpBefore": 100, "hpAfter": 100, "defBase": 25, "powerBefore": 0, "powerAfter": 0 },
                "boss": { "hpBefore": 900, "hpAfter": 900, "defBase": 50, "powerBefore": 0, "powerAfter": 0 }
            }
            """;

        using (var documented = JsonDocument.Parse(documentedJson))
        {
            var healing = documented.RootElement.GetProperty("healing");

            // Applicable heal: the member is written, and a real 0 is still written.
            Assert.True(healing[0].TryGetProperty("cycleOrdinal", out var cycledOrdinal));
            Assert.Equal(1, cycledOrdinal.GetInt32());
            Assert.Equal(0, healing[0].GetProperty("amount").GetInt32());

            // Heal belonging to no cycle: omitted entirely, and never null.
            Assert.False(
                healing[1].TryGetProperty("cycleOrdinal", out _),
                "a heal that belongs to no actor cycle omits cycleOrdinal (§3.3.1 presence rule, §3.2.5)");
            Assert.Equal(12, healing[1].GetProperty("amount").GetInt32());
            Assert.DoesNotContain("null", documentedJson, StringComparison.Ordinal);
        }

        // Now the transport produces that same shape. The uncycled heal is expressed
        // the way the producer expresses it — the member absent from the Domain value —
        // and the serializer is what must omit it.
        var trace = new CombatTraceWireDto(
            BattleId: "battle-heal-cycle-ordinal",
            ServerSequence: 3,
            Move: new CombatTraceMoveWireDto(Kind: "swap", FromCell: 0, ToCell: 1),
            Cycles: [],
            Damage: [],
            Healing:
            [
                new CombatTraceHealWireDto(
                    Ordinal: 1,
                    CycleOrdinal: 1,
                    Target: "player",
                    Amount: 0,
                    RawAmount: 15,
                    HpBefore: 100,
                    HpAfter: 100),
                new CombatTraceHealWireDto(
                    Ordinal: 2,
                    CycleOrdinal: null,
                    Target: "boss",
                    Amount: 12,
                    RawAmount: 12,
                    HpBefore: 40,
                    HpAfter: 52),
            ],
            Pet: new CombatTraceTargetWireDto(
                HpBefore: 100, HpAfter: 100, DefBase: 25, PowerBefore: 0, PowerAfter: 0),
            Boss: new CombatTraceTargetWireDto(
                HpBefore: 900, HpAfter: 900, DefBase: 50, PowerBefore: 0, PowerAfter: 0));

        var json = JsonSerializer.Serialize(trace);
        using var document = JsonDocument.Parse(json);
        var written = document.RootElement.GetProperty("healing");

        // The applicable heal writes its ordinal; its real zero amount is written too.
        Assert.Equal(1, written[0].GetProperty("cycleOrdinal").GetInt32());
        Assert.Equal(0, written[0].GetProperty("amount").GetInt32());

        // The uncycled heal omits the member: not present, not null, and not 0. The
        // exact serialized member set is pinned, so neither substitute can slip in.
        Assert.Equal(
            new[] { "amount", "hpAfter", "hpBefore", "ordinal", "rawAmount", "target" },
            written[1].EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));
        Assert.DoesNotContain("cycleOrdinal", written[1].EnumerateObject().Select(p => p.Name));
        Assert.Equal(12, written[1].GetProperty("amount").GetInt32());

        // The transport writes no `null` for the omitted member anywhere, in either the
        // delivered document form or the transport's own.
        Assert.DoesNotContain("null", json, StringComparison.Ordinal);

        // An absent member stays absent across a wire round trip: the omission is the
        // serializer's, not an artifact of the in-memory value.
        var roundTripped = JsonSerializer.Deserialize<CombatTraceWireDto>(json);
        Assert.NotNull(roundTripped);
        Assert.Equal(1, roundTripped!.Healing[0].CycleOrdinal);
        Assert.Null(roundTripped.Healing[1].CycleOrdinal);
        Assert.Equal(0, roundTripped.Healing[0].Amount);
        Assert.Equal(12, roundTripped.Healing[1].Amount);
    }

    /// <summary>
    /// Both omission directions above in the one shape the contract actually pins:
    /// the serialized member sets of a trace in which every not-applicable member
    /// is absent and every real zero is written.
    ///
    /// <b>It is built from the transport's own DTOs, not from JSON literals</b>, so
    /// a serializer or DTO change is what makes it fail — the members it pins are
    /// the ones the producer really does omit today (a card cycle's cells, a status
    /// tick's <c>cycleOrdinal</c>) plus the ones whose presence a real ignore-null
    /// condition could silently break (the snapshots' five members, both arrays, and
    /// the emptiness-preserving <c>matchedGems</c>/<c>cascades</c>).
    /// </summary>
    [Fact]
    public void CombatTraceWireDto_ShouldKeepTheDocumentedMemberSetsWhenNotApplicableMembersAreOmitted()
    {
        var trace = new CombatTraceWireDto(
            BattleId: "battle-member-sets",
            ServerSequence: 4,
            Move: new CombatTraceMoveWireDto(Kind: "swap", FromCell: 2, ToCell: 3),
            Cycles:
            [
                new CombatTraceCycleWireDto(
                    Ordinal: 1,
                    Actor: "boss",
                    Action: "card",
                    FromCell: null,
                    ToCell: null,
                    CardId: "earthquake",
                    MatchedGems: [],
                    Cascades: []),
            ],
            Damage: [StatusTickInstance(ordinal: 1)],
            Healing: [],
            Pet: new CombatTraceTargetWireDto(
                HpBefore: 100, HpAfter: 100, DefBase: 25, PowerBefore: 10, PowerAfter: 10),
            Boss: new CombatTraceTargetWireDto(
                HpBefore: 895, HpAfter: 900, DefBase: 50, PowerBefore: 30, PowerAfter: 0));

        var json = JsonSerializer.Serialize(trace);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        // The root keeps its eight documented members, in camelCase.
        Assert.Equal(
            new[] { "battleId", "boss", "cycles", "damage", "healing", "move", "pet", "serverSequence" },
            root.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));

        // A card cycle omits the board cells it does not have, and keeps its
        // identity and its (empty but present) arrays.
        var cycle = root.GetProperty("cycles")[0];
        Assert.Equal(
            new[] { "action", "actor", "cardId", "cascades", "matchedGems", "ordinal" },
            cycle.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));

        // A status tick belongs to no cycle.
        var damage = root.GetProperty("damage")[0];
        Assert.DoesNotContain("cycleOrdinal", damage.EnumerateObject().Select(p => p.Name));

        // Both snapshots keep all five members: a zero is a value, not an omission.
        var boss = root.GetProperty("boss");
        Assert.Equal(
            new[] { "defBase", "hpAfter", "hpBefore", "powerAfter", "powerBefore" },
            boss.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));
        Assert.Equal(0, boss.GetProperty("powerAfter").GetInt32());

        Assert.Empty(root.GetProperty("healing").EnumerateArray());
        Assert.DoesNotContain("null", json, StringComparison.Ordinal);
    }

    /// <summary>One ATK-gated attack instance naming its cycle.</summary>
    private static CombatTraceDamageWireDto AttackInstance(int ordinal, int cycleOrdinal) =>
        new(
            Ordinal: ordinal,
            Phase: "attack",
            CycleOrdinal: cycleOrdinal,
            Source: "player",
            Target: "boss",
            Base: 30,
            ComboModifier: 1.0,
            ElementModifier: 1.0,
            OtherModifiers: 1.0,
            MitigatedDamage: 20.0,
            FinalDamage: 20,
            DefenderDefBase: 50,
            DefenderDefEffective: 50,
            ShieldAbsorbed: 0,
            HpBefore: 900,
            HpAfter: 880,
            HpLoss: 20);

    /// <summary>One step-19a tick, which belongs to no actor cycle.</summary>
    private static CombatTraceDamageWireDto StatusTickInstance(int ordinal) =>
        new(
            Ordinal: ordinal,
            Phase: "statusTick",
            CycleOrdinal: null,
            Source: "player",
            Target: "boss",
            Base: 50,
            ComboModifier: 1.0,
            ElementModifier: 1.0,
            OtherModifiers: 1.0,
            MitigatedDamage: 40.0,
            FinalDamage: 40,
            DefenderDefBase: 50,
            DefenderDefEffective: 50,
            ShieldAbsorbed: 0,
            HpBefore: 880,
            HpAfter: 840,
            HpLoss: 40);

    // =======================================================================
    // Fixtures
    // =======================================================================

    private static SwapRequest FindMatchProducingPair(BoardState board, CommittedSwapPair? committed)
    {
        foreach (var (from, to) in AllAdjacentPairs())
        {
            if (committed is { } pair
                && CommittedSwapPair.FromCells(from, to)
                    == CommittedSwapPair.FromCells(pair.MinCellIndex, pair.MaxCellIndex))
            {
                continue;
            }

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
