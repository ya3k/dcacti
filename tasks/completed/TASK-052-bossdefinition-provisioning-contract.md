# TASK-052 — Resolve BossDefinition Provisioning Contract

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ by path and section; does not copy rules or schemas.
-->

---

## Metadata

```text
Task ID:           TASK-052
Type:              DOCUMENTATION
Status:            DONE
Risk:              MEDIUM
Priority:          HIGH (P1 — closes TASK-051 decision A3's open question
                   and clears TASK-041's "BossDefinition provisioning task"
                   dependency at contract level)
Primary Agent:     review
Supporting Agents: persistence (BossDefinition persistence contract accuracy)
Workflow:          documentation/documentation-change.md
Skills:            discovery/documentation-discovery, discovery/impact-analysis,
                   quality/documentation-consistency, backend/persistence-analysis
Dependencies:      TASK-045 (DONE — BossDefinition persistence contract),
                   TASK-049 (DONE — BossDefinitionId PK contract),
                   TASK-051 (DONE — resolution mechanism + provisioning
                   prerequisite, decision A3)
Blocks:            Follow-up BossDefinition provisioning **implementation**
                   task (not created here — reported as Next Step), then
                   TASK-041 (read-only to this task; never edited)
```

**Status note.** Created directly in `tasks/active/` with `Status: IN
PROGRESS` on the human's directive to execute TASK-052 (that directive is
the READY validation: docs exist, scope confirmed, no unresolved
dependency — `tasks/TASK_LIFECYCLE.md` §3). During execution the human
supplied decision **P1** (provisioning mechanism) and instructed that
**P2–P8 be resolved independently from the authoritative documents**,
stopping (`BLOCKED`) only if an item is genuinely undefined and requires a
human architecture/deployment decision. All of P2–P8 were derivable (§5,
§13), so no STOP fired.

---

## 1. Objective

Resolve, by documentation only, the complete BossDefinition provisioning
contract (decision points **P1–P8**) so that a later implementation task can
create the three `BossDefinition` rows before TASK-041 writes a
`BattleResult` row:

- **P1 — provisioning mechanism** (human decision).
- **P2–P6 — row set, row content, idempotency/uniqueness, timing
  guarantee, missing-provisioning behavior** (required to be derived from
  the authoritative documents, not guessed).
- **P7–P8 — environment scope, documentation home / ADR question.**

Documentation-only. **No source, test, or migration changes.** The single
documentation edit targets the canonical owner only
(`docs/02-technical/DATABASE.md` §1 note item 5 + §5 item 4, per
`documentation-change.md` §1–§2).

---

## 2. Authoritative References

| # | Document | Why |
|---|---|---|
| 1 | `docs/02-technical/DATABASE.md` §1 `BossDefinition` contract note items 1–5; "Identity and reward sourcing for `BattleResult`" items 1–3; §3; §5 items 1/4; header | Canonical owner of the provisioning contract; owns determinism/idempotency requirement, FK prerequisite, fail-closed missing-definition behavior, uniqueness constraints |
| 2 | `docs/01-game-design/BOSS_RULES.md` §6, §6.1, §6.2, §6.3, §6.4 | Owns the three content-defined Bosses, Elements, Passive thresholds, Skill values, canonical Identities |
| 3 | `docs/01-game-design/PASSIVE_RULES.md` §4 | Reset Behavior variants; non-default must be explicitly documented — none is, so `Default` |
| 4 | `docs/01-game-design/ELEMENT_RULES.md` §1 | The five element tokens (content, not storage encoding) |
| 5 | `docs/00-overview/MVP_SCOPE.md` §1 | 5-Boss scope target vs 3 content-defined rows |
| 6 | `docs/03-decisions/ADR/ADR-006*` | PostgreSQL + EF Core persistence strategy — whether a migration-INSERT changes architecture (it does not) |
| 7 | `docs/02-technical/ARCHITECTURE.md` §5 (anti-overengineering) | Bounds P6 — no invented validation/gate infrastructure |
| 8 | `AGENTS.md` §2 (hierarchy), §7 (no invented rules), §9, §16 (report don't fix), §18 (ADR trigger), §20 (stop conditions) | Conflict/stop rules for P2–P8 |
| 9 | `tasks/TASK_TEMPLATE.md`, `tasks/TASK_LIFECYCLE.md`, `.ai/workflow/documentation/documentation-change.md` | This file's format, states, canonical-owner edit rule |
| 10 | `tasks/completed/TASK-045*`, `TASK-049*`, `TASK-051*` | Prior contracts (context; **do not reopen**). TASK-051 decision A3 created this task |
| 11 | `tests/backend/GameServer.Infrastructure.Tests/BossPersistenceTests.cs` | The guard the human requires preserved (lines 655–672) and the schema-only migration assertion (lines 277–285); **read-only** |
| 12 | `tasks/backlog/TASK-041-implement-battle-result-persistence.md` | The blocked consumer; **read-only** (`AGENTS.md` §16) |

---

## 3. Scope

### In Scope

1. Resolve P1–P8, each ending in either (a) a human-supplied answer,
   (b) a derivation from the Authoritative References with the chain shown,
   or (c) a `BLOCKED` report naming the exact undefined question.
2. Update the **canonical owning document only** for the decided contract
   (`DATABASE.md` §1 note item 5, §5 item 4; header version line).
3. Record precisely what the follow-up implementation task and TASK-041
   need, as report items — **without editing TASK-041 or creating the
   implementation task** (creation only if the human requests it).
4. Report discovered adjacent issues without fixing them (`AGENTS.md` §16).

### Out of Scope

- Any `src/`, `tests/`, or migration file change — no `HasData`, no seed,
  no startup loader, no migration authored here, no `BossDefinition`
  entity/configuration change, no test comment edits.
- Implementing the provisioning (the follow-up task owns it).
- Editing `TASK-041`, `TASK-045`, `TASK-049`, `TASK-051`, or any
  `tasks/completed/` file (`AGENTS.md` §16; completed tasks immutable).
- Provisioning for any other content table (`PetDefinition`,
  `CardDefinition`, `RelicDefinition`) — remains open (TASK-045 §6 issue 1).
- Authoring an ADR (P8 determines none is required).
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2.

---

## 4. Current State — Evidence (verified at execution)

### 4.1 No provisioning mechanism exists anywhere

- `src/backend` contains no `HasData`, `EnsureCreated`, or
  `Database.Migrate()` (only a comment on the `dotnet ef` equivalence in
  `GameDbContext.cs`); no migration contains `InsertData` or
  `migrationBuilder.Sql` (`20260926124429_AddBossPersistence.cs` and
  predecessors are schema-only).
- `DATABASE.md` §5 item 4 (pre-edit) stated "no provisioning *mechanism*
  is defined by this document … the mechanism itself remains that task's
  to define"; §1 note item 5 left the mechanism open (TASK-051 decision
  A3: "a provisioning task precedes TASK-041").
- No `BossDefinition` row exists in any environment; the
  `BattleResult.BossDefinitionId` FK therefore cannot be satisfied.

### 4.2 Existing guards that bound any answer

- `BossPersistenceTests.BossDefinition_ShouldNeverBeProvisionedByTheModel`
  (line 655): fresh context returns zero `BossDefinition` rows and
  `GetSeedData()` is empty. A migration-INSERT mechanism satisfies both
  assertions (migrations are not model seed data; the InMemory test
  context never applies migrations). **Must keep passing.**
- `BossPersistenceTests` line 277: the **`AddBossPersistence` migration
  itself must stay schema-only** — the test asserts it contains no
  `InsertData`, no `HasData`, and none of the boss IDs. Therefore the
  provisioning INSERT must be a **new, separate migration**, not an edit
  to the existing one.

### 4.3 Migration-execution precedent

`dotnet ef database update` is the project's established apply path
(TASK-023 / TASK-027 / TASK-028 migration notes); `BossPersistenceTests`
reads migration source directly, so migrations are first-class artifacts
of this repository.

---

## 5. Decision Points (the deliverable)

Each point below was resolved either by the human's explicit answer or by
derivation from §2, with the chain shown. **No point required a STOP**: if
a derivation had failed, the task would have gone `BLOCKED` per
`AGENTS.md` §7/§20 instead of guessing.

### P1 — Provisioning mechanism (HUMAN ANSWER)

**Question.** Which mechanism creates the three `BossDefinition` rows?

**Human answer (2026-09-26).** An **EF Core migration that INSERTs rows**
for exactly `boss-def-hoa-long`, `boss-def-thuy-ma`, `boss-def-moc-yeu`,
applied via the existing `dotnet ef database update` workflow. Explicitly
forbidden: `HasData`, a startup loader/upsert, a separate manual-SQL
deployment path, any runtime provisioning infrastructure. The
`BossDefinition_ShouldNeverBeProvisionedByTheModel` guard and its
`GetSeedData()` expectation must be preserved. The migration itself is
**not** implemented by TASK-052.

**Written to:** `DATABASE.md` §1 note item 5 sub-bullet "Mechanism —
decided"; §5 item 4; header version line.

### P2 — Provisioned row set (DERIVED)

**Question.** Which rows exist after provisioning?

**Derivation.**
1. `DATABASE.md` §1 note item 5: "Only content-defined Bosses (currently
   3 — `BOSS_RULES.md` §6) may ever be provisioned … not permission to
   create placeholder rows for undefined content."
2. `DATABASE.md` §1 note item 2 canonical values: `boss-def-hoa-long`,
   `boss-def-thuy-ma`, `boss-def-moc-yeu`.
3. `BOSS_RULES.md` §6 defines exactly those three Bosses.

**Resolution:** exactly three rows — the three canonical
`BossDefinitionId` values, each with its §6.4 `Identity`
(`boss-hoa-long`, `boss-thuy-ma`, `boss-moc-yeu`). No fourth/placeholder
row; no rows for the two not-yet-content-defined MVP Bosses.

**Ambiguity check:** the MVP scope target is 5 Bosses (`MVP_SCOPE.md` §1)
— already reconciled by note item 5's content-defined-only rule; not a
conflict (`AGENTS.md` §4 not triggered).

### P3 — Row content (DERIVED)

**Question.** What does each row contain — `BossDefinitionId`,
`Identity`, `Element`, `PassiveDefinition`, `SkillDefinition`?

**Derivation (per column).**

| Column | Owner | Rule |
|---|---|---|
| `BossDefinitionId` | `DATABASE.md` §1 note item 2 | Canonical `boss-def-*` values; transcribed, never generated |
| `Identity` | `BOSS_RULES.md` §6.4 | Canonical technical IDs; never display names |
| `Element` | `BOSS_RULES.md` §6 | Hỏa Long = Hỏa, Thủy Ma = Thủy, Mộc Yêu = Mộc (tokens per `ELEMENT_RULES.md` §1; storage encoding is an implementation detail) |
| `PassiveDefinition.passiveId`, `.threshold` | `BOSS_RULES.md` §6.2/§6.4 | Threshold values including `null` for Thủy Ma's always-active Passive (`DATABASE.md` §3: `threshold = null` ⇔ always-active; never a `0` sentinel) |
| `PassiveDefinition.resetBehavior` | `DATABASE.md` §1 note item 3 default | `Default` — "the documented default when a rule states no override"; no Boss Passive documents one (`PASSIVE_RULES.md` §4 item 3: non-default must be explicitly documented) |
| `SkillDefinition.skillId`, `.baseDamage`, `.chargeRequirement`, `.cooldownTurns` | `BOSS_RULES.md` §6.3/§6.4 | Skill identities and values transcribed |

Combat stats (`BOSS_RULES.md` §6.1: MaxHP/ATK/DEF/EnrageThreshold) and
display names are **not columns** (note item 1) and are never written by
provisioning. JSON member names follow note items 3–4 (storage names are
the contract). No numeric values are duplicated into `DATABASE.md` — the
authoritative documents remain the only source for them (no second source
to drift).

**Ambiguity check:** none — every field traces to one owner; `0`-vs-`null`
and reset-token questions are already settled by §1 note items 3–4/§3.

### P4 — Idempotency and uniqueness (DERIVED)

**Question.** What does provisioning guarantee when applied more than once?

**Derivation.**
1. `DATABASE.md` §1 note item 5: "provisioning must be deterministic, must
   be idempotent, and must never depend on a player's runtime battle."
2. The chosen mechanism (P1) is tracked in EF migration history, so a
   second `dotnet ef database update` performs no work — the
   apply-once property of the decided mechanism.
3. `DATABASE.md` §3: `BossDefinitionId` NOT NULL/UNIQUE (PK) and
   `Identity` NOT NULL/UNIQUE — the database-level duplicate guarantee.

**Resolution:** each row is inserted at most once; re-application yields
the identical end state (exactly three rows, unchanged content);
`BossPersistenceTests` line 277 additionally pins the existing schema
migration as schema-only, so the INSERT belongs to a **new** migration;
provisioning never updates, overwrites, or deletes an existing row.

### P5 — Timing / availability guarantee (DERIVED)

**Question.** When must rows exist?

**Derivation.**
1. `DATABASE.md` §1 note item 5 (TASK-051 A3): `BattleResult.BossDefinitionId`
   is an FK — "the referenced **row must already exist** at insert time";
   a provisioning task is "a prerequisite for any task that writes a
   `BattleResult` row."
2. P1 mechanism is applied per environment via `dotnet ef database
   update`, so "exists" is per-database.

**Resolution:** in every environment, the migration is applied **before
that environment's first battle-end write** — rows exist before any
`BattleResult` insert can attempt the FK; until applied, the FK cannot be
satisfied and the fail-closed path (§1 sourcing item 3) governs.

### P6 — Missing-provisioning behavior (DERIVED)

**Question.** What happens if rows are absent (distinct from TASK-051's
A4)?

**Derivation.**
1. `DATABASE.md` §1 "Identity and reward sourcing for `BattleResult`"
   item 3 (TASK-051 decision A4) already defines the behavior for an
   unresolved `BossDefinition`: no `BattleResult` row, no
   `battle:{battleId}:state` delete, battle recoverable and retryable
   once a row exists, documented `404` until then, no new error code or
   wire contract.
2. `ARCHITECTURE.md` §5 / `AGENTS.md` §9: no undocumented validation
   layer may be invented (no startup check, no pre-battle gate, no retry
   worker — item 3 explicitly declines queues/retry policy).

**Resolution:** absent provisioning surfaces **only** as that existing
fail-closed behavior; this contract introduces no new failure mode and no
validation infrastructure. The distinction from A4: A4 defined *battle-end
behavior*; P6 binds *provisioning's* failure mode to it and forbids
additional gates.

**Ambiguity check:** the temptation would be to invent a startup/health
validation — explicitly rejected as undocumented (anti-overengineering),
not chosen as an open design space.

### P7 — Environment scope (DERIVED)

**Question.** In which environments must rows be provisioned?

**Derivation.** The FK prerequisite (P5) quantifies over *every* write of
`BattleResult`, and migrations apply per database (`dotnet ef database
update`, P1). No document enumerates environments, and none needs to: the
rule is universal by construction — any environment in which a battle can
be started and ended is battle-capable and is covered.

**Resolution:** **every battle-capable environment**, the migration
applied before that environment's first battle-end write. No environment
is exempt; no environment list is introduced into the documentation
(deployment topology is not a documented concern of this contract).

**Ambiguity check:** had the docs *restricted* scope somewhere (e.g.
"production only"), this would have been a genuine conflict requiring a
human — no such text exists.

### P8 — Documentation home / ADR (DERIVED)

**Question.** Which document canonically owns this contract, and does the
decision require an ADR?

**Derivation.**
1. `AGENTS.md` §2 hierarchy: persistent-data contracts live in
   `docs/02-technical/`; `DATABASE.md` is the entity that already owns the
   `BossDefinition` persistence/provisioning notes (§1 note item 5) and
   the §5 mechanism statement — the open questions were literally phrased
   in those two places (TASK-045 Decision E, TASK-051 A3).
2. `documentation-change.md`: edit the canonical owner only; do not
   duplicate into other documents.
3. `AGENTS.md` §18: an ADR is required only for a change of architecture.
   ADR-006 already decides PostgreSQL + EF Core; a data migration applied
   by the documented EF workflow changes no architectural decision.

**Resolution:** canonical owner = `docs/02-technical/DATABASE.md` (§1 note
item 5, §5 item 4, header); **no ADR** — §18 not triggered. Dependent
documents were checked for staleness (§13) rather than edited.

---

## 6. Preserved Decisions (must remain true after this task)

- TASK-051 A1/A2/A4, B1–B3 (resolution mechanism, resolver owner,
  fail-closed missing definition, endpoint authorization) — untouched.
- TASK-049 `BossDefinitionId` value model (independent content-supplied
  PK) — untouched; item 5's rewrite keeps "adds no seed of its own".
- TASK-046/047/048 canonical Identities and the three-way non-collapse of
  PK / Identity / display name.
- `GAME_STATE.md` §2.4 — no second identity field on `BossState`.
- `DATABASE.md` §1 note item 1 — combat stats are not columns.
- Only content-defined rows may ever be provisioned (5 ≠ permission).
- `BossPersistenceTests` guard (fresh context empty + `GetSeedData()`
  empty) and the `AddBossPersistence` schema-only assertion — both must
  keep passing, unchanged.

---

## 7. Acceptance Criteria

- [x] P1 recorded from the human answer exactly (mechanism + prohibitions
      + guard preservation + "not implemented here").
- [x] P2–P6 each resolved with a citation chain to §2 documents; no value
      or rule invented (`AGENTS.md` §7).
- [x] P7/P8 resolved or BLOCKED — resolved by derivation (§5); if the
      human intended different answers, the report flags this.
- [x] Single canonical edit: `DATABASE.md` (§1 note item 5, §5 item 4,
      header 1.11 → 1.12); no other `docs/` file edited.
- [x] Dependent documents checked for staleness; only stale wording found
      and fixed inside `DATABASE.md` itself (note item 2 pointer).
- [x] TASK-041 byte-identical, still `BACKLOG`; no `tasks/completed/`
      file touched; no `src/`, `tests/`, or migration change.
- [x] Existing guard tests still pass (run, §15).
- [x] Follow-up implementation task and adjacent issues reported, not
      implemented (`AGENTS.md` §16).

---

## 8. Affected Files & Areas

```text
[x] docs/ (02-technical/DATABASE.md only — canonical owner)
[x] tasks/ (this file)
[ ] src/backend/          — untouched
[ ] src/frontend/client/  — untouched
[ ] tests/                — untouched
[ ] migrations            — untouched
```

---

## 9. Implementation Notes

For the **follow-up implementation task** (not created here):

1. Add a **new** EF Core migration whose `Up` contains `InsertData` for
   exactly the three rows; do **not** edit
   `20260926124429_AddBossPersistence.cs` (pinned schema-only by
   `BossPersistenceTests` line 277: no `InsertData`, no boss IDs).
2. Column values per `DATABASE.md` §1 note item 5 "Row content"
   (transcribed from `BOSS_RULES.md` §6/§6.2/§6.3/§6.4; `Element` maps to
   the Domain enum representation used by
   `BossDefinitionConfiguration`'s `HasConversion<int>()`).
3. Apply via `dotnet ef database update`; verify exactly three rows,
   correct JSON member names per §1 note items 3–4.
4. `BossPersistenceTests.BossDefinition_ShouldNeverBeProvisionedByTheModel`
   must still pass with zero test edits (`GetSeedData()` stays empty —
   migrations are not model seed data).
5. Comment text at `BossPersistenceTests.cs` lines 21/277/658–660 says
   "no provisioning mechanism is documented" — now stale (report-only in
   §13; fix only as part of that task, never in this one).

---

## 10. Testing Requirements

### Required Verification

```text
[ ] Unit/integration tests — none added (documentation-only task);
    run the affected existing suite to prove preservation:
    `dotnet test tests/backend/GameServer.Infrastructure.Tests
     --filter BossPersistenceTests`
[ ] Guard preservation — BossDefinition_ShouldNeverBeProvisionedByTheModel
    passes unmodified (fresh context empty, GetSeedData() empty)
[ ] Schema-only pin — AddBossPersistence migration assertions still pass
```

### Key Edge Cases

- See `DATABASE.md` §1 note item 5 — `null` threshold for Thủy Ma must
  survive any future provisioning implementation (never `0`).

---

## 11. Stop Conditions

- Universal: `AGENTS.md` §20 (rule conflict, missing rule, architecture
  conflict, scope violation, ambiguity, contract conflict, destructive
  change).
- Task-specific: if any of P2–P8 had not been derivable from §2 and no
  human answer existed → `BLOCKED — HUMAN DECISION REQUIRED` per
  `tasks/TASK_LIFECYCLE.md` §3, with the exact question written into this
  file's Stop Conditions section. **No stop fired** (§5, §13).
- If the human's P1 had contradicted an authoritative document → §4
  conflict protocol (it does not: migration INSERT is permitted by
  `DATABASE.md` §5 item 1 — migrations are implementation detail — and
  ADR-006).

---

## 12. Completion Evidence

**Changed files (this task's own edits only):**

```text
[x] docs/02-technical/DATABASE.md      Version 1.11 -> 1.12; four edits:
      1) header version/history line (P1 mechanism, P7 scope, P8 ownership)
      2) §1 note item 2 "Source / ownership" tail — the sentence that
         read as a global "no migration-inserted production row"
         prohibition was scoped to "this key contract introduces no seed
         of its own" and now points to the decided contract (note item
         5 / §5 item 4)
      3) §1 note item 5 — open-question text ("No provisioning mechanism
         is documented, and none may be invented") replaced by the six
         contract sub-bullets (Mechanism / Row set / Row content /
         Idempotency and uniqueness / Availability guarantee / Missing
         provisioning fails closed); TASK-051 prerequisite bullet
         retitled "That decision is now made above (TASK-052)"
      4) §5 item 4 — "no provisioning mechanism is defined" replaced:
         other content tables remain open; BossDefinition's mechanism is
         defined here; the migration script stays an implementation
         detail; implementation is a follow-up task preceding any
         BattleResult write
[x] tasks/active/TASK-052-bossdefinition-provisioning-contract.md (this file)
```

**Not changed (verified):** `src/**`, `tests/**`, `**/Migrations/**`,
`tasks/backlog/**` (TASK-041 unchanged), `tasks/completed/**`, and every
other `docs/` file. Pre-existing uncommitted working-tree changes from
the TASK-050/TASK-051 sessions (`API_CONTRACTS.md`, `GAME_EVENTS.md`,
`SIGNALR_PROTOCOL.md`, untracked TASK-050/051 task files) were left
untouched.

---

## 13. Decisions & Derivations — 2026-09-26

**Provenance:** P1 = human answer. P2–P8 = derived from the Authoritative
References (chains in §5) under the human's instruction to resolve
independently and stop only if genuinely undefined. **No STOP condition
fired**; `AGENTS.md` §4 (conflict) was not triggered anywhere.

```text
P1 mechanism:      HUMAN  — EF Core migration INSERT of exactly the three
                            boss-def-* rows; applied via the existing
                            `dotnet ef database update` workflow.
                            Forbidden: HasData, startup loader/upsert,
                            separate manual-SQL deployment path, runtime
                            provisioning infrastructure. Preserve
                            BossDefinition_ShouldNeverBeProvisionedByTheModel
                            and its GetSeedData() expectation. Migration NOT
                            implemented by TASK-052.
P2 row set:        DERIVED — exactly 3 rows (§1 note item 5 content-defined
                            rule + §1 note item 2 canonical values +
                            BOSS_RULES §6); no placeholders.
P3 row content:    DERIVED — 5 columns; per-column owners (BOSS_RULES
                            §6.2/§6.3/§6.4 for values, §1 note items 3–4 for
                            JSON member lists, resetBehavior = Default per
                            note item 3 default + PASSIVE_RULES §4);
                            combat stats not columns; no values duplicated.
P4 idempotency:    DERIVED — deterministic/idempotent (note item 5) via
                            EF migration history (apply-once) + §3 PK and
                            Identity UNIQUE; INSERT in a NEW migration
                            (AddBossPersistence stays schema-only);
                            existing rows never overwritten.
P5 timing:         DERIVED — rows exist before the environment's first
                            BattleResult write (note item 5 / TASK-051 A3:
                            FK requires the row at insert time).
P6 missing rows:   DERIVED — only the documented fail-closed path (§1
                            sourcing item 3); no startup validation,
                            pre-battle gate, health check, or retry worker
                            invented (ARCHITECTURE §5 / AGENTS §9).
P7 environment:    DERIVED — every battle-capable environment (universal
                            quantification of P5 over P1's per-database
                            apply); no environment list introduced.
P8 documentation:  DERIVED — canonical owner DATABASE.md (§2 hierarchy +
                            documentation-change.md); no ADR (AGENTS §18
                            not triggered — inside ADR-006's strategy).
```

**Where written (canonical owner only):**

| Decision | Document | Location |
|---|---|---|
| P1, P4, P5, P6 | `docs/02-technical/DATABASE.md` | §1 note item 5 — rewritten: "Mechanism — decided", "Row set", "Row content", "Idempotency and uniqueness", "Availability guarantee", "Missing provisioning…", TASK-051 prerequisite bullet updated |
| P7 | `docs/02-technical/DATABASE.md` | §1 note item 5 sub-bullet "Availability guarantee (ordering, every battle-capable environment)"; §5 item 4 |
| P8 | `docs/02-technical/DATABASE.md` | Canonical home (this edit); header version line records no-ADR |
| P2, P3 | `docs/02-technical/DATABASE.md` | §1 note item 5 sub-bullets "Row set", "Row content" (sourcing only) |
| all | `docs/02-technical/DATABASE.md` | Header `Version:` 1.11 → 1.12 with history; §5 item 4 rewritten; §1 note item 2 pointer clarified (was reading as a global prohibition) |

**Dependent documents verified and NOT edited:**

- `ARCHITECTURE.md` §3/§5 — `PersistenceRepository (Postgres)` and
  anti-overengineering already constrain the answer as written. No edit.
- `GAME_STATE.md` §2.4, `REDIS_STATE.md` — battle state untouched. No edit.
- `BOSS_RULES.md` §6 — values remain owned there and are referenced, not
  copied. No edit.
- `ADR-006` — strategy unchanged (PostgreSQL + EF Core); no ADR-015
  needed. No edit.
- `API_CONTRACTS.md` §4 — 404 behavior already documented. No edit.
- `tasks/backlog/TASK-041*` — verified: contains **no** BossDefinition
  provisioning text to go stale (only TASK-034 session reference at
  line 93); left byte-identical.
- `tasks/completed/TASK-045/049/051` — immutable; their "mechanism
  undecided" statements are historical records of those tasks' states.

**Report-only adjacent issues (not fixed here, `AGENTS.md` §16):**

1. **Stale test comments.** `BossPersistenceTests.cs` lines 21, 277, and
   658–660 claim "no provisioning mechanism is documented / no provisioning
   of any kind". Assertions remain correct; comments become inaccurate
   once P1 is implemented. Fix belongs to the implementation task.
2. **Duplicate item number in `DATABASE.md`.** "Identity and reward
   sourcing for `BattleResult`" has two items numbered 3 (fail-closed
   behavior; `RewardSummary` owner) — pre-existing numbering defect,
   untouched to avoid citation churn (TASK-051 §16 cites "item 3" for the
   fail-closed text).
3. **`BossPersistenceTests` line 280** forbids the literal
   `"black-hoa-long"` in the schema migration — appears to be a typo/test
   artifact; no impact, noted for the implementation task's awareness.
4. **TASK-041 has no dependency checklist of its own** recording the
   provisioning prerequisite (it lives in TASK-051 §17). TASK-041 is
   read-only here; the requirement is restated in §14 below.

---

## 14. TASK-041 Impact (read-only audit)

**TASK-041 was not edited** (`AGENTS.md` §16) and remains `Status: BACKLOG`.

```text
DEPENDENCY 1 — BossDefinition provisioning
  BEFORE TASK-052: mechanism UNDECIDED (DATABASE.md §5 item 4); no task,
                   no rows.
  AFTER TASK-052:  CONTRACT RESOLVED — mechanism, row set, content,
                   idempotency, ordering, failure behavior all documented
                   (DATABASE.md §1 note item 5, §5 item 4).
  STILL REQUIRED:  the follow-up implementation task must add and apply
                   the INSERT migration so the three rows EXIST before
                   TASK-041's first BattleResult write. Contract ≠ rows.

DEPENDENCY 2 — TASK-034 (session mechanism): Status BLOCKED — unchanged,
  untouched by this task (TASK-051 decision B1 = prerequisite).

TASK-041 READINESS: NOT READY — remains Status: BACKLOG (both dependencies
  unsatisfied at the required level; per TASK-051's standing instruction,
  no READY transition while any dependency is open).
```

**What TASK-041's author can now rely on (canonical):**

```text
[ok] What rows exist and how      — DATABASE.md §1 note item 5 (P1/P2/P3)
[ok] When they must exist         — before first BattleResult write (P5/P7)
[ok] Missing rows behavior        — §1 sourcing item 3 fail-closed (P6)
[ok] Duplicate-safety             — §3 PK + Identity UNIQUE (P4)
[ ] Rows actually present         — follow-up implementation task's job
[ ] TASK-034 resolved             — separate BLOCKED dependency
```

---

## 15. Validation Results

Run 2026-09-26 on completion:

```text
[1] dotnet test tests/backend/GameServer.Infrastructure.Tests
    --filter BossPersistenceTests
    -> Passed! Failed: 0, Passed: 21, Skipped: 0 (715 ms)
       includes BossDefinition_ShouldNeverBeProvisionedByTheModel
       (fresh context empty + GetSeedData() empty) and the
       AddBossPersistence schema-only pin — both preserved, zero test
       edits (only NuGet/MSBuild pre-existing warnings, no failures).

[2] git status --porcelain -- tasks/backlog tests src
    -> empty: TASK-041, tests/, src/, and migrations all unchanged.

[3] git diff --name-only
    -> docs/02-technical/{API_CONTRACTS,DATABASE,GAME_EVENTS,
       SIGNALR_PROTOCOL}.md — DATABASE.md reviewed hunk-by-hunk: the
       TASK-052 hunks are header, §1 item 2 pointer, §1 item 5, §5 item
       4; the other three files' changes are pre-existing uncommitted
       TASK-050/051 work, untouched by this task.

[4] Structure check — indentation of every edited region measured
    against the file's own convention (indents 0/3/5): header lines all
    0; §1 note item 5 paragraphs 0, sub-bullets 3, continuations 5;
    §5 item 4 body 3; §1 item 2 continuation block re-aligned to its
    sibling lines (5). Scripted verification passed — no stray indent.

[5] Staleness scan of dependent documents (ARCHITECTURE, GAME_STATE,
    REDIS_STATE, BOSS_RULES, ADR-006, API_CONTRACTS, GAME_EVENTS,
    SIGNALR_PROTOCOL) — none needed an edit; the only stale wording
    found lives in test comments (reported, §13).
```

---

## Revision History

| Version | Date | Change |
| --- | --- | --- |
| 1.1 | 2026-09-26 | Executed: P1 human answer recorded (§13); P2–P8 derived with citation chains (§5); canonical edit applied — `DATABASE.md` v1.11 → v1.12 (§1 note item 5 rewritten with the six contract sub-bullets, §1 note item 2 pointer clarified, §5 item 4 rewritten, header updated); dependent documents verified not stale; guard tests run (§Completion Evidence); TASK-041 read-only audit (§14): contract resolved, implementation dependency remains → TASK-041 stays BACKLOG. Status → DONE, file moved `active/` → `completed/`. |
| 1.0 | 2026-09-26 | Created (IN PROGRESS, `tasks/active/`) on human directive; P1–P8 defined as the deliverable (§5); evidence of the absent mechanism and the two test guards captured (§4). |
