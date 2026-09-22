namespace GameServer.Domain.Match3;

/// <summary>
/// The outcome of resolving one detection pass (<c>MATCH3_RULES.md</c> §4.1) —
/// the board after the pass's Match Resolution, activation, creation, Gravity, and
/// Spawn, together with the ordered reports the pass produced and the RNG state
/// its Spawn step left behind.
///
/// This is the return value of one iteration of the §4 loop. It is not
/// authoritative state: the authoritative values are the <see cref="Board"/> and
/// <see cref="RngState"/> it carries (<c>GAME_STATE.md</c> §2.1.5, §2.6.2).
/// </summary>
/// <param name="Board">
/// The board after the pass's Slide 1–4 sub-steps — full again, one Gem per cell,
/// with every Special Gem the pass created committed into its cell entry
/// (<c>MATCH3_RULES.md</c> §4.1, §1).
/// </param>
/// <param name="RngState">
/// The RNG state after the pass's Spawn step (<c>MATCH3_RULES.md</c> §4.5 item 4).
/// Unchanged by any pass that spawned nothing.
/// </param>
/// <param name="Matches">
/// The pass's match set, in §3.2 order, each with the Special Gems its shape
/// created (<c>MATCH3_RULES.md</c> §3.2, §5.5.1).
/// </param>
/// <param name="MatchedCellGemMatched">
/// The <c>GemMatched</c> reports for the pass's matched cells — union of the match
/// set, ascending §1.0 cell index (<c>GAME_EVENTS.md</c> §1.3 sub-step 1).
/// </param>
/// <param name="ActivationCellGemMatched">
/// The <c>GemMatched</c> reports for the cells the pass's activations and chains
/// cleared, excluding the already-reported matched cells, ascending §1.0 cell
/// index (<c>GAME_EVENTS.md</c> §1.3 sub-step 2).
/// </param>
/// <param name="ActivatedSpecialGems">
/// The Special Gems the pass activated, in activation order: the pass's consumed
/// Special Gems in §3.2 match-set order, then each breadth-first chain level
/// (<c>MATCH3_RULES.md</c> §5.8.3 level 3, §5.5.5 item 7).
/// </param>
/// <param name="CreatedSpecialGems">
/// The Special Gems the pass committed, in §5.5.1 order.
/// </param>
/// <param name="ClearedCellUnion">
/// The union of every cell the pass cleared — matched cells plus every activation
/// and chain — each cell once, ascending §1.0 index. This is the set resource
/// generation is computed over (<c>MATCH3_RULES.md</c> §5.8.2 item 5, §5.7
/// item 2).
/// </param>
/// <param name="ClearedGems">
/// The same union, each cell once, carrying the cell's Gem type and the match
/// tier it generates at (<c>COMBAT_RULES.md</c> §2, <c>MATCH3_RULES.md</c> §5.7).
/// This is the input resource generation consumes.
///
/// A cell consumed by a Match carries that shape's tier; a cell cleared by a
/// Special Gem activation carries <see cref="MatchTier.Base"/>, because an
/// activation is not a Match and has no tier (§5.7 item 6). A cell that is both —
/// a Match consumed it and an activation's affected set named it — is a member of
/// the Match and carries the Match's tier, and it appears here once
/// (§5.8.2 items 2, 6).
///
/// The Gem type is read from the board the pass was resolved against, which is
/// the state in which the cell was still present (<c>GAME_EVENTS.md</c> §2
/// item 2). This is the pass's own record of what it cleared, so a consumer does
/// not re-read the post-resolution board, where those cells no longer hold the
/// Gems that were consumed.
/// </param>
public readonly record struct PassResult(
    BoardState Board,
    RngState RngState,
    IReadOnlyList<MatchResolution> Matches,
    IReadOnlyList<GemMatchedEvent> MatchedCellGemMatched,
    IReadOnlyList<GemMatchedEvent> ActivationCellGemMatched,
    IReadOnlyList<ActivatedSpecialGem> ActivatedSpecialGems,
    IReadOnlyList<SpecialGemClaim> CreatedSpecialGems,
    IReadOnlyList<int> ClearedCellUnion,
    IReadOnlyList<ClearedGem> ClearedGems);

/// <summary>
/// One cell a pass cleared, with the Gem type it held and the match tier it
/// generates at (<c>COMBAT_RULES.md</c> §2, <c>MATCH3_RULES.md</c> §5.7).
///
/// This is <b>Transient Resolution State</b> (<c>GAME_STATE.md</c> §3): it is
/// pass-local accounting, is never a <c>BattleState</c> field, and is not
/// serialized or delivered.
/// </summary>
/// <param name="CellIndex">The cleared cell's §1.0 index.</param>
/// <param name="GemType">
/// The Gem type the cell held when it was cleared — always one of the four §1.1
/// types. A cell that held a Special Gem reports the Gem type that cell carried:
/// a Special Gem adds metadata to a cell's occupant and does not replace its type
/// (<c>GAME_STATE.md</c> §2.1.3 items 1–2).
/// </param>
/// <param name="Tier">
/// The tier the cell generates at. For a cell consumed by a Match this is the
/// tier of the shape that consumed it (<c>COMBAT_RULES.md</c> §2 item 2); for a
/// cell cleared only by a Special Gem activation it is
/// <see cref="MatchTier.Base"/> (<c>§2 item 1</c>, <c>MATCH3_RULES.md</c> §5.7
/// item 6).
/// </param>
public readonly record struct ClearedGem(int CellIndex, GemType GemType, MatchTier Tier);

/// <summary>
/// A Special Gem the pass activated, recorded in activation order
/// (<c>MATCH3_RULES.md</c> §5.8.3 level 3).
/// </summary>
/// <param name="CellIndex">The cell the Special Gem occupied when it activated.</param>
/// <param name="SpecialGem">The Special Gem's type and orientation.</param>
/// <param name="ChainLevel">
/// The breadth-first level at which it activated. Level 0 holds the Special Gems
/// the pass's match set consumed — they are siblings, neither a chain of the
/// other; level ≥ 1 holds gems consumed by the affected set of a gem at the
/// previous level (<c>MATCH3_RULES.md</c> §5.5.5 item 7, §5.8.6 item 5).
/// </param>
public readonly record struct ActivatedSpecialGem(
    int CellIndex,
    SpecialGem SpecialGem,
    int ChainLevel);

/// <summary>
/// Board resolution — one detection pass, and the cascade loop that repeats it
/// (<c>MATCH3_RULES.md</c> §4).
///
/// The pass executes the §4.1 step order exactly, and the order may not be
/// reordered:
///
/// <code>
/// 1. Match Resolution   remove the union of the pass's matched cells
/// 2. Special Gems       activate the Special Gems the pass consumed, then
///                       create the Special Gems its shapes require, at their
///                       §5.5.3 positions - before Gravity runs
/// 3. Gravity            each column's remaining Gems fall to fill empty cells
/// 4. Spawn              draw new Gems into the now-empty cells at the top of
///                       each column
/// 5. Re-detect          run Match Detection on the completed board
/// </code>
///
/// Resolution is deterministic end to end (§4.6): given the same starting board,
/// the same match set, and the same <c>RngState</c>, every pass, removal, created
/// Special Gem, fall, spawn, and the final board and <c>RngState</c> are fully
/// determined. The only operation that consumes randomness is Spawn (§4.5 item 4);
/// everything else is a pure function of the board state (§4.6 item 3, §7.2).
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
    /// <param name="swapOriginIndex">
    /// The swap origin for this pass, or <c>null</c> for a Cascade pass
    /// (<c>MATCH3_RULES.md</c> §5.5.3 items 1–3).
    /// </param>
    /// <param name="cascadeDepth">
    /// The pass's depth: 1 for the first pass of a Swap, ≥ 2 for a Cascade
    /// (§4.2 items 1–2).
    /// </param>
    public static PassResult ResolvePass(
        BoardState board,
        IReadOnlyList<MatchShape> matchSet,
        Pcg32 rng,
        int? swapOriginIndex,
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
        // cell is cleared and its Special Gem consumed by the same act
        // (GAME_EVENTS.md §2 item 2, GAME_STATE.md §2.1.8 item 2).
        var matchedReports = BuildGemMatchedReports(board, matchedUnion);

        var empties = new HashSet<int>(matchedUnion);

        // --- §4.1 step 2, part 1: plan the pass's creations ---------------
        // §5.5.4 item 2: the pass computes its required Special Gem positions
        // FIRST. Those cells are reserved, and every affected set of this step's
        // activations is then reduced by them.
        var plan = SpecialGemPlanner.PlanCreations(matchSet, swapOriginIndex);

        // --- §4.1 step 2, part 2: gather the consumed Special Gems -------
        // §5.5.5 item 1: a Special Gem is consumed when it is part of the pass's
        // match set - its own colour matched normally, exactly like any other Gem.
        //
        // The order is the pass's §3.2 MATCH-SET order, not cell-index order:
        // §5.5.5 item 5 ("Two Special Gems activated by the same match set are
        // resolved sequentially, in the match-set order of §3.2") and §5.8.3 level 3
        // both fix it. The two orders differ whenever a vertical shape has a lower
        // start index than a horizontal shape, because §3.2 item 1 puts every
        // horizontal shape before every vertical one regardless of index.
        //
        // Iterating the match set and then each shape's own cells in ascending index
        // yields the §3.2 order directly. Within one shape's cells the order matters
        // only for the level-0 siblings it contains: §5.8.3 level 3a fixes
        // "ascending §1.0 index of the consumed Special Gems" for discoveries WITHIN
        // one effect, and the same ascending-index order is the consistent reading for
        // the members of one shape.
        var consumed = new List<ActivatedSpecialGem>();

        foreach (var shape in matchSet)
        {
            foreach (var index in shape.Cells)
            {
                if (board.SpecialGemAt(index) is { } gem)
                {
                    consumed.Add(new ActivatedSpecialGem(index, gem, ChainLevel: 0));
                }
            }
        }

        // --- §4.1 step 2, part 3: resolve their effects, including chains ---
        // §5.5.4 item 2 step 3 / §5.8.3 level 3: activation resolves over
        // (affected set − reserved cells), including chains, breadth-first.
        var activation = ResolveActivations(board, consumed, plan.ReservedCells);

        // --- §4.1 step 2, part 4: create the Special Gems -----------------
        // §5.5.1 item 1: after the match set is known and its matched cells are
        // removed, and before Gravity runs. The created Special Gem carries the Gem
        // type of the Gem that was removed from that cell in this pass
        // (§5.5.4 items 1, 4).
        var creations = new List<(int Index, Cell Cell)>();

        // The cells this step's activations cleared.
        foreach (var index in activation.ClearedCells)
        {
            empties.Add(index);
        }

        foreach (var claim in plan.CommittedClaims)
        {
            var cell = Cell.WithSpecial(claim.GemType, claim.SpecialGem);
            creations.Add((claim.CellIndex, cell));

            // §5.5.4 item 1: the cell is emptied by step 1 like every other matched
            // cell and then receives the Special Gem in step 2. The Special Gem is a
            // NEW board object at that cell; it is never "the same Gem, upgraded".
            //
            // §5.5.4 item 2: creation cells are RESERVED before any activation runs,
            // and an activation never clears a reserved cell. A committed creation
            // therefore always survives step 2 and reaches Gravity — so its cell is
            // removed from the empty set unconditionally, whatever produced the
            // emptiness. This is what makes the created Special Gem reach the board.
            empties.Remove(claim.CellIndex);
        }

        var afterStep2 = board.WithCells(creations);

        // --- §4.1 step 3: Gravity ----------------------------------------
        var gravity = Gravity.Apply(afterStep2, empties);

        // --- §4.1 step 4: Spawn ------------------------------------------
        // §4.5 item 1: spawn fills exactly the cells that are empty AFTER Gravity,
        // which are always the topmost cells of their column. A column with k
        // survivors spawns exactly k Gems into its top k cells.
        //
        // The set Spawn receives is the one Gravity produced, not the pre-gravity set:
        // a gem that fell has moved INTO a cell that was empty, so that cell is no
        // longer empty. Filling the pre-gravity set would overwrite the gems gravity
        // just moved — including the Special Gems this pass just created, which must
        // reach the published board (§4.1 item 2, §4.5 item 7).
        var spawn = Spawn.Fill(gravity.Board, rng, new HashSet<int>(gravity.VacatedCells));

        // --- Reports ------------------------------------------------------
        // GAME_EVENTS.md §1.3: within one resolved cleared union, GemMatched is
        // emitted once per cell of the union, in ascending cell index, within each
        // sub-step of the pass - the pass's matched cells first, then the
        // activations of that pass, breadth-first.
        //
        // The activation sub-step reports only cells the matched sub-step did not
        // already report: a cell is reported once over the whole pass's cleared
        // union (§5.8.2 item 2), and the matched cells are reported first.
        var activationReports = BuildGemMatchedReports(
            board,
            activation.ClearedCells,
            alreadyReported: matchedUnion);

        var matches = new List<MatchResolution>(matchSet.Count);
        for (var shapeIndex = 0; shapeIndex < matchSet.Count; shapeIndex++)
        {
            var shapeCreations = plan.CommittedClaims
                .Where(c => c.ShapeIndex == shapeIndex)
                .ToArray();

            matches.Add(new MatchResolution(matchSet[shapeIndex], cascadeDepth, shapeCreations));
        }

        // §5.8.2 item 5: the cleared set is the union of the matched cells and every
        // activation and chain cell of this pass, each cell once, ascending §1.0 index.
        // A SortedSet both de-duplicates and orders, so the union is built in one pass.
        var union = new SortedSet<int>(matchedUnion);
        union.UnionWith(activation.ClearedCells);
        var clearedUnion = union.ToArray();

        // --- Resource generation accounting (COMBAT_RULES.md §2, §5.7) ------
        // The same union again, carrying each cell's Gem type and the tier it
        // generates at. The type is read from `board` - the state the pass was
        // resolved against, in which the cell still held the Gem that was consumed
        // (GAME_EVENTS.md §2 item 2). The post-resolution board holds spawned Gems
        // in those cells, so it is not a source for this.
        //
        // Tier: a cell consumed by a Match generates at THAT SHAPE's tier
        // (COMBAT_RULES.md §2 item 2), and a cell cleared only by a Special Gem
        // activation generates at the base rate, because an activation is not a
        // Match and has no tier (§2 item 1, §5.7 item 6).
        //
        // A cell can be named by both sub-steps. It is then a member of the Match,
        // so it takes the Match's tier and is recorded once: the matched sub-step is
        // written first and an activation claiming a cell already accounted for at a
        // Match tier does not overwrite it (§5.8.2 items 2, 6 - a cell generates
        // once, and it is the Match that consumed it that generates for it).
        var tiersByCell = new Dictionary<int, MatchTier>(matchedUnion.Count);

        foreach (var shape in matchSet)
        {
            var tier = TierOf(shape);

            foreach (var index in shape.Cells)
            {
                // §3.3 item 3: a shared cell is one cell for removal and for resource
                // generation. An L/T's arms are one shape at one tier, so a shared
                // cell cannot be given two different tiers by this loop.
                tiersByCell[index] = tier;
            }
        }

        var clearedGems = new List<ClearedGem>(clearedUnion.Length);

        foreach (var index in clearedUnion)
        {
            var tier = tiersByCell.TryGetValue(index, out var matchTier)
                ? matchTier
                : MatchTier.Base;

            clearedGems.Add(new ClearedGem(index, board[index], tier));
        }

        return new PassResult(
            spawn.Board,
            rng.CurrentState,
            matches,
            matchedReports,
            activationReports,
            activation.Activated,
            plan.CommittedClaims,
            clearedUnion,
            clearedGems);
    }

    /// <summary>
    /// The tier a shape's consumed Gems generate at (<c>COMBAT_RULES.md</c> §2,
    /// <c>MATCH3_RULES.md</c> §5.5.2).
    ///
    /// <code>
    /// L/T                      → L/T        1.25×   (§5.4)
    /// straight line of 4       → Match 4    1.5×    (§5.2)
    /// straight line of 5+      → Match 5    2.0×    (§5.3; a run of 6+ is Match 5)
    /// straight line of 3       → Match 3    1.0×    (§5.1)
    /// </code>
    ///
    /// <b>The shape is one Match at one tier.</b> An L/T counts once at its own
    /// tier however many Special Gems it creates and however long its arms are:
    /// its arms' higher classifications decide which Special Gems are created, not
    /// a second, higher multiplier for the cells they share with the pattern
    /// (<c>MATCH3_RULES.md</c> §5.4 item 4 item 4, §5.4 item 6). Its tier is the
    /// L/T row, which <c>COMBAT_RULES.md</c> §2 owns at <c>1.25×</c>.
    /// </summary>
    private static MatchTier TierOf(MatchShape shape)
    {
        if (shape.IsLt)
        {
            return MatchTier.Lt;
        }

        return MatchTierMultipliers.ForStraightRun((shape.HorizontalArm ?? shape.VerticalArm)!.Length);
    }

    /// <summary>
    /// Resolves one pass's activations and their chains
    /// (<c>MATCH3_RULES.md</c> §5.5.5, §5.8.2, §5.8.3).
    ///
    /// The pass's consumed Special Gems are level 0 — siblings resolved in §3.2
    /// match-set order, each with its own complete affected set. A Special Gem
    /// inside another Special Gem's affected set is consumed by it and activates in
    /// turn at the next level: a chain resolved <b>breadth-first in activation
    /// order</b>, where all effects discovered at one level are resolved before any
    /// effect discovered by them (§5.5.5 item 7, §5.8.3 item 2).
    ///
    /// Each effect's affected set is computed against the board the pass produced,
    /// reduced by the reserved cells, and never against a board partially mutated
    /// by a sibling effect (§5.8.3 item 3). Overlap is handled by the union at the
    /// result level: a cell is cleared once, and it generates and reports once
    /// (§5.8.2 items 1–2).
    ///
    /// A Special Gem that has already activated in this step does not activate
    /// again, so a chain is finite: each activation either consumes a Special Gem
    /// that has not yet activated or changes nothing. No depth limit, recursion
    /// cap, or iteration budget is introduced, and none may be added as a safeguard
    /// (§5.5.5 item 7, §5.8.3 item 7).
    /// </summary>
    private static ActivationResult ResolveActivations(
        BoardState board,
        IReadOnlyList<ActivatedSpecialGem> levelZero,
        IReadOnlySet<int> reservedCells)
    {
        var activated = new List<ActivatedSpecialGem>();
        var activatedCells = new HashSet<int>();
        var cleared = new SortedSet<int>();

        // The breadth-first frontier: all effects discovered at one level, in the
        // order the consuming effects were resolved, and within one effect by
        // ascending §1.0 index of the consumed Special Gems (§5.8.3 level 3a).
        //
        // `levelZero` already arrives in the pass's §3.2 match-set order, which is the
        // order this level must be resolved in (§5.5.5 item 5, §5.8.3 level 3). It is
        // enqueued as given — re-sorting it here by cell index would substitute a
        // different, undocumented order.
        var frontier = new Queue<ActivatedSpecialGem>();

        foreach (var gem in levelZero)
        {
            frontier.Enqueue(gem);
        }

        var currentLevel = 0;

        while (frontier.Count > 0)
        {
            // One level at a time: everything already queued belongs to the current
            // level, and anything discovered during this level's processing belongs
            // to the next. Counting the queue down is what makes the traversal
            // breadth-first rather than depth-first.
            var levelCount = frontier.Count;
            currentLevel++;

            var nextLevelDiscoveries = new List<int>();
            var queuedForNextLevel = new HashSet<int>();

            for (var i = 0; i < levelCount; i++)
            {
                var gem = frontier.Dequeue();

                // A Special Gem activates at most once per step (§5.8.2 item 3). A
                // gem named by two effects, or consumed twice through overlapping
                // chains, activates once.
                if (!activatedCells.Add(gem.CellIndex))
                {
                    continue;
                }

                activated.Add(gem with { ChainLevel = currentLevel - 1 });

                // §5.5.5 item 4 / §5.8.2: the effective set is the clipped affected
                // set minus the reserved creation cells — unless the reserved cell
                // is the activated Special Gem's own cell, which every geometry
                // includes and which is therefore never withheld (§5.5.4 item 2,
                // §5.8.1 item 4).
                var affected = SpecialGemEffects.AffectedCells(gem.SpecialGem, gem.CellIndex);

                foreach (var index in affected)
                {
                    if (index != gem.CellIndex && reservedCells.Contains(index))
                    {
                        continue;
                    }

                    // §5.8.2 item 6: a cell cleared earlier in the same step is no
                    // longer on the board, so a later effect naming it has nothing to
                    // clear there. Adding to the set keeps the union semantics.
                    cleared.Add(index);
                }

                // §5.5.5 item 7: a Special Gem inside another Special Gem's affected
                // set is consumed by it and activates in turn.
                //
                // §5.8.3 level 3a orders a level by "the order the consuming effects
                // were resolved, and within one effect by ascending §1.0 index of the
                // consumed Special Gems". This loop walks the level in activation
                // order and, within one effect, in ascending index — so appending to a
                // list in this order IS the documented order. A sorted container would
                // instead re-order the whole level by cell index, which the contract
                // does not say.
                foreach (var index in affected.OrderBy(i => i))
                {
                    if (index == gem.CellIndex || activatedCells.Contains(index))
                    {
                        continue;
                    }

                    if (board.SpecialGemAt(index) is { } chained
                        && !reservedCells.Contains(index)
                        && queuedForNextLevel.Add(index))
                    {
                        nextLevelDiscoveries.Add(index);
                    }
                }
            }

            foreach (var index in nextLevelDiscoveries)
            {
                if (!activatedCells.Contains(index) && board.SpecialGemAt(index) is { } gem)
                {
                    frontier.Enqueue(new ActivatedSpecialGem(index, gem, currentLevel));
                }
            }
        }

        return new ActivationResult(activated, cleared.ToArray());
    }

    /// <summary>
    /// Builds the <c>GemMatched</c> reports for a cleared union, ascending §1.0
    /// cell index, reading Special Gem metadata from the <b>pre-removal</b> board
    /// (<c>GAME_EVENTS.md</c> §1.3, §2 item 2).
    /// </summary>
    private static IReadOnlyList<GemMatchedEvent> BuildGemMatchedReports(
        BoardState preRemovalBoard,
        IReadOnlyList<int> clearedCells,
        IReadOnlyList<int>? alreadyReported = null)
    {
        var skip = alreadyReported is null ? null : new HashSet<int>(alreadyReported);
        var reports = new List<GemMatchedEvent>(clearedCells.Count);

        // Ascending index is a total order: every cleared cell is in 0..63, no two
        // cells share an index, and there is no tie to break (GAME_EVENTS.md §1.3
        // item 2). The order is a property of the cell, not of the shape or effect
        // that contributed it.
        foreach (var index in clearedCells.Distinct().OrderBy(i => i))
        {
            if (skip is not null && skip.Contains(index))
            {
                continue;
            }

            reports.Add(new GemMatchedEvent(
                index,
                preRemovalBoard[index],
                preRemovalBoard.SpecialGemAt(index)));
        }

        return reports;
    }

    /// <summary>The activations of one pass and the cells they cleared.</summary>
    private readonly record struct ActivationResult(
        IReadOnlyList<ActivatedSpecialGem> Activated,
        IReadOnlyList<int> ClearedCells);
}