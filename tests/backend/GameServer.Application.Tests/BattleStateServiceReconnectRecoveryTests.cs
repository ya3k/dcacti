using GameServer.Application.Battle;
using GameServer.Domain.Battle;
using GameServer.Domain.Bosses;
using GameServer.Domain.Cards;
using GameServer.Domain.Elements;
using GameServer.Domain.Passives;
using GameServer.Domain.Pets;
using GameServer.Domain.Players;
using Xunit;

namespace GameServer.Application.Tests;

/// <summary>
/// Application-layer tests for the reconnect/resync snapshot read —
/// <see cref="BattleStateService.GetOwnedBattleStateAsync"/>
/// (<c>SIGNALR_PROTOCOL.md</c> §7, <c>ADR-008</c>, <c>GAME_STATE.md</c> §5.3).
///
/// <code>
/// authenticated caller PlayerId
///         ↓
/// active-state read                     (REDIS_STATE.md §2 item 2)
///         ↓
/// ownership comparison                  (GAME_STATE.md §2.8, API_CONTRACTS.md §4 note 7)
///         ↓
/// snapshot, or one indistinguishable absence
/// </code>
///
/// <b>What is asserted here.</b> The ownership rule (§2.8 / §4 note 7), the
/// "unknown, expired, and foreign are one answer" rule (§7.3,
/// <c>REDIS_STATE.md</c> §3), and the rule that recovery <b>reads</b>: it takes no
/// <c>Sequence</c>, performs no write, and resets no TTL (<c>GAME_STATE.md</c>
/// §5.1 item 1). Projection shape is asserted in the API suite, where the actual
/// wire JSON is produced.
/// </summary>
public sealed class BattleStateServiceReconnectRecoveryTests
{
    private static readonly PlayerId Owner = new("player_recovery_owner");
    private static readonly PlayerId Foreigner = new("player_recovery_foreigner");

    private static BattleStateService.PetConfiguration PetConfig() => new(
        new PetId("pet_instance_recovery_1"),
        Element.Hoa,
        new PassiveId("xich-lang"),
        PassiveThreshold: 5);

    /// <summary>
    /// Given: a battle owned by <see cref="Owner"/>, stored in the active-state
    /// record.
    /// When: the owner asks for the recovery snapshot.
    /// Then: the authoritative record is returned unchanged
    /// (<c>SIGNALR_PROTOCOL.md</c> §7.1 — the snapshot plus its <c>Sequence</c>).
    /// </summary>
    [Fact]
    public async Task GetOwnedBattleState_ReturnsSnapshot_ForOwningCaller()
    {
        var repository = new InMemoryBattleStateRepository();
        var service = new BattleStateService(repository, new FixedRngSeedSource());

        var created = await service.CreateBattleAsync(
            "battle-recovery-owner", Owner, PetConfig(), BossDefinitions.HoaLong);

        var recovered = await service.GetOwnedBattleStateAsync("battle-recovery-owner", Owner.Value);

        Assert.NotNull(recovered);
        Assert.Equal(created.BattleId, recovered!.BattleId);
        Assert.Equal(created.Sequence, recovered.Sequence);

        // §5.3 item 1: everything a resolution produces is BattleState, so the
        // snapshot is the whole authoritative value — the board, the RNG pair, and
        // the root counters included — and not a reduced subset.
        Assert.Equal(created.Turn, recovered.Turn);
        Assert.Equal(created.RngSeed, recovered.RngSeed);
        Assert.Equal(created.RngState, recovered.RngState);
        Assert.Equal(created.BoardState, recovered.BoardState);
        Assert.Equal(created.Combo, recovered.Combo);
        Assert.Equal(created.MatchCount, recovered.MatchCount);
        Assert.Equal(created.PetState, recovered.PetState);
    }

    /// <summary>
    /// Given: a battle owned by <see cref="Owner"/>.
    /// When: a different authenticated Player asks for it.
    /// Then: nothing is returned, and the answer is identical to the answer for a
    /// battle that does not exist (<c>API_CONTRACTS.md</c> §4 note 7) — so the
    /// caller cannot learn that another Player's battle exists.
    /// </summary>
    [Fact]
    public async Task GetOwnedBattleState_ReturnsNull_ForForeignCaller()
    {
        var repository = new InMemoryBattleStateRepository();
        var service = new BattleStateService(repository, new FixedRngSeedSource());

        await service.CreateBattleAsync(
            "battle-recovery-foreign", Owner, PetConfig(), BossDefinitions.HoaLong);

        var foreign = await service.GetOwnedBattleStateAsync("battle-recovery-foreign", Foreigner.Value);

        Assert.Null(foreign);
    }

    /// <summary>
    /// Given: a battle id with no stored record.
    /// When: an authenticated caller asks for it.
    /// Then: the same absence a foreign battle reports — the documented
    /// "unknown or expired" answer (<c>SIGNALR_PROTOCOL.md</c> §7.3,
    /// <c>REDIS_STATE.md</c> §3), not a defaulted or empty state.
    /// </summary>
    [Fact]
    public async Task GetOwnedBattleState_ReturnsNull_ForUnknownBattle()
    {
        var repository = new InMemoryBattleStateRepository();
        var service = new BattleStateService(repository, new FixedRngSeedSource());

        var unknown = await service.GetOwnedBattleStateAsync("battle-recovery-absent", Owner.Value);

        Assert.Null(unknown);
    }

    /// <summary>
    /// Given: a battle whose record has been cleared — the store's observable
    /// equivalent of a key that expired from inactivity, since
    /// <c>REDIS_STATE.md</c> §3 makes an expired key and a deleted key the same
    /// absence to a reader.
    /// When: the owning caller asks for the recovery snapshot.
    /// Then: absence is reported, and the recovery path neither re-creates the
    /// record nor invents a state for it (<c>REDIS_STATE.md</c> §2 item 2: a
    /// missing record is never repaired by creating a battle).
    /// </summary>
    /// <remarks>
    /// The TTL itself is not set or shortened here: production TTL is
    /// <c>REDIS_STATE.md</c> §3's documented value and no test may change it. The
    /// real sliding expiry is exercised against a live Redis in
    /// <c>RedisBattleStateRepositoryTests</c> and the API hub suite.
    /// </remarks>
    [Fact]
    public async Task GetOwnedBattleState_ReturnsNull_WhenRecordNoLongerExists()
    {
        var repository = new InMemoryBattleStateRepository();
        var service = new BattleStateService(repository, new FixedRngSeedSource());

        await service.CreateBattleAsync(
            "battle-recovery-expired", Owner, PetConfig(), BossDefinitions.HoaLong);

        await repository.DeleteAsync("battle-recovery-expired");

        var expired = await service.GetOwnedBattleStateAsync("battle-recovery-expired", Owner.Value);

        Assert.Null(expired);

        // The read left the store exactly as it found it: one fewer record than a
        // re-creation would produce, and no write of any kind.
        Assert.Equal(0, repository.RecordCount);
        Assert.Equal(1, repository.WriteCount);
    }

    /// <summary>
    /// Given: a battle owned by <see cref="Owner"/>, already at a committed
    /// <c>Sequence</c>.
    /// When: the owner requests the recovery snapshot.
    /// Then: recovery writes nothing at all — no record write, no
    /// <c>Sequence</c> increment, no TTL refresh. Recovery is a read
    /// (<c>GAME_STATE.md</c> §5.1 item 1; <c>REDIS_STATE.md</c> §4 item 5's one
    /// write-back belongs to a resolution, not to a snapshot request).
    /// </summary>
    [Fact]
    public async Task GetOwnedBattleState_PerformsNoWrite_AndDoesNotAdvanceSequence()
    {
        var repository = new InMemoryBattleStateRepository();
        var service = new BattleStateService(repository, new FixedRngSeedSource());

        var created = await service.CreateBattleAsync(
            "battle-recovery-readonly", Owner, PetConfig(), BossDefinitions.HoaLong);

        var writesAfterCreation = repository.WriteCount;

        var recovered = await service.GetOwnedBattleStateAsync("battle-recovery-readonly", Owner.Value);

        Assert.NotNull(recovered);
        Assert.Equal(writesAfterCreation, repository.WriteCount);

        // Sequence is unchanged in the stored record too — not merely unchanged in
        // the returned value.
        var stored = await repository.GetAsync("battle-recovery-readonly");
        Assert.NotNull(stored);
        Assert.Equal(created.Sequence, stored!.Sequence);
        Assert.Equal(0, stored.Sequence);
    }

    /// <summary>
    /// Given: the same battle, read by its owner and then by a foreign caller.
    /// When: both requests are made against the one stored record.
    /// Then: the owner receives it and the foreign caller receives the identical
    /// absence an unknown battle produces — the two rejections are
    /// indistinguishable from each other and from "no such battle"
    /// (<c>API_CONTRACTS.md</c> §4 notes 6–7).
    /// </summary>
    [Fact]
    public async Task GetOwnedBattleState_ForeignAndUnknown_AreIndistinguishable()
    {
        var repository = new InMemoryBattleStateRepository();
        var service = new BattleStateService(repository, new FixedRngSeedSource());

        await service.CreateBattleAsync(
            "battle-recovery-indistinct", Owner, PetConfig(), BossDefinitions.HoaLong);

        var foreign = await service.GetOwnedBattleStateAsync("battle-recovery-indistinct", Foreigner.Value);
        var unknown = await service.GetOwnedBattleStateAsync("battle-recovery-never-created", Foreigner.Value);

        Assert.Null(foreign);
        Assert.Null(unknown);

        // ...while the owner still recovers the very same record.
        Assert.NotNull(await service.GetOwnedBattleStateAsync("battle-recovery-indistinct", Owner.Value));
    }

    /// <summary>
    /// The battle id and the caller identity are both required: an absent battle
    /// id names no battle, and an absent identity could only be satisfied by
    /// trusting the request, which <c>API_CONTRACTS.md</c> §4 note 7 forbids.
    /// </summary>
    [Theory]
    [InlineData("", "player_recovery_owner")]
    [InlineData("   ", "player_recovery_owner")]
    [InlineData("battle-recovery-owner", "")]
    [InlineData("battle-recovery-owner", "   ")]
    public async Task GetOwnedBattleState_RejectsAbsentBattleIdOrIdentity(
        string battleId,
        string callerPlayerId)
    {
        var repository = new InMemoryBattleStateRepository();
        var service = new BattleStateService(repository, new FixedRngSeedSource());

        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => service.GetOwnedBattleStateAsync(battleId, callerPlayerId));
    }
}
