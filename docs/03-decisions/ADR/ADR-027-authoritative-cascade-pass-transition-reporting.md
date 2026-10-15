# ADR-027: Authoritative Cascade Pass Transition Reporting

**Status:** Accepted
**Date:** 2026-10-15
**Deciders:** Operator (Product Owner), Systems Designer, Backend Architecture, Client Architecture
**Consulted:** AGENTS.md, docs/AGENTS.md, GAME_RULES.md, MATCH3_RULES.md, GAME_STATE.md, GAME_EVENTS.md, SIGNALR_PROTOCOL.md, docs/03-decisions/README.md

## Context

The Match-3 resolution is a repeating cycle of detection passes
(`MATCH3_RULES.md` §4). Until this decision, a pass's Gravity and Spawn were
**server-internal** steps: `MATCH3_RULES.md` §4.4 item 7 states that Gravity
"produces no Match, no Combo, and no events", `GAME_EVENTS.md` §1.1 placed
`gravity + spawn` on the cycle with "(no events)", and `SIGNALR_PROTOCOL.md`
§3.1 item 2, §3.2.12 item 1, and §8 items 5 and 7 prohibited per-pass delivery,
a board on any event, and any board-resolution-specific message. The
consequence was a **settle-only** client presentation: a multi-pass round
disappeared and re-appeared in bulk at the end of the round, and passes `>= 2`
could not be animated, because the client was never told where a surviving Gem
fell or which cell a refill placed a Gem in (`TASK-254` DR-1 Option A;
`TASK-275`).

`TASK-277` established that closing that gap is **not merely an implementation
task**: it requires a protocol change that the prohibitions above appear to
forbid. `TASK-278` recorded the authorization gate, the required approval
artifact, and the mandatory document-update order, and presented the Operator
with the deltas-only and hybrid proposals for decision. The request was
therefore escalated rather than resolved unilaterally (`AGENTS.md` §4, §7, §18,
§20).

The Operator Decision then granted the authorization this record documents:

> "Approve the addition of `CascadeStepResolved` to the existing `ReceiveEvents`
> batch protocol", carrying `passDepth`, `dropMoves` (authoritative
> source+destination coordinates for each gem movement) and `spawnedGems`
> (authoritative spawn positions and gem types); the server emits it only after
> that cascade pass has completed gravity and refill resolution; the client uses
> it to orchestrate Match/Fall/Refill animations and subsequent cascade passes
> in order.

That Decision approves `TASK-278` **A-1(i)** (authorize the additive per-pass
presentation event) and **A-4** (reconcile the explicit prohibitions), subject
to seven constraints: (1) no full board snapshot per cascade pass, (2) no
client-side RNG simulation, (3) no Redis CAS semantic change, (4) no change to
existing event sequence semantics, (5) existing turn banner behavior preserved,
(6) no second authoritative source of truth for board state, and (7) the
approved per-pass animation requirements preserved. `TASK-278` remains open
pending review.

**This ADR is written last, as a historical record.** The governing documents
were amended first, in the order `TASK-278` §2.3 fixes — `GAME_RULES.md` §16,
then `GAME_EVENTS.md`, then `SIGNALR_PROTOCOL.md`, then `GAME_STATE.md` — and
this record documents the decision those amendments establish. It creates no
contract of its own (`docs/03-decisions/README.md` §1, §3, §5).

## Decision

1. **One additive event, in the existing batch.** `CascadeStepResolved` is added
   to the canonical event list (`GAME_RULES.md` §16) and to the closed wire
   discriminator set (`SIGNALR_PROTOCOL.md` §3.2.2, 18 names becoming 19). It is
   an item of the round's **existing single atomic `ReceiveEvents` batch**; it
   adds no hub method, no argument, no subscription, no state-push member, and
   no per-pass or mid-resolution network push.

2. **The payload is fixed and deltas-only.** `passDepth` (integer, always
   present: the reported pass's own 1-based depth, `1` for the pass on the
   committed Swap's board, `>= 2` for a Cascade pass, identical to
   `MatchCreated.cascadeDepth` on that pass and to
   `CascadeCreated.cascadeDepth + 1` on a pass preceded by a `CascadeCreated` —
   a **new member name on purpose**, because `cascadeDepth` is never reused for
   a third meaning), `dropMoves` (`fromCell`/`toCell` per surviving Gem that
   changed cell during that pass's Gravity; omitted when empty), and
   `spawnedGems` (`cellIndex`/`gemType` per Gem that pass's Spawn placed;
   omitted when empty). Both arrays carry the fixed read orders
   `MATCH3_RULES.md` §4.4 item 1 and §4.5 item 2 already own. Exact wire schema:
   `SIGNALR_PROTOCOL.md` §3.2.28.

3. **A per-pass board was rejected; the deltas are the whole delta.** The event
   carries **no `board`**, and no member is added to it for the acting side, the
   pass's cleared cells, or Special Gem metadata inside a spawn. A per-pass
   board would be **redundant with the state push** (the authoritative board is
   already delivered by `BattleStateUpdated` and `GetBattleState`), it would be a
   **second authoritative source of board state**, and it is **prohibited** by
   `SIGNALR_PROTOCOL.md` §3.2.12 item 1 ("no event carries … a board") and §8
   item 5 (no board-specific message). The acting side is already carried by the
   sibling `SwapStarted.actor`; the pass's cleared cells are already delivered by
   that pass's `GemMatched` run (one union per pass); and Spawn never creates a
   Special Gem (`MATCH3_RULES.md` §4.5 item 7). Special Gem state remains part of
   the board and travels in the state push, as before.

4. **Emission point and position.** Exactly one `CascadeStepResolved` per
   **recorded** pass, emitted only after that pass's Gravity and Spawn have
   completed, and placed as that pass's **last** item — after its
   `MatchCreated`/`GemMatched`/`ComboChanged` and before the next pass's
   `CascadeCreated` (or before the following stage's events). **No item is
   emitted for the terminating pass that detects no Match**
   (`GAME_EVENTS.md` §1.1 items 6 and 8). The event reports a pass transition; it
   reorders no existing item and changes no existing event sequence semantics.

5. **The prohibitions are reconciled narrowly, never weakened.** The governance
   documents now admit **one event *report* of a pass transition inside the
   round's single batch** while keeping the existing prohibitions in force:
   `SIGNALR_PROTOCOL.md` §3.1 item 2 still forbids per-step delivery, per-pass
   **messages**, and partial batches (and states that the new item is a report,
   not a delivery); §3.1 item 3 still introduces no gameplay message type; and
   §8 items 5 and 7 still admit no board-specific and no
   board-resolution-specific **message** or subscription. `GAME_RULES.md` §16
   gains the name, and `GAME_EVENTS.md` §1.1's cycle no longer claims the step
   emits nothing: Gravity and Spawn still emit nothing themselves — the pass's
   completed transition is what is reported. No prohibition was deleted, and no
   per-pass board, message, or partial batch became legal.

6. **Intermediate board state stays transient.** `GAME_STATE.md` §3 (now item 5),
   §5.1 item 2, and §5.3 item 4 state that the pass transition is **reported by
   an event** while every intermediate board configuration remains **Transient
   Resolution State**: never written to Redis mid-resolution, never in a
   snapshot, and never delivered as a board. The board is delivered only in
   `BattleStateUpdated` / `GetBattleState`.

7. **Nothing about persistence, sequencing, or recovery changes.** Redis CAS
   semantics are unchanged (one atomic compare-and-set write-back per round);
   the round remains a **single atomic `ReceiveEvents` batch** with one
   `serverSequence`; `Sequence` semantics are unchanged (exactly one new value
   per committed round); snapshot recovery remains `ADR-008`'s (a snapshot
   supersedes pending batches and is never replayed as events); and the existing
   turn banner behavior is preserved. The event adds no store write, no Redis
   key, no snapshot field, and no second write path.

8. **RNG stays server-authoritative.** The event carries no RNG state and no
   value the client may draw from; the client performs no RNG simulation and
   never reads `rngSeed`/`rngState` (`GAME_RULES.md` §18, `TDD.md` §2.1,
   `AGENTS.md` §11). Determinism of Gravity and Spawn is unchanged.

9. **Client use is bounded presentation input.** The client may use the item
   **only to present the pass it reports** — Match/Fall/Refill animation and the
   ordering of subsequent cascade passes. It derives no gravity destination, no
   spawned identity, and no board of its own. A **missing, malformed, or
   internally inconsistent** item is **skipped**, and presentation degrades to
   the existing single settle convergence; skipping it never changes gameplay
   state, never holds input, and never weakens the existing sequence guarantees
   or recovery rules (`SIGNALR_PROTOCOL.md` §3.2.28 item 8, §6, §7).

10. **Compatibility is one additive discriminator plus an explicit
    tolerant-reader rule.** An item whose `type` is not in the discriminator
    table is **ignored** by a consumer: it is not a desync, not a second event,
    and not evidence about the resolution
    (`SIGNALR_PROTOCOL.md` §3.2.2 item 6). **No version negotiation exists** —
    no handshake, capability member, or negotiation channel — and none is
    introduced by this decision.

## Alternatives Considered

- **Per-pass full board snapshot (the "hybrid" proposal, `TASK-276`).**
  Rejected per Decision item 3: it duplicates the state push, creates a second
  authoritative statement of board state, and is prohibited by
  `SIGNALR_PROTOCOL.md` §3.2.12 item 1 and §8 item 5. It also multiplies the
  per-pass wire cost for facts the client already receives once per round.
- **Client-side reconstruction of the pass (client-computed Gravity and Spawn,
  or client-side RNG).** Rejected: the battle server is authoritative
  (`GAME_RULES.md` §18, ADR-001), Determinism & RNG forbids a second
  randomization mechanism and uncontrolled client randomness (`AGENTS.md`
  §11), and a client-computed board would violate the "no second authoritative
  source of board state" constraint (6).
- **A per-pass message or a second `ReceiveEvents` call.** Rejected:
  `SIGNALR_PROTOCOL.md` §3.1 item 2 forbids per-pass messages and partial
  batches, and the round's single atomic batch is what the sequence, desync and
  recovery contracts are built on (`§6`, `§7`).
- **Keeping settle-only presentation (`TASK-254` DR-1 Option A / A-1(ii)).**
  Considered and not selected by the Operator: it needed no protocol change but
  permanently forgoes per-pass Match/Fall/Refill animation for passes `>= 2`.
- **Carrying `actor` or `clearedCells` inside the new event.** Rejected as
  redundant: the acting side is the sibling `SwapStarted.actor`, and the pass's
  cleared cells are already delivered by that pass's `GemMatched` union — a
  second spelling of one fact is exactly what `GAME_STATE.md` §0 item 5 avoids.

## Consequences

### Positive

- The client can orchestrate per-pass Match/Fall/Refill animation in order, for
  the pass it was told about and no other, without simulating anything.
- The wire delta is small (one item per recorded pass carrying only the
  movements and spawns of that pass) and the authoritative board keeps exactly
  one delivery path.
- Server authority, determinism, the single atomic round, Redis CAS, `Sequence`
  semantics, and snapshot recovery are all untouched.
- The protocol's compatibility rule is now stated explicitly for any future
  additive discriminator, so an older consumer's behavior is defined rather
  than assumed.

### Negative

- A round's batch grows with its pass count, and the payload of a pathological
  full-board pass is the largest item in the batch.
- A consumer that ignores the item (older client, or a skipped malformed item)
  falls back to the settle-only presentation for that round — an accepted,
  documented degradation rather than an error.
- The event is presentation-only data that must never be promoted into a second
  board authority; that boundary is stated in the wire subsection and in
  `GAME_STATE.md` §3, and must be re-checked by any future consumer.
- Implementation is downstream and not authorized by this record: the wire
  projection, Domain event, and client presenter remain separate tasks
  (`TASK-278` §2.3 steps 6–7); `TASK-278` itself remains open pending review.

## Related Documents

- `docs/01-game-design/GAME_RULES.md` §16, §17, §18
- `docs/01-game-design/MATCH3_RULES.md` §4.2, §4.4 (item 1), §4.5 (items 2, 4, 7)
- `docs/02-technical/GAME_EVENTS.md` §1, §1.1 (items 6, 8), §2, §3
- `docs/02-technical/SIGNALR_PROTOCOL.md` §3, §3.1 (items 1–3), §3.2.2,
  §3.2.5, §3.2.6, §3.2.7, §3.2.9, §3.2.12 (item 1), §3.2.26, §3.2.27, §3.2.28,
  §4, §6, §7, §8 (items 5, 7)
- `docs/02-technical/GAME_STATE.md` §3 (item 5), §5.1 (items 1–2), §5.3 (items
  3–4)
- `docs/02-technical/TDD.md` §2.1, §6
- `docs/03-decisions/README.md` §1, §3, §5, §7
- `docs/03-decisions/ADR/ADR-001-server-authoritative-battle.md`
- `docs/03-decisions/ADR/ADR-008-battle-recovery.md`
- `docs/03-decisions/ADR/ADR-026-shared-board-gameplay-and-actor-relative-match-resolution.md`
- `tasks/backlog/TASK-278-cascade-protocol-authorization-gate.md`
