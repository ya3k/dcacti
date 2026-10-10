# TASK-247 — Align Web Smoke E2E Harness with Sequential Battle Presentation

<!--
  GEN-TASK EXECUTION MANIFEST TEMPLATE
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
-->

---

## Metadata

```text
Task ID:           TASK-247
Type:              BUG
Status:            DONE
Risk:              LOW
Priority:          MEDIUM
Primary Agent:     testing
Supporting Agents: client, review
Workflow:          development/bug-fix.md
Skills:            client/phaser-battle-presentation, client/client-event-projection, testing/test-scenario-generation, quality/implementation-review
Dependencies:      TASK-246, TASK-245
Declared Files:    src/frontend/client/scripts/standalone-web-smoke.mjs, scripts/standalone-web-smoke.mjs
```

---

## Objective

Align the standalone web browser smoke test harness (`scripts/standalone-web-smoke.mjs` / `src/frontend/client/scripts/standalone-web-smoke.mjs`) with TASK-245's sequential presentation timeline and TASK-246's pacing model. Fix harness-level probe collisions, timing race conditions, and outdated snapshot assertions so that the documented E2E browser smoke suite reliably passes against a live backend, Redis, and CDP browser stack.

---

## Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1 — End-to-end browser smoke verification in MVP scope
- `docs/01-game-design/GAME_RULES.md` §17 — Fixed resolution step order
- `docs/02-technical/GAME_EVENTS.md` §1, §1.1 — Event delivery ordering
- `docs/02-technical/SIGNALR_PROTOCOL.md` §3.1, §3.2 — Batch atomicity and event presentation
- `tasks/completed/TASK-245-phaser-cascade-animation-and-combat-presentation-sequencing.md` — Sequential cascade animation and timeline supersession
- `tasks/completed/TASK-246-battle-ux-phase-legibility-selection-feedback-and-pacing.md` — Phase pacing, selection feedback, and interaction indicator
- `.ai/workflow/quality/local-e2e-verification.md` — Documented local E2E verification procedure

---

## Scope

### In Scope
- Strictly increasing `serverSequence` values for injected probe batches to prevent supersession discards under TASK-245's sequence ordering.
- Evaluating sequential floater presentation over a temporal resolution window rather than asserting instantaneous coexistence of multiple sequential floaters.
- Checking floater anchoring per individual damage/power event instance against the respective target/source panels.
- Waiting for `isInputLocked() === false` before dispatching the phase-6 cast click, or retrying the cast click safely across the input-lock window.
- Updating the stale `presentationLocked` probe property to reflect the current presentation-timeline state (`isInputLocked()` or timeline activity).
- Adding the TASK-246 `phaseText` interaction indicator to the HUD bounds verification probe.
- Re-running the documented smoke suite (`npm run verify:e2e:smoke`) against a running backend, Redis, and CDP browser stack.

### Out of Scope
- Modifying production client runtime, BattleScene, or presenter logic (scoped strictly to test harness).
- Altering wire contracts, server event shapes, or backend battle logic.
- Fixing unrelated backend test failures or protocol issues.

---

## Current State

`scripts/standalone-web-smoke.mjs` (delegating to `src/frontend/client/scripts/standalone-web-smoke.mjs`) was authored before TASK-245 and TASK-246 introduced sequential presentation phases and pacing holds. The harness's phase-5c and phase-6 checks fail because probes use descending serverSequence numbers, expect simultaneous floaters from different phases, fail to wait for input-lock release before clicking cast controls, read removed presentationLocked flags, and omit phaseText from HUD bounds.

---

## Declared File Set (P-2 / P-5)

Exact declared implementation files for TASK-247:
```text
src/frontend/client/scripts/standalone-web-smoke.mjs
scripts/standalone-web-smoke.mjs
```

---

## Acceptance Criteria

- [x] Injected probe batches in `standalone-web-smoke.mjs` use strictly increasing `serverSequence` values that are not discarded by timeline supersession.
- [x] Floater assertions evaluate sequential presentation across the phase resolution window rather than requiring simultaneous coexistence in a single snapshot.
- [x] Floater anchoring is verified per individual damage/power event over its respective entity panel.
- [x] Phase-6 cast click waits for `isInputLocked() === false` or safely retries to ensure reliable dispatch without timing out against the input guard.
- [x] Probe diagnostics reflect active presentation timeline state rather than the removed `presentationLocked` field.
- [x] The `phaseText` HUD element is included and validated in the `hudBounds` probe.
- [x] The full browser smoke suite passes cleanly when executed against a live local stack per `.ai/workflow/quality/local-e2e-verification.md`.
- [x] No production game runtime or server files are modified.
- [x] All relevant tests pass at the required validation depth (`core/validation.md` §2).
- [x] Quality review checklist passes (`quality/review.md` §1).
- [x] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001).

---

## Affected Files & Areas

```text
[ ] src/backend/ (Domain / Application / Infrastructure / Api)
[x] src/frontend/client/ (scenes / runtime / services / state / ui) [scripts/ only]
[ ] tests/ (unit / integration / gameplay scenarios)
[ ] docs/ (documentation updates if applicable)
```

---

## Implementation Notes

- `src/frontend/client/scripts/standalone-web-smoke.mjs` is the authoritative implementation file executed by `npm run verify:e2e:smoke` from `src/frontend/client/`.
- `scripts/standalone-web-smoke.mjs` is the root convenience wrapper delegating via import.
- Root cause investigation and diagnostic findings are documented in TASK-246 §Remaining Issues (I-5, I-6, I-7).

---

## Testing Requirements

### Required Verification
```text
[x] Browser E2E smoke  — npm run verify:e2e:smoke (executed from src/frontend/client)
```

### Environment Prerequisites (.ai/workflow/quality/local-e2e-verification.md)
- Docker services running: PostgreSQL (port 5433) and Redis (port 6379) via `docker compose up -d`
- Database schema applied: `dotnet ef database update --project src/backend/GameServer.Infrastructure --startup-project src/backend/GameServer.Api`
- Backend running on `http://localhost:5000` (`dotnet run --project src/backend/GameServer.Api/GameServer.Api.csproj`) with `ApplicationSession:CurrentKey:Secret` configured via `dotnet user-secrets`
- Frontend dev server running on `http://localhost:5173` (`npm run dev` from `src/frontend/client`)
- Chromium-based browser (Chrome or Edge) available for CDP automation

### Key Edge Cases
- Probe batches injected mid-timeline must not clobber preceding timelines without valid sequence advancement.
- Cast clicks landing while a timeline is playing must handle input guard gracefully.

---

## Stop Conditions

- If harness fixes require altering production game logic or server contracts: STOP per `AGENTS.md` §10.
- If scope expands beyond test script repairs: STOP per `AGENTS.md` §16.

---

## Completion Evidence

### Commit
- Implementation slice (Phase A): `1617493989c0f5e2406bc781e17172cb790de5c9`
- Subject: `fix(test): align web smoke e2e harness with sequential battle presentation`
- Owns: `TASK-247`
- Files: `src/frontend/client/scripts/standalone-web-smoke.mjs, scripts/standalone-web-smoke.mjs`
- Shared with: `none`
- Unowned / pre-existing: `none`

### Changed Files
- `src/frontend/client/scripts/standalone-web-smoke.mjs` — Implemented probe sequence allocator, temporal floater resolution assertions with distinct instance tracking, floater anchoring checks against HP/power gauges, Phase 6 cast click input-unlock gate, BATTLE_SNAPSHOT presentationTimeline and phaseText diagnostics, and foreground visibility restoration after CDP screenshot operations.
- `scripts/standalone-web-smoke.mjs` — Updated header docstring to reflect TASK-247 alignment.

### Validation Results
- `npm run verify:e2e:smoke` (from `src/frontend/client`) — PASS (Run 1: 160/160 PASS; Run 2: 160/160 PASS; 0 failures)
- `npm test -- --run` (from `src/frontend/client`) — PASS (23 test files, 991/991 tests passing)

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
- [x] Confirmed P-1…P-6 commit policy compliance
