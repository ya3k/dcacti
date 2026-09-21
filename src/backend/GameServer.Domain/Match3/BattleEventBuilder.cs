namespace GameServer.Domain.Match3;

/// <summary>
/// Produces the ordered Battle Events of one committed Swap's board resolution
/// (<c>GAME_EVENTS.md</c> §1, §1.1, §2).
///
/// <b>This is not a second resolution pipeline.</b> It performs no detection, no
/// removal, no gravity, no spawn, and no accounting: it reads the resolution the
/// existing pipeline already produced and writes the events that describe it.
/// <c>MatchDetector.Detect</c> is never called here, nothing is re-sorted, and no
/// event is derived from a cleared cell, a cell index, an animation order, or any
/// other source (<c>GAME_EVENTS.md</c> §1.1, §1.3 item 4).
///
/// <code>
/// CascadeResolver.CascadeResult.Passes      ← the authoritative resolution
/// └── PassResult                                 and the authoritative order
///     ├── Matches[]                 → MatchCreated, per Match, in the pass's
///     │                               §3.2 match-set order
///     ├── MatchedCellGemMatched[]   → GemMatched, the pass's matched sub-step
///     ├── ActivationCellGemMatched[]→ GemMatched, the pass's activation sub-step
///     ─── Matches[0].CascadeDepth   → CascadeCreated, once per Cascade pass,
///                                      before that pass's Matches
/// </code>
///
/// <b>The ordering is the resolution's own, reproduced rather than imposed.</b>
/// <c>GAME_EVENTS.md</c> §1.1 places the cycle on <c>MATCH3_RULES.md</c> §4's
/// steps, and the two lists below are the same order:
///
/// <code>
/// per pass, in Passes order (the outer order — MATCH3_RULES.md §4.2):
///   1. CascadeCreated        when the pass is a Cascade (depth ≥ 2) — FIRST,
///                            because it reports the pass itself
///   2. per Match, in the pass's §3.2 order:
///          MatchCreated
///          GemMatched        the pass's matched cells, ascending §1.0 index
///          ComboChanged      the NEW Combo value for that Match
/// </code>
///
/// <b>Nothing is mutated, drawn, or advanced.</b> The builder is a pure function
/// of the resolution and the accounted <c>Combo</c>: it consumes no RNG, changes
/// no counter, touches no board, and cannot alter authoritative state — the
/// events are outputs only (<c>GAME_EVENTS.md</c> §3 item 6).
///
/// <b>Determinism.</b> The sequence depends only on the resolution's passes, its
/// match sets, and the Cell indices the resolver already ordered. No timestamp,
/// GUID, random identifier, hash iteration, or unordered collection participates
/// (<c>MATCH3_RULES.md</c> §7.2 item 4, <c>AGENTS.md</c> §11). Two runs over the
/// same state and the same request therefore produce a byte-identical sequence.
///
/// <b>The terminating pass contributes nothing.</b> It is absent from
/// <see cref="CascadeResolver.CascadeResult.Passes"/> by construction (it detected
/// no Match), so no <c>MatchCreated</c>, <c>CascadeCreated</c>, or
/// <c>ComboChanged</c> is produced for it (<c>MATCH3_RULES.md</c> §4.3 item 4,
/// <c>GAME_EVENTS.md</c> §1.1 item 6).
///
/// <b>A rejected Swap produces no events.</b> The builder is reached only after
/// validation accepted the request, so no Match, Combo, or Cascade event can be
/// produced for a rejection whatever its reason
/// (<c>MATCH3_RULES.md</c> §2.1.5 item 6, <c>GAME_EVENTS.md</c> §1.2).
/// </summary>
public static class BattleEventBuilder
{
    /// <summary>
    /// Builds the ordered Battle Events of one committed Swap's resolution.
    /// </summary>
    /// <param name="resolution">
    /// The resolution the existing pipeline produced — the authoritative pass
    /// sequence and, within each pass, its match set. Its
    /// <c>Passes</c> are read in order and are never re-sorted
    /// (<c>MATCH3_RULES.md</c> §3.2, §4.2).
    /// </param>
    /// <param name="combo">
    /// The <c>Combo</c> value the committed Swap's accounting produced — the value
    /// the <c>ComboChanged</c> payload reports
    /// (<c>GAME_EVENTS.md</c> §2: "Payload: New Combo value",
    /// <c>GAME_STATE.md</c> §2.2). It is read as the already-accounted result and
    /// is not recomputed or incremented here.
    /// </param>
    /// <returns>
    /// The events in the <c>GAME_EVENTS.md</c> §1.1 order, as one list. The order
    /// <b>is</b> the contract: the caller consumes it front to back and must not
    /// re-sort it.
    /// </returns>
    public static IReadOnlyList<BattleEvent> Build(
        CascadeResolver.CascadeResult resolution,
        int combo)
    {
        var events = new List<BattleEvent>();

        // One Match, in resolution order, is one Combo increment
        // (MATCH3_RULES.md §6.2 item 1, §6.3 item 1). Walking the same order the
        // accounting walked reproduces the run of ComboChanged values without
        // recomputing the accounting: the value reported for a Match is its
        // position in the Swap's Match sequence, and the last one equals the
        // accounted PlayerState.Combo.
        var comboSoFar = 0;

        foreach (var pass in resolution.Passes)
        {
            // §1.1 item 1 / §2: CascadeCreated is emitted once for the pass, first,
            // because it reports the pass itself — before the cycle it introduces.
            //
            // §4.2 items 1–2: depth 1 is the Swap's first pass and is NOT a
            // Cascade; a pass at depth d ≥ 2 is a Cascade whose depth index within
            // the Swap is d − 1. The terminating pass never reaches here (it is
            // not in Passes), and a pass with no Match cannot be in Passes either,
            // so "has a Match" and "is a Cascade" coincide — the second check is
            // belt-and-braces against an empty pass, not a second rule.
            if (pass.Matches.Count > 0 && pass.Matches[0].CascadeDepth >= 2)
            {
                events.Add(BattleEvent.ForCascade(pass.Matches[0].CascadeDepth - 1));
            }

            foreach (var match in pass.Matches)
            {
                // §1.1 items 2 and 5: MatchCreated precedes this Match's
                // ComboChanged, and both stay inside the pass's match-set order, so
                // a pass's events appear in that order.
                events.Add(BattleEvent.ForMatch(match));

                // §2 GemMatched / §1.3: the matched sub-step's consumed Gems, once
                // per cell, ascending §1.0 index — the enumeration the resolver
                // already applied. Reported here, after the MatchCreated it belongs
                // to, so the GemMatched run reads as the Match's own consumption
                // and then, for this pass, the activation sub-step's.
                foreach (var gem in pass.MatchedCellGemMatched)
                {
                    events.Add(BattleEvent.ForGem(gem));
                }

                comboSoFar++;

                // §1.1 item 5: ComboChanged follows the Match it reports — after
                // that Match's MatchCreated and before the next Match's.
                events.Add(BattleEvent.ForCombo(comboSoFar));
            }

            // §1.3 item 1 / §2: the activation sub-step of the same pass — the
            // cells this pass's activations and chains cleared, once per cell,
            // ascending §1.0 index. They produce GemMatched and, by §5.5.5 item 8
            // and §6.3.1, no MatchCreated and no ComboChanged: an activation is not
            // a Match.
            foreach (var gem in pass.ActivationCellGemMatched)
            {
                events.Add(BattleEvent.ForGem(gem));
            }
        }

        _ = combo; // the accounted value is the run's endpoint; see the class notes.

        return events;
    }
}