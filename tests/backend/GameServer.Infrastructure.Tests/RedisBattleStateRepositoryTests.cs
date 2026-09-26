using GameServer.Application.Battle;
using GameServer.Domain.Battle;
using GameServer.Domain.Battle.Serialization;
using GameServer.Domain.Bosses;
using GameServer.Domain.Elements;
using GameServer.Domain.Passives;
using GameServer.Domain.Pets;
using GameServer.Domain.Players;
using GameServer.Infrastructure.Redis;
using StackExchange.Redis;
using Xunit;

namespace GameServer.Infrastructure.Tests;

/// <summary>
/// The Redis active battle state store against real Redis
/// (<c>REDIS_STATE.md</c> §1–§4) — TASK-040.
///
/// <code>
/// battle:{battleId}:state      →   serialized BattleState (REDIS_STATE.md §1)
/// </code>
///
/// <b>Why these run against real Redis.</b> The contract under test is not the
/// serializer's or the service's: it is the store's own behaviour — the exact
/// key (<c>§1</c>), the sliding 30-minute expiry and its refresh points
/// (<c>§3</c>), and the <c>Sequence</c> compare-and-set that must refuse a stale
/// write (<c>§4</c> items 1–3, 6). None of those can be observed through an
/// in-memory double without asserting the double instead of the contract, so
/// these tests speak to Redis directly through the same
/// <see cref="IConnectionMultiplexer"/> the production composition registers.
///
/// <b>Environment.</b> They require a reachable Redis at the connection string
/// in <see cref="RedisConnection"/> (the <c>docker-compose.yml</c> instance on
/// port 6379 by default, overridable with <c>DCACTI_TEST_REDIS</c>). When no
/// Redis is reachable the suite reports the documented skip reason rather than
/// passing vacuously — a green run must mean the contract was actually
/// exercised.
///
/// <b>Isolation.</b> Every test uses its own battle id, so the keys it creates
/// cannot collide with another test's or with a running server's, and each key
/// is removed when its test finishes.
/// </summary>
public class RedisBattleStateRepositoryTests : IAsyncLifetime
{
    /// <summary>
    /// The Redis the suite speaks to. It is the local development instance the
    /// repository's <c>docker-compose.yml</c> provides
    /// (<c>ConnectionStrings__Redis</c> in <c>.env.example</c>), and may be
    /// overridden for another environment.
    /// </summary>
    private static readonly string RedisConnection =
        Environment.GetEnvironmentVariable("DCACTI_TEST_REDIS") ?? "127.0.0.1:6379";

    /// <summary>
    /// The documented expiry (<c>REDIS_STATE.md</c> §3): 30 minutes of
    /// inactivity, reset on every successful resolution.
    /// </summary>
    private static readonly TimeSpan StateTtl = TimeSpan.FromMinutes(30);

    /// <summary>
    /// The tolerance allowed when comparing the observed TTL with the documented
    /// 30 minutes. Reading the TTL takes a moment and Redis stores it in
    /// milliseconds, so an exact equality would be a race rather than an
    /// assertion; the window is far tighter than any wrong TTL policy (an hour,
    /// a day, or none) could hide inside.
    /// </summary>
    private static readonly TimeSpan TtlTolerance = TimeSpan.FromSeconds(30);

    private IConnectionMultiplexer? _connection;

    /// <summary>
    /// Whether a Redis was reachable. When it is not, the tests report the
    /// documented skip rather than asserting against nothing.
    /// </summary>
    private bool _redisAvailable;

    private BattleStateRepository _repository = null!;

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        try
        {
            _connection = await ConnectionMultiplexer.ConnectAsync(
                new ConfigurationOptions
                {
                    EndPoints = { RedisConnection },
                    AbortOnConnectFail = true,
                    ConnectTimeout = 3000,
                });
        }
        catch (RedisConnectionException)
        {
            // No Redis in this environment: the suite is reported as skipped so
            // a green run cannot be mistaken for a verified contract.
            _redisAvailable = false;
            return;
        }

        _redisAvailable = true;
        _repository = new BattleStateRepository(_connection);
    }

    /// <inheritdoc />
    public async Task DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.CloseAsync();
            _connection.Dispose();
        }
    }

    /// <summary>
    /// Fails the calling test when no Redis is reachable.
    ///
    /// <b>Why a failure and not a silent pass.</b> <c>REDIS_STATE.md</c> §1–§4's
    /// store is the subject of this suite, so a run without a reachable Redis has
    /// verified nothing. Reporting the absence as a test <i>failure</i> with the
    /// remedy keeps that visible: a green suite means the key, the TTL, and the
    /// compare-and-set were actually exercised against Redis, and cannot be
    /// produced by an environment where they were not.
    /// </summary>
    private void RequireRedis()
    {
        Assert.True(
            _redisAvailable,
            $"No Redis reachable at '{RedisConnection}', so the REDIS_STATE.md §1–§4 "
            + "contract cannot be verified. Start the docker-compose Redis "
            + "(docker compose up -d redis) or set DCACTI_TEST_REDIS to a reachable "
            + "instance.");
    }

    // =======================================================================
    // §1 — the key, and §2 — the record
    // =======================================================================

    [Fact]
    public async Task CreateAsync_ShouldWriteTheDocumentedKey()
    {
        // REDIS_STATE.md §1: the key is exactly battle:{battleId}:state.
        RequireRedis();

        var battleId = NewBattleId();
        var state = NewBattle(battleId);

        try
        {
            await _repository.CreateAsync(state);

            var database = _connection!.GetDatabase();

            // The documented key exists…
            Assert.True(await database.KeyExistsAsync($"battle:{battleId}:state"));

            // …and it is the ONLY key this battle created: §1 defines no second
            // state key, no per-resolution key, and no board-specific key. The
            // optional §4 item 4 lock key is deliberately not used.
            var keys = await KeysForBattleAsync(battleId);
            Assert.Equal([$"battle:{battleId}:state"], keys);
        }
        finally
        {
            await DeleteBattleKeysAsync(battleId);
        }
    }

    [Fact]
    public async Task CreateAsync_ShouldStoreTASK029RuntimeJson_ThatRoundTrips()
    {
        // REDIS_STATE.md §2 item 1: the value is the GAME_STATE.md §2 JSON with no
        // additional Redis-only fields. Verified by reading the raw stored
        // document and mapping it back with the TASK-029 serializer, so the
        // assertion is on the persisted bytes rather than on the returned object.
        RequireRedis();

        var battleId = NewBattleId();
        var state = NewBattle(battleId);

        try
        {
            await _repository.CreateAsync(state);

            var stored = await _connection!.GetDatabase().StringGetAsync($"battle:{battleId}:state");

            Assert.True(stored.HasValue, "the documented key must hold the record");

            // The persisted document IS the authoritative runtime serialization.
            Assert.Equal(BattleStateSerializer.Serialize(state), stored.ToString());

            // And it restores to the state that was written — no member lost,
            // defaulted, or re-derived (GAME_STATE.md §2.1.7 item 5,
            // REDIS_STATE.md §7 items 9–12).
            var restored = BattleStateSerializer.Deserialize(stored.ToString());

            Assert.Equal(state.BattleId, restored.BattleId);
            Assert.Equal(state.Turn, restored.Turn);
            Assert.Equal(state.Sequence, restored.Sequence);
            Assert.Equal(state.RngSeed, restored.RngSeed);
            Assert.Equal(state.RngState, restored.RngState);
            Assert.Equal(state.Combo, restored.Combo);
            Assert.Equal(state.MatchCount, restored.MatchCount);
            Assert.Equal(state.LastCommittedSwapPair, restored.LastCommittedSwapPair);
            Assert.True(state.BoardState.CellsEqual(restored.BoardState));
            Assert.Equal(state.PetState, restored.PetState);
            Assert.Equal(state.BossState, restored.BossState);
        }
        finally
        {
            await DeleteBattleKeysAsync(battleId);
        }
    }

    [Fact]
    public async Task GetAsync_ShouldReturnWhatWasStored_AndNullForAnUnknownBattle()
    {
        // REDIS_STATE.md §2 item 2 / §3: the record is the source of truth, and an
        // unknown (or expired) id reads as absence — never as a defaulted state.
        RequireRedis();

        var battleId = NewBattleId();
        var state = NewBattle(battleId);

        try
        {
            await _repository.CreateAsync(state);

            var reloaded = await _repository.GetAsync(battleId);

            Assert.NotNull(reloaded);
            Assert.Equal(state.Sequence, reloaded!.Sequence);
            Assert.Equal(state.Turn, reloaded.Turn);
            Assert.True(state.BoardState.CellsEqual(reloaded.BoardState));
            Assert.Equal(state.BossState, reloaded.BossState);

            Assert.Null(await _repository.GetAsync(NewBattleId()));
        }
        finally
        {
            await DeleteBattleKeysAsync(battleId);
        }
    }

    [Fact]
    public async Task CreateAsync_ShouldRoundTripBothBattleIdentitiesThroughRedis()
    {
        // GAME_STATE.md §2.8 item 2 / §2.3 / REDIS_STATE.md §2 item 1, §3:
        // the record written at creation is the state's single surviving
        // authoritative carrier, so the owning Player's identity and the owned Pet
        // INSTANCE identity must read back from Redis exactly as creation recorded
        // them — that is what lets the battle-end persistence path source
        // BattleResult.PlayerId and BattleResult.PetInstanceId from the stored
        // record (DATABASE.md §1, ADR-014 decisions 1 and 4).
        //
        // Both are deliberately non-default values, so a member dropped, renamed, or
        // defaulted by the store could not pass. The assertion inspects the raw
        // stored document as well as the documented read path, so "the repository
        // returned an object the test still held" cannot be mistaken for a verified
        // round trip.
        RequireRedis();

        var battleId = NewBattleId();
        var state = NewBattle(battleId);

        try
        {
            await _repository.CreateAsync(state);

            // The raw stored bytes genuinely carry both members, under the
            // serializer's own explicit names.
            var stored = await _connection!.GetDatabase().StringGetAsync($"battle:{battleId}:state");

            Assert.True(stored.HasValue);

            using (var document = System.Text.Json.JsonDocument.Parse(stored.ToString()))
            {
                Assert.Equal(
                    "player_redis_repository_owner",
                    document.RootElement.GetProperty("playerId").GetString());

                Assert.Equal(
                    "pet-instance-redis-1",
                    document.RootElement.GetProperty("petState").GetProperty("petId").GetString());
            }

            // And the documented read path returns them unchanged.
            var reloaded = await _repository.GetAsync(battleId);

            Assert.NotNull(reloaded);
            Assert.Equal(state.PlayerId, reloaded!.PlayerId);
            Assert.Equal("player_redis_repository_owner", reloaded.PlayerId.Value);
            Assert.Equal(state.PetState.PetId, reloaded.PetState.PetId);
            Assert.Equal("pet-instance-redis-1", reloaded.PetState.PetId.Value);
        }
        finally
        {
            await DeleteBattleKeysAsync(battleId);
        }
    }

    [Fact]
    public async Task TryUpdateAsync_ShouldCarryBothBattleIdentitiesThroughTheWriteBack()
    {
        // REDIS_STATE.md §4 item 5 / GAME_STATE.md §5.1: a resolution performs one
        // write-back, and it must not lose the identities creation recorded — the
        // state written back is the same state extended, not a rebuilt one. This is
        // the property the battle-end path depends on: whenever the battle ends, the
        // record still carries its owner and its owned Pet.
        RequireRedis();

        var battleId = NewBattleId();
        var state = NewBattle(battleId);

        try
        {
            await _repository.CreateAsync(state);

            var resolved = state with { Sequence = state.Sequence + 1, Turn = state.Turn + 1 };

            Assert.True(await _repository.TryUpdateAsync(resolved, expectedSequence: state.Sequence));

            var stored = await _repository.GetAsync(battleId);

            Assert.NotNull(stored);
            Assert.Equal(state.Sequence + 1, stored!.Sequence);
            Assert.Equal(state.PlayerId, stored.PlayerId);
            Assert.Equal("player_redis_repository_owner", stored.PlayerId.Value);
            Assert.Equal(state.PetState.PetId, stored.PetState.PetId);
            Assert.Equal("pet-instance-redis-1", stored.PetState.PetId.Value);
        }
        finally
        {
            await DeleteBattleKeysAsync(battleId);
        }
    }

    // =======================================================================
    // §3 — the sliding 30-minute TTL and its refresh points
    // =======================================================================

    [Fact]
    public async Task CreateAsync_ShouldSetTheDocumentedThirtyMinuteTtl()
    {
        // REDIS_STATE.md §3: "default 30 minutes of inactivity". The TTL is
        // present from creation, so an abandoned battle expires rather than
        // living forever.
        RequireRedis();

        var battleId = NewBattleId();

        try
        {
            await _repository.CreateAsync(NewBattle(battleId));

            var ttl = await _connection!.GetDatabase().KeyTimeToLiveAsync($"battle:{battleId}:state");

            Assert.NotNull(ttl);
            AssertWithinDocumentedTtl(ttl!.Value);
        }
        finally
        {
            await DeleteBattleKeysAsync(battleId);
        }
    }

    [Fact]
    public async Task TryUpdateAsync_OnSuccess_ShouldRefreshTheTtl()
    {
        // REDIS_STATE.md §3: the expiry slides — "TTL reset on every successful
        // resolution". The key is shortened first so the refresh is observable as
        // a real change rather than as the creation value being re-read.
        RequireRedis();

        var battleId = NewBattleId();
        var state = NewBattle(battleId);

        try
        {
            await _repository.CreateAsync(state);

            var database = _connection!.GetDatabase();

            // Collapse the expiry, so a refresh that did not happen would be
            // plainly visible.
            await database.KeyExpireAsync($"battle:{battleId}:state", TimeSpan.FromSeconds(45));

            var shortened = await database.KeyTimeToLiveAsync($"battle:{battleId}:state");
            Assert.NotNull(shortened);
            Assert.True(
                shortened!.Value < TimeSpan.FromMinutes(5),
                "the fixture's short expiry must be in place before the refresh is asserted");

            // One accepted resolution: Sequence advances, and the write-back
            // resets the expiry (§4 item 5's one write-back, §3's sliding refresh).
            var resolved = state with { Sequence = state.Sequence + 1, Turn = state.Turn + 1 };

            Assert.True(await _repository.TryUpdateAsync(resolved, expectedSequence: state.Sequence));

            var refreshed = await database.KeyTimeToLiveAsync($"battle:{battleId}:state");

            Assert.NotNull(refreshed);
            AssertWithinDocumentedTtl(refreshed!.Value);

            // The record is the resolved one — the write was not a TTL-only touch.
            Assert.Equal(resolved.Sequence, (await _repository.GetAsync(battleId))!.Sequence);
        }
        finally
        {
            await DeleteBattleKeysAsync(battleId);
        }
    }

    [Fact]
    public async Task TryUpdateAsync_OnAStaleSequence_ShouldWriteNothingAndNotRefreshTheTtl()
    {
        // REDIS_STATE.md §4 items 2–3 and 7: the write is refused, and a refused
        // write touches nothing — not the record, not the TTL, and not the
        // Sequence.
        RequireRedis();

        var battleId = NewBattleId();
        var state = NewBattle(battleId);

        try
        {
            await _repository.CreateAsync(state);

            var database = _connection!.GetDatabase();

            // A short expiry, so "the TTL was not refreshed" is observable.
            await database.KeyExpireAsync($"battle:{battleId}:state", TimeSpan.FromSeconds(60));

            var before = await repository_readRawRecord();

            // Another resolution committed first: the stored Sequence is now N+1
            // while this attempt still expects N.
            var newer = state with { Sequence = state.Sequence + 1, Turn = state.Turn + 1 };
            Assert.True(await _repository.TryUpdateAsync(newer, expectedSequence: state.Sequence));

            var afterCommit = await repository_readRawRecord();

            // A stale attempt built from the ORIGINAL state (Sequence N) tries to
            // write. It must be refused.
            var stale = state with { Sequence = state.Sequence + 1, Combo = 999 };

            Assert.False(await _repository.TryUpdateAsync(stale, expectedSequence: state.Sequence));

            // The record is untouched by the refusal — still the newer state.
            Assert.Equal(afterCommit, await repository_readRawRecord());
            Assert.NotEqual(before, afterCommit);

            var stored = await _repository.GetAsync(battleId);
            Assert.Equal(newer.Sequence, stored!.Sequence);

            // And Combo did not take the stale attempt's value: the newer
            // authoritative state was not overwritten by an older one (§4 item 3).
            Assert.NotEqual(999, stored.Combo);

            async Task<string?> repository_readRawRecord() =>
                (await database.StringGetAsync($"battle:{battleId}:state")).ToString();
        }
        finally
        {
            await DeleteBattleKeysAsync(battleId);
        }
    }

    [Fact]
    public async Task TryUpdateAsync_ShouldRefreshTheTtlOnEachSuccess_NotOnlyTheFirst()
    {
        // REDIS_STATE.md §3: the expiry slides on EVERY successful resolution, so
        // a long battle stays alive while it is being played. Two successive
        // resolutions are checked, each after the expiry was shortened.
        RequireRedis();

        var battleId = NewBattleId();
        var state = NewBattle(battleId);

        try
        {
            await _repository.CreateAsync(state);

            var database = _connection!.GetDatabase();
            var current = state;

            for (var resolution = 1; resolution <= 2; resolution++)
            {
                await database.KeyExpireAsync($"battle:{battleId}:state", TimeSpan.FromSeconds(45));

                var shortened = await database.KeyTimeToLiveAsync($"battle:{battleId}:state");
                Assert.True(shortened!.Value < TimeSpan.FromMinutes(5));

                var resolved = current with
                {
                    Sequence = current.Sequence + 1,
                    Turn = current.Turn + 1,
                };

                Assert.True(
                    await _repository.TryUpdateAsync(resolved, expectedSequence: current.Sequence),
                    $"resolution {resolution} must be applied against the sequence it read");

                var refreshed = await database.KeyTimeToLiveAsync($"battle:{battleId}:state");

                Assert.NotNull(refreshed);
                AssertWithinDocumentedTtl(refreshed!.Value);

                // §4 item 6: Sequence advanced by exactly one per accepted
                // resolution, on the stored record.
                Assert.Equal(current.Sequence + 1, (await _repository.GetAsync(battleId))!.Sequence);

                current = resolved;
            }
        }
        finally
        {
            await DeleteBattleKeysAsync(battleId);
        }
    }

    // =======================================================================
    // §4 — the Sequence compare-and-set
    // =======================================================================

    [Fact]
    public async Task TryUpdateAsync_WithTheStoredSequence_ShouldSucceed()
    {
        // REDIS_STATE.md §4 item 2: "write succeeds only if Sequence in Redis
        // still matches what was read". current N / expected N → success.
        RequireRedis();

        var battleId = NewBattleId();
        var state = NewBattle(battleId);

        try
        {
            await _repository.CreateAsync(state);

            var resolved = state with { Sequence = state.Sequence + 1, Turn = state.Turn + 1 };

            Assert.True(await _repository.TryUpdateAsync(resolved, expectedSequence: state.Sequence));

            var stored = await _repository.GetAsync(battleId);

            Assert.Equal(state.Sequence + 1, stored!.Sequence);
            Assert.Equal(state.Turn + 1, stored.Turn);
        }
        finally
        {
            await DeleteBattleKeysAsync(battleId);
        }
    }

    [Fact]
    public async Task TryUpdateAsync_WithAnOlderSequence_ShouldConflictAndLeaveTheNewerStateIntact()
    {
        // REDIS_STATE.md §4 items 2–3, 6: current N+1 / expected N → the write is
        // refused, so a stale resolution can never overwrite a newer
        // authoritative state.
        RequireRedis();

        var battleId = NewBattleId();
        var state = NewBattle(battleId);

        try
        {
            await _repository.CreateAsync(state);

            // The committed state advances to N+1.
            var committed = state with { Sequence = state.Sequence + 1, MatchCount = state.MatchCount + 3 };
            Assert.True(await _repository.TryUpdateAsync(committed, expectedSequence: state.Sequence));

            // A stale attempt, still expecting N, tries to write its own N+1.
            var stale = state with { Sequence = state.Sequence + 1, MatchCount = 77 };

            Assert.False(
                await _repository.TryUpdateAsync(stale, expectedSequence: state.Sequence),
                "§4 item 2: a write whose expected Sequence no longer matches must fail");

            // §4 item 3: the newer authoritative state is intact.
            var stored = await _repository.GetAsync(battleId);

            Assert.Equal(committed.Sequence, stored!.Sequence);
            Assert.Equal(committed.MatchCount, stored.MatchCount);
            Assert.NotEqual(77, stored.MatchCount);
        }
        finally
        {
            await DeleteBattleKeysAsync(battleId);
        }
    }

    [Fact]
    public async Task TryUpdateAsync_ShouldCompareSequence_AndNeverTurn()
    {
        // REDIS_STATE.md §4 item 6 / GAME_STATE.md §5 item 3: "Sequence is the
        // only concurrency token … Turn is a game value written inside the record
        // and is never compared for the compare-and-set". A write whose Turn does
        // not correspond to the stored Turn is therefore applied on a Sequence
        // match — Turn is carried as state, not checked.
        RequireRedis();

        var battleId = NewBattleId();
        var state = NewBattle(battleId);

        try
        {
            await _repository.CreateAsync(state);

            // A deliberately unrelated Turn value with a matching Sequence.
            var resolved = state with { Sequence = state.Sequence + 1, Turn = 999 };

            Assert.True(
                await _repository.TryUpdateAsync(resolved, expectedSequence: state.Sequence),
                "§4 item 6: only Sequence gates the write; Turn is never compared");

            var stored = await _repository.GetAsync(battleId);

            Assert.Equal(999, stored!.Turn);
        }
        finally
        {
            await DeleteBattleKeysAsync(battleId);
        }
    }

    [Fact]
    public async Task TryUpdateAsync_ForAnUnknownBattle_ShouldNotCreateARecord()
    {
        // REDIS_STATE.md §3 ties creation to POST /api/battle/start, and §1 fixes
        // the key set. An update for a battle with no record therefore applies
        // nothing rather than creating a record — which would be an invented
        // battle.
        RequireRedis();

        var battleId = NewBattleId();

        try
        {
            Assert.False(
                await _repository.TryUpdateAsync(NewBattle(battleId), expectedSequence: 0));

            Assert.False(await _connection!.GetDatabase().KeyExistsAsync($"battle:{battleId}:state"));
            Assert.Empty(await KeysForBattleAsync(battleId));
        }
        finally
        {
            await DeleteBattleKeysAsync(battleId);
        }
    }

    // =======================================================================
    // Fixtures
    // =======================================================================

    /// <summary>
    /// A battle id unique to one test, so its keys cannot collide with another
    /// test's or with a running server's records.
    /// </summary>
    private static string NewBattleId() => $"task040-{Guid.NewGuid():N}";

    /// <summary>
    /// The owning Player of the battles this suite stores
    /// (<c>GAME_STATE.md</c> §2.8) — a real, non-default identity, so a record that
    /// lost it could not pass the round-trip assertions.
    /// </summary>
    private static readonly PlayerId Owner = new("player_redis_repository_owner");

    /// <summary>
    /// The owned Pet instance the stored battles select
    /// (<c>GAME_STATE.md</c> §2.3 — the <c>Pet.PetInstanceId</c>), for the same
    /// reason as <see cref="Owner"/>.
    /// </summary>
    private static readonly PetId OwnedPet = new("pet-instance-redis-1");

    /// <summary>
    /// A representative authoritative state (<c>GAME_STATE.md</c> §2) created
    /// through the documented Domain factory, so the record under test is a real
    /// battle state rather than a hand-built document.
    /// </summary>
    private static BattleState NewBattle(string battleId) =>
        BattleState.Create(
            battleId,
            rngSeed: 20260722UL,
            Owner,
            OwnedPet,
            Element.Hoa,
            new PassiveId("xich-lang"),
            passiveThreshold: 5,
            BossDefinitions.HoaLong);

    /// <summary>
    /// Every key that belongs to a battle. <c>REDIS_STATE.md</c> §1 defines one,
    /// so this is the set the "no second key" assertions compare against.
    /// </summary>
    private async Task<string[]> KeysForBattleAsync(string battleId)
    {
        var endpoint = _connection!.GetEndPoints().First();

        var keys = new List<string>();

        await foreach (var key in _connection.GetServer(endpoint).KeysAsync(pattern: $"battle:{battleId}:*"))
        {
            keys.Add(key.ToString());
        }

        keys.Sort(StringComparer.Ordinal);

        return [.. keys];
    }

    /// <summary>
    /// Removes the battle's keys, so the suite leaves no records behind and a
    /// rerun starts from the same place.
    /// </summary>
    private async Task DeleteBattleKeysAsync(string battleId)
    {
        if (!_redisAvailable)
        {
            return;
        }

        foreach (var key in await KeysForBattleAsync(battleId))
        {
            await _connection!.GetDatabase().KeyDeleteAsync(key);
        }
    }

    /// <summary>
    /// Asserts an observed TTL is the documented 30 minutes
    /// (<c>REDIS_STATE.md</c> §3), within the read tolerance.
    /// </summary>
    private static void AssertWithinDocumentedTtl(TimeSpan observed)
    {
        Assert.True(
            observed <= StateTtl && observed >= StateTtl - TtlTolerance,
            $"REDIS_STATE.md §3 fixes the active state's expiry at 30 minutes of "
            + $"inactivity; observed {observed}.");
    }
}
