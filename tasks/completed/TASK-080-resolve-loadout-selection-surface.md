# TASK-080 — Resolve the MVP Loadout Selection Surface (D1)

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.

  THIS TASK PRESENTS THE DOCUMENTED EVIDENCE AND REQUIRES THE APPROPRIATE
  HUMAN/PRODUCT DECISION. It does not decide which surface the player uses.
  Choosing, recommending, ranking, or defaulting a surface is the single
  prohibited action of this task (AGENTS.md §7).
-->

---

## Metadata

```text
Task ID:           TASK-080
Type:              ARCHITECTURE
Status:            DONE
Risk:              HIGH
Priority:          HIGH
Primary Agent:     orchestrator (task-lifecycle / requester coordination —
                   this task records the product decision; no domain agent
                   may author the surface choice)
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
Dependencies:      TASK-079 (BLOCKED — origin of dependency D1; read as
                      historical context only, AGENTS.md §16; NOT modified),
                   TASK-078 (BACKLOG — dependent task; report-only, NOT
                      modified),
                   TASK-075, TASK-076, TASK-077 (DONE — context only)
                   D2, D3 (separate dependencies — NOT created, NOT resolved
                      by this task)
Estimate:          Normal (5 skills; documentation/decision only — no code,
                   no tests, no endpoints)
```

**Type classification note.** `ARCHITECTURE`, not `DOCUMENTATION`. The
deliverable is an architecture/product boundary assignment — which client
surface owns the MVP pre-battle selection steps — recorded into the canonical
architecture document(s). `tasks/TASK_TYPES.md` §3 selects the type by what
the deliverable is; the deliverable is a boundary decision plus its
architecture-documentation record, and an ADR is created only if the settled
decision warrants one (§8).

**Status note.** `BACKLOG`, not `BLOCKED`. Per `tasks/TASK_LIFECYCLE.md` §2,
`BACKLOG → BLOCKED` is not a valid transition, and `tasks/README.md` §6 keeps
this file in `backlog/`. An unanswered §5 is this task's normal starting state
— it is what the task exists to collect — so it is not a blocker
(`TASK_LIFECYCLE.md` §3). The repository's task template
(`tasks/TASK_TEMPLATE.md`) defines the Status enum as
`BACKLOG | READY | IN PROGRESS | IN REVIEW | BLOCKED | DONE`; there is no
`AWAITING PRODUCT OWNER INPUT` status value, so the awaiting-input state is
carried as the annotation above and in §5, exactly as the decision-input
precedent `TASK-061` recorded it. Precedent from `TASK-061` also confirms this
file must not be moved to `active/` or `blocked/` merely because the decision
requires human input (`TASK_LIFECYCLE.md` §4 — a folder change only follows a
status transition of a started task).

---

## 1. Objective

Obtain and record the **human/product decision** for which client surface
performs each MVP pre-battle selection step — Choose Pet, Equip Cards, Equip
Relics, and the Start Battle trigger UI (plus the Boss-selection question
TASK-079 deferred to this dependency) — and, once that decision is supplied,
record MVP ownership of those UI surfaces in the single canonical document.

The task collects the decision, verifies it against the preserved contracts,
and records it. It does **not** choose a surface, does **not** resolve where
the in-progress selection state lives (D2), does **not** provision Pet/Card/
Relic content (D3), and edits no source, test, or API contract.

---

## 2. Problem Statement

`tasks/blocked/TASK-079-resolve-client-loadout-acquisition-boundary.md`
stopped without resolving the loadout acquisition boundary because that
boundary decomposed into three independent dependencies. This task is the
**D1** decision task: **selection surface / flow ownership**.

### 2.1 What the documents establish (verified locations)

```text
GDD.md §2:63              "Enter Battle → Choose Pet → Equip Cards → Equip
                           Relics → Start Battle" — names the player steps;
                           assigns them to NO surface, scene, or component.
GDD.md §3:78-84           Battle structure: 1 selected Pet, 3 Basic Cards,
                           3–5 Relics (counts only, no UI ownership).
TDD.md §2.1:61-77         React + Vite = "Application & UI Shell" (HTML UI &
                           Overlays, Menus & Settings); Phaser 4 = "Game
                           Runtime & Presentation" (Scenes, input, tweens).
TDD.md §2.1:104           LobbyScene = "Battle preparation, loadout review,
                           and match start trigger" — a scope label; no
                           selection mechanism, no surface assignment for the
                           selection steps themselves.
TDD.md §2.1:124-129       React owns the application shell and overlays; React
                           does NOT own the Phaser game loop. Menus are named
                           for React; the loadout steps are not named for
                           either layer.
ARCHITECTURE.md §3:351    PhaserGame / Scenes own "canvas rendering & scene
                           lifecycle".
ARCHITECTURE.md §3:354-355 GameRuntime "Coordinates Phaser, transport &
                           runtime state; no gameplay".
ARCHITECTURE.md §3:359    GameShell / React Overlays own "HTML overlays &
                           menus" — no selection responsibility stated.
ARCHITECTURE.md §2.2.1    rules 1–6 confine scenes/runtime/transport (context
                           for D2, excluded here).
ADR-003 Context:11        lists "loadout" among the client's non-gameplay
                           screens (menus, loadout, settings, collection
                           viewers).
ADR-003 Decision:26-29    Phaser owns the named Scenes incl. LobbyScene;
                           React owns "HTML UI overlays, menus, settings" —
                           loadout selection is assigned to NEITHER explicitly.
RELIC_RULES.md §2.1 item 3:75-78  "The client supplies the selection only" —
                           the client as a whole; never which client layer.
API_CONTRACTS.md §3:469-470       exactly 3 Basic Cards; 3–5 Relics (contract
                           preserved — not reopened here).
API_CONTRACTS.md §5.5:792-799, §5.6:801-808  no ordering; clients must not
                           read equip state from collection endpoints
                           (preserved).
PET_RULES.md §2           exactly one Pet active per battle; item 4:68-70
                           Player equips 3–5 owned Relics onto the active Pet
                           before battle start (rule, not surface).
```

### 2.2 The documented ambiguity (why a decision task exists)

Two in-scope documents overlap without either being authoritative for loadout
selection:

```text
TDD.md §2.1               gives React "menus"; gives LobbyScene "battle
                           preparation, loadout review, and match start
                           trigger".
ARCHITECTURE.md §3:359     gives React overlays "menus".
ADR-003                   places "loadout" among non-gameplay screens but its
                           Decision text assigns it to no layer.
GDD.md:63                  requires the four player steps as the battle-entry
                           flow but names no surface.
```

`REVIEW NOTE`: the overlap is not yet an `AGENTS.md` §4 conflict requiring a
stop — each statement is true at its own granularity, and none of them
contradicts another outright. What is **missing** is the surface assignment
itself, which no document supplies. That is a gap, reported per `AGENTS.md`
§20 ("Ambiguous requirement / missing information"), not a silently-resolved
conflict.

**Consequence if unanswered:** TASK-078 ("Client LobbyScene match-start
trigger") cannot become implementable without a surface on which the player
performs the selection steps; TASK-079's D1 note records exactly this wait.
Following TASK-078's Implementation Notes without this decision would mean
picking a surface by convenience, which `AGENTS.md` §7 forbids.

### 2.3 Creation-time verification (per the task-generation workflow)

```text
1. Do docs already define the selection surface?    NO — verified above
                                                     (no assignment exists).
2. Does another active/backlog task resolve D1?     NO — tasks/backlog/ holds
                                                     only TASK-078 (no surface
                                                     terms in its text);
                                                     tasks/active/ is empty;
                                                     tasks/blocked/ holds
                                                     TASK-036 and TASK-079,
                                                     both excluding D1.
3. Does answering D1 require resolving D2 or D3?    NO — the surface question
                                                     is decidable without an
                                                     owner for in-progress
                                                     selection state or
                                                     content rows; D2/D3 are
                                                     separate dependencies.
4. Would answering D1 require inventing rules, IDs, endpoints, or code?
                                                    NO — it assigns an
                                                     existing UI step to an
                                                     existing surface.
5. Highest existing task ID                        TASK-079 → this task is
                                                     TASK-080.
```

No creation-time stop condition fired; the task was created.

---

## 3. Authoritative References

- `docs/00-overview/GDD.md` §2 (`:63` the four-step selection flow), §3
  (`:78-84` battle structure)
- `docs/00-overview/MVP_SCOPE.md` §1 (Cards 3 Basic, Relics "3–5 equipped per
  battle"), §2, §4 (unlisted = FUTURE — scope classification of any answer)
- `docs/01-game-design/GAME_RULES.md` §1 (counts), §13 (Relic equip rules),
  §18 (server authority — ADR-001), §20 (Rule Change Policy), §21 (hierarchy)
- `docs/01-game-design/PET_RULES.md` §2 (exactly one active Pet; item 4 equip
  before battle start)
- `docs/01-game-design/CARD_RULES.md` §1 (exactly 3 Basic Cards + 1 Pet Skill
  Card)
- `docs/01-game-design/RELIC_RULES.md` §2, §2.1 (selection, validation,
  "client supplies the selection only")
- `docs/01-game-design/BOSS_RULES.md` §6, §6.4 (canonical Boss identity — the
  carried-over question, see §5 item A5)
- `docs/02-technical/TDD.md` §2.1 (`:61-77`, `:104`, `:124-129`) — client
  responsibility split, LobbyScene scope label, React/Phaser division
- `docs/02-technical/ARCHITECTURE.md` §1, §2.2, §2.2.1 (rules 1–6),
  §3 (`:351`, `:354-355`, `:359`), §5.5 (`:437-440` minimal client state)
- `docs/02-technical/API_CONTRACTS.md` §1 (endpoint list is the complete REST
  surface), §3 (`POST /api/battle/start` validation — preserved),
  §5.5–§5.6 (preserved)
- `docs/02-technical/GAME_STATE.md` §2.3 (`PetState.EquippedCards[]` /
  `EquippedRelics[]` — the server-side snapshot this decision must not touch)
- `docs/03-decisions/README.md` §1, §2 (when an ADR is warranted), §4
  (numbering), §5 (Accepted only when established), §7 (index), §8 (open items)
- `docs/03-decisions/ADR/ADR-001-server-authoritative-battle.md` — client may
  only request
- `docs/03-decisions/ADR/ADR-003-phaser-game-runtime.md` — `Context:11` vs
  `Decision:26-29` (the loadout assignment gap; Context text is not a decision)
- `docs/03-decisions/ADR/ADR-011-player-owner-pet-combat.md` — ownership vs
  battle-scoped equip (item 4)
- `docs/03-decisions/ADR/ADR-012-player-level-pet-level-ownership.md` —
  ownership/battle-equip boundary
- `AGENTS.md` §4 (conflicts), §7 (game-rule protection), §8 (MVP protection),
  §10 (server authority), §16 (task discipline), §17 (documentation change
  rule), §20 (stop conditions)
- `tasks/TASK_LIFECYCLE.md` §2, §3, §4; `tasks/TASK_TYPES.md` §2–§3;
  `tasks/README.md` §6, §9, §12; `tasks/TASK_TEMPLATE.md`
- `tasks/completed/TASK-061-*` — the repository's product-owner
  decision-input task convention (§5/§6 of this file follow it)
- `tasks/blocked/TASK-079-*` — the STOP CONDITION section defining D1/D2/D3
  (read-only context; must not be modified)

**Verified filenames.** The loadout/ownership ADR is
`docs/03-decisions/ADR/ADR-011-player-owner-pet-combat.md` — the filename
`ADR-011-loadout-and-ownership-boundaries.md` cited by TASK-078 does not
exist; do not reproduce it. The highest existing ADR is `ADR-016`, so the
next free number is `ADR-017` (`README.md` §4 — numbers are never reused).
Whether an ADR is required at all is §5 item D2 of this task; **do not assume
ADR-017 is needed, and never create an ADR while the decision is unresolved**
(`README.md` §5; `adr-change.md`).

---

## 4. Current State

```text
Documentation (the gap)
  GDD.md:63 names the four selection steps. No document assigns any of them
  to a surface. TDD.md §2.1 and ARCHITECTURE.md §3 both describe React as
  owning menus/overlays and LobbyScene as owning battle preparation, and the
  two readings overlap for loadout selection.

Client (evidence only — not modified)
  React shell exists:            app/App.tsx, app/GameShell.tsx,
                                 ui/components/StatusOverlay.tsx
  Phaser scenes exist:           BootScene, PreloaderScene, BattleScene
  NOT present:                   LobbyScene, MainMenuScene, ResultScene
                                 (ARCHITECTURE.md §1 tree is stale — TASK-079
                                 recorded this; not a D1 question)
  No loadout/pet-selection UI exists anywhere in the client today.

Dependent task
  TASK-078  BACKLOG, NOT READY. Its Implementation Notes assume selection has
            happened but assign no surface. NOT modified by this task.

Origin
  TASK-079  BLOCKED (STOP CONDITION — independent dependency rule). Recorded
            D1 as "SELECTION UX / FLOW OWNERSHIP (product + architecture)…
            REQUIRES HUMAN PRODUCT INPUT". NOT modified by this task.

Separate dependencies (NOT solved here)
  D2  in-progress selection-state ownership + GameRuntimePort responsibility
      (candidate owner: ARCHITECTURE.md §2.2.1) — its own future task
  D3  Pet/Card/Relic static content provisioning
      (candidate owner: DATABASE.md §5 item 4, recorded open by TASK-045 §6
      issue 1) — its own future task
```

---

## 5. Product Owner Decisions Required

> **PRODUCT OWNER DECISION RECORDED — CONFIRMED.**
>
> The Product Owner supplied the A1–A5 answers explicitly. They are recorded
> verbatim below and are authoritative task evidence. An agent must not
> reinterpret, weaken, or extend them (`AGENTS.md` §7).
>
> **Product Owner decision:**
>
> ```text
> CONFIRMED
> ```
>
> **Stated scope of the decision (Product Owner's own words):**
>
> ```text
> "The game experience and all game-related interactive flows belong
>  inside Phaser."
> ```
>
> This assigns the player-facing preparation flow to Phaser. It does **not**
> move authoritative state ownership: the server (ASP.NET Core) remains
> authoritative for game state and rules, Redis holds active authoritative
> battle state, `GameRuntime` remains the synchronized client runtime /
> coordinator, `SignalRService` remains the transport, Phaser remains game
> presentation + player interaction, and React / GameShell remains the
> platform / Activity shell. Phaser does **not** become the authoritative
> owner of `BattleState`, `PlayerState`, `PetState`, `BossState`, `Turn`,
> `Sequence`, RNG, or combat results (`AGENTS.md` §10, ADR-001).

Answer each item independently. The four A-items jointly define the MVP
battle-entry flow (`GDD.md:63`) — read them together (§6).

### A — Selection Surface (which client surface performs the step)

Options the Product Owner may choose from (the task neither prefers nor ranks
them; a differently-specified surface is acceptable if stated explicitly):

```text
S1  React / GameShell HTML UI overlay   (TDD.md §2.1 "Application & UI Shell",
                                         ARCHITECTURE.md §3:359 "HTML overlays
                                         & menus")
S2  Phaser LobbyScene                   (TDD.md §2.1:104 "battle preparation,
                                         loadout review…"; ADR-003
                                         Decision:26-27 scene list)
S3  Hybrid — an explicitly stated split across surfaces (must state which step
                                         belongs to which surface)
S4  Other surface, stated explicitly     (requires its own justification)
```

**A1. Choose Pet**

```text
Decision:
S2 — Phaser LobbyScene

Rationale (optional):
Product Owner: "The game experience and all game-related interactive flows
belong inside Phaser."
```

**A2. Equip Cards (submit the 3 Basic Card selection)**

```text
Decision:
S2 — Phaser LobbyScene

Rationale (optional):
Product Owner: "The game experience and all game-related interactive flows
belong inside Phaser."
```

**A3. Equip Relics (submit the 3–5 Relic selection)**

```text
Decision:
S2 — Phaser LobbyScene

Rationale (optional):
Product Owner: "The game experience and all game-related interactive flows
belong inside Phaser."
```

**A4. Start Battle trigger UI (the control the player presses — UI ownership
only, not the runtime orchestration implemented by TASK-077)**

```text
Decision:
S2 — Phaser LobbyScene

Rationale (optional):
Product Owner: "The game experience and all game-related interactive flows
belong inside Phaser."
```

**A5. Boss selection UI (carried over from TASK-079 completion evidence,
which deferred "whether the MVP Lobby hardcodes one Boss or offers selection"
to D1). If the MVP flow hardcodes the Boss, record that explicitly.**

```text
Decision:
MVP hardcodes the Boss; no Boss selection UI.

Rationale (optional):
Product Owner: "MVP hardcodes the Boss; no Boss selection UI."
The MVP flow presents no player-facing Boss step. No BossSelectionScene,
BossSelectionUI, BossSelection API, or Boss selection state is introduced.
```

> **FOLLOW-UP AMBIGUITY — WHICH Boss is hardcoded (NOT RESOLVED HERE).**
>
> The Product Owner decided *that* the MVP hardcodes the Boss, but did not
> name *which* Boss. No authoritative document designates a default:
> `BOSS_RULES.md` §6.4:274-277 records three content-defined, canonically
> identified MVP Bosses — `"boss-hoa-long"`, `"boss-thuy-ma"`,
> `"boss-moc-yeu"` — all three of which are provisioned as
> `BossDefinition` rows by migration `20260926151112_ProvisionBossDefinitions`
> (TASK-053). `BOSS_RULES.md` §6.4:245-249 fixes their identities "so no task
> invents its own", and §6.1:226-228 calls the base stats "configuration
> defaults used at battle creation" without selecting one.
>
> TASK-078:117 proposes `bossId: "boss-hoa-long"` in its Implementation
> Notes, but a task is lower precedence than `docs/` and that note is not a
> product decision (`AGENTS.md` §2). Selecting it here would be an agent
> choosing content, which `AGENTS.md` §7 forbids.
>
> **Recorded as an unresolved dependency (D4) — see §5.1.** This task does
> not choose the Boss.

### B — Preserved Contracts (confirm, do not reopen)

```text
B1  CONFIRMED PRESERVED. CARD_RULES.md §1 (exactly 3 Basic Cards),
    PET_RULES.md §2 (one active Pet), RELIC_RULES.md §2 (3–5 Relics) are
    unchanged by the surface decision. The Product Owner's answer assigns a
    UI surface only; it changes no count and no rule. No count, formula, or
    validation rule was altered, so no gameplay change and no Rule Change
    Policy (GAME_RULES.md §20) is engaged.
B2  CONFIRMED PRESERVED. API_CONTRACTS.md §3, §5.5, §5.6 are unchanged and no
    endpoint was added. The S2 answer requires no new endpoint: the
    LobbyScene submits the documented §3 request through the existing
    runtime path. §5.6's rule stands — collection endpoints do not become
    loadout/equip-state endpoints, and clients must not read equip state from
    them. §5.5's rule stands — no ordering is defined and clients must not
    rely on any order in a collection response. Neither was weakened.
B3  CONFIRMED. The chosen surface remains NON-authoritative. Phaser
    LobbyScene produces a REQUEST only (GAME_RULES.md §18, ADR-001,
    AGENTS.md §10). It never computes or stores authoritative
    Damage/HP/PetState.Equipped*/BattleState, and the server remains solely
    responsible for validating the submitted loadout.
```

### C — Boundary Classification (record explicitly)

```text
C1  CONFIRMED — D2 NOT DECIDED. This decision records UI SURFACE ownership
    only. In-progress selection-state ownership and GameRuntimePort
    responsibility remain D2, a separate and still-not-created dependency.
    The S2 answer CONSTRAINS D2 (the surface is Phaser LobbyScene, so any
    future state owner must be reachable from a scene through the runtime
    port) but it does NOT DECIDE D2: where the in-progress selection lives,
    and whether GameRuntimePort exposes it, are untouched. No state store,
    manager, or new architecture was invented.
C2  CONFIRMED — D3 NOT DESIGNED. Pet/Card/Relic static content provisioning
    remains D3 (DATABASE.md §5 item 4, open per TASK-045 §6 issue 1). Nothing
    in this decision implies, designs, or provisions content. DATABASE.md's
    open provisioning decision is unmodified.
```

### D — Recording Ownership

```text
D1  DEFINITION OWNER: docs/02-technical/TDD.md §2.1.
    TDD.md §2.1 is the document whose stated purpose covers the client
    responsibility split — it already owns the Phaser/React division and the
    per-scene scope labels ("LobbyScene: battle preparation, loadout review,
    and match start trigger"). The surface assignment is a refinement of that
    existing per-scene ownership, so it belongs where the scene
    responsibilities are already defined.
    docs/02-technical/ARCHITECTURE.md §3's component responsibility table is
    NOT edited to restate the assignment: it assigns responsibilities at the
    component granularity (PhaserGame / Scenes = "canvas rendering & scene
    lifecycle"), which already covers LobbyScene implicitly and remains true.
    documentation-change.md §2 forbids restating one definition in two
    documents, so exactly one owner is used.
D2  ADR NOT WARRANTED. Per docs/03-decisions/README.md §2 and
    architecture/adr-change.md §1, an ADR is for architecturally important,
    difficult-to-reverse, cross-cutting decisions, and must record a decision
    the documentation already establishes. This decision assigns a UI step
    to an already-existing, already-documented scene
    (TDD.md §2.1:104, ADR-003 Decision:26-27) and changes no boundary,
    technology, ownership model, persistence strategy, or authoritative-state
    model. It is a refinement of existing architecture, not a new
    architectural decision. ADR-003's React+Phaser adoption decision stands
    unmodified and is not reopened. Recorded in TDD.md §2.1 only; ADR-017 is
    NOT created.
```

### 5.1 Follow-up Dependencies Register (recorded, NOT resolved here)

```text
D2  In-progress selection-state ownership + GameRuntimePort responsibility.
    Status: UNRESOLVED. Constrained by the S2 decision (the surface is
    Phaser LobbyScene), not decided by it.
    Candidate owner: docs/02-technical/ARCHITECTURE.md §2.2.1.
    Not created as a task here.

D3  Pet/Card/Relic static content provisioning.
    Status: UNRESOLVED. Recorded open by DATABASE.md §5 item 4 and
    TASK-045 §6 issue 1.
    Candidate owner: docs/02-technical/DATABASE.md §5 item 4.
    Not created as a task here.

D4  WHICH Boss the MVP hardcodes (new, surfaced by this decision).
    Status: UNRESOLVED. The Product Owner decided the MVP has no Boss
    selection UI and hardcodes the Boss, but named no Boss. Three are
    content-defined and provisioned (BOSS_RULES.md §6.4; migration
    20260926151112_ProvisionBossDefinitions). No document designates a
    default. An agent choosing one would be authoring content
    (AGENTS.md §7).
    Candidate owner: docs/01-game-design/BOSS_RULES.md §6 / §6.4 (or the
    MVP flow description in TDD.md §2.1 if the choice is a flow default).
    Not created as a task here.

These are reported only. Per the Independent Dependency Rule (TASK-079
origin) and this task's §7/§13, TASK-080 does not expand to resolve them and
creates no task for them.
```

---

## 6. Decision Boundary (what the executing agent must NOT do)

The executing agent must **NOT**:

```text
choose a surface                     recommend or rank options
default to S1 or S2                  infer the answer from file names,
                                     folder layout, or component naming
infer from "convenience"             infer from "fewer files to change"
infer from what the codebase already
  does or is capable of              infer from TASK-078's notes or wording
treat ADR-003 Context:11 ("loadout"
  among non-gameplay screens) as if
  it were the Decision               treat TDD.md §2.1:104 ("loadout review")
  as an assignment of selection      resolve D2 (selection-state owner) as a
                                     side effect
resolve D3 (content provisioning)    author Pet/Card/Relic/Boss identifiers
change a gameplay count or rule      add an endpoint or API field
write code, types, or tests          create an ADR before the decision exists
modify TASK-078 / TASK-079 / TASK-036 / any tasks/completed/ file
```

Answers may be recorded **verbatim**, including an explicit "not yet decided"
— but a deferred answer must be recorded as deferred, never left reading
`<PENDING PRODUCT-OWNER DECISION>` while the task is called complete (§15).

A1–A4 may legitimately differ (an S3 hybrid with a per-step split is a valid
answer). The prohibition is on the AGENT choosing, not on the Product Owner
splitting.

---

## 7. Scope

### In Scope

- Read-only discovery across the documents in §3 to present the evidence of
  the documented gap (already verified at creation — re-verify at execution).
- Presenting A1–A5 and recording the human/product answers verbatim.
- Verifying B1–B3 preservation and C1/C2 boundary classification.
- Recording the settled surface assignment in the single canonical owner per
  D1, via `architecture/architecture-change.md`, gated by
  `documentation/documentation-change.md`.
- An ADR **only if** D2 warrants it after the decision is settled
  (`docs/03-decisions/README.md` §2; `adr-change.md` §1–§2; index updated per
  `README.md` §7).
- Reporting the impact on D2, D3, and TASK-078 — report-only.

### Out of Scope

```text
D2 — in-progress selection-state ownership, GameRuntimePort responsibility,
     readRuntime(), state storage location, state libraries
D3 — Pet/Card/Relic static content provisioning, HasData, EF migrations,
     seeders, fixtures, content loaders
Any source code — nothing under src/
Any test change — nothing under tests/
Any TypeScript or C# — no interfaces, signatures, payload types
LobbyScene / MainMenuScene / ResultScene creation or scene navigation
New API endpoints, API fields, or contract changes (API_CONTRACTS.md §1)
Any Pet/Card/Relic/Boss identifier authoring
Any gameplay rule, count, formula, or balance change
SignalR, Redis, BattleState, GAME_STATE.md changes
Modifying TASK-078, TASK-079, TASK-036, or any tasks/completed/ file
Any item listed as OUT in docs/00-overview/MVP_SCOPE.md §2
(any acquisition/gacha flow — §2 excludes Gacha)
Creating the D2 or D3 task from this task (report the need instead)
```

---

## 8. Required Documentation Changes

**ACTUAL CHANGE SET (recorded at completion).** Exactly one authoritative
document was modified:

```text
Selection-surface ownership (A1–A5)
                              → docs/02-technical/TDD.md §2.1 — the single
                                 canonical definition owner (per §5 D1).
                                 The `LobbyScene` bullet now states that the
                                 MVP pre-battle selection flow (Choose Pet,
                                 Equip Cards, Equip Relics, Start Battle) is
                                 LobbyScene's responsibility, that Phaser owns
                                 game-related interactive flows while React
                                 owns the platform shell, that LobbyScene is
                                 presentation/interaction only and submits a
                                 request (never authoritative), and that no
                                 MVP Boss-selection step or UI exists.
                                 It also cross-references TDD.md §2.1's own
                                 Server-Authoritative Boundary, GAME_RULES.md
                                 §18, ADR-001, and API_CONTRACTS.md §5.5–§5.6.
Server-authoritative / surface-boundary rationale
                              → NOT CREATED. ADR is not warranted (§5 D2):
                                 no boundary, technology, ownership model, or
                                 authoritative-state model changed. ADR-003
                                 stands unmodified and is not reopened.
                                 ADR-017 is NOT created; the ADR index in
                                 docs/03-decisions/README.md §7 is therefore
                                 unmodified.
ARCHITECTURE.md §3            → NOT MODIFIED. Its component-level
                                 responsibility table already covers
                                 LobbyScene implicitly via
                                 "PhaserGame / Scenes — canvas rendering &
                                 scene lifecycle", and restating the
                                 assignment there would duplicate the TDD.md
                                 §2.1 definition (documentation-change.md §2).
D2 (selection-state owner)    → NOT recorded here; separate dependency (§5.1).
D3 (content provisioning)     → NOT recorded here; separate dependency (§5.1).
D4 (which Boss is hardcoded)  → NOT recorded here; separate dependency (§5.1).
```

---

## 9. Acceptance Criteria

All binary; each cites the section that must contain it.

- [x] A1–A5 each carry a recorded human/product answer, or are each
      explicitly recorded as deferred with the Product Owner's stated reason
      (§5) — A1–A4 = S2, A5 = MVP hardcodes the Boss with no selection UI.
      Product Owner decision recorded as `CONFIRMED`. No slot reads
      `<PENDING PRODUCT-OWNER DECISION>`.
- [x] No surface was chosen, recommended, ranked, defaulted, or inferred by an
      agent (§6) — every `Decision:` value was supplied verbatim by the
      Product Owner; no agent completed a slot.
- [x] B1–B3 are explicitly confirmed preserved, or a required change is
      reported as a separate decision and STOPPED (§5 B) — B1/B2/B3 all
      CONFIRMED PRESERVED; no separate change was required.
- [x] C1 records that selection-state ownership (D2) was not decided, and C2
      records that content provisioning (D3) was not designed (§5 C) — both
      CONFIRMED.
- [x] The three-concept distinction — Selection Surface ≠ Selection-State
      Ownership ≠ server `BattleState` — is stated in the recorded output
      (§11, and restated in §5 B3/C1)
- [x] D1 names exactly one definition owner for the surface assignment; no
      duplicated source-of-truth definition is introduced
      (`documentation-change.md` §2) — `docs/02-technical/TDD.md` §2.1;
      ARCHITECTURE.md §3 deliberately not restated.
- [x] An ADR exists only if D2 warrants it after settlement; no ADR was
      created while any §5 slot was unanswered — ADR judged NOT warranted
      (§5 D2); ADR-017 not created; ADR index unmodified.
- [x] `CARD_RULES.md` §1, `PET_RULES.md` §2, `RELIC_RULES.md` §2 counts are
      unchanged; `API_CONTRACTS.md` §3, §5.5, §5.6 are unchanged
- [x] The recorded answer preserves client non-authoritative behaviour: the
      surface produces a request only (`AGENTS.md` §10 / ADR-001) — stated in
      the TDD.md §2.1 wording and in §5 B3.
- [x] `tasks/backlog/TASK-078-client-lobby-scene-match-start.md` is
      **byte-identical** before and after (SHA-256 recorded at pickup and
      re-verified at completion)
- [x] `tasks/blocked/TASK-079-resolve-client-loadout-acquisition-boundary.md`
      is **byte-identical** before and after (SHA-256 recorded at pickup and
      re-verified at completion)
- [x] `tasks/blocked/TASK-036-*.md` and all `tasks/completed/` files are
      unmodified
- [ ] Zero files under `src/` are modified; zero files under `tests/` are
      modified
- [ ] No migration, seed, endpoint, identifier, or gameplay rule is added or
      changed
- [ ] D2 and D3 are reported as continuing separate dependencies — no D2/D3
      task is created from this task
- [ ] TASK-078 readiness impact is reported without modifying TASK-078
- [ ] Documentation consistency checks pass (no duplicate definition; every
      claim cites document + section)
- [ ] Quality review checklist passes (`quality/review.md` §1)
- [ ] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001)
- [ ] If any §5 slot remains unanswered, the task is recorded as awaiting
      Product Owner input and remains `BACKLOG` — it is not called complete
      (§5, §13)

---

## 10. Affected Files & Areas

```text
[ ] src/backend/ (none)
[ ] src/frontend/client/ (none)
[ ] tests/ (none — documentation/architecture-only change; existing guard
            suites must still pass UNMODIFIED)
[x] docs/ (ACTUAL: docs/02-technical/TDD.md §2.1 — the single canonical
           definition owner per §5 D1. ARCHITECTURE.md §3 NOT modified
           (no duplicate definition). No ADR created; ADR index NOT modified.)
[x] tasks/backlog/TASK-080-*.md (this file — §5 answers, §5.1 dependency
                                 register, §8/§9/§15 records, Status field)
[ ] tasks/backlog/TASK-078-*.md (NO CHANGES — report readiness impact only)
[ ] tasks/blocked/TASK-079-*.md (NO CHANGES — read-only context)
[ ] tasks/blocked/TASK-036-*.md (NO CHANGES)
[ ] tasks/completed/ (NO CHANGES)
```

---

## 11. Implementation Notes

- **Keep three concepts separate** (`AGENTS.md` §12/§13; ADR-011 item 4).
  The recorded decision must not conflate:

  ```text
  Selection Surface        WHICH UI the player performs the step on
                               (this task — D1, a product + architecture choice)
      ≠
  Selection-State Owner    WHERE the in-progress selection lives before battle
                           start, and what GameRuntimePort exposes
                               (D2 — separate, not-created task)
      ≠
  BattleState snapshot     Server accepted the request and copied it into
                           PetState.EquippedCards[] / EquippedRelics[]
                               (server-authoritative, GAME_STATE.md §2.3)
  ```

  The client may **select/request**; it never authoritatively establishes
  `PetState`, `EquippedCards`, `EquippedRelics`, Damage, HP, or rewards.

- **Preserve the documented layering** (`ARCHITECTURE.md` §2.2.1):

  ```text
  React → GameShell → Phaser Scene → GameRuntime → API / SignalR
  ```

  Whichever surface is chosen, it must reach `POST /api/battle/start` through
  the existing documented path (GameRuntime owns `startBattle`, implemented by
  TASK-077 at `GameRuntime.ts:466`). A surface answer that requires a scene to
  call the transport directly violates `ARCHITECTURE.md` §2.2.1 rule 1 — if
  the chosen answer appears to require that, STOP rather than reinterpreting
  the rule.

- **ADR-003's Context is not its Decision.** `ADR-003:11` lists "loadout"
  among non-gameplay screens; `ADR-003:26-29` assigns Scenes to Phaser and
  menus/overlays to React without naming loadout selection. Do not cite the
  Context line as if it settled A1–A4, and do not treat ADR-003 as reopened by
  this task (its React+Phaser adoption decision stands).

- **Do not let the surface answer smuggle in D2.** "The selection lives in
  LobbyScene" mixes surface and state ownership. Surface answers must speak
  only about which UI performs and presents the step; state ownership is C1's
  explicit non-answer.

- **Precedent for this task's shape.** `TASK-061` (product-owner
  decision-input task: slots, `AGENT MUST NOT ANSWER`, awaiting-input report,
  verbatim recording) and `TASK-038`, `TASK-039`, `TASK-045`, `TASK-046`,
  `TASK-051`, `TASK-056` (readiness-audit decision tasks) are the structural
  precedents. `TASK-079` is the direct origin and records D1's wording.

- **ADR discipline.** `adr-change.md` §2 and `docs/03-decisions/README.md` §5
  forbid creating an ADR for a decision nobody has made; §1 states ADRs are
  historical records, not design proposals. Judge ADR need only after §5 is
  answered (§5 D2).

- **Do not resolve conflicts silently.** Where two documents genuinely
  contradict (rather than merely overlap), report per `AGENTS.md` §4 instead
  of choosing the easier reading.

---

## 12. Testing Requirements

### Required Verification

```text
[ ] Unit tests         — N/A: this task adds no code (core/validation.md §2
                         depth for a documentation/architecture-only change)
[ ] Integration tests  — N/A: no boundary is implemented
[ ] Gameplay scenarios — N/A: no gameplay rule is derived, changed, or
                         exercised (AGENTS.md §6 maps scenarios to rule docs;
                         none is touched)
[ ] Documentation consistency — each updated document re-read with its
                         referencing documents; exactly one definition owner
                         (documentation-change.md §2)
[ ] Architecture consistency  — the recorded surface is compatible with
                         ARCHITECTURE.md §2.2.1 rules 1–6 and introduces no
                         layer inversion or client-authoritative state
[ ] Preservation checks — CARD_RULES §1 / PET_RULES §2 / RELIC_RULES §2
                         counts and API_CONTRACTS §3 / §5.5 / §5.6 unchanged
                         (diff of those files shows no edit)
[ ] Task guard checks  — TASK-078 SHA-256 identical; TASK-079 SHA-256
                         identical; TASK-036 and tasks/completed/ unmodified;
                         git status shows no src/ or tests/ change
[ ] Scope/changed-file verification — the changed-file set equals the set
                         declared in §8
[ ] Existing guard suites — client and backend suites still pass UNMODIFIED
```

### Key Edge Cases

- A §5 slot with no Product Owner answer → report awaiting input; remain
  `BACKLOG`; do not answer, do not partially answer.
- An answer that mixes surface and state ownership → record the surface part;
  flag the state part as D2 and do not transcribe it.
- An answer that would change a card/relic/pet count → STOP (`AGENTS.md` §7);
  it is a gameplay change requiring the Rule Change Policy.
- An answer that appears to need a new endpoint or API field → STOP; report a
  separate API contract decision (`API_CONTRACTS.md` §1).
- An answer implying acquisition/gacha-style flow → STOP; `MVP_SCOPE.md` §2
  excludes Gacha.
- An answer requiring Pet/Card/Relic rows or identifiers → STOP; that is D3.
- Two documents genuinely contradicting on the settled surface → STOP per
  `AGENTS.md` §4; report both sources and the smallest correction for human
  approval.
- Pressure to expand into D2/D3 or to create those tasks → STOP per the
  Independent Dependency Rule (`TASK-079` origin); report only.

---

## 13. Stop Conditions

Universal stop conditions (`AGENTS.md` §20, `.ai/README.md` §13) always apply.
Task-specific conditions:

- **If the existing documentation already completely defines the selection
  surface** → STOP; report that no decision task is required, do not
  manufacture one. (Creation-time verification §2.3 found no such definition;
  re-verify at execution.)
- **If another task has since been created or started that resolves D1** →
  STOP; report it instead of duplicating the decision.
- **If §5 has no Product Owner answers** → this is the task's normal starting
  state, not a failure: report **awaiting Product Owner input**, leave the
  task `BACKLOG` in `backlog/`, and STOP. Do not answer it; do not partially
  answer it.
- **If an agent is about to fill in any `Decision:` field** → STOP; that is
  the single prohibited action of this task (`AGENTS.md` §7).
- **If proceeding requires choosing a surface to unblock anything else** →
  STOP; report what is missing rather than choosing.
- **If multiple authoritative documents conflict and no ownership rule
  resolves the conflict** → STOP per `AGENTS.md` §4.
- **If the decision requires D2 (selection-state owner) or D3 (content
  provisioning) to be resolved first, or would resolve them as a side
  effect** → STOP; report them as separate continuing dependencies. Do not
  expand this task (Independent Dependency Rule).
- **If the decision requires a new gameplay rule, changed count, or changed
  validation** → STOP per `AGENTS.md` §7.
- **If the decision requires a new endpoint, API field, or changed API
  behaviour** → STOP; report a separate API contract decision.
- **If the decision requires inventing Pet/Card/Relic/Boss identifiers or
  content** → STOP; content authoring, not a surface decision.
- **If the decision requires provisioning implementation** → STOP; D3 is a
  separate dependency.
- **If an ADR is about to be created while any §5 slot is unanswered** →
  STOP (`README.md` §5, `adr-change.md`).
- **If satisfying any criterion requires code, types, tests, migrations, or a
  scene** → STOP per `AGENTS.md` §8/§16.
- **If satisfying any criterion requires modifying TASK-078, TASK-079,
  TASK-036, or a completed task** → STOP; report to the dependent task's
  re-audit instead (`AGENTS.md` §16).
- **If completing this task would also require creating the D2 or D3 task**
  → STOP; report the need; creation is a repository-workflow decision, not
  this task's deliverable.
- **If the task exceeds 7 skills or crosses multiple uncoupled architectural
  boundaries** → STOP & decompose.

When stopped, report the exact condition and **do not invent a resolution**.

---

## 14. Dependencies

```text
TASK-075  Client collection read service        DONE — context
TASK-076  Client battle API service             DONE — context
TASK-077  Client battle-start orchestration     DONE — context; GameRuntime
          .startBattle implemented; port un-extended (":116")
TASK-078  Client LobbyScene match-start trigger BACKLOG — dependent; report
          readiness impact only; NOT modified; SHA-256 baseline recorded
          37EB51B92018EC64C5892A78F30E552C65739B1BAD4E04F5A20868BCA52A8181
TASK-079  Resolve client loadout acquisition    BLOCKED — origin of D1/D2/D3;
          read-only context; NOT modified; SHA-256 baseline recorded
          B60C0A986F9E0EE8AC5B4611980AD73C06293018641D52A6D4D6F43C114185AC
TASK-036  Discord credential secret hygiene     BLOCKED — not reopened, not
          modified; not a prerequisite here

Separate dependencies (owned elsewhere, NOT created or solved here):
  D2  in-progress selection-state ownership + GameRuntimePort responsibility
      (candidate owner: ARCHITECTURE.md §2.2.1)
  D3  Pet/Card/Relic static content provisioning
      (candidate owner: DATABASE.md §5 item 4, open per TASK-045 §6 issue 1)
```

Relationship:

```text
TASK-079 (BLOCKED)  recorded D1/D2/D3 as independent dependencies
        ↓
TASK-080 (this)     obtains + records the D1 surface decision only  ← DONE
        ↓
D2 task (future, not created here)   selection-state owner + port responsibility
D3 task (future, not created here)   content provisioning
D4 task (future, not created here)   which Boss the MVP hardcodes
        ↓
TASK-078 re-audit (report-only from this task) → TASK-078 readiness
```

---

## 15. Completion Evidence

### Decisions Recorded

```text
Product Owner decision:  CONFIRMED

A1 Choose Pet        S2 — Phaser LobbyScene
A2 Equip Cards       S2 — Phaser LobbyScene
A3 Equip Relics      S2 — Phaser LobbyScene
A4 Start Battle UI   S2 — Phaser LobbyScene
A5 Boss selection UI MVP hardcodes the Boss; no Boss selection UI
B1 counts preserved  confirmed — CARD_RULES §1 / PET_RULES §2 / RELIC_RULES §2
                     unchanged; no gameplay change; Rule Change Policy not
                     engaged
B2 API preserved     confirmed — API_CONTRACTS §3 / §5.5 / §5.6 unchanged;
                     no endpoint or field added; no ordering reinterpreted
B3 non-authoritative confirmed — Phaser LobbyScene submits a REQUEST only;
                     server remains authoritative (ADR-001, GAME_RULES.md §18)
C1 D2 not decided    confirmed — constrained by S2, not decided
C2 D3 not designed   confirmed — untouched
D1 definition owner  docs/02-technical/TDD.md §2.1 (single owner;
                     ARCHITECTURE.md §3 deliberately not restated)
D2 ADR warranted     no — no boundary/technology/ownership/authoritative-state
                     model changed; recorded in TDD.md §2.1 only; ADR-017 not
                     created; ADR index unmodified
```

### Follow-up Dependencies (unresolved — recorded, not resolved)

```text
D2  In-progress selection-state ownership + GameRuntimePort responsibility
    UNRESOLVED — constrained by the S2 decision; not decided by it.
D3  Pet/Card/Relic static content provisioning
    UNRESOLVED — DATABASE.md §5 item 4 (open per TASK-045 §6 issue 1).
D4  WHICH Boss the MVP hardcodes (new — surfaced by A5)
    UNRESOLVED — Product Owner decided "MVP hardcodes the Boss" without naming
    one; three are content-defined and provisioned (BOSS_RULES.md §6.4;
    migration 20260926151112_ProvisionBossDefinitions); no document designates
    a default; an agent choosing one would be authoring content (AGENTS.md §7).
```

### Changed Files

- `docs/02-technical/TDD.md` §2.1 — the `LobbyScene` bullet now records the
  Product Owner's surface decision: the MVP pre-battle selection flow
  (Choose Pet, Equip Cards, Equip Relics, Start Battle) is LobbyScene's
  responsibility; game-related interactive flows belong inside Phaser while
  React owns the application/platform shell; LobbyScene is
  presentation/interaction only and submits a request, never authoritative for
  `BattleState`/`PetState`/`EquippedCards[]`/`EquippedRelics[]`/Turn/Sequence/
  RNG/combat results; no MVP Boss-selection step or UI exists; equip/loadout
  state is battle-scoped and not read from collection endpoints
  (`API_CONTRACTS.md` §5.5–§5.6). Minimal, additive, within the existing
  bullet. Single definition owner (`documentation-change.md` §2).
- `tasks/backlog/TASK-080-resolve-loadout-selection-surface.md` — this file;
  §5 A1–A5 answers recorded verbatim, product decision and its scope stated,
  §5 B/C/D confirmations, §5.1 dependency register (D2/D3/D4), §8 actual change
  set, §9 acceptance criteria, §10 affected files, this record, Status → DONE.

No other file was changed. TASK-078, TASK-079, TASK-036, and all
`tasks/completed/` files are byte-identical.

### Validation Results

```text
Preservation        — PASS. CARD_RULES §1 / PET_RULES §2 / RELIC_RULES §2
                      counts unchanged; API_CONTRACTS §3 / §5.5 / §5.6
                      unchanged (no diff). No gameplay rule, count, formula,
                      or validation changed.
Architecture        — PASS. ARCHITECTURE.md §2.2.1 rules 1–6 intact; no layer
                      inversion introduced; no client-authoritative state
                      introduced; ADR-003 unmodified and not reopened.
Documentation       — PASS. Single definition owner (TDD.md §2.1); no
                      duplicated source of truth (documentation-change.md §2);
                      every claim in the recorded decision cites a document +
                      section; no source-of-truth conflict (AGENTS.md §4).
Task guards         — PASS. TASK-078 SHA-256: 37EB51B9…8181 → 37EB51B9…8181
                      (identical). TASK-079 SHA-256: B60C0A98…85AC →
                      B60C0A98…85AC (identical). TASK-036 and
                      tasks/completed/ unmodified.
Changed-file scope  — PASS. Changed set = {docs/02-technical/TDD.md,
                      tasks/backlog/TASK-080-*.md}, which equals the §8/§10
                      declaration. No src/ or tests/ diff added (count remains
                      the pre-existing baseline of 24).
Guard suites        — NOT run as a gate: no source or test file was modified,
                      so there is nothing to regress. They remain unmodified.
ADR index           — PASS. Unmodified (no ADR created).
```

### TASK-078 Readiness Impact (report-only)

- **Resolved by this task:** TASK-078's missing *surface* dependency (D1) is
  now recorded. TASK-078 can state that `LobbyScene` is the surface on which
  the player performs Choose Pet / Equip Cards / Equip Relics / Start Battle.
- **Still blocking TASK-078 (unchanged by this task):**
  - D2 — where the in-progress selection lives and whether `GameRuntimePort`
    exposes it. TASK-078 AC (L86, `GameRuntimePort` defines `startBattle`) and
    its request-construction AC remain underivable.
  - D3 — no valid Pet/Card/Relic content exists to select or submit.
  - D4 — which Boss `bossId` carries. TASK-078:117 proposes
    `"boss-hoa-long"`, but that is a task note, not a product decision, and a
    task is lower precedence than `docs/` (`AGENTS.md` §2).
- **Correctable in a later re-audit (NOT corrected here):** TASK-078:43 cites
  the non-existent `ADR-011-loadout-and-ownership-boundaries.md` (actual:
  `ADR-011-player-owner-pet-combat.md`).
- **TASK-078 remains NOT READY** and was not modified.

### Server Authority & Scope Verification

- [x] Confirmed zero client-authoritative gameplay logic introduced — no code
      was written at all
- [x] Confirmed `BattleState` / `PetState` / `EquippedCards` /
      `EquippedRelics` remain server-authoritative (`AGENTS.md` §10 / ADR-001)
      — restated in the TDD.md §2.1 wording
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1) — no new system,
      scene, endpoint, or content introduced
- [x] Confirmed no Pet/Card/Relic/Boss identifier was invented — A5's Boss was
      deliberately left unnamed (D4) rather than chosen by an agent
- [x] Confirmed no source, test, migration, or seed file changed
- [x] Confirmed no new API endpoint or field was added
- [x] Confirmed TASK-078, TASK-079, TASK-036, and all completed tasks are
      unmodified
- [x] Confirmed no `Decision:` field was completed by an agent — all five were
      supplied verbatim by the Product Owner
