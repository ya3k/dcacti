# TASK-227 — Route the Phase 6b / Phase 6d E2E Cast Waits Through a Settled-Acknowledgement Predicate (V-1)

**Retrospective reconstruction authored under TASK-224, Product Owner Decision 2 = A
(evidence-only).** This file records work already completed and delivered; it re-derives
nothing, invents no result, and edits no existing file. It is the completion record for
TASK-227, whose absence TASK-228's record reports as pre-existing record debt
(`tasks/completed/TASK-228-tighten-phase6-first-cast-wait.md:329-341`).

**Type:** COMPLETION RECORD — record creation only. No production code, test, contract
document, smoke script, migration, or historical task record is modified by it.
**Subject:** V-1 — the Phase 6b/6d waits resolved on a caption that had merely *changed*,
including the scene's transient `… in flight…` transport caption.
**Predecessor:** TASK-226 — `tasks/completed/TASK-226-reconnect-group-rejoin.md`.
**Successor:** TASK-228 — `tasks/completed/TASK-228-tighten-phase6-first-cast-wait.md:8`.

---

## 1. Metadata

```text
Task ID:           TASK-227
Type:              DOCUMENTATION (completion record; retrospective reconstruction — it
                   changes no code, test, contract, or prior record)
Status:            DONE (lifecycle reconstruction — see Evidence status)
Risk:              LOW (record creation only)
Priority:          MEDIUM (verification-artifact tightening; no production behavior)
Primary Agent:     client
Supporting Agents: testing, review
Workflow:          documentation/documentation-change.md
Skills:            N/A — reconstructed record; no skill procedure executed by this task
Dependencies:      TASK-219A (V-1 origin), TASK-226 (predecessor), TASK-224 (record authority)
Evidence status:   PARTIAL — the implementation is present in the working tree and is
                   asserted by TASK-228's record; TASK-227's OWN validation, E2E run and
                   review evidence are NOT FOUND anywhere under the repository (see §9).
Lifecycle note:    reconstructed retrospectively; the original task file was never authored
                   (recorded as record debt by TASK-228's record).
```

Product Owner Decision 1 = A is on disk and owns the commit rule: `tasks/TASK_LIFECYCLE.md:3-4`
("adopted by Product Owner Decision 1 = A under TASK-224") and its §6 (`:304-362`), adopting
TASK-223 §6.2 P-1…P-6 (`tasks/completed/TASK-223-commit-worktree-ownership.md:511-538`).
**Decision 2 = A ("retrospective completion records, evidence-only") is now on disk.** It was
authored in TASK-224's Revision 2 while this record was being written, so the repo-wide search that
originally returned NOT FOUND predates it:
`tasks/active/TASK-224-commit-step-and-task-209-record.md:335-392` (§Product Owner Decisions —
"Decision 2 — Missing task records: A — Reconstruct records (evidence-only)" at `:339`, and
"### Decision 2 = A — the reconstruction set" at `:359`; the same section records Decision 1 = A at
`:338`/`:342`). `tasks/TASK_LIFECYCLE.md` §6 records Decision 1 only and
`.ai/workflow/core/completion.md` §1/§2 reference §6 — neither restates Decision 2, which is
correct: TASK-224 owns it. The evidence-only instruction is also stated in TASK-224 §In Scope items
2–3 — reconstruct "from the code, tests, migrations and the citing records", and where verification
evidence does not exist "say so explicitly rather than asserting a result"
(`tasks/active/TASK-224-commit-step-and-task-209-record.md:105-114`; that manifest moved from
`tasks/backlog/` to `tasks/active/` while this record was written).

---

## 2. Objective

Record V-1: the routing of the standalone-web E2E **Phase 6b** and **Phase 6d** cast waits
through a settled-acknowledgement predicate (`isSettledCastAcknowledgement`) combined with a
stale-caption guard, so a caption that merely *changed* — including the transient
`CardCast <id> in flight…` / `PetSkillCast in flight…` line — no longer resolves either wait.

V-1 was a **verification-artifact defect of the E2E script**, not a production defect:
TASK-219A classifies it as "a defect of the **E2E script** added by this work, not of
production behaviour and not of the contract" and names as the smallest fix "one predicate
change (ignore the in-flight caption, or require `accepted|rejected` inside the wait)"
(`TASK-219A-post-implementation-audit.md:639-643`), assigning it to the re-scoped TASK-219B
(`:846`); TASK-218A still carried it as TASK-219B's open scope over the same files
(`tasks/completed/TASK-218A-relic-trigger-presentation-audit.md:658-661`). TASK-227 is that fix
landing. TASK-226 explicitly did **not** take it — the phase-6d caption race is in TASK-226 §8's
exclusion list (`TASK-226-…md:247`).

---

## 3. Authoritative References

- `tasks/completed/TASK-228-tighten-phase6-first-cast-wait.md` — §3 (`:69-78`: the helper, and
  "TASK-227 (V-1) routed the Phase 6b and Phase 6d waits through it"), §7 (`:252-258`), §9.1
  (`:299-314`), §9.3 (`:329-341`: record debt), §10 (`:353-357`), §11 (`:366-375`)
- `tasks/completed/TASK-219A-post-implementation-audit.md` §6.2 (`:625-651`), §9 (`:846`),
  §10 (`:862-867`), §11 (`:964`)
- `tasks/completed/TASK-226-reconnect-group-rejoin.md` §8 (`:236-255`), §10 (`:284-298`)
- `tasks/completed/TASK-223-commit-worktree-ownership.md` §6.2 (`:511-538`) — P-2, P-3;
  `tasks/TASK_LIFECYCLE.md` §6 (`:304-399`) — the adopted rule and §6.4's `9b5c5fe` record
- `src/frontend/client/scripts/standalone-web-smoke.mjs` — `:237-249`, `:251-269`, `:2871-2883`,
  `:2936-2947`, `:3059-3070`; `src/frontend/client/tests/CastFeedbackWaitPredicate.test.ts`
- `SIGNALR_PROTOCOL.md` §2, §5 and `BattleScene.renderCastStatus` — the caption ladder the
  predicate is defined against (cited by the helper's own doc comment, smoke `:252-259`; not
  restated here)

---

## 4. Scope

### In Scope
- The **Phase 6b** wait `secondCastText = await waitForCondition(...)`: settled acknowledgement
  **and** rejection of the stale first-cast caption.
- The **Phase 6d** wait `skillFeedback = await waitForCondition(...)`: settled acknowledgement
  **and** rejection of the stale Phase 6b caption.
- The shared `isSettledCastAcknowledgement(text)` helper those two waits route through, and the
  guard assertion(s) covering them.

### Out of Scope
- The **Phase 6** first-cast wait (TASK-228's V-2 slice, `TASK-228-…md:84-88`, `:255`); TASK-228
  states it "does not create" TASK-227's record and "does not modify TASK-227 in any way"
  (`:338-341`).
- R-1 (no retry loop for the Phase 6 first cast under `isInputLocked`) — `TASK-228-…md:345-351`.
- Any production caption, scene, runtime, hub, or protocol behavior: TASK-228 §7 records
  `BattleScene.ts`, `GameRuntime.ts`, `SignalRService.ts`, `BattleHub.cs`, `SIGNALR_PROTOCOL.md`,
  `GAME_RULES.md`, GDD and `MVP_SCOPE.md` as unchanged, and §10 states "No production change was
  necessary or authorized" (`:359-362`).
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2.

---

## 5. Evidence Base

| Evidence | Location | Establishes |
|---|---|---|
| Helper + doc comment ending "(TASK-227)" | smoke `:251-269` (tag `:265`) | The helper is TASK-227's artifact |
| Phase 6b wait, in-hunk `// TASK-227:` tag | smoke `:2936-2947` (`:2940` tag, `:2943` predicate) | Phase 6b routes through the helper **plus** `text !== castFeedback` |
| Phase 6d wait, in-hunk `// TASK-227:` tag | smoke `:3059-3070` (`:3063` tag, `:3065-3066` predicate) | Phase 6d routes through the helper **plus** `text !== secondCastText` |
| Pre-change baseline | `git show 9b5c5fe:src/frontend/client/scripts/standalone-web-smoke.mjs` → `:2589` `return text && text !== castFeedback ? text : false;`; `:2709` `return text.length > 0 && text !== secondCastText ? text : false;` | The committed baseline has the stale guard but **no** settled gate; `git grep -n isSettledCastAcknowledgement 9b5c5fe -- <that path>` → no match (exit 1), so the helper is working-tree-only |
| Routing table | `TASK-228-…md:254-258` | "Phase 6b … → TASK-227", "Phase 6d … → TASK-227" |
| V-1 origin / fix shape / re-scope | `TASK-219A-…md:627-644`, `:846`, `:863-865`; `TASK-218A-…md:658-661` | Cause, classification, prescribed fix, and where it was carried |
| Shared-file statements | `TASK-228-…md:299-314`, `:366-375`; `TASK-226-…md:284-298` | The smoke script is shared; TASK-227's hunks are inseparable from TASK-228's |
| Test guard | `CastFeedbackWaitPredicate.test.ts:6`, `:145`, `:173-207`, `:209-237`, `:250-277` | Joint TASK-227/TASK-228 guard (see §7) |

`git status --porcelain` at writing lists `M src/frontend/client/scripts/standalone-web-smoke.mjs`
and `?? src/frontend/client/tests/CastFeedbackWaitPredicate.test.ts`; `git show --name-only 9b5c5fe`
lists the smoke script among its 73 paths and not the guard file, and
`git ls-files --error-unmatch` on the guard path exits 1 (unknown to git).

---

## 6. Declared file set

Per TASK-223 §6.2 P-3 (`TASK-223-…md:522-526`) a record declares an exact file set.

| Path | Evidence | Marker | Other tasks sharing the file |
|---|---|---|---|
| `src/frontend/client/scripts/standalone-web-smoke.mjs` | Helper + `(TASK-227)` tag `:251-269`; in-hunk tags `:2940`, `:3063`; `TASK-228-…md:75`, `:254-258` | `COMMITTED-IN-9b5c5fe` **and** `WORKING-TREE-MODIFIED` — the file is in commit `9b5c5fe`; TASK-227's hunks are uncommitted | TASK-226 (`TASK-226-…md:88`, `:290-292`), TASK-228 (`TASK-228-…md:368-371`), and TASK-209 / TASK-210 / TASK-211 / TASK-212A-1 (`tasks/active/TASK-224-…md:202-205`) |
| `src/frontend/client/tests/CastFeedbackWaitPredicate.test.ts` | `TASK-228-…md:114-118` ("**Modified:**"), `:370-373` ("an untracked new file that now contains both TASK-227's and TASK-228's guard assertions"); header `:6`; `describe` title `:145` | `WORKING-TREE-UNTRACKED` | TASK-228 (its V-2 assertions in the same file) |

No third path declares TASK-227. **NOT FOUND**: any TASK-227 task file (`tasks/**/TASK-227*`
glob → no files), any TASK-227 audit/review record, any other artifact carrying a `TASK-227`
tag (repo-wide `*.md` search for `TASK-227` returns only `TASK-218A:658`, `TASK-219A:846`,
`:863`, and TASK-228's record).

**Marker summary:** 0 × `ABSENT`; 1 × `COMMITTED-IN-9b5c5fe` + `WORKING-TREE-MODIFIED`;
1 × `WORKING-TREE-UNTRACKED`.

Under P-2/P-3 this set cannot be a TASK-227-exclusive slice: the smoke script is shared, and
before this record existed TASK-227 was an id with no record, so committing its hunks under that
id would have needed the P-3 `unowned/pre-existing:` disclosure. `9b5c5fe` itself predates the
policy — recorded as a pre-adoption batch commit complying with none of P-2…P-4
(`tasks/TASK_LIFECYCLE.md:382-399`). No commit was created by TASK-227 on the recorded evidence,
and none is created by this record; TASK-224 owns deferred integration (`TASK-228-…md:374-375`).

---

## 7. TASK-227 vs TASK-228 separation

Both tasks' changes live in the same two files and neither is committed — TASK-228's record says
exactly this:

> "TASK-227 and TASK-228 remain uncommitted in the shared `standalone-web-smoke.mjs` working
> tree. Attribution therefore rests on: task tags in individual hunks; modification-time
> isolation; direct diff inspection. The evidence is consistent and unambiguous, but it is
> weaker than the baseline separation a committed TASK-227 state would provide."
> — `TASK-228-…md:305-314`

| Hunk / assertion | Owner | Basis |
|---|---|---|
| smoke `:251-269` — `isSettledCastAcknowledgement` + doc comment | **TASK-227** | In-hunk tag `(TASK-227)` `:265`; absent from `9b5c5fe`; TASK-228 §4: "No new acknowledgement predicate was introduced" (`:106-108`) |
| smoke `:2936-2947` — Phase 6b wait | **TASK-227** | In-hunk tag `:2940`; routing table `TASK-228-…md:256`; TASK-228 lists "Phase 6b and Phase 6d V-1 logic" as unchanged by it (`:103`) |
| smoke `:3059-3070` — Phase 6d wait | **TASK-227** | In-hunk tag `:3063`; routing table `TASK-228-…md:257` |
| smoke `:2871-2883` — Phase 6 first-cast wait (V-2) | **TASK-228** | Exact predicate `TASK-228-…md:84-88`; "Only the Phase 6 first-cast wait was changed" (`:91`); in-hunk tag `(TASK-228, V-2)` `:2877` |
| guard `:250-277` (V-2 test) and `:244` (four-occurrence count) | **TASK-228** | `TASK-228-…md:126-139` lists these as what the guard "now verifies"; the fourth call site exists only after V-2 is wired |
| guard `:173-207`, `:209-237` (Phase 6b/6d tests, stale guards `text !== castFeedback`, `text !== secondCastText`) | **TASK-227** assertions — **file boundary unprovable** | They exercise the V-1 predicates and the `describe` title is "(TASK-227)" (`:145`), but the file is untracked with no committed baseline, so no diff can prove which assertions TASK-227 wrote |
| guard `:279-287` — "leaves the production captions untouched" | **Unprovable** | Shared untracked file; no task tag |

**Proven:** the *content* boundary in the smoke script (whose predicate is whose), from the
in-hunk tags plus TASK-228's own statements, corroborated by the `9b5c5fe` baseline still
holding guard-only predicates (`:2589`, `:2709`) and no helper.

**Not provable:** (a) any line-level split *inside* the guard file — no recorded pre-TASK-228
version of it exists; (b) that no third task touched the same two predicates between TASK-219A
and TASK-227 — no intermediate snapshot exists; (c) TASK-226's part in the same file except by
citation (`TASK-226-…md:88`, `:290-292`), since the whole file is one uncommitted delta against
`9b5c5fe` (660 insertions / 10 deletions, mixing several tasks).

---

## 8. Acceptance criteria

Derived from V-1's prescribed fix (`TASK-219A-…md:640-643`) and the delivered behavior.
Verification status is stated per criterion; no passing run is asserted.

- [x] The Phase 6b wait no longer resolves on a caption that merely changed. **Structural** —
  `:2943` is `text !== castFeedback && isSettledCastAcknowledgement(text) ? text : false`;
  baseline `9b5c5fe:2589` had no settled gate.
- [x] The Phase 6d wait no longer resolves on the transient `in flight…` caption. **Structural** —
  `:3065-3066` gates `changedCaption` on `isSettledCastAcknowledgement(text)`; baseline
  `9b5c5fe:2709` had no such gate.
- [x] Both waits retain the stale-caption guard, so retry behavior is unchanged. **Structural** —
  `text !== castFeedback` (`:2943`), `text !== secondCastText` (`:3065`); TASK-228 §4 lists retry
  behavior among the unchanged (`:100-103`).
- [ ] The three waits' boundary is explicit (6 = settled only; 6b/6d = settled + stale guard).
  **By citation** — `TASK-228-…md:254-258`, which presents it as the post-TASK-228 boundary, not
  as TASK-227's own verification.
- [ ] TASK-227's own test/guard run passed at the required depth. **NOT FOUND.** No record states
  a TASK-227 run, count, or command. The guard file's `7 passed / 0 failed`
  (`TASK-228-…md:147-151`) is TASK-228's run of the file as TASK-228 left it: corroboration, not
  TASK-227's evidence, and not claimed here.
- [ ] A live E2E run of the tightened phases passed. **NOT FOUND / NOT RUN.** TASK-228 records E2E
  as `NOT RUN — infrastructure unavailable` at its verification time (`:208-222`), and no TASK-227
  E2E result exists. V-1 was latent and never observed firing (`TASK-219A-…md:635-637`).
- [ ] Quality review (`quality/review.md` §1) for TASK-227. **NOT FOUND** — no TASK-227 audit or
  review artifact exists under `tasks/`.
- [x] No client-authoritative gameplay logic and no production behavior change. **By citation** —
  `TASK-228-…md:232-250`, `:359-362`.

---

## 9. Tests / Validation

Only what a citing record states is repeated; no result is inferred or fabricated.

- **Guard file.** `CastFeedbackWaitPredicate.test.ts` is a source guard in the repository's
  established style: it reads the smoke script, strips comments, and compiles and executes the
  predicates it finds rather than paraphrasing them (`:5-32`, `:42-48`, `:83-100`, `:117-143`). It
  declares both tasks in its header (`:6`) and carries the V-1 tests for Phase 6b (`:173-207`) and
  Phase 6d (`:209-237`). TASK-228 §5 states it was **modified** in that task and that the guard
  "now verifies" the seven items at `TASK-228-…md:126-139`; §11 states it "contains both TASK-227's
  and TASK-228's guard assertions" (`:370-373`). The file currently holds 7 `it` blocks; how many
  predate TASK-228 is **NOT FOUND** in any record and not derivable from git (§7).
- **V-1 preservation re-confirmation.** `TASK-228-…md:197-206` records a deterministic run in which,
  with Phase 6b's marker set to the settled `accepted` caption, `in flight`, `not sent` and
  `unavailable` all resolve to `false`, the settled-but-stale caption still resolves to `false`, and
  a new `rejected` caption resolves — V-1's protections were found intact after TASK-228. That is
  TASK-228's verification of preservation, **not** evidence that TASK-227 ran or passed anything.
- **Deterministic suites at TASK-227's time. NOT FOUND.** TASK-226's counts
  (`TASK-226-…md:170-181`) predate it and are not attributable to it; TASK-228's
  (`898 passed / 22 files`, `TASK-228-…md:154-159`) postdate it.
- **Live E2E: NOT RUN in any record for TASK-227.** TASK-219A's run exercised the *pre-tightening*
  phase 6d and reported V-1 as a latent flake (`:627-637`, `:846`).

---

## 10. Remaining Issues

- **TASK-227's verification evidence is permanently thin.** The record now exists; the run evidence
  never did. This reconstruction does not create — and must not be read as creating — a passing
  test/E2E result for TASK-227 (`TASK-228-…md:329-341`).
- **Attribution caveat persists.** The smoke script is shared, and TASK-226's, TASK-227's and
  TASK-228's edits are one uncommitted delta against `9b5c5fe` (`TASK-228-…md:299-314`, `:366-375`);
  until a P-2 group commit names its member tasks, §7 rests on in-hunk tags and citation.
- **P-3 exposure.** TASK-227 was, until this file, an id with no record
  (`TASK-223-…md:522-526`); integration/commit handling stays with TASK-224 (`TASK-228-…md:374-375`).
- **R-1 remains separate and untouched** — no retry loop for the Phase 6 first cast under
  `isInputLocked` (`TASK-228-…md:345-351`).
- **C-3, C-5, C-6 residuals** (`TASK-219A-…md:846`) were outside TASK-227's slice; V-2 went to
  TASK-228 (`:6`, `:84-88`), the rest remain open where TASK-219A recorded them.
- **Decision 2 = A is on disk** at `tasks/active/TASK-224-commit-step-and-task-209-record.md:335-392`
  (§1 above; TASK-224 Revision 2 landed it while this record was written). **No TASK-227 review
  artifact exists**, so that checklist item is reported unchecked rather than assumed passed.

---

## 11. Revision History

**Revision 1 — retrospective completion record authored (evidence-only).** Created under the
TASK-224 / Decision-2 = A reconstruction directive to close TASK-227's record debt as reported by
`TASK-228-…md:329-341`. Provenance: TASK-228's record (primary), TASK-219A's audit (V-1 origin and
re-scope), TASK-226's record, TASK-223 §6.2 / `tasks/TASK_LIFECYCLE.md` §6, the working-tree smoke
script and its `9b5c5fe` baseline, and the untracked guard file. No existing file was edited; no Git
write command was run; no acceptance, test, E2E, commit, or review result was asserted that no
artifact states.
