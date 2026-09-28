# TASK-079 — Resolve Client Loadout Acquisition Boundary

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
-->

---

## Metadata

```text
Task ID:           TASK-079
Type:              ARCHITECTURE
Status:            BLOCKED
Risk:              HIGH
Priority:          CRITICAL
Primary Agent:     orchestrator
Supporting Agents: client, backend, review
Workflow:          architecture/architecture-change.md
                   (+ architecture/adr-change.md ONLY if the settled decision
                      warrants an ADR per docs/03-decisions/README.md §2),
                   gated by documentation/documentation-change.md
Skills:            discovery/documentation-discovery, discovery/impact-analysis,
                   quality/architecture-conformance,
                   quality/documentation-consistency,
                   quality/scope-validation
Dependencies:      TASK-075, TASK-076, TASK-077 (context only — all DONE);
                   TASK-078 (blocked by this task; NOT modified here)
```

---

## Objective

Determine and record the **smallest authoritative contract** answering where the
client obtains the selected Pet, the selected 3 Basic Cards, and the selected 3–5
Relic instances that `POST /api/battle/start` requires, which layer owns that
in-progress selection, and which already-documented boundary `LobbyScene` uses to
reach it — without making Phaser or the client authoritative and without
inventing identifiers, endpoints, or provisioning.

This task resolves the **loadout acquisition boundary only**. Static content
provisioning and the scene-order discrepancy are identified as separate
dependencies and are **not** solved here.

---

## Problem Statement

`tasks/backlog/TASK-078-client-lobby-scene-match-start.md` is **NOT READY**. A
read-only implementation-readiness audit found that `LobbyScene` is required to
construct a `BattleStartRequest`, but the client-side boundary that supplies
three of its four members is undocumented:

```text
LobbyScene
    ↓
selected Pet          → NO AUTHORITATIVE CONTRACT
selected Cards        → NO AUTHORITATIVE CONTRACT
selected Relics       → NO AUTHORITATIVE CONTRACT
    ↓
BattleStartRequest { petId, bossId, cardLoadout, relicLoadout }
```

`bossId` is already resolved by existing documentation (`BOSS_RULES.md` §6.4 —
`"boss-hoa-long"`; the row is provisioned by TASK-053) and is only confirmed
here.

Why the existing pieces do not already answer it:

```text
TDD.md §2.1            defines LobbyScene in one line — "Battle preparation,
                       loadout review, and match start trigger" — and assigns
                       no acquisition mechanism.
ARCHITECTURE.md §2.2.1 confines GameRuntime to "connection state, runtime
                       lifecycle, server event subscription and scene lifecycle
                       coordination only" (rule 3), and rule 1 forbids scenes
                       reaching the transport.
GameRuntimePort        has six members; none is a collection read, a loadout,
                       or a selection.
API_CONTRACTS.md §5.6  forbids reading equip state from the collection
                       endpoints; §5.5 states no ordering is defined.
```

Consequence: TASK-078's Implementation Notes instruct an implementer to supply
"3 Basic Card IDs" and "3–5 Relic IDs" with no source. Following them would mean
inventing architecture or content, which `AGENTS.md` §7 forbids and TASK-078's own
Stop Conditions already prohibit.

---

## Authoritative References

- `docs/02-technical/TDD.md` §2.1 — Scene lifecycle and per-scene ownership
  (`LobbyScene`), React/Phaser responsibility split, Service Boundaries,
  Discord/auth architecture
- `docs/02-technical/ARCHITECTURE.md` §1, §2.2, §2.2.1 — Frontend layers,
  `GameRuntime` coordination boundary and rules 1–6 ("scenes depend on the
  runtime port, never on the transport"; "the runtime coordinates; it does not
  compute"), `services/api/` isolation
- `docs/02-technical/API_CONTRACTS.md` §3 (`POST /api/battle/start` request and
  its validation), §1, §5.1–§5.4 (collection read endpoints), §5.5 (list
  semantics), §5.6 (no equip/loadout state)
- `docs/02-technical/GAME_STATE.md` §2.3 — the server-authoritative
  battle-scoped snapshot (`PetState.EquippedCards[]` / `EquippedRelics[]`)
- `docs/02-technical/DATABASE.md` §1, §2, §3, §5 item 4 — ownership vs
  battle-scoped equip; the recorded open content-provisioning question
- `docs/01-game-design/PET_RULES.md` §2 — "exactly one Pet is selected as
  'active' per battle" (the rule; not a client mechanism)
- `docs/01-game-design/CARD_RULES.md` §1, §2 — Basic Card category and effect
  names; the document states it defines no values (§1 item 5)
- `docs/01-game-design/RELIC_RULES.md` §2 — ownership vs battle-scoped equip
- `docs/01-game-design/BOSS_RULES.md` §6, §6.4 — canonical Boss technical
  Identity
- `docs/00-overview/MVP_SCOPE.md` §1 (content counts), §2, §4 — scope
  classification; unlisted = FUTURE
- `docs/03-decisions/README.md` §1, §2, §4, §5, §7, §8 — when an ADR is
  warranted, numbering, and open-item registration
- `docs/03-decisions/ADR/ADR-001-server-authoritative-battle.md` — server
  authority
- `docs/03-decisions/ADR/ADR-003-phaser-game-runtime.md` — Phaser scene
  lifecycle and runtime boundary
- `docs/03-decisions/ADR/ADR-011-player-owner-pet-combat.md` — Player = owner,
  Pet = combat character; ownership vs battle-scoped equip (item 4)
- `docs/03-decisions/ADR/ADR-012-player-level-pet-level-ownership.md` — items 3,
  4, 6: Relic/Card ownership vs battle equip; MVP scope closure
- `docs/03-decisions/ADR/ADR-014-battle-state-player-identity.md` —
  `PetState.PetId` is the owned Pet instance
- `docs/03-decisions/ADR/ADR-015-application-session-authentication-contract.md`
  — the application session mechanism (already implemented; not reopened)

**Verified filenames.** The ADR governing loadout/ownership boundaries is
`docs/03-decisions/ADR/ADR-011-player-owner-pet-combat.md`.
`ADR-011-loadout-and-ownership-boundaries.md` **does not exist**; TASK-078 cites
that non-existent filename and this task must not reproduce it. The highest
existing ADR is `ADR-016`, so `ADR-017` is the next free number
(`docs/03-decisions/README.md` §4 — numbers are never reused).

---

## Current State

```text
Client (DONE — evidence only, not modified)
  ApiService.getPets / getPet / getCards / getRelics      TASK-075
  ApiService.startBattle / getBattleResult                TASK-076
  GameRuntime.startBattle(request) — implemented + tested TASK-077
  GameRuntime implements GameRuntimePort                  (GameRuntime.ts:93)
  readRuntime(scene) → concrete GameRuntime | null        RuntimeRegistry.ts:24

Client (the gap)
  GameRuntimePort has SIX members, none of them loadout-related:
  getState · getBattleState · onRuntimeEvent · onBattleEvents
  · onBattleState · requestAction
  TASK-077:116 recorded the precedent "Do NOT extend GameRuntimePort"
  because no scene called startBattle at that time. TASK-078 needs the port
  extended — that tension is what B1 must resolve.

Documentation (the gap)
  No document defines where LobbyScene obtains a selected Pet, 3 Basic Cards,
  or 3–5 Relic instances, nor which layer owns the in-progress selection.

Content (separate dependency — NOT solved here)
  BossDefinition     provisioned (migration 20260926151112, TASK-053)
  PetDefinition / CardDefinition / RelicDefinition
                     no provisioning mechanism defined
                     (DATABASE.md §5 item 4 records this as open)
```

TASK-078 is `Status: BACKLOG` and is the task this decision unblocks. TASK-078 is
**not modified** by this task.

---

## Decision Questions

Each question must be **resolved** or **explicitly deferred** with a named
owner. Every resolution must cite the owning document that will carry it. No
resolution may invent content, identifiers, endpoints, or gameplay rules.

### A. Loadout

```text
A1  Where does the client obtain the SELECTED Pet instance (petId)?
A2  Where does the client obtain the SELECTED 3 Basic Cards (their
    CardDefinitionId values)?
A3  Where does the client obtain the SELECTED 3–5 Relic instances (their
    RelicInstanceId values, whose array order is equip slot order)?
A4  Which layer owns the IN-PROGRESS selected loadout (the selection made
    before battle start, distinct from owned content and distinct from the
    server's BattleState snapshot)?
A5  Which already-documented boundary does LobbyScene use to access that
    state?
A6  Are API_CONTRACTS.md §5.5 and §5.6 preserved unchanged? Default
    assumption is YES. If the contract genuinely cannot be satisfied without
    changing them, STOP — that is a separate API contract decision, not
    something to change inside this task.
```

### B. Runtime

```text
B1  Does the loadout contract require GameRuntimePort to expose new
    capability? Evaluate each candidate responsibility INDIVIDUALLY and label
    it "required" / "not required" / "belongs elsewhere":

      get collection · get owned pets · get owned cards · get owned relics
      · get selected loadout · set selected loadout · start battle

    Do not expose all of them by default. Reconcile explicitly with
    TASK-077:116's recorded precedent against extending the port.
B2  For each capability labelled "required", state the RESPONSIBILITY only —
    what the runtime owns and exposes, and what it must never compute.
B3  Do the existing architecture documents (TDD.md §2.1 and/or
    ARCHITECTURE.md §2.2.1) need clarification to state the scene/runtime
    loadout boundary? Note that §2.2.1 rule 1 names SignalR only; whether it
    also covers services/api/ and fetch is currently unstated in docs/ and
    must be decided here.
```

### C. Content

```text
C1  Is valid Pet/Card/Relic static-content provisioning a SEPARATE
    prerequisite for TASK-078 to be implementable?
C2  Which document and/or follow-up task should own that prerequisite?
```

C1/C2 are classification questions only. Do **not** solve them by designing or
implementing provisioning behaviour.

### D. Boss

```text
D1  Confirm the existing documented Boss source/default (BOSS_RULES.md §6.4;
    the provisioned BossDefinition rows). Confirmation only — no redesign, no
    new Boss content, no Boss selection UI.
```

### E. Scene order

```text
E1  Does the scene-order discrepancy (docs list MainMenuScene between
    PreloaderScene and LobbyScene; TASK-078 skips it) DIRECTLY block loadout
    acquisition?
E2  If it does not, record it as a separate follow-up dependency and do not
    resolve it here.
```

---

## Scope

### In Scope

- Read-only discovery across the documents listed above.
- The architectural decision on the **loadout acquisition boundary**.
- Responsibility ownership for the **in-progress selected loadout**.
- Runtime/scene boundary clarification **only if directly required** by the
  loadout decision.
- Documentation updates recording the resolved contract, in the single
  canonical owner per `docs/AGENTS.md` §2.
- An ADR **only if** the settled decision satisfies `docs/03-decisions/README.md`
  §2 and `architecture/adr-change.md` §1–§2.
- Identifying static content provisioning as a separate dependency, with a named
  owner.
- Reporting the impact on TASK-078's readiness (report-only).

### Out of Scope

- **Any source code** — nothing under `src/`.
- **Any test change** — nothing under `tests/`.
- **Any TypeScript** — no interfaces, method signatures, or payload types.
- `LobbyScene`, `GameRuntime`, `GameRuntimePort`, or API endpoint implementation.
- **New API endpoints.**
- **Any Pet/Card/Relic identifier** (`pet-001`, `card-001`, `relic-001`, …) or
  any conversion of `MVP_SCOPE.md` / `CARD_RULES.md` / `RELIC_RULES.md` /
  `PET_RULES.md` display names into technical IDs.
- **Content provisioning implementation**: `HasData`, EF migrations, startup
  seeders, fixtures, content loaders, and Player/Pet/Card/Relic bootstrap.
- New Boss identifiers, Boss selection UI, or Boss provisioning.
- `MainMenuScene`, scene registration, or scene transitions.
- SignalR, Redis, or `BattleState` changes.
- Gameplay, Match-3, Combat, XP, Rewards, Passive, and Pet/Card/Relic gameplay.
- OAuth credential changes; **TASK-036 is not reopened or modified**.
- **Modifying TASK-078** (`AGENTS.md` §16 — report, do not fix inline).
- Modifying `tasks/completed/` (immutable, `TASK_LIFECYCLE.md` §3).
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2.

---

## Required Documentation Changes

To be determined by the decisions above; the executing agent records the actual
set in Completion Evidence. Candidate owners (one concept, one owner —
`docs/AGENTS.md` §2):

```text
Loadout acquisition + scene/runtime boundary   → TDD.md §2.1 and/or
                                                  ARCHITECTURE.md §2.2.1
Server-authoritative scene/loadout boundary    → an ADR (ADR-017) ONLY if the
                                                  settled decision warrants one
                                                  per docs/03-decisions/README.md
                                                  §2 / adr-change.md §1–§2
Content provisioning dependency                → recorded as a follow-up
                                                  dependency only; NOT resolved,
                                                  NOT designed here
Scene order                                    → recorded as a separate
                                                  follow-up only; NOT resolved
```

If an ADR is created, `docs/03-decisions/README.md` §7's index table must be
updated in the same change (`adr-change.md` §2). No new document may restate
content owned by another (`documentation-change.md` §2).

---

## Acceptance Criteria

- [ ] Loadout acquisition source for **Pet** is explicitly documented, or
      deferred with a named follow-up.
- [ ] Loadout acquisition source for **Cards** is explicitly documented, or
      deferred with a named follow-up.
- [ ] Loadout acquisition source for **Relics** is explicitly documented, or
      deferred with a named follow-up.
- [ ] Ownership of the **in-progress selected-loadout** state is explicitly
      documented, and is kept distinct from owned content and from the
      server's `BattleState` snapshot.
- [ ] `LobbyScene`'s architectural boundary is explicitly documented.
- [ ] `GameRuntimePort` responsibility is explicitly **accepted**, **rejected**,
      or **deferred** — with each candidate responsibility in B1 individually
      labelled required / not required / belongs elsewhere.
- [ ] No TypeScript interface, method signature, or payload type is invented.
- [ ] `API_CONTRACTS.md` §5.5 and §5.6 are explicitly preserved, or a separate
      contract decision is reported as required (the latter is a STOP).
- [ ] No Pet/Card/Relic identifier is invented anywhere in this task's output.
- [ ] Static content provisioning is explicitly identified as a **separate
      dependency** with a named owner, and is not solved here.
- [ ] The existing Boss contract is confirmed; no Boss content is added.
- [ ] The scene-order discrepancy is either proven irrelevant to this task or
      recorded as a separate follow-up.
- [ ] `tasks/backlog/TASK-078-*.md` is **byte-identical** before and after
      (SHA-256 recorded at pickup and re-verified at completion).
- [ ] `tasks/blocked/TASK-036-*.md` is unmodified.
- [ ] All `tasks/completed/` files are unmodified.
- [ ] Zero files under `src/` are modified.
- [ ] Zero files under `tests/` are modified.
- [ ] No migration, seed, or provisioning implementation is added.
- [ ] No API endpoint is added.
- [ ] No gameplay rule is changed.
- [ ] Documentation ownership is unambiguous; no duplicated source-of-truth
      definition is introduced.
- [ ] Documentation consistency checks pass.
- [ ] Required review checks pass (`quality/review.md` §1).
- [ ] TASK-078 readiness impact is reported **without modifying TASK-078**.
- [ ] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001).

---

## Affected Files & Areas

```text
[ ] src/backend/ (none)
[ ] src/frontend/client/ (none)
[ ] tests/ (none — documentation/architecture-only change; existing guard
            suites must still pass UNMODIFIED)
[x] docs/ (documentation updates — TDD.md and/or ARCHITECTURE.md; a new
           ADR + docs/03-decisions/README.md index ONLY if warranted)
[x] tasks/backlog/TASK-079-*.md (this file — Status field only)
[ ] tasks/backlog/TASK-078-*.md (NO CHANGES — report to its re-audit instead)
[ ] tasks/blocked/TASK-036-*.md (NO CHANGES)
[ ] tasks/completed/ (NO CHANGES)
```

---

## Implementation Notes

- **Keep three concepts separate** (`AGENTS.md` §12/§13; ADR-011 item 4). The
  contract must not conflate:

  ```text
  Owned content            Player owns Pet/Card/Relic (persistent, DATABASE.md §2)
      ≠
  Selected loadout         Player selected them for THIS battle (in-progress,
                           unpersisted client-side selection)
      ≠
  BattleState snapshot     Server accepted the loadout and copied it into
                           PetState.EquippedCards[] / EquippedRelics[]
                           (server-authoritative, GAME_STATE.md §2.3)
  ```

  The client may **select/request** a loadout; it never authoritatively
  establishes `PetState`, `EquippedCards`, or `EquippedRelics`.

- **Preserve the documented layering** unless the decision explicitly changes it:

  ```text
  React → GameShell → Phaser Scene → GameRuntime → API / SignalR
  ```

  `LobbyScene → SignalRService` is forbidden today
  (`ARCHITECTURE.md` §2.2.1 rule 1). Whether `LobbyScene → services/api/` is
  also forbidden is **currently unstated in docs/** — resolve that as B3 rather
  than guessing.

- **Do not reinterpret collection ordering as loadout ordering.**
  `API_CONTRACTS.md` §5.5 states ordering is undefined and clients must not rely
  on any order; §5.6 forbids reading equip state from the collection endpoints.
  Their default is preservation.

- **Do not invent an endpoint because it is convenient.** `API_CONTRACTS.md` §1
  states its endpoint list is the complete REST surface of a battle. If the
  loadout contract appears to need a new endpoint, STOP — that is a separate API
  contract decision.

- **Precedent for this task's shape.** `TASK-038`, `TASK-039`, `TASK-045`,
  `TASK-046`, `TASK-051`, and `TASK-056` are each a documentation/decision task
  created because a readiness audit found a missing contract. Follow their
  structure: read-first, decide, record in the canonical owner, report the
  dependent task's readiness impact without editing it.

- **ADR discipline.** `adr-change.md` §2 and `docs/03-decisions/README.md` §5
  forbid creating an ADR for a decision nobody has actually made, and §1 states
  ADRs are historical records, not design proposals. Create `ADR-017` only if
  the decision this task settles is genuinely architectural
  (`README.md` §2) and confirmed by this task's own documentation update. If it
  is fully owned by `TDD.md` / `ARCHITECTURE.md`, record it there and create no
  ADR.

- **`readRuntime()` returns the concrete `GameRuntime`**, not the port type
  (`RuntimeRegistry.ts:24`). That is part of what B1/B3 must decide.

- **Enforcement note.** `RuntimeBoundaries.test.ts` checks a hardcoded
  three-file scene list and forbids only SignalR patterns, so TASK-078's
  "no `fetch`/`ApiService` from `LobbyScene`" criterion currently has no test
  enforcement. It also forbids the literal terms `boss` and `relic` inside
  `GameRuntimeEvents.ts`, which constrains where loadout types could live.
  Whether to extend the boundary rule is a B3 question.

- **Do not resolve conflicts silently.** Where two documents disagree, report
  per `AGENTS.md` §4 rather than choosing the easier reading.

---

## Testing Requirements

### Required Verification

```text
[ ] Unit tests         — N/A: this task adds no code (core/validation.md §2
                         depth for a documentation/architecture-only change).
[ ] Integration tests  — N/A: no boundary is implemented.
[ ] Gameplay scenarios — N/A: no gameplay rule is derived, changed, or
                         exercised (AGENTS.md §6 maps gameplay scenarios to
                         rule docs; none is touched here).
[ ] Documentation consistency — each updated document re-read together with its
                         referencing documents; no duplicated definition
                         introduced (documentation-change.md §2).
[ ] Architecture consistency  — the documented boundary matches ARCHITECTURE.md
                         §2.2.1's rules and introduces no layer inversion.
[ ] Task guard checks  — TASK-078 SHA-256 identical; TASK-036 and all completed
                         tasks unmodified; `git status` shows no `src/` or
                         `tests/` change.
[ ] Scope/changed-file verification — the changed-file set equals the set
                         declared in Required Documentation Changes.
[ ] Existing guard suites — the client and backend suites still pass
                         UNMODIFIED, proving no source file was touched.
```

### Key Edge Cases

- A decision question that cannot be answered from existing documents → mark it
  explicitly as requiring human/product input; do not guess.
- The existing collection endpoints tempting a "read the list and take the first
  item" shortcut → preserve §5.5/§5.6 unless a separate contract decision is
  reported.
- An answer that would require an acquisition or gacha flow → STOP;
  `MVP_SCOPE.md` §2 excludes Gacha.
- A resolution that would require a new endpoint or changed API behaviour →
  STOP and report a separate API contract decision.
- Content provisioning proving unresolved → record it as a separate dependency;
  do not design it.
- TASK-078 criteria that depend on the outcome → report the delta to TASK-078's
  re-audit; never edit TASK-078.

---

## Stop Conditions

- If the existing documentation already completely defines the loadout
  acquisition boundary: STOP — report that no decision task is required, do not
  manufacture one.
- If multiple authoritative documents conflict and no ownership rule resolves
  the conflict: STOP per `AGENTS.md` §4.
- If human/product input is required to decide loadout behaviour: STOP and mark
  accordingly using the repository's decision-input convention.
- If the decision requires inventing Pet/Card/Relic content or any identifier:
  STOP — that is content authoring, not a boundary decision.
- If the decision requires a new gameplay rule: STOP per `AGENTS.md` §7.
- If the decision requires changing API behaviour not already authorized, or
  implementing a new API endpoint: STOP — report a separate API contract
  decision.
- If the decision requires provisioning implementation: STOP — provisioning is a
  separate dependency identified only.
- If the decision requires a new architectural decision beyond this boundary:
  STOP.
- If scene-order ambiguity becomes a product decision rather than a
  documentation clarification: STOP and record it as a separate follow-up.
- **If more than one independent architectural dependency must be solved before
  TASK-078 can become READY: STOP. Do not expand TASK-079. Create/report the
  separate dependency instead.**
- If satisfying any criterion requires implementing `LobbyScene`, changing the
  scene list, or writing TypeScript: STOP per `AGENTS.md` §8/§16.
- If satisfying any criterion requires modifying TASK-078 or a completed task:
  STOP — report to the re-audit instead.
- If the task exceeds 7 skills or crosses multiple uncoupled architectural
  boundaries: STOP & decompose.

---

## STOP CONDITION — EXECUTED (Independent Dependency Rule)

```text
STOP CONDITION

Problem:
TASK-079 cannot resolve the loadout acquisition boundary, because that
boundary is not one missing contract. Resolving it requires deciding at
least THREE independent product/architecture questions, none of which
any existing document answers and none of which TASK-079 is authorized
to decide. TASK-079's own Independent Dependency Rule and Stop Conditions
therefore fire, and the task halts at core/context-discovery.md §3.

Relevant sources:
  docs/00-overview/GDD.md:63 — "Enter Battle → Choose Pet → Equip Cards
    → Equip Relics → Start Battle" names the user-facing steps but assigns
    them to NO client layer, scene, component, or state owner.
  docs/02-technical/TDD.md §2.1:104 — LobbyScene = "Battle preparation,
    loadout review, and match start trigger" (scope label only).
  docs/02-technical/TDD.md §2.1:124-129 — React owns "menus"; React does
    NOT own the Phaser game loop. Collections are not assigned to either.
  docs/02-technical/ARCHITECTURE.md §2.2.1 rule 3:183-186 — GameRuntime
    owns "connection state, runtime lifecycle, server event subscription
    and scene lifecycle coordination only".
  docs/02-technical/ARCHITECTURE.md §3:354-355 — GameRuntime
    "Coordinates Phaser, transport & runtime state; no gameplay".
  docs/02-technical/ARCHITECTURE.md §3:359 — GameShell / React Overlays
    own "HTML overlays & menus" (no selection responsibility stated).
  docs/02-technical/API_CONTRACTS.md §5.5:796 and §5.6:801-808 — no
    ordering; clients must not read equip state from collection endpoints.
  docs/01-game-design/RELIC_RULES.md §2.1 item 3:75-78 — "The client
    supplies the selection only" — the CLIENT's source is never defined.
  docs/02-technical/DATABASE.md §5 item 4:768-772 — Pet/Card/Relic
    provisioning "none is defined here; each remains open".

Conflict / missing information:
Three independent, separately-decidable gaps — not one boundary decision:

  D1. SELECTION UX / FLOW OWNERSHIP (product + architecture).
      GDD.md:63 requires Choose Pet / Equip Cards / Equip Relics as player
      steps. No document states WHICH surface performs them: a Phaser
      LobbyScene, React HTML UI, or both. TDD.md §2.1 gives React "menus"
      and Phaser "battle preparation"; ARCHITECTURE.md §3:359 gives React
      overlays "menus". These overlap and neither is authoritative for
      loadout selection. This is a product decision (which surface the
      player uses) with an architectural consequence.

  D2. IN-PROGRESS SELECTION STATE OWNERSHIP (architecture).
      Where the in-progress selection lives before battle start is stated
      nowhere. ARCHITECTURE.md §2.2.1 rule 5:191-196 forbids gameplay
      state in client runtime state; §5.5 keeps client state minimal and
      excludes state libraries. GameRuntimePort has no such member. No
      document names an owner — and choosing one changes what
      GameRuntimePort must expose (TASK-079 question B1), so the two
      cannot be decided independently.

  D3. STATIC CONTENT PROVISIONING (product + persistence).
      Independently recorded open by DATABASE.md §5 item 4. Even with D1
      and D2 settled, no valid Pet/Card/Relic rows exist to select in any
      environment. TASK-079 is explicitly forbidden to solve this.

Proposed resolution:
Do not expand TASK-079. The three gaps are independent dependencies, each
requiring its own decision task with its own owner document:

  D1 → SELECTION SURFACE / LOADOUT FLOW OWNERSHIP decision task
       (candidate owner: TDD.md §2.1 + ARCHITECTURE.md §2.2/§3; product
       input required — which surface the player uses)
  D2 → IN-PROGRESS SELECTION STATE OWNERSHIP + GameRuntimePort
       responsibility decision task (candidate owner: ARCHITECTURE.md
       §2.2.1; probably ADR-017, since it is an ownership-boundary and
       authoritative-state decision)
  D3 → PET/CARD/RELIC STATIC CONTENT PROVISIONING decision task
       (candidate owner: DATABASE.md §5 item 4; already recorded as open
       by TASK-045 §6 issue 1)

TASK-079 is stopped without expanding scope, without creating those
follow-up tasks (report-only; the repository workflow does not require
the current task to create them), and without guessing any product
decision.

Waiting for:
A human/product decision on D1 (which client surface owns loadout
selection), after which D2 and D3 can be scoped as their own decision
tasks. TASK-078 remains NOT READY and untouched.

---

## Dependencies

```text
TASK-075  Client collection read service          DONE — context
TASK-076  Client battle API service               DONE — context
TASK-077  Client battle-start orchestration        DONE — context; records the
          un-extended GameRuntimePort (":116") and defers "Loadout selection
          UI, scene navigation, and scene creation"
TASK-078  Client LobbyScene match-start trigger     BACKLOG — NOT READY,
          blocked by this task; NOT modified here
TASK-036  Discord credential secret hygiene         BLOCKED — not reopened,
          not modified; NOT the general authentication prerequisite
TASK-053  Provision BossDefinition rows             DONE — the only provisioned
          content today

Separate dependencies identified (owned elsewhere, NOT solved here):
  - Pet/Card/Relic static content provisioning   (C1/C2 classification only)
  - MainMenuScene / scene-order reconciliation   (E1/E2 classification only)
```

---

## Completion Evidence

<!--
  TO BE COMPLETED BY THE EXECUTING AGENT after reaching DONE.
  Keep concise and factual.
-->

### Decisions Recorded

**Outcome: TASK-079 STOPPED — see the STOP CONDITION section above.** Per the
Independent Dependency Rule, the investigation established that TASK-078 depends
on three independent dependencies, so TASK-079 did not resolve the boundary and
did not modify any authoritative document.

#### A — Loadout

```text
A1 selected Pet      NOT RESOLVED — blocked by D1 (selection surface) and D2
                     (selection-state owner). Owned content is readable
                     (API_CONTRACTS.md §5.1, TASK-075), but which layer
                     performs and holds the *selection* is undocumented.
A2 selected Cards    NOT RESOLVED — same block. GDD.md:63 names "Equip Cards"
                     as a player step; no document assigns it to a surface.
A3 selected Relics   NOT RESOLVED — same block. RELIC_RULES.md §2.1 item 3
                     states "The client supplies the selection only" and
                     never defines the client-side source.
A4 in-progress owner NOT RESOLVED — no owner named in any document;
                     ARCHITECTURE.md §2.2.1 rule 5 and §5.5 constrain where
                     it may live but do not assign it. (Dependency D2.)
A5 LobbyScene
   boundary          NOT RESOLVED — depends on B1, which depends on D2.
A6 §5.5 / §5.6       PRESERVED — no change proposed. Evidence: §5.5 states
                     "ordering none defined — clients must not rely on any
                     order"; §5.6 states equip state is battle-scoped and
                     "Clients must not read equip state from these
                     endpoints." No authoritative evidence contradicts either,
                     so both remain in force unchanged.
```

#### B — Runtime (`GameRuntimePort` candidate capabilities)

Individual labels, as required. All loadout-related capabilities are **deferred**
rather than decided, because their answers depend on D1/D2 and on A4.

```text
get collection        deferred  — no document defines a "collection" runtime
                                concept; §2.2.1 rule 3 confines GameRuntime to
                                connection/lifecycle/event coordination.
get owned pets        deferred  — blocked by D2.
get owned cards       deferred  — blocked by D2.
get owned relics      deferred  — blocked by D2.
get selected loadout  deferred  — blocked by D2 and D1.
set selected loadout  deferred  — blocked by D2 and D1; also an ownership
                                decision (ADR-017 candidate), not decidable
                                inside TASK-079.
start battle          belongs elsewhere / already satisfied — implemented on
                                the concrete GameRuntime by TASK-077
                                (GameRuntime.ts:466). Whether it belongs on the
                                PORT is a B1 question blocked by D2, so it is
                                NOT added here.
```

No capability was labelled `required`, so no responsibility statement is owed and
none is written. No TypeScript, signature, or payload type was produced.

#### C — Content

```text
C1  Is Pet/Card/Relic provisioning a separate prerequisite?  YES.
C2  Owner                                                    DATABASE.md §5
    item 4 already records it as open ("none is defined here; each remains
    open (TASK-045 §6 issue 1). No HasData, seed, or startup loader exists
    for any of them"). It is a separate dependency (D3) and was NOT designed
    or implemented here.
```

#### D — Boss

```text
D1  CONFIRMED. BOSS_RULES.md §6/§6.4:274 defines the canonical technical
    Identity "boss-hoa-long" (Hỏa Long), and the row is provisioned by
    migration 20260926151112_ProvisionBossDefinitions (TASK-053).
    BOSS_RULES.md also records three content-defined MVP Bosses
    ("boss-hoa-long", "boss-thuy-ma", "boss-moc-yeu"). No Boss content was
    added, and no Boss selection mechanism was introduced. Whether the MVP
    Lobby hardcodes one Boss or offers selection is a product question that
    belongs to the D1 selection-surface decision; it was NOT decided here.
```

#### E — Scene order

```text
E1  Does the MainMenu discrepancy directly block loadout acquisition?  NO.
    The loadout gap is identical whether the chain is
    Preloader→MainMenu→Lobby→Battle or Preloader→Lobby→Battle; the missing
    contract is the selection source, not the scene that hosts it.
E2  Recorded as a SEPARATE follow-up concern. Not resolved here.
    Sources: TDD.md §2.1:87-99 and ARCHITECTURE.md §2.2:137-138 place
    MainMenuScene between PreloaderScene and LobbyScene; TASK-078:58-59
    skips it. ARCHITECTURE.md §1:68-71 also lists MainMenuScene.ts /
    LobbyScene.ts / ResultScene.ts as existing files although none exist
    (stale tree).
```

### Separate Dependencies Identified

```text
D1  Loadout selection surface / flow ownership
    Owner: undecided — candidate TDD.md §2.1 + ARCHITECTURE.md §2.2/§3;
           REQUIRES HUMAN PRODUCT INPUT (which surface the player uses).
    GDD.md:63 requires the steps; no document assigns them.

D2  In-progress selection-state ownership + GameRuntimePort responsibility
    Owner: undecided — candidate ARCHITECTURE.md §2.2.1; likely ADR-017.
    Blocks TASK-079 questions A4, A5, B1, B2, B3.

D3  Pet/Card/Relic static content provisioning
    Owner: DATABASE.md §5 item 4 (already recorded open by TASK-045 §6
           issue 1). Classification only — not designed here.
```

Report-only, per the Independent Dependency Rule: none of these follow-up tasks
was created or modified by TASK-079.

### Changed Files

- `tasks/active/TASK-079-resolve-client-loadout-acquisition-boundary.md` —
  Status `BACKLOG` → `IN PROGRESS` → `BLOCKED`; STOP CONDITION report, decision
  analysis, and completion evidence recorded. Moved `backlog/` → `active/` →
  `blocked/`.

No authoritative document was changed. No ADR was created.

### Validation Results

```text
Documentation consistency   — PASS. No document was edited, so no duplicate
                              definition or source-of-truth conflict was
                              introduced (AGENTS.md §4; documentation-change.md
                              §2). Every claim above cites an existing
                              document + section; no content was restated into
                              a new owner.
Architecture consistency    — PASS. No boundary was changed; the STOP preserves
                              ARCHITECTURE.md §2.2.1 rules 1–6 and ADR-001
                              unchanged, and introduces no client-authoritative
                              state.
Task guards                 — PASS. TASK-078 SHA-256 identical (below);
                              TASK-036 unmodified; tasks/completed/ unmodified.
Changed-file scope          — PASS. The only file changed is this task file.
                              `src/` and `tests/` diff count unchanged from the
                              pre-existing baseline of 24 entries (none of them
                              touched by this task).
Review checklist            — N/A for implementation items; the task stopped
                              before any documentation change was warranted.
Regression suites           — NOT RUN as a gate: no source or test file was
                              modified, so there is nothing to regress. Running
                              them was unnecessary; they remain unmodified.
```

```text
TASK-078 SHA-256 at pickup:       37EB51B92018EC64C5892A78F30E552C65739B1BAD4E04F5A20868BCA52A8181
TASK-078 SHA-256 at completion:   37EB51B92018EC64C5892A78F30E552C65739B1BAD4E04F5A20868BCA52A8181
Result: IDENTICAL — TASK-078 not modified.
```

### TASK-078 Readiness Impact (report-only)

- <which blockers this resolves>
- <which TASK-078 notes/criteria become derivable; which must change — reported,
  not edited>

### Server Authority & Scope Verification

- [x] Confirmed zero client-authoritative gameplay logic introduced — no code
      was written at all.
- [x] Confirmed `BattleState` / `PetState` / `EquippedCards` / `EquippedRelics`
      remain server-authoritative; the STOP explicitly refuses to assign them to
      the client.
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1) — no new system,
      content, or endpoint introduced.
- [x] Confirmed no Pet/Card/Relic/Boss identifier was invented.
- [x] Confirmed no source, test, migration, or seed file changed.
- [x] Confirmed no new API endpoint was added.
- [x] Confirmed TASK-078, TASK-036, and all completed tasks are unmodified.
