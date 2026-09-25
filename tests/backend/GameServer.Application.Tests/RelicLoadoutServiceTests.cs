using GameServer.Application.Relics;
using GameServer.Domain.Relics;

namespace GameServer.Application.Tests;

/// <summary>
/// The Relic loadout contract — <c>RELIC_RULES.md</c> §2.1–§2.5,
/// <c>API_CONTRACTS.md</c> §3 (TASK-027).
///
/// These tests assert the gameplay contract TASK-038 resolved:
/// 3–5 owned instances (count), owned by the requesting Player (ownership),
/// with no repeated <c>RelicInstanceId</c> (distinctness), and with the
/// submitted request order preserved as equip-slot order.
///
/// Every rejection maps to the one documented <c>INVALID_LOADOUT</c> outcome;
/// no distinct wire code is introduced.
/// </summary>
public class RelicLoadoutServiceTests
{
    private const string Owner = "player_1";
    private const string OtherPlayer = "player_2";

    private static RelicLoadoutService CreateService(IDictionary<string, string> ownedByPlayer)
    {
        return new RelicLoadoutService(new FakeRelicRepository(ownedByPlayer));
    }

    /// <summary>
    /// The ownership read the validator depends on, backed by an in-memory
    /// map of <c>RelicInstanceId → PlayerId</c>. It deliberately returns rows
    /// in the <b>reverse</b> of any plausible definition or id order, so a
    /// validator that used the repository's row order instead of the request
    /// order cannot pass the ordering tests by accident.
    /// </summary>
    private sealed class FakeRelicRepository : IRelicRepository
    {
        private readonly IDictionary<string, string> _ownerByInstanceId;

        public FakeRelicRepository(IDictionary<string, string> ownerByInstanceId)
        {
            _ownerByInstanceId = ownerByInstanceId;
        }

        public Task AddAsync(Relic relic, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The loadout validator never writes ownership rows.");

        public Task AddDefinitionAsync(
            RelicDefinition definition,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The loadout validator never writes definition rows.");

        public Task<IReadOnlyList<Relic>> ListByPlayerIdAsync(
            string playerId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Relic>>(
                _ownerByInstanceId
                    .Where(pair => pair.Value == playerId)
                    .OrderByDescending(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => NewRelic(pair.Key, pair.Value))
                    .ToList());

        public Task<IReadOnlyList<Relic>> ListOwnedInstancesAsync(
            string playerId,
            IReadOnlyCollection<string> relicInstanceIds,
            CancellationToken cancellationToken = default) =>
            // Deliberately reverse-sorted: order here is not a contract
            // (RELIC_RULES.md §2.3), and the validator must not depend on it.
            Task.FromResult<IReadOnlyList<Relic>>(
                _ownerByInstanceId
                    .Where(pair => pair.Value == playerId)
                    .Where(pair => relicInstanceIds.Contains(pair.Key))
                    .OrderByDescending(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => NewRelic(pair.Key, pair.Value))
                    .ToList());

        public Task<RelicDefinition?> GetDefinitionAsync(
            string relicDefinitionId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<RelicDefinition?>(null);

        private static Relic NewRelic(string instanceId, string playerId) => new()
        {
            RelicInstanceId = instanceId,
            PlayerId = playerId,
            RelicDefinitionId = "relic_def_1",
            AcquiredAt = DateTimeOffset.UtcNow,
        };
    }

    private static Dictionary<string, string> Owned(params string[] instanceIds) =>
        instanceIds.ToDictionary(id => id, _ => Owner, StringComparer.Ordinal);

    private static string[] SnapshotValues(RelicLoadoutValidation result) =>
        result.EquippedRelics.Select(identity => identity.Value).ToArray();

    // -----------------------------------------------------------------------
    // Count — RELIC_RULES.md §2.1 item 1 (3–5)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Validate_ShouldAcceptThreeRelics()
    {
        var service = CreateService(Owned("R1", "R2", "R3"));

        var result = await service.ValidateAsync(Owner, new[] { "R1", "R2", "R3" });

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validate_ShouldAcceptFourRelics()
    {
        var service = CreateService(Owned("R1", "R2", "R3", "R4"));

        var result = await service.ValidateAsync(Owner, new[] { "R1", "R2", "R3", "R4" });

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validate_ShouldAcceptFiveRelics()
    {
        var service = CreateService(Owned("R1", "R2", "R3", "R4", "R5"));

        var result = await service.ValidateAsync(
            Owner,
            new[] { "R1", "R2", "R3", "R4", "R5" });

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(6)]
    [InlineData(7)]
    public async Task Validate_ShouldRejectCountsOutsideThreeToFive(int count)
    {
        // RELIC_RULES.md §2.1 item 1: the documented bound is 3–5.
        // API_CONTRACTS.md §3: relicLoadout must be 3–5 Relics.
        var ids = Enumerable.Range(1, count).Select(i => $"R{i}").ToArray();
        var service = CreateService(Owned(ids));

        var result = await service.ValidateAsync(Owner, ids);

        Assert.False(result.IsValid);
        Assert.Equal(RelicLoadoutRejectionReason.CountOutOfRange, result.Reason);
        Assert.Empty(result.EquippedRelics);
    }

    [Fact]
    public async Task Validate_ShouldRejectAnEmptySelection()
    {
        var service = CreateService(Owned());

        var result = await service.ValidateAsync(Owner, Array.Empty<string>());

        Assert.False(result.IsValid);
        Assert.Equal(RelicLoadoutRejectionReason.CountOutOfRange, result.Reason);
    }

    // -----------------------------------------------------------------------
    // Ownership — RELIC_RULES.md §2.1 item 2
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Validate_ShouldAcceptWhenEveryRelicIsOwnedByThePlayer()
    {
        var service = CreateService(Owned("R1", "R2", "R3"));

        var result = await service.ValidateAsync(Owner, new[] { "R1", "R2", "R3" });

        Assert.True(result.IsValid);
        Assert.Equal(RelicLoadoutRejectionReason.None, result.Reason);
    }

    [Fact]
    public async Task Validate_ShouldRejectARelicOwnedByAnotherPlayer()
    {
        // RELIC_RULES.md §2.1 item 2: every selected instance must be owned by
        // the requesting Player. The server establishes this from persistence,
        // not from a client claim (GAME_RULES.md §18, ADR-001).
        var owned = Owned("R1", "R2", "R3");
        owned["R9"] = OtherPlayer;
        var service = CreateService(owned);

        var result = await service.ValidateAsync(Owner, new[] { "R1", "R2", "R9" });

        Assert.False(result.IsValid);
        Assert.Equal(RelicLoadoutRejectionReason.NotOwned, result.Reason);
        Assert.Empty(result.EquippedRelics);
    }

    [Fact]
    public async Task Validate_ShouldRejectAnUnknownRelicInstance()
    {
        // An identity that does not exist is not owned by anyone.
        var service = CreateService(Owned("R1", "R2", "R3"));

        var result = await service.ValidateAsync(Owner, new[] { "R1", "R2", "R404" });

        Assert.False(result.IsValid);
        Assert.Equal(RelicLoadoutRejectionReason.NotOwned, result.Reason);
    }

    [Fact]
    public async Task Validate_ShouldRejectTheWholeSelectionWhenOneRelicIsForeign()
    {
        // A rejection equips nothing (RELIC_RULES.md §2.5) — the valid
        // members of a partially-invalid selection are not equipped.
        var owned = Owned("R1", "R2", "R3");
        owned["R8"] = OtherPlayer;
        var service = CreateService(owned);

        var result = await service.ValidateAsync(Owner, new[] { "R1", "R2", "R3", "R8" });

        Assert.False(result.IsValid);
        Assert.Equal(RelicLoadoutRejectionReason.NotOwned, result.Reason);
        Assert.Empty(result.EquippedRelics);
    }

    // -----------------------------------------------------------------------
    // Duplicates — RELIC_RULES.md §2.4 items 1–4
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Validate_ShouldRejectTheSameInstanceTwice()
    {
        // RELIC_RULES.md §2.4 item 1: a RelicInstanceId may occupy at most one
        // slot. [A, B, A] is invalid.
        var service = CreateService(Owned("R1", "R2"));

        var result = await service.ValidateAsync(Owner, new[] { "R1", "R2", "R1" });

        Assert.False(result.IsValid);
        Assert.Equal(RelicLoadoutRejectionReason.DuplicateInstance, result.Reason);
        Assert.Empty(result.EquippedRelics);
    }

    [Fact]
    public async Task Validate_ShouldRejectAFullyRepeatedSelection()
    {
        var service = CreateService(Owned("R1"));

        var result = await service.ValidateAsync(Owner, new[] { "R1", "R1", "R1" });

        Assert.False(result.IsValid);
        Assert.Equal(RelicLoadoutRejectionReason.DuplicateInstance, result.Reason);
    }

    [Fact]
    public async Task Validate_ShouldNotCollapseADuplicateIntoARangeCount()
    {
        // RELIC_RULES.md §2.4 item 4: a duplicate is rejected, never merged or
        // silently collapsed, so it cannot reduce an otherwise out-of-range
        // selection into range. [A, A] is 2 elements — still out of range —
        // and is rejected as a count violation, not silently accepted as 1.
        var service = CreateService(Owned("R1"));

        var result = await service.ValidateAsync(Owner, new[] { "R1", "R1" });

        Assert.False(result.IsValid);
        Assert.Equal(RelicLoadoutRejectionReason.CountOutOfRange, result.Reason);
    }

    [Fact]
    public async Task Validate_ShouldAcceptDistinctInstancesOfTheSameDefinition()
    {
        // RELIC_RULES.md §2.4 item 3: two DISTINCT owned instances that
        // reference the same RelicDefinitionId may be equipped together. The
        // fake repository gives every instance the same definition id, so
        // this selection is exactly that case.
        var service = CreateService(Owned("R1", "R2", "R3"));

        var result = await service.ValidateAsync(Owner, new[] { "R1", "R2", "R3" });

        Assert.True(result.IsValid);
        Assert.Equal(3, result.EquippedRelics.Length);
    }

    // -----------------------------------------------------------------------
    // Order preservation — RELIC_RULES.md §2.3, §2.5
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Validate_ShouldPreserveTheSubmittedOrderAsSlotOrder()
    {
        // RELIC_RULES.md §2.3: slot index = request array position + 1.
        // The repository returns these ids in reverse order, so this asserts
        // the validator rebuilds the snapshot from the request sequence rather
        // than from the ownership read's order.
        var service = CreateService(Owned("R1", "R3", "R5"));

        var result = await service.ValidateAsync(Owner, new[] { "R3", "R1", "R5" });

        Assert.True(result.IsValid);
        Assert.Equal(new[] { "R3", "R1", "R5" }, SnapshotValues(result));
    }

    [Fact]
    public async Task Validate_ShouldNotSortByInstanceId()
    {
        // RELIC_RULES.md §2.3 item 2: sorting by RelicInstanceId is forbidden.
        var service = CreateService(Owned("R1", "R2", "R3"));

        var result = await service.ValidateAsync(Owner, new[] { "R3", "R2", "R1" });

        Assert.Equal(new[] { "R3", "R2", "R1" }, SnapshotValues(result));
    }

    [Fact]
    public async Task Validate_ShouldPreserveOrderForAFiveRelicSelection()
    {
        var service = CreateService(Owned("R1", "R2", "R3", "R4", "R5"));

        var result = await service.ValidateAsync(
            Owner,
            new[] { "R5", "R4", "R3", "R2", "R1" });

        Assert.Equal(new[] { "R5", "R4", "R3", "R2", "R1" }, SnapshotValues(result));
    }

    [Fact]
    public async Task Validate_ShouldMapElementIndexToSlot()
    {
        // slot 1 → R3, slot 2 → R1, slot 3 → R5 (RELIC_RULES.md §2.3, §2.5).
        var service = CreateService(Owned("R1", "R3", "R5"));

        var result = await service.ValidateAsync(Owner, new[] { "R3", "R1", "R5" });

        Assert.Equal("R3", result.EquippedRelics[0].Value);
        Assert.Equal("R1", result.EquippedRelics[1].Value);
        Assert.Equal("R5", result.EquippedRelics[2].Value);
    }

    // -----------------------------------------------------------------------
    // Snapshot identity — RELIC_RULES.md §2.2, GAME_STATE.md §2.3
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Validate_ShouldReturnInstanceIdentitiesNotDefinitions()
    {
        // RELIC_RULES.md §2.2: each element is one owned Relic INSTANCE
        // identity, not a RelicDefinitionId and not resolved definition data.
        var service = CreateService(Owned("R1", "R2", "R3"));

        var result = await service.ValidateAsync(Owner, new[] { "R1", "R2", "R3" });

        Assert.Equal(typeof(EquippedRelicIdentity[]), result.EquippedRelics.GetType());
        Assert.Equal(new[] { "R1", "R2", "R3" }, SnapshotValues(result));
    }

    // -----------------------------------------------------------------------
    // Server authority — GAME_RULES.md §18, ADR-001
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Validate_ShouldRequireARequestingPlayer()
    {
        // Without a requesting identity ownership cannot be established, and
        // defaulting it would be a client-authoritative claim.
        var service = CreateService(Owned("R1", "R2", "R3"));

        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => service.ValidateAsync("  ", new[] { "R1", "R2", "R3" }));
    }

    [Fact]
    public async Task Validate_ShouldScopeToTheOwningPlayer()
    {
        // The same instance ids are valid for their owner and invalid for
        // anyone else — ownership is resolved server-side per Player.
        var owned = Owned("R1", "R2", "R3");
        var service = CreateService(owned);

        var asOwner = await service.ValidateAsync(Owner, new[] { "R1", "R2", "R3" });
        var asOther = await service.ValidateAsync(OtherPlayer, new[] { "R1", "R2", "R3" });

        Assert.True(asOwner.IsValid);
        Assert.False(asOther.IsValid);
        Assert.Equal(RelicLoadoutRejectionReason.NotOwned, asOther.Reason);
    }

    // -----------------------------------------------------------------------
    // Documented constants
    // -----------------------------------------------------------------------

    [Fact]
    public void LoadoutBounds_ShouldBeTheDocumentedThreeToFive()
    {
        // RELIC_RULES.md §2.1 item 1, GAME_RULES.md §13.3,
        // GAME_STATE.md §2.3 ("3–5").
        Assert.Equal(3, RelicLoadoutService.MinLoadoutSize);
        Assert.Equal(5, RelicLoadoutService.MaxLoadoutSize);
    }
}
