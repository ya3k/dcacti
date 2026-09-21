# TASK-007A — Resolve ReceiveEvents Delivery Contract

---

## Metadata

```text
Task ID:           TASK-007A
Type:              CONTRACT RESOLUTION (documentation / test-contract)
Status:            DONE
Risk:              LOW
Priority:          HIGH
Primary Agent:     review
Supporting Agents: realtime, client, testing
Workflow:          architecture/architecture-change.md
                   documentation/documentation-change.md
Skills:            realtime-protocol-validation, documentation-consistency,
                   architecture-conformance, impact-analysis
Dependencies:      TASK-006 (Battle Event Emission, DONE) — the produced batch
                   TASK-007 (ReceiveEvents Delivery, BLOCKED) — the blocker report
Blocked:           TASK-007
```

---

## Objective

Resolve the two documented contract contradictions that blocked TASK-007 —
ReceiveEvents Delivery, so that TASK-007 can be implemented deterministically.
Contract resolution only: no delivery implementation, no production code, no
frontend runtime code, no gameplay behaviour.

---

## Discovery — What Was Read

```text
Root                  AGENTS.md (§2, §4, §10, §13, §15, §17, §20)
docs/                 docs/AGENTS.md (§2, §4)
docs/00-overview/     MVP_SCOPE.md
docs/01-game-design/  GAME_RULES.md (§16, §17, §18)
                      MATCH3_RULES.md (§2.1.5, §4.2, §6, §8.1–§8.3)
docs/02-technical/    SIGNALR_PROTOCOL.md (§0, §1, §2, §3, §3.1, §4, §4.1, §4.2,
                                           §5, §6, §7, §8 — in full)
                      GAME_EVENTS.md (§1, §1.1, §1.2, §1.3, §2, §3 — in full)
                      GAME_STATE.md (§0, §2.0, §2.1.7, §2.2, §5, §5.1, §5.2)
                      ARCHITECTURE.md (§1, §2.1, §2.2.1, §3, §4, §4.1, §5)
                      TDD.md (§0, §2.2, §3, §5, §6)
                      API_CONTRACTS.md (boundary with SignalR)
docs/03-decisions/    README.md (§1–§8), ADR-001, ADR-004, ADR-008
tasks/                tasks/README.md, TASK_LIFECYCLE.md
                      completed/TASK-006-battle-event-emission.md
                      blocked/TASK-007 (report referenced by the task)
Existing implementation / tests:
                      src/backend/GameServer.Api/Hubs/BattleHub.cs
                      tests/backend/GameServer.Api.Tests/ApiIntegrationTests.cs
                        → BattleHub_ShouldNotRegisterGameplayMethods
                      src/backend/GameServer.Domain/Match3/BattleEvent.cs
                      src/backend/GameServer.Domain/Match3/BattleEventBuilder.cs
                      src/backend/GameServer.Domain/Match3/SwapExecution.cs
                      src/frontend/client/src/game/runtime/GameRuntimeEvents.ts
                      src/frontend/client/src/game/runtime/GameRuntime.ts
                      src/frontend/client/src/services/realtime/SignalRService.ts
                      src/frontend/client/tests/RuntimeBoundaries.test.ts
                      src/frontend/client/tests/GameRuntime.test.ts
                      tests/backend/GameServer.Domain.Tests/BattleEventEmissionTests.cs
```

---

## Executive Summary

**Both contradictions are test defects, not protocol defects. Neither
authoritative document is ambiguous, and neither required an edit.**

1. **Decision 1.** `SIGNALR_PROTOCOL.md` §2 is titled *"Client → Server (Hub
   Methods)"*; §3 is titled *"Server → Client (Event Delivery)"*. `ReceiveEvents`
   appears **only** under §3. Directions are separated by section, so the
   protocol is unambiguous and no precedence rule is even needed.
   `BattleHub_ShouldNotRegisterGameplayMethods` asserts that a client
   `InvokeAsync("ReceiveEvents")` fails — which is **correct and must keep
   passing**, because `ReceiveEvents` is genuinely not a client-invokable hub
   method. The defect is not the assertion but the **enumeration**: the test
   groups `ReceiveEvents` with §2/§7 client-invokable methods, which mislabels a
   server → client method as a client → server one. The test is valid in effect
   and wrong in classification and commentary.

2. **Decision 2.** `GAME_EVENTS.md` §3 item 1 assigns the wire envelope and JSON
   schema of `ReceiveEvents` to `SIGNALR_PROTOCOL.md`; §3 item 3 makes client-side
   handling of each event a *"client implementation detail, not a documentation
   concern"*; and `GAME_EVENTS.md` §2 owns event payload **content**, not a
   frontend type model. `ARCHITECTURE.md` §2.2.1 rule 4 states `GameRuntime`
   *"forwards `ReceiveEvents` batches exactly as the server sent them. It does
   not reorder …, filter, or interpret them."* Combined with §2.1 (Api is the
   *thin* boundary that translates wire messages *"and back into wire
   messages"*) the documented answer is **Option A** — the runtime stays opaque.
   `RuntimeBoundaries.test.ts`'s no-event-names rule is therefore **valid** and
   `GameRuntimeEvents.ts`'s `readonly unknown[]` is **correct as written**; both
   stay unchanged.

No document was edited: `AGENTS.md` §4 and the task's own instruction both forbid
restating an already-unambiguous rule. No ADR was created: `docs/03-decisions/
README.md` §2 excludes an ADR that duplicates content a technical document
already fully owns, and §1/§5 require an ADR to record a decision *already
established* — both decisions here were already established.

---

## Required Investigation — Findings

### 3. Which document owns each contract

```text
Directionality (Decision 1)
    Owner          SIGNALR_PROTOCOL.md §2, §3        (this document owns how
                                                      realtime communication works)
    Corroborated   ADR-004 §Decision: "client-to-server Hub method calls
                   (Swap, CardCast, PetSkillCast) and server-to-client event
                   delivery (ReceiveEvents)" — the URL is /hubs/battle (§1.2)
    Precedence     AGENTS.md §2: technical docs > ADR; both agree, so no
                   precedence question arises.
    NOT the owner  Code, and not a test. AGENTS.md §2 places Task > Code and
                   puts tests last in the hierarchy.

Event payload boundary (Decision 2)
    Owner          GAME_EVENTS.md §2        → payload CONTENT of each event
                   GAME_EVENTS.md §3 item 1 → the wire-level envelope is
                                             SIGNALR_PROTOCOL.md's
                   GAME_EVENTS.md §3 item 3 → client-side handling of each
                                             event is a client implementation
                                             detail, not a documentation concern
                   SIGNALR_PROTOCOL.md §3   → the batch's delivery contract
                   ARCHITECTURE.md §2.2.1 rule 4 → what GameRuntime may do with
                                             a forwarded batch
    Precedence     AGENTS.md §2 (technical docs) > ADR > Task > Code > Tests.
```

### 4. Is the existing test green but wrong?

```text
BattleHub_ShouldNotRegisterGameplayMethods
    ASSERTION      Invoking "ReceiveEvents" as a hub method throws.
    VERDICT        CORRECT — ReceiveEvents is not a client → server hub method
                   (SIGNALR_PROTOCOL.md §2 lists exactly Swap, CardCast,
                   PetSkillCast, plus the non-gameplay JoinBattle).
    DEFECT         ENUMERATION + COMMENT. The list {CardCast, PetSkillCast,
                   GetBattleState, ReceiveEvents} is described as "these
                   in-battle methods belong to SIGNALR_PROTOCOL.md §2/§7".
                   §2 lists no ReceiveEvents, and §3 is a different direction.
                   The test therefore cannot be fixed by deleting the entry
                   without losing a real (accidental) guarantee.

RuntimeBoundaries.test.ts — 'the runtime does not invent battle event names'
    ASSERTION      GameRuntimeEvents.ts must not contain the 20 §2 event names.
    VERDICT        CORRECT and CONSISTENT with ARCHITECTURE.md §2.2.1 rule 4
                   and GAME_EVENTS.md §3 item 3. No change required.
    SCOPE NOTE     Its comment says "the runtime must not enumerate them",
                   which is accurate; it does not forbid a *transport* module
                   from doing so, and none exists.
```

---

## Decision 1 — ReceiveEvents Directionality

### Authoritative document

**`SIGNALR_PROTOCOL.md` §3 (*"Server → Client (Event Delivery)"*)**, corroborated
by **ADR-004 §Decision**, which names `ReceiveEvents` as *"server-to-client event
delivery"* while naming `Swap`/`CardCast`/`PetSkillCast` as *"client-to-server Hub
method calls"*. `docs/03-decisions/README.md` §3 confirms an ADR may not override
a technical document — here the two agree, so the ADR merely corroborates.

The distinction the task asks for **already exists structurally in the document**:

```text
SIGNALR_PROTOCOL.md §2   "Client → Server (Hub Methods)"
        Swap, CardCast, PetSkillCast        +  JoinBattle (§1.2, non-gameplay)

SIGNALR_PROTOCOL.md §3   "Server → Client (Event Delivery)"
        ReceiveEvents(battleId, serverSequence, events[])

SIGNALR_PROTOCOL.md §7   "Reconnect & Resync"
        GetBattleState(battleId)            — a client → server request/response
                                              method, not a broadcast
```

`BattleHub` already reflects exactly this: `Swap` and `JoinBattle` are public hub
methods; `ReceiveEvents` and `BattleStateUpdated` appear only as
`Clients.Group(...).SendAsync(...)` / `Clients.Caller.SendAsync(...)` targets
(`BattleHub.cs` lines 305, 386) — i.e. **server → client client-method
invocations**, never `[HubMethodName]`-invokable server methods.

### Interpretation

```text
Client → Server invocation        A hub method the client may InvokeAsync.
                                  §2: Swap, CardCast, PetSkillCast.
                                  §1.2: JoinBattle (non-gameplay group join).
                                  §7: GetBattleState (reconnect/resync).
                                  → These are the ONLY names a client may invoke.

Server → Client client invocation A *client-side* handler the server calls via
                                  Clients.Group(...).SendAsync(...).
                                  §3: ReceiveEvents.
                                  §4: BattleStateUpdated.
                                  → These are NOT hub methods. A client that
                                    InvokeAsync's them is misusing the protocol,
                                    and the server MUST NOT implement them as
                                    invokable methods.
```

`ReceiveEvents` is **exclusively** the second kind. Its meaning is unchanged: it
is still one atomic batch per resolved action, carrying no battle state
(§3.1 item 3, §4 item 6). Nothing about the decision changes what `ReceiveEvents`
means — STOP condition 7 does **not** fire.

### Is the existing Hub regression test valid or contradictory?

```text
VALID IN EFFECT, DEFECTIVE IN CLASSIFICATION.
```

- The **assertion** (`InvokeAsync("ReceiveEvents")` must throw) is **correct** and
  must keep passing: §2's method list does not contain `ReceiveEvents`, and the
  debug-friendly reading is that a client invoking a §3 server → client method is
  a protocol misuse the server is right to refuse.
- The **enumeration and comment** are **wrong**: the test claims all four names
  "belong to SIGNALR_PROTOCOL.md §2/§7". `ReceiveEvents` belongs to **§3**, and
  §3 is the opposite direction. The test conflates "the client may not invoke
  this" with "this is a §2/§7 client → server method" — two different claims that
  happen to share an assertion.
- Consequence: the test accidentally guards the correct property while stating
  the wrong reason. It is not evidence that the protocol is wrong, and it is not
  a reason to change the protocol.

### Exact contract TASK-007 should implement

```text
1. ReceiveEvents(battleId, serverSequence, events[]) is Server → Client ONLY.
   BattleHub must NOT expose it as an invokable hub method, and adding one would
   be a protocol violation (SIGNALR_PROTOCOL.md §2 lists three gameplay methods
   and nothing else).

2. Delivery is Clients.Group(battleId).SendAsync("ReceiveEvents", payload) — the
   same group-broadcast shape JoinBattle/Swap already use for
   "BattleStateUpdated" (§3.3, §4.3).

3. The payload is exactly the three documented members: battleId,
   serverSequence, events[]. serverSequence is BattleState.Sequence AFTER this
   resolution (§3.2, GAME_STATE.md §5) — strictly increasing, no gaps, one value
   per resolved action however many Matches/Cascades/Special Gems it contained.

4. It is sent strictly AFTER the single BattleState write-back (§3.1 item 1,
   GAME_STATE.md §5.1 item 3), so a client reacting by requesting state (§7)
   never sees the pre-resolution state.

5. One batch per resolved action, atomic at the message level (§3.1): no
   per-step message, no partial batch, no "gravity" message.

6. A rejected action sends nothing: no batch and no serverSequence change
   (§3.1 item 4, GAME_EVENTS.md §1.2). A rejected Swap returns the §5 direct
   acknowledgement only.

7. The batch is the ONLY gameplay message for that action; board and counters
   continue to travel in the existing BattleStateUpdated push (§3.1 item 3,
   §4 item 6). §8 item 7 forbids any MatchCreated-style *method*,
   BoardResolved, CascadeUpdated, or SpecialGemActivated message.
```

---

## Decision 2 — Event Payload Boundary

### Authoritative document

```text
GAME_EVENTS.md §3 item 1   "Wire-level message envelope (method name, JSON shape
                            sent over SignalR): see SIGNALR_PROTOCOL.md."
GAME_EVENTS.md §3 item 3   "Client-side handling of each event: client
                            implementation detail, not a documentation concern."
GAME_EVENTS.md §2          Owns each event's payload CONTENT (purpose, trigger,
                            key payload fields, ordering).
ARCHITECTURE.md §2.2.1
        rule 4             "Battle events pass through unchanged. GameRuntime
                            forwards ReceiveEvents batches exactly as the server
                            sent them. It does not reorder …, filter, or
                            interpret them."
ARCHITECTURE.md §2.1 item 4 Api is "the thin composition root: SignalR Hub methods
                            and REST controllers translate wire messages into
                            Application use-case calls and back into wire
                            messages."
```

### Frontend events: opaque or typed?

```text
OPAQUE. Option A.
```

The runtime is a **transport** boundary, not a deserializer. Three documented
facts settle it without any architectural preference being exercised (STOP
condition 9 does **not** fire):

1. **The runtime is forbidden to interpret.** `ARCHITECTURE.md` §2.2.1 rule 4
   names `ReceiveEvents` explicitly and forbids `GameRuntime` to reorder, filter,
   or interpret a batch. A deserializer that maps wire JSON onto typed payloads
   *is* interpretation — it makes the runtime a party to `GAME_EVENTS.md` §2's
   payload definitions.
2. **Client-side event handling is not a documentation concern at all.**
   `GAME_EVENTS.md` §3 item 3 explicitly declines to define it, and §3 item 1
   delegates the envelope and JSON shape to `SIGNALR_PROTOCOL.md` — which itself
   delegates the *payload content* back to `GAME_EVENTS.md` §2 (`SIGNALR_PROTOCOL.md`
   §8 item 1: *"payload content is defined in `GAME_EVENTS.md` §2; exact wire
   schema is an implementation detail"*). No document requires the frontend to
   model the four Match-3 payloads, and none authorises duplicating §2 into the
   client.
3. **The client is non-authoritative and derives nothing.** `GAME_RULES.md` §18,
   ADR-001, and `SIGNALR_PROTOCOL.md` §3.1 item 5 require the client to apply the
   batch in order and render from the state it holds — it does not compute
   Matches, Cascades, Combo, Special Gems, gravity, or spawns. Typed event
   projection would create a second, non-authoritative spelling of §2's payloads
   in the client, which is exactly the "parallel representation" shape
   `GAME_STATE.md` §0 item 5 forbids for state.

`GAME_EVENTS.md` §2 remains fully authoritative for payload **content** — it is
simply consumed by the *transport/serialization* side (the backend that builds
`BattleEvent` into the §3 batch) and by whatever presentation code later chooses
to read `events[]`, not by `GameRuntime`.

### Exact responsibility of transport vs runtime

```text
TRANSPORT DESERIALIZATION                          GAMEPLAY INTERPRETATION
-------------------------------------------------  ---------------------------------
Owner: the SignalR client + the envelope reader    Owner: NONE in the runtime.
       (SignalRService.on / GameRuntime's          GAME_EVENTS.md §3 item 3 leaves
       readBattleEventsEnvelope).                  it to a later presentation stage.
Scope: SignalR's own JSON decoding; the             Scope: what a MatchCreated /
       three envelope members battleId,            ComboChanged / GemMatched MEANS,
       serverSequence, events.                     and any board/combo/state change
                                                   such an event implies.
Rule:  shape only, exactly as GameRuntime           Rule:  GAME_RULES.md §18,
       already does for BattleStateUpdated         ADR-001, ARCHITECTURE.md §2.2.1
       (readBattleState checks shape, not          rule 4 — never in GameRuntime; the
       gameplay meaning).                          client renders the state it holds.
Rule:  events stays readonly unknown[].            Rule:  no event name may enter
                                                   GameRuntimeEvents.ts.
```

**Do not conflate them.** Validating that a batch *has* an `events` array is
transport shape-checking (already implemented, and `GameRuntimeEvents.ts` /
`GameRuntime.ts` already document it as such). Reading `event.type ===
'ComboChanged'` would be gameplay interpretation and is forbidden in the runtime.

### Exact contract TASK-007 should implement

```text
1. The frontend contract is UNCHANGED: BattleEventsEnvelope stays
   { battleId: string; serverSequence: number; events: readonly unknown[] }.
   TASK-007 must not introduce an event-type union, a discriminated payload, or
   per-event interfaces in the frontend.

2. The runtime surface is UNCHANGED: onBattleEvents(listener) receiving the
   envelope, forwarded in order, with no filtering, reordering, or synthesis.

3. The server is the only side that knows event kinds. GAME_EVENTS.md §2's
   payloads are projected onto the §3 batch by the backend that already builds
   BattleEvent (TASK-006's SwapExecutionResult.Events), and the exact wire schema
   stays an implementation detail (GAME_EVENTS.md §3 item 1, SIGNALR_PROTOCOL.md
   §8 item 1).

4. The frontend must NOT be changed by TASK-007. The existing
   src/frontend/client/src/game/runtime/GameRuntimeEvents.ts,
   GameRuntime.ts and SignalRService.ts already implement the receiving half of
   §3/§4 correctly; the missing half is the server SEND.
```

---

## Documentation Changes

```text
None.
```

**Reason.** `AGENTS.md` §4 and the task's own Critical Rule both require the
smallest correction and forbid restating an already-unambiguous rule.
`SIGNALR_PROTOCOL.md` §2/§3 already separate the two directions by section title
and by method list; §3 already documents the batch, its atomicity, its ordering,
and its post-write-back timing; `GAME_EVENTS.md` §3 items 1 and 3 already assign
the envelope to `SIGNALR_PROTOCOL.md` and client handling to the client; and
`ARCHITECTURE.md` §2.2.1 rule 4 already forbids the runtime to interpret a batch.
Every fact TASK-007 needs is already stated in an owning document. Editing any of
them would be re-statement, not correction, and `docs/03-decisions/README.md` §3
plus `AGENTS.md` §17 both warn against editing documents to match code.

**Specifically considered and rejected:**

```text
SIGNALR_PROTOCOL.md §3       already titled "Server → Client (Event Delivery)"
                             and §2 titled "Client → Server (Hub Methods)".
                             The direction is stated; no new item required.
SIGNALR_PROTOCOL.md §8       adding "ReceiveEvents is not a client-invokable
                             method" would restate §2's closed method list.
GAME_EVENTS.md §3 item 1/3   already delegate envelope → protocol, client
                             handling → client. Nothing to add.
ARCHITECTURE.md §2.2.1       rule 4 already covers ReceiveEvents by name.
docs/03-decisions/README.md  §8 Known Open Items — neither decision is an open
                             item; both are established.
```

---

## ADR

```text
NOT CREATED
```

**Reason.** `docs/03-decisions/README.md` governs this:

- §1 — an ADR records a decision **already made** and reflected in existing
  documentation. Both decisions here are already made and already reflected:
  `SIGNALR_PROTOCOL.md` §2/§3 for directionality and `ARCHITECTURE.md` §2.2.1
  rule 4 with `GAME_EVENTS.md` §3 for the opaque-runtime boundary.
- §2 — do **not** create an ADR for a decision *"that duplicates content already
  fully owned by a technical document."* That is precisely what both would be.
- §5 — `Accepted` is only used when the decision is genuinely established in
  `docs/00-overview/`, `docs/01-game-design/`, or `docs/02-technical/`. It is.
- §3 — an ADR must never override a technical document, so an ADR here could not
  add authority the technical docs lack.

`AGENTS.md` §18 requires an ADR when implementation changes architecture,
realtime strategy, the battle-state model, the authoritative model, module
boundaries, or infrastructure. **TASK-007A changed none of these, and TASK-007
will not either**: it adds one `SendAsync` on the already-documented §3 path
inside the already-existing hub, using `ARCHITECTURE.md` §4.1's documented
`BattleHub → ReceiveEvents / state push` arrow. No new message type, no new
module boundary, no state-model change.

**This also means the task's "No implementation" constraint is satisfied with no
production file touched.**

---

## Tests

### What must change

```text
ONE test contract requires a comment-and-enumeration correction:
    tests/backend/GameServer.Api.Tests/ApiIntegrationTests.cs
        BattleHub_ShouldNotRegisterGameplayMethods
```

**This is a TASK-007 prerequisite, not a TASK-007A change** (the task explicitly
says: *"Do not implement the test change yet unless this task's
contract-resolution process explicitly requires updating the test."* It does not
— the assertion already holds and the resolution is complete without editing it).

The correction is **comment-and-enumeration only**, and it is **strictly
stronger**, never weaker:

```text
KEEP     The assertion block for "CardCast", "PetSkillCast" — §2 methods that
         genuinely are not implemented.
KEEP     The assertion block for "GetBattleState" — §7's client → server
         reconnect/resync method, also not implemented.
KEEP     The assertion block for "ReceiveEvents" — it must still throw, because
         §2's method list does not contain it. Deleting this would WEAKEN the
         test and is explicitly forbidden by the task and AGENTS.md §15.
CHANGE   The comment/grouping so the list no longer claims all four "belong to
         SIGNALR_PROTOCOL.md §2/§7". The correct classification is:
             §2 not implemented : CardCast, PetSkillCast
             §7 not implemented : GetBattleState
             §3 wrong direction : ReceiveEvents — a Server → Client client-method
                                  invocation (§3), which no client may invoke as
                                  a hub method.
```

**Do not weaken.** The resolution does not permit removing `ReceiveEvents` from
the list, nor relaxing the assertion to `ThrowsAnyAsync<Exception>` differently,
nor skipping the test. The directionality property it accidentally guards —
*the server exposes no invokable `ReceiveEvents` method* — is exactly the
property TASK-007 depends on, and must survive.

### What must NOT change

```text
src/frontend/client/tests/RuntimeBoundaries.test.ts
    'the runtime does not invent battle event names'   UNCHANGED — valid.
    The 20-name list is exactly GAME_RULES.md §16's canonical list, and
    forbidding those names in GameRuntimeEvents.ts is the boundary that follows
    from ARCHITECTURE.md §2.2.1 rule 4 and GAME_EVENTS.md §3 item 3.
    ⚠️ TASK-007 MUST NOT modify this test to make a typed frontend model fit.

src/frontend/client/tests/GameRuntime.test.ts
    transport.subscriptions.size === 2 (ReceiveEvents + BattleStateUpdated)
                                                        UNCHANGED — valid, and it
    already asserts the runtime subscribes to ReceiveEvents (§3).
    'forwards server-authoritative event batches unchanged'  UNCHANGED — this is
    ARCHITECTURE.md §2.2.1 rule 4 in test form.
    'does not reorder events'                           UNCHANGED.

tests/backend/GameServer.Domain.Tests/BattleEventEmissionTests.cs
                                                        UNCHANGED — TASK-006's
    emission contract is the input to TASK-007, not its subject.

src/frontend/client/src/game/runtime/GameRuntimeEvents.ts
src/frontend/client/src/game/runtime/GameRuntime.ts
src/frontend/client/src/services/realtime/SignalRService.ts
                                                        UNCHANGED — they already
    implement the receiving half of §3/§4. `readonly events: readonly unknown[]`
    is the correct and documented shape.
```

### New tests TASK-007 should add (not this task)

```text
A hub/transport test that asserts the §3 send actually happens, and the §2/§7
negative property is preserved:
    - an accepted Swap produces exactly one "ReceiveEvents" client call on the
      battle group, carrying battleId, serverSequence = BattleState.Sequence,
      and events[] in the documented order;
    - the batch arrives after the write-back, i.e. a state request triggered by
      the batch sees the post-resolution state (§3.1 item 1);
    - serverSequence advances by exactly 1 per committed action (§3.2);
    - a rejected Swap produces no "ReceiveEvents" call at all (§3.1 item 4);
    - the batch is atomic: the client never receives a partial batch (§3.1);
    - no MatchCreated-style method / BoardResolved / CascadeUpdated /
      SpecialGemActivated message is introduced (§8 item 7).
```

---

## STOP Conditions

**None fired.** Each was checked against the authoritative documents:

```text
1  Two authoritative documents define incompatible semantics with no precedence.
   NO — the two apparent sources are a TEST and a PROTOCOL. AGENTS.md §2 gives
   the precedence outright (technical docs > Task > Code > Tests), and the
   protocol is unopposed by any document.

2  Resolving requires inventing a new gameplay rule.            NO — no rule added.
3  Resolving requires changing BattleState semantics.           NO — no state change.
4  Resolving requires inventing a new event type or payload.    NO — §2's payloads
   are untouched; the frontend gains no new type.
5  The ownership of the frontend event boundary cannot be uniquely determined.
   NO — ARCHITECTURE.md §2.2.1 rule 4 names ReceiveEvents and forbids
   interpretation; GAME_EVENTS.md §3 items 1 and 3 delegate the envelope to the
   protocol and client handling to the client. Unique.
6  The SignalR directionality cannot be uniquely determined.     NO — §2 and §3
   are titled by direction and §2's method list is closed.
7  The decision requires changing the meaning of ReceiveEvents.  NO — its meaning
   (one atomic batch per resolved action, carrying no state) is unchanged.
8  The existing test and protocol are both explicitly marked authoritative with no
   precedence rule.  NO — the test is not an authoritative document at all
   (AGENTS.md §2), and AGENTS.md §2 supplies the rule regardless.
9  The decision requires choosing an architectural preference rather than deriving
   from the docs.  NO — the docs state both answers.
10 Any implementation would be required to prove the contract first.
   NO — the contract is documentary; no code had to run to establish it.
```

**Additional contradiction check.** The task's Critical Rule requires stopping if
*another* contradiction is found. None was found in scope. `BattleHub`'s class
comment already calls `ReceiveEvents` a "remaining in-battle hub method … NOT
implemented" alongside `GetBattleState` (SIGNALR_PROTOCOL.md §2, §3, §7) — the
same classification imprecision as the test, in a code comment. It is a comment,
not a contract, it asserts nothing, and it is in the file TASK-007 will touch;
recorded below as a Remaining Issue rather than silently resolved here.

---

## Acceptance Criteria

```text
[✓] Decision 1 resolved with its authoritative document named
[✓] Decision 1 states whether the Hub regression test is valid or contradictory
[✓] Decision 1 gives the exact contract TASK-007 must implement
[✓] Decision 2 resolved with its authoritative document named
[✓] Decision 2 states opaque vs typed, and answers Option A/B explicitly
[✓] Transport deserialization and gameplay interpretation are separated
[✓] Decision 2 gives the exact contract TASK-007 must implement
[✓] Documentation changes listed (None, with reason)
[✓] ADR decision stated (NOT CREATED, with reason per README.md §2/§5)
[✓] Test contracts that must change are named
[✓] No test is weakened, skipped, or deleted
[✓] No production code written; no frontend runtime code written
[✓] No gameplay behaviour added or modified
[✓] STOP conditions checked and reported
[✓] TASK-007 readiness concluded
```

---

## Remaining Issues

Recorded, not fixed (`AGENTS.md` §16):

```text
1. BattleHub.cs class comment (lines ~219–222) lists `ReceiveEvents` among the
   "remaining in-battle hub methods (CardCast, PetSkillCast, ReceiveEvents,
   GetBattleState — SIGNALR_PROTOCOL.md §2, §3, §7)" that are "intentionally NOT
   implemented". The § references are correct, but grouping §3's Server → Client
   delivery with §2/§7 client → server methods repeats the classification
   imprecision this task resolved for the test. It is a comment with no
   behavioural effect and asserts nothing.
   Impact:  documentation/comment accuracy only; no contract depends on it.
   Suggested follow-up: correct the comment while implementing TASK-007, in the
   same edit as the `SendAsync("ReceiveEvents", …)` addition.

2. TASK-007's blocker report was not present in `tasks/blocked/` when this task
   ran — the directory contains only `.gitkeep`, and no TASK-007 file exists
   anywhere under `tasks/`. This task therefore worked from the two blockers as
   stated in TASK-007A's own Context section, plus a fresh verification of the
   four cited sources (BattleHub, BattleHub_ShouldNotRegisterGameplayMethods,
   GameRuntimeEvents.ts, RuntimeBoundaries.test.ts). Both stated blockers were
   confirmed to exist exactly as described, so the resolution stands either way.
   Impact:  none on the decisions; the two contradictions are real and were
   independently reproduced.
   Suggested follow-up: none required, unless TASK-007's report contains further
   findings not restated in TASK-007A's Context.

3. MatchResolved / action-boundary events (TurnStarted, TurnEnded, SwapStarted,
   SwapResolved) remain unemitted (TASK-006 Risks). Unchanged by this task and
   not a TASK-007 blocker: TASK-007 delivers whatever batch
   SwapExecutionResult.Events carries.
```

---

## Build / Verification

```text
No file under src/ or tests/ was modified by this task.
No build or test run was required: no code, test, or document was changed.
```

---

## Final Report

```text
TASK-007A — Resolve ReceiveEvents Delivery Contract

Status:
DONE

Decision 1 — ReceiveEvents Directionality

Authoritative document: SIGNALR_PROTOCOL.md §3 ("Server → Client (Event
Delivery)"), corroborated by ADR-004 §Decision and by §2's title ("Client →
Server (Hub Methods)") plus its closed method list {Swap, CardCast,
PetSkillCast}, with §1.2's JoinBattle and §7's GetBattleState as the other
client → server calls.

Interpretation: the two directions are already distinct in the protocol. A
client → server invocation is a hub method the client may InvokeAsync (§2, §1.2,
§7). A server → client invocation is a client-side handler the server calls
through Clients.Group(...).SendAsync(...) (§3 ReceiveEvents, §4
BattleStateUpdated). ReceiveEvents is exclusively the second kind and is not a
hub method.

Is the existing Hub regression test valid or contradictory? VALID IN EFFECT,
DEFECTIVE IN CLASSIFICATION. Its assertion — InvokeAsync("ReceiveEvents") must
throw — is correct and must keep passing, because §2's method list does not
contain ReceiveEvents. Its enumeration and comment are wrong: they claim all
four names belong to §2/§7, whereas ReceiveEvents belongs to §3, the opposite
direction. The test conflates "the client may not invoke this" with "this is a
§2/§7 method".

Exact contract TASK-007 implements: ReceiveEvents is Server → Client only and
must NOT be exposed as an invokable hub method; delivery is
Clients.Group(battleId).SendAsync("ReceiveEvents", payload) with exactly
{battleId, serverSequence, events[]}; serverSequence is BattleState.Sequence
after this resolution, strictly increasing with no gaps; it is sent strictly
after the single write-back; one atomic batch per resolved action with no
per-step message and no partial batch; a rejected action sends nothing; and the
batch is the only gameplay message for that action, with board and counters
continuing to travel in the existing BattleStateUpdated push.

Decision 2 — Event Payload Boundary

Authoritative document: ARCHITECTURE.md §2.2.1 rule 4 (GameRuntime forwards
ReceiveEvents batches exactly as the server sent them and does not reorder,
filter, or interpret them), with GAME_EVENTS.md §3 item 1 (the wire envelope is
SIGNALR_PROTOCOL.md's), §3 item 3 (client-side handling of each event is a client
implementation detail, not a documentation concern), and §2 (payload content).

Opaque or typed? OPAQUE — Option A. The runtime is a transport boundary. A
deserializer that maps wire JSON onto typed payloads is interpretation, which
rule 4 forbids by name; GAME_EVENTS.md §3 item 3 declines to define client
handling at all; and GAME_RULES.md §18 / ADR-001 require the client to apply the
batch in order and render the state it holds without deriving Matches, Cascades,
Combo, Special Gems, gravity, or spawns. GAME_EVENTS.md §2 stays authoritative
for payload content, consumed by the transport/serialization side and by later
presentation code — not by GameRuntime. No new event schema is invented and §2
is not duplicated into the frontend.

Transport vs runtime responsibility: TRANSPORT DESERIALIZATION is SignalR's own
JSON decoding plus the envelope shape check for battleId / serverSequence /
events — shape only, exactly as GameRuntime already checks BattleStateUpdated's
shape, with events remaining readonly unknown[]. GAMEPLAY INTERPRETATION belongs
to no runtime component at all: GAME_EVENTS.md §3 item 3 leaves it to a later
presentation stage, and GAME_RULES.md §18 with ARCHITECTURE.md §2.2.1 rule 4
forbid it in GameRuntime. The two are not conflated: checking that a batch has an
events array is transport; reading event.type === 'ComboChanged' is gameplay
interpretation and is forbidden in the runtime.

Exact contract TASK-007 implements: the frontend contract is unchanged —
BattleEventsEnvelope stays { battleId, serverSequence, events: readonly
unknown[] }, onBattleEvents keeps receiving the envelope forwarded in order with
no filtering, reordering, or synthesis, and TASK-007 must NOT touch
GameRuntimeEvents.ts, GameRuntime.ts, or SignalRService.ts. The server is the
only side that knows event kinds, projecting §2's payloads onto the §3 batch from
the BattleEvent list TASK-006 already produces, with the exact wire schema
remaining an implementation detail.

Documentation Changes:
None. Every fact TASK-007 needs is already stated in an owning document —
SIGNALR_PROTOCOL.md §2/§3 for directionality, §3/§3.1 for the batch, its
atomicity, and its post-write-back timing, GAME_EVENTS.md §3 items 1 and 3 for
the envelope and client handling, and ARCHITECTURE.md §2.2.1 rule 4 for the
opaque runtime. AGENTS.md §4 and this task's Critical Rule both forbid restating
an already-unambiguous rule, and AGENTS.md §17 forbids editing documents to match
code. Specifically considered and rejected: adding a "not client-invokable" note
to SIGNALR_PROTOCOL.md §3 or §8 (restates §2's closed list), adding envelope
detail to GAME_EVENTS.md §3 (already delegated), and adding a runtime note to
ARCHITECTURE.md §2.2.1 (rule 4 already covers ReceiveEvents by name).

ADR:
NOT CREATED. docs/03-decisions/README.md §2 excludes an ADR for a decision that
duplicates content a technical document already fully owns, and §1 requires an
ADR to record a decision already established in the documentation — both
decisions are. §5 would permit Accepted only for a decision genuinely established
in docs/00-overview, docs/01-game-design, or docs/02-technical; both are. §3
confirms an ADR could not add authority the technical documents lack. AGENTS.md
§18 requires an ADR when architecture, realtime strategy, the battle-state model,
the authoritative model, module boundaries, or infrastructure change — TASK-007A
changed none, and TASK-007 adds only one SendAsync on the already-documented §3
path inside the existing hub, along ARCHITECTURE.md §4.1's documented
BattleHub → ReceiveEvents / state push arrow.

Tests:
One test contract requires a comment-and-enumeration correction, and it is a
TASK-007 prerequisite rather than a TASK-007A change:
tests/backend/GameServer.Api.Tests/ApiIntegrationTests.cs →
BattleHub_ShouldNotRegisterGameplayMethods. KEEP all four assertion blocks,
including "ReceiveEvents" — it must still throw, because §2's method list does
not contain it, and the property it guards (the server exposes no invokable
ReceiveEvents method) is exactly what TASK-007 depends on. CHANGE only the
comment and grouping so the list stops claiming all four belong to §2/§7:
CardCast and PetSkillCast are §2 methods not implemented, GetBattleState is §7's
client → server method not implemented, and ReceiveEvents is §3's Server → Client
method — the wrong direction for a client invocation. Strictly stronger, never
weaker: no assertion removed, relaxed, skipped, or deleted.
Must NOT change: RuntimeBoundaries.test.ts's no-event-names rule (valid under
ARCHITECTURE.md §2.2.1 rule 4 — and TASK-007 must not modify it to fit a typed
frontend model), GameRuntime.test.ts's subscription and forwarding contracts,
BattleEventEmissionTests.cs, and the three frontend runtime/service files.
New tests belong to TASK-007: the §3 send on an accepted Swap, the post-write-back
ordering, the +1-per-action serverSequence, no send on a rejection, batch
atomicity, and the absence of any MatchCreated-style / BoardResolved /
CascadeUpdated / SpecialGemActivated message.

STOP Conditions:
None fired. Directionality is determined by §2/§3's own structure. The frontend
boundary is uniquely determined by ARCHITECTURE.md §2.2.1 rule 4 with
GAME_EVENTS.md §3 items 1 and 3. No gameplay rule, BattleState semantic, event
type, or payload was invented, and ReceiveEvents' meaning is unchanged. The
apparent conflict was between a test and a protocol, and AGENTS.md §2 supplies
the precedence rule outright, so no condition 8 case arises. No code had to run
to establish the contract. One out-of-scope imprecision was found and recorded
rather than silently resolved: BattleHub's class comment groups §3's
ReceiveEvents with §2/§7 — a comment with no behavioural effect.

Implementation Readiness:
TASK-007 UNBLOCKED
```

---

## Implementation Boundary for TASK-007 (≤10 bullets)

- **Direction.** Add `ReceiveEvents` as a Server → Client send only: `await Clients.Group(battleId).SendAsync("ReceiveEvents", payload)` in `BattleHub.Swap`, on the accepted path only. Do **not** add an invokable hub method, and do not touch §2's method list.
- **Payload.** Send exactly three members — `battleId`, `serverSequence` (= the resolved `BattleState.Sequence`), `events[]` (the ordered list already on `SwapExecutionResult.Events`). No fourth member, no state, no `Status`.
- **Ordering.** Send the batch **after** the single write-back and after/alongside the existing `BattleStateUpdated` push, so a client reacting with §7 sees post-resolution state (`SIGNALR_PROTOCOL.md` §3.1 item 1, `GAME_STATE.md` §5.1 item 3).
- **Atomicity.** One `ReceiveEvents` call per resolved action, carrying the whole batch. No per-step, per-pass, gravity, or partial message; no second subscription.
- **Rejection.** A rejected Swap (and an unknown battle) sends nothing and returns only the §5 acknowledgement — no batch, no `serverSequence` change (`§3.1` item 4, `GAME_EVENTS.md` §1.2).
- **No new message.** Do not introduce any `MatchCreated`-style method, `BoardResolved`, `CascadeUpdated`, `SpecialGemActivated`, or parallel state-sync message (`§8` item 7, `§4` item 11); board and counters keep travelling in `BattleStateUpdated`.
- **Frontend untouched.** Change no file under `src/frontend/client/src/` and no frontend test. `events` stays `readonly unknown[]`; do not add event types, unions, discriminants, or per-event interfaces.
- **Test correction (prerequisite, in the same task).** Rewrite only the comment/grouping of `BattleHub_ShouldNotRegisterGameplayMethods` so `ReceiveEvents` is classified as §3 (wrong direction) rather than §2/§7 — keeping **all four** assertions, including the one that `InvokeAsync("ReceiveEvents")` throws.
- **Comment fix.** While editing `BattleHub.cs`, correct its class comment that groups `ReceiveEvents` with the §2/§7 unimplemented methods (Remaining Issue 1).
- **New tests.** Add hub tests for: exactly one `ReceiveEvents` call per accepted Swap with the documented three members; `serverSequence` equal to the resolved `Sequence` and advancing by exactly 1 per committed action; no call on a rejection; batch atomicity; and no board-resolution-specific message.

---

## Status

DONE

---

## Handoff

TASK-007 may proceed with no further contract decision. The two contradictions
were both test/comment defects against an unambiguous protocol, so no document
was edited and no ADR was created. The only supporting change TASK-007 must carry
is the comment-and-enumeration correction to
`BattleHub_ShouldNotRegisterGameplayMethods` — assertion-preserving and strictly
stronger — plus the `BattleHub.cs` class-comment fix in the same edit as the
`SendAsync` addition. The frontend is complete as written and must not be
modified; the missing half is the server send.