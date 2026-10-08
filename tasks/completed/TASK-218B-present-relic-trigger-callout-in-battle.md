# TASK-218B — Present the Relic Trigger Callout in Battle

**Retrospective reconstruction authored under TASK-224, Product Owner Decision 2 = A
(evidence-only).** This file records work already completed and delivered; it re-derives
nothing, invents no result, and edits no existing file. It is the completion record whose
absence TASK-218C records (`tasks/completed/TASK-218C-post-implementation-audit.md:504-506`, `:584`).

**Type:** COMPLETION RECORD — record creation only. No production code, test, contract
document, smoke script, migration, or historical task record is modified by it.
**Subject:** presenting a delivered `RelicTriggered` event as the battle scene's single
player-facing callout, naming the Relic from the delivered `GET /api/relics` read.
**Predecessor:** TASK-218A — Relic Trigger Presentation Audit (`Decision: PASS`,
`tasks/completed/TASK-218A-relic-trigger-presentation-audit.md:15-40`).
**Successor:** TASK-218C — post-implementation audit (`Decision: MODIFY`, test-only), and
the recommended `TASK-218D` (`TASK-218C-…md:619-620`).

## 1. Metadata

```text
Task ID:           TASK-218B
Type:              FEATURE — client presentation only (frontend; no backend,
                   contract, mechanic, or schema change). This file itself is a
                   DOCUMENTATION completion record, written retrospectively.
Status:            DONE (lifecycle reconstruction — see Evidence status below)
Risk:              LOW (two client source files + two client specs + one E2E phase)
Priority:          P2 (scheduled by TASK-211's audit §6 row 2, :992)
Primary Agent:     client
Supporting Agents: testing, review
Workflow:          development/feature.md
Skills:            N/A — reconstructed record; no skill procedure was executed here
Dependencies:      TASK-218A (audit, PASS), TASK-211 audit (scheduling), TASK-213
                   (decision, no contract change), TASK-224 (record authority under
                   Product Owner Decision 2 = A)
Evidence status:   PRIMARY for existence, SECOND-HAND for verification. The
                   implementation, its two specs and its E2E phase are present in
                   the working tree and were audited by TASK-218C — the ONLY
                   verification evidence that exists. TASK-218B's own test,
                   typecheck, build, E2E and review results are NOT FOUND.
Lifecycle note:    reconstructed retrospectively; the original task file was never
                   authored (recorded by TASK-218C §O-7).
```

Product Owner Decision 2 = A is on disk: `tasks/active/TASK-224-commit-step-and-task-209-record.md:335-371`
records `Decision 2 — Missing task records: A — Reconstruct records (evidence-only)`
(`:339`), lists this task among the six reconstructions (`:368`), and requires each record
to state only what code, tests, docs, history and citing records prove (`:373-375`).
Decision 1 = A adopted P-1…P-6 (`tasks/TASK_LIFECYCLE.md:304-309`, `:331-362`; P-3 at
`:345-349`).

## 2. Objective

Record TASK-218B: the battle scene presents a delivered `RelicTriggered` event as the
single selective callout it already used for Combo, cast, Boss, Passive and Match —
`RELIC: <delivered name>` at priority 2 — resolving `relicId` (the owned **instance**
identity) to the Relic's own `name` through the `GET /api/relics` read loaded via
`GameRuntimePort.getRelics()`, and showing nothing when that read cannot name the instance.

TASK-218A fixed the boundary: presentation-only, frontend-only, no contract change
(`TASK-218A-…md:15-40`, `:217-244`), reusing the existing single batch callout and the
existing collection-read + name-map pattern (`:254-279`, `:765-774`), carrying the
one-callout-per-batch constraint and Burning Curse's non-observability into the
implementation (`:300-322`, `:624-627`). It required two production files, two client
specs and one E2E phase (`:727-750`, `:788-802`, `:806-819`), forbade any mechanic,
contract, loadout, ownership, or server change (`:822-832`), and closed with
`Implementation: NOT DONE` (`:958`) — TASK-218B is that implementation landing.

## 3. Authoritative References

Cited, not restated. TASK-218A §1.1 (`:47-56`) enumerates the owning documents; those were
**not** re-read for this record, so the references below are inherited from that section:

- `docs/01-game-design/RELIC_RULES.md` §2.2 item 3 (owned instance identity), §4.2
  (equip-slot order), §6, §7 (the event's shape), §8.2 item 1 (no effect derived from prose)
- `docs/02-technical/SIGNALR_PROTOCOL.md` §3.2.23 items 1 and 5 — `{ type, relicId }` is
  final (`TASK-218A-…md:461-479`); `docs/02-technical/API_CONTRACTS.md` §5.4; plus
  `GAME_EVENTS.md` §2 and `GAME_STATE.md` §2.0.5.2 item 2; `ADR-018` item 10 and `ADR-001`;
  `AGENTS.md` §10; `docs/00-overview/MVP_SCOPE.md` §1
- `tasks/completed/TASK-218A-…md` and `TASK-218C-…md` (boundary; the only verification
  evidence), `tasks/completed/TASK-223-commit-worktree-ownership.md` §2/§6.2,
  `tasks/active/TASK-224-commit-step-and-task-209-record.md` §Product Owner Decisions,
  `tasks/TASK_LIFECYCLE.md` §6

## 4. Scope

### In Scope
- The `RelicTriggered` arm of `describeEventCallout` and its priority-2 slot
  (`BattleEventPresenter.ts:679-690`, `:595`).
- The `resolveRelicName` seam threaded through `describeEventCallout` and
  `selectBatchCallout` (`:653-657`, `:738-742`).
- `BattleScene`'s Relic definition map, loader, name resolver, `shutdown()` clear, and the
  third argument at the `selectBatchCallout` call site (`BattleScene.ts:397-411`,
  `:1284-1318`, `:2148-2161`, `:583-584`, `:1882-1886`).
- The two specs and the phase-5c-relic E2E pass (`BattleEventPresentation.test.ts`,
  `SceneLifecycle.test.ts`, `standalone-web-smoke.mjs`).

### Out of Scope
- Any Relic mechanic, trigger, condition, magnitude, effect, ownership, acquisition, or the
  3–5 loadout rule (`TASK-218A-…md:822-832`; `TASK-218C-…md:489-503`).
- `GameRuntimeEvents.ts` — `getRelics()` already existed there and TASK-218A §10.1 forbade
  touching the module (`TASK-218A-…md:752-754`; `TASK-218C-…md:501`).
- Any wire/contract/backend change; Burning Curse's non-observability (by design,
  `TASK-218C-…md:244-247`); Card-cost work, keeping TASK-212B deferred (`TASK-218A-…md:663-676`);
  any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2.

## 5. Evidence Base

| Evidence | Location | What it establishes |
|---|---|---|
| Relic colour, priority ladder, arm, seam | `BattleEventPresenter.ts:566-567`, `:571-599` (`:595`), `:626-638`, `:653-657`, `:679-690`, `:738-742`, `:748` | `null` name → no callout; otherwise `RELIC: ${name}` at priority 2; tie rule `<=` unchanged |
| Scene field / loader / call site / resolver | `BattleScene.ts:397-411`, `:440-442`, `:1284-1318`, `:1867-1889` (`:1885`), `:2148-2161`, `:583-584` | Loaded from `getRelics()`, rebuilt not merged, cleared in `shutdown()`, `?? null` fallback |
| Port pre-existed and is untouched | `GameRuntimeEvents.ts:538`; `git status --porcelain` lists no such path | The callout depends on the port; it added nothing to it |
| Audited change set | `TASK-218C-…md:55-61`, `:476-487` | The five files and their then-current counts; exactly these five, all uncommitted |
| Only delta in the event chain | `TASK-218C-…md:66-81` (`:79`) | The sole change is the third argument at `BattleScene.ts:1885` |
| Priority / tie; identity domain | `TASK-218C-…md:98-136`, `:142-188`, `:452-472` | Ladder preserved, exact `priority: 2`; owned **instance** both sides; a definition-keyed map is rejected |
| Name source / fail-closed | `TASK-218C-…md:214-228` | Delivered `name` only; deviation from TASK-218A §10.1 assessed correct |
| Batch behaviour and lifecycle | `TASK-218C-…md:253-283`, `:289-300` | One callout per batch; several triggers name the last; loaded, rebuilt, cleared on both teardown signals |
| Scope integrity; E2E quality | `TASK-218C-…md:384-449`, `:476-507` | No backend, protocol, API, schema, mechanic, loadout, ownership, or module change; allow-listed E2E form, independent rejection predicate, delivered-name expectation |
| Task tags in the artifacts | `BattleEventPresentation.test.ts:1469`; `SceneLifecycle.test.ts:3624`; `standalone-web-smoke.mjs:285`, `:519`, `:2534`, `:2607`, `:2714`, `:2726`, `:2739` | The work identifies itself as TASK-218B |

Read-only Git facts at reconstruction time (2026-10-09 00:04 +07:00): `HEAD` is `9b5c5fe`
(73 paths). `git show --name-only 9b5c5fe` lists all five declared paths; `git show
9b5c5fe:<BattleEventPresenter.ts>` matches `RELIC:|resolveRelicName|CALLOUT_PRIORITY_RELIC`
**zero** times and `git show 9b5c5fe:<BattleScene.ts>` matches
`relicDefinitions|loadRelicDefinitions|getRelics|relicDisplayName` **zero** times — the
committed baseline has no callout. `git status --porcelain` reports all five as `M`.

## 6. Declared file set

Per TASK-223 §6.2 **P-3** (`tasks/TASK_LIFECYCLE.md:345-349`; `TASK-223-…md:522-526`) a
record must declare an exact file set. No original TASK-218B declaration survives, so the
set is **derived**: TASK-218A §10.1/§10.5/§10.6 required it (`:725-754`, `:786-802`,
`:806-819`) and TASK-218C §1.1/§11 measured it delivered (`:55-61`, `:476-487`).

| Path | Evidence | Marker | Other tasks sharing the file |
|---|---|---|---|
| `src/frontend/client/src/game/scenes/BattleEventPresenter.ts` | Arm `:679-690`; ladder `:595`; seam `:653-657`, `:738-742`; `TASK-218C-…md:56` (52+/8−), `:99-107`, `:483` | `COMMITTED-IN-9b5c5fe` **+** `WORKING-TREE-MODIFIED` — the path is committed, but `9b5c5fe`'s content has no Relic callout; the arm exists only in the working tree | TASK-209 + TASK-210 (`TASK-223-…md:153`, row 17, category 3+6); TASK-219B, sequencing courtesy (`TASK-218A-…md:656-661`) |
| `src/frontend/client/src/game/scenes/BattleScene.ts` | Field `:397-411`; `create()` `:442`; `shutdown()` `:584`; loader `:1284-1318`; call site `:1882-1886`; resolver `:2148-2161`; `TASK-218C-…md:57` (79+/5−), `:253-263` | `COMMITTED-IN-9b5c5fe` **+** `WORKING-TREE-MODIFIED` — no relic-callout marker exists at `9b5c5fe` | TASK-209 + TASK-210 (`TASK-223-…md:152`, row 16); the TASK-219A implementation (`TASK-219A-…md:847`: "the TASK-219A edits are already in the tree"); TASK-219B (`TASK-218A-…md:656-661`) |
| `src/frontend/client/tests/BattleEventPresentation.test.ts` | `describe('TASK-218B …')` `:1469`; exact equality `:670`; batch relations `:907`, `:911-915`, `:920-924`, `:929-932`; scene level `:1500-1514`, `:1517-1537`, `:1551-1557`, `:1581-1631`; rejecting read `:1565-1579`; `TASK-218C-…md:58` (354+/22−), `:467` | `COMMITTED-IN-9b5c5fe` **+** `WORKING-TREE-MODIFIED` | TASK-209 + TASK-210 (`TASK-223-…md:160`, row 24, category 3+6) |
| `src/frontend/client/tests/SceneLifecycle.test.ts` | `describe('… (TASK-218B …)')` `:3624`; ten-name loop `:3642-3661`; lifecycle `:3663-3707`; failing-read test `:3709-3754`; source scan `:3756-3786`; `TASK-218C-…md:59` (179+/0− **at audit time**), `:349-378` | `COMMITTED-IN-9b5c5fe` **+** `WORKING-TREE-MODIFIED` | TASK-209 + TASK-210 + TASK-211 (`TASK-223-…md:168`, row 32, category 3+6); the TASK-219A implementation shares it (`TASK-219A-…md:461-462`, `:591`) |
| `src/frontend/client/scripts/standalone-web-smoke.mjs` | Allow-list `:271-305` (tag `:285`, `RELIC:` arm `:300`); rejection `:319-330`; fixture `:509-553` (`:519-526`); `RELIC_PROBE_SETUP` `:555-574`; phase 5c `:2519-2551`, `:2557-2572`, `:2606-2623`; phase 5c-relic `:2713-2851` (`:2714`, `:2726`); `TASK-218C-…md:60` (333+/6− **at audit time**), `:384-449` | `COMMITTED-IN-9b5c5fe` **+** `WORKING-TREE-MODIFIED` | TASK-209 + TASK-210 + TASK-211 + TASK-212A-1 (`TASK-223-…md:159`, row 23, category 3+6), plus TASK-226 / TASK-227 / TASK-228 hunks (`TASK-227-…md:157`, citing `TASK-226-…md:88`, `:290-292` and `TASK-228-…md:368-371`) |

Consulted, **not** in the declared set (marker `COMMITTED-IN-9b5c5fe`, unmodified):
`src/frontend/client/src/game/runtime/GameRuntimeEvents.ts` — supplies `getRelics()` at
`:538`; TASK-218A §10.1 and TASK-218C §11 both record that it was not to be and was not
touched (`TASK-218A-…md:752-754`; `TASK-218C-…md:501`).

**Marker summary:** 0 × `ABSENT`; 0 × `WORKING-TREE-UNTRACKED`; 5 ×
`COMMITTED-IN-9b5c5fe` **+** `WORKING-TREE-MODIFIED` (pre-existing committed paths whose
TASK-218B content is uncommitted).

Searched for and **NOT FOUND**: any TASK-218B task file (`tasks/**/TASK-218*` → only
`TASK-218A` and `TASK-218C`, both in `tasks/completed/`); any TASK-218B run, review, or
audit artifact; any `TASK-218D` file under `tasks/` at 2026-10-09 00:04 (+07:00).

Under P-2 all five paths are shared, so no TASK-218B-exclusive slice is committable; before
this record existed TASK-218B was a task id with no record, so committing its hunks would
have required the P-3 `unowned/pre-existing:` disclosure. No commit was created by
TASK-218B on the recorded evidence, and none by this record. TASK-224 remains the authority
for deferred integration (`TASK-218C-…md:504-506`).

## 7. Acceptance criteria

Derived from what TASK-218A required and TASK-218C verified. Verification status is stated
per criterion; TASK-218C is the **only** verification evidence and nothing below is
asserted as a TASK-218B run. TASK-218C's `Decision: MODIFY` (`:5`, `:17-48`) is not
upgraded to PASS anywhere here.

- [x] A delivered `RelicTriggered` produces a callout naming the Relic; the name comes only
  from the delivered read; no hard-coded name or id; an unresolved instance yields no
  callout rather than the raw identity. **Verified by TASK-218C** — §0 (`:17-21`), §2.2
  (`:117`), §4 (`:214-228`), §5 (`:236-238`). TASK-218C records the fail-closed `?? null` as
  a deliberate, correct deviation from TASK-218A §10.1's `?? relicId` sketch, matching the
  `BossSkillCast` precedent.
- [x] The priority slot is chosen (exactly 2), the existing ladder is not renumbered, and a
  batch still yields at most one callout. **Verified by TASK-218C** — §2 (`:98-136`), §7
  (`:289-300`).
- [x] Lifecycle: loaded from `create()`, rebuilt not merged, cleared on `SHUTDOWN` and
  `DESTROY`, no stale data across reuse. **Verified by TASK-218C** — §6 (`:253-263`).
- [x] No backend, protocol, API, schema, mechanic, loadout, ownership, or new-module change.
  **Verified by TASK-218C** — §11 (`:476-507`).
- [x] `BattleEventPresentation.test.ts` was edited as TASK-218A §10.5 required — the
  `RelicTriggered` row left the "no callout" table and positive coverage replaced it.
  **Verified by TASK-218C** — §1.1 (`:58`), §10 (`:467`); the table now lists GemMatched /
  CascadeCreated / DamageCalculated / DamageDealt / DamageTaken / PowerChanged
  (`BattleEventPresentation.test.ts:642-649`).
- [ ] The new Relic lifecycle test is non-vacuous. **NOT SATISFIED as TASK-218C recorded
  it** — D-1, §8.4 (`:349-378`), §13.1 (`:531-541`): the failing-`/api/relics` test never
  fails the read, so "the test passes — but it is not testing a failed read" (`:366-370`).
- [~] The phase-5c-relic E2E fixture and widened assertions are present. **Verified
  statically only** — `TASK-218C-…md:384-449`; TASK-218C executed no E2E (§15 `:640-646`).
- [ ] TASK-218B's own tests / typecheck / build passed at the required depth. **NOT FOUND**
  — no TASK-218B record, log, or report exists. TASK-218C's own suite result (21 files, 886
  tests passing; `tsc --noEmit` exit 0) is its post-implementation run (`:469-471`,
  `:632-637`), not TASK-218B's evidence.
- [ ] A live E2E run of phase 5c-relic passed, and quality review (`quality/review.md` §1)
  for TASK-218B. **NOT RUN / NOT FOUND** — `TASK-218C-…md:640-646`; no TASK-218B review
  artifact exists (TASK-218C is a post-implementation audit, not TASK-218B's review).

## 8. Tests / Validation

Only what a citing record states is repeated; no result is inferred or fabricated.

- **`src/frontend/client/tests/BattleEventPresentation.test.ts`** — the primary spec; its
  `describe` names the task (`:1469`). Presenter level: exact
  `toEqual({ message: 'RELIC: Berserker Core', color: '#7dd3fc', priority: 2 })` (`:670`);
  `not.toContain('relicinst_a1')` / `not.toContain('relic-')` (`:674-675`); unknown and
  empty resolvers yield no callout (`:688-706`); three triggers in one batch name the last
  (`:721-737`). Scene level: rendered text carries the name and not the identity
  (`:1500-1514`, `:1517-1537`); an unowned instance names nothing (`:1551-1557`); a throwing
  `getRelics` names nothing (`:1565-1579`, harness `:73-75`, `:124-129`); three triggers
  yield exactly one line (`:1581-1631`). Batch relations: Combo beats Relic (`:907`), Relic
  beats Boss and Match (`:911-915`), Relic ties a cast with the later equal winning
  (`:920-924`), an unresolved Relic loses to Combo (`:929-932`).
  **TASK-218C's verdict, quoted:** the central assertion is "exact equality against the
  delivered name, not a substring or a presence check, so it cannot be satisfied by a raw
  identity" (`TASK-218C-…md:318-319`); the three-trigger test is "**non-vacuous** on the
  mechanism" (`:297-300`); the planted-leak protection is "genuine and demonstrated twice"
  (`:321-339`); the tests **would** fail if `relicId` were displayed instead of the name
  (`:306-319`).
- **`src/frontend/client/tests/SceneLifecycle.test.ts`** — `describe('BattleScene — Relic
  definition lookup (TASK-218B, …)')` (`:3624`): all ten provisioned Relics resolve to their
  delivered names and the definition-looking spelling is not a key (`:3642-3661`);
  load/clear across `SHUTDOWN`, reuse and `DESTROY` (`:3663-3686`); rebuild-not-merge
  (`:3688-3707`); a comment-stripped source scan forbidding every canonical id/name,
  `relicinst_`, `.trigger`, `.effectDefinition`, `conditionType`, while requiring
  `relicDefinitions.get(` and `runtime.getRelics(` (`:3756-3786`).
- **TASK-218C's verdict on that file, quoted and not upgraded:** D-1 is a "DEFECT (low,
  test-only)" at `SceneLifecycle.test.ts:3701` — the test "never fails the read … so it
  passes for a reason other than the one its name states. `relicsFailure` is a dead argument
  member." (`TASK-218C-…md:28-35`); "The test passes — but it is not testing a failed read."
  (`:366-370`). The rejected-read behaviour is genuinely covered at
  `BattleEventPresentation.test.ts:1565-1579` (`:372-375`).
- **E2E — static review only.** `standalone-web-smoke.mjs` allow-lists the form
  (`/^RELIC: \S/`, `:300`); the injected batch gains two `RelicTriggered` events from the
  scene's own loaded map (`:519-526`, `:555-574`); phase 5c-relic asserts exact equality
  against the delivered name plus `!callouts[0].includes(relic.relicId)` (`:2757-2768`), an
  independent identity-rejection record (`:2773-2777`), and an unresolvable-instance probe
  that pre-seeds `MATCH`, asserts the row is untouched, and scans every scene Text for the
  identity (`:2788-2841`). TASK-218C records these as strong and not broadened to pass
  (`:387-448`).
- **NOT executed for this record.** No test, typecheck, build, or E2E run was performed
  while authoring it; no run result is claimed for TASK-218B.
- **Observed post-audit tree change, unattributed.** TASK-218C measured
  `SceneLifecycle.test.ts` at 179+/0− with D-1 at `:3701` (`:59`, `:349-378`). At
  2026-10-09 00:04 (+07:00) the working tree holds it at 212+/1− vs `9b5c5fe`, declares
  `relicsFailure` (`:106`), destructures it (`:127`), and the port throws it (`:345-346`),
  so D-1's mechanism is no longer reproducible there. No record under `tasks/` attributes
  this delta (`TASK-218D`: **NOT FOUND**; `TASK-224-…md:454-455` lists six reconstructions
  and does not include it). It is **not** TASK-218B's evidence and does not upgrade D-1.

## 9. Remaining Issues

- **D-1 stands as TASK-218C recorded it** at this record's boundary: the Relic lifecycle
  test's failing-read case passes for a reason other than the one its name states
  (`TASK-218C-…md:531-541`). Low severity, test-only; no production behaviour or required
  coverage is missing (`:372-375`).
- **Recommended follow-up — `TASK-218D`**, "make the TASK-218B Relic lifecycle test
  non-vacuous and restore the combo-callout record's descriptive integrity (test-only)"
  (`TASK-218C-…md:619-620`): honour `relicsFailure` in the `SceneLifecycle.test.ts` harness,
  or delete the dead parameter and re-point the test; optionally rename
  `phase5c.comboCalloutUsesTheDeliveredValue` (`:593-609`). Its boundary is test files only
  (`:611-617`). **NOT FOUND** under `tasks/` at the time of writing.
- **TASK-218C's observations O-1…O-7 remain open and unmodified** (`:543-585`): O-1
  case-sensitive rejection arms (`:546-552`); O-2 the form-only combo-callout record
  (`:554-557`); O-3 the one-callout record proves a value, not an invocation count
  (`:559-565`, pre-existing); O-4 fixture ids come from the scene's own map (`:567-570`);
  O-5 two overstating E2E comments (`:572-577`); O-6 an empty delivered name would render
  `RELIC: ` (`:579-582`); O-7 the missing record, closed by this file for the record gap
  only (`:584`).
- **The phase-6d `CAST_CONTROL_CAPTIONS` race is untouched and separate**
  (`TASK-218C-…md:510-525`), and TASK-218C excludes it from TASK-218D (`:621-622`).
- **TASK-218A's F-1** — Relic content lives only in the in-process `_relicConfiguration`, so
  a process that did not create the battle resolves no Relic — stays a mechanic/recovery
  contract question with its own task (`TASK-218A-…md:686-711`; excluded from TASK-218D at
  `TASK-218C-…md:621-622`).
- **TASK-218A's gaps G-2 and G-3 remain open** (no resolver-level unit test for six
  TASK-184 Relics; the recovery suites assert nothing about Relics, `TASK-218A-…md:553-563`).
  G-1 and G-4 were addressed by TASK-218B's coverage (`TASK-218C-…md:295`); G-6 (Burning
  Curse) remains by design (`:244-247`).
- **The `rewardLoadRun`-style run guard TASK-218A §3.5 recommended was not added**;
  TASK-218C assessed it and found a pre-existing `BattleScene`-wide pattern, with the new
  loader safer than its siblings because its continuation writes only to a `Map`
  (`TASK-218C-…md:265-283`). Recorded, not fixed.
- **Record debt.** TASK-218B's own validation evidence never existed; this reconstruction
  cannot cure that and must not be read as creating a passing test or E2E result. The P-3
  exposure (a task id with no record, `TASK_LIFECYCLE.md:345-349`) is closed by this file's
  existence; integration/commit handling remains with TASK-224 (`TASK-218C-…md:504-506`).

## 10. Revision History

**Revision 1 — retrospective completion record authored (evidence-only).** Created under
the TASK-224 / Decision 2 = A directive
(`tasks/active/TASK-224-commit-step-and-task-209-record.md:339`, `:368`) to close
TASK-218B's record debt, reported by TASK-218C §O-7 and §11. Provenance: TASK-218C (the
only verification evidence), TASK-218A (the implementation boundary and acceptance
expectations), TASK-211's audit §2.1/§3 Part D/§6 row 2, TASK-213's decision §6 row 3,
TASK-223 §2/§6.2, TASK-219A's audit §9.2 (shared `BattleScene.ts`), TASK-227's record
(shared smoke script), the working-tree sources and specs, and read-only Git facts at
`HEAD = 9b5c5fe`. No existing file was edited; no Git write command was run; no
acceptance, test, E2E, review, commit hash, or date was asserted that no artifact states.
