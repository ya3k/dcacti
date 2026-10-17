namespace GameServer.Domain.Match3;

/// <summary>
/// The outcome of resolving one detection pass (<c>MATCH3_RULES.md</c> §4.1) —
/// the board after the pass's Match Resolution, Gravity, and Spawn, together with
/// the ordered reports the pass produced and the RNG state its Spawn step left
/// behind.
///
/// This is the return value of one iteration of the §4 loop. It is not
/// authoritative state: the authoritative values are the <see cref="Board"/> and
/// <see cref="RngState"/> it carries (<c>GAME_STATE.md</c> §2.1.5, §2.6.2).
/// </summary>
/// <param name="Board">
/// The board after the pass's sub-steps — full again, one ordinary Gem per cell
/// (<c>MATCH3_RULES.md</c> §4.1, §4.5).
/// </param>
/// <param name="RngState">
/// The RNG state after the pass's Spawn step (<c>MATCH3_RULES.md</c> §4.5 item 4).
/// Unchanged by any pass that spawned nothing.
/// </param>
/// <param name="Matches">
/// The pass's match set, in §3.2 order, each counted as one Match whatever tier it
/// is (<c>MATCH3_RULES.md</c> §3.2, §5.7).
/// </param>
/// <param name="GemMatched">
/// The <c>GemMatched</c> reports for the pass's cleared cells — the pass's
/// confirmed matched union, each cell once, ascending §1.0 cell index
/// (<c>GAME_EVENTS.md</c> §1.3).
/// </param>
/// <param name="ClearedCellUnion">
/// The union of every cell the pass cleared — the matched cells, each cell once,
/// ascending §1.0 index. This is the set resource generation is computed over
/// (<c>MATCH3_RULES.md</c> §5.7 item 2).
/// </param>
/// <param name="ClearedGems">
/// The same union, each cell once, carrying the cell's Gem type
/// (<c>MATCH3_RULES.md</c> §3.3 item 3, §5.7 item 2). This is the input resource
/// generation consumes.
///
/// A cell appears here once whatever named it — a Match shape, or two overlapping
/// shapes — because a cell is one cell for removal and for resource generation
/// (<c>§3.3 item 3</c>). There is no match-tier value on an entry: resource
/// contribution is <c>BaseOutput(type) × unique cleared count(type)</c> with no
/// tier factor (<c>COMBAT_RULES.md</c> §2 item 6, OD-3).
///
/// The Gem type is read from the board the pass was resolved against, which is
/// the state in which the cell was still present (<c>GAME_EVENTS.md</c> §2
/// item 2). This is the pass's own record of what it cleared, so a consumer does
/// not re-read the post-resolution board, where those cells no longer hold the
/// Gems that were consumed.
/// </param>
/// <param name="DropMoves">
/// The survivors this pass's Gravity relocated, source → destination, in
/// <see cref="Gravity.Apply"/>'s own read order: columns ascending, and within a
/// column bottom-to-top (<c>MATCH3_RULES.md</c> §4.4 item 1). This is the
/// authoritative report <c>SIGNALR_PROTOCOL.md</c> §3.2.28 delivers as
/// <c>dropMoves</c>; a survivor that kept its cell is absent.
/// </param>
/// <param name="SpawnedGems">
/// The Gems this pass's Spawn placed, in <c>MATCH3_RULES.md</c> §4.5 item 2 fill
/// order: columns ascending, and within a column top-to-bottom. Each entry is the
/// destination cell and the drawn Gem type — the pass's own record of what it
/// spawned, so no consumer re-reads the post-resolution board for it. This is the
/// authoritative report <c>SIGNALR_PROTOCOL.md</c> §3.2.28 delivers as
/// <c>spawnedGems</c>. A spawned Gem is always an ordinary Gem (§4.5 item 7).
/// </param>
public readonly record struct PassResult(
    BoardState Board,
    RngState RngState,
    IReadOnlyList<MatchResolution> Matches,
    IReadOnlyList<GemMatchedEvent> GemMatched,
    IReadOnlyList<int> ClearedCellUnion,
    IReadOnlyList<ClearedGem> ClearedGems,
    IReadOnlyList<DropMove> DropMoves,
    IReadOnlyList<SpawnedGem> SpawnedGems);

/// <summary>
/// One Gem a pass's Spawn step placed (<c>MATCH3_RULES.md</c> §4.5,
/// <c>SIGNALR_PROTOCOL.md</c> §3.2.28).
///
/// Spawn never creates anything but an ordinary Gem, whatever the cell held before
/// (§4.5 item 7), so an entry states the cell and the drawn type and nothing else.
///
/// This is a delivery-facing report of a step that already ran: it is not used by
/// the next detection pass and changes no board value.
/// </summary>
/// <param name="CellIndex">
/// The destination §1.0 cell — always one of the cells Gravity vacated, never a
/// cell a survivor already occupies (§4.5 item 1).
/// </param>
/// <param name="GemType">
/// The Gem type the draw selected, always one of the four §1.1 types
/// (§4.5 item 5). It is the value the pass placed on the board.
/// </param>
public readonly record struct SpawnedGem(int CellIndex, GemType GemType);

/// <summary>
/// One cell a pass cleared, with the Gem type it held
/// (<c>MATCH3_RULES.md</c> §3.3 item 3).
///
/// This is <b>Transient Resolution State</b> (<c>GAME_STATE.md</c> §3): it is
/// pass-local accounting, is never a <c>BattleState</c> field, and is not
/// serialized or delivered.
/// </summary>
/// <param name="CellIndex">The cleared cell's §1.0 index.</param>
/// <param name="GemType">
/// The Gem type the cell held when it was cleared — always one of the four §1.1
/// types.
/// </param>
public readonly record struct ClearedGem(int CellIndex, GemType GemType);

/// <summary>
/// Board resolution — one detection pass, and the cascade loop that repeats it
/// (<c>MATCH3_RULES.md</c> §4).
///
/// The pass executes the §4.1 step order exactly, and the order may not be
/// reordered:
///
/// <code>
/// 1. Match Resolution   remove the union of the pass's matched cells
/// 2. Gravity            each column's remaining Gems fall to fill empty cells
/// 3. Spawn              draw new Gems into the now-empty cells at the top of
///                       each column
/// 4. Re-detect          run Match Detection on the completed board
/// </code>
///
/// Every match tier — Match 3, Match 4, Match 5, and L/T — is removed by step 1
/// and counted as one Match; no tier adds a step, a board object, or a side effect
/// (<c>MATCH3_RULES.md</c> §1.A, §5.7).
///
/// Resolution is deterministic end to end (§4.6): given the same starting board,
/// the same match set, and the same <c>RngState</c>, every pass, removal, fall,
/// spawn, and the final board and <c>RngState</c> are fully determined. The only
/// operation that consumes randomness is Spawn (§4.5 item 4); everything else is a
/// pure function of the board state (§4.6 item 3, §7.2).
/// </summary>
public static class BoardResolver
{
    /// <summary>
    /// Resolves one detection pass on the board the pass's match set was detected
    /// on (<c>MATCH3_RULES.md</c> §4.1).
    /// </summary>
    /// <param name="board">The board the match set was detected on. Never mutated.</param>
    /// <param name="matchSet">The pass's match set, in §3.2 order.</param>
    /// <param name="rng">
    /// The generator whose stream the pass's Spawn step continues. Advanced in
    /// place by exactly one selection per spawned cell and by nothing else
    /// (<c>MATCH3_RULES.md</c> §4.5 item 4, §7.2 item 2).
    /// </param>
    /// <param name="cascadeDepth">
    /// The pass's depth: 1 for the first pass of a Swap, ≥ 2 for a Cascade
    /// (§4.2 items 1–2).
    /// </param>
    public static PassResult ResolvePass(
        BoardState board,
        IReadOnlyList<MatchShape> matchSet,
        Pcg32 rng,
        int cascadeDepth)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(matchSet);
        ArgumentNullException.ThrowIfNull(rng);

        // --- §4.1 step 1: Match Resolution --------------------------------
        // Remove the union of the pass's matched cells. Removal is simultaneous
        // for the pass (§3.3 item 4): the board never exists in a state where only
        // some of a pass's matched cells are gone.
        var matchedUnion = MatchDetector.ClearedCellUnion(matchSet);

        // The pre-removal board is the only source of the GemMatched metadata: the
        // cell is cleared by the same act (GAME_EVENTS.md §2 item 2).
        var matchedReports = BuildGemMatchedReports(board, matchedUnion);

        var empties = new HashSet<int>(matchedUnion);

        // --- §4.1 step 2: Gravity ----------------------------------------
        var gravity = Gravity.Apply(board, empties);

        // --- §4.1 step 3: Spawn ------------------------------------------
        // §4.5 item 1: spawn fills exactly the cells that are empty AFTER Gravity,
        // which are always the topmost cells of their column. A column with k
        // survivors spawns exactly k Gems into its top k cells.
        //
        // The set Spawn receives is the one Gravity produced, not the pre-gravity
        // set: a gem that fell has moved INTO a cell that was empty, so that cell is
        // no longer empty. Filling the pre-gravity set would overwrite the gems
        // gravity just moved, and the published board would lose them (§4.5 item 1).
        var spawn = Spawn.Fill(gravity.Board, rng, new HashSet<int>(gravity.VacatedCells));

        // --- Reports ------------------------------------------------------
        // §3.2.28: the pass's own report of the Gems its Spawn placed. Spawn
        // already returns them in §4.5 item 2 order, so the report keeps the
        // destination cell and the drawn Gem type and derives nothing — a spawned
        // Gem is always an ordinary Gem (§4.5 item 7).
        var spawnedGems = new List<SpawnedGem>(spawn.CreatedCells.Count);

        foreach (var (createdIndex, createdType) in spawn.CreatedCells)
        {
            spawnedGems.Add(new SpawnedGem(createdIndex, createdType));
        }

        var matches = new List<MatchResolution>(matchSet.Count);
        for (var shapeIndex = 0; shapeIndex < matchSet.Count; shapeIndex++)
        {
            matches.Add(new MatchResolution(matchSet[shapeIndex], cascadeDepth));
        }

        // §3.3 item 3 / §5.7 item 2: the cleared set is the matched union itself — each
        // matched cell once, ascending §1.0 index. Removal, resource generation, and
        // GemMatched are all computed over that one union, never per shape.
        var clearedUnion = matchedUnion;

        // --- Resource generation accounting (COMBAT_RULES.md §2, §5.7) ------
        // The same union again, carrying each cell's Gem type. The type is read
        // from `board` - the state the pass was resolved against, in which the cell
        // still held the Gem that was consumed (GAME_EVENTS.md §2 item 2). The
        // post-resolution board holds spawned Gems in those cells, so it is not a
        // source for this.
        //
        // No match-tier value is carried: a cell contributes its type's BaseOutput
        // once, whatever shape consumed it (COMBAT_RULES.md §2 item 6, OD-3). A
        // cell named by more than one shape is recorded once, because the union is
        // already de-duplicated and ordered.
        var clearedGems = new List<ClearedGem>(clearedUnion.Count);

        foreach (var index in clearedUnion)
        {
            clearedGems.Add(new ClearedGem(index, board[index]));
        }

        return new PassResult(
            spawn.Board,
            rng.CurrentState,
            matches,
            matchedReports,
            clearedUnion,
            clearedGems,
            gravity.DropMoves,
            spawnedGems);
    }

    /// <summary>
    /// Builds the <c>GemMatched</c> reports for a cleared union, reading each cell's
    /// Gem type from the <b>pre-removal</b> board (<c>GAME_EVENTS.md</c> §1.3, §2
    /// item 2). The union it is given is already each-cell-once and ascending §1.0
    /// index (<see cref="MatchDetector.ClearedCellUnion"/>), and nothing is
    /// re-sorted here: ascending index is a total order over cells, so the
    /// documented enumeration order is the union's own order
    /// (<c>GAME_EVENTS.md</c> §1.3 item 2).
    /// </summary>
    private static IReadOnlyList<GemMatchedEvent> BuildGemMatchedReports(
        BoardState preRemovalBoard,
        IReadOnlyList<int> clearedCells)
    {
        var reports = new List<GemMatchedEvent>(clearedCells.Count);

        foreach (var index in clearedCells)
        {
            reports.Add(new GemMatchedEvent(index, preRemovalBoard[index]));
        }

        return reports;
    }
}
