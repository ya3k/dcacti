using System.Collections.Concurrent;
using GameServer.Application.Battle;
using GameServer.Domain.Battle;
using GameServer.Domain.Bosses;
using GameServer.Domain.Players;

namespace GameServer.Application.Tests;

/// <summary>
/// An in-memory <see cref="IBattleResultRepository"/> for the Application-layer
/// unit tests.
///
/// <b>It is a TEST DOUBLE, not a production fallback.</b> The real store is
/// <c>GameServer.Infrastructure.Postgres.Repositories.BattleResultRepository</c>
/// against PostgreSQL (<c>DATABASE.md</c> §1), and the battle-end path's
/// durable-write semantics — including the fail-closed behaviour — belong to
/// that boundary. This type exists so a test can run the real
/// <see cref="BattleResultService"/> over a store it can inspect and make fail.
///
/// <b>It models the two documented semantics the battle-end path depends on</b>,
/// so a test cannot pass against a double more permissive than the contract:
/// <list type="bullet">
/// <item><b>The key is the battle's own id</b> (<c>DATABASE.md</c> §1 sourcing
/// item 1): a second write for one battle replaces the stored row rather than
/// appending, exactly as the primary key makes a second row impossible — so a
/// test can prove a repeated terminal persistence still yields one row.</item>
/// <item><b>Failure is raised</b> (<c>DATABASE.md</c> §1 sourcing item 3): when
/// configured to fail, the write throws instead of silently succeeding, which is
/// what lets a test prove the active state is left intact.</item>
/// </list>
/// </summary>
internal sealed class InMemoryBattleResultRepository : IBattleResultRepository
{
    private readonly ConcurrentDictionary<string, BattleResult> _rows = new(StringComparer.Ordinal);

    /// <summary>The rows currently stored, in insertion order.</summary>
    public IReadOnlyCollection<BattleResult> Rows => _rows.Values.ToArray();

    /// <summary>
    /// The number of <see cref="AddAsync"/> calls this double has received,
    /// including refused ones — so a test can assert that a non-terminal
    /// resolution attempted no write at all.
    /// </summary>
    public int WriteAttempts { get; private set; }

    /// <summary>
    /// When set, <see cref="AddAsync"/> raises — the documented PostgreSQL failure
    /// on the terminal path (<c>DATABASE.md</c> §1 sourcing item 3), which must
    /// leave the Redis record in place.
    /// </summary>
    public bool WriteFails { get; set; }

    /// <summary>
    /// When set, the caller's cancellation token is treated as cancelled, so a
    /// test can drive the cancelled path without racing a real timer.
    /// </summary>
    public bool WriteCancelled { get; set; }

    /// <inheritdoc />
    public Task AddAsync(BattleResult result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);

        WriteAttempts++;

        if (WriteFails)
        {
            throw new InvalidOperationException(
                "DATABASE.md §1: this double is configured to fail the durable battle-end write.");
        }

        if (WriteCancelled)
        {
            throw new OperationCanceledException(cancellationToken);
        }

        // DATABASE.md §1 sourcing item 1: one row per battle, keyed by the
        // battle's own BattleId — a repeat replaces rather than appends.
        _rows[result.BattleResultId] = result;

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<BattleResult?> GetByIdAsync(
        string battleResultId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(battleResultId);

        return Task.FromResult(
            _rows.TryGetValue(battleResultId, out var row) ? row : null);
    }
}

/// <summary>
/// A scripted <see cref="IBossDefinitionLookup"/> for the Application-layer unit
/// tests.
///
/// <b>It is a TEST DOUBLE for the battle-end lookup</b> that
/// <c>DATABASE.md</c> §1 note item 2 assigns to Infrastructure. It resolves the
/// Identity through the same content-derived mapping the provisioned rows carry
/// (<c>DATABASE.md</c> §1 note item 5 "Row set": <c>boss-hoa-long</c> ↔
/// <c>boss-def-hoa-long</c>), so a resolved value is a real canonical pair
/// rather than an invented string — and it can be pointed at an empty mapping to
/// drive the documented unresolved-definition path.
/// </summary>
internal sealed class ScriptedBossDefinitionLookup : IBossDefinitionLookup
{
    /// <summary>
    /// The Identity → persistence-key pairs this double resolves. Empty by
    /// default, which is the "no provisioned row" case the battle-end path must
    /// fail closed on.
    /// </summary>
    public Dictionary<string, string> Resolvable { get; } = new(StringComparer.Ordinal);

    /// <summary>How many lookups were attempted, so a test can assert ordering.</summary>
    public int LookupCount { get; private set; }

    /// <summary>
    /// Seeds the map with a BattleSeed pairing so a test's battle resolves.
    /// </summary>
    public static ScriptedBossDefinitionLookup Resolving(BossDefinition definition)
    {
        var lookup = new ScriptedBossDefinitionLookup();

        lookup.Resolvable[definition.BossId.Value] = definition.BossDefinitionId;

        return lookup;
    }

    /// <inheritdoc />
    public Task<string?> FindBossDefinitionIdByIdentityAsync(
        string bossIdentity,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bossIdentity);

        LookupCount++;

        return Task.FromResult(
            Resolvable.TryGetValue(bossIdentity, out var key) ? key : null);
    }
}
