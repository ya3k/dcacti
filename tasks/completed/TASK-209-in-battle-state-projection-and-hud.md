# TASK-209 — In-Battle Pet HP/Power/Boss-Identity Projection and the Player-Facing Battle HUD

> **Retrospective reconstruction authored under TASK-224, Product Owner Decision 2 = A
> (evidence-only).** This is a historical reconstruction, not a direct execution record:
> every claim carries its evidence, and unproven items are labelled **NOT PROVEN**.
> TASK-209's original task file was never authored, so this record is the first and only
> TASK-209 artifact. It creates no code, test, contract, or historical record, and it
> edits nothing.

---

## Metadata

```text
Task ID:           TASK-209
Type:              FEATURE (derived — no artifact states a Type value. Derived from
                   tasks/completed/TASK-207-product-roadmap-and-gameplay-gap-audit.md:397
                   (a roadmap implementation row, "Estimated complexity: M–L") and
                   tasks/completed/TASK-208-…md §L:1083–1174 (an implementation envelope).
                   NOT RECORDED)
Status:            DONE
Risk:              NOT FOUND — no artifact records a Risk value for TASK-209. Searched:
                   TASK-207 (roadmap row), TASK-208 §L/§M, TASK-208A §TASK-209 Unblock
                   Check, TASK-210 §Metadata, TASK-211/TASK-213 audits, TASK-223 §2/§3,
                   tasks/active/TASK-224-…md. This reconstruction does not assign one.
Priority:          CRITICAL (recorded as "P0" — TASK-207-…:398 and its priority table
                   row at :494)
Primary Agent:     NOT FOUND — no artifact records one. Searched as for Risk.
Supporting Agents: NOT FOUND — no artifact records them.
Workflow:          NOT FOUND — no artifact names a workflow for TASK-209. (TASK-208 §L is
                   an envelope, not a workflow reference.)
Skills:            NOT FOUND — no artifact records a skill set for TASK-209.
Dependencies:      TASK-208 (TASK-207-…:401 "Dependencies: TASK-208.") — the projection
                   decision; TASK-208A — the docs/ amendment that had to land before any
                   code (TASK-208A-…md:29,45; TASK-208-…md §K:1005–1009)
Evidence status:   PARTIALLY PROVEN
Lifecycle note:    reconstructed retrospectively; the original task file was never
                   authored.
```

**Status justification.** `DONE` is a `tasks/TASK_LIFECYCLE.md` §3 value and is used here
only because artifacts prove the deliverable landed, not because a TASK-209 record declared
it:

- `tasks/completed/TASK-210-battle-feedback-and-presentation-pass.md:30` — "Dependencies:
  TASK-209 (DONE — the authoritative state contract this pass presents …)".
- `tasks/active/TASK-224-commit-step-and-task-209-record.md:194` anticipates
  `tasks/completed/TASK-209-<title>.md` as a required artifact.
- The 16 code/test paths in the Declared file set below exist, and 16 of 16 are present in
  commit `9b5c5fe`. That figure was measured independently by this reconstruction and by
  TASK-224, and the two upstream statements now agree with it
  (`tasks/TASK_LIFECYCLE.md:391` "it commits all 16 of the 16 paths"; `tasks/active/TASK-224-…md:410`
  "16 of 16 are already committed in 9b5c5fe").

`tasks/completed/TASK-223-commit-worktree-ownership.md` §3 A-1:223–236 records the
opposite side of this coin: with no record, TASK-209's lifecycle state was *UNDEFINED*, and
this reconstruction supplies the record, not the verification.

---

## Objective

Deliver the in-battle state projection and its player-facing presentation, exactly as the
amended contract defines them: add the three Pet live-combat members and the one Boss
identity member to the existing `BattleStateUpdated` projection (push **and** reconnect
snapshot), extend the client's synchronized copy and runtime readers to consume them under
the contract's always-present strictness, and render a player-facing battle HUD from
already-delivered authoritative values only. The scope, boundaries, and acceptance
conditions are **envelope items of TASK-208 §L:1083–1174**, not restatements of this
record — see `tasks/completed/TASK-208-…md` §L, §K:1003–1079 and §M:1178–1202, and the
contract itself at `docs/02-technical/SIGNALR_PROTOCOL.md` §4.3 item 15:2001 and §4.4 item
10:2163.

---

## Authoritative References

Cited, not copied (`tasks/README.md` §9).

- `docs/02-technical/SIGNALR_PROTOCOL.md` §4.3:1757 (tree + item 15:2001), §4.4:2071 (tree +
  item 10:2163), §7 (snapshot parity), §3.2.3 (camelCase wire naming)
- `docs/02-technical/GAME_STATE.md` §2.3:1143 (`PetState.HP`/`MaxHP`/`Power`), §2.4:2369
  (`BossState.BossId`)
- `docs/01-game-design/BOSS_RULES.md` §6.2.6 (amended visibility constraint — TASK-208A
  §Remaining discharge at `tasks/completed/TASK-208A-…md:321–367`)
- `docs/01-game-design/GAME_RULES.md` §12 (Power range), §18 (server authority);
  `docs/03-decisions/ADR/ADR-001`
- `tasks/completed/TASK-208-…md` §L (implementation envelope + binary acceptance criteria),
  §K (amendment scope), §M (remaining decisions), §N (stop-condition check)
- `tasks/completed/TASK-208A-…md` §TASK-209 Unblock Check:301–317, §Remaining:321–367
- `tasks/completed/TASK-207-…md`:397–404 (the roadmap row defining TASK-209), :494

---

## Scope

### In Scope (evidence-backed only)

- The four wire members on the existing projection and carrier
  (`TASK-208-…md` §L "May do" items 1–2; `SIGNALR_PROTOCOL.md` §4.3 item 15, §4.4 item 10).
- The client transport type, runtime state interface, and runtime readers for those members
  (`TASK-208-…md` §L item 2; evidence: rows 3–5 of the Declared file set below).
- A player-facing battle HUD in `BattleScene` from delivered-authoritative values only
  (`TASK-208-…md` §L item 3).
- Updating the contract-pinning tests to the amended exact member sets, without weakening
  them (`TASK-208-…md` §L item 4 and its "Tests that currently pin the pre-amendment
  contract" list; `TASK-208A-…md` §Remaining 2:330–336).
- The U10 developer-overlay fix: `App.tsx` gates both overlays behind
  `import.meta.env.DEV` (`TASK-211` audit §2.3 row U10:166 attributes this to TASK-209 §6).

### Out of Scope

- Any gameplay rule, formula, magnitude, threshold, or content value
  (`TASK-208-…md` §L "Must NOT do").
- New SignalR method/event/subscription/store/key/column, a second boss catalog, or a
  presentation-state object (`TASK-208-…md` §L "Must NOT do").
- Boss telegraph/passive/element/portrait and Pet Tier/Star/Level
  (`TASK-208-…md` §M items 2, 4, 6).
- Card cost / affordability presentation — a separate decision, which TASK-209 was
  explicitly required **not** to work around client-side (`TASK-208-…md` §M item 3:1186–1189;
  `TASK-210-…md` §Remaining 5:419–422).
- Any item OUT in `docs/00-overview/MVP_SCOPE.md` §2.

An earlier reconstruction constraint is recorded so it is not re-searched:
`tasks/active/TASK-224-commit-step-and-task-209-record.md` §Stop conditions:261 states that
"TASK-209's scope cannot be reconstructed faithfully from code, tests and the citing records"
is a stop condition *for TASK-224*, and the same manifest records it as RESOLVED by
Decision 2 = A (:387). This record honours the evidence-only disposition rather than failing
that stop condition: it declares only what artifacts prove and labels the rest.

---

## Evidence Base

Records (all paths relative to `E:\dcacti`):

```text
tasks/completed/TASK-207-product-roadmap-and-gameplay-gap-audit.md:397–404, :494, :552
tasks/completed/TASK-208-resolve-in-battle-pet-state-presentation-contract.md:1003–1079 (K),
    :1083–1174 (L, incl. the 8 binary acceptance criteria), :1178–1202 (M), :1206–1271 (N)
tasks/completed/TASK-208A-apply-…-documentation.md:29, :45, :301–317 (Unblock Check),
    :321–367 (Remaining 1–3)
tasks/completed/TASK-210-battle-feedback-and-presentation-pass.md:30 (TASK-209 declared
    DONE), :40–43 (lifecycle note), :248–291 (Tests), :294–303 (Verification),
    :365–385 (Diff audit), :407–418 (Remaining 3, 4)
tasks/completed/TASK-211-non-battle-screen-robustness-and-recovery.md:540–541 (Remaining 5)
tasks/completed/TASK-211-post-task-210-product-audit.md:33–39 (suite runs this audit made),
    :140–151 (§2.1, verdicts), :199–239 (§2.5 attribution), :766–789 (NG-23)
tasks/completed/TASK-213-post-task-212a-1-product-gameplay-audit.md:1021–1034 (N-12), :1062
tasks/completed/TASK-223-commit-worktree-ownership.md:130–131, :147–173 (§2 rows marked
    209*), :223–244 (A-1, A-2), :287–291 (A-6), :425–429 (§5.1 item 4), :511–538 (§6.2 P-1…P-6),
    :568–588 (§6.3 COMMIT 2), :750–761 (F-1)
tasks/active/TASK-224-commit-step-and-task-209-record.md:27 (Status IN PROGRESS), :45 and
    :335–340 (the Product Owner decisions), :359–375 (§Decision 2 = A — the reconstruction
    set, TASK-209 first in the list, with the A-6 no-upgrade rule at :373–375), :151–153
    (the as-created state block), :194 (the required artifact), :261 (Stop Condition 2),
    :397–412 (the re-measured tree and the 16-of-16 commit membership)
tasks/TASK_LIFECYCLE.md:199–220 (§3 DONE), :304–309 (§6, PO Decision 1 = A), :331–362
    (§6.2 P-1…P-6), :382–399 (§6.4 the 9b5c5fe pre-adoption batch commit)
```

Artifacts inspected directly (`pwsh`, read-only git and file reads):

```text
git log --oneline -n 5 · git rev-parse HEAD (9b5c5fe8e9c3b0e2b7025e558ae3f8a32a23f527)
git show --name-only --format= 9b5c5fe        → 73 paths
git show --name-status --format= 9b5c5fe      → no migration/schema file added
git rev-parse 9b5c5fe^                        → afe5b1405dc9de49059f176577a4735056ea7769
                                                (so 9b5c5fe's diff is against afe5b14)
git status --porcelain                        → snapshot, not a stable figure: 28 modified
                                                + 12 untracked, then 30 + 1 deleted + 18.
                                                The tree is changed concurrently by TASK-224
                                                (IN PROGRESS) and its sibling reconstructions;
                                                the 16 declared paths' markers were identical
                                                in both measurements
glob tasks/**/*209*                           → no pre-existing file; this record is the
                                                first TASK-209 artifact
```

---

## Declared file set

**This is the explicit, exact file set P-3 (`tasks/TASK_LIFECYCLE.md` §6.2:345–349) requires
before any path may be committed under TASK-209.** The set is TASK-223 §2's 16 `209*` rows
(`TASK-223-…md:147–173`, rows 11–17, 23, 24, 28, 31–34, 36, 37). Markers were determined by
`git show --name-only 9b5c5fe` and `git status --porcelain` at HEAD `9b5c5fe`.

Marker legend: `COMMITTED-IN-9b5c5fe` = the path is present in commit `9b5c5fe`;
`WORKING-TREE-MODIFIED` = the path additionally has uncommitted changes at measurement time,
which are **not** TASK-209's (they post-date the commit).

| # | Repo-relative path | Evidence attributing TASK-209's work to it | Marker |
|---|---|---|---|
| 1 | `src/backend/GameServer.Api/Hubs/BattleHub.cs` | TASK-223 §2 row 11 (`PetStatePayload` gains `hp`/`maxHp`/`power`, `BossStatePayload` gains `bossId`); `TASK-208A-…md` §Remaining 1:325–329 names it. Content: `BattleHub.cs:120–124,235–243,415–418,1238–1280,1301–1340`; confirmed inside the `9b5c5fe` diff | `COMMITTED-IN-9b5c5fe` |
| 2 | `src/frontend/client/src/app/App.tsx` | TASK-223 §2 row 12 (the added comment names "TASK-209 §6" and DEV-gates `StatusOverlay`); the citation is in the file itself: `App.tsx:129–140` | `COMMITTED-IN-9b5c5fe` |
| 3 | `src/frontend/client/src/game/runtime/GameRuntime.ts` | TASK-223 §2 row 13 (validators/readers for the four members). Content: `GameRuntime.ts:1216–1313` (`readPetState`), `:1408–1435` (`readBossState`). Sole owner TASK-209, plus later post-commit work by TASK-226 (`TASK-226-…md:86,101,288`) | `COMMITTED-IN-9b5c5fe` + `WORKING-TREE-MODIFIED` |
| 4 | `src/frontend/client/src/game/runtime/GameRuntimeEvents.ts` | TASK-223 §2 row 14 (`RuntimePetState`/`RuntimeBossState` gain the members). Content: `GameRuntimeEvents.ts:121–124,147,208–228,278–301` | `COMMITTED-IN-9b5c5fe` |
| 5 | `src/frontend/client/src/services/realtime/SignalRService.ts` | TASK-223 §2 row 15 (payload + snapshot types widened). Content: `SignalRService.ts:86–97,197–210,284–305,365–404` | `COMMITTED-IN-9b5c5fe` |
| 6 | `src/frontend/client/src/game/scenes/BattleScene.ts` | TASK-223 §2 row 16 — **shared with TASK-210**: TASK-210 §Diff audit:367–371 declares it and says the tree "also carries TASK-209's uncommitted work in the same two source files". TASK-209 part: the readout replacement and the identity/HP/Power readouts (`BattleScene.ts:101,134,142–149,292,591–740,1023–1124,2265–2268`; TASK-209 §4/§5 self-citations at `:609,968,1071,1854,2199,2298`). The gauges/callouts/floaters are TASK-210's | `COMMITTED-IN-9b5c5fe` + `WORKING-TREE-MODIFIED` |
| 7 | `src/frontend/client/src/game/scenes/BattleEventPresenter.ts` | TASK-223 §2 row 17 — **shared with TASK-210** (TASK-210 §Diff audit:372 = "callout policy"). **Attribution partially unproven:** the file contains no `TASK-209` self-citation, so which hunks are TASK-209's cannot be identified from the artifact; only the file-level share is proven | `COMMITTED-IN-9b5c5fe` + `WORKING-TREE-MODIFIED` |
| 8 | `src/frontend/client/scripts/standalone-web-smoke.mjs` | TASK-223 §2 row 23 — **shared with TASK-210 + TASK-211 + TASK-212A-1** (also TASK-226/227/228 post-commit, `TASK-228-…md` §11). TASK-209's part: the HUD and resync check families, self-cited at `standalone-web-smoke.mjs:816,2190,2203–2249,2383,3139,3208` | `COMMITTED-IN-9b5c5fe` + `WORKING-TREE-MODIFIED` |
| 9 | `src/frontend/client/tests/BattleEventPresentation.test.ts` | TASK-223 §2 row 24 — **shared with TASK-210** (this file's own `describe` is labelled TASK-210). TASK-209 part self-cited at `BattleEventPresentation.test.ts:1097,1226` (TASK-209 §5: the feed is recorded, not rendered) | `COMMITTED-IN-9b5c5fe` + `WORKING-TREE-MODIFIED` |
| 10 | `src/frontend/client/tests/GameRuntime.test.ts` | TASK-223 §2 row 28 (fixtures widened with the four members). Content: `GameRuntime.test.ts:1071,1124–1150,1332–1391,1820–1843`. Sole owner TASK-209, plus TASK-226 (`TASK-226-…md:288`) | `COMMITTED-IN-9b5c5fe` + `WORKING-TREE-MODIFIED` |
| 11 | `src/frontend/client/tests/RuntimeBoundaries.test.ts` | TASK-223 §2 row 31 — **shared with TASK-212A-1**. TASK-209 part: the forbidden-term change removing `power` from the list and adding the derivation guards, self-cited at `RuntimeBoundaries.test.ts:196,274–280` | `COMMITTED-IN-9b5c5fe` |
| 12 | `src/frontend/client/tests/SceneLifecycle.test.ts` | TASK-223 §2 row 32 — **shared with TASK-210 + TASK-211**. TASK-209 part self-cited at `SceneLifecycle.test.ts:1265,1448–1456,1528–1548,1954,2047,2071,2274,2319` (projection fixtures + HUD assertions) | `COMMITTED-IN-9b5c5fe` + `WORKING-TREE-MODIFIED` |
| 13 | `src/frontend/client/tests/SignalRService.test.ts` | TASK-223 §2 row 33 (payload fixtures widened). Content: `SignalRService.test.ts:136–198,616–624` | `COMMITTED-IN-9b5c5fe` |
| 14 | `tests/backend/GameServer.Api.Tests/ApiIntegrationTests.cs` | TASK-223 §2 row 34; `TASK-208A-…md` §Remaining 2:330–336 names it. Content: `ApiIntegrationTests.cs:444–511,674–788,1096–1126,1161–1230,1237–1263,1276–1374,1512–1572` | `COMMITTED-IN-9b5c5fe` |
| 15 | `tests/backend/GameServer.Api.Tests/Hubs/BattleHubReconnectRecoveryTests.cs` | TASK-223 §2 row 36; `TASK-208A-…md` §Remaining 2:333 names it. Content: `BattleHubReconnectRecoveryTests.cs:341–363,386–390` | `COMMITTED-IN-9b5c5fe` |
| 16 | `tests/backend/GameServer.Api.Tests/Hubs/PetStateWireProjectionTests.cs` | TASK-223 §2 row 37 (new projection fixture + member-set assertions); `TASK-208-…md` §L:1146–1147 names it. Content: `PetStateWireProjectionTests.cs:36,63–92,112` | `COMMITTED-IN-9b5c5fe` |

**Nothing outside this set is claimed for TASK-209**, and this record declares no path that
TASK-223 §2 does not already attribute to it. `git show --name-status 9b5c5fe` adds no
migration, schema, ADR, or `docs/` file attributable to TASK-209; the docs amendment belongs
to TASK-208A.

**Disclosure required by the same policy.** Per P-2 (`tasks/TASK_LIFECYCLE.md` §6.2:337–344)
six of these paths carry other tasks' work (rows 6–9, 11, 12) and therefore cannot be a
per-task commit slice. Per P-3, the commit that placed them in history predates the policy
and names no task at all: `tasks/TASK_LIFECYCLE.md` §6.4:382–399 records `9b5c5fe` as a
pre-adoption batch commit that "complied with none of P-2…P-4", carries no task id and no
`unowned/pre-existing:` disclosure, and "is not to be remedied by rewriting history". The
`COMMITTED-IN-9b5c5fe` marker therefore means **present in that commit**, not **attributed by
that commit to TASK-209**.

---

## Acceptance Criteria

The criteria are TASK-208 §L's eight binary acceptance criteria
(`tasks/completed/TASK-208-…md:1154–1174`). Each is restated only by its identifier; the
wording lives at that citation.

| # | Criterion (TASK-208 §L:1156–1173) | Verification status |
|---|---|---|
| AC-1 | Join push, resolved-Swap push, and `GetBattleState` snapshot carry the four members with identical member sets | **PROVEN as an artifact**; NOT PROVEN as a TASK-209 run. `ApiIntegrationTests.cs:1285–1342` (`BattleStateUpdated_JoinUpdateAndSnapshot_ShouldAgreeMemberForMember`) asserts the exact sets on all three paths |
| AC-2 | Each delivered value equals the authoritative state value, no transformation | **PROVEN as an artifact.** `ApiIntegrationTests.cs:1237–1263` asserts against the Domain `PetState`; `PetStateWireProjectionTests.cs:83–88` asserts read-as-sent (`640/1000/0`) |
| AC-3 | Always present, never null, zero sent as zero | **PROVEN as an artifact.** `PetStateWireProjectionTests.cs:63–92` (`power = 0`); `BattleHubReconnectRecoveryTests.cs:359–363`; the client reader rejects an absent member at `GameRuntime.ts:1276–1282,1427–1435` and is pinned by `GameRuntime.test.ts:1354–1391` |
| AC-4 | No member outside the approved sets at any depth | **PROVEN as an artifact.** `ApiIntegrationTests.cs:1096–1126,1332–1342,2312`; `GameRuntime.test.ts:1127,1150,1843` |
| AC-5 | Client renders the four values without deriving any of them; no arithmetic producing HP/MaxHP/Power or a cost/affordability/legality judgment | **PROVEN as an artifact** by source-level assertions and E2E check names, **NOT** by a TASK-209 execution: `SceneLifecycle.test.ts:2274,2319,3137`; `RuntimeBoundaries.test.ts:274–280`; `standalone-web-smoke.mjs:2249,3208` |
| AC-6 | No new SignalR method, event, subscription, store, key, column, or presentation-state object | **PROVEN as an artifact.** `git show 9b5c5fe -- src/backend/GameServer.Api/Hubs/BattleHub.cs` adds no `public` hub method; the commit adds no migration/schema file; `TASK-210-…md` §Diff audit:382–385 states no protocol change; `TASK-208A-…md` §TASK-209 Unblock Check item 9:313 |
| AC-7 | Amended exact-member-set tests pass; PlayerId/PetId identity exclusions still pass | **PARTIALLY PROVEN.** The tests exist (`ApiIntegrationTests.cs:1096–1126,1512–1572`). A green run is recorded only by **other tasks** (see Tests/Validation). No artifact records a TASK-209 run of them |
| AC-8 | No gameplay, balance, content, damage, healing, Power-generation, or progression file changed by TASK-209 | **PROVEN as an artifact** at file-set level: the 16 declared paths contain no such file. TASK-223 §2:133–192 assigns every other path it measured to a different task, and the entries that appeared after its measurement belong to TASK-217B / TASK-226 / TASK-227 / TASK-228 (`tasks/active/TASK-224-…md:397–442`) |

**Criteria the reconstruction cannot place.** `TASK_TEMPLATE.md:80–82` adds three generic
criteria (tests at the required validation depth, quality-review checklist, no authoritative
rule or contract violated). No artifact records that TASK-209 ran a validation depth, a
quality review, or a contract check: **NOT PROVEN** for the TASK-209 execution. The nearest
evidence is a later audit's suite runs (see below), which is not the same thing.

---

## Tests / Validation

**No artifact records a test run, type check, build, or E2E execution performed by TASK-209.
NOT PROVEN.** This is the load-bearing honesty constraint of this record, established by
TASK-223 §3 A-6 (`TASK-223-…md:287–291`):

> "TASK-209's own runs are reported only second-hand, inside records that are not its own,
> and no record states what TASK-209 verified or failed."

Searched for a first-hand TASK-209 result and found none: `glob tasks/**/*209*` → no file;
TASK-210 §Verification:294–303, TASK-211 audit §0:33–39, TASK-213 audit, and TASK-208A
§Validation all report **their own** runs.

The nearest — and explicitly second-hand — evidence, listed so it is not mistaken for
TASK-209's:

```text
TASK-210-…md §Verification:294–303      npx tsc --noEmit PASS; npx vitest run PASS (20 files,
                                        783 tests); npm run build PASS; backend tests NOT RUN;
                                        Browser E2E PASS (TASK-210's own two runs, 100 checks
                                        each, over a tree that also carried TASK-209's work)
TASK-211 audit §0:33–39                 tsc PASS; vitest 783/20 PASS; dotnet test 326 / 1558 /
                                        623 / 412 PASS — run by the AUDIT, on the accumulated
                                        tree, not by TASK-209; E2E NOT RE-RUN by that audit
TASK-210-…md §Diff audit:367–380        file-level accounting only ("TASK-209's uncommitted
                                        work in the same two source files and the same
                                        test/E2E files")
TASK-211 audit §2.1:140–151             verdicts IMPLEMENTED for the TASK-209/TASK-210 surface,
                                        from code/tests + E2E check names — an audit's
                                        verification of artifacts, attributed to the pair
```

Consequently: this record asserts that the **artifacts exist and encode the contract**, and
does **not** assert that TASK-209 verified anything. The E2E check names it relies on for
AC-5 (`standalone-web-smoke.mjs:2203–2249,3208`) are cited as the E2E surface that later
records report as passing, not as a TASK-209 result.

---

## Remaining Issues

Reported, not repaired (`AGENTS.md` §16; TASK_LIFECYCLE §3: completed records are immutable).

```text
R-1  The authorizing Product-Owner decisions are recorded in an ACTIVE task manifest, not
     in a completed record. Decision 1 = A is recorded at tasks/TASK_LIFECYCLE.md:304–309
     (it adopts TASK-223 §6.2's P-1…P-6 as repository policy); Decision 2 = A is recorded
     at tasks/active/TASK-224-commit-step-and-task-209-record.md:45 and :335–340, with the
     reconstruction set at :359–371 (TASK-209 first) and the governing rule at :373–375
     ("TASK-223 §3 A-6 governs TASK-209: its verification is second-hand only and must not
     be upgraded"). Nothing is missing, but a reader looking only under tasks/completed/
     will not find Decision 2: TASK-224 has no completed record
     (`glob tasks/**/*224*` → its manifest only, currently `Status: IN PROGRESS` at
     tasks/active/TASK-224-…md:27). The other five reconstructions Decision 2 = A
     commissioned (`tasks/active/TASK-224-…md:364–371`) are being filed alongside this one.
     Reported as an informational provenance note, not a defect of this reconstruction.

R-2  TASK-209's own verification is unrecorded and unrecoverable (TASK-223 §3 A-6:287–291;
     TASK-211 audit NG-23:766–789; TASK-213 audit N-12:1021–1034). Nothing in this
     record closes it: a reconstruction cannot manufacture a run.

R-3  Commit-membership figure — RAISED BY THIS RECONSTRUCTION, THEN CLOSED AT THE SOURCE.
     While reconstructing, this record measured the intersection of
     `git show --name-only --format= 9b5c5fe` (73 paths) with the 16 full paths TASK-223 §2
     marks `209*`, and obtained 16 of 16 — every one as `M` in
     `git show --name-status --format= 9b5c5fe`, against parent
     `afe5b14` (`git rev-parse 9b5c5fe^`). Two upstream artifacts then stated 14 of 16
     (tasks/TASK_LIFECYCLE.md §6.4 and tasks/active/TASK-224-…md), which this record
     reported as an unresolved discrepancy rather than adopting.

     **Resolved:** TASK-224 re-measured and traced the 14-of-16 figure to a regex artifact
     (a bare filename `SIGNALR_PROTOCOL.md` captured instead of its full path) and corrected
     both locations. The corrected upstream statements now read
     tasks/TASK_LIFECYCLE.md:391 — "it commits all 16 of the 16 paths TASK-223 §2 attributes
     to TASK-209," — and tasks/active/TASK-224-…md:410 — "`209*`  16 of 16 are already
     committed in 9b5c5fe". Both were verified by reading those lines, not accepted from the
     correction notice.

     No open issue remains on this point: the measured figure, the correction, and both
     upstream statements agree at 16 of 16, and the Declared file set's 16
     `COMMITTED-IN-9b5c5fe` markers stand unchanged. Retained here rather than deleted so
     the record shows that the figure was independently measured and that the earlier
     14-of-16 statements were superseded, not silently dropped.

R-4  Six paths are shared and TASK-209's hunks in two of them are not separable:
     `BattleEventPresenter.ts` (row 7) carries no TASK-209 self-citation, and
     `BattleScene.ts` (row 6) interleaves TASK-209's readouts with TASK-210's gauges.
     TASK-223 §5.1 item 5:431–439 excludes hunk-splitting as unverifiable; this record
     follows that and marks the hunks unproven rather than inferring authorship.

R-5  The recorded TASK-209 scope is a reconstruction pressure point, not a closed item:
     tasks/active/TASK-224-…md:261 lists "TASK-209's scope cannot be reconstructed
     faithfully" as a stop condition, and the same manifest records it as RESOLVED by
     Decision 2 = A (:387). This record satisfies that disposition by proving what the
     artifacts support and labelling the rest, but it cannot promote the reconstruction to
     a first-hand execution record.

R-6  `ARCHITECTURE.md` §2.2.2 item 5 ("No gameplay HUD exists yet") was already false
     after TASK-209 (`TASK-210-…md` §Remaining 4:415–418;
     tasks/completed/TASK-211-post-task-210-product-audit.md:1050). Assigned to TASK-217;
     not touched here.
```

---

## Revision History

```text
Revision 1 — retrospective reconstruction under TASK-224 (PO Decision 2 = A).
             Authored as tasks/completed/TASK-209-in-battle-state-projection-and-hud.md.
             No production code, test, contract, docs/, ADR, or historical record was
             created, modified, staged, or reverted by it. No git writing command was run;
             git was used read-only (log, rev-parse, show, status).
```

**Provenance of this record.** TASK-223 §10 F-1(b):750–761 commissioned the reconstruction
of TASK-209's record from the code, tests, and citing records; the Product Owner's
Decision 2 = A commissioned it explicitly and named TASK-209 first among the six
reconstructions (`tasks/active/TASK-224-…md:359–371`), and its manifest
:194 lists `tasks/completed/TASK-209-<title>.md` as a required artifact. This is that file.
