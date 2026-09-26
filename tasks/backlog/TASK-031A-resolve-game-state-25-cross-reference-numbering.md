# TASK-031A — Resolve `GAME_STATE.md` §2.5 Cross-Reference Numbering Gap

---

## Metadata

```text
Task ID:           TASK-031A
Type:              DOCUMENTATION
Status:            BACKLOG
Risk:              LOW
Priority:          MEDIUM
Primary Agent:     orchestrator
Supporting Agents: backend, client, review
Workflow:          documentation/documentation-change.md
Skills:            discovery/documentation-discovery, quality/documentation-consistency, discovery/impact-analysis, quality/implementation-review
Dependencies:      TASK-031
```

---

## Objective

Close the `GAME_STATE.md` §2.5 cross-reference gap reported by TASK-031 by
renumbering every stale citation that pairs `GAME_STATE.md` with §2.5 to the
section that actually owns `PassiveProgress` (`GAME_STATE.md` §2.3 for
`PetState.PassiveProgress`; §2.4 / §2.4.2 for `BossState.PassiveProgress`) —
prose and code-comment text only — leaving `GAME_STATE.md`'s heading
numbering, all state/wire/test behavior, and `tasks/completed/*` unchanged,
unless the STOP condition in §Stop Conditions is triggered.

---

## Authoritative References

- `docs/02-technical/GAME_STATE.md` §2 — the heading list this task verifies
  against (`§2.0`, `§2.0.5`, `§2.1`–`§2.4`, `§2.6`, `§2.7` — no `§2.5`) and the
  `BattleState` tree, which has no root `PassiveProgress` member
- `docs/02-technical/GAME_STATE.md` §2.3 — owns `PetState.PassiveProgress`
  ("current count vs. threshold", `PASSIVE_RULES.md` §2); §2.4 / §2.4.2 own
  `BossState.PassiveProgress`
- `docs/01-game-design/PASSIVE_RULES.md` §2, §6 — owns the progress pair's
  semantics (`GAME_STATE.md` records where the value lives only)
- `docs/02-technical/SIGNALR_PROTOCOL.md` §4 item 13, §4.3 item 4 — the two
  documentation-side stale citations
- `.ai/workflow/documentation/documentation-change.md` §1 (update dependent
  references only when their wording is stale), §2 (no duplication), §3
  (canonical owner), §4 (review/completion still apply)
- `tasks/completed/TASK-031-wire-compatibility-verification.md` Remaining
  Issues #1 — immutable source report (never edited by this task)
- `AGENTS.md` §2 (source-of-truth hierarchy), §4 (conflict process), §15
  (tests), §16 (task discipline — report, don't fix inline), §17
  (documentation change rule), §20 (stop conditions)
- `docs/00-overview/MVP_SCOPE.md` §1 — documentation consistency only; no new
  system, rule, or content

---

## Scope

### In Scope

- Renumbering the 17 inventoried `GAME_STATE.md` §2.5 citations (see
  `Evidence & Determination` §3) across 9 files: 2 lines of documentation prose
  and 15 lines of `//`, `///`, or `/** */` comment text
- A repo-wide post-edit search proving no `GAME_STATE` + `§2.5` citation
  remains outside `tasks/`
- Recording the determination and its evidence in this task's Completion
  Evidence

### Out of Scope

- Adding a `## 2.5` heading, renumbering `§2.6`/`§2.7`, or otherwise editing
  `GAME_STATE.md`'s structure (see `Evidence & Determination` §4 — a design
  decision, not a documentation correction; triggers Stop Conditions if
  contradicted)
- Any change to state shape, wire payload, SignalR members, `petState`
  projection, RNG, Passive charge/threshold/reset behavior, Redis, PostgreSQL,
  or API contracts
- Any test logic or test assertion change — comment text only
- `tasks/completed/*` (including `TASK-013`, `TASK-014`, `TASK-027`,
  `TASK-031`) — immutable
- §2.5 citations whose antecedent is **not** `GAME_STATE.md`:
  `API_CONTRACTS.md` §2.5, `AGENTS.md` §2.5, `RELIC_RULES.md` §2.5,
  `ADR-007` §2.5, and the source comments citing them
  (`AuthController.cs`, `RelicLoadoutService.cs`, `BattleStartService.cs`,
  `IPlayerRepository.cs`, `Player.cs`, `PlayerConfiguration.cs`,
  `PlayerRepository.cs`, `BattleStateSerializer.cs`, `AuthPlayerOwnershipTests.cs`)
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2

---

## Current State

`GAME_STATE.md` has no `## 2.5` heading: its `§2` headings run
`§2.4 BossState` (line 1041) → `§2.6 RNG` (line 1125). `PassiveProgress` is
documented inside `§2.3 PetState` (tree lines 896 and 904) and
`§2.4` / `§2.4.2` for the Boss (lines 1052, 1083). Nevertheless,
`SIGNALR_PROTOCOL.md` §4 item 13 (line 963) and §4.3 item 4 (line 1107), plus
15 comment lines in backend and client sources, cite "§2.5" as the owner of
`PassiveProgress`. TASK-031 Remaining Issues #1 reported this as a numbering
gap, not a contract conflict, and explicitly deferred the fix to a
DOCUMENTATION task that must "either add the §2.5 heading or renumber the
citations."

**Exact ambiguity:** two materially different fixes are superficially
consistent with the report — (A) add/restore a `## 2.5` heading so the
existing citations become correct, or (B) renumber the stale citations to the
sections that actually own the value. The choice must be derived from document
structure and semantic content, not assumed.

---

## Evidence & Determination

### 1. Actual section structure of `GAME_STATE.md` §2

```text
## 2.0 Battle State Foundation        (L133)
## 2.0.5 Board Foundation State       (L235)
## 2.1 BoardState                     (L340)
## 2.2 Match / Combo Accounting       (L800)
## 2.3 PetState                       (L865)  ← PassiveProgress tree entry (L896), implemented list (L904)
## 2.4 BossState                      (L1041) ← PassiveProgress tree entry (L1052), §2.4.2 prose (L1083)
## 2.6 RNG                            (L1125)
## 2.7 Initial Board Generation       (L1210)
```

No `§2.5` heading exists. Nothing but `§2.4.5 Stunned` (ends L1123) precedes
`§2.6` — there is no orphaned content whose heading was lost.

### 2. Git history

- `git log -S"## 2.5" -- docs/02-technical/GAME_STATE.md` → no commits;
  `git log -G"^## 2\.5" -- docs/02-technical/GAME_STATE.md` → no commits.
  A `## 2.5` heading has **never existed** in any revision of the document.
- The `§2.4 → §2.6` gap is original, not the residue of a deletion: `## 2.6`
  and `## 2.7` were introduced at `9c38e15` with the gap already present.

### 3. Complete inventory of `GAME_STATE` §2.5 citations (17 locations / 9 files)

| # | File : Line | Current text | Replace with |
|---|---|---|---|
| 1 | `docs/02-technical/SIGNALR_PROTOCOL.md:963` | ``(`GAME_STATE.md` §2.3, §2.5)`` | ``(`GAME_STATE.md` §2.3)`` |
| 2 | `docs/02-technical/SIGNALR_PROTOCOL.md:1107` | `` `GAME_STATE.md` §2.5's `PassiveProgress` `` | `` `GAME_STATE.md` §2.3's `PassiveProgress` `` |
| 3 | `src/backend/GameServer.Api/Hubs/BattleHub.cs:152` | `§2.5's PassiveProgress` | `§2.3's PassiveProgress` |
| 4 | `src/backend/GameServer.Api/Hubs/BattleHub.cs:167` | `(GAME_STATE.md §2.5)` | `(GAME_STATE.md §2.3)` |
| 5 | `src/backend/GameServer.Domain/Battle/PetState.cs:102` | `(§2.3, §2.5)` | `(§2.3)` |
| 6 | `src/backend/GameServer.Domain/Battle/PetState.cs:237` | `(GAME_STATE.md §2.3, §2.5;` | `(GAME_STATE.md §2.3;` |
| 7 | `src/backend/GameServer.Domain/Battle/BossState.cs:21` | `(§2.4, §2.5)` | `(§2.4, §2.4.2)` |
| 8 | `src/backend/GameServer.Domain/Battle/BossState.cs:186` | `(GAME_STATE.md §2.4, §2.4.2, §2.5)` | `(GAME_STATE.md §2.4, §2.4.2)` |
| 9 | `src/backend/GameServer.Domain/Battle/Serialization/BattleStateJson.cs:363` | `§2.3, §2.5)` | `§2.3)` |
| 10 | `src/frontend/client/src/game/runtime/GameRuntime.ts:575` | `GAME_STATE.md §2.5` | `GAME_STATE.md §2.3` |
| 11 | `src/frontend/client/src/game/runtime/GameRuntimeEvents.ts:145` | `GAME_STATE.md §2.5` | `GAME_STATE.md §2.3` |
| 12 | `src/frontend/client/src/game/runtime/GameRuntimeEvents.ts:161` | `GAME_STATE.md §2.5's` | `GAME_STATE.md §2.3's` |
| 13 | `src/frontend/client/src/game/runtime/GameRuntimeEvents.ts:171` | `GAME_STATE.md §2.5` | `GAME_STATE.md §2.3` |
| 14 | `src/frontend/client/src/services/realtime/SignalRService.ts:106` | `GAME_STATE.md §2.5` | `GAME_STATE.md §2.3` |
| 15 | `src/frontend/client/src/services/realtime/SignalRService.ts:128` | `GAME_STATE.md §2.5's` | `GAME_STATE.md §2.3's` |
| 16 | `src/frontend/client/src/services/realtime/SignalRService.ts:140` | `GAME_STATE.md §2.5` | `GAME_STATE.md §2.3` |
| 17 | `src/frontend/client/tests/GameRuntime.test.ts:612` | `GAME_STATE.md §2.3, §2.5` | `GAME_STATE.md §2.3` |

`GAME_STATE.md` itself contains no self-citation of `§2.5`: its line 10
`§2.3–§2.5` belongs to the version-history note about `RELIC_RULES.md`
§2.3–§2.5 (antecedent `RELIC_RULES.md`, line 8) and must not be touched.

### 4. Authoritative-source reasoning

1. **Canonical owner.** Per `.ai/workflow/documentation/documentation-change.md`
   §3 and `AGENTS.md` §2, `GAME_STATE.md` owns its own section numbering. Its
   actual structure is the authority; a citation to a section that does not
   exist is stale, not a second authority.
2. **"Restore without changing content" is impossible.** No `§2.5` heading
   ever existed (§2 above) and no headingless content sits between `§2.4.5`
   and `§2.6` (§1 above). Option A therefore means *authoring a new section*,
   not restoring one.
3. **A root-level `§2.5 PassiveProgress` would contradict the state shape.**
   `GAME_STATE.md` §2's `BattleState` tree (lines 101–120) has no root
   `PassiveProgress` entry; the value is a nested member of `PetState` (§2.3)
   and `BossState` (§2.4). Placing it at root level would contradict the
   document's own contract, and it would be ambiguous which owner (Pet or
   Boss) the new section describes — a design ambiguity, i.e. `AGENTS.md` §20.
4. **No `GAME_STATE.md` content is missing.** The pair's semantics are owned
   by `PASSIVE_RULES.md` §2 (the §2.3 tree entry cites it directly); §2.3
   records where the value lives. Nothing that any rule needs is absent.
5. **The citations are self-inconsistent with the doc they cite.** They already
   pair the phantom `§2.5` with the real `§2.3` (`§2.3, §2.5`) — evidence the
   author knew §2.3 was the location and duplicated a non-existent second
   reference.
6. **Smallest correct change.** The workflow's §1 rule applies verbatim:
   dependent references are updated "only if a reference's wording is now
   stale — e.g. a section number changed". Renumbering 17 comment/prose lines
   is smaller, safer, and behavior-free compared with restructurizing the
   authoritative document to satisfy stale citations.
7. **Rejected alternative:** renumbering `§2.6→§2.5` and `§2.7→§2.6` to close
   the gap would break dozens of accurate `§2.6`/`§2.7` citations across docs,
   code, and tests, and would still leave `PassiveProgress` citations pointing
   at the RNG section.

### 5. Determination

**Option B — renumber the stale citations.** Option A (adding a `§2.5`
heading) is not a documentation correction: it requires inventing content and
structure, contradicts §2's tree, and is ambiguous between Pet and Boss —
`AGENTS.md` §17/§20 territory requiring explicit human approval first. If such
approval or an authoritative plan for a real `§2.5` section exists, the STOP
condition applies and this determination is superseded.

---

## Acceptance Criteria

- [x] All 17 inventoried locations contain the replacement text from
      `Evidence & Determination` §3 (no other text altered)
- [x] Repo-wide search for `GAME_STATE` + `§2.5` returns **zero** matches in
      `docs/`, `src/`, and `tests/` (matches under `tasks/completed/` are
      expected and untouched)
- [x] `GAME_STATE.md` heading list is byte-identical: still `§2.4` → `§2.6`,
      no `§2.5` added, `§2.6`/`§2.7` unrenumbered, line 10 untouched
- [x] `git diff` shows changes only in `docs/02-technical/SIGNALR_PROTOCOL.md`
      prose and `//`/`///`/`/** */` comment lines — zero statement, payload,
      assertion, or rule lines
- [x] Backend build and full backend test suite pass
      (`dotnet test src/backend/GameServer.sln`)
- [x] Client type-check/build and tests pass (`npm run build`,
      `npm run test:run` in `src/frontend/client/`)
- [x] All relevant tests pass at the required validation depth
      (`core/validation.md` §2 — LOW risk → full suite, no reduced depth)
- [x] Quality review checklist passes (`quality/review.md` §1, code items
      satisfied by the diff-shape check above)
- [x] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001)

---

## Affected Files & Areas

```text
[ ] src/backend/ (comment text only: BattleHub.cs, PetState.cs, BossState.cs, BattleStateJson.cs)
[ ] src/frontend/client/ (comment text only: GameRuntime.ts, GameRuntimeEvents.ts, SignalRService.ts, tests/GameRuntime.test.ts)
[ ] tests/ (no test files changed; comment inside src/frontend/client/tests/ only)
[x] docs/ (documentation updates: SIGNALR_PROTOCOL.md §4 item 13, §4.3 item 4; GAME_STATE.md verified unchanged)
```

---

## Implementation Notes

- Read `.ai/workflow/documentation/documentation-change.md` first; this task is
  a dependent-reference update, not a canonical-owner edit.
- Apply the mapping table in `Evidence & Determination` §3 exactly; do not
  "improve" surrounding wording, reflow paragraphs, or touch neighbouring
  citations.
- `SIGNALR_PROTOCOL.md` line 963's `§2.3, §2.5` becomes `§2.3` only — §4.3
  item 4 (line 1107) is the sole `§2.5` owner citation in that document.
- Backend edits are XML/line doc comments; C# must still compile and XML doc
  generation must stay valid. Client edits are TS doc comments only.
- The `BossState.cs:21` tree row (item 7) gains `§2.4.2` because that
  subsection is where Boss `PassiveProgress` is actually described.
- Verification commands (PowerShell environment — `rg` is unavailable):
  `git grep -n "2.5" -- docs src tests` and inspect every hit's antecedent;
  only `GAME_STATE`-antecedent hits must disappear.
- Do not run or modify any test as part of "fixing" a citation; a failing test
  caused by these edits would itself be a STOP (it would mean a test asserts
  the stale number — report per `AGENTS.md` §16).

---

## Testing Requirements

### Required Verification
```text
[ ] Unit tests         — none added; full backend suite executed to prove comment edits compile (dotnet test src/backend/GameServer.sln)
[ ] Integration tests  — N/A (no boundary, payload, or contract change)
[ ] Gameplay scenarios — N/A (no rule change — no Given/When/Then derives from an edited line)
[ ] Client validation   — npm run build (tsc) + npm run test:run in src/frontend/client/
[ ] Reference audit     — repo-wide search proves zero GAME_STATE+§2.5 matches outside tasks/
```

### Key Edge Cases
- `RELIC_RULES.md` §2.5 and `API_CONTRACTS.md` §2.5 citations must survive the
  audit untouched — filter by antecedent, never by the bare string `§2.5`
- `GAME_STATE.md` line 10's `§2.3–§2.5` (RELIC_RULES antecedent) must survive
- `tasks/completed/*` must be byte-identical after the task

---

## Stop Conditions

- Universal stop conditions in `AGENTS.md` §20 always apply
- **Numbering ambiguity:** if, at execution time, the `§2` numbering remains
  ambiguous after re-reading `GAME_STATE.md` §2 — specifically if any
  authoritative source (a document, an ADR, an approved task/design record, or
  a human decision) indicates that a `§2.5` section was *intended to exist*
  with its own content — **STOP** per `AGENTS.md` §4 and §20 (ambiguous
  requirement / conflict). Present options (A) author an approved `§2.5`
  section versus (B) renumber per this task, and wait for approval. Do not
  guess, and do not partially apply either option.
- If any required change would alter a rule, payload, test assertion, state
  shape, or a file under `tasks/completed/`: STOP per `AGENTS.md` §15 / §16
- If a test fails because it asserts the stale `§2.5` number: STOP and report
  (issue, location, impact, suggested follow-up) instead of editing the test's
  meaning
- If task exceeds 7 skills or crosses multiple uncoupled architectural
  boundaries: STOP & decompose

---

## Completion Evidence

<!-- TO BE COMPLETED BY THE EXECUTING AGENT after reaching DONE. -->

### Changed Files
- `docs/02-technical/SIGNALR_PROTOCOL.md` — §4 item 13 (L963): `(GAME_STATE.md §2.3, §2.5)` → `(GAME_STATE.md §2.3)`; §4.3 item 4 (L1107): `GAME_STATE.md §2.5's` → `GAME_STATE.md §2.3's` (inventory #1–#2)
- `src/backend/GameServer.Api/Hubs/BattleHub.cs` — L152 `GAME_STATE.md §2.5's` → `§2.3's`; L167 `(GAME_STATE.md §2.5)` → `(GAME_STATE.md §2.3)` (inventory #3–#4)
- `src/backend/GameServer.Domain/Battle/PetState.cs` — L102 `(§2.3, §2.5)` → `(§2.3)`; L237 `(GAME_STATE.md §2.3, §2.5;` → `(GAME_STATE.md §2.3;` (inventory #5–#6)
- `src/backend/GameServer.Domain/Battle/BossState.cs` — L21 `(§2.4, §2.5)` → `(§2.4, §2.4.2)`; L186 `(GAME_STATE.md §2.4, §2.4.2, §2.5)` → `(GAME_STATE.md §2.4, §2.4.2)` (inventory #7–#8)
- `src/backend/GameServer.Domain/Battle/Serialization/BattleStateJson.cs` — L363 `§2.3, §2.5)` → `§2.3)` (inventory #9)
- `src/frontend/client/src/game/runtime/GameRuntime.ts` — L575 `GAME_STATE.md §2.5` → `GAME_STATE.md §2.3` (inventory #10)
- `src/frontend/client/src/game/runtime/GameRuntimeEvents.ts` — L145, L161, L171 `GAME_STATE.md §2.5` → `GAME_STATE.md §2.3` (inventory #11–#13)
- `src/frontend/client/src/services/realtime/SignalRService.ts` — L106, L128, L140 `GAME_STATE.md §2.5` → `GAME_STATE.md §2.3` (inventory #14–#16)
- `src/frontend/client/tests/GameRuntime.test.ts` — L612 `GAME_STATE.md §2.3, §2.5` → `GAME_STATE.md §2.3` (inventory #17)

All 17 locations match `Evidence & Determination` §3 exactly; no other text altered
(17 insertions / 17 deletions across 9 files).

### Validation Results
- `dotnet test src/backend/GameServer.sln` — PASS (1304 tests, 0 failed: Domain 905, Application 199, Api 112, Infrastructure 88)
- `npm run build` (tsc + vite) in `src/frontend/client/` — PASS (exit 0)
- `npm run test:run` in `src/frontend/client/` — PASS (12 files, 188 tests, 0 failed)
- Reference audit — `GAME_STATE` + `§2.5` matches outside `tasks/`: **0**
  (`git grep -niE "GAME_STATE([^A-Za-z0-9]|\.md)+.{0,8}2\.5" -- docs src tests` → exit 1, no matches)
- Excluded-antecedent `§2.5` citations verified intact: `RELIC_RULES.md` (4), `API_CONTRACTS.md` (2), `DATABASE.md` (2); `GAME_STATE.md` L10's §2.3–§2.5 (RELIC_RULES antecedent) untouched
- Every `GAME_STATE` section cited in the 9 edited files resolves to an existing heading — no reference to a nonexistent section introduced
- `GAME_STATE.md` and `tasks/` — no diff (both byte-identical)

### Determination Record
- Determination applied: **B (renumber)**
- Evidence re-verified at execution: heading list unchanged (§2.0, §2.0.5, §2.1–§2.4, §2.6, §2.7 — still no `## 2.5`); no `## 2.5` in git history
  (`git log -S"## 2.5"` and `git log -G"^## 2\.5"` both return no commits); inventory count matched (17 locations / 9 files);
  `GAME_STATE.md` §2.4.2 "Boss Passive" prose (L1073–L1084) confirms Boss `PassiveProgress` ownership, so `BossState.cs` gains `§2.4.2`
- No STOP condition triggered: `PASSIVE_RULES.md` §2 remains the semantics owner and was not modified; no authoritative source indicates a `§2.5`
  was intended to exist

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic (no behavior touched)
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
