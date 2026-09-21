using System.Text.Json.Serialization;
using GameServer.Application.Battle;
using GameServer.Application.Runtime;
using GameServer.Domain.Battle;
using GameServer.Domain.Match3;
using Microsoft.AspNetCore.SignalR;

namespace GameServer.Api.Hubs;

public record PingResponse(bool Accepted, string? ClientSequence, DateTimeOffset ServerTime);

/// <summary>
/// The initial-state push payload (<c>SIGNALR_PROTOCOL.md</c> §4, §4.1, §4.2).
///
/// It carries exactly the fields of the currently implemented
/// <c>GAME_STATE.md</c> §0 stage — for the Match / Combo accounting stage that is
/// <c>battleId</c>, <c>turn</c>, <c>sequence</c>, <c>board</c>, <c>rngSeed</c>,
/// <c>rngState</c>, and <c>playerState</c> — and no others
/// (§4.4, §4.1 item 1). No gameplay field beyond the stage's own is carried, and
/// no <c>Status</c>/lifecycle value is carried anywhere in the protocol (§8.3).
///
/// <c>board</c> carries the server-generated <c>Cells[64]</c>
/// (<c>GAME_STATE.md</c> §2.1.1, <c>MATCH3_RULES.md</c> §1.2). The client renders
/// the received board and must not generate, fill, repair, validate, or re-derive
/// it (§4 item 10).
///
/// <c>rngSeed</c>/<c>rngState</c> are delivered as part of the authoritative state
/// (<c>GAME_STATE.md</c> §2.6). They are included because they are
/// <c>BattleState</c> fields, not because the client uses them: the client never
/// advances, re-seeds, or draws from the RNG, and never uses it to produce a Gem
/// value. A client that needs a board reads <c>board</c> (§4.1 item 2).
///
/// <c>playerState</c> carries the resolution's <c>MatchCount</c> and <c>Combo</c>
/// (<c>GAME_STATE.md</c> §2.2). It is delivered because it is a <c>BattleState</c>
/// field, not because the client computes it: both values are server-determined
/// and the client renders them (<c>GAME_RULES.md</c> §18, §4.9). They are always
/// present, including at their documented <c>0</c> values — zero is a value here,
/// not an absence (§4.2 item 4).
///
/// This remains the only state-push method in the protocol at every stage. No
/// board-specific message (<c>BoardCreated</c>, <c>BoardGenerated</c>,
/// <c>BoardUpdated</c>, <c>BoardReady</c>, or similar) is part of this contract
/// and none is introduced (§4 item 11, §8.5): the board is a field of the battle
/// state, so extending that state is what delivers it. No Match-, Combo-, or
/// progression-specific message is introduced either (§4.2 item 6).
/// </summary>
/// <param name="RngSeed">
/// The battle's server-chosen PRNG seed (<c>GAME_STATE.md</c> §2.6.1), projected
/// one-to-one from the authoritative state.
/// </param>
/// <param name="RngState">
/// The PRNG state after generating the initial board (<c>GAME_STATE.md</c>
/// §2.6.2) — a state + increment <b>pair</b>, not a single word
/// (§2.6.2 item 1).
/// </param>
/// <param name="Board">
/// The authoritative 64-cell board (<c>GAME_STATE.md</c> §2.1.1), projected
/// one-to-one from the authoritative state.
/// </param>
/// <param name="PlayerState">
/// The authoritative <c>PlayerState</c> projection (<c>GAME_STATE.md</c> §2.2) —
/// the resolution's <c>MatchCount</c> and <c>Combo</c>, projected one-to-one from
/// the authoritative state.
/// </param>
public record BattleStateUpdated(
    string BattleId,
    int Turn,
    int Sequence,
    ulong RngSeed,
    RngStatePayload RngState,
    BoardPayload Board,
    PlayerStatePayload PlayerState);

/// <summary>
/// The wire projection of <c>PlayerState</c> (<c>GAME_STATE.md</c> §2.2).
///
/// The two members are the two <c>PlayerState</c> fields this stage implements —
/// <c>Combo</c> and <c>MatchCount</c> (<c>MATCH3_RULES.md</c> §6.1 item 1,
/// <c>GAME_RULES.md</c> §3) — and nothing else: the rest of §2.2 belongs to
/// later stages and is not delivered.
///
/// Both are always present and non-nullable, both default to <c>0</c>, and
/// neither is omitted when it is <c>0</c>: <c>Combo = 0</c> is the value the
/// state reads before the battle's first committed Swap, and it is a value, not
/// a gap (<c>MATCH3_RULES.md</c> §6.5 item 4, <c>GAME_STATE.md</c> §2.1.7
/// item 5).
/// </summary>
/// <param name="Combo">
/// The <c>Combo</c> of the most recently committed Swap — the number of Matches
/// that Swap produced (<c>MATCH3_RULES.md</c> §6.2–§6.3) — or <c>0</c> before the
/// battle's first committed Swap. It is never authored, adjusted, or recomputed
/// by the client (<c>GAME_RULES.md</c> §18).
/// </param>
/// <param name="MatchCount">
/// The cumulative number of Matches this battle has produced
/// (<c>GAME_RULES.md</c> §3). It is battle-cumulative and never reset by a later
/// Swap.
/// </param>
public record PlayerStatePayload(int Combo, int MatchCount);

/// <summary>
/// The wire projection of <c>RngState</c> (<c>GAME_STATE.md</c> §2.6.2).
///
/// The two components stay together as one logical field
/// (<c>GAME_STATE.md</c> §2.6.2 item 1: "the two components are never split
/// across separate <c>BattleState</c> fields"), which is why they travel as one
/// nested object rather than as two flat properties.
/// </summary>
/// <param name="State">The PRNG's current internal state.</param>
/// <param name="Increment">The PRNG's stream selector.</param>
public record RngStatePayload(ulong State, ulong Increment);

/// <summary>
/// The wire projection of <c>BoardState</c> (<c>GAME_STATE.md</c> §2.1.1,
/// <c>SIGNALR_PROTOCOL.md</c> §4.1 item 5).
///
/// <c>Cells</c> is exactly 64 entries in row-major order —
/// <c>index = row * 8 + column</c> (<c>MATCH3_RULES.md</c> §1.0). The client does
/// not receive a partial board and does not request cells individually
/// (§4.1 item 3).
///
/// Each entry carries the cell's Gem type plus, optionally, the Special Gem at
/// that cell — the same shape the state holds, delivered entry for entry with no
/// additional payload member (<c>SIGNALR_PROTOCOL.md</c> §4.1 item 5,
/// <c>GAME_STATE.md</c> §2.1.7 items 1–4). There is no <c>PendingSpecialGems[]</c>
/// and no second collection to deliver (§2.1.2 item 1).
///
/// The client renders this and derives nothing: it never creates, places, moves,
/// matches, activates, chains, or clears a Special Gem, and never infers a
/// Special Gem's type or orientation from anything but the state it was sent
/// (<c>SIGNALR_PROTOCOL.md</c> §4.1 item 6, <c>GAME_RULES.md</c> §18).
/// </summary>
/// <param name="Cells">
/// One cell entry per cell, in ascending §1.0 index order. The array position
/// <b>is</b> the cell index (<c>GAME_STATE.md</c> §2.1.7 item 2), so no index is
/// written per element.
/// </param>
public record BoardPayload(IReadOnlyList<CellPayload> Cells);

/// <summary>
/// The wire projection of one <c>Cells[64]</c> entry
/// (<c>GAME_STATE.md</c> §2.1.1, <c>SIGNALR_PROTOCOL.md</c> §4.1 item 5).
/// </summary>
/// <param name="GemType">
/// The cell's Gem type, as the documented contract name (<c>ATK</c>, <c>DEF</c>,
/// <c>HP</c>, <c>POWER</c> — <c>MATCH3_RULES.md</c> §1.1). It is always present,
/// including for a cell that holds a Special Gem: a Special Gem adds metadata to
/// a cell's occupant and does not replace its Gem type
/// (<c>GAME_STATE.md</c> §2.1.3 items 1–2).
/// </param>
/// <param name="SpecialGem">
/// The Special Gem at this cell, or <c>null</c> for an ordinary Gem. Absence is
/// the documented representation of "this cell holds no Special Gem" — not a
/// null-element sentinel and not one of the three real types
/// (<c>GAME_STATE.md</c> §2.1.7 item 3).
/// </param>
public record CellPayload(string GemType, SpecialGemPayload? SpecialGem);

/// <summary>
/// The wire projection of <c>SpecialGem</c> metadata
/// (<c>GAME_STATE.md</c> §2.1.4).
/// </summary>
/// <param name="Type">
/// <c>LineClear</c>, <c>Burst</c>, or <c>Area</c> — the three MVP types
/// (<c>GAME_STATE.md</c> §2.1.4 item 1, <c>MATCH3_RULES.md</c> §5.2–§5.4).
/// </param>
/// <param name="Orientation">
/// <c>Horizontal</c> or <c>Vertical</c>, present <b>if and only if</b>
/// <paramref name="Type"/> is <c>LineClear</c> (<c>GAME_STATE.md</c> §2.1.4
/// item 2). A <c>Burst</c> or <c>Area</c> entry carries no orientation member,
/// because an orientation on those types would be a value no rule reads.
/// </param>
public record SpecialGemPayload(string Type, string? Orientation);

/// <summary>
/// The direct invocation result of a <c>Swap</c> request
/// (<c>SIGNALR_PROTOCOL.md</c> §5).
///
/// It is transport-level feedback about **the request**, not authoritative state
/// and not a Battle Event: it is delivered to the caller only, is not broadcast,
/// is not sequenced, and changes nothing by itself (§5 items 1 and 4,
/// <c>GAME_EVENTS.md</c> §1.2).
///
/// <c>Accepted: true</c> means the action was resolved and the resulting
/// authoritative state has already been pushed through <c>BattleStateUpdated</c>
/// (<c>SIGNALR_PROTOCOL.md</c> §4 item 6: the state push carries **state** and no
/// events, and §3 carries **events** and no state). <c>Accepted: false</c> means
/// the action was rejected under <c>MATCH3_RULES.md</c> §2.1.2 and nothing
/// happened: no state changed and no push follows (§5 item 2).
///
/// <c>Reason</c> is the machine-readable rejection code of
/// <c>SIGNALR_PROTOCOL.md</c> §5 item 3 — the failing check of
/// <c>MATCH3_RULES.md</c> §2.1.2 together with the staleness rejection of §2.1.4.
/// The protocol maintains no parallel list, so the codes cannot drift from the
/// rule.
/// </summary>
/// <param name="Accepted">True when the Swap was resolved; false when rejected.</param>
/// <param name="Reason">
/// The documented rejection reason, or <c>null</c> when <paramref name="Accepted"/>
/// is true. <c>INVALID_CELL_INDEX</c>, <c>INVALID_SWAP</c>, <c>STALE_ACTION</c>,
/// and <c>NO_MATCH_FROM_SWAP</c> are the possible values
/// (<c>MATCH3_RULES.md</c> §2.1.2).
/// </param>
public record SwapResponse(bool Accepted, string? Reason);

/// <summary>
/// The <c>ReceiveEvents</c> delivery payload
/// (<c>SIGNALR_PROTOCOL.md</c> §3).
///
/// It carries exactly the three documented members and nothing else:
/// <c>battleId</c>, <c>serverSequence</c>, and <c>events[]</c>. It carries no
/// state (<c>§3.1</c> item 3, <c>§4</c> item 6), no <c>Status</c>/lifecycle value
/// (<c>§8.3</c>), no board snapshot, no Combo or Match-count duplication, no
/// <c>clientSequence</c>, and no event-specific transport message.
///
/// <c>events</c> is the ordered Battle Event list the resolution already produced
/// — <c>SwapExecutionResult.Events</c> — projected one-to-one onto the §3.2 wire
/// schema, in the order <c>GAME_RULES.md</c> §17 and <c>GAME_EVENTS.md</c> §1.1
/// place it. This boundary sorts nothing, filters nothing, regroups nothing, runs
/// no second Match Detection pass, and derives nothing from the final board
/// (<c>SIGNALR_PROTOCOL.md</c> §3 item 1, <c>GAME_EVENTS.md</c> §3 item 6).
///
/// <b>The projected items are wire DTOs, never <c>BattleEvent</c> values.</b>
/// <c>BattleEvent</c> is a Domain type and must never become a wire DTO
/// (<c>§3.2.1</c> item 1): its non-applicable payload accessors throw, so direct
/// serialization of it is invalid by construction. <c>events[]</c> therefore
/// carries <see cref="BattleEventWireDto"/> and the transport layer owns the
/// serialization (<c>§3.2.1</c> item 2).
///
/// <b>Casing and omission are fixed here, not delegated.</b> <c>§3.2.3</c>
/// fixes camelCase explicitly and warns against relying on a serializer
/// default, so every member of this payload and of every wire item is named
/// explicitly. <c>§3.2.5</c> requires a not-applicable member to be
/// <b>omitted</b> rather than written as <c>null</c>, so the non-applicable
/// members of a wire item are skipped at serialization. Neither setting is a
/// serializer-wide policy: they are the contract of this boundary.
/// </summary>
/// <param name="BattleId">
/// The battle the batch belongs to (<c>SIGNALR_PROTOCOL.md</c> §3) — the same id
/// that scopes the group broadcast.
/// </param>
/// <param name="ServerSequence">
/// <c>BattleState.Sequence</c> after this resolution (<c>§3.2</c>,
/// <c>GAME_STATE.md</c> §5) — strictly increasing with no gaps, one value per
/// resolved action however many Matches, Cascades, or Special Gems it contained.
/// It is read from the committed state, not calculated here, and it is not the
/// client's opaque correlation value.
/// </param>
/// <param name="Events">
/// The complete batch of the resolution's events, projected onto the §3.2 wire
/// schema and in TASK-006's order. It is sent whole: the batch is atomic at the
/// message level, so no client ever receives part of a resolution's events
/// (<c>§3.1</c> item 1).
/// </param>
public record ReceiveEventsPayload(
    [property: JsonPropertyName("battleId")] string BattleId,
    [property: JsonPropertyName("serverSequence")] int ServerSequence,
    [property: JsonPropertyName("events")] IReadOnlyList<BattleEventWireDto> Events);

/// <summary>
/// Thin realtime communication boundary (SIGNALR_PROTOCOL.md, ARCHITECTURE.md §1/§2.1).
///
/// The hub translates wire messages into Application-layer calls and back. It
/// must never contain Match-3 logic, combat logic, domain calculations, Redis
/// business logic, or any gameplay rule: those belong to Domain/Application
/// (ARCHITECTURE.md §2.1, GAME_RULES.md §18, ADR-001).
///
/// <c>Swap</c> is implemented as the thin translation of the documented request
/// (<c>SIGNALR_PROTOCOL.md</c> §2, §2.1): it forwards the two cells to the
/// Application layer, which owns validation and resolution, and returns the §5
/// acknowledgement. It decides nothing itself — it does not check range,
/// adjacency, staleness, or matches, and it does not touch the board.
///
/// The hub's methods are split by direction
/// (<c>SIGNALR_PROTOCOL.md</c> §2, §3). Client → server methods are the ones the
/// client may invoke; server → client ones are client-side handlers the server
/// calls through <c>SendAsync</c> and are never invokable.
///
/// <c>CardCast</c>, <c>PetSkillCast</c> (§2) and <c>GetBattleState</c> (§7) are
/// client → server methods that are intentionally NOT implemented: they require
/// Card/Pet systems and reconnect recovery, which are out of scope.
///
/// <c>ReceiveEvents</c> is the opposite direction: it is a server → client
/// delivery (§3), implemented by the accepted-Swap path below, and it must never
/// be added as an invokable hub method.
/// </summary>
public class BattleHub : Hub
{
    private readonly RuntimeService _runtime;
    private readonly BattleStateService _battles;

    public BattleHub(RuntimeService runtime, BattleStateService battles)
    {
        _runtime = runtime;
        _battles = battles;
    }

    /// <summary>
    /// Technical connection acknowledgement.
    ///
    /// Established by the framework lifecycle below rather than a bespoke
    /// client-invoked method: <c>OnConnectedAsync</c> records the connection
    /// and returns the status to the caller, which is the smallest technical
    /// connection verification the documentation supports
    /// (SIGNALR_PROTOCOL.md §1; ADR-008 relies on the connection having a
    /// stable identity for reconnect/resync).
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var status = _runtime.OnConnected(Context.ConnectionId);

        await Clients.Caller.SendAsync("RuntimeStatusChanged", status);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var status = _runtime.OnDisconnected(Context.ConnectionId, faulted: exception is not null);

        // The client may already be gone; a failed notification is not an error
        // condition for a disconnected connection.
        if (status is not null)
        {
            try
            {
                await Clients.Caller.SendAsync("RuntimeStatusChanged", status);
            }
            catch (Exception)
            {
                // Connection already closed — nothing to report.
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Joins the caller to the battle's group and delivers the authoritative
    /// initial battle state (SIGNALR_PROTOCOL.md §1.2, §4).
    ///
    /// Trigger (§4.1): joining the group is what triggers delivery. The server
    /// pushes the state to the joining caller before any <c>ReceiveEvents</c>
    /// for that battle; there is no separate client request, and no
    /// <c>GetBattleState</c>-style invocation on this path (§4.2).
    ///
    /// Scope (§4.3): the joining caller only — this is not a group broadcast.
    ///
    /// The hub only delegates: the state comes from the Application layer, and
    /// no value is computed, derived, or adjusted here (§4.9, ARCHITECTURE.md
    /// §2.1).
    /// </summary>
    public async Task JoinBattle(string battleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(battleId);

        await Groups.AddToGroupAsync(Context.ConnectionId, battleId);

        var state = _battles.GetInitialStateForGroup(battleId);

        // An unknown battle yields no push. The hub defines no error contract
        // for this path: SIGNALR_PROTOCOL.md §5 defines rejection shapes for
        // in-battle action methods only.
        if (state is null)
        {
            return;
        }

        await Clients.Caller.SendAsync("BattleStateUpdated", ToPayload(state));
    }

    /// <summary>
    /// Resolves one player Swap action (<c>SIGNALR_PROTOCOL.md</c> §2, §2.1).
    ///
    /// The hub only delegates: the two cells are handed to the Application layer,
    /// which validates them per <c>MATCH3_RULES.md</c> §2.1.2 and, when the action
    /// is accepted, commits and resolves it (§2.1.6). No gameplay value is
    /// computed, derived, or adjusted here, and the hub does not inspect the board
    /// at all (ARCHITECTURE.md §2.1, §4.1).
    ///
    /// <b>Accepted.</b> The resolution has already completed and written the
    /// resulting state back (<c>GAME_STATE.md</c> §5.1), so the hub pushes that
    /// state to the battle group through the existing <c>BattleStateUpdated</c>
    /// path, and then delivers the resolution's ordered events to the same group
    /// through the documented <c>ReceiveEvents</c> batch
    /// (<c>SIGNALR_PROTOCOL.md</c> §3, §3.1). Both messages describe the one
    /// committed transition and are sent after the write-back, never before it.
    ///
    /// <c>ReceiveEvents</c> is Server → Client only: it is a client callback the
    /// server invokes, never an invokable hub method (§2 lists the client →
    /// server methods and does not include it). It carries exactly
    /// <c>battleId</c>, <c>serverSequence</c>, and <c>events[]</c> — no state, no
    /// board, no Combo/Match duplication, and no per-event or per-pass message
    /// (§3.1 items 1–3, §8 item 7).
    ///
    /// <b>Rejected.</b> Nothing happened: no state changed, and no push or event
    /// follows (<c>MATCH3_RULES.md</c> §2.1.5, <c>GAME_EVENTS.md</c> §1.2). The
    /// rejection is the direct return of this call and nothing more
    /// (<c>SIGNALR_PROTOCOL.md</c> §5, §3.1 item 4).
    ///
    /// <b>No new field of the wrong kind is delivered.</b>
    /// <c>LastCommittedSwapPair</c> is authoritative state that is deliberately not
    /// sent to the client (<c>SIGNALR_PROTOCOL.md</c> §4 item 12,
    /// <c>GAME_STATE.md</c> §2.1.10 item 9): it is server-side bookkeeping.
    /// <c>playerState</c> is delivered because it is the implemented stage's own
    /// field, and it is the only member this method added to the projection
    /// (<c>SIGNALR_PROTOCOL.md</c> §4 item 4, §4.2).
    ///
    /// <c>clientSequence</c> is accepted and ignored. It is an opaque correlation
    /// id the client echoes to match a request with its result — it is **not** the
    /// authoritative <c>BattleState.Sequence</c> and is never used to detect
    /// staleness (<c>SIGNALR_PROTOCOL.md</c> §2 item 1, <c>MATCH3_RULES.md</c>
    /// §2.1.4 item 1). Staleness is decided by the server from its own committed
    /// record.
    /// </summary>
    /// <param name="battleId">The battle the action applies to; also scopes the group.</param>
    /// <param name="fromCell">The §1.0 cell index the player is moving.</param>
    /// <param name="toCell">The §1.0 cell index it is exchanged with.</param>
    /// <param name="clientSequence">
    /// The client's opaque correlation value. Carried for transport compatibility
    /// and not consulted (<c>SIGNALR_PROTOCOL.md</c> §2.1 item 1).
    /// </param>
    /// <returns>The §5 acknowledgement: accepted, or rejected with its reason.</returns>
    public async Task<SwapResponse> Swap(
        string battleId,
        int fromCell,
        int toCell,
        string? clientSequence = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(battleId);

        _ = clientSequence;

        var result = _battles.ExecuteSwap(battleId, new SwapRequest(fromCell, toCell));

        if (result is null)
        {
            // An unknown battle resolves nothing. The rejection code for this path
            // is not a Swap-validation reason, so none of §2.1.2's four is
            // fabricated for it; the action simply did not happen.
            return new SwapResponse(Accepted: false, Reason: "BATTLE_NOT_FOUND");
        }

        if (result.Value.IsRejected)
        {
            // The documented rejection code (SIGNALR_PROTOCOL.md §5 item 3), spelled
            // by its owning mapping rather than derived from the enum's identifier.
            return new SwapResponse(
                Accepted: false,
                Reason: SwapRejectionCodes.ToContractCode(result.Value.Reason));
        }

        // The state is committed before anything is pushed: events and state
        // describe a resolution that has already happened (SIGNALR_PROTOCOL.md
        // §3.1 item 1, GAME_STATE.md §5.1 item 3).
        await Clients.Group(battleId).SendAsync("BattleStateUpdated", ToPayload(result.Value.State));

        // The resolution's ordered events follow the write-back, as one atomic
        // batch for this one resolved action (SIGNALR_PROTOCOL.md §3, §3.1). The
        // batch is projected one-to-one from the executor's own list onto the §3.2
        // wire schema and its serverSequence is read from the committed state —
        // nothing is sorted, filtered, regrouped, re-derived, or incremented here,
        // and no second detection pass is run (GAME_EVENTS.md §3 item 6). The
        // Domain events are replaced by wire DTOs precisely so that this send is
        // serializable at all (§3.2.1 item 1).
        await Clients.Group(battleId).SendAsync("ReceiveEvents", ToPayload(result.Value));

        return new SwapResponse(Accepted: true, Reason: null);
    }

    /// <summary>
    /// Projects the committed Swap result onto the §3 <c>ReceiveEvents</c> payload.
    ///
    /// A pure field mapping, exactly like <see cref="ToPayload(BattleState)"/>: the
    /// battle id, the post-resolution <c>BattleState.Sequence</c>, and the
    /// executor's own event list are carried across one-to-one and in order. The
    /// hub derives nothing, counts nothing, re-detects nothing, and adds no member
    /// (<c>SIGNALR_PROTOCOL.md</c> §3, §3.2, §3.3).
    ///
    /// The event list is projected onto the §3.2 wire schema rather than passed
    /// through as Domain values: the Domain <c>BattleEvent</c> is not a wire DTO
    /// and cannot be serialized (<c>§3.2.1</c> item 1). The projection is
    /// one-to-one and order-preserving, so the batch is still exactly the
    /// resolution's own list (<c>§3.2.1</c> item 3).
    /// </summary>
    /// <param name="result">
    /// The accepted result of the Swap this call just committed. A rejection never
    /// reaches this projection: a rejected action delivers nothing
    /// (<c>SIGNALR_PROTOCOL.md</c> §3.1 item 4).
    /// </param>
    private static ReceiveEventsPayload ToPayload(SwapExecutionResult result) =>
        new(
            // §3: the batch is scoped to — and delivered to — the battle's group.
            result.State.BattleId,

            // §3.2 / GAME_STATE.md §5: the committed post-resolution Sequence. Read
            // from the state the write-back stored, so the batch is associated with
            // the same transition BattleStateUpdated reported. It is never
            // incremented or recomputed here.
            result.State.Sequence,

            // §3 item 1 / §3.2.1 item 3: the ordered event list exactly as the
            // resolution produced it — not re-sorted, not filtered, not regrouped,
            // and not derived from the final board — mapped onto the §3.2 wire
            // schema, which is the only transformation applied to it.
            BattleEventWireProjection.Project(result.Events));

    /// <summary>
    /// Projects the domain state onto the §4 wire payload.
    ///
    /// A pure field mapping — no calculation is performed. The board, the seed,
    /// the RNG state, and the player's Match/Combo state are projected one-to-one
    /// from the authoritative server state (<c>SIGNALR_PROTOCOL.md</c> §4.4, §4.9);
    /// the hub derives nothing, validates nothing, and generates nothing.
    /// </summary>
    private static BattleStateUpdated ToPayload(BattleState state) =>
        new(
            state.BattleId,
            state.Turn,
            state.Sequence,
            state.RngSeed,
            // RngState is one logical field with two components; it stays
            // together on the wire (GAME_STATE.md §2.6.2 item 1).
            new RngStatePayload(state.RngState.State, state.RngState.Increment),
            // Exactly the 64 cell entries, row-major (GAME_STATE.md §2.1.1,
            // MATCH3_RULES.md §1.0), each carrying its Gem type and its optional
            // Special Gem — the state's own shape, delivered entry for entry
            // (SIGNALR_PROTOCOL.md §4.1 item 5). No separate Special Gem collection
            // is projected, because none exists (§2.1.2 item 1).
            new BoardPayload(state.BoardState.Cells.Select(ToCellPayload).ToArray()),
            // GAME_STATE.md §2.2 / SIGNALR_PROTOCOL.md §4.2: the two implemented
            // PlayerState fields, projected one-to-one. Both are always present — a
            // zero is delivered as a zero, never omitted (MATCH3_RULES.md §6.5 item 4).
            new PlayerStatePayload(state.PlayerState.Combo, state.PlayerState.MatchCount));

    /// <summary>
    /// Projects one cell entry onto the §4 wire shape — a pure field mapping.
    ///
    /// The Gem type is rendered as its documented contract name
    /// (<c>MATCH3_RULES.md</c> §1.1) and the Special Gem, when present, as its type
    /// and — for a Line Clear Gem only — its orientation
    /// (<c>GAME_STATE.md</c> §2.1.4 item 2). Nothing is derived, validated, or
    /// computed here.
    /// </summary>
    private static CellPayload ToCellPayload(Cell cell) =>
        new(GemTypes.ToContractName(cell.GemType), ToSpecialGemPayload(cell.SpecialGem));

    /// <summary>
    /// Projects a cell's optional Special Gem, or <c>null</c> when the cell holds an
    /// ordinary Gem (<c>GAME_STATE.md</c> §2.1.7 item 3).
    /// </summary>
    private static SpecialGemPayload? ToSpecialGemPayload(SpecialGem? specialGem) =>
        specialGem is not { } gem
            ? null
            : new SpecialGemPayload(gem.Type.ToString(), gem.Orientation?.ToString());

    /// <summary>
    /// Bootstrap-era connectivity probe retained from the integration smoke
    /// test. It is technical only and carries no gameplay meaning.
    /// </summary>
    public Task<PingResponse> Ping(string? clientSequence = null)
    {
        return Task.FromResult(new PingResponse(
            Accepted: true,
            ClientSequence: clientSequence,
            ServerTime: DateTimeOffset.UtcNow));
    }
}