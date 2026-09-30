# TASK-099 — Record the Loadout Selection Surface Decision (TASK-079 D1, Option A)

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ by path and section — it does NOT copy game rules,
  formulas, schemas, or contracts.

  THIS TASK AUTHORS NO CODE AND IMPLEMENTS NO BEHAVIOR. It records an
  already-made product/architecture decision (TASK-079 dependency D1) in the
  canonical owner documents, and brings the two documents that contradict it
  into line.

  BOUNDARY: documentation only. Zero files under src/ or tests/.

  PROVENANCE: TASK-079 was STOPPED on 2026-09-30 with three independent
  dependencies (D1 selection surface, D2 in-progress selection-state
  ownership + GameRuntimePort, D3 Pet/Card/Relic provisioning) and explicitly
  did NOT create follow-up tasks (report-only). The human has now supplied the
  D1 decision (Option A). This task records D1 ONLY.

  D2 IS ALREADY LARGELY SATISFIED by TASK-081 (ARCHITECTURE.md §2.2.3 +
  GameRuntimePort extension) and D3 by TASK-082/084/085; neither is reopened,
  re-decided, or modified here. This task does not modify TASK-079.
-->

---

## Metadata

```text
Task ID:           TASK-099
Type:              DOCUMENTATION (TASK_TYPES.md §2 — "Change docs/ content —
                   documentation is the primary output, not code"; workflow
                   documentation/documentation-change.md). The output is
                   docs/02-technical/TDD.md and docs/02-technical/
                   ARCHITECTURE.md. See "Type classification note" below.
Status:            BACKLOG
Risk:              LOW—MEDIUM (TASK_TYPES.md §4 — DOCUMENTATION baseline
                   LOW—MEDIUM; MEDIUM because it corrects a cross-referenced
                   client-boundary contract that other documents and tasks
                   cite. LOW in that it changes no code, no API contract, no
                   protocol, and no gameplay rule.)
Priority:          HIGH (a stale authoritative statement currently contradicts
                   implemented client architecture and would cause a
                   duplicate implementation task to be created)
Primary Agent:     review (documentation consistency — TASK_TYPES.md §2
                   DOCUMENTATION "Primary Agent: Review Agent")
Supporting Agents: client (client-layer boundary accuracy),
                   backend (no change — consulted only if a boundary
                   statement proves to touch Application/Api)
Workflow:          documentation/documentation-change.md
Skills:            discovery/documentation-discovery,
                   quality/documentation-consistency,
                   quality/scope-validation,
                   quality/architecture-conformance
                   (4 skills — Simple budget, tasks/README.md §12)
Dependencies:      TASK-079 (BLOCKED — the decision task that produced D1;
                     NOT modified, NOT resolved, NOT reopened by this task),
                   TASK-080 (DONE — resolved D1's selection-surface
                     classification; context),
                   TASK-081 (DONE — D2: in-progress selection ownership +
                     GameRuntimePort; context, NOT re-decided here),
                   TASK-078 (DONE — the LobbyScene implementation this
                     decision's UI ownership describes; context),
                   TASK-075/076/077 (DONE — client read/start/orchestration
                     services; context)
Blocks:            Nothing. This task resolves a documentation contradiction;
                   it does not gate an implementation task.
Estimate:          Simple (two documents, the decision recorded in one
                   canonical owner and one stale paragraph synchronized)
```

**Type classification note.** `DOCUMENTATION`, not `ARCHITECTURE`. The decision
itself has **already been made by the human** — this task does not make it. Per
`docs/03-decisions/README.md` §1 and `architecture/adr-change.md` §3, an ADR
records a decision that is *already reflected in the existing documentation*; it
is a historical record, not a design proposal. `TASK_TYPES.md` §2 defines
`DOCUMENTATION` as "A document in `docs/` needs to be created, updated, or
corrected, and no code change is required" — which is exactly this task's whole
output. The `ARCHITECTURE` type is reserved for a task that *changes* structure,
layering, persistence/realtime strategy, or the authoritative-state model
(`TASK_TYPES.md` §2 `ARCHITECTURE`); this task changes none of those — it records
and synchronizes documentation for a boundary whose implementation already
exists.

**This task creates no ADR by default.** `ARCHITECTURE.md` §2.2.3 (added by
TASK-081) and `TDD.md` §2.1 already own the pre-battle selection boundary. Per
`architecture/adr-change.md` §1 ("Decision vs. Implementation") and
`docs/03-decisions/README.md` §2, an ADR is warranted only if the settled
decision is *architecturally important, difficult to reverse, likely to be
questioned later, and cross-cutting*. The D1 answer is a surface-ownership
clarification already realized in `TDD.md` §2.1's existing LobbyScene paragraph
and `ARCHITECTURE.md` §2.2.3; recording it there discharges the decision. **If —
and only if — the executing agent concludes the decision satisfies
`docs/03-decisions/README.md` §2 and is not already fully owned by those two
documents, it may create `ADR-017` (the next free number; highest existing is
`ADR-016`) and must then update the `docs/03-decisions/README.md` §7 index in the
same change.** It must not create an ADR that merely restates §2.2.3.

---

## Objective

Record the settled **loadout selection surface** decision — **Option A: the
Phaser `LobbyScene` owns the loadout-selection UI and the temporary
(in-progress) selection state** — in its canonical owning documents, and
synchronize the authoritative statements that currently contradict it, so that
`docs/` describes the client boundary the repository has already implemented and
no agent re-derives the question or schedules duplicate work.

This task resolves **TASK-079 dependency D1 only**. It does not resolve D2 or D3,
does not reopen TASK-079, and changes no code.

### The decision being recorded

```text
LobbyScene                    owns the loadout-selection UI + the in-progress
                              (temporary) selection state
      ↓
SelectedLoadout               one Pet · 3 Basic Cards · 3–5 Relic instances
                              (ephemeral, scene-local, discarded on shutdown)
      ↓
GameRuntime                   owns runtime coordination only — it performs the
                              documented start sequence and computes nothing
      ↓
POST /api/battle/start        API_CONTRACTS.md §3
      ↓
Server validates ownership / loadout cardinality / category / copy limit /
distinctness, and owns authoritative BattleState
```

Ownership rules this decision fixes:

```text
Phaser owns the game-flow UI        Pet, Card, and Relic selection
GameRuntime owns runtime coordination
Server validates and owns authoritative state
React remains shell / platform UI
LobbyScene holds no API call and no business rule
```

---

## Authoritative References

- `docs/02-technical/TDD.md` §2.1 — the **primary owner** of the scene
  lifecycle and the Phaser/React split; its LobbyScene paragraph (L116–129)
  already assigns the GDD.md §2 selection flow to LobbyScene and must now also
  state the **selection-surface ownership** the D1 decision settles
- `docs/02-technical/TDD.md` §2.1 "React Responsibilities & Boundaries"
  (L147–154) — "React owns: … HTML overlays, menus, settings …"; the boundary
  this decision must be reconciled with, since "menus" must not be read as
  owning the Pet/Card/Relic selection flow
- `docs/02-technical/TDD.md` §2.1 "Service Boundaries" (L170–177) — transport
  isolation; the rule that makes "no API calls in the scene" a documented
  boundary rather than a convention
- `docs/02-technical/TDD.md` §2.1 scene-lifecycle MVP staging note (L103–111)
  — **verified stale**: it still states `MainMenuScene` and `ResultScene` "are
  deferred to their own tasks", although TASK-090 and TASK-087 implemented both
  (see Current State). This is a **second, independent** wording defect in the
  same section; see Scope item 3 for how it is bounded
- `docs/02-technical/ARCHITECTURE.md` §2.2.3 — "Pre-Battle Selection Boundary"
  (added by TASK-081): in-progress selection is ephemeral `LobbyScene` state
  (rule 1), the runtime does not hold it (rule 2), scenes reach transport only
  through the runtime port (rule 3), read data is a selection source not a
  selection (rule 4), the port exposes `startBattle` and validity is never the
  scene's to decide (rule 5), the port carries capabilities not models (rule 6)
- `docs/02-technical/ARCHITECTURE.md` §2.2.1 — runtime coordination boundary
  and its rules 1–6; L231–233's "not implemented yet" paragraph (**verified
  partially stale** — see Current State)
- `docs/02-technical/ARCHITECTURE.md` §2.2 item 1–4 — frontend layers,
  services isolation, server-authoritative principle; §3's component table
  (`GameShell / React Overlays — HTML overlays & menus`)
- `docs/02-technical/ARCHITECTURE.md` §5 item 5 — minimal client state
  management; the anti-overengineering constraint the selection state must obey
- `docs/00-overview/GDD.md` §2 (L62–68) — the user-facing steps
  "Choose Pet → Equip Cards → Equip Relics → Start Battle" that D1 assigns to a
  surface. **This document names the steps and assigns them to no layer**; it is
  not the owner of the client boundary and must **not** be edited
- `docs/00-overview/MVP_SCOPE.md` §1, §2, §4 — the flow must stay in IN scope;
  unlisted = FUTURE
- `docs/03-decisions/ADR/ADR-003-phaser-game-runtime.md` — Phaser owns the game
  runtime and scene lifecycle; the rationale this decision is consistent with
- `docs/03-decisions/ADR/ADR-001-server-authoritative-battle.md` — server
  authority; unchanged by this decision
- `docs/03-decisions/ADR/ADR-011-player-owner-pet-combat.md` item 4 — ownership
  vs battle-scoped equip; the three-concept separation §2.2.3 restates
- `docs/03-decisions/README.md` §1, §2, §4, §5, §7 — ADR purpose, warrant
  criteria, numbering (never reuse; next free is **ADR-017**), and the §7 index
  that must be updated in the same change if an ADR is created
- `tasks/blocked/TASK-079-resolve-client-loadout-acquisition-boundary.md` —
  the STOP CONDITION and D1 statement (**read-only; NOT modified**)
- `tasks/completed/TASK-080-resolve-loadout-selection-surface.md` — the prior
  selection-surface resolution this decision extends/closes (read-only)
- `tasks/completed/TASK-081-resolve-in-progress-selection-state-and-runtime-boundary.md`
  — D2's resolution (read-only; **not re-decided**)
- `tasks/completed/TASK-078-client-lobby-scene-match-start.md` — the
  implementation whose surface this decision names (read-only)
- `AGENTS.md` §4 (conflict resolution), §16 (report, do not fix inline), §17
  (documentation change rule), §9 (anti-overengineering)

---

## Current State

### The decision is supplied; the documents do not yet state it

D1 asked: *which client surface owns the Pet / Card / Relic selection flow, and
where does the in-progress selection live?* TASK-079 recorded it as **NOT
RESOLVED**, with no document assigning the GDD.md §2 steps to any layer. The
human has now decided **Option A**.

`ARCHITECTURE.md` §2.2.3 (TASK-081) **already implements most of Option A** in
substance — its rule 1 makes the in-progress selection ephemeral `LobbyScene`
state and its rule 3 forbids a scene reaching the transport directly. What is
**not** yet stated as a decision anywhere is the *surface-ownership* claim
itself — that Phaser/LobbyScene is the chosen owner of the selection UI as
opposed to React — and `TDD.md` §2.1's React paragraph still lists "menus" as
React's without excluding this flow.

### Verified stale statements (must be synchronized)

**1. `TDD.md` §2.1 staging note (L103–111) — contradicts DONE work.**

```text
TDD.md L103-111 currently states:

  "The MVP implementation stages this lifecycle as BootScene → PreloaderScene →
   LobbyScene → BattleScene: MainMenuScene and ResultScene are deferred to their
   own tasks, so the implemented transition order passes from PreloaderScene
   directly into LobbyScene for now ..."

Verified repository reality:

  TASK-087 (DONE, FEATURE)  implemented ResultScene.ts
  TASK-090 (DONE, FEATURE)  implemented MainMenuScene.ts, changed
                            PreloaderScene.ts to target MainMenuScene, and
                            registered it in GameConfig.ts
  PreloaderScene.ts:83      this.scene.start('MainMenuScene')
  MainMenuScene.ts:80       this.scene.start('LobbyScene')
  GameConfig.ts:64          scene: [BootScene, PreloaderScene, MainMenuScene,
                            LobbyScene, BattleScene, ResultScene]

  TASK-090's Changed Files list does NOT include TDD.md — the note was left
  behind when the deferred scenes landed.
```

**2. `ARCHITECTURE.md` §2.2.1 (L231–233) — partially stale.**

```text
Current text: "Battle resolution, the client → server gameplay methods
(Swap, CardCast, PetSkillCast — SIGNALR_PROTOCOL.md §2), and reconnect/resync
snapshot recovery (SIGNALR_PROTOCOL.md §7, ADR-008) are not implemented yet."

Verified reality: Swap IS implemented end-to-end —
  BattleHub.Swap (BattleHub.cs:567), GameRuntime.requestAction
  (GameRuntime.ts:358, RUNTIME_ACTION_SWAP), client action path TASK-069.
  CardCast, PetSkillCast, and GetBattleState remain unimplemented
  (BattleHub.cs:389; GameRuntimeEvents.ts:267).
```

Both statements are **documentation bugs, not implementation bugs**: the code
matches the intent recorded as DONE by TASK-087/TASK-090/TASK-069, so the
documents are the stale side (`AGENTS.md` §17; `.ai/README.md` §18's default
assumption applies in the opposite direction only with evidence, and the DONE
task evidence is that evidence).

### What is still genuinely deferred

```text
MainMenuScene → no post-battle return navigation is documented
ResultScene   → RewardSummary member list still deferred
                (API_CONTRACTS.md §4 note 1, DATABASE.md §1)
PetState Tier/Star/Level → still staged (GAME_STATE.md §2.0.5.3)
CardCast / PetSkillCast / GetBattleState → still unimplemented
```

Scope item 3 bounds this task's edits to the **staging note's false clause
only**; it does not sweep the rest of §2.1.

---

## Scope

### In Scope

1. **Record the D1 decision in its canonical owner (`TDD.md` §2.1).** State that
   the MVP pre-battle selection flow (Pet, Card, Relic) is owned by **Phaser /
   `LobbyScene`**, that the in-progress selection is **temporary scene-local
   state**, and that **React remains the application/platform shell** and does
   not own this flow. Cite `ARCHITECTURE.md` §2.2.3 for the boundary mechanics
   rather than restating them (`documentation-change.md` §2 — no duplication).
2. **Reconcile `TDD.md` §2.1's React paragraph** (L147–154) so its "menus"
   entry does not contradict item 1 — by an explicit scope note, **not** by
   deleting React's menu ownership. Keep the Phaser/React split intact.
3. **Correct the false clause of the `TDD.md` §2.1 staging note.** The note must
   stop saying `MainMenuScene` and `ResultScene` "are deferred to their own
   tasks", because TASK-087/TASK-090 implemented them. Keep the note's accurate
   remainder (it records *why* the order was staged and that the diagram above
   remains the specified design). **This is a single targeted clause, not a §2.1
   rewrite.** Do not remove the lifecycle diagram, do not renumber sections, and
   do not touch any deferred item listed in "What is still genuinely deferred".
4. **Correct `ARCHITECTURE.md` §2.2.1's implementation-status sentence**
   (L231–233) so it no longer claims `Swap` is unimplemented, while it continues
   to state correctly that `CardCast`, `PetSkillCast`, and reconnect/resync
   snapshot recovery are not. Cite `SIGNALR_PROTOCOL.md` §2 and §7 as it already
   does.
5. **Verify `ARCHITECTURE.md` §2.2.3 needs no change** and record the
   verification: its rules 1–6 already state Option A's boundary. Edit it only
   if a statement is *factually wrong* against the decision, and then only
   minimally. The expected outcome is **no change** to §2.2.3.
6. **Optionally create `ADR-017`** — only if the executing agent determines the
   settled decision meets `docs/03-decisions/README.md` §2 **and** is not already
   fully owned by `TDD.md` §2.1 + `ARCHITECTURE.md` §2.2.3, per `adr-change.md`
   §1's Decision-vs-Implementation test. If created, update the
   `docs/03-decisions/README.md` §7 index and its Version line in the same
   change. **The default is to create no ADR.**
7. **Report — do not resolve — D2 and D3.** State in Completion Evidence that
   this task does not reopen them, and cite where each is already addressed
   (`ARCHITECTURE.md` §2.2.3 for D2; `DATABASE.md` §5 item 4 +
   `20260929152651_ProvisionPetCardRelicContentDefinitions` for D3).

### Out of Scope

- **Any source code** — nothing under `src/`. No `LobbyScene.ts`, `GameRuntime.ts`,
  `GameRuntimeEvents.ts`, `GameConfig.ts`, or any other client or backend file.
- **Any test change** — nothing under `tests/`.
- **Any TypeScript** — no interface, method signature, type, or payload.
- **Implementing anything.** No selection UI, no `SelectedLoadout` type, no port
  member, no runtime wiring, no endpoint. This task records a decision and edits
  prose.
- **Resolving D2** (in-progress selection-state ownership / `GameRuntimePort`
  responsibility) — already resolved by TASK-081; **not reopened**.
- **Resolving D3** (Pet/Card/Relic static content provisioning) — already
  addressed by TASK-082/084/085; **not reopened**, not designed.
- **Modifying TASK-079** (`tasks/blocked/`) — read-only. `AGENTS.md` §16.
- **Modifying TASK-080, TASK-081, TASK-078, TASK-075/076/077**, or any other
  completed task (`TASK_LIFECYCLE.md` §3 — completed tasks are immutable).
- **Modifying TASK-036** (`tasks/blocked/`) — not reopened.
- **Creating any implementation task** for the selection UI. The UI already
  exists (TASK-078); this task is documentation only.
- **Editing `GDD.md`** — it names the player-facing steps and assigns no layer;
  it is not the owner of the client boundary (`documentation-change.md` §3).
- **Editing `API_CONTRACTS.md`, `SIGNALR_PROTOCOL.md`, `REDIS_STATE.md`,
  `DATABASE.md`, `GAME_STATE.md`, `GAME_EVENTS.md`, or any `docs/01-game-design/`
  rule document.** No contract, protocol, schema, state, or rule changes.
- **Changing the scene lifecycle, scene registration, or scene order** in any
  way — this task corrects the *description* of an already-implemented order.
- **Inventing a Pet/Card/Relic identifier, selection UX mechanic, endpoint, or
  gameplay rule.**
- **Reinterpreting `API_CONTRACTS.md` §5.5/§5.6** — TASK-079's A6 preserved both;
  they stay preserved and unchanged.
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2.

---

## Acceptance Criteria

- [ ] `TDD.md` §2.1 explicitly states that the MVP Pet/Card/Relic loadout
      selection flow is owned by Phaser / `LobbyScene`, not React.
- [ ] `TDD.md` §2.1 explicitly states that the in-progress selection is
      temporary/scene-local state, and cites `ARCHITECTURE.md` §2.2.3 for the
      boundary rather than restating its rules.
- [ ] `TDD.md` §2.1's React responsibilities statement no longer contradicts the
      Phaser ownership in item 1 — React's shell/platform/menu ownership is
      **retained**, and the game-flow selection is excluded by an explicit note.
- [ ] `TDD.md` §2.1 no longer states that `MainMenuScene` and `ResultScene` are
      deferred; it agrees with `GameConfig.ts`'s registered scene order.
- [ ] `TDD.md` §2.1's lifecycle diagram (Boot → Preloader → MainMenu → Lobby →
      Battle → Result) is **byte-unchanged**.
- [ ] `ARCHITECTURE.md` §2.2.1 no longer states that `Swap` is not implemented;
      it still correctly states that `CardCast`, `PetSkillCast`, and
      reconnect/resync snapshot recovery are not.
- [ ] `ARCHITECTURE.md` §2.2.3 is either **unchanged**, or changed only where a
      statement was factually wrong against the decision — and the reason is
      recorded in Completion Evidence.
- [ ] No new duplication is introduced: the decision has exactly one canonical
      owner (`TDD.md` §2.1), and every other mention is a cross-reference
      (`documentation-change.md` §2).
- [ ] Either **no ADR is created**, with the §2 non-warrant reasoning recorded —
      or `ADR-017` is created with `docs/03-decisions/README.md` §7's index and
      Version line updated in the same change.
- [ ] `docs/00-overview/GDD.md` is **unmodified** (verified by hash).
- [ ] `docs/02-technical/API_CONTRACTS.md`, `SIGNALR_PROTOCOL.md`,
      `REDIS_STATE.md`, `DATABASE.md`, `GAME_STATE.md`, `GAME_EVENTS.md` are
      **unmodified**.
- [ ] Zero files under `src/` are modified.
- [ ] Zero files under `tests/` are modified.
- [ ] `tasks/blocked/TASK-079-*.md` is **byte-identical** before and after
      (SHA-256 recorded at pickup and re-verified at completion).
- [ ] `tasks/blocked/TASK-036-*.md` is unmodified.
- [ ] All `tasks/completed/` files are unmodified.
- [ ] D2 and D3 are reported as already addressed and **not** resolved,
      re-decided, or modified by this task.
- [ ] No API endpoint, request, response, or contract is changed or added.
- [ ] No SignalR method, payload, or event is changed or added.
- [ ] No Redis key, field, record, or behavior is changed or added.
- [ ] No PostgreSQL schema, entity, migration, or seed is changed or added.
- [ ] No gameplay rule, formula, balance value, or timing is changed.
- [ ] Documentation consistency checks pass (`quality/documentation-consistency`).
- [ ] Required review checks pass (`quality/review.md` §1), skipping
      code-behavior items a documentation-only change cannot exercise.
- [ ] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001).

### Explicit Constraints

```text
No gameplay.
No speculative architecture.
No client-authoritative state.
No undocumented API.
No undocumented SignalR method.
No undocumented Redis behavior.
No speculative PostgreSQL BattleState persistence.
No source code.
No test changes.
D2 and D3 are NOT resolved here.
TASK-079 is NOT modified.
```

---

## Affected Files & Areas

```text
[x] docs/02-technical/TDD.md          (the canonical owner — §2.1: the decision,
                                       the React reconciliation, the staging
                                       note's false clause)
[x] docs/02-technical/ARCHITECTURE.md (§2.2.1 L231–233 implementation-status
                                       sentence; §2.2.3 expected UNCHANGED —
                                       verify first)
[?] docs/03-decisions/ADR/ADR-017-*.md + docs/03-decisions/README.md §7 index
                                       (ONLY if an ADR is warranted; default
                                       is NO ADR — see Scope item 6)
[ ] docs/00-overview/GDD.md           (NO CHANGE — not the owner)
[ ] docs/02-technical/API_CONTRACTS.md, SIGNALR_PROTOCOL.md, REDIS_STATE.md,
    DATABASE.md, GAME_STATE.md, GAME_EVENTS.md (NO CHANGE)
[ ] docs/01-game-design/ (NO CHANGE)
[ ] src/ (none)
[ ] tests/ (none)
[x] tasks/backlog/TASK-099-*.md       (this file — Status and Completion
                                       Evidence only)
[ ] tasks/blocked/TASK-079-*.md       (NO CHANGE)
[ ] tasks/blocked/TASK-036-*.md       (NO CHANGE)
[ ] tasks/completed/ (NO CHANGE)
```

---

## Implementation Notes

- **Record, do not decide.** The decision is supplied. Do not re-open the
  Option A vs. React question, do not add a third option, and do not
  re-litigate TASK-079's D1 analysis. If the recorded decision appears to
  contradict an authoritative document, STOP per `AGENTS.md` §4 rather than
  choosing a side.
- **Cite, do not restate.** `ARCHITECTURE.md` §2.2.3 already owns the boundary
  mechanics (ephemeral state, transport isolation, `startBattle`, capabilities
  and not models). `TDD.md` §2.1 should state the *ownership decision* and point
  at §2.2.3 — not copy its six rules. A second copy is exactly the duplication
  `documentation-change.md` §2 forbids.
- **Owner discipline.** `documentation-change.md` §3: the owner is the document
  whose stated purpose answers the question. "What technical approach are we
  using to build this?" is `TDD.md`'s question, which is why the decision lands
  in `TDD.md` §2.1 and not in `ARCHITECTURE.md` (structure) or `GDD.md`
  (gameplay).
- **Reconcile, do not delete, React's menus.** `TDD.md` §2.1's React list is
  correct that React owns "HTML overlays, menus, settings". Option A does not
  contradict that; it clarifies that the *game-flow* Pet/Card/Relic selection is
  a Phaser interactive flow, not one of React's application menus. Prefer a
  scoping sentence over a rewrite — `ARCHITECTURE.md` §3 already assigns
  "HTML overlays & menus" to `GameShell / React Overlays`, and that stays true.
- **The staging note is the one place a rewrite is a trap.** Its false clause is
  "`MainMenuScene` and `ResultScene` are deferred to their own tasks". Its
  accurate content is the reason the order was staged and the statement that the
  diagram remains the specified design. Fix the clause; preserve the reasoning.
  `MainMenuScene`'s post-battle navigation and `ResultScene`'s `RewardSummary`
  are still genuinely deferred — do not let a careless edit imply otherwise.
- **Prefer past tense for closed staging.** Once the scenes exist, the note
  should read as a record of staging *that has since completed*, not as a
  standing statement that they are absent.
- **Verify `ARCHITECTURE.md` §2.2.3 before touching it.** Expected result: no
  change. TASK-081's rules 1–6 already state Option A's boundary. If you edit it,
  you must be able to name the specific statement that is factually wrong.
- **`Swap` is implemented; prove it from code, not from a task file.**
  `BattleHub.cs:567` (`Swap`), `GameRuntime.ts:358` (`requestAction`), and
  `RUNTIME_ACTION_SWAP` (`GameRuntimeEvents.ts:342`) are the evidence. Do not
  widen §2.2.1's sentence beyond the Swap correction.
- **Do not sweep.** `TDD.md` §2.1, `TASK_LIFECYCLE.md`, `tasks/README.md`, and
  `AGENTS.md` contain other stale cross-references (e.g. `TASK_LIFECYCLE.md`
  cites non-existent `core/*.md` paths relative to `tasks/`). Those are
  **out of scope**; report them in Completion Evidence instead (`AGENTS.md` §16).
- **TASK-079 is a guard file.** Record its SHA-256 at pickup and re-verify at
  completion. Do not edit it, do not move it, and do not change its Status —
  `TASK_LIFECYCLE.md` §3 provides no legal transition for a BLOCKED task whose
  blocker was resolved out-of-band.
- **The `.cs`-comment items are not part of this task.**
  `BattleState.cs` L162's "Still `0` at this stage" (ambiguous; TASK-098
  reported it) and `BattleStateJson.cs` L415–418 ("not yet supplied" — verified
  still accurate) are both out of scope. Neither is a `docs/` change, and
  `TASK_TYPES.md` §2 places source-comment changes under a code-change type.
- **Encoding safety.** Write these UTF-8 markdown files with a UTF-8-preserving
  writer. TASK-098's Process Note records typographic damage from a
  `Get-Content`/`Set-Content` round-trip; do not repeat it. Verify no BOM, no
  U+FFFD, and that em-dashes/en-dashes/arrows survive.

---

## Testing Requirements

This is a documentation-only change. Validation proves **the absence of a code
or contract change** and **the consistency of the edited prose**, not new
behavior.

### Required Verification

```text
[ ] Documentation consistency — re-read every edited passage together with the
                         document it cites (`ARCHITECTURE.md` §2.2.3, §2.2.1,
                         §3; `GAME_STATE.md` §2.3; `API_CONTRACTS.md` §3, §5.5,
                         §5.6) and confirm no contradiction survives and no
                         duplicated definition was introduced
                         (documentation-change.md §2/§3).
[ ] Code-agreement check — each corrected statement is verified against the
                         actual source it describes:
                           scene order  → GameConfig.ts:64, PreloaderScene.ts:83,
                                          MainMenuScene.ts:80
                           Swap         → BattleHub.cs:567,
                                          GameRuntime.ts:358
                           not impl.    → BattleHub.cs:389,
                                          GameRuntimeEvents.ts:267
[ ] Stale-claim sweep   — search the two edited documents for the corrected
                         claims ("deferred to their own tasks", "not implemented
                         yet") and confirm zero residual false statement about
                         the same fact.
[ ] Unmodified-guard verification — hash comparison at pickup vs. completion for:
                         docs/00-overview/GDD.md
                         docs/02-technical/API_CONTRACTS.md
                         docs/02-technical/SIGNALR_PROTOCOL.md
                         docs/02-technical/REDIS_STATE.md
                         docs/02-technical/DATABASE.md
                         docs/02-technical/GAME_STATE.md
                         docs/02-technical/GAME_EVENTS.md
                         tasks/blocked/TASK-079-*.md
                         tasks/blocked/TASK-036-*.md
                         tasks/completed/** (all)
[ ] Changed-file scope  — the changed-file set equals exactly the set declared
                         in Affected Files & Areas. `git status` shows zero
                         `src/` and zero `tests/` changes.
[ ] Docs-render check   — edited files re-read in full for broken section
                         numbering, broken cross-references, and intact code
                         fences / diagram blocks.
[ ] ADR-decision record — either the §2 non-warrant reasoning is written down,
                         or ADR-017 exists AND `docs/03-decisions/README.md` §7
                         index + Version line are updated together.
[ ] Integration tests    — N/A: no boundary is implemented or altered.
[ ] Gameplay scenarios   — N/A: no gameplay rule is derived, changed, or
                         exercised (AGENTS.md §6 maps gameplay scenarios to rule
                         docs; none is touched).
```

### Key Edge Cases

- **React's "menus" wording.** Removing it would break a correct statement;
  leaving it unqualified can be read as React owning the selection flow. Prefer
  an explicit scope sentence.
- **The staging note's two halves.** One clause is false (scenes deferred), the
  rest is accurate reasoning. A careless rewrite drops a correct record; a
  careless non-edit leaves the false claim.
- **`§2.2.1`'s sentence mixes one implemented verb with three unimplemented
  ones.** Correct only the `Swap` part; over-correcting would falsely claim
  `CardCast`/`PetSkillCast`/`GetBattleState` exist.
- **`Swap` "not implemented" may look true if read as "the whole battle
  resolution loop is unfinished."** It is not: `Swap` resolves end-to-end
  (`SwapExecution.cs`). Judge the sentence as written.
- **Duplication pull.** Because §2.2.3 already says much of Option A, there is a
  pull to copy it into `TDD.md`. Do not — cite it.
- **ADR pull.** Because D1 was an architecture question, there is a pull to
  create `ADR-017` reflexively. Create it only if §2's criteria are met and the
  decision is not already fully owned by the two technical documents.
- **Scope pull toward D2/D3.** Both are already addressed elsewhere; do not
  resolve, re-decide, or modify them.
- **Scope pull toward the `.cs` comments.** Out of scope and a different task
  type.
- **A "decision" that turns out to contradict an ADR.** STOP per `AGENTS.md`
  §4/§18 rather than editing around it.

---

## Stop Conditions

Universal `AGENTS.md` §20 stops always apply. Task-specific:

- **If the recorded Option A decision contradicts an existing authoritative
  document or an existing ADR (`ADR-003`, `ADR-011`): STOP per `AGENTS.md` §4** —
  report both sources and the conflict; do not silently pick a side.
- **If `TDD.md` §2.1 already fully states the decision at pickup: STOP** — report
  that no documentation change is required; do not manufacture one.
- **If `ARCHITECTURE.md` §2.2.3 turns out to contradict Option A rather than
  support it: STOP** — that is an architecture-conflict question, not a
  documentation edit.
- **If satisfying any criterion requires changing source code, a test, or a
  contract: STOP** — this task is documentation-only.
- **If satisfying any criterion requires modifying TASK-079, TASK-036, or any
  completed task: STOP** — report instead (`AGENTS.md` §16).
- **If satisfying any criterion requires resolving D2 or D3: STOP** — they are
  already addressed elsewhere and are explicitly out of scope.
- **If the decision would require a new endpoint, a new hub method, or an
  `API_CONTRACTS.md` change: STOP** — report a separate contract decision.
- **If the decision would require a new Pet/Card/Relic identifier or new content:
  STOP** — that is content authoring, not a boundary record.
- **If the decision would require a new gameplay rule or UX mechanic: STOP** per
  `AGENTS.md` §7.
- **If the task appears to require implementing the selection UI: STOP** — it
  already exists (TASK-078); this task records a decision about it.
- **If the two edited documents' changes cannot be kept to the enumerated
  clauses without a broader §2.1/§2.2 rewrite: STOP and report** — decompose per
  `tasks/README.md` §13 rather than expanding this task.
- **If a cross-document conflict is found that no ownership rule resolves: STOP**
  per `AGENTS.md` §4.
- **If the task exceeds 7 skills or crosses multiple uncoupled boundaries: STOP &
  decompose.**

---

## Completion Evidence

<!--
  TO BE COMPLETED BY THE EXECUTING AGENT after reaching DONE.
  Keep concise and factual.
-->

### Decision Recorded

```text
D1 — Loadout selection surface: OPTION A.
     Phaser / LobbyScene owns the loadout-selection UI and the temporary
     (in-progress) selection state. GameRuntime owns runtime coordination.
     The server validates and owns authoritative BattleState. React remains
     the application/platform shell.

Recorded in: <canonical owner path + section>
Cross-referenced from: <other mentions>
ADR: <none — §2 non-warrant reasoning below>  |  ADR-017 created
D2: NOT resolved here — already addressed by <citation>
D3: NOT resolved here — already addressed by <citation>
```

### Changed Files

- `<file path>` — <summary of change>

### Validation Results

```text
Documentation consistency   — <result>
Code-agreement check        — <result, with the source lines cited>
Stale-claim sweep           — <result>
Unmodified-guard hashes     — <result>
Changed-file scope          — <result>
Docs-render check           — <result>
```

```text
TASK-079 SHA-256 at pickup:      <hash>
TASK-079 SHA-256 at completion:  <hash>
Result: <IDENTICAL / DIFFERENT — explanation required if different>
```

### Items Reported, Not Fixed (`AGENTS.md` §16)

- <any further stale statement discovered, with location and impact>

### Server Authority & Scope Verification

- [ ] Confirmed zero client-authoritative gameplay logic introduced — no code
      was written at all
- [ ] Confirmed the decision keeps the server authoritative for
      `BattleState`/`PetState`/`EquippedCards[]`/`EquippedRelics[]`
- [ ] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
- [ ] Confirmed no source, test, migration, or seed file changed
- [ ] Confirmed no API endpoint, SignalR method, Redis behavior, or PostgreSQL
      change
- [ ] Confirmed D2 and D3 were not resolved, re-decided, or modified
- [ ] Confirmed TASK-079, TASK-036, and all completed tasks are unmodified
