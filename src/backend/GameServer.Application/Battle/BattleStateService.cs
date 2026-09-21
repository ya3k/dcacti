using System.Collections.Concurrent;
using GameServer.Domain.Battle;
using GameServer.Domain.Match3;

namespace GameServer.Application.Battle;

/// <summary>
/// Application-layer boundary for the Board Foundation State lifecycle
/// (<c>GAME_STATE.md</c> §2.0.5) and for one Swap resolution
/// (<c>MATCH3_RULES.md</c> §2.1.6).
///
/// Responsibility: coordinate the documented staging flow —
///
/// <code>
/// Battle initialization
///         ↓
/// RNG initialization        (server-chosen seed, GAME_STATE.md §2.6.1)
///         ↓
/// Board generation          (MATCH3_RULES.md §1.2.1, §1.5)
///         ↓
/// Battle State creation     (GAME_STATE.md §2.0.5, §2.7.1)
/// </code>
///
/// — and the documented Swap resolution flow —
///
/// <code>
/// SwapRequest
///         ↓
/// Validation                (MATCH3_RULES.md §2.1.2, via Domain)
///         ↓
/// Exchange + resolution     (MATCH3_RULES.md §2.1.6, §4, via Domain)
///         ↓
/// One write-back            (GAME_STATE.md §5.1)
/// </code>
///
/// — and hold the authoritative state of a battle session so the realtime
/// boundary can deliver it when a client joins that battle's group
/// (<c>SIGNALR_PROTOCOL.md</c> §1.2, §4.1) or when a Swap resolves. It performs
/// sequencing and coordination only — no game rule logic
/// (<c>ARCHITECTURE.md</c> §2.1). Board generation itself, the RNG, the initial-board
/// constraints, swap validation, and the whole board-resolution pipeline all live
/// in Domain; this service only orders the calls and records the result.
///
/// It must never:
/// <list type="bullet">
/// <item>implement Match-3, combat, or any domain rule,</item>
/// <item>compute an authoritative gameplay value (<c>GAME_RULES.md</c> §18,
/// <c>ADR-001</c>),</item>
/// <item>persist foundation state (<c>REDIS_STATE.md</c> §7.1, §7.4: neither
/// Battle State Foundation nor Board Foundation State is written to Redis;
/// <c>GAME_STATE.md</c> §2.0.5.4) — nor the Match-3 resolution stage, which
/// <c>REDIS_STATE.md</c> §7 item 8 keeps equally deferred,</item>
/// <item>emit or deliver Battle Events — board generation emits none
/// (<c>GAME_STATE.md</c> §2.0.5.2 item 2), and the Swap boundary hands back the
/// events Domain produced without building, altering, or delivering any of them.
/// <c>ReceiveEvents</c> is the protocol's event path
/// (<c>SIGNALR_PROTOCOL.md</c> §3) and is not implemented here,</item>
/// <item>contain SignalR, Hub, or client concerns (<c>ARCHITECTURE.md</c>
/// §2.1).</item>
/// </list>
///
/// The session registry here is deliberately not a battle-state store in the
/// <c>REDIS_STATE.md</c> sense, and not an alternative to it. It holds only the
/// staged subset — no full <c>BattleState</c> (§2) can be expressed here, because
/// <c>PetState</c> and <c>BossState</c> do not exist and <c>PlayerState</c>
/// carries only its Match / Combo members (§2.2) — and it
/// is process-local and safe to lose, exactly as §2.0.5.4 describes: Board
/// Foundation State is not persisted to Redis or PostgreSQL.
/// </summary>
public sealed class BattleStateService
{
    private readonly ConcurrentDictionary<string, BattleState> _battles = new(StringComparer.Ordinal);
    private readonly IRngSeedSource _seedSource;

    public BattleStateService()
        : this(new SystemEntropyRngSeedSource())
    {
    }

    public BattleStateService(IRngSeedSource seedSource)
    {
        _seedSource = seedSource ?? throw new ArgumentNullException(nameof(seedSource));
    }

    /// <summary>
    /// Creates the authoritative Board Foundation state for a new battle session
    /// (<c>GAME_STATE.md</c> §2.0.5, §2.7.1).
    ///
    /// The server chooses the battle's seed from its own entropy (§2.6.1), then
    /// the board is generated deterministically from that seed
    /// (<c>MATCH3_RULES.md</c> §1.2.1), and the accepted board plus the retained
    /// resulting RNG state become the battle state (§2.7.1 steps 5–6).
    ///
    /// Generation is initialization, not resolution: it consumes no Turn and no
    /// <c>Sequence</c>, both of which remain <c>0</c> (§2.0.5.2 item 1), and it
    /// emits no Battle Event (§2.0.5.2 item 2). Nothing here increments,
    /// re-rolls, or repairs anything.
    ///
    /// This is not a battle-creation endpoint or hub method. Battle creation
    /// remains <c>POST /api/battle/start</c> (<c>API_CONTRACTS.md</c> §3), which
    /// is unchanged by this stage and which no board-foundation battle can
    /// satisfy, because the loadout systems it validates do not exist yet
    /// (<c>REDIS_STATE.md</c> §7.3–§7.4, <c>ROADMAP.md</c> §1 Phase 2).
    /// </summary>
    /// <exception cref="GameServer.Domain.Match3.BoardGenerationFailedException">
    /// No candidate board satisfied the documented initial-board constraints
    /// within the documented 64-attempt bound (<c>MATCH3_RULES.md</c> §1.5
    /// item 3). The battle creation is rejected as an error and no state is
    /// recorded; the seed is not changed and no fallback board is substituted.
    /// </exception>
    public BattleState CreateBattle(string battleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(battleId);

        var seed = _seedSource.CreateSeed();
        var state = BattleState.Create(battleId, seed);

        _battles[battleId] = state;
        return state;
    }

    /// <summary>
    /// Returns the authoritative state for a battle, or <c>null</c> when no
    /// session with that id exists.
    /// </summary>
    public BattleState? GetBattle(string battleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(battleId);

        return _battles.TryGetValue(battleId, out var state) ? state : null;
    }

    /// <summary>
    /// Returns the state to deliver to a client that has just joined a battle's
    /// group, or <c>null</c> when the group names no known battle.
    ///
    /// This is the server-side half of the initial state push
    /// (<c>SIGNALR_PROTOCOL.md</c> §4, §4.1): joining the group is what triggers
    /// delivery, so the caller asks for the state rather than invoking a separate
    /// request. Board generation happens server-side before this push;
    /// <c>BattleStateUpdated</c> reports the result and never triggers generation
    /// (§4.1 item 4). No value is derived, adjusted, or recomputed here.
    /// </summary>
    public BattleState? GetInitialStateForGroup(string battleId)
    {
        return GetBattle(battleId);
    }

    /// <summary>
    /// Resolves the board of a battle to stability and records the resulting
    /// authoritative state (<c>MATCH3_RULES.md</c> §4).
    ///
    /// This is the Application-layer boundary for one board resolution: it orders
    /// the documented calls and records the result, and it implements no game rule
    /// (<c>ARCHITECTURE.md</c> §2.1). The cascade loop, Match Detection, Special Gem
    /// creation and activation, Gravity, and Spawn all live in Domain
    /// (<c>ARCHITECTURE.md</c> §4.1).
    ///
    /// The whole resolution runs against the battle's retained <c>RngState</c>
    /// (<c>GAME_STATE.md</c> §2.6.2 item 2): Spawn is the only operation that advances
    /// it (<c>MATCH3_RULES.md</c> §4.5 item 4), and the resulting state is retained so
    /// a recovered battle resumes the same stream (ADR-008).
    ///
    /// <b>Scope.</b> This resolves the board only, from whatever board the state
    /// currently holds. It is not the Swap path: it validates no Swap, takes no
    /// <see cref="SwapRequest"/>, exchanges nothing, commits no pair, emits no
    /// Battle Event, and — because it is not a Swap — leaves <c>Turn</c> and
    /// <c>Sequence</c> unchanged (<c>MATCH3_RULES.md</c> §8.1 item 4, §8.2
    /// item 2). A committed Swap's exchange *and* resolution is
    /// <see cref="ExecuteSwap"/>, which is the documented
    /// <c>§2.1.6</c> sequence; this method remains the board-resolution entry
    /// point for a state that has no Swap behind it.
    /// </summary>
    /// <param name="battleId">The battle whose board is resolved.</param>
    /// <param name="swapOriginIndex">
    /// The swap origin for the first detection pass, or <c>null</c> when the board is
    /// not being resolved from a Swap (in which case every Match 4 / Match 5 is placed
    /// at its line centre — <c>MATCH3_RULES.md</c> §5.5.3 item 3).
    /// </param>
    /// <returns>
    /// The resulting authoritative state, or <c>null</c> when no session with that id
    /// exists.
    /// </returns>
    public BattleState? ResolveBoard(string battleId, int? swapOriginIndex = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(battleId);

        if (!_battles.TryGetValue(battleId, out var state))
        {
            return null;
        }

        // The battle's retained RngState is where the stream resumes
        // (GAME_STATE.md §2.6.2 item 2). Spawn advances it and by nothing else
        // (MATCH3_RULES.md §7.2 item 1).
        var rng = new Pcg32(state.RngState.State, state.RngState.Increment);

        var resolution = CascadeResolver.Resolve(state.BoardState, rng, swapOriginIndex);

        var resolved = state with
        {
            BoardState = resolution.Board,

            // §8.4: Spawn is the step that advances RngState; the resolution ends with
            // the state Spawn produced.
            RngState = resolution.RngState,
        };

        _battles[battleId] = resolved;

        return resolved;
    }

    /// <summary>
    /// Executes one requested Swap against a battle's authoritative state and
    /// records the resulting state (<c>MATCH3_RULES.md</c> §2.1.6, §8.3).
    ///
    /// This is the Application-layer boundary for one Swap resolution: it orders
    /// the documented calls and records the result, and it implements no game rule
    /// (<c>ARCHITECTURE.md</c> §2.1, §4.1). Validation, the exchange, Match
    /// Detection, Special Gem creation and activation, Gravity, Spawn, and the
    /// cascade loop all live in Domain — this method delegates to
    /// <see cref="SwapExecutor.Execute"/>, which sequences them, and stores what
    /// it returns. There is no second swap pipeline here.
    ///
    /// <b>A rejected request writes nothing.</b> The authoritative state is left
    /// exactly as it was — board, <c>Turn</c>, <c>Sequence</c>, <c>RngState</c>,
    /// <c>LastCommittedSwapPair</c>, and the player's <c>MatchCount</c> and
    /// <c>Combo</c> — and the caller receives the validator's
    /// reason (<c>MATCH3_RULES.md</c> §2.1.5). No partial swap is performed before
    /// the checks pass.
    ///
    /// <b>A committed request is one write-back.</b> The resolved state the
    /// executor returns already carries the stable board, the advanced
    /// <c>RngState</c>, the begun <c>Turn</c>, the incremented <c>Sequence</c>,
    /// the recorded committed pair, and the resolution's Match/Combo values
    /// together, so the store is only ever handed
    /// a consistent post-resolution state (<c>GAME_STATE.md</c> §5.1).
    ///
    /// <b>Scope.</b> This resolves one Swap and nothing else, and the Match/Combo
    /// accounting it records is the Domain executor's (<c>GAME_STATE.md</c> §2.2,
    /// <c>MATCH3_RULES.md</c> §6) — this boundary computes no gameplay value.
    /// The ordered Battle Events of the resolution travel on the returned result
    /// (<c>GAME_EVENTS.md</c> §1.1, <c>SwapExecutionResult.Events</c>), which
    /// this boundary neither builds nor alters: it stores state and hands back
    /// what Domain produced. No event is delivered from here —
    /// <c>ReceiveEvents</c> is the protocol's event path
    /// (<c>SIGNALR_PROTOCOL.md</c> §3) and remains unimplemented — and
    /// no state is persisted (<c>REDIS_STATE.md</c> §7 items 8 and 12). Recording the
    /// resolved state in the process-local registry is not Redis persistence: it
    /// is the same staged, safe-to-lose boundary this service already holds.
    /// </summary>
    /// <param name="battleId">The battle the Swap applies to.</param>
    /// <param name="request">The two §1.0 cell indices the player is exchanging.</param>
    /// <returns>
    /// The rejection or commit result, or <c>null</c> when no session with that id
    /// exists — consistent with <see cref="GetBattle"/> and
    /// <see cref="ResolveBoard"/>, which resolve nothing for an unknown battle
    /// rather than creating one.
    /// </returns>
    public SwapExecutionResult? ExecuteSwap(string battleId, SwapRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(battleId);

        if (!_battles.TryGetValue(battleId, out var state))
        {
            return null;
        }

        var result = SwapExecutor.Execute(state, request);

        // §2.1.5: a rejected action writes nothing, so the registry keeps the state
        // it already held. Only a commit replaces it.
        if (result.IsAccepted)
        {
            _battles[battleId] = result.State;
        }

        return result;
    }

    /// <summary>Number of battle sessions currently held.</summary>
    public int ActiveBattleCount => _battles.Count;
}