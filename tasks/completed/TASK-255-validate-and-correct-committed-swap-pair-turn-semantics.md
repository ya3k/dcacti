# TASK-255 — Validate and Correct Committed Swap Pair Turn Semantics

<!--
  GEN-TASK EXECUTION MANIFEST TEMPLATE
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
-->

---

## Metadata

```text
Task ID:           TASK-255
Type:              BUG
Status:            SUPERSEDED
Risk:              LOW
Priority:          MEDIUM
Primary Agent:     gameplay (TASK_TYPES.md §5 Match-3 × BUG; AGENT_SELECTION.md §3
                   "Bug in Match-3 logic → Gameplay Agent")
Supporting Agents: backend, testing, review
Workflow:          development/bug-fix.md
Skills:            discovery/documentation-discovery, discovery/impact-analysis,
                   quality/documentation-consistency,
                   gameplay/authority-determinism-audit, testing/test-scenario-generation
Dependencies:      None (logically independent of TASK-254)
Declared Files:    src/backend/GameServer.Domain/Match3/SwapValidator.cs,
                   src/backend/GameServer.Domain/Match3/SwapExecution.cs,
                   src/backend/GameServer.Domain/Battle/BattleState.cs,
                   docs/01-game-design/MATCH3_RULES.md,
                   docs/02-technical/GAME_STATE.md,
                   tests/backend/GameServer.Domain.Tests/SwapExecutionTests.cs,
                   tests/backend/GameServer.Domain.Tests/CommittedSwapPairContractTests.cs
```

---

## Objective

Determine, from the authoritative rules and the actual implementation, whether the committed-Swap staleness check violates the documented rule that a pair of cells can be exchanged **at most once per Turn** — and correct it only if the existing request and state contract can distinguish a legitimate same-coordinate swap in a later Turn from a duplicate/replayed request of the same Turn and from a request delayed across a Turn boundary.

`LastCommittedSwapPair` is never cleared: it is replaced only by a later **committed** Swap (`GAME_STATE.md` §2.1.10 items 6–7), and the Swap request carries no Turn number, board version or idempotency key (`SIGNALR_PROTOCOL.md` §2, §2.1; the transport discards `clientSequence` by design). **Do not assume that clearing the pair at a Turn boundary is safe.** If the contract cannot make the three cases distinguishable, the task documents the exact missing contract information and stops for a design decision; it does not introduce a reset that may permit a stale request.

This task makes **no** protocol change and no gameplay-rule change. If the correct outcome turns out to require either, that outcome is reported and handed off, not implemented here.

---

## Authoritative References

- `docs/01-game-design/MATCH3_RULES.md` §2.1.1 item 2 — the swap's identity is the unordered pair `{from, to}`; argument order has no gameplay meaning
- `docs/01-game-design/MATCH3_RULES.md` §2.1.2 — the four-check validation order, with "already applied / idempotency" as check 3
- `docs/01-game-design/MATCH3_RULES.md` §2.1.4 — the owner of staleness and idempotency: item 1 (`clientSequence` is not an action version and is not used to detect staleness), **item 2** (the operative test is "the pair most recently committed to the board" → `STALE_ACTION`, with the same item's justification "The two cells are exchanged once per Turn at most"), item 3 (a Swap is otherwise stateless against the current board; no per-Turn queue), item 4 (no observable in-flight window; arrival order), item 5 (`STALE_ACTION` is not a version-mismatch code), item 6 (determinism)
- `docs/01-game-design/MATCH3_RULES.md` §2.1.5 — rejection is a gameplay no-op (board, `Turn`, `Sequence`, `RngState` unchanged; no event)
- `docs/01-game-design/MATCH3_RULES.md` §2.1.6 — the commit stage and its write-back
- `docs/02-technical/GAME_STATE.md` §2.1.10 — the owner of the field: items 1–2 (most recently committed pair, canonical `(min, max)`), item 3 (absent before the first commit; no sentinel), item 4 (absent never rejects), item 5 (set only by a successful commit), **item 6** (a rejection changes nothing), **item 7** (a rejection never clears the previous commit; only a later committed Swap replaces it), item 8 (the client never supplies or reads it), item 9 (not delivered), item 10 (serialization), **item 11** (no action id, request id, correlation value, timestamp, retry count, generation counter, **Turn number**, or queue is stored here or beside it), items 12–13 (defines no new Swap rule; no gameplay behavior)
- `docs/02-technical/GAME_STATE.md` §2.0.1 — the meaning of `Turn`; §5 / §5.1 — `Sequence` and the single post-resolution write-back, and that nothing is written mid-resolution; §5.3 — snapshots between resolutions
- `docs/02-technical/SIGNALR_PROTOCOL.md` §2 — `Swap(battleId, fromCell, toCell, clientSequence)`; §2 item 1 — `clientSequence` is **not** an action version and is **not** used to reject stale actions; §2.1 items 1–4 — the parameters, the unordered pair, and "no gameplay field is accepted" (no `Turn`, no `Sequence`)
- `docs/01-game-design/GAME_RULES.md` §2 item 1 — "A Turn represents one player Swap/Action"; §18 — server authority
- `docs/00-overview/MVP_SCOPE.md` §1 — Match-3 (8×8 board, Cascade, Combo) is IN scope
- `docs/03-decisions/ADR/ADR-001-server-authoritative-battle.md` — server authority; the client cannot declare or influence staleness
- `tasks/TASK_TYPES.md` §2/§4 — a rule/behavior change is GAMEPLAY-CHANGE (always HIGH risk) and must update authoritative documentation first; a BUG fixes behavior that deviates from documented intent
- `.ai/workflow/development/bug-fix.md` §1 (bug category must be determined first), §3 (docs describe intended behavior by default, but "the documentation itself is stale" must be reasoned and reported, never silently assumed), §4 (never change behavior merely to make a test pass)
- `AGENTS.md` §4 (never silently resolve a documentation conflict — detect, cite both, propose the smallest correction, STOP for approval), §7 (missing rule), §17 (documentation change rule), §18 (architecture change rule), §20 (stop conditions), §10/§11 (authority and determinism)
- `tasks/TASK_LIFECYCLE.md` §6 — the two-stage commit protocol; §6.5 — the pre-adoption batch-commit history (see Declared File Set)
- `.ai/workflow/core/validation.md` §2 — LOW validation depth (build/compile + focused unit tests + review); this task performs the documentation validation explicitly because a documentation/state contract **is** its subject

---

## Audit Findings This Task Validates

Verified by direct inspection at HEAD `4645e64`. Symbol references are canonical; line references are to that revision.

```text
AF-1  The record is never cleared at a Turn boundary.
      SwapExecutor.Execute (SwapExecution.cs:401) writes
      LastCommittedSwapPair = CommittedSwapPair.FromCells(request.From, request.To)
      in the single post-resolution write-back (:484–523), beside Turn = state.Turn + 1
      (:494) and Sequence = state.Sequence + 1 (:506). No code path clears the field:
      BattleState carries it as an optional member (BattleState.cs:368), creation
      leaves it absent (:425), and the serializer round-trips presence/absence
      (BattleStateSerializer.cs:204, :511).
      Consequence: the record is Turn-unbounded — it survives every later Turn until
      some other Swap commits.

AF-2  The staleness check reads that record with no Turn input.
      SwapValidator.Validate (SwapValidator.cs:146–209) performs check 3 as
      CommittedSwapPair.FromCells(from, to).Matches(lastCommittedSwapPair) → 
      SwapRejectionReason.StaleAction (:192–195). Its inputs are the board, the pair
      and the request only; it has no Turn, sequence or version parameter, and it
      never writes the pair (the class doc, :42–49).
      Consequence: the same coordinates are rejected for the whole battle, not for
      the Turn in which they were committed.

AF-3  The request cannot express a Turn, a board version or an idempotency key.
      SwapRequest is `readonly record struct SwapRequest(int From, int To)`
      (SwapRequest.cs:35). On the wire the only client-supplied identifier is
      clientSequence, which MATCH3_RULES.md §2.1.4 item 1 and SIGNALR_PROTOCOL.md §2
      item 1 define as an opaque correlation value that is **not** used to detect
      staleness — and BattleHub accepts and discards it (`_ = clientSequence;`,
      BattleHub.cs:1068, :1126, :1160). SIGNALR_PROTOCOL.md §2.1 item 4 forbids the
      request carrying any gameplay field.
      Consequence: the server holds no signal that can separate a later-Turn reuse
      from a same-Turn replay or a delayed request.

AF-4  Two authoritative statements pull in different directions.
      MATCH3_RULES.md §2.1.4 item 2 states the operative test as the pair "most
      recently committed to the board" (no Turn scope; GAME_STATE.md §2.1.10 items 7
      and 11 confirm that only a later commit replaces it and that no Turn number is
      stored), and in the same item justifies it with "The two cells are exchanged
      once per Turn at most". The justification describes exactly the case the check
      was written for — applying the pair again immediately would revert the
      exchange — and that reasoning does not hold once later committed Swaps have
      moved the board on.
      Consequence: the implementation is consistent with the operative sentence and
      inconsistent with the per-Turn sentence; which one governs is a decision, not
      an inference (AGENTS.md §4/§20).

AF-5  The three cases the task must distinguish are indistinguishable to the server.
      (1) a legitimate swap using the same coordinates in a later Turn, (2) a
      duplicate/replayed request from the same Turn, and (3) a delayed request
      crossing a Turn boundary all arrive as the same (from, to) against the same
      current board, with the same absent Turn/version signal (MATCH3_RULES.md §2.1.4
      item 3 makes the swap stateless against the current board, and item 4 states
      the server resolves one action at a time in arrival order with no observable
      in-flight window).
      Consequence: no reset, and no check, can separate them from the request and the
      state alone — the missing contract information is an idempotency key or a
      Turn/board-version binding, which is a protocol/state-shape decision.

AF-6  No existing test covers a later-Turn reuse or a turn-boundary-crossing request.
      The staleness suites assert same-commit replay in both spellings
      (SwapExecutionTests.cs:647–664, CommittedSwapPairContractTests.cs:316–350), a
      different pair (:692–705), a fresh battle (:706–722), determinism (:744–760),
      server authority (:762 ff.) and rejection no-op semantics (:661–690). Every one
      of them is a same-Turn (indeed same-state) scenario; none advances Turn between
      the commit and the replayed pair.
```

---

## Supersession Evidence

```text
[x] Task was never directly executed
[x] 100% of the task's original scope/deliverables are verified complete in documented downstream tasks
[x] Supersession / downstream satisfaction evidence is explicitly recorded linking each deliverable to its satisfying downstream task
[x] Zero actionable independent scope remains in the task
```

### Satisfaction Audit

- **Investigation & Finding:** The audit findings AF-1 through AF-6 proved that the 3 cases cannot be distinguished without new protocol and state contracts (an expected state version precondition and action idempotency identifier).
- **Handoff:** Because TASK-255 was scoped strictly as a BUG task without protocol or gameplay-rule change authority, its open decision items (DR-1 and DR-2) required an architecture/protocol task.
- **Downstream Task:** TASK-256 ("Design and Implement Swap Action Preconditions & Idempotency") and ADR-025 ("Swap Action Preconditions and Idempotency", superseding ADR-010) were authorized and approved by the Product Owner to design, specify, implement, and test the full resolution.
- **Status Transition:** Per `tasks/TASK_LIFECYCLE.md` §1 and §3, TASK-255 is marked `SUPERSEDED` and moved to `tasks/completed/`.
