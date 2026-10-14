# TASK-278 — Cascade Protocol Authorization Gate

---

## Metadata

```text
Task ID:           TASK-278
Type:              ARCHITECTURE / GOVERNANCE & DECISION PREPARATION
Status:            BACKLOG — PENDING OPERATOR / PRODUCT-OWNER DECISION
Risk:              MEDIUM
Priority:          HIGH
Mode:              Controlled Repository Workflow — decision preparation and documentation only
Production code:   NONE CHANGED (no source, test, doc, or ADR edited; backlog task records only)
Dependencies:      TASK-254 (DONE — DR-1 option B), TASK-272 (IN REVIEW), TASK-273 (DONE),
                   TASK-274 (DONE), TASK-275 (IN REVIEW — CONDITIONAL PASS),
                   TASK-276 (BACKLOG — planning), TASK-277 (BACKLOG — decision gate)
Declared Files:    tasks/backlog/TASK-278-cascade-protocol-authorization-gate.md,
                   tasks/backlog/TASK-279-investigate-multi-match-gemmatched-duplication.md,
                   tasks/backlog/TASK-280-investigate-battlehub-state-push-projection-batch-loss.md
Verification:      git diff --check (see §8)
```

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
STATUS: PENDING — OPERATOR / PRODUCT-OWNER DECISION REQUIRED
```

- **Option (i) — Authorize Protocol Change:**
  Authorize adding one additive wire event (`CascadeStepResolved`) to the atomic round batch, amending the owning documents per §2.3 to permit client-side per-pass drop and spawn animations.
- **Option (ii) — Retain Settle-Only Presentation (TASK-254 DR-1 Option A):**
  Decline the protocol change. Retain the current production architecture where the round's single committed board transitions in bulk during the `settle` phase. Close TASK-254 DR-1 as an accepted permanent limitation.

### 3.2 Question A-4: Reconcile Prohibiting Rules & Dependent Contracts

```text
STATUS: PENDING — OPERATOR / PRODUCT-OWNER DECISION REQUIRED
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
