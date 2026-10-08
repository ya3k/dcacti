# TASK-217A — Architecture / Component / Directory Reconciliation + Stale Code Comments

> **RETROSPECTIVE RECONSTRUCTION.** Retrospective reconstruction authored under TASK-224, Product Owner Decision 2 = A (evidence-only).
> Historical reconstruction, not a direct execution record: every claim carries
> its evidence, and unproven items are labelled NOT PROVEN.
>
> This record is the only file this reconstruction writes. No existing file —
> including the stale source comments examined below — was edited, and no Git
> write operation was performed.

---

## Metadata

```text
Task ID:           TASK-217A
Type:              DOCUMENTATION
Status:            IN PROGRESS (reconstructed)
Risk:              LOW (documentation and code-comment text only)
Priority:          P1 documentation-blocker
                   (TASK-213-content-reachability-decision.md:610;
                    TASK-213-post-task-212a-1-…audit.md:1085)
Primary Agent:     review
Supporting Agents: N/A
Workflow:          documentation/documentation-change.md
Skills:            NOT RECORDED — the original task file was never authored, so
                   no skill selection exists (TASK_TEMPLATE.md:11 requires 2–7
                   from .ai/skills/SKILL_REGISTRY.md). Not reconstructible.
Dependencies:      None
                   (TASK-213-content-reachability-decision.md:610 "Dependency:
                   none"; the A-20 half is a Product Owner decision —
                   TASK-212-post-task-211-product-audit.md:996,
                   :1352)
Evidence status:   PARTIALLY PROVEN
Lifecycle note:    reconstructed retrospectively; the original task file was
                   never authored; its status was never validated. No legal
                   terminal state fits what the artifacts prove: DONE is false
                   (sites remain stale — §Remaining Issues) and SUPERSEDED is
                   false (no downstream task satisfied 100% of this scope;
                   TASK-219A:850 and TASK-221A:1100 both record it as *open*).
                   `tasks/TASK_LIFECYCLE.md` §3 defines no partial state, so
                   `IN PROGRESS` is used descriptively rather than as a
                   validated transition.
```

---

## 1. Objective

Reconcile the architecture document set with the implementation as-built, and
correct the source comments that assert behaviour the code contradicts:
`docs/02-technical/ARCHITECTURE.md` must stop naming components that do not
exist in `src/` and stop describing directory trees that do not match the
repository; the drift it propagated into `DATABASE.md` and `REDIS_STATE.md`
must be removed; `docs/02-technical/TDD.md`'s Discord Activity residue must be
addressed together with the retired-Discord DI seam decided by TASK-212 §A-20
(`TASK-212-post-task-211-product-audit.md:973-998`); and the stale/misleading
`BattleStartService.cs` / `BattleStateService.cs` comments assigned by
TASK-213 §11 must be corrected.

**This objective is a reconstruction of the citing records' assignment, not an
invented scope.** Every clause above is traceable to a `path:line` in §2.

---

## 2. Authoritative References

- `tasks/completed/TASK-212-post-task-211-product-audit.md` §A-11a (`:658-719`,
  owner table `:1326`), §A-11a (adjacent) (`:1332`), §A-20 (`:973-998`, `:1352`),
  §Recommended action line splitting TASK-217 into 217A/217B/217C (`:713-718`),
  §6 row (`:1263-1268`)
- `tasks/completed/TASK-213-content-reachability-decision.md` §11 (`:699-749`:
  §11.1 the two named stale comments, §11.2 sites A4–A8), §9 (`:610`), §9
  split with priorities (`:638-641`)
- `tasks/completed/TASK-213-post-task-212a-1-product-gameplay-audit.md` §N-10
  (`:975-994`, "Add both to TASK-217A's list"), §N-11 (`:996-1009`), §9
  (`:1061`), §10 row 5 (`:1085`), §9.5 (`:993`, `:1120`)
- `tasks/completed/TASK-215A-pet-tier-star-passive-scope-decision.md:182`
- `tasks/completed/TASK-218A-relic-trigger-presentation-audit.md` §9.4
  (`:641-646`, `:938`)
- `tasks/completed/TASK-219A-post-implementation-audit.md:827-828`, `:850`,
  `:965`
- `tasks/completed/TASK-221A-post-implementation-audit.md` §9.5 (`:988-1003`),
  `:1100`
- `tasks/completed/TASK-223-commit-worktree-ownership.md` §10 F-5 (`:786-788`),
  §6.2 P-1…P-6 (`:504-538`)
- `tasks/active/TASK-224-commit-step-and-task-209-record.md` Decision 2 = A
  (`:335-381`), `:134-136`
- `tasks/TASK_LIFECYCLE.md` §3 (legal Status values), §4 (file movement)
- `AGENTS.md` §2 (source-of-truth precedence), §4 (conflicts), §16 (task
  discipline), §17 (documentation change rule), §20 (stop conditions)

Contracts are cited, not restated. No game rule, wire member, state model,
Redis key, database column or endpoint is described here.

---

## 3. Scope

### In Scope (evidence-bounded)

1. `docs/02-technical/ARCHITECTURE.md` §3's component table, §1's directory
   trees, the HUD claim, and the `BattleResolutionService` references in §4,
   §4.1 and §5 item 2 — the sites enumerated in
   `TASK-212-post-task-211-product-audit.md:676-682`.
2. The drift that document propagated into `DATABASE.md` and `REDIS_STATE.md`
   (`TASK-212-post-task-211-product-audit.md:682`).
3. `docs/02-technical/TDD.md`'s Discord Activity residue
   (`TASK-212-post-task-211-product-audit.md:1332`).
4. The retired-Discord DI/wiring question, "decide it inside TASK-217A"
   (`TASK-212-post-task-211-product-audit.md:996`).
5. The two named stale code comments
   (`TASK-213-content-reachability-decision.md:708-709`), sites A4–A8
   (`:727-731`), and `BattleScene.ts`'s stale `renderEventCallout` name
   (`TASK-218A-relic-trigger-presentation-audit.md:643-646`).
6. Documentation and comment text only. **No** production behaviour, test,
   contract, migration, ADR, gameplay rule, or state model.

### Out of Scope

- Every item TASK-213 §9 assigned to TASK-217B or TASK-217C
  (`TASK-213-content-reachability-decision.md:670`, `:735`): the
  cross-reference/envelope sweep and the `SIGNALR_PROTOCOL.md` §3.2.20 Card-cost
  sentence.
- Any product decision. `TASK-213-content-reachability-decision.md:640-641`:
  "217A/217B must not absorb any product decision."
- The A-20 *decision* itself, which is the Product Owner's
  (`TASK-212-post-task-211-product-audit.md:994-997`, `:1352`).
- Editing any completed task record (`TASK_LIFECYCLE.md:218`).
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2.

---

## 4. Evidence Base

Read-only Git only (`git log`, `git show`, `git diff`, `git status`,
`git ls-tree`); no write operation was performed.

```text
git log --oneline -n 8                       9b5c5fe / afe5b14 / 1a472f7 / …
git rev-parse HEAD                           9b5c5fe8e9c3b0e2b7025e558ae3f8a32a23f527
git status --porcelain                       30 modified tracked + 12 untracked
git diff --check                             PASS (exit 0)
git diff --  docs/02-technical/ARCHITECTURE.md | TDD.md | REDIS_STATE.md |
              DATABASE.md | docs/01-game-design/BOSS_RULES.md |
              src/backend/…/BattleStartService.cs |
              src/backend/…/BattleStateService.cs
git show 9b5c5fe --name-only / --stat        the 73-path pre-adoption batch commit
git show afe5b14:<path> | Select-Object …    the pre-9b5c5fe revision
git ls-tree -r --name-only HEAD              tracked-path existence
Get-ChildItem src/backend, src/frontend/client   the real trees
```

Line numbers below are the **current working-tree** lines unless labelled
otherwise.

---

## 5. Id scope: planned vs executed

This section is required because the id `TASK-217A` is *planned* for one thing
and *credited by the artifacts* with more than that. Both readings are stated;
neither is silently chosen.

### 5.1 The planned identity (documentation-only)

Six citing records assign TASK-217A the same subject: architecture/component/
directory reconciliation plus stale code comments, documentation-only, P1.

```text
TASK-212-post-task-211-product-audit.md:713-718   "TASK-217A = the architecture/
                                                  component/directory-tree
                                                  reconciliation (P1-blocker class)"
                                       :1263-1268  "Documentation-only in
                                                  *production* terms"
                                       :1326       owner = ARCHITECTURE.md, "TASK-217A"
                                       :1332       TDD.md Discord residue, "TASK-217A"
TASK-213-content-reachability-decision.md:610     "Architecture / component /
                                                  directory reconciliation + stale
                                                  code comments … DOCUMENTATION-ONLY"
                                       :638-641    the three-way split
TASK-213-post-…audit.md:993                       "Add both to TASK-217A's list"
TASK-215A-…decision.md:182                        code is correct; housekeeping
                                                  deferred to TASK-217A
TASK-219A-post-implementation-audit.md:850        "UNCHANGED (still P1 documentation)"
TASK-221A-post-implementation-audit.md:994-1003   same scope, nothing added
TASK-218A-…audit.md:643-646                       same scope, +BattleScene.ts:1143
TASK-224-…record.md:366                           "architecture / component /
                                                  directory reconciliation + stale
                                                  comments" (Decision 2 = A list)
```

### 5.2 What the artifacts actually credit to the id

Two mutually consistent streams are credited to `TASK-217A` in the working
tree, by the documents' own version headers:

```text
RECONCILIATION STREAM
  docs/02-technical/ARCHITECTURE.md:3-15   "Version 1.9 (§3, §4, §4.1 and §5 item 2
                                           reconciled with the implementation
                                           as-built per TASK-217A … Decision
                                           source: TASK-217A."
  docs/01-game-design/BOSS_RULES.md:729    "(TASK-047; TASK-217A corrected the
                                           corresponding code comments)."
  docs/02-technical/REDIS_STATE.md:555     "Superseding note (TASK-217A, current
                                           contract)."

IMPLEMENTATION STREAM ("Atomic Battle Reward Persistence")
  tasks/completed/TASK-217B-post-implementation-audit.md:1-3
                                           "TASK-217B — Post-Implementation Audit:
                                           Battle Reward Atomicity / Audited
                                           implementation: TASK-217A — Atomic
                                           Battle Reward Persistence"
  docs/02-technical/DATABASE.md:24         "Decision source: TASK-217A" (v1.37, the
                                           battle-end atomicity contract)
  docs/02-technical/DATABASE.md:1262-1263  "(TASK-217A)" on the atomicity section
  docs/02-technical/ARCHITECTURE.md:15-25  "(TASK-216 / TASK-217A / TASK-217B)"
```

**Reading supported by the evidence.** The two streams do not contradict each
other: an implementer that lands the atomic battle-end path and then reconciles
the documents and comments to what it built produces exactly this artifact set.
**Nothing in the artifacts forbids `TASK-217A` being one executed task with two
deliverable streams**, so this record does **not** assert an id collision for
217A. What it does assert is a **scope divergence**:

```text
PLANNED  (TASK-213-content-reachability-decision.md:610, :640-641)
         documentation-only; P1 documentation-blocker; "must not absorb any
         product decision"; dependency none.
EXECUTED (as the artifacts credit it)
         a production implementation with tests, migrations-adjacent work and
         its own audit (TASK-217B), plus the reconciliation stream above.
```

That divergence is an observation about task scoping, not a documentation
conflict: the reconciliation stream stayed inside the planned boundary, and the
implementation stream is a separate body of work that happens to carry the same
id. Whether that is an accepted multi-stream task or an id-allocation error is
a process question (`tasks/README.md` §3 "numbers are never reused"), not
something an agent may settle.

### 5.3 The part that is genuinely irreconcilable — a Product Owner question

Two facts in the current tree cannot both be true under any single reading, and
they concern the *sibling* ids, not 217A's own scope. They are reported, not
resolved (`AGENTS.md` §4, §20).

```text
(1) TASK-217B
    "already recorded, closed audit of the atomicity implementation"
      tasks/completed/TASK-217B-post-implementation-audit.md exists, is
      complete (865 lines, Verdict PASS), and is declared untracked by
      `git status --porcelain` (?? tasks/completed/TASK-217B-…audit.md).
    "open documentation/cross-reference sweep"
      TASK-213-content-reachability-decision.md:670 assigns the sweep to
      TASK-217B; TASK-212-post-…audit.md:1273 lists "TASK-217B
      Cross-reference, envelope and stale-claim sweep  P3  M" as NOT DONE;
      TASK-221A-post-…audit.md:1005-1023 still enumerates its open items
      (D-F1, D-F2, D-F3, D-F6, O-F1).
    Contradiction: an id cannot simultaneously be a closed audit record and an
    unstarted sweep.

(2) TASK-217C
    "the Card-cost authority ruling applied to SIGNALR_PROTOCOL.md §3.2.20"
      TASK-213-content-reachability-decision.md:611; restated by the
      authorizing record at TASK-224-…record.md:367 ("TASK-217C — the Card-cost
      authority ruling applied to SIGNALR_PROTOCOL.md §3.2.20").
    "the decision source for the battle-end atomicity documentation sync"
      docs/02-technical/ARCHITECTURE.md:25; TDD.md:6; REDIS_STATE.md:12;
      DATABASE.md:13 — four documents, one of them in the same change that also
      credits TASK-217A.
    Corroboration of the split: the Card-cost sentence those records assign to
    217C is STILL PRESENT — docs/02-technical/SIGNALR_PROTOCOL.md:1132
    ("that must show the spent Cost reads the Card's definition"), which
    TASK-219A-post-…audit.md:851 records as 217C's one-sentence job.
    A TASK-217C record now exists —
    tasks/completed/TASK-217C-apply-card-cost-authority-ruling.md (untracked,
    authored during this reconstruction's window; not present when this
    record's first evidence pass ran). It reaches the same conclusion
    independently: C1 "ABSENT — superseded sentence present verbatim" (:209),
    C4b "attribution split" (:214), C5b "one text region is claimed by two
    different ids (v1.8/TASK-217C and v1.9/TASK-217A), and no artifact
    separates them" (:216, :228-230).

(3) Internal attribution conflict inside one file
      docs/02-technical/REDIS_STATE.md:5-12 (v1.13 preamble) attributes §3's
      commit-before-delete lifecycle entry to "Decision source: TASK-217C",
      while :555 attributes the same commit-before-delete rule to
      "Superseding note (TASK-217A, current contract)". Both lines are
      working-tree additions.
```

**Disposition.** Items (1)–(3) are a documentation/authority conflict. Under
`AGENTS.md` §4 and §20 the agent must detect, explain, name the owner and stop;
the owner here is the Product Owner (they are questions of task-id allocation
and of which task's record may carry which path under TASK-223 §6.2 P-3).
**This record does not resolve them and must not be used to.** They are the same
class of defect TASK-223 §10 F-3 reserved TASK-225 for
(`TASK-223-commit-worktree-ownership.md:770-774`), which named TASK-211 and
TASK-213 only.

---

## 6. Per-site disposition

Classification vocabulary: `FIXED IN 9b5c5fe` · `FIXED IN WORKING TREE` ·
`PARTIALLY FIXED` · `STILL STALE` · `NOT FOUND`. Where a `9b5c5fe` attribution is
relevant it is given in the evidence column. No site in this table was fixed in
`9b5c5fe` by this task's own work; `9b5c5fe` predates the reconciliation stream.

| # | Site | What the citing record required | Current state | Current line(s) | Evidence |
|---|---|---|---|---|---|
| 1 | `docs/02-technical/ARCHITECTURE.md` §3 component table | Remove the eight components that do not exist in `src/` (`BattleResolutionService`, `Match3Engine`, `ElementResolver`, `RelicTriggerEngine`, `BossController`, `BattleEventBus`, `PersistenceRepository`, `DiscordService`) — `TASK-212-post-…audit.md:676-682`, `:1326` | **PARTIALLY FIXED** — 7 of 8 are replaced by real types (`BoardResolver`, `CascadeResolver`, `MatchDetector`, `ElementMatchups`, `RelicResolver`, `CardCastExecutor`, `BossState`, `BossDefinitions`, `BattleStateService`, `BattleResultService`, `BattleEndTransaction`, `BossDefinitionLookup`); **`DiscordService` remains** | table `:661-710`; `DiscordService` `:706` | `git diff` hunks `@@ -638,18 +661,40 @@` and `@@ -669,18 +714,25 @@`; version 1.9 preamble `:3-15` ("no longer lists types that do not exist") |
| 2 | `ARCHITECTURE.md` §1 directory trees | Reconcile with `src/` — `TASK-212-post-…audit.md:676-682` (cites `:64-89`, `:91-139`); "client `public/` does not exist" | **STILL STALE** — `Domain/Events/`, `Application/BattleResolution/`, `Application/UseCases/`, `Infrastructure/SignalR/`, `client/public/` and `services/discord/` do not exist; `GameServer.Shared` is absent from the tree | backend tree `:87-112`; client tree `:114-162`; `public/` `:116` | `Test-Path` false for all six; real `Application/` = Accounts, Battle, Cards, Collection, Identity, Pets, Players, Relics, Runtime; real `Infrastructure/` = Accounts, Discord, Postgres, Redis; `services/` = api, realtime; the §1 region is untouched by the working-tree diff (hunks are `@638`, `@669`, `@691`, `@705`, `@714`, `@731`) |
| 3 | `ARCHITECTURE.md` HUD claim | "No gameplay HUD exists yet" is stale — `TASK-212-post-…audit.md:210` ("**STILL STALE**"), `:676-682` | **STILL STALE** | `:617` | verbatim "against the viewport edge. No gameplay HUD exists yet."; mirrored at `src/frontend/client/src/game/GameViewport.ts:25` ("No gameplay HUD is implemented yet") |
| 4 | `ARCHITECTURE.md` §4, §4.1, §5 item 2 (`BattleResolutionService`) | Replace the phantom type — `TASK-212-post-…audit.md:676-682` (cites `:672-683`, `:694`, `:708`, `:717`, `:720`, `:734`) | **FIXED IN WORKING TREE** | §4 `:714-737`; §4.1 diagram `:743`; §5 item 2 `:786` | diff hunks `@691`, `@705`, `@714`, `@731`; no `BattleResolutionService` type exists under `src/` (the only surviving mention is a doc comment, `src/backend/GameServer.Domain/Passives/PassiveTracker.cs:9`) |
| 5 | `docs/02-technical/DATABASE.md` "~:241" | Remove the `PersistenceRepository (Postgres)` drift — `TASK-212-post-…audit.md:682`. At `afe5b14` line 241 was the version-preamble occurrence | **PARTIALLY FIXED** — the §1 *live* mechanism note (old `:887`) now names the real boundary; the historical version-history mention survives as history | live note `:915-920` (`BossDefinitionLookup`, `GameServer.Infrastructure.Postgres.BossDefinitionLookup`); historical mention `:271` | diff `+` lines "the lookup is the existing `BossDefinitionLookup` boundary rather than the `PersistenceRepository (Postgres)` component `ARCHITECTURE.md` §3 no longer names"; DATABASE.md `:8-11` attributes that reconciliation to **TASK-217C**, not 217A |
| 6 | `DATABASE.md` "~:887" | Same — `TASK-212-post-…audit.md:682`. At `afe5b14` line 887 was the live §1 mechanism note | **FIXED IN WORKING TREE** | `:915-920` | as row 5; the surviving `PersistenceRepository` string is now only inside the version-history paragraph |
| 7 | `docs/02-technical/REDIS_STATE.md` "~:102" and "~:198" | Remove `BattleResolutionService` — `TASK-212-post-…audit.md:682`. At `afe5b14` those lines carried it | **STILL STALE** (both) | `:114` (§2 item 2) and `:227` (§4 item 1) | both lines still read "`BattleResolutionService` loads, uses, and saves it…" / "Before resolving an action, `BattleResolutionService` reads"; the file's working-tree hunks are `@3`, `@176`, `@179`, `@185`, `@189`, `@524` — neither line is touched |
| 8 | `docs/02-technical/TDD.md` Discord Activity residue | Correct the five sites — `TASK-212-post-…audit.md:1332` (cites `:75`, `:93`, `:100`, `:241`, `:252`), which is self-contradicted by `TDD.md`'s own "Standalone Web SPA; ADR-020" line | **STILL STALE** (all five) | `:82`, `:100`, `:107`, `:248`, `:259` (self-contradiction anchor at `:74-75`) | line mapping verified by `git show afe5b14:docs/02-technical/TDD.md` (`afe5b14:75` → current `:82`, `:93`→`:100`, `:100`→`:107`, `:241`→`:248`, `:252`→`:259`); TDD's working-tree hunks are `@3` and `@352` only, and `@3` is attributed to **TASK-217C** (`TDD.md:6`, `:356-366` §4 item 2) |
| 9 | `ARCHITECTURE.md` component-table `DiscordService` + the DI seam (TASK-212 §A-20) | Decide the retired-Discord residue "inside TASK-217A" — `TASK-212-post-…audit.md:996`, `:1352`; evidence `ADR-020:24-28`, `ARCHITECTURE.md:622`, `DependencyInjection.cs:101` | **STILL STALE / undecided** — no decision artifact exists; the seam is still registered | `ARCHITECTURE.md:706`; `src/backend/GameServer.Infrastructure/DependencyInjection.cs:114` and `:178`; four files under `GameServer.Infrastructure/Discord/` | `AddSingleton<IDiscordIdentityResolver, UnconfiguredDiscordIdentityResolver>()` at `:114` and a `Replace(...)` at `:178`; ADR-020 records Discord as retired. The *decision* is the Product Owner's, so "not done" is correct behaviour, not a defect |
| 10 | `src/backend/…/Battle/BattleStartService.cs:248-253` | Correct the "exactly three content-defined MVP Bosses" comment — `TASK-213-content-reachability-decision.md:708`, `TASK-213-post-…audit.md:975-993`, `TASK-215A-…:182`, `TASK-219A-…:850`, `TASK-221A-…:997-1000` | **FIXED IN WORKING TREE** | `:251-256` | diff `@@ -249,5 +251,6 @@` adds "BOSS_RULES.md §6 defines five content-defined MVP Bosses and BossDefinitions holds exactly those five … the earlier 'two further MVP Bosses are not yet content-defined' note was retired by TASK-172"; corroborated by `BOSS_RULES.md:729` crediting TASK-217A |
| 11 | `src/backend/…/Battle/BattleStateService.cs:1582-1584` | Correct the "Passive EFFECT application is out of this task's scope for every Boss … applies nothing" comment — `TASK-213-content-reachability-decision.md:709`, `TASK-215A-…:182`, `TASK-219A-…:850`, `TASK-221A-…:997-1000` | **FIXED IN WORKING TREE** | `:1588-1590` | diff `@@ -1576,9 +1580,11 @@` adds "The match-charged Boss Passives' EFFECTS are applied below, at GAME_RULES.md §17 step 18a …: a trigger emits its event and applies that Boss's declared effect" |
| 12 | `BattleStartService.cs:31, :60, :157` (A8) | Broken `cref` — member is `CreateBattleAsync` — `TASK-213-content-reachability-decision.md:731` | **FIXED IN WORKING TREE** | `:31`, `:60`, `:159` | diff `@31`, `@60`, `@157` all add `BattleStateService.CreateBattleAsync` |
| 13 | `BattleStartService.cs:498-501` (A4) | "a lookup over the **three** transcribed definitions" — `TASK-213-content-reachability-decision.md:727` | **FIXED IN WORKING TREE** | `:501-503` | diff `@@ -498,2 +501,2 @@` → "a lookup over the five transcribed definitions"; `ResolveBoss` iterates `BossDefinitions.All` at `:515` |
| 14 | `BattleStartService.cs:70-72` (A6) | Remove the false "(GAME_RULES.md §17 steps 18–19 remain unimplemented)" — `TASK-213-content-reachability-decision.md:729` | **FIXED IN WORKING TREE** | `:70-74` | diff `@@ -71,2 +71,4 @@`; the clause now reads "every one of those stages is implemented elsewhere — in the Domain modules and in the `BattleStateService` resolution flow" |
| 15 | `BattleStateService.cs:1576-1581` (A5) | Retired §6.2 wording and the "Always-Active marker" — `TASK-213-content-reachability-decision.md:728` | **FIXED IN WORKING TREE** | `:1580-1587` | diff `@1576` removes "Its PassiveThreshold is therefore the Always-Active marker 0" and adds "the wording was corrected from the retired 'always active' form by TASK-124 … the non-charged marker (null, read back as 0)"; second site at `:1654` likewise |
| 16 | `BattleStateService.cs:113, :161-167, :454, :461-463` (A7) | Pet / Relic / Boss "selection is not implemented" — `TASK-213-content-reachability-decision.md:730` | **FIXED IN WORKING TREE** | `:113-117`, `:163-169`, `:458`, `:465-468` | diff `@@ -113,3 +113,5 @@`, `@@ -161,7 +163,7 @@` ("selection is not implemented end-to-end" → "selection is implemented end-to-end on the battle-start path"), `@454`, `@461` |
| 17 | `src/frontend/client/src/game/scenes/BattleScene.ts:1143` (`renderEventCallout`) | Correct the method name; the method is `showCallout` — `TASK-218A-relic-trigger-presentation-audit.md:643-646`, `:938` | **STILL STALE** — and it was **introduced by `9b5c5fe`**, not by this task | `:1161`; real method `showCallout` `:2104` (call site `:1888`) | occurrence counts: `afe5b14` = 0, `9b5c5fe` = 1, working tree = 1 |
| 18 | `src/frontend/client/src/game/GameViewport.ts:25` | Mirrored HUD claim — `TASK-212-post-…audit.md:1331` (code-comment row, "Report with TASK-217") | **STILL STALE** | `:25` | "No gameplay HUD is implemented yet"; the file is clean against `HEAD` (absent from `git status`) |

**Sites of the sibling ids, recorded for completeness and not fixed here:**
`docs/02-technical/SIGNALR_PROTOCOL.md:1132` (TASK-217C's Card-cost sentence,
still present — `TASK-219A-…:851`).

**Additional instances observed by this reconstruction but not in any citing
record's site list** (reported, not attributed): `REDIS_STATE.md:114`/`:227` are
the cited ones, but `docs/02-technical/GAME_STATE.md:2727` and `:2813` also name
`BattleResolutionService`, and `docs/02-technical/API_CONTRACTS.md:1113` still
names a "Discord Activity OAuth token exchange (§2, ADR-007)". These belong to
the same class as rows 1–9; no task assignment for them exists in the records
read for this reconstruction.

---

## 7. Declared file set

Markers are exactly: `COMMITTED-IN-9b5c5fe` · `WORKING-TREE-MODIFIED` ·
`WORKING-TREE-UNTRACKED` · `ABSENT`. `stream` is `recon` (the planned
documentation/comment scope this record reconstructs), `impl` (the atomicity
stream the artifacts also credit to the id), or `both`.

### 7.1 The reconciliation stream — this record's declared set

```text
stream  path                                                             marker
------  ---------------------------------------------------------------  ----------------------
recon   docs/02-technical/ARCHITECTURE.md                                WORKING-TREE-MODIFIED
recon   docs/01-game-design/BOSS_RULES.md                                WORKING-TREE-MODIFIED
recon   src/backend/GameServer.Application/Battle/BattleStartService.cs   WORKING-TREE-MODIFIED
recon   src/backend/GameServer.Application/Battle/BattleStateService.cs   WORKING-TREE-MODIFIED
```

Evidence per path:

- `ARCHITECTURE.md` — not in `9b5c5fe`'s 73-path list (`git show --name-only
  9b5c5fe`); modified against `HEAD`; version 1.9 preamble `:3-15` names
  TASK-217A as the decision source. **`both`** in substance: the same
  working-tree diff also contains the v1.8 atomicity sync (`:15-25`) attributed
  to `TASK-216 / TASK-217A / TASK-217B` / `TASK-217C`. The two streams share one
  file and one diff, so under TASK-223 §6.2 P-2 this path cannot be committed as
  a per-task slice.
- `BOSS_RULES.md` — modified against `HEAD`; `git show 9b5c5fe:docs/01-game-design/BOSS_RULES.md`
  contains **no** TASK-217A string, so `:729` is a working-tree addition. The file
  also carries a committed TASK-213/TASK-221 slice from `9b5c5fe` (37 lines), so
  it is shared.
- `BattleStartService.cs` / `BattleStateService.cs` — not in `9b5c5fe`'s list;
  modified against `HEAD`; the six comment blocks in §6 rows 10–16 are added
  lines in the working-tree diff. `BattleStateService.cs` is **`both`**:
  TASK-217B §12.2 Finding A / §10 Defects row A (`TASK-217B-…audit.md:681-690`,
  `:838`) also
  cites `BattleStateService.cs:2850-2873` on the implementation stream.

### 7.2 Sites the citing records named, whose edits the files attribute elsewhere

Listed so a reader does not fold them into this record's declared set. Each is
named in `TASK-212-post-…audit.md:682` or `:1332` as drift this task owns, but
the file's own working-tree preamble attributes the edit to **TASK-217C**:

```text
stream  path                                      marker                  attribution in the file
------  ----------------------------------------  ----------------------  -----------------------------
recon?  docs/02-technical/DATABASE.md             WORKING-TREE-MODIFIED   :8-13 "TASK-217C"
recon?  docs/02-technical/REDIS_STATE.md          WORKING-TREE-MODIFIED   :5-12 "TASK-217C"
recon?  docs/02-technical/TDD.md                  WORKING-TREE-MODIFIED   :3-9  "TASK-217C"
recon?  docs/02-technical/SIGNALR_PROTOCOL.md     WORKING-TREE-MODIFIED   not read in full here
```

`DATABASE.md` is additionally **`both`**: `:24` attributes v1.37 (the atomicity
contract) to `TASK-217A`.

**Explicitly NOT this record's declared set.** These four paths' edits are
attributed by their own texts to another id; declaring them here would
contradict those statements and TASK-223 §6.2 P-5. The divergence is resolved
*for commit purposes only* by the fact that
`tasks/completed/TASK-217C-apply-card-cost-authority-ruling.md` §7 (`:241-247`)
declares exactly these four paths as its own set, with the same
`WORKING-TREE-MODIFIED` markers and the same overlap warning
(`:264-267`). That is a second record agreeing on the boundary, not a Product
Owner decision on the underlying attribution (R-10 below). They are recorded
here only so the P-2/P-3 commit plan can see the split.

### 7.3 The implementation stream — declared under Product Owner Decision D-2

No TASK-217A record existed before this one. The paths below are credited to
`TASK-217A` by name in `TASK-217B-post-implementation-audit.md`, which names the
id as the audited implementation ("Atomic Battle Reward Persistence", `:1-3`)
and passes it, and by `git status`. TASK-224's Stop Condition 6 report lists
them (`tasks/active/TASK-224-commit-step-and-task-209-record.md:417-445`).

**Product Owner Decision D-2 (recorded under TASK-224): this record's declared
file set is EXTENDED to include the implementation stream below**, so that P-3
has a claiming record and the work can be committed under `TASK-217A`. The
stream was written before this record existed, so this record reconstructs no
acceptance criteria of its own for it; its verification evidence is
`TASK-217B-post-implementation-audit.md` §Verdict (PASS: 653 Application + 422
Infrastructure tests, 0 skipped, PostgreSQL on localhost:5433 reachable) —
cited, never upgraded into a TASK-217A result. The `?` rows below stay
`NOT PROVEN` for authorship and are declared so that a commit cannot omit them
silently.

```text
stream  path                                                                             marker
------  -------------------------------------------------------------------------------  ----------------------
impl    src/backend/GameServer.Application/Battle/IBattleEndTransaction.cs                WORKING-TREE-UNTRACKED
impl    src/backend/GameServer.Infrastructure/Postgres/BattleEndTransaction.cs            WORKING-TREE-UNTRACKED
impl    tests/backend/GameServer.Application.Tests/BattleEndAtomicityTests.cs             WORKING-TREE-UNTRACKED
impl    tests/backend/GameServer.Infrastructure.Tests/BattleEndAtomicityPostgresTests.cs  WORKING-TREE-UNTRACKED
both    src/backend/GameServer.Application/Battle/BattleResultService.cs                  WORKING-TREE-MODIFIED
impl    src/backend/GameServer.Application/Battle/IBattleResultPersistence.cs             WORKING-TREE-MODIFIED
?       src/backend/GameServer.Application/DependencyInjection.cs                       WORKING-TREE-MODIFIED
impl    src/backend/GameServer.Infrastructure/DependencyInjection.cs                      WORKING-TREE-MODIFIED
impl    tests/backend/GameServer.Application.Tests/BattleResultServiceTests.cs            WORKING-TREE-MODIFIED
impl    tests/backend/GameServer.Application.Tests/BattleResultTerminalFlowTests.cs       WORKING-TREE-MODIFIED
impl    tests/backend/GameServer.Application.Tests/BattleResultTestDoubles.cs             WORKING-TREE-MODIFIED
impl    tests/backend/GameServer.Application.Tests/PlayerXpBattleRewardTests.cs           WORKING-TREE-MODIFIED
impl    tests/backend/GameServer.Application.Tests/PetXpBattleRewardTests.cs              WORKING-TREE-MODIFIED
impl    tests/backend/GameServer.Infrastructure.Tests/InfrastructureRegistrationTests.cs  WORKING-TREE-MODIFIED
?       tests/backend/GameServer.Api.Tests/BattleHistoryEndpointTests.cs                  WORKING-TREE-MODIFIED
?       tests/backend/GameServer.Api.Tests/BattleResultEndpointTests.cs                   WORKING-TREE-MODIFIED
both    docs/02-technical/DATABASE.md                                                     WORKING-TREE-MODIFIED
```

Named by `TASK-217B-post-implementation-audit.md`: `IBattleEndTransaction.cs`
(`:96`, `:570`, `:821`), `BattleEndTransaction.cs` (`:97`, `:138`, `:782`),
`Infrastructure/DependencyInjection.cs` (`:110`, `:124-127`, `:768`),
`IBattleResultPersistence.cs:94-115` (`:148`), `BattleEndAtomicityTests.cs`
(`:387`, `:406`, `:716`), `BattleEndAtomicityPostgresTests.cs` (`:165`, `:503`),
`BattleResultServiceTests.cs` / `BattleResultTerminalFlowTests.cs` /
`PlayerXpBattleRewardTests.cs` / `PetXpBattleRewardTests.cs` (`:652-655`),
`BattleResultTestDoubles.cs:305-320` (`:441`), and the two Api test files
(`:524-525`). Rows marked `?` rest on `git status` alone: TASK-217B mentions
`BattleResultEndpointTests.cs` and `BattleHistoryEndpointTests.cs` at `:524-525`
and the Application `DependencyInjection.cs` nowhere, but in neither case does
TASK-217B attribute the edit to the TASK-217A change — so their authorship is
`NOT PROVEN`.

### 7.4 Paths this record declares as OPEN sites (no edit exists)

```text
stream  path                                                          marker
------  ------------------------------------------------------------  ----------------------
recon   docs/02-technical/ARCHITECTURE.md (:617, :85-162, :706)        WORKING-TREE-MODIFIED
recon   docs/02-technical/REDIS_STATE.md (:114, :227)                  WORKING-TREE-MODIFIED
recon   docs/02-technical/TDD.md (:82, :100, :107, :248, :259)         WORKING-TREE-MODIFIED
recon   src/frontend/client/src/game/scenes/BattleScene.ts (:1161)     COMMITTED-IN-9b5c5fe
recon   src/frontend/client/src/game/GameViewport.ts (:25)             COMMITTED-IN-9b5c5fe
```

`BattleScene.ts` and `GameViewport.ts` carry stale text in the committed state
and were **not** edited by this task; `git diff` shows no reconnaissance edit in
either.

---

## 8. Acceptance criteria and verification status

The criteria are the ones the citing records impose; none is invented. `[x]` =
proven by an artifact; `[ ]` = not satisfied.

```text
[ ] §3's component table names no component that does not exist in src/
    NOT SATISFIED — `DiscordService` remains (ARCHITECTURE.md:706).
[x] §4, §4.1 and §5 item 2 name the components that actually perform the work
    SATISFIED by the working-tree diff (@691, @705, @714, @731).
[x] The two named stale code comments are corrected
    SATISFIED — BattleStartService.cs:251-256; BattleStateService.cs:1588-1590.
[x] Sites A4, A5, A6, A7, A8 are corrected
    SATISFIED — see §6 rows 12-16.
[ ] §1's directory trees match src/
    NOT SATISFIED — six non-existent directories are still listed (§6 row 2).
[ ] The stale HUD claim is removed
    NOT SATISFIED — ARCHITECTURE.md:617, GameViewport.ts:25.
[ ] The drift echoed in DATABASE.md and REDIS_STATE.md is removed
    PARTIALLY — DATABASE.md's live note is fixed; REDIS_STATE.md:114/:227 is not
    (§6 rows 5-7).
[ ] TDD.md's Discord Activity residue is corrected
    NOT SATISFIED — five sites remain (§6 row 8).
[ ] The A-20 Discord residue is decided
    NOT SATISFIED BY DESIGN — the decision is the Product Owner's
    (TASK-212-post-…audit.md:994-997); no decision artifact exists.
[ ] BattleScene.ts:1143's stale method name is corrected
    NOT SATISFIED — BattleScene.ts:1161 (TASK-218A-…audit.md:643-646).
[x] No product decision was absorbed
    SATISFIED as far as the reconciliation stream goes — every correction is a
    statement of the as-built implementation; A-20 was left undecided.
[-] Tests, typecheck, build, E2E
    N/A — §9.
[-] Documentation-only, no production behaviour changed
    NOT PROVEN for the id as a whole: the id's implementation stream changed
    production code. PROVEN for the reconciliation stream's four paths beyond
    `BattleStateService.cs`'s shared comment region.
```

**Acceptance verdict: NOT SATISFIED.** The reconciliation stream's *code-comment*
half is complete and proven; its *document* half is incomplete. No citing record
asserts otherwise: `TASK-219A-…:850` and `TASK-221A-…:1000` both recorded the
task as still open, and both predate the working-tree comment fixes.

---

## 9. Tests / Validation

**N/A, and consistently so.** No citing record assigns a test, typecheck, build
or E2E requirement to TASK-217A:

- `TASK-213-content-reachability-decision.md:610` — Contract impact
  `DOCUMENTATION-ONLY`.
- `TASK-212-post-task-211-product-audit.md:1263-1268` — "Documentation-only in
  *production* terms".
- `TASK-224-…record.md:225-227` (the reconstruction's own template) — "Unit
  tests — N/A (no executable file is edited by this task)"; the reconciliation
  stream edits only comment text inside two `.cs` files, which no test suite
  asserts.

No test command was executed by this reconstruction. Nothing here establishes
that any suite passes; the last recorded suite results belong to other tasks and
are not restated.

**Implementation-stream verification is not this record's, and is attributed to
the audit:** `TASK-217B-post-implementation-audit.md` §Verdict (`:10-18`,
"**PASS**") and §Audit method (`:27-42`) report `dotnet build` 0 errors,
`GameServer.Application.Tests` 653 passed / 0 failed / 0 skipped and
`GameServer.Infrastructure.Tests` 422 passed / 0 failed / 0 skipped with
PostgreSQL on `localhost:5433` confirmed reachable. That evidence belongs to
TASK-217B's audit of the *atomicity implementation*; it is cited as-is and is
**not** upgraded into a TASK-217A acceptance result, because no TASK-217A record
ever existed to hold one.

---

## 10. Remaining Issues

Ordered by the citing records' own ranking. Each is an open site from §6.

```text
R-1  ARCHITECTURE.md §1's two directory trees still do not match src/ (§6 row 2).
     Six named directories do not exist; §1 was not touched by the working-tree
     diff. Still P1 documentation: AGENTS.md §6 routes backend work to §1.
R-2  ARCHITECTURE.md:617's "No gameplay HUD exists yet" and its mirror at
     GameViewport.ts:25 are stale. TASK-212-post-…audit.md:210 has recorded this
     as STILL STALE since TASK-212; nothing has fixed it.
R-3  TDD.md:82, :100, :107, :248, :259 still describe a Discord Activity client,
     self-contradicted by TDD.md:74-75 and by ADR-020 (TASK-212-post-…audit.md:1332).
R-4  REDIS_STATE.md:114 and :227 still name `BattleResolutionService`, a type that
     does not exist. Same class as GAME_STATE.md:2727/:2813 (not in any citing
     record's list).
R-5  ARCHITECTURE.md:706 still lists `DiscordService` in the component table while
     §2.3 (:645) says the Discord SDK is retired and ADR-020:26 says the frontend
     service is retired (§6 row 9).
R-6  TASK-212 §A-20's decision — delete the retired-Discord DI seam or record it
     as deliberately retained — has no artifact in the tree. The seam is still
     wired (Infrastructure/DependencyInjection.cs:114, :178) with four files under
     GameServer.Infrastructure/Discord/. OWNER: Product Owner
     (TASK-212-post-…audit.md:994-997, :1352).
R-7  BattleScene.ts:1161's `renderEventCallout` reference is stale and entered the
     tree in 9b5c5fe (TASK-218A-…audit.md:643-646).
R-8  DATABASE.md:271's historical version-history mention of
     `PersistenceRepository (Postgres)` survives the reconciliation. Judgement
     needed: a version-history paragraph is a record of what version 1.11 said,
     so correcting it might itself be a history edit. NOT DECIDED here.
R-9  The id-allocation conflicts set out in §5.3. OWNER: Product Owner
     (AGENTS.md §4/§20); same class as TASK-223 §10 F-3's reserved TASK-225.
R-10 Whether the edits TASK-217C's version prose claims (DATABASE.md:8-11,
     REDIS_STATE.md:5-12, TDD.md:3-9) belong to 217C or to 217A. That record now
     exists and declares them, but its authorship claim rests on version prose
     only (TASK-217C-…:224-230); part of R-9, not settled here.
```

---

## 11. Revision History

**Revision 1 — retrospective reconstruction (evidence-only).** Authored under
TASK-224 Product Owner Decision 2 = A
(`tasks/active/TASK-224-commit-step-and-task-209-record.md:335-381`), which
commissions "TASK-217A — architecture / component / directory reconciliation +
stale comments" and requires every unproven item to be labelled `NOT PROVEN`.
The original task file was never authored; no TASK-217A record existed under
`tasks/` before this one (`glob tasks/**/*217*` at the start of this
reconstruction → only `tasks/completed/TASK-217B-post-implementation-audit.md`;
six further records, including
`tasks/completed/TASK-217C-apply-card-cost-authority-ruling.md`, appeared in the
untracked set while this record was being written, and are cited where they bear
on it).

Scope of this record: it declares an exact file set (§7), classifies every site
the citing records named (§6), reports what is still open (§10), and stops at
the two conflicts it may not settle — the Discord-residue decision (R-6) and the
id-allocation conflicts (R-9, R-10) — per `AGENTS.md` §4 and §20.

What this record could not prove, stated plainly:

```text
NOT PROVEN  that TASK-217A executed §1's directory-tree reconciliation, the HUD
            claim, TDD's Discord residue, or REDIS_STATE.md:114/:227 — none of
            those sites has any edit in the working tree, so the honest reading
            is that the task's list was not completed.
NOT PROVEN  any verification result, test count, build result or date for
            TASK-217A. No artifact records one.
NOT PROVEN  that the reconciliation stream and the atomicity stream are one task
            or two. §5.2 records both readings; the artifacts do not force either.
NOT PROVEN  authorship of src/backend/GameServer.Application/DependencyInjection.cs
            and of the two Api test files listed in §7.3; they are in
            `git status` but are not named by the TASK-217B sections read.
NOT PROVEN  whether the four paths in §7.2 belong to TASK-217A or TASK-217C.
NOT FOUND   any acceptance, review, or completion artifact for TASK-217A;
            searched tasks/** (glob *217*), tasks/completed/, tasks/active/,
            tasks/backlog/ and the full-text of every record that names the id.
NOT FOUND   any date or commit hash attributable to TASK-217A's own work.
            `9b5c5fe` predates the reconciliation stream: `git show --name-only
            9b5c5fe` contains neither ARCHITECTURE.md nor TASK-217A's comment
            edits, and `git show 9b5c5fe:docs/01-game-design/BOSS_RULES.md`
            contains no TASK-217A string. The reconciliation stream is entirely
            uncommitted, so its only provenance is the working tree plus each
            document's own version header.
```

No Git write operation was performed by this reconstruction, consistent with the
prohibition in force and with TASK-223 §6.2 P-6.
