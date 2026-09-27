using GameServer.Domain.Battle;
using GameServer.Domain.Players;

namespace GameServer.Application.Battle;

/// <summary>
/// The durable battle result boundary — the battle-end persistence step of
/// <c>ARCHITECTURE.md</c> §4 item 4 and <c>DATABASE.md</c> §1.
///
/// <code>
/// Authoritative BattleState            (Redis record, REDIS_STATE.md §2)
///         ↓
/// BossDefinition lookup by Identity    (DATABASE.md §1 note item 2 — PostgreSQL)
///         ↓
/// BattleResult                         (DATABASE.md §1 — the eight documented fields)
///         ↓
/// PostgreSQL write                     (this boundary)
///         ↓
/// battle:{battleId}:state delete       (REDIS_STATE.md §3 — only after the write)
/// </code>
///
/// <b>Why this step lives beside the resolution and not in it.</b>
/// <c>ARCHITECTURE.md</c> §4 item 4 gives the battle-end sequence to the same
/// Application orchestrator that resolves an action, and the resolution already
/// knows whether the action was terminal: the terminal paths of
/// <see cref="BattleStateService"/> emit <c>BattleWon</c>/<c>BattleLost</c>
/// (<c>GAME_EVENTS.md</c> §2 — the only documented end signal, because
/// <c>GAME_STATE.md</c> §2.0.3 forbids a lifecycle field on the state). This
/// service owns the step those paths invoke, so the ordering rule lives in one
/// place rather than being repeated at each terminal branch.
///
/// <b>It coordinates; it computes nothing.</b> Every value it persists is read
/// from the authoritative state it is handed or returned by the persistence
/// boundary: the battle id, the owning Player, and the Pet instance come from
/// the state (<c>DATABASE.md</c> §1 sourcing item 2); the Boss key comes from the
/// documented <c>Identity</c> lookup (note item 2); the outcome comes from the
/// resolution's own events (<c>GAME_EVENTS.md</c> §2); the duration is the
/// state's <c>Turn</c> (<c>DATABASE.md</c> §1 "Duration and completion sourcing"
/// item 1); the completion instant is this server's clock at the durable write
/// (item 2); and the reward summary is the documented staging value
/// <c>{}</c> until TASK-033 owns its member list. No client value, no session
/// value, no display name, and no derived key enters this path
/// (<c>GAME_RULES.md</c> §18, <c>ADR-001</c>, <c>AGENTS.md</c> §10).
///
/// <b>It must never:</b>
/// <list type="bullet">
/// <item>delete the active state before the PostgreSQL write succeeded
/// (<c>ARCHITECTURE.md</c> §4 item 4, <c>REDIS_STATE.md</c> §3),</item>
/// <item>write a result when the Boss definition did not resolve — the path
/// fails closed and the battle stays recoverable
/// (<c>DATABASE.md</c> §1 sourcing item 3),</item>
/// <item>fabricate a <c>BossDefinitionId</c>: not the canonical Identity, not a
/// display name, not a null, an empty string, a default, or a mapping convention
/// (<c>DATABASE.md</c> §1 note item 2, sourcing item 3),</item>
/// <item>invent reward content — the summary is the documented empty object, and
/// no XP, line item, magnitude, or curve is introduced
/// (<c>AGENTS.md</c> §7; TASK-033 owns them),</item>
/// <item>add a retry worker, queue, completion service, cache, or second
/// idempotency mechanism: a duplicate cannot arise because
/// <c>BattleResultId</c> is the battle's own <c>BattleId</c>
/// (<c>DATABASE.md</c> §1, <c>ARCHITECTURE.md</c> §5).</item>
/// </list>
/// </summary>
public sealed class BattleResultService
{
    /// <summary>
    /// The documented staging value of <c>RewardSummary</c> until TASK-033 owns
    /// its member list (<c>DATABASE.md</c> §1: "the documented staging value is
    /// the <b>empty JSON object <c>{}</c></b> — a value that is always present,
    /// never absent, for both <c>Outcome</c>s").
    ///
    /// It is a constant rather than a computed document: no reward field, value,
    /// amount, or XP curve exists to compute one from, and deriving a summary
    /// would be inventing the semantics TASK-033 owns.
    /// </summary>
    public const string EmptyRewardSummary = "{}";

    private readonly IBattleResultRepository _results;
    private readonly IBossDefinitionLookup _bossDefinitions;
    private readonly IBattleStateRepository _battles;
    private readonly TimeProvider _clock;

    /// <summary>
    /// Creates the battle-end persistence boundary over the durable result store,
    /// the documented lookup boundary, the active-state store, and the server
    /// clock.
    ///
    /// <b>Every dependency is required and none has a default.</b> The result
    /// store is where the durable record goes; without it there is no result to
    /// write. The lookup is the documented battle-end read; without it the
    /// foreign key cannot be resolved and the path must not guess. The
    /// active-state store is the record the delete acts on
    /// (<c>REDIS_STATE.md</c> §3), and the clock is the only permitted source of
    /// <c>CompletedAt</c> (<c>DATABASE.md</c> §1 sourcing item 2) — a defaulted
    /// clock would let a caller silently persist a non-server instant.
    /// </summary>
    /// <param name="results">
    /// The durable result store (<c>DATABASE.md</c> §1).
    /// </param>
    /// <param name="bossDefinitions">
    /// The Identity → <c>BossDefinitionId</c> lookup
    /// (<c>DATABASE.md</c> §1 note item 2).
    /// </param>
    /// <param name="battles">
    /// The active battle state store whose record is cleared on battle end
    /// (<c>REDIS_STATE.md</c> §3).
    /// </param>
    /// <param name="clock">
    /// The server clock (<c>DATABASE.md</c> §1 "Duration and completion
    /// sourcing" item 2 — "the server clock reading captured on the battle-end
    /// path when the durable result is written").
    /// </param>
    public BattleResultService(
        IBattleResultRepository results,
        IBossDefinitionLookup bossDefinitions,
        IBattleStateRepository battles,
        TimeProvider clock)
    {
        _results = results ?? throw new ArgumentNullException(nameof(results));
        _bossDefinitions = bossDefinitions ?? throw new ArgumentNullException(nameof(bossDefinitions));
        _battles = battles ?? throw new ArgumentNullException(nameof(battles));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <summary>
    /// Persists the durable result for a battle that has just reached
    /// <paramref name="outcome"/>, then clears the battle's active state —
    /// in that order (<c>ARCHITECTURE.md</c> §4 item 4, <c>REDIS_STATE.md</c>
    /// §3).
    ///
    /// <b>The ordering is the contract.</b> The PostgreSQL write happens first
    /// and the delete only after it has completed. If the write does not happen,
    /// this method returns without deleting: the battle-end path has failed
    /// closed (<c>DATABASE.md</c> §1 sourcing item 3), the authoritative
    /// <c>BattleState</c> is still in Redis under its normal sliding TTL
    /// (<c>REDIS_STATE.md</c> §3), and the battle can be retried once the
    /// configuration issue is resolved.
    ///
    /// <b>An unresolved <c>BossDefinition</c> fails the write closed.</b> The
    /// lookup runs before anything is constructed; when it yields nothing, no
    /// row is written and no key is deleted, and this method reports that with
    /// <c>false</c> so the caller's outcome is not mistaken for a completed
    /// persistence. This is the only fail-closed condition on the path:
    /// <c>DATABASE.md</c> §1 states that <c>BattleResult</c> persistence requires
    /// no Player combat-readiness condition and that no Player-side eligibility
    /// predicate is part of the contract, so nothing else gates the write.
    ///
    /// <b>A failed delete does not fail the battle.</b> When the result write
    /// succeeded and the delete did not, the durable result stands and the
    /// method still reports <c>true</c>: <c>REDIS_STATE.md</c> §3 performs no
    /// automatic retry and no worker exists for it, the TTL remains the cleanup
    /// path, and the primary-key contract makes a second row impossible.
    ///
    /// <b>Failure is raised, not absorbed.</b> A PostgreSQL failure propagates
    /// to the caller — this boundary never reports a write that did not happen,
    /// and never deletes the active state on that path.
    /// </summary>
    /// <param name="state">
    /// The authoritative post-resolution <c>BattleState</c> the terminal action
    /// produced (<c>GAME_STATE.md</c> §2, §5.1) — the source of the result's
    /// identities and of <c>DurationTurns</c>.
    /// </param>
    /// <param name="outcome">
    /// The terminal outcome the resolution reported (<c>GAME_EVENTS.md</c> §2).
    /// </param>
    /// <param name="cancellationToken">Cancels the lookups, the write, and the delete.</param>
    /// <returns>
    /// <c>true</c> when the durable result was written — including when the
    /// subsequent active-state delete failed, which leaves the result valid;
    /// <c>false</c> when the battle-end write failed closed because the Boss
    /// definition did not resolve.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// The battle state carries no battle id, owning Player, Pet instance, or
    /// Boss identity. These are present from battle creation
    /// (<c>GAME_STATE.md</c> §2.8, §2.3, §2.4) and none may be invented for a
    /// missing one.
    /// </exception>
    public async Task<bool> PersistTerminalResultAsync(
        BattleState state,
        BattleOutcome outcome,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);

        var battleId = state.BattleId;
        var playerId = state.PlayerId.Value;
        var petInstanceId = state.PetState.PetId.Value;
        var bossIdentity = state.BossState.BossId.Value;

        ArgumentException.ThrowIfNullOrWhiteSpace(battleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(playerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(petInstanceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(bossIdentity);

        // ===============================================================
        // The documented battle-end read (DATABASE.md §1 note item 2):
        // BossState.BossId (canonical Identity) → the row's persistence key.
        // ===============================================================
        // DATABASE.md §1 note item 2: the lookup is a PostgreSQL read "performed
        // on the battle-end path", and TDD.md §4 item 3 keeps PostgreSQL off the
        // hot resolution path — this is the terminal path, where it is allowed.
        var bossDefinitionId = await _bossDefinitions
            .FindBossDefinitionIdByIdentityAsync(bossIdentity, cancellationToken)
            .ConfigureAwait(false);

        // DATABASE.md §1 sourcing item 3: the battle-end path treats a
        // BossDefinition it cannot resolve as a server-side battle-resolution
        // failure — no BattleResult row is written, and the active state is NOT
        // deleted. The identity is never taken from the canonical Identity
        // (item 2, "never substitute one value for the other"), from a display
        // name, from a null/empty/default, or from a mapping convention (note
        // item 5 item 4), and the foreign key is never written by skipping the
        // constraint.
        //
        // This is the ONLY condition the battle-end write fails closed on.
        // DATABASE.md §1 states that BattleResult persistence requires no Player
        // combat-readiness condition and that no Player-side eligibility
        // predicate is part of the contract, so the path consults no such
        // condition and no substitute for one.
        if (bossDefinitionId is not { Length: > 0 })
        {
            return false;
        }

        // ===============================================================
        // The durable shape (DATABASE.md §1) — server-authoritative values only.
        // ===============================================================
        var result = new BattleResult(
            // DATABASE.md §1 sourcing item 1: BattleResultId IS the battle's own
            // BattleId — no second identifier, and the primary key is what makes
            // a second row for one battle impossible.
            BattleResultId: battleId,

            // §1 sourcing item 2: copied from the authoritative state, never
            // re-derived from a session or from client input at battle end.
            PlayerId: playerId,

            // §1 sourcing item 2 / GAME_STATE.md §2.3: the owned Pet INSTANCE.
            PetInstanceId: petInstanceId,

            // §1 note item 2: the resolved row key, never the Identity.
            BossDefinitionId: bossDefinitionId,

            // GAME_EVENTS.md §2 owns the value set; the resolution's own terminal
            // reports are what selected it (GAME_RULES.md §1.4).
            Outcome: outcome,

            // DATABASE.md §1 "Duration and completion sourcing" item 1: the
            // authoritative Turn at terminal resolution. The terminal Turn is
            // counted (TurnEnded precedes BattleWon/BattleLost —
            // GAME_EVENTS.md §1), and Sequence is never the source.
            DurationTurns: state.Turn,

            // Item 2: the server clock reading at this write. Never a client
            // timestamp, a JWT instant, the creation time, or a Redis/SignalR
            // timestamp.
            CompletedAt: _clock.GetUtcNow(),

            // DATABASE.md §1: {} until TASK-033 owns the member list. No reward
            // value is invented (AGENTS.md §7).
            RewardSummary: EmptyRewardSummary);

        // ===============================================================
        // The durable write, first (ARCHITECTURE.md §4 item 4).
        // ===============================================================
        // A failure here propagates: REDIS_STATE.md §3 conditions the delete on
        // this write having happened, so an exception must leave the record in
        // place rather than let the caller proceed to the delete.
        await _results.AddAsync(result, cancellationToken).ConfigureAwait(false);

        // ===============================================================
        // The active-state delete, only after the write (REDIS_STATE.md §3).
        // ===============================================================
        // §3: "Deleted: explicitly, when BattleWon/BattleLost is resolved and the
        // result has been written to PostgreSQL", and, when the delete fails,
        // "no automatic retry is performed and no worker or queue exists for it:
        // the sliding TTL above remains the cleanup path". A failed delete is
        // therefore absorbed here — deliberately, because it is the documented
        // no-retry outcome and not a second failure mode: the durable result is
        // written, the battle's own key is what the TTL will clear, and
        // BattleResultId = BattleId means no second result can arise.
        try
        {
            await _battles.DeleteAsync(battleId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // REDIS_STATE.md §3's documented delete-failure behaviour: the result
            // stands, the TTL cleans the record up, and there is nothing to
            // retry. A cancelled request is not this case and propagates.
        }

        return true;
    }
}
