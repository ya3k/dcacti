# TASK-070 — Resolve Collection List Endpoint Response Contracts

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.

  This is a documentation/contract decision task (Outcome B of the
  post-TASK-069 dependency audit). API_CONTRACTS.md §5 does not define
  implementable response contracts for GET /api/cards, GET /api/relics or
  GET /api/pets/{petId}, and its "mirrors DATABASE.md" wording conflicts
  with what DATABASE.md §1 actually defines. This task identifies the exact
  unresolved questions and obtains the decisions; it does NOT decide them
  here, does NOT write endpoint code, and does NOT touch TASK-069.

  Workflow precedent: documentation/documentation-change.md + AGENTS.md §4
  (conflict → propose smallest correction → STOP for approval) + §7
  (missing rule → report, do not guess). Same shape as TASK-056 / TASK-068.
-->

---

## Metadata

```text
Task ID:           TASK-070
Type:              DOCUMENTATION (tasks/TASK_TYPES.md §2 — decision recorded
                   in the canonical owner document; zero src/ output)
Status:            DONE (2026-09-27 — human decisions D1–D4 received in
                   session; D5–D7 derived and recorded; file moved
                   backlog/ → completed/ per TASK_LIFECYCLE.md §5)
Risk:              MEDIUM (baseline for DOCUMENTATION is LOW-MEDIUM; raised
                   because the answer fixes an authorization-adjacent response
                   contract — cross-owner status code, ownership-derived
                   identity — that every later client collection consumer
                   will build against)
Priority:          HIGH (the client battle-start/loadout flow cannot be
                   tasked until these responses are defined; ROADMAP §1
                   Phase 1 "one playable battle, start to finish")
Primary Agent:     review (documentation consistency — TASK_TYPES.md §2 sets
                   DOCUMENTATION's Primary Agent to the Review Agent)
Supporting Agents: backend (API_CONTRACTS.md §5 is the contract this task
                   edits),
                   persistence (DATABASE.md §1/§2 is the entity source §5
                   cites — the "Card" entity conflict),
                   client (report-only: which members the battle-start
                   loadout selector actually needs)
Workflow:          documentation/documentation-change.md
Skills:            discovery/documentation-discovery,
                   discovery/impact-analysis,
                   quality/documentation-consistency,
                   quality/scope-validation
                   (4 skills — Normal budget, tasks/README.md §12)
Dependencies:      None (TASK-069 DONE — read-only context, must NOT be
                   modified or reopened, AGENTS.md §16)
Blocks:            the future "Implement Collection List Endpoints" task and
                   the future client battle-start/loadout selection task;
                   neither may be created until the decisions below are
                   recorded in API_CONTRACTS.md §5
Estimate:          Simple-Normal (documentation-only; seven questions, one
                   canonical owner doc, zero code)
```

**Status note.** `Status: BLOCKED` is set at creation, exactly as in
TASK-033/TASK-036/TASK-056: the stop condition was verified *before* the task
was written, not discovered during execution — the contract questions in §2
below cannot be derived from any authoritative document, so execution cannot
begin without a human decision. Per `tasks/README.md` §6 the file stays in
`backlog/` (per `TASK_LIFECYCLE.md` §4 the folder only changes when a started
task blocks: `active/ → blocked/`). `BLOCKED → IN PROGRESS` is the legal
resumption transition once a human supplies the decisions.

**Resolution (2026-09-27).** A human supplied all four non-derivable
decisions in session (each the proposed smallest-correction option):
**D1** = `cardId, name, category`; **D2** = `relicId, name`; **D3** = the
§5 example is the binding exhaustive member list; **D4** = bare-object 200
and `404 PET_NOT_FOUND` for both missing and foreign `petId`. D5–D7 were
derived from authoritative documents (no human input needed) and recorded
with citations. All seven outcomes were written once into
`API_CONTRACTS.md` §5 (v1.13), plus the §4 note 7 pointer repair and the
§1 detail-route pointer retarget. Transition: `BLOCKED → IN PROGRESS →
IN REVIEW → DONE`; file moved `backlog/ → completed/`.

---

## 1. Objective

Obtain explicit decisions for the response contracts of the four documented
collection read endpoints —

```text
GET     /api/pets
GET     /api/pets/{petId}
GET     /api/cards
GET     /api/relics
```

— and record each decided contract **once** in the canonical owner
(`docs/02-technical/API_CONTRACTS.md` §5; `DATABASE.md` only if a decision
actually changes an entity-level statement), so that a later implementation
task can build the endpoints without guessing any field, status code, or
ownership behavior. The task must answer, or put to a human, the seven
questions in §3; it must not answer them by inference.

---

## 2. Exact Contract Ambiguity (verified at creation)

Authoritative state, read 2026-09-27:

```text
API_CONTRACTS.md §1 (L82-85)   lists all four routes.
                               /api/pets/{petId} points to "PET_RULES.md".
API_CONTRACTS.md §5 (L617-637) heading covers only the THREE list endpoints
                               ("/api/pets, /api/cards, /api/relics") — the
                               single-Pet detail route has NO section.
                               Body: "Response shape mirrors the persistent
                               entities in DATABASE.md (Pet, Card, Relic)
                               plus progression fields (Tier/Star/Level for
                               Pets)" + ONE example, labeled "example, /api/pets".
API_CONTRACTS.md §6            error envelope { error, message } only.
DATABASE.md §1                 Pet, PetDefinition, CardDefinition,
                               PlayerUnlockedCard, RelicDefinition, Relic…
                               — there is NO entity named "Card".
DATABASE.md §2                 "Card ownership is settled (ADR-012)… unlock
                               flags — not a per-instance table"; equip is
                               battle-scoped, not persisted (no equip table
                               for Relics either).
PET_RULES.md / CARD_RULES.md /
RELIC_RULES.md                 define NO API response shape and no status
                               codes (verified by search: zero /api response
                               definitions).
Source                          zero collection endpoints exist
                               (no api/pets|api/cards|api/relics hits in
                               src/backend; only BattleController exists).
```

Unresolved consequences (each is a §16 stop condition):

1. **§5's premise is factually wrong for Cards.** "Mirrors the persistent
   entities… (Pet, Card, Relic)" cites a `Card` entity that `DATABASE.md`
   §1/§2 do not define (ownership is the `PlayerUnlockedCard` unlock-flag
   join + static `CardDefinition`). A response built from §5's sentence is
   not derivable — API_CONTRACTS §5 and DATABASE.md §1 conflict
   (AGENTS.md §4; specific technical doc > task).
2. **`/api/cards` and `/api/relics` have no response example at all** — not
   one member name, type, or nullability is stated anywhere in `docs/`.
3. **The `/api/pets` "example" is not declared exhaustive or binding**, so it
   is ambiguous whether members such as `xp` (`DATABASE.md §1 Pet.XP`,
   owned by PET_RULES §5) or `acquiredAt` are exposed, whether `petId` is
   formally `PetInstanceId`, and whether the definition-joined members
   (`identity`, `element`) are required or incidental.
4. **`GET /api/pets/{petId}` has no response contract whatsoever** — no
   200 shape (single object vs wrapper), no not-found status code, and no
   cross-owner behavior (§4 note 7's 404-not-disclosing-existence pattern is
   defined for the **result** endpoint only; it is a precedent, not a rule
   for this route). §1's pointer to `PET_RULES.md` yields nothing.
5. **List semantics are undefined** — ordering, pagination, and the
   empty-collection response are stated nowhere for any of the three list
   endpoints.
6. **Loadout/equip field exposure is unstated in §5** (the consumer is the
   loadout selector), while DATABASE.md §2 states equip state is not
   persisted at all — the contract should say so explicitly rather than
   leave consumers to infer absence.
7. **Cross-reference defect (documentation consistency):** API_CONTRACTS §4
   note 7 (L612) cites "`API_CONTRACTS.md` §7 item 1" for "identity is
   derived server-side from the session… never re-derived from client input
   at read time", but §7 item 1 is the gameplay-result prohibition and does
   not state that. The ownership-sentence's true source is §1's global
   authentication preamble + ADR-007 item 4 (+ ADR-015 for the 401 mapping).

**Already resolved — must NOT be re-opened or re-designed by this task:**
authentication requirement and `401 UNAUTHENTICATED` behavior (§1 preamble,
ADR-007 item 4, ADR-015, §4 note 6); ownership source = authenticated
session with no client-supplied identity (§4 note 7, ADR-001/ADR-011/ADR-014,
`GAME_RULES.md` §18); error envelope shape (§6); the entire
`POST /api/battle/start` contract (§3).

---

## 3. Decisions Required (the task's output)

One explicit human decision per item; record each in `API_CONTRACTS.md` §5
(canonical owner — `documentation/documentation-change.md` §3):

```text
D1  /api/cards response DTO — exact member list, types, nullability, and
    the identifier semantics, given that no "Card" entity exists
    (PlayerUnlockedCard + CardDefinition per DATABASE.md §1/§2, ADR-012).
D2  /api/relics response DTO — exact member list, types, nullability;
    which members come from the Relic instance row vs RelicDefinition.
D3  /api/pets list DTO — is the §5 example the binding member list? Which
    of the persisted Pet members (notably xp, acquiredAt) are exposed? Is
    petId formally the PetInstanceId? Are joined definition members
    (identity, element) required members?
D4  GET /api/pets/{petId} — response shape, 200/404 behavior, and the
    cross-owner result (404 vs 403 vs other), including which document owns
    this route's section (§1 currently points at PET_RULES.md, which
    defines no response). Note §1's route list must stay consistent with
    whichever section owns it.
D5  List semantics for /api/pets, /api/cards, /api/relics — ordering (or an
    explicit statement that order is unspecified), pagination (documented
    as absent unless a doc already requires it — do not invent any), and
    the empty-collection response.
D6  Loadout/equip exposure — confirm and state that collection responses
    carry NO equip/loadout/active/slot members (DATABASE.md §2: equip is
    battle-scoped and unpersisted; ADR-011), so clients never read equip
    state from these endpoints.
D7  Repair the §4 note 7 cross-reference identified in §2 item 7 (point it
    at the passage that actually states read-time identity derivation).
```

**Decisions recorded in `API_CONTRACTS.md` §5 (v1.13):**

```text
D1  HUMAN (2026-09-27, smallest-correction option): /api/cards members =
    cardId (CardDefinition.CardDefinitionId), name, category
    ("Basic"|"PetSkill"); unlock state = array membership (no `unlocked`
    member — no Card entity exists, ADR-012); playerId, powerCost,
    loadoutCopyLimit, effectDefinition not exposed (§3 validates
    server-side). → §5.3
D2  HUMAN (2026-09-27, smallest-correction option): /api/relics members =
    relicId (Relic.RelicInstanceId — the id submitted in relicLoadout
    §3 / snapshotted into PetState.EquippedRelics[]), name
    (RelicDefinition.Name); playerId, acquiredAt, definitionId and
    Trigger/Condition/EffectDefinition not exposed. → §5.4
D3  HUMAN (2026-09-27, smallest-correction option): the former §5 example
    is the binding exhaustive member list — petId (Pet.PetInstanceId),
    identity, element, tier, star, level; xp, acquiredAt, playerId,
    petDefinitionId explicitly not exposed. → §5.1
D4  HUMAN (2026-09-27, smallest-correction option): 200 = the same bare
    object as one /api/pets element (no wrapper); missing and foreign
    petId both → 404 PET_NOT_FOUND (no existence disclosure, extending
    §4 note 7's pattern). Section ownership = this document: §5 now owns
    the route; §1's pointer retargeted from PET_RULES.md (defines no
    response) to §5. → §5.2, §1
D5  DERIVED: empty collection → 200 with []; no ordering defined (clients
    must not rely on order); no pagination/filtering/sorting/search in MVP
    (none required by any document — recorded as absent). → §5.5
D6  DERIVED: no collection response carries isEquipped/equipped/slot/
    loadoutPosition/active (equip battle-scoped and unpersisted —
    DATABASE.md §2, ADR-011; snapshot only in GAME_STATE.md §2.3). → §5.6
D7  DERIVED: §4 note 7's "(`API_CONTRACTS.md` §7 item 1)" pointer was
    wrong (§7 item 1 is the gameplay-result prohibition); corrected to
    §2.8, which actually states the client never supplies or overrides
    PlayerId for authentication or ownership. → §4 note 7
```

If a decision is determinable from an authoritative document already, record
the derivation with its citation instead of asking (AGENTS.md §7 does not
apply to answers that exist). Anything not determinable goes to the human —
**the task must not choose an answer, invent a field, or add a DTO
convention of its own** (AGENTS.md §20 "Ambiguous requirement").

---

## Authoritative References

- `docs/02-technical/API_CONTRACTS.md` §1 (route list), §5 (the contract
  being resolved — canonical owner), §6 (error envelope), §4 notes 6–7
  (auth/ownership precedent), §3 (battle/start consumers of these responses)
- `docs/02-technical/DATABASE.md` §1 (Pet / PlayerUnlockedCard / CardDefinition /
  Relic / RelicDefinition entities), §2 (ownership relationships, unlock-flag
  note, no equip tables)
- `docs/02-technical/GAME_STATE.md` §2.3 — battle-scoped EquippedCards /
  EquippedRelics snapshot (equip is not collection state)
- `docs/01-game-design/PET_RULES.md` §2 (Pet ownership), `CARD_RULES.md` §1
  (unlock + LoadoutCopyLimit), `RELIC_RULES.md` §2 (owned instances)
- `docs/03-decisions/ADR/ADR-011*` — Player owns persistent collection;
  loadout battle-scoped; `ADR-012*` — Card unlock-flag ownership;
  `ADR-007*` item 4 / `ADR-015*` — authenticated session + 401 mapping;
  `ADR-001*` — server authority; `ADR-014*` — identity never from client input
- `docs/00-overview/MVP_SCOPE.md` §1 — collection reads for loadout
  selection are IN; `ROADMAP.md` §1 Phase 1
- `AGENTS.md` §4 (conflict resolution), §7 (missing rule → report),
  §17 (documentation change rule), §20 (stop conditions)
- `.ai/workflow/documentation/documentation-change.md` — canonical-owner
  editing, no duplication

---

## Scope

### In Scope
- The seven decisions D1–D7 and their single recording in
  `API_CONTRACTS.md` §5 (plus a §5 heading/§1-list fix if D4 assigns the
  detail route a section, and the D7 pointer repair).
- Synchronizing only the references that become stale as a direct result
  (per documentation-change workflow: smallest authoritative source first).

### Out of Scope
- **No endpoint implementation** — no `src/` or `tests/` changes of any kind.
- **No battle-start work** — `POST /api/battle/start` (§3) is already
  implemented and unchanged by this task.
- **No JoinBattle implementation; no frontend loadout UI; no client code.**
- **No undocumented response fields, no invented pagination/filtering/
  sorting/search, no envelope/DTO conventions, no new auth or ownership
  model** — anything not documented or decided by a human is not written.
- **No `GetBattleState`** — Phase 3, excluded by project decision.
- **No gameplay content** — Match-3/Swap/Combat/CardCast/PetSkillCast/XP/
  Rewards are untouched; TASK-069's completed Swap path
  (`SignalRService.swap`, `GameRuntime.requestAction(Swap)`, `BattleScene`
  two-cell selection) must not be modified or reopened.
- **No housekeeping** — file moves/renames/cleanup are not this task.
- Report-only observation, not a decision here: no endpoint lists Bosses,
  yet §3 requires a `bossId`; bossId sourcing for the future client
  battle-start task is a separate question and must not be designed here.

---

## Current State

*(State at creation — superseded by the recorded decisions in §3 and
Completion Evidence below.)*

`API_CONTRACTS.md` v1.12 §5 gives one pets example plus a "mirrors
DATABASE.md (Pet, Card, Relic)" sentence; §1 lists a fourth route
(`/api/pets/{petId}`) that no section defines. `DATABASE.md` defines no
`Card` entity. No collection endpoint exists in the backend (only
`BattleController`), and no task in `tasks/` has ever covered these
endpoints (searched: `collection`, `GET /api/pets`, `api/cards`,
`api/relics`, `battle/start`, `JoinBattle`, `loadout`, `ownership` — the
hits are battle-start/loadout tasks 027/028/030/038/039 and unrelated
contract tasks; none define collection responses). TASK-069 is DONE in
`tasks/completed/` and is read-only context.

---

## Dependencies

- **Requires:** a human API-contract decision on D1–D7 (BLOCKED).
- **Read-only inputs:** TASK-069 (DONE — must not be edited).
- **No code or ADR dependency:** no existing ADR conflicts with resolving
  §5 (ADR-011/ADR-012 already fix ownership; this task only fixes transport
  shapes), so no new ADR is required — if a decision *would* need one,
  STOP per AGENTS.md §18.

---

## Affected Files & Areas

```text
[ ] src/backend/                 (NOT touched — no implementation)
[ ] src/frontend/client/         (NOT touched)
[ ] tests/                       (NOT touched)
[x] docs/02-technical/API_CONTRACTS.md   (§5 records D1-D6; §4 note 7
                                          pointer fix D7; §1/§5 heading
                                          consistency if D4 moves the route)
[ ] docs/02-technical/DATABASE.md        (verified §1/§2 — no entity
                                           statement changed, so NOT
                                           edited)
[x] tasks/                        (this file only — updated and moved
                                    backlog/ → completed/; completed
                                    tasks immutable)
```

---

## Implementation Notes

- `documentation/documentation-change.md`: identify owner → read referencing
  docs → check conflicts → **stop on conflict, do not silently resolve**
  (AGENTS.md §4) → edit the smallest authoritative source → fix stale
  references → re-read for consistency. §5's conflict with DATABASE.md §1 is
  exactly the §4 case: explain it plainly, propose the smallest correction,
  wait for approval.
- The seven questions are deliberately phrased so that answering them does
  not require re-deriving battle-start: §3's `petId`/`cardLoadout`/
  `relicLoadout` consumers are listed only to show which members matter.
- Keep §5 a contract, not a design essay: decided member lists and status
  codes, no rationale dumps (AGENTS.md §21).

---

## Acceptance Criteria

- [x] D1–D7 each has an explicit recorded outcome (human decision, or a
      derivation cited to an authoritative section) — see "Decisions
      recorded" in §3 above
- [x] `API_CONTRACTS.md` §5 defines, for each of `/api/pets`,
      `/api/cards`, `/api/relics`: exact response members, field types,
      nullability, 200 semantics, error/status codes, ownership rule, and
      the D5 ordering/pagination/empty-collection answer (§5.1, §5.3–§5.5)
- [x] `GET /api/pets/{petId}` has a defined section, 200 shape, not-found
      status, and cross-owner behavior (D4) — §5.2, and §1's pointer now
      targets §5 instead of `PET_RULES.md`
- [x] The §5 vs `DATABASE.md` §1 "Card entity" conflict is stated and
      resolved per AGENTS.md §4 with the smallest correction (§5.3 answers
      the route from `PlayerUnlockedCard` + `CardDefinition` without
      adding a `Card` entity anywhere — `DATABASE.md` not edited)
- [x] D6's "no equip/loadout members" statement is present in §5 (§5.6)
- [x] D7's cross-reference in §4 note 7 resolves to text that actually
      states the cited property (now §2.8)
- [x] Zero `src/` and zero `tests/` changes; TASK-069 byte-identical
      (never opened for edit this session — read-only context);
      no TASK-071 created
- [x] Quality review checklist passes (`quality/review.md` §1, doc items
      only) — recorded in Completion Evidence below
- [x] No authoritative rule or contract violated (AGENTS.md §10 / ADR-001);
      MVP scope confirmed (`MVP_SCOPE.md` §1 — collection reads are IN)

---

## Testing Requirements

### Required Verification
```text
[x] Unit tests         — N/A (documentation-only)
[x] Integration tests  — N/A (documentation-only)
[x] Doc consistency    — re-read API_CONTRACTS §1/§4/§5/§6 with DATABASE.md
                         §1/§2 together; confirmed no duplicated definition
                         and no stale cross-reference remains
                         (quality/documentation-consistency) — PASS:
                         §5 answers cards/relics without restating entity
                         definitions; §4 note 7 now points at §2.8; §1
                         points at §5; no doc in docs/ cites the old §5
                         heading or §7 item 1 for identity derivation
                         (DATABASE.md's §7 item 1 citation concerns the
                         gameplay-result prohibition, which §7 item 1
                         actually states — left unchanged, report-only)
[x] Scope check        — §5 contains nothing beyond decided members/status
                         codes: no pagination invented (documented absent),
                         no new fields beyond the human-chosen D1–D3 lists
                         (quality/scope-validation) — PASS
```

### Key Edge Cases
- Empty collection response is decided explicitly (D5), not left implicit.
- Cross-owner vs nonexistent pet are decided distinctly or documented as
  indistinguishable (D4) — matching §4 note 7's stated rationale pattern.
- Members persisted by DATABASE.md but omitted from a response (`xp`,
  `acquiredAt`, `PlayerId`) are decided as exposed or excluded, never
  "unknown".

---

## Stop Conditions

- Universal stops: AGENTS.md §20 (rule conflict, missing rule, ambiguous
  requirement, contract conflict) always apply.
- If a human decision contradicts an ADR or another authoritative document:
  STOP and report the conflict (AGENTS.md §4/§18) — do not reconcile.
- If answering a decision would require inventing a field, status code,
  pagination, or DTO convention not present in docs and not explicitly
  chosen by a human: STOP (AGENTS.md §7).
- If the scope ever grows to include endpoint code, battle-start, JoinBattle,
  loadout UI, or `GetBattleState`: STOP — those are different tasks
  (AGENTS.md §16).

---

## Completion Evidence

### Changed Files
- `docs/02-technical/API_CONTRACTS.md` — version 1.12 → 1.13 (changelog
  entry naming TASK-070 and the D1–D7 outcomes); §5 rewritten with §5.1
  (`/api/pets`), §5.2 (`/api/pets/{petId}`), §5.3 (`/api/cards`),
  §5.4 (`/api/relics`), §5.5 (list semantics), §5.6 (no equip/loadout
  members); §4 note 7 pointer corrected to §2.8 (D7); §1 detail-route
  pointer retargeted from `PET_RULES.md` to §5 (D4)
- `tasks/backlog/TASK-070-resolve-collection-list-endpoint-response-contracts.md`
  — decisions recorded, AC/evidence completed; moved to
  `tasks/completed/` (BLOCKED → IN PROGRESS → IN REVIEW → DONE)
- Not edited (verified): `DATABASE.md` (no entity statement changed —
  §5.3/§5.4 answer routes from existing `PlayerUnlockedCard` +
  `CardDefinition` / `Relic` rows), all `src/`, all `tests/`,
  `tasks/completed/*` (TASK-069 included)

### Validation Results
`quality/review.md` §1 (documentation task — code-only items skipped):
- **Correctness** — PASS: every §5 member cites its owning source
  (`DATABASE.md` §1/§2/§3, `CARD_RULES.md` §1, `RELIC_RULES.md` §2.2,
  `GAME_STATE.md` §2.3); statuses match §1/§4 note 7/§6 patterns;
  D1–D4 match the human's recorded choices verbatim.
- **Architecture** — PASS: transport contract stays in API_CONTRACTS
  (canonical owner, documentation-change §3); no domain rule duplicated
  (`GAME_STATE`/`RELIC_RULES`/`CARD_RULES` referenced, not restated).
- **Scope** — PASS: docs-only; no pagination/fields/conventions beyond
  the decisions; no battle-start/JoinBattle/loadout-UI/GetBattleState
  work; no DATABASE.md edit; no TASK-071.
- **Tests** — N/A (documentation-only).
- **Documentation** — PASS: version bumped; §1/§4/§5 synchronized; no
  other document cites a now-stale reference (searched `docs/`).
- **Security** — PASS: ownership stated as session-derived only; the 404
  non-disclosure prevents cross-Player existence probing; no identity or
  data exposure added.
- **Performance** — N/A. **Maintainability** — PASS: no abstraction or
  convention added; contract stated as flat member lists. **Determinism**
  — N/A (no gameplay/RNG surface).

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative behavior introduced (§5 is
      read-only reporting of server-owned collection state)
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1 — collection
      reads for loadout selection are IN; pagination/Filtering OUT)
- [x] Confirmed TASK-069 and all completed tasks unmodified
