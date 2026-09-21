# ADR-010: Committed-Swap State for Idempotent Swap Rejection

**Status:** Accepted
**Date:** 2026-09-20

## Context

`MATCH3_RULES.md` §2.1.2 defines a four-check validation order for a player
Swap, and requires that "**all checks pass** before the swap is committed".
Check 3 of that order is the already-applied / idempotency check, whose rule
§2.1.4 item 2 states as: reject as `STALE_ACTION` when the action's unordered
pair `{from, to}` "is exactly the pair most recently committed to the board".

No state capable of expressing that value existed. `GAME_STATE.md` §2's
`BattleState` field list had no such member, §2.0.5's Board Foundation stage
had none, and `BoardState` cannot carry one: §2.1.2 item 1 fixes `BoardState`
at exactly one field, `Cells[64]`. A swap leaves no distinguishing trace in the
board — after an exchange the board looks precisely as it would had the two
cells simply been in those positions, so "exchanged once" and "exchanged back"
are indistinguishable from `Cells[64]` alone. §3's `ResolutionContext` and its
pass-local bookkeeping are transient and discarded, so they cannot hold it
either.

This gap was identified and deliberately deferred by the Swap-validation task,
which implemented the three stateless checks and reported the fourth as
unimplementable at that layer. The Swap-execution task then stopped rather than
implement §2.1.2's ordered check list while silently omitting check 3, because
§2.1.2 makes the order normative: "Checks are evaluated in the order above so
that two implementations reject the same action with the same reason when more
than one check could fail."

Two constraints bound the solution and rule out the obvious shortcuts:

- `MATCH3_RULES.md` §2.1.1 item 3 forbids a client-supplied gameplay field, so
  the request cannot carry the record.
- `MATCH3_RULES.md` §2.1.4 item 1 and `SIGNALR_PROTOCOL.md` §2 item 1 both
  state `clientSequence` is an opaque correlation id that is **not** used to
  detect staleness. The client cannot be the source of truth.

## Decision

The most recently committed Swap is recorded as one optional field of
authoritative `BattleState`:

```text
BattleState.LastCommittedSwapPair?
    └── (MinCellIndex, MaxCellIndex)    both §1.0 indices,
                                        MinCellIndex < MaxCellIndex
```

1. **Owner.** `BattleState` (`GAME_STATE.md` §2), not `BoardState` and not the
   request. It is authoritative server state (`GAME_RULES.md` §18, ADR-001),
   and it is declared in §2 because §0 item 5 requires a field added by a later
   stage to be a field §2 already declares.
2. **Canonical representation.** The pair is stored with its two indices in
   ascending order, because `MATCH3_RULES.md` §2.1.1 item 2 makes the unordered
   pair `{from, to}` the swap's identity and §2.1.3 item 3 makes the two inputs
   symmetric. Request argument order is never the stored identity, so
   `(13, 12)` and `(12, 13)` record the same value.
3. **Initial value is absence.** Before the first committed Swap the field is
   absent. No sentinel pair (`(0, 0)`, `(-1, -1)`, or a defaulted `(0, 1)`) is
   used, matching the absent-`SpecialGem` convention of `GAME_STATE.md` §2.1.7
   item 3. `(0, 0)` is not a representable pair at all, since item 2 above
   requires two distinct indices.
4. **Written only by a commit.** It is set when a Swap is committed, in the
   same single post-resolution write-back as `Turn` and `Sequence`
   (`GAME_STATE.md` §5.1). It is not a version and never a concurrency token.
5. **A rejection changes nothing.** For every rejection reason, including
   `STALE_ACTION`, the field keeps its value and no pair is cleared
   (`MATCH3_RULES.md` §2.1.5).
6. **Not delivered to the client.** It stays authoritative server-side state
   and is not added to the `BattleStateUpdated` payload
   (`SIGNALR_PROTOCOL.md` §4 item 4 admits no field beyond the implemented
   stage's).

The full state contract is owned by `GAME_STATE.md` §2.1.10; the rule that
reads it remains owned by `MATCH3_RULES.md` §2.1.4.

## Alternatives Considered

### Option A — Store the pair in `BoardState`

Rejected. `GAME_STATE.md` §2.1.2 item 1 defines `BoardState` as exactly one
field, `Cells[64]`, and §2.1.2 item 2 rejects a second collection precisely
because two writers of one fact can disagree. More fundamentally the value is
not derivable from the board: an exchange leaves no mark, so no `Cells[64]`
representation can distinguish a committed swap from an unswapped board.
Recording it on the board would also make a board-level concept out of what is
action history.

### Option B — Use `clientSequence` as the staleness token

Rejected, and explicitly forbidden. `MATCH3_RULES.md` §2.1.4 items 1 and 5 and
`SIGNALR_PROTOCOL.md` §2 item 1 all state the value is an opaque correlation id
and not an action version, and §2.1.4 item 5 states `STALE_ACTION` is "not a
version-mismatch code: nothing in this contract compares a client-supplied
number against `BattleState.Sequence`". It would also hand the client authority
over a server-side decision (`GAME_RULES.md` §18).

### Option C — Compare against `Turn`

Rejected. A Turn is one committed Swap (`GAME_RULES.md` §2 item 1), so the
current `Turn` value identifies *that* a swap happened but not *which pair* it
was. `MATCH3_RULES.md` §2.1.4 item 2 defines the check against the pair, and
§2.1.4 item 3 states a Swap is otherwise stateless against the current board
with no per-Turn queue — so no Turn number can reconstruct the pair.

### Option D — Record the full action history or an action id

Rejected as overengineering. `MATCH3_RULES.md` §2.1.4 defines staleness against
the **most recent** commit only, and §2.1.4 item 3 defines no queue of
superseded actions. `ARCHITECTURE.md` §5.2 excludes event-sourcing
infrastructure for MVP. Storing more than the one pair would add state no rule
reads.

### Option E — Reject the swap by simulating it twice

Rejected. Applying the pair again reverts the board, but §2.1.2 item 4 requires
a Match to commit, and a reverted board cannot be distinguished from a legal
swap that merely produces no match — the two would collapse into one rejection
reason. `MATCH3_RULES.md` §2.1.4 item 2 also requires the pair be recognised as
*already applied*, which is not what a simulation tests.

## Why

The check `MATCH3_RULES.md` §2.1.2 already mandates needs exactly one fact —
which pair the board's current arrangement came from — and that fact exists
nowhere in the state model. Adding it as one optional `BattleState` field is
the smallest change that makes the documented validation order implementable as
written, and it sits in the document §0 item 5 designates as the home for every
`BattleState` field.

It is also the only placement consistent with the server-authority rule
(ADR-001): the value is derived by the server from its own commit history, so
no client can declare an action already committed.

## Consequences

### Positive
- `MATCH3_RULES.md` §2.1.2's four-check order becomes implementable exactly as
  documented, with check 3 evaluated in its documented position.
- The value is server-derived, so staleness cannot be influenced by the client.
- One optional field, with no new key, message, method, or store.

### Negative
- `BattleState` gains a member, so the §2.0.5 staged subset grows. This is the
  documented direction of §0 item 5 (stages grow toward §2) but it does mean
  any serializer must round-trip the field and its absence
  (`GAME_STATE.md` §2.1.10 item 10, `REDIS_STATE.md` §7 item 11).
- A recovery snapshot taken before the first commit and one taken after it are
  distinguishable by a member the client never sees; the server-side snapshot
  must preserve it for staleness to behave identically after recovery
  (ADR-008).

### Trade-offs
- Storing only the most recent pair means the server cannot answer "was this
  pair ever committed earlier in the battle?" — only "is it the most recent
  commit?". That is exactly the scope `MATCH3_RULES.md` §2.1.4 item 3 defines
  (a Swap is validated against the *current* board, with no per-Turn queue), so
  the narrower state is the correct one rather than a limitation.

## Related Documents

- `docs/01-game-design/MATCH3_RULES.md` (§2.1.1, §2.1.2, §2.1.4, §2.1.5,
  §2.1.6, §8.3)
- `docs/01-game-design/GAME_RULES.md` (§2, §18)
- `docs/02-technical/GAME_STATE.md` (§0, §2, §2.0.5, §2.1.10, §5.1)
- `docs/02-technical/SIGNALR_PROTOCOL.md` (§2, §4)
- `docs/02-technical/REDIS_STATE.md` (§4, §7)
- `docs/03-decisions/ADR/ADR-001-server-authoritative-battle.md`
- `docs/03-decisions/ADR/ADR-008-battle-recovery.md`