using GameServer.Domain.Match3;

namespace GameServer.Domain.Battle;

/// <summary>
/// Active Battle State through the Match / Combo accounting stage — the
/// authoritative battle state at the Board Foundation stage
/// (<c>GAME_STATE.md</c> §2.0.5) plus the <c>PlayerState</c> fields this stage
/// implements (§2.2).
///
/// It is the Battle State Foundation (§2.0) <b>plus</b> the fields §2 already
/// defines for the Match-3 board, the randomness that generates it, the commit
/// record the staleness check reads, and the player's Match/Combo progression —
/// nothing else is added, and nothing already in §2.0 changes (§0 item 5):
///
/// <code>
/// BattleState
/// ├── BattleId
/// ├── Turn          = 0
/// ├── Sequence      = 0
/// ├── RngSeed                        (§2.6)
/// ├── RngState                       (§2.6)
/// ├── BoardState                     (§2.1)
/// │   └── Cells[64]
/// ├── PlayerState                    (§2.2)
/// │   ├── MatchCount = 0
/// │   └── Combo      = 0
/// ── LastCommittedSwapPair         (§2.1.10; absent until the first
///                                    committed Swap)
/// </code>
///
/// Each field has the same name, meaning, and rules as its §2 counterpart; each
/// stage is where those §2 fields first come into existence (§2.0.5). The
/// remaining §2 fields — the rest of <c>PlayerState</c> (§2.2), <c>PetState</c>
/// (§2.3), and <c>BossState</c> (§2.4) — are still absent and still owned by
/// later stages (§2.0.5.3). Their absence is a staging position, not a scope
/// reduction of §2: a field absent from a stage is <b>not yet implemented</b>,
/// not <b>not required</b> (§0 item 4).
///
/// <see cref="PlayerState"/> is the documented owner of <c>MatchCount</c> and
/// <c>Combo</c> (§2.2), which is why they appear here as one nested field rather
/// than as flat members of this record: §0 item 5 forbids a stage from
/// introducing a parallel representation of a concept another stage owns, and a
/// flat <c>BattleState.MatchCount</c> would be a second owner.
///
/// <c>BoardState</c> carries Special Gem state inside its <c>Cells[64]</c>
/// entries (<c>GAME_STATE.md</c> §2.1.1, §2.1.4). No field was added to this
/// type for it: the former <c>BoardState.PendingSpecialGems[]</c> placeholder
/// does not exist in any form (§2.1.2 item 1), and Special Gem state travels
/// with the board because it travels with the cells.
///
/// This type is deliberately minimal and framework-independent
/// (<c>ARCHITECTURE.md</c> §2.1): it references no ASP.NET Core, SignalR,
/// EF Core, Redis, HTTP, Phaser, or Discord concern.
///
/// It contains no <c>Status</c> field and no lifecycle enum
/// (<c>GAME_STATE.md</c> §2.0.3, §2.0.5): a battle has no lifecycle state
/// machine, and battle outcome is expressed as the
/// <c>BattleWon</c>/<c>BattleLost</c> events (<c>GAME_EVENTS.md</c> §2), not as
/// state. Extending the state to the board stage adds no lifecycle value either,
/// and neither does this stage.
/// </summary>
/// <param name="BattleId">
/// Identity of the battle session (<c>GAME_STATE.md</c> §2.0.1, §2.0.5.1). It
/// scopes the client's SignalR group membership (<c>SIGNALR_PROTOCOL.md</c>
/// §1.2) and carries no gameplay content — it selects no Pet, Boss, or loadout.
/// </param>
/// <param name="Turn">
/// Current Turn number (<c>GAME_STATE.md</c> §2.0.1, §2.0.5.1, §2,
/// <c>GAME_RULES.md</c> §2). Still <c>0</c> at this stage: board generation is
/// not a player Swap/Action and starts no Turn (§2.0.5.2 item 1). This type
/// defines no Turn increment rule — §2.0.2 leaves that to the task that
/// implements action resolution.
/// </param>
/// <param name="Sequence">
/// Monotonic resolution counter (<c>GAME_STATE.md</c> §2.0.1, §2.0.5.1, §2, §5).
/// Still <c>0</c> at this stage: board generation is not an action resolution and
/// increments no counter (§2.0.5.2 item 1). <c>0</c> is the only valid value
/// until the first resolution succeeds. Not to be confused with
/// <c>GameServer.Application.Runtime.RuntimeStatus.Sequence</c>, which is a
/// technical connection counter and not battle state.
/// </param>
/// <param name="RngSeed">
/// The battle's server-chosen PRNG seed (<c>GAME_STATE.md</c> §2.6.1). It records
/// the battle's origin point and is never rewritten after creation
/// (§2.6.1 item 3). It is an unsigned 64-bit value produced by the server's own
/// entropy at battle creation and is never supplied, influenced, or derived from
/// client input (§2.6.1 items 1–2, <c>TDD.md</c> §6 item 3).
/// </param>
/// <param name="RngState">
/// The PRNG state after generating the initial board (<c>GAME_STATE.md</c>
/// §2.0.5.1, §2.6.2, §2.7.1 step 6). It is the state <b>pair</b> — internal state
/// plus stream selector — not a single word (§2.6.2 item 1). It is retained, not
/// reset and not re-derived from the final board, so recovering a battle from a
/// snapshot restores the stream exactly (§2.6.2 item 4).
/// </param>
/// <param name="BoardState">
/// The authoritative board (<c>GAME_STATE.md</c> §2.0.5.1, §2.1). At this stage
/// its <c>Cells[64]</c> hold the initial board generated per §2.7. It is
/// server-authored and the client never generates, fills, or repairs it
/// (§2.1.1 item 4).
/// </param>
/// <param name="PlayerState">
/// The player's battle progression state (<c>GAME_STATE.md</c> §2.2) — the
/// documented owner of <c>MatchCount</c> and <c>Combo</c>. It exists from battle
/// creation with both values at their documented starting point
/// (<see cref="PlayerState.Initial"/>); it is never <c>null</c> and is never
/// created lazily on the first Swap.
///
/// It is written in the same single post-resolution write-back as
/// <see cref="Turn"/> and <see cref="Sequence"/> (§5.1), for a <b>committed</b>
/// Swap only: a rejected Swap writes nothing, so both values keep what they held
/// (<c>MATCH3_RULES.md</c> §2.1.5 item 5). No intermediate value of either field
/// is ever written or published (§5.1 item 2): the state only ever carries a
/// finished resolution's result.
/// </param>
/// <param name="LastCommittedSwapPair">
/// The unordered pair most recently committed to the board
/// (<c>GAME_STATE.md</c> §2.1.10) — the authoritative record
/// <c>MATCH3_RULES.md</c> §2.1.4's already-applied check reads, stored
/// canonically as <c>(min, max)</c> so a request's argument order cannot make
/// two spellings of one pair compare unequal (§2.1.10 item 2).
///
/// It is <c>null</c> until the first Swap is committed: absence is the
/// documented representation of "no Swap has been committed", and no default
/// or sentinel pair stands in for it (§2.1.10 item 3). A newly created battle
/// therefore has none — board generation commits no Swap and is not an action
/// resolution (§2.0.5.2 item 1) — which is also why a request can never be
/// rejected as already applied against a battle that has not played a Swap.
///
/// It is written only when a Swap is committed, in the same single
/// post-resolution write-back as <see cref="Turn"/> and
/// <see cref="Sequence"/> (§2.1.10 item 5, §5.1 item 7); a rejected action —
/// including <c>STALE_ACTION</c> — leaves it unchanged and never clears it
/// (§2.1.10 items 6–7, <c>MATCH3_RULES.md</c> §2.1.5).
///
/// It is <b>not</b> delivered to the client and is never carried by a Swap
/// request: it is server-side bookkeeping inside authoritative state
/// (<c>SIGNALR_PROTOCOL.md</c> §4 item 12, <c>MATCH3_RULES.md</c> §2.1.1
/// item 3, §2.1.10 item 8). It is not a version and is never used for
/// concurrency control (§5 item 3, §2.1.10 item 11).
/// </param>
public sealed record BattleState(
    string BattleId,
    int Turn,
    int Sequence,
    ulong RngSeed,
    RngState RngState,
    BoardState BoardState,
    PlayerState PlayerState,
    CommittedSwapPair? LastCommittedSwapPair = null)
{
    /// <summary>
    /// Initial <c>Turn</c> for a battle with no resolved action
    /// (<c>GAME_STATE.md</c> §2.0.2, §2.0.5.2).
    /// </summary>
    public const int InitialTurn = 0;

    /// <summary>
    /// Initial <c>Sequence</c> for a battle with no resolved action
    /// (<c>GAME_STATE.md</c> §2.0.2, §2.0.5.2, §5).
    /// </summary>
    public const int InitialSequence = 0;

    /// <summary>
    /// Creates the authoritative state for a newly created battle session: the
    /// documented initial values, including the player's progression state, plus
    /// the generated board and the retained RNG state (<c>GAME_STATE.md</c>
    /// §2.0.5, §2.2, §2.7.1).
    ///
    /// Board generation is not a resolution, so <c>Turn</c> and <c>Sequence</c>
    /// remain <c>0</c> (§2.0.5.2 item 1), and <c>MatchCount</c> and <c>Combo</c>
    /// stay at the documented <c>0</c>: generation produces no Match, commits no
    /// Swap, and is not a Swap/Action (<c>MATCH3_RULES.md</c> §8.1 item 4).
    /// No Battle Event is emitted by board generation (§2.0.5.2 item 2) and
    /// nothing within this type emits one.
    ///
    /// <c>LastCommittedSwapPair</c> is likewise absent: generation commits no
    /// Swap (§2.1.10 item 3, §2.0.5.2 item 1), so the returned state carries
    /// none and no pair is invented to stand for the absence.
    ///
    /// Authoritative state is server-produced (<c>GAME_RULES.md</c> §18,
    /// <c>ADR-001</c>); this factory is the only place a battle begins.
    /// </summary>
    /// <param name="battleId">Identity of the battle session.</param>
    /// <param name="rngSeed">
    /// The server-chosen seed (<c>GAME_STATE.md</c> §2.6.1). It must originate
    /// server-side; this type neither generates nor influences it.
    /// </param>
    /// <exception cref="BoardGenerationFailedException">
    /// No candidate board satisfied the documented initial-board constraints
    /// within the documented 64-attempt bound
    /// (<c>MATCH3_RULES.md</c> §1.5 item 3): the battle creation is rejected as an
    /// error.
    /// </exception>
    public static BattleState Create(string battleId, ulong rngSeed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(battleId);

        // Generation continues the stream from the seed and returns the board
        // together with the state after all consumption the accepted attempt
        // used (MATCH3_RULES.md §1.2.1, GAME_STATE.md §2.7.1).
        var generation = BoardGenerator.Generate(rngSeed);

        return new BattleState(
            battleId,
            InitialTurn,
            InitialSequence,
            rngSeed,
            generation.RngState,
            generation.Board!,
            // §2.2: PlayerState exists from creation with MatchCount = 0 and
            // Combo = 0 — no null, no sentinel, and no lazy creation on the
            // first Swap.
            PlayerState.Initial);
    }
}