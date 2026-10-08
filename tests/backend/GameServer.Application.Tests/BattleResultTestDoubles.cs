using System.Collections.Concurrent;
using GameServer.Application.Battle;
using GameServer.Application.Pets;
using GameServer.Application.Players;
using GameServer.Domain.Battle;
using GameServer.Domain.Bosses;
using GameServer.Domain.Elements;
using GameServer.Domain.Pets;
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
    public Task<bool> AddAsync(BattleResult result, CancellationToken cancellationToken = default)
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
        // battle's own BattleId. An existing row IS this battle's
        // already-durable terminal result, so a repeat writes NOTHING and is
        // reported as "not the first write" — which is what lets a test prove the
        // Player XP grant (COMBAT_RULES.md §7.2) is bound to the first durable
        // write rather than to every call.
        if (_rows.ContainsKey(result.BattleResultId))
        {
            return Task.FromResult(false);
        }

        _rows[result.BattleResultId] = result;

        return Task.FromResult(true);
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

    /// <summary>
    /// The Player-scoped ordered history read
    /// (<c>API_CONTRACTS.md</c> §4.5 notes 4 and 8).
    ///
    /// <b>It models the two documented semantics the history read depends on</b>,
    /// so an Application-layer test cannot pass against a double more permissive
    /// than the contract:
    /// <list type="bullet">
    /// <item><b>The Player filter is part of the read</b> (note 8): another
    /// Player's rows are never returned, which is what lets a test prove the
    /// caller's scope is the authenticated identity.</item>
    /// <item><b>The order is the contract's</b> (note 4): <c>CompletedAt</c>
    /// descending, then <c>BattleResultId</c> descending as the deterministic
    /// tie-break — the same total order the production query expresses, so a test
    /// that asserts "newest first" asserts the documented order rather than
    /// insertion order.</item>
    /// </list>
    ///
    /// The comparisons are ordinal for the same reason the production query's are:
    /// the ids are opaque server-authored strings, so the tie-break must not depend
    /// on a culture-sensitive collation.
    /// </summary>
    public Task<IReadOnlyList<BattleResult>> ListByPlayerIdAsync(
        string playerId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(playerId);

        IReadOnlyList<BattleResult> history = _rows.Values
            .Where(row => string.Equals(row.PlayerId, playerId, StringComparison.Ordinal))
            .OrderByDescending(row => row.CompletedAt)
            .ThenByDescending(row => row.BattleResultId, StringComparer.Ordinal)
            .ToList();

        return Task.FromResult(history);
    }

    /// <summary>
    /// Seeds a stored result directly, so a history test can establish exactly the
    /// rows it needs — including two results that share a <c>CompletedAt</c>, which
    /// the tie-break rule exists for (<c>API_CONTRACTS.md</c> §4.5 note 4).
    ///
    /// It writes through <see cref="AddAsync"/> so the double's own key semantics
    /// ("one row per battle", the primary-key guard) hold for seeded rows too.
    /// </summary>
    public void Seed(BattleResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        _rows[result.BattleResultId] = result;
    }

    /// <summary>
    /// The rows this double currently holds, so the test transaction double can put
    /// them back when the battle-end unit of work rolls back
    /// (<see cref="InMemoryBattleEndTransaction"/>).
    ///
    /// <b>It captures durability, which for this store is which rows exist.</b> A
    /// <see cref="BattleResult"/> is immutable and this store never rewrites one, so
    /// restoring the stored set is exactly the undo a database transaction performs
    /// on an insert.
    /// </summary>
    public IReadOnlyDictionary<string, BattleResult> Snapshot() =>
        new Dictionary<string, BattleResult>(_rows, StringComparer.Ordinal);

    /// <summary>
    /// Restores the stored set captured by <see cref="Snapshot"/> — the rollback the
    /// real store performs in PostgreSQL, where an uncommitted insert leaves no row.
    /// </summary>
    public void Restore(IReadOnlyDictionary<string, BattleResult> snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        _rows.Clear();

        foreach (var (battleResultId, row) in snapshot)
        {
            _rows[battleResultId] = row;
        }
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

/// <summary>
/// An in-memory <see cref="IPlayerRepository"/> for the Application-layer unit
/// tests.
///
/// <b>It is a TEST DOUBLE, not a production fallback.</b> The real store is
/// <c>GameServer.Infrastructure.Postgres.Repositories.PlayerRepository</c>
/// against PostgreSQL (<c>DATABASE.md</c> §1). This type exists so a test can run
/// the real <see cref="BattleResultService"/> against a Player progression row it
/// can inspect.
///
/// <b>It models the documented semantics the reward path depends on</b>, so a
/// test cannot pass against a double more permissive than the contract:
/// <list type="bullet">
/// <item><b>An absent Player is reported as absence</b> (<c>DATABASE.md</c> §1):
/// the reward path must not invent a Player, so a lookup for an unknown id
/// returns <c>null</c> and a save for one writes nothing.</item>
/// <item><b>The Player is a real Domain entity.</b> XP and Level are the values
/// of <see cref="Player"/> itself, so the grant goes through the same
/// <see cref="Player.GrantBattleXp"/> the production path uses and no formula is
/// re-implemented in the double.</item>
/// </list>
///
/// <b>It touches no Pet state.</b> The Player and Pet tracks are independent
/// (<c>ADR-016</c> item 12), and this double exposes nothing about a Pet — so an
/// implementation that reached for Pet progression through the Player boundary
/// could not compile.
/// </summary>
internal sealed class InMemoryPlayerRepository : IPlayerRepository
{
    private readonly ConcurrentDictionary<string, Player> _players = new(StringComparer.Ordinal);

    /// <summary>The Players currently stored, keyed by their identifier.</summary>
    public IReadOnlyCollection<Player> Players => _players.Values.ToArray();

    /// <summary>
    /// How many progression saves this double has received, so a test can assert
    /// that a battle with no owning Player performed no write, and that a
    /// non-terminal resolution saved nothing.
    /// </summary>
    public int ProgressionSaveCount { get; private set; }

    /// <summary>
    /// When set, <see cref="SaveProgressionAsync"/> raises instead of writing —
    /// the documented PostgreSQL failure on the battle-end path
    /// (<c>DATABASE.md</c> §1 sourcing item 3), which must undo the whole battle-end
    /// unit of work rather than leave the battle half-recorded.
    ///
    /// <b>Why the failure belongs to the double and not to production code.</b> No
    /// production flag, switch, or test hook exists for failing a persistence write
    /// (<c>AGENTS.md</c> §9); a failure that the battle-end transaction must survive
    /// is injected here, where it can do nothing but fail a test.
    /// </summary>
    public bool ProgressionSaveFails { get; set; }

    /// <summary>
    /// When set, <see cref="SaveProgressionAsync"/> reports the row as absent —
    /// <c>false</c> — without raising, which is the other way a participating write
    /// can fail to happen (<c>DATABASE.md</c> §1: an absent row is reported as
    /// absence). It models the row disappearing between the read the battle-end path
    /// performs and the progression write it then asks for, and it must fail the
    /// battle end exactly as the raising case does, because the reward the durable
    /// result states would otherwise never be recorded.
    /// </summary>
    public bool ProgressionRowAbsentOnSave { get; set; }

    /// <summary>
    /// The progression values this double currently holds, so the test transaction
    /// double can put them back when the unit of work rolls back
    /// (<see cref="InMemoryBattleEndTransaction"/>).
    ///
    /// <b>It captures values, not rows.</b> A stored <see cref="Player"/> is the
    /// very object the battle-end path reads and grants to, so the rollback this
    /// models is the write being undone: the row's <c>XP</c> and <c>Level</c> return
    /// to what they were. The reward path creates no Player
    /// (<c>DATABASE.md</c> §1), so membership is not part of the undo.
    /// </summary>
    public IReadOnlyDictionary<string, (int Xp, int Level)> SnapshotProgression() =>
        _players.ToDictionary(
            entry => entry.Key,
            entry => (entry.Value.XP, entry.Value.Level),
            StringComparer.Ordinal);

    /// <summary>
    /// Restores the progression values captured by
    /// <see cref="SnapshotProgression"/> — the rollback the real store performs in
    /// PostgreSQL, where an uncommitted update leaves the previous values.
    /// </summary>
    public void RestoreProgression(IReadOnlyDictionary<string, (int Xp, int Level)> snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        foreach (var (playerId, progression) in snapshot)
        {
            if (_players.TryGetValue(playerId, out var stored))
            {
                stored.XP = progression.Xp;
                stored.Level = progression.Level;
            }
        }
    }

    /// <summary>
    /// Seeds a Player row — the account the battle's <c>PlayerId</c> refers to
    /// (<c>GAME_STATE.md</c> §2.8 / <c>DATABASE.md</c> §1).
    ///
    /// The Level is derived from the seeded XP through the Domain's own
    /// <see cref="Player.LevelForXp"/> unless a test explicitly states one, so a
    /// seeded row is a state the documented contract can actually hold
    /// (<c>COMBAT_RULES.md</c> §7.4).
    /// </summary>
    public Player Seed(
        string playerId,
        int xp = Player.InitialXp,
        int? level = null)
    {
        var player = new Player
        {
            PlayerId = playerId,
            AccountId = Guid.NewGuid(),
            XP = xp,
            Level = level ?? Player.LevelForXp(xp),
            CreatedAt = DateTimeOffset.UtcNow,
        };

        _players[playerId] = player;

        return player;
    }

    /// <summary>The stored Player for <paramref name="playerId"/>, or null.</summary>
    public Player? Find(string playerId) =>
        _players.TryGetValue(playerId, out var player) ? player : null;

    public async Task<Player> GetOrCreateForAccountAsync(
        Guid accountId,
        Func<CancellationToken, Task<PlayerStarterGrant>> composeStarterGrant,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(composeStarterGrant);

        var existing = _players.Values
            .FirstOrDefault(player => player.AccountId == accountId);

        if (existing is not null)
        {
            return existing;
        }

        _ = await composeStarterGrant(cancellationToken);

        var created = new Player
        {
            PlayerId = $"player_{Guid.NewGuid():N}",
            AccountId = accountId,
            XP = Player.InitialXp,
            Level = Player.InitialLevel,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        _players[created.PlayerId] = created;

        return created;
    }

    public Task<Player?> GetByAccountIdAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        var player = _players.Values.FirstOrDefault(p => p.AccountId == accountId);
        return Task.FromResult(player);
    }

    /// <inheritdoc />
    public Task<Player?> GetByIdAsync(
        string playerId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Find(playerId));

    /// <inheritdoc />
    public Task<bool> SaveProgressionAsync(
        Player player,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(player);

        ProgressionSaveCount++;

        if (ProgressionSaveFails)
        {
            throw new InvalidOperationException(
                "DATABASE.md §1: this double is configured to fail the Player progression write.");
        }

        if (ProgressionRowAbsentOnSave)
        {
            return Task.FromResult(false);
        }

        // DATABASE.md §1: an absent row is reported as absence and nothing is
        // created — a reward must not bring a Player into existence.
        if (!_players.TryGetValue(player.PlayerId, out var stored))
        {
            return Task.FromResult(false);
        }

        stored.XP = player.XP;
        stored.Level = player.Level;

        return Task.FromResult(true);
    }
}

/// <summary>
/// An in-memory <see cref="IPetRepository"/> for the Application-layer unit tests.
///
/// <b>It is a TEST DOUBLE, not a production fallback.</b> The real store is
/// <c>GameServer.Infrastructure.Postgres.Repositories.PetRepository</c> against
/// PostgreSQL (<c>DATABASE.md</c> §1). This type exists so a test can run the real
/// <see cref="BattleResultService"/> against Pet rows it can inspect.
///
/// <b>It models the documented semantics the Pet reward path depends on</b>, so a
/// test cannot pass against a double more permissive than the contract:
/// <list type="bullet">
/// <item><b>An absent Pet is reported as absence</b> (<c>DATABASE.md</c> §1): the
/// reward path must not invent a Pet, so a lookup for an unknown id returns
/// <c>null</c> and a save for one writes nothing.</item>
/// <item><b>The Pet is a real Domain entity.</b> XP and Level are the values of
/// <see cref="Pet"/> itself, so the grant goes through the same
/// <see cref="Pet.GrantBattleXp"/> the production path uses — including the hard
/// <c>4900</c> cap (<c>PET_RULES.md</c> §5.5) — and no formula is re-implemented
/// in the double.</item>
/// <item><b>Saves are per instance</b> (<c>PET_RULES.md</c> §5.3 items 1–3): only
/// the addressed instance is written, so a test can prove that every other owned
/// Pet receives <c>+0</c>.</item>
/// </list>
///
/// <b>It touches no Player state.</b> The two tracks are independent
/// (<c>ADR-016</c> item 12), and this double exposes nothing about a Player — so
/// an implementation that reached for Player progression through the Pet
/// boundary could not compile.
/// </summary>
internal sealed class InMemoryPetRepository : IPetRepository
{
    private readonly ConcurrentDictionary<string, Pet> _pets = new(StringComparer.Ordinal);

    /// <summary>The Pet instances currently stored, keyed by their identifier.</summary>
    public IReadOnlyCollection<Pet> Pets => _pets.Values.ToArray();

    /// <summary>
    /// How many progression saves this double has received, so a test can assert
    /// that a battle with no such Pet performed no write.
    /// </summary>
    public int ProgressionSaveCount { get; private set; }

    /// <summary>
    /// When set, <see cref="SaveProgressionAsync"/> raises instead of writing —
    /// the documented PostgreSQL failure on the battle-end path
    /// (<c>DATABASE.md</c> §1 sourcing item 3), which must undo the whole battle-end
    /// unit of work rather than leave a durable result whose Pet XP was never
    /// applied (<c>PET_RULES.md</c> §5.3 item 1).
    ///
    /// <b>Why the failure belongs to the double and not to production code.</b> No
    /// production flag, switch, or test hook exists for failing a persistence write
    /// (<c>AGENTS.md</c> §9); a failure the battle-end transaction must survive is
    /// injected here, where it can do nothing but fail a test.
    /// </summary>
    public bool ProgressionSaveFails { get; set; }

    /// <summary>
    /// When set, <see cref="SaveProgressionAsync"/> reports the row as absent —
    /// <c>false</c> — without raising, which is the other way a participating write
    /// can fail to happen (<c>DATABASE.md</c> §1: an absent row is reported as
    /// absence), and the exact case the battle-end path used to ignore. It must fail
    /// the battle end exactly as the raising case does.
    /// </summary>
    public bool ProgressionRowAbsentOnSave { get; set; }

    /// <summary>
    /// The progression values this double currently holds, so the test transaction
    /// double can put them back when the unit of work rolls back
    /// (<see cref="InMemoryBattleEndTransaction"/>).
    ///
    /// <b>It captures values, not rows.</b> A stored <see cref="Pet"/> is the very
    /// object the battle-end path reads and grants to, so the rollback this models
    /// is the write being undone: the instance's <c>XP</c> and <c>Level</c> return to
    /// what they were. Every other owned Pet is untouched by that path anyway
    /// (<c>PET_RULES.md</c> §5.3 items 1–3), and no Pet is created by it.
    /// </summary>
    public IReadOnlyDictionary<string, (int Xp, int Level)> SnapshotProgression() =>
        _pets.ToDictionary(
            entry => entry.Key,
            entry => (entry.Value.XP, entry.Value.Level),
            StringComparer.Ordinal);

    /// <summary>
    /// Restores the progression values captured by
    /// <see cref="SnapshotProgression"/> — the rollback the real store performs in
    /// PostgreSQL, where an uncommitted update leaves the previous values.
    /// </summary>
    public void RestoreProgression(IReadOnlyDictionary<string, (int Xp, int Level)> snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        foreach (var (petInstanceId, progression) in snapshot)
        {
            if (_pets.TryGetValue(petInstanceId, out var stored))
            {
                stored.XP = progression.Xp;
                stored.Level = progression.Level;
            }
        }
    }

    /// <summary>
    /// Seeds a Pet instance — the combat Pet a battle's
    /// <c>PetState.PetId</c> refers to (<c>GAME_STATE.md</c> §2.3 /
    /// <c>DATABASE.md</c> §1).
    ///
    /// The Level is derived from the seeded XP through the Domain's own
    /// <see cref="Pet.LevelForXp"/> unless a test explicitly states one, so a
    /// seeded row is a state the documented contract can actually hold
    /// (<c>PET_RULES.md</c> §5.4).
    /// </summary>
    public Pet Seed(
        string petInstanceId,
        int xp = Pet.InitialXp,
        int? level = null,
        string playerId = "player_pet_owner",
        string petDefinitionId = "pet-definition-seeded")
    {
        var pet = new Pet
        {
            PetInstanceId = petInstanceId,
            PlayerId = playerId,
            PetDefinitionId = petDefinitionId,
            Tier = PetTier.Common,
            Star = Pet.MinStar,
            XP = xp,
            Level = level ?? Pet.LevelForXp(xp),
            AcquiredAt = DateTimeOffset.UtcNow,
        };

        _pets[petInstanceId] = pet;

        return pet;
    }

    /// <summary>The stored Pet for <paramref name="petInstanceId"/>, or null.</summary>
    public Pet? Find(string petInstanceId) =>
        _pets.TryGetValue(petInstanceId, out var pet) ? pet : null;

    /// <inheritdoc />
    public Task AddAsync(Pet pet, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pet);

        _pets[pet.PetInstanceId] = pet;

        return Task.CompletedTask;
    }

    /// <summary>
    /// No definition rows are seeded by this double. The Pet reward path never
    /// reads one — <c>PET_RULES.md</c> §5.3 item 1 resolves the recipient from the
    /// battle state, not from content — so an implementation that reached for a
    /// definition here could not resolve one.
    /// </summary>
    public Task<PetDefinition?> GetDefinitionAsync(
        string petDefinitionId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<PetDefinition?>(null);

    /// <summary>
    /// The collection read (<c>API_CONTRACTS.md</c> §5.1) is not exercised by the
    /// battle-end reward path, so this double keeps its documented
    /// <c>PlayerId</c> scoping available rather than throwing: a test that wants
    /// to assert the boundary is Player-filtered can do so directly.
    /// </summary>
    public Task<IReadOnlyList<Pet>> ListByPlayerIdAsync(
        string playerId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Pet>>(
            _pets.Values
                .Where(pet => pet.PlayerId == playerId)
                .ToList());

    /// <summary>
    /// No definition rows are seeded (<see cref="GetDefinitionAsync"/>), so the
    /// bulk content read resolves nothing — which is the same absence, stated for
    /// the whole set at once.
    /// </summary>
    public Task<IReadOnlyList<PetDefinition>> ListDefinitionsAsync(
        IReadOnlyCollection<string> petDefinitionIds,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PetDefinition>>([]);

    /// <inheritdoc />
    public Task<Pet?> GetByIdAsync(
        string petInstanceId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Find(petInstanceId));

    /// <inheritdoc />
    public Task<bool> SaveProgressionAsync(
        Pet pet,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pet);

        ProgressionSaveCount++;

        if (ProgressionSaveFails)
        {
            throw new InvalidOperationException(
                "DATABASE.md §1: this double is configured to fail the Pet progression write.");
        }

        if (ProgressionRowAbsentOnSave)
        {
            return Task.FromResult(false);
        }

        // DATABASE.md §1: an absent row is reported as absence and nothing is
        // created — a reward must not bring a Pet instance into existence.
        if (!_pets.TryGetValue(pet.PetInstanceId, out var stored))
        {
            return Task.FromResult(false);
        }

        // PET_RULES.md §5.3 item 1: exactly one instance is addressed, so no
        // other owned Pet is touched by this write (item 3).
        stored.XP = pet.XP;
        stored.Level = pet.Level;

        return Task.FromResult(true);
    }
}

/// <summary>
/// An in-memory <see cref="IBattleEndTransaction"/> for the Application-layer unit
/// tests.
///
/// <b>It is a TEST DOUBLE, not a production fallback.</b> The real transaction is
/// <c>GameServer.Infrastructure.Postgres.BattleEndTransaction</c> over the scoped
/// <c>GameDbContext</c>, and the atomicity the battle end requires is a property of
/// that database transaction — which is why it is proven against real PostgreSQL in
/// the Infrastructure integration suite, not here. This type exists so the real
/// <see cref="BattleResultService"/> can run over the in-memory stores above while
/// still being held to the unit-of-work semantics it depends on.
///
/// <b>It models the three semantics the battle-end path depends on</b>, so a test
/// cannot pass against a double more permissive than a database transaction:
/// <list type="bullet">
/// <item><b>A rollback undoes every write made inside the unit of work.</b> The
/// participating stores are captured when the transaction begins and restored when
/// it is rolled back — which is what lets a test assert that a failed battle end
/// left no durable result and no progression write, and what makes a retry after a
/// rollback behave exactly as it does against PostgreSQL.</item>
/// <item><b>Only a commit makes the writes stand.</b> A scope that is disposed
/// without having been committed restores the stores as well, which is the
/// framework transaction's own documented behaviour.</item>
/// <item><b>Commit and rollback are terminal.</b> A second call is an error rather
/// than a silent no-op, so an implementation that believed it had undone a committed
/// unit of work cannot pass.</item>
/// </list>
///
/// <b>It records what it was asked to do.</b> The counts below exist because the
/// ordering rule is part of the contract: the active state may only be cleared after
/// a successful commit (<c>REDIS_STATE.md</c> §3), and
/// <see cref="OnCommit"/> lets a test observe what had happened by the moment of the
/// commit.
/// </summary>
internal sealed class InMemoryBattleEndTransaction : IBattleEndTransaction
{
    private readonly InMemoryBattleResultRepository _results;
    private readonly InMemoryPlayerRepository _players;
    private readonly InMemoryPetRepository _pets;

    public InMemoryBattleEndTransaction(
        InMemoryBattleResultRepository results,
        InMemoryPlayerRepository players,
        InMemoryPetRepository pets)
    {
        _results = results ?? throw new ArgumentNullException(nameof(results));
        _players = players ?? throw new ArgumentNullException(nameof(players));
        _pets = pets ?? throw new ArgumentNullException(nameof(pets));
    }

    /// <summary>How many transactions this double has begun.</summary>
    public int BeginCount { get; private set; }

    /// <summary>
    /// How many of them were committed — the battle ends that became durable.
    /// </summary>
    public int CommitCount { get; private set; }

    /// <summary>
    /// How many were rolled back, whether explicitly or by disposing an
    /// uncommitted scope.
    /// </summary>
    public int RollbackCount { get; private set; }

    /// <summary>
    /// Invoked from <see cref="IBattleEndTransactionScope.CommitAsync"/>, so a test
    /// can record what was true at the moment of the commit — the ordering proof
    /// for <c>REDIS_STATE.md</c> §3's "the delete happens only after the durable
    /// write".
    /// </summary>
    public Action? OnCommit { get; set; }

    /// <inheritdoc />
    public Task<IBattleEndTransactionScope> BeginAsync(CancellationToken cancellationToken = default)
    {
        BeginCount++;

        // The state of every participating store at the moment the unit of work
        // starts: what a rollback must put back.
        return Task.FromResult<IBattleEndTransactionScope>(new Scope(this));
    }

    private sealed class Scope : IBattleEndTransactionScope
    {
        private readonly InMemoryBattleEndTransaction _transaction;
        private readonly IReadOnlyDictionary<string, BattleResult> _results;
        private readonly IReadOnlyDictionary<string, (int Xp, int Level)> _players;
        private readonly IReadOnlyDictionary<string, (int Xp, int Level)> _pets;
        private bool _ended;

        public Scope(InMemoryBattleEndTransaction transaction)
        {
            _transaction = transaction;

            _results = transaction._results.Snapshot();
            _players = transaction._players.SnapshotProgression();
            _pets = transaction._pets.SnapshotProgression();
        }

        /// <inheritdoc />
        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            if (_ended)
            {
                throw new InvalidOperationException(
                    "IBattleEndTransactionScope: this double's transaction was already completed, so it "
                    + "cannot be committed again — the same error a completed database transaction reports.");
            }

            _ended = true;
            _transaction.CommitCount++;
            _transaction.OnCommit?.Invoke();

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            if (_ended)
            {
                throw new InvalidOperationException(
                    "IBattleEndTransactionScope: this double's transaction was already completed, so it "
                    + "cannot be rolled back — the same error a completed database transaction reports.");
            }

            Undo();

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public ValueTask DisposeAsync()
        {
            // The framework transaction is disposed after having been committed or
            // rolled back in the battle-end path, in which case there is nothing
            // left to undo. A scope disposed while still open is the case the
            // database also treats as an undo, so the stores are restored.
            Undo();

            return ValueTask.CompletedTask;
        }

        private void Undo()
        {
            if (_ended)
            {
                return;
            }

            _ended = true;
            _transaction.RollbackCount++;

            _transaction._results.Restore(_results);
            _transaction._players.RestoreProgression(_players);
            _transaction._pets.RestoreProgression(_pets);
        }
    }
}
