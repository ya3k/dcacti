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
/// <c>GET /api/battle/history</c> — <c>API_CONTRACTS.md</c> §4.5 (TASK-165).
///
/// <code>
/// Authorization: Bearer &lt;sessionToken&gt;
///         ↓
/// GET /api/battle/history
///         ↓
/// authenticated PlayerId          (the player_id claim — API_CONTRACTS.md §2.8)
///         ↓
/// player-scoped ordered read      (§4.5 notes 4, 5, 8)
///         ↓
/// [ { battleId, outcome, rewards, durationTurns, completedAt } ]
/// </code>
///
/// <b>What these tests establish.</b> §4.5's documented responses and every
/// invariant behind them: the bare array with no envelope (note 1), the five
/// element members with the complete 8-member <c>RewardSummary</c> and no Boss or
/// Pet identifier (notes 2, 3, 12), the explicit ordering (note 4), no pagination
/// or other request input (notes 5, 6), <c>401 UNAUTHENTICATED</c> for an
/// unauthenticated caller (note 7), the server-derived Player scope (note 8),
/// <c>200 []</c> for an empty history (note 9), and the exclusion of active
/// battles (note 10).
///
/// Ownership is exercised against the real authentication pipeline with real
/// issued sessions, and the ordering against real <c>BattleResult</c> rows written
/// by the real battle-end path — so neither is taken on the endpoint's word.
/// </summary>
public class BattleHistoryEndpointTests
{
    // -----------------------------------------------------------------------
    // 401 — API_CONTRACTS.md §1, §2.8, §4.5 note 7
    // -----------------------------------------------------------------------

    [Fact]
    public async Task History_WithoutASession_ShouldReturnUnauthenticated()
    {
        // API_CONTRACTS.md §4.5 note 7: a caller presenting no authenticated
        // session receives 401 with the §6 envelope and UNAUTHENTICATED — "exactly
        // as §4 note 6 establishes, and never a 404".
        using var factory = new HistoryFactory();

        var response = await factory.GetHistoryAsync(factory.CreateClient(), session: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("UNAUTHENTICATED", body.GetProperty("error").GetString());
    }

    [Theory]
    [InlineData("not-a-jwt")]
    [InlineData("header.payload.signature")]
    public async Task History_WithAnInvalidSession_ShouldReturnUnauthenticated(string token)
    {
        // §2.8 "Failure behavior" / §4.5 note 7: a missing, invalid/tampered, or
        // expired session all resolve to this same response, with no distinct code
        // and no token-validation detail disclosed.
        using var factory = new HistoryFactory();

        var response = await factory.GetHistoryAsync(factory.CreateClient(), token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("UNAUTHENTICATED", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task History_WithATamperedSession_ShouldReturnUnauthenticated()
    {
        // A token signed with a key outside the configured validation set
        // (ADR-015 D11) is refused with the same single code.
        using var factory = new HistoryFactory();

        var response = await factory.GetHistoryAsync(
            factory.CreateClient(),
            TestApplicationSession.MintWithUnknownKey("player_anyone"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("UNAUTHENTICATED", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task History_WithAnExpiredSession_ShouldReturnUnauthenticated()
    {
        // ADR-015 D5: 24h absolute expiry. A session past it is refused with the
        // same single code — no separate "expired" outcome is disclosed.
        using var factory = new HistoryFactory();

        var expired = TestApplicationSession.Mint(
            playerId: "player_expired",
            notBefore: DateTime.UtcNow.AddHours(-48),
            expires: DateTime.UtcNow.AddHours(-24));

        var response = await factory.GetHistoryAsync(factory.CreateClient(), expired);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("UNAUTHENTICATED", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task History_WithASessionCarryingNoPlayerIdentity_ShouldReturnUnauthenticated()
    {
        // A token that validates but carries no player_id identifies nobody, so it
        // is not an authenticated caller — and §4.5 note 8 forbids any
        // request-supplied substitute for the identity.
        using var factory = new HistoryFactory();

        var response = await factory.GetHistoryAsync(
            factory.CreateClient(),
            TestApplicationSession.Mint(playerId: null));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("UNAUTHENTICATED", body.GetProperty("error").GetString());
    }

    // -----------------------------------------------------------------------
    // 200 [] — API_CONTRACTS.md §4.5 note 9
    // -----------------------------------------------------------------------

    [Fact]
    public async Task History_WithNoCompletedBattles_ShouldReturnAnEmptyArray()
    {
        // API_CONTRACTS.md §4.5 note 9: "A player with no completed battles receives
        // 200 and [] — not 404, not 204, and not an error." The array is the body,
        // so this asserts both the status and that the body really is JSON [].
        using var factory = new HistoryFactory();
        var playerId = await factory.NewPlayerAsync();

        var response = await factory.GetHistoryAsync(
            factory.CreateClient(),
            TestApplicationSession.Mint(playerId));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(JsonValueKind.Array, body.ValueKind);
        Assert.Empty(body.EnumerateArray());
    }

    [Fact]
    public async Task History_ForAPlayerWithNoResults_ShouldNotDiscloseAnotherPlayersHistory()
    {
        // §4.5 note 8: "Because the scope is fixed by the session, the endpoint
        // discloses nothing about whether another Player has any history." A Player
        // with results therefore does not make another Player's array non-empty.
        using var factory = new HistoryFactory();

        var stranger = await factory.NewPlayerAsync();
        var owner = await factory.NewPlayerAsync();

        var outcome = await factory.PlayToTerminalOutcomeAsync(owner);

        Assert.True(outcome.Written, "the terminal resolution must have persisted a result");

        var response = await factory.GetHistoryAsync(
            factory.CreateClient(),
            TestApplicationSession.Mint(stranger));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Empty(body.EnumerateArray());
    }

    // -----------------------------------------------------------------------
    // 200 — the documented §4.5 response shape
    // -----------------------------------------------------------------------

    [Fact]
    public async Task History_ShouldBeABareArray_WithNoWrapperOrMetadata()
    {
        // API_CONTRACTS.md §4.5 note 1: "The response is a bare JSON array — never
        // a wrapper object. No { "battles": [...] } envelope, no total, no
        // nextCursor, and no other metadata member exists. The array IS the
        // response body."
        using var factory = new HistoryFactory();

        var owner = await factory.NewPlayerAsync();

        var outcome = await factory.PlayToTerminalOutcomeAsync(owner);

        Assert.True(outcome.Written);

        var response = await factory.GetHistoryAsync(
            factory.CreateClient(),
            TestApplicationSession.Mint(owner));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        // The root value is an array, not an object — so no envelope can be hiding
        // one, and no metadata member exists to be read.
        Assert.Equal(JsonValueKind.Array, body.ValueKind);
        Assert.NotEqual(JsonValueKind.Object, body.ValueKind);

        // And the array element is the element itself, not a nested wrapper.
        var element = Assert.Single(body.EnumerateArray());

        Assert.Equal(JsonValueKind.Object, element.ValueKind);
        Assert.Equal(outcome.BattleId, element.GetProperty("battleId").GetString());
    }

    [Fact]
    public async Task History_Element_ShouldCarryExactlyTheFiveDocumentedMembers()
    {
        // API_CONTRACTS.md §4.5 note 12: "The element member set is exactly the five
        // members in the response block above and no more." Note 2 makes the first
        // four the §4 result members and note 3 adds completedAt.
        using var factory = new HistoryFactory();

        var owner = await factory.NewPlayerAsync();

        var outcome = await factory.PlayToTerminalOutcomeAsync(owner);

        var response = await factory.GetHistoryAsync(
            factory.CreateClient(),
            TestApplicationSession.Mint(owner));

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        var element = Assert.Single(body.EnumerateArray());

        var members = element.EnumerateObject()
            .Select(member => member.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[] { "battleId", "completedAt", "durationTurns", "outcome", "rewards" },
            members);

        // §4.5 note 2 / §4 note 2: battleId is the result row's primary key — the
        // battle's own id.
        Assert.Equal(outcome.BattleId, element.GetProperty("battleId").GetString());

        // §4.5 note 2 / §4 note 4: exactly one of the two documented outcome values.
        var resultOutcome = element.GetProperty("outcome").GetString();

        Assert.Contains(resultOutcome, new[] { "victory", "defeat" });

        // §4.5 note 2 / §4 note 5: durationTurns is the terminal Turn.
        Assert.Equal(outcome.DurationTurns, element.GetProperty("durationTurns").GetInt32());

        // §4.5 note 3: completedAt is the row's own persisted server instant.
        Assert.Equal(
            JsonValueKind.String,
            element.GetProperty("completedAt").ValueKind);
    }

    [Fact]
    public async Task History_ElementRewards_ShouldCarryAllEightRewardSummaryMembers()
    {
        // API_CONTRACTS.md §4.5 note 2: rewards "carries exactly the same meaning,
        // source, type, and value vocabulary as" the §4 response; §4 note 1 and
        // DATABASE.md §1 own the 8-member RewardSummary. All eight must be present —
        // the history element is not a reduced history summary.
        using var factory = new HistoryFactory();

        var owner = await factory.NewPlayerAsync();

        await factory.PlayToTerminalOutcomeAsync(owner);

        var response = await factory.GetHistoryAsync(
            factory.CreateClient(),
            TestApplicationSession.Mint(owner));

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        var element = Assert.Single(body.EnumerateArray());

        var rewards = element.GetProperty("rewards");

        Assert.Equal(JsonValueKind.Object, rewards.ValueKind);

        Assert.Equal(
            new[]
            {
                "newPetLevel", "newPetXp", "newPlayerLevel", "newPlayerXp",
                "petLeveledUp", "petXpGained", "playerLeveledUp", "playerXpGained",
            }.OrderBy(name => name, StringComparer.Ordinal),
            rewards.EnumerateObject()
                .Select(member => member.Name)
                .OrderBy(name => name, StringComparer.Ordinal));

        // DATABASE.md §1 item 5: the 8-member set applies to both outcomes, and a
        // victory grants +100 on each track — so these are real values, not nulls.
        Assert.Equal(100, rewards.GetProperty("playerXpGained").GetInt32());
        Assert.Equal(100, rewards.GetProperty("petXpGained").GetInt32());
    }

    [Fact]
    public async Task History_Element_ShouldExposeNoBossOrPetIdentifier()
    {
        // API_CONTRACTS.md §4.5 note 12: "Neither bossDefinitionId/bossId nor
        // petInstanceId/petId is a member of a history element, even though
        // BattleResult persists both." Asserted structurally on the member names,
        // and cross-checked against the stored row so this proves the endpoint
        // omits them rather than that they do not exist.
        using var factory = new HistoryFactory();

        var owner = await factory.NewPlayerAsync();

        var outcome = await factory.PlayToTerminalOutcomeAsync(owner);

        var response = await factory.GetHistoryAsync(
            factory.CreateClient(),
            TestApplicationSession.Mint(owner));

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        var element = Assert.Single(body.EnumerateArray());

        var members = element.EnumerateObject()
            .Select(member => member.Name)
            .ToArray();

        foreach (var forbidden in new[]
                 {
                     "bossId", "bossDefinitionId", "petId", "petInstanceId",
                     "playerId", "rewardSummary", "sequence", "rngSeed", "rngState",
                     "status",
                 })
        {
            Assert.DoesNotContain(forbidden, members);
        }

        // The stored row really does carry the identities, so their absence above is
        // the endpoint's projection and not an empty fixture.
        var storedRow = await factory.GetStoredRowAsync(outcome.BattleId);

        Assert.NotNull(storedRow);
        Assert.Equal(owner, storedRow!.PlayerId);
        Assert.False(string.IsNullOrWhiteSpace(storedRow.PetInstanceId));
        Assert.False(string.IsNullOrWhiteSpace(storedRow.BossDefinitionId));
    }

    // -----------------------------------------------------------------------
    // Ownership — API_CONTRACTS.md §4.5 note 8
    // -----------------------------------------------------------------------

    [Fact]
    public async Task History_ShouldReturnOnlyTheAuthenticatedPlayersResults()
    {
        // API_CONTRACTS.md §4.5 note 8: "The caller reads only their own
        // BattleResult rows." Two Players each fight, and each caller's array
        // contains exactly their own battle — Player A must never receive Player B's
        // records.
        using var factory = new HistoryFactory();

        var playerA = await factory.NewPlayerAsync();
        var playerB = await factory.NewPlayerAsync();

        var battleA = await factory.PlayToTerminalOutcomeAsync(playerA);
        var battleB = await factory.PlayToTerminalOutcomeAsync(playerB);

        Assert.True(battleA.Written);
        Assert.True(battleB.Written);

        var client = factory.CreateClient();

        var historyA = await factory.GetHistoryAsync(
            client,
            TestApplicationSession.Mint(playerA));

        var elementA = Assert.Single(
            (await historyA.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray());

        Assert.Equal(battleA.BattleId, elementA.GetProperty("battleId").GetString());

        var historyB = await factory.GetHistoryAsync(
            client,
            TestApplicationSession.Mint(playerB));

        var elementB = Assert.Single(
            (await historyB.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray());

        Assert.Equal(battleB.BattleId, elementB.GetProperty("battleId").GetString());

        // Neither array contains the other Player's battle id at all.
        Assert.NotEqual(
            battleB.BattleId,
            elementA.GetProperty("battleId").GetString());

        Assert.NotEqual(
            battleA.BattleId,
            elementB.GetProperty("battleId").GetString());
    }

    [Fact]
    public async Task History_ShouldIgnoreAClientSuppliedPlayerId()
    {
        // API_CONTRACTS.md §4.5 note 8: "PlayerId is never client-supplied: no
        // playerId request member, query parameter, header, or body field may
        // select, override, or stand in for the caller's identity, and there is no
        // route parameter for ownership." A stranger who names the owner — by query
        // parameter, by header, and by path — still receives only their own (empty)
        // history, never the owner's.
        using var factory = new HistoryFactory();

        var owner = await factory.NewPlayerAsync();
        var stranger = await factory.NewPlayerAsync();

        var outcome = await factory.PlayToTerminalOutcomeAsync(owner);

        Assert.True(outcome.Written);

        var client = factory.CreateClient();

        var message = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/battle/history?playerId={owner}");

        // Every request-supplied spelling of an identity that note 8 forbids.
        message.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestApplicationSession.Mint(stranger));
        message.Headers.Add("playerId", owner);
        message.Headers.Add("X-Player-Id", owner);

        var response = await client.SendAsync(message);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        // The owner's own history is not disclosed to the stranger.
        Assert.Empty(body.EnumerateArray());
    }

    [Fact]
    public async Task History_ShouldNotAcceptAPlayerIdRouteSegment()
    {
        // API_CONTRACTS.md §4.5 note 8: "there is no route parameter for ownership —
        // reading another Player's history is not expressible in this contract". A
        // /history/{playerId} path therefore routes to no action at all rather than
        // to an ownership selector.
        using var factory = new HistoryFactory();

        var owner = await factory.NewPlayerAsync();

        var client = factory.CreateClient();

        var message = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/battle/history/{owner}");

        message.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestApplicationSession.Mint(owner));

        var response = await client.SendAsync(message);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // -----------------------------------------------------------------------
    // Ordering — API_CONTRACTS.md §4.5 note 4
    // -----------------------------------------------------------------------

    [Fact]
    public async Task History_ShouldOrderNewestCompletedBattleFirst()
    {
        // API_CONTRACTS.md §4.5 note 4: "Elements are ordered by CompletedAt
        // descending (newest completed battle first) ... Clients MAY rely on this
        // ordering." Three real battles are resolved in sequence, so their
        // CompletedAt values differ, and the array must be newest-first.
        using var factory = new HistoryFactory();

        var owner = await factory.NewPlayerAsync();

        var first = await factory.PlayToTerminalOutcomeAsync(owner);
        await factory.AdvanceCompletedAtAsync(first.BattleId);

        var second = await factory.PlayToTerminalOutcomeAsync(owner);
        await factory.AdvanceCompletedAtAsync(second.BattleId);

        var third = await factory.PlayToTerminalOutcomeAsync(owner);

        var response = await factory.GetHistoryAsync(
            factory.CreateClient(),
            TestApplicationSession.Mint(owner));

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        var battleIds = body.EnumerateArray()
            .Select(element => element.GetProperty("battleId").GetString())
            .ToArray();

        Assert.Equal(3, battleIds.Length);

        Assert.Equal(
            new[] { third.BattleId, second.BattleId, first.BattleId },
            battleIds);

        // And the completedAt values really are strictly decreasing, so the order
        // above is the documented one rather than an artifact of insertion order.
        var instants = body.EnumerateArray()
            .Select(element => element.GetProperty("completedAt").GetDateTimeOffset())
            .ToArray();

        Assert.True(
            instants[0] > instants[1] && instants[1] > instants[2],
            "the three results must carry distinct, decreasing completion instants");
    }

    [Fact]
    public async Task History_ShouldBreakCompletedAtTies_ByDescendingBattleResultId()
    {
        // API_CONTRACTS.md §4.5 note 4: "When two results share a CompletedAt, the
        // tie is broken by BattleResultId descending (the higher BattleResultId
        // first), so the total order is deterministic even though DATABASE.md §1
        // does not require CompletedAt to be unique." Two results are made to share
        // one instant so exactly that rule decides their order.
        using var factory = new HistoryFactory();

        var owner = await factory.NewPlayerAsync();

        var shared = new DateTimeOffset(2026, 9, 27, 10, 0, 0, TimeSpan.Zero);

        var lower = await factory.WriteResultAtAsync(owner, "history-tie-a-1", shared);
        var higher = await factory.WriteResultAtAsync(owner, "history-tie-a-2", shared);

        var response = await factory.GetHistoryAsync(
            factory.CreateClient(),
            TestApplicationSession.Mint(owner));

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        var elements = body.EnumerateArray().ToArray();

        Assert.Equal(2, elements.Length);

        // The higher BattleResultId comes first, and both carry the shared instant.
        Assert.Equal(
            higher.BattleId,
            elements[0].GetProperty("battleId").GetString());

        Assert.Equal(
            lower.BattleId,
            elements[1].GetProperty("battleId").GetString());

        Assert.All(
            elements,
            element => Assert.Equal(
                shared,
                element.GetProperty("completedAt").GetDateTimeOffset()));
    }

    // -----------------------------------------------------------------------
    // Active and failed battles — API_CONTRACTS.md §4.5 notes 10, 11
    // -----------------------------------------------------------------------

    [Fact]
    public async Task History_ShouldNotIncludeAnActiveBattle()
    {
        // API_CONTRACTS.md §4.5 note 10: "A still-active battle never appears. Only
        // durable BattleResult rows are returned, and such a row exists only after
        // terminal persistence." An unresolved battle therefore contributes no
        // element.
        using var factory = new HistoryFactory();

        var owner = await factory.NewPlayerAsync();

        var active = await factory.CreateActiveBattleAsync(owner);

        var response = await factory.GetHistoryAsync(
            factory.CreateClient(),
            TestApplicationSession.Mint(owner));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Empty(body.EnumerateArray());

        // The battle really is active — it exists in the active-state store — so its
        // absence above is the documented boundary and not a battle that never
        // existed.
        Assert.NotNull(await factory.GetStoredStateAsync(active.BattleId));
        Assert.Null(await factory.GetStoredRowAsync(active.BattleId));
    }

    [Fact]
    public async Task History_ShouldNotIncludeAFailedDurableWrite()
    {
        // API_CONTRACTS.md §4.5 note 11: "If the terminal BattleResult write does
        // not succeed, there is no history entry — no error member, no placeholder,
        // and no partial or pending entry. Absence is reported as absence." A
        // resolved battle whose BossDefinition does not resolve fails the write
        // closed (DATABASE.md §1 sourcing item 3), and no history element stands in
        // for it.
        using var factory = new HistoryFactory();

        var owner = await factory.NewPlayerAsync();

        var failed = await factory.PlayToFailedDurableWriteAsync(owner);

        Assert.False(failed.Written, "the battle-end write must have failed closed");
        Assert.Null(await factory.GetStoredRowAsync(failed.BattleId));

        var response = await factory.GetHistoryAsync(
            factory.CreateClient(),
            TestApplicationSession.Mint(owner));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Empty(body.EnumerateArray());
    }

    // -----------------------------------------------------------------------
    // End to end over the real pipeline
    // -----------------------------------------------------------------------

    [Fact]
    public async Task History_ShouldServeEveryPersistedTerminalResult()
    {
        // The documented chain in one test: two real battles are created and ended
        // by real committed Swaps, their durable rows are written, and the endpoint
        // serves both to the authenticated owner — newest first, each carrying the
        // stored row's own values.
        using var factory = new HistoryFactory();

        var owner = await factory.NewPlayerAsync();

        var older = await factory.PlayToTerminalOutcomeAsync(owner);
        await factory.AdvanceCompletedAtAsync(older.BattleId);

        var newer = await factory.PlayToTerminalOutcomeAsync(owner, BattleOutcome.Defeat);

        Assert.True(older.Written);
        Assert.True(newer.Written);

        var response = await factory.GetHistoryAsync(
            factory.CreateClient(),
            TestApplicationSession.Mint(owner));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var elements = (await response.Content.ReadFromJsonAsync<JsonElement>())
            .EnumerateArray()
            .ToArray();

        Assert.Equal(2, elements.Length);

        Assert.Equal(newer.BattleId, elements[0].GetProperty("battleId").GetString());
        Assert.Equal("defeat", elements[0].GetProperty("outcome").GetString());

        Assert.Equal(older.BattleId, elements[1].GetProperty("battleId").GetString());
        Assert.Equal("victory", elements[1].GetProperty("outcome").GetString());

        // DATABASE.md §1 item 5 / API_CONTRACTS.md §4 note 1: the defeat's rewards
        // are the same 8-member shape with the documented +0 values, not {}.
        var defeatRewards = elements[0].GetProperty("rewards");

        Assert.Equal(0, defeatRewards.GetProperty("playerXpGained").GetInt32());
        Assert.Equal(0, defeatRewards.GetProperty("petXpGained").GetInt32());
        Assert.False(defeatRewards.GetProperty("playerLeveledUp").GetBoolean());
        Assert.False(defeatRewards.GetProperty("petLeveledUp").GetBoolean());
    }

    // -----------------------------------------------------------------------
    // Test host
    // -----------------------------------------------------------------------

    /// <summary>
    /// A host exposing the real endpoint, the real persistence composition, and the
    /// real application-session pipeline over isolated stores — so the whole
    /// documented read path is exercised without a live PostgreSQL or Redis. The
    /// arrangement mirrors <c>BattleResultEndpointTests.ResultFactory</c> so the two
    /// endpoints of one shape are verified against the same real composition.
    /// </summary>
    private sealed class HistoryFactory : IDisposable
    {
        private readonly WebApplicationFactory<Program> _host;

        public HistoryFactory()
        {
            _host = new HistoryHost();
        }

        public IServiceProvider Services => _host.Services;

        public HttpClient CreateClient() => _host.CreateClient();

        /// <summary>
        /// Creates the Player row the identity names, with the owned Pet instance the
        /// battle will fight with — so the result row's foreign keys
        /// (<c>DATABASE.md</c> §1, §2) are satisfiable.
        /// </summary>
        public async Task<string> NewPlayerAsync()
        {
            var playerId = $"player_history_ep_{Guid.NewGuid():N}";

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

            var petDefinitionId = $"pet_def_{playerId}";

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

        public Task<HttpResponseMessage> GetHistoryAsync(HttpClient client, string? session)
        {
            var message = new HttpRequestMessage(HttpMethod.Get, "/api/battle/history");

            if (session is not null)
            {
                message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session);
            }

            return client.SendAsync(message);
        }

        /// <summary>
        /// Creates a battle for the Player and resolves it to the requested terminal
        /// outcome through the real Domain pipeline, then reports what the documented
        /// battle-end step did.
        /// </summary>
        public async Task<TerminalOutcome> PlayToTerminalOutcomeAsync(
            string playerId,
            BattleOutcome expected = BattleOutcome.Victory)
        {
            using var scope = Services.CreateScope();

            var battles = scope.ServiceProvider.GetRequiredService<BattleStateService>();
            var results = scope.ServiceProvider.GetRequiredService<IBattleResultRepository>();

            // The fixture Boss dies to one committed Swap (Victory) or kills the
            // player in one Basic Attack (Defeat), with every other value left at the
            // MVP configuration (BOSS_RULES.md §6.1/§6.3–§6.4).
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

            var battleId = $"history-ep-battle-{Guid.NewGuid():N}";

            var created = await battles.CreateBattleAsync(
                battleId,
                new PlayerId(playerId),
                new BattleStateService.PetConfiguration(
                    new PetId(PetInstanceIdFor(playerId)),
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

            Assert.True(
                row is not null,
                $"""
                DATABASE.md §1: the terminal resolution must persist a BattleResult.
                battleId={battleId}
                expected={expected}
                """);

            return new TerminalOutcome(
                battleId,
                row is not null,
                row?.Outcome ?? expected,
                row?.DurationTurns ?? result.Value.State.Turn);
        }

        /// <summary>
        /// Resolves a battle for an outcome whose durable write must fail closed,
        /// because the battle's Boss identity has no provisioned
        /// <c>BossDefinition</c> row (<c>DATABASE.md</c> §1 sourcing item 3). No
        /// result row exists afterwards, which is the state §4.5 note 11 describes.
        /// </summary>
        public async Task<TerminalOutcome> PlayToFailedDurableWriteAsync(string playerId)
        {
            using var scope = Services.CreateScope();

            var battles = scope.ServiceProvider.GetRequiredService<BattleStateService>();
            var results = scope.ServiceProvider.GetRequiredService<IBattleResultRepository>();

            // A Boss whose canonical Identity has no provisioned row: the battle-end
            // lookup resolves nothing, so the write fails closed and no row is
            // written. MaxHP 1 keeps it terminating on the first committed Swap.
            var unresolvable = BossDefinitions.HoaLong with
            {
                MaxHP = 1,
                BossId = new BossId("boss-identity-with-no-provisioned-row"),
            };

            var battleId = $"history-ep-unwritten-{Guid.NewGuid():N}";

            var created = await battles.CreateBattleAsync(
                battleId,
                new PlayerId(playerId),
                new BattleStateService.PetConfiguration(
                    new PetId(PetInstanceIdFor(playerId)),
                    Element.Hoa,
                    new PassiveId("xich-lang"),
                    PassiveThreshold: 5),
                unresolvable);

            var pair = FindMatchProducingPair(created.BoardState);
            var result = await battles.ExecuteSwapAsync(battleId, pair);

            Assert.NotNull(result);
            Assert.True(result!.Value.IsAccepted);

            var row = await results.GetByIdAsync(battleId);

            return new TerminalOutcome(
                battleId,
                row is not null,
                row?.Outcome ?? BattleOutcome.Victory,
                row?.DurationTurns ?? result.Value.State.Turn);
        }

        /// <summary>
        /// Creates a battle and leaves it un-resolved, so §4.5 note 10's
        /// active-battle exclusion can be exercised.
        /// </summary>
        public async Task<ActiveBattle> CreateActiveBattleAsync(string playerId)
        {
            using var scope = Services.CreateScope();

            var battles = scope.ServiceProvider.GetRequiredService<BattleStateService>();

            var battleId = $"history-ep-active-{Guid.NewGuid():N}";

            await battles.CreateBattleAsync(
                battleId,
                new PlayerId(playerId),
                new BattleStateService.PetConfiguration(
                    new PetId(PetInstanceIdFor(playerId)),
                    Element.Hoa,
                    new PassiveId("xich-lang"),
                    PassiveThreshold: 5),
                BossDefinitions.HoaLong);

            return new ActiveBattle(battleId);
        }

        /// <summary>
        /// Moves one stored result's <c>CompletedAt</c> into the past, so a test can
        /// establish distinct completion instants for results written moments apart.
        ///
        /// It edits the stored row directly rather than through the repository,
        /// because the repository deliberately exposes no update
        /// (<c>API_CONTRACTS.md</c> §4.5 introduces none) and the value it changes is
        /// the server clock reading the battle-end path captured — which is exactly
        /// what a test needs to control to exercise the documented ordering.
        /// </summary>
        public async Task AdvanceCompletedAtAsync(string battleId)
        {
            using var scope = Services.CreateScope();

            var context = scope.ServiceProvider
                .GetRequiredService<GameServer.Infrastructure.Postgres.GameDbContext>();

            var stored = await context.BattleResults
                .FirstOrDefaultAsync(result => result.BattleResultId == battleId);

            Assert.NotNull(stored);

            // One hour earlier, so the previously-written result is unambiguously
            // older than any result written after this call. A `with` expression
            // rewrites the row in place, because the repository deliberately exposes
            // no update (API_CONTRACTS.md §4.5 introduces none).
            context.Entry(stored!).CurrentValues.SetValues(
                stored! with { CompletedAt = stored!.CompletedAt.AddHours(-1) });

            await context.SaveChangesAsync();
        }

        /// <summary>
        /// Writes one durable result for the Player at an exact completion instant, so
        /// a test can construct the equal-<c>CompletedAt</c> case the tie-break rule
        /// exists for (<c>API_CONTRACTS.md</c> §4.5 note 4).
        /// </summary>
        public async Task<TerminalOutcome> WriteResultAtAsync(
            string playerId,
            string battleIdPrefix,
            DateTimeOffset completedAt)
        {
            using var scope = Services.CreateScope();

            var results = scope.ServiceProvider.GetRequiredService<IBattleResultRepository>();

            var battleId = $"{battleIdPrefix}-{Guid.NewGuid():N}";

            var result = new BattleResult(
                BattleResultId: battleId,
                PlayerId: playerId,
                PetInstanceId: PetInstanceIdFor(playerId),
                BossDefinitionId: BossDefinitions.HoaLong.BossDefinitionId,
                Outcome: BattleOutcome.Victory,
                DurationTurns: 3,
                CompletedAt: completedAt,
                RewardSummary:
                    "{\"playerXpGained\":100,\"newPlayerXp\":200,\"playerLeveledUp\":false,"
                    + "\"newPlayerLevel\":1,\"petXpGained\":100,\"newPetXp\":200,"
                    + "\"petLeveledUp\":false,\"newPetLevel\":1}");

            var written = await results.AddAsync(result);

            Assert.True(written, "the seeded result must be newly stored");

            return new TerminalOutcome(battleId, true, BattleOutcome.Victory, 3);
        }

        public async Task<BattleState?> GetStoredStateAsync(string battleId)
        {
            using var scope = Services.CreateScope();

            return await scope.ServiceProvider
                .GetRequiredService<IBattleStateRepository>()
                .GetAsync(battleId);
        }

        /// <summary>
        /// The durable result row for a battle, or <c>null</c> when none was written —
        /// read from the store the runtime wrote it to.
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
            int DurationTurns);

        /// <summary>An un-resolved battle, which therefore has no result row.</summary>
        public sealed record ActiveBattle(string BattleId);

        private sealed class HistoryHost : WebApplicationFactory<Program>
        {
            private readonly string _storeName = $"battle-history-ep-{Guid.NewGuid():N}";

            public HistoryHost()
            {
                // The canonical BossDefinition rows the result row's foreign key needs
                // (DATABASE.md §1 note item 5, TASK-053). In a real environment they
                // are provisioned by migration; here the isolated store is seeded with
                // the same canonical values.
                SeedAsync().GetAwaiter().GetResult();
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
                // registers neither the Npgsql provider nor the Redis store; this host
                // supplies the isolated in-memory pair instead — the established API
                // test-host pattern.
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
                            // verifies is the history read over the rows the battle-end
                            // step wrote.
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
