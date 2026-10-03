# TASK-121 — Resolve Client Loadout and Active Pet Skill Visibility Contract

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ and src/ by path and section; copies only what an
  evaluator needs to know where to look.

  THIS TASK DOES NOT PRESELECT THE DECISION AND IMPLEMENTS NO CODE.
  The executing agent must determine the contract from authoritative evidence,
  record the approved resolution in the canonical owner document(s), and stop
  if the evidence is insufficient.
  It is a decision and documentation task created because TASK-120 is BLOCKED:
  the client requires authoritative EquippedCards[] and active Pet Signature
  Skill information to render CardCast and PetSkillCast action paths in BattleScene,
  but RuntimeBattleState.petState currently exposes only passiveId, passiveProgress,
  and passiveResetOverride (SIGNALR_PROTOCOL.md §4.3 item 2).

  PROVENANCE: Identified during the execution analysis of TASK-120. Under
  SIGNALR_PROTOCOL.md §4.3 item 2, BattleStateUpdated deliberately does not deliver
  loadout snapshots or Pet identity over SignalR, while API_CONTRACTS.md §3 provides
  EquippedCards[] at POST /api/battle/start, and GAME_STATE.md §2.3 maintains them in
  domain PetState. TASK-120 cannot proceed without resolving the authoritative
  client visibility and ingestion contract.
-->

---

## Metadata

```text
Task ID:           TASK-121
Title:             Resolve Client Loadout and Active Pet Skill Visibility Contract
Type:              DOCUMENTATION
Status:            DONE
Risk:              LOW
Priority:          HIGH
Primary Agent:     review
Supporting Agents: client, realtime, backend
Workflow:          documentation/documentation-change.md
Skills:            discovery/documentation-discovery,
                   discovery/impact-analysis,
                   quality/documentation-consistency,
                   quality/architecture-conformance,
                   quality/scope-validation
                   (5 skills — Normal budget, tasks/README.md §12)
Dependencies:      None
Blocks:            TASK-120
```

---

## Objective

Resolve the authoritative client visibility contract for `EquippedCards[]` and the active Pet Signature Skill so `TASK-120` can implement the client CardCast and PetSkillCast action surface in `BattleScene` using authoritative data, without client-authoritative gameplay, hardcoded registries, or uncoordinated wire modifications. Determine how the client obtains the authoritative information necessary to render:
1. Equipped Cards
2. Active Pet Signature Skill
by evaluating whether the client should source loadout information from the initial battle bootstrap (`POST /api/battle/start`), an expanded minimal SignalR projection (`SIGNALR_PROTOCOL.md` §4.3), or another documented mechanism; record the decision in the canonical owner document(s); and establish the exact unblocking specification for `TASK-120`.

---

## Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1 — 3 Basic Cards, Pet Signature Skill, server authority, SignalR realtime communication, and client presentation are IN scope
- `docs/00-overview/ROADMAP.md` §1 — Phase 1 Core Loop Vertical Slice: one playable battle start to finish against Bosses; 3 Basic Cards; one Pet fully implemented; server-authoritative resolution over SignalR
- `docs/01-game-design/CARD_RULES.md` §1 (Loadout constraints: 3 Basic Cards + 1 Pet Skill Card), §2 (Basic Cards), §3 (Casting rules), §4 (Pet Skill Cards & Signature Skill derivation)
- `docs/01-game-design/GAME_RULES.md` §11 (Cards), §12 (Power range), §18 (Server authority: client only requests actions)
- `docs/02-technical/API_CONTRACTS.md` §3 — `POST /api/battle/start` request and response payload (`initialState.petState.equippedCards` snapshot), §5 (collection read endpoints)
- `docs/02-technical/ARCHITECTURE.md` §2.2 (Client layering: scenes depend on `GameRuntimePort`, never directly on transport or HTTP), §2.2.3 (Pre-battle selection boundary: owned collection ≠ in-progress selection ≠ BattleState snapshot)
- `docs/02-technical/GAME_STATE.md` §2.3 — Domain `PetState.EquippedCards[]` (3 Basic Cards + 1 derived Signature Skill Card), `PetId`, combat stats, and loadout lifecycle
- `docs/02-technical/SIGNALR_PROTOCOL.md` §2 (`CardCast` and `PetSkillCast` client → server hub methods), §4.3 (`petState` wire projection rule: carries only `passiveId`, `passiveProgress`, `passiveResetOverride`), §5 (action acknowledgements), §7 (reconnect & resync)
- `docs/02-technical/TDD.md` §2.1 — Client responsibility split, scene lifecycle (`LobbyScene` → `BattleScene`), and runtime boundaries
- `docs/02-technical/DATABASE.md` §1 (`PetDefinition`, `CardDefinition`), §2 (`PlayerUnlockedCard`, `Pet`)
- `docs/03-decisions/ADR/ADR-001-server-authoritative-battle.md` — Server authority; client requests actions, server validates and resolves
- `docs/03-decisions/ADR/ADR-003-phaser-game-runtime.md` — Phaser scene lifecycle and battle presentation
- `docs/03-decisions/ADR/ADR-004-signalr-realtime.md` — SignalR realtime transport
- `docs/03-decisions/ADR/ADR-008-battle-recovery.md` — Snapshot-based battle reconnection
- `docs/03-decisions/ADR/ADR-011-player-owner-pet-combat.md` — Player = account owner; Pet = combat character; PetState = battle combat runtime (no PlayerState)
- `docs/03-decisions/ADR/ADR-012-player-level-pet-level-ownership.md` — Relic/Card ownership vs battle equip; no Card instances
- `docs/03-decisions/ADR/ADR-014-battle-state-player-identity.md` — `PetState.PetId` = owned Pet instance

---

## Current State

`TASK-120` is currently **BLOCKED**.

1. `SIGNALR_PROTOCOL.md` §4.3 item 2 explicitly defines that `petState` in `BattleStateUpdated` carries **exactly three members**: `passiveId`, `passiveProgress`, and the conditional `passiveResetOverride`. The rest of `GAME_STATE.md` §2.3 — identity, progression, combat stats, and both loadout snapshots (`EquippedCards[]`, `EquippedRelics[]`) — is not delivered over the SignalR push.
2. `src/backend/GameServer.Api/Hubs/BattleHub.cs` lines 147–151 and 809–831 confirm that `PetStatePayload` serializes only `passiveId`, `passiveProgress`, and `passiveResetOverride`.
3. `src/frontend/client/tests/GameRuntime.test.ts` lines 835–858 explicitly asserts that `petState` in `RuntimeBattleState` must NOT have properties `petId`, `equippedCards`, `power`, or combat stats.
4. However, `API_CONTRACTS.md` §3, `src/backend/GameServer.Api/Controllers/BattleStartStateSummary.cs` lines 107–109, and `src/frontend/client/src/services/api/BattleModels.ts` line 371 confirm that `POST /api/battle/start` returns `BattleStartResponse.initialState.petState.equippedCards` containing the 4 `CardDefinitionId` entries (3 Basics + 1 derived Signature Skill).
5. In `src/frontend/client/src/game/scenes/LobbyScene.ts` line 397 and `src/frontend/client/src/game/runtime/GameRuntime.ts` lines 472–482, `startBattle` orchestrates HTTP start, connects to SignalR, joins the battle group, and discards `response.initialState`, transitioning to `BattleScene` with `BattleScene` dependent solely on `GameRuntime.getBattleState()`, which receives only the partial SignalR projection.
6. The client currently has no documented mechanism to determine the active Pet's Signature Skill or equipped cards from synchronized runtime state without violating existing regression tests or inventing undocumented contracts.

---

## Critical Decision Questions

The executing agent must investigate and answer the following 10 questions using documented evidence:

1. **Where is authoritative `EquippedCards[]` defined?**
   Inspect `GAME_STATE.md` §2.3, `API_CONTRACTS.md` §3, and `DATABASE.md` §1–§2. Is it part of BattleState, part of PetState, part of another authoritative state object, available only during battle creation, available through an existing API, intentionally server-only, or otherwise exposed through an existing documented mechanism? How should the client receive/read it?
2. **Where is authoritative active Pet identity defined?**
   Inspect `GAME_STATE.md` §2.3, `DATABASE.md` §1–§2, and `ADR-014`. Is `PetId` or `PetDefinitionId` available to the client, and through which channel?
3. **Where is authoritative Signature Skill identity defined?**
   Inspect `CARD_RULES.md` §1, §4, and `DATABASE.md` §1 (`PetDefinition.SignatureSkillCardId`). Does `PetState.EquippedCards[]` (which contains 3 Basics + 1 Signature Skill) provide sufficient identity, or is dedicated metadata required to identify which card is the Signature Skill? Do NOT assume `petId → skill` can be resolved client-side. Do NOT create a local Pet database. Do NOT duplicate backend Pet definitions. Do NOT hardcode a mapping.
4. **How should the client obtain these values?**
   Should the client read them from `POST /api/battle/start`'s `initialState`, from an extended `BattleStateUpdated` push, or from a pre-battle runtime bridge?
5. **Is initial battle bootstrap sufficient?**
   What state is available during initial battle creation? What state is available after `BattleStateUpdated`? Does the client need the information only once? Must it remain synchronized? Can `EquippedCards[]` and Signature Skill identity safely remain static after battle creation throughout a single battle session (per `ROADMAP.md` Phase 1: "no rewards/persistence beyond a single battle")?
6. **Must the values be included in subsequent `BattleStateUpdated` pushes?**
   Does reconnect/resync (`SIGNALR_PROTOCOL.md` §7, `ADR-008`) require `equippedCards` in `BattleStateUpdated` or `GetBattleState`, or does mid-battle state synchronization only concern dynamic mutable values (`Power`, `HP`, `Board`, `PassiveProgress`)?
7. **Is the current SignalR omission intentional architecture or incomplete staging?**
   In `SIGNALR_PROTOCOL.md` §4.3 item 2 ("Domain state implemented is not the same as client wire delivery"), was omitting `EquippedCards[]` and `PetId` an intentional visibility/security boundary, or simply an incomplete client contract staged for future Card casting?
8. **What is the minimum client-visible contract?**
   Under the Minimal Data Principle, what is the exact minimal payload required (e.g. `cardId: string[]` vs full card definitions)? Distinguish required client visibility from full domain state exposure.
9. **Does the resolution require an Architectural Decision Record (ADR)?**
   Check `docs/03-decisions/ADR/` and `AGENTS.md` §18. Does bridging state from HTTP bootstrap to Phaser runtime or extending the SignalR projection require a new ADR?
10. **Which authoritative document owns the final contract?**
    Identify the canonical document(s) (`SIGNALR_PROTOCOL.md`, `GAME_STATE.md`, `API_CONTRACTS.md`, `ARCHITECTURE.md`, `CARD_RULES.md`, `GAME_RULES.md`, and/or a new ADR) where the resolution must be recorded.

---

## Architectural Options to Evaluate

The investigation must evaluate the following five options without preselecting an outcome:

- **Option A (Initial State Bootstrap Bridge):**
  The client already receives `initialState.petState.equippedCards` from `POST /api/battle/start` (`API_CONTRACTS.md` §3). `LobbyScene` / `GameRuntime` can bridge this initial authoritative loadout to the runtime session so `BattleScene` can access it via `GameRuntimePort`, preserving the existing lean SignalR push.
- **Option B (Minimal SignalR Projection Expansion):**
  Update `SIGNALR_PROTOCOL.md` §4.3 and `BattleHub.ToPetStatePayload` to include `equippedCards` (readonly string[]) in `PetStatePayload`, updating the frontend regression test in `GameRuntime.test.ts`.
- **Option C (Dedicated Read Endpoint / Pre-Battle Query):**
  Existing API is the correct authoritative bootstrap mechanism. Define a documented query or bootstrap contract that provides loadout snapshot metadata specifically for active battle presentation.
- **Option D (Alternative Existing Documented Mechanism):**
  Identify any existing documented contract or pattern in `docs/` that already specifies how scenes read active loadouts without transport violation.
- **Option E (Product/Architecture Stop Condition):**
  If authoritative documents conflict or intentionally prohibit client visibility of loadout data, record the exact unresolved question for Product Owner / Architecture ruling.

---

## Required Decision Coverage

The completed task must define:

```text
Authoritative source
        ↓
Client-visible projection/bootstrap
        ↓
Runtime representation
        ↓
BattleScene consumption
```

for:
- `EquippedCards[]`
- Active Pet Signature Skill

It must also define:
- Initial synchronization behavior
- Subsequent synchronization behavior if required
- Staleness rules
- Server authority preservation
- Client responsibilities and non-responsibilities

---

## Documentation Ownership

The task must identify the correct canonical owner for the resolved contract. Potential documents include:
- `docs/02-technical/GAME_STATE.md`
- `docs/02-technical/SIGNALR_PROTOCOL.md`
- `docs/02-technical/API_CONTRACTS.md`
- `docs/02-technical/ARCHITECTURE.md`
- `docs/01-game-design/CARD_RULES.md`
- `docs/01-game-design/GAME_RULES.md`
- `docs/03-decisions/ADR/`

Do not modify them during task creation. The executing agent will update the owning document(s) once the decision is approved.

---

## Scope

### In Scope
- Inspecting `GAME_STATE.md`, `SIGNALR_PROTOCOL.md`, `API_CONTRACTS.md`, `ARCHITECTURE.md`, `CARD_RULES.md`, and relevant ADRs.
- Producing a rigorous comparative analysis answering all 10 Critical Decision Questions.
- Evaluating Options A through E against server authority (`ADR-001`), realtime transport (`ADR-004`), and client boundaries (`ARCHITECTURE.md` §2.2).
- Determining whether an ADR is required per `AGENTS.md` §18.
- Specifying the exact target document(s) and proposed section edits.
- Documenting the resolution record and unblocking criteria for `TASK-120`.

### Out of Scope
- No SignalRService implementation.
- No GameRuntime implementation.
- No BattleScene implementation.
- No BattleHub implementation.
- No API implementation.
- No backend implementation.
- No frontend implementation.
- No Redis changes.
- No PostgreSQL changes.
- No gameplay changes.
- No card balance changes.
- No Pet Skill balance changes.
- No Match-3 changes.
- No combat changes.
- No Relic changes.
- No Boss Passive changes.
- No GetBattleState implementation.
- No TASK-120 modification.
- Modifying source code in `src/` (backend or frontend).
- Modifying test files in `tests/` (backend or frontend).
- Modifying `TASK-120`, `TASK-118`, or `TASK-119`.
- Authoring client-authoritative gameplay logic, local damage formulas, or client-side stat mutation.
- Creating frontend card/pet database stores or duplicating backend game rules.
- Introducing out-of-scope mechanics per `MVP_SCOPE.md` §2.

---

## Acceptance Criteria

- [x] The authoritative source of `EquippedCards[]` is identified.
- [x] The authoritative source of active Pet identity is identified.
- [x] The authoritative source of Signature Skill identity is identified.
- [x] The client acquisition mechanism is explicitly defined.
- [x] Initial synchronization behavior is explicitly defined.
- [x] Subsequent synchronization behavior is explicitly defined if required.
- [x] The minimum client-visible data is explicitly defined under the Minimal Data Principle.
- [x] Server authority is preserved (`GAME_RULES.md` §18, `ADR-001`).
- [x] No client-side gameplay authority is introduced.
- [x] No undocumented API or SignalR method is invented.
- [x] The canonical documentation owner is identified.
- [x] TASK-120 is explicitly unblocked only after the contract resolution is complete.
- [x] No source code is implemented by TASK-121.
- [x] All 10 Critical Decision Questions are answered with specific citations to authoritative documentation (`docs/`).
- [x] Architectural Options A through E are comparatively evaluated against architectural constraints and minimal data principles.
- [x] A determination is made on whether an ADR is required under `AGENTS.md` §18.

---

## Affected Files & Areas

```text
[ ] src/backend/ (None — decision/documentation only)
[ ] src/frontend/client/ (None — decision/documentation only)
[ ] tests/ (None — decision/documentation only)
[x] docs/02-technical/SIGNALR_PROTOCOL.md (Canonical contract owner: §4.3 updated)
[x] docs/02-technical/GAME_STATE.md (Dependent reference: §2.3 updated)
[ ] docs/02-technical/API_CONTRACTS.md (Verified consistent, unchanged)
[ ] docs/02-technical/ARCHITECTURE.md (Verified consistent, unchanged)
[ ] docs/03-decisions/ADR/ (Governed by existing ADRs; no new ADR required)
[ ] tasks/backlog/TASK-120-implement-client-card-and-skill-cast-action-paths.md (Downstream task unblocked; not modified)
```

---

## Implementation Notes

- **Separation of Concerns:** Keep transport concerns (`SIGNALR_PROTOCOL.md`), API contracts (`API_CONTRACTS.md`), and state architecture (`GAME_STATE.md`) in their documented canonical owners (`AGENTS.md` §2).
- **Client Boundary:** Remember `ARCHITECTURE.md` §2.2 Rule 1: "Scenes depend on the runtime port, never on the transport." Whether data comes from REST bootstrap or SignalR, `BattleScene` must consume it strictly through `GameRuntimePort`.
- **Regression Awareness:** Note that `tests/GameRuntime.test.ts:839` currently asserts `Object.keys(petState) == ['passiveId', 'passiveProgress']`. Any change to SignalR `PetStatePayload` requires updating this regression test in the downstream implementation task.
- **Minimal Data Principle:** The client only needs the opaque `cardId` strings to present interactive buttons and pass `cardId` to `requestAction({ kind: 'CardCast', cardId })`. Full card costs, damage magnitudes, and effects are validated and resolved server-side.

---

## Testing Requirements

### Required Verification
```text
[x] Documentation consistency audit across API_CONTRACTS.md, SIGNALR_PROTOCOL.md, and GAME_STATE.md
[x] Architecture conformance check against ARCHITECTURE.md §2.2 and ADR-001 / ADR-004
[x] Verification that downstream implementation scope for TASK-120 is fully unblocked and deterministic
```

### Key Edge Cases
- See `SIGNALR_PROTOCOL.md` §7 & `ADR-008`: Reconnection/resync mid-battle must be able to restore castable controls.
- See `API_CONTRACTS.md` §3: `cardLoadout` validation guarantees exactly 3 Basic Cards plus 1 derived Signature Skill in `EquippedCards[]`.

---

## Stop Conditions

- STOP if `EquippedCards[]` ownership is ambiguous.
- STOP if Pet identity ownership is ambiguous.
- STOP if Signature Skill identity is ambiguous.
- STOP if existing API and SignalR contracts conflict.
- STOP if current wire omission is intentionally security-sensitive but no approved exposure exists.
- STOP if a new wire field requires an architectural decision not already covered.
- STOP if the minimal client-visible representation cannot be determined.
- STOP if the resolution would require gameplay changes (`AGENTS.md` §7).
- STOP if the resolution would require inventing an API (`AGENTS.md` §20).
- STOP if the resolution would require inventing a SignalR method or event (`AGENTS.md` §20).
- STOP if the resolution would require duplicating server definitions on the client (`AGENTS.md` §7, §10).
- STOP if any option under consideration requires the client to calculate, validate, or mutate gameplay state (`AGENTS.md` §10).
- STOP if the decision requires changing frozen game design rules in `docs/01-game-design/` (`AGENTS.md` §7).
- STOP if an irreconcilable conflict between `API_CONTRACTS.md` and `SIGNALR_PROTOCOL.md` requires a human product owner ruling (`AGENTS.md` §4, §20).

---

## Completion Evidence

### Recorded Decision

Resolved client loadout visibility under **Option B (Minimal SignalR Projection Expansion)**:
1. `SIGNALR_PROTOCOL.md` §4.3 is extended to project `equippedCards: string[]` (the 4 `CardDefinitionId` strings: 3 Basic Cards + 1 derived Signature Skill Card) inside `petState` in the `BattleStateUpdated` push.
2. `BattleState.PetState.EquippedCards[]` (`GAME_STATE.md` §2.3) is the sole authoritative source of equipped cards during battle.
3. The client receives `equippedCards` over SignalR upon initial join (`JoinBattle`) and on every `BattleStateUpdated` push (including reconnect recovery via `GetBattleState`, `SIGNALR_PROTOCOL.md` §7, `ADR-008`), establishing a unified, single-source runtime synchronization path (`ARCHITECTURE.md` §2.2.1).
4. `GameRuntime` stores `equippedCards` under `RuntimeBattleState.petState.equippedCards` and exposes it via `GameRuntimePort.getBattleState()`.
5. `BattleScene` presents casting controls for each card and dispatches `CardCast` and `PetSkillCast` requests through `GameRuntimePort.requestAction` (`SIGNALR_PROTOCOL.md` §2). Full card mechanics, power costs, and effects remain strictly server-authoritative (`CARD_RULES.md` §3, `GAME_RULES.md` §18, `ADR-001`).
6. `response.initialState` from `POST /api/battle/start` is not promoted into runtime state, preserving the architectural invariant of `ARCHITECTURE.md` §2.2.1 and `tests/GameRuntime.test.ts:1331`.
7. No new API, Hub method, event, or ADR is required. Existing ADRs (ADR-001, ADR-004, ADR-008, ADR-011, ADR-012, ADR-014) fully govern this boundary.

### Decision Evidence

- `docs/01-game-design/CARD_RULES.md` §1 (Loadout composition: 3 Basic Cards + 1 derived Pet Skill Card), §3 (Server-authoritative validation & resolution), §4 (Signature Skill derivation)
- `docs/01-game-design/GAME_RULES.md` §18 (Server authority: client only sends action requests; server determines all state)
- `docs/01-game-design/PET_RULES.md` §2 (Active Pet as combat character; locks in Element, Passive, Signature Skill)
- `docs/02-technical/GAME_STATE.md` §0 (Staged state: absent fields are not yet implemented; stages strictly extend §2), §2.3 (`PetState.EquippedCards[]`, `PetState.PetId`), §2.8 (`BattleState.PlayerId` excluded from wire)
- `docs/02-technical/SIGNALR_PROTOCOL.md` §0 (State delivery stages), §2 (`CardCast` and `PetSkillCast` signatures; PetSkillCast implies active Pet's Signature Skill), §4 (`BattleStateUpdated` push), §4.3 (Delivering Pet / Passive stage), §7 (Snapshot-based reconnect / resync)
- `docs/02-technical/ARCHITECTURE.md` §2.2.1 (GameRuntime coordination; single synchronization path via SignalR push), §2.2.3 (Ephemeral LobbyScene selection vs server-authoritative BattleState snapshot)
- `docs/02-technical/API_CONTRACTS.md` §3 (`POST /api/battle/start` creates battle snapshot), §4 (Active battle state only available via SignalR), §5.3 (`GET /api/cards` provides category Basic vs PetSkill)
- `docs/03-decisions/ADR/ADR-001-server-authoritative-battle.md` (Server authority)
- `docs/03-decisions/ADR/ADR-004-signalr-realtime.md` (SignalR realtime transport)
- `docs/03-decisions/ADR/ADR-008-battle-recovery.md` (Snapshot-based resync)
- `docs/03-decisions/ADR/ADR-011-player-owner-pet-combat.md` (PetState owns battle loadout and combat stats)
- `docs/03-decisions/ADR/ADR-014-battle-state-player-identity.md` (PetState.PetId is owned instance; wire exclusion distinction)
- `src/frontend/client/src/game/runtime/GameRuntime.ts` lines 431–436 (Synchronized copy established only by SignalR push; REST initialState deliberately not promoted)
- `src/frontend/client/tests/GameRuntime.test.ts` lines 1180–1183, 1331–1335 (Regression test asserting REST initialState is never promoted into runtime state)

### Selected Option

Option B — Minimal SignalR Projection Expansion

### Client Visibility Contract

#### EquippedCards
- **Authoritative Source:** `BattleState.PetState.EquippedCards[]` (`GAME_STATE.md` §2.3).
- **Wire Delivery:** Carried in `BattleStateUpdated`'s `petState.equippedCards` as `readonly string[]` containing exactly 4 `CardDefinitionId` strings (3 Basic + 1 PetSkill) (`SIGNALR_PROTOCOL.md` §4.3 item 13).
- **Client Presentation:** Exposed via `GameRuntimePort.getBattleState().petState.equippedCards`. `BattleScene` renders interactive buttons for each card definition identity.
- **Action Invocation:** For Basic Cards, the player invokes `runtime.requestAction({ kind: 'CardCast', cardId })` routing to `BattleHub.CardCast(battleId, cardId, clientSequence)`.

#### Active Pet
- **Authoritative Source:** `BattleState.PetState.PetId` (`GAME_STATE.md` §2.3, `ADR-014`), representing the validated `Pet.PetInstanceId` (`DATABASE.md` §1) locked in at battle start.
- **Wire Status:** Excluded from the wire payload; not required by the client for battle action dispatch.
- **Display Identity:** The Pet's passive identity `petState.passiveId` is delivered in `BattleStateUpdated` (`SIGNALR_PROTOCOL.md` §4.3 item 3), providing unambiguous technical correlation to the Pet definition if needed.

#### Signature Skill
- **Authoritative Source:** `PetDefinition.SignatureSkillCardId` (`DATABASE.md` §1, `CARD_RULES.md` §4), derived at battle creation into `PetState.EquippedCards[]` (`API_CONTRACTS.md` §3).
- **Client Identification:** Identified from `equippedCards` as the unique entry having `Category == PetSkill` per card definition metadata (`API_CONTRACTS.md` §5.3, `DATABASE.md` §1).
- **Action Invocation:** `BattleScene` dispatches `runtime.requestAction({ kind: 'PetSkillCast' })` routing to `BattleHub.PetSkillCast(battleId, clientSequence)`. Under `SIGNALR_PROTOCOL.md` §2, `PetSkillCast` is a shorthand where the active Pet's Signature Skill is implied, requiring no card or skill identifier from the client.

### Synchronization

#### Initial Bootstrap
The client starts the battle via `GameRuntimePort.startBattle(request)` (`POST /api/battle/start`). The REST response supplies transport endpoints (`battleId`, `signalrHub`). The client connects to SignalR and joins the battle group (`JoinBattle`). Group join triggers the server-initiated `BattleStateUpdated` push (`SIGNALR_PROTOCOL.md` §4.1), delivering the initial synchronized `RuntimeBattleState` including `petState.equippedCards`. The REST response's `initialState` is discarded and never promoted into runtime state (`GameRuntime.ts:431–436`, `tests/GameRuntime.test.ts:1331–1335`).

#### Subsequent Updates
`equippedCards` is immutable during battle (`CARD_RULES.md` §1, `GAME_STATE.md` §2.3). It is delivered identically in every `BattleStateUpdated` push following resolved actions (Swap, CardCast, PetSkillCast) and in the reconnect snapshot (`GetBattleState`, `SIGNALR_PROTOCOL.md` §7, `ADR-008`). If a client disconnects and reconnects mid-battle, `GetBattleState` restores the full synchronized `RuntimeBattleState` including `equippedCards`.

### Authority

- **Server Authority:** The server remains 100% authoritative (`GAME_RULES.md` §18, `ADR-001`). The server validates power costs, loadout membership, cooldowns, and resolves all card/skill effects. The client never calculates damage, modifies power, or asserts local cast validity.
- **Client Presentation:** The client is strictly a presenter and action requester (`ARCHITECTURE.md` §2.2, `TDD.md` §2.1). It renders buttons from `equippedCards` and displays server acknowledgements (`accepted: true/false`, `reason`).

### Canonical Documentation

- `docs/02-technical/SIGNALR_PROTOCOL.md` §4.3 (Canonical contract owner: wire projection tree, payload shape, and item 13 specification)
- `docs/02-technical/GAME_STATE.md` §2.3 (Canonical state owner: wire delivery synchronization note)

### Unblocking Status for TASK-120

- [x] The resolved authoritative data-access contract is explicitly recorded and is sufficient for TASK-120 to consume.
- [x] TASK-120 is identified as unblocked by this decision.
- [x] TASK-120 itself was not modified by TASK-121.
