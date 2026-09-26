using GameServer.Application.Battle;
using GameServer.Domain.Battle;
using GameServer.Domain.Battle.Serialization;
using GameServer.Domain.Bosses;
using GameServer.Domain.Cards;
using GameServer.Domain.Elements;
using GameServer.Domain.Match3;
using GameServer.Domain.Passives;
using GameServer.Domain.Pets;
using GameServer.Domain.Players;
using GameServer.Domain.Relics;
using Xunit;

namespace GameServer.Application.Tests;

/// <summary>
/// Proves the <c>BattleState</c> serialization mapping is usable from the real
/// battle lifecycle — the Application-level integration seam required by
/// TASK-029 §23.
///
/// <code>
/// Battle creation (BattleStateService, the production path)
///         ↓
/// BattleState
///         ↓
/// BattleStateSerializer.Serialize
///         ↓
/// JSON
///         ↓
/// BattleStateSerializer.Deserialize
///         ↓
/// BattleState
/// </code>
///
/// <b>This test remains in memory.</b> TASK-029's mapping under test is a pure
/// function of the state, so the lifecycle integration it verifies is exactly
/// "the state the application actually creates can round-trip" — the
/// precondition the active-state store relies on. The store used here is
/// <see cref="InMemoryBattleStateRepository"/>, a test double: the Redis
/// implementation of the same contract is
/// <c>GameServer.Infrastructure.Redis.BattleStateRepository</c> and is verified
/// against a live Redis in the Infrastructure and smoke suites, where the key,
/// the document, the TTL, and the compare-and-set can actually be observed.
///
/// <b>TASK-040 lifted the §7 deferral.</b> The former assertion that the
/// lifecycle "writes no Redis key" encoded the <c>REDIS_STATE.md</c> §7 deferral,
/// which §7's status note recorded as "due rather than deferred" once
/// <c>POST /api/battle/start</c> could create a real battle. Creation now
/// persists the documented record, so this suite asserts the mapping's
/// round-trip property (its own subject) and leaves the store's key, TTL, and
/// compare-and-set to the suites that own them.
///
/// The state is created through <see cref="BattleStateService.CreateBattleAsync"/> — the
/// production bootstrap the documented <c>POST /api/battle/start</c> path composes
/// — rather than through a hand-built fixture, so the test would fail if the
/// lifecycle ever produced a state the mapping cannot represent.
/// </summary>
public class BattleStateSerializationLifecycleTests
{
    /// <summary>
    /// The owned Pet instance identity these battles select
    /// (<c>GAME_STATE.md</c> §2.3 — the <c>Pet.PetInstanceId</c>).
    /// </summary>
    private const string OwnedPetInstanceId = "owned-pet-instance-1";

    /// <summary>
    /// The owning Player of these battles (<c>GAME_STATE.md</c> §2.8) — the
    /// identity the production creation path records.
    /// </summary>
    private static readonly PlayerId Owner = new("player_lifecycle_owner");

    /// <summary>
    /// The active Pet's configuration, supplied to the production creation path
    /// exactly as the battle-start composition supplies it — the identity of the
    /// owned Pet instance the battle selected (<c>GAME_STATE.md</c> §2.3) and both
    /// battle-scoped loadout snapshots.
    /// </summary>
    private static BattleStateService.PetConfiguration RepresentativePetConfiguration() =>
        new(
            PetId: new PetId(OwnedPetInstanceId),
            Element: Element.Hoa,
            PassiveId: new PassiveId("hoa-long-combo"),
            PassiveThreshold: 5,
            PassiveResetOverride: PassiveResetBehavior.Partial,
            EquippedRelics:
            [
                new EquippedRelicIdentity("owned-relic-3"),
                new EquippedRelicIdentity("owned-relic-1"),
                new EquippedRelicIdentity("owned-relic-2"),
                new EquippedRelicIdentity("owned-relic-4"),
            ],
            EquippedCards:
            [
                new EquippedCardIdentity("card-heal"),
                new EquippedCardIdentity("card-heal"),
                new EquippedCardIdentity("card-shield"),
                new EquippedCardIdentity("card-hoa-long-skill"),
            ]);

    /// <summary>
    /// A deterministically seeded service over the in-memory store double, so the
    /// created board is reproducible and an assertion about the round trip cannot
    /// pass or fail by luck (<c>GAME_STATE.md</c> §2.6.1 — the seed's origin is
    /// the server, and the source is a replaceable dependency).
    /// </summary>
    private static BattleStateService SeededService() =>
        new(new InMemoryBattleStateRepository(), new FixedRngSeedSource());

    [Fact]
    public async Task CreatedBattle_ShouldRoundTripThroughTheSerializer()
    {
        // The core integration obligation: the state the CURRENT application
        // lifecycle creates survives serialize → deserialize.
        var service = SeededService();

        var created = await service.CreateBattleAsync(
            "battle-lifecycle-roundtrip",
            Owner,
            RepresentativePetConfiguration(),
            BossDefinitions.HoaLong);

        var restored = BattleStateSerializer.Deserialize(BattleStateSerializer.Serialize(created));

        Assert.Equal(created.BattleId, restored.BattleId);
        Assert.Equal(created.Turn, restored.Turn);
        Assert.Equal(created.Sequence, restored.Sequence);
        Assert.Equal(created.RngSeed, restored.RngSeed);
        Assert.Equal(created.RngState, restored.RngState);
        Assert.Equal(created.Combo, restored.Combo);
        Assert.Equal(created.MatchCount, restored.MatchCount);
        Assert.Equal(created.LastCommittedSwapPair, restored.LastCommittedSwapPair);
        Assert.True(created.BoardState.CellsEqual(restored.BoardState));
    }

    [Fact]
    public async Task CreatedBattle_ShouldRoundTripBothLoadoutSnapshots()
    {
        // The post-TASK-027/028 members: the state created by the lifecycle carries
        // both snapshots, and each survives with its count, identity, and order
        // intact (RELIC_RULES.md §2.3, §2.5; CARD_RULES.md §1).
        var service = SeededService();

        var created = await service.CreateBattleAsync(
            "battle-lifecycle-loadouts",
            Owner,
            RepresentativePetConfiguration(),
            BossDefinitions.HoaLong);

        // The creation path really did populate them — otherwise the round trip below
        // would be asserting null equals null.
        Assert.NotNull(created.PetState.EquippedRelics);
        Assert.NotNull(created.PetState.EquippedCards);

        var restored = BattleStateSerializer.Deserialize(BattleStateSerializer.Serialize(created));

        Assert.Equal(
            created.PetState.EquippedRelics!.Select(r => r.Value),
            restored.PetState.EquippedRelics!.Select(r => r.Value));
        Assert.Equal(
            created.PetState.EquippedCards!.Select(c => c.Value),
            restored.PetState.EquippedCards!.Select(c => c.Value));

        // Relic order is the equip-slot order and is NOT the sorted order.
        Assert.Equal(
            ["owned-relic-3", "owned-relic-1", "owned-relic-2", "owned-relic-4"],
            restored.PetState.EquippedRelics!.Select(r => r.Value));

        // The repeated Card definition survives as a duplicate.
        Assert.Equal(2, restored.PetState.EquippedCards!.Count(c => c.Value == "card-heal"));
    }

    [Fact]
    public async Task CreatedBattle_ShouldRoundTripEveryPetAndBossMember()
    {
        // The state the lifecycle produces is fully representable: no PetState or
        // BossState member is lost, defaulted, or re-derived on the way back
        // (GAME_STATE.md §2.3, §2.4).
        var service = SeededService();

        var created = await service.CreateBattleAsync(
            "battle-lifecycle-members",
            Owner,
            RepresentativePetConfiguration(),
            BossDefinitions.HoaLong);

        var restored = BattleStateSerializer.Deserialize(BattleStateSerializer.Serialize(created));

        var petBefore = created.PetState;
        var petAfter = restored.PetState;

        Assert.Equal(petBefore.HP, petAfter.HP);
        Assert.Equal(petBefore.MaxHP, petAfter.MaxHP);
        Assert.Equal(petBefore.ATK, petAfter.ATK);
        Assert.Equal(petBefore.DEF, petAfter.DEF);
        Assert.Equal(petBefore.Crit, petAfter.Crit);
        Assert.Equal(petBefore.Power, petAfter.Power);
        Assert.Equal(petBefore.Element, petAfter.Element);
        Assert.Equal(petBefore.PassiveId, petAfter.PassiveId);
        Assert.Equal(petBefore.PassiveProgress, petAfter.PassiveProgress);
        Assert.Equal(petBefore.PassiveResetOverride, petAfter.PassiveResetOverride);

        // BossState is a value type over value members, so its own equality is exact.
        Assert.Equal(created.BossState, restored.BossState);
    }

    [Fact]
    public async Task CreatedBattle_ShouldRoundTripItsGeneratedBoardAndSpecialGems()
    {
        // The board the lifecycle generated is restored cell for cell, with every
        // Special Gem's type and orientation at the same index (GAME_STATE.md §2.1.7
        // item 5). A freshly generated board holds no Special Gem, so one is placed
        // to exercise the conditional member on real lifecycle state.
        var service = SeededService();

        var created = await service.CreateBattleAsync(
            "battle-lifecycle-board",
            Owner,
            RepresentativePetConfiguration(),
            BossDefinitions.HoaLong);

        // The generated board's entries are rebuilt with two Special Gems placed,
        // through the public board API only (BoardState.Cells[64] carries the cell's
        // Gem type and its Special Gem together — GAME_STATE.md §2.1.5 item 1).
        var entries = created.BoardState.ToCellArray();
        entries[3] = new Cell(entries[3].GemType, SpecialGem.LineClearVertical());
        entries[44] = new Cell(entries[44].GemType, SpecialGem.Burst());

        var withSpecials = created with { BoardState = BoardState.FromCellEntries(entries) };

        var restored = BattleStateSerializer.Deserialize(BattleStateSerializer.Serialize(withSpecials));

        Assert.True(withSpecials.BoardState.CellsEqual(restored.BoardState));
        Assert.Equal(SpecialGemType.LineClear, restored.BoardState.SpecialGemAt(3)!.Value.Type);
        Assert.Equal(SpecialGemOrientation.Vertical, restored.BoardState.SpecialGemAt(3)!.Value.Orientation);
        Assert.Equal(SpecialGemType.Burst, restored.BoardState.SpecialGemAt(44)!.Value.Type);
        Assert.Null(restored.BoardState.SpecialGemAt(44)!.Value.Orientation);
    }

    [Fact]
    public async Task CreatedBattle_ShouldRoundTripAfterACommittedSwapAdvancesTheState()
    {
        // The stronger lifecycle case: a battle that has actually been PLAYED. A
        // committed Swap advances Turn and Sequence, writes the commit record, and
        // changes the board, the RNG state, Combo, and MatchCount (GAME_STATE.md
        // §5.1) — so this is the state a real persisted record would hold, and the
        // round trip must preserve all of it.
        //
        // The Swap is found by asking the production validator, not by guessing:
        // a deterministic seed makes the board reproducible, and the first accepted
        // adjacent Swap drives the real resolution.
        var service = SeededService();

        var created = await service.CreateBattleAsync(
            "battle-lifecycle-played",
            Owner,
            RepresentativePetConfiguration(),
            BossDefinitions.HoaLong);

        var result = await ExecuteFirstAcceptedSwapAsync(service, created);

        Assert.True(result.IsAccepted, "the fixture battle must accept at least one Swap");

        var committed = result.State;

        // The Swap really did move the state — otherwise this test would be a
        // duplicate of the creation case.
        Assert.Equal(BattleState.InitialTurn + 1, committed.Turn);
        Assert.Equal(BattleState.InitialSequence + 1, committed.Sequence);
        Assert.NotNull(committed.LastCommittedSwapPair);

        var restored = BattleStateSerializer.Deserialize(BattleStateSerializer.Serialize(committed));

        Assert.Equal(committed.Turn, restored.Turn);
        Assert.Equal(committed.Sequence, restored.Sequence);
        Assert.Equal(committed.Combo, restored.Combo);
        Assert.Equal(committed.MatchCount, restored.MatchCount);
        Assert.Equal(committed.RngState, restored.RngState);
        Assert.Equal(committed.LastCommittedSwapPair, restored.LastCommittedSwapPair);
        Assert.True(committed.BoardState.CellsEqual(restored.BoardState));
        Assert.Equal(committed.PetState.PassiveProgress, restored.PetState.PassiveProgress);
        Assert.Equal(committed.BossState, restored.BossState);
    }

    [Fact]
    public async Task CreatedBattle_ShouldPersistTheDocumentedRecordAndRoundTripItThroughTheStore()
    {
        // TASK-040 / REDIS_STATE.md §2 item 1: the record the store holds IS the
        // documented runtime JSON — the same TASK-029 mapping, with no
        // Redis-only field added to it. The store is read back through the
        // Application contract, so this asserts the mapping's contract at the
        // persistence seam the real Redis implementation also uses.
        var store = new InMemoryBattleStateRepository();
        var service = new BattleStateService(store, new FixedRngSeedSource());

        var created = await service.CreateBattleAsync(
            "battle-lifecycle-persisted",
            Owner,
            RepresentativePetConfiguration(),
            BossDefinitions.HoaLong);

        // Creation persisted exactly one record (REDIS_STATE.md §3 "Created").
        Assert.Equal(1, store.WriteCount);

        var reloaded = await service.GetBattleAsync("battle-lifecycle-persisted");

        Assert.NotNull(reloaded);
        Assert.Equal(created.BattleId, reloaded!.BattleId);
        Assert.Equal(created.PlayerId, reloaded.PlayerId);
        Assert.Equal(created.Turn, reloaded.Turn);
        Assert.Equal(created.Sequence, reloaded.Sequence);
        Assert.Equal(created.RngSeed, reloaded.RngSeed);
        Assert.Equal(created.RngState, reloaded.RngState);
        Assert.Equal(created.Combo, reloaded.Combo);
        Assert.Equal(created.MatchCount, reloaded.MatchCount);
        Assert.True(created.BoardState.CellsEqual(reloaded.BoardState));
        Assert.Equal(created.PetState.PetId, reloaded.PetState.PetId);
        Assert.Equal(created.BossState, reloaded.BossState);

        // The document is the state's own shape: it names no key, carries no TTL
        // or lock, and holds no transport concern. Checked as JSON MEMBER NAMES
        // rather than as raw substrings, because a battle id is arbitrary text
        // and would otherwise produce a false positive against its own fixture
        // value.
        var json = BattleStateSerializer.Serialize(created);

        using var document = System.Text.Json.JsonDocument.Parse(json);
        var rootNames = document.RootElement.EnumerateObject().Select(p => p.Name).ToArray();

        foreach (var forbidden in new[] { "key", "ttl", "expire", "lock", "connectionId", "state" })
        {
            Assert.DoesNotContain(forbidden, rootNames);
        }

        // The record is exactly the GAME_STATE.md §2 root member set — no
        // Redis-only field was added (REDIS_STATE.md §2 item 1).
        // `lastCommittedSwapPair` is absent here because this is a NEWLY CREATED
        // battle: no Swap has been committed, and §2.1.10 item 3 makes that absence
        // the documented representation rather than a null or sentinel pair.
        Assert.Equal(
            [
                "battleId", "playerId", "turn", "sequence", "rngSeed", "rngState",
                "boardState", "combo", "matchCount", "petState", "bossState",
            ],
            rootNames);
        Assert.Null(created.LastCommittedSwapPair);
    }

    // =======================================================================
    // Fixture helper — drives the production Swap path to find a legal move.
    // =======================================================================

    /// <summary>
    /// Executes the first adjacent Swap the production validator accepts, so the
    /// post-resolution state comes from the real resolution pipeline rather than from
    /// a hand-written state.
    ///
    /// It scans the documented adjacent pairs in index order and returns the first
    /// accepted result. A rejection is a documented no-op
    /// (<c>MATCH3_RULES.md</c> §2.1.5), so scanning until one is accepted does not
    /// alter state beyond the single committed Swap that is returned.
    /// </summary>
    private static async Task<SwapExecutionResult> ExecuteFirstAcceptedSwapAsync(
        BattleStateService service,
        BattleState state)
    {
        for (var row = 0; row < BoardState.Rows; row++)
        {
            for (var column = 0; column < BoardState.Columns; column++)
            {
                var from = BoardState.ToIndex(row, column);

                // Right and down neighbours are the two directions that enumerate
                // every adjacent pair exactly once.
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
                    var result = await service.ExecuteSwapAsync(state.BattleId, new SwapRequest(from, to));

                    if (result is { IsAccepted: true })
                    {
                        return result.Value;
                    }
                }
            }
        }

        throw new InvalidOperationException(
            "The fixture board accepted no adjacent Swap; the scenario cannot exercise the resolution path.");
    }
}
