using GameServer.Domain.Battle;
using GameServer.Domain.Cards;
using GameServer.Domain.Match3;
using Xunit;

namespace GameServer.Domain.Tests.Cards;

/// <summary>
/// B-02 integration coverage: the per-Turn Card-cast allowance across the real
/// Swap pipeline (<c>CARD_RULES.md</c> §3 item 6, <c>MATCH3_RULES.md</c> §8.1
/// item 6, <c>ADR-021</c>).
///
/// <see cref="CardCastExecutorTests"/> covers the cast-side rule in isolation.
/// This file proves the two properties that only the real swap executor can
/// show:
///
/// <code>
/// committed Swap  → allowance restored (MATCH3_RULES.md §8.1 item 1)
/// rejected Swap   → allowance untouched (MATCH3_RULES.md §2.1.5)
/// </code>
///
/// The swap fixtures are the canonical ones <c>SwapExecutionTests</c> uses, so
/// the "committed" and "rejected" swaps below are real resolutions rather than
/// hand-modelled state transitions.
/// </summary>
public sealed class CardCastTurnAllowanceTests
{
    private const ulong TestSeed = 20260815UL;

    // The canonical fixture for "a swap that completes a match": row 4 holds
    // 'A','D','A' at columns 0-2 and (3,1) holds the 'A' above the 'D', so
    // exchanging (3,1)<->(4,1) makes row 4 'AAA'. Neither cell is in a run
    // before the swap. Mirrors SwapExecutionTests (§3 horizontal Match).
    private const int From = 25; // I(3, 1)
    private const int To = 33;   // I(4, 1)

    private static int I(int row, int column) => TestBoard.I(row, column);

    private static BoardState MatchingBoard() =>
        TestBoard.Background().WithGems(
            (I(4, 0), GemType.Atk),
            (I(4, 1), GemType.Def),
            (I(4, 2), GemType.Atk),
            (I(3, 1), GemType.Atk));

    private static readonly CardDefinition HealCard = new()
    {
        CardDefinitionId = "card-heal",
        Name = "Heal",
        Category = CardCategory.Basic,
        PowerCost = 20,
        LoadoutCopyLimit = 1,
        EffectDefinition = CardEffectDefinitions.Create(
            CardEffectDefinition.Create(CardEffectType.Heal, CardEffectValueType.PercentMaxHp, 20)),
    };

    private static readonly CardDefinition ShieldCard = new()
    {
        CardDefinitionId = "card-shield",
        Name = "Shield",
        Category = CardCategory.Basic,
        PowerCost = 20,
        LoadoutCopyLimit = 1,
        EffectDefinition = CardEffectDefinitions.Create(
            CardEffectDefinition.Create(CardEffectType.Shield, CardEffectValueType.PercentMaxHp, 20)),
    };

    /// <summary>
    /// A battle on the completing-swap board with both castable Cards equipped and
    /// Power enough for either.
    /// </summary>
    private static BattleState Battle() =>
        BattleState.CreateWith("battle-cast-allowance", TestSeed) with
        {
            BoardState = MatchingBoard(),
            PetState = BattleState.CreateWith("battle-cast-allowance", TestSeed).PetState with
            {
                Power = 100,
                EquippedCards =
                [
                    new EquippedCardIdentity("card-heal"),
                    new EquippedCardIdentity("card-shield"),
                ],
            },
        };

    [Fact]
    public void CommittedSwap_RestoresTheTurnAllowance()
    {
        // Scenario 4 (through the real pipeline): the committed Swap's write-back
        // is the allowance's one reset point (MATCH3_RULES.md §8.1 item 1, §8.3).
        var state = Battle();

        // Turn N: the one cast succeeds and spends the allowance.
        var cast = CardCastExecutor.Execute(state, HealCard);
        Assert.True(cast.IsAccepted);
        Assert.Equal(1, cast.State.CardCastsUsedThisTurn);

        // Still Turn N: a second cast is rejected.
        var blocked = CardCastExecutor.Execute(cast.State, ShieldCard);
        Assert.True(blocked.IsRejected);
        Assert.Equal(CardCastRejectionReason.CardCastAlreadyUsedThisTurn, blocked.Reason);

        // The committed Swap advances the Turn and restores the allowance in the
        // same write-back.
        var swap = SwapExecutor.Execute(cast.State, new SwapRequest(From, To));
        Assert.True(swap.IsAccepted);

        var nextTurn = swap.State;
        Assert.Equal(cast.State.Turn + 1, nextTurn.Turn);
        Assert.Equal(BattleState.InitialCardCastsUsedThisTurn, nextTurn.CardCastsUsedThisTurn);

        // Turn N+1: casting is available again.
        var afterSwapCast = CardCastExecutor.Execute(nextTurn, ShieldCard);
        Assert.True(afterSwapCast.IsAccepted);
        Assert.Equal(1, afterSwapCast.State.CardCastsUsedThisTurn);
    }

    [Fact]
    public void RejectedSwap_DoesNotRestoreTheTurnAllowance()
    {
        // Scenario 5: a rejected Swap begins no Turn (MATCH3_RULES.md §2.1.5
        // item 2), so it neither advances Turn nor restores the allowance. A
        // player cannot farm extra casts by submitting invalid swaps.
        var state = Battle();

        var cast = CardCastExecutor.Execute(state, HealCard);
        Assert.True(cast.IsAccepted);
        Assert.Equal(1, cast.State.CardCastsUsedThisTurn);

        // Cell 0 and cell 1 are adjacent but produce no Match, so the Swap is
        // rejected (MATCH3_RULES.md §2.1.2 item 4).
        var rejectedSwap = SwapExecutor.Execute(cast.State, new SwapRequest(0, 1));
        Assert.True(rejectedSwap.IsRejected);

        // A rejected Swap returns no state at all, so the allowance is provably
        // unchanged: the same rejected rule still blocks the next cast.
        var stillBlocked = CardCastExecutor.Execute(cast.State, ShieldCard);
        Assert.True(stillBlocked.IsRejected);
        Assert.Equal(CardCastRejectionReason.CardCastAlreadyUsedThisTurn, stillBlocked.Reason);

        // Turn is likewise unchanged on the pre-rejection state.
        Assert.Equal(cast.State.Turn, state.Turn);
    }

    [Fact]
    public void MultipleCommittedSwaps_EachRestoreExactlyOneCast()
    {
        // The allowance is per committed Turn, not per battle: two committed Turns
        // permit exactly two successful casts in total, one each.
        var state = Battle();

        var first = CardCastExecutor.Execute(state, HealCard);
        Assert.True(first.IsAccepted);

        var blocked = CardCastExecutor.Execute(first.State, HealCard);
        Assert.True(blocked.IsRejected);

        var swap1 = SwapExecutor.Execute(first.State, new SwapRequest(From, To));
        Assert.True(swap1.IsAccepted);

        // Re-arm the completing swap on the new board: the pair just committed
        // cannot be replayed (MATCH3_RULES.md §2.1.4's already-applied check), and
        // the exchange moved the fixture's gems, so the board is restored to the
        // fixture as a fresh authoritative input for the second Turn.
        var secondTurn = swap1.State with { BoardState = MatchingBoard() };
        var second = CardCastExecutor.Execute(secondTurn, ShieldCard);
        Assert.True(second.IsAccepted);
        Assert.Equal(1, second.State.CardCastsUsedThisTurn);

        // And the second Turn's allowance is spent just the same.
        var blockedAgain = CardCastExecutor.Execute(second.State, HealCard);
        Assert.True(blockedAgain.IsRejected);
        Assert.Equal(CardCastRejectionReason.CardCastAlreadyUsedThisTurn, blockedAgain.Reason);
    }

    [Fact]
    public void CommittedSwap_DoesNotGrandfatherMoreThanOneCastPerTurn()
    {
        // The reset is exactly one cast's worth: it returns the counter to the
        // documented initial value rather than accumulating a budget across Turns.
        var state = Battle();

        var cast = CardCastExecutor.Execute(state, HealCard);
        Assert.True(cast.IsAccepted);

        var swap = SwapExecutor.Execute(cast.State, new SwapRequest(From, To));
        Assert.True(swap.IsAccepted);

        Assert.Equal(BattleState.InitialCardCastsUsedThisTurn, swap.State.CardCastsUsedThisTurn);
        Assert.Equal(0, BattleState.InitialCardCastsUsedThisTurn);
    }
}
