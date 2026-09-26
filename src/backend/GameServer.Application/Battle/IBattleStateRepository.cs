using GameServer.Domain.Battle;

namespace GameServer.Application.Battle;

/// <summary>
/// The active battle state persistence boundary (<c>REDIS_STATE.md</c> §1–§4,
/// <c>ARCHITECTURE.md</c> §3 <c>BattleStateRepository (Redis)</c>).
///
/// <code>
/// BattleStateService                   Application — sequencing only
///         ↓  IBattleStateRepository          this contract
/// BattleStateRepository (Redis)          Infrastructure
///         ↓
/// battle:{battleId}:state
/// </code>
///
/// <b>It exposes the documented active-state operations and nothing else.</b>
/// <c>REDIS_STATE.md</c> §1 defines exactly one state key,
/// <c>battle:{battleId}:state</c>; §3 defines its lifecycle (created on battle
/// creation, refreshed on a successful resolution); §4 defines the
/// <c>Sequence</c> compare-and-set that guards the write. Those are the four
/// operations below, and there is deliberately no more surface than that — no
/// enumeration, no deletion, no query, no second record.
///
/// <b>It is not a Redis contract.</b> No key format, TTL value, connection,
/// lock, or client type appears in this file: the members are expressed in
/// terms of the authoritative <see cref="BattleState"/> and its
/// <c>Sequence</c> (<c>GAME_STATE.md</c> §2, §5), which is what the callers
/// own. How the record is stored — key spelling, TTL, script, connection — is
/// the Infrastructure implementation's, exactly as
/// <c>ARCHITECTURE.md</c> §2.1 item 3 requires ("Infrastructure implements
/// persistence … it depends on Domain/Application interfaces, never the other
/// way around"). This is also why the contract is free of Redis concepts the
/// Application layer must not know about.
///
/// <b>Failure is not this contract's to soften.</b> <c>REDIS_STATE.md</c> §2
/// item 2 makes the stored record the single source of truth for a battle's
/// live state and §7 item 5 states that nothing permits that state to live in
/// process memory. An implementation therefore raises its failure to the caller
/// rather than falling back to an in-process store; this interface defines no
/// "unavailable" result that would let a caller treat a missing store as a
/// successful persistence. <c>GAME_STATE.md</c> §2.0.5.4 and
/// <c>ADR-005</c> own that boundary.
/// </summary>
public interface IBattleStateRepository
{
    /// <summary>
    /// Stores the record for a newly created battle (<c>REDIS_STATE.md</c> §3
    /// "Created: on <c>POST /api/battle/start</c>").
    ///
    /// The state is written as it is — creation is not a resolution, so
    /// <c>Turn</c> and <c>Sequence</c> are both <c>0</c>
    /// (<c>GAME_STATE.md</c> §2.0.5.2 item 1) and no compare-and-set applies:
    /// there is no prior value for a sequence to be checked against, and the id
    /// is server-authored and fresh (<c>API_CONTRACTS.md</c> §3).
    /// </summary>
    /// <param name="state">
    /// The authoritative state the battle was created with
    /// (<c>GAME_STATE.md</c> §2, §2.7.1). It is stored losslessly, so a record
    /// read back is the value that was written (<c>REDIS_STATE.md</c> §7
    /// items 9–12).
    /// </param>
    /// <param name="cancellationToken">Cancels the store operation.</param>
    Task CreateAsync(BattleState state, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the record for a battle, or <c>null</c> when no record exists
    /// (<c>REDIS_STATE.md</c> §2 item 2 — the record is the source of truth for
    /// the battle's live state).
    ///
    /// <b>Absence is not "no state yet".</b> A missing record means the battle
    /// is unknown or its key has expired from inactivity
    /// (<c>REDIS_STATE.md</c> §3). It is never repaired by creating a battle,
    /// and a caller must not treat it as an empty state.
    /// </summary>
    /// <param name="battleId">
    /// Identity of the battle (<c>GAME_STATE.md</c> §2.0.1). It is the whole
    /// key — the record is scoped by battle and by nothing else
    /// (<c>REDIS_STATE.md</c> §1).
    /// </param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The authoritative state, or <c>null</c> when none is stored.</returns>
    Task<BattleState?> GetAsync(string battleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes a resolved state back under the documented <c>Sequence</c>
    /// compare-and-set, and reports whether the write was applied
    /// (<c>REDIS_STATE.md</c> §4 items 1–3, 5–6).
    ///
    /// <b>The write succeeds only if the stored <c>Sequence</c> still matches
    /// <paramref name="expectedSequence"/>.</b> <c>REDIS_STATE.md</c> §4 item 2
    /// states the check and §4 item 6 makes <c>Sequence</c> the only
    /// concurrency token — <c>Turn</c> is a game value written inside the record
    /// and is never compared. A mismatch means another resolution committed
    /// first, so the value is <b>not</b> written: the newer authoritative state
    /// is never overwritten by an older one, which is exactly the guarantee §4
    /// item 3 requires. A <c>false</c> result is the caller's signal to retry
    /// against the fresh state (§4 item 2).
    ///
    /// <b>One call is one write-back.</b> §4 item 5 makes the write the
    /// resolution's single write-back: no intermediate board, half-resolved
    /// cascade, or per-pass state is written, and this operation writes the
    /// record once.
    ///
    /// <b>A successful write refreshes the TTL.</b> §3 makes the expiry slide
    /// on every successful resolution, so the refresh is part of this one
    /// operation rather than a second call a caller could forget — and a
    /// failed check performs no write and therefore no refresh (§4 item 7).
    /// </summary>
    /// <param name="state">
    /// The finished post-resolution state (<c>GAME_STATE.md</c> §5.1). Its own
    /// <see cref="BattleState.Sequence"/> is the value written.
    /// </param>
    /// <param name="expectedSequence">
    /// The <c>Sequence</c> read at the start of this resolution — the
    /// pre-resolution value (§4 item 6). The write applies only while the store
    /// still holds it.
    /// </param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>
    /// <c>true</c> when the state was written (the sequences matched);
    /// <c>false</c> when the stored sequence had moved on and the write was
    /// refused.
    /// </returns>
    Task<bool> TryUpdateAsync(
        BattleState state,
        int expectedSequence,
        CancellationToken cancellationToken = default);
}
