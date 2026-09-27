# TASK-034 — Application Authentication Session Mechanism

---

## Metadata

```text
Task ID:           TASK-034
Type:              FEATURE
Status:            DONE
Risk:              HIGH
Priority:          HIGH
Primary Agent:     backend
Supporting Agents: client, realtime, testing, review
Workflow:          development/feature.md
Skills:            discovery/documentation-discovery, backend/api-contract-validation, quality/documentation-consistency, quality/architecture-conformance, testing/test-scenario-generation
Dependencies:      TASK-023 (DONE)
```

---

## Resolution — Contract Defined (Revision 2)

This task is **READY**. The application session mechanism that this task was
BLOCKED on is now recorded in authoritative documents by TASK-054:

```text
Design decision recorded in   docs/03-decisions/ADR/ADR-015-application-session-authentication-contract.md
                                (Status: Accepted, human decisions D1-D6;
                                 security configuration D7-D11, 2026-09-27)
Wire/behavior contract in     docs/02-technical/API_CONTRACTS.md §2.8 (+ §1, §2.4, §2.5, §4 note 6)
Hub contract in               docs/02-technical/SIGNALR_PROTOCOL.md §1 (items 3-5)
ADR registered in             docs/03-decisions/README.md §7 (index, version 1.5)
```

### The decided contract (ADR-015 D1-D6 / API_CONTRACTS §2.8)

```text
artifact      self-contained signed JWT, issued by POST /api/auth/discord only
              after the §2 identity exchange succeeds (D1)
storage       none — stateless; not in Redis (ADR-005), not in PostgreSQL
              (ADR-006), not in in-memory state (D2)
identity      claim player_id = PlayerId; server-authoritative; the client
              never supplies or overrides PlayerId or DiscordUserId;
              GameServer.PlayerId stays the server-internal request-context
              key (D3)
transport     REST: Authorization: Bearer <sessionToken>
              SignalR: the same JWT via SignalR's standard access-token
              mechanism; no application-session cookie; a Discord access
              token is never a BattleHub credential (D4, SIGNALR_PROTOCOL §1)
lifecycle     24h absolute expiry; no idle timeout, renewal, refresh, or
              revocation in MVP (D5)
enforcement   ASP.NET Core JWT Bearer scheme — exactly one application
              authentication mechanism; every REST endpoint except
              POST /api/auth/discord requires it; BattleHub rejects a
              connection without a valid session at the authentication/
              authorization boundary, with no second mechanism in the hub (D6)
failure       missing | invalid/tampered | expired → 401
              { "error": "UNAUTHENTICATED" } — one public code, no validation
              oracle; authenticated owner → 200, foreign or missing battle →
              404 BATTLE_NOT_FOUND (D5, API_CONTRACTS §4 notes 6-7)
```

### The seven original blocker decisions → resolutions

| # | Required decision | Resolution | Source |
|---|---|---|---|
| 1 | Session mechanism | Self-contained signed JWT; Discord access token is never the session | ADR-015 D1; §2.8 |
| 2 | Token/session format | Self-contained signed JWT; wire member `sessionToken` in the §2.5 response | ADR-015 D1; §2.5, §2.8 |
| 3 | Claims / identity | `player_id` = `PlayerId`, server-authoritative, never client-supplied | ADR-015 D3; §2.8 |
| 4 | Expiration / lifetime | 24h absolute expiry; no idle timeout, renewal, refresh, or revocation | ADR-015 D5; §2.8 Lifecycle |
| 5 | Validation strategy | Stateless self-validation; no session storage anywhere | ADR-015 D2; §2.8 |
| 6 | ASP.NET Core authentication scheme | Exactly one JWT Bearer scheme; no second mechanism | ADR-015 D6 |
| 7 | Authorization behavior | §1 coverage (all REST except `POST /api/auth/discord`), BattleHub boundary rejection, `401 UNAUTHENTICATED` single code, owner → `200` / foreign → `404 BATTLE_NOT_FOUND` | ADR-015 D5/D6; §1, §2.8, §4 notes 6-7; SIGNALR_PROTOCOL §1 |

### Session security configuration — decided (ADR-015 D7-D11)

`ADR-015`'s former "Not decided" scope was supplied by the human (TASK-055,
2026-09-27) and is now recorded as decision items D7-D11 in the same ADR:

```text
D7 algorithm   HS256 (HMAC-SHA-256) with one symmetric signing secret;
               validation rejects alg=none and any asymmetric alg header
D8 issuer      no iss claim; issuer validation disabled
D9 audience    claim aud = "dcacti-backend"; exact-match validation, one
               value for REST and BattleHub (one token, one scheme)
D10 key        signing secret from configuration only: host environment
   storage       variable in production, uncommitted .env or dotnet
                 user-secrets locally; never in appsettings*.json,
                 .env.example, source, tests, or any tracked file
D11 rotation   manual with overlap: kid header; validation accepts current
               + previous key; old tokens live until their 24h expiry
               (D5); no JWKS/KMS/automation
```

These five values must be implemented exactly as recorded — never replaced
by a library default. They are security configuration only: they change no
wire shape, so `API_CONTRACTS.md` §2.8 and `SIGNALR_PROTOCOL.md` §1 are
unchanged. Their scope is the JWT signing key only; Discord credential
hygiene stays with TASK-036 (BLOCKED).

### Why blocking is no longer the correct outcome

Revision 1 was BLOCKED because no document decided the mechanism — an
unrecorded gap that `AGENTS.md` §18 required be recorded as an approved ADR
before code. TASK-054 made that decision explicitly (human decisions D1-D6)
and recorded it as `ADR-015` (Accepted) with the wire contract in
`API_CONTRACTS.md` §2.8 and the hub contract in `SIGNALR_PROTOCOL.md` §1. The
task now implements a recorded contract instead of inventing one. The original
BLOCKED record is retained at the end of this file.

---

## Objective

Implement the application authentication session contract that carries a
verified Discord identity through to an authenticated request identity, so
that the backend can resolve an authenticated REST or SignalR call to the
owning Player:

```text
Discord verified identity
        ↓
authenticated application session (JWT, player_id claim)
        ↓
authenticated API request
        ↓
Player identity
```

This task owns the session issuance, its validation, the authentication
pipeline, the authenticated-request identity, and frontend credential
propagation (ADR-015 D4). It does **not** own Player persistence, which is
TASK-023 (DONE), and it does not own the Discord identity exchange, which is
`API_CONTRACTS.md` §2.2-§2.4 / `ADR-013`.

---

## Authoritative References

- `docs/03-decisions/ADR/ADR-015-application-session-authentication-contract.md` — the session decision (D1-D6) and the session security configuration (D7-D11); why
- `docs/02-technical/API_CONTRACTS.md` §2.8 — the authoritative session wire/behavior contract; §1 (coverage), §2.4 (identity), §2.5 (issuance response), §4 notes 6-7 (401 / owner-only read), §6 (error envelope)
- `docs/02-technical/SIGNALR_PROTOCOL.md` §1 items 3-5 — BattleHub connection authentication
- `docs/03-decisions/ADR/ADR-007-discord-activity-authentication.md` — items 2-4: the authentication boundary (unchanged; item 4's session is the one ADR-015 defines)
- `docs/03-decisions/ADR/ADR-013-discord-identity-exchange-contract.md` — the exchange that produces the `PlayerId` this session carries
- `docs/02-technical/API_CONTRACTS.md` §2.1-§2.7 — the exchange/issuance boundary and security requirements
- `docs/02-technical/ARCHITECTURE.md` §2.3 — Discord SDK & authentication boundaries; item 3 "returns an application session token", item 4 SignalR authentication
- `docs/02-technical/TDD.md` §2.1 — items 2 and 4 (session issued after exchange; hub connects only once the session is authenticated)
- `docs/03-decisions/ADR/ADR-002-modular-monolith.md` — single ASP.NET Core service boundary
- `docs/00-overview/MVP_SCOPE.md` §1 — "Player account (Discord identity, collection owner)"
- `docs/01-game-design/GAME_RULES.md` §18 / `ADR-001` — server authority; the authenticated identity is server-derived

---

## Scope

### In Scope

- Replacing the `session_{guid}` placeholder with the §2.8 JWT issuance in `POST /api/auth/discord` (after the §2 exchange succeeds)
- Registering exactly one ASP.NET Core JWT Bearer authentication scheme + authorization in the API pipeline (D6), and enforcing coverage per §1
- Backend validation of a presented session (stateless, per D2) and resolution of `player_id` → the server-internal request identity (`GameServer.PlayerId` context key, D3)
- `BattleHub` enforcement at the authentication/authorization boundary via the standard access-token mechanism — no custom hub authentication (SIGNALR_PROTOCOL §1, D6)
- Frontend credential propagation: store the §2.5 `sessionToken`, send `Authorization: Bearer` on REST, supply the token to SignalR's access-token mechanism (D4)
- 24h absolute expiry configuration (D5); the failure contract (`401 UNAUTHENTICATED` for missing/invalid/tampered/expired) per §2.8
- Implementing the recorded JWT security configuration exactly as ADR-015 D7-D11 specifies (HS256; no `iss`; `aud` = `dcacti-backend`; signing key from environment variable / uncommitted `.env` / user-secrets; `kid` rotation with current + previous overlap) — configuration values are read, never invented
- Integration/unit test contract for unauthenticated, authenticated, invalid, tampered, and expired sessions on REST and the hub, plus identity → Player mapping

### Out of Scope

- **Player persistence, Player entity, `Player.Level`, match-or-create** — owned by TASK-023 (DONE)
- The Discord authorization-code exchange (`ADR-013` / `API_CONTRACTS.md` §2.2-§2.4)
- Any session storage (Redis, PostgreSQL, in-memory) — excluded by D2; `ADR-005`/`ADR-006` are not extended
- Idle timeout, renewal, refresh tokens, logout, or revocation — excluded by D5
- A second authentication mechanism, a cookie-based session, or Discord-access-token passthrough — excluded by D4/D6
- Player XP / Level progression (TASK-033), Pet Level (TASK-024), Cards (TASK-028), Relics (TASK-027)
- BattleState, Redis battle state, SignalR protocol shape (`SIGNALR_PROTOCOL.md` is not redefined here)
- Introducing new gameplay rules of any kind
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2

---

## Current State

| Area | Current state |
|---|---|
| `AuthController` | Identity exchange + Player match/create implemented (`IDiscordIdentityResolver`, `IPlayerRepository`); `SessionToken` still the unvalidated `session_{guid}` placeholder |
| Authentication middleware | None registered (`Program.cs` — no auth services, no `UseAuthentication`/`UseAuthorization`) |
| Authentication packages | None referenced (all backend `.csproj` — no JWT Bearer package) |
| Authorization attributes | None (`[Authorize]` appears nowhere in `src/`) |
| `BattleController` | Reads `HttpContext.Items["GameServer.PlayerId"]`; no production writer exists, so `POST /api/battle/start` currently returns `401 UNAUTHENTICATED` (tests inject the key directly) |
| `BattleHub` / REST endpoints | Fully unauthenticated |
| Client credential propagation | `sessionToken` received (`ApiService.ts` response type) but never stored or sent; `SignalRService.connect()` builds the connection with no access-token factory |
| ADR coverage | `ADR-015` (Accepted) defines the mechanism; `API_CONTRACTS.md` §2.8 and `SIGNALR_PROTOCOL.md` §1 define the contracts |

---

## Acceptance Criteria

Every criterion below cites the authoritative document section that defines
it; no mechanism, format, claim, lifetime, or scheme may be chosen by the
implementing agent.

- [ ] `POST /api/auth/discord` issues `sessionToken` as a self-contained signed JWT after the §2 exchange succeeds, replacing `session_{guid}` (`API_CONTRACTS.md` §2.5, §2.8; ADR-015 D1)
- [ ] The JWT carries claim `player_id` = `PlayerId`; no client input supplies or overrides `PlayerId`/`DiscordUserId` (ADR-015 D3; §2.8)
- [ ] Exactly one ASP.NET Core JWT Bearer authentication scheme is registered and enforced; `Authorization: Bearer <sessionToken>` on REST; no cookie; no second mechanism anywhere (ADR-015 D4/D6)
- [ ] Authentication is stateless — no session record in Redis, PostgreSQL, or in-memory state (ADR-015 D2)
- [ ] Session lifetime is 24 hours absolute, with no idle timeout, renewal, refresh, or revocation (ADR-015 D5; §2.8 Lifecycle)
- [ ] A missing, invalid/tampered, or expired session yields `401` + `{ "error": "UNAUTHENTICATED" }` — one public code for all three, with no validation detail disclosed (`API_CONTRACTS.md` §2.8, §4 note 6, §6)
- [ ] Every REST endpoint except `POST /api/auth/discord` requires an authenticated session (`API_CONTRACTS.md` §1)
- [ ] `BattleHub` rejects a connection without a valid session at the authentication/authorization boundary, using SignalR's standard access-token mechanism, with no custom hub authentication (`SIGNALR_PROTOCOL.md` §1 items 3-5; ADR-015 D6)
- [ ] An authenticated request resolves to the owning `PlayerId`; authenticated owner → `200`, foreign or missing battle → `404 BATTLE_NOT_FOUND` (`API_CONTRACTS.md` §4 notes 6-7)
- [ ] The frontend transmits the session: `Authorization: Bearer` on REST and SignalR's access-token mechanism on hub connect (ADR-015 D4)
- [ ] The Discord access token is never issued to the client and never accepted as a `BattleHub` credential (`API_CONTRACTS.md` §2.7 item 4; `SIGNALR_PROTOCOL.md` §1 item 4)
- [ ] `POST /api/auth/discord` continues to satisfy `API_CONTRACTS.md` §2.5's response shape and §2.6's failure contract
- [ ] Session security configuration matches `ADR-015` D7-D11 exactly — algorithm HS256 only; no `iss` claim validated; `aud` = `dcacti-backend` exact-match; signing key read from configuration (environment variable in production, uncommitted `.env`/user-secrets locally) and never present in a tracked file; `kid` validated against current + previous key only (ADR-015 D7-D11)
- [ ] All relevant tests pass at the required validation depth (`core/validation.md` §2)
- [ ] Quality review checklist passes (`quality/review.md` §1)
- [ ] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001)

---

## Affected Files & Areas

```text
[x] src/backend/ (Api — Controllers/, Hubs/, Program.cs, GameServer.Api.csproj;
                  Application/Infrastructure only if a token service module is needed)
[x] src/frontend/client/ (credential propagation is in scope per ADR-015 D4:
                          ApiService.ts Authorization header; SignalRService
                          access-token factory)
[x] tests/ (integration tests for the session contract on REST and BattleHub)
[x] docs/ — DONE by TASK-054: ADR-015 created, API_CONTRACTS §2.8 added
            (v1.9), SIGNALR_PROTOCOL §1 amended (v2.6), README §7 index (v1.5).
            This task changes no docs/ content.
```

---

## ADR Status

**Satisfied — no new ADR is required.** `ADR-015` (Status: Accepted,
2026-09-26) records the session mechanism, format, claims, lifetime,
validation strategy, authentication scheme, and authorization behavior, and is
registered in `docs/03-decisions/README.md` §7. It was created by TASK-054 per
`AGENTS.md` §18 before any implementation code lands.

- `ADR-007` is not superseded; its boundary decisions stand — `ADR-015` fills
  the one item (`item 4`) it deliberately left open.
- `ADR-013` is not superseded; it owns the exchange that produces the
  `PlayerId` this session carries.
- The stale Revision 1 text naming "ADR-013" as the next ADR number was
  historical: ADR-013 was taken by the Discord exchange contract; the session
  ADR is ADR-015.
- If implementation ever requires *changing* `ADR-015` rather than implementing
  it: STOP per `AGENTS.md` §4.

---

## Implementation Notes

- Start point: `AuthController.AuthenticateDiscord`
  (`src/backend/GameServer.Api/Controllers/AuthController.cs`) — the exchange
  and Player match/create are done; replace
  `SessionToken: $"session_{Guid.NewGuid():N}"` with §2.8-compliant JWT
  issuance. Its XML comment ("session mechanism owned by TASK-034, A2.5
  defines no format") becomes false on landing — update it in the same change.
- `DiscordAuthResponse(string SessionToken, string PlayerId)` now matches the
  documented §2.5 shape — preserve both members.
- `Program.cs` must register authentication/authorization (exactly one JWT
  Bearer scheme, D6) and the corresponding middleware. The JWT Bearer package
  is not yet referenced by any backend `.csproj`.
- `BattleController.AuthenticatedPlayerItemKey` (`"GameServer.PlayerId"`) has
  no production writer today: the authenticated pipeline must write it from
  the validated `player_id` claim (D3). Tests currently inject it directly
  (`BattleStartEndpointTests`, `BattleStartSmokeTest`,
  `RedisBattleStateSmokeTest`) — those simulators may be replaced by real
  issued tokens without weakening any assertion.
- `BattleHub` (`src/backend/GameServer.Api/Hubs/BattleHub.cs`) is a thin
  transport boundary (`ARCHITECTURE.md` §2.1): enforcement happens at the
  authentication/authorization boundary; the hub must not grow identity or
  authorization logic of its own (SIGNALR_PROTOCOL §1 item 5).
- `ARCHITECTURE.md` §2.1's layer direction (`Domain ◀ Application ◀
  Infrastructure ◀ Api`) applies: authentication is an Api/Infrastructure
  concern and must not leak into Domain.
- Frontend: store the §2.5 `sessionToken`, attach
  `Authorization: Bearer <token>` to REST calls (`ApiService.ts`), and supply
  it to `HubConnectionBuilder.withUrl(url, { accessTokenFactory })`
  (`SignalRService.ts` — the builder currently has none).
- `ADR-007` item 3's Client Secret boundary is already decided and must be
  preserved: the Client Secret never reaches the frontend.
- JWT security configuration is recorded in `ADR-015` D7-D11 (HS256; no
  `iss`; `aud` = `dcacti-backend`; signing key from environment variable /
  uncommitted `.env` / user-secrets; manual `kid` rotation with
  current + previous overlap). Implement those values exactly — do not
  substitute a library default for any of them (Stop Conditions).

---

## Testing Requirements

### Required Verification
```text
[ ] Unit tests         — session issuance (claim content, expiry); token
                         validation (valid, tampered, expired); identity →
                         PlayerId resolution from the claim
[ ] Integration tests  — unauthenticated REST request → 401 UNAUTHENTICATED;
                         authenticated request → documented behavior; invalid/
                         tampered/expired session → 401 UNAUTHENTICATED (same
                         single code); BattleHub connection without a session
                         rejected at the boundary; coverage check (every
                         endpoint except POST /api/auth/discord); identity →
                         Player mapping; owner → 200 / foreign → 404
                         BATTLE_NOT_FOUND
[ ] Gameplay scenarios — N/A: authentication is not gameplay. No gameplay rule is
                         touched by this task.
```

### Key Edge Cases
- An unauthenticated request to a protected endpoint/hub method receives the documented response
- An invalid, malformed, or tampered session is rejected with the same single code as a missing or expired one (no validation oracle)
- A session expired past 24h absolute is rejected
- A valid session resolves to the correct `PlayerId` via the `player_id` claim
- A session for Player A cannot act as Player B
- The Discord access token, if presented, is not accepted as a session or hub credential

**Existing assertions must not be weakened to make tests pass** (`AGENTS.md`
§15). The old `player_dev` stub assertions are already gone
(`AuthPlayerOwnershipTests` asserts the stub is absent) and
`BattleStartEndpointTests` already asserts the documented
`401 UNAUTHENTICATED` — both must keep passing under real token issuance.

---

## Stop Conditions

- If implementation would deviate from `ADR-015` D7-D11 (algorithm, issuer, audience, signing-key storage, rotation), or needs any of those five values that is not recorded there: STOP per `AGENTS.md` §7 / §4 — do not choose a default (`AGENTS.md` §20: ambiguous requirement)
- If any element of ADR-015 D1-D6 / `API_CONTRACTS.md` §2.8 would have to be invented rather than implemented: STOP per `AGENTS.md` §7
- If satisfying the contract requires session storage (Redis/PostgreSQL/memory), a cookie session, a second authentication mechanism, or custom `BattleHub` authentication: STOP & report — that contradicts D2/D4/D6
- If any client-supplied input would establish authenticated identity or ownership: STOP per `AGENTS.md` §10
- If the resolution requires changing an existing ADR decision (ADR-015, ADR-007, ADR-013) rather than implementing it: STOP per `AGENTS.md` §4
- If scope expands into Player persistence (TASK-023) or any gameplay system: STOP & decompose
- If task exceeds 7 skills or crosses multiple uncoupled architectural boundaries: STOP & decompose

---

## Completion Evidence

### Changed Files

**Created — backend authentication layer (`src/backend/GameServer.Api/Authentication/`)**
- `ApplicationSessionClaims.cs` — the session contract's fixed strings: claim `player_id` (D3), audience `dcacti-backend` (D9), header `kid` (D11). Declared once so issuance and validation cannot drift.
- `ApplicationSessionOptions.cs` — every ADR-015 value as a constant or configuration key: 24h absolute lifetime and zero clock skew (D5), HS256 (D7), configuration paths for the current/previous secret and `kid` (D10/D11). No library default stands in for a recorded value.
- `ApplicationSessionSigningKeys.cs` — the current + previous HS256 key pair (D7/D11), read from configuration only (D10). Refuses to compose without a secret, without a `kid`, or with a secret shorter than HS256 requires (RFC 7518 §3.2); never generates or defaults a key (`AGENTS.md` §7).
- `ApplicationSessionTokenService.cs` — issues the signed JWT: `player_id` = PlayerId, `aud` = `dcacti-backend`, no `iss`, 24h absolute, `kid` header. Writes nothing anywhere (D2); called only by `POST /api/auth/discord` (D1).
- `ApplicationSessionAuthentication.cs` — registers exactly one JWT Bearer scheme and the authorization pipeline (D6). Validation is set from ADR-015 explicitly: `ValidAlgorithms = [HS256]`, `ValidateIssuer = false`, `ValidAudience = "dcacti-backend"`, `ClockSkew = 0`, `IssuerSigningKeys = current + previous`. No JWKS/KMS/automated rotation.
- `PlayerIdClaimRequirement.cs` — requires the validated principal to carry `player_id` (D3). A requirement rather than `RequireClaim` so "no identity" is not a `403`.
- `UnauthenticatedForbidResultHandler.cs` — routes an authorization forbid to the documented response, so every session failure is one `401 UNAUTHENTICATED` (D5).
- `UnauthenticatedResponse.cs` — writes `401 { "error": "UNAUTHENTICATED" }` for the challenge path (D5, §2.8, §6).
- `AuthenticatedPlayer.cs` — resolves the validated principal's `player_id` → `PlayerId`; the single writer of the `GameServer.PlayerId` request-context key (D3).
- `AuthenticatedPlayerMiddleware.cs` — publishes that identity into the request context after authentication; invents nothing.

**Modified — backend**
- `Program.cs` — registers the session authentication; `UseRouting`/`UseAuthentication`/identity middleware/`UseAuthorization` in order; the SignalR negotiate handshake exempted from the fallback policy (transport constraint) while the connection stays protected; health probes left anonymous.
- `Controllers/AuthController.cs` — replaced `session_{guid}` with real §2.8 JWT issuance; `[AllowAnonymous]` on the one session-establishing endpoint; stale "TBD by TASK-034" XML comments corrected.
- `Controllers/BattleController.cs` — `[Authorize]`; reads the authenticated identity through the shared resolver; the `GameServer.PlayerId` key now delegates to its single definition.
- `Hubs/BattleHub.cs` — `OnConnectedAsync` refuses a connection whose validated principal carries no `player_id`. No token parsing, no key, no ownership decision: the session is authenticated by the one JWT Bearer scheme and this is one identity read. No gameplay or protocol member added.
- `GameServer.Api.csproj` — references `Microsoft.AspNetCore.Authentication.JwtBearer` (the handler only; every validated value comes from ADR-015).

**Created — backend tests (`tests/backend/GameServer.Api.Tests/`)**
- `TestApplicationSession.cs` — test-only signing keys and token minting, so a test can produce tokens the production issuer would refuse. Explicitly test-only constants; no real secret.
- `ApplicationSessionConfigurationTests.cs` — D5/D7–D11 asserted against the composed `JwtBearerOptions` and the key builder (algorithm, issuer, audience, skew, key set, absence/shortness/no-`kid`/duplicate-`kid` refusal).
- `ApplicationSessionRESTTests.cs` — valid/invalid/foreign/unknown-key/tampered/expired/wrong-audience/no-`player_id`/`alg=none`/RS256/Discord-access-token all → `401 UNAUTHENTICATED`; the exchange endpoint stays the one exception; identity comes from the claim, never from a client-supplied `PlayerId`.
- `ApplicationSessionSignalRTests.cs` — the same matrix over the hub, plus valid-session connect, previous-key overlap (D11), claimed-player identity, and the documented hub methods still working.

**Modified — backend tests**
- `ApiIntegrationTests.cs`, `BattleStartEndpointTests.cs`, `BattleStartSmokeTest.cs`, `RedisBattleStateSmokeTest.cs`, `BossResponseWireTests.cs`, `AuthPlayerOwnershipTests.cs` — the test hosts now supply the signing key through configuration and present **real Bearer JWTs** (REST) and the standard SignalR access token (hub). The former `X-Test-PlayerId`/`X-Smoke-PlayerId` request-context injections and the stand-in identity middleware are gone.
- `GameServer.Api.Tests.csproj` — adds the JwtBearer package so the hosts can compose the real scheme.

**Created / modified — frontend**
- `src/frontend/client/src/services/api/ApplicationSession.ts` (new) — holds the §2.5 session in memory and exposes the `Authorization: Bearer` header (D4). No persistence, no refresh, no revocation (D5).
- `src/frontend/client/src/services/api/ApiService.ts` — records the session from the exchange response and attaches it to authenticated REST calls (D4).
- `src/frontend/client/src/services/realtime/SignalRService.ts` — supplies the session through SignalR's standard `accessTokenFactory` (D4); the Discord access token is never used.
- `src/frontend/client/tests/ApplicationSession.test.ts` (new) and `tests/SignalRService.test.ts` — propagation and the access-token mechanism asserted.

### Validation Results

Baseline before implementation (recorded first, per §26): `Domain 911 · Application 210 · Infrastructure 139 · API 115` — 1375 tests, 0 failures.

- `dotnet build src/backend/GameServer.sln` — **PASS**, 0 errors, 8 warnings (all `NU1900` package-vulnerability-feed network timeouts; no new code warnings)
- `GameServer.Domain.Tests` — **PASS (911)**
- `GameServer.Application.Tests` — **PASS (210)**
- `GameServer.Infrastructure.Tests` — **PASS (139)**
- `GameServer.Api.Tests` — **PASS (168**, was 115; **+53** authentication tests)
- Full backend suite — **PASS (1428 tests, 0 failures)**
- Frontend `npx tsc --noEmit` — **PASS**, no errors
- Frontend `npx vitest run` — **PASS (202 tests**, was 188)

### Security Contract Implemented (ADR-015)

```text
Algorithm:    HS256 only — ValidAlgorithms = [HmacSha256]; alg=none and any
              asymmetric alg rejected (D7)
Issuer:       no iss claim; ValidateIssuer = false (D8)
Audience:     aud = "dcacti-backend"; ValidateAudience = true, exact match (D9)
Key Storage:  configuration only — ApplicationSession:CurrentKey:{Secret,KeyId}
              and :PreviousKey:{...}; never in appsettings*.json, .env.example,
              source, tests, or any tracked artifact. Absent secret refuses to
              compose (D10)
Rotation:     manual overlap — kid header; validation accepts exactly the
              current + previous key; no JWKS, no KMS, no automation (D11)
Lifetime:     24 hours absolute; ClockSkew = 0; no idle timeout, renewal,
              refresh, or revocation (D5)
```

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic — authentication only; no gameplay file touched
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1) — "Player account (Discord identity, collection owner)"
- [x] Confirmed no Player persistence or gameplay logic implemented (TASK-023 boundary) — `IPlayerRepository` consumed, unchanged
- [x] Confirmed no Redis/DB session store, no refresh token, no revocation system (D2/D5)
- [x] Confirmed exactly one authentication mechanism — one `AddAuthentication`/`AddJwtBearer`; no cookie, no custom session middleware
- [x] Confirmed no gameplay, `BattleState`, or `BattleResult` change — `git diff` on Domain is empty
- [x] Confirmed no TASK-033 / TASK-035 / TASK-036 / TASK-041 implementation
- [x] Confirmed no speculative architecture (`AuthServiceManager`, `SecurityOrchestrator`, etc.)
- [x] Confirmed no real signing key committed; `.env.example` unmodified
- [x] Confirmed documentation unchanged — no `docs/` or `docs/02-technical/` edit was required

---

## Revision History

**Revision 4 — implemented; Status READY → DONE.** The application session
contract was implemented against `ADR-015` D1–D11 exactly, with no value chosen
by the implementing agent. The `session_{guid}` placeholder is replaced by real
§2.8 JWT issuance; one JWT Bearer scheme is registered and enforced on every
endpoint except the exchange, with the `BattleHub` connection held to the same
session; the `GameServer.PlayerId` request-context key that previously had no
production writer is now populated from the validated `player_id` claim; and the
frontend transmits the session on REST and to the hub.

Two implementation findings were resolved without changing any documented
contract. (1) A token that validates but carries no `player_id` would have been
answered `403` by `RequireClaim`; `API_CONTRACTS.md` §2.8 fixes one response for
every session failure, so the requirement is expressed as a custom
`PlayerIdentity` requirement and the forbid result is routed to the same
`401 UNAUTHENTICATED`. (2) SignalR's negotiate handshake is issued before the
client can attach `Authorization`, so the fallback session policy would have made
the hub unreachable; that single request is exempted, and the connection itself
is still authenticated and refused without a valid session.

The pre-existing `ApiIntegrationTests` hub tests failed at baseline for an
unrelated reason (a static field in the new test helper was initialized before
the key configuration it depends on) and were fixed by minting the session on
first use. No assertion was weakened to make a test pass.

`ADR-015` was not modified. No `docs/` content was changed: the implementation
matches D1–D11 and every claim in `API_CONTRACTS.md` §2.8,
`SIGNALR_PROTOCOL.md` §1, `ARCHITECTURE.md` §2.3, and `TDD.md` §2.1 as written,
so no contradiction was found and none was invented.

Revisions 1–3 text is retained unchanged below.

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
invented. (Full original text retained under **Original Blocker Record**.)

**Revision 2 — contract reconciliation; task READY.** Readiness/contract
reconciliation audit after TASK-054. The seven blocker decisions are resolved
by `ADR-015` (Accepted, human decisions D1-D6), `API_CONTRACTS.md` §2.8
(v1.9), and `SIGNALR_PROTOCOL.md` §1 (v2.6); the resolution table above maps
each original decision to its source. Reconciled stale text: Status
`BLOCKED` → `READY`; Type `ARCHITECTURE` → `FEATURE` and Workflow
`architecture/architecture-change.md` (with its "requires a new ADR before
implementation" note) → `development/feature.md`, because no architecture doc
or ADR remains to be created — the task now builds a fully documented
capability (`TASK_TYPES.md` §2 FEATURE); Supporting Agents `persistence` →
`client` (session storage is excluded by D2; frontend propagation is in scope
per D4); Dependencies annotated `TASK-023 (DONE)` (verified in
`tasks/completed/`); ADR Requirement section (stale "next sequential ADR
number is ADR-013") replaced by ADR Status (satisfied by ADR-015); Current
State re-inspected (exchange + Player match/create now implemented; only the
session issuance/validation/propagation gap remains); Acceptance Criteria
replaced with §2.8-cited criteria; Testing note about `player_dev` assertions
replaced (they are already updated); Stop Conditions rewritten (no longer
"while BLOCKED"). The JWT algorithm/issuer/audience/key-storage/rotation gap
is explicitly reported as implementation security configuration requiring
human input (ADR-015 "Not decided") and does not block readiness. Verified
dependency states: TASK-023 DONE, TASK-035 DONE, TASK-054 DONE; TASK-041
untouched (BACKLOG). No `src/`, `tests/`, ADR, or technical document was
modified by this reconciliation.

**Revision 3 — security configuration recorded; still READY.** The five
ADR-015 "Not decided" items were supplied by the human (TASK-055 decision
round, 2026-09-27) and recorded in ADR-015 as D7-D11: HS256; no `iss`
claim; `aud` = `dcacti-backend`; signing key from configuration only
(environment variable in production, uncommitted `.env`/`dotnet
user-secrets` in local development, never in a tracked file); manual
rotation with `kid` and current + previous key overlap. References to the
former gap reconciled in five places (Resolution block, Authoritative
References, In Scope, Acceptance Criteria, Implementation Notes, Stop
Conditions) to cite D7-D11. Status remains READY — it was already READY
since Revision 2, and recording configuration values removes work, not
readiness. Revisions 1-2 text is retained as history and was not rewritten.
No `src/`, `tests/`, `docs/02-technical/` content, TASK-041, or TASK-033
file was modified. TASK-055 owns the ADR edit itself.

---

## Original Blocker Record (Revision 1, superseded)

The following records the blocked state this task was created in. It is
retained for history; the **Resolution** section above now resolves it.

> This task is **BLOCKED**. The application authentication session mechanism is
> not defined by any authoritative document, and this task **must not choose
> one** (`AGENTS.md` §7, §18; `.ai/skills/backend/api-contract-validation.md`
> Stop Conditions).
>
> `ADR-007` and the technical documents establish the authentication **boundary**
> and **requirement** — frontend obtains a Discord authorization code, the
> backend exchanges it server-side, the Player is matched/created, and an
> "authenticated application session" is issued that all later REST and SignalR
> traffic uses. **No document decides what that session actually is.**
>
> The seven required decisions were: session mechanism; token/session format;
> claims/identity representation; expiration/lifetime; validation strategy;
> ASP.NET Core authentication scheme; authorization behavior — each **NOT
> DECIDED**, and none markable complete until an authoritative document
> defined it.
>
> Why blocking was the correct outcome: selecting JWT, an opaque session, or a
> cookie-backed session is an architectural decision with security
> consequences, and `AGENTS.md` §18 required that such a decision be proposed
> as an ADR and approved **before** code lands; the gap was unrecorded in
> `docs/03-decisions/README.md` §8. The implementation was a development
> placeholder: `AuthController` returned `session_{guid}`, `Program.cs`
> registered no authentication middleware, no authentication packages were
> referenced, `BattleHub` and every REST endpoint were open, and the client
> received `sessionToken` without ever sending it. **Implementation convention
> must not be promoted into design.**
