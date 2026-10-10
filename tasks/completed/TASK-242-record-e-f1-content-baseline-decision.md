# TASK-242 — Record the E-F1 MVP Content Baseline Product Owner Decision

<!--
  PRODUCT DECISION FORMALIZATION RECORD.

  This task formalizes a Product Owner decision that has already been made. It
  invents no rule, implements nothing, and authorizes nothing.

  ID provenance: highest existing task id across all folders was TASK-241
  (tasks/completed/TASK-241-battle-history-e2e-harness-selector-fix.md), so the
  next available id per tasks/README.md §3 is TASK-242. Verified before authoring.

  Source finding: tasks/completed/TASK-221A-post-implementation-audit.md §5.3
  (E-F1), restated at :1104 and :1156-1157. The decision request was carried
  forward by tasks/artifacts/MVP-ACCEPTANCE-RELEASE-CLOSURE-AUDIT.md §3 and §5
  table 6.1.
-->

---

## Metadata

```text
Task ID:           TASK-242
Type:              DOCUMENTATION (Product Owner decision record; no production code,
                   test, migration, schema, API, or gameplay change)
Status:            DONE
Risk:              NONE (documentary decision record only; no executable change)
Priority:          MEDIUM (resolves the single outstanding Product Owner decision
                   that TASK-221A §5.3 raised and the acceptance audit §5 table 6.1
                   tracks; it blocks no automated test)
Primary Agent:     review
Supporting Agents: N/A
Workflow:          documentation/documentation-change.md
Skills:            discovery/documentation-discovery,
                   quality/documentation-consistency,
                   quality/scope-validation
                   (3 skills — Simple budget, tasks/README.md §12)
Dependencies:      TASK-221A (DONE — the audit whose §5.3 raised E-F1),
                   TASK-213 (DONE — the content-reachability decision whose R-3
                   this confirms), TASK-221 (DONE — the implementation whose
                   creation-time grant this confirms)
Declared Files:    tasks/completed/TASK-242-record-e-f1-content-baseline-decision.md
```

---

## Objective

Record, in the repository's authoritative decision-record location, the Product
Owner's explicit resolution of finding **E-F1** raised by
`tasks/completed/TASK-221A-post-implementation-audit.md` §5.3, and confirm that the
recorded decision is already consistent with the existing authoritative
documentation and the implemented behaviour — so that no rule, contract, schema,
or gameplay change is required.

---

## Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1 "Content ownership & reachability (TASK-213 decision)" — the product decision this confirms
- `docs/02-technical/DATABASE.md` §2 items 1–3 — the starter-grant composition and creation-time semantics
- `docs/01-game-design/GAME_RULES.md` §20 — Rule Change Policy (the channel a change to this rule would require)
- `tasks/completed/TASK-213-content-reachability-decision.md` §3.1 (R-2, R-3) — the binding contract statements
- `tasks/completed/TASK-221A-post-implementation-audit.md` §5.3 (E-F1) — the finding this record resolves
- `AGENTS.md` §2 (source-of-truth hierarchy), §16 (task discipline), §23
- `tasks/TASK_LIFECYCLE.md` §6 (two-stage commit protocol)

---

## 1. The Product Owner decision, recorded verbatim

The Product Owner has explicitly decided:

```text
E-F1 — OPTION A.

The MVP starter content baseline applies exclusively to newly created
accounts. Existing accounts retain their current ownership unchanged.

No backfill or top-up is required.
```

**Decision identity:**

```text
Decision ID:      E-F1
Decision:         Option A — newly created accounts only
Made by:          Product Owner
Recorded by:      TASK-242 (this record)
Source question:  TASK-221A §5.3 (E-F1), :1104, :1156-1157
                  carried by the acceptance audit §3.2 and §5 table 6.1
```

---

## 2. Binding statements of this decision

```text
E-F1-1  The MVP starter content baseline applies exclusively to newly created
        accounts.

E-F1-2  Existing accounts retain their current ownership unchanged. An account
        whose Player row predates the TASK-221 grant keeps exactly the
        ownership it already has.

E-F1-3  No backfill, top-up, migration, or related implementation task is
        authorized or required by this decision. None is created by this record.

E-F1-4  The existing creation-time starter-grant rule remains authoritative and
        unchanged. This decision confirms it; it does not modify it.

E-F1-5  This decision resolves the specific open question recorded in TASK-221A
        §5.3 (E-F1). It resolves no other finding.

E-F1-6  This decision does not select a production hosting provider and does not
        authorize deployment work of any kind.
```

Confirmed, this record makes **no** change to `MVP_SCOPE.md`, `GAME_RULES.md`,
`DATABASE.md`, or any other authoritative document, and it changes no code, test,
schema, or contract.

---

## 3. The question this decision answers

Quoted from the acceptance audit §3.2, which restated TASK-221A §5.3:

> **Is the MVP content baseline defined over newly created accounts only?**
>
> *does the MVP's "5 Pets / 3 Basic Cards / 10 Relics" claim apply (a) only to
> accounts created under the TASK-221 baseline, or (b) to all accounts, including
> those created before it?*

```text
ANSWERED: (a) — newly created accounts only. Option A.
```

The audit's §3.4 recorded that option (b) had **no supporting evidence in `docs/`**
and would have required a `GAME_RULES.md` §20 Rule Change plus a one-off backfill
task, directly contradicting `MVP_SCOPE.md:116-120` and `TASK-213 R-3`. The Product
Owner selected (a), which is the behaviour already implemented and documented.

---

## 4. Rule consistency — verified against repository evidence

Re-derived from the working tree for this record, not carried forward from
TASK-221A.

### 4.1 Starter content is granted during Player creation

| Claim | Evidence |
|---|---|
| The grant is composed only on the new-Player branch | `src/backend/GameServer.Infrastructure/Postgres/Repositories/PlayerRepository.cs:33-39` — an existing Player returns at `:38` **before** `composeStarterGrant` is invoked at `:66`; `:66-69` (compose → stage → `Players.Add`) is reachable only past that early return |
| The composition cannot target an existing Player | `src/backend/GameServer.Application/Players/PlayerStarterGrantFactory.cs:166-171` — "the composition deliberately addresses no `Player` and the boundary binds the owner when it stages the rows. This keeps 'creation-time only' structural: **there is no overload of this operation that could be called for an already-existing Player**" |
| The write is atomic | `PlayerRepository.cs:69-95` — one `SaveChangesAsync` (`:73`) for the Player and all ownership rows; both catch branches call `DiscardStagedBatch` (`:78`, `:89`), so it is 18 rows or none |
| Existing Player records are returned unchanged | `PlayerRepository.cs:36-38` — `if (existing is not null) { return existing; }`. No read, no write, no comparison against the expected counts |

### 4.2 The baseline is 5 Pets / 3 Basic Cards / 10 Relics

| Category | Count | Evidence |
|---|---|---|
| Pets | **5** | `PlayerStarterGrantFactory.cs:82-89` names `pet-xich-lang`, `pet-bach-ho`, `pet-huyen-quy`, `pet-thanh-xa`, `pet-son-hung` |
| Basic Cards | **3** | `PlayerStarterGrantFactory.cs:99-104` names `card-heal`, `card-shield`, `card-power-charge` |
| Relics | **10** | `PlayerStarterGrantFactory.cs:126-138` names all ten (`relic-berserker-core` … `relic-battle-instinct`) |
| Total | 18 rows | `DATABASE.md:1465` — "A single `SaveChangesAsync` commits the Player and all 18 starter ownership rows in one atomic database transaction"; `:1450-1451` — "(5 `Pet`, 3 `PlayerUnlockedCard`, 10 `Relic`)" |

Arithmetic: 5 + 3 + 10 = 18 — consistent with `DATABASE.md:1450-1451`, `:1465`,
`:1474`.

### 4.3 There is no post-creation backfill or top-up

| Claim | Evidence |
|---|---|
| The rule is stated negatively and authoritatively | `DATABASE.md:1457-1459` — "**Not a repair / top-up mechanism:** The initialization is strictly bound to Player creation. The system must **NOT** evaluate conditional top-ups (e.g. 'if Player has no Pet / fewer than 3 Cards / no Relics → grant')" |
| The contract forbids it too | `TASK-213` §3.1 **R-2** (`:310-311`) — "The grant is idempotent by construction and runs only on the new-Player branch; **there are no conditional top-ups**"; **R-3** (`:312`) — "An account's owned content set does not change after creation in MVP" |
| No such code path exists | The only production call site of the composition is the creation branch (`PlayerRepository.cs:66`). No conditional re-grant, comparison-against-expected-counts, or "repair" path was located |

**Conclusion.** The decision records behaviour that is **already implemented and
already documented**. Per `AGENTS.md` §16 and the task's instruction, **no
implementation task was created** for behaviour that is already implemented and
documented.

---

## 5. Documentation consistency assessment

### 5.1 Do existing rules already express Option A?

**Yes — all three authoritative sources already state Option A.** Verbatim:

```text
MVP_SCOPE.md:105-107   "The whole MVP content set is owned by a newly created
                       Player. The deterministic Player-creation starter grant
                       (DATABASE.md §2) IS the MVP content grant — it is not a
                       minimum bootstrap"

MVP_SCOPE.md:115-120   "MVP has NO acquisition system. No unlock, drop, purchase,
                       claim, reward-grant, gacha, shop, quest, or progression
                       path adds content to an account after creation: an
                       account's owned content set is fixed at creation."

DATABASE.md:1454-1456  "**Existing Player:** Authenticating an existing Player
                       performs no starter grant; the starter initialization path
                       is reachable only when a new `Player` row is inserted."

DATABASE.md:1457-1459  "**Not a repair / top-up mechanism:** … The system must
                       NOT evaluate conditional top-ups"

TASK-213 §3.1 R-3      "An account's owned content set does not change after
                       creation in MVP."
```

`MVP_SCOPE.md`, `DATABASE.md` §2 item 3, `TASK-213` R-2/R-3, and the implemented
code are **mutually consistent** under Option A. The decision is therefore a
**confirmation of the documented rule**, not a change to it.

### 5.2 Were authoritative rule changes necessary?

**No.** Because `MVP_SCOPE.md`, `DATABASE.md`, and `TASK-213` already express
Option A, this record makes **zero** edits to:

```text
docs/00-overview/MVP_SCOPE.md        — unmodified
docs/01-game-design/GAME_RULES.md    — unmodified
docs/02-technical/DATABASE.md        — unmodified
any other file under docs/           — unmodified
```

No `GAME_RULES.md` §20 Rule Change is required or requested: a Rule Change is the
channel for **changing** a rule, and this decision changes none.

### 5.3 Non-blocking observations reported, not fixed

Per `AGENTS.md` §16, the following are reported and **not** fixed by this record.
Neither is caused by, nor resolved by, the E-F1 decision.

```text
N-1  The acceptance audit artifact (tasks/artifacts/MVP-ACCEPTANCE-RELEASE-
     CLOSURE-AUDIT.md) is untracked-but-staged and records "PO DECISION
     REQUIRED" for E-F1 at :19, :45, :253, :388. It would now read as stale.
     This record does NOT edit or unstage that artifact — the task forbids
     changing its staging status, and it is owned by the acceptance-audit task,
     not by this one. Recording the decision here is what retires the finding;
     refreshing the audit's status line is a separate authorization.

N-2  Documentation drift items O-F1, D-F1, D-F2, D-F3, D-F6 (TASK-221A §2.4,
     §7.2) remain open and already have an owner (TASK-217B). They are unrelated
     to E-F1 and are not resolved by it.
```

---

## 6. Acceptance impact

```text
E-F1: RESOLVED by this record.
```

E-F1 is resolved **because** the decision is now recorded in the repository's
required authoritative location for Product Owner decisions — a task record under
`tasks/completed/`, following the established precedent (TASK-061, TASK-104,
TASK-108, TASK-163, TASK-171, TASK-215A, TASK-224) — and because it is consistent
with `MVP_SCOPE.md` §1, `DATABASE.md` §2 item 3, `TASK-213` R-2/R-3, and the
implemented behaviour.

**This record does not claim that other findings are resolved.** The following
remain open and are explicitly unaffected by E-F1:

```text
Acceptance audit §5 table 1.9 / 2.6   Battle-history E2E suite — was failing on a
                                      pre-existing harness selector defect
                                      (F-1). NOTE: TASK-241 (completed after the
                                      audit's HEAD) fixed the selector lookup;
                                      re-verification of the suite is that task's
                                      evidence, not this record's claim.

Acceptance audit §5 table 2.7          E2E run procedure undocumented (F-2)
Acceptance audit §5 table 2.8          Pristine-machine E2E reproducibility
Acceptance audit §5 tables 3.7, 4.4-4.6, 5.1-5.2, 5.6-5.7, 6.2, 7.3-7.4
                                       Release-process gaps; hosting remains a
                                       PO decision (6.2 / 5.1)

TASK-221A findings R-F1, R-F2, R-F3, P-F2, P-F3, O-F1, D-F1…D-F7
                                       Unrelated documentation / coverage items

Pet Passive Option A (audit §5 table 6.3) NOT REOPENED by this record
```

---

## 7. Scope confirmation

```text
✔ Formalized the explicit E-F1 decision (Option A) and its provenance
✔ Verified the authoritative record location against repository governance
✔ Verified Option A against the creation-time starter-grant rule from source
✔ Made zero changes to MVP_SCOPE.md, GAME_RULES.md, DATABASE.md, or any docs/ file
✔ Created no backfill, top-up, migration, or implementation task
✔ Created no hosting decision, provider choice, or infrastructure
✔ Modified no completed task record (TASK-213, TASK-221, TASK-221A untouched)
✔ Staged, modified, or unstaged no file outside this record's declared file set
✔ The staged acceptance-audit artifact was left exactly as found
✔ No push, merge, rebase, reset, amend, or history rewrite
✔ The six pre-existing local commits remain unpublished
```

**Out of scope (untouched):** backfill/top-up implementation; database migrations
or schema changes; starter content or gameplay rule changes; production hosting
selection; CI/CD or deployment implementation; resolution of other Product Owner
decisions; Pet Passive scope; any Git push or remote publication.

---

## 8. Completion Evidence

### Commit

- Implementation slice (Phase A): `N/A — documentation-only task with no implementation slice`
- Phase B completion-record commit: `dc6f481005296982c826a22ce5a25446ea197fa3`
- Subject: `docs: record the e-f1 mvp content baseline product owner decision`
- Owns: `TASK-242`
- Files: `tasks/completed/TASK-242-record-e-f1-content-baseline-decision.md`
- Shared with: `none`
- Unowned / pre-existing: `none`

*(This task's sole deliverable is the decision record itself. It declares no
implementation file, so no Phase A slice exists — the record is the deliverable
and is filed under the Phase B completion-record commit per `TASK_LIFECYCLE.md`
§6, Option 1A. The commit was created path-scoped via `git commit --only`, so
the pre-existing staged `tasks/artifacts/MVP-ACCEPTANCE-RELEASE-CLOSURE-AUDIT.md`
entry was neither included nor altered: it remains staged with the identical blob
`38e4b26` before and after.)*

### Changed Files

- `tasks/completed/TASK-242-record-e-f1-content-baseline-decision.md` — this decision record (new file; the only file this task authors)

### Validation Results

- `git diff --cached --name-status` before the Phase B commit — PASS (staged set is exactly this record; the pre-existing staged audit artifact was excluded without altering its index entry)
- `git status --porcelain` after the Phase B commit — PASS (only the pre-existing staged audit artifact remains staged; no other entry)
- `git rev-list --left-right --count origin/master...HEAD` — PASS (0 behind, unpushed; no push performed)
- Documentation consistency check — PASS (Option A already expressed by `MVP_SCOPE.md` §1, `DATABASE.md` §2 item 3, `TASK-213` R-2/R-3)
- No production code, test, schema, migration, or contract file changed — PASS by declared file set

### Server Authority & Scope Verification

- [x] Confirmed zero client-authoritative gameplay logic (no code changed)
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1 — this decision confirms §1, it does not expand it)
- [x] Confirmed P-1…P-6 commit policy compliance (no batching of completion records; no task id in the commit subject; no history rewrite)

---

## 9. Remaining boundaries

```text
Hosting:                              UNDECIDED. This record selects no provider,
                                      platform, or hosting architecture.

CI/CD and production infrastructure:  NOT STARTED. No Dockerfile, pipeline,
                                      deployment script, or infrastructure was
                                      created or authorized.

Backfill / top-up:                    NOT AUTHORIZED and NOT REQUIRED under
                                      Option A. No such task exists.

Local commits:                        REMAIN UNPUBLISHED. No push, merge, or
                                      publication was performed. The six commits
                                      the task identified as pre-existing
                                      (8f12b10, daf4f6a, e26ca11, 5a56ef1,
                                      ce15e07, 385acfb) are all still present and
                                      still unpublished; this record adds exactly
                                      one further commit (dc6f481) on top.
```
