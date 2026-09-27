using System.Globalization;
using System.Text;
using GameServer.Application.Pets;
using GameServer.Application.Players;
using GameServer.Domain.Battle;
using GameServer.Domain.Pets;
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
/// Player XP grant + Pet XP grant       (COMBAT_RULES.md §7.2, §7.4;
///         ↓                             PET_RULES.md §5.3, §5.4 — resolved here,
///         ↓                             so the reward members can be recorded)
///         ↓
/// BattleResult                         (DATABASE.md §1 — the documented fields)
///         ↓
/// PostgreSQL write                     (this boundary — the first write is the
///         ↓                             exactly-once point)
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
/// <b>It coordinates; it computes nothing itself.</b> Every value it persists is
/// read from the authoritative state it is handed or returned by the persistence
/// boundary: the battle id, the owning Player, and the Pet instance come from
/// the state (<c>DATABASE.md</c> §1 sourcing item 2); the Boss key comes from the
/// documented <c>Identity</c> lookup (note item 2); the outcome comes from the
/// resolution's own events (<c>GAME_EVENTS.md</c> §2); the duration is the
/// state's <c>Turn</c> (<c>DATABASE.md</c> §1 "Duration and completion sourcing"
/// item 1); the completion instant is this server's clock at the durable write
/// (item 2); the two reward grants are the documented outcome amounts, applied
/// through the Domains' own calculations (<c>COMBAT_RULES.md</c> §7.2/§7.4,
/// which <see cref="Player.GrantBattleXp"/> implements, and <c>PET_RULES.md</c>
/// §5.3/§5.4, which <see cref="Pet.GrantBattleXp"/> implements); and the reward
/// summary is projected from those two grants' resulting values
/// (<c>DATABASE.md</c> §1 "Reward semantics"). No client value, no session
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
/// <item>derive the reward from anything but the outcome — <c>COMBAT_RULES.md</c>
/// §7.2 and <c>PET_RULES.md</c> §5.3 fix two amounts each and no third case is
/// invented (<c>AGENTS.md</c> §7),</item>
/// <item>apply either grant more than once for one battle: both are bound to the
/// first durable write, which the existing primary-key guard already identifies
/// (<c>DATABASE.md</c> §1 sourcing item 1),</item>
/// <item>let one track read or write the other: <c>ADR-016</c> item 12 and
/// <c>PET_RULES.md</c> §5.1 item 5 make the Player and Pet pools independent, so
/// each grant touches only its own instance and neither is derived from the
/// other,</item>
/// <item>invent reward <i>summary</i> content beyond the members
/// <c>DATABASE.md</c> §1 defines — no XP amount other than the documented
/// grants, no item, no currency, no magnitude, and no curve value is written
/// into it,</item>
/// <item>add a retry worker, queue, completion service, cache, or second
/// idempotency mechanism: a duplicate cannot arise because
/// <c>BattleResultId</c> is the battle's own <c>BattleId</c>
/// (<c>DATABASE.md</c> §1, <c>ARCHITECTURE.md</c> §5).</item>
/// </list>
/// </summary>
public sealed class BattleResultService
{
    /// <summary>
    /// The JSON member names the documented <c>RewardSummary</c> contract is
    /// written with (<c>DATABASE.md</c> §1 "Reward semantics for
    /// <c>RewardSummary</c>" items 1, 2 and 5).
    ///
    /// <b>The Player-track names are frozen by the contract</b> (item 1: "These
    /// four members are the complete Player-track contract"). The Pet-track names
    /// are the representation this implementation defines from the semantics
    /// <c>PET_RULES.md</c> §5.3–§5.5 fixes — the member list itself was delegated
    /// to this task by item 2, and TASK-059's illustrative names are explicitly
    /// not contract.
    /// </summary>
    public const string PlayerXpGainedMember = "playerXpGained";
    public const string NewPlayerXpMember = "newPlayerXp";
    public const string PlayerLeveledUpMember = "playerLeveledUp";
    public const string NewPlayerLevelMember = "newPlayerLevel";
    public const string PetXpGainedMember = "petXpGained";
    public const string NewPetXpMember = "newPetXp";
    public const string PetLeveledUpMember = "petLeveledUp";
    public const string NewPetLevelMember = "newPetLevel";

    private readonly IBattleResultRepository _results;
    private readonly IBossDefinitionLookup _bossDefinitions;
    private readonly IBattleStateRepository _battles;
    private readonly IPlayerRepository _players;
    private readonly IPetRepository _pets;
    private readonly TimeProvider _clock;

    /// <summary>
    /// Creates the battle-end persistence boundary over the durable result store,
    /// the documented lookup boundary, the active-state store, the two
    /// progression boundaries, and the server clock.
    ///
    /// <b>Every dependency is required and none has a default.</b> The result
    /// store is where the durable record goes; without it there is no result to
    /// write. The lookup is the documented battle-end read; without it the
    /// foreign key cannot be resolved and the path must not guess. The
    /// active-state store is the record the delete acts on
    /// (<c>REDIS_STATE.md</c> §3). The Player boundary is what the documented
    /// Player reward grant updates (<c>COMBAT_RULES.md</c> §7.2) and the Pet
    /// boundary is what the documented Pet reward grant updates
    /// (<c>PET_RULES.md</c> §5.3) — a defaulted one would let a reward silently
    /// not happen. The clock is the only permitted source of <c>CompletedAt</c>
    /// (<c>DATABASE.md</c> §1 sourcing item 2) — a defaulted clock would let a
    /// caller silently persist a non-server instant.
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
    /// <param name="players">
    /// The Player progression boundary the documented battle reward is applied
    /// through (<c>COMBAT_RULES.md</c> §7.2, <c>DATABASE.md</c> §1). The owning
    /// Player's identity comes from the authoritative state, so this boundary is
    /// used to read and update that Player — never to resolve one from a client
    /// value.
    /// </param>
    /// <param name="pets">
    /// The Pet persistence boundary the documented Pet reward is applied through
    /// (<c>PET_RULES.md</c> §5.3, <c>DATABASE.md</c> §1). The recipient is the
    /// Pet instance the authoritative state already resolved
    /// (<c>BattleState.PetState.PetId</c>, <c>GAME_STATE.md</c> §2.3) — never a
    /// Pet inferred from the Player, from collection order, or from client
    /// input.
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
        IPlayerRepository players,
        IPetRepository pets,
        TimeProvider clock)
    {
        _results = results ?? throw new ArgumentNullException(nameof(results));
        _bossDefinitions = bossDefinitions ?? throw new ArgumentNullException(nameof(bossDefinitions));
        _battles = battles ?? throw new ArgumentNullException(nameof(battles));
        _players = players ?? throw new ArgumentNullException(nameof(players));
        _pets = pets ?? throw new ArgumentNullException(nameof(pets));
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
    /// <b>A repeated terminal persistence reports <c>true</c> without writing.</b>
    /// When the battle's result is already durable, the terminal transition is
    /// already recorded and the battle end is complete, so this method reports
    /// <c>true</c> — the same way a failed active-state delete still reports a
    /// completed persistence. It re-awards nothing, rewrites nothing, and deletes
    /// nothing: the first durable write is the record of what happened, and the
    /// reward for it was granted then (<c>DATABASE.md</c> §1 sourcing item 1,
    /// <c>REDIS_STATE.md</c> §3).
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
    /// <c>true</c> when the durable result stands for this battle at return —
    /// whether this call wrote it, it was already durably recorded by an earlier
    /// terminal persistence, or the subsequent active-state delete failed (which
    /// leaves the result valid); <c>false</c> only when the battle-end write
    /// failed closed because the Boss definition did not resolve.
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
        // Exactly-once pre-check (DATABASE.md §1 sourcing item 1).
        // ===============================================================
        // The two reward grants must be applied to a battle's progression exactly
        // once, so they must not be applied at all when this battle's terminal
        // result is already durable. `BattleResultId` IS the battle's own
        // `BattleId`, so asking the store for that key is the same primary-key
        // guard the write below uses — no ledger, lock, or second idempotency
        // mechanism is introduced.
        //
        // The check has to happen BEFORE the grants rather than only around the
        // writes: a retry re-reads the same progression rows and would otherwise
        // re-apply the grant to the in-memory entities a second time. Today that
        // would corrupt the values any later `SaveChangesAsync` persists; the
        // ordering makes it impossible instead.
        //
        // This is a probe, not the decision: the write below still reports the
        // authoritative first-write result, and the grants are applied only when
        // both agree that this is the first durable write.
        var alreadyDurable = await _results
            .GetByIdAsync(battleId, cancellationToken)
            .ConfigureAwait(false) is not null;

        if (alreadyDurable)
        {
            // The terminal transition was already recorded, so the reward for it
            // was already granted and stored when that first write happened.
            // Nothing is re-awarded, nothing is rewritten, and the stored summary
            // is left exactly as the first write left it (TASK-067 §5.5 (b)) —
            // the in-memory entities are deliberately not even read, so no later
            // SaveChangesAsync could pick up a re-applied grant.
            //
            // `true` reports the documented end state: the durable result stands
            // for this battle, so its battle end is complete. The active state is
            // not deleted here for the same reason the retry does not re-write the
            // result — the first durable write is the record of what happened, and
            // REDIS_STATE.md §3's TTL remains the cleanup path for the key.
            return true;
        }

        // ===============================================================
        // The two documented reward grants (COMBAT_RULES.md §7.2, §7.4;
        // PET_RULES.md §5.3, §5.4).
        // ===============================================================
        // Sequencing (TASK-067 §5.5): the grants are RESOLVED here, before the
        // durable row is built, because RewardSummary records their post-grant
        // values and a reward summary cannot state an outcome it has not yet
        // produced.
        //
        // This does not weaken the exactly-once guard. The write below is still
        // what decides whether the row becomes durable, and it is reached only on
        // the path the pre-check above has already established is the first:
        //   (a) the primary-key insert remains the exactly-once source of truth —
        //       `BattleResultId` = `BattleId` is what the probe and the insert
        //       both key on;
        //   (b) a retry neither re-awards nor rewrites: it returned above, so no
        //       in-memory entity is mutated and the stored row is untouched;
        //   (c) no second pipeline, service, or transaction architecture is
        //       introduced — these are still the same two Domain grants, in the
        //       same method, ahead of the same single `SaveChangesAsync`.
        //
        // A battle whose owning Player or Pet row is absent simply has no
        // progression to maintain (DATABASE.md §1).
        var player = await _players.GetByIdAsync(playerId, cancellationToken).ConfigureAwait(false);
        var pet = await _pets.GetByIdAsync(petInstanceId, cancellationToken).ConfigureAwait(false);

        // COMBAT_RULES.md §7.2 / PET_RULES.md §5.3: each track fixes two
        // amounts and GAME_EVENTS.md §2 fixes exactly two outcomes, so each
        // grant is a lookup over that closed set. §7.3 / §5.4 keep each track's
        // REWARD AMOUNT and CURVE CONSTANT as independent concepts even though
        // both currently equal 100, so the amounts are read from the reward
        // constants that own them.
        var playerXpGained = outcome switch
        {
            BattleOutcome.Victory => Player.BattleWonXpReward,
            BattleOutcome.Defeat => Player.BattleLostXpReward,
            _ => throw new ArgumentOutOfRangeException(
                nameof(outcome),
                outcome,
                "GAME_EVENTS.md §2 defines exactly two outcomes; no third reward case exists."),
        };

        var petXpGained = outcome switch
        {
            BattleOutcome.Victory => Pet.BattleWonXpReward,
            BattleOutcome.Defeat => Pet.BattleLostXpReward,
            _ => throw new ArgumentOutOfRangeException(
                nameof(outcome),
                outcome,
                "GAME_EVENTS.md §2 defines exactly two outcomes; PET_RULES.md §5.3 item 5 defines no other reward case."),
        };

        // The Level each entity holds BEFORE its grant, so the "did it level up"
        // members can be stated from the change the grant actually made rather
        // than guessed from the post-grant state alone.
        var playerLevelBeforeGrant = player?.Level;
        var petLevelBeforeGrant = pet?.Level;

        // The resulting values are computed here, WITHOUT mutating the entities
        // yet. The RewardSummary has to record post-grant values, but the grant
        // itself must not be applied to a row that this call may fail to make
        // durable: a failed write must leave the battle — and its progression —
        // exactly as they were, so the eventual successful retry is what grants
        // once (DATABASE.md §1 sourcing item 3).
        //
        // COMBAT_RULES.md §7.2/§7.4: the outcome's amount added to the current XP,
        // with the Level re-derived from the result. §7.5 item 1 keeps the Player
        // XP uncapped.
        var playerXpAfterGrant = player is null
            ? (int?)null
            : player.XP + playerXpGained;

        // PET_RULES.md §5.3/§5.4/§5.5: the same shape on the Pet's own pool, with
        // this track's own HARD cap applied — a grant crossing 4900 lands exactly
        // on it and a grant at 4900 stores nothing (§5.5 items 1-3). The recipient
        // is the Pet the authoritative state already resolved (§5.3 item 1); every
        // other owned Pet is untouched because no other instance is read or
        // written (item 3).
        var petXpAfterGrant = pet is null
            ? (int?)null
            : Math.Min(pet.XP + petXpGained, Pet.MaxXp);

        var playerLevelAfterGrant = playerXpAfterGrant is null
            ? (int?)null
            : Player.LevelForXp(playerXpAfterGrant.Value);

        var petLevelAfterGrant = petXpAfterGrant is null
            ? (int?)null
            : Pet.LevelForXp(petXpAfterGrant.Value);

        var playerLeveledUp = player is null
            ? (bool?)null
            : playerLevelAfterGrant != playerLevelBeforeGrant;

        var petLeveledUp = pet is null
            ? (bool?)null
            : petLevelAfterGrant != petLevelBeforeGrant;

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

            // DATABASE.md §1 "Reward semantics": the two tracks' documented
            // members, projected from the computed post-grant values above.
            RewardSummary: BuildRewardSummary(
                playerXpGained,
                petXpGained,
                playerXpAfterGrant,
                playerLevelAfterGrant,
                playerLeveledUp,
                petXpAfterGrant,
                petLevelAfterGrant,
                petLeveledUp));

        // ===============================================================
        // The durable write, first (ARCHITECTURE.md §4 item 4).
        // ===============================================================
        // A failure here propagates: REDIS_STATE.md §3 conditions the delete on
        // this write having happened, so an exception must leave the record in
        // place rather than let the caller proceed to the delete.
        //
        // The return value is the existing primary-key guard's own report of
        // whether this call was the FIRST durable write of this battle's result
        // (DATABASE.md §1 sourcing item 1): an identical row already stored means
        // the terminal transition was already recorded and this call wrote
        // nothing.
        var firstDurableWrite = await _results
            .AddAsync(result, cancellationToken)
            .ConfigureAwait(false);

        // ===============================================================
        // The two documented progression writes, bound to the first durable
        // write — the exactly-once point.
        // ===============================================================
        // `firstDurableWrite` is the store's own authoritative report that this
        // call performed the first durable write (DATABASE.md §1 sourcing item
        // 1); the pre-check above already returned for the already-durable case,
        // and this remains the decision that gates both writes. No XP transaction
        // table, distributed lock, or second idempotency mechanism is added
        // (ARCHITECTURE.md §5).
        //
        // The two writes are side by side and sequential, exactly as the two
        // tracks are independent (PET_RULES.md §5.1 item 5, ADR-016 item 12):
        // neither reads the other's values and neither is derived from the
        // other. A missing Player or Pet row means there is nothing to persist
        // on that track (DATABASE.md §1) — the reward does not create either.
        if (firstDurableWrite)
        {
            // The grant is applied here — AFTER the durable write — so a failed
            // write leaves every progression value untouched and the battle
            // recoverable. The values applied are exactly the ones the persisted
            // summary above already recorded, so the stored row and the stored
            // progression cannot disagree.
            if (player is not null)
            {
                player.GrantBattleXp(playerXpGained);

                await _players.SaveProgressionAsync(player, cancellationToken).ConfigureAwait(false);
            }

            if (pet is not null)
            {
                pet.GrantBattleXp(petXpGained);

                await _pets.SaveProgressionAsync(pet, cancellationToken).ConfigureAwait(false);
            }
        }

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

    /// <summary>
    /// Projects the documented <c>RewardSummary</c> document from the two tracks'
    /// post-grant values — <c>DATABASE.md</c> §1 "Reward semantics for
    /// <c>RewardSummary</c>" items 1, 2 and 5.
    ///
    /// <code>
    /// {
    ///   "playerXpGained":  100 | 0   (COMBAT_RULES.md §7.2)
    ///   "newPlayerXp":     Player.XP after applying the grant
    ///   "playerLeveledUp": whether Player.Level changed
    ///   "newPlayerLevel":  Player.Level after applying the grant
    ///   "petXpGained":     100 | 0   (PET_RULES.md §5.3)
    ///   "newPetXp":        the active Pet's XP after applying the grant
    ///   "petLeveledUp":    whether that Pet's Level changed
    ///   "newPetLevel":     that Pet's Level after applying the grant
    /// }
    /// </code>
    ///
    /// <b>Why these eight members are the whole contract.</b> Item 1 freezes the
    /// four Player-track names. Item 2 delegates the Pet-track member list to
    /// this implementation, to be defined from the semantics
    /// <c>PET_RULES.md</c> §5.3–§5.5 already fixes; the smallest set that conveys
    /// them is exactly this one — the recipient's grant amount
    /// (<see cref="PetXpGainedMember"/>), the resulting XP
    /// (<see cref="NewPetXpMember"/>), and the resulting Level
    /// (<see cref="NewPetLevelMember"/>), plus the same "did it change" bit the
    /// Player track carries (<see cref="PetLeveledUpMember"/>). Item 3 forbids
    /// placeholder members, so nothing is added merely to complete a schema, and
    /// no item, currency, streak, bonus, or curve value appears: none is
    /// documented (<c>PET_RULES.md</c> §5.3 item 5, <c>AGENTS.md</c> §7).
    ///
    /// <b>Both tracks are present for both outcomes.</b> Item 5 settles the shape
    /// as identical for <c>BattleWon</c> and <c>BattleLost</c> — the TASK-068
    /// Option A resolution: a defeat serializes <c>playerXpGained = 0</c> and
    /// <c>petXpGained = 0</c> with <c>playerLeveledUp</c>/<c>petLeveledUp</c>
    /// <c>false</c> and the XP/Level members at their unchanged current values.
    /// There is no outcome-specific member arity and no <c>{}</c> on the landed
    /// path (item 4 keeps <c>{}</c> only as the pre-implementation staging
    /// value).
    ///
    /// <b>Every value is post-grant and server-derived.</b> The amounts are the
    /// two outcomes' documented grants; the resulting XP and Level are the values
    /// the grants produce, computed through the two tracks' own documented
    /// formulas (<c>COMBAT_RULES.md</c> §7.4, <c>PET_RULES.md</c> §5.4, with the
    /// Pet hard cap of §5.5 applied), so the summary can never disagree with the
    /// progression the same call persists. Nothing client-supplied participates
    /// (<c>GAME_RULES.md</c> §18, <c>ADR-001</c>, <c>AGENTS.md</c> §10).
    ///
    /// <b>A track with no row reports the documented grant amount and no
    /// resulting state.</b> When the battle's owning Player or Pet row does not
    /// exist there is no progression to record, so the resulting-value members are
    /// JSON <c>null</c> rather than an invented number, and no row is created
    /// (<c>DATABASE.md</c> §1). The grant amount is still the outcome's documented
    /// amount, because that is what the battle awarded.
    /// </summary>
    /// <param name="playerXpGained">The Player track's documented outcome amount
    /// (<c>COMBAT_RULES.md</c> §7.2).</param>
    /// <param name="petXpGained">The Pet track's documented outcome amount
    /// (<c>PET_RULES.md</c> §5.3).</param>
    /// <param name="newPlayerXp">The Player's XP after the grant, or <c>null</c>
    /// when the battle had no owning Player row.</param>
    /// <param name="newPlayerLevel">The Player's Level after the grant, or
    /// <c>null</c> when the battle had no owning Player row.</param>
    /// <param name="playerLeveledUp">
    /// Whether the grant changes <c>Player.Level</c> — computed from the Level
    /// before the grant and the Level the grant produces.
    /// </param>
    /// <param name="newPetXp">The active combat Pet's XP after the grant, or
    /// <c>null</c> when the battle had no such row.</param>
    /// <param name="newPetLevel">That Pet's Level after the grant, or <c>null</c>
    /// when the battle had no such row.</param>
    /// <param name="petLeveledUp">
    /// Whether the grant changes that Pet's <c>Level</c>, computed the same way. A
    /// grant that reaches the <c>4900</c> hard cap reports <c>false</c> once the
    /// Level has stopped rising (<c>PET_RULES.md</c> §5.5), and a defeat's
    /// <c>+0</c> always reports <c>false</c>.
    /// </param>
    private static string BuildRewardSummary(
        int playerXpGained,
        int petXpGained,
        int? newPlayerXp,
        int? newPlayerLevel,
        bool? playerLeveledUp,
        int? newPetXp,
        int? newPetLevel,
        bool? petLeveledUp)
    {
        var summary = new StringBuilder();

        summary.Append('{');

        // --- Player track (DATABASE.md §1 item 1 — the frozen member set) ---
        AppendIntegerMember(summary, PlayerXpGainedMember, playerXpGained);
        summary.Append(',');
        AppendIntegerMember(summary, NewPlayerXpMember, newPlayerXp);
        summary.Append(',');
        AppendBooleanMember(summary, PlayerLeveledUpMember, playerLeveledUp);
        summary.Append(',');
        AppendIntegerMember(summary, NewPlayerLevelMember, newPlayerLevel);

        // --- Pet track (DATABASE.md §1 item 2 — defined here from
        //     PET_RULES.md §5.3-§5.5) ---
        summary.Append(',');
        AppendIntegerMember(summary, PetXpGainedMember, petXpGained);
        summary.Append(',');
        AppendIntegerMember(summary, NewPetXpMember, newPetXp);
        summary.Append(',');
        AppendBooleanMember(summary, PetLeveledUpMember, petLeveledUp);
        summary.Append(',');
        AppendIntegerMember(summary, NewPetLevelMember, newPetLevel);

        summary.Append('}');

        return summary.ToString();
    }

    /// <summary>
    /// Appends one integer member. A track with no row has no resulting value,
    /// which is written as JSON <c>null</c> rather than an invented number.
    /// </summary>
    private static void AppendIntegerMember(StringBuilder summary, string name, int? value)
    {
        summary.Append('"').Append(name).Append("\":");

        if (value is null)
        {
            summary.Append("null");
            return;
        }

        summary.Append(value.Value.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Appends one boolean member. A track with no row has no resulting state, so
    /// the flag is written as JSON <c>null</c> rather than a fabricated
    /// <c>false</c>.
    /// </summary>
    private static void AppendBooleanMember(StringBuilder summary, string name, bool? value)
    {
        summary.Append('"').Append(name).Append("\":");

        if (value is null)
        {
            summary.Append("null");
            return;
        }

        summary.Append(value.Value ? "true" : "false");
    }
}
