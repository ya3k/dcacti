# TASK-097 — Synchronize StatusEffect and Pet/Card/Relic Provisioning Implementation Status

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ by path and section — it does NOT copy game rules,
  formulas, schemas, or contracts.

  THIS TASK AUTHORS NO CONTRACT. It corrects implementation-STATUS wording
  that completed implementation work has made false. Every contract it
  touches stays byte-identical in meaning.

  BOUNDARY: documentation accuracy only. It touches no source file, no
  test, no migration, no schema, and no gameplay rule.
-->

---

## Metadata

```text
Task ID:           TASK-097
Type:              DOCUMENTATION (TASK_TYPES.md §2 — "A document needs to be
                   corrected"; §3 selection guide: "A document needs to be
                   corrected or added" → DOCUMENTATION)
Status:            DONE
Risk:              LOW (TASK_TYPES.md §4 — DOCUMENTATION baseline LOW–MEDIUM;
                   LOW because this is a status correction that changes no
                   contract, no value, and no behavior. It is not the MEDIUM
                   case, because it introduces no cross-referenced contract
                   change — it removes a now-false statement about work that
                   is already complete.)
Priority:          MEDIUM (the false statements mislead any agent reading
                   GAME_STATE.md §2.3 / DATABASE.md §5 item 4 into believing
                   completed work is still pending, and would cause a
                   duplicate implementation task to be created)
Primary Agent:     review (TASK_TYPES.md §5 Domain × Type matrix —
                   Documentation → Review; review owns documentation
                   consistency)
Supporting Agents: realtime (StatusEffect serializer status — TASK-096 owner),
                   persistence (provisioning migration status — TASK-085 owner)
Workflow:          documentation/documentation-change.md
Skills:            discovery/documentation-discovery,
                   quality/documentation-consistency,
                   quality/scope-validation
                   (3 skills — Simple budget, tasks/README.md §12)
Dependencies:      TASK-095 (DONE — StatusEffect Domain state and step-19a
                     lifecycle implemented),
                   TASK-096 (DONE — StatusEffects serializer round-trip
                     implemented),
                   TASK-085 (DONE — Pet/Card/Relic provisioning migration
                     completed),
                   TASK-083 (DONE — player starter ownership initialization,
                     context for DATABASE.md §5 item 4 item 3)
Blocks:            None. This task unblocks no implementation; it removes a
                   documentation defect that would otherwise cause a
                   duplicate implementation task to be created.
Estimate:          Simple (two documents, a small number of now-false status
                   clauses; no new prose, no contract restated)
```

**Type classification note.** `DOCUMENTATION`, not `BUG`. Although `TASK_TYPES.md`
§2's BUG definition includes "documentation bugs (docs are stale)", the same
section states the bug category **must be determined first**
(`development/bug-fix.md` §1), and §3's selection guide routes "a document needs
to be corrected" to `DOCUMENTATION`. This task's primary and only output is a
correction to `docs/`; no code, test, or behavior is involved. `DOCUMENTATION`
with `documentation/documentation-change.md` is therefore the governing type and
workflow, matching the TASK-089 precedent for exactly this class of change.

**This task is not a general documentation audit.** It is scoped to two
verified, enumerated defect areas. Anything not listed in §"Verified
Documentation Defects" is out of scope, even if it also looks stale
(`AGENTS.md` §16 — report, do not fix inline).

---

## Objective

Correct the implementation-**status** statements in `docs/02-technical/GAME_STATE.md`
and `docs/02-technical/DATABASE.md` that still describe the `StatusEffects[]`
collection and the Pet/Card/Relic content-definition provisioning as
"not yet implemented" / "not yet written", so that both documents accurately
reflect the implementation that TASK-095, TASK-096, and TASK-085 have already
completed — while changing no contract, no field, no value, no lifecycle
semantic, and no schema.

---

## Authoritative References

- `docs/02-technical/GAME_STATE.md` §0 item 4 — **the rule this task's
  correction must respect**: "A field absent from §2.0 is **not yet
  implemented**, not **not required**." The staging contract this item defines
  is the vocabulary being corrected; the item itself must not be reworded.
- `docs/02-technical/GAME_STATE.md` §0 item 5 — the no-parallel-representation
  rule; this task introduces none.
- `docs/02-technical/GAME_STATE.md` §1 — the state-category table and the
  staged-state framing (§2.0, §2.0.5).
- `docs/02-technical/GAME_STATE.md` §2.3 — the `PetState` tree and the
  prose stating `StatusEffects[]` is contract-defined but "not yet
  implemented" (items around the `StatusEffects[]` / `EquippedRelics[]` /
  `EquippedCards[]` entries, and the "remaining collection member" paragraph).
- `docs/02-technical/GAME_STATE.md` §2.3.1 — the **instance schema** being
  referenced, not changed.
- `docs/02-technical/GAME_STATE.md` §2.3.2 — the **serialization and round-trip
  contract** being referenced, not changed.
- `docs/02-technical/GAME_STATE.md` §2.4 / §2.4.1 — the `BossState` counterpart
  and its identical schema/lifecycle statement.
- `docs/02-technical/GAME_STATE.md` §5.1 / §5.1.1 — the write-back and the
  lifecycle whose implementation status is being corrected.
- `docs/02-technical/DATABASE.md` §5 item 4 — the provisioning contract whose
  **mechanism is decided** and whose **implementation status wording** is stale.
- `docs/02-technical/DATABASE.md` §1 — the `PetDefinition` / `CardDefinition` /
  `RelicDefinition` entity blocks (schema owner; not changed).
- `docs/02-technical/DATABASE.md` §2 — the MVP starter ownership contract
  (TASK-084); referenced for context, not changed.
- `docs/02-technical/REDIS_STATE.md` §2 item 1, §7 item 9 — the round-trip
  obligation the completed serializer discharges; referenced, not changed.
- `tasks/completed/TASK-095-implement-statuseffect-domain-state-and-step-19a-lifecycle.md`
  — implemented the Domain `StatusEffect` model and the §17 step 19a lifecycle
  (read-only; not modified).
- `tasks/completed/TASK-096-serialize-statuseffects-round-trip.md` — implemented
  the `statusEffects` serializer member and round trip; its Out of Scope item
  *"`docs/` — No documentation change is expected … report instead if
  implementation reveals a documentation gap, per AGENTS.md §17"* is the
  deferral this task discharges (read-only; not modified).
- `tasks/completed/TASK-085-provision-mvp-pet-card-relic-content-definitions.md`
  — completed the Pet/Card/Relic provisioning migration (read-only; not
  modified).
- `tasks/completed/TASK-083-implement-player-creation-starter-ownership-initialization.md`
  — records the provisioned row sets and migration identity (read-only; not
  modified).
- `tasks/completed/TASK-089-synchronize-rewardsummary-documentation-contracts.md`
  — the structural precedent for a documentation-only status synchronization
  task (read-only; not modified).
- `docs/03-decisions/ADR/ADR-005-redis-active-battle-state.md` — Redis is the
  active battle state store; unchanged by this task.
- `docs/03-decisions/ADR/ADR-001-server-authoritative-battle.md` — server
  authority; unchanged by this task.
- `AGENTS.md` §17 — the documentation-change rule this task follows: the
  documents are outdated **relative to already-landed code**, so the
  documentation is corrected and the code is not.
- `AGENTS.md` §4 — if a correction would require reinterpreting a contract
  rather than restating an implementation-status fact, that is a STOP.

**ADR check:** no ADR is required. This task records no architectural decision;
it removes a false statement about work whose architecture was already decided
and implemented (`ADR-005` is satisfied unchanged, and the provisioning
mechanism was decided by TASK-052/TASK-082 and recorded in `DATABASE.md`).

---

## Current State

Both targeted documents currently assert that completed work is still pending.

### GAME_STATE.md (verified)

```text
docs/02-technical/GAME_STATE.md   (Version 2.8 — authored by TASK-093)

§2.3 prose states StatusEffects[] "is now contract-defined (§2.3.1, §5.1.1)
   but is not yet implemented — the contract below is authoritative and a
   later task implements it."

§2.3 prose states the "remaining collection member ... is contract-defined by
   §2.3.1 below and is not yet implemented — not not required (§0 item 4)."

§2.3 PetState tree entries carry "(... — not yet implemented)" on
   EquippedRelics[] and EquippedCards[].

§2.4 / §2.4.1 carry the parallel StatusEffects[] statement for BossState.
```

Reality today:

```text
src/backend/GameServer.Domain/Battle/
  StatusEffect.cs             IMPLEMENTED (TASK-095)
  StatusEffectType.cs         IMPLEMENTED (TASK-095)
  StatusEffectLifecycle.cs    IMPLEMENTED (TASK-095)
  PetState.cs                 ActiveStatusEffects implemented, [] default
  BossState.cs                ActiveStatusEffects implemented, [] default

src/backend/GameServer.Domain/Battle/Serialization/
  BattleStateJson.cs          statusEffects member on PetStateJson AND
                              BossStateJson (TASK-096)
  BattleStateSerializer.cs    element projection both directions (TASK-096)

tests/backend/GameServer.Domain.Tests/BattleStateSerializationTests.cs
                              presence + round-trip assertions (TASK-096)
```

`EquippedRelics[]` / `EquippedCards[]` are likewise implemented (TASK-027 /
TASK-028, snapshotted by `POST /api/battle/start` per TASK-030) and are mapped
as `equippedRelics` / `equippedCards` in `BattleStateJson.cs`. Their
"not yet implemented" tree annotations are false for the same reason.

### DATABASE.md (verified)

```text
docs/02-technical/DATABASE.md   (Version 1.20)

§5 item 4 states the provisioning implementation (migration script and exact
row values) "remains an implementation detail (item 1) and is not yet written
— it is the follow-up provisioning implementation task."
```

Reality today:

```text
src/backend/GameServer.Infrastructure/Postgres/Migrations/
  20260929152651_ProvisionPetCardRelicContentDefinitions.cs   EXISTS (TASK-085)
      INSERTs PetDefinition (3), CardDefinition (6), RelicDefinition (4)
      — e.g. "pet-xich-lang" verified in the migration body

Player starter ownership initialization is implemented (TASK-083), including
single-scope SaveChangesAsync grant of the 7 starter ownership rows.
```

The same item already correctly records that the `BossDefinition` migration is
**complete** (`20260926151112_ProvisionBossDefinitions`, TASK-053) — so the
document is internally inconsistent: one provisioning area is recorded as
complete, the other as pending, although both are complete.

---

## Scope

### In Scope

1. **`docs/02-technical/GAME_STATE.md`** — correct every implementation-status
   statement that still declares `StatusEffects[]` (on `PetState` and on
   `BossState`) to be not yet implemented, so the document states the
   implemented position consistently with §0 item 4's vocabulary (implemented
   vs. not yet implemented).
2. **`docs/02-technical/GAME_STATE.md`** — correct the equivalent stale
   "not yet implemented" annotations on the `EquippedRelics[]` and
   `EquippedCards[]` tree entries, which TASK-027/TASK-028/TASK-030 implemented.
   *Only* include these if verification confirms the annotation is still
   present and still false at pickup (see Implementation Notes).
3. **`docs/02-technical/DATABASE.md`** §5 item 4 — correct the "is not yet
   written — it is the follow-up provisioning implementation task" statement so
   it records the completed migration
   (`20260929152651_ProvisionPetCardRelicContentDefinitions`, TASK-085) and the
   completed starter-ownership initialization (TASK-083), in the same style the
   same item already uses for the completed `BossDefinition` migration.
4. **Version/changelog metadata** in each edited document **only if** the
   repository's documentation convention requires it — both documents carry a
   version header with a "Prior x.y:" changelog chain, and TASK-089 (the
   precedent) updated it. Follow the existing convention; do not invent one.
5. **Reporting, not fixing, any further stale statement discovered** that is not
   enumerated in this task (`AGENTS.md` §16).

### Out of Scope

- **Any source code** — nothing under `src/`.
- **Any test change** — nothing under `tests/`.
- **Any migration** — no new migration, no edit to
  `20260929152651_ProvisionPetCardRelicContentDefinitions`, no `dotnet ef`
  invocation.
- **Any schema change** — no entity member, column, constraint, or index.
- **The StatusEffect contract** — §2.3.1's instance schema, §2.3.2's
  serialization shape, §5.1.1's lifecycle, and §2.3.3 are **not** reworded,
  re-derived, or extended.
- **Lifecycle semantics** — no apply/refresh/consume/expire rule changes.
- **Serialization behavior** — no member added, removed, or renamed.
- **Redis** — no key, field, TTL, concurrency, or record change
  (`REDIS_STATE.md` is not edited).
- **SignalR** — no event, payload member, or Hub method; `StatusEffects[]`
  remains **not** a wire member (`SIGNALR_PROTOCOL.md` §4.2 item 2).
- **PostgreSQL BattleState persistence** — active battle state remains
  Redis-scoped (`AGENTS.md` §13).
- **Gameplay rules** — no Match-3, Combat, Status Effect, Pet, Card, Relic, or
  Boss rule change.
- **Boss Skill secondary-effect producers** — not designed, not resolved, not
  scoped here. This is a separate future implementation concern.
- **`GetBattleState` / reconnect-resync** — not touched
  (`SIGNALR_PROTOCOL.md` §7).
- **`tasks/blocked/TASK-036-*.md`** — not reopened, not modified.
- **`tasks/blocked/TASK-079-*.md`** — not resolved, not modified.
- **`tasks/completed/`** — immutable (`TASK_LIFECYCLE.md` §3); not modified.
- **Any general documentation audit or cleanup** — no sweep beyond the
  enumerated defects.
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2.

---

## Acceptance Criteria

- [x] `docs/02-technical/GAME_STATE.md` no longer states or implies that
      `StatusEffects[]` is not yet implemented, on `PetState` **or** on
      `BossState`.
- [x] `docs/02-technical/GAME_STATE.md` no longer states or implies that
      `EquippedRelics[]` or `EquippedCards[]` is not yet implemented.
- [x] No stale equivalent wording remains anywhere in
      `docs/02-technical/GAME_STATE.md` for the `StatusEffects[]` and
      loadout-collection implementation status (all occurrences corrected, not
      only the first).
- [x] `docs/02-technical/DATABASE.md` no longer states or implies that the
      Pet/Card/Relic provisioning migration is still pending, and records the
      completed migration by identifier
      (`20260929152651_ProvisionPetCardRelicContentDefinitions`).
- [x] No stale equivalent wording remains in `docs/02-technical/DATABASE.md`
      for that provisioning implementation status.
- [x] The `StatusEffects[]` contract is unchanged: §2.3.1's instance-member set,
      §2.3.2's serialization shape and round-trip obligation, §5.1.1's
      lifecycle, and §2.3.3 are semantically identical before and after.
- [x] The provisioning **contract** (mechanism, permitted rows, value sourcing)
      in `DATABASE.md` §1 / §5 item 4 is unchanged — only its implementation
      status wording changes.
- [x] `GAME_STATE.md` §0 items 4 and 5 are unchanged.
- [x] No database schema, entity, column, constraint, or migration is changed.
- [x] Zero files under `src/` are modified.
- [x] Zero files under `tests/` are modified.
- [x] No SignalR contract is changed (`SIGNALR_PROTOCOL.md` unmodified).
- [x] No Redis contract is changed (`REDIS_STATE.md` unmodified).
- [x] No gameplay rule is changed (`docs/01-game-design/` unmodified).
- [x] No `tasks/completed/` file is modified; TASK-036 and TASK-079 are
      unmodified (`TASK_LIFECYCLE.md` §3, `AGENTS.md` §16).
- [x] Document version/changelog metadata is updated **iff** the repository's
      documentation convention requires it, following the existing
      `Version: x.y (§… per TASK-NNN …) — Prior x.y:` form; no new convention is
      invented.
- [x] No duplicate definition is introduced: the correction states a
      completion fact and does not restate any rule owned by another document
      (`documentation-change.md` §2).
- [x] Documentation consistency check passes — each edited document re-read
      together with the documents that reference it.
- [x] Quality review checklist passes (`quality/review.md` §1), skipping
      code-only items per `documentation-change.md` §4.
- [x] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001).

### Explicit Constraints

```text
No source code changes.
No database migration.
No schema changes.
No gameplay changes.
No SignalR changes.
No Redis changes.
No StatusEffect contract changes.
No lifecycle semantics changes.
```

---

## Affected Files & Areas

```text
[ ] src/backend/ (none)
[ ] src/frontend/client/ (none)
[ ] tests/ (none — documentation-only change; existing suites must remain green
            UNMODIFIED, proving no source was touched)
[x] docs/02-technical/GAME_STATE.md (stale implementation-status wording only)
[x] docs/02-technical/DATABASE.md   (§5 item 4 stale implementation-status
                                     wording only)
[x] tasks/backlog/TASK-097-*.md (this file — Status field only)
[ ] tasks/completed/ (NO CHANGES)
[ ] tasks/blocked/TASK-036-*.md, tasks/blocked/TASK-079-*.md (NO CHANGES)
```

---

## Documentation Notes

- **Canonical owner discipline.** `GAME_STATE.md` owns the Battle State model
  and its staged implementation contract; `DATABASE.md` owns persistence and
  the provisioning contract. Each correction belongs to exactly one of them
  (`docs/AGENTS.md` §2, `documentation-change.md` §3). Do not mirror the
  `GAME_STATE.md` correction into `DATABASE.md` or vice versa.
- **Correct status, never the contract.** `AGENTS.md` §17's rule is that the
  design is not changing here — the *documentation* is behind the already-landed
  implementation. The edit is therefore one-directional: the document is
  brought up to the code, and no code is brought to the document.
- **Preserve §0 item 4's vocabulary.** That item defines the
  "not yet implemented / not required" distinction and is itself a contract
  statement. Do not reword it; apply its vocabulary correctly elsewhere.
- **§2.0 / §2.0.5 staging framing must survive.** `GAME_STATE.md` §2.0 is the
  staged implementation contract and §2.0.5 its board stage. Correcting a field's
  status does not change the staging model, its item 4, or the "§2.0's field list
  grows toward §2" statement. Do not add a staging stage and do not remove one.
- **Match the existing register.** Both documents use a terse, citation-marked
  style (`§`, `TASK-NNN`). The replacement wording should read as a status
  update in that register — do not introduce new prose sections, new headings,
  or new vocabulary.
- **Do not prescribe or invent values.** The migration identifier, the
  provisioned row counts, and the table names to cite are taken from
  `TASK-085` / `TASK-083` evidence and `DATABASE.md` §1 — never invented.
- **Task-ID citations are the repository's existing convention.** Both documents
  already cite `TASK-0NN` inline (e.g. `DATABASE.md` §5 item 4 cites TASK-052,
  TASK-053, TASK-082; `GAME_STATE.md` cites TASK-027/028/030/046). Follow it.
- **Version-header convention.** Each document's `Version:` line carries a
  cumulative changelog with `Prior x.y:` entries. If the convention requires a
  bump, extend it in the established form and attribute the change to TASK-097;
  do not renumber or rewrite prior entries.
- **No contract drift.** After editing, the contract sections named in the
  acceptance criteria must be provably unchanged — a diff limited to
  status/version wording is the expected shape.
- **Stop rather than reinterpret.** If any targeted sentence turns out to carry
  a contract meaning as well as a status meaning, that is an `AGENTS.md` §4
  STOP — report it and do not edit it.

---

## Testing Requirements

### Required Verification

```text
[ ] Unit tests         — N/A: no code is added or changed (core/validation.md
                         §2 depth for a documentation-only change).
[ ] Integration tests  — N/A: no boundary is implemented or altered.
[ ] Gameplay scenarios — N/A: no gameplay rule is derived, changed, or
                         exercised (AGENTS.md §6 maps gameplay scenarios to
                         rule docs; none is touched here).
[x] Documentation consistency — each edited document re-read together with its
                         referencing documents; no duplicated definition
                         introduced (documentation-change.md §2).
[x] Contract-preservation check — the StatusEffect contract sections
                         (§2.3.1, §2.3.2, §2.3.3, §5.1.1) and the provisioning
                         contract (DATABASE.md §1, §5 item 4 mechanism) are
                         verified semantically unchanged.
[x] Stale-wording sweep — an exhaustive search of BOTH documents for
                         "not yet implemented", "not yet written", "deferred",
                         and equivalent phrasings, confirming no residual false
                         status statement remains for the enumerated areas.
[x] Scope/changed-file verification — the changed-file set equals exactly the
                         set declared in Affected Files & Areas.
[x] Guard checks        — `git status` shows no `src/` or `tests/` change;
                         `tasks/completed/`, TASK-036, and TASK-079 are
                         unmodified.
[x] Existing guard suites — the backend and client suites still pass
                         UNMODIFIED, proving no source file was touched.
```

### Key Edge Cases

- **A status statement expressed indirectly.** Some occurrences are parenthetical
  tree annotations rather than full sentences; a search for the exact phrase
  alone will miss them. Sweep the whole document, not one match.
- **The `StatusEffects[]` statement appears for both `PetState` and
  `BossState`** — correcting only one leaves the document self-contradictory.
- **`EquippedRelics[]` / `EquippedCards[]` are a different implementation
  vintage** (TASK-027/028/030) and are only in scope because the same wording
  pattern makes them false. If verification shows they are already correctly
  recorded, omit them rather than forcing an edit.
- **`DATABASE.md` §5 item 4 mixes a decided contract with a status claim.**
  Only the status clause changes; the mechanism, the row-permission rule, and
  the value-sourcing rule are contract text and must survive verbatim.
- **The same item already describes the `BossDefinition` migration as
  complete.** The correction should make the two provisioning statements
  consistent in form, not rewrite the `BossDefinition` sentence.
- **A sentence that also carries contract meaning** → STOP per `AGENTS.md` §4;
  do not edit it.
- **`GAME_STATE.md` §0 item 4's own use of "not yet implemented"** is the
  definition of the vocabulary, not a stale claim about any field — leave it.
- **Version metadata divergence** → follow whichever convention the two
  documents actually share; if they differ and neither establishes the rule,
  STOP and report rather than inventing one.

---

## Stop Conditions

Universal `AGENTS.md` §20 stops always apply. Task-specific:

- If the stale statements cannot be located in the current documents: STOP and
  report — do not manufacture a correction.
- If TASK-083 / TASK-085 / TASK-095 / TASK-096 do not actually support the
  claimed implementation state (e.g. the migration or the serializer member is
  absent from the working tree): STOP and report.
- If correcting the wording would require changing an authoritative gameplay or
  technical **contract** rather than an implementation-status statement: STOP
  per `AGENTS.md` §4 — a document conflict is not resolved by choosing the
  easier reading.
- If a correction would require a new architectural decision or an ADR: STOP —
  that is not a documentation-synchronization task.
- If the repository's task-generation workflow requires a different task type
  than `DOCUMENTATION`: STOP and report the classification conflict rather than
  proceeding.
- If the scope cannot remain documentation-only: STOP and report.
- If the two documents' version/changelog conventions conflict and neither
  establishes the governing form: STOP and report rather than inventing one.
- If the task appears to require touching `SIGNALR_PROTOCOL.md`,
  `REDIS_STATE.md`, `src/`, `tests/`, or a migration: STOP — out of scope.
- If the task expands toward a general documentation audit beyond the two
  enumerated defect areas: STOP and decompose (`tasks/README.md` §13).
- If satisfying any criterion requires modifying a completed task, TASK-036, or
  TASK-079: STOP — report instead.
- If the task exceeds 7 skills or crosses multiple uncoupled boundaries: STOP &
  decompose.

---

## Completion Evidence

<!--
  TO BE COMPLETED BY THE EXECUTING AGENT after reaching DONE.
  Keep concise and factual.
-->

**Picker-up note (pre-existing working-tree state).** At pickup, `git status`
already showed uncommitted changes from earlier tasks (TASK-086…TASK-096),
including `docs/02-technical/GAME_STATE.md` (Version 2.8, TASK-093) and
`docs/02-technical/DATABASE.md` (Version 1.20, TASK-089). Those were **not**
authored by TASK-097. The corrections below are layered on top of that
pre-existing state, and the baseline used for change-isolation was the
working-tree content at pickup (GAME_STATE.md `73E3165B…`, DATABASE.md
`CBBA36D7…`), not `HEAD`.

### Changed Files

- `docs/02-technical/GAME_STATE.md` — corrected every false implementation-status
  claim for the enumerated areas: the `PetState.StatusEffects[]` tree entry
  (was "Burn/Shield/Buff-Debuff instances … not yet implemented"), the
  `EquippedRelics[]` and `EquippedCards[]` tree annotations (both "not yet
  implemented"), the "implemented so far" prose paragraph, the "remaining
  collection member" prose paragraph, and the `BossState` counterpart —
  §2.4's tree entry and §2.4.1's `Deferred:` staging block (both now record the
  implemented position). No contract text restated; version header 2.8 → 2.9.
- `docs/02-technical/DATABASE.md` — corrected the §5 item 4 status clause that
  stated the Pet/Card/Relic provisioning implementation "remains an
  implementation detail (item 1) and is **not yet written** — it is the
  follow-up provisioning implementation task"; it now records the completed
  migration `20260929152651_ProvisionPetCardRelicContentDefinitions` (TASK-085)
  and the completed TASK-083 starter-ownership initialization, in the same form
  the item already uses for the completed `BossDefinition` migration. The
  mechanism, permitted-rows rule, and value-sourcing text are untouched. Version
  header 1.20 → 1.21.
- `tasks/active/TASK-097-…md` — lifecycle `Status:` field and this Completion
  Evidence section only.

### Validation Results

- Change isolation (GAME_STATE.md) — PASS. Pre-edit baseline reconstructed
  byte-for-byte from the working tree; `git diff --no-index` against it shows
  **exactly 6 hunks / 33 insertions / 18 deletions**, every line a
  status-wording change. No hunk falls inside §2.3.1, §2.3.2, §2.3.3, §5.1.1,
  or §0 items 4–5.
- Change isolation (DATABASE.md) — PASS. Baseline reconstruction hash matched
  the recorded pickup hash exactly (`CBBA36D7…`), and the diff shows **exactly
  2 hunks**: the version header and the §5 item 4 status clause. The two
  binding rules (a)/(b), the permitted-rows rule, the deferred-row citations,
  and the value-sourcing rule through "…never invented." are byte-identical.
- Protected-section byte comparison — PASS. §2.3.1 (104 lines), §2.3.2 (55),
  §2.3.3 (18), §5.1.1 (80), and §0 items 4–5 (9) each compare **identical**
  (`-ceq`) between baseline and current.
- Stale-wording sweep — PASS (0 residual false claims). GAME_STATE.md searched
  for `not yet implemented|not implemented|not yet written|deferred`, then
  cross-checked with `(StatusEffects|EquippedRelics|EquippedCards).{0,200}(not yet|not implemented|deferred)`
  → **zero matches**. DATABASE.md searched for
  `not yet written|not yet|pending|follow-up provisioning` → the stale claim is
  gone; the 2 residual hits (L464 "Bosses that are not yet content-defined";
  L523 the conditional "migration … not yet been applied, no row exists") are
  legitimate and unrelated.
- `dotnet test tests/backend/GameServer.Domain.Tests` — PASS, 1012/1012, 0 failed.
- `dotnet test tests/backend/GameServer.Application.Tests` — PASS, 377/377, 0 failed.
- `dotnet test tests/backend/GameServer.Infrastructure.Tests` — PASS, 297/297, 0 failed.
- `npx vitest run` (client) — PASS, 447/447 across 18 files, 0 failed.
- `dotnet test tests/backend/GameServer.Api.Tests` — **1 pre-existing
  environmental failure**, unrelated to this task and not caused by it:
  `BattleResultSmokeTest.SmokeTest_AuthoritativeBattleActionToResultRead_ShouldWalkTheWholeDocumentedPath`
  fails with `AggregateException : An error occurred while writing to logger(s).
  (Cannot open log for source '.NET Runtime'. You may not have write access.)`
  — inner exception `System.Diagnostics.EventLogInternal.OpenForWrite`. The
  throw comes from the Windows Event Log logging provider while EF Core emits
  a `FirstWithoutOrderByAndFilterWarning`, not from a test assertion. 253/254
  passed. Deterministic on re-run, and `git status` shows the Api test project
  unmodified. All suites were run **unmodified**; no test was edited.
- Guard checks — PASS (see Guard Verification).

### Contract Preservation Verification

```text
GAME_STATE.md §2.3.1 instance schema        UNCHANGED (104 lines, byte-identical)
GAME_STATE.md §2.3.2 serialization shape    UNCHANGED (55 lines, byte-identical)
GAME_STATE.md §2.3.3                        UNCHANGED (18 lines, byte-identical)
GAME_STATE.md §5.1.1 lifecycle              UNCHANGED (80 lines, byte-identical)
GAME_STATE.md §0 items 4–5                  UNCHANGED (9 lines, byte-identical)
DATABASE.md §1 entity blocks                UNCHANGED (no hunk in §1)
DATABASE.md §5 item 4 provisioning contract UNCHANGED (status clause only)
```

### Guard Verification

- [x] Zero files under `src/` modified — **by TASK-097**. (The working tree
      contains pre-existing uncommitted `src/` changes from TASK-086…TASK-096,
      present before pickup; TASK-097 added none. Proven by mtime: the
      `src/`, `tests/`, and `docs/01-game-design/` files were last written
      19:34–19:38, while the two TASK-097 documents were written 20:29.)
- [x] Zero files under `tests/` modified — by TASK-097 (same pre-existing-state
      note as above).
- [x] No migration added or modified — `git diff -- .../Migrations/` empty.
- [x] `tasks/completed/` unmodified — `git diff -- tasks/completed/` empty.
- [x] TASK-036 and TASK-079 unmodified — `git status --short -- tasks/blocked/` empty.
- [x] `SIGNALR_PROTOCOL.md`, `REDIS_STATE.md`, and `docs/01-game-design/`
      unmodified — **by TASK-097**. `git diff` for `SIGNALR_PROTOCOL.md` and
      `REDIS_STATE.md` is empty. `docs/01-game-design/*.md` do show uncommitted
      changes, but those predate pickup (mtime 19:34–19:38) and are outside
      this task's scope; TASK-097 wrote neither file.

### Stale Status Verification

The enumerated false implementation-status claims no longer remain:

```text
PetState.StatusEffects[]      "not yet implemented"  →  implemented (TASK-095/TASK-096)
BossState.StatusEffects[]     "Deferred / not yet"   →  implemented (TASK-095/TASK-096)
PetState.EquippedRelics[]     "not yet implemented"  →  implemented (TASK-027/TASK-030)
PetState.EquippedCards[]      "not yet implemented"  →  implemented (TASK-028/TASK-030)
Pet/Card/Relic provisioning   "not yet written"      →  complete (TASK-085, migration
                                                        20260929152651_…)
```

Verified against repository evidence, not against TASK-097's copied claims:
`StatusEffect.cs` / `StatusEffectType.cs` / `StatusEffectLifecycle.cs` exist;
`PetState.ActiveStatusEffects` and `BossState.ActiveStatusEffects` are declared
`StatusEffect[] = []`; `BattleStateJson` declares `statusEffects` on both
`PetStateJson` and `BossStateJson`; `BattleStateSerializer` projects both
directions; and
`src/backend/GameServer.Infrastructure/Postgres/Migrations/20260929152651_ProvisionPetCardRelicContentDefinitions.cs`
exists with `InsertData` for 3 Pets, 6 Cards, and 4 Relics.

### Unrelated Stale Documentation Discovered (REPORTED, NOT CHANGED)

Per `AGENTS.md` §16 and TASK-097 §"Scope", these are outside the two enumerated
defect areas and were deliberately left untouched:

1. `src/backend/GameServer.Domain/Battle/BattleState.cs` L61–66 — the XML doc
   comment still states `PetState`'s `StatusEffects` and `BossState`'s
   `StatusEffects[]` "are still absent and still owned by later stages
   (§2.0.5.3)". This is now false for `StatusEffects[]` in the same way the
   `GAME_STATE.md` claims were. **A source file — out of scope; modifying it is
   a STOP condition.** Suggested follow-up: a code-comment synchronization task.
2. `src/backend/GameServer.Domain/Battle/Serialization/BattleStateJson.cs`
   L415–418 — "whose stages have a documented 'not yet supplied' state to
   spell". This refers to the loadout collections' pre-supply DTO staging
   state, which is arguably still accurate and is a different claim from the
   implementation-status wording corrected here; no change recommended without
   a separate determination. **Source file — out of scope.**
3. `docs/02-technical/GAME_STATE.md` L880 — "These fields exist, and Redis
   persistence remains deferred." This is the `REDIS_STATE.md` §7 storage
   deferral, not a `StatusEffects[]` implementation claim; `REDIS_STATE.md` is
   a guard file and the topic is out of scope.

### Server Authority & Scope Verification

- [x] Confirmed zero client-authoritative gameplay logic (no `src/` file changed)
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
- [x] Confirmed no contract, field, value, lifecycle semantic, or schema changed
- [x] Confirmed no Redis, SignalR, or PostgreSQL behavior changed
