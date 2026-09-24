# TASK-034 — Application Authentication Session Mechanism

---

## Metadata

```text
Task ID:           TASK-034
Type:              ARCHITECTURE
Status:            BLOCKED
Risk:              HIGH
Priority:          HIGH
Primary Agent:     backend
Supporting Agents: persistence, realtime, testing, review
Workflow:          architecture/architecture-change.md (and architecture/adr-change.md — this task requires a new ADR before implementation)
Skills:            discovery/documentation-discovery, backend/api-contract-validation, quality/documentation-consistency, quality/architecture-conformance, testing/test-scenario-generation
Dependencies:      TASK-023
```

---

## BLOCKED — Missing Authentication Design

This task is **BLOCKED**. The application authentication session mechanism is
not defined by any authoritative document, and this task **must not choose
one** (`AGENTS.md` §7, §18; `.ai/skills/backend/api-contract-validation.md`
Stop Conditions).

`ADR-007` and the technical documents establish the authentication **boundary**
and **requirement** — frontend obtains a Discord authorization code, the
backend exchanges it server-side, the Player is matched/created, and an
"authenticated application session" is issued that all later REST and SignalR
traffic uses. **No document decides what that session actually is.**

### Required decisions before this task may start

```text
[ ] Session mechanism          — opaque server-side session, signed token,
                                 cookie-backed session, or Discord-token
                                 passthrough. NOT DECIDED.
[ ] Token/session format       — the wire shape of the session artifact.
                                 NOT DECIDED. API_CONTRACTS.md §2 offers
                                 "(e.g. JWT token or session cookie)" as an
                                 illustrative parenthetical, not a selection.
[ ] Claims / identity representation
                               — what the session carries and how the
                                 authenticated request's identity is
                                 represented. NOT DECIDED.
[ ] Expiration / lifetime      — absolute lifetime, idle timeout, renewal or
                                 refresh behaviour. NOT DECIDED.
[ ] Validation strategy        — how the backend verifies a presented session
                                 on each request. NOT DECIDED.
[ ] ASP.NET Core authentication scheme
                               — which scheme(s) are registered and configured.
                                 NOT DECIDED; no scheme is registered today.
[ ] Authorization behavior     — which endpoints/hub methods require an
                                 authenticated identity, and what an
                                 unauthenticated caller receives.
                                 NOT DECIDED.
```

None of these items may be marked complete until an authoritative document
actually defines it. See **Decisions Already Authoritative** below for what is
settled — the boundary, not the mechanism.

### Why blocking is the correct outcome

Selecting JWT, an opaque session, or a cookie-backed session is an
architectural decision with security consequences, and `AGENTS.md` §18 requires
that such a decision be proposed as an ADR and approved **before** code lands.
`docs/03-decisions/README.md` §8 records open items that are "explicitly **not
decided** by any current document"; the session mechanism is not listed there
today, which makes it an **unrecorded gap** rather than a tracked decision.

The current implementation is a development placeholder, not a design:

- `AuthController` returns `SessionToken: $"session_{Guid.NewGuid():N}"` — an
  opaque GUID string that nothing validates and no component consumes.
- No authentication or authorization middleware is registered in `Program.cs`.
- No authentication packages are referenced by any backend `.csproj`.
- `BattleHub` and every REST endpoint are entirely open; `BattleHub.Swap` and
  `JoinBattle` accept any caller with no identity check.
- The client receives `sessionToken` (`ApiService.ts`) but never sends it — no
  `Authorization` header, no `Bearer` scheme, no credential propagation.

**Implementation convention must not be promoted into design.** The existence of
`session_{guid}` is evidence of a stub, not of a chosen mechanism.

---

## Objective

*(Stated for completeness; not actionable until the BLOCKED section above is
resolved.)*

Define and implement the application authentication session contract that
carries a verified Discord identity through to an authenticated request
identity, so that the backend can resolve an authenticated REST or SignalR
call to the owning Player:

```text
Discord verified identity
        ↓
authenticated application session
        ↓
authenticated API request
        ↓
Player identity
```

This task owns the session mechanism, its validation, the authentication
pipeline, and the authenticated-request identity. It does **not** own Player
persistence, which is TASK-023.

---

## Authoritative References

- `docs/03-decisions/ADR/ADR-007-discord-activity-authentication.md` — authentication boundary; items 2–4 establish the flow and state that an "application session token" is issued and used downstream, without defining it
- `docs/02-technical/API_CONTRACTS.md` §1, §2 — endpoint summary; the auth boundary, including the "(e.g. JWT token or session cookie)" placeholder and §2 item 3's downstream-usage requirement
- `docs/02-technical/ARCHITECTURE.md` §2.3 — Discord SDK & authentication boundaries; item 3's "returns an application session token"
- `docs/02-technical/TDD.md` §2.1 — Discord SDK & authentication architecture; items 2 and 4
- `docs/03-decisions/README.md` §8 — Known Open Items registry (does **not** currently list this gap)
- `docs/03-decisions/ADR/ADR-002-modular-monolith.md` — single ASP.NET Core service boundary
- `docs/00-overview/MVP_SCOPE.md` §1 — "Persistent storage (PostgreSQL)" and MVP-required infrastructure

---

## Scope

### In Scope (once unblocked)
- The authoritative decision for the session mechanism, transport, and authentication scheme
- The identity mapping `DiscordUserId → PlayerId → authenticated request identity`
- Session payload definition (identity, claims, expiration, issuer/audience where applicable) — limited to what the authoritative decision states
- Backend validation of a presented session
- Authentication/authorization enforcement points in the ASP.NET Core pipeline
- Integration-test contract for unauthenticated, authenticated, invalid, and expired sessions, plus identity → Player mapping

### Out of Scope
- **Player persistence, Player entity, `Player.Level`, match-or-create** — owned by TASK-023
- Re-designing the Discord OAuth code-exchange flow (`ADR-007` already defines it)
- Any Discord Embedded App SDK change on the frontend beyond what the chosen mechanism requires
- Player XP / Level progression (TASK-033), Pet Level (TASK-024), Cards (TASK-028), Relics (TASK-027)
- BattleState, Redis battle state, SignalR protocol shape (`SIGNALR_PROTOCOL.md` is not redefined here)
- Introducing new gameplay rules of any kind
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2

---

## Current State

| Area | Current state |
|---|---|
| `AuthController` | Returns stub `session_{guid}` + hard-coded `player_dev` |
| Authentication middleware | None registered (`Program.cs`) |
| Authentication packages | None referenced (all backend `.csproj`) |
| Authorization attributes | None (`[Authorize]` appears nowhere) |
| `BattleHub` / REST endpoints | Fully unauthenticated |
| Client credential propagation | `sessionToken` received but never sent |
| ADR coverage | `ADR-007` defines the boundary; no ADR defines the mechanism |

---

## Acceptance Criteria

*(Provisional — every value must be replaced by a reference to the
authoritative document section that defines it, once the decision is recorded.
No mechanism, format, claim, lifetime, or scheme may be chosen by the
implementing agent.)*

- [ ] The session mechanism, transport, and authentication scheme are defined by an **approved** authoritative document (ADR-013 or an update to `ADR-007`), not by implementation convention
- [ ] An authenticated request resolves to the owning `PlayerId`, mapped from the verified `DiscordUserId` per the documented identity mapping
- [ ] A session presented to REST/SignalR is validated per the documented strategy
- [ ] Authentication and authorization are enforced at the documented pipeline location
- [ ] Unauthenticated requests receive the documented response
- [ ] Invalid and expired sessions are rejected per the documented behavior
- [ ] `POST /api/auth/discord` continues to satisfy `API_CONTRACTS.md` §2's response shape
- [ ] All relevant tests pass at the required validation depth (`core/validation.md` §2)
- [ ] Quality review checklist passes (`quality/review.md` §1)
- [ ] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001)

---

## Affected Files & Areas

```text
[x] src/backend/ (Api — Controllers/, Hubs/; Application; Infrastructure)
[ ] src/frontend/client/ (only if the chosen mechanism requires credential propagation —
                          note: ApiService.ts currently receives sessionToken without sending it)
[x] tests/ (integration tests for the session contract)
[x] docs/ (a new ADR, or an ADR-007 update — REQUIRED before implementation)
```

---

## ADR Requirement

`ADR-007` is **insufficient**: it defines the authentication boundary,
responsibility split, and Client Secret security, but decides no mechanism. Per
`AGENTS.md` §18, the mechanism needs an approved ADR **before** code.

Do **not** create a duplicate of `ADR-007` — it is not superseded and its
boundary decisions stand.

The next sequential ADR number is `ADR-013` (the highest existing is
`ADR-012`). If a new ADR is required, create it as:

```text
docs/03-decisions/ADR/ADR-013-<slug>.md
Status: NEEDS DECISION        ← must NOT be ACCEPTED while this task is BLOCKED
```

It must record the session mechanism, format, claims, lifetime, validation
strategy, authentication scheme, and authorization behavior, and it must be
registered in `docs/03-decisions/README.md`'s ADR index (§7). `docs/03-decisions/README.md`
§8 requires that a resolved open item be recorded as a new ADR rather than by
retroactively editing an existing one.

**This task's decomposition does not create that ADR**, because choosing the
mechanism is the decision the task is blocked on — writing it would be
inventing the very design it is meant to record. Creating the ADR is the first
step *after* a human supplies the decision.

---

## Implementation Notes

- Start point: `AuthController.AuthenticateDiscord`
  (`src/backend/GameServer.Api/Controllers/AuthController.cs`) returns
  `SessionToken: $"session_{Guid.NewGuid():N}"`, and `Program.cs` registers no
  authentication or authorization services.
- `DiscordAuthResponse(string SessionToken, string PlayerId)` is the auth
  endpoint's current wire record; `API_CONTRACTS.md` §2 does not name its
  members, so its shape is implementation convention rather than a documented
  contract.
- `BattleHub` (`src/backend/GameServer.Api/Hubs/BattleHub.cs`) is a thin
  transport boundary (`ARCHITECTURE.md` §2.1). Enforcement must follow the
  documented pipeline decision; the hub must not grow identity or
  authorization logic of its own.
- `ARCHITECTURE.md` §2.1's layer direction (`Domain ◀ Application ◀
  Infrastructure ◀ Api`) applies: authentication is an Api/Infrastructure
  concern and must not leak into Domain.
- `ADR-007` item 3's Client Secret boundary is already decided and must be
  preserved: the Client Secret never reaches the frontend.
- The frontend gap — `ApiService.ts` receiving `sessionToken` and never
  sending it — is a consequence of the missing mechanism, not an independent
  task.

---

## Testing Requirements

### Required Verification
```text
[ ] Unit tests         — session validation logic; identity → PlayerId resolution
[ ] Integration tests  — unauthenticated request; authenticated request; invalid
                         session; expired session; identity → Player mapping
[ ] Gameplay scenarios — N/A: authentication is not gameplay. No gameplay rule is
                         touched by this task.
```

### Key Edge Cases
- An unauthenticated request to a protected endpoint/hub method receives the documented response
- An invalid, malformed, or tampered session is rejected
- An expired session is rejected, if the documented mechanism has a lifetime
- A valid session resolves to the correct `PlayerId` via the documented identity mapping
- A session for Player A cannot act as Player B

**Existing assertions must not be weakened to make tests pass** (`AGENTS.md`
§15). The auth integration tests currently assert the `player_dev` stub and
must be updated to assert the documented behavior once it exists — that update
is shared with TASK-023 and must be coordinated, not duplicated.

---

## Stop Conditions

- **While BLOCKED: any attempt to implement is itself the violation.** Do not choose a session mechanism, token format, claim set, lifetime, signing algorithm, or authentication scheme.
- If any required decision in the BLOCKED checklist is still undefined when work resumes: STOP per `AGENTS.md` §7
- If implementation requires inventing an identity representation, claim name, or expiration value: STOP per `AGENTS.md` §7
- If the resolution requires changing an existing ADR decision rather than adding one: STOP per `AGENTS.md` §4
- If scope expands into Player persistence (TASK-023) or any gameplay system: STOP & decompose
- If task exceeds 7 skills or crosses multiple uncoupled architectural boundaries: STOP & decompose

---

## Completion Evidence

### Changed Files
- `<file path>` — <summary of change>

### Validation Results
- `<test command or suite>` — PASS (<N> tests)

### Server Authority & Scope Verification
- [ ] Confirmed zero client-authoritative gameplay logic
- [ ] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
- [ ] Confirmed no Player persistence or gameplay logic implemented (TASK-023 boundary)

---

## Revision History

**Revision 1 — task created (BLOCKED).** TASK-034 was created by the
TASK-023 decomposition revision to own the authentication session mechanism
that TASK-023 must not invent. Investigation confirmed that `ADR-007` and the
technical documents define the authentication **boundary** (frontend obtains a
code → backend exchanges it server-side → Player matched/created → session
issued) but decide **no mechanism**: no session type, format, claims, lifetime,
validation strategy, ASP.NET Core authentication scheme, or authorization
behavior is defined anywhere. `API_CONTRACTS.md` §2's "(e.g. JWT token or
session cookie)" is an illustrative parenthetical, not a decision. The task is
therefore BLOCKED pending an explicit design decision. No mechanism, token
format, claim, expiration, signing algorithm, or middleware behavior was
invented.
