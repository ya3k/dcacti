using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GameServer.Application.Battle;
using GameServer.Domain.Battle;
using GameServer.Domain.Bosses;
using GameServer.Domain.Elements;
using GameServer.Domain.Match3;
using GameServer.Domain.Passives;
using GameServer.Domain.Pets;
using GameServer.Domain.Players;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace GameServer.Api.Tests;

/// <summary>
/// <c>GET /api/battle/{battleId}/result</c> — <c>API_CONTRACTS.md</c> §4, §6
/// (TASK-041).
///
/// <code>
/// Authorization: Bearer &lt;sessionToken&gt;
///         ↓
/// GET /api/battle/{battleId}/result
///         ↓
/// authenticated PlayerId          (the player_id claim — API_CONTRACTS.md §2.3)
///         ↓
/// BattleResult row                (DATABASE.md §1)
///         ↓
/// 200 owner | 404 foreign | 404 missing | 401 unauthenticated
/// </code>
///
/// <b>What these tests establish.</b> §4's four documented responses, and the
/// invariant behind them: the caller's identity comes from the session
/// (<c>ADR-015</c> D3), never from the request — so a caller cannot read another
/// Player's result, and cannot select whose result to read. The owner check is
/// exercised with real issued sessions against the real authentication pipeline.
/// </summary>
public class BattleResultEndpointTests
{
    // -----------------------------------------------------------------------
    // 401 — API_CONTRACTS.md §1, §2.3, §4 note 6
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Result_WithoutASession_ShouldReturnUnauthenticated()
    {
        // API_CONTRACTS.md §4 note 6: a caller that presents no authenticated
        // session receives 401 UNAUTHENTICATED — NOT 404 BATTLE_NOT_FOUND, which
        // note 6 reserves for an authenticated caller and which would make an
        // authorization failure indistinguishable from a missing row.
        using var factory = new ResultFactory();

        var response = await factory.GetResultAsync(factory.CreateClient(), "any-battle", session: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("UNAUTHENTICATED", body.GetProperty("error").GetString());
    }

    [Theory]
    [InlineData("not-a-jwt")]
    [InlineData("header.payload.signature")]
    public async Task Result_WithAnInvalidSession_ShouldReturnUnauthenticated(string token)
    {
        // §2.3 / §4 note 6: a missing, invalid/tampered, or expired session all
        // resolve to the same single 401 UNAUTHENTICATED response, with no distinct
        // code and no token-validation detail disclosed.
        using var factory = new ResultFactory();

        var response = await factory.GetResultAsync(factory.CreateClient(), "any-battle", token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("UNAUTHENTICATED", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Result_WithATamperedSession_ShouldReturnUnauthenticated()
    {
        // A token signed with a key outside the configured validation set (ADR-015
        // D11: exactly the current and previous key are accepted) is refused with
        // the same single code.
        using var factory = new ResultFactory();

        var response = await factory.GetResultAsync(
            factory.CreateClient(),
            "any-battle",
            TestApplicationSession.MintWithUnknownKey("player_anyone"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("UNAUTHENTICATED", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Result_WithAnExpiredSession_ShouldReturnUnauthenticated()
    {
        // ADR-015 D5: 24h absolute expiry. A session past it is refused with the
        // same single code — no separate "expired" outcome is disclosed.
        using var factory = new ResultFactory();

        var expired = TestApplicationSession.Mint(
            playerId: "player_expired",
            notBefore: DateTime.UtcNow.AddHours(-48),
            expires: DateTime.UtcNow.AddHours(-24));

        var response = await factory.GetResultAsync(factory.CreateClient(), "any-battle", expired);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("UNAUTHENTICATED", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Result_WithASessionCarryingNoPlayerIdentity_ShouldReturnUnauthenticated()
    {
        // A token that validates but carries no player_id identifies nobody, so it
        // is not an authenticated caller — and API_CONTRACTS.md §4 note 7 forbids
        // any request-supplied substitute for the identity.
        using var factory = new ResultFactory();

        var identityless = TestApplicationSession.Mint(playerId: null);

        var response = await factory.GetResultAsync(factory.CreateClient(), "any-battle", identityless);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("UNAUTHENTICATED", body.GetProperty("error").GetString());
    }

    // -----------------------------------------------------------------------
    // 404 — missing result and foreign result are one answer
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Result_ForAnUnknownBattle_ShouldReturnBattleNotFound()
    {
        // API_CONTRACTS.md §4: a result that does not exist returns 404 with the §6
        // envelope and the BATTLE_NOT_FOUND code.
        using var factory = new ResultFactory();
        var playerId = await factory.NewPlayerAsync();

        var response = await factory.GetResultAsync(
            factory.CreateClient(),
            "battle-that-does-not-exist",
            TestApplicationSession.Mint(playerId));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("BATTLE_NOT_FOUND", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Result_ForAnotherPlayersBattle_ShouldReturnTheSameBattleNotFound()
    {
        // API_CONTRACTS.md §4 note 7: "A caller requesting a battle they do not own
        // receives the same 404 BATTLE_NOT_FOUND as a battle that does not exist, so
        // the endpoint never discloses the existence of another Player's battle."
        using var factory = new ResultFactory();

        var owner = await factory.NewPlayerAsync();
        var stranger = await factory.NewPlayerAsync();

        var outcome = await factory.PlayToTerminalOutcomeAsync(owner);
        Assert.True(outcome.Written, "the terminal resolution must have persisted a result");

        var client = factory.CreateClient();

        // The owner reads it…
        var ownerResponse = await factory.GetResultAsync(
            client,
            outcome.BattleId,
            TestApplicationSession.Mint(owner));

        Assert.Equal(HttpStatusCode.OK, ownerResponse.StatusCode);

        // …and the stranger gets the missing-row answer, not a 403 or a 200.
        var strangerResponse = await factory.GetResultAsync(
            client,
            outcome.BattleId,
            TestApplicationSession.Mint(stranger));

        Assert.Equal(HttpStatusCode.NotFound, strangerResponse.StatusCode);

        var body = await strangerResponse.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("BATTLE_NOT_FOUND", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Result_ShouldIgnoreAClientSuppliedPlayerId()
    {
        // API_CONTRACTS.md §4 note 7: "Ownership is never established from
        // client-supplied input: no playerId request member, query parameter,
        // header, or body field may select, override, or stand in for the caller's
        // identity". A stranger who names the owner cannot read the owner's result.
        using var factory = new ResultFactory();

        var owner = await factory.NewPlayerAsync();
        var stranger = await factory.NewPlayerAsync();

        var outcome = await factory.PlayToTerminalOutcomeAsync(owner);

        var client = factory.CreateClient();

        var message = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/battle/{outcome.BattleId}/result?playerId={owner}");

        // Every request-supplied spelling of an identity that note 7 forbids.
        message.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestApplicationSession.Mint(stranger));
        message.Headers.Add("playerId", owner);
        message.Headers.Add("X-Player-Id", owner);

        var response = await client.SendAsync(message);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("BATTLE_NOT_FOUND", body.GetProperty("error").GetString());
    }

    // -----------------------------------------------------------------------
    // 200 — the documented §4 response shape
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Result_ForTheOwner_ShouldReturnExactlyTheDocumentedShape()
    {
        // API_CONTRACTS.md §4: the response carries exactly battleId, outcome,
        // rewards, and durationTurns — no persistence value such as PlayerId,
        // PetInstanceId, BossDefinitionId, or CompletedAt, and no internal state
        // (no Redis key, no Sequence, no RngSeed/RngState).
        using var factory = new ResultFactory();

        var owner = await factory.NewPlayerAsync();

        var outcome = await factory.PlayToTerminalOutcomeAsync(owner);
        Assert.True(outcome.Written);

        var response = await factory.GetResultAsync(
            factory.CreateClient(),
            outcome.BattleId,
            TestApplicationSession.Mint(owner));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        var members = body.EnumerateObject()
            .Select(member => member.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[] { "battleId", "durationTurns", "outcome", "rewards" },
            members);

        // §4 note 2: battleId is the result row's primary key — the battle's own id.
        Assert.Equal(outcome.BattleId, body.GetProperty("battleId").GetString());

        // §4 note 4 / GAME_EVENTS.md §2: exactly one of the two documented values.
        var resultOutcome = body.GetProperty("outcome").GetString();

        Assert.Contains(resultOutcome, new[] { "victory", "defeat" });

        // §4 note 1: rewards is always present, and its value is the stored
        // RewardSummary exactly as DATABASE.md §1 documents it. TASK-068 resolved
        // the defeat shape as Option A (DATABASE.md §1 item 5), so once the
        // implementation task lands the member set applies to BOTH outcomes — this
        // is not the empty object, and its members are the two tracks' documented
        // ones.
        var rewards = body.GetProperty("rewards");

        Assert.Equal(JsonValueKind.Object, rewards.ValueKind);
        Assert.NotEmpty(rewards.EnumerateObject());

        Assert.Equal(
            new[]
            {
                "newPetLevel", "newPetXp", "newPlayerLevel", "newPlayerXp",
                "petLeveledUp", "petXpGained", "playerLeveledUp", "playerXpGained",
            }.OrderBy(name => name, StringComparer.Ordinal),
            rewards.EnumerateObject()
                .Select(member => member.Name)
                .OrderBy(name => name, StringComparer.Ordinal));

        // §4 note 5 / DATABASE.md §1 item 1: durationTurns is the terminal Turn.
        Assert.Equal(outcome.DurationTurns, body.GetProperty("durationTurns").GetInt32());
    }

    [Fact]
    public async Task Result_ForTheOwner_ShouldNotExposeAnyPersistenceOrInternalValue()
    {
        // §4's shape is a representation of the row, not the row: the persisted
        // identities and the completion instant stay internal, and no Redis key,
        // Sequence, RNG value, or authentication internal is exposed.
        using var factory = new ResultFactory();

        var owner = await factory.NewPlayerAsync();

        var outcome = await factory.PlayToTerminalOutcomeAsync(owner);

        var response = await factory.GetResultAsync(
            factory.CreateClient(),
            outcome.BattleId,
            TestApplicationSession.Mint(owner));

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        // Asserted structurally, on the payload's member names, rather than by
        // searching the raw text: a battle's own id is an opaque server-authored
        // string, so a substring search could match inside a legitimate value.
        var members = body.EnumerateObject()
            .Select(member => member.Name)
            .ToArray();

        foreach (var forbidden in new[]
                 {
                     "playerId", "petInstanceId", "bossDefinitionId", "completedAt",
                     "sequence", "rngSeed", "rngState", "rewardSummary",
                     "key", "ttl", "expire", "lock", "status",
                 })
        {
            Assert.DoesNotContain(forbidden, members);
        }

        // The stored row really does carry the internal values, so this proves the
        // endpoint omits them rather than that they do not exist.
        var storedRow = await factory.GetStoredRowAsync(outcome.BattleId);

        Assert.NotNull(storedRow);
        Assert.Equal(owner, storedRow!.PlayerId);
      }

    [Fact]
    public async Task Result_ForATerminalVictory_ShouldReportTheVictoryOutcomeAndTheTerminalTurn()
    {
        // GAME_EVENTS.md §2 / DATABASE.md §1: the outcome is the resolution's own
        // terminal report and the duration is the authoritative Turn at that
        // resolution — read from the stored row, not recomputed by the endpoint.
        using var factory = new ResultFactory();

        var owner = await factory.NewPlayerAsync();

        var outcome = await factory.PlayToTerminalOutcomeAsync(owner, BattleOutcome.Victory);

        Assert.True(outcome.Written);
        Assert.Equal(BattleOutcome.Victory, outcome.Outcome);

        var response = await factory.GetResultAsync(
            factory.CreateClient(),
            outcome.BattleId,
            TestApplicationSession.Mint(owner));

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("victory", body.GetProperty("outcome").GetString());
        Assert.Equal(outcome.DurationTurns, body.GetProperty("durationTurns").GetInt32());
    }

    [Fact]
    public async Task Result_ForATerminalDefeat_ShouldReportTheDefeatOutcome()
    {
        // The other documented value, with rewards present and populated for BOTH
        // outcomes per §4 note 1 and DATABASE.md §1 item 5 (the TASK-068 Option A
        // resolution): a defeat grants +0 on both tracks, so those members hold
        // their documented 0 / unchanged values rather than the object being empty.
        using var factory = new ResultFactory();

        var owner = await factory.NewPlayerAsync();

        var outcome = await factory.PlayToTerminalOutcomeAsync(owner, BattleOutcome.Defeat);

        Assert.True(outcome.Written);
        Assert.Equal(BattleOutcome.Defeat, outcome.Outcome);

        var response = await factory.GetResultAsync(
            factory.CreateClient(),
            outcome.BattleId,
            TestApplicationSession.Mint(owner));

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("defeat", body.GetProperty("outcome").GetString());

        var rewards = body.GetProperty("rewards");

        Assert.Equal(JsonValueKind.Object, rewards.ValueKind);
        Assert.NotEmpty(rewards.EnumerateObject());

        // DATABASE.md §1 item 5: playerXpGained = 0 on a defeat, playerLeveledUp =
        // false, and newPlayerXp / newPlayerLevel unchanged — the SAME four-player
        // member set as victory, not {}.
        Assert.Equal(0, rewards.GetProperty("playerXpGained").GetInt32());
        Assert.False(rewards.GetProperty("playerLeveledUp").GetBoolean());

        // PET_RULES.md §5.3 item 2: the active combat Pet's +0, with the same
        // member shape on the Pet track.
        Assert.Equal(0, rewards.GetProperty("petXpGained").GetInt32());
        Assert.False(rewards.GetProperty("petLeveledUp").GetBoolean());
    }

    [Fact]
    public async Task Result_ForAnActiveBattle_ShouldReturnBattleNotFound()
    {
        // API_CONTRACTS.md §4: "Only returns data for a battle that has already
        // ended ... While a battle is active, its state is only available via the
        // SignalR connection, not this endpoint." An unresolved battle has no row,
        // so it answers the documented 404.
        using var factory = new ResultFactory();

        var owner = await factory.NewPlayerAsync();

        var active = await factory.CreateActiveBattleAsync(owner);

        var response = await factory.GetResultAsync(
            factory.CreateClient(),
            active.BattleId,
            TestApplicationSession.Mint(owner));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("BATTLE_NOT_FOUND", body.GetProperty("error").GetString());
    }

    // -----------------------------------------------------------------------
    // End to end over the real pipeline
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Result_AfterACommittedTerminalSwap_ShouldServeThePersistedRow()
    {
        // The documented chain in one test: a real battle is created, a real
        // committed Swap ends it, the durable row is written, the active state is
        // cleared (REDIS_STATE.md §3), and the endpoint serves that row to its
        // authenticated owner.
        using var factory = new ResultFactory();

        var owner = await factory.NewPlayerAsync();

        var outcome = await factory.PlayToTerminalOutcomeAsync(owner);

        Assert.True(outcome.Written);
        Assert.Equal(1, outcome.DeleteCount);

        // The active state was cleared, so a further action against that battle
        // resolves nothing — the documented post-battle position.
        Assert.Null(await factory.GetStoredStateAsync(outcome.BattleId));

        var response = await factory.GetResultAsync(
            factory.CreateClient(),
            outcome.BattleId,
            TestApplicationSession.Mint(owner));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(outcome.BattleId, body.GetProperty("battleId").GetString());
        Assert.Equal("victory", body.GetProperty("outcome").GetString());
        Assert.Equal(outcome.DurationTurns, body.GetProperty("durationTurns").GetInt32());
    }

    // -----------------------------------------------------------------------
    // Test host
    // -----------------------------------------------------------------------

    /// <summary>
    /// A host exposing the real endpoint, the real persistence composition, and
    /// the real application-session pipeline over isolated stores — so the whole
    /// documented read path is exercised without a live PostgreSQL or Redis.
    ///
    /// The two battle-end lookup boundaries are served by real repositories over
    /// the isolated <c>GameDbContext</c>; the canonical <c>BossDefinition</c> rows
    /// are seeded so the foreign key the result row needs is satisfiable, exactly
    /// as the provisioned rows make it in a real environment (TASK-053).
    /// </summary>
    private sealed class ResultFactory : IDisposable
    {
        private readonly WebApplicationFactory<Program> _host;

        public ResultFactory()
        {
            _host = new ResultHost();
        }

        public IServiceProvider Services => _host.Services;

        public HttpClient CreateClient() => _host.CreateClient();

        public string NewPlayerId() => $"player_result_ep_{Guid.NewGuid():N}";

        /// <summary>
        /// Creates the Player row the identity names, so the result row's
        /// <c>PlayerId</c> foreign key (<c>DATABASE.md</c> §1, §2) can be
        /// satisfied. Without the row the insert cannot complete, no result
        /// exists, and the endpoint's documented <c>404</c> is the correct
        /// answer — which is the documented behaviour, not a fixture shortcut.
        ///
        /// The battle-end path consults no Player-side eligibility condition:
        /// <c>DATABASE.md</c> §1 states that <c>BattleResult</c> persistence
        /// requires no Player combat-readiness condition.
        /// </summary>
        public async Task<string> NewPlayerAsync()
        {
            var playerId = NewPlayerId();

            using var scope = Services.CreateScope();

            var context = scope.ServiceProvider
                .GetRequiredService<GameServer.Infrastructure.Postgres.GameDbContext>();

            context.Players.Add(new Player
            {
                PlayerId = playerId,
                AccountId = Guid.NewGuid(),
                Level = Player.InitialLevel,
                CreatedAt = DateTimeOffset.UtcNow,
            });

            // The identifiers are derived from the Player, so two battles in one
            // test class never share a Pet instance: each battle's result row
            // references its own owner's Pet, exactly as DATABASE.md §1/§2 require.
            var petDefinitionId = $"pet_def_{playerId}";

            // The owned Pet instance the battle's PetState will name, so the
            // BattleResult row's Pet foreign key is satisfiable (DATABASE.md §1, §2).
            context.PetDefinitions.Add(new PetDefinition
            {
                PetDefinitionId = petDefinitionId,
                Identity = "Thanh Xà",
                Element = Element.Moc,
                PassiveId = new PassiveId("thanh-xa-regen"),
                PassiveThreshold = 5,
                SignatureSkillCardId = $"card_skill_{playerId}",
            });

            context.Pets.Add(new Pet
            {
                PetInstanceId = PetInstanceIdFor(playerId),
                PlayerId = playerId,
                PetDefinitionId = petDefinitionId,
                Tier = PetTier.Common,
                Star = 1,
                Level = 1,
                AcquiredAt = DateTimeOffset.UtcNow,
            });

            await context.SaveChangesAsync();

            return playerId;
        }

        /// <summary>
        /// The owned Pet instance identity a Player's battle is fought with — the
        /// same derivation <see cref="NewPlayerAsync"/> seeds.
        /// </summary>
        public static string PetInstanceIdFor(string playerId) => $"pet_instance_{playerId}";

        public Task<HttpResponseMessage> GetResultAsync(
            HttpClient client,
            string battleId,
            string? session)
        {
            var message = new HttpRequestMessage(HttpMethod.Get, $"/api/battle/{battleId}/result");

            if (session is not null)
            {
                message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session);
            }

            return client.SendAsync(message);
        }

        /// <summary>
        /// Creates a battle for the Player and resolves it to the requested
        /// terminal outcome through the real Domain pipeline, then reports what the
        /// documented battle-end step did.
        /// </summary>
        public async Task<TerminalOutcome> PlayToTerminalOutcomeAsync(
            string playerId,
            BattleOutcome expected = BattleOutcome.Victory)
        {
            using var scope = Services.CreateScope();

            var battles = scope.ServiceProvider.GetRequiredService<BattleStateService>();
            var results = scope.ServiceProvider.GetRequiredService<IBattleResultRepository>();

            // The fixture Boss dies to one committed Swap (Victory) or kills the
            // player in one Basic Attack (Defeat), with every other value left at
            // the MVP configuration (BOSS_RULES.md §6.1/§6.3–§6.4).
            var boss = expected == BattleOutcome.Victory
                ? BossDefinitions.HoaLong with { MaxHP = 1 }
                : BossDefinitions.HoaLong with
                {
                    ATK = 100_000,
                    SkillDefinition = BossDefinitions.HoaLong.SkillDefinition
                        with { ChargeRequirement = int.MaxValue },
                    PassiveDefinition = BossDefinitions.HoaLong.PassiveDefinition
                        with { Threshold = null },
                };

            var battleId = $"result-ep-battle-{Guid.NewGuid():N}";

            var created = await battles.CreateBattleAsync(
                battleId,
                new PlayerId(playerId),
                new BattleStateService.PetConfiguration(
                    new PetId(ResultFactory.PetInstanceIdFor(playerId)),
                    Element.Hoa,
                    new PassiveId("xich-lang"),
                    PassiveThreshold: 5),
                boss);

            var pair = FindMatchProducingPair(created.BoardState);
            var result = await battles.ExecuteSwapAsync(battleId, pair);

            Assert.NotNull(result);
            Assert.True(result!.Value.IsAccepted);

            var terminalEvent = expected == BattleOutcome.Victory
                ? BattleEventType.BattleWon
                : BattleEventType.BattleLost;

            Assert.Contains(result.Value.Events, e => e.Type == terminalEvent);

            var row = await results.GetByIdAsync(battleId);
            var store = (ApiTestBattleStateRepository)scope.ServiceProvider
                .GetRequiredService<IBattleStateRepository>();

            Assert.True(
                row is not null,
                $"""
                DATABASE.md §1: the terminal resolution must persist a BattleResult.
                battleId={battleId}
                expected={expected}
                turn={result.Value.State.Turn}
                bossHp={result.Value.State.BossState.HP}
                petHp={result.Value.State.PetState.HP}
                petId={result.Value.State.PetState.PetId.Value}
                deleteCount={store.DeleteCount}
                unpersisted={battles.UnpersistedResultFailure(battleId)}
                """);

            return new TerminalOutcome(
                battleId,
                row is not null,
                row?.Outcome ?? result.Value.Events
                    .Select(e => e.Type == BattleEventType.BattleWon
                        ? BattleOutcome.Victory
                        : BattleOutcome.Defeat)
                    .First(),
                row?.DurationTurns ?? result.Value.State.Turn,
                store.DeleteCount);
        }

        /// <summary>
        /// Creates a battle and leaves it un-resolved, so the documented
        /// active-battle read path can be exercised.
        /// </summary>
        public async Task<ActiveBattle> CreateActiveBattleAsync(string playerId)
        {
            using var scope = Services.CreateScope();

            var battles = scope.ServiceProvider.GetRequiredService<BattleStateService>();

            var battleId = $"result-ep-active-{Guid.NewGuid():N}";

            await battles.CreateBattleAsync(
                battleId,
                new PlayerId(playerId),
                new BattleStateService.PetConfiguration(
                    new PetId(ResultFactory.PetInstanceIdFor(playerId)),
                    Element.Hoa,
                    new PassiveId("xich-lang"),
                    PassiveThreshold: 5),
                BossDefinitions.HoaLong);

            return new ActiveBattle(battleId);
        }

        public async Task<BattleState?> GetStoredStateAsync(string battleId)
        {
            using var scope = Services.CreateScope();

            return await scope.ServiceProvider
                .GetRequiredService<IBattleStateRepository>()
                .GetAsync(battleId);
        }

        /// <summary>
        /// The durable result row for a battle, or <c>null</c> when none was
        /// written — read from the store the runtime wrote it to.
        /// </summary>
        public async Task<BattleResult?> GetStoredRowAsync(string battleId)
        {
            using var scope = Services.CreateScope();

            return await scope.ServiceProvider
                .GetRequiredService<IBattleResultRepository>()
                .GetByIdAsync(battleId);
        }

        public void Dispose() => _host.Dispose();

        /// <summary>What the documented battle-end step did for a resolved battle.</summary>
        public sealed record TerminalOutcome(
            string BattleId,
            bool Written,
            BattleOutcome Outcome,
            int DurationTurns,
            int DeleteCount);

        /// <summary>An un-resolved battle, which therefore has no result row.</summary>
        public sealed record ActiveBattle(string BattleId);

        private sealed class ResultHost : WebApplicationFactory<Program>
        {
            private readonly string _storeName = $"battle-result-ep-{Guid.NewGuid():N}";
            private readonly Task _seeded;

            public ResultHost()
            {
                // The canonical BossDefinition rows the result row's foreign key
                // needs (DATABASE.md §1 note item 5, TASK-053). In a real
                // environment they are provisioned by migration; here the isolated
                // store is seeded with the same canonical values.
                _seeded = SeedAsync();
            }

            public async Task SeedAsync()
            {
                using var scope = Services.CreateScope();

                var context = scope.ServiceProvider
                    .GetRequiredService<GameServer.Infrastructure.Postgres.GameDbContext>();

                foreach (var definition in BossDefinitions.All)
                {
                    context.BossDefinitions.Add(definition);
                }

                await context.SaveChangesAsync();
            }

            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                // Blank both connection strings so the production composition
                // registers neither the Npgsql provider nor the Redis store; this
                // host supplies the isolated in-memory pair instead — the
                // established API test-host pattern.
                builder.UseSetting("ConnectionStrings:DefaultConnection", "");
                builder.UseSetting("ConnectionStrings:Redis", "");

                // The application session's signing key, from configuration as
                // ADR-015 D10 requires, so these tests present real JWTs.
                foreach (var (key, value) in TestApplicationSession.CurrentKeyConfiguration)
                {
                    builder.UseSetting(key, value);
                }

                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<DbContextOptions<GameServer.Infrastructure.Postgres.GameDbContext>>();
                    services.RemoveAll<GameServer.Infrastructure.Postgres.GameDbContext>();
                    services.AddDbContext<GameServer.Infrastructure.Postgres.GameDbContext>(options =>
                        options
                            .UseInMemoryDatabase(_storeName)
                            // The battle-end step writes the result row and both
                            // progression tracks inside one database transaction
                            // (DATABASE.md §1's battle-end atomicity contract), and
                            // the in-memory provider this host substitutes for
                            // PostgreSQL supports no transactions at all — it reports
                            // TransactionIgnoredWarning, which is an error by default.
                            // The substitution therefore declares that fact here rather
                            // than failing on it. Nothing about atomicity is verified
                            // by this host either way: that is the PostgreSQL
                            // integration suite's
                            // (BattleEndAtomicityPostgresTests), because only a real
                            // database can roll a transaction back. What this host
                            // verifies is the documented read path over the row the
                            // battle-end step wrote.
                            .ConfigureWarnings(warnings =>
                                warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning)));

                    services.AddSingleton<IBattleStateRepository, ApiTestBattleStateRepository>();
                });
            }
        }
    }

    // -----------------------------------------------------------------------
    // Fixtures and helpers
    // -----------------------------------------------------------------------

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
