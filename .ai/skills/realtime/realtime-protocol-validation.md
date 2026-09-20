# skills/realtime/realtime-protocol-validation.md — Skill: Realtime Protocol Validation

**Version:** 1.0

> Validate that hub methods, event delivery, ordering, sequencing, and
> reconnect/resync behave as documented — with the server as the only
> authority and the client as a renderer of ordered events.

## Purpose

Check server → client event delivery and client → server requests against
`SIGNALR_PROTOCOL.md`, `GAME_EVENTS.md`, and `GAME_STATE.md`: event set and
order, one-call-per-resolution delivery, sequence numbering, request
acknowledgement, concurrency, and snapshot-based reconnect (ADR-004, ADR-008).

## When to Use

- A task adds/changes a hub method, an event, event ordering, or the
  client's event handling.
- `development/refactor.md` §2 (events and wire behavior must not change).
- `quality/testing.md` §1 "Realtime tests".
- `quality/review.md` for changes to `Hubs/`, the SignalR infrastructure, or
  the client `network/` layer.

## When Not to Use

- What an event *means* in gameplay terms (`gameplay-behavior-derivation`).
- REST endpoints (`api-contract-validation`).
- Redis key layout (`persistence-analysis`).

## Inputs

```text
The hub/event/client-handler change or proposal
Documentation Context (documentation-discovery output)
Expected-Behavior Record for the action (optional, from gameplay-behavior-derivation)
```

## Prerequisites & Required Context

- The events an action can emit and their order (`GAME_EVENTS.md`).
- The `Sequence` semantics of `BattleState` (`GAME_STATE.md`).
- Hub is thin and delegates to Application (`ARCHITECTURE.md` §2–§4).

## Authoritative Sources

```text
SIGNALR_PROTOCOL.md                connection, hub methods, event delivery, ack,
                                   sequencing, reconnect/resync, exclusions
GAME_EVENTS.md §1–§3               ordering within one resolution; event payload content; what is not defined
GAME_STATE.md §5                   Sequence semantics
GAME_RULES.md §16, §17, §18        event names, resolution order, authority
REDIS_STATE.md §4                  optimistic concurrency behind the ordering guarantee
ARCHITECTURE.md §2–§4              Hub → BattleResolutionService flow
TDD.md §2, §5                      client/server split; SignalR for in-battle traffic
ADR-004, ADR-008                   realtime transport; snapshot reconnection
```

## Procedure

1. **Enumerate** the hub methods, events, and client handlers in scope.
2. **Client → server requests.** Compare each to the documented set and its
   parameters. Confirm they are *requests* only (`GAME_RULES.md` §18), are
   validated by the owning domain document before state changes, and that a
   rejection returns to the caller only and emits no Battle Events (per the
   protocol document).
3. **Acknowledgement.** Confirm the direct result convention is followed and
   that the correlation id the client sends is treated as an opaque
   correlation value, not as the authoritative sequence.
4. **Event delivery.** Confirm all events from one resolved action are
   delivered together, in resolution order, to the battle's group, with the
   authoritative sequence value the protocol specifies.
5. **Order and content.** Check the emitted sequence against
   `GAME_EVENTS.md` §1: event names/ordering, conditional events (only emitted
   when their condition holds), repeated events across cascades. Payload
   *content* must match `GAME_EVENTS.md` §2; do not enforce a specific wire
   schema the documents leave open.
6. **Sequencing and concurrency.** Confirm strictly increasing sequence
   without gaps, single resolution at a time per battle, arrival-order
   processing, and that gap detection on the client is treated as desync
   (`SIGNALR_PROTOCOL.md` §5–§6).
7. **Reconnect / resync.** Confirm the snapshot approach: authoritative state
   fetched, local prediction discarded, no per-event replay, and the
   documented behavior when the battle no longer exists.
8. **Client reconstruction.** Confirm the client can reach a correct
   presentation state from the ordered events and, after reconnect, from the
   snapshot alone; client presentation state stays non-authoritative
   (`GAME_STATE.md` §4). Hand authority questions to
   `authority-determinism-audit`.
9. **Cross-document consistency.** Compare the protocol against the event and
   state documents (methods referenced in one document but not listed in the
   other, event names differing from `GAME_RULES.md` §16, sequence semantics).
   Mismatches → `documentation-consistency`.
10. **Layer check.** Hub stays thin; no game logic in Hub or client
    (`ARCHITECTURE.md` §2).

## Outputs

```text
Realtime Validation Report
- Hub methods: documented? parameters match? validated by owning rule?
- Event sequence for the action: expected (by reference) vs actual/proposed
- Delivery: grouping, atomicity per resolution, sequence handling
- Concurrency/ordering findings
- Reconnect/resync findings
- Client reconstruction findings
- Cross-document inconsistencies
- Defects (client authority, reordering, missing/extra events, logic in Hub)
- Recommended smallest correction / required decision
```

## Validation

```text
[ ] Every method/event/handler in scope matched to a document section or reported undocumented
[ ] Event order checked against GAME_EVENTS.md §1 and GAME_RULES.md §17
[ ] Sequence and single-writer guarantees explicitly checked
[ ] Reconnect path checked against the documented snapshot behavior
[ ] No event, field, method, or error reason was invented
```

## Stop Conditions

- A hub method, event, or ack reason is needed that is not documented.
- Events and protocol documents disagree on names, ordering, or sequence
  meaning (data-contract conflict).
- The client would compute or hold an authoritative value to make the
  protocol work (report as a defect — `authority-determinism-audit`).
- The change needs replay/event-log semantics or multi-client divergent
  state: not in MVP (`GAME_EVENTS.md` §3, `SIGNALR_PROTOCOL.md` §7,
  `ARCHITECTURE.md` §5).
- The change would replace the transport or the reconnection model (ADR-004
  / ADR-008 → architecture change).

Baseline stop conditions in `.ai/skills/README.md` §6 also apply.

## Common Failure Modes

- Sending events per-step instead of together per resolution.
- Reordering or omitting conditional events "for simplicity".
- Using the client correlation id as the authoritative sequence.
- Replaying missed individual events on reconnect instead of using the
  snapshot.
- Throwing on invalid requests instead of the documented rejection result.
- Client displaying its own predicted Damage/HP as final.
- Enforcing a wire schema the documents deliberately leave open.

## Traceability

```text
Used by:    development/feature.md (realtime), refactor.md (§2 events / realtime),
            bug-fix.md; quality/testing.md (realtime tests); quality/review.md
Reads:      SIGNALR_PROTOCOL.md; GAME_EVENTS.md; GAME_STATE.md;
            GAME_RULES.md §16–§18; REDIS_STATE.md §4; ARCHITECTURE.md; ADR-004/008
Produces:   Realtime Validation Report
Depends on: documentation-discovery (only if context not already supplied)
```
