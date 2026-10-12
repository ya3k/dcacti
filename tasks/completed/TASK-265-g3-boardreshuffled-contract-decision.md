# TASK-265 — G-3 `BoardReshuffled` Contract Decision

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
-->

---

## Metadata

```text
Task ID:           TASK-265
Type:              DOCUMENTATION
Status:            DONE
Risk:              MEDIUM
Priority:          HIGH
Primary Agent:     orchestrator
Supporting Agents: realtime, client, review
Workflow:          documentation/documentation-change.md
Skills:            quality/documentation-consistency,
                   realtime/realtime-protocol-validation,
                   client/client-event-projection,
                   quality/scope-validation
Dependencies:      TASK-258 (DONE), TASK-259 (DONE), TASK-260 (DONE), TASK-261 (DONE),
                   TASK-262 (DONE), TASK-263 (DONE), TASK-264 (DONE)
Declared Files:    docs/03-decisions/ADR/ADR-026-shared-board-gameplay-and-actor-relative-match-resolution.md,
                   docs/02-technical/GAME_EVENTS.md
```

---

## Objective

Resolve the documented inconsistency TASK-261 §Remaining Issue 4, TASK-262 G-3 and TASK-263 G-3
reported: `ADR-026` §8 promises that the Boss AI's deadlock reshuffle emits `BoardReshuffled`,
while the authoritative event list (`GAME_RULES.md` §16, `GAME_EVENTS.md` §2), the closed wire
discriminator set (`SIGNALR_PROTOCOL.md` §3.2.2) and the implementation (`BattleEventType`,
`BossAiEngine`, `BattleStateService`) contain no such event, and none is emitted. Decide the
smallest correct contract decision, record it in the owning documents, and write down the
implementation recommendation for a future `BoardReshuffled` feature if product requirements ever
justify one. **No event is implemented; no wire, state, persistence or gameplay code changes.**

---

## Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1 (the change adds no system or content)
- `docs/01-game-design/GAME_RULES.md` §16 (the canonical event-name list), §17 step 18a (the Boss's
  board action), §18 (server authority), §20–§21 (rule-change policy; precedence)
- `docs/01-game-design/MATCH3_RULES.md` §1.0 (cell indexing), §1.4/§1.4.1 (initial-board validity and
  the generation-time validator's boundary), §4 (cascade loop), §8.2/§8.3 (`Sequence`, `Turn`)
- `docs/02-technical/GAME_EVENTS.md` §1 (event order in one resolution), §2 (event semantics),
  §3 items 5–7 (internal vs delivered; events are not state; emission is a separate stage)
- `docs/02-technical/SIGNALR_PROTOCOL.md` §3/§3.1 (one atomic batch per resolution; no per-step
  delivery; board travels in the state push), §3.2.2 (closed discriminator set), §3.2.26/§3.2.27
  (`SwapStarted`/`SwapResolved`; no board member), §4 (`BattleStateUpdated`), §6 item 4, §7
- `docs/02-technical/GAME_STATE.md` §2 (BattleState), §2.1 (`BoardState` = `Cells[64]`, no second
  field), §3 (Transient Resolution State), §5/§5.1 (one atomic round, one `Sequence`, one write-back;
  nothing written mid-resolution)
- `docs/03-decisions/ADR/ADR-026-...md` §2 (atomic round), §7 (persistence vs. delivery), §8
  (deterministic Boss AI and deadlock reshuffling)
- `docs/03-decisions/ADR/ADR-008-battle-recovery.md` (snapshot resync; no event replay) and
  `ADR-009` (deterministic PRNG)
- `docs/03-decisions/README.md` §1, §3, §5 (ADR records *why*; an ADR never overrides a technical
  document), §7 (index), §8 (open items are resolved by a new ADR, not by retroactive edits)
- `.ai/workflow/documentation/documentation-change.md` (canonical owner; no duplication),
  `.ai/workflow/architecture/adr-change.md` §2–§3, `.ai/workflow/quality/review.md`,
  `.ai/skills/quality/documentation-consistency.md`
- `tasks/completed/TASK-261` (Remaining Issue 4), `TASK-262` (G-3), `TASK-263` (G-3),
  `TASK-264` (§Remaining Contract Limitation) — leads, re-verified against current source

---

## Preflight Findings

Verified by direct inspection at HEAD `292981a` (working tree: `.ai/HARNESS_COMPATIBILITY.md`,
`.ai/HARNESS_MAPPING.md`, `.ai/agents/AGENT_SELECTION.md`, `.ai/agents/orchestrator.md`,
`.ai/skills/README.md`, `.ai/skills/SKILL_REGISTRY.md`, `.ai/workflow/README.md`, `.gitignore`
modified; `.agents/`, `.ai/PROJECT_STATE.md`, `.ai/skills/discovery/task-readiness-assessment.md`,
`.ai/workflow/core/task-selection.md`, `.claude/` untracked — **not this task's**, disclosed as
unowned/pre-existing).

1. **Task ID, authorization and lifecycle.** `tasks/README.md` §3: "The next ID is the highest
   existing ID + 1 across all folders." The highest existing ID on disk is `TASK-264`
   (`tasks/completed/`), there is **no** `TASK-265*` file anywhere, and `tasks/backlog/`,
   `tasks/active/` and `tasks/blocked/` contain only `.gitkeep`, so no other task holds an active
   manifest and no joint group commit is required (`TASK_LIFECYCLE.md` §6.1 item 5). **Recorded
   discrepancy, not a blocker:** `TASK-258` §9's candidate roadmap pre-allocated the name
   `TASK-265` to "Headless Gameplay Balance Simulation and Integration Verification". That roadmap
   is explicitly a *candidate* map ("All IDs verified unassigned on disk; tasks remain in proposed
   candidate status and are NOT marked READY"), `TASK-259` §Out of Scope forbade pre-authorizing
   `TASK-260`…`TASK-265`, and the executed numbering already departed from it (`TASK-261` executed
   the roadmap's `TASK-262` scope; `TASK-260` merged the roadmap's 260 and 261). An ID is assigned
   when a task is created (`tasks/README.md` §3), never by a proposal inside a completed audit
   record, so `TASK-265` is the correct next sequential ID and is available. The still-unexecuted
   headless-simulation work therefore needs a fresh ID; reported in §Remaining Issues.
2. **Where the reshuffle actually is.** The only runtime deadlock recovery in the repository is
   inside `BossAiEngine.SelectMove` (`src/backend/GameServer.Domain/Bosses/BossAiEngine.cs:199-217`):
   it enumerates legal match-producing adjacent swaps and, when that set is empty, replaces the board
   with `BoardGenerator.Generate(rng)`'s valid board (`:205-206`), bounded by
   `BoardGenerator.MaxAttempts = 64`, continues the **same** `Pcg32` stream (`:207`), and then selects
   the move on the reshuffled board. The result records `Reshuffled: true`
   (`BossAiEngine.cs:48-55, 246`). Its one production consumer is the Boss Response phase:
   `BattleStateService.cs:1622-1624` (select), `:1629-1630` (`bossBoard = bossMove.Board`; the flag is
   used **only** to carry the RNG state), `:1632-1635` (the Boss's swap is resolved on the reshuffled
   board), `:1678-1683` (the round's board becomes that resolution's board).
3. **No event exists, at any layer.** The literal `BoardReshuffled` occurs in `docs/` only at
   `ADR-026:67`, and in `src/`/`tests/` nowhere. `BattleEventType` has 18 members and none of them is
   a reshuffle (`src/backend/GameServer.Domain/Match3/BattleEvent.cs:53-293`; closed-set assertion
   `tests/backend/GameServer.Domain.Tests/BattleEventEmissionTests.cs:1490-1513`), the wire projection
   has no such `type` (`src/backend/GameServer.Api/Hubs/BattleEventWireProjection.cs:774-807`), the
   client union has none (`BattleEventPresenter.ts:187-203`), `GAME_RULES.md` §16 does not list it,
   `GAME_EVENTS.md` §2 does not define it, and `SIGNALR_PROTOCOL.md` §3.2.2's 18-value set is
   declared **closed** against events `GAME_EVENTS.md` §2 does not define (`:518-528`). Nothing in
   the delivered payload identifies a reshuffle: `BattleStateUpdated` carries
   `board, turn, sequence, rngSeed, rngState, playerState, petState, bossState` and no flag or board
   revision (`BattleHub.cs:96-105`), and no board revision exists in the state model at all
   (`GAME_STATE.md` §2.1: `BoardState` has "exactly one field: `Cells[64]`"; §5.1 item 4: `Turn` "is
   not a version"; `MATCH3_RULES.md` §8.3: `Sequence` is "the **single** published state version").
4. **Delivery is atomic per round.** `ExecuteSwapAsync` builds the whole round — events included —
   then CAS-writes it (`BattleStateService.cs:906-923`), and the hub then pushes
   `BattleStateUpdated` and the single `ReceiveEvents` batch in that order
   (`BattleHub.cs:1120-1121`). One accepted round therefore produces one state push and one batch
   (`SIGNALR_PROTOCOL.md` §3 items 1–2, §3.1 items 1–3: "no per-pass message, and no partial
   batch"; §6 item 4), and one `Sequence` value (`GAME_STATE.md` §5.1 item 1).
5. **The client is already correct and needs nothing.** `BattleScene.playSwapPhase` exchanges the two
   delivered cells only for an action validated against the board it displays —
   `exchangedOnHeldBoard = delivered === null || delivered.actor === 'player'`
   (`BattleScene.ts:4235`) — and marks the two §1.0 cells instead for every other delivered boundary
   (`:4237-4240`, `markSwapCells` `:4281-4312`); the board is written once, at the round's single
   `settle` (`:4051-4055`). `SceneLifecycle.test.ts:8266-8315` asserts the reshuffle case: "Nothing
   delivered says so, which is exactly why the client may not move the contents it holds."
6. **Root cause of the inconsistency.** The name was invented as an engineering proposal, not derived
   from any owning document: `TASK-258` §10's audit table, row `C-TRX-03`
   (`tasks/completed/TASK-258-...md:116`), recommends "Implement deterministic board reshuffle when
   `HasValidSwap == false` after a cascade, consuming server PRNG and **emitting `BoardReshuffled`**",
   classified `Engineering Proposal`. `git blame`/`git log` show `ADR-026` §8's sentence entered the
   repository in `083372b` ("docs: synchronize game rules and technical specifications for
   shared-board battle", TASK-259) — a documentation-synchronization task whose scope was the game
   rules and technical specifications, not the addition of an event to the event list's owner. The
   ADR therefore recorded a promise no owning document makes, which `docs/03-decisions/README.md` §3
   forbids: an ADR must never override a technical document, and `GAME_EVENTS.md` outranks it
   (`AGENTS.md` §2, `GAME_RULES.md` §21). A second inaccuracy is in the same sentence: the runtime
   predicate is not `BoardGenerationValidator.HasValidSwap` (a generation-time helper used only by
   `BoardGenerator.Generate`, `MATCH3_RULES.md` §1.4.1) but the Boss move selection's own
   enumeration of legal match-producing swaps.
7. **A real behavioural gap, adjacent but out of scope.** Because the check lives only in the Boss
   phase, a deadlock is recovered only if the round reaches step 18a. The Player path
   (`SwapExecutor.Execute`, `SwapExecution.cs:415-553`) and `CascadeResolver.Resolve` have no
   validity check at all, so a round that short-circuits at the Boss-HP-0 terminal check
   (`BattleStateService.cs:1559-1593`) commits the board as-is, and a deadlocked board can reach the
   Player's next turn with no legal move and no recovery path. Also, nothing under `docs/` except
   `ADR-026` §8 mentions the reshuffle at all, so the recovery *mechanic* has no game-design or
   technical owner. Both are reported in §Remaining Issues; neither is fixed here (the task's
   out-of-scope list forbids changing the Boss AI or reshuffle generation, and authoring a new
   gameplay rule needs Product Owner approval, `AGENTS.md` §7, `GAME_RULES.md` §20).

---

## Scope

### In Scope

- The contract decision itself: what a `BoardReshuffled` event would have to mean, whether a public
  event is necessary, and what the atomic round permits (§Decision).
- Correcting the false promise and the inaccurate predicate in `ADR-026` §8, and recording in the
  same ADR that deadlock recovery is server-internal, emits no event, and is reported by the round's
  committed board (the ADR is the document that states it today).
- Recording the absence in the event documentation's own "what this document does not define"
  section (`GAME_EVENTS.md` §3 item 8), so the event contract's owner states that no event reports
  the operation and that a future need is raised by adding an event to `GAME_RULES.md` §16 first.
- A decision-level specification of the future `BoardReshuffled` option (semantics, emission
  condition, wire shape, ordering, duplicate/sequence/snapshot behaviour, compatibility cost) —
  recorded, **not implemented**.

### Out of Scope

- Implementing `BoardReshuffled`; adding any event, discriminator, DTO member, hub method, state
  field, Redis key or database column.
- Changing SignalR delivery, round atomicity, the Redis CAS write-back, snapshot recovery, sequence
  handling, the Boss AI, or reshuffle generation.
- Reworking TASK-264's presentation; any frontend, backend or test source change.
- G-2 (`Combo`/`MatchCount`), which remains untouched as TASK-263 reported it.
- Creating a new ADR; relocating the reshuffle mechanic into a game-design rule document; fixing the
  Player-side deadlock window.
- Re-auditing TASK-258…TASK-264.

---

## Declared File Set (P-2 / P-5)

Exact declared implementation files for TASK-265:

```text
docs/03-decisions/ADR/ADR-026-shared-board-gameplay-and-actor-relative-match-resolution.md
docs/02-technical/GAME_EVENTS.md
```

*(The task record itself is excluded from the implementation declared file set, and the list strictly
matches the `Declared Files:` field in `## Metadata`. Both declared files are documentation the
decision must land in: the ADR is the document that states the false promise, and `GAME_EVENTS.md` is
the owner of what a Battle Event is and of the delivered/internal boundary — `docs/03-decisions/README.md`
§3 keeps the *what* in the technical document and the *why* in the ADR, so the decision cannot live in
the ADR alone. No other document is stale: `GAME_RULES.md` §16 already omits the event and
`SIGNALR_PROTOCOL.md` §3.2.2's closed set already excludes it by construction.)*

**Shared-file ownership.** `ADR-026` was last owned by `TASK-259` (DONE, immutable) and
`GAME_EVENTS.md` by `TASK-263` (DONE, immutable). No task holds an active manifest, so no concurrent
shared-file owner exists and no joint group commit is required (`TASK_LIFECYCLE.md` §6.1 item 5).

**Unowned / pre-existing disclosure (P-3).** `git status --porcelain` at pickup reports
**modified**: `.ai/HARNESS_COMPATIBILITY.md`, `.ai/HARNESS_MAPPING.md`,
`.ai/agents/AGENT_SELECTION.md`, `.ai/agents/orchestrator.md`, `.ai/skills/README.md`,
`.ai/skills/SKILL_REGISTRY.md`, `.ai/workflow/README.md`, `.gitignore`; **untracked**: `.agents/`,
`.ai/PROJECT_STATE.md`, `.ai/skills/discovery/task-readiness-assessment.md`,
`.ai/workflow/core/task-selection.md`, `.claude/`. None is this task's; none is staged, and all are
disclosed in the Phase A commit body.

---

## Decision

### Q1 — What `BoardReshuffled` means, with the three candidate meanings kept apart

| Candidate meaning | Verdict | Evidence |
| --- | --- | --- |
| A server-internal deadlock-recovery **operation** | **This is what exists.** | `BossAiEngine.SelectMove:199-217`; `BattleStateService.cs:1622-1635, 1678-1683` |
| A **domain event** describing a committed board replacement | **Does not exist and is not needed to describe the state.** The board replacement *is* committed state: one write-back, one `Sequence`, delivered by the state push. | `GAME_STATE.md` §5.1 items 1–3; `SIGNALR_PROTOCOL.md` §3.1 items 1–3, §4; no board revision exists (`GAME_STATE.md` §2.1, §5.1 item 4) |
| A **client-facing presentation event** needed to explain the transition | **Not necessary, and insufficient on its own.** The client already presents the transition honestly from the board it receives; an event without a board could only add a label. | `BattleScene.ts:4235-4240, 4051-4055`; `SIGNALR_PROTOCOL.md` §3.1 item 2, §3.2.26 item 7 |

The inconsistency exists because `ADR-026` §8 used the **third** meaning's form (an event name) for
the **first** meaning (an internal operation) without any owning document defining it — the
root cause in §Preflight Findings 6.

### Q2 — Option comparison

**Option A — keep reshuffling server-internal** (correct `ADR-026` §8; state that the committed board
stays authoritative and travels in the round's existing state transition; record the intentional
limitation that the client cannot identify the operation from the event stream; add no discriminator
and change no delivery).

**Option B — define an explicit `BoardReshuffled` event** (admissible only on a demonstrated consumer
requirement the committed board transition cannot meet).

| Criterion | Option A | Option B |
| --- | --- | --- |
| Authoritative state & round atomicity | Preserved unchanged; the reshuffled board is the committed round board. | Preserved only if the event rides the round's single batch; it must not introduce per-pass delivery (`SIGNALR_PROTOCOL.md` §3.1 item 2). |
| Does it claim the client knows a board it never received? | No. Nothing infers a board. | Still no board, so it cannot restore the Boss's content animation either — it buys a label only. |
| Invented event semantics | None. | Requires a new name in `GAME_RULES.md` §16, a `GAME_EVENTS.md` §2 payload, a `SIGNALR_PROTOCOL.md` §3.2.x subsection, and a closed-set change — i.e. exactly the artifact that caused G-3. |
| Snapshot recovery / sequence handling | Unchanged (ADR-008: snapshot resync, no replay; `Sequence` still the single version). | Unchanged in substance: a reconnecting client still gets no event, and a duplicate or stale batch is deduplicated only by `serverSequence`, as today. |
| Frontend work required | None. TASK-264's uniform rule already covers it. | Still none required for correctness; a label would be optional polish. |
| **Demonstrated consumer requirement** | — | **None exists.** TASK-264's cell-mark rule is contract-sufficient and is asserted for the reshuffle case (`SceneLifecycle.test.ts:8266-8315`). |
| Cost / risk | One ADR correction + one absence statement. | Domain enum + projection + wire schema + client union + tests, for a label the client cannot act on. |

**Decision: Option A.** Option B is admissible only on a demonstrated consumer requirement that the
existing committed board transition cannot meet, and none exists — while the only *observable* want
(animating the Boss's Gems, or naming the board the Boss acted on) requires delivering the per-action
board, which `SIGNALR_PROTOCOL.md` §3.1 items 2–3 forbid, so Option B would not satisfy it either.
Preferring Option B would be inventing event semantics solely to satisfy an outdated ADR sentence.

### Q2 (Option B, recorded for a future decision — NOT implemented)

If product requirements later justify a public signal, the decision-level contract would have to be:

- **Meaning / emission condition:** one event emitted at most once per committed round, when the Boss
  Response phase's move selection replaced the board because it had no legal match-producing swap. It
  reports the *operation*, never a board.
- **Domain vs. wire:** both only if admitted through the owning chain `GAME_RULES.md` §16 →
  `GAME_EVENTS.md` §2 → `SIGNALR_PROTOCOL.md` §3.2.x, plus a `BattleEventType` member; a wire-only
  member is not admissible (`SIGNALR_PROTOCOL.md` §8 item 1).
- **Payload:** metadata only (no board, no board revision — none exists). A board revision identifier
  would be new state and would contradict `GAME_STATE.md` §2.1/§5.
- **Position:** inside the Boss Response phase's part of the single batch, after the Player's cycle,
  before the Boss's `SwapStarted`; the state push for the round still precedes the batch
  (`BattleHub.cs:1120-1121`, `SIGNALR_PROTOCOL.md` §6 item 4).
- **How the client gets the right board:** it does not — from the round's state push, exactly as
  today. The event would only let the client label the transition; without a per-action board it
  cannot animate the Boss's contents, and a per-action board is a much larger, separate decision.
- **Duplicate / stale / snapshot behaviour:** identical to every other event — delivered once in the
  round's batch; a duplicate or older batch is ignored by `serverSequence`; a reconnect recovers by
  snapshot with no replay (ADR-008).
- **Compatibility:** the discriminator set is closed and the change is additive-but-breaking for any
  strict consumer; it requires a protocol revision, a projection branch, a client union member and
  tests.

### Q3 — What atomicity permits

| Layer | Status under the current contract |
| --- | --- |
| Internal intermediate board mutations | Permitted and invisible. The reshuffle is one: it happens inside the round, before the single write-back, and is Transient Resolution State (`GAME_STATE.md` §3, §5.1 item 2; `MATCH3_RULES.md` §4.1). |
| Events included in the committed round | Only the events `GAME_EVENTS.md` §2 defines, in `GAME_RULES.md` §17 order, in **one** batch (`SIGNALR_PROTOCOL.md` §3 items 1–2, §3.1 item 2 — "no per-pass message, and no partial batch"). |
| The single authoritative committed board/state push | `BattleStateUpdated`, sent before the batch, carrying the post-round board, `turn` and `sequence`; `Sequence` versions the round, not the board (`GAME_STATE.md` §5.1). |
| Client animation / presentation | Renders from the pushed board at the round's single transition; computes nothing (`SIGNALR_PROTOCOL.md` §3.1 item 5, `ARCHITECTURE.md` §2.2.1). |

A reshuffle report could therefore only ever be an event **inside** the round's batch; it can never be
per-pass state delivery. No per-pass state delivery, extra Redis commit or new persistence model is
required by this decision — and none is introduced.

### Q4 — Relationship to TASK-264

Under Option A nothing changes at all: no wire member, no discriminator, no delivery change, so
TASK-264's rule (`BattleScene.ts:4235-4240`) and its regression tests stay byte-identical and remain
correct. Under Option B, if it were ever adopted, the rule would **still** be correct, because Option
B would not deliver a board: the client could add a label but must still not exchange the displayed
cells' contents. The safe fallback and the no-false-movement guarantee are therefore preserved under
either option; Option A is the only one that does not touch the frontend at all.

---

## Current State

`ADR-026` §8 is the only document in the tree that asserts a `BoardReshuffled` event exists, and it is
the lowest-precedence document asserting it. `GAME_RULES.md` §16 (the canonical list),
`GAME_EVENTS.md` §2 (semantics), `SIGNALR_PROTOCOL.md` §3.2.2 (the closed wire set) and the
implementation are already consistent with each other: the reshuffle happens, the board it produces is
committed, and no event reports it. The correction therefore lands in the two documents that can carry
it at the right layer — the ADR (why the operation is internal) and the event owner (that no event
reports it, and how a future need is raised).

---

## Acceptance Criteria

*(Marked satisfied at completion, matching the convention every recent DONE record follows. No
criterion was altered after the fact; the marks were applied by this task's own annotation commit,
recorded under `### Commit`.)*

- [x] `ADR-026` §8 no longer promises a `BoardReshuffled` event, states that the reshuffle is a
      server-internal recovery operation, that the committed board remains authoritative and reaches
      the client through the round's existing state transition, and that the client cannot identify
      the operation from the event stream; its runtime predicate matches the implementation.
- [x] The correction is recorded in `ADR-026` itself (what was wrong, why it was wrong, why the
      decision itself is unchanged and no new ADR is required).
- [x] `GAME_EVENTS.md` §3 states the absence at the event contract's owner, with the delivery path
      that carries the result instead, and points to `GAME_RULES.md` §16 as the place a future event
      must be introduced; its Version line records the revision.
- [x] No event, discriminator, payload member, hub method, state field, Redis key or database column
      is added; no wire, state, persistence, gameplay or frontend file changes
      (`git diff --name-only` equals the declared set exactly).
- [x] The decision is evidenced from current source and documents (not from earlier reports), and the
      Option A/B comparison and the Option B decision-level specification are recorded here.
- [x] The declared-file-set validation and documentation-consistency validation pass; the repository's
      applicable documentation checks are run and any check that could not be run is reported as
      NOT RUN rather than as passing.
- [x] Zero unrelated working-tree change: the Phase A staged set equals the declared file set exactly,
      and every unowned/pre-existing path is left unstaged and disclosed.

---

## Implementation Notes

- Two in-place documentation corrections and one added item; no code. `ADR-026` keeps its `Accepted`
  status: its decision (deterministic, server-side deadlock recovery) is unchanged, so this is a
  record correction, not a supersession (`docs/03-decisions/README.md` §3, §8;
  `.ai/workflow/architecture/adr-change.md` §3's superseding path is not triggered).
- `GAME_EVENTS.md` gains one item in §3 ("What This Document Does Not Define") and no §2 payload: the
  decision adds no event, so no §1 ordering entry and no §2 reference block change.
- A stale reference in `ADR-026`'s `## Related Documents` (`tasks/active/TASK-258-...`, which has been
  in `tasks/completed/` since TASK-258 closed) is corrected to its actual path while that list is
  edited for this task.
- No new abstraction, registry, mechanism or consistency script: the repository has no automated
  documentation-consistency check (no test, script or CI job reads `docs/`; `scripts/` holds only
  `collect-project-state.sh` and `standalone-web-smoke.mjs`), so none is invented
  (`.ai/skills/quality/documentation-consistency.md` is the established *procedure*, and it is applied
  instead).

---

## Testing Requirements

### Required Verification

```text
[ ] Documentation consistency — the corrected ADR and the event owner read together state one
                              rule; no other document contradicts them (quality/documentation-
                              consistency.md mode C); `GAME_RULES.md` §16, `GAME_EVENTS.md` §2 and
                              `SIGNALR_PROTOCOL.md` §3.2.2 re-checked for staleness (none)
[ ] Closed-set integrity    — the 18-value discriminator set, `BattleEventType`'s 18 members and the
                              client union are unchanged (grep/read, no code edit)
[ ] Diff scope              — `git diff --name-only` equals the declared file set exactly; no source,
                              test, ADR index, or `GAME_RULES.md` change; `git diff --check` clean
[ ] Formatting/markdown     — no trailing whitespace introduced in the declared files; tables and
                              fences render
[ ] Lifecycle               — Phase A staged set equals the declared file set; Phase B files only this
                              completion record
```

*(No unit, integration, gameplay-scenario, build or E2E suite is required or run: the change is
documentation-only and touches no executable contract. Backend/frontend suites, the balance harness
and the browser E2E suites are out of scope by this task's brief, and the E2E harnesses cannot run in
this environment in any case — reported as NOT RUN.)*

### Key Edge Cases

- An ADR sentence that contradicts a higher-precedence document (the case here): corrected in the ADR,
  not by editing the technical owner to match the ADR (`docs/03-decisions/README.md` §3).
- A reader arriving from the event list: `GAME_EVENTS.md` §3 now answers "is there an event for the
  reshuffle?" with "no, by decision" instead of leaving the question to `ADR-026`.
- A future animation feature: the path is `GAME_RULES.md` §16 first, recorded in both documents.

---

## Stop Conditions

- If resolving the inconsistency required changing a gameplay rule, a wire member, the round's
  atomicity, persistence, snapshot recovery, or the Boss AI: **STOP** and report
  (`AGENTS.md` §17/§18, `GAME_RULES.md` §20). Only documentation of the existing contract is
  authorized here.
- If `GAME_RULES.md` §16, `GAME_EVENTS.md` §2 and `SIGNALR_PROTOCOL.md` §3.2.2 disagreed with each
  other about whether the event exists: report per `AGENTS.md` §4 rather than choosing a side. They
  agree — all three omit it.
- If the task required a new ADR, or a status change to an existing one: STOP and escalate. It does
  not: no decision is reversed.
- If the Player-side deadlock window had to be repaired to close this question: STOP — that is a
  gameplay-rule change plus a code change, reported instead (§Preflight Findings 7).

---

## Remaining Issues

1. **The reshuffle mechanic has no game-design or technical owner.** Nothing under `docs/` except
   `ADR-026` §8 mentions deadlock recovery, so a reader of `GAME_RULES.md` §17 or `MATCH3_RULES.md`
   cannot learn that the board may be replaced without a board action. Placing a runtime
   deadlock-recovery rule in its owning domain document is a rule decision requiring Product Owner
   approval (`AGENTS.md` §7, `GAME_RULES.md` §20); a follow-up task should be filed for it.
2. **Player-side deadlock is unrecovered.** The only runtime validity check runs inside the Boss's
   move selection, so a round that skips the Boss phase can commit a board with no legal swap, and
   the next Player turn has no legal action and no recovery path (§Preflight Findings 7). Needs a
   gameplay decision plus a code change; explicitly out of this task's scope.
3. **`TASK-265`'s candidate roadmap slot.** `TASK-258` §9 proposed `TASK-265` for the headless
   gameplay balance simulation; that work is still unexecuted and must now take a fresh sequential ID
   (`tasks/README.md` §3). No task was created here (the task's brief forbids it).
4. **G-2 is untouched and unchanged** (Player/Boss `Combo`/`MatchCount` representation), as TASK-263
   reported it.

---

## Completion Evidence

### Commit

- Implementation slice (Phase A): `6680e55afa7f3b01573fe10b371f6d7662868435`
- Subject: `docs: correct the deadlock-reshuffle event promise and record the contract decision`
  (no task ID in the subject, `TASK_LIFECYCLE.md` §6.2 P-4; the task ID is in the body under `owns:`)
- Owns: `TASK-265`
- Files: the declared file set, exactly (2 paths)
- Shared with: `none`
- Unowned / pre-existing (left unstaged, disclosed in the commit body):
  `.ai/HARNESS_COMPATIBILITY.md`, `.ai/HARNESS_MAPPING.md`,
  `.ai/agents/AGENT_SELECTION.md`, `.ai/agents/orchestrator.md`,
  `.ai/skills/README.md`, `.ai/skills/SKILL_REGISTRY.md`, `.ai/workflow/README.md`,
  `.gitignore`, `.agents/`, `.ai/PROJECT_STATE.md`,
  `.ai/skills/discovery/task-readiness-assessment.md`,
  `.ai/workflow/core/task-selection.md`, `.claude/`
- Completion record filing (Phase B): this completion record alone, filed under its own
  bookkeeping commit (a rename: `tasks/active/` → `tasks/completed/`).
- Completion annotation (bookkeeping, immediately after Phase B): this record alone, marking the
  `## Acceptance Criteria` boxes satisfied — the Phase B commit filed the record before the marks
  were applied. No criterion, decision, or evidence was changed by it; it exists so this DONE record
  follows the same all-satisfied convention as every other recent completion record.

### Changed Files

- `docs/03-decisions/ADR/ADR-026-shared-board-gameplay-and-actor-relative-match-resolution.md`
  - Decision item 8's second bullet is replaced: deadlock recovery is stated as a server-internal
    operation of the Boss's move selection (the real predicate and the real generator/bound), and a
    third bullet states that it emits no Battle Event, that the committed board follows from it and
    is delivered by the Round's single state push, and that the client cannot identify the operation
    from the delivered stream. The unimplemented "emitting `BoardReshuffled`" promise and the
    incorrect `BoardGenerationValidator.HasValidSwap` predicate are gone.
  - A new `## Amendment — Deadlock-Reshuffle Reporting (TASK-265)` section records what the first
    revision said, why it was wrong (an engineering-proposal event name carried into the ADR by the
    TASK-259 sync; an ADR may not create a contract), what the corrected rule is, and why the record
    is amended in place with no new ADR and no supersession (no decision is reversed).
  - `## Related Documents` gains `GAME_EVENTS.md` §1, §2, §3 item 8, and its stale
    `tasks/active/TASK-258-...` reference is corrected to `tasks/completed/TASK-258-...` while the
    list is edited (that file has been in `tasks/completed/` since TASK-258 closed).
- `docs/02-technical/GAME_EVENTS.md`
  - Version 2.13: records this revision at the document that owns event semantics.
  - New §3 item 8: an operation that produces no Battle Event at all — the deadlock reshuffle is the
    documented case; no `BoardReshuffled` event/discriminator/payload member exists or is promised;
    the board that follows from the reshuffle reaches the client in the Round's state push; the
    inability to identify the operation is an accepted limitation, and a future need is raised by
    adding the event to `GAME_RULES.md` §16 first (§3 item 5).

### Validation Results

- **Documentation consistency (mode C, re-read owner + referencing documents)** — PASS. After the
  change, `docs/` mentions `BoardReshuffled` only to state that it does **not** exist
  (`GAME_EVENTS.md:3`, `:712`; `ADR-026:68`, `:98`); `GAME_RULES.md` §16's canonical list,
  `GAME_EVENTS.md` §2 and `SIGNALR_PROTOCOL.md` §3.2.2's closed set are unchanged and no longer
  contradicted by any document. No duplicate definition was introduced: the mechanic stays with its
  owner (`ADR-026` §8) and `GAME_EVENTS.md` §3 item 8 states only the event-contract absence and
  points at the owner.
- **Closed-set integrity (read/grep only)** — PASS. `BattleEventType` still has its 18 members
  (`BattleEvent.cs:53-293`), the wire projection and client union are untouched, and
  `git diff --name-only` contains no `src/` or `tests/` path.
- **Diff scope (P-5)** — PASS. `git diff --name-only` for this task's work lists exactly the two
  declared files; every other listed modification is the pre-existing unowned set disclosed above.
- **Whitespace/formatting** — PASS. `git diff --check` clean; no trailing whitespace and no tab
  characters in either declared file.
- **Stale-reference check** — PASS. Every section/file cited by the changed text resolves
  (`GAME_RULES.md` §16/§17 step 18a/§21, `GAME_EVENTS.md` §2/§3 items 5–8,
  `SIGNALR_PROTOCOL.md` §3.1/§3.2.2/§4, `GAME_STATE.md` §5.1, `MATCH3_RULES.md` §1.4.1,
  `BossAiEngine.SelectMove`, `BoardGenerator.MaxAttempts`, `ADR-008`, `ADR-018` Decision 12), and the
  one stale reference found in a declared file was corrected (above).
- **`docs/03-decisions/README.md` — checked, no change required.** Its §7 index row for ADR-026 and
  its `Accepted` status remain accurate: this task adds no decision and reverses none, so the
  "Accepted (amended)" convention ADR-018 needed for an added Decision 12 does not apply. It is
  therefore deliberately outside the declared set (P-5 requires the staged set to equal that set).
- **Not run (no applicable suite).** No unit, integration, gameplay-scenario, build or E2E suite was
  run: this change touches no executable contract, no `src/` or `tests/` file is in the declared
  set, and the task's brief forbids broad backend/frontend suites for a documentation-only change.
  The backend/frontend test suites, the balance harness, `dotnet build`, `npm run build` and the
  browser E2E harnesses are **NOT RUN**, and the E2E harnesses cannot run in this environment
  (`docker` is unavailable and no local stack is listening) — reported as NOT RUN, never as passing.
  **No runtime verification is claimed**; the validation above is documentation-level only.
- **Working-tree hygiene.** `git status --porcelain` after the change reports the two declared files
  modified, the unowned/pre-existing paths unchanged and unstaged, and this task record untracked.

### Server Authority & Scope Verification

- [x] No gameplay value, rule, RNG draw, damage, HP, Power, Combo, reward or battle outcome was
      changed; the change is documentation only (`AGENTS.md` §10/§11, ADR-001).
- [x] No event, discriminator, payload member, hub method, state field, Redis key or database column
      was added; no delivery, persistence or recovery path was touched.
- [x] The Boss AI and reshuffle generation are untouched; the ADR text now describes them accurately
      instead of restating them as an event.
- [x] `MVP_SCOPE.md` §1 adherence: nothing new is introduced; an existing contract's documentation is
      corrected.
- [x] No new ADR, no status change, no supersession (`docs/03-decisions/README.md` §3, §8;
      `.ai/workflow/architecture/adr-change.md` §3 not triggered).
- [x] P-1…P-6 commit policy: single named task, exact declared set, no task ID in the subject, no
      batching, no amend/rebase, unowned paths disclosed.

### Review Pass (`quality/review.md` §1)

```text
Correctness     the corrected statements match GAME_RULES.md §16, GAME_EVENTS.md §2, SIGNALR_PROTOCOL.md
                §3.1/§3.2.2/§4, GAME_STATE.md §5.1 and the actual implementation
                (BossAiEngine.SelectMove, BattleStateService.cs:1622-1683, BattleHub.cs:1120-1121);
                no document now contradicts another
Architecture    no layering, boundary or module change; the decision is recorded at the owning layer
                (event contract in the technical owner, rationale in the ADR)
Scope           exactly the declared 2 documentation files; no source, test, ADR index or GAME_RULES
                change; the Option B specification is recorded, not implemented
Tests           no suite applies to a documentation-only change; the validation performed is
                documentation consistency, closed-set integrity and diff scope (reported as such)
Documentation   both changed documents are the required ones; GAME_RULES.md §16, GAME_EVENTS.md §2
                and SIGNALR_PROTOCOL.md §3.2.2 needed no change and were verified not stale
Security        untouched (no auth, session, transport or data-exposure surface)
Performance     no runtime effect
Maintainability no new abstraction, mechanism or consistency script; one absence statement and one
                correction record
Determinism     no RNG, state or gameplay semantics changed; the ADR now describes the existing
                deterministic recovery accurately
```
