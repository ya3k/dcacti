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
///     ├── GemMatched[]              → GemMatched, the pass's matched cells
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
///          GemMatched        ONLY for the pass's first Match: the pass's matched
///                            cells are ONE union for the pass, ascending §1.0
///                            index, once per cell (§1.3 items 1–2)
///          ComboChanged      the NEW Combo value for that Match
///   3. CascadeStepResolved   the pass's completed Gravity + Spawn transition —
///                            LAST, after the pass's own reports and before the
///                            next pass's CascadeCreated (§1.1 item 8, §3.2.28)
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
/// <b><c>ComboChanged</c> is produced only for the resolution the Combo
/// accounting counted.</b> The event's payload is the <b>new</b> value of
/// <c>BattleState.Combo</c> (<c>GAME_EVENTS.md</c> §2, <c>SIGNALR_PROTOCOL.md</c>
/// §3.2.8 item 1), and that counter is the Player action's committed Swap's
/// (<c>GAME_STATE.md</c> §2.2 — written in the round's single write-back for a
/// committed Swap only; delivered under the fixed <c>playerState</c> label;
/// <c>BOSS_RULES.md</c> §6.2/§6.2.5 read "Player Matches" / "Player Combo"). A
/// resolution the accounting did not count therefore reports no <c>Combo</c> at
/// all — it is described by <paramref name="accountedCombo"/> being
/// <see langword="null"/> — so the event can never carry a value that disagrees
/// with the state field it reports. The Boss Response phase's own board action is
/// that case (<c>BattleStateService</c> step 18a: <c>Combo</c> and
/// <c>MatchCount</c> stay the Player action's and that phase writes neither);
/// the Player's committed Swap is not.
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
    /// <param name="accountedCombo">
    /// The <c>Combo</c> value the committed Swap's accounting produced, when this
    /// resolution is the one it counted — the value the <c>ComboChanged</c> payload
    /// reports (<c>GAME_EVENTS.md</c> §2: "Payload: New Combo value",
    /// <c>GAME_STATE.md</c> §2.2). It is read as the already-accounted result and
    /// is not recomputed or incremented here.
    /// <para>
    /// <see langword="null"/> states that this resolution's Matches are <b>not</b>
    /// the ones <c>BattleState.Combo</c> counts — the Boss Response phase's own
    /// board cycle (<c>GAME_STATE.md</c> §2.2, <c>BOSS_RULES.md</c> §6.2.5) — so no
    /// <c>ComboChanged</c> is produced for it and its Matches are reported by
    /// <c>MatchCreated</c>/<c>GemMatched</c> alone. The parameter is deliberately
    /// not defaulted: which resolution the accounting counted is a statement each
    /// caller must make, not one this projector may assume.
    /// </para>
    /// </param>
    /// <returns>
    /// The events in the <c>GAME_EVENTS.md</c> §1.1 order, as one list. The order
    /// <b>is</b> the contract: the caller consumes it front to back and must not
    /// re-sort it.
    /// </returns>
    public static IReadOnlyList<BattleEvent> Build(
        CascadeResolver.CascadeResult resolution,
        int? accountedCombo)
    {
        var events = new List<BattleEvent>();

        // One Match, in resolution order, is one Combo increment
        // (MATCH3_RULES.md §6.2 item 1, §6.3 item 1). Walking the same order the
        // accounting walked reproduces the run of ComboChanged values without
        // recomputing the accounting: the value reported for a Match is its
        // position in the Swap's Match sequence, and the last one equals the
        // accounted BattleState.Combo.
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

            // §1.3 items 1–2: the pass's cleared union is ONE union for the pass —
            // the pass's matched cells, ascending §1.0 index, each cell once — and it
            // is not a per-Match list. It is therefore reported once per pass: with
            // the MatchCreated that opens the pass's match sequence, which is the
            // position §1.1's cycle fixes for it. Emitting it once per Match would
            // report every matched cell of a pass with M ≥ 2 Matches M times, which
            // §1.3 item 2's "reported once" and item 7's "repeats a cell" forbid.
            var clearedUnionReported = false;

            foreach (var match in pass.Matches)
            {
                // §1.1 items 2 and 5: MatchCreated precedes this Match's
                // ComboChanged, and both stay inside the pass's match-set order, so
                // a pass's events appear in that order.
                events.Add(BattleEvent.ForMatch(match));

                // §2 GemMatched / §1.3: the pass's consumed Gems, once per cell,
                // ascending §1.0 index — the enumeration the resolver already
                // applied. Reported with the first MatchCreated of the pass it
                // belongs to, so the GemMatched run reads as the pass's own cleared
                // union.
                if (!clearedUnionReported)
                {
                    foreach (var gem in pass.GemMatched)
                    {
                        events.Add(BattleEvent.ForGem(gem));
                    }

                    clearedUnionReported = true;
                }

                comboSoFar++;

                // §1.1 item 5: ComboChanged follows the Match it reports — after
                // that Match's MatchCreated and before the next Match's.
                //
                // Only for the resolution the Combo accounting counted (the class
                // notes): reporting this run for a cycle the accounting did not
                // count would assert a Combo the state field never took, and the
                // event's payload is that field's new value.
                if (accountedCombo is not null)
                {
                    events.Add(BattleEvent.ForCombo(comboSoFar));
                }
            }

            // §1.1 item 8 / §3.2.28: the pass's completed Gravity + Spawn
            // transition is reported as the pass's LAST item — after every Match,
            // Gem and Combo report of the pass and before the next pass's
            // CascadeCreated, so the run reads as "the pass was detected, these
            // cells were consumed, this is how the board changed".
            //
            // A report of the transition cannot precede the transition it reports
            // (§4.1 steps 1–4), and the pass's own results are what is reported:
            // the survivors Gravity relocated and the Gems Spawn placed, both as
            // those steps produced them. Nothing is derived, no board is carried,
            // and the pass's cleared cells already travelled in the GemMatched run
            // above (§1.3, §3.2.28 item 4).
            events.Add(BattleEvent.ForCascadeStepResolved(
                pass.Matches[0].CascadeDepth,
                pass.DropMoves,
                pass.SpawnedGems));
        }

        // The accounted value is the run's endpoint (see the class notes): when it
        // is present, the number of ComboChanged items above is the resolution's
        // Match total and the last value equals this one. It is never re-derived
        // into an event value here.
        _ = accountedCombo;

        return events;
    }
}