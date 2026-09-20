using GameServer.Domain.Battle;
using Xunit;

namespace GameServer.Domain.Tests;

/// <summary>
/// Battle State Foundation contract tests (GAME_STATE.md §2.0).
///
/// These verify the documented field set and initial values only. The
/// foundation has no gameplay: no board, no gems, no combat, no Pet, Boss,
/// Card, or Relic state exists here and none is added.
/// </summary>
public class BattleStateTests
{
    [Fact]
    public void Create_ShouldProduceDocumentedInitialState()
    {
        // GAME_STATE.md §2.0.2: Turn = 0, Sequence = 0.
        var state = BattleState.Create("battle-1");

        Assert.Equal("battle-1", state.BattleId);
        Assert.Equal(0, state.Turn);
        Assert.Equal(0, state.Sequence);
    }

    [Fact]
    public void Create_ShouldMatchTheDocumentedInitialValueConstants()
    {
        var state = BattleState.Create("battle-1");

        Assert.Equal(BattleState.InitialTurn, state.Turn);
        Assert.Equal(BattleState.InitialSequence, state.Sequence);
        Assert.Equal(0, BattleState.InitialTurn);
        Assert.Equal(0, BattleState.InitialSequence);
    }

    [Fact]
    public void FoundationState_ShouldCarryExactlyTheDocumentedFields()
    {
        // GAME_STATE.md §2.0: BattleId, Turn, Sequence — nothing else.
        var properties = typeof(BattleState)
            .GetProperties()
            .Select(p => p.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(new[] { "BattleId", "Sequence", "Turn" }, properties);
    }

    [Fact]
    public void FoundationState_ShouldDeclareNoStatusOrLifecycleField()
    {
        // GAME_STATE.md §2.0.3: §2.0 contains no Status field, and no
        // READY/STARTING/ACTIVE/PAUSED/FINISHED/WON/LOST lifecycle enum exists
        // anywhere in this foundation type.
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
    public void FoundationState_ShouldDeclareNoGameplayStateField()
    {
        // GAME_STATE.md §2.0.3: the remaining §2 fields are not present in
        // §2.0 — they are gameplay systems that do not exist yet.
        var declared = typeof(BattleState)
            .GetProperties()
            .Select(p => p.Name)
            .Concat(typeof(BattleState).GetFields().Select(f => f.Name))
            .ToArray();

        foreach (var futureField in new[]
                 {
                     "BoardState", "PlayerState", "PetState", "BossState",
                     "RngSeed", "RngState",
                 })
        {
            Assert.DoesNotContain(futureField, declared);
        }
    }

    [Fact]
    public void Create_ShouldScopesStateToTheRequestedBattleId()
    {
        // GAME_STATE.md §2.0.1: BattleId identifies the battle session and
        // carries no gameplay content.
        var first = BattleState.Create("battle-1");
        var second = BattleState.Create("battle-2");

        Assert.NotEqual(first.BattleId, second.BattleId);
        Assert.Equal(0, first.Turn);
        Assert.Equal(0, second.Turn);
    }

    [Fact]
    public void Create_ShouldRejectAnEmptyBattleId()
    {
        Assert.Throws<ArgumentException>(() => BattleState.Create(string.Empty));
        Assert.Throws<ArgumentException>(() => BattleState.Create("   "));
    }

    [Fact]
    public void Create_ShouldProduceAnImmutableSnapshot()
    {
        // State is server-authored (GAME_RULES.md §18, ADR-001): a caller
        // cannot mutate a snapshot in place.
        var state = BattleState.Create("battle-1");

        Assert.IsType<BattleState>(state with { Turn = state.Turn }, exactMatch: false);
        Assert.Equal(0, state.Turn);
    }
}