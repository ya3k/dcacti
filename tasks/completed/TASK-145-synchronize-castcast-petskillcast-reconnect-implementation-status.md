# TASK-145 — Synchronize the `ARCHITECTURE.md` §2.2.1 CardCast / PetSkillCast / Reconnect Implementation-Status Wording

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ by path and section — it does NOT copy game rules,
  formulas, schemas, or contracts.

  THIS TASK AUTHORS NO CONTRACT AND IMPLEMENTS NO BEHAVIOR. It corrects one
  stale implementation-status sentence and the one dependent cross-reference
  clause that sentence anchors. Every contract it touches stays identical in
  meaning.

  BOUNDARY: docs/ only, in exactly one document. Zero files under src/ or
  tests/. Zero ADRs.

  PROVENANCE: the staleness was verified during the post-TASK-144 repository
  reconciliation (a read-only pass) against source and against the completed
  tasks TASK-107, TASK-115, TASK-120, TASK-143, and TASK-144. It was NOT fixed
  inline (AGENTS.md §16).

  THIS TASK IS NOT the `BATTLE_NOT_FOUND` result-presentation follow-up and is
  NOT the `runtime-smoke.mjs` reconciliation. Those are separate concerns and
  must not be combined with this one.

  LINE REFERENCES are to the file as of this task's creation and must be
  re-verified at pickup (`Implementation Notes`).
-->

---

## Metadata

```text
Task ID:           TASK-145
Type:              DOCUMENTATION (TASK_TYPES.md §2 — "Change `docs/` content —
                   documentation is the primary output, not code"; workflow
                   documentation/documentation-change.md). See "Type
                   classification note" below.
Status:            DONE (executed directly at pickup; the lifecycle gates
                   BACKLOG → READY → IN PROGRESS → IN REVIEW → DONE were
                   passed in one session — see Completion Evidence →
                   Lifecycle)
Risk:              LOW (TASK_TYPES.md §4 — DOCUMENTATION baseline LOW–MEDIUM,
                   "LOW for corrections". This change alters no code, no API
                   contract, no SignalR method or payload, no Redis behavior,
                   no state model, no gameplay rule, and no ADR. It is not the
                   MEDIUM case: §2.2.1's rules 1–6 and its boundaries are
                   unchanged.)
Priority:          MEDIUM (the stale sentence tells every reader — human or
                   agent — that three implemented capabilities do not exist,
                   which is the duplicate-work failure mode TASK-097/TASK-100/
                   TASK-101 already recorded. It blocks no implementation
                   task.)
Primary Agent:     review (documentation consistency — TASK_TYPES.md §2
                   DOCUMENTATION: "Primary Agent: Review Agent
                   (documentation consistency)")
Supporting Agents: realtime (the corrected sentence names the §2 method
                   surface and §7 recovery — the contracts it must cite
                   rather than restate), client (the runtime/subscription
                   boundary §2.2.1 describes)
Workflow:          documentation/documentation-change.md
Skills:            discovery/documentation-discovery,
                   discovery/impact-analysis,
                   quality/documentation-consistency,
                   quality/implementation-review
                   (4 skills — Simple budget, tasks/README.md §12; the set is
                   .ai/skills/README.md §5.3's mapping for
                   documentation/documentation-change.md plus the review
                   skill)
Dependencies:      TASK-107 (DONE — CardCast server path),
                   TASK-115 (DONE — PetSkillCast server path),
                   TASK-120 (DONE — client CardCast / PetSkillCast action
                     paths),
                   TASK-143 (DONE — server BattleState reconnect recovery),
                   TASK-144 (DONE — client reconnect / resync recovery);
                   all five are read-only evidence sources and are NOT
                   modified by this task
Blocks:            Nothing. This task unblocks no implementation; it removes a
                   documentation defect that would otherwise cause an agent to
                   rebuild three implemented capabilities.
Estimate:          Simple (one stale sentence plus its dependent clause in one
                   section, plus the document Version line; no new prose
                   section, no code, no test)
```

**Type classification note.** `DOCUMENTATION`, not `BUG` and not `REFACTOR`.

It is **not** `BUG`: `TASK_TYPES.md` §2's BUG definition does mention stale
documentation, but `development/bug-fix.md` §2 requires a regression test that
fails before the fix. No executable behavior is involved and no test can fail on
a documentation sentence, so the BUG workflow cannot be honestly discharged
here. This is the same reasoning TASK-097 and TASK-100 already established for
stale implementation-status wording.

It is **not** `REFACTOR`: no source file is edited, so `development/refactor.md`'s
code-preservation checks have no subject.

**No ADR is created by this task, and none is required.** `ADR-008`
(Snapshot-Based Battle Reconnection) already records the reconnect decision and
is **unchanged and not reopened**; `ADR-014`/`ADR-015` are likewise untouched.
This task records **no decision** — it corrects a sentence that misdescribes
already-DONE implementation work. `architecture/adr-change.md` §1 and
`docs/03-decisions/README.md` §1 forbid minting an ADR that merely restates
existing technical documentation.

---

## Objective

Correct the stale implementation-status wording in
`docs/02-technical/ARCHITECTURE.md` §2.2.1 (L246–251) so that it describes the
capability set the repository has actually implemented, and so that the
cross-reference clause the sentence anchors stays true:

```text
CardCast client → server action path      implemented (server TASK-107, client TASK-120)
PetSkillCast client → server action path  implemented (server TASK-115, client TASK-120)
reconnect / resync snapshot recovery      implemented (server TASK-143, client TASK-144)
```

Every other statement in §2.2.1 — the coordination boundary, its diagram, rules
1–6, the initial-state paragraph, the board-delivery paragraph, and the `Swap`
paragraph — is accurate and must be preserved. This is a documentation-only
reconciliation: no source code, no test, no protocol, no API, no Redis
contract, no gameplay rule, and no ADR changes.

---

## Authoritative References

- `docs/02-technical/ARCHITECTURE.md` §2.2.1 — **the canonical owner** of the
  client game-runtime coordination boundary, and the **only** document this
  task edits; contains the stale sentence at L246–251
- `docs/02-technical/ARCHITECTURE.md` §2.2 rule 3, §2.2.3, §5.2 — the transport
  isolation, pre-battle selection, and no-event-sourcing boundaries §2.2.1
  supports; cited for consistency, **not edited**
- `docs/02-technical/SIGNALR_PROTOCOL.md` §2 — the client → server method
  surface (`Swap`, `CardCast`, `PetSkillCast`) and their parameters; the owner
  of *which methods exist*, cited by the corrected sentence
- `docs/02-technical/SIGNALR_PROTOCOL.md` §4 (initial-state push), §5
  (acknowledgement envelope), §6 (sequencing), §7 (Reconnect & Resync —
  `GetBattleState`), §8 items 7–8 (no new method; no event log or replay
  channel)
- `docs/02-technical/GAME_STATE.md` §2.0.5, §4 (staged state / client
  presentation copy), §5.1 (the authoritative write-back), §5.2 (`Sequence` is
  not the client correlation id), §5.3 (reconnect and snapshot compatibility)
- `docs/02-technical/REDIS_STATE.md` §3 (TTL), §5 (recovery) — the expiry
  condition behind `BATTLE_NOT_FOUND`, owned there and not restated
- `docs/01-game-design/CARD_RULES.md` §2–§3 (Basic Card casting) and §4–§4.1
  (Pet Skill Card / Signature Skill) — the domain owners of cast behaviour
- `docs/01-game-design/MATCH3_RULES.md` §2–§8 — board resolution; the contract
  the existing `Swap` paragraph cites
- `docs/02-technical/TDD.md` §6 — determinism; a recovered session behaves
  consistently; cited for consistency, **not edited**
- `docs/03-decisions/ADR/ADR-008-battle-recovery.md` — snapshot-over-replay;
  the decision the corrected sentence keeps citing; **unchanged, not reopened**
- `docs/00-overview/MVP_SCOPE.md` §1 — scope confirmation
- `tasks/completed/TASK-107-implement-basic-cardcast-server-path.md`,
  `TASK-115-implement-server-authoritative-petskillcast-crit-and-burn.md`,
  `TASK-120-implement-client-card-and-skill-cast-action-paths.md`,
  `TASK-143-implement-server-battle-state-reconnect-recovery.md`,
  `TASK-144-implement-client-reconnect-and-resync-recovery.md` — the DONE work
  that made the sentence false (read-only; **not modified**)
- `tasks/completed/TASK-100-synchronize-scene-staging-and-client-implementation-status.md`
  and
  `tasks/completed/TASK-101-synchronize-battlestateservice-boss-response-comment.md`
  — the precedent and the lineage of the wording being corrected (read-only;
  **not modified**)
- `AGENTS.md` §16 (report, do not fix inline), §17 (documentation change rule),
  §4 (conflict resolution), §9 (anti-overengineering)
- `.ai/workflow/documentation/documentation-change.md` §1–§3 — the governing
  workflow and the "one concept, one owner" / no-duplication rule
- `tasks/TASK_LIFECYCLE.md` §3 (DONE — completed tasks are immutable)

---

## Current State

`docs/02-technical/ARCHITECTURE.md` §2.2.1 ("Game Runtime Coordination",
L167–251) describes the client runtime boundary. Its final paragraph reads,
verbatim (L246–251):

```text
The remaining client → server gameplay methods (`CardCast`, `PetSkillCast` —
`SIGNALR_PROTOCOL.md` §2) and reconnect/resync snapshot recovery
(`SIGNALR_PROTOCOL.md` §7, ADR-008) are not implemented yet. The contract they
implement is owned by `MATCH3_RULES.md` §2–§8 (board resolution) and
`GAME_STATE.md` §5.1 (the state write-back); neither is restated in the client
runtime.
```

All three named capabilities have since been implemented and their owning tasks
are DONE.

### Verified stale-statement inventory (`ARCHITECTURE.md` §2.2.1)

| # | Statement (location) | Classification | Why |
|---|---|---|---|
| 1 | "The remaining client → server gameplay methods (`CardCast`, `PetSkillCast` — `SIGNALR_PROTOCOL.md` §2) … are not implemented yet." (L246–247) | **STALE** | Both methods exist as invokable hub methods, with client action paths; TASK-107/TASK-115/TASK-120 are DONE |
| 2 | "… and reconnect/resync snapshot recovery (`SIGNALR_PROTOCOL.md` §7, ADR-008) are not implemented yet." (L247–248) | **STALE** | Server `GetBattleState` and client recovery orchestration both exist; TASK-143/TASK-144 are DONE |
| 3 | "The contract they implement is owned by `MATCH3_RULES.md` §2–§8 (board resolution) and `GAME_STATE.md` §5.1 (the state write-back); neither is restated in the client runtime." (L248–251) | **STALE AS ANCHORED** | Once "they" denotes the cast/recovery capabilities (statement 1–2 corrected), this mapping is false: `MATCH3_RULES.md` §2–§8 and `GAME_STATE.md` §5.1 describe the **Swap/board-resolution** contract, which the preceding paragraph already cites (L244–245). The trailing boundary claim ("not restated in the client runtime") remains true, but its antecedent does not |
| 4 | L169–187 the coordination-boundary description and diagram | **TRUE — preserve byte-for-byte** | Matches `GameRuntime` / `SignalRService` / `BattleHub` |
| 5 | L189–221 rules 1–6 (transport independence, React/Phaser separation, "coordinates; does not compute", events pass through unchanged, technical-only runtime state, one runtime one connection) | **TRUE — preserve byte-for-byte** | No architectural boundary changed; `SIGNALR_PROTOCOL.md` L308/L331/L485 cite rules 3 and 4 and must keep resolving |
| 6 | L223–227 initial state subscription (`BattleStateUpdated`, §4) | **TRUE — preserve** | Implemented; unchanged by this task |
| 7 | L229–237 runtime foundation / §4 push / board delivery | **TRUE — preserve** | Implemented; unchanged by this task |
| 8 | L239–245 `Swap` implemented end-to-end over the `GAME_RULES.md` §17 pipeline | **TRUE — preserve** | Synchronized by TASK-100/TASK-101; not this task's subject |

Statement 1 is the sentence that is *deliberately* left in this state by
TASK-100 (which corrected only the `Swap` half) and by TASK-101 (which
corrected the source comment about the §17 Boss Response step). Neither task's
Changed Files covers `CardCast`, `PetSkillCast`, or reconnect recovery, because
none of the three was implemented when they ran.

### Verified implementation evidence (re-verify at pickup)

| Claim in §2.2.1 | Verified state | Evidence |
|---|---|---|
| `CardCast` not implemented | **FALSE — implemented** | Server: `src/backend/GameServer.Api/Hubs/BattleHub.cs:859` `public async Task<CardCastResponse> CardCast(string battleId, string cardId, string? clientSequence = null)` → `BattleStateService.ExecuteCardCastAsync`, Domain `GameServer.Domain/Cards/CardCastExecutor.cs`, wire arm in `BattleEventWireProjection.cs`. Client: `GameRuntime.ts:359–391` (`requestAction` routes `CardCast`), `BattleScene.ts:750–753`, `SignalRService.ts:618`. Task: **TASK-107** (`Status: COMPLETED`) |
| `PetSkillCast` not implemented | **FALSE — implemented** | Server: `BattleHub.cs:895` `public async Task<PetSkillCastResponse> PetSkillCast(string battleId, string? clientSequence = null)` → `BattleStateService.ExecutePetSkillCastAsync`, `CardCastExecutor.cs`, `BattleEventWireProjection.cs`. Client: `GameRuntime.ts:359–391`, `BattleScene.ts:782`, `SignalRService.ts:645`. Task: **TASK-115** (DONE; completion evidence records the full backend suite PASS, 2,344 tests) |
| Client cast action paths absent | **FALSE — implemented** | `BattleScene.ts` invokes `requestAction` with `RUNTIME_ACTION_CARD_CAST` (L750–753) and the Pet Skill kind (L782); `RuntimeBoundaries.test.ts` records the invoked-method set `['CardCast','GetBattleState','JoinBattle','PetSkillCast','Ping','Swap']`. Task: **TASK-120** (DONE) |
| reconnect/resync snapshot recovery not implemented | **FALSE — implemented** | Server: `BattleHub.cs:698` `public async Task<GetBattleStateResponse> GetBattleState(string battleId)` + `BattleStateService.GetOwnedBattleStateAsync` (ownership-checked snapshot read, one write-back shape, `BATTLE_NOT_FOUND` for unknown/expired/foreign). Task: **TASK-143** (DONE; 2,606 backend tests PASS, real-Redis recovery smoke test). Client: `SignalRService.ts:691 getBattleState`, `GameRuntime.ts:203` (`onReconnected` → `recoverBattleState()`), `:600`, `:612` (recovered snapshot routed through the existing `receiveBattleState()`; no second ingestion path, no event replay). Task: **TASK-144** (DONE; 495 client tests PASS) |
| "The contract they implement is owned by `MATCH3_RULES.md` §2–§8 … and `GAME_STATE.md` §5.1" | **FALSE as anchored** | `MATCH3_RULES.md` §2–§8 owns board resolution (the `Swap` contract, cited at L244); the method surface is owned by `SIGNALR_PROTOCOL.md` §2; cast semantics by `CARD_RULES.md` §2–§4; recovery by `SIGNALR_PROTOCOL.md` §7 with `GAME_STATE.md` §5.3 / `REDIS_STATE.md` §3, §5 |
| "neither is restated in the client runtime" | **TRUE boundary, bad antecedent** | `GameRuntime.ts` restates no board-resolution or snapshot contract — it forwards and ingests. Keep only if its antecedent is made true, otherwise remove the clause |

### Document-header note (scope boundary — see Stop Conditions)

The document's `**Version:**` block (L3–L19) contains a present-tense clause
inside its **historical** Version 1.3 entry (L7–L8):

```text
`CardCast`, `PetSkillCast`, and reconnect/resync recovery remain not implemented.
```

The repository convention is that a document's prior version entries are
history and the newest entry records the current status — `GAME_STATE.md`
retains "Prior 2.x" narrative that describes superseded wording, and
`DATABASE.md` records a status synchronization the same way. This task therefore
**prepends a new `**Version:**` entry** and leaves L3–L19's prior-entry text
byte-unchanged.

### Known artifacts that contradict the implementation (reported, never fixed here)

These are **not** documentation and are **not** in scope. They are listed so the
executing agent does not mistake them for evidence of absence — a stale comment
is not evidence that code does not exist:

- `src/backend/GameServer.Api/Hubs/BattleHub.cs:489–491` — the class comment
  still says `PetSkillCast` "is intentionally NOT implemented", while
  `BattleHub.cs:895` implements it. `src/` scope: report, do not fix.
- `tests/backend/GameServer.Api.Tests/ApiIntegrationTests.cs:261–262` and
  `:284` — `BattleHub_ShouldNotRegisterGameplayMethods` still lists
  `"PetSkillCast"` as not registered and states that rationale; the assertion
  passes only because the invocation supplies no arguments. `tests/` scope:
  report, do not fix.
- `tasks/completed/TASK-107-implement-basic-cardcast-server-path.md` — declares
  `Status: COMPLETED` but its `## Completion Evidence` block is still the
  unfilled template and its acceptance boxes are unchecked. Completed tasks are
  immutable (`TASK_LIFECYCLE.md` §3): report, do not edit.

---

## Scope

### In Scope

1. **Re-verify the stale-statement inventory above against source at pickup.**
   Every row of both evidence tables must be reproduced. A row that no longer
   holds changes the task's premises — see Stop Conditions.
2. **Correct the stale implementation-status sentence only**
   (`ARCHITECTURE.md` §2.2.1, L246–248) so it no longer states that `CardCast`,
   `PetSkillCast`, or reconnect/resync snapshot recovery are not implemented,
   and instead states their status accurately. Keep the existing
   `SIGNALR_PROTOCOL.md` §2 / §7 and `ADR-008` citations — those documents own
   *what* is implemented and must be cited, not restated.
3. **Resolve the dependent clause** (L248–251) whose antecedent ("they") the
   correction changes. The two acceptable minimal outcomes are:
   - **remove the clause**, if its only remaining anchor was the
     unimplemented grouping; or
   - **correct it** to cite the documents that actually own the newly stated
     capabilities.

   Whichever is chosen, the executing agent must record the choice and its
   reason in Completion Evidence. The clause must not restate any contract
   (`documentation-change.md` §2), and must not assert ownership that is not
   traceable to an existing document. The boundary claim "not restated in the
   client runtime" may stay only if it remains true of its antecedent.
4. **Prepend a new `**Version:**` entry** to the document header recording this
   synchronization, in the convention that file already uses (newest first,
   prior entries retained verbatim). Do not renumber, restructure, or rewrite
   the existing 1.1 / 1.2 / 1.3 entries.
5. **Confirm — not "improve" — the rest of §2.2.1.** Rules 1–6, the diagram,
   the initial-state paragraph, the board paragraph, and the `Swap` paragraph
   are expected to require **no change**. Verify each cited cross-reference
   still resolves (§2, §4, §7 of `SIGNALR_PROTOCOL.md`; §2, §2.0.5, §4, §5.1 of
   `GAME_STATE.md`; `MATCH3_RULES.md` §2–§8; `ADR-008`).
6. **Report, never fix, anything else** (`AGENTS.md` §16): additional stale
   implementation-status claims in other documents, and the source-comment /
   test-rationale artifacts listed in Current State.

### Out of Scope

- **Any source code** — nothing under `src/`. No `BattleHub.cs`,
  `BattleStateService.cs`, `CardCastExecutor.cs`, `SignalRService.ts`,
  `GameRuntime.ts`, `GameRuntimeEvents.ts`, `BattleScene.ts`, or any other file.
- **Any test change** — nothing under `tests/`, including the stale
  `ApiIntegrationTests.cs` rationale listed in Current State.
- **Implementing anything.** `CardCast`, `PetSkillCast`, and reconnect/resync
  recovery are already implemented; this task only describes them.
- **`BATTLE_NOT_FOUND` result presentation** — a separate follow-up concern.
  Not investigated, not scoped, not created here.
- **`runtime-smoke.mjs` reconciliation** — a separate follow-up concern. Not
  investigated, not scoped, not created here.
- **Reopening or editing `ADR-008`**, or creating any ADR.
- **Editing any other `docs/` file** — `SIGNALR_PROTOCOL.md`,
  `GAME_STATE.md`, `REDIS_STATE.md`, `TDD.md`, `API_CONTRACTS.md`,
  `GAME_EVENTS.md`, `DATABASE.md`, all of `docs/00-overview/`, all of
  `docs/01-game-design/`, and all of `docs/03-decisions/`.
- **Rewriting the document's prior `**Version:**` entries** (L3–L19) — they are
  historical records; only a new entry is added.
- **Modifying TASK-107, TASK-115, TASK-120, TASK-143, TASK-144, TASK-100,
  TASK-101, or any other completed task** (`TASK_LIFECYCLE.md` §3) — including
  TASK-107's unfilled Completion Evidence block.
- **Any protocol, API, Redis, SignalR, schema, payload, or event change.**
- **Any gameplay rule, formula, balance value, timing, or content change.**
- **Any architecture change** — the runtime boundary, its rules 1–6, and its
  ownership model are correct and are not redesigned.
- **Sweeping `docs/` for unrelated stale statements.** Discovered items are
  reported, not fixed.
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2.

---

## Dependencies

All five dependencies are **DONE evidence sources**, read-only for this task,
and immutable per `tasks/TASK_LIFECYCLE.md` §3:

```text
TASK-107  DONE  CardCast server path          → falsifies the CardCast half
TASK-115  DONE  PetSkillCast server path      → falsifies the PetSkillCast half
TASK-120  DONE  client CardCast/PetSkillCast  → falsifies the "client → server" framing
TASK-143  DONE  server reconnect recovery     → falsifies the recovery half (server)
TASK-144  DONE  client reconnect / resync     → falsifies the recovery half (client)
```

`TASK-100` and `TASK-101` are the **lineage** of the sentence (each corrected a
neighbouring claim and deliberately left this one), cited for context only.
`SIGNALR_PROTOCOL.md` §2/§7 and `ADR-008` are the **contract owners** the
corrected sentence cites; none of them is modified.

This task has no incomplete dependency and creates no dependency for another
task.

---

## Acceptance Criteria

- [x] Every stale implementation-status statement in `ARCHITECTURE.md` §2.2.1
      has been identified.
- [x] Each stale statement is cross-checked against completed task evidence.
- [x] Only stale implementation-status wording is corrected.
- [x] Architectural intent and boundaries remain unchanged.
- [x] No source code is modified.
- [x] No SignalR / API / Redis contract is modified.
- [x] No gameplay rule is modified.
- [x] No completed task is modified.
- [x] No unrelated documentation is changed.
- [x] References to `CardCast`, `PetSkillCast`, and reconnect/resync accurately
      reflect the completed implementation state.
- [x] Documentation validation passes according to the repository workflow
      (`documentation/documentation-change.md` §1 final step, `quality/review.md`,
      `core/completion.md`).
- [x] `ARCHITECTURE.md` §2.2.1 no longer contains the string "not implemented
      yet" as a claim about `CardCast`, `PetSkillCast`, or reconnect/resync
      snapshot recovery.
- [x] `ARCHITECTURE.md` §2.2.1 still cites `SIGNALR_PROTOCOL.md` §2 for the
      client → server method surface and `SIGNALR_PROTOCOL.md` §7 + `ADR-008`
      for reconnect/resync snapshot recovery.
- [x] No dangling pronoun or orphaned antecedent remains: the corrected passage
      contains no "they"/"neither" whose referent no longer exists, and no
      clause asserts contract ownership that contradicts the corrected status.
- [x] `ARCHITECTURE.md` §2.2.1 rules 1–6, the coordination diagram, the
      initial-state paragraph, the board-delivery paragraph, and the `Swap`
      paragraph are **byte-unchanged**.
- [x] `ARCHITECTURE.md` §2.2.3 and §5 are **byte-unchanged**.
- [x] Every §-reference inside §2.2.1 resolves to an existing section of the
      document it names.
- [x] The document's `**Version:**` line records this synchronization as a new
      entry; the existing 1.1 / 1.2 / 1.3 entry text is byte-unchanged.
- [x] No new duplicated definition is introduced: no rule, schema, payload,
      state field, or contract is restated anywhere in the edited document
      (`documentation-change.md` §2).
- [x] `ARCHITECTURE.md` requires changes in **exactly one** content location
      (§2.2.1 L246–251) plus its `**Version:**` line.
- [x] No ADR is created; `docs/03-decisions/` is unmodified.
- [x] `docs/00-overview/`, `docs/01-game-design/`, and every other
      `docs/02-technical/` file are unmodified (verified by hash).
- [x] Zero files under `src/` are modified.
- [x] Zero files under `tests/` are modified.
- [x] All `tasks/completed/` files — including TASK-107, TASK-115, TASK-120,
      TASK-143, TASK-144, TASK-100, and TASK-101 — are unmodified (verified by
      hash).
- [x] The changed-file set equals exactly: `docs/02-technical/ARCHITECTURE.md`
      plus this task file.
- [x] No API endpoint, request, response, SignalR method, event, payload, Redis
      key, or database schema is changed or added.
- [x] No gameplay rule, formula, balance value, or timing is changed.
- [x] Required review checks pass (`quality/review.md` §1), skipping
      code-behavior items a documentation-only change cannot exercise.
- [x] No authoritative rule or contract is violated (`AGENTS.md` §10 / ADR-001).

### Explicit Constraints

```text
No gameplay.
No source code.
No test changes.
No architecture redesign.
No new ADR.
No protocol / API / Redis / SignalR contract change.
No speculative architecture.
No client-authoritative state.
No BATTLE_NOT_FOUND presentation work.
No runtime-smoke.mjs reconciliation.
TASK-107 / TASK-115 / TASK-120 / TASK-143 / TASK-144 and all completed tasks are NOT modified.
```

---

## Affected Files & Areas

```text
EDITED (completed)

[x] docs/02-technical/ARCHITECTURE.md
        §2.2.1 stale sentence + its dependent cross-reference clause
        + the `**Version:**` line (new entry only)

[x] tasks/backlog/TASK-145-synchronize-castcast-petskillcast-reconnect-implementation-status.md
        Status and Completion Evidence only (moved to tasks/completed/ at DONE)

VERIFIED UNCHANGED (hash-verified at completion)

[x] docs/02-technical/SIGNALR_PROTOCOL.md   (the cited §2/§7 owner)
[x] docs/02-technical/GAME_STATE.md         (the cited §5.1/§5.3 owner)
[x] docs/02-technical/TDD.md
[x] docs/02-technical/API_CONTRACTS.md
[x] docs/02-technical/GAME_EVENTS.md
[x] docs/02-technical/REDIS_STATE.md
[x] docs/02-technical/DATABASE.md
[x] docs/00-overview/**                     (GDD.md, MVP_SCOPE.md, ROADMAP.md)
[x] docs/01-game-design/**                  (all rule documents)
[x] docs/03-decisions/**                    (proof no ADR was created)
[x] src/**                                  (none)
[x] tests/**                                (none)
[x] tasks/completed/**                      (all completed tasks immutable)
[x] tasks/blocked/**, tasks/backlog/**      (except this task file)
```

---

## Implementation Notes

- **Verified reality is the authority, not this task's prose.** Re-verify every
  row of both Current State tables against source at pickup. If the source
  differs from what this task documents, **the source wins** and the difference
  is reported.
- **The correction is surgical, not a rewrite.** The defect is one sentence plus
  the clause it anchors. Rewriting the whole final paragraph, or the whole
  section, risks dropping the correct statements in rules 1–6 and violates
  `AGENTS.md` §16.
- **Do not delete the sentence.** Deleting it removes the citation of
  `SIGNALR_PROTOCOL.md` §2/§7 and `ADR-008` from §2.2.1 and silently drops the
  statement that the runtime does not restate those contracts.
- **Do not over-correct into "everything is implemented".** Only `CardCast`,
  `PetSkillCast`, and reconnect/resync recovery changed status. Do not claim the
  MVP is complete, do not claim anything about result presentation or the
  runtime smoke path, and do not describe mechanisms.
- **Status wording, not mechanism wording.** The corrected text must not restate
  the `§7` snapshot contract, the `§2` acknowledgement envelope, the `§2`
  parameter lists, the cast cost/effect rules (`CARD_RULES.md`), or the
  `GAME_STATE.md` snapshot shape. Naming an implementing surface at the same
  granularity the existing `Swap` sentence already uses (L239–245) is permitted
  **only** if verified against source; restating the contract it implements is
  not.
- **Cite, do not restate.** Keep the existing citation idiom. Every document
  cited must already own the thing cited.
- **The dependent clause is the trap.** "The contract they implement is owned by
  `MATCH3_RULES.md` §2–§8 … and `GAME_STATE.md` §5.1" was written when "they"
  included board resolution; with the corrected antecedent it is false. Removing
  it is acceptable (the `Swap` paragraph above already cites the same owners for
  the same reason); correcting it is acceptable only with real owners. Leaving
  it untouched is not.
- **Watch the pronouns.** After the edit, re-read the whole paragraph and
  confirm no "they"/"neither"/"the remaining" survives with a referent that no
  longer exists.
- **Version lines follow their own established convention.** Prepend the new
  entry; extend the existing parenthetical rather than renumbering or
  restructuring the header. Do not rewrite the Version 1.3 entry's historical
  "remain not implemented" clause.
- **Comments and test rationales are not evidence.** `BattleHub.cs:489–491` and
  `ApiIntegrationTests.cs:261/284` still say `PetSkillCast` is unimplemented;
  they are wrong about the code that exists a few hundred lines below them.
  Report them; do not fix them, and do not let them talk you out of the
  correction.
- **Do not touch TASK-107's unfilled Completion Evidence.** Completed tasks are
  immutable (`TASK_LIFECYCLE.md` §3). Its empty evidence block is a finding, not
  a defect for this task.
- **Do not sweep.** Other documents contain unrelated stale cross-references
  (for example `SIGNALR_PROTOCOL.md` §4 item 7 refers to `GetBattleState` as
  "§6" where the section is §7). Report; do not fix (`AGENTS.md` §16).
- **Encoding safety.** Write this UTF-8 markdown file with a UTF-8-preserving
  writer. TASK-098's process note records typographic damage from a
  `Get-Content`/`Set-Content` round-trip; do not repeat it. Verify no BOM, no
  U+FFFD, and that em-dashes, en-dashes, arrows (`→`), and `§` survive.
- **The working tree is not clean.** `git status` at this task's creation
  already reports ~149 entries of pre-existing modification/rename state
  (including untracked `tasks/completed/TASK-143`/`TASK-144`). Record guard-file
  hashes at pickup rather than relying on `git status` alone to prove what this
  task changed.

---

## Testing / Validation Requirements

This is a documentation-only change. Validation exists to prove **the absence of
a code or contract change** and **the agreement of the corrected prose with the
implemented system** — not to exercise behavior. Per `core/validation.md` §2, a
LOW `DOCUMENTATION` task takes documentation validation plus review; the build,
unit, integration, and gameplay layers have no subject here and are explicitly
N/A.

### Required Verification

```text
[ ] Evidence re-verification — every row of both Current State tables
                    reproduced against source at pickup:
                      BattleHub.cs:859  (CardCast)         + :895 (PetSkillCast)
                      BattleHub.cs:698  (GetBattleState)
                      BattleStateService.ExecuteCardCastAsync /
                        ExecutePetSkillCastAsync / GetOwnedBattleStateAsync
                      GameServer.Domain/Cards/CardCastExecutor.cs
                      SignalRService.ts:618 / :645 / :691
                      GameRuntime.ts:203 / :359–391 / :600 / :612
                      BattleScene.ts:656 / :750–753 / :782
                      completed-task status fields and completion evidence for
                        TASK-107 / TASK-115 / TASK-120 / TASK-143 / TASK-144
[ ] Stale-claim sweep        — search ARCHITECTURE.md for
                    `not implemented`, `remain`, `unimplemented`,
                    `out of scope`, `not yet` and confirm zero residual false
                    claim about CardCast, PetSkillCast, or reconnect/resync in
                    the edited content span. Confirm the document-level Version
                    block's prior entries were left byte-unchanged.
[ ] No-over-correction check — confirm the corrected passage claims nothing
                    beyond the three capabilities, and that no sentence states
                    or implies the MVP, the result-presentation path, or the
                    runtime smoke path changed.
[ ] Pronoun/antecedent check — re-read the full §2.2.1 final paragraph; no
                    "they"/"neither"/"the remaining" without a live referent;
                    no contract ownership asserted against its owner.
[ ] Reference-resolution check — every §-reference inside §2.2.1 resolves:
                    SIGNALR_PROTOCOL.md §2, §4, §7; GAME_STATE.md §2, §2.0.5,
                    §4, §5.1; MATCH3_RULES.md §2–§8; ADR-008.
[ ] Cross-document agreement — SIGNALR_PROTOCOL.md §2/§7, GAME_STATE.md
                    §2.0.5/§5.3, REDIS_STATE.md §3/§5, and TDD.md §6 remain
                    unmodified and non-contradicting; no two documents disagree
                    about the implementation status of these capabilities.
[ ] Docs-render check       — the edited file re-read in full: code fences
                    balanced, blockquote/diagram rendering intact, section
                    numbering unbroken, cross-references intact.
[ ] Encoding check          — no BOM, zero U+FFFD, zero mojibake; em-dashes,
                    en-dashes, arrows, and `§` counts unchanged outside the
                    edited lines.
[ ] Unmodified-guard check  — hash comparison, pickup vs completion, over:
                    docs/00-overview/** , docs/01-game-design/** ,
                    docs/03-decisions/** , docs/02-technical/** (except
                    ARCHITECTURE.md), src/** , tests/** , tasks/completed/**
[ ] Changed-file scope      — the changed set is exactly
                    docs/02-technical/ARCHITECTURE.md plus this task file;
                    zero `src/` and zero `tests/` changes.
[ ] Unit tests              — N/A: no code exists in this task's subject.
[ ] Integration tests       — N/A: no boundary is implemented or altered.
[ ] Gameplay scenarios      — N/A: no gameplay rule is derived, changed, or
                    exercised (AGENTS.md §6).
```

### Key Edge Cases

- **Whole-sentence deletion.** The likeliest failure: it would drop the
  `SIGNALR_PROTOCOL.md` §2/§7 and `ADR-008` citations from §2.2.1 and lose the
  "runtime does not restate the contract" boundary.
- **Orphaned "neither"/"they".** Correcting statement 1–2 without touching
  statement 3 leaves a pronoun whose referent no longer exists, and a contract
  mapping that is now false.
- **Over-correction.** The section must not start claiming that the whole battle
  system, the result flow, or the runtime smoke path is complete.
- **Under-correction.** Leaving the `CardCast`/`PetSkillCast` clause because the
  phrases "Basic Card" and "Pet Skill Card" appear elsewhere is exactly the
  defect this task exists to remove.
- **The historical Version entry looks stale.** L7–L8 says the three
  capabilities "remain not implemented" inside the Version 1.3 record. It is
  history; the new entry supersedes it. Rewriting it is out of scope.
- **A stale source comment as false evidence.** `BattleHub.cs:489–491` and
  `ApiIntegrationTests.cs:261/284` claim `PetSkillCast` is unimplemented while
  the method exists. Source code, not comments, is the evidence.
- **TASK-107's empty Completion Evidence.** A reader may conclude `CardCast` was
  never finished; the `Status` field plus source and tests disprove that.
  Report the gap; do not edit the completed task.
- **Duplication pull.** §2.2.3 and `SIGNALR_PROTOCOL.md` §2/§4/§7 already
  describe the boundary and the mechanism. Cite them; do not restate them.
- **A "correction" that needs a contract reinterpreted rather than a status
  restated** — STOP per `AGENTS.md` §4.

---

## Stop Conditions

Universal `AGENTS.md` §20 and `.ai/README.md` §13 stops always apply.
Task-specific:

- **If the §2.2.1 sentence is no longer stale at pickup: STOP and report.** Do
  not manufacture a correction, and do not convert this task into a different
  cleanup task.
- **If the implementation status cannot be verified from the completed tasks and
  source: STOP and report** — the correction would be a guess.
- **If any verified-reality premise has changed** (e.g. a capability was
  reverted, a hub method removed, an action path withdrawn): **STOP and report**
  — the premises must be re-derived before editing.
- **If correcting the section requires an architectural decision** (a change to
  the runtime boundary, to rules 1–6, or to the ownership model): **STOP** — an
  ADR and an architecture workflow, not this task.
- **If correcting it requires changing an authoritative contract** — for
  example editing `SIGNALR_PROTOCOL.md`, `GAME_STATE.md`, `REDIS_STATE.md`,
  `CARD_RULES.md`, or `ADR-008` — **STOP** per `AGENTS.md` §4/§18.
- **If multiple documents conflict about implementation status:** **STOP** per
  `AGENTS.md` §4 and report both sources (file + section, both sides) rather
  than picking the easier side.
- **If correcting §2.2.1 requires rewriting a prior `**Version:**` entry rather
  than prepending a new one: STOP and report.** The status of historical
  version entries is a documentation-policy question, not a §2.2.1 status
  correction.
- **If the correction cannot be confined to §2.2.1's stale sentence, its
  dependent clause, and the Version line: STOP and report** — decompose per
  `tasks/README.md` §13 instead of expanding this task.
- **If a further stale implementation-status statement is discovered in
  `ARCHITECTURE.md` beyond the three enumerated:** report it and leave it
  (`AGENTS.md` §16), unless it is inside the same sentence/clause span.
- **If satisfying any criterion would require editing a completed task
  (including TASK-107's unfilled Completion Evidence), source code, or a test:
  STOP** and report instead.
- **If satisfying any criterion would require touching the `BATTLE_NOT_FOUND`
  result presentation or `runtime-smoke.mjs`: STOP** — those are separate
  follow-up concerns and must not be combined with this documentation task.
- **If the task exceeds 7 skills or crosses multiple uncoupled architectural
  boundaries: STOP and decompose.**

---

## Completion Evidence

<!--
  TO BE COMPLETED BY THE EXECUTING AGENT after reaching DONE.
  Keep concise and factual.
-->

### Changed Files

- `docs/02-technical/ARCHITECTURE.md` — **§2.2.1 only**, plus the
  `**Version:**` line. The stale implementation-status sentence now states that
  `CardCast`, `PetSkillCast`, and reconnect/resync snapshot recovery **are
  implemented**; the remaining-unimplemented framing ("The remaining …") became
  "The other …". The dependent contract-ownership clause was **corrected** (not
  removed) — see Clause-Resolution Record for the outcome chosen and why.
  `**Version:**` moved from 1.3 to 1.4 with a new entry.
- `tasks/backlog/TASK-145-synchronize-castcast-petskillcast-reconnect-implementation-status.md`
  — `Status` and Completion Evidence only; no objective, scope, criteria,
  stop-condition, or reference text changed. Moved to `tasks/completed/` at DONE
  (`TASK_LIFECYCLE.md` §3, §4).

### Validation Results

```text
Evidence re-verification      — PASS. Re-checked at pickup against source:
                                BattleHub.cs:698 GetBattleState, :859 CardCast,
                                :895 PetSkillCast;
                                SignalRService.ts:589 swap / :618 cardCast /
                                :645 petSkillCast / :691 getBattleState;
                                GameRuntime.ts:203 onReconnected →
                                recoverBattleState(), :408–:451 the
                                CardCast/PetSkillCast action kinds, :600/:612
                                the §7 snapshot request;
                                BattleScene.ts:657 Swap / :751 CardCast /
                                :783 PetSkillCast.
                                Premise delta: the task file documented the
                                BattleScene call sites as :750/:782; they are
                                :751/:783 (1-line shift). Every other premise
                                reproduced exactly, so no Stop Condition fired.
                                Evidence task statuses: TASK-107 COMPLETED,
                                TASK-115 / TASK-120 / TASK-143 / TASK-144 DONE.
Stale-claim sweep             — PASS. No residual "not implemented" /
                                "unimplemented" claim about CardCast,
                                PetSkillCast, or reconnect/resync remains in
                                ARCHITECTURE.md's body. The remaining hits are
                                the new Version 1.4 entry's description of the
                                correction and the historical Version 1.3/1.2
                                entry text, which is immutable history.
                                Removed-phrase check ("The contract they",
                                "neither is restated", "remaining client")
                                returns zero hits.
No-over-correction check      — PASS. The corrected passage claims nothing
                                beyond the three capabilities; nothing was
                                added about the MVP, result presentation, or
                                the runtime smoke path.
Pronoun/antecedent check      — PASS. No "they" / "neither" / "the remaining"
                                survives without a live referent; the closing
                                claim now reads "none of those contracts is
                                restated in the client runtime".
Reference-resolution check    — PASS. Newly cited `CARD_RULES.md` §2 (Basic
                                Cards) / §3 (Casting Rules) / §4 (Pet Skill
                                Card) and `GAME_STATE.md` §5.3 (Reconnect and
                                Snapshot Compatibility) exist and own what the
                                sentence claims; retained citations
                                `SIGNALR_PROTOCOL.md` §2 / §7 and ADR-008
                                resolve.
Cross-document agreement      — PASS. `SIGNALR_PROTOCOL.md` §2/§7,
                                `GAME_STATE.md` §5.1/§5.3, `CARD_RULES.md`, and
                                ADR-008 are unmodified and contain no status
                                claim contradicting the corrected sentence.
Architectural intent          — PASS. §2.2.1 rules 1–6, the coordination
                                diagram, the initial-state paragraph, the
                                board-delivery paragraph, and the Swap
                                paragraph are byte-unchanged; §2.2.3 and §5 are
                                byte-unchanged. `git diff` for the document is
                                exactly 2 hunks, 12 insertions / 6 deletions.
Docs-render check             — PASS. Code fences 24 (balanced), backticks 396
                                (balanced), section numbering unbroken,
                                cross-references intact.
Encoding check                — PASS. No BOM, zero U+FFFD, zero mojibake;
                                em-dashes 35, en-dashes 3, arrows 12, "§" 83;
                                the file remains uniformly CRLF (0 bare LF).
Unmodified-guard check        — PASS. See Unmodified-Guard Results.
Changed-file scope            — PASS. The changed set is exactly
                                `docs/02-technical/ARCHITECTURE.md` plus this
                                task file; zero `src/`, `tests/`, or other
                                `docs/` files.
Unit / integration / gameplay — N/A. Documentation-only change with no code
                                subject (core/validation.md §2: LOW →
                                documentation validation + review). No
                                documentation validator script exists in the
                                repository; the workflow's own consistency step
                                (documentation/documentation-change.md §1) was
                                performed as above, plus quality/review.md §1
                                items that a documentation-only change can
                                exercise.
```

### Clause-Resolution Record

```text
Stale sentence (was L246–248) corrected to:
  The other client → server gameplay methods (`CardCast`, `PetSkillCast` —
  `SIGNALR_PROTOCOL.md` §2) and reconnect/resync snapshot recovery
  (`SIGNALR_PROTOCOL.md` §7, ADR-008) are implemented. Their contracts are
  owned by `SIGNALR_PROTOCOL.md` §2 and §7, `CARD_RULES.md` §2–§4 (casting),
  and `GAME_STATE.md` §5.3 (snapshot compatibility); none of those contracts is
  restated in the client runtime.

Dependent clause (was L248–251):
  [ ] removed   [x] corrected (cite the owners actually used)
Reason:
  The clause's boundary claim ("… is not restated in the client runtime") is
  still true and still carries this section's client-runtime boundary, so the
  clause was corrected rather than removed. Its old owners described the Swap
  contract the preceding paragraph already cites (`MATCH3_RULES.md` §2–§8 board
  resolution; `GAME_STATE.md` §5.1 write-back), so they were replaced by the
  owners of the capabilities actually named: `SIGNALR_PROTOCOL.md` §2 (method
  surface) and §7 (reconnect/resync), `CARD_RULES.md` §2–§4 (casting), and
  `GAME_STATE.md` §5.3 (snapshot compatibility). No ownership was invented and
  no contract was restated.
```

### Version Line Record

```text
New entry added (prepended, newest-first, matching the file's convention):
  **Version:** 1.4 (§2.2.1 implementation-status wording synchronized — the
  `CardCast`/`PetSkillCast` clause and the reconnect/resync recovery clause are
  corrected from "not implemented yet" to implemented per TASK-107, TASK-115,
  TASK-120, TASK-143, and TASK-144, and the sentence's contract-ownership
  citation now names the documents that own those contracts. Status
  synchronization only: no wire member, contract, boundary, or rule is
  changed.)

Prior 1.1/1.2/1.3 entries: VERIFIED byte-identical. The text from
"1.3 (§2.2.1 implementation-status sentence corrected again" through
"client source file changed.))" was compared against HEAD with line endings
normalized: IDENTICAL, 1,204 characters on both sides. The Version 1.3 entry's
historical "remain not implemented" wording is preserved as history, as the
task requires.
```

### Unmodified-Guard Results

```text
src/**              276 files  IDENTICAL  (aggregate SHA-256 eb24ee4f…edd3)
tests/**            146 files  IDENTICAL  (aggregate SHA-256 6c501aa1…dc8c)
tasks/completed/**  150 files  IDENTICAL  (aggregate SHA-256 35bf4235…f57d3)
docs/02-technical/** (except ARCHITECTURE.md), docs/00-overview/**,
docs/01-game-design/**, docs/03-decisions/**:
                    UNCHANGED. ARCHITECTURE.md is the only file under docs/
                    written during this session (2026-10-03 20:22; every other
                    docs file ≤ 17:22), and `git status -- docs` shows exactly
                    the pre-existing modification/untracked set recorded at
                    pickup plus ARCHITECTURE.md.
ARCHITECTURE.md     6655EAD97C8139257E03F1637775AB420D5351EF466ADE4759C1F30AAF7BF475
                    → 2E10863F9C842C086CD278BBCF4D9A745FDE589ECE5A991829283198E9F9C76C
                    (pickup → completion; the two intended hunks only)
Changed-file set:   docs/02-technical/ARCHITECTURE.md + this task file.
Note: the working tree carried ~149 pre-existing dirty entries (including
untracked tasks/completed/TASK-143/TASK-144) before this task. Nothing was
reset, reverted, stashed, or cleaned, and no pre-existing change was touched.
```

### Lifecycle

```text
Execution:  direct pickup from tasks/backlog/. No separate active/ file was
            materialized because pickup, implementation, validation, and
            completion happened in one session. Gates passed in order:
            BACKLOG → READY (docs + scope confirmed) → IN PROGRESS →
            IN REVIEW → DONE.
File move:  tasks/backlog/ → tasks/completed/ (TASK_LIFECYCLE.md §3 DONE
            "File location: tasks/completed/", §4 terminal move).
Scope text: unchanged — no objective, scope, acceptance-criteria, stop-condition,
            or authoritative-reference text was altered while recording
            completion.
```

### Items Reported, Not Fixed (`AGENTS.md` §16)

- `src/backend/GameServer.Api/Hubs/BattleHub.cs:489–491` — the class comment
  still states `PetSkillCast` "is intentionally NOT implemented" although
  `BattleHub.cs:895` implements it. Impact: a reader could wrongly conclude the
  §2.2.1 correction is false. Source scope; not touched by this task; needs its
  own comment-synchronization task (TASK-101 precedent).
- `tests/backend/GameServer.Api.Tests/ApiIntegrationTests.cs:261–262` and `:284`
  — `BattleHub_ShouldNotRegisterGameplayMethods` still lists `"PetSkillCast"`
  as unregistered with the same stale rationale; the assertion passes only
  because the invocation supplies no arguments. Test scope; not touched.
- `tasks/completed/TASK-107-implement-basic-cardcast-server-path.md` — records
  `Status: COMPLETED` but its `## Completion Evidence` is still the unfilled
  template and its acceptance boxes are unchecked. Completed tasks are
  immutable (`TASK_LIFECYCLE.md` §3); reported, not edited.
- `docs/02-technical/ARCHITECTURE.md` Version block — the historical Version 1.3
  entry opens a parenthesis it never closes (5 "(" vs 4 ")" before this edit,
  6 vs 5 after). Pre-existing, inside immutable history; not fixed.
- `docs/02-technical/SIGNALR_PROTOCOL.md` §4 item 7 — refers to
  `GetBattleState` as "§6" while the method is documented under §7. Unrelated
  stale cross-reference; explicitly out of scope; not fixed and no task
  created.

### Server Authority & Scope Verification

- [x] Confirmed zero client-authoritative gameplay logic introduced — no code
      was written at all
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
- [x] Confirmed no API endpoint, SignalR method, Redis behavior, or database
      change
- [x] Confirmed no gameplay rule, formula, or timing changed
- [x] Confirmed no ADR created and no architectural decision recorded
- [x] Confirmed TASK-107 / TASK-115 / TASK-120 / TASK-143 / TASK-144 and all
      completed tasks unmodified
