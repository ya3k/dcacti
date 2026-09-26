using GameServer.Application.Battle;
using GameServer.Domain.Battle;
using GameServer.Domain.Battle.Serialization;
using StackExchange.Redis;

namespace GameServer.Infrastructure.Redis;

/// <summary>
/// The Redis-backed active battle state store
/// (<c>REDIS_STATE.md</c> §1–§4, <c>ARCHITECTURE.md</c> §1, §3
/// <c>Redis/ — Active BattleState read/write</c>).
///
/// <code>
/// BattleStateService              Application — sequencing only
///         ↓  IBattleStateRepository
/// BattleStateRepository           Infrastructure — this type
///         ↓  StackExchange.Redis
/// battle:{battleId}:state
/// </code>
///
/// <b>What it implements.</b> Exactly the store <c>REDIS_STATE.md</c> §1
/// defines — one key per battle, holding the serialized authoritative
/// <c>BattleState</c> — with §3's lifecycle (created on battle creation,
/// sliding expiry refreshed on a successful resolution), §4's <c>Sequence</c>
/// compare-and-set (the only concurrency token, §4 item 6), and §4 item 5's one
/// write-back per resolution. It adds no key, no hash field, no index, and no
/// second record: §1 fixes the key set at one state key (the optional lock key
/// of §4 item 4 is deliberately not introduced — §4 item 4 makes the compare-and-set,
/// not the lock, the source of correctness, and <c>ARCHITECTURE.md</c> §5
/// forbids building infrastructure the contract does not require).
///
/// <b>This is the only place Redis is named.</b> <c>REDIS_STATE.md</c> §2 owns
/// the record, §1 owns the key, and <c>ARCHITECTURE.md</c> §2.1 item 3 puts the
/// framework-specific implementation in Infrastructure. Nothing above this
/// layer references a Redis type: the Application contract is expressed in
/// terms of <see cref="BattleState"/> and its <c>Sequence</c>, and Domain —
/// including <see cref="BattleStateSerializer"/> — remains storage-independent
/// (<c>REDIS_STATE.md</c> §2 item 1).
///
/// <b>Serialization is TASK-029's mapping, reused.</b> The record is written and
/// read through <see cref="BattleStateSerializer"/>, which is the documented
/// runtime JSON representation of <c>GAME_STATE.md</c> §2 — not a Redis-specific
/// schema, not a second serializer, and not the <c>SIGNALR_PROTOCOL.md</c> §4
/// wire projection, which is a separate contract (§2 item 1: "no additional
/// Redis-only fields beyond <c>Sequence</c>, which is already part of
/// <c>BattleState</c>"). Redis here is a store for that document, nothing more:
/// it does not adjust, normalize, re-derive, or repair the state it is handed
/// (<c>GAME_STATE.md</c> §2.1.7 item 5, <c>REDIS_STATE.md</c> §7 items 9–12).
///
/// <b>Failure is raised, never absorbed.</b> <c>REDIS_STATE.md</c> §2 item 2
/// makes this record the single source of truth for a battle's live state, §7
/// item 5 states that nothing permits that state to live in process memory, and
/// <c>ADR-005</c> rejected that option. So a connexion or command failure
/// propagates to the caller as the failure it is — there is deliberately no
/// in-process fallback, no cached copy, and no "pretend it worked" path that
/// would let resolution succeed while the authoritative state was never stored.
/// The provider's exception is allowed to surface rather than being wrapped in
/// a type this layer invents; §5 item 2 states the consequence for a lost
/// instance plainly ("that battle's progress is lost; this is accepted for
/// MVP").
/// </summary>
public sealed class BattleStateRepository : IBattleStateRepository
{
    /// <summary>
    /// The documented active-state key format (<c>REDIS_STATE.md</c> §1):
    /// <c>battle:{battleId}:state</c>. It is the only state key the contract
    /// defines, and no other key is created by this type.
    /// </summary>
    private const string KeyPrefix = "battle:";

    /// <summary>
    /// The key's suffix (<c>REDIS_STATE.md</c> §1). The battle id sits between
    /// the two, so the full key is exactly <c>battle:{battleId}:state</c>.
    /// </summary>
    private const string KeySuffix = ":state";

    /// <summary>
    /// The documented active-state expiry: 30 minutes of inactivity, refreshed
    /// on every successful resolution (<c>REDIS_STATE.md</c> §3 — "TTL reset on
    /// every successful resolution (sliding expiry), default 30 minutes of
    /// inactivity").
    ///
    /// It is a stored constant rather than configuration because §3 states the
    /// value as the documented default; introducing a setting for it would be
    /// inventing a configuration surface the contract does not define
    /// (<c>AGENTS.md</c> §9).
    /// </summary>
    private static readonly TimeSpan StateTtl = TimeSpan.FromMinutes(30);

    /// <summary>
    /// The compare-and-set write (<c>REDIS_STATE.md</c> §4 item 2).
    ///
    /// <b>Why a Lua script.</b> §4 item 2 names the mechanism ("compare-and-set,
    /// e.g. via a Lua script or <c>WATCH</c>/<c>MULTI</c>/<c>EXEC</c>"). A
    /// script is used because the read of the stored <c>Sequence</c>, the
    /// comparison, the write, and the TTL refresh must be one atomic step: with
    /// a separate read the comparison could pass against a value another
    /// resolution has already replaced, which is precisely the lost update §4
    /// items 2–3 forbid. Redis executes the whole script atomically, so the
    /// check and the write cannot interleave with another client's.
    ///
    /// <b>What it does, and only this.</b>
    /// <list type="number">
    /// <item>reads the stored record; if none exists the write applies nothing
    /// (there is no battle to update, and creating one here would invent a
    /// battle — <c>REDIS_STATE.md</c> §3 ties creation to
    /// <c>POST /api/battle/start</c>),</item>
    /// <item>compares the stored <c>Sequence</c> with the expected one — the
    /// only token compared (§4 item 6), never <c>Turn</c>,</item>
    /// <item>on a match, writes the new record, resets the TTL to §3's
    /// documented 30 minutes, and returns <c>1</c>,</item>
    /// <item>on a mismatch, writes nothing at all — no record, no TTL reset,
    /// no <c>Sequence</c> change — and returns <c>0</c> (§4 items 2–3, 7).</item>
    /// </list>
    ///
    /// The stored <c>Sequence</c> is read with <c>cjson.decode</c> rather than
    /// by pattern-matching the document, so the comparison is on the field
    /// <c>GAME_STATE.md</c> §2 defines and not on a spelling of it. A stored
    /// value that is not decodable is reported as a mismatch (no write) rather
    /// than overwritten: an unreadable record is not evidence that the expected
    /// sequence still holds, and clobbering it would be the silent overwrite
    /// §4 forbids.
    /// </summary>
    private static readonly string CompareAndSetScript = """
        local stored = redis.call('GET', KEYS[1])

        -- No record exists: there is no stored Sequence to compare against, so
        -- the update applies nothing. Creation is a separate documented step
        -- (REDIS_STATE.md §3) and is not performed here.
        if not stored then
            return 0
        end

        local decoded = cjson.decode(stored)

        -- A record whose Sequence cannot be read cannot be shown to match, so
        -- the write is refused rather than applied blind.
        if not decoded or decoded['sequence'] == nil then
            return 0
        end

        -- REDIS_STATE.md §4 item 2: the write succeeds only if the stored
        -- Sequence still matches what the resolution read. §4 item 6: Sequence
        -- is the only token compared — Turn is never compared.
        if tostring(decoded['sequence']) ~= ARGV[1] then
            return 0
        end

        -- The one write-back of the resolution (§4 item 5), with the sliding
        -- expiry refreshed on this successful resolution (§3).
        redis.call('SET', KEYS[1], ARGV[2], 'PX', ARGV[3])

        return 1
        """;

    private readonly IConnectionMultiplexer _connection;

    /// <summary>
    /// Creates the store over the process's Redis connection
    /// (<c>GameServer.Infrastructure.DependencyInjection</c> registers the
    /// multiplexer conditionally, on <c>ConnectionStrings:Redis</c>).
    /// </summary>
    /// <param name="connection">
    /// The shared Redis connection. It is the boundary's one dependency, so the
    /// store owns no connection lifecycle of its own and never opens a second
    /// connection to the same instance.
    /// </param>
    public BattleStateRepository(IConnectionMultiplexer connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }

    /// <summary>
    /// The key a battle's record is stored under — <c>battle:{battleId}:state</c>
    /// (<c>REDIS_STATE.md</c> §1).
    ///
    /// It is the contract's own spelling, assembled in one place so the create,
    /// read, and compare-and-set paths cannot drift from each other or from §1.
    /// The write carries no expiry of its own: the TTL is set by §3's rules on
    /// each path (creation sets it, and a successful resolution resets it).
    /// </summary>
    private static RedisKey StateKey(string battleId) => $"{KeyPrefix}{battleId}{KeySuffix}";

    /// <inheritdoc />
    public async Task CreateAsync(BattleState state, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);

        // REDIS_STATE.md §2 item 1 / §7 items 9–12: the documented runtime JSON,
        // written as it is. Redis adds nothing to the document and removes
        // nothing from it.
        var record = BattleStateSerializer.Serialize(state);

        // §3 "Created: on POST /api/battle/start" and the same section's
        // "default 30 minutes of inactivity": the record starts its sliding
        // expiry at creation, so an abandoned battle expires rather than
        // living forever.
        //
        // An existing record for this id is overwritten: the battle id is
        // server-authored and fresh (API_CONTRACTS.md §3), so this is a new
        // battle's first write rather than a state replacement. No
        // compare-and-set applies — there is no prior Sequence of this battle's
        // to guard, and creation is not a resolution (§2.0.5.2 item 1).
        await _connection.GetDatabase()
            .StringSetAsync(StateKey(state.BattleId), record, StateTtl)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<BattleState?> GetAsync(
        string battleId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(battleId);

        var record = await _connection.GetDatabase()
            .StringGetAsync(StateKey(battleId))
            .ConfigureAwait(false);

        // REDIS_STATE.md §3: a key that expired from inactivity means the battle
        // is abandoned, so a missing record is reported as absence. Nothing is
        // fabricated for it — the caller distinguishes "no battle" from a
        // battle's state, and no empty or default state is returned.
        if (!record.HasValue)
        {
            return null;
        }

        // The stored document is restored through the same TASK-029 mapping that
        // wrote it, so the state is the value that was persisted — not
        // re-derived, re-defaulted, or rebuilt from anything else (GAME_STATE.md
        // §2.6.2 item 4: a recovered battle resumes the same stream).
        return BattleStateSerializer.Deserialize(record.ToString());
    }

    /// <inheritdoc />
    public async Task<bool> TryUpdateAsync(
        BattleState state,
        int expectedSequence,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);

        var record = BattleStateSerializer.Serialize(state);

        // The script owns the check and the write together (see
        // CompareAndSetScript): the stored Sequence is compared with the
        // expected one and only a match writes the record and slides the TTL.
        //
        // The expected value is passed as culture-invariant text because the
        // comparison runs inside Redis on the stored document; formatting it
        // through the current culture would let a non-invariant separator
        // produce a mismatch that no state change caused.
        var result = await _connection.GetDatabase()
            .ScriptEvaluateAsync(
                CompareAndSetScript,
                [StateKey(state.BattleId)],
                [
                    expectedSequence.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    record,
                    ((long)StateTtl.TotalMilliseconds).ToString(
                        System.Globalization.CultureInfo.InvariantCulture),
                ])
            .ConfigureAwait(false);

        // 1 = written (the sequences matched), 0 = refused (the stored Sequence
        // had moved on, or the record was gone/unreadable). REDIS_STATE.md §4
        // item 2: a refusal is the caller's signal to retry against fresh state,
        // and the newer authoritative state was left untouched.
        return (long)result == 1;
    }
}
