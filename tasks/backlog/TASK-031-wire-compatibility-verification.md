# TASK-031 — Wire Compatibility Verification

---

## Metadata

```text
Task ID:           TASK-031
Type:              REFACTOR
Status:            BACKLOG
Risk:              MEDIUM
Priority:          HIGH
Primary Agent:     realtime
Supporting Agents: client, testing, review
Workflow:          development/refactor.md
Skills:            realtime/realtime-protocol-validation, backend/api-contract-validation, testing/test-scenario-generation, client/client-event-projection, quality/implementation-review
Dependencies:      TASK-025, TASK-026
```

---

## Objective

Verify and lock SignalR wire compatibility after the PetState-authority refactor: `playerState` label continues to carry exactly `{combo, matchCount}` sourced from BattleState root, `petState` carries exactly the Passive trio, `finalPlayerHp` equals active-Pet HP, `target="player"` is retained as a fixed label — with wire member-set tests proving no renames and no payload widening.

---

## Authoritative References

- `docs/02-technical/SIGNALR_PROTOCOL.md` §3.2 — events wire schema; §4.2 — playerState delivery; §4.3 — petState delivery; §3.2.19 — BattleWon/BattleLost finalPlayerHp
- `docs/03-decisions/ADR/ADR-011-player-owner-pet-combat.md` item 6 — wire names not renamed
- `docs/03-decisions/ADR/ADR-012-player-level-pet-level-ownership.md` item 11 — labels remain fixed protocol labels
- `docs/02-technical/GAME_STATE.md` §2.2 — root accounting delivered under playerState label; §2.3 PetState
- `docs/00-overview/MVP_SCOPE.md` §1 — server-authoritative battle resolution IN

---

## Scope

### In Scope
- Wire member-set tests for `BattleStateUpdated` payload stages (playerState, petState) asserting exact member names/sets
- Event payload tests for `BattleWon`/`BattleLost` (`finalPlayerHp` = PetState.HP) and damage events (`target="player"` retained)
- Projection source verification: `BattleHub` maps root Combo/MatchCount → `PlayerStatePayload`; PetState Passive trio → `PetStatePayload`; no extra members
- Fix any accidental rename/widening introduced by TASK-025/026 (projection-only corrections)

### Out of Scope
- Deliberate protocol version bump or label renames (`playerState`→something clearer, `finalPlayerHp`, `target`) — ADR-011 item 6 defers these
- Adding new wire members (equipped loadout arrays are not delivered on current stages — SIGNALR_PROTOCOL §4.2/§4.3 own what is sent)
- Client rendering changes beyond what projection tests require (client only reads existing labels)
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2

---

## Current State

`BattleHub.cs` projects `state.PlayerState.Combo/MatchCount` into `PlayerStatePayload` and Passive trio into `PetStatePayload` (pre-TASK-025 source paths). `BattleEventWireProjection.cs` maps `finalPlayerHp` from event payloads. Frontend `SignalRService.ts` declares matching `playerState` interface. No dedicated wire member-set regression tests exist for the post-refactor shape.

---

## Acceptance Criteria

- [ ] Test asserts `BattleStateUpdated.playerState` serializes with exactly members `combo` and `matchCount` (SIGNALR_PROTOCOL §4.2)
- [ ] Test asserts `petState` serializes with exactly the Passive trio members (SIGNALR_PROTOCOL §4.3)
- [ ] Test asserts `BattleWon`/`BattleLost.finalPlayerHp` equals active Pet HP sourced from PetState (SIGNALR_PROTOCOL §3.2.19)
- [ ] Test asserts damage-event `target` retains the fixed `"player"` label (SIGNALR_PROTOCOL §3.2.14–15)
- [ ] No wire member is renamed or added relative to the SIGNALR_PROTOCOL contract; client `SignalRService.ts` interfaces unchanged
- [ ] All relevant tests pass at the required validation depth (`core/validation.md` §2)
- [ ] Quality review checklist passes (`quality/review.md` §1)
- [ ] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001)

---

## Affected Files & Areas

```text
[x] src/backend/ (Domain / Application / Infrastructure / Api — projection fixes only if tests fail)
[x] src/frontend/client/ (services — only if a type drifted; expected unchanged)
[x] tests/ (unit / integration / gameplay scenarios — wire member-set tests)
[ ] docs/ (SIGNALR_PROTOCOL already correct — do not edit)
```

---

## Implementation Notes

- Projection call sites after TASK-025: `BattleHub.cs` ~L630–656 (Combo/MatchCount now from BattleState root, not `state.PlayerState`); `BattleEventWireProjection.cs` BattleWon/BattleLost factories L414/L434.
- Prefer asserting on serialized JSON member names (not only C# record shape) to catch casing/schema drift per SIGNALR_PROTOCOL §3.2.3.
- If a test reveals a semantic mismatch that requires a protocol change: STOP — that is a protocol-breaking task per ADR-011 item 6, not this task.
- Do not edit `tasks/completed/` (wire contracts landed in TASK-007A/007A3, TASK-021A).

---

## Testing Requirements

### Required Verification

```text
[x] Unit tests         — JSON member-set assertions for playerState/petState/finalPlayerHp/target labels
[x] Integration tests  — BattleHub projection end-to-end from a refactored BattleState
[x] Gameplay scenarios — Given a won battle, When events serialize, Then finalPlayerHp equals PetState.HP and playerState contains only combo/matchCount (SIGNALR_PROTOCOL §4.2, §3.2.19)
```

### Key Edge Cases
- See `docs/02-technical/SIGNALR_PROTOCOL.md` §3.2.5 — omitted, never explicit null (member-set tests must not expect nulls)
- See `docs/02-technical/SIGNALR_PROTOCOL.md` §3.2.12 — no member outside the schema

---

## Stop Conditions

- If required behavior cannot be fully derived from Authoritative References: STOP per `AGENTS.md` §7
- If a rename appears required for correctness: STOP — conflicts with ADR-011 item 6 / ADR-012 item 11; report per `AGENTS.md` §4
- If task requires client-authoritative game logic calculation: STOP per `AGENTS.md` §10
- If task exceeds 7 skills or crosses multiple uncoupled architectural boundaries: STOP & decompose

---

## Completion Evidence

### Changed Files
- `<file path>` — <summary of change>

### Validation Results
- `<test command or suite>` — PASS (<N> tests)

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
