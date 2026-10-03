# TASK-147 — Resolve BattleHub.Ping Contract Ownership and Classification

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ and task files by path and section.

  PROVENANCE: Identified during TASK-146 verification.
  BattleHub.Ping(string? clientSequence = null) is an existing, registered,
  client-invokable SignalR Hub method in src/backend/GameServer.Api/Hubs/BattleHub.cs
  and exercised by backend and frontend tests. However, it was not documented anywhere
  in docs/02-technical/SIGNALR_PROTOCOL.md or the broader docs/ tree.

  THIS TASK IS A DOCUMENTATION AND CONTRACT RECONCILIATION TASK ONLY.
  It decides nothing in source code and implements no runtime changes.
  Zero files under src/ or tests/ are to be modified by this task.
-->

---

## Metadata

```text
Task ID:           TASK-147
Type:              DOCUMENTATION
Status:            DONE
Risk:              LOW
Priority:          MEDIUM
Primary Agent:     review
Supporting Agents: realtime, backend
Workflow:          documentation/documentation-change.md
Skills:            discovery/documentation-discovery,
                   realtime/realtime-protocol-validation,
                   quality/documentation-consistency,
                   quality/architecture-conformance
Dependencies:      TASK-146 (DONE)
```

---

## Objective

Establish the authoritative architectural classification and contract ownership of `BattleHub.Ping(string? clientSequence = null)` — determining whether it is:
1. an intended public SignalR protocol method that must be documented in `docs/02-technical/SIGNALR_PROTOCOL.md`;
2. an internal / technical transport health-check probe that belongs outside the gameplay protocol and in operational / architectural documentation;
3. a legacy bootstrap probe that should be formally deprecated and scheduled for removal in a separate implementation task; or
4. an ambiguous architectural decision requiring explicit human / architectural ruling.

Reconcile the authoritative technical documentation accordingly without modifying any source code, tests, or runtime behavior.

---

## Context

During TASK-146 verification, inspection of the codebase confirmed that `BattleHub.Ping` is a real, client-invokable Hub method:
- Implementation exists at `src/backend/GameServer.Api/Hubs/BattleHub.cs:1078`:
  `public Task<PingResponse> Ping(string? clientSequence = null)`
- Return DTO defined at `src/backend/GameServer.Api/Hubs/BattleHub.cs:12`:
  `public record PingResponse(bool Accepted, string? ClientSequence, DateTimeOffset ServerTime);`
- In-source doc comment states:
  *"Bootstrap-era connectivity probe retained from the integration smoke test. It is technical only and carries no gameplay meaning."*
- Tested by integration tests:
  - `tests/backend/GameServer.Api.Tests/ApiIntegrationTests.cs:136` (`BattleHub_Ping_ShouldEchoClientSequenceAndAccepted`)
  - `tests/backend/GameServer.Api.Tests/ApplicationSessionSignalRTests.cs:271`
- Referenced by frontend service and tests:
  - `src/frontend/client/src/services/realtime/SignalRService.ts:554` (`ping(clientSequence)`)
  - `src/frontend/client/tests/SignalRService.test.ts:104`
  - `src/frontend/client/tests/RuntimeBoundaries.test.ts:350`
- However, `docs/02-technical/SIGNALR_PROTOCOL.md` did not document `Ping`. A search across the authoritative `docs/` tree found zero mentions of `Ping` as a contract or Hub method.
- TASK-146 explicitly recorded this contract gap as a premise delta / deviation reported without inline fix (`AGENTS.md` §16).

---

## Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1 — MVP scope verification
- `docs/02-technical/SIGNALR_PROTOCOL.md` §1 — Connection lifecycle and authentication
- `docs/02-technical/SIGNALR_PROTOCOL.md` §2 — Client → Server Hub methods (`Swap`, `CardCast`, `PetSkillCast`, `JoinBattle`, and `Ping`)
- `docs/02-technical/SIGNALR_PROTOCOL.md` §7 — Reconnect & Resync (`GetBattleState`)
- `docs/02-technical/SIGNALR_PROTOCOL.md` §8 — Protocol boundary ("What Is Not Here")
- `docs/02-technical/ARCHITECTURE.md` §1, §4 — SignalR Hub architectural boundary, thin transport role, composition root
- `docs/02-technical/API_CONTRACTS.md` §1, §2 — Authentication boundary and session tokens
- `docs/02-technical/TDD.md` §2.1, §4 — Realtime architecture and transport layer boundaries
- `docs/03-decisions/ADR/ADR-004-realtime-protocol-signalr.md` — SignalR selection rationale
- `docs/03-decisions/ADR/ADR-015-application-session-token-flow.md` — Session authentication boundary

---

## Current Evidence

| Property | Value / Evidence | Authoritative / Source Reference |
|---|---|---|
| Method Name | `Ping` | `src/backend/GameServer.Api/Hubs/BattleHub.cs:1078` |
| Method Signature | `Task<PingResponse> Ping(string? clientSequence = null)` | `src/backend/GameServer.Api/Hubs/BattleHub.cs:1078` |
| Return Type | `PingResponse(bool Accepted, string? ClientSequence, DateTimeOffset ServerTime)` | `src/backend/GameServer.Api/Hubs/BattleHub.cs:12` |
| Authentication | Inherits connection authentication; `/hubs/battle` requires JWT Bearer application session | `src/backend/GameServer.Api/Program.cs:160-169`, `ADR-015` |
| BattleState Access | Zero reads, zero mutations | `BattleHub.cs:1078-1084` |
| Redis Access | Zero reads, zero writes | `BattleHub.cs:1078-1084` |
| Event Broadcast | Emits no Battle Events; does not call `ReceiveEvents`; caller-only return | `BattleHub.cs:1078-1084` |
| Runtime Gameplay Usage | Not called in any battle scene, gameplay flow, or turn resolution (`GameRuntime.ts:491` confirms it is not part of starting or running battles) | `src/frontend/client/src/game/runtime/GameRuntime.ts:491-492` |
| Frontend Client Presence | Exposed on `SignalRService.ping()`, tested in `SignalRService.test.ts`, verified in `RuntimeBoundaries.test.ts` | `SignalRService.ts:554`, `RuntimeBoundaries.test.ts:350` |
| Backend Integration Tests | Asserted in `ApiIntegrationTests.cs:136` and `ApplicationSessionSignalRTests.cs:271` | `ApiIntegrationTests.cs`, `ApplicationSessionSignalRTests.cs` |
| Documentation Presence | Pre-task: 0 occurrences in `docs/`; Post-task: documented in `docs/02-technical/SIGNALR_PROTOCOL.md` §2.2 | Grep across `docs/` |

---

## Decision Question

What is the canonical architectural classification and contract home for `BattleHub.Ping`?

Specifically: Does `Ping` represent an intentional public client-facing transport/utility method of the SignalR protocol, an internal diagnostic/test probe, a deprecated legacy method awaiting removal, or an unresolved architectural question requiring human ruling?

---

## Decision Options

### Option A — Public SignalR Protocol Contract (CHOSEN)
- **Classification:** `Ping` is an intentional public client → server utility / keepalive method.
- **Contract Home:** `docs/02-technical/SIGNALR_PROTOCOL.md` §2.2 (as a non-gameplay transport/utility method alongside `JoinBattle`).
- **Contract Definition Required:** Document `Ping(clientSequence?) -> PingResponse`, parameter semantics (opaque correlation id), response fields (`accepted`, `clientSequence`, `serverTime`), authentication requirement, and caller-only delivery. Clarify that it touches zero battle state and emits zero events.
- **Downstream Impact:** Documentation update only. Code and tests already conform.

### Option B — Non-Gameplay / Internal Transport Diagnostic Method
- **Classification:** `Ping` is intentionally outside the gameplay protocol (`SIGNALR_PROTOCOL.md`), functioning as an infrastructure-level transport connectivity probe.
- **Contract Home:** `docs/02-technical/ARCHITECTURE.md` §4 (Infrastructure / Hub transport boundary).
- **Contract Definition Required:** Document that `BattleHub` exposes an unsequenced transport probe `Ping` for connectivity verification separate from gameplay action streams. Confirm `SIGNALR_PROTOCOL.md` explicitly lists `Ping` under §8 ("What Is Not Here") as a non-gameplay diagnostic probe outside the battle protocol.
- **Downstream Impact:** Documentation update only.

### Option C — Legacy / Unwanted Bootstrap Method
- **Classification:** `Ping` is a legacy bootstrap probe retained from early smoke tests that should not exist on the production `BattleHub` surface.
- **Contract Home:** Record deprecation / retirement decision in the task report / architectural log.
- **Action:** Do NOT delete `Ping` in this task. Create a separate, sequential follow-up implementation task (`REFACTOR` or `BUG`) to remove `Ping` and `PingResponse` from `BattleHub.cs`, update `SignalRService.ts`, and adjust affected tests (`ApiIntegrationTests.cs`, `ApplicationSessionSignalRTests.cs`, `RuntimeBoundaries.test.ts`).
- **Downstream Impact:** Follow-up implementation task required.

### Option D — Ambiguous (Architectural Ruling Required)
- **Classification:** The repository lacks sufficient evidence to determine whether public SignalR keepalive/latency probing is desired or prohibited by project architecture.
- **Action:** STOP per `AGENTS.md` §20 / `.ai/README.md` §13. Present the options and trade-offs to the human architect / Product Owner. Await approval before altering contracts.

---

## Scope

### In Scope
- Analyze repository architecture, ADRs, and codebase conventions to determine which Decision Option applies.
- Identify the canonical owner document for `BattleHub.Ping` per `.ai/workflow/documentation/documentation-change.md` §3.
- Update the canonical owner document with the resolved classification and contract details (Option A selected).
- Ensure consistency between `docs/02-technical/SIGNALR_PROTOCOL.md`, `docs/02-technical/ARCHITECTURE.md`, and existing test boundary assertions.

### Out of Scope
- **No source code implementation:** Do not modify `BattleHub.cs`, `PingResponse`, `SignalRService.ts`, or any file in `src/`.
- **No Ping removal:** Do not remove or deprecate in code within this task.
- **No SignalR runtime changes:** Do not modify hub routing, event dispatching, or connection lifecycle.
- **No BattleState changes:** Do not touch `BattleState`, `BoardState`, `PetState`, or `BossState`.
- **No Redis changes:** Do not alter active battle state persistence or serialization.
- **No gameplay changes:** Do not touch domain rules, damage, turns, or match-3 resolution.
- **No unrelated SignalR audit:** Do not alter unrelated findings from TASK-146 (e.g. `SIGNALR_PROTOCOL.md` §4 item 7 stale `GetBattleState` reference, historical migration comments, or test naming conventions).

---

## Stop Conditions

Universal stop conditions in `AGENTS.md` §20 and `.ai/README.md` §13 apply:
- If existing documentation is found to already define `BattleHub.Ping`: STOP and report conflict / redundancy.
- If an active task already owns this ambiguity: STOP and report collision.
- If resolving ownership requires changing broader architectural decisions (e.g., redesigning the authentication model or transport framework): STOP per `AGENTS.md` §18.
- If evidence is genuinely contradictory between Option A, Option B, and Option C such that choosing one would be speculative AI invention: STOP at Option D, report the ambiguity, and request human architectural ruling per `AGENTS.md` §20.

---

## Acceptance Criteria

- [x] Exactly one Decision Option (Option A, Option B, Option C, or Option D) is selected based on documented evidence and architectural rules (Option A selected).
- [x] If Option A is selected: `docs/02-technical/SIGNALR_PROTOCOL.md` is updated to define `BattleHub.Ping`, its parameters (`clientSequence`), return record (`PingResponse`), authentication requirement, and transport-only / non-gameplay semantics.
- [x] Zero lines of source code in `src/` are modified.
- [x] Zero lines of test code in `tests/` are modified.
- [x] No SignalR runtime behavior, routing, or event delivery is altered.
- [x] No BattleState, Redis, or PostgreSQL models or queries are altered.
- [x] Server authority rules are preserved (`AGENTS.md` §10).
- [x] Documentation updates follow `.ai/workflow/documentation/documentation-change.md` (canonical owner only, no duplication).

---

## Affected Files & Areas

```text
[ ] src/backend/ (Domain / Application / Infrastructure / Api) — NONE (Out of Scope)
[ ] src/frontend/client/ (scenes / runtime / services / state / ui) — NONE (Out of Scope)
[ ] tests/ (unit / integration / gameplay scenarios) — NONE (Out of Scope)
[x] docs/ (docs/02-technical/SIGNALR_PROTOCOL.md)
```

---

## Testing / Validation Requirements

### Required Verification
```text
[x] Documentation consistency check (.ai/skills/quality/documentation-consistency)
[x] Architecture conformance check (.ai/skills/quality/architecture-conformance)
[x] Scope validation check (.ai/skills/quality/scope-validation)
[x] Confirmation that git status reports zero modifications to src/ and tests/
```

---

## Completion Evidence

### Decision Taken
- **Option A — Public client-facing SignalR utility method / connectivity probe.**
  - `BattleHub.Ping(string? clientSequence = null)` is an intentional, public, client-callable SignalR Hub method retained for technical connectivity verification and keepalive probing.
  - It is exposed by the frontend client library (`SignalRService.ts:554`), tested by client runtime boundary assertions (`RuntimeBoundaries.test.ts:350`), and tested by backend API integration suites (`ApiIntegrationTests.cs:136`, `ApplicationSessionSignalRTests.cs:271`).
  - Its canonical documentation home is `docs/02-technical/SIGNALR_PROTOCOL.md` §2.2, alongside `JoinBattle`, in a dedicated non-gameplay transport calls section.

### Changed Files
- `docs/02-technical/SIGNALR_PROTOCOL.md` — Incremented version to 2.13 and added §2.2 defining `Ping(clientSequence?)` contract (parameters, `PingResponse` return shape, caller-only delivery, authentication boundary, and non-gameplay isolation).
- `tasks/completed/TASK-147-resolve-battlehub-ping-contract-ownership.md` — Moved from backlog and completed.

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
- [x] Confirmed zero source code or test modifications
