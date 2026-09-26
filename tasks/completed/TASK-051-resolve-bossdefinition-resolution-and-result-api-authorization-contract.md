# TASK-051 — Resolve BattleResult BossDefinition Resolution and Result API Authorization Contract

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ by path and section; does not copy rules or schemas.
-->

---

## Metadata

```text
Task ID:           TASK-051
Type:              DOCUMENTATION
Status:            DONE
Risk:              MEDIUM
Priority:          HIGH (P1 — clears two implementation-readiness blockers
                   for TASK-041, a CRITICAL task)
Primary Agent:     review
 Supporting Agents: persistence (BossDefinition contract accuracy),
                    backend (ARCHITECTURE.md module boundaries, endpoint
                    authorization conformance)
Workflow:          documentation/documentation-change.md
Skills:            discovery/documentation-discovery, discovery/impact-analysis,
                   quality/documentation-consistency, backend/persistence-analysis,
                   backend/api-contract-validation
Dependencies:      TASK-044 (DONE — BossDefinition schema), TASK-049 (DONE —
                   BossDefinitionId PK contract), TASK-042 (DONE),
                   TASK-043 (DONE), TASK-050 (DONE — Duration/CompletedAt/
                   Outcome)
Blocks:            TASK-041 (must not be edited by this task); read-only
                   context: TASK-034 (BLOCKED — owns session/auth design)
```

**Status note.** `BACKLOG` was correct for file creation; the task was **not**
moved to `READY`, because §15 requires a human answer to Decision A and Decision
B before that transition. The first execution pass reached `BLOCKED` (§13),
because neither decision was derivable and no answer had been supplied. On
2026-09-26 the human supplied **all nine answers** (recorded verbatim in §16).
The block is therefore resolved: the file returned `blocked/` → `active/`, both
canonical owners were updated, and the task reached **DONE** via
`IN PROGRESS` → `IN REVIEW` → `DONE` (`tasks/TASK_LIFECYCLE.md` §3).

> **Readiness outcome (see §17).** TASK-051 resolved every **contract**
> blocker for TASK-041, but its own answers introduced/confirmed two
> **unresolved dependencies** — a still-missing BossDefinition provisioning
> task (decision A3) and TASK-034, still `BLOCKED` (decision B1). Per the
> human's explicit instruction, **TASK-041 was therefore NOT moved to
> `READY`** and remains `Status: BACKLOG`, byte-identical.

---

## 1. Objective

Resolve, by explicit human decision and documentation only, the two
implementation-readiness blockers a fresh TASK-041 readiness audit found after
TASK-050 closed the previous contract gaps:

- **Decision A — `BossDefinitionId` resolution at battle completion.** The
  documented contract sources `BattleResult.BossDefinitionId` from the
  `BossDefinition` row whose `Identity` equals
  `BattleState.BossState.BossId` (`DATABASE.md` §1, "Identity and reward
  sourcing for `BattleResult`" item 2), but no resolution mechanism, resolver
  owner, or provisioning dependency is documented — and no read repository for
  `BossDefinition` exists. TASK-041 cannot invent an architecture component,
  a resolver, or a provisioning mechanism.
- **Decision B — `GET /api/battle/{battleId}/result` authorization.** The
  endpoint falls under the global authenticated-session requirement
  (`API_CONTRACTS.md` §1 header), but the session mechanism is TASK-034's
  open decision (BLOCKED), so the endpoint's caller authorization contract and
  TASK-041's dependency on TASK-034 are undetermined.

Documentation-only task. **No source, test, or migration changes.** The answers
are **human decision points** — this task makes them explicit, evidenced, and
answerable; it must not choose for the project (`AGENTS.md` §7, §20, §23).

---

## 2. Authoritative References

| # | Document | Why |
|---|---|---|
| 1 | `docs/02-technical/DATABASE.md` §1 — `BattleResult` block; "Identity and reward sourcing for `BattleResult`" item 2; `BossDefinition` contract note items 1, 2, 5; §2 (`BattleResult N ── 1 BossDefinition`); §3; §5 | Owns the FK contract, the Identity→BossDefinitionId sourcing statement, and the no-invented-provisioning rule |
| 2 | `docs/02-technical/API_CONTRACTS.md` §1 header (global authenticated-session requirement), §4 (result endpoint), §6 (error convention) | Owns the endpoint authorization surface |
| 3 | `docs/03-decisions/ADR/ADR-007*` item 4 (application session; mechanism open) | Session boundary rationale |
| 4 | `docs/03-decisions/ADR/ADR-014*` | Session-derived identity rejected; identity from battle state |
| 5 | `docs/02-technical/ARCHITECTURE.md` §2.1 (layer direction), §3 (module/component ownership), §4 item 4 (battle-end lifecycle), §5 (anti-overengineering) | Bounds any resolver-component answer |
| 6 | `docs/02-technical/GAME_STATE.md` §2.4 (`BossId` = canonical Identity, never `BossDefinitionId`) | State contract must not be reopened |
| 7 | `docs/01-game-design/BOSS_RULES.md` §6.4 (identity contract) | Three never-collapsed concepts |
| 8 | `docs/02-technical/REDIS_STATE.md` §2 (round-trip), §7 (no state in process memory) | Bounds Decision A options touching battle state |
| 9 | `docs/02-technical/TDD.md` §4 (Postgres off the hot path; terminal path may touch Postgres) | Bounds a DB-lookup option |
| 10 | `docs/00-overview/MVP_SCOPE.md` §1, §4 | Scope check (no new system added) |
| 11 | `tasks/backlog/TASK-041-implement-battle-result-persistence.md` | The blocked consumer; **read-only** (`AGENTS.md` §16) |
| 12 | `tasks/backlog/TASK-034-application-authentication-session-mechanism.md` | Session/auth owner (BLOCKED); **read-only — never modified, never implemented here** |
| 13 | `tasks/completed/TASK-042*`, `TASK-043*`, `TASK-049*`, `TASK-050*` | Resolved contracts (context; **do not reopen**) |
| 14 | `AGENTS.md` §4, §7, §16, §17, §18, §20, §21 | Conflict handling, no invented rules, task discipline, stop conditions |

---

## 3. Scope

### In Scope

1. Establish and document the evidence for Decision A (resolution mechanism +
   provisioning dependency) and Decision B (endpoint authorization +
   TASK-034 relationship), each ending in one explicit, human-approved answer
   or a `BLOCKED` report naming the exact question.
2. Update the **canonical owning document only** for each decided answer
   (expected owners: `DATABASE.md` §1 for Decision A; `API_CONTRACTS.md`
   §1/§4 for Decision B — verify per `documentation-change.md` rather than
   assuming), and make dependent references consistent only where they become
   stale.
3. Record precisely what TASK-041 needs afterward, as a report item —
   **without editing TASK-041**.
4. If a decision requires implementation work (e.g. a provisioning task, a
   resolver component), record it as a follow-up task/dependency in the
   completion report — do not create or implement it here unless the human
   explicitly requests a new task file.
5. Report discovered adjacent issues without fixing them (`AGENTS.md` §16).

### Out of Scope

- Any `src/`, `tests/`, or migration file change — no BattleResult entity,
  no BossDefinition repository, no resolver, no seed/`HasData`/startup loader,
  no BattleState change, no Redis serialization change, no SignalR change, no
  API controller change.
- Implementing authentication or deciding a session mechanism (JWT / opaque
  token / cookie / Discord passthrough) — **TASK-034 owns this**; this task
  only determines whether TASK-041's acceptance depends on that decision.
- Editing `TASK-041`, `TASK-034`, `TASK-050`, `TASK-042`, `TASK-043`,
  `TASK-049`, or any `tasks/completed/` file (`AGENTS.md` §16).
- Reopening any resolved contract (§6 Preserved Decisions).
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2.

---

## 4. Current State — Evidence (verified at creation; re-locate by search)

### 4.1 Blocker A — the sourcing statement exists; the mechanism does not

| Fact | Location |
|---|---|
| `BattleResult.BossDefinitionId` is "the key of the `BossDefinition` row whose `Identity` equals `BattleState.BossState.BossId` … resolved server-side on that path from battle state" | `DATABASE.md` §1, "Identity and reward sourcing" item 2 |
| "A caller resolves the row **by `Identity`** and then stores that row's `BossDefinitionId` as a foreign key" | `DATABASE.md` §1 contract note item 2 |
| `BattleResult.BossDefinitionId (FK → BossDefinition)`; `BattleResult N ── 1 BossDefinition (opponent)` | `DATABASE.md` §1 entity block, §2 |
| "**No provisioning mechanism is documented, and none may be invented** — a future provisioning-contract decision is required before any row exists. Only content-defined Bosses (currently 3) may ever be provisioned" | `DATABASE.md` §1 contract note item 5 |
| `BossDefinitionId` is content-supplied, "never derived from `Identity`", value form `boss-def-*`, carried on the Domain record (TASK-049) | `DATABASE.md` §1 note item 2 |
| Schema exists: `DbSet<BossDefinition>`, `BossDefinitionConfiguration`, migration `AddBossPersistence` | `GameDbContext.cs` L68; `Configurations/BossDefinitionConfiguration.cs`; `Migrations/20260926124429_*` |
| **No BossDefinition read repository exists** — `Postgres/Repositories/` holds only `PlayerRepository`, `PetRepository`, `CardRepository`, `RelicRepository` | directory listing |
| Battle start resolves the Boss from static Domain content, not the database: `ResolveBoss` iterates `BossDefinitions.All` | `BattleStartService.cs` L359–366 |
| The Domain content already carries both values side by side (`BossDefinitions.HoaLong` = `BossDefinitionId: "boss-def-hoa-long"` with its Identity) | `BossDefinitions.cs` L95–96 |
| Battle-end path has an in-process `BossConfiguration(BossDefinition Definition)` registry alongside the Pet one; documented "not authoritative state" pattern | `BattleStateService.cs` L213–219, L370, L446 |
| Process-memory state is forbidden; static content is not state | `REDIS_STATE.md` §7 item 5; `ARCHITECTURE.md` §5 |
| Boss rows are not provisioned anywhere (no insert/seed code) | code search (absence) |

**Consequence:** TASK-041 must produce an FK value for which no documented
resolution mechanism, no resolver owner, and no provisioning dependency exist —
and the FK relationship (§2) means a row must exist at insert time regardless
of which mechanism resolves the *value*. This is a contract/provisioning
decision, not an implementation detail.

### 4.2 Blocker B — the global requirement exists; the caller contract does not

| Fact | Location |
|---|---|
| "All endpoints (except `/api/auth/discord`) require an authenticated session … the application session mechanism itself remains undecided (`ADR-007` item 4, TASK-034)" | `API_CONTRACTS.md` §1 header |
| §4 documents only `404 BATTLE_NOT_FOUND`; no 401/unauthenticated behavior is specified for this endpoint; §6 defines no global error list | `API_CONTRACTS.md` §4, §6 |
| The existing requesting-player mechanism: `ResolveRequestingPlayerId()` reads `HttpContext.Items["GameServer.PlayerId"]`; when absent, `POST /api/battle/start` returns `401 { "error": "UNAUTHENTICATED" }` | `BattleController.cs` L102–117, L167–179 |
| Nothing populates that Items key yet — the session mechanism is TASK-034's open decision; the controller comment records "rejects a caller that presents none" | `BattleController.cs` L77–84, L106–111; code search (no writer) |
| `TASK-034` is `Status: BLOCKED` and forbids choosing any mechanism while blocked | `TASK-034` L10, L278 |
| `TASK-041` excludes "Session / authentication mechanism — TASK-034 (BLOCKED); reuse the endpoint's current requesting-player resolution, design nothing", yet its acceptance requires the endpoint to return the §4 response | `TASK-041` L93–94, L135–137 (read-only) |
| `BattleResult.PlayerId` comes from `BattleState.PlayerId` — persistence itself needs no session | `DATABASE.md` §1 sourcing item 2; `GAME_STATE.md` §2.8 |

**Consequence:** persistence is independent of TASK-034, but the endpoint's
acceptance is ambiguous: under the current mechanism an unauthenticated caller
can never read a result, and no document states whether TASK-041 may be
considered complete without TASK-034, must depend on it, or must split scope.

---

## 5. Decision Points (the deliverable)

Each question must be answered **by a human**. If an answer cannot be
obtained, report `BLOCKED` naming the exact question (`AGENTS.md` §4, §7, §20;
`.ai/README.md` §13). Never select an option for implementation convenience.

### Decision A — `BossDefinitionId` resolution mechanism and provisioning

- **A1. What is the authoritative resolution mechanism** that maps
  `BattleState.BossState.BossId` (Identity) to `BossDefinitionId` on the
  battle-end path? Which document owns that statement?
- **A2. Who owns/provides the resolver**, in `ARCHITECTURE.md` §3's terms
  (module/component), or is no resolver component needed because the chosen
  mechanism requires none?
- **A3. What is the provisioning dependency?** Can TASK-041 depend on
  `BossDefinition` rows existing in PostgreSQL — i.e. does a separate
  provisioning/content task have to exist first (A), does the chosen
  mechanism use an already-authoritative non-DB source (B), or does another
  documented mechanism apply (C)? The FK relationship (`DATABASE.md` §2) must
  be reconciled with the answer: if the value is resolved non-DB, what makes
  the FK satisfiable at insert?
- **A4. What happens when no `BossDefinition` is found** for the battle's
  Identity at battle end? (Battle start documents `BOSS_NOT_FOUND`; §4/§6 of
  `API_CONTRACTS.md` do not define battle-end behavior.) The behavior must be
  defined — but never invented as a new error contract without the human
  approving where it is documented.

**Options (evaluate against the listed criteria; choose only with human
approval, or report blocked):**

- **A-i — Resolve through PostgreSQL by Identity at battle end**
  (`WHERE Identity = BossState.BossId` → row's `BossDefinitionId`).
  *Must check:* no `BossDefinition` read repository exists today; existence
  of a read abstraction would be a separate implementation dependency;
  rows must be provisioned first (§1 note item 5 forbids inventing
  provisioning here); `TDD.md` §4 permits Postgres on the terminal path
  only; layer direction (`ARCHITECTURE.md` §2.1); who owns the provisioning
  task and whether TASK-041 may depend on it. Do **not** implement the
  repository or provisioning in this task.
- **A-ii — Carry `BossDefinitionId` in `BattleState`.**
  *Must check:* this changes the authoritative battle-state contract —
  `GAME_STATE.md` §2, Redis runtime JSON round-trip (`REDIS_STATE.md` §2),
  serialization, battle-creation snapshot semantics, and an explicit
  non-exposure statement for SignalR. Adding a state member is a
  staged-position change (compare TASK-043's precedent) and possibly an
  `AGENTS.md` §18 architecture trigger. **Do not choose this merely because
  it avoids a database query.** Do not modify `GAME_STATE.md` in this task
  beyond what the human's approved answer requires (and stop per §8 if it
  requires an ADR).
- **A-iii — Deterministic content mapping without a persistence lookup**
  (e.g. `boss-hoa-long → boss-def-hoa-long`).
  *Must check:* `DATABASE.md` §1 note item 2 records "never derived from
  `Identity`" for the *key value* — whether an explicit mapping table
  violates or implements that must be evaluated, not assumed; whether
  `DATABASE.md`, `BOSS_RULES.md`, or `ARCHITECTURE.md` authorize a mapping
  mechanism at all; whether it stays valid at the MVP's five BossDefinitions;
  and that the FK row-existence problem (A3) is still reconciled. **The
  current three mappings are not proof that a hard-coded mapping is an
  accepted architecture.**
- **A-iv — Another documented mechanism.** If authoritative docs support one
  (e.g. resolution from the static Domain `BossDefinitions` content that
  battle start already uses), document what the docs actually say. **Do not
  invent one**; a mechanism no document supports is a new design and stops
  this task.
- **A-v — not derivable** → `BLOCKED`, human decision required (the expected
  outcome unless the human supplies the basis).

### Decision B — Result-endpoint authorization and the TASK-034 relationship

- **B1. What is TASK-041's relationship to TASK-034?** Is TASK-034 a
  prerequisite for TASK-041's acceptance (A)? Does an existing architecture
  support a documented authorization mechanism independent of TASK-034 (B)?
  Should TASK-041's scope be explicitly split so persistence completes while
  `GET /api/battle/{battleId}/result` is deferred until TASK-034 (C)?
  The answer must be recorded where TASK-041's readiness is determinable —
  **without editing TASK-041** (deliver as a report item).
- **B2. What is the explicit unauthenticated behavior of the result
  endpoint?** (Existing pattern: `401 UNAUTHENTICATED` as in `POST
  /api/battle/start`; §4 currently documents only `404`.) If documenting a
  401 for §4, that is a contract addition the human must approve, with
  `API_CONTRACTS.md` as canonical owner.
- **B3. What is the caller-authorization requirement** — which player may
  read which result (ownership scoping), stated explicitly, consistent with
  `BattleResult.PlayerId` sourcing and with §4's documented failure modes?

**Constraints on every option:**

- **B-2 must only be considered if existing architecture explicitly supports
  it.** Forbidden: temporary tokens, query-string identity, BattleId
  ownership without authentication, Discord user ID from client input,
  client-supplied `PlayerId` — any caller-controlled identity is not
  authoritative (`AGENTS.md` §10, ADR-001, ADR-014).
- **This task must NOT decide** JWT vs opaque token vs cookie vs Discord
  passthrough. TASK-034 owns the mechanism; the task only decides whether
  and how TASK-041's acceptance depends on it.

---

## 6. Preserved Decisions (must remain true after this task)

The only remaining Boss question is *how Identity → `BossDefinitionId` is
resolved*; the only remaining API question is *how TASK-041 relates to the
unresolved authentication contract*. Do not reopen:

1. `BattleResultId` = `BattleId` (TASK-042)
2. `PlayerId` = `BattleState.PlayerId`; `PetInstanceId` =
   `BattleState.PetState.PetId` (TASK-042, TASK-043)
3. `BossState.BossId` = canonical Boss Identity, never `BossDefinitionId`
   (TASK-046/047/048)
4. `BossDefinitionId` = independent content-supplied PK, never derived from
   `Identity` (TASK-049)
5. `DurationTurns` = terminal `BattleState.Turn`; `CompletedAt` = server clock
   at durable write; `Outcome` = `"victory" | "defeat"` (TASK-050)
6. `RewardSummary` = `{}` until TASK-033 (TASK-042)
7. No `Status`/lifecycle field on `BattleState` (`GAME_STATE.md` §2.0.3)
8. No provisioning mechanism may be invented (`DATABASE.md` §1 note item 5,
   §5 item 4) — resolving this task may *record* a human-approved
   provisioning decision, never fabricate one
9. Battle-end ordering: result write then active-state delete
   (`ARCHITECTURE.md` §4 item 4, `REDIS_STATE.md` §3)

---

## 7. Acceptance Criteria

### BossDefinitionId

- [ ] The authoritative source of `BossDefinitionId` for
      `BattleResult` at battle completion is defined (document + section).
- [ ] The Identity → `BossDefinitionId` resolution mechanism is defined.
- [ ] Ownership of the resolver is defined (component/module per
      `ARCHITECTURE.md` §3, or an explicit statement that none is needed).
- [ ] The provisioning dependency is defined — including whether a separate
      BossDefinition provisioning/content task must precede TASK-041, and
      how the FK (`DATABASE.md` §2) is satisfied under the chosen mechanism.
- [ ] Behavior when no `BossDefinition` resolves is defined.
- [ ] No hard-coded undocumented mapping remains implicit.
- [ ] TASK-041 knows exactly how to obtain the FK value (delivered as a
      report item — TASK-041 not edited).
- [ ] No new undocumented architecture is silently introduced.

### API Authorization

- [ ] The caller authorization requirement for
      `GET /api/battle/{battleId}/result` is explicit (document + section).
- [ ] The relationship to TASK-034 is explicit (prerequisite / independent /
      scope split — one answer, written where TASK-041's readiness derives).
- [ ] Unauthenticated behavior of the endpoint is explicit.
- [ ] No client-controlled identity is accepted by any documented path.
- [ ] TASK-041 knows whether TASK-034 is a prerequisite (report item).
- [ ] No authentication architecture (format, token, claims, lifetime) is
      invented — TASK-034's BLOCKED ownership untouched.

### Both

- [ ] Each answer is recorded in its canonical owning document only, with
      dependent references fixed only where stale, version header bumped per
      the file's convention (`documentation-change.md`).
- [ ] Preserved Decisions (§6) verified unchanged after the edits.
- [ ] No source, test, or migration file changed; TASK-041, TASK-034,
      TASK-050, and all `tasks/completed/` files byte-identical.
- [ ] All relevant tests pass at the required validation depth
      (`core/validation.md` §2 — documentation validation).
- [ ] Quality review checklist passes (`quality/review.md` §1).
- [ ] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001).

---

## 8. Affected Files & Areas

```text
[ ] src/backend/                (forbidden)
[ ] src/frontend/               (forbidden)
[ ] tests/                      (forbidden)
[ ] docs/ (documentation only — expected canonical owners:
            DATABASE.md §1 for Decision A; API_CONTRACTS.md §1/§4 for
            Decision B; dependent references only if wording becomes stale)
[ ] tasks/ (this new task file only; no existing task modified)
```

---

## 9. Implementation Notes

- Follow `.ai/workflow/documentation/documentation-change.md`: identify the
  canonical owner → read referencing docs → check conflicts → update the
  smallest authoritative source → fix a dependent reference only if stale →
  validate no duplicate definition.
- **Ask, do not assume.** Present Decision A and Decision B with the §4
  evidence and §5 options; stop for the human answer. Record answers verbatim
  where they define a value or a dependency, as TASK-049/TASK-050 did.
- Line numbers are creation-time evidence; re-locate by search before editing.
- Do not derive, rename, or re-case any identity value; do not collapse
  `BossDefinitionId` / `Identity` / display name; do not treat
  `BossDefinitions.All`'s current three entries as authorization for a
  mapping mechanism.
- Any follow-up implementation work discovered (provisioning task, resolver
  component, endpoint 401 addition) is recorded in the completion report as a
  dependency/task proposal — never implemented here.
- Report format for any `BLOCKED` outcome: `.ai/README.md` §13's exact
  format, then set `Status: BLOCKED` and move the file to `tasks/blocked/`
  (`tasks/README.md` §10).

---

## 10. Testing Requirements

### Required Verification

```text
[ ] Search docs/ for the resolution mechanism / authorization statement —
    each decided answer is recorded exactly once, in its owning document
[ ] Re-read DATABASE.md §1 (sourcing note, note items 2 and 5) with §2 —
    the mechanism, the provisioning dependency, and the FK agree
[ ] Re-read API_CONTRACTS.md §1 header with §4 and §6 — the authorization
    requirement and the documented failure modes agree
[ ] Confirm Preserved Decisions (§6) unchanged (grep for each contract term)
[ ] Confirm no source/test file changed and no existing task file changed
    (git diff scoped to this task)
```

### Key Edge Cases

- FK insert when the `BossDefinition` row does not exist — must be resolved
  by A3's answer, not discovered by TASK-041.
- A battle whose Boss Identity has no matching definition at battle end —
  A4's answer.
- An unauthenticated or foreign caller reading a result — B2/B3's answer,
  consistent with §4's documented status codes.
- Code tests are **not required**: documentation-only; verification is the
  consistency checks above.

---

## 11. Stop Conditions

Universal stops in `AGENTS.md` §20 apply. Task-specific stops — STOP and
report instead of guessing if:

1. The BossDefinitionId resolution mechanism, the provisioning ownership, the
   missing-BossDefinition behavior, the TASK-041/TASK-034 relationship, or
   the required endpoint authorization behavior cannot be determined from the
   documents **and** the human declines to decide → `HUMAN DECISION REQUIRED`
   (report `BLOCKED` naming the exact question).
2. A chosen option requires an architecture change (battle-state model, new
   store, module-boundary move, resolver service) → ADR first
   (`AGENTS.md` §18); stop this task.
3. A chosen option would require inventing provisioning, a seed, `HasData`,
   a startup loader, or a migration-inserted row — or would reopen
   `DATABASE.md` §1 note item 5 / §5 item 4 without explicit human approval.
4. A chosen option requires deciding any authentication mechanism
   (format/token/claims/lifetime) → that is TASK-034's; stop.
5. An option is selected based on implementation convenience, on the current
   three BossDefinitions as proof of a mapping architecture, or on deriving
   `BossDefinitionId` from `Identity` (`AGENTS.md` §7, §20; TASK-049
   recorded "never derived from `Identity`").
6. The answer requires touching `src/`, `tests/`, TASK-041, TASK-034, or any
   `tasks/completed/` file (`AGENTS.md` §16).
7. The answer contradicts another authoritative document → report per
   `AGENTS.md` §4; do not silently resolve.
8. The skill budget (7) is exceeded → stop and decompose
   (`tasks/README.md` §12).

Unrelated issues discovered while editing are report-only (`AGENTS.md` §16).

---

## 12. Completion Evidence

**Status: IN REVIEW → DONE.** The first execution pass reached `BLOCKED` (§13)
because neither decision was derivable. On 2026-09-26 the human supplied **all
nine answers** (recorded verbatim in §16), each was verified against the
authoritative documents, and both canonical owners were updated. **No source,
test, or migration file was touched**; TASK-041 is byte-identical and still
`Status: BACKLOG`.

```text
Task Created          tasks/backlog/TASK-051-…md (backlog → blocked → active → completed)
Blockers Resolved     A: RESOLVED (A1–A4 recorded) — but A3 creates a NEW
                      preceding dependency (provisioning task)
                      B: RESOLVED as a contract; B1 makes TASK-034 a
                      prerequisite for TASK-041's acceptance
Decisions Required    A1/A2/A3/A4 and B1/B2/B3 — ALL ANSWERED by the human
Files Created         none (no provisioning task was created — that is the
                      human's/new-task decision, recorded as a dependency)
Files Modified        docs/02-technical/DATABASE.md        (v1.10 → v1.11)
                      docs/02-technical/API_CONTRACTS.md   (v1.7  → v1.8)
Files Explicitly Not Modified
                      TASK-041, TASK-034, TASK-050, TASK-042/043/049,
                      src/, tests/, migrations
Follow-ups Recorded   (1) NEW PREREQUISITE: a BossDefinition provisioning/content
                      task must precede any BattleResult write (TASK-041
                      dependency). NOT created by this task.
                      (2) TASK-034 (BLOCKED) is a prerequisite for TASK-041's
                      acceptance per Decision B1.
                      (3) Implementation follow-up for TASK-041: the
                      Infrastructure Postgres persistence boundary must expose
                      the Identity → BossDefinitionId lookup
                      (DATABASE.md §1 note item 2 sub-bullet).
```

### Server Authority & Scope Verification

- [x] Confirmed zero client-authoritative gameplay logic introduced (no source
      touched at all)
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1) — no system added
- [x] Confirmed TASK-041 unmodified and still `Status: BACKLOG`
- [x] Confirmed TASK-034, TASK-050, TASK-042/043/049 unmodified
- [x] Confirmed `src/`, `tests/`, migrations unmodified (`git status`)

---

## 13. STOP CONDITION

Reported 2026-09-26 by the executing agent. Follows `.ai/README.md` §13's exact
format. **Two** stop conditions fired (§11 Stop Condition 1), one per blocker;
both are reported.

```text
STOP CONDITION

Problem:
TASK-051 exists to turn two TASK-041 readiness blockers into deterministic,
documented contracts. Both blockers were re-audited against the current
authoritative documents. Neither is determined by any authoritative document,
and no human answer to either was supplied. Recording an answer would require
the agent to author an architecture/security decision the project has not made,
which AGENTS.md §7, §20 and §23 forbid. The task therefore stops before any
document edit.

Relevant sources:
Decision A — BossDefinitionId resolution mechanism / owner / provisioning
- docs/02-technical/DATABASE.md §1 "Identity and reward sourcing for
  `BattleResult`" item 2 — states the VALUE is "the key of the `BossDefinition`
  row whose `Identity` equals `BattleState.BossState.BossId` … resolved
  server-side on that path from battle state", but names NO mechanism.
- docs/02-technical/DATABASE.md §1 contract note item 2 — "A caller resolves the
  row **by `Identity`** and then stores that row's `BossDefinitionId` as a
  foreign key". "A caller" is unnamed: no layer, component, or interface is
  designated, and no read path is documented.
- docs/02-technical/DATABASE.md §1 contract note item 5 + §5 item 4 — "**No
  provisioning mechanism is documented, and none may be invented** … a future
  provisioning-contract decision is required before any row exists."
- docs/02-technical/DATABASE.md §2 — `BattleResult N ── 1 BossDefinition
  (opponent)`; §1 entity block — `BossDefinitionId (FK → BossDefinition)`. A row
  must therefore exist at insert time.
- docs/02-technical/ARCHITECTURE.md §3 — component table lists
  "PersistenceRepository (Postgres) … DATABASE.md" as the Infrastructure
  component; it names NO Boss-definition read component, and §5 item 1 forbids
  introducing a registry/plugin abstraction.
- docs/02-technical/TDD.md §4 item 3 — PostgreSQL is never read on the hot path;
  only the terminal path may touch it. This bounds (but does not select) a
  lookup option.
- docs/02-technical/GAME_STATE.md §2.4 — `BossId` is the canonical technical
  Identity and is explicitly "**not** `BossDefinitionId`"; "No second identity
  field … is added to `BossState`." Adding a carrier is a contract change to
  this document.
- docs/02-technical/REDIS_STATE.md §2 item 1, §7 item 5 — the serialized record
  must match `GAME_STATE.md` §2 exactly, and active state may not live in
  process memory.
- docs/01-game-design/BOSS_RULES.md §6.4 — three never-collapsed concepts;
  `BossDefinitionId` is "Owned there, not here" (i.e. by `DATABASE.md` §1).
- Evidence only, NOT authority: no BossDefinition read repository exists
  (`src/backend/GameServer.Infrastructure/Postgres/Repositories/` holds Player,
  Pet, Card, Relic only); battle start resolves the Boss from static Domain
  content (`BattleStartService.ResolveBoss` iterates `BossDefinitions.All`);
  the Domain `BossDefinition` record already carries `BossDefinitionId`
  alongside its `Identity`; no row is provisioned anywhere.

Decision B — GET /api/battle/{battleId}/result authorization behaviour
- docs/02-technical/API_CONTRACTS.md §1 header — "All endpoints (except
  `/api/auth/discord`) require an authenticated session … the application
  session mechanism itself remains undecided (`ADR-007` item 4, TASK-034)."
  The requirement is stated; the mechanism that satisfies it is not.
- docs/02-technical/API_CONTRACTS.md §4 — documents exactly one failure mode,
  `404 BATTLE_NOT_FOUND`. It defines NO unauthenticated behaviour and NO
  caller-ownership rule.
- docs/02-technical/API_CONTRACTS.md §6 — deliberately "does not enumerate an
  exhaustive global error list".
- docs/03-decisions/ADR/ADR-007 item 4 — "All subsequent REST endpoints and
  SignalR Hub connections authenticate using the application session token" —
  a boundary, with no format, claims, lifetime, or validation strategy.
- docs/03-decisions/ADR/ADR-014 Options C and D — session-derived identity
  rejected at battle end; ADR-014 governs the WRITE path only and decides
  nothing about the READ path's caller identity.
- tasks/backlog/TASK-034-...md — `Status: BLOCKED`; its own BLOCKED section
  records that "Authorization behavior — which endpoints/hub methods require an
  authenticated identity, and what an unauthenticated caller receives. NOT
  DECIDED." Its Stop Conditions forbid choosing a mechanism while blocked.
- Evidence only, NOT authority: `BattleController.ResolveRequestingPlayerId()`
  reads `HttpContext.Items["GameServer.PlayerId"]`, returns
  `401 { "error": "UNAUTHENTICATED" }` when absent, and nothing in the codebase
  populates that key. This is a placeholder, and TASK-034's own text states that
  implementation convention must not be promoted into design.

Conflict / missing information:
Neither blocker is an ambiguity the documents resolve by precedence — each is a
genuine missing rule at a contract boundary:

DECISION A — three independent gaps, all undetermined by the documents:
  A1  Resolution mechanism. The documents state WHICH value is needed and that
      it is obtained "by `Identity`", but no document authorizes any of the
      three candidate mechanisms. (i) a PostgreSQL lookup by `Identity` would
      require a documented read path that does not exist; ARCHITECTURE.md §3
      names no such component and §5 item 1 forbids inventing an abstraction.
      (ii) carrying `BossDefinitionId` in `BattleState` is forbidden as written
      by GAME_STATE.md §2.4 ("No second identity field … is added to
      `BossState`") and would additionally touch Redis serialization and
      snapshot semantics. (iii) a content-defined mapping is not authorized
      anywhere, and DATABASE.md §1 note item 2 records the key is "never derived
      from `Identity`". Choosing among these is an architecture decision
      (AGENTS.md §18), not a documentation clarification.
  A2  Resolver owner. ARCHITECTURE.md §3's component table designates no owner
      for `BossIdentity → BossDefinitionId`, and the only generic component
      ("PersistenceRepository (Postgres)") is not scoped to Boss definitions.
      No document assigns the step to Application, Infrastructure, or Domain.
  A3  Provisioning dependency. DATABASE.md §1 note item 5 and §5 item 4 state
      that NO provisioning mechanism is documented and that NONE may be
      invented, and that a provisioning-contract decision is required before
      any row exists. Because `BossDefinitionId` is an FK, at least one option
      (A-i) cannot satisfy the FK without that undecided provisioning task.
  A4  Missing-definition behaviour. No document defines what happens when
      `BossState.BossId` exists but no `BossDefinition` resolves at battle end.
      API_CONTRACTS.md §4 defines only the read-side 404; `BOSS_NOT_FOUND` is
      documented as a battle-START validation failure only. No document
      authorizes a 404, null, fallback id, skipped FK, or auto-created row here.

DECISION B — three further undetermined points:
  B1  TASK-041 ↔ TASK-034 relationship. No document states whether TASK-034 is
      a prerequisite for TASK-041's acceptance, whether persistence may land
      while the endpoint is deferred, or whether the task must be split. §1
      establishes the authenticated-session REQUIREMENT for the endpoint; it
      does not decide what an unsatisfied requirement means for task readiness
      or scope. Resolving this by splitting TASK-041 would itself be a scope
      decision about a task this task must not edit.
  B2  Unauthenticated behaviour. API_CONTRACTS.md §4 documents only
      `404 BATTLE_NOT_FOUND`. Whether the canonical contract gains
      `401 UNAUTHENTICATED` — and in which section it is owned — is a contract
      ADDITION. The existing `401 UNAUTHENTICATED` in code is a placeholder that
      TASK-034's own text says must not be promoted into design.
  B3  Caller ownership. No document states which player may read which result.
      API_CONTRACTS.md §4 defines no ownership scoping, and the requirement
      cannot be derived from §3's loadout-ownership validation, which is a
      battle-START input check, not a read-authorization rule. ADR-014's
      rejected option C is about the write path and does not settle the read
      path.

Searches performed (all negative for a determining statement):
- `docs/` for a BossDefinition read path, resolver, repository, or service
  component → 0 matches.
- `docs/` for any statement authorizing a content/Identity → BossDefinitionId
  mapping → 0 matches (`boss-def-*` appears only in DATABASE.md as a PK value).
- `docs/` for provisioning, `HasData`, seed, or startup-loader authorization
  → only the prohibitions in DATABASE.md §1 note item 5 and §5 item 4.
- `docs/` for battle-end missing-Boss behaviour → 0 matches.
- `docs/` for `401`/`UNAUTHENTICATED` outside `/api/auth/discord`'s upstream
  Discord failures → 0 matches.
- `docs/` for a result-endpoint caller-ownership rule → 0 matches.

Proposed resolution:
Obtain the human decisions recorded below, then re-run this task (or its
successor) to write each answer into its canonical owner. No new task is needed
for the decisions themselves — TASK-051 is the correct, minimal, already-scoped
vehicle. Each answer must name the owning document and section, per
`.ai/workflow/documentation/documentation-change.md`.

Waiting for:
The human's answers to A1–A4 and B1–B3, in the terms recorded in this task's
§5. None may be inferred from implementation convenience, the current source
tree, the three existing content mappings, `BossDefinitions.All`, sibling
repository patterns, TASK-034's placeholder `session_{guid}`, or the existing
`401 UNAUTHENTICATED` string — those are evidence only, not authority
(AGENTS.md §7, §20).
```

### 13.1 Smallest exact decision required from the human

Each item is stated so that a single sentence answers it. Items marked
**[A-v]/[B-d]** are the branches that apply if the answer is "keep the current
text" — they are still decisions, because they change what TASK-041 may assume.

```text
DECISION A — BossDefinitionId

A1  Which mechanism resolves BossState.BossId (Identity) → BossDefinitionId on
    the battle-end path?
    [ ] A-i  PostgreSQL lookup by Identity (requires authorizing a read path
             AND a provisioning task; ARCHITECTURE.md §3 names no component)
    [ ] A-ii  Carry BossDefinitionId in BattleState (amends GAME_STATE.md §2.4,
             which currently forbids a second identity field; also Redis
             serialization + snapshot semantics → AGENTS.md §18 ADR trigger)
    [ ] A-iii Deterministic content mapping without a persistence lookup
             (asserts DATABASE.md §1 note item 2's "never derived from Identity"
             does not forbid an explicit table — needs your confirmation)
    [ ] A-iv  Another documented mechanism → name the document and section
    [ ] A-v   None — the contract must stay as written and TASK-041 is
             responsible for obtaining the value at implementation time

A2  Which layer owns the resolution step, in ARCHITECTURE.md §3's terms —
    Application, Infrastructure (persistence repository), or Domain — or is no
    new component needed because A1's mechanism requires none?

A3  Provisioning: is a separate BossDefinition provisioning/content task a
    PREREQUISITE for TASK-041 (A3-a), does A1's mechanism use a non-DB source so
    provisioning is not required at that point (A3-b), does an existing
    documented provisioning mechanism apply (A3-c), or another mechanism
    (A3-d)? If A3-a, this task must record the dependency — it must not invent
    the seed/HasData/loader.

A4  What is the deterministic behaviour when BossState.BossId exists but no
    BossDefinition resolves at battle end — and in which document is that
    behaviour owned? (Do not default to 404/null/fallback/skip-FK without your
    answer.)

DECISION B — Result-endpoint authorization

B1  TASK-041's relationship to TASK-034:
    [ ] B1-a  TASK-034 is a prerequisite for TASK-041
    [ ] B1-b  TASK-041 implements persistence; the GET endpoint is deferred
              until TASK-034 (implies a TASK-041 scope split, which this task
              must not perform and must not edit into TASK-041)
    [ ] B1-c  An existing documented authorization mechanism applies
              independently of TASK-034 → name it
    [ ] B1-d  Another documented relationship

B2  Should the canonical API contract state an explicit unauthenticated
    response for GET /api/battle/{battleId}/result (e.g. 401 UNAUTHENTICATED in
    API_CONTRACTS.md §4), or is another behaviour correct — and which document
    owns it? (The requirement "unauthenticated callers must not receive
    BattleResult data" is already satisfied by §1; the RESPONSE is what is
    undocumented.)

B3  Which player may read which BattleResult — is the rule
    authenticated PlayerId = BattleResult.PlayerId, and where is that rule
    owned and stated? (Client-supplied PlayerId must not establish ownership
    under any answer.)
```

**Note on scope.** Answering B1-b would require a TASK-041 scope split. This
task did **not** perform it and did **not** edit TASK-041 (`AGENTS.md` §16); the
split is itself a human decision, since it changes a task file this task is
forbidden to modify.

---

## 14. Execution Evidence — Readiness Impact

### Blocker A — status: HUMAN DECISION REQUIRED

```text
A1 Resolution mechanism:      NOT DETERMINED — no document authorizes a
                              mechanism (DATABASE.md §1 states the needed
                              VALUE and that it is obtained "by Identity", and
                              nothing more; ARCHITECTURE.md §3 designates no
                              read component; GAME_STATE.md §2.4 forbids the
                              BattleState carrier option as currently written).
A2 Resolver owner:            NOT DETERMINED — ARCHITECTURE.md §3 defines no
                              owner for BossIdentity → BossDefinitionId.
A3 Provisioning dependency:   NOT DETERMINED — DATABASE.md §1 note item 5 and
                              §5 item 4 forbid inventing one and require a
                              future decision; the FK (§2) cannot be shown
                              satisfiable under the DB-derived option until
                              that decision exists.
A4 Missing-definition behavior: NOT DETERMINED — no document defines battle-end
                              behaviour when no BossDefinition resolves.
```

### Blocker B — status: HUMAN DECISION REQUIRED

```text
B1 TASK-041 ↔ TASK-034:       NOT DETERMINED — no document states whether
                              TASK-034 is a prerequisite, whether persistence
                              may land while the endpoint is deferred, or
                              whether TASK-041 splits. TASK-034 remains
                              Status: BLOCKED and untouched.
B2 Unauthenticated behavior:  NOT DETERMINED — API_CONTRACTS.md §4 documents
                              only 404 BATTLE_NOT_FOUND; adding 401
                              UNAUTHENTICATED is a contract addition needing
                              approval. §1's requirement still stands and still
                              forbids unauthenticated access.
B3 Caller ownership:          NOT DETERMINED — no document states which player
                              may read which result. The invariant "ownership
                              must never be established from a client-supplied
                              PlayerId" is already guaranteed by ADR-001 /
                              GAME_RULES.md §18 and §1, and was not weakened.
```

### TASK-041 impact

```text
Before:
BLOCKED by A + B.

After:
UNCHANGED — still blocked by A + B. This task did not remove either blocker,
because neither is derivable and no human answer was supplied. TASK-041 was
NOT implemented and was NOT moved to READY (still Status: BACKLOG,
byte-identical).
```

**What TASK-041 must re-check after the human answers** (report item only —
TASK-041 was deliberately not edited):

```text
[ ] A1 — the mechanism, and its owning document + section
[ ] A2 — whether a resolver component is required, and in which layer
[ ] A3 — whether a provisioning task precedes TASK-041 (a new dependency)
[ ] A4 — the battle-end behaviour when no BossDefinition resolves
[ ] B1 — whether TASK-041 depends on TASK-034, defers the endpoint, or splits
[ ] B2 — whether the endpoint must return an unauthenticated response
[ ] B3 — the caller-ownership rule the endpoint must enforce
[ ] Re-confirm the §6 Preserved Decisions are still unchanged
```

---

## 15. Validation Results

```text
Documentation consistency:  N/A — no document was edited (BLOCKED before edit,
                            per §13). No contradiction was introduced; the two
                            blockers are missing rules, not conflicting ones, so
                            AGENTS.md §4 was not triggered.
TASK-041 unchanged:         PASS — byte-identical; Status: BACKLOG
TASK-034 unchanged:         PASS — byte-identical; Status: BLOCKED
TASK-050 unchanged:         PASS — byte-identical
TASK-042/043/049 unchanged: PASS — byte-identical (tasks/completed/ untouched)
Source unchanged:           PASS — zero changes under src/
Tests unchanged:            PASS — zero changes under tests/
Migrations unchanged:       PASS — no migration added or modified
```

Verification method: `git status --porcelain` before and after execution, plus a
targeted re-audit of every §4 fact by search. The only working-tree change this
task produced is its own task file (status line + §12–§15), which is the
documentation-only change the lifecycle requires. No `docs/` file appears in
`git status`, confirming the "BLOCKED before edit" outcome.

---

## 16. Decisions Received — 2026-09-26 (A1–A4, B1–B3 answered; written to docs)

The human answered all nine decision points. Every answer was verified against
the authoritative documents before writing. **No STOP condition fired** and no
rule conflict arose (`AGENTS.md` §4 not triggered) — notably, **A-i was chosen
rather than A-ii**, so `GAME_STATE.md` §2.4's no-second-identity-field rule is
preserved and **no ADR is required** (`AGENTS.md` §18 not triggered).

```text
A1 Resolution mechanism:   A-i   — PostgreSQL lookup by Identity
A2 Resolver owner:         Infrastructure
A3 Provisioning:           a provisioning task precedes TASK-041
A4 Missing-definition:     Treat unresolved BossDefinition as a server-side
                           battle-resolution failure; do not persist
                           BattleResult and do not delete Redis active state.
                           The battle remains recoverable from authoritative
                           Redis state until the configuration/provisioning
                           issue is resolved.
A4 canonical owner:        DATABASE.md

B1 TASK-041 ↔ TASK-034:    prerequisite
B2 Unauthenticated:        define 401 UNAUTHENTICATED
B2 canonical owner:        API_CONTRACTS.md
B3 Caller ownership:       only authenticated owner (BattleResult.PlayerId)
                           may read the result
```

**Human-supplied rationale (recorded verbatim, not agent-authored):**

- **A1 = A-i** keeps `BattleState` unchanged and preserves the existing
  separation between canonical Boss Identity and persistence identity.
- **A2 = Infrastructure** keeps PostgreSQL lookup/persistence concerns outside
  Domain/Application business rules.
- **A3 = provisioning task precedes TASK-041** is required because
  `BattleResult.BossDefinitionId` is an FK. Resolution of the ID alone does not
  guarantee the referenced row exists.
- **A4** must fail closed: never insert an invalid FK, never fabricate a
  definition, never silently omit the result, and never delete the authoritative
  Redis state after an unresolved definition.
- **B1 = prerequisite** avoids modifying/splitting TASK-041. TASK-034 owns the
  authentication mechanism; TASK-041 should not invent an independent security
  path.
- **B2 = 401 UNAUTHENTICATED** makes the existing global authenticated-session
  requirement explicit for the result endpoint.
- **B3 = authenticated owner only** preserves the existing server-derived
  identity/ownership model and prevents client-controlled `PlayerId`.

**Where written (canonical owner only, per `documentation-change.md` §1–§2):**

| Decision | Document | Location |
|---|---|---|
| A1, A2 | `docs/02-technical/DATABASE.md` | §1 `BossDefinition` persistence contract note item 2, new sub-bullet "Resolution mechanism and resolver owner" |
| A3 | `docs/02-technical/DATABASE.md` | §1 note item 5, new sub-bullet "A separate provisioning decision is required, and it precedes `BattleResult` persistence"; §5 item 4 updated |
| A4 | `docs/02-technical/DATABASE.md` | §1 "Identity and reward sourcing for `BattleResult`", new item 3 "An unresolved `BossDefinition` fails the battle-end write closed" |
| B2, B3 | `docs/02-technical/API_CONTRACTS.md` | §4 — new `Response 401` block and new contract notes 6 (unauthenticated) and 7 (caller ownership) |

**Dependent documents verified and NOT edited** (each already states the rule
or defers ownership, so no wording became stale):

- `docs/02-technical/ARCHITECTURE.md` §3 already designates
  `PersistenceRepository (Postgres)  Infrastructure  DATABASE.md` — A2's answer
  is already the documented component ownership, so no edit was needed and no
  new component was introduced. §2.1's layer direction already requires this.
- `docs/02-technical/GAME_STATE.md` §2.4 already forbids a second identity field
  — A-i **preserves** it. No edit.
- `docs/02-technical/REDIS_STATE.md` §2 item 1 / §7 item 5 already govern the
  active-state record A4 must not delete, and §3's ordering already conditions
  the delete on the result write. No edit.
- `docs/02-technical/TDD.md` §4 item 3 already bounds Postgres to the terminal
  path. No edit.
- `docs/01-game-design/BOSS_RULES.md` §6.4 already defers `BossDefinitionId` to
  `DATABASE.md` §1 ("Owned there, not here"). No edit.
- `docs/03-decisions/ADR/ADR-007` / `ADR-014` — B1 keeps TASK-034's ownership
  intact and ADR-014's session-derived-identity rejection is preserved. No edit.

**Not reopened:** TASK-046/047/048 canonical Identities; TASK-049
`BossDefinitionId` value model; TASK-050 `Outcome`/`DurationTurns`/`CompletedAt`;
TASK-042/043 identity carriage; `BattleState` shape; any authentication
mechanism (TASK-034's).

---

## 17. Fresh TASK-041 Readiness Audit (post-TASK-051)

Performed as a read-only audit. **TASK-041 was not edited** (`AGENTS.md` §16).

### Contract blockers

| # | Blocker | Status after TASK-051 | Evidence |
|---|---|---|---|
| A | `BossDefinitionId` resolution mechanism | **RESOLVED (contract)** | `DATABASE.md` §1 note item 2 sub-bullet — PostgreSQL lookup by `Identity`, Infrastructure-owned |
| A | Resolver owner | **RESOLVED (contract)** | Same sub-bullet + `ARCHITECTURE.md` §3 component ownership |
| A | Missing-definition behaviour | **RESOLVED (contract)** | `DATABASE.md` §1 sourcing item 3 — fail closed, no row, no delete |
| A | Provisioning dependency | **RESOLVED (as a dependency)** — but **the dependency itself is UNRESOLVED** | `DATABASE.md` §1 note item 5 sub-bullet: a provisioning task **must precede** any `BattleResult` write. **No such task exists yet**, and no `BossDefinition` row is provisioned anywhere |
| B | Unauthenticated behaviour | **RESOLVED (contract)** | `API_CONTRACTS.md` §4 — `401 UNAUTHENTICATED` + note 6 |
| B | Caller ownership | **RESOLVED (contract)** | `API_CONTRACTS.md` §4 note 7 |
| B | TASK-041 ↔ TASK-034 | **RESOLVED (as a dependency)** — and it **blocks** | Decision B1 = prerequisite; **TASK-034 is `Status: BLOCKED`** |

### Verdict

```text
TASK-041 BLOCKERS:      ALL CONTRACT AMBIGUITIES RESOLVED (A1/A2/A4, B2/B3)
                        BUT TWO EXTERNAL DEPENDENCIES ARE NOW UNAMBIGUOUSLY
                        REQUIRED AND NEITHER IS SATISFIED:

  DEPENDENCY 1 — BossDefinition provisioning task
                 Does not exist. Required by DATABASE.md §1 note item 5
                 (TASK-051 decision A3) before ANY BattleResult row can be
                 inserted, because BossDefinitionId is an FK and no row is
                 provisioned anywhere today.

  DEPENDENCY 2 — TASK-034 (application authentication session mechanism)
                 Status: BLOCKED. Required by TASK-041 decision B1 as a
                 PREREQUISITE for TASK-041's acceptance, because the result
                 endpoint's caller identity cannot be resolved until the
                 session mechanism exists.

TASK-041 READINESS:     NOT READY — remains Status: BACKLOG.
```

**Per the human's explicit instruction (step 6), TASK-041 must NOT be moved to
`READY` while either dependency is unresolved.** Both are unresolved. TASK-041
therefore stays `BACKLOG`, byte-identical.

### What cleared (no longer blockers)

The **contract indeterminacy** that motivated TASK-051 is gone. TASK-041's
author now knows, from canonical documents:

```text
[ok] HOW the FK value is obtained  — DATABASE.md §1 note item 2 (PG lookup by
                                     Identity, Infrastructure owns it)
[ok] WHAT to do with no definition — DATABASE.md §1 sourcing item 3 (fail
                                     closed: no row, no Redis delete, battle
                                     stays recoverable)
[ok] WHAT the endpoint returns     — API_CONTRACTS.md §4 (401 UNAUTHENTICATED,
                                     404 BATTLE_NOT_FOUND, owner-only read)
[ok] Whether TASK-034 is required  — API_CONTRACTS.md §4 notes 6–7 + B1 =
                                     prerequisite
```

### What TASK-041 must do when the dependencies clear

```text
[ ] Confirm the BossDefinition provisioning task is DONE and rows exist for the
    three content-defined Bosses (BOSS_RULES.md §6)
[ ] Confirm TASK-034 is DONE (or its mechanism is documented) so the endpoint
    can resolve an authenticated caller identity
[ ] Implement the Identity → BossDefinitionId lookup on the Infrastructure
    Postgres persistence boundary (DATABASE.md §1 note item 2)
[ ] Implement the A4 fail-closed path: unresolved definition ⇒ no BattleResult
    row AND no battle:{battleId}:state delete
[ ] Implement the §4 401 UNAUTHENTICATED and owner-only read per notes 6–7
[ ] Re-confirm §6 Preserved Decisions are still unchanged
```

### Report-only (not fixed here, `AGENTS.md` §16)

1. **No BossDefinition provisioning task exists.** Recommended follow-up: create
   it as **TASK-052** (next sequential ID). It was **not** created by this task,
   because the human's instruction was to record the dependency, not to author
   new task scope; and §Scope forbids creating tasks unless explicitly
   requested.
2. `API_CONTRACTS.md` §1 lists `GET /api/battle/history` with no defining
   section — a pre-existing gap, unrelated to this task.

---

## Revision History

| Version | Date | Change |
| --- | --- | --- |
| 1.2 | 2026-09-26 | Human supplied all nine decisions (§16). Both canonical owners updated — `DATABASE.md` v1.10 → v1.11 (A1/A2 resolution mechanism + resolver owner; A3 provisioning dependency; A4 fail-closed missing-definition behaviour; §5 item 4 reconciled) and `API_CONTRACTS.md` v1.7 → v1.8 (B2 `401 UNAUTHENTICATED`; B3 owner-only read). Dependent documents verified; none stale. Fresh TASK-041 readiness audit (§17): all contract blockers resolved, but two dependencies (a new BossDefinition provisioning task, and TASK-034) remain unsatisfied → **TASK-041 stays `BACKLOG`**, not moved to READY. No source, test, or migration change. Status → DONE. |
| 1.1 | 2026-09-26 | Executed against the current authorities. All of §4 was re-verified and found accurate; §5's options were each evaluated against the listed criteria. Neither decision is derivable and no human answer was supplied, so §11 Stop Condition 1 fired for BOTH blockers: no `docs/` edit was made, the STOP CONDITION report (§13) and the exact human decision required (§13.1) are recorded, Status → BLOCKED, and the file moved `backlog/` → `blocked/` per `tasks/README.md` §10. TASK-041 remains BACKLOG and byte-identical. |
| 1.0 | 2026-09-26 | Created (BACKLOG) from the fresh TASK-041 readiness audit after TASK-050: two remaining blockers — BossDefinitionId resolution/provisioning (Decision A) and result-endpoint authorization / TASK-034 relationship (Decision B). Both marked HUMAN DECISION REQUIRED. |


