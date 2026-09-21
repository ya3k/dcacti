using System.Collections.Concurrent;
using GameServer.Domain.Battle;
using GameServer.Domain.Match3;
using GameServer.Domain.Passives;

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
/// Charge Passive            (GAME_RULES.md §17 step 10, PASSIVE_RULES.md §2)
///         ↓
/// One write-back            (GAME_STATE.md §5.1)
/// </code>
///
/// — and hold the authoritative state of a battle session so the realtime
/// boundary can deliver it when a client joins that battle's group
/// (<c>SIGNALR_PROTOCOL.md</c> §1.2, §4.1) or when a Swap resolves. It performs
/// sequencing and coordination only — no game rule logic
/// (<c>ARCHITECTURE.md</c> §2.1). Board generation itself, the RNG, the initial-board
/// constraints, swap validation, the whole board-resolution pipeline, and the
/// Passive charge/threshold/reset rules all live in Domain; this service only
/// orders the calls, supplies each engine the values the other produced, and
/// records the result.
///
/// It must never:
/// <list type="bullet">
/// <item>implement Match-3, Passive, combat, or any domain rule,</item>
/// <item>compute an authoritative gameplay value (<c>GAME_RULES.md</c> §18,
/// <c>ADR-001</c>) — including Passive progress, which is
/// <see cref="PassiveTracker"/>'s result and is written back unchanged,</item>
/// <item>persist foundation state (<c>REDIS_STATE.md</c> §7.1, §7.4: neither
/// Battle State Foundation nor Board Foundation State is written to Redis;
/// <c>GAME_STATE.md</c> §2.0.5.4) — nor the Match-3 resolution stage, which
/// <c>REDIS_STATE.md</c> §7 item 8 keeps equally deferred, nor
/// <c>PetState</c>, whose persistence <c>REDIS_STATE.md</c> §7 and
/// <c>SIGNALR_PROTOCOL.md</c> §4.3 item 12 leave equally unchanged,</item>
/// <item>emit or deliver Battle Events — board generation emits none
/// (<c>GAME_STATE.md</c> §2.0.5.2 item 2), and the Swap boundary hands back the
/// events Domain and the Passive stage produced without building, altering, or
/// delivering any of them. <c>ReceiveEvents</c> is the protocol's event path
/// (<c>SIGNALR_PROTOCOL.md</c> §3) and is not implemented here,</item>
/// <item>contain SignalR, Hub, or client concerns (<c>ARCHITECTURE.md</c>
/// §2.1).</item>
/// </list>
///
/// The session registry here is deliberately not a battle-state store in the
/// <c>REDIS_STATE.md</c> sense, and not an alternative to it. It holds the staged
/// subset — no full <c>BattleState</c> (§2) can be expressed yet, because
/// <c>BossState</c> does not exist, <c>PetState</c> carries only its Passive
/// members (§2.3), and <c>PlayerState</c> carries only its Match / Combo members
/// (§2.2) — and it
/// is process-local and safe to lose, exactly as §2.0.5.4 describes: Board
/// Foundation State is not persisted to Redis or PostgreSQL.
/// </summary>
public sealed class BattleStateService
{
    /// <summary>
    /// The Passive configuration a battle's active Pet carries.
    ///
    /// Pet selection and progression are not implemented (<c>GAME_STATE.md</c>
    /// §2.3, <c>SIGNALR_PROTOCOL.md</c> §4.3 item 2), so the battle's Passive is
    /// supplied by the caller rather than resolved from a Pet definition. It is
    /// exactly the three values <see cref="PetState"/> holds — the identity, the
    /// Threshold, and the optional non-default Reset Behavior — and it introduces
    /// no fourth: the Threshold is part of the documented progress pair
    /// (<c>PASSIVE_RULES.md</c> §6 item 1) and is data-driven configuration, never
    /// a value this layer invents (<c>ARCHITECTURE.md</c> §5 item 1).
    /// </summary>
    /// <param name="PassiveId">
    /// The active Pet's Passive identity (<c>GAME_STATE.md</c> §2.3). It is set at
    /// battle creation and never changes (§2.3 item 2).
    /// </param>
    /// <param name="PassiveThreshold">
    /// The Passive's Threshold — "e.g. 'every 5 Matches'" (<c>PASSIVE_RULES.md</c>
    /// §1). Progress begins at <c>0</c> against it
    /// (<c>SIGNALR_PROTOCOL.md</c> §4.3 item 4).
    /// </param>
    /// <param name="PassiveResetOverride">
    /// The Passive's declared non-default Reset Behavior, or <c>null</c> for the
    /// default (<c>PASSIVE_RULES.md</c> §4 items 1–3). It must be declared on the
    /// Pet's Passive definition; it is never assumed (§4 item 3).
    /// </param>
    public readonly record struct PassiveConfiguration(
        PassiveId PassiveId,
        int PassiveThreshold,
        PassiveResetBehavior? PassiveResetOverride = null)
    {
        /// <summary>
        /// The <c>PetState</c> this configuration initializes a battle's
        /// <c>BattleState</c> with — progress at the Passive's start
        /// (<c>GAME_STATE.md</c> §2.3 item 3).
        /// </summary>
        public PetState ToPetState() =>
            PetState.AtBattleCreation(PassiveId, PassiveThreshold, PassiveResetOverride);
    }

    private readonly ConcurrentDictionary<string, BattleState> _battles = new(StringComparer.Ordinal);
    private readonly IRngSeedSource _seedSource;

    /// <summary>
    /// The Active Pet's Passive for each battle, attached at creation
    /// (<c>GAME_STATE.md</c> §2.3 item 2: <c>PassiveId</c> "is set at battle
    /// creation and never changes"). It is the battle's loadout input, not battle
    /// state: <c>PetState</c> in <see cref="BattleState"/> is the authoritative
    /// record and is what the resolution reads and writes, so this registry is not
    /// a second copy of any value it holds.
    /// </summary>
    private readonly ConcurrentDictionary<string, PassiveConfiguration> _passiveConfiguration = new(StringComparer.Ordinal);

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
    /// (<c>GAME_STATE.md</c> §2.0.5, §2.2, §2.3, §2.7.1).
    ///
    /// The server chooses the battle's seed from its own entropy (§2.6.1), then
    /// the board is generated deterministically from that seed
    /// (<c>MATCH3_RULES.md</c> §1.2.1), and the accepted board plus the retained
    /// resulting RNG state become the battle state (§2.7.1 steps 5–6). The
    /// player's progression state and the active Pet's Passive state are created
    /// with it at their documented starting values (§2.2, §2.3 item 3).
    ///
    /// Generation is initialization, not resolution: it consumes no Turn and no
    /// <c>Sequence</c>, both of which remain <c>0</c> (§2.0.5.2 item 1), and it
    /// emits no Battle Event (§2.0.5.2 item 2). Nothing here increments,
    /// re-rolls, or repairs anything, and nothing here charges the Passive: no
    /// Match has been produced (<c>PASSIVE_RULES.md</c> §2 item 1).
    ///
    /// This is not a battle-creation endpoint or hub method. Battle creation
    /// remains <c>POST /api/battle/start</c> (<c>API_CONTRACTS.md</c> §3), which
    /// is unchanged by this stage and which no board-foundation battle can
    /// satisfy, because the loadout systems it validates do not exist yet
    /// (<c>REDIS_STATE.md</c> §7.3–§7.4, <c>ROADMAP.md</c> §1 Phase 2).
    /// </summary>
    /// <param name="battleId">Identity of the battle session.</param>
    /// <param name="passiveConfiguration">
    /// The active Pet's Passive — its identity, Threshold, and declared Reset
    /// Behavior. <c>PetState</c> is present from battle creation
    /// (<c>GAME_STATE.md</c> §2.3 item 3) and no value may be invented for it, so
    /// this is required: Pet selection is not implemented and the battle's Passive
    /// configuration is therefore supplied by the caller.
    /// </param>
    /// <exception cref="GameServer.Domain.Match3.BoardGenerationFailedException">
    /// No candidate board satisfied the documented initial-board constraints
    /// within the documented 64-attempt bound (<c>MATCH3_RULES.md</c> §1.5
    /// item 3). The battle creation is rejected as an error and no state is
    /// recorded; the seed is not changed and no fallback board is substituted.
    /// </exception>
    public BattleState CreateBattle(string battleId, PassiveConfiguration passiveConfiguration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(battleId);

        var seed = _seedSource.CreateSeed();

        // §2.3: the battle's PetState is built from the caller's Passive
        // configuration with progress at the start of its first charge. It is
        // never absent, never defaulted with an invented Threshold, and never
        // created lazily on the first Swap.
        var state = BattleState.Create(battleId, seed, passiveConfiguration.ToPetState());

        _battles[battleId] = state;
        _passiveConfiguration[battleId] = passiveConfiguration;

        return state;
    }

    /// <summary>
    /// Returns the Passive configuration the battle's active Pet carries, or
    /// <c>null</c> when no session with that id exists.
    ///
    /// This is the battle's loadout input, attached at creation; the authoritative
    /// current progress is <c>BattleState.PetState</c>
    /// (<c>GAME_STATE.md</c> §2.3, §5.1) and is read from there.
    /// </summary>
    internal PassiveConfiguration? GetPassiveConfiguration(string battleId) =>
        _passiveConfiguration.TryGetValue(battleId, out var configuration) ? configuration : null;

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
    /// <c>MATCH3_RULES.md</c> §6) — this boundary computes no gameplay value. The
    /// Passive charge it records is the Domain tracker's
    /// (<c>GAME_STATE.md</c> §2.3, <c>PASSIVE_RULES.md</c> §2–§5) — this boundary
    /// computes no progress, evaluates no Threshold, and applies no reset. The
    /// ordered Battle Events of the resolution travel on the returned result
    /// (<c>GAME_EVENTS.md</c> §1.1, <c>SwapExecutionResult.Events</c>), which
    /// this boundary assembles only by appending the Passive stage's own reports
    /// to the executor's list — it builds no event, and it reorders, filters,
    /// regroups, and drops none. No event is delivered from here —
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
        //
        // A rejection also charges the Passive nothing: PASSIVE_RULES.md §2 item 1
        // charges per Match, the board pipeline is never reached for a rejected
        // Swap, and Charge is therefore not called at all — the progress, the
        // event list, and every other value are the ones the state already held.
        if (result.IsRejected)
        {
            return result;
        }

        // §17 step 10 / PASSIVE_RULES.md §2, §4, §5: charge the active Pet's
        // Passive over the Matches this Cascade resolution produced. The tracker is
        // a pure Domain function — it reads no board and no RNG, and this call
        // decides no rule.
        //
        // The inputs are the values the two stages already own: the progress held
        // in the authoritative PetState (GAME_STATE.md §2.3, §5.1), the
        // resolution's own Match total (MATCH3_RULES.md §3 item 5, §5.5.5 item 8:
        // Matches only, so a Special Gem detonation charges nothing), the
        // Passive's identity, and its declared Reset Behavior. Nothing is
        // re-derived, and the Match count is never recomputed from the passes.
        var petState = state.PetState;

        var charged = PassiveTracker.Charge(
            petState.PassiveProgress,
            result.Resolution.TotalMatches,
            petState.PassiveId,
            petState.ResetBehavior);

        // §2.3 / GAME_EVENTS.md §2: the settled progress is written back into
        // PetState, in the same single post-resolution write-back the executor
        // already performed for the board, the counters, the committed pair, and
        // the Match/Combo values. The Passive's identity and its declared Reset
        // Behavior are carried across unchanged — §2.3 item 2 makes the identity
        // settable only at battle creation, and §4 makes the behavior a property of
        // the Passive's definition, not of a resolution.
        var resolved = result.State with
        {
            PetState = petState with { PassiveProgress = charged.Progress },
        };

        // GAME_EVENTS.md §1, §1.1 / GAME_RULES.md §17 step 10: the events of this
        // resolution, in the order the pipeline produced them. The executor's list
        // is the Match-3 part, which §1 places from MatchCreated through
        // ComboChanged; the tracker's reports follow it, one PassiveCharged per
        // Match in Match order and then, when the Threshold was crossed, the single
        // PassiveTriggered. Nothing is sorted, filtered, or rebuilt — the assembled
        // list is the two stages' own output concatenated, and WithEvents carries
        // every other member of the result across unchanged.
        var events = new List<BattleEvent>(result.Events.Count + charged.Charges.Count + charged.Triggers.Count);

        events.AddRange(result.Events);
        events.AddRange(charged.Charges.Select(BattleEvent.ForPassiveCharged));
        events.AddRange(charged.Triggers.Select(BattleEvent.ForPassiveTriggered));

        // GAME_STATE.md §5.1: the result carries the SAME single post-resolution
        // write-back that is stored — board, counters, committed pair, Match/Combo
        // values, and now the settled Passive progress, all together. Handing back
        // the executor's pre-charge state would split the write-back in two and let
        // a caller observe a state in which the Passive has not been charged while
        // the batch already reports its charges (§3 item 6: an event is never a
        // substitute for the state write-back).
        var committed = result
            .WithEvents(events)
            .WithState(resolved);

        _battles[battleId] = resolved;

        return committed;
    }

    /// <summary>Number of battle sessions currently held.</summary>
    public int ActiveBattleCount => _battles.Count;
}