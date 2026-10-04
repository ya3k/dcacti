# TASK-164 — Apply the `GET /api/battle/history` Response Contract

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ by path and section — it does NOT copy game rules,
  formulas, schemas, or contracts.

  THIS TASK IS A DOCUMENTATION / CONTRACT-APPLICATION TASK.

    TASK-163 (DONE)
      Product Owner decisions, 24/24 recorded
            ↓
    TASK-164  ← THIS TASK
      authoritative API contract written into API_CONTRACTS.md
            ↓
    implementation task (NOT created here)
      GET /api/battle/history implementation

  THIS TASK DECIDES NOTHING. Every statement it writes is either (a) one of
  TASK-163's 24 recorded Product Owner decisions, or (b) an element already
  owned by an existing authoritative section that this task references rather
  than invents. It implements no endpoint and creates no downstream task.

  BOUNDARY: documentation only. Zero files under src/ or tests/. TASK-163 is
  immutable and was not modified. This task's only substantive output is
  docs/02-technical/API_CONTRACTS.md.
-->

---

## Metadata

```text
Task ID:           TASK-164
Type:              DOCUMENTATION (TASK_TYPES.md §2 — "Change docs/ content —
                   documentation is the primary output, not code"; workflow
                   documentation/documentation-change.md)
Status:            DONE
Risk:              LOW–MEDIUM (TASK_TYPES.md §4 — DOCUMENTATION baseline is
                   LOW–MEDIUM. MEDIUM rather than LOW for the same reason
                   TASK-163 recorded: the change fixes a public REST response
                   contract that must remain consistent with §4's result
                   endpoint and with the persisted BattleResult member set.
                   No source is touched.)
Priority:          MEDIUM (it gates the last undocumented endpoint in the
                   authoritative REST surface. It unblocks the implementation
                   task; it does not gate the core battle loop.)
Primary Agent:     review (TASK_TYPES.md §2 / AGENT_SELECTION.md §1 —
                   "Documentation change: Primary Agent Review". This task
                   authors a contract from a recorded decision and decides no
                   value, which is the Review Agent's posture.)
Supporting Agents: backend (API_CONTRACTS.md owns the REST surface; the §4
                   result endpoint is the shape /history extends),
                   persistence (DATABASE.md §1 owns BattleResult and the
                   8-member RewardSummary; §4 owns the index the ordering
                   relies on — consulted to CONFIRM, not to author),
                   realtime (consulted to CONFIRM no SignalR member is added;
                   the endpoint is REST-only)
Workflow:          documentation/documentation-change.md
Skills:            discovery/documentation-discovery,
                   discovery/impact-analysis,
                   backend/api-contract-validation,
                   quality/documentation-consistency,
                   quality/scope-validation
                   (5 skills — Normal budget, tasks/README.md §12)
Dependencies:      TASK-163 (DONE — the sole Product Owner decision source,
                     24/24 slots. IMMUTABLE, read-only, NOT modified. This
                     task's INPUT),
                   TASK-070 (DONE — established §5.5's list-response
                     conventions, including "empty collection 200 with []",
                     "ordering none defined" scoped to §5.1/§5.3/§5.4, and
                     "pagination none in MVP". IMMUTABLE, read-only),
                   TASK-051 (DONE — established §4 notes 6–7: 401
                     UNAUTHENTICATED for an unauthenticated caller, owner-only
                     read, ownership never from client input. IMMUTABLE),
                   TASK-042 / TASK-041 (DONE — BattleResult identity, the
                     reward-summary contract, and the PlayerId/CompletedAt
                     persistence the ordering reads. IMMUTABLE),
                   TASK-089 / TASK-068 (DONE — the landed 8-member
                     RewardSummary and its symmetrical victory/defeat shape
                     that §4.5 inherits by reference. IMMUTABLE)
Blocks:            The backend implementation task for
                   GET /api/battle/history. NOT created here.
Estimate:          Simple (one docs/ file, three edits; no code, no tests)
```

**Type classification note.** `DOCUMENTATION`, not `ARCHITECTURE` and not
`FEATURE`. No gameplay rule changed, no architecture changed, no schema
changed, and no endpoint behaviour changed. What changed is the *content* of
an authoritative contract document for an endpoint that was already listed in
its §1 summary — exactly `TASK_TYPES.md` §2's DOCUMENTATION definition.
`documentation/documentation-change.md` §1's flow is followed end to end:
canonical owner identified (D-6.1), related documents read, conflicts checked,
the smallest authoritative source updated, dependent references updated, and
consistency validated.

---

## Objective

Apply TASK-163's 24 recorded Product Owner decisions to the canonical REST
documentation, so that `docs/02-technical/API_CONTRACTS.md` contains a
complete, standalone, implementation-ready contract for
`GET /api/battle/history` and a later implementation agent can implement the
endpoint without inventing its response shape, ordering, pagination, filters,
authorization, ownership, empty behaviour, active-battle behaviour, or
persistence semantics.

This task implements nothing and creates no downstream task.

---

## Authoritative References

- **`tasks/backlog/TASK-163-resolve-battle-history-endpoint-response-contract.md`**
  — **the sole Product Owner decision source.** Its §"Decisions" carries all 24
  explicit answers (D-1.1–D-1.5, D-2.1–D-2.5, D-3.1–D-3.3, D-4.1–D-4.3,
  D-5.1–D-5.3, D-6.1–D-6.5). This task's INPUT. **IMMUTABLE — not modified.**
- `AGENTS.md` **§4** (never silently resolve a conflict), **§7** (invent
  nothing), **§10** (server authority), **§16** (report, do not fix inline),
  **§17** (documentation change rule), **§18** (architecture change rule),
  **§20** (stop conditions), **§21** (output discipline), **§23** (implement
  documented intent, do not design on the project's behalf)
- `.ai/README.md` **§6** (source-of-truth rule; `docs/` wins), **§13** (stop
  conditions), **§14** (MVP protection)
- `.ai/workflow/documentation/documentation-change.md` **§1** (flow), **§2**
  (no duplication, ever — the governing rule for this task's shape), **§3**
  (determining the canonical owner), **§4** (composition)
- `docs/02-technical/API_CONTRACTS.md` **§1** (the endpoint summary that
  already listed `GET /api/battle/history`; the global authentication rule),
  **§2.8** (the session mechanism — `ADR-015`), **§4** (the single-result
  endpoint whose shape `/history` extends, and notes 1–7), **§5.5** (the
  collection list conventions, scoped by its own heading to §5.1/§5.3/§5.4),
  **§6** (the error envelope), **§7** (what is deliberately not in the
  document)
- `docs/02-technical/DATABASE.md` **§1** (`BattleResult`: `BattleResultId` =
  `BattleId` one row per battle; `PlayerId`; `Outcome`; `DurationTurns`;
  `CompletedAt`; the 8-member `RewardSummary`; "Duration and completion
  sourcing for `BattleResult`" items 1–2), **§2** (the `Player 1 ── N
  BattleResult` relationship; the `N ── 1` Pet and BossDefinition
  relationships that are **not** exposed), **§4** (the index list including
  `BattleResult(PlayerId, CompletedAt DESC) — battle history, most recent
  first`)
- `docs/02-technical/ARCHITECTURE.md` **§3** (the `PersistenceRepository
  (Postgres)` boundary the read passes through)
- `docs/03-decisions/ADR/ADR-001-server-authoritative-battle.md`,
  `ADR-006-postgresql-persistence.md`,
  `ADR-014-battle-state-player-identity.md` (`PlayerId` identity),
  `ADR-015-application-session-authentication-contract.md` (the session that
  resolves the caller's identity) — checked for conflict; **none modified**
- `docs/00-overview/MVP_SCOPE.md` **§1** (persistent storage / Battle Results
  are IN), **§2** (nothing OUT may be introduced)
- `tasks/completed/TASK-161-apply-task-160-pet-statuseffects-and-boss-live-hp-decisions-to-authoritative-documentation.md`
  — the structural precedent for a decision-application documentation task

---

## Scope

### In Scope

1. Add a dedicated §4.5 contract section for `GET /api/battle/history` to
   `API_CONTRACTS.md`, as D-6.1 requires.
2. Reference it from §1's endpoint summary, per this repository's convention.
3. Update the document's version header per the existing changelog convention.

### Out of Scope

- **Any source code.** Zero files under `src/`. The endpoint is NOT
  implemented, no controller/repository/service is touched, and
  `IBattleResultRepository`'s operation set is NOT extended.
- **Any test.** Zero files under `tests/`.
- **`DATABASE.md`.** Not modified — D-6.2 expressly ruled that no wording
  change is required, and D-6.3 confirms no column, table, or index is added.
- **`GAME_STATE.md`, `SIGNALR_PROTOCOL.md`, `GAME_EVENTS.md`,
  `REDIS_STATE.md`.** Not modified — the endpoint is REST-only (D-6.4).
- **Any ADR.** D-6.5 confirms none is required; none was created.
- **TASK-163.** Not modified, re-opened, or re-statused — it is the immutable
  decision record this task consumes.
- **The implementation task.** Not created. The Orchestrator sequences it.
- **§4's existing semantics.** Not changed. `CompletedAt` is added to the
  *history element only*; §4 is silent about the member rather than
  prohibiting it (TASK-163's recorded conflict report).
- **§5.5.** Not rewritten. Its heading scopes it to §5.1/§5.3/§5.4, so the
  `/history` contract carries its own ordering rule without contradicting it.
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2.

---

## The Applied Contract

Every line traces to a TASK-163 decision slot, which is named. No line is
invented and no line reinterprets its decision.

```text
Response shape        bare JSON array, never a wrapper object   D-1.1
Element members       the FULL §4 result shape — `battleId`,     D-1.2
                      `outcome`, `rewards`, `durationTurns`
RewardSummary         complete 8-member structure —              D-1.3
                      `playerXpGained`, `newPlayerXp`,
                      `playerLeveledUp`, `newPlayerLevel`,
                      `petXpGained`, `newPetXp`, `petLeveledUp`,
                      `newPetLevel`; no member omitted
CompletedAt           YES — the history element's own            D-1.4
                      additional member, exposed as `completedAt`;
                      a deliberate, additive extension of the §4
                      shape, which does not expose it
Boss/Pet identifiers  NO — neither `bossDefinitionId`/`bossId`     D-1.5
                      nor `petInstanceId`/`petId` is exposed
Ordering              `CompletedAt` DESC — newest first           D-2.1
Storage index governs YES — DATABASE.md §4's index governs        D-2.2
                      this endpoint
Client reliability    YES — clients MAY rely on the ordering;     D-2.3
                      §5.5 is scoped to §5.1/§5.3/§5.4 and is not
                      extended to this endpoint
Tie-break             `BattleResultId` DESC — deterministic       D-2.5
                      total order (BattleResultId is unique)
Pagination            UNBOUNDED — no page/limit/offset/cursor;    D-3.1,
                      the array is the full history; no cap in     D-3.3
                      MVP
Metadata members      none — no `total`, no `nextCursor`          D-3.2
Authorization         authenticated session required; `401`       D-4.1
                      `UNAUTHENTICATED` for a caller presenting
                      none — never a 404
Ownership             server-derived authenticated `PlayerId`;    D-4.2
                      own `BattleResult` rows only; NEVER
                      client-supplied
Filters               none — no `bossId`, `outcome`, date-range,  D-4.3
                      sort, or search parameter
Empty history         `200` with `[]`                             D-5.1
Active battle         cannot appear — durable rows only           D-5.2
Failed durable write  simply absent — no placeholder, no          D-5.3
                      partial or pending entry
Canonical owner       `docs/02-technical/API_CONTRACTS.md` §4.5   D-6.1
DATABASE.md           no wording change required                   D-6.2
Schema/index          no new column, table, or index              D-6.3
SignalR/Event/Redis   none introduced — REST-only                 D-6.4
ADR                   not required                                 D-6.5
```

---

## Changes

### 1. `docs/02-technical/API_CONTRACTS.md` — **§4.5 (new)**

The dedicated contract section, placed as §4.5 so it sits with the §4 result
endpoint whose shape it extends. It contains:

- the `Response 200` example — a bare array whose single documented element
  carries the five members, with the `rewards` object showing all 8 members;
- the `Response 401` example;
- **13 contract notes** covering: the bare-array rule (no wrapper, no
  `total`/`nextCursor`); that each element is the full §4 shape by
  **reference** to §4 rather than by restatement; `completedAt` as the
  additional member and an additive extension of §4; the ordering rule,
  tie-break, and client reliability; no pagination and full history; no
  filter/sort/search; authentication and the explicit `401`; ownership scoped
  to the server-derived `PlayerId` and never client-supplied; empty history as
  `200 []`; active-battle exclusion; failed-write absence; no Boss/Pet
  identifying member; and the REST-only / no-schema / no-SignalR / no-Redis
  boundary.

**No duplication** (`documentation-change.md` §2). The section **references**
the §4 members, `DATABASE.md` §1's `RewardSummary` and `CompletedAt` sourcing,
`DATABASE.md` §4's index, and `ADR-015`'s session failure contract rather than
restating any of them. The `RewardSummary` member list appears only in the
response example, which is the shape being defined here; its ownership stays
with `DATABASE.md` §1.

### 2. `docs/02-technical/API_CONTRACTS.md` — **§1 Endpoint Summary**

The pre-existing `GET /api/battle/history` entry gained a `(§4.5)` pointer,
and `/api/battle/{battleId}/result` gained `(§4)` — matching how the document
already points at `§2` and `§5` from this summary. The endpoint list itself is
unchanged: no endpoint was added or removed.

### 3. `docs/02-technical/API_CONTRACTS.md` — **version header**

Version `1.16` → `1.17`, following the document's existing convention (a
narrative summary of the current change, then `Prior 1.16: (...)` retaining the
full changelog). The header records what was added **and** what did not
change — no endpoint behaviour, gameplay rule, schema, index, SignalR member,
or Redis contract — and states that `DATABASE.md` is unmodified and no ADR is
required.

---

## Acceptance Criteria

All binary and testable.

```text
[x] API_CONTRACTS.md contains a dedicated GET /api/battle/history contract.
    — §4.5, a new §-level subsection under §4.
[x] Response is documented as a bare JSON array.
    — §4.5 note 1: "a bare JSON array — never a wrapper object", no
    `{ "battles": [...] }` envelope.
[x] Each element contains the full §4 result members.
    — §4.5 note 2, by reference to §4; the example carries all four plus
    `completedAt`.
[x] Each element contains CompletedAt.
    — §4.5 note 3; `"completedAt"` in the example.
[x] RewardSummary contains all 8 documented members.
    — the §4.5 example's `rewards` object carries all eight; note 2 binds it
    to §4 and note 3 to DATABASE.md §1.
[x] No Boss/Pet identifying fields are exposed.
    — §4.5 note 12 names all four excluded identifiers explicitly.
[x] Ordering is explicitly CompletedAt DESC, then BattleResultId DESC.
    — §4.5 note 4.
[x] Ordering is explicitly stated as client-reliable.
    — §4.5 note 4: "Clients MAY rely on this ordering".
[x] MVP pagination is explicitly absent.
    — §4.5 note 5: no page/limit/offset/cursor, no total/nextCursor.
[x] Full history is explicitly returned.
    — §4.5 note 5: the authenticated Player's complete history.
[x] No filter/sort/search parameters are accepted.
    — §4.5 note 6.
[x] Authentication is required.
    — §4.5 note 7.
[x] Unauthenticated callers receive 401 UNAUTHENTICATED.
    — §4.5 note 7 and the Response 401 example.
[x] History is restricted to the authenticated PlayerId.
    — §4.5 note 8.
[x] PlayerId is never client-supplied.
    — §4.5 note 8; no query parameter and no route parameter for ownership.
[x] Empty history returns 200 [].
    — §4.5 note 9.
[x] Active battles are excluded.
    — §4.5 note 10.
[x] Failed durable BattleResult writes produce no history entry.
    — §4.5 note 11; no pending/partial/failed record.
[x] No database schema/index change was made.
    — DATABASE.md unmodified; §4.5 note 13 records no new column/table/index.
[x] No SignalR method/event/member was added.
    — §4.5 note 13; SIGNALR_PROTOCOL.md unmodified.
[x] No Redis history contract was added.
    — §4.5 note 13; REDIS_STATE.md unmodified.
[x] No ADR was created.
    — docs/03-decisions/ADR/ unmodified; header and §4.5 note 13 record it.
[x] TASK-163 was not modified.
    — byte-identical; not written by this task.
[x] No source code was modified.
    — zero files under src/.
[x] No tests were modified.
    — zero files under tests/.
[x] No implementation task was created.
    — no task file created other than this one.
[x] Documentation remains consistent with existing §4 and §5.5 scope.
    — §4's body untouched (pure insertion after it); §5.5's heading and
    its "ordering none defined" line unchanged, and §4.5 note 4 states it is
    scoped apart rather than overriding it.
```

**Explicitly required by this task:**

```text
No speculative gameplay.
No undocumented API behavior.
No undocumented SignalR behavior.
No undocumented Redis behavior.
No speculative database schema.
No client-authoritative state.
No broad repository refactor.
```

---

## Affected Files & Areas

```text
[x] docs/02-technical/API_CONTRACTS.md            (§4.5 new; §1 pointer;
                                                   version header)
[x] tasks/backlog/TASK-164-<this file>.md         — this task file only
[ ] src/backend/ (Domain / Application / Infrastructure / Api)   — NONE
[ ] src/frontend/client/ (scenes / runtime / services / state)   — NONE
[ ] tests/ (unit / integration / gameplay scenarios)             — NONE
[ ] docs/02-technical/DATABASE.md                                — NONE
[ ] docs/02-technical/GAME_STATE.md                              — NONE
[ ] docs/02-technical/SIGNALR_PROTOCOL.md                        — NONE
[ ] docs/02-technical/GAME_EVENTS.md                             — NONE
[ ] docs/02-technical/REDIS_STATE.md                             — NONE
[ ] docs/02-technical/ARCHITECTURE.md                            — NONE
[ ] docs/02-technical/TDD.md                                     — NONE
[ ] docs/ (all other documents)                                  — NONE
[ ] docs/03-decisions/ADR/                                       — NONE
[x] tasks/backlog/TASK-163-*.md                                  — NONE
                                                                   (immutable)
[ ] tasks/completed/* (all)                                      — NONE
                                                                   (immutable)
```

---

## Testing Requirements

This task changes no code and writes no test, so it produces no unit,
integration, or gameplay test. Its verification is the documentation-change
workflow's consistency validation plus a **coverage check** against TASK-163's
24 decision slots and an **isolation check**, at the depth
`core/validation.md` requires for a LOW–MEDIUM-risk DOCUMENTATION task.

### Required Verification

```text
[x] Contract completeness check   — every semantic a later implementation
                                    agent would otherwise have to invent has a
                                    stated answer in §4.5
[x] TASK-163 coverage check       — all 24 decision slots map to a §4.5
                                    statement; none is contradicted, and none
                                    required a new decision
[x] Internal consistency check    — §4.5 does not contradict §1, §4, §5.5,
                                    §6, or §7 of its own document
[x] §4 relationship check         — §4's body is unmodified; §4.5 reuses its
                                    members by reference and adds exactly one
[x] §5.5 scope check              — §5.5 heading and lines unchanged; §4.5
                                    note 4 states the scoping rather than
                                    overriding §5.5
[x] DATABASE.md consistency check — DATABASE.md unmodified and still
                                    consistent: its §1 row and §4 index support
                                    the contract with nothing added
[x] Auth/ownership check          — §4.5 notes 7–8 match §2.8 and ADR-015;
                                    no conflict
[x] No-new-SignalR/Event/Redis    — none added; those documents untouched
[x] No speculative schema         — no column, table, or index proposed
[x] No source changes             — zero files under src/
[x] No test changes               — zero files under tests/
[x] Unit tests                    — N/A (no code)
[x] Integration tests             — N/A (no code)
[x] Gameplay scenarios            — N/A (no behavior changed)
```

### Key Edge Cases

- **`§5.5` superficially looks like it governs ordering.** It does not: its
  line is scoped to §5.1/§5.3/§5.4 by its own heading, which is still present
  and unchanged. §4.5 states the scoping explicitly instead of overriding it,
  which is the smallest correction that removes the apparent conflict.
- **`DATABASE.md` §4's index comment is not a response contract.** It is
  evidence that most-recent-first was intended; the contract itself is written
  at its owner, and the index comment is referenced rather than restated.
- **The tie-break is not in `DATABASE.md`.** D-2.5 decided it, so it is
  documented at the API level (where the contract lives) and `DATABASE.md` is
  left alone — as D-6.2 ruled.
- **`CompletedAt`'s exact serialization is deliberately not fixed here.**
  `DATABASE.md` §1 explicitly leaves the column type to implementation, so
  §4.5 note 3 states the member's source and authority and asserts no timezone
  — inventing a format would be a new decision.
- **A player with an arbitrarily long history** receives the whole array; the
  contract states this is accepted and defers any cap to post-MVP (D-3.3).
- **A player with no history** receives `200 []`, not `404` and not `204`.
- **An authenticated caller asking for another player's history** is not
  expressible: ownership is fixed by the session and there is no parameter to
  select it, so no existence disclosure is possible.

---

## Stop Conditions

Universal stop conditions in `AGENTS.md` §20 and `.ai/README.md` §13 apply.

- If TASK-163's decision record were incomplete: STOP.
- If a decision contradicted an authoritative document: STOP.
- If existing §4 types/formats were ambiguous: STOP.
- If `CompletedAt`'s response type or serialization could not be determined:
  STOP.
- If authentication/session semantics conflicted with an existing ADR: STOP.
- If the existing `BattleResult` persistence could not support the decided
  contract: STOP.
- If a schema change became necessary: STOP.
- If a new SignalR/event contract became necessary: STOP.
- If a Redis contract became necessary: STOP.
- If an ADR became necessary: STOP.
- If the documentation workflow required a decision not present in TASK-163:
  STOP.
- If applying the decision would require changing gameplay rules: STOP.
- If `/history` were discovered to be already documented elsewhere: STOP.
- If task exceeds 7 skills or crosses multiple uncoupled boundaries: STOP &
  decompose.

---

## Stop-Condition Check

**No stop condition fired.** Each is recorded as checked, with its evidence:

| Stop condition | Outcome | Evidence |
|---|---|---|
| TASK-163 record incomplete | **Did not fire** | All 24 slots carry an explicit answer; TASK-163's own §"Decision status" reads `DECIDED`, and no sub-item is blank. |
| A decision contradicts an authoritative document | **Did not fire** | TASK-163's conflict report already established: D-2 vs §5.5 is resolved by scope (not override); D-1.4 is an additive extension of a §4 shape that is silent about `CompletedAt`; D-1.4/D-2.1 agree with `DATABASE.md` §1. Applying them created no new conflict. |
| §4 types/formats ambiguous | **Did not fire** | §4's example, notes 1–5, and `DATABASE.md` §1's `RewardSummary` fix every member's name, type, and source. §4.5 references them instead of restating. |
| `CompletedAt` type/serialization undeterminable | **Did not fire** | `DATABASE.md` §1's sourcing item 2 fixes its source (server clock on the battle-end path), authority, and that no timezone is asserted, while explicitly leaving the column type to implementation. §4.5 states exactly that and invents no format. |
| Auth/session conflicts with an ADR | **Did not fire** | §4.5 notes 7–8 restate §2.8's / `ADR-015`'s already-decided failure and identity rules, and match §4 notes 6–7. |
| Existing persistence cannot support the contract | **Did not fire** | `DATABASE.md` §1 persists every member the contract exposes (`PlayerId`, `CompletedAt`, `DurationTurns`, `Outcome`, `RewardSummary`) and §4's index `BattleResult(PlayerId, CompletedAt DESC)` supports the ordering — the exact index D-2.2 named. |
| Schema change necessary | **Did not fire** | D-6.3 confirms none; the contract is served by the existing row and index. `DATABASE.md` unmodified. |
| New SignalR/event contract necessary | **Did not fire** | D-6.4 confirms none; the endpoint is REST-only. |
| Redis contract necessary | **Did not fire** | D-6.4 confirms none; no history cache exists or is proposed. |
| ADR necessary | **Did not fire** | D-6.5 confirms none; this is a response-contract decision over an already-persisted row (`AGENTS.md` §18). |
| Workflow requires an absent decision | **Did not fire** | The workflow needed the canonical owner (D-6.1), the content (D-1…D-5), and the boundaries (D-6.2…D-6.5) — all recorded. No semantic in §4.5 lacks a decision or an owning section. |
| Gameplay rules would change | **Did not fire** | No rule, magnitude, or value was written; §4.5 only projects persisted data. |
| `/history` already documented elsewhere | **Did not fire** | A docs/-wide search for `battle/history` returned only §1's listing and `DATABASE.md` §4's index comment before this change; no defining contract existed. |
| >7 skills or uncoupled boundaries | **Did not fire** | 5 skills (Normal budget); one document, one endpoint, one boundary (REST read). |

---

## Completion Evidence

### Decision Source

```text
TASK-163 — 24/24 Product Owner decisions.

D-1.1 bare array (no wrapper)              D-2.1 CompletedAt DESC
D-1.2 full §4 result shape                D-2.2 DATABASE.md §4 index governs
D-1.3 full 8-member RewardSummary         D-2.3 clients MAY rely on the order
D-1.4 CompletedAt exposed                 D-2.4 N/A (ordering is defined)
D-1.5 no Boss/Pet identifiers             D-2.5 BattleResultId DESC tie-break
D-3.1 unbounded                            D-4.1 authenticated; 401
D-3.2 N/A (no bounding params)             D-4.2 own PlayerId, server-derived
D-3.3 unbounded array accepted             D-4.3 no filter/sort/search
D-5.1 200 []                               D-6.1 API_CONTRACTS.md canonical
D-5.2 active battle cannot appear          D-6.2 no DATABASE.md change
D-5.3 failed write is absent               D-6.3 no new column/table/index
                                           D-6.4 no SignalR/Event/Redis
                                           D-6.5 no ADR

No slot was reinterpreted, and no decision was added.
```

### Changed Files

- `docs/02-technical/API_CONTRACTS.md` — version header `1.16` → `1.17`;
  §1's summary gains `(§4.5)` and `(§4)` pointers; **§4.5 (new)** is the
  `GET /api/battle/history` response contract (response examples plus 13
  contract notes). No other section was edited: §4's body is a pure
  insertion point, and §5.5, §6, and §7 are unchanged.
- `tasks/backlog/TASK-164-<this file>.md` — this task file.

Isolation check: exactly **three** diff hunks exist in `API_CONTRACTS.md`
(header, §1, §4.5). No other file was written by this task.

### Validation Results

```text
[x] Contract completeness check   PASS — §4.5 answers all 12 semantic groups a
                                  later implementation agent would otherwise
                                  have to invent: response shape, members,
                                  RewardSummary, CompletedAt, ordering,
                                  tie-break, pagination, filters,
                                  authorization, ownership, empty, active
                                  battle, failed persistence.
[x] TASK-163 24/24 coverage check PASS — every slot maps to a §4.5 statement;
                                  none is contradicted; D-2.4 and D-3.2 are
                                  N/A dispositions that §4.5 honours by
                                  defining the ordering and adding no
                                  bounding or metadata member.
[x] Internal consistency check    PASS — §1's pointer resolves to the new
                                  section; §4.5 references §4, §5.5, §6, and
                                  §2.8 consistently; §7's exclusions are
                                  unaffected.
[x] §4 relationship check         PASS — §4's response example, 404/401
                                  examples, and notes 1–7 are byte-identical;
                                  the new section is inserted after them.
[x] §5.5 scope check              PASS — heading still reads
                                  "## 5.5 List semantics (§5.1, §5.3, §5.4)"
                                  and its three lines are unchanged; §4.5
                                  note 4 records that §5.5 does not govern
                                  this endpoint.
[x] DATABASE.md consistency check PASS — unmodified; §1's row and §4's index
                                  still support the contract with nothing
                                  added.
[x] Auth/ownership check          PASS — §4.5 notes 7–8 are consistent with
                                  §2.8, §4 notes 6–7, and ADR-015.
[x] No-new-SignalR/Event/Redis    PASS — none added; those documents are
                                  unmodified.
[x] No speculative schema         PASS — no column, table, or index proposed.
[x] No source changes             PASS — zero files under src/.
[x] No test changes               PASS — zero files under tests/.
[x] Unit tests                    N/A (no code)
[x] Integration tests             N/A (no code)
[x] Gameplay scenarios            N/A (no behavior changed)
```

### Quality Review Checklist (`quality/review.md` §1)

```text
1 Correctness        PASS — every statement traces to a TASK-163 slot or to an
                     existing owning section; nothing rests on a preference or
                     on source code
2 Architecture       PASS — REST read through the existing persistence
                     boundary; no ADR conflict (ADR-001/006/014/015 checked)
3 Scope              PASS — one docs/ file, three edits, one endpoint; no
                     unrelated section edited
4 Tests              N/A — no code; validation is the documentation audit
5 Documentation      PASS — canonical owner used (D-6.1), no duplication
                     (referenced, not restated), dependent docs re-read
6 Security           PASS — authorization and ownership are explicit and match
                     §2.8/ADR-015; no Boss/Pet identifier exposure; no
                     client-supplied identity
7 Performance        N/A — no runtime path touched
8 Maintainability    PASS — one section plus a pointer; no new convention
9 Determinism        PASS — server-derived identity; deterministic total order
                     stated; no client computation
Final recommendation PASS
```

### Server Authority & Scope Verification

- [x] Confirmed zero files modified under `src/`
- [x] Confirmed zero files modified under `tests/`
- [x] Confirmed `docs/02-technical/DATABASE.md` unmodified
- [x] Confirmed `GAME_STATE.md`, `SIGNALR_PROTOCOL.md`, `GAME_EVENTS.md`,
      `REDIS_STATE.md` unmodified
- [x] Confirmed no ADR created or modified
- [x] Confirmed no gameplay rule, magnitude, or value changed
- [x] Confirmed no new SignalR method, event, or member
- [x] Confirmed no new Redis key or contract
- [x] Confirmed no new database column, table, or index
- [x] Confirmed no implementation task created
- [x] Confirmed TASK-163 is byte-identical (not modified)
- [x] Confirmed no completed task reopened or modified
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1); nothing OUT or
      FUTURE introduced
- [x] Confirmed the endpoint is still NOT implemented — this task wrote no code

---

## Remaining Issues

Reported, not fixed (`AGENTS.md` §16). Each is outside this task's boundary.

1. **Implementing the endpoint is the downstream implementation task's work.**
   The contract now exists at its owner; no controller, repository, or query
   was written. `IBattleResultRepository` still exposes only `AddAsync` and
   `GetByIdAsync`, and the two source comments recording that
   `GET /api/battle/history` "has no defining section" are now **stale**.
   They are implementation-side comments in files this task must not modify;
   the implementation task should update them.
2. **`API_CONTRACTS.md`'s version header now reads `1.17`, not `1.18`.** The
   task instruction warned against assuming `1.18` from TASK-163; the actual
   header was `1.16`, and the repository's convention is a single increment
   per documentation change (`DATABASE.md` moved 1.28 → 1.29 under TASK-133).
   `1.17` is therefore the next appropriate version. Noted because TASK-163's
   §"Current State" anticipated `1.18`.
3. **`src/.../BattleResultConfiguration.cs` line 150's stale comment** (the
   `{}` staging `RewardSummary` and TASK-033 as member-list owner) is still
   stale, as TASK-163 already recorded. Unrelated to this task; that file is
   outside its scope.
4. **The endpoint remains unimplemented**, so the contract is currently
   descriptive of intended behaviour rather than of running behaviour — which
   is exactly the state the documentation-first sequence intends at this step
   in the chain (`AGENTS.md` §17).
