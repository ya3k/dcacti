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
            DiscordUserId = $"discord-{playerId}",
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

    /// <inheritdoc />
    /// <remarks>
    /// The starter grant is accepted and deliberately <b>not</b> modelled: this
    /// double exists for the battle-reward path, which reads and writes one
    /// Player progression row and observes no ownership. Modelling the seven
    /// starter rows here would add state no test of this double's purpose could
    /// assert — the atomic starter bootstrap is verified against real
    /// persistence in the Infrastructure suite
    /// (<c>PlayerStarterOwnershipTests</c>, <c>PlayerStarterOwnershipPostgresTests</c>).
    ///
    /// The composition callback is invoked exactly where the real boundary
    /// invokes it — on the creation branch — so a test that asserts the starter
    /// set is composed only for a new Player observes the same behavior here as
    /// in the production path.
    /// </remarks>
    public async Task<Player> GetOrCreateByDiscordUserIdAsync(
        string discordUserId,
        Func<CancellationToken, Task<PlayerStarterGrant>> composeStarterGrant,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(composeStarterGrant);

        var existing = _players.Values
            .FirstOrDefault(player => player.DiscordUserId == discordUserId);

        if (existing is not null)
        {
            return existing;
        }

        _ = await composeStarterGrant(cancellationToken);

        var created = new Player
        {
            PlayerId = $"player_{Guid.NewGuid():N}",
            DiscordUserId = discordUserId,
            XP = Player.InitialXp,
            Level = Player.InitialLevel,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        _players[created.PlayerId] = created;

        return created;
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
