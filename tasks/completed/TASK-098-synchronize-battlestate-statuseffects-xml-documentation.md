# TASK-098 — Synchronize `BattleState` XML Documentation with the Implemented `StatusEffects[]` State

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ by path and section — it does NOT copy game rules,
  formulas, schemas, or contracts.

  THIS TASK AUTHORS NO CONTRACT AND IMPLEMENTS NO BEHAVIOR. It corrects
  source-embedded documentation (XML doc comments) that completed
  implementation work has made false. Every contract it touches stays
  byte-identical in meaning.

  BOUNDARY: source-file COMMENT wording only. The edited file is a .cs file,
  but not one line of executable code, declaration, or member changes. No
  runtime behavior, no contract, no value.

  PROVENANCE: this defect was discovered and reported by TASK-097
  §"Unrelated Stale Documentation Discovered" item 1, and deliberately NOT
  fixed there because TASK-097 was bound to a documentation-only scope
  (AGENTS.md §16 — report, do not fix inline).
-->

---

## Metadata

```text
Task ID:           TASK-098
Type:              REFACTOR (TASK_TYPES.md §2 — "restructure existing code
                   without any intended behavior change"; development/
                   refactor.md §1 — behavior preserved unless a behavior
                   change is explicitly part of the task, and none is).
                   See "Type classification note" below — this is NOT a
                   DOCUMENTATION task, because its primary output is a
                   source file, not docs/.
Status:            DONE
Risk:              LOW (TASK_TYPES.md §4 — REFACTOR baseline LOW—MEDIUM; LOW
                   because the change is confined to XML doc comment text on
                   one type, alters no executable statement, and preserves
                   every contract. It is not the MEDIUM case: it crosses no
                   contract boundary — it removes a now-false statement about
                   work that is already complete.)
Priority:          LOW—MEDIUM (the false statement misleads any agent reading
                   BattleState.cs into believing StatusEffects[] is still
                   absent, and would cause a duplicate implementation task to
                   be created. It is not HIGH: no runtime defect exists and
                   nothing is broken at execution time.)
Primary Agent:     backend (owns the Application/Api layers, but here acts as
                   the .cs source-file editor; the comment documents the
                   Domain BattleState type)
Supporting Agents: review (documentation-consistency verification),
                   testing (existing suites must stay green, unmodified)
Workflow:          development/refactor.md
Skills:            discovery/impact-analysis,
                   quality/documentation-consistency,
                   quality/scope-validation
                   (3 skills — Simple budget, tasks/README.md §12)
Dependencies:      TASK-095 (DONE — StatusEffect Domain state and the §5.1.1
                     step-19a lifecycle implemented),
                   TASK-096 (DONE — StatusEffects JSON serialization and
                     round trip implemented),
                   TASK-097 (DONE — corrected the GAME_STATE.md / DATABASE.md
                     status wording and REPORTED this remaining source
                     comment; TASK-097 is NOT modified by this task)
Blocks:            None. This task unblocks no implementation; it removes a
                   source-documentation defect that would otherwise cause a
                   duplicate implementation task to be created.
Estimate:          Simple (one XML doc comment block in one source file;
                   no new prose section, no code, no test)
```

**Type classification note.** `REFACTOR`, not `DOCUMENTATION`. `TASK_TYPES.md` §2
defines `DOCUMENTATION` as *"Change `docs/` content — documentation is the
primary output, not code"*, and states explicitly: *"Code changes that also
update docs as a side effect do not use this type — they use the appropriate
code-change type and include the doc update in the same task."* This task's
edited artifact is `src/backend/GameServer.Domain/Battle/BattleState.cs` — a
source file, **not** a document under `docs/` — so the `DOCUMENTATION` type and
`documentation/documentation-change.md` do **not** govern it, and the TASK-097
precedent (which edited `docs/` only) does not transfer.

It is not `BUG` either. `TASK_TYPES.md` §2's BUG definition does include
"documentation bugs (docs are stale)", but its workflow
(`development/bug-fix.md` §2) requires a regression test that fails before the
fix, and §4 hard-rules that behavior is never changed to satisfy a test. There is
no executable behavior to fix and no test that can fail on a comment — so the
BUG workflow cannot be honestly discharged here.

`REFACTOR` is the correct governing type: `refactor.md` §1 requires behavior to
be preserved unless a behavior change is explicitly part of the task, and §2
lists exactly the contract classes this task must not move (public contracts,
domain behavior, events, state transitions, persistence behavior, realtime
behavior). This task preserves all of them by construction — the change is
comment text only — which is precisely the §2 preservation check the workflow
demands.

**This task is not a general source-comment audit.** It is scoped to one
verified, enumerated defect in one file. Anything else that also looks stale —
including the second item TASK-097 reported in `BattleStateJson.cs` L415–418 — is
out of scope, even if it looks wrong (`AGENTS.md` §16 — report, do not fix
inline).

---

## Objective

Correct the stale XML documentation on `BattleState` in
`src/backend/GameServer.Domain/Battle/BattleState.cs` (around lines 61—66) that
still declares `PetState`'s `StatusEffects` and `BossState`'s `StatusEffects[]`
to be "still absent and still owned by later stages" (`§2.0.5.3`), so the comment
accurately describes the state that TASK-095 and TASK-096 have already
implemented — while changing no executable code, no member, no serialized shape,
and no contract, and while preserving the genuinely-still-staged part of the same
sentence (`Tier`/`Star`/`Level`).

---

## Authoritative References

- `docs/02-technical/GAME_STATE.md` §2.3.1 — the `StatusEffects[]` **instance
  schema**: the authoritative element shape on both entities (the contract being
  *described*, not changed)
- `docs/02-technical/GAME_STATE.md` §2.3.2 — the **JSON serialization and
  round-trip** contract, including item 1 (member name `statusEffects`, empty
  array when no effect is active, never omitted, never `null`) and item 7 (no
  Redis-only field)
- `docs/02-technical/GAME_STATE.md` §2.3.3 — what the model does **not** add
- `docs/02-technical/GAME_STATE.md` §2.4.1 — the `BossState` staged-field table;
  it now records `StatusEffects[]` as **contract-defined by §2.3.1 and §5.1.1;
  implemented (TASK-095/TASK-096)**, and states the collection shares
  `PetState`'s "identical element schema, identical lifecycle"
- `docs/02-technical/GAME_STATE.md` §5.1.1 — the **apply / refresh / consume /
  expire** lifecycle (items 1—12) the implemented state carries
- `docs/02-technical/GAME_STATE.md` §2.3, §2.4 — the `PetState` / `BossState`
  trees; §2.3's tree entry for `StatusEffects[]` now records **implemented,
  TASK-095/TASK-096**. This is the vocabulary the replacement wording must
  match, and the §2 tree is the contract `BattleState`'s own tree comment
  mirrors.
- `docs/02-technical/GAME_STATE.md` §0 item 4 — the "not yet implemented / not
  required" staging vocabulary this comment is written in; the item itself is a
  contract statement and is **not** edited (this task edits no `docs/` file)
- `docs/02-technical/GAME_STATE.md` §0 item 5 — the no-parallel-representation
  rule; the corrected comment introduces none
- `docs/02-technical/GAME_STATE.md` §2.0.5.3 — **the staging section the stale
  sentence cites**. It remains the correct home for the fields that *are* still
  deferred; the correction must not delete the citation, only stop applying it
  to `StatusEffects[]`
- `docs/02-technical/REDIS_STATE.md` §2 item 1, §7 item 9 — the round-trip
  obligation the implemented collection discharges; referenced for factual
  context only
- `docs/03-decisions/ADR/ADR-005-redis-active-battle-state.md` — Redis is the
  active battle state store; unchanged by this task
- `docs/03-decisions/ADR/ADR-001-server-authoritative-battle.md` — server
  authority; unchanged by this task
- `tasks/completed/TASK-095-implement-statuseffect-domain-state-and-step-19a-lifecycle.md`
  — implemented the Domain `StatusEffect` model, `PetState.ActiveStatusEffects`,
  `BossState.ActiveStatusEffects`, and the §5.1.1 lifecycle (read-only; **not
  modified**). Its §"Current State" L149 records this exact comment as stale at
  that time and its Scope item 7 anticipated updating it.
- `tasks/completed/TASK-096-serialize-statuseffects-round-trip.md` — implemented
  the `statusEffects` serializer member and the round trip (read-only; **not
  modified**)
- `tasks/completed/TASK-097-synchronize-statuseffect-and-provisioning-implementation-status.md`
  — corrected the `docs/` implementation-status wording and reported this
  remaining source comment, deferring it as out of its own scope (read-only;
  **not modified**)
- `AGENTS.md` §17 — the documentation-change rule this task follows: the comment
  is outdated **relative to already-landed code**, so the comment is corrected
  and the code is not
- `AGENTS.md` §16 — the report-don't-fix-inline rule that produced this task
- `AGENTS.md` §4 — if a correction would require reinterpreting a contract rather
  than restating an implementation-status fact, that is a **STOP**

**ADR check:** no ADR is required. This task records no architectural decision.
It removes a false statement about work whose architecture was already decided
and implemented: `ADR-005` is satisfied unchanged (the collection travels in the
existing `battle:{battleId}:state` record, `GAME_STATE.md` §2.3.2 item 7) and
`ADR-001` is unaffected (the comment describes server-authoritative state).

---

## Current State

`BattleState.cs` currently asserts that six §2 fields are absent from the type.
**Two of those claims are now false.**

### The stale comment (verified present)

```text
src/backend/GameServer.Domain/Battle/BattleState.cs   L61–66

/// stage is where those §2 fields first come into existence (§2.0.5). The
/// remaining §2 fields — <c>PetState</c>'s <c>StatusEffects</c> and its
/// <c>Tier</c>/<c>Star</c>/<c>Level</c>, and
/// <c>BossState</c>'s <c>StatusEffects[]</c> — are still
/// absent and still owned by later stages (§2.0.5.3). Their absence is a staging
/// position, not a scope reduction of §2: a field absent from a stage is <b>not
/// yet implemented</b>, not <b>not required</b> (§0 item 4).
```

`StatusEffects[]` is named twice as absent — once for `PetState` and once for
`BossState`. Both claims are false.

### Reality today (verified)

```text
src/backend/GameServer.Domain/Battle/
  StatusEffect.cs             EXISTS (TASK-095) — the §2.3.1 instance schema
  StatusEffectType.cs         EXISTS (TASK-095) — type/source vocabularies
  StatusEffectLifecycle.cs    EXISTS (TASK-095) — the §5.1.1 lifecycle
  PetState.cs                 L404  public StatusEffect[] ActiveStatusEffects
                                    { get; init; } = [];
                              L329–350 carries the implemented-member XML doc
  BossState.cs                L289  public StatusEffect[] ActiveStatusEffects
                                    { get; init; } = [];
                              L80—…  carries the implemented-member XML doc;
                              L48–52 states outright "All §2.4 fields now exist"

src/backend/GameServer.Domain/Battle/Serialization/
  BattleStateJson.cs          statusEffects member on PetStateJson AND
                              BossStateJson (TASK-096)
  BattleStateSerializer.cs    element projection both directions (TASK-096)
```

`BossState.cs` L48–52 already states the implemented position in its own words —
*"`StatusEffects[]` — which §2.4.1 lists as deferred to the Status Effects
system's own task — is implemented by that task"* — so `BattleState.cs` is
**inconsistent with the sibling types it documents**, not merely behind `docs/`.

### The still-correct part of the same sentence

`Tier`/`Star`/`Level` **are genuinely still absent** and must keep their staging
statement:

```text
src/backend/GameServer.Domain/Battle/PetState.cs   L64

/// <c>Tier</c>/<c>Star</c>/<c>Level</c>. Those belong to the Pet progression stage
```

and `docs/02-technical/GAME_STATE.md` §2.3's tree still lists
`└── Tier / Star / Level` with no implementation annotation, unlike its
neighbouring `StatusEffects[]` entry, which is annotated **implemented,
TASK-095/TASK-096**.

The correction is therefore **surgical, not a deletion**: `StatusEffects[]` moves
out of the "still absent / still owned by later stages" clause, while
`Tier`/`Star`/`Level` stays in it and keeps its `§2.0.5.3` citation.

---

## Scope

### In Scope

1. **Synchronize the `BattleState` type-level XML doc comment** at
   `src/backend/GameServer.Domain/Battle/BattleState.cs` L59–66 so that:
   - `PetState`'s `StatusEffects` is **no longer** described as absent or as
     owned by a later stage;
   - `BossState`'s `StatusEffects[]` is **no longer** described as absent or as
     owned by a later stage;
   - the comment instead states the implemented position, in the register and
     citation style the file already uses;
   - `Tier`/`Star`/`Level` **keeps** its "still absent and still owned by later
     stages (`§2.0.5.3`)" statement and its `§0 item 4` "not yet implemented, not
     not required" framing — that half of the sentence remains true and is a
     contract statement, not a status claim to be swept away;
   - the `§2.0.5.3` citation, the `§0 item 4` citation, and the "staging
     position, not a scope reduction of §2" framing are **preserved** for the
     fields they still describe.
2. **Inspecting the actual current code before editing** — the implementing
   agent must read `StatusEffect.cs`, `StatusEffectType.cs`,
   `StatusEffectLifecycle.cs`, `PetState.cs`, `BossState.cs`, the two
   `Serialization/` files, and `GAME_STATE.md` §2.3.1/§2.3.2/§2.4.1/§5.1.1 at
   pickup, and derive the replacement wording from **what is actually there**.
   This task deliberately does **not** prescribe the replacement sentence: the
   wording must describe the code as found, not as this task assumes it to be.
3. **The `BattleState` tree diagram inside the same comment (L24–57) only if it
   would otherwise become self-contradictory.** The tree currently omits
   `StatusEffects[]` under both `PetState` and `BossState`. If the corrected
   prose asserts the member exists while the tree two paragraphs above still
   omits it, the comment contradicts itself and a minimal tree line is required.
   Verify before editing; if the tree is acceptable as-is, leave it.
   **Do not** restructure the tree, add unrelated members to it, or expand it
   into a full §2 transcription.
4. **Reporting, not fixing, any further stale source comment discovered** that is
   not enumerated here — in particular the `BattleStateJson.cs` L415–418 item
   TASK-097 reported and explicitly declined to characterize
   (`AGENTS.md` §16).

### Out of Scope

- **Any executable code.** No statement, expression, member, parameter,
  constructor, factory, default value, or access modifier changes. The record's
  positional parameter list, its `Create` overloads, `CreateWith`, and the
  `Initial*` constants are **untouched**.
- **`BattleState` structure.** No member is added, removed, reordered, renamed,
  or retyped. The record's shape is the same before and after.
- **The `StatusEffect` schema** — §2.3.1's element members, types, and presence
  rules are **not** redefined, summarized into a second copy, or extended.
- **The `StatusEffect` lifecycle** — §5.1.1's apply/refresh/consume/expire rules,
  the step-19a position, and the `Id`-ascending pass order are **not** described
  beyond what the comment already cites, and are not changed.
- **`PetState.StatusEffects[]` / `BossState.StatusEffects[]`** — the members
  themselves, their `= []` defaults, and their non-nullability are untouched.
- **The serialization contract.** No serialized member, name, casing, or
  round-trip obligation changes. `BattleStateJson.cs` and
  `BattleStateSerializer.cs` are **not modified**.
- **Serialization behavior** — no member added, removed, or renamed anywhere.
- **Redis** — no key, field, TTL, record, or concurrency change;
  `REDIS_STATE.md` is not edited.
- **SignalR** — no event, payload member, or Hub method; `StatusEffects[]`
  remains **not** a wire member (`GAME_STATE.md` §2.3.1 wire note;
  `SIGNALR_PROTOCOL.md` §4.2 item 2).
- **API** — no endpoint, request, or response contract changes.
- **PostgreSQL** — no schema, entity, column, constraint, or migration.
- **Any test change** — nothing under `tests/`. No test may be added, edited, or
  deleted to accommodate a comment (`refactor.md` §3: existing tests must still
  pass, unchanged in intent).
- **`docs/`** — **no document under `docs/` is modified at all**, explicitly
  including `docs/02-technical/GAME_STATE.md` and
  `docs/02-technical/DATABASE.md`. The authoritative documentation is already
  correct as of TASK-097; this task brings the *source comment* up to it, in that
  direction only.
- **Gameplay** — no rule, formula, balance value, or timing changes.
- **Match-3, board, swap, match detection, cascade, gravity, combo.**
- **Combat, Damage, the Damage Pipeline, Boss Skills, Boss Response, Passive
  logic, and StatusEffect producers.** No code that *creates* a Status Effect is
  written, described as existing, or implied to exist.
- **`GameRuntime`, Phaser, frontend, client** — nothing under `src/frontend/`.
- **Discord** — untouched.
- **`tasks/blocked/TASK-036-*.md`** — not reopened, not modified.
- **`tasks/blocked/TASK-079-*.md`** — not resolved, not modified.
- **`tasks/completed/`** — immutable (`TASK_LIFECYCLE.md` §3). TASK-095, TASK-096,
  and TASK-097 are **not** modified.
- **Any general source-comment or documentation audit** — no sweep beyond the one
  enumerated defect.
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2.

---

## Acceptance Criteria

- [x] `src/backend/GameServer.Domain/Battle/BattleState.cs` no longer states or
      implies that `PetState`'s `StatusEffects` is absent, deferred, unimplemented,
      or owned by a later stage.
- [x] `src/backend/GameServer.Domain/Battle/BattleState.cs` no longer states or
      implies that `BossState`'s `StatusEffects[]` is absent, deferred,
      unimplemented, or owned by a later stage.
- [x] Both stale claims are corrected — **not only the first occurrence**. The
      `StatusEffects` reference appears twice in the same sentence (once per
      entity) and both are addressed.
- [x] The replacement wording is consistent with the implemented `BattleState`
      model **as verified in the code at pickup** — specifically with
      `PetState.ActiveStatusEffects`, `BossState.ActiveStatusEffects`, and the
      `statusEffects` serializer members — and with `GAME_STATE.md` §2.3.1,
      §2.3.2, §2.4.1, and §5.1.1.
- [x] The replacement wording describes the implemented position **without
      restating the contract**: it cites `GAME_STATE.md` sections by number and
      does not copy the element schema, the member list, the lifecycle steps, or
      any duration/magnitude value (`documentation-change.md` §2's no-duplication
      rule; `AGENTS.md` §9).
- [x] `Tier`/`Star`/`Level` **remain** described as still-absent and still owned
      by later stages, with their `§2.0.5.3` citation preserved — they are
      genuinely unimplemented (`PetState.cs` L64).
- [x] The `§0 item 4` "not yet implemented / not required" framing and the
      "staging position, not a scope reduction of §2" statement survive, still
      applying to the fields that actually remain staged.
- [x] If the `BattleState` tree diagram in the same comment was edited, the edit
      is limited to the minimal `StatusEffects[]` line(s) required to keep the
      comment self-consistent; no unrelated tree member is added, removed, or
      reordered.
- [x] **No executable code changed:** the record's positional parameter list, all
      three `Create`/`CreateWith` factories, the four `Initial*` constants, and
      every member declaration are byte-identical before and after.
- [x] **No `BattleState` structure change:** no member added, removed, renamed,
      reordered, or retyped.
- [x] **No serialization behavior change:** a `BattleState` serializes to and
      deserializes from a byte-identical document before and after this task.
- [x] **No `StatusEffect` contract change:** the §2.3.1 schema, the §5.1.1
      lifecycle, and the `StatusEffects[]` members on both entities are
      semantically identical before and after.
- [x] **Server authority unchanged:** the comment continues to describe
      server-produced authoritative state (`GAME_RULES.md` §18, ADR-001); no
      client-authoritative claim is introduced (`AGENTS.md` §10).
- [x] No production behavior changes — proven by the existing backend suites
      passing **unmodified**.
- [x] No serialization behavior changes — proven by the existing serializer and
      round-trip suites passing **unmodified**.
- [x] No authoritative documentation changes: **zero** files under `docs/` are
      modified — in particular `GAME_STATE.md` and `DATABASE.md`.
- [x] No unrelated source files are modified: the changed-file set is exactly
      `src/backend/GameServer.Domain/Battle/BattleState.cs` plus this task file.
- [x] Zero files under `tests/` are modified; no test is added, edited, or
      deleted.
- [x] Relevant backend validation passes — at minimum the project builds and
      `GameServer.Domain.Tests` passes, per Testing Requirements.
- [x] `tasks/completed/` is unmodified; TASK-097, TASK-096, and TASK-095 are
      unmodified (`TASK_LIFECYCLE.md` §3, `AGENTS.md` §16).
- [x] No new stale claim is introduced: a re-read of the corrected comment block
      against `GAME_STATE.md` §2.3/§2.4 finds no remaining false
      implementation-status statement about `StatusEffects[]`.
- [x] Quality review checklist passes (`quality/review.md` §1), skipping
      code-behavior items that the comment-only change cannot exercise.
- [x] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001).

### Explicit Constraints

```text
No gameplay.
No runtime behavior changes.
No StatusEffect contract changes.
No serialization changes.
No Redis changes.
No SignalR changes.
No API changes.
No authoritative documentation changes.
No test changes.
No BattleState structure changes.
```

---

## Affected Files & Areas

```text
[x] src/backend/GameServer.Domain/Battle/BattleState.cs
      (XML doc comment text only — the L61–66 stale clause; optionally the
       L24–57 tree diagram line(s) if the comment would otherwise become
       self-contradictory. NO executable code.)
[ ] src/backend/GameServer.Domain/Battle/ (StatusEffect.cs, StatusEffectType.cs,
      StatusEffectLifecycle.cs, PetState.cs, BossState.cs — READ-ONLY; inspected
      to derive correct wording, never edited)
[ ] src/backend/GameServer.Domain/Battle/Serialization/ (READ-ONLY — the
      contract being described; not edited)
[ ] src/backend/GameServer.Application/ (none)
[ ] src/backend/GameServer.Infrastructure/ (none — no Redis, no PostgreSQL)
[ ] src/backend/GameServer.Api/ (none — no endpoint, no Hub method)
[ ] src/frontend/client/ (none)
[ ] tests/ (none — existing suites must remain green UNMODIFIED)
[ ] docs/ (NONE — explicitly includes GAME_STATE.md and DATABASE.md)
[x] tasks/active/TASK-098-*.md → tasks/completed/ (this file — Status field and
      Completion Evidence only)
[ ] tasks/completed/ (NO CHANGES)
[ ] tasks/blocked/TASK-036-*.md, tasks/blocked/TASK-079-*.md (NO CHANGES)
```

---

## Implementation Notes

- **Read the code first; the wording must follow the code.** The replacement
  sentence must be derived from what `StatusEffect.cs`, `StatusEffectType.cs`,
  `StatusEffectLifecycle.cs`, `PetState.cs`, `BossState.cs`, and the two
  `Serialization/` files actually contain at pickup — not from this task's
  description, and not from TASK-097's report. If the code differs from what
  this task documents, the code wins and the difference is reported.
- **The sibling types already show the target register.** `PetState.cs`
  L329–350 and `BossState.cs` L48–52/L80—… both already document
  `StatusEffects[]` as an implemented member, citing `GAME_STATE.md` §2.3.1,
  §2.3.2, and §5.1.1. Match that established form; do not invent new vocabulary,
  new headings, or an essay.
- **Cite, do not restate.** The file's whole documentation idiom is
  `<c>GAME_STATE.md</c> §N` citations. The correction should keep that idiom: name
  the sections, do not copy the schema, the seven element members, the lifecycle
  steps, or any value into the comment. A comment that restates a contract
  becomes a second source of truth — exactly what `AGENTS.md` §9 and
  `documentation-change.md` §2 forbid.
- **Preserve the staging framing, retarget it.** The paragraph's purpose is to
  state which §2 fields a stage adds and which remain absent. After the fix it
  still has a job: `Tier`/`Star`/`Level` remain staged. Keep the paragraph, keep
  `§2.0.5.3`, keep `§0 item 4`'s vocabulary — narrow the field list.
- **Do not delete the `§0 item 4` sentence.** *"Their absence is a staging
  position, not a scope reduction of §2: a field absent from a stage is not yet
  implemented, not not required"* is about the fields still absent. Removing it
  would drop a correct contract statement.
- **TASK-095's own Scope item 7 anticipated this.** It stated the superseded
  staging comments on `PetState`, `BossState`, `BattleState`, and the JSON DTOs
  should be updated "only to the extent they would otherwise become false" —
  and then TASK-095 updated `PetState`/`BossState` but left `BattleState.cs`
  behind. This task discharges that remainder, and nothing more.
- **TASK-097 deliberately left this alone.** It reported the comment under
  "REPORTED, NOT CHANGED" because TASK-097 was documentation-only and touching a
  source file was one of its own STOP conditions. That is why this is a separate
  task rather than a TASK-097 follow-up.
- **`BattleState.cs` is a Domain type and must stay framework-independent.**
  The comment already states this (`ARCHITECTURE.md` §2.1). Do not add any
  Redis, SignalR, EF Core, HTTP, Phaser, or Discord reference to the comment.
- **The tree diagram is a trap for over-editing.** If you touch it, add only the
  `StatusEffects[]` line(s) needed for consistency with the corrected prose.
  Do not add `EquippedRelics[]`, `EquippedCards[]`, or any other member — that
  would be an unrelated change to a different staging question
  (`AGENTS.md` §16).
- **Schema/state wording must not imply capability.** Saying the collection
  "is implemented" is accurate. Do **not** write that Status Effects are
  *applied*, *ticked*, or *delivered* in gameplay if the code does not show it —
  no producer exists and the collection is not a wire member.
- **Comment-only means comment-only.** Verify before and after that the file's
  compiled content is unchanged. A convenient proof: strip `///`-prefixed lines
  from both versions and compare — the remaining text must be identical.
- **Do not "improve" adjacent comments.** If a neighbouring sentence looks
  imprecise, report it; do not edit it (`AGENTS.md` §16).

---

## Testing Requirements

Because this is an XML-documentation-only source change, validation exists to
prove **the absence of a behavior change**, not to exercise new behavior.

### Required Verification

```text
[x] Build/compile validation — the solution builds with zero new warnings or
                         errors. A malformed XML doc comment (e.g. an
                         unclosed <c> tag) is a build warning and would be a
                         real regression in this task, so this check is
                         substantive here, not ceremonial.
                         Preferred: `dotnet build src/backend/GameServer.sln`
[x] Unit tests         — the Domain suite must pass UNMODIFIED, proving the
                         comment edit changed no domain behavior.
                         Preferred smallest relevant run:
                         `dotnet test tests/backend/GameServer.Domain.Tests`
                         This is the project whose assembly contains the edited
                         file. A full backend suite may be run if repository
                         workflow requires it; the smaller run is preferred
                         because no behavior changed.
[x] Source-equivalence check — the edited file's code (every line that is not a
                         `///` XML doc comment) is byte-identical to the
                         pre-edit file. This is the direct proof of "no runtime
                         behavior change".
[x] Documentation consistency — the corrected comment re-read against
                         `GAME_STATE.md` §2.3, §2.4, §2.3.1, §2.3.2, §2.4.1,
                         §5.1.1, and against the sibling `PetState.cs` /
                         `BossState.cs` comments; no statement survives that
                         contradicts them, and no contract is duplicated.
[x] Stale-claim sweep  — a search of `BattleState.cs` for `StatusEffects`
                         confirms every remaining mention is accurate, with zero
                         residual "absent / not yet implemented / later stage"
                         claim about it.
[x] Scope/changed-file verification — `git status` shows exactly
                         `src/backend/GameServer.Domain/Battle/BattleState.cs`
                         plus this task file; zero `docs/`, `tests/`, or other
                         `src/` changes.
[x] Guard checks       — `tasks/completed/` unmodified; TASK-095, TASK-096, and
                         TASK-097 unmodified; TASK-036 and TASK-079 unmodified.
[ ] Integration tests  — N/A: no boundary is implemented or altered.
[ ] Gameplay scenarios — N/A: no gameplay rule is derived, changed, or
                         exercised (AGENTS.md §6 maps gameplay scenarios to rule
                         docs; none is touched). This is the one place
                         core/validation.md §2's depth is deliberately not
                         reached, because the corresponding risk layer does not
                         apply — LOW-risk REFACTOR depth is
                         "build/compile + focused unit test(s) + review".
```

### Key Edge Cases

- **Two stale claims in one sentence.** `StatusEffects` is named for `PetState`
  and `StatusEffects[]` for `BossState`. Correcting one and missing the other
  leaves the comment self-contradictory — the same failure mode TASK-097
  enumerated for `GAME_STATE.md`.
- **A true claim sits between the two false ones.** `Tier`/`Star`/`Level` is
  genuinely unimplemented. A careless rewrite that deletes the whole clause
  silently drops a correct statement; a careless rewrite that keeps the clause
  verbatim leaves the two false claims in place. Both halves must be separated
  deliberately.
- **The `§2.0.5.3` citation has two possible readings.** It is correct for
  `Tier`/`Star`/`Level` and false for `StatusEffects[]`. Do not delete or
  renumber it; retarget it.
- **The tree diagram directly above the prose lists neither entity's
  `StatusEffects[]`.** Correcting the prose without checking the tree can produce
  a comment that contradicts itself within twenty lines. Verify, then make the
  minimal edit if needed.
- **A "documentation-only" mindset produces an out-of-scope edit.** Because the
  stale text is a *documentation* claim, there is a pull toward also fixing
  `GAME_STATE.md` or the `BattleStateJson.cs` item TASK-097 reported. Both are out
  of scope; the former is already correct and the latter needs its own
  determination.
- **The change is in a `.cs` file but is not a code change.** The file
  compiles identically. Confirm that explicitly rather than assuming the
  compiler proves it: XML doc comments *do* affect generated documentation and
  can produce build warnings.
- **A sentence that turns out to carry contract meaning as well as status
  meaning** — STOP per `AGENTS.md` §4; report it rather than editing it.
- **`BattleState.cs` may hold other now-stale statements** beyond L61–66 (e.g.
  around L157, where `Turn` says "still `0` at this stage"). Those are a
  different question, are not proven false by this task's evidence, and are out
  of scope: report, do not fix.

### Explicitly Not Tested Here

- That a gameplay path *applies* a Status Effect — no producer exists; this task
  changes no code and asserts no new behavior.
- Burn's damage-per-tick value — the Damage Pipeline tick remains unimplemented.
- The §5.1.1 lifecycle itself — TASK-095 delivered and tested it; this task
  touches only a comment that cites it.
- Any wire or Redis behavior for `StatusEffects[]` — it is not a wire member and
  this task adds no Redis structure.

---

## Stop Conditions

Universal `AGENTS.md` §20 stops always apply. Task-specific:

- **If `BattleState.cs` no longer contains the stale claim at pickup: STOP and
  report.** Do not manufacture a correction, and do not convert this into a
  different comment-cleanup task.
- **If the comment's meaning is ambiguous and cannot be corrected from
  authoritative documentation: STOP per `AGENTS.md` §20 (ambiguous requirement)**
  — report the sentence and the candidate readings rather than choosing one.
- **If correcting the comment would require changing the `BattleState`
  contract: STOP.** A contract clarification is a different task with its own
  documentation impact.
- **If correcting the comment would require changing gameplay behavior: STOP**
  per `AGENTS.md` §8/§10.
- **If a targeted sentence turns out to carry contract meaning as well as status
  meaning: STOP per `AGENTS.md` §4** — report it, do not edit it.
- **If the replacement wording cannot be derived from the implemented code and
  `GAME_STATE.md` §2.3/§2.4 without inventing detail: STOP per `AGENTS.md` §7.**
- **If the `StatusEffects[]` implementation turns out to be absent or partial at
  pickup** (e.g. `ActiveStatusEffects` or the `statusEffects` serializer member
  is missing, or `PetState`/`BossState` disagree): **STOP and report.** The
  premise of this task is that TASK-095/TASK-096 landed; if they did not, the
  correct follow-up is an implementation task, not a comment fix.
- **If the correction requires touching `docs/` — including `GAME_STATE.md` or
  `DATABASE.md`: STOP.** Those are explicitly out of scope and are already
  correct as of TASK-097.
- **If the correction requires touching `BattleStateJson.cs`,
  `BattleStateSerializer.cs`, `PetState.cs`, or `BossState.cs`: STOP and report.**
  This task edits exactly one source file.
- **If satisfying any criterion requires modifying a test: STOP** — report the
  conflict rather than editing the test (`refactor.md` §3, `AGENTS.md` §15).
- **If the repository's task-generation workflow requires a different task type
  for source-documentation synchronization: STOP and report the classification
  conflict** rather than proceeding.
- **If a broader source synchronization issue is discovered that would make this
  task non-coherent** (e.g. the same stale claim exists across many source files
  and cannot be corrected piecemeal): **STOP and report**, then decompose per
  `tasks/README.md` §13 — do not expand this task automatically.
- **If existing task dependencies contradict this task: STOP and report.**
- **If satisfying any criterion requires modifying TASK-095, TASK-096, TASK-097,
  TASK-036, or TASK-079: STOP** — report instead.
- **If the change cannot remain comment-only: STOP and report.**
- **If the task exceeds 7 skills or crosses multiple uncoupled boundaries: STOP &
  decompose.**

---

## Completion Evidence

<!--
  TO BE COMPLETED BY THE EXECUTING AGENT after reaching DONE.
  Keep concise and factual.
-->

**Picker-up note (pre-existing working-tree state).** At pickup, `git status`
shows uncommitted changes from earlier tasks (TASK-086…TASK-097), including
`docs/02-technical/GAME_STATE.md` (Version 2.9, TASK-093/097) and
`src/backend/GameServer.Domain/Battle/PetState.cs` / `BossState.cs` (TASK-095).
Those were **not** authored by TASK-098. The baseline for change-isolation is the
working-tree content at pickup, not `HEAD`.

### Changed Files

- `src/backend/GameServer.Domain/Battle/BattleState.cs` — two XML doc comment
  edits, no executable line touched:
  1. **The stale paragraph (was L59–66, now L61–72).** The clause *"The remaining
     §2 fields — `PetState`'s `StatusEffects` and its `Tier`/`Star`/`Level`, and
     `BossState`'s `StatusEffects[]` — are still absent and still owned by later
     stages (§2.0.5.3)"* was replaced. `StatusEffects[]` is now stated as the
     Status Effect stage's member and **implemented** on both entities, citing
     `GAME_STATE.md` §2.3.1 (element schema), §5.1.1 (lifecycle), and §2.3.2
     (serialization with the rest of the record), and naming
     `PetState.ActiveStatusEffects` / `BossState.ActiveStatusEffects` as the
     carriers. `Tier`/`Star`/`Level` **remains** described as still absent and
     still owned by a later stage, with its `§2.0.5.3` citation and the
     `§0 item 4` "not yet implemented, not not required" framing preserved for it.
  2. **The tree diagram (L24–59).** Added exactly two lines, one per entity, so
     the tree does not contradict the corrected prose twenty lines below it:
     `StatusEffects[]  (active instances — §2.3.1, §5.1.1)` under `PetState`, and
     the same under `BossState`. No other tree member was added, removed, or
     reordered; no other part of the file's documentation was rewritten.
- `tasks/active/TASK-098-…md` — lifecycle `Status:` field, acceptance-criteria
  checkboxes, this Completion Evidence section, and the Affected Files path
  (`backlog/` → `active/`) only.

Diff against the pickup baseline: **10 insertions / 5 deletions**, every changed
line inside the XML doc comment. No other file was changed by this task. **No
documentation, test, frontend, Application, Infrastructure, or Api file was
modified.**

### Validation Results

```text
Build               dotnet build src/backend/GameServer.sln
                    → Build succeeded. 0 Error(s), 2 Warning(s).
                      Both warnings are pre-existing MSB3277 EF Core
                      version-conflict warnings in the two test projects;
                      proven pre-existing by rebuilding with the unmodified
                      baseline file, which produced the identical 2/0 counts.
                      No `warning CS` / `error CS` from the edited file, so the
                      XML doc comment is well-formed.

Domain tests        dotnet test tests/backend/GameServer.Domain.Tests
                    → Passed! Failed: 0, Passed: 1012, Skipped: 0, Total: 1012
                      Run twice (before and after the correction) with the
                      identical result. Matches TASK-096's recorded 1012
                      baseline, proving no test file was modified and no
                      behavior changed. Tests were run UNMODIFIED.

Source-equivalence  Stripped every line whose first non-space characters are
                    `///` from the baseline and the corrected file, then
                    SHA256'd the remainder:
                      before: 130 non-`///` lines, 9C3D6467…AE3F
                      after : 130 non-`///` lines, 9C3D6467…AE3F
                    → PASS — byte-identical. This is the direct proof that zero
                      executable code changed.

Stale-claim sweep   Searched BattleState.cs for `StatusEffects` (4 hits) and for
                    `not yet implemented|absent|later stage|deferred` (11 hits).
                    → PASS. All 4 `StatusEffects` mentions are accurate (2 tree
                      lines stating the member exists; 2 prose references naming
                      the real members). The only "absent / later stage" claim in
                      the corrected paragraph (L69) now attaches solely to
                      `Tier`/`Star`/`Level`. Every other match is an unrelated,
                      correct convention (`PassiveResetOverride?`,
                      `LastCommittedSwapPair?`, "never absent" statements, etc.).
                      Zero residual false claims about `StatusEffects[]`.

Documentation       Cross-checked the corrected comment against GAME_STATE.md and
consistency         the sibling types:
                      GAME_STATE.md L916 / L984 / L1313 annotate StatusEffects[]
                        as "implemented, TASK-095/TASK-096" → my comment states
                        implemented on both entities with the same citations.
                      GAME_STATE.md L906 lists `Tier / Star / Level` with NO
                        implemented annotation, and PetState.cs L64 states they
                        "belong to the Pet progression stage" and are "not yet
                        implemented" → my comment preserves exactly that.
                    → PASS. StatusEffects[] = implemented state; Tier/Star/Level
                      = still staged. The comment cites sections by number and
                      does NOT restate the element schema, the member list, the
                      lifecycle steps, or any value — no contract duplication.

Changed-file        mtime comparison against the pickup baseline time
verification        (2026-09-30 20:40:33) across docs/, tests/, src/, tasks/:
                      docs/   → 0 files modified
                      tests/  → 0 files modified
                      src/    → exactly 1: Battle/BattleState.cs
                    → PASS. TASK-098's production change is exactly one file.
```

### Contract Preservation Verification

```text
BattleState structure       UNCHANGED (no member added, removed, renamed,
                            reordered, or retyped; 130 non-/// lines identical)
BattleState factories       UNCHANGED (Create, both overloads; CreateWith)
BattleState Initial*        UNCHANGED (InitialTurn/Sequence/MatchCount/Combo)
StatusEffect schema         UNCHANGED (referenced by §2.3.1, not restated)
StatusEffect lifecycle      UNCHANGED (referenced by §5.1.1, not restated)
PetState collection         UNCHANGED (ActiveStatusEffects, = [] default)
BossState collection        UNCHANGED (ActiveStatusEffects, = [] default)
Serialization               UNCHANGED (BattleStateJson / BattleStateSerializer
                            not modified; statusEffects members intact)
Redis                       UNCHANGED (REDIS_STATE.md not modified)
SignalR                     UNCHANGED (StatusEffects[] remains not a wire member)
API                         UNCHANGED (API_CONTRACTS.md not modified)
PostgreSQL                  UNCHANGED (no schema, no migration)
GAME_STATE.md               UNCHANGED (0 docs/ files modified)
DATABASE.md                 UNCHANGED (0 docs/ files modified)
```

### Server Authority & Scope Verification

- [x] Confirmed zero client-authoritative gameplay logic (no `src/frontend/` file
      changed by this task)
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
- [x] Confirmed no runtime behavior, serialization, Redis, SignalR, API, or
      schema change
- [x] Confirmed TASK-095 / TASK-096 / TASK-097 were not modified
- [x] Confirmed zero `docs/` and zero `tests/` files were modified

### Review Checklist (`quality/review.md` §1)

```text
Correctness     PASS — the comment now matches GAME_STATE.md §2.3/§2.4 (which
                       already record StatusEffects[] as implemented) and the
                       actual code. No rule reinterpreted or extended.
Architecture    PASS — no dependency added or moved; the comment keeps stating
                       that BattleState is framework-independent
                       (ARCHITECTURE.md §2.1).
Scope           PASS — 1 source file, comment text only, 2 minimal tree lines;
                       no unrelated cleanup, no "while I was here" change.
Tests           PASS — LOW-risk REFACTOR depth: build + compile validation +
                       focused suite, all unmodified and green.
Documentation   PASS — no docs/ change required; the authoritative docs were
                       already correct as of TASK-097 and are unchanged.
Security        PASS — no auth surface, no data exposure.
Performance     PASS — comments only; no hot-path change.
Maintainability PASS — no abstraction added; the correction cites sections
                       instead of duplicating the contract.
Determinism     PASS — no RNG, clock, or ordering change; server-authoritative
                       state is described, not altered.
```

### Unrelated Stale Documentation Discovered (REPORTED, NOT CHANGED)

Per `AGENTS.md` §16 and TASK-098's Scope item 4, these were left untouched:

1. `BattleState.cs` L160–166 — the `Turn` param doc says *"Still `0` at this
   stage: board generation is not a player Swap/Action and starts no Turn"*. This
   is a **different** question: `Turn` is now incremented by resolution
   (`GAME_STATE.md` §5.1), so the "at this stage" framing may be stale. It was
   **not** in TASK-098's enumerated defect, is not proven false by this task's
   evidence, and was deliberately not edited. Suggested follow-up: a separate
   determination, not a sweep.
2. `src/backend/GameServer.Domain/Battle/Serialization/BattleStateJson.cs`
   L415–418 — the "not yet supplied" comment TASK-097 reported and declined to
   characterize. **Explicitly out of scope for TASK-098**; not changed, not
   reinterpreted.

### Process Note (reported, not silent)

Two execution incidents are recorded here rather than hidden, per `AGENTS.md`
§16/§21:

1. **A baseline-restore step briefly reverted the correction.** While proving the
   2 build warnings were pre-existing, the corrected file was temporarily
   replaced with the baseline copy, and the intended restore silently no-op'd
   because the source path did not exist. A follow-up `-match` check then gave a
   false "corrected" reading. The correction was **re-applied from scratch** and
   re-verified — the final file hash (`811CC8A1…`) differs from the baseline
   (`F03D9137…`), and all validations above were run against the final file.
2. **The task file's typography was damaged and repaired.** Marking the
   acceptance-criteria checkboxes used `Get-Content`/`Set-Content`, which read the
   UTF-8 file as Windows-1252 and re-encoded it, mangling em-dashes, en-dashes,
   ellipses, and one box-drawing fragment (153 occurrences across 136 lines) and
   adding a BOM. This affected **only this task file** — `BattleState.cs` and all
   other files were written with tools that preserve UTF-8 and were verified
   clean. The damage was reversed by mapping each distinct damaged sequence back
   to its character from surrounding context; the file now contains **zero**
   U+FFFD, no mojibake, and no BOM. No semantic content was lost: all sections,
   all 23 acceptance criteria, and all constraint blocks were verified present
   and readable afterwards.
