using GameServer.Application.Battle;
using Xunit;

namespace GameServer.Application.Tests;

/// <summary>
/// Battle State Foundation lifecycle tests (GAME_STATE.md §2.0.4).
///
/// The Application layer owns the lifecycle of the authoritative foundation
/// state: it creates it and hands it to the realtime boundary on group join. It
/// performs no gameplay calculation and defines no Turn increment rule —
/// GAME_STATE.md §2.0.2 leaves that to the task that implements action
/// resolution.
/// </summary>
public class BattleStateServiceTests
{
    [Fact]
    public void CreateBattle_ShouldProduceTheDocumentedInitialState()
    {
        var service = new BattleStateService();

        var state = service.CreateBattle("battle-1");

        Assert.Equal("battle-1", state.BattleId);
        Assert.Equal(0, state.Turn);
        Assert.Equal(0, state.Sequence);
    }

    [Fact]
    public void CreatedState_ShouldBeOwnedAndRetrievableByTheServerRuntime()
    {
        // GAME_STATE.md §2.0.4.1: Foundation State is authoritative server
        // state, produced and held server-side.
        var service = new BattleStateService();
        service.CreateBattle("battle-1");

        var retrieved = service.GetBattle("battle-1");

        Assert.NotNull(retrieved);
        Assert.Equal("battle-1", retrieved!.BattleId);
        Assert.Equal(0, retrieved.Turn);
        Assert.Equal(0, retrieved.Sequence);
    }

    [Fact]
    public void GetBattle_ForUnknownBattle_ShouldReturnNull()
    {
        var service = new BattleStateService();

        Assert.Null(service.GetBattle("never-created"));
    }

    [Fact]
    public void GetInitialStateForGroup_ShouldReturnTheCreatedFoundationState()
    {
        // SIGNALR_PROTOCOL.md §4.1: the state delivered when the client joins
        // the battle group is the current authoritative state.
        var service = new BattleStateService();
        service.CreateBattle("battle-1");

        var delivered = service.GetInitialStateForGroup("battle-1");

        Assert.NotNull(delivered);
        Assert.Equal("battle-1", delivered!.BattleId);
        Assert.Equal(0, delivered.Turn);
        Assert.Equal(0, delivered.Sequence);
    }

    [Fact]
    public void GetInitialStateForGroup_ForUnknownBattle_ShouldReturnNull()
    {
        var service = new BattleStateService();

        Assert.Null(service.GetInitialStateForGroup("never-created"));
    }

    [Fact]
    public void RepeatedReads_ShouldNotAlterTheAuthoritativeState()
    {
        // Foundation State has no resolutions (GAME_STATE.md §2.0.2), so no
        // read, join, or repeat delivery may change Turn or Sequence.
        var service = new BattleStateService();
        service.CreateBattle("battle-1");

        service.GetBattle("battle-1");
        service.GetInitialStateForGroup("battle-1");
        var second = service.GetInitialStateForGroup("battle-1");

        Assert.Equal(0, second!.Turn);
        Assert.Equal(0, second.Sequence);
        Assert.Equal(1, service.ActiveBattleCount);
    }

    [Fact]
    public void CreateBattle_ShouldTrackEachBattleIdSeparately()
    {
        var service = new BattleStateService();

        service.CreateBattle("battle-1");
        service.CreateBattle("battle-2");

        Assert.Equal("battle-1", service.GetBattle("battle-1")!.BattleId);
        Assert.Equal("battle-2", service.GetBattle("battle-2")!.BattleId);
        Assert.Equal(2, service.ActiveBattleCount);
    }

    [Fact]
    public void CreateBattle_ShouldRejectAnEmptyBattleId()
    {
        var service = new BattleStateService();

        Assert.Throws<ArgumentException>(() => service.CreateBattle(string.Empty));
    }

    [Fact]
    public void GetBattle_ShouldRejectAnEmptyBattleId()
    {
        var service = new BattleStateService();

        Assert.Throws<ArgumentException>(() => service.GetBattle(string.Empty));
    }
}