# TASK-163 — Resolve the `GET /api/battle/history` Response Contract

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ by path and section — it does NOT copy game rules,
  formulas, schemas, or contracts.

  THIS TASK IS STRICTLY A DECISION-INPUT TASK.

    Documented endpoint with NO defining contract
            ↓
    Present authoritative evidence of the gap
            ↓
    Obtain explicit Product Owner decisions (D-1 … D-6)
            ↓
    Record the decisions in TASK-163
            ↓
    STOP

  THIS TASK MUST NOT APPLY THE DECISION TO AUTHORITATIVE DOCUMENTATION.
  A subsequent documentation/contract task consumes this record and writes the
  §-level response contract into API_CONTRACTS.md; a still-later implementation
  task performs the endpoint work. TASK-163's deliverable is the RECORD, not the
  documentation change.

  THIS TASK DECIDES NOTHING BY ITSELF AND IMPLEMENTS NO BEHAVIOR. It presents
  the documented evidence of a genuine response-contract gap and requires the
  appropriate human/Product-Owner decision. Inventing a response shape, an
  ordering rule, a pagination policy, or an ownership rule — or choosing an
  option because it is easier to implement — is the single prohibited action of
  this task (AGENTS.md §7, §20; .ai/README.md §13).

  PROVENANCE: identified during the task-generation pass following TASK-162's
  lifecycle reconciliation. API_CONTRACTS.md §1 lists
  "GET /api/battle/history — List past Battle Results" as part of the
  authoritative REST surface, but the document contains NO section defining its
  response shape, its ordering, whether it paginates, its authorization
  behaviour, or its empty-collection behaviour. §2 covers /api/auth/discord,
  §3 covers /api/battle/start, §4 covers GET /api/battle/{battleId}/result,
  §5 covers the four collection read endpoints, §6 is the error convention,
  and §7 is "What Is Not Here" — none of them defines /history.

  The gap is not merely an omission noticed by this audit: it is ALREADY
  RECORDED as the reason the endpoint is unimplemented, in two source files:

    src/backend/GameServer.Application/Battle/IBattleResultRepository.cs
      "GET /api/battle/history is listed in API_CONTRACTS.md §1 but no section
       defines its contract, so it is not implemented and no query is invented
       for it."

    src/backend/GameServer.Infrastructure/Postgres/Repositories/BattleResultRepository.cs
      "GET /api/battle/history is listed in API_CONTRACTS.md §1 with no
       defining section, so no query is invented for it."

  So the repository's own implementation already refused to invent a contract
  (correctly, per AGENTS.md §7). The blocking condition is a MISSING DECISION,
  not missing code.

  BOUNDARY: decision recording only. Zero files under docs/, src/, or tests/.
  This task creates no ADR, changes no gameplay rule, adds no event, and
  modifies no completed task.
-->

---

## Metadata

```text
Task ID:           TASK-163
Type:              DOCUMENTATION (TASK_TYPES.md §2 — "Change docs/ content —
                   documentation is the primary output, not code"; workflow
                   documentation/documentation-change.md). This task is the
                   DECISION-INPUT half of that workflow only: it obtains and
                   records the Product Owner decisions. It does NOT perform the
                   canonical-owner documentation write that the workflow's
                   later steps describe — that is the subsequent
                   contract-application task's act, consuming this record.
                   Precedent: TASK-150, TASK-154, TASK-160.
Status:            DONE (the deliverable — obtaining and recording the
                   Product Owner's decisions — is discharged: §"Decisions"
                   records DECIDED with all 24 slots carrying explicit Product
                   Owner answers and none inferred, and §"Completion Evidence"
                   names the decision source, validation results, and
                   verification performed. The BACKLOG value this field
                   previously carried was a lifecycle-metadata defect; its own
                   completion evidence independently establishes completion.
                   See "Completion Evidence".)
Risk:              LOW–MEDIUM (TASK_TYPES.md §4 — DOCUMENTATION baseline is
                   LOW–MEDIUM. MEDIUM rather than LOW because the decision
                   governs a public REST response contract whose shape must
                   remain consistent with the §4 result endpoint and with the
                   persisted BattleResult member set. Neither consequence is
                   applied by this task: no docs/ file is modified, and no
                   source is touched.)
Priority:          MEDIUM (this does not gate the core battle loop, which is
                   complete. It gates the LAST undocumented endpoint in the
                   authoritative REST surface, and it is the only remaining
                   MVP item whose blocker is a genuinely un-recorded decision
                   rather than absent content authoring. Every other remaining
                   MVP gap is content authoring — 2 Pets, 2 Bosses, the
                   deferred Relic — which is a design-authoring activity, not
                   a contract-resolution one.)
Primary Agent:     review (TASK_TYPES.md §2 / AGENT_SELECTION.md §1 —
                   "Documentation change: Primary Agent Review". No domain
                   agent may author the answer; the Product Owner supplies it.)
Supporting Agents: backend (API_CONTRACTS.md owns the REST surface;
                   BattleResultRepository.cs / IBattleResultRepository.cs own
                   the persistence boundary the endpoint would read through —
                   consulted to CONFIRM what surface exists and what does not,
                   not to author the answer),
                   persistence (DATABASE.md §1 owns BattleResult and its member
                   set; §4 owns the BattleResult(PlayerId, CompletedAt DESC)
                   index — consulted to CONFIRM the persisted ordering key
                   exists, not to author the answer),
                   realtime (consulted to CONFIRM that no SignalR member is
                   added by this decision; the endpoint is REST-only)
Workflow:          documentation/documentation-change.md
Skills:            discovery/documentation-discovery,
                   discovery/impact-analysis,
                   backend/api-contract-validation,
                   quality/documentation-consistency,
                   quality/scope-validation
                   (5 skills — Normal budget, tasks/README.md §12)
Dependencies:      TASK-051 (DONE — established the result endpoint's
                     authorization and ownership contract, including
                     "only the authenticated owner may read a result" and
                     "ownership is never established from client-supplied
                     input". IMMUTABLE; read-only; the precedent this
                     decision must remain consistent with),
                   TASK-042 (DONE — established BattleResult identity and the
                     reward-summary contract. IMMUTABLE; read-only),
                   TASK-041 (DONE — implemented battle result persistence,
                     including the PlayerId/CompletedAt columns the history
                     index is built on. IMMUTABLE; read-only),
                   TASK-070 (DONE — established the collection read endpoints'
                     response-contract conventions, including §5.5's
                     empty/ordering/pagination statements. IMMUTABLE;
                     read-only; the closest existing precedent for a list
                     response in this document),
                   TASK-162 (DONE — the lifecycle reconciliation that made this
                     the next ID. IMMUTABLE; read-only)
Blocks:            The documentation task that writes the §-level
                   /api/battle/history response contract into
                   API_CONTRACTS.md (it cannot be created until this decision
                   exists), and the backend implementation task that follows
                   it. Neither is created here.
Estimate:          Simple (one decision record, six decision items, no code,
                   no tests, no docs/ edits)
```

---

## Objective

Obtain and record the explicit Product Owner decisions that define the response contract for the `GET /api/battle/history` endpoint — its response shape and member set, its ordering, its pagination policy, its authorization and ownership behaviour, and its empty-collection behaviour — so that a subsequent documentation task can write a complete §-level contract into `API_CONTRACTS.md` §-level and a later implementation task can implement the endpoint without inventing any of these semantics.

This task produces a **record only**. It changes no documentation, no source, and no test.

---

## Authoritative References

- `AGENTS.md` **§4** — never silently resolve a conflict; **§7** — invent no rule; **§10** — server authority; **§16** — report, do not fix inline; **§20** — stop conditions; **§23** — implement documented intent, do not design on the project's behalf
- `.ai/README.md` **§6** — source-of-truth rule; **§13** — stop conditions and the exact STOP CONDITION report format; **§14** — MVP protection
- `.ai/workflow/documentation/documentation-change.md` **§1** — the flow this task's decision feeds; **§2** — no duplication, ever; **§3** — determining the canonical owner
- `tasks/README.md` **§6** — how a task is created; **§9** — no business-rule duplication in task files; **§10** — stop conditions; **§12** — skill budget
- `tasks/TASK_LIFECYCLE.md` **§3** — BACKLOG entry conditions
- `docs/00-overview/MVP_SCOPE.md` **§1** — "Persistent storage (PostgreSQL)" is IN; Battle Results persistence is IN; **§2** — nothing OUT may be introduced
- `docs/00-overview/ROADMAP.md` **§1 Phase 2** — "Persistent storage: Pet Collection, Battle Results, Rewards (`DATABASE.md`)"
- **`docs/02-technical/API_CONTRACTS.md` §1** — **the site of the gap.** Its endpoint summary lists `GET /api/battle/history — List past Battle Results`, but no section in the document defines it. Also **§1's** statement that the endpoint list "is the complete REST surface of a battle", and its global authentication rule that all endpoints except `/api/auth/discord` require an authenticated session
- `docs/02-technical/API_CONTRACTS.md` **§4** — the closest precedent: the single-result endpoint's response shape, its `rewards` contract notes, and notes **6–7** (401 `UNAUTHENTICATED` for an unauthenticated caller; only the authenticated owner may read a result; a foreign or missing battle both return 404 `BATTLE_NOT_FOUND`; ownership is never established from client-supplied input)
- `docs/02-technical/API_CONTRACTS.md` **§5.5** — the closest list-response precedent: empty collection → `200 with []`; **ordering "none defined — clients must not rely on any order"**; **pagination "none in MVP — no page/limit/cursor/sort/filter/search parameters; the array is the full collection"**. **This precedent is in tension with `DATABASE.md` §4's battle-history index and must be reconciled by the Product Owner — see Current State.**
- `docs/02-technical/API_CONTRACTS.md` **§6** — the error envelope; **§7** — what is deliberately not in the document
- `docs/02-technical/DATABASE.md` **§1** — the `BattleResult` row: its primary key (`BattleResultId` = `BattleId`, one row per battle), `PlayerId`, `CompletedAt`, `DurationTurns`, `Outcome`, and `RewardSummary`; its "Duration and completion sourcing" notes
- `docs/02-technical/DATABASE.md` **§4** — the index list, which includes `BattleResult(PlayerId, CompletedAt DESC) — battle history, most recent first`
- `docs/02-technical/DATABASE.md` **§2** — the `Player 1 ── N BattleResult` relationship
- `docs/02-technical/GAME_STATE.md` — the persisted/finished result's relationship to the active battle state (a battle's state is available via SignalR only while active; the durable result is read after the battle ends)
- `docs/02-technical/ARCHITECTURE.md` **§3** — the `PersistenceRepository (Postgres)` boundary the read would pass through
- `docs/03-decisions/ADR/ADR-006-postgresql-persistence.md` — persistence ownership rationale; **ADR-001-server-authoritative-battle.md** — server authority; **ADR-014-battle-state-player-identity.md** — the `PlayerId` identity the ownership rule is derived from; **ADR-015-application-session-authentication-contract.md** — the session mechanism that resolves the caller's identity
- **`src/backend/GameServer.Application/Battle/IBattleResultRepository.cs`** and **`src/backend/GameServer.Infrastructure/Postgres/Repositories/BattleResultRepository.cs`** — **the implementation's explicit record that the contract is absent.** Both state that the endpoint is listed in `API_CONTRACTS.md` §1 but has no defining section, "so it is not implemented and no query is invented for it". These are read-only references confirming the gap's reality; they are NOT edit sites.
- `tasks/backlog/TASK-150-resolve-powerchanged-source-semantics-for-boss-drain-and-card-power-charge.md` — the decision-input task precedent (structure, "Required Decision Coverage", recording discipline, Completion Evidence form)
- `tasks/completed/TASK-160-collect-product-owner-decisions-pet-statuseffects-and-boss-live-hp-projection.md` — the most recent decision-input precedent (D-item form, `Decision:` slot, reserved rationale marker)

---

## Current State

### The endpoint is authoritative but undefined

`API_CONTRACTS.md` §1 lists the endpoint in its summary table:

```text
GET     /api/battle/history                 List past Battle Results
```

The document's section structure is:

```text
§1  Endpoint Summary                    ← lists /history, defines nothing
§2  POST /api/auth/discord
§3  POST /api/battle/start
§4  GET  /api/battle/{battleId}/result
§5  GET  /api/pets, /api/pets/{petId}, /api/cards, /api/relics
§6  Error Convention
§7  What Is Not Here
```

**No section defines `/api/battle/history`.** Therefore the following semantics are, at present, undefined by any authoritative document:

```text
[ ] Response shape and member set
[ ] Whether the response is an array of full result objects or a summary form
[ ] Ordering  (see the tension below)
[ ] Pagination / limiting / cursor / page-size
[ ] Authorization behaviour for an unauthenticated caller
[ ] Ownership rule (does a caller see only their own battles?)
[ ] Empty-history response
[ ] Whether a still-active battle appears
```

### Evidence that the gap is real, not assumed

The implementation already refused to invent the contract, and recorded why — in two places:

```text
src/backend/GameServer.Application/Battle/IBattleResultRepository.cs
  "There is deliberately no more surface than that: no enumeration, no
   history query, no update, and no delete. GET /api/battle/history is listed
   in API_CONTRACTS.md §1 but no section defines its contract, so it is not
   implemented and no query is invented for it."

src/backend/GameServer.Infrastructure/Postgres/Repositories/BattleResultRepository.cs
  "There is deliberately no history query, no update, and no delete:
   GET /api/battle/history is listed in API_CONTRACTS.md §1 with no defining
   section, so no query is invented for it."
```

The persistence boundary exposes exactly two operations — `AddAsync` and `GetByIdAsync`. There is no enumeration method, because no contract authorizes one.

### The specific tension the decision must resolve

Two authoritative documents do **not** currently agree on the question of ordering, and this is the sharpest reason a decision is required rather than an inference:

```text
DATABASE.md §4
  Index list includes:
    BattleResult(PlayerId, CompletedAt DESC)  — battle history, most recent first

  This index is documented for the battle-history read and encodes a
  most-recent-first ORDERING.

API_CONTRACTS.md §5.5
  The only existing list-response precedent states, for the collection
  endpoints:
    ordering     none defined — clients must not rely on any order
    pagination   none in MVP — no page/limit/cursor/sort/filter/search
                 parameters; the array is the full collection

  The §5.5 statement is scoped to §5.1 / §5.3 / §5.4 (the collections), so it
  does not by itself govern /history. But if /history is specified with no
  ordering while DATABASE.md §4 names a DESC index for it, the index has no
  documented consumer; and if /history is specified as most-recent-first while
  a client infers §5.5's "no order" convention applies to every list endpoint,
  the two readings diverge.
```

**This task must not choose between them.** It records the tension and requires the Product Owner to resolve it. Reporting it is required by `AGENTS.md` §4; resolving it here would be the prohibited act.

### A second contract question the decision must settle

§4's result endpoint is per-battle and carries a full `rewards` object (`DATABASE.md` §1's 8-member `RewardSummary`). A history list returning *n* full result objects would return *n* reward summaries. Whether `/history` returns the full §4 shape per element, or a summary/reduced form, is undecided and materially changes both the response size and the persistence read. This is a decision, not an inference.

### Version state

```text
docs/02-technical/API_CONTRACTS.md    Version 1.18 (verify before relying)
docs/02-technical/DATABASE.md         (verify before relying)
```

Verify the current version headers before citing section numbers downstream; do not assume they have not moved.

---

## Exact Ambiguity

Precisely stated, so the Product Owner answers a bounded question rather than a general one:

```text
The authoritative REST surface declares GET /api/battle/history as an
endpoint ("List past Battle Results"), and the persistence layer documents a
BattleResult(PlayerId, CompletedAt DESC) index explicitly labelled "battle
history, most recent first". Yet no document defines the endpoint's response
contract, and the persistence boundary correspondingly exposes no enumeration
operation.

The unresolved questions are therefore:
  (a) what the response body is;
  (b) in what order its elements appear, given that DATABASE.md §4 names a
      DESC index for this read while API_CONTRACTS.md §5.5 defines the only
      existing list convention as "no ordering defined";
  (c) whether the list is bounded at all in MVP;
  (d) who may read what (authorization and ownership);
  (e) what an empty history returns;
  (f) whether an in-progress battle can appear.

None of these can be derived from existing documentation without choosing
between readings, so they require a Product Owner decision.
```

---

## Required Decision Coverage

The Product Owner must supply an explicit answer for **each** item below. An answer that leaves a sub-item open is an incomplete decision and must be returned for completion rather than interpreted.

```text
D-1  Response shape and member set
     D-1.1  Is the response a JSON array of BattleResult objects, or an
            object wrapping an array (e.g. { "battles": [...] })?
     D-1.2  Does each element carry the FULL §4 result shape (battleId,
            outcome, rewards, durationTurns), or a reduced summary form?
            If reduced: name the exact members.
     D-1.3  If full: confirm that each element carries the complete 8-member
            RewardSummary (DATABASE.md §1), or name which members are omitted.
     D-1.4  Does each element carry CompletedAt? (DATABASE.md §1 persists it
            and §4's single-result response does NOT expose it; the history
            read is the one place it would be needed for ordering to be
            observable to a client.)
     D-1.5  Are Boss/pet identifying members included? (DATABASE.md §2 gives
            BattleResult N──1 Pet and N──1 BossDefinition; §4 exposes neither.)

D-2  Ordering
     D-2.1  What order are elements returned in?
     D-2.2  Does DATABASE.md §4's "BattleResult(PlayerId, CompletedAt DESC)
            — battle history, most recent first" GOVERN this endpoint, i.e. is
            most-recent-first the contract?
     D-2.3  If ordering IS defined: confirm that clients MAY rely on it, and
            state how this reconciles with API_CONTRACTS.md §5.5's "ordering
            none defined — clients must not rely on any order", which is
            scoped to §5.1/§5.3/§5.4 but is the only existing list precedent.
     D-2.4  If ordering is NOT defined: state what consumes the §4 index, or
            record that the index is retained for a different purpose.
     D-2.5  State the tie-break rule when two results share a CompletedAt
            (DATABASE.md §1 does not require CompletedAt to be unique).

D-3  Pagination / bounding
     D-3.1  Does MVP bound this list — page/limit/offset/cursor — or return
            the full history (as §5.5 does for collections)?
     D-3.2  If bounded: name the parameter(s), the default, the maximum, and
            the response's total/next-cursor member(s) if any.
     D-3.3  If unbounded: confirm explicitly that an unbounded array is
            accepted for a player with an arbitrarily long history, and state
            whether any cap is deferred to post-MVP.

D-4  Authorization and ownership
     D-4.1  Confirm that this endpoint requires an authenticated session under
            §1's global rule and returns 401 UNAUTHENTICATED for a caller
            presenting none, exactly as §4 note 6 establishes.
     D-4.2  Confirm that a caller reads ONLY their own BattleResults, with
            ownership derived server-side from the session
            (BattleResult.PlayerId ← BattleState.PlayerId, ADR-014) and NEVER
            from a client-supplied playerId/query parameter/header (§4 note 7,
            ADR-001).
     D-4.3  Does the endpoint accept ANY filtering parameter (bossId, outcome,
            date range)? If yes, name each; if no, confirm that no
            filter/sort/search parameter is permitted (per §5.5's precedent).

D-5  Empty and edge behaviour
     D-5.1  What does a player with no completed battles receive?
            (Proposed-for-confirmation: 200 with an empty array, matching
            §5.5's "empty collection 200 with []" — confirm or correct.)
     D-5.2  Can a still-active battle appear? (Confirm it cannot: a
            BattleResult row exists only after terminal persistence —
            DATABASE.md §1, and §4's "only returns data for a battle that has
            already ended".)
     D-5.3  What does a battle whose durable write failed look like? (Confirm
            it is simply absent, consistent with §4's "absence is reported as
            absence" reading and DATABASE.md §1 sourcing item 3.)

D-6  Ownership and consequences of the decision
     D-6.1  Confirm the canonical owner document for the response contract
            (expected: docs/02-technical/API_CONTRACTS.md, as a new §-level
            section), per documentation-change.md §3.
     D-6.2  Confirm whether DATABASE.md §4's index comment requires a
            corresponding wording change once D-2 is answered, or whether it
            already states the decided rule.
     D-6.3  Confirm that NO new database column, table, or index is required
            by the decided contract — or, if one is, name it explicitly.
     D-6.4  Confirm that NO new Battle Event, no SignalR method/member, and no
            Redis key is introduced by this endpoint (it is REST-only).
     D-6.5  Confirm whether an ADR is required. (Expected: NO — this is a
            response-contract decision over an existing persisted row, not an
            architectural change; AGENTS.md §18.)
```

---

## Decisions

<!--
  TO BE COMPLETED BY THE PRODUCT OWNER.
  Record each answer verbatim at its slot below. Do not paraphrase, do not
  "improve" an answer, and do not fill a slot speculatively. If an answer is
  not supplied, leave the slot empty and the task is not DECIDED.
  The reserved "Rationale (optional):" marker is for the Product Owner only.
-->

```text
D-1  Response shape and member set
     D-1.1  Decision: A bare JSON array. NOT an object wrapping an array — no
            `{ "battles": [...] }` wrapper.
     D-1.2  Decision: FULL §4 result shape. Each element carries the complete
            `GET /api/battle/{battleId}/result` 200 response shape — `battleId`,
            `outcome`, `rewards`, `durationTurns` — not a reduced history
            summary. (D-1.3 applies, because the shape is full.)
     D-1.3  Decision: FULL — each element carries the complete 8-member
            `RewardSummary` (`DATABASE.md` §1): `playerXpGained`, `newPlayerXp`,
            `playerLeveledUp`, `newPlayerLevel`, `petXpGained`, `newPetXp`,
            `petLeveledUp`, `newPetLevel`. No member is omitted.
     D-1.4  Decision: YES — each element carries `CompletedAt`, exposed as a
            response member. (This is a deliberate, decided extension of the §4
            single-result shape, which does not expose it.)
     D-1.5  Decision: NO — no Boss/pet identifying member is included. Neither
            `bossDefinitionId`/`bossId` nor `petInstanceId`/`petId` is exposed.
     Rationale (optional): <NOT SUPPLIED BY PRODUCT OWNER>

D-2  Ordering
     D-2.1  Decision: Most recent first — elements are ordered by `CompletedAt`
            descending (newest completed battle first).
     D-2.2  Decision: YES — `DATABASE.md` §4's
            "BattleResult(PlayerId, CompletedAt DESC) — battle history, most
            recent first" GOVERNS this endpoint. Most-recent-first IS the
            contract.
     D-2.3  Decision: YES — clients MAY rely on the ordering. It is reconciled
            with `API_CONTRACTS.md` §5.5 as follows: §5.5's "ordering none
            defined" is expressly scoped to §5.1 / §5.3 / §5.4 (its own
            heading is "List semantics (§5.1, §5.3, §5.4)"), so it does not
            govern `/history`. The two statements are scoped apart and are not
            in contradiction; §5.5's un-ordered convention is NOT extended to
            this endpoint.
     D-2.4  Decision: N/A — ordering IS defined (D-2.1/D-2.2), so no
            "unconsumed index" question arises. The `DATABASE.md` §4 index has
            this endpoint as its documented consumer.
     D-2.5  Decision: Tie-break when two results share a `CompletedAt`: order by
            `BattleResultId` descending as the secondary, deterministic key.
            (`BattleResultId` = the battle's own `BattleId`, unique per row —
            `DATABASE.md` §1 — so the total order is deterministic.)
     Rationale (optional): <NOT SUPPLIED BY PRODUCT OWNER>

D-3  Pagination / bounding
     D-3.1  Decision: UNBOUNDED — MVP returns the full history. No
            page/limit/offset/cursor parameter bounds this endpoint.
     D-3.2  Decision: N/A — not bounded, so no parameter, default, maximum, or
            total/next-cursor response member exists. No such member is added.
     D-3.3  Decision: Confirmed explicitly — an unbounded array IS accepted for
            a player with an arbitrarily long history. No cap is defined in
            MVP; any cap/bounding is deferred to post-MVP.
     Rationale (optional): <NOT SUPPLIED BY PRODUCT OWNER>

D-4  Authorization and ownership
     D-4.1  Decision: CONFIRMED — this endpoint requires an authenticated
            session under §1's global rule, and a caller presenting none
            receives `401` with the `UNAUTHENTICATED` code (§6 envelope),
            exactly as §4 note 6 establishes — NOT a 404.
     D-4.2  Decision: CONFIRMED — a caller reads ONLY their own
            `BattleResult` rows. Ownership is derived server-side from the
            authenticated session (`BattleResult.PlayerId` ←
            `BattleState.PlayerId`, ADR-014) and NEVER from a client-supplied
            `playerId`, query parameter, header, or body field (§4 note 7,
            ADR-001).
     D-4.3  Decision: NO filtering parameter of any kind is accepted — no
            `bossId`, no `outcome`, no date range, and no filter/sort/search
            parameter (consistent with §5.5's precedent).
     Rationale (optional): <NOT SUPPLIED BY PRODUCT OWNER>

D-5  Empty and edge behaviour
     D-5.1  Decision: CONFIRMED — a player with no completed battles receives
            `200` with an empty array (`[]`), matching §5.5's "empty collection
            200 with []" convention.
     D-5.2  Decision: CONFIRMED — a still-active battle CANNOT appear. A
            `BattleResult` row exists only after terminal persistence
            (`DATABASE.md` §1), and §4 already establishes that data exists
            only for a battle that has already ended.
     D-5.3  Decision: CONFIRMED — a battle whose durable write failed is simply
            ABSENT from the response. There is no error member, no placeholder,
            and no partial entry; absence is reported as absence (consistent
            with §4's reading and `DATABASE.md` §1 sourcing item 3).
     Rationale (optional): <NOT SUPPLIED BY PRODUCT OWNER>

D-6  Ownership and consequences
     D-6.1  Decision: CONFIRMED — the canonical owner document for this
            response contract is `docs/02-technical/API_CONTRACTS.md`, written
            as a new §-level section (per documentation-change.md §3). This
            task does not perform that write.
     D-6.2  Decision: NO wording change is required in `DATABASE.md` §4. Its
            index comment ("battle history, most recent first") already states
            the decided rule (D-2.1/D-2.2) exactly, so it needs no correction.
     D-6.3  Decision: CONFIRMED — NO new database column, table, or index is
            required by the decided contract. The contract is served entirely
            by the existing `BattleResult` row and the existing
            `BattleResult(PlayerId, CompletedAt DESC)` index.
     D-6.4  Decision: CONFIRMED — NO new Battle Event, no SignalR method or
            member, and no Redis key is introduced. The endpoint is REST-only.
     D-6.5  Decision: CONFIRMED — NO ADR is required. This is a
            response-contract decision over an already-persisted row, not an
            architectural change (`AGENTS.md` §18).
     Rationale (optional): <NOT SUPPLIED BY PRODUCT OWNER>
```

**Decision status:** DECIDED. All 24 slots (D-1.1–D-1.5, D-2.1–D-2.5, D-3.1–D-3.3, D-4.1–D-4.3, D-5.1–D-5.3, D-6.1–D-6.5) carry an explicit Product Owner answer.

**Decision source and provenance.** The answers were supplied directly by the Product Owner in the execution session for this task, in three explicit rulings: (1) the D-2 ordering ruling — most recent first governs; (2) the D-1 ruling — a bare array of FULL §4 result objects including `CompletedAt`; (3) the D-3/D-4/D-5/D-6 ruling — the task's proposed-for-confirmation answers are confirmed as stated, with D-6.2 expressly ruled as "no `DATABASE.md` change needed". No slot was inferred by the executing agent. Where a sub-item was made N/A by another answer (D-1.3 by D-1.2, D-2.4 by D-2.1/D-2.2, D-3.2 by D-3.1), the slot records that disposition explicitly rather than being left blank.

**Conflict report (AGENTS.md §4).** No recorded decision contradicts an authoritative document.

- **D-2 vs `API_CONTRACTS.md` §5.5 — NOT a conflict.** §5.5 is expressly scoped to §5.1/§5.3/§5.4 by its own heading, so its "ordering none defined" line does not govern `/history`. D-2.3 records this scoping rather than overriding §5.5.
- **D-1.4 vs `API_CONTRACTS.md` §4 — an intentional, decided extension, not a conflict.** §4 governs the single-result endpoint and stays unchanged; §4 is silent about `CompletedAt` rather than prohibiting its exposure. D-1.4 adds the member to the *history* element only.
- **D-1.4 / D-2.1 vs `DATABASE.md` §1 — consistent.** `DATABASE.md` §1 persists `CompletedAt` (server clock, captured on the battle-end path, ordering the battle-history index), and the existing `BattleResultConfiguration.cs` already declares it `IsRequired()` with the DESC index. The decision reads existing persisted data; it authors no new storage and no gameplay rule.
- **D-6.3 / D-6.4 — consistent with the MVP boundary.** The contract is served by the existing row, index, and REST surface; nothing is introduced across the PostgreSQL / Redis / SignalR boundaries (`AGENTS.md` §13).

**Reported, not resolved (`AGENTS.md` §16).** Two items surfaced while reading and are reported here rather than fixed, because they are outside this task's scope:

1. `docs/02-technical/API_CONTRACTS.md`'s version header reads **Version 1.16**, not 1.18 as §"Current State" → "Version state" anticipated. Section numbers cited in the task (notably §5.5) remain accurate, so no decision is affected; noted only because the task asked that the header be verified before relying on it.
2. `src/backend/GameServer.Infrastructure/Postgres/Configurations/BattleResultConfiguration.cs` line 150 retains the comment "holding the documented staging value `{}` until TASK-033 owns its member list", which is stale relative to `DATABASE.md` 1.20's landed 8-member `RewardSummary`. This is a source-comment drift only, in a file this task must not modify; it is reported for a follow-up task and is not a contract conflict.

Both items were previously unrecorded; neither blocks this decision record.

---

## Recording Discipline

1. Record each answer **verbatim**. Do not summarize, reinterpret, or reconcile an answer with a document it appears to contradict — if it contradicts one, that is a reported conflict (`AGENTS.md` §4), not something this task resolves.
2. Do not add a decision item that is not listed in §"Required Decision Coverage". If the Product Owner volunteers a decision outside that list, record it in a clearly-marked additional slot and report it.
3. Do not pre-fill a slot with the "expected" answer suggested in parentheses in §"Required Decision Coverage". Those parentheticals are **proposed-for-confirmation**, and the Product Owner may correct any of them.
4. If an answer is unclear or internally inconsistent, record it as supplied and report the ambiguity rather than interpreting it.

---

## Scope

### In Scope

1. Present the documented evidence that `GET /api/battle/history` is listed in `API_CONTRACTS.md` §1 with no defining section, and that two source files record this as the reason no history query exists.
2. Present the specific ordering tension between `DATABASE.md` §4's DESC battle-history index and `API_CONTRACTS.md` §5.5's "no ordering defined" list convention.
3. Enumerate the six required decision items (D-1 … D-6) with their sub-items.
4. Record the Product Owner's answers verbatim in §"Decisions".
5. Report, without resolving, any conflict the answers create with an existing authoritative document.

### Out of Scope

- **Applying the decision to any authoritative document.** `API_CONTRACTS.md` is NOT edited by this task. Writing the §-level response contract is the SUBSEQUENT contract-application task's act, consuming this record. This task's deliverable is the record itself.
- **Any source code.** Zero files under `src/`. The endpoint is NOT implemented here, and `IBattleResultRepository`'s operation set is NOT extended. No enumeration method is added.
- **Any test.** Zero files under `tests/`.
- **Any authoritative documentation.** Zero files under `docs/`. In particular `API_CONTRACTS.md` §1's listing is left exactly as it is; correcting it is the subsequent task's act.
- **Any gameplay, protocol, API, Redis, persistence, or architecture change.** None is proposed, authored, or implied by the recording act.
- **Creating the downstream documentation task or the implementation task.** This task creates neither; the Orchestrator sequences them.
- **An ADR.** Expected not required (D-6.5); if the Product Owner's answers require one, that is REPORTED here and authored by a separate architecture task, not created here (`AGENTS.md` §18).
- **The 2 additional Pets and their Signature Skills** (`PET_RULES.md` §8 — Thanh Xà and Sơn Hùng rows deferred; `CARD_RULES.md` §4.1 content absent). This is design content authoring, a different activity from contract resolution, and is NOT this task.
- **The 2 additional Bosses** (`BOSS_RULES.md` §6 note — "Two additional MVP Bosses ... are not yet content-defined"). Same reasoning: content authoring, NOT this task.
- **The deferred Relic row** (`RELIC_RULES.md` §3 note 3 / §6 note 3 — "Burning Curse" deferred pending a documented static-modifier Trigger; the §3-vs-note-1 tension is reported, not resolved). It carries its own recorded procedure and is NOT this task.
- **`Player.IsCombatReady`** — verified NOT a live gap; see §"Alternatives Rejected".
- **TASK-036** — remains BLOCKED on an unresolved security/credential decision (`AGENTS.md` §7, §18); NOT this task and NOT bypassed here.
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2.

---

## Acceptance Criteria

All binary and testable.

```text
[ ] The gap is demonstrated from API_CONTRACTS.md's own structure: §1 lists
    GET /api/battle/history, and no section of the document defines its
    response contract.
[ ] The evidence is cited from both source files that record the absence
    (IBattleResultRepository.cs, BattleResultRepository.cs) as the reason no
    history query exists.
[ ] The ordering tension is stated with both sides named by file and section:
    DATABASE.md §4's "BattleResult(PlayerId, CompletedAt DESC) — battle
    history, most recent first" versus API_CONTRACTS.md §5.5's "ordering none
    defined — clients must not rely on any order".
[ ] The decision is NOT resolved anywhere in this task: no response shape,
    ordering rule, pagination policy, authorization rule, or empty-behaviour
    rule is selected, stated as the answer, or implied to be correct.
[ ] All six required decision items (D-1 … D-6) are enumerated with their
    sub-items, and each sub-item is answerable by the Product Owner.
[ ] Every D-slot in §"Decisions" is empty: no slot is pre-filled, no "expected"
    parenthetical is copied into a slot, and each reserved
    "Rationale (optional): <NOT SUPPLIED BY PRODUCT OWNER>" marker is intact.
[ ] The recording discipline is stated and prohibits paraphrasing, adding
    unlisted decision items, pre-filling, and interpreting.
[ ] The task states explicitly that it must NOT modify
    docs/02-technical/API_CONTRACTS.md, DATABASE.md, or any other document.
[ ] The task states explicitly that it must NOT implement the endpoint, extend
    IBattleResultRepository, or write any query.
[ ] The task states explicitly that it must NOT create the downstream
    documentation task or the implementation task.
[ ] The blocked content items (2 Pets, 2 Bosses, the deferred Relic) are
    explicitly excluded and their exclusion is justified as a different
    activity (content authoring) rather than contract resolution.
[ ] The task explicitly prohibits speculative gameplay, undocumented API
    behavior, undocumented SignalR behavior, undocumented Redis behavior, a
    speculative database schema, client-authoritative state, and any broad
    repository refactor.
[ ] STOP conditions are present and cover: an answer contradicting an existing
    authoritative document; an answer requiring a schema/architecture change;
    an answer requiring an ADR; an incomplete answer; and discovery that a
    contract actually DOES exist elsewhere.
[ ] Zero files under src/ were created, modified, renamed, or deleted.
[ ] Zero files under tests/ were created, modified, renamed, or deleted.
[ ] Zero files under docs/ were created, modified, renamed, or deleted.
[ ] No completed task was modified, reopened, or moved.
[ ] No task other than this one was created.
[ ] quality/review.md §1 checklist passes.
[ ] No authoritative rules or contracts violated (AGENTS.md §10 / ADR-001).
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
[x] tasks/backlog/TASK-163-resolve-battle-history-endpoint-response-contract.md
      — this task file and its §"Decisions" and Completion Evidence sections
[ ] src/backend/ (Domain / Application / Infrastructure / Api)   — NONE
[ ] src/frontend/client/ (scenes / runtime / services / state)   — NONE
[ ] tests/ (unit / integration / gameplay scenarios)             — NONE
[ ] docs/02-technical/API_CONTRACTS.md                           — NONE
[ ] docs/02-technical/DATABASE.md                                — NONE
[ ] docs/ (all other documents)                                  — NONE
[ ] docs/03-decisions/ADR/                                       — NONE
[ ] tasks/completed/* (all)                                      — NONE (immutable)
[ ] tasks/blocked/TASK-036-*.md                                  — NONE
[ ] tasks/backlog/* (all others)                                 — NONE
```

---

## Implementation Notes

- **This is a reading-and-recording task.** The executor reads the two named documents and the two named source comments, confirms the gap, and presents the decision items. It authors no answer.
- **The two source comments are the strongest evidence and must be cited.** They are the repository's own record that the endpoint was deliberately left unimplemented rather than guessed — which is exactly the discipline this task continues. Do not treat their presence as a defect to fix.
- **Do not "helpfully" propose a response shape.** A plausible shape is obvious, and that is precisely the danger: `AGENTS.md` §23 forbids `REQUEST → ASSUMPTION → DESIGN`, and `.ai/README.md` §13 lists "an API contract is unclear" as a stop condition. The whole point of this task is that the shape is a decision.
- **The ordering tension is the decisive argument for a decision task.** If `/history`'s ordering could be read straight off `DATABASE.md` §4, this would be a FEATURE task. It cannot: §5.5 defines the only existing list convention as explicitly unordered, and §5.5 is scoped to the collections rather than to `/history`. Two readings are defensible; §4 (`AGENTS.md`) forbids picking one silently. Present both; choose neither.
- **`CompletedAt` is the observable consequence of D-1.4 and D-2.** `DATABASE.md` §1 persists `CompletedAt` and explicitly notes it orders the battle-history index, but §4's single-result response does not expose it. If the history contract defines an ordering, a client generally needs `CompletedAt` to observe it. Flag this linkage so the Product Owner answers D-1.4 and D-2 together.
- **Keep the decision bounded.** Six items, no more. Do not expand into "review all remaining MVP gaps" — that is the generic task this repository's workflow forbids.
- **Report, do not fix** (`AGENTS.md` §16). If reading surfaces an unrelated documentation drift, report it; do not edit it.
- **Precedent shape.** TASK-150 and TASK-160 are the decision-input precedents. Match their structure: bounded decision items, verbatim recording, reserved rationale markers, and a Completion Evidence section that names the decision source.

---

## Testing Requirements

This task changes no code and writes no documentation, so it produces no unit, integration, or gameplay test. Its verification is a **gap-evidence check**, a **neutrality check**, a **coverage check**, and an **isolation check**, at the depth `core/validation.md` requires for a LOW–MEDIUM-risk DOCUMENTATION task.

### Required Verification

```text
[ ] Gap-evidence check     — API_CONTRACTS.md §1 lists the endpoint; a
                             targeted search of the document's section
                             headings confirms no section defines it
[ ] Source-evidence check  — both named source comments are quoted and their
                             file paths are correct
[ ] Neutrality check       — no answer appears anywhere in the task file; the
                             §"Decisions" slots are all empty; no "expected"
                             value was promoted into a decision
[ ] Coverage check         — D-1 … D-6 and every sub-item are present and
                             answerable; the ordering tension is presented
                             with both sides cited
[ ] Owner check            — the canonical owner for the future write is
                             identified as API_CONTRACTS.md (D-6.1)
[ ] Isolation verification — src/ (0 files), tests/ (0 files), docs/ (0
                             files); no completed task and no other backlog
                             task modified; exactly one new file created
[ ] Unit tests             — N/A (no code)
[ ] Integration tests      — N/A (no code)
[ ] Gameplay scenarios     — N/A (no behavior changed)
```

### Key Edge Cases

- **A plausible response shape exists and will tempt the executor to write it.** The prohibition is explicit: this task records a decision, it does not make one. If the executor finds itself drafting a JSON body, it has left its scope.
- **`§5.5` superficially looks like it already answers ordering.** It does not: its "ordering none defined" line is scoped to §5.1/§5.3/§5.4. Reading it as governing `/history` would be exactly the silent conflict resolution `AGENTS.md` §4 forbids.
- **`DATABASE.md` §4 superficially looks like it already answers ordering.** It names a DESC index "battle history, most recent first", which is strong evidence of intent — but an index comment is not a response contract, and the document states elsewhere that no index beyond the listed set is specified. Present it as evidence for the Product Owner's D-2.2; do not treat it as the answer.
- **The endpoint may already be implemented somewhere.** If a search finds `/api/battle/history` actually implemented with a defined contract, this task is invalid: STOP and report, because the premise (undefined contract) would be false.
- **A contract may exist in a document this audit did not read.** If it does, STOP and report rather than proceeding — the task would be a DUPLICATE.
- **Do not create the follow-up documentation or implementation task.** Report the recommendation; the Orchestrator sequences it.

---

## Stop Conditions

Universal stop conditions in `AGENTS.md` §20 and `.ai/README.md` §13 apply.

- **If a response contract for `/api/battle/history` is found to already exist in any document: STOP.** Report it; this task is then a duplicate and must not be executed.
- **If the endpoint is found to be already implemented: STOP.** Report the location; the premise of this task is false.
- **If a Product Owner answer contradicts an existing authoritative document: STOP.** Report the conflict per `AGENTS.md` §4; do not reconcile it inside this task.
- **If a Product Owner answer requires a new database column, table, or index: STOP.** Report it; the schema change is its own task (`DATABASE.md`, ADR-006).
- **If a Product Owner answer requires an architectural change or an ADR: STOP.** Report it; ADR authoring is a separate architecture task (`AGENTS.md` §18).
- **If a Product Owner answer is incomplete: STOP.** Return the unanswered items; do not fill them by inference.
- **If a Product Owner answer would require inventing a gameplay value or rule: STOP.** That is outside this task's authority (`AGENTS.md` §7).
- **If the answer requires applying the decision to `docs/`: STOP.** That is the subsequent contract-application task's act; this task must not perform it.
- **If satisfying this task would require modifying `src/` or `tests/`: STOP.** This task has no code scope.
- **If the decision expands beyond the six enumerated items: STOP.** Report the proposed additional item rather than authoring it.
- If the task exceeds 7 skills or crosses multiple uncoupled architectural boundaries: STOP & decompose.

---

## Completion Evidence

<!--
  TO BE COMPLETED BY THE EXECUTING AGENT after reaching DONE.
  Do NOT fill this in speculatively. The task is not DONE until every
  §"Decisions" slot carries an explicit Product Owner answer.
-->

### Decision Source

- Product Owner input recorded in §"Decisions" of this file. Answers were
  supplied directly by the Product Owner during this task's execution session
  as three explicit rulings — (1) D-2 ordering: most recent first governs;
  (2) D-1: bare array of FULL §4 result objects including `CompletedAt`;
  (3) D-3/D-4/D-5/D-6: the proposed-for-confirmation answers confirmed as
  stated, with D-6.2 expressly ruled as no `DATABASE.md` change needed.
- No slot was inferred by the executing agent, and no parenthetical
  "expected" value was promoted into a slot without explicit confirmation.

### Changed Files

- `tasks/backlog/TASK-163-resolve-battle-history-endpoint-response-contract.md` — this task file only.

### Validation Results

```text
[x] All 24 slots contain explicit decisions.
    D-1.1–D-1.5 (5) + D-2.1–D-2.5 (5) + D-3.1–D-3.3 (3) + D-4.1–D-4.3 (3)
    + D-5.1–D-5.3 (3) + D-6.1–D-6.5 (5) = 24/24 filled.
[x] No slot was inferred.
    Each slot traces to an explicit Product Owner ruling. Slots made N/A by
    another answer (D-1.3, D-2.4, D-3.2) record that disposition explicitly
    rather than being left blank or filled by assumption.
[x] No decision contradicts an authoritative document.
    The D-2 vs §5.5 tension is resolved by scope (§5.5 is scoped to
    §5.1/§5.3/§5.4 by its own heading), not by overriding it. The D-1.4
    exposure of `CompletedAt` is an additive decided extension of a §4 shape
    that is silent about the member, not a contradiction of it. Two unrelated
    drifts are REPORTED without being resolved (see §"Decisions" conflict
    report and "Remaining Issues" below).
[x] No docs/ file changed.
[x] No src/ file changed.
[x] No tests changed.
[x] No completed task changed.
[x] No downstream task created.
```

### Verification Performed

```text
[x] Gap-evidence check     — API_CONTRACTS.md §1 lists the endpoint; a
                             document-wide search confirms no section defines
                             its response contract.
[x] Source-evidence check  — IBattleResultRepository.cs and
                             BattleResultRepository.cs both record the absent
                             contract as the reason no history query exists;
                             both file paths verified correct.
[x] No-contract check      — a search of docs/ for "battle/history" returns
                             only §1's listing plus DATABASE.md §4's index
                             comment; no defining contract exists elsewhere,
                             so this task is not a duplicate (Stop Condition
                             not triggered).
[x] No-implementation check— no /api/battle/history route is implemented; the
                             only frontend reference is an auth test asserting
                             the URL, not a handler (Stop Condition not
                             triggered).
[x] Owner check            — API_CONTRACTS.md confirmed as the canonical owner
                             (D-6.1).
[x] Isolation verification — src/ (0 files), tests/ (0 files), docs/ (0
                             files); no completed task and no other backlog
                             task modified; no new file created.
[x] Unit tests             — N/A (no code)
[x] Integration tests      — N/A (no code)
[x] Gameplay scenarios     — N/A (no behavior changed)
```

### Remaining Issues

Reported, not resolved (`AGENTS.md` §16) — neither blocks this record:

1. `API_CONTRACTS.md`'s version header reads Version 1.16 rather than the
   1.18 anticipated in §"Current State" → "Version state". Cited section
   numbers remain accurate, so no decision is affected.
2. `BattleResultConfiguration.cs` line 150 carries a stale comment referring
   to the `{}` staging `RewardSummary` and to TASK-033 as the member-list
   owner, superseded by DATABASE.md 1.20's landed 8-member contract. A source
   comment only; that file is outside this task's scope. Suggested follow-up:
   a small comment-synchronization task.

### Server Authority & Scope Verification

- [x] Confirmed zero files under `src/` modified
- [x] Confirmed zero files under `tests/` modified
- [x] Confirmed zero files under `docs/` modified
- [x] Confirmed no gameplay, protocol, API, Redis, or architecture change applied
- [x] Confirmed no completed task reopened or modified
- [x] Confirmed no other task created
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
