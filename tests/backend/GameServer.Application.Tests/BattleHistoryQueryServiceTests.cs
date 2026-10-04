using GameServer.Application.Battle;
using GameServer.Domain.Battle;
using GameServer.Domain.Bosses;
using Xunit;

namespace GameServer.Application.Tests;

/// <summary>
/// The completed-battle history read through
/// <see cref="BattleResultQueryService"/> — <c>API_CONTRACTS.md</c> §4.5.
///
/// <code>
/// authenticated PlayerId            (§4.5 note 8 — supplied by the caller)
///         ↓
/// BattleResultQueryService          (Application — no request value read)
///         ↓
/// IBattleResultRepository           (Player-scoped, ordered)
///         ↓
/// [ durable BattleResult rows ]     (§4.5 notes 4, 5, 9)
/// </code>
///
/// <b>Why this suite exists beside the API endpoint tests.</b> The endpoint suite
/// proves the wire contract end to end. This one drives the Application boundary
/// directly, so the ownership rule is verified where it is actually decided
/// (<c>AGENTS.md</c> §15 — "Do not rely only on controller-level mocking"): the
/// service takes the caller's identity as an argument and reads no request value,
/// and the Player filter is the read's own rather than a post-filter the endpoint
/// could bypass.
/// </summary>
public class BattleHistoryQueryServiceTests
{
    private const string PlayerA = "player_history_a";
    private const string PlayerB = "player_history_b";

    /// <summary>
    /// One stored result with the documented member set
    /// (<c>DATABASE.md</c> §1) — a real record rather than a defaulted shape.
    /// </summary>
    private static BattleResult Result(
        string battleResultId,
        string playerId,
        DateTimeOffset completedAt,
        BattleOutcome outcome = BattleOutcome.Victory,
        int durationTurns = 7,
        string rewardSummary = "{}") =>
        new(
            battleResultId,
            playerId,
            $"pet_instance_{playerId}",
            "boss-def-hoa-long",
            outcome,
            durationTurns,
            completedAt,
            rewardSummary);

    private static (BattleResultQueryService Service, InMemoryBattleResultRepository Store) Create()
    {
        var store = new InMemoryBattleResultRepository();

        return (new BattleResultQueryService(store), store);
    }

    // -----------------------------------------------------------------------
    // §4.5 note 8 — history is scoped to the authenticated PlayerId
    // -----------------------------------------------------------------------

    [Fact]
    public async Task History_ShouldReturnOnlyTheCallersOwnResults()
    {
        // API_CONTRACTS.md §4.5 note 8: "The caller reads only their own
        // BattleResult rows: the identity resolved from the authenticated session
        // must equal BattleResult.PlayerId." Player A must never receive Player B's
        // records.
        var (service, store) = Create();

        var instant = new DateTimeOffset(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);

        store.Seed(Result("battle-a-1", PlayerA, instant));
        store.Seed(Result("battle-b-1", PlayerB, instant.AddMinutes(1)));

        var historyA = await service.ListHistoryAsync(PlayerA);

        var onlyA = Assert.Single(historyA);

        Assert.Equal("battle-a-1", onlyA.BattleResultId);
        Assert.Equal(PlayerA, onlyA.PlayerId);

        var historyB = await service.ListHistoryAsync(PlayerB);

        var onlyB = Assert.Single(historyB);

        Assert.Equal("battle-b-1", onlyB.BattleResultId);
        Assert.Equal(PlayerB, onlyB.PlayerId);
    }

    [Fact]
    public async Task History_ShouldBeEmpty_WhenTheCallerHasNoResults()
    {
        // §4.5 note 9: no completed battles is an empty history — and another
        // Player's results do not make it non-empty.
        var (service, store) = Create();

        store.Seed(Result("battle-b-1", PlayerB, DateTimeOffset.UtcNow));

        Assert.Empty(await service.ListHistoryAsync(PlayerA));
        Assert.Empty(await service.ListHistoryAsync("player_who_never_fought"));
    }

    [Fact]
    public async Task History_ShouldRejectAnAbsentCallerIdentity()
    {
        // §4.5 note 8 permits no request-supplied identity, so an absent one cannot
        // be satisfied by reading the request — it is refused instead. An identity
        // that named no Player would otherwise have to mean "every Player".
        var (service, _) = Create();

        await Assert.ThrowsAnyAsync<ArgumentException>(() => service.ListHistoryAsync("  "));
    }

    [Fact]
    public async Task History_ShouldCompareIdentitiesOrdinally()
    {
        // Both sides are opaque server-authored identity strings, so the match is
        // ordinal (as the owner-only single-result read's is). A case-different
        // identity is a different Player and receives nothing.
        var (service, store) = Create();

        store.Seed(Result("battle-case", "Player_History_Case", DateTimeOffset.UtcNow));

        Assert.Empty(await service.ListHistoryAsync("player_history_case"));
    }

    // -----------------------------------------------------------------------
    // §4.5 note 4 — ordering
    // -----------------------------------------------------------------------

    [Fact]
    public async Task History_ShouldReturnNewestFirst()
    {
        // §4.5 note 4: most recent completed battle first. Seeded out of order, so
        // the assertion is about the documented ordering rather than insertion
        // order.
        var (service, store) = Create();

        var baseInstant = new DateTimeOffset(2026, 9, 27, 10, 0, 0, TimeSpan.Zero);

        store.Seed(Result("battle-oldest", PlayerA, baseInstant));
        store.Seed(Result("battle-newest", PlayerA, baseInstant.AddHours(2)));
        store.Seed(Result("battle-middle", PlayerA, baseInstant.AddHours(1)));

        var history = await service.ListHistoryAsync(PlayerA);

        Assert.Equal(
            new[] { "battle-newest", "battle-middle", "battle-oldest" },
            history.Select(result => result.BattleResultId));
    }

    [Fact]
    public async Task History_ShouldBreakTies_ByDescendingBattleResultId()
    {
        // §4.5 note 4: equal CompletedAt values are ordered by descending
        // BattleResultId, "so the total order is deterministic even though
        // DATABASE.md §1 does not require CompletedAt to be unique".
        var (service, store) = Create();

        var shared = new DateTimeOffset(2026, 9, 27, 10, 0, 0, TimeSpan.Zero);

        store.Seed(Result("battle-tie-1", PlayerA, shared));
        store.Seed(Result("battle-tie-10", PlayerA, shared));
        store.Seed(Result("battle-tie-2", PlayerA, shared));

        var history = await service.ListHistoryAsync(PlayerA);

        Assert.All(history, result => Assert.Equal(shared, result.CompletedAt));

        Assert.Equal(
            new[] { "battle-tie-2", "battle-tie-10", "battle-tie-1" },
            history.Select(result => result.BattleResultId));
    }

    // -----------------------------------------------------------------------
    // §4.5 notes 2, 3, 12 — the returned record the endpoint projects from
    // -----------------------------------------------------------------------

    [Fact]
    public async Task History_ShouldReturnTheStoredRecordsUnchanged()
    {
        // §4.5 notes 2–3: every element member is the stored row's own value — the
        // outcome, the duration, the 8-member reward summary, and the persisted
        // completion instant. The read computes nothing, so this asserts the record
        // reaches the projection whole.
        var (service, store) = Create();

        var completedAt = new DateTimeOffset(2026, 9, 27, 8, 15, 42, TimeSpan.Zero);

        const string rewardSummary =
            "{\"playerXpGained\":100,\"newPlayerXp\":200,\"playerLeveledUp\":false,"
            + "\"newPlayerLevel\":1,\"petXpGained\":100,\"newPetXp\":200,"
            + "\"petLeveledUp\":false,\"newPetLevel\":1}";

        store.Seed(Result(
            "battle-intact",
            PlayerA,
            completedAt,
            outcome: BattleOutcome.Defeat,
            durationTurns: 0,
            rewardSummary: rewardSummary));

        var only = Assert.Single(await service.ListHistoryAsync(PlayerA));

        Assert.Equal("battle-intact", only.BattleResultId);
        Assert.Equal(BattleOutcome.Defeat, only.Outcome);
        Assert.Equal(0, only.DurationTurns);
        Assert.Equal(completedAt, only.CompletedAt);
        Assert.Equal(rewardSummary, only.RewardSummary);

        // The two persisted identities §4.5 note 12 keeps out of the response are
        // still carried by the record — the endpoint's projection omits them, and
        // this read does not trim them.
        Assert.Equal("pet_instance_player_history_a", only.PetInstanceId);
        Assert.Equal("boss-def-hoa-long", only.BossDefinitionId);
    }

    [Fact]
    public async Task History_ShouldNotBoundTheNumberOfResults()
    {
        // §4.5 note 5: the array is the complete history, "however long it is; an
        // unbounded array is the accepted MVP contract". The Application read adds
        // no cap of its own.
        var (service, store) = Create();

        var baseInstant = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);

        for (var index = 0; index < 40; index++)
        {
            store.Seed(Result($"battle-many-{index:D3}", PlayerA, baseInstant.AddMinutes(index)));
        }

        var history = await service.ListHistoryAsync(PlayerA);

        Assert.Equal(40, history.Count);
        Assert.Equal("battle-many-039", history[0].BattleResultId);
        Assert.Equal("battle-many-000", history[^1].BattleResultId);
    }

    [Fact]
    public async Task History_ShouldRefuseARowTheCallerDoesNotOwn()
    {
        // §4.5 note 8 is an invariant on the boundary, not merely a query the
        // service hopes is filtered: a repository that returned another Player's row
        // must not have it disclosed. The service refuses the leaked row rather than
        // passing it on.
        var store = new LeakingBattleResultRepository(
            Result("battle-not-yours", PlayerB, DateTimeOffset.UtcNow));

        var service = new BattleResultQueryService(store);

        Assert.Empty(await service.ListHistoryAsync(PlayerA));
    }

    /// <summary>
    /// A deliberately over-permissive <see cref="IBattleResultRepository"/> that
    /// answers every history read with a row belonging to another Player — the
    /// boundary violation <c>API_CONTRACTS.md</c> §4.5 note 8 forbids. It exists only
    /// to prove the Application read refuses such a row rather than disclosing it.
    /// </summary>
    private sealed class LeakingBattleResultRepository : IBattleResultRepository
    {
        private readonly BattleResult _leaked;

        public LeakingBattleResultRepository(BattleResult leaked)
        {
            _leaked = leaked;
        }

        public Task<bool> AddAsync(BattleResult result, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<BattleResult?> GetByIdAsync(
            string battleResultId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<BattleResult?>(null);

        public Task<IReadOnlyList<BattleResult>> ListByPlayerIdAsync(
            string playerId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<BattleResult>>([_leaked]);
    }
}
