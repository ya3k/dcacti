# TASK-081 — Resolve In-Progress Selection-State Ownership and Runtime Port Boundary (D2)

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
-->

---

## Metadata

```text
Task ID:           TASK-081
Type:              ARCHITECTURE
Status:            DONE
Risk:              HIGH
Priority:          HIGH
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
                   (5 skills — Normal budget, tasks/README.md §12)
Dependencies:      TASK-080 (DONE — established selection surface S2 in TDD.md §2.1),
                   TASK-079 (BLOCKED — origin of dependency D2; read-only context, NOT modified),
                   TASK-078 (BACKLOG — dependent task; report-only, NOT modified),
                   D3, D4 (separate dependencies — NOT resolved by this task)
```

---

## Objective

Determine and record the authoritative architectural contract for the client-side pre-battle selection boundary (D2): specifically, (1) where the player's in-progress, unpersisted Pet, Card, and Relic selection state lives during the `LobbyScene` lifecycle, (2) how `LobbyScene` accesses collection read data, and (3) what capability `GameRuntimePort` exposes to scenes for initiating `startBattle`, while strictly preserving server authority (`AGENTS.md` §10, ADR-001) and client runtime boundaries (`ARCHITECTURE.md` §2.2.1 rules 1–6).

---

## Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1 — Battle Loop and Loadouts are in MVP scope
- `docs/02-technical/TDD.md` §2.1 — Phaser/React responsibility split, `LobbyScene` pre-battle selection flow ownership, Server-Authoritative Boundary
- `docs/02-technical/ARCHITECTURE.md` §1, §2.2, §2.2.1 (rules 1–6), §3, §5.5 — `GameRuntime` coordination boundary, transport isolation, minimal technical client state, exclusion of state libraries
- `docs/02-technical/API_CONTRACTS.md` §3 (`POST /api/battle/start`), §5.1–§5.4 (collection read endpoints), §5.5 (list semantics), §5.6 (no equip/loadout state in collection responses)
- `docs/02-technical/GAME_STATE.md` §2.3 — `PetState.EquippedCards[]` and `EquippedRelics[]` server-authoritative snapshot
- `docs/03-decisions/ADR/ADR-001-server-authoritative-battle.md` — Server-authoritative battle
- `docs/03-decisions/ADR/ADR-003-phaser-game-runtime.md` — Phaser scene lifecycle and runtime boundary
- `docs/03-decisions/ADR/ADR-011-player-owner-pet-combat.md` — Player = owner, Pet = combat character; ownership vs battle-scoped equip
- `docs/03-decisions/ADR/ADR-012-player-level-pet-level-ownership.md` — Relic/Card ownership vs battle equip
- `AGENTS.md` §4 (conflicts), §7 (game-rule protection), §8 (MVP protection), §9 (anti-overengineering), §10 (server authority), §12 (domain boundaries), §13 (technical boundaries), §16 (task discipline), §17 (documentation change rule), §20 (stop conditions)
- `tasks/TASK_LIFECYCLE.md` §2–§4, `tasks/TASK_TYPES.md` §2–§3, `tasks/README.md` §6, §9, §11, §12

---

## Scope

### In Scope

- Documenting that in-progress selection state is ephemeral presentation state managed within `LobbyScene` during its scene lifecycle, keeping it distinct from persistent database collections and distinct from the server-authoritative `BattleState` snapshot.
- Defining the architectural boundary for collection queries (`ApiService.getPets`, `getCards`, `getRelics`) and confirming whether `LobbyScene` consumes `ApiService` directly or via a runtime abstraction, updating `ARCHITECTURE.md` §2.2.1 accordingly.
- Formally defining the `GameRuntimePort` interface extension (`startBattle(request: BattleStartRequest): Promise<void>`) to connect `LobbyScene`'s start trigger to the existing `GameRuntime.startBattle` implementation (`TASK-077`).
- Recording the resolved architectural contract in the canonical documentation owner (`ARCHITECTURE.md` §2.2.1 and/or `TDD.md` §2.1).
- Evaluating whether an ADR (e.g. `ADR-017`) is warranted per `docs/03-decisions/README.md` §2.
- Reporting the readiness impact on dependent backlog task `TASK-078` without modifying `TASK-078`.

### Out of Scope

- Any source code changes under `src/` (no TypeScript, C#, scene, or runtime implementation).
- Any test changes under `tests/`.
- Static content provisioning (D3 — `DATABASE.md` §5 item 4, open per `TASK-045` §6 issue 1).
- Default MVP Boss selection (D4 — `BOSS_RULES.md` §6.4).
- Creating new API endpoints or modifying `API_CONTRACTS.md` schemas.
- Introducing client-side state management libraries, universal stores, or managers (`LoadoutStore`, `SelectionStateManager`, `UniversalStateStore`, etc., forbidden by `ARCHITECTURE.md` §5.5 and `AGENTS.md` §9).
- Modifying `TASK-078`, `TASK-079`, `TASK-080`, `TASK-036`, or any file in `tasks/completed/`.
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2.

---

## Current State

TASK-080 (DONE) resolved the pre-battle selection surface (D1), assigning Choose Pet, Equip Cards, Equip Relics, and Start Battle UI to `Phaser LobbyScene` in `docs/02-technical/TDD.md` §2.1. However, the architectural contract governing where `LobbyScene`'s in-progress selections reside before dispatch, how `LobbyScene` accesses `ApiService` collection reads, and what `GameRuntimePort` exposes remains unrecorded in `docs/02-technical/ARCHITECTURE.md` §2.2.1. `GameRuntime.startBattle` was implemented in TASK-077, but `GameRuntimePort` (`GameRuntimeEvents.ts`) was left unextended pending scene caller assignment.

---

## Acceptance Criteria

- [x] In-progress selection-state ownership is documented as ephemeral presentation state owned by `LobbyScene` during its active lifecycle, distinct from persistent owned collection data (`DATABASE.md` §2) and distinct from server-authoritative `BattleState` (`GAME_STATE.md` §2.3).
- [x] Client runtime state (`GameRuntimeState.ts`) is confirmed to carry technical connection and session state only, containing zero gameplay or loadout selection state (`ARCHITECTURE.md` §2.2.1 rule 5).
- [x] `GameRuntimePort` responsibility is explicitly defined: it exposes `startBattle(request: BattleStartRequest): Promise<void>` for scene-initiated battle orchestration, and excludes collection read models or loadout state management.
- [x] The architectural boundary for collection reads (`ApiService.getPets`, `getCards`, `getRelics`) by `LobbyScene` is explicitly documented — via `GameRuntimePort`/`GameRuntime`, never `services/api/` directly (§2.2.3 rule 3).
- [x] Exactly one canonical definition owner (`docs/02-technical/ARCHITECTURE.md` §2.2.3, with §2.2.1 rule 1 clarified) is updated without introducing duplicate source-of-truth definitions (`documentation-change.md` §2).
- [x] ADR necessity is evaluated per `docs/03-decisions/README.md` §2 — **NOT warranted**; ADR-017 not created and the index is unmodified.
- [x] `API_CONTRACTS.md` §5.5 (list semantics) and §5.6 (no equip state on collection endpoints) remain preserved unchanged.
- [x] No state library, store pattern (`LoadoutStore`, `SelectionStateManager`), or unneeded abstraction is introduced (`AGENTS.md` §9, `ARCHITECTURE.md` §5.5).
- [x] Zero files under `src/` and zero files under `tests/` are modified.
- [x] `tasks/backlog/TASK-078-client-lobby-scene-match-start.md` is byte-identical before and after (SHA-256 `37EB51B9…8181` verified at pickup and completion).
- [x] `tasks/blocked/TASK-079-resolve-client-loadout-acquisition-boundary.md` is byte-identical before and after (SHA-256 `B60C0A98…85AC` verified at pickup and completion).
- [x] `tasks/blocked/TASK-036-*.md` and all `tasks/completed/` files are unmodified.
- [x] Quality review checklist passes (`quality/review.md` §1).
- [x] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001).

---

## Affected Files & Areas

```text
[ ] src/backend/ (none)
[ ] src/frontend/client/ (none)
[ ] tests/ (none — documentation/architecture-only change)
[x] docs/02-technical/ARCHITECTURE.md §2.2.1 (and/or docs/02-technical/TDD.md §2.1)
[ ] docs/03-decisions/ (ADR-017 only if warranted per docs/03-decisions/README.md §2)
[x] tasks/backlog/TASK-081-resolve-in-progress-selection-state-and-runtime-boundary.md (this file)
[ ] tasks/backlog/TASK-078-*.md (NO CHANGES — report readiness impact only)
[ ] tasks/blocked/TASK-079-*.md (NO CHANGES — read-only context)
[ ] tasks/blocked/TASK-036-*.md (NO CHANGES)
[ ] tasks/completed/ (NO CHANGES)
```

---

## Implementation Notes

- **Keep three concepts separate** (`AGENTS.md` §12/§13; ADR-011 item 4):
  ```text
  Owned collection        Player owns Pet/Card/Relic (persistent, DATABASE.md §2)
      ≠
  In-progress selection   Player selected items in LobbyScene for THIS upcoming battle
                          (ephemeral presentation/scene state, LobbyScene local)
      ≠
  BattleState snapshot    Server validated request and copied loadout into
                          PetState.EquippedCards[] / EquippedRelics[]
                          (server-authoritative, GAME_STATE.md §2.3)
  ```
- **Preserve documented layering** (`ARCHITECTURE.md` §2.2.1):
  ```text
  React → GameShell → Phaser Scene → GameRuntime → API / SignalR
  ```
  `LobbyScene` must never import `@microsoft/signalr` or interact directly with SignalR hubs (`ARCHITECTURE.md` §2.2.1 rule 1).
- **Runtime coordinates, does not store loadout state** (`ARCHITECTURE.md` §2.2.1 rule 3 & rule 5): `GameRuntimeState` holds connection/session status only. Do not add loadout models to `GameRuntimeState`.
- **Precedent for documentation/contract tasks**: Follow `TASK-080`, `TASK-074`, `TASK-054`, `TASK-049`, and `TASK-046`.

---

## Testing Requirements

### Required Verification

```text
[ ] Unit tests         — N/A: documentation/architecture-only task
[ ] Integration tests  — N/A: no code implemented
[ ] Gameplay scenarios — N/A: no gameplay rules altered
[ ] Documentation consistency — updated docs cross-checked; single canonical definition owner
[ ] Architecture consistency  — preserves ARCHITECTURE.md §2.2.1 rules 1–6 and ADR-001
[ ] Preservation checks — API_CONTRACTS.md §3, §5.5, §5.6 unchanged
[ ] Task guard checks  — TASK-078 SHA-256 identical; TASK-079 SHA-256 identical; tasks/completed/ unmodified; git status shows no src/ or tests/ change
```

### Key Edge Cases

- Attempting to introduce a global client loadout store → STOP per `ARCHITECTURE.md` §5.5 / `AGENTS.md` §9.
- Attempting to make `LobbyScene` authoritative over loadout validity → STOP per `AGENTS.md` §10 / ADR-001 (server validates on `POST /api/battle/start`).
- Conflating collection read ordering with equip slot order → preserve `API_CONTRACTS.md` §5.5 and `RELIC_RULES.md` §2.3 (request array position defines slot, collection ordering undefined).

---

## Stop Conditions

- If required behavior cannot be derived from Authoritative References: STOP per `AGENTS.md` §7.
- If task requires client-authoritative game logic calculation: STOP per `AGENTS.md` §10.
- If resolving boundary requires inventing state stores, global managers, or new frameworks: STOP per `AGENTS.md` §9 / `ARCHITECTURE.md` §5.5.
- If resolving boundary requires modifying `API_CONTRACTS.md` schemas or adding REST endpoints: STOP.
- If modifying TASK-078 or completed tasks is required: STOP.

---

## Completion Evidence

### Decisions Recorded

```text
D2-a  IN-PROGRESS SELECTION STATE OWNER
      RESOLVED — ephemeral `LobbyScene`-local scene state. Owned by the scene,
      created with it, discarded at its shutdown. Not in `GameRuntimeState`
      (rule 5), not on `GameRuntime`, no store/manager/module of its own.

D2-b  COLLECTION-READ BOUNDARY
      RESOLVED — `LobbyScene` → `GameRuntimePort` → `GameRuntime` → `ApiService`
      (`services/api/`). A scene must not import `fetch`/`ApiService` any more
      than it may import `@microsoft/signalr`.

D2-c  GameRuntimePort PRE-BATTLE CAPABILITY
      RESOLVED — `startBattle(request: BattleStartRequest): Promise<void>`,
      and the collection read the flow needs. The port carries CAPABILITIES,
      not read models or loadout/selection types.

ADR   NOT WARRANTED — recorded in `ARCHITECTURE.md` §2.2.3 only; ADR-017 not
      created; ADR index (`docs/03-decisions/README.md` §7) unmodified.

D3    UNTOUCHED — content provisioning remains open (`DATABASE.md` §5 item 4).
D4    UNTOUCHED — default MVP Boss remains unresolved (`BOSS_RULES.md` §6.4).
```

#### Evidence per decision

```text
D2-a  Ephemeral-scene-state category is already documented, not invented:
      `.ai/skills/client/client-state-authority/SKILL.md` §"State
      Classification" enumerates exactly three categories and names item 3
      "Ephemeral Interaction State (Client — Phaser Scene memory) …
      Discarded when scene resets".
      Working in-repo precedent: `BattleScene.ts:101` `selectedCell`
      ("Presentation-local input state only — it is not board state").
      `ARCHITECTURE.md` §2.2.1 rule 5 keeps `GameRuntimeState.ts` technical-only;
      §5.5 excludes state stores. `GAME_STATE.md` §4 ("Client Presentation
      State") already assigns non-authoritative presentation state to the
      client and defines it non-exhaustively. `LobbyScene` owns the flow per
      `TDD.md` §2.1 (TASK-080) — but ownership of the SURFACE was not treated
      as ownership of the STATE; the state claim rests on the classification
      evidence above, independently verified.
      `GameRuntimeState.ts` re-read: 7 members, all connection/session/runtime/
      sync/engine/connectionId/lastError — zero gameplay or loadout state.

D2-b  `ARCHITECTURE.md` §2.2 item 3 classifies `services/api/` as an isolated
      transport layer ("isolates HTTP REST communication") alongside
      `services/realtime/`. §2.2.1 rule 1 makes scenes depend on "the runtime
      port, never on the transport". The rule named SignalR only; TASK-079
      B3 recorded the REST case as "currently unstated in docs/ and must be
      decided here". §2.2.3 rule 3 + the restated rule 1 decide it:
      transport-general. Corroboration: TASK-078 AC:89 already forbids
      `fetch`/`ApiService` in `LobbyScene`.
      `GameRuntime` already injects `ApiService` (`GameRuntime.ts:106`,
      TASK-077 precedent), so no new dependency direction is introduced.

D2-c  `GameRuntime.startBattle` exists (`GameRuntime.ts:466`, TASK-077) and is
      the documented REST → connect → join sequence. TASK-077 left the PORT
      unextended pending a scene caller; §2.2.3 rule 5 names the caller
      capability. Rule 6 keeps the port free of read models — the collection
      wire shapes already live in `services/api/CollectionModels.ts` and are
      not duplicated.
```

### Changed Files

- `docs/02-technical/ARCHITECTURE.md` — **the single canonical definition
  owner.** Added **§2.2.3 Pre-Battle Selection Boundary** (diagram + 6 rules +
  the three-concept inequality), and restated **§2.2.1 rule 1** as
  transport-general so it covers `fetch`/`ApiService` and not only
  `@microsoft/signalr`. Version header 1.0 → 1.1 recording the change.
  `§3` deliberately **not** modified: its component-granularity responsibility
  table already covers `GameRuntime`/`ApiService`/Scenes implicitly, and
  restating §2.2.3 there would duplicate the definition
  (`documentation-change.md` §2) — the same reasoning TASK-080 applied to §3.
- `tasks/backlog/TASK-081-resolve-in-progress-selection-state-and-runtime-boundary.md`
  — this file; Status → DONE, completion evidence recorded.

No other file was changed. `TDD.md` was **not** modified (no second owner).
`API_CONTRACTS.md` was **not** modified. No ADR was created.

### Validation Results

```text
Preservation        — PASS. `API_CONTRACTS.md` §3/§5.5/§5.6 unchanged
                      (`git diff --stat` empty); `TDD.md` unchanged. No
                      gameplay rule, count, formula, endpoint, or contract
                      changed.
Architecture        — PASS. §2.2.1 rules 1–6 preserved; rule 1's clarification
                      narrows nothing and adds no exception. No layer
                      inversion: the documented direction
                      (React → GameShell → Scene → GameRuntime → API/SignalR)
                      is unchanged, and D2-b routes the scene's read through
                      the runtime rather than around it. ADR-001 reinforced,
                      not modified.
Documentation       — PASS. Exactly one definition owner (ARCHITECTURE.md
                      §2.2.3); no duplicate source of truth
                      (documentation-change.md §2). Every rule cites a
                      document + section. No AGENTS.md §4 conflict: the
                      three-concept inequality matches ADR-011 item 4.
Task guards         — PASS. TASK-078 SHA-256 37EB51B9…8181 → 37EB51B9…8181
                      (IDENTICAL). TASK-079 SHA-256 B60C0A98…85AC →
                      B60C0A98…85AC (IDENTICAL). TASK-036 and all
                      tasks/completed/ files unmodified. `git status` shows
                      only `docs/02-technical/ARCHITECTURE.md` (modified) and
                      this task file (new) — zero `src/` and zero `tests/`
                      changes.
Changed-file scope  — PASS. Changed set = {ARCHITECTURE.md, this task file},
                      equal to the §Affected-Files declaration.
ADR index           — PASS. Unmodified (no ADR created).
Guard suites        — NOT run as a gate: no source or test file was modified,
                      so there is nothing to regress; they remain unmodified.
```

### ADR Evaluation (`docs/03-decisions/README.md` §2)

```text
Criterion                          Result
─────────────────────────────────  ──────────────────────────────────────────
architecturally important          No — records where an already-documented
                                   flow's state belongs; changes no boundary.
difficult to reverse               No — no technology, ownership model,
                                   persistence, or realtime strategy changes.
cross-cutting                      No — one client boundary; server, Redis,
                                   PostgreSQL, SignalR untouched.
security-sensitive                 No.
state-management-sensitive         Addressed WITHOUT an ADR: the decision is
                                   "ephemeral scene state, no store", i.e. it
                                   PRESERVES §2.2.1 rule 5 / §5.5.
duplicates a technical document    YES — "how the client is structured" is
                                   ARCHITECTURE.md's own stated purpose.
                                   README.md §2 excludes exactly this.
contradicts an Accepted ADR        No — ADR-001, ADR-003, ADR-011, ADR-012 all
                                   stand; none is superseded or reopened.
─────────────────────────────────  ──────────────────────────────────────────
CONCLUSION: ADR-017 NOT CREATED. Recorded in ARCHITECTURE.md §2.2.3 only.
            docs/03-decisions/README.md §7 index unmodified.
```

`adr-change.md` §1 additionally bars an ADR that would "mostly describe *how*
something works rather than *why* it was chosen" — §2.2.3 is a structural
contract, so it belongs in `docs/02-technical/`, not in an ADR.

### Server Authority & Scope Verification

- [x] Confirmed zero client-authoritative gameplay logic — §2.2.3 rule 5 makes
      the server the validator of the submitted request and the sole author of
      the resulting `BattleState`; the scene owns only an unsubmitted selection.
- [x] Confirmed `BattleState` / `PetState` / `EquippedCards[]` /
      `EquippedRelics[]` remain server-authoritative (`GAME_STATE.md` §2.3,
      ADR-001) — the §2.2.3 inequality keeps the snapshot distinct from both
      the owned collection and the in-progress selection.
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1) — no new system,
      content, endpoint, or identifier; the flow is the existing documented one.
- [x] Confirmed D3 and D4 remain unresolved and out of scope — neither was
      designed, decided, or provisioned.
- [x] Confirmed TASK-078 and TASK-079 remain unmodified (byte-identical).
- [x] Confirmed no source code or tests were modified — `git status` shows no
      `src/` or `tests/` change.
- [x] Confirmed no state library, store, manager, or unneeded abstraction
      introduced (`AGENTS.md` §9, `ARCHITECTURE.md` §5.5) — §2.2.3 rule 1
      explicitly forbids one.

### TASK-078 Readiness Impact (report-only)

- **Resolved by this task (D2):**
  - TASK-078's AC ("`GameRuntimePort` defines
    `startBattle(request: BattleStartRequest): Promise<void>`") is now
    **derivable** — §2.2.3 rule 5 states the capability.
  - Where the in-progress selection lives and who owns it is now stated
    (§2.2.3 rules 1–2), so the request-construction AC has a defined origin.
  - The collection-read path is now stated (§2.2.3 rule 3), so
    "`LobbyScene` imports no transport libraries" is a documented rule rather
    than an unstated assumption.
- **Still blocking TASK-078 (unchanged by this task):**
  - **D3** — no Pet/Card/Relic content exists to read or select
    (`DATABASE.md` §5 item 4, open per `TASK-045` §6 issue 1).
  - **D4** — which `bossId` the MVP carries. TASK-078:117 proposes
    `"boss-hoa-long"`, but a task note is lower precedence than `docs/`
    (`AGENTS.md` §2) and no authoritative document designates a default.
- **Correctable in a later re-audit (NOT corrected here):** TASK-078:43 cites
  the non-existent `ADR-011-loadout-and-ownership-boundaries.md` (actual:
  `ADR-011-player-owner-pet-combat.md`).
- **TASK-078 remains NOT READY** and was not modified.
