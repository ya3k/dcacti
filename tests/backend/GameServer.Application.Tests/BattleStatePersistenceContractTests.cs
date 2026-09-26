using GameServer.Application.Battle;
using GameServer.Domain.Battle;
using GameServer.Domain.Bosses;
using GameServer.Domain.Elements;
using GameServer.Domain.Match3;
using GameServer.Domain.Passives;
using GameServer.Domain.Pets;
using GameServer.Domain.Players;
using Xunit;

namespace GameServer.Application.Tests;

/// <summary>
/// The Application layer's use of the documented <c>Sequence</c>
/// compare-and-set — TASK-040 (<c>REDIS_STATE.md</c> §4).
///
/// <code>
/// SwapRequest
///         ↓
/// load the stored record            §4 item 1
///         ↓
/// resolve (Domain)                  §4 item 5 — one write-back per resolution
///         ↓
/// write guarded by the Sequence     §4 items 2–3 — stale writes are refused
///   ├── match    → committed        the resolution's single write-back
///   └── mismatch → abort + retry against the fresh state (§4 item 2)
/// </code>
///
/// <b>What this suite covers that the store's own suite cannot.</b> The Redis
/// store tests (<c>GameServer.Infrastructure.Tests.RedisBattleStateRepositoryTests</c>)
/// verify the compare-and-set primitive against real Redis. These tests verify
/// that the <i>Application layer uses it correctly</i>: that a stale resolution
/// is aborted and retried against the fresh state rather than being allowed to
/// overwrite it, that an accepted action performs exactly one write-back, and
/// that a rejected action performs none at all. That is the behaviour
/// <c>REDIS_STATE.md</c> §4 items 2–3, 5, 7 require of the resolution path, and
/// it is exercised here through <see cref="BattleStateService"/> — the same
/// production pipeline the hub calls.
///
/// The store is the in-memory double, which implements the documented
/// compare-and-set semantics (including a switch for forcing a conflict, so the
/// retry path is driven deterministically rather than by racing threads).
/// </summary>
public class BattleStatePersistenceContractTests
{
    /// <summary>
    /// The active Pet's configuration, as the battle-start composition supplies
    /// it (<c>GAME_STATE.md</c> §2.3) — including the identity of the owned Pet
    /// instance the battle selected.
    /// </summary>
    private static readonly BattleStateService.PetConfiguration PetConfiguration =
        new(new PetId("pet_instance_1"), Element.Hoa, new PassiveId("xich-lang"), PassiveThreshold: 5);

    /// <summary>
    /// The owning Player of the battles these tests create
    /// (<c>GAME_STATE.md</c> §2.8) — the identity the creation path records.
    /// </summary>
    private static readonly PlayerId Owner = new("player_owner_1");

    /// <summary>
    /// The Boss the battles are fought against — Hỏa Long, an MVP Boss
    /// (<c>BOSS_RULES.md</c> §6.1).
    /// </summary>
    private static readonly BossDefinition BossDefinition = BossDefinitions.HoaLong;

    private static BattleStateService NewService(InMemoryBattleStateRepository repository) =>
        new(repository, new FixedRngSeedSource());

    // =======================================================================
    // §3 — creation writes the record
    // =======================================================================

    [Fact]
    public async Task CreateBattle_ShouldWriteTheRecordBeforeReturning()
    {
        // REDIS_STATE.md §3 "Created: on POST /api/battle/start": a created
        // battle is a stored battle. There is no window in which it exists only
        // in process memory (§2 item 2, §7 item 5).
        var store = new InMemoryBattleStateRepository();
        var service = NewService(store);

        var created = await service.CreateBattleAsync("battle-persisted", Owner, PetConfiguration, BossDefinition);

        // The store received exactly the creation write…
        Assert.Equal(1, store.WriteCount);
        Assert.Equal(1, store.RecordCount);

        // …and the record is readable through the Application contract, equal to
        // the state creation returned.
        var reloaded = await service.GetBattleAsync("battle-persisted");

        Assert.NotNull(reloaded);
        Assert.Equal(created.Sequence, reloaded!.Sequence);
        Assert.Equal(created.Turn, reloaded.Turn);
        Assert.Equal(created.RngSeed, reloaded.RngSeed);
        Assert.True(created.BoardState.CellsEqual(reloaded.BoardState));
    }

    [Fact]
    public async Task GetBattle_ForAnUnknownBattle_ShouldReturnNull_WithoutCreatingOne()
    {
        // REDIS_STATE.md §3 ties creation to POST /api/battle/start. A read for
        // an unknown (or expired) id reports absence and writes nothing — it
        // never repairs the miss by inventing a battle.
        var store = new InMemoryBattleStateRepository();
        var service = NewService(store);

        Assert.Null(await service.GetBattleAsync("battle-never-created"));
        Assert.Null(await service.GetInitialStateForGroupAsync("battle-never-created"));

        Assert.Equal(0, store.WriteCount);
        Assert.Equal(0, store.RecordCount);
    }

    // =======================================================================
    // §4 item 5 — one accepted action is one write-back
    // =======================================================================

    [Fact]
    public async Task AcceptedSwap_ShouldPerformExactlyOneWriteBack()
    {
        // REDIS_STATE.md §4 item 5: "The write is one write-back per resolution."
        // A board resolution mutates the board many times internally; the record
        // is written once, after the board is stable and Sequence is incremented.
        var store = new InMemoryBattleStateRepository();
        var service = NewService(store);

        var created = await service.CreateBattleAsync("battle-one-write", Owner, PetConfiguration, BossDefinition);

        var writesAfterCreation = store.WriteCount;

        var pair = FindAdjacentPairThatProducesAMatch(created.BoardState);
        var result = await service.ExecuteSwapAsync("battle-one-write", pair);

        Assert.True(result!.Value.IsAccepted);

        // Exactly one further write for this one resolved action — not one per
        // Match, per cascade pass, or per Special Gem.
        Assert.Equal(writesAfterCreation + 1, store.WriteCount);

        // And the record holds the resolved state, at Sequence 1.
        var stored = await service.GetBattleAsync("battle-one-write");

        Assert.Equal(1, stored!.Sequence);
        Assert.Equal(1, stored.Turn);
        Assert.Equal(result.Value.State.Sequence, stored.Sequence);
        Assert.True(result.Value.State.BoardState.CellsEqual(stored.BoardState));
    }

    [Fact]
    public async Task RejectedSwap_ShouldPerformNoWriteAtAll()
    {
        // REDIS_STATE.md §4 item 7 / MATCH3_RULES.md §2.1.5: "A rejected action
        // writes nothing at all: it does not touch this key, does not reset the
        // TTL, and does not change Sequence."
        var store = new InMemoryBattleStateRepository();
        var service = NewService(store);

        var created = await service.CreateBattleAsync("battle-no-write", Owner, PetConfiguration, BossDefinition);

        var writesAfterCreation = store.WriteCount;

        // 99 → 100 is out of range, so the validator rejects it (INVALID_CELL_INDEX).
        var rejected = await service.ExecuteSwapAsync("battle-no-write", new SwapRequest(99, 100));

        Assert.True(rejected!.Value.IsRejected);

        // No store call was made at all: not a write, and no TTL refresh.
        Assert.Equal(writesAfterCreation, store.WriteCount);

        // The record is untouched — same Sequence, same Turn, exactly as created.
        var stored = await service.GetBattleAsync("battle-no-write");

        Assert.Equal(created.Sequence, stored!.Sequence);
        Assert.Equal(created.Turn, stored.Turn);
        Assert.Equal(created.Combo, stored.Combo);
        Assert.Equal(created.MatchCount, stored.MatchCount);
    }

    // =======================================================================
    // §4 items 2–3 — a stale Sequence is refused and retried, never applied
    // =======================================================================

    [Fact]
    public async Task StaleResolution_ShouldBeAbortedAndRetriedAgainstTheFreshState()
    {
        // REDIS_STATE.md §4 item 2: "write succeeds only if Sequence in Redis
        // still matches what was read. If it does not match, the resolution is
        // aborted and retried against the fresh state."
        //
        // The conflict is forced deterministically (rather than by racing two
        // threads), so the retry path is exercised every run.
        var store = new InMemoryBattleStateRepository();
        var service = NewService(store);

        var created = await service.CreateBattleAsync("battle-cas-retry", Owner, PetConfiguration, BossDefinition);

        var writesAfterCreation = store.WriteCount;

        // The FIRST compare-and-set attempt is refused, as if another resolution
        // had committed in between. The service must re-read and retry.
        store.ForcedConflicts = 1;

        var pair = FindAdjacentPairThatProducesAMatch(created.BoardState);
        var result = await service.ExecuteSwapAsync("battle-cas-retry", pair);

        Assert.True(result!.Value.IsAccepted, "the retry must commit the action");

        // Exactly ONE write was applied for this action — the refused attempt
        // wrote nothing, and the contract allows only one write-back per
        // resolution (§4 item 5).
        Assert.Equal(writesAfterCreation + 1, store.WriteCount);

        // The committed record is at Sequence 1 — the action was applied once,
        // not twice, so the retry did not double-apply it.
        var stored = await service.GetBattleAsync("battle-cas-retry");

        Assert.Equal(1, stored!.Sequence);
        Assert.Equal(1, stored.Turn);
        Assert.Equal(result.Value.State.Sequence, stored.Sequence);
        Assert.True(result.Value.State.BoardState.CellsEqual(stored.BoardState));
    }

    [Fact]
    public async Task RepeatedStaleResolution_ShouldNeverOverwriteTheNewerRecord()
    {
        // REDIS_STATE.md §4 item 3: "This guarantees the single-writer-at-a-time
        // property ... even if two requests for the same battle somehow arrive
        // concurrently" — and §4 items 2–3 forbid an older state overwriting a
        // newer one.
        //
        // Every attempt is refused, so the action can never be committed. The
        // service must not report a false acknowledgement, and it must leave the
        // stored record exactly as it was.
        var store = new InMemoryBattleStateRepository();
        var service = NewService(store);

        var created = await service.CreateBattleAsync("battle-cas-conflict", Owner, PetConfiguration, BossDefinition);

        var writesAfterCreation = store.WriteCount;
        var pair = FindAdjacentPairThatProducesAMatch(created.BoardState);

        // More forced conflicts than the service's bounded retry budget, so the
        // path that gives up is exercised.
        store.ForcedConflicts = 64;

        var result = await service.ExecuteSwapAsync("battle-cas-conflict", pair);

        // The action was NOT committed. Whether the service reports the
        // documented staleness rejection or reports the battle as unresolvable,
        // it must never report acceptance for a resolution that was not stored.
        Assert.False(result is { IsAccepted: true }, "an uncommitted resolution must not be reported as accepted");

        // The stored record is intact: no write was applied beyond the creation,
        // and the state is exactly what creation stored.
        Assert.Equal(writesAfterCreation, store.WriteCount);

        var stored = await service.GetBattleAsync("battle-cas-conflict");

        Assert.Equal(created.Sequence, stored!.Sequence);
        Assert.Equal(created.Turn, stored.Turn);
        Assert.Equal(created.Combo, stored.Combo);
        Assert.Equal(created.MatchCount, stored.MatchCount);
        Assert.True(created.BoardState.CellsEqual(stored.BoardState));
    }

    // =======================================================================
    // §4 item 6 — Sequence is the ONLY compare-and-set token
    // =======================================================================

    [Fact]
    public async Task CompareAndSet_ShouldGateOnSequence_AndNeverTurn()
    {
        // REDIS_STATE.md §4 item 6 / GAME_STATE.md §5 item 3: "Sequence is the
        // only concurrency token. Turn is a game value written inside the record
        // and is never compared for the compare-and-set."
        //
        // Verified at the boundary the service uses: an update whose expected
        // Sequence matches is accepted even though its Turn is unrelated, which
        // it could not be if Turn participated in the check.
        var store = new InMemoryBattleStateRepository();
        var service = NewService(store);

        var created = await service.CreateBattleAsync("battle-cas-token", Owner, PetConfiguration, BossDefinition);

        var withUnrelatedTurn = created with { Sequence = created.Sequence + 1, Turn = 777 };

        Assert.True(
            await store.TryUpdateAsync(withUnrelatedTurn, expectedSequence: created.Sequence),
            "only Sequence gates the write (§4 item 6)");

        // An update whose expected Sequence does not match is refused, whatever
        // its Turn claims.
        Assert.False(
            await store.TryUpdateAsync(created with { Sequence = created.Sequence + 2 }, expectedSequence: 999));

        var stored = await service.GetBattleAsync("battle-cas-token");

        Assert.Equal(777, stored!.Turn);
        Assert.Equal(created.Sequence + 1, stored.Sequence);
    }

    // =======================================================================
    // Fixture
    // =======================================================================

    /// <summary>
    /// The first adjacent Swap the production validator accepts, so the
    /// scenario drives the real resolution pipeline rather than a hand-built
    /// state. A rejection is a documented no-op, so scanning does not alter
    /// state.
    /// </summary>
    private static SwapRequest FindAdjacentPairThatProducesAMatch(BoardState board)
    {
        for (var row = 0; row < BoardState.Rows; row++)
        {
            for (var column = 0; column < BoardState.Columns; column++)
            {
                var from = BoardState.ToIndex(row, column);

                var candidates = new List<int>(2);

                if (column + 1 < BoardState.Columns)
                {
                    candidates.Add(BoardState.ToIndex(row, column + 1));
                }

                if (row + 1 < BoardState.Rows)
                {
                    candidates.Add(BoardState.ToIndex(row + 1, column));
                }

                foreach (var to in candidates)
                {
                    var request = new SwapRequest(from, to);

                    // Asking the production validator is what keeps this fixture
                    // honest: the chosen pair is one the server accepts.
                    if (SwapValidator.Validate(board, lastCommittedSwapPair: null, request).IsAccepted)
                    {
                        return request;
                    }
                }
            }
        }

        throw new InvalidOperationException(
            "The fixture board accepted no adjacent Swap; the scenario cannot exercise the resolution path.");
    }
}
