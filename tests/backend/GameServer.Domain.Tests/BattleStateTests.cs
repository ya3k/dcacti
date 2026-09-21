using GameServer.Domain.Battle;
using GameServer.Domain.Match3;
using Xunit;

namespace GameServer.Domain.Tests;

/// <summary>
/// Board Foundation State contract tests (GAME_STATE.md §2.0.5).
///
/// These verify the documented field set, the retained initial values, and the
/// fields this stage still does not add. The board stage is §2.0 plus the board
/// and RNG fields — it removes nothing, renames nothing, and redefines nothing
/// (§0 item 5). It is still not the full §2 gameplay state: no Pet, Boss, Player,
/// Card, or Relic state exists here and none is added.
/// </summary>
public class BattleStateTests
{
    /// <summary>A fixed seed, so the generated board is deterministic in tests.</summary>
    private const ulong TestSeed = 42UL;

    [Fact]
    public void Create_ShouldProduceDocumentedInitialValues()
    {
        // GAME_STATE.md §2.0.2, §2.0.5.2 item 1: Turn = 0, Sequence = 0 — board
        // generation is not a player Swap/Action and is not an action resolution.
        var state = BattleState.Create("battle-1", TestSeed);

        Assert.Equal("battle-1", state.BattleId);
        Assert.Equal(0, state.Turn);
        Assert.Equal(0, state.Sequence);
    }

    [Fact]
    public void Create_ShouldMatchTheDocumentedInitialValueConstants()
    {
        var state = BattleState.Create("battle-1", TestSeed);

        Assert.Equal(BattleState.InitialTurn, state.Turn);
        Assert.Equal(BattleState.InitialSequence, state.Sequence);
        Assert.Equal(0, BattleState.InitialTurn);
        Assert.Equal(0, BattleState.InitialSequence);
    }

    [Fact]
    public void BoardFoundationState_ShouldCarryExactlyTheDocumentedFields()
    {
        // GAME_STATE.md §2.0.5: BattleId, Turn, Sequence, RngSeed, RngState,
        // BoardState — nothing else, plus the Swap stage's LastCommittedSwapPair
        // (§2.1.10) and the Match / Combo stage's PlayerState (§2.2), each added to
        // this same record by its own owning stage.
        var properties = typeof(BattleState)
            .GetProperties()
            .Select(p => p.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[]
            {
                "BattleId", "BoardState", "LastCommittedSwapPair", "PlayerState", "RngSeed",
                "RngState", "Sequence", "Turn",
            },
            properties);
    }

    [Fact]
    public void BoardFoundationState_ShouldKeepTheFoundationFieldsIntact()
    {
        // §0 item 5: the board stage is §2.0 plus fields — it removes nothing,
        // renames nothing, and redefines nothing.
        var declared = typeof(BattleState)
            .GetProperties()
            .Select(p => p.Name)
            .ToArray();

        foreach (var foundationField in new[] { "BattleId", "Turn", "Sequence" })
        {
            Assert.Contains(foundationField, declared);
        }
    }

    [Fact]
    public void BoardFoundationState_ShouldDeclareNoStatusOrLifecycleField()
    {
        // GAME_STATE.md §2.0.3 / §2.0.5: the board stage adds no Status field and
        // no lifecycle value.
        var declared = typeof(BattleState)
            .GetProperties()
            .Select(p => p.Name)
            .Concat(typeof(BattleState).GetFields().Select(f => f.Name))
            .ToArray();

        Assert.DoesNotContain("Status", declared);

        foreach (var lifecycle in new[]
                 {
                     "READY", "STARTING", "ACTIVE", "PAUSED", "FINISHED", "WON", "LOST",
                 })
        {
            Assert.DoesNotContain(lifecycle, declared);
        }
    }

    [Fact]
    public void BoardFoundationState_ShouldStillDeclareNoLaterStageField()
    {
        // GAME_STATE.md §2.0.5.3: PetState (§2.3) and BossState (§2.4) are still
        // absent and still owned by later stages, and the rest of PlayerState (§2.2)
        // — HP, ATK/DEF/Crit, Power, StatusEffects, EquippedRelics, EquippedCards —
        // is not implemented either.
        //
        // PendingSpecialGems is in the list for a different reason: it is not a deferred
        // field — the placeholder was removed and does not exist in any form
        // (GAME_STATE.md §2.1.2 item 1). Special Gem state is carried inside each
        // `Cells[64]` entry (§2.1.1), so no board-level collection for it may appear.
        //
        // PlayerState is deliberately NOT in the list: it is the field the Match /
        // Combo accounting stage implements (§2.2), and §2 nests it inside
        // BattleState. Its own two fields are covered by PlayerStateContractTests.
        // LastCommittedSwapPair is likewise NOT in the list: it is the one field the
        // Swap stage adds, and §2.1.10 documents it as required state, not a
        // convenience. The check below is that no OTHER later-stage field appeared.
        var declared = typeof(BattleState)
            .GetProperties()
            .Select(p => p.Name)
            .Concat(typeof(BattleState).GetFields().Select(f => f.Name))
            .Concat(typeof(BoardState).GetProperties().Select(p => p.Name))
            .Concat(typeof(PlayerState).GetProperties().Select(p => p.Name))
            .ToArray();

        foreach (var laterStageField in new[]
                 {
                     "PetState", "BossState", "PendingSpecialGems",
                     "HP", "MaxHP", "ATK", "DEF", "Crit", "Power",
                     "StatusEffects", "EquippedRelics", "EquippedCards",
                 })
        {
            Assert.DoesNotContain(laterStageField, declared);
        }
    }

    [Fact]
    public void Create_ShouldRetainTheSeedItWasGiven()
    {
        // GAME_STATE.md §2.6.1 item 3: RngSeed records the battle's origin point
        // and is never rewritten after creation.
        var state = BattleState.Create("battle-1", TestSeed);

        Assert.Equal(TestSeed, state.RngSeed);
    }

    [Fact]
    public void Create_ShouldRecordTheRngStateAfterGeneration()
    {
        // GAME_STATE.md §2.7.1 step 6: the resulting RngState is retained.
        var state = BattleState.Create("battle-1", TestSeed);

        // It is a state + increment pair, not a single word (§2.6.2 item 1), and
        // the stream selector must be odd for PCG-XSH-RR 64/32 (ADR-009).
        Assert.Equal(1UL, state.RngState.Increment & 1UL);
    }

    [Fact]
    public void Create_ShouldNotResetTheRngStateToItsSeededValue()
    {
        // §15 of the task and GAME_STATE.md §2.6.2: the state represents the
        // actual state after all RNG consumption used by the accepted generation
        // attempt. It is not the freshly seeded state, because the board consumed
        // at least 64 draws.
        var state = BattleState.Create("battle-1", TestSeed);
        var freshlySeeded = Pcg32.FromSeed(TestSeed).CurrentState;

        Assert.NotEqual(freshlySeeded, state.RngState);
    }

    [Fact]
    public void Create_ShouldGenerateAValidAuthoritativeBoard()
    {
        // MATCH3_RULES.md §1.2 items 2–3, enforced during generation.
        var state = BattleState.Create("battle-1", TestSeed);

        Assert.Equal(64, state.BoardState.Cells.Count);
        Assert.False(BoardGenerationValidator.HasMatch(state.BoardState));
        Assert.True(BoardGenerationValidator.HasValidSwap(state.BoardState));
    }

    [Fact]
    public void Create_ShouldBeDeterministicForTheSameSeed()
    {
        // MATCH3_RULES.md §7 item 4: the same seed produces the same initial
        // board, including the same §1.5 retry sequence.
        var first = BattleState.Create("battle-1", TestSeed);
        var second = BattleState.Create("battle-2", TestSeed);

        Assert.Equal(first.BoardState.Cells, second.BoardState.Cells);
        Assert.Equal(first.RngState, second.RngState);
    }

    [Fact]
    public void Create_ShouldScopesStateToTheRequestedBattleId()
    {
        // GAME_STATE.md §2.0.1: BattleId identifies the battle session and
        // carries no gameplay content.
        var first = BattleState.Create("battle-1", TestSeed);
        var second = BattleState.Create("battle-2", TestSeed);

        Assert.NotEqual(first.BattleId, second.BattleId);
        Assert.Equal(0, first.Turn);
        Assert.Equal(0, second.Turn);
    }

    [Fact]
    public void Create_ShouldRejectAnEmptyBattleId()
    {
        Assert.Throws<ArgumentException>(() => BattleState.Create(string.Empty, TestSeed));
        Assert.Throws<ArgumentException>(() => BattleState.Create("   ", TestSeed));
    }

    [Fact]
    public void Create_ShouldProduceAnImmutableSnapshot()
    {
        // State is server-authored (GAME_RULES.md §18, ADR-001): a caller
        // cannot mutate a snapshot in place.
        var state = BattleState.Create("battle-1", TestSeed);

        Assert.IsType<BattleState>(state with { Turn = state.Turn }, exactMatch: false);
        Assert.Equal(0, state.Turn);
    }
}