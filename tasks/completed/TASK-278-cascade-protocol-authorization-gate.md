# TASK-278 — Cascade Protocol Authorization Gate

---

## Metadata

```text
Task ID:           TASK-278
Type:              ARCHITECTURE / GOVERNANCE & DECISION PREPARATION
Status:            DONE
Risk:              MEDIUM
Priority:          HIGH
Mode:              Controlled Repository Workflow — decision record plus the
                   implementation the decision authorizes
Production code:   CHANGED under the §3.6 authorization (the backend pass-transition
                   report, the wire projection, and the client per-pass Fall/Refill
                   presentation)
Primary Agent:     orchestrator
Supporting Agents: backend, client, review
Workflow:          architecture/adr-change.md
Skills:            quality/implementation-review, quality/documentation-consistency
Dependencies:      TASK-254 (DONE — DR-1 option B), TASK-272 (IN REVIEW), TASK-273 (DONE),
                   TASK-274 (DONE), TASK-275 (IN REVIEW — CONDITIONAL PASS),
                   TASK-276 (BACKLOG — planning), TASK-277 (BACKLOG — decision gate)
Declared Files:    docs/01-game-design/GAME_RULES.md,
                   docs/01-game-design/MATCH3_RULES.md,
                   docs/02-technical/GAME_EVENTS.md,
                   docs/02-technical/GAME_STATE.md,
                   docs/02-technical/SIGNALR_PROTOCOL.md,
                   docs/03-decisions/README.md,
                   docs/03-decisions/ADR/ADR-027-authoritative-cascade-pass-transition-reporting.md,
                   src/backend/GameServer.Domain/Match3/BattleEvent.cs,
                   src/backend/GameServer.Domain/Match3/BattleEventBuilder.cs,
                   src/backend/GameServer.Domain/Match3/BoardResolver.cs,
                   src/backend/GameServer.Domain/Match3/GravityAndSpawn.cs,
                   src/backend/GameServer.Api/Hubs/BattleEventWireProjection.cs,
                   src/frontend/client/src/game/scenes/BattleEventPresenter.ts,
                   src/frontend/client/src/game/scenes/BattleScene.ts,
                   src/frontend/client/tests/BattleEventPresentation.test.ts,
                   src/frontend/client/tests/SceneLifecycle.test.ts,
                   tests/backend/GameServer.Api.Tests/ApiIntegrationTests.cs,
                   tests/backend/GameServer.Application.Tests/BattleStateServiceDeadlockRecoveryTests.cs,
                   tests/backend/GameServer.Application.Tests/BattleStateServiceTests.cs,
                   tests/backend/GameServer.Application.Tests/BossResponseTests.cs,
                   tests/backend/GameServer.Application.Tests/RoundTestSupport.cs,
                   tests/backend/GameServer.Domain.Tests/BattleEventEmissionTests.cs,
                   tests/backend/GameServer.Domain.Tests/PlayerEffectHealingTests.cs,
                   tests/backend/GameServer.Domain.Tests/ResourceGenerationTests.cs
Verification:      git diff --check; §7.3 (authorized-implementation evidence) and §7.4
                   (closure-readiness review) — see §7. (The prior field cited a §8 that
                   this record does not contain; corrected under §7.5, item C-1.)
```

---

## Declared File Set (P-2 / P-5)

Exact declared implementation files for TASK-278 — the 24 paths the §3.6 Operator
Decision authorized, verified by the closure-readiness review (§7.4), and declared by
the Operator's §7.5 decision (D-278-1 = A). The list below must strictly match the
`Declared Files:` field in `## Metadata`:

```text
docs/01-game-design/GAME_RULES.md
docs/01-game-design/MATCH3_RULES.md
docs/02-technical/GAME_EVENTS.md
docs/02-technical/GAME_STATE.md
docs/02-technical/SIGNALR_PROTOCOL.md
docs/03-decisions/README.md
docs/03-decisions/ADR/ADR-027-authoritative-cascade-pass-transition-reporting.md
src/backend/GameServer.Domain/Match3/BattleEvent.cs
src/backend/GameServer.Domain/Match3/BattleEventBuilder.cs
src/backend/GameServer.Domain/Match3/BoardResolver.cs
src/backend/GameServer.Domain/Match3/GravityAndSpawn.cs
src/backend/GameServer.Api/Hubs/BattleEventWireProjection.cs
src/frontend/client/src/game/scenes/BattleEventPresenter.ts
src/frontend/client/src/game/scenes/BattleScene.ts
src/frontend/client/tests/BattleEventPresentation.test.ts
src/frontend/client/tests/SceneLifecycle.test.ts
tests/backend/GameServer.Api.Tests/ApiIntegrationTests.cs
tests/backend/GameServer.Application.Tests/BattleStateServiceDeadlockRecoveryTests.cs
tests/backend/GameServer.Application.Tests/BattleStateServiceTests.cs
tests/backend/GameServer.Application.Tests/BossResponseTests.cs
tests/backend/GameServer.Application.Tests/RoundTestSupport.cs
tests/backend/GameServer.Domain.Tests/BattleEventEmissionTests.cs
tests/backend/GameServer.Domain.Tests/PlayerEffectHealingTests.cs
tests/backend/GameServer.Domain.Tests/ResourceGenerationTests.cs
```

*(Note: the task manifest file itself is excluded from the implementation declared file
set (`tasks/TASK_TEMPLATE.md`); it is filed by Phase B. This section and the matching
`Declared Files:` field were amended under the §7.5 Operator decision — the prior field
declared three task records and no implementation file, so it could not satisfy the
Phase A exact-set staging verification of `tasks/TASK_LIFECYCLE.md` §6.1 / P-5.)*

---

## 1. Executive Summary & Purpose

TASK-277 established that presenting per-pass gravity/drop and refill/spawn transitions in the client is **not merely an implementation task**: it requires a protocol change that directly contradicts multiple authoritative prohibitions in `SIGNALR_PROTOCOL.md` (§3.1 item 2, §3.1 item 3, §3.2.12 item 1, §8 items 5 and 7), `GAME_EVENTS.md` (§1.1 line 148, §3 item 8), `MATCH3_RULES.md` (§4.4 item 7), and `GAME_STATE.md` (§3, §5.1 item 2, §5.3 item 4).

Under `AGENTS.md` (§4, §7, §17, §18, §20, §23), an AI agent is strictly forbidden from inventing game rules, modifying protocol contracts without prior approval, or interpreting operator silence as authorization.

The purpose of **TASK-278** is to establish the formal **Authorization Gate**:
1. State the exact **decision authority**, **required approval artifact**, and **mandatory document-update order** required before any protocol change may proceed.
2. Present a comprehensive **Operator / Product-Owner Decision Brief** where **A-1** (authorization of per-pass cascade presentation) and **A-4** (reconciliation of explicit prohibitions) are **explicitly marked PENDING**.
3. Detail the recommended minimal protocol change (the TASK-277 deltas-only proposal) as an **option under consideration**, not an approved decision, alongside the consequences of retaining the current settle-only presentation.
4. Provide independent, unverified follow-up investigation records for two potential production code defects:
   - **TASK-279**: Suspected multi-match `GemMatched` duplication in `BattleEventBuilder`.
   - **TASK-280**: Suspected state-push-before-projection batch-loss path in `BattleHub`.
5. Establish a **revised implementation dependency graph** delineating exactly which tasks remain blocked pending authorized decisions.

---

## 2. Decision Authority, Required Approval Artifact & Mandatory Document-Update Order

### 2.1 Exact Decision Authority

Per the repository's governance contract (`AGENTS.md` §2, §7, §18, §20, §23):

```text
Source-of-Truth Hierarchy:
  GAME DESIGN (docs/00-overview/, docs/01-game-design/)
    ↓
  TECHNICAL DESIGN (docs/02-technical/)
    ↓
  ARCHITECTURAL DECISIONS (docs/03-decisions/ADR/)
    ↓
  AI AGENTS / WORKFLOW (.ai/)
    ↓
  TASKS
    ↓
  CODE
    ↓
  TESTS
```

1. **Game Design & Gameplay Events Authority:**
   The Product Owner and Systems Designer hold sole decision authority over game design, game rules, and canonical event definitions (`AGENTS.md` §2, §7; `GAME_EVENTS.md` §3 item 8). Technical design cannot invent or authorize gameplay mechanics or events.
2. **Technical Architecture Authority:**
   The Backend and Client Architecture deciders (`ADR-026` lines 4–6) govern protocol schemas and network delivery mechanisms, but **cannot override game rules or MVP scope** (`AGENTS.md` §2, §8; `docs/03-decisions/README.md` §3).
3. **AI Agent Authority:**
   **Zero.** AI agents must never resolve conflicts unilaterally, assume design intent, or mark proposals approved without explicit repository evidence (`AGENTS.md` §4, §20, §23). Silence is never approval.

### 2.2 Required Approval Artifact

An Architectural Decision Record (ADR) is **not** an approval artifact. As established by `docs/03-decisions/README.md` §1 and confirmed by the TASK-265 `BoardReshuffled` precedent (`ADR-026:104-113`):
> "ADRs are historical records, not design proposals. Writing an ADR does not create or change a decision... An ADR may not create a contract."

The **required approval artifact** is an explicit written sign-off by the **Product Owner / Operator** recorded in repository evidence (e.g. an approved decision record or signed-off task document) addressing the decision items A-1 through A-7. Until this artifact exists in the repository, all related protocol and code modifications remain blocked.

### 2.3 Mandatory Document-Update Order

The document-update sequence is strictly governed by precedence (`AGENTS.md` §2; `GAME_EVENTS.md` §3 items 5 and 8; `TASK-275:312-316`; `TASK-277` §9.1). TASK-276's proposal to draft an ADR first and skip game rules was a governance violation.

The mandatory, immutable document-update order is:

```text
Step 0: Operator / Product-Owner explicit approval artifact recorded in repository evidence.
        (No document or code is modified before this step).

Step 1: docs/01-game-design/GAME_RULES.md §16
        Add `CascadeStepResolved` to the canonical event-name list.
        Owner: Game Design / Product Owner.

Step 2: docs/02-technical/GAME_EVENTS.md §1.1, §2, §3
        Define event trigger, semantic payload, and exact resolution cycle placement
        (last item of each pass, before next pass's CascadeCreated or following stage).
        Owner: Technical Design (Event Semantics).

Step 3: docs/02-technical/SIGNALR_PROTOCOL.md
        - §3.2.2: Add discriminator to the closed 18-name wire set (becoming 19).
        - §3.2.28 (new): Specify exact wire schema, casing, member types, and omissions.
        - §3.1 item 2, §3.1 item 3, §3.2.12 item 1, §8 items 5 & 7: Reconcile explicit prohibitions per A-4.
        - §3.2.2: Authoritatively define the tolerant-reader rule for additive events.
        Owner: Technical Design (Wire Contract).

Step 4: docs/02-technical/GAME_STATE.md §3, §5.1 item 2, §5.3 item 4
        Clarify that while an event *report* of the pass transition is delivered in the
        round batch, intermediate board states remain transient, unpersisted in Redis,
        and absent from snapshots.
        Owner: Technical Design (State & Persistence).

Step 5: docs/03-decisions/ADR/ (ADR-027 or ADR-026 Amendment)
        Record the architectural decision LAST as a historical record of what the
        governing documents have established.
        Owner: Architecture.

Step 6: Contract Enforcement Tests
        Update ApiIntegrationTests.cs, SignalRWireJson.cs, and presentation tests to
        reflect the new discriminator and schema invariants.

Step 7: Production Code Implementation
        Backend Domain & Wire Projection → Client Presenter & Presentation Pipeline.
```

---

## 3. Operator / Product-Owner Decision Brief

### 3.1 Question A-1: Authorize Per-Pass Cascade Presentation?

```text
STATUS: APPROVED — OPTION (i) AUTHORIZED BY THE OPERATOR / PRODUCT OWNER (§3.6)
```

- **Option (i) — Authorize Protocol Change (SELECTED):**
  Authorize adding one additive wire event (`CascadeStepResolved`) to the atomic round batch, amending the owning documents per §2.3 to permit client-side per-pass drop and spawn animations.
- **Option (ii) — Retain Settle-Only Presentation (TASK-254 DR-1 Option A) (NOT SELECTED):**
  Decline the protocol change. Retain the current production architecture where the round's single committed board transitions in bulk during the `settle` phase. Close TASK-254 DR-1 as an accepted permanent limitation.

### 3.2 Question A-4: Reconcile Prohibiting Rules & Dependent Contracts

```text
STATUS: APPROVED — RECONCILIATION AUTHORIZED WITH A-1(i) (§3.6)
```

If Option A-1(i) is selected, the following 10 explicit contract prohibitions must be formally amended:
1. `SIGNALR_PROTOCOL.md` §3.1 item 2: *"No per-step delivery. The intermediate states of a resolution... are Transient Resolution State... and are never sent. There is no 'gravity' message, no per-pass message, and no partial batch."*
2. `SIGNALR_PROTOCOL.md` §3.1 item 3: *"No gameplay message type is introduced. The board and the resolution's resulting values travel in the state push of §4... resolution's events travel in §3..."*
3. `SIGNALR_PROTOCOL.md` §3.2.12 item 1: *"No event carries a battleId, a serverSequence, a turn, a board, a combo on a non-ComboChanged event..."*
4. `SIGNALR_PROTOCOL.md` §8 item 5: Prohibits board-specific resolution messages.
5. `SIGNALR_PROTOCOL.md` §8 item 7: Prohibits board-resolution-specific messages (*"no BoardResolved, no CascadeUpdated, no SpecialGemActivated message..."*).
6. `GAME_EVENTS.md` §1.1 line 148: *"gravity + spawn (no events: MATCH3_RULES.md §4.4 item 7)"*.
7. `MATCH3_RULES.md` §4.4 item 7: *"Gravity produces no Match, no Combo, and no events."*
8. `GAME_STATE.md` §3, §5.1 item 2, §5.3 item 4: Specifies that intermediate cascade configurations are transient and never sent or snapshotted.
9. `TASK-245` PD-4: Explicit binding decision prohibiting per-pass board snapshots.
10. `ApiIntegrationTests.cs:4058-4060`: Test assertion enforcing that no event item carries a board.

**Reconciliation Mechanism:**
Amend these sections to state that an event **report of the pass transition** (`dropMoves` and `spawnedGems`) is delivered within the existing atomic `ReceiveEvents` batch, while intermediate board states remain transient, unpersisted, and non-snapshotted.

---

### 3.3 Protocol Change Proposals: Deltas-Only vs Hybrid

*(Presented as options under evaluation; neither is approved until A-1 is decided).*

| Feature | TASK-277 Proposal (Deltas-Only) — RECOMMENDED | TASK-276 Proposal (Hybrid) — NOT RECOMMENDED |
| :--- | :--- | :--- |
| **Event Discriminator** | `CascadeStepResolved` | `CascadeStepResolved` |
| **Pass Index** | `passDepth` (1-based int; 1 for swap, $\ge 2$ for cascades) | `passDepth` (1-based int) |
| **Movement Data** | `dropMoves`: `[{ fromCell, toCell }]` | `dropMoves`: `[{ fromCell, toCell }]` |
| **Refill Data** | `spawnedGems`: `[{ cellIndex, gemType }]` | `spawnedGems`: `[{ cellIndex, gemType, specialGem }]` |
| **Board Payload** | **OMITTED** (Redundant, violates §3.2.12 item 1 & §8 item 5) | **INCLUDED** (Full 64-cell `BoardPayload`) |
| **Actor Member** | **OMITTED** (Carried by sibling `SwapStarted.actor`) | **INCLUDED** (`"player"` or `"boss"`) |
| **Cleared Cells Member**| **OMITTED** if D-1 fixed; otherwise included | **INCLUDED** (`clearedCells[]`) |
| **Wire Schema Names** | Explicitly attributed with `[JsonPropertyName]` | Embeds unattributed default camelCase DTOs |
| **Payload Size Added** | **≈ 0.5 KB** per pass (≈ 1.0–2.0 KB per round) | **≈ 1.6–2.0 KB** per pass (≈ 3.5–10.7 KB per round) |
| **Contract Invariants** | Cleanly reconstructs board: $B_{k+1} = B_k - \text{cleared} + \text{drops} + \text{spawns}$ | Redundant dual authority (deltas vs board) |

**Recommendation Rationale:**
The deltas-only proposal is the minimal sufficient protocol change. It delivers the exact physical movements the client lacks without transmitting redundant 64-cell board states, preserving the fundamental architectural rule that full boards travel exclusively in `BattleStateUpdated` state pushes and `GetBattleState` snapshots.

---

### 3.4 Consequences of Retaining Settle-Only Presentation (TASK-254 DR-1 Option A)

If Option A-1(ii) is selected, the repository maintains the current settle-only architecture:

#### Positive Consequences (Continuity & Stability)
1. **Zero Protocol Risk:** Requires zero contract changes, zero ADR revisions, and zero modifications to `SIGNALR_PROTOCOL.md` or `GAME_RULES.md`.
2. **Proven Correctness:** TASK-275 already passed conditional verification (`CONDITIONAL PASS`) under this model. Turn banners, match destruction overlays, and audio/visual callouts function reliably.
3. **Guaranteed Authority:** Absolutely zero risk of client-side board desynchronization during cascade sequences.
4. **Minimal Wire Footprint:** No additional bytes added to the WebSocket connection.
5. **Robust Concurrency:** State push, sequence admission, and snapshot recovery logic remain simple and battle-tested.

#### Negative Consequences (Player Experience Limitations)
1. **No Per-Pass Falling Animations:** Gems matched in cascades (passes $\ge 2$) do not visually drop column-by-column in sequential beats; they disappear, and the board updates in bulk at the end of the round (`settle` phase).
2. **Match Confirmation Gating:** As proven in TASK-275, confirmed gem destruction can only clear rendered cells for Pass 1. For Pass 2+, rendered cells cannot match server-cleared coordinates because intermediate gravity did not occur visually. Refill entrance is delayed until the end of the round.
3. **Reduced Visual Punch:** Multi-cascade combos feel less dynamic than commercial match-3 titles that feature rhythmic per-cascade drop animations.

---

### 3.5 Technical Implications

#### 1. Compatibility
- **Server:** `BattleEventWireProjection.cs:803-806` throws on unknown event kinds. `ApiIntegrationTests.cs:4006-4007` fails on any undocumented discriminator. Adding an event requires synchronized code and test updates.
- **Client:** `BattleEventPresenter.ts:252-255` safely drops unrecognised event kinds. Older clients will ignore `CascadeStepResolved` without throwing, falling back cleanly to the `settle` phase.
- **No Version Handshake:** There is no capability negotiation or version channel in the protocol (`C-7`). Changes must maintain forward and backward wire compatibility.

#### 2. Payload Overhead
- Deltas-only adds ≈0.5 KB per pass (≈1.0 KB for a typical 2-pass round; ≈2.0 KB for 4 passes; ≈4.0 KB in pathological full-board clear scenarios).
- Hybrid adds an additional 1.1–1.3 KB per pass.
- While WebSocket frame sizes can easily accommodate either, deltas-only avoids wasteful repetition.

#### 3. Recovery & Desynchronization
- Gated strictly by `ADR-008` and `SIGNALR_PROTOCOL.md` §7 (`GetBattleState`).
- Recovery snapshots carry only settled authoritative state and **never replay cascade history**.
- If a client disconnects or falls out of sync mid-cascade, the recovery snapshot instantly converges the board to the final settled state, terminating in-flight presentation.

#### 4. Delivery Integrity & Network Edge Cases
- SignalR broadcasts occur after single-key Redis CAS commit.
- If network connection drops between Redis CAS and SignalR broadcast, events are lost; recovery occurs via `GetBattleState`.
- Sequence admission safeguards (TASK-273/TASK-274) remain unweakened: batches ahead of held sequence are withheld pending snapshot.

---

### 3.6 Decision Record — Operator / Product-Owner approval artifact

This subsection is the **Step 0 artifact** §2.3 requires. It records the decision the
owning documents were then amended to state; it is not an ADR (`§2.2`).

```text
Decision:          APPROVED — Minimal Authoritative Cascade Protocol Extension
Decided by:        Operator / Product-Owner
Scope decided:     TASK-278 A-1 (i) and A-4
Event authorized:  CascadeStepResolved
Members:           passDepth, dropMoves (source + destination per movement),
                   spawnedGems (destination + gem type)
Emission:          once per recorded detection pass, only after that pass's Gravity
                   and refill resolution, inside the existing ReceiveEvents batch
Client use:        orchestrate the pass's Match, Fall and Refill presentation, then
                   subsequent cascade passes in the prescribed order
Turn boundary:     combat effects and the boss-turn handoff occur only after the
                   required board resolution and animation lifecycle completion
```

Operator constraints (binding, each verified in §7.3):

```text
1. no full board snapshot per cascade pass
2. no client-side simulation of the authoritative RNG
3. no change to Redis CAS semantics
4. no change to existing event sequence semantics
5. existing turn banner behaviour preserved
6. no second authoritative source of truth for board state
7. the approved per-pass animation requirements preserved
```

Operator-mandated implementation checks (each recorded in §7.3): event ordering and
pass identification; compatibility with existing `ReceiveEvents` consumers; duplicate
delivery, retry and replay behaviour; validation of coordinates, Gem types and pass
ordering; client recovery when a pass event is missing or invalid; and deterministic
tests proving the boss cannot act on an incompletely resolved board.

**The decision authorizes implementation within this scope. It does not certify the
implementation, its test results, deployment, or task completion.** This record
therefore stays `BACKLOG` and `OPEN` until the backend and frontend changes, tests,
and verification evidence have been reviewed.

Consequential correction recorded with the decision: `MATCH3_RULES.md` §4.4 item 7 was
amended (it is prohibition 7 of §3.2) so that Gravity still produces no Match and no
Combo and emits no event of its own, while the *pass transition* it completes with
Spawn is reported afterwards by that pass's `CascadeStepResolved`.

---

## 4. Proposed Follow-Up Defect Investigations

During protocol analysis in TASK-277, two potential production code defects were identified. Per `AGENTS.md` §16, they are reported and isolated into independent investigation tasks:

### 4.1 TASK-279: Multi-Match `GemMatched` Duplication in `BattleEventBuilder`
- **Status:** BACKLOG
- **Label:** **UNVERIFIED (Code-Level Finding)**
- **Target:** `src/backend/GameServer.Domain/Match3/BattleEventBuilder.cs:114-136`
- **Issue:** In `Build()`, the loop emitting `pass.MatchedCellGemMatched` is nested inside `foreach (var match in pass.Matches)`. If a pass contains $M \ge 2$ matches, all matched cells for the entire pass are emitted $M$ times, violating `GAME_EVENTS.md` §1.3.
- **Record:** See [TASK-279](TASK-279-investigate-multi-match-gemmatched-duplication.md).

### 4.2 TASK-280: State-Push-Before-Projection Batch-Loss Path in `BattleHub`
- **Status:** BACKLOG
- **Label:** **UNVERIFIED (Architectural Inspection)**
- **Target:** `src/backend/GameServer.Api/Hubs/BattleHub.cs:1143-1156`, `1295-1308`, `1341-1350`
- **Issue:** `BattleStateUpdated` is broadcast before `ReceiveEventsPayload` projection occurs. If `BattleEventWireProjection.Project()` throws, `BattleStateUpdated` has already advanced the client's sequence, preventing sequence gap detection from catching the lost event batch.
- **Record:** See [TASK-280](TASK-280-investigate-battlehub-state-push-projection-batch-loss.md).

---

## 5. Revised Implementation Dependency Graph

```text
                                [TASK-254] (Done)
                                     │
                 ┌───────────────────┴───────────────────┐
                 │                                       │
           [TASK-272] (In Review)                  [TASK-275] (In Review)
                 │                                       │
           [TASK-273] / [TASK-274] (Done)                │
                 │                                       │
                 └───────────────────┬───────────────────┘
                                     │
                                [TASK-276] (Planning - Incomplete)
                                     │
                                [TASK-277] (Compatibility Gate)
                                     │
                                     ▼
                      ┌─────────────────────────────┐
                      │          TASK-278           │
                      │  PROTOCOL AUTHORIZATION GATE│
                      └──────────────┬──────────────┘
                                     │
                 ┌───────────────────┴───────────────────┐
                 │ (A-1, A-4 Decisions Pending)          │ (Defect Investigations)
                 ▼                                       ▼
       [AUTHORIZATION GATE]                     [INDEPENDENT TRACK]
                 │                                       │
       ┌─────────┴─────────┐                     ┌───────┴───────┐
       │                   │                     │               │
  Option A-1(i)       Option A-1(ii)          TASK-279        TASK-280
 (Authorize)          (Settle-Only)         (Multi-Match    (State-Push
       │                   │                 GemMatched)     Batch-Loss)
       │                   ▼                     │               │
       │           [CLOSE DR-1 AS                └───────┬───────┘
       │          ACCEPTED LIMITATION]                   ▼
       │                   │                     [CORRECTIVE FIXES]
       ▼                   ▼                     (Independent of A-1)
  [PHASE 1: DOCS]    [MAINTAIN CURRENT
   GAME_RULES §16     SETTLE-ONLY PIPELINE]
   GAME_EVENTS §1-3
   SIGNALR_PROTOCOL §3, §8
   GAME_STATE §3, §5
   ADR-027 / ADR-026 Amend
       │
       ▼
  [PHASE 2: BACKEND]
   BT-1 GravityAndSpawn
   BT-2 BoardResolver
   BT-3 BattleEventBuilder
   BT-4 BattleEventType
   BT-5 WireProjection
   ApiIntegrationTests
       │
       ▼
  [PHASE 3: FRONTEND]
   FT-1 Presenter
   FT-2 Presentation Step
   FT-3 BattleScene Phases
   FT-4 Transition Anim
   FT-5 Invariant Guards
   FT-6 Lock Safety
```

---

## 6. Implementation Readiness & Blocked Task Status

| Task / Work Item | Status | Blocker |
| :--- | :--- | :--- |
| **A-1 Authorization Decision** | **BLOCKED** | Pending Operator / Product-Owner ruling |
| **A-4 Prohibitions Reconciliation** | **BLOCKED** | Pending Operator / Product-Owner ruling |
| **Documentation Updates (Phase 1)** | **BLOCKED** | Blocked by A-1, A-4 approval artifact |
| **Backend Implementation (Phase 2)** | **BLOCKED** | Blocked by Phase 1 documentation updates |
| **Frontend Per-Pass Animation (Phase 3)** | **BLOCKED** | Blocked by Phase 2 backend delivery |
| **TASK-279 (GemMatched Duplication)** | **READY** | Independent; unblocked by protocol decision |
| **TASK-280 (BattleHub Batch Loss)** | **READY** | Independent; unblocked by protocol decision |
| **TASK-275 Baseline Settle-Only** | **OPERATIONAL** | Green under CONDITIONAL PASS; no blockers |

---

## 7. Verification & Limitations

### 7.1 Verification Executed
- `git status --short`: Verified untracked files and working tree status.
- `git diff --check`: Verified zero whitespace defects, tab indentations, or line ending inconsistencies across modified/created files.
- Preserved existing untracked files (`TASK-276`, `TASK-277`) without modification.
- Zero production code, test, or documentation contracts modified.
- No commit or push commands executed.

### 7.2 Limitations
1. This task does not authorize or implement protocol changes; all decisions remain pending operator review.
2. Defect findings in TASK-279 and TASK-280 are based on lexical and structural analysis and remain **unverified at runtime** until dedicated test fixtures are constructed in their respective tasks.

### 7.3 Verification of the §3.6-authorized implementation

Recorded so this gate can be reviewed against evidence rather than against claims.
Every command below was run in this working tree; no commit, push, or staging was
performed.

```text
Domain     dotnet test tests/backend/GameServer.Domain.Tests   1629 passed, 0 failed
Api        dotnet test tests/backend/GameServer.Api.Tests       377 passed, 0 failed
Application  dotnet test ... --filter "FullyQualifiedName!~Balance"
                                                               684 passed, 0 failed
Frontend   npx vitest run                                      1223 passed, 0 failed (24 files)
Frontend   npx tsc --noEmit                                    exit 0
Frontend   npm run build                                       exit 0 (tsc + vite)
Docs       git diff --check                                    exit 0
```

Pre-existing failures, kept separate from this change (both sets reproduce
identically on the pristine baseline `e33254e`, verified by extracting that commit
with `git archive` into a temporary directory and running the same filters there):

```text
Application.Tests.Balance.*                10 failed / 62 in the Balance namespace; the recorded
                                           TASK-194/TASK-195 expectations are stale against the
                                           current ADR-026 balance rules (e.g. a recorded
                                           `skillBaseDamage: 100` against the retired value)
Infrastructure.Tests.Boss*Persistence/
  Definition*/Postgres*                     4 failed / 96 in those classes; Postgres fixture
                                           availability, not this change
```

Running the Balance suites rewrites the tracked artifact
`tasks/artifacts/TASK-197-controlled-balance-experiments.json` as a side effect; it
was restored to its committed content with `git checkout --` and is not part of this
work. No commit, push, or staging was performed.

The six operator-mandated checks, and where each is asserted:

```text
ordering & pass identification     BattleEventEmissionTests
                                   .CascadeStepResolved_ShouldSitLastInItsOwnPass_OncePerPass
                                   (last item of its own pass, once per recorded pass,
                                    ascending 1-based depth equal to MatchCreated.cascadeDepth)
compatibility with existing        ApiIntegrationTests'
  ReceiveEvents consumers          Swap_ReceiveEvents_ShouldPreserveTheExecutorEventOrderExactly,
                                   ...ShouldSerializeAcrossTheSignalRJsonBoundary and
                                   ...ShouldCarryNoMemberOutsideTheDocumentedSchema (member tables
                                   extended, never loosened), plus the frontend closed-set test
duplicate delivery / retry /       SceneLifecycle test 26 (a repeated batch presents no second Fall
  replay                           or Refill and moves no view twice); acceptedResolutionSequence
                                   and admitsBatch are unchanged
validation of coordinates, gem     BattleEventPresentation "ADR-027 — the delivered per-pass
  types and pass ordering          Gravity + Spawn report" (14 malformed payloads each refused
                                   whole); the Domain reconstruction test
coordinates in range, one source   CascadeStepResolved_ShouldReportEachPassesOwnMovesAndSpawns
  and destination each
client recovery on a missing or    SceneLifecycle test 27 (malformed item presents nothing and the
  invalid pass event               settle transition converges the delivered board) and test 28
                                   (recovery between Fall and Refill converges, leaves no empty
                                   cell, and releases the pool)
boss cannot act on an              SceneLifecycle test 25 (the boss banner and the boss's own
  incompletely resolved board      exchange both follow the player's Refill; player damage follows
                                   the player's pass report)
```

Constraint checks:

```text
1 no per-pass board         the item carries no `board` member, and no board field exists on the
                            wire DTO for it (ApiIntegrationTests member table)
2 no client RNG simulation  the client reads `gemType` as delivered and never touches rngSeed/rngState
3 Redis CAS unchanged       no file under Infrastructure/Application/Redis was modified
4 sequence semantics        the envelope stays 3 members, one batch per round, `serverSequence`
  unchanged                 untouched (BattleHubTests + the Api envelope test)
5 turn banners preserved    the Player-turn banner suite and the TASK-275 banner tests pass unchanged
6 no second board authority the board still travels only in `BattleStateUpdated` / `GetBattleState`;
                            the client's `settle` transition remains the convergence
7 per-pass animation        SceneLifecycle tests 23/24 (Fall then Refill, per pass, in order)
  requirements preserved
```

### 7.4 Focused review addendum — closure-readiness review (one corrected defect)

Recorded by the review session that inspected this implementation against the §3.6
approval artifact. It **adds no approval and changes no status**: this record stays
`BACKLOG` / `OPEN` until the Operator reviews it.

Method: the approved contract was re-read (`ADR-027`; `GAME_EVENTS.md` §1.1, §2;
`SIGNALR_PROTOCOL.md` §3.2.28; `GAME_STATE.md` §3; `MATCH3_RULES.md` §4.4 item 7), the
whole working-tree diff was inspected file by file, and the §7.3 checks were re-executed
in this tree. The envelope, the hub, Redis, persistence and sequencing were confirmed
untouched (no file under `src/backend/GameServer.Infrastructure/`,
`src/backend/GameServer.Application/` or `Api/Hubs/BattleHub.cs` is modified).

**One confirmed in-scope defect was found and corrected** (`AGENTS.md` §16/§17, not a
style change):

```text
Defect    BattleScene.playRefillSpawn allocated a NEW tile+label pair for a spawned
          cell whenever its Fall had pooled no view for it. The pass's own clear had
          already hidden a view on exactly those cells when nothing could fall into
          them — a cleared cell at the TOP of its column, which any Match touching
          row 0 produces. The hidden view was then abandoned in the board layer.
Evidence  A delivered fixture pass that clears cells 0–2 and reports `spawnedGems`
          for them with no `dropMoves` left harness.boardCells at 134 instead of 128
          (three abandoned tile+label pairs) and replaced the views at cells 0–2 —
          a regression against TASK-245's persistent-view invariant, which tests
          23/28 assert only for passes whose clear a Fall fills.
Fix       the Refill step now re-uses, in order: the view still on the spawned cell,
          then the view the Fall pooled, then — only when the scene has neither — a
          newly created one. No contract, wire, gameplay or ordering behaviour changes.
Test      SceneLifecycle test 29 ("re-places the very view a top-row clear emptied
          when the pass's Refill names it"), which fails on the pre-fix code (134
          objects) and passes after it.
```

Re-executed evidence, in this working tree, after that correction:

```text
Domain       dotnet test tests/backend/GameServer.Domain.Tests        1629 passed, 0 failed
Api          dotnet test tests/backend/GameServer.Api.Tests            377 passed, 0 failed
Application  dotnet test ... --filter "FullyQualifiedName!~Balance"    684 passed, 0 failed
Frontend     npx vitest run                                  1224 passed, 0 failed (24 files)
Frontend     npx tsc --noEmit                                          exit 0
Frontend     npm run build                                             exit 0 (tsc + vite)
Docs         git diff --check                                          exit 0
```

The frontend total is §7.3's 1223 plus the added regression test. The Balance and Postgres
suites were not re-run; they are the pre-existing baseline failures §7.3 records, and
nothing in this change touches them. No artifact under `tasks/artifacts/` was modified, no
file was staged, and no commit or push was performed.

Two review findings that are **not** corrected here, for the Operator and the owning
implementation workflow:

```text
1 Metadata `Declared Files` lists only this record and TASK-279/TASK-280, while the
  authorized implementation changed production, test and documentation files. The
  Phase A exact-set staging verification (§6 of tasks/TASK_LIFECYCLE.md, P-5) compares
  the staged set against that field, so it cannot pass for this change set until the
  record's declared file set is updated to the files actually changed.
2 TASK-280 (separate, CLOSED under TASK-282) leaves the client unable to observe a batch
  lost behind its own delivered state push. The per-pass items ride in that same batch,
  so they are lost with it; that is the documented delivery limitation and is not a
  TASK-278 defect. The recommended client recovery-trigger task remains unopened.
```

### 7.5 Operator Decision — manifest amendment and lifecycle route (D-278-1 = A, D-278-2 = Route 1)

This subsection is the **repository evidence** for the metadata and lifecycle decision
the closure-readiness review requested. It is **not** a protocol approval: §3.6 stands
unchanged, is not re-opened, and is neither extended nor weakened by it.

```text
Decision:          D-278-1 = OPTION A — the retrospective amendment of this record's
                   `Declared Files` field to the 24 authorized implementation files,
                   together with the matching `## Declared File Set (P-2 / P-5)` section,
                   is AUTHORIZED and is recorded as applied under this decision.
                   Authority: the requester owns the declared file set (TASK-254:351,
                   "the declared set must be amended by the requester"), and the
                   repository's precedent for a Product-Owner-ordered post-hoc amendment
                   is TASK-224 D-2 ("extend TASK-217A's declared file set… Applied —
                   TASK-217A's record §7.3 was amended under this decision"). It is
                   therefore an authorized scope correction, not the agent
                   self-legalization TASK_LIFECYCLE.md §6.1 step 2 / P-5 prohibits.

                   D-278-2 = ROUTE 1 — the forward lifecycle route is AUTHORIZED: the
                   Orchestrator promotes BACKLOG → READY, moves this record to
                   tasks/active/ with Status: IN PROGRESS (READY → IN PROGRESS, which is
                   what TASK_LIFECYCLE.md P-3 requires before Phase A), then IN REVIEW,
                   then Phase A stages exactly the 24 declared paths (this record
                   excluded) and Phase B files the completion record with the Phase A SHA.

Decided by:        Requester / Product Owner (Operator), 2026-10-15.
Scope decided:     this record's manifest metadata and its lifecycle route — nothing else.
Applied here:      — the 24-path `Declared Files` amendment and the `## Declared File
                     Set (P-2 / P-5)` section (D-278-1);
                   — C-1: the `Verification:` field no longer cites a non-existent §8;
                   — C-3: the template's Primary Agent / Supporting Agents / Workflow /
                     Skills keys are present and marked NOT RECORDED rather than invented;
                   — C-4: the Status field leads with the one canonical lifecycle state,
                     `BACKLOG`, which is unchanged.
Expected Phase A:  24 entries — 23 `M` + 1 `A` (the new ADR-027), this record excluded,
                   zero extras (`tasks/TASK_LIFECYCLE.md` §6.1 step 2 / P-5).
Not applied here:  the BACKLOG → READY promotion and the move to tasks/active/ (the
                   Orchestrator's transitions, D-278-2 Route 1); any staging, commit,
                   push, or `Status: DONE`. The agent holds no authorization for those.
Not decided here:  the §3.6 protocol approval (unchanged); TASK-280, TASK-283 and
                   TASK-284A (untouched, and no recovery task is created here).
Disclosure:        the authorized implementation was executed while this record was
                   `Status: BACKLOG` and was verified only after that fact (§7.3, §7.4).
                   This decision authorizes the manifest and the lifecycle route; it
                   certifies neither the implementation nor its test results, and it is
                   not an operator approval of completion (TASK-273:8–9, 50's shape).
```

**Disposition of §7.4's two findings.**

1. *Metadata `Declared Files` stale against the authorized change set* — **RESOLVED** by
   this decision: the field now declares the 24 implementation files, the matching
   `## Declared File Set (P-2 / P-5)` section exists, and the Phase A exact-set check can
   be satisfied once the Orchestrator completes D-278-2 Route 1.
2. *TASK-280's batch-loss limitation* — **unchanged and still separate**: it remains
   `CLOSED` under TASK-282, it is not a TASK-278 defect, and the recovery work recorded
   as TASK-283 / TASK-284A is neither touched nor pre-empted here.

---

## Completion Evidence

### Commit
- Implementation slice (Phase A): `86fcdd04cc50019b0aa47d43c66f0eaa717640b8`
- Subject: `feat: implement authoritative cascade pass transition reporting and presentation`
- Owns: `TASK-278`
- Files: `docs/01-game-design/GAME_RULES.md, docs/01-game-design/MATCH3_RULES.md, docs/02-technical/GAME_EVENTS.md, docs/02-technical/GAME_STATE.md, docs/02-technical/SIGNALR_PROTOCOL.md, docs/03-decisions/README.md, docs/03-decisions/ADR/ADR-027-authoritative-cascade-pass-transition-reporting.md, src/backend/GameServer.Domain/Match3/BattleEvent.cs, src/backend/GameServer.Domain/Match3/BattleEventBuilder.cs, src/backend/GameServer.Domain/Match3/BoardResolver.cs, src/backend/GameServer.Domain/Match3/GravityAndSpawn.cs, src/backend/GameServer.Api/Hubs/BattleEventWireProjection.cs, src/frontend/client/src/game/scenes/BattleEventPresenter.ts, src/frontend/client/src/game/scenes/BattleScene.ts, src/frontend/client/tests/BattleEventPresentation.test.ts, src/frontend/client/tests/SceneLifecycle.test.ts, tests/backend/GameServer.Api.Tests/ApiIntegrationTests.cs, tests/backend/GameServer.Application.Tests/BattleStateServiceDeadlockRecoveryTests.cs, tests/backend/GameServer.Application.Tests/BattleStateServiceTests.cs, tests/backend/GameServer.Application.Tests/BossResponseTests.cs, tests/backend/GameServer.Application.Tests/RoundTestSupport.cs, tests/backend/GameServer.Domain.Tests/BattleEventEmissionTests.cs, tests/backend/GameServer.Domain.Tests/PlayerEffectHealingTests.cs, tests/backend/GameServer.Domain.Tests/ResourceGenerationTests.cs`
- Shared with: `none`
- Unowned / pre-existing: `none`

### Changed Files
- `docs/01-game-design/GAME_RULES.md` — Added CascadeStepResolved to canonical authoritative event list (§16).
- `docs/01-game-design/MATCH3_RULES.md` — Reconciled §4.4 item 7 clarifying gravity pass transition reporting via CascadeStepResolved.
- `docs/02-technical/GAME_EVENTS.md` — Defined CascadeStepResolved semantics, trigger, payload, and cycle placement (§1.1, §2, §3).
- `docs/02-technical/GAME_STATE.md` — Clarified transient nature of intermediate cascade pass states (§3, §5.1, §5.3).
- `docs/02-technical/SIGNALR_PROTOCOL.md` — Reconciled prohibitions, added 19th discriminator, defined wire schema (§3.1, §3.2.2, §3.2.28, §8).
- `docs/03-decisions/README.md` — Indexed ADR-027.
- `docs/03-decisions/ADR/ADR-027-authoritative-cascade-pass-transition-reporting.md` — Architectural decision record for pass transition reporting.
- `src/backend/GameServer.Domain/Match3/BattleEvent.cs` — Added CascadeStepResolved BattleEvent record definition.
- `src/backend/GameServer.Domain/Match3/BattleEventBuilder.cs` — Implemented CascadeStepResolved recording and emission.
- `src/backend/GameServer.Domain/Match3/BoardResolver.cs` — Integrated pass transition reporting into resolution loop.
- `src/backend/GameServer.Domain/Match3/GravityAndSpawn.cs` — Tracked drop movements and refill gem spawns per cascade step.
- `src/backend/GameServer.Api/Hubs/BattleEventWireProjection.cs` — Added wire projection mapping for CascadeStepResolved.
- `src/frontend/client/src/game/scenes/BattleEventPresenter.ts` — Added CascadeStepResolved event handling and step orchestration.
- `src/frontend/client/src/game/scenes/BattleScene.ts` — Implemented playRefillSpawn view reuse, per-pass drop/refill presentation.
- `src/frontend/client/tests/BattleEventPresentation.test.ts` — Added unit tests for CascadeStepResolved presentation and payload validation.
- `src/frontend/client/tests/SceneLifecycle.test.ts` — Added lifecycle, recovery, order, and view persistent invariant tests (tests 23-29).
- `tests/backend/GameServer.Api.Tests/ApiIntegrationTests.cs` — Updated wire contract assertions and serialization tests.
- `tests/backend/GameServer.Application.Tests/BattleStateServiceDeadlockRecoveryTests.cs` — Updated event counts and assertions for new event.
- `tests/backend/GameServer.Application.Tests/BattleStateServiceTests.cs` — Updated round support and event verification.
- `tests/backend/GameServer.Application.Tests/BossResponseTests.cs` — Verified boss turn timing after pass resolution.
- `tests/backend/GameServer.Application.Tests/RoundTestSupport.cs` — Updated test round builder support for CascadeStepResolved.
- `tests/backend/GameServer.Domain.Tests/BattleEventEmissionTests.cs` — Added domain emission, ordering, and structure tests.
- `tests/backend/GameServer.Domain.Tests/PlayerEffectHealingTests.cs` — Updated healing resolution tests.
- `tests/backend/GameServer.Domain.Tests/ResourceGenerationTests.cs` — Updated resource generation event counts.

### Validation Results
- `dotnet test tests/backend/GameServer.Domain.Tests` — PASS (1629 passed, 0 failed)
- `dotnet test tests/backend/GameServer.Api.Tests` — PASS (377 passed, 0 failed)
- `dotnet test tests/backend/GameServer.Application.Tests --filter "FullyQualifiedName!~Balance"` — PASS (684 passed, 0 failed)
- `npx vitest run` (client) — PASS (1224 passed in 24 files, 0 failed)
- `npx tsc --noEmit` (client) — PASS (exit 0)
- `npm run build` (client) — PASS (exit 0)
- `git diff --check` — PASS (exit 0)

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic (RNG server-seeded, intermediate states non-authoritative)
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
- [x] Confirmed P-1…P-6 commit policy compliance
