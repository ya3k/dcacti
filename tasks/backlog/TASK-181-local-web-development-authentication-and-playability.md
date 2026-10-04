# TASK-181 — Local Web Development Authentication & Playability

<!--
  GEN-TASK EXECUTION MANIFEST — FEATURE / INFRASTRUCTURE TASK
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.

  References docs/ and src/ by path and section — it does NOT copy game
  rules, formulas, magnitudes, schemas, or API payload shapes.

  SCOPE OF THIS TASK: remove the local-development authentication/iframe
  blocker reported by the TASK-180 MVP playability audit, so one developer
  can play the existing MVP loop in a normal browser tab.

  IT ADDS NO GAMEPLAY. IT CHANGES NO RULE. IT RESOLVES NO OTHER TASK.

  READ "Authoritative Constraint That Overrides A Naive Reading" BEFORE
  PLANNING: the obvious secret-file approach is forbidden by ADR-015 D10.
-->

---

## Metadata

```text
Task ID:           TASK-181
Type:              FEATURE (Infrastructure) — TASK_TYPES.md §2; crosses
                   backend auth configuration + frontend bootstrap, so
                   core/validation.md §2's integration depth applies.
Status:            BACKLOG
Risk:              HIGH (the change adds an authentication path. A mistake
                   is an authorization bypass, not a cosmetic defect. Every
                   acceptance criterion below exists to bound that risk.)
Priority:          HIGH
Primary Agent:     backend (the session-issuing side owns the boundary;
                   the frontend consumes what it issues)
Supporting Agents: client (standalone-browser bootstrap + development
                   adapter), review (security boundary review is REQUIRED,
                   not optional), testing (dev-auth boundary coverage)
Workflow:          development/feature.md
Skills:            discovery/documentation-discovery,
                   discovery/impact-analysis,
                   quality/architecture-conformance,
                   quality/scope-validation,
                   testing/test-scenario-generation
Dependencies:      TASK-180 (audit — source of this work; see "TASK-180
                   Audit Report Availability"), TASK-036 (BLOCKED —
                   constrains AC-15/AC-16), TASK-023 (Player
                   ownership/authentication), TASK-034 (application session
                   mechanism), TASK-035 (Discord identity exchange
                   contract — NOT implemented by this task)
```

---

## Objective

Give a developer a documented, development-only way to authenticate the
existing frontend against the existing backend from a **normal browser tab**
— no Discord Activity iframe, no real Discord session, no manually injected
JWT — so that the already-implemented MVP loop (Player → collection →
Lobby → Battle → Match-3/combat → Victory/Defeat) is playable locally.

The development path must mint a **real** application session through the
**existing** session infrastructure and then use the **existing**,
unchanged authenticated endpoints and authorization policies. It is a
different way to *obtain* the session, never a way to *avoid* it.

```text
INTENDED                         FORBIDDEN
dev auth                         dev auth
   ↓                                ↓
real session (existing service)  bypass all auth
   ↓                                ↓
real authenticated APIs          special unauthenticated battle mode
```

---

## Authoritative Constraint That Overrides A Naive Reading

**This is the single most important section of this task. Read it before
writing any plan.**

`ADR-015` D10 states the application session signing secret must **never**
appear in `appsettings.json`, **`appsettings.Development.json`**,
`.env.example`, source code, a test fixture, a log line, or any committed
artifact. It permits exactly two local-development sources:

```text
an uncommitted .env file   |   a dotnet user-secrets store
```

`ApplicationSessionOptions.SectionName`'s own documentation repeats this
explicitly for the very file this work is tempted to edit, and
`ApplicationSessionConfigurationTests.NoApplicationSessionValue_ShouldBeReadFromATrackedFile`
exists to hold that line.

Consequences that bind this task:

1. **Do NOT place a signing secret in `appsettings.Development.json`.**
   A clearly-labelled, obviously-fake, development-only placeholder is
   still a committed secret value in a tracked file and still violates
   D10. "It is only development" is not an exception the ADR grants.
2. **`ApplicationSessionOptions` may not be changed to relax D7–D11.**
   That ADR's own implementation constraints state that any proposed change
   to D7–D11 is a change to the ADR and must STOP per `AGENTS.md` §4 and be
   recorded as a human decision first. Do not widen `MinimumSecretBytes`,
   add a development default, or generate-and-continue on an absent secret.
   The current fail-closed behaviour must stay fail-closed.
3. **The mechanism is already decided.** Satisfy AC-15 through the
   existing, ADR-sanctioned channel (an uncommitted local `.env` or
   `dotnet user-secrets`), and make the *convenience* problem the thing
   this task actually solves — see AC-15.

**TASK-036 is BLOCKED and is not resolved here.** TASK-036 owns the
undecided *local secrets convention* and the plaintext-at-rest question for
the **Discord** credential. The signing secret is a different secret with a
different owner (`ADR-015` D10 already decides it), so this task may follow
D10 without deciding anything TASK-036 owns. Do not create the ADR or the
secrets-management decision TASK-036 is blocked on, and do not touch the
Discord `ClientSecret` while working here.

If any step of this task appears to require changing D7–D11 or committing a
secret: **STOP per `AGENTS.md` §4/§7 and report**, rather than proceeding.

---

## TASK-180 Audit Report Availability

The TASK-180 MVP playability audit report is **not present in the working
tree and no commit on `master` references it** — a repository-wide search
for `TASK-180` returns no matches. Its findings are therefore taken from
the work request that generated this task, and only the parts that are
**independently verifiable in the code** are treated as established fact:

| Reported finding | Independently verified here | Evidence |
|---|---|---|
| Backend startup requires `ApplicationSession:CurrentKey:Secret` | **Confirmed** | `ApplicationSessionSigningKeys.CreateHmacKey` throws `ConfigurationException` when absent; `ApplicationSessionAuthentication.AddApplicationSessionAuthentication` reads it at composition, so the host refuses to start |
| The key is absent from local development configuration | **Confirmed** | `appsettings.json` has no `ApplicationSession` section; `appsettings.Development.json` has none; `src/backend/.env.example` has none |
| `window.self !== window.top` blocks a normal browser | **Confirmed** | `DiscordService.initialize` returns `isAvailable: false` when not framed; `App.bootstrapApplication` sets session status `error` and returns without authenticating |

**The implementing agent must not treat this task as a substitute for the
audit.** If the TASK-180 report becomes available, read it and reconcile
it against the table above before planning. Where the report and the code
disagree, the code and the authoritative documents govern (`AGENTS.md` §2).
If the report describes a blocker not listed here, report the delta rather
than silently expanding scope.

---

## Authoritative References

- `docs/03-decisions/ADR/ADR-015-application-session-authentication-contract.md` D1–D11 — **owning contract.** D5 (24h absolute, no refresh/revocation), D6 (exactly one authentication mechanism, JWT Bearer, every endpoint except the exchange requires a session), D7 (HS256 only), D10 (**secret from configuration, never from tracked files** — the binding constraint above), D11 (`kid`; current + previous key only)
- `docs/02-technical/API_CONTRACTS.md` §2.7 — exchange security requirements; §2.8 — the session's artifact, `player_id` identity, Bearer transport, **coverage** (only `POST /api/auth/discord` is unauthenticated), failure behaviour (`401 UNAUTHENTICATED`), lifecycle
- `docs/03-decisions/ADR/ADR-007-discord-activity-authentication.md` items 1–4 — Discord Activity identity boundary; the Client Secret is backend-only and never in client artifacts
- `docs/03-decisions/ADR/ADR-013-discord-identity-exchange-contract.md` — the exchange contract consumed (not implemented) here; item 13 places the implementation in Infrastructure
- `docs/02-technical/TDD.md` §2.1 item 4 — session issued after the exchange; hub connects only once the session is authenticated
- `docs/02-technical/SIGNALR_PROTOCOL.md` §1 — connection authentication and the access-token mechanism
- `docs/02-technical/ARCHITECTURE.md` §5 — anti-overengineering standard (the reason a development adapter must be the smallest possible one)
- `docs/00-overview/MVP_SCOPE.md` §1 — the loop this task makes reachable; §2/§4 — OUT/FUTURE (nothing here may be added)
- `docs/00-overview/ROADMAP.md` §1 Phase 3 — Discord Activity integration remains a separate phase; this task does not pull it forward

---

## Scope

### In Scope

- A **development-only** backend authentication path that issues a real
  application session through the existing `ApplicationSessionTokenService`,
  activated only in the Development environment (AC-01, AC-02, AC-06).
- A deterministic development identity that resolves to a **real persisted
  Player** through the existing `IPlayerRepository` create/load path
  (AC-05, AC-07).
- A frontend development bootstrap mode that lets the app start in a normal
  browser tab and obtain a session, without removing or weakening the
  Discord Activity path (AC-03, AC-04, AC-14).
- Making local startup possible **without inventing an undocumented
  secret**, by making the ADR-sanctioned secret channel discoverable and
  documented rather than by committing a secret (AC-15, AC-16).
- Tests for the development-authentication boundary and confirmation that
  the existing suites still pass (AC-17, AC-18).
- Documentation of the local flow in the existing developer documentation
  location, plus the verified smoke test (AC-19, AC-20).

### Out of Scope

- **The real Discord OAuth token exchange** — owned by TASK-035
  (`UnconfiguredDiscordIdentityResolver` stays the registered
  implementation; see AC-04)
- **Discord Client Secret hygiene, the local secrets convention, and
  rotation** — owned by TASK-036 (BLOCKED). Do not resolve it; do not
  touch the Discord `ClientSecret`
- **Any change to `ADR-015` D7–D11** — that is an ADR change (`AGENTS.md`
  §18) and a STOP condition, not an implementation choice
- Production Discord Activity deployment, HTTPS/tunnel setup, production
  database deployment, production Redis deployment
- Any gameplay change: Match-3, combat, Boss, Relic, Card, Pet mechanics,
  ResultScene navigation, campaign/stage implementation
- The migration for the six Relics (`RELIC_RULES.md` content provisioning)
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2
- A second authentication scheme, a second JWT implementation, or
  per-endpoint authorization special-casing (see Technical Design
  Constraints)

---

## Current State

```text
src/backend/GameServer.Api/Program.cs
    AddApplicationSessionAuthentication(configuration)  ← reads the secret at
                                                          composition; throws if absent
src/backend/GameServer.Api/Authentication/ApplicationSessionSigningKeys.cs
    CreateHmacKey(...)  ← ConfigurationException when secret missing/<32 bytes
                          (the fail-closed behaviour that MUST remain)
src/backend/GameServer.Api/Authentication/ApplicationSessionOptions.cs
    SectionName / CurrentSecretPath / CurrentKeyIdPath / PreviousSecretPath /
    PreviousKeyIdPath, Lifetime (24h), ClockSkew (zero)
src/backend/GameServer.Api/Authentication/ApplicationSessionTokenService.cs
    Issue(playerId, now)  ← the ONLY session issuer to reuse
src/backend/GameServer.Api/Controllers/AuthController.cs
    POST /api/auth/discord → IDiscordIdentityResolver → GetOrCreateByDiscordUserIdAsync
                           → ApplicationSessionTokenService.Issue → { sessionToken, playerId }
    [AllowAnonymous] on this one action only
src/backend/GameServer.Infrastructure/Discord/UnconfiguredDiscordIdentityResolver.cs
    always returns 503 DISCORD_UNAVAILABLE; creates no Player
src/backend/GameServer.Infrastructure/DependencyInjection.cs
    AddSingleton<IDiscordIdentityResolver, UnconfiguredDiscordIdentityResolver>()
src/frontend/client/src/services/discord/DiscordService.ts
    initialize()  ← window.self !== window.top gate; returns isAvailable:false
                    in a normal tab
src/frontend/client/src/app/App.tsx
    bootstrapApplication()  ← initialize → getAuthorizationCode → authenticateDiscord
                               → setSessionStatus → runtime.initialize(hub)
                               (returns early with 'error' when isAvailable is false)
src/frontend/client/src/services/api/ApplicationSession.ts
    in-memory session holder; never persisted (ADR-015 D5)
```

No environment branching exists anywhere in the backend today: a search for
`IsDevelopment` / `IHostEnvironment` / `EnvironmentName` across
`src/backend/GameServer.Api` returns no matches. Environment-conditional
registration would be a new pattern here, so it must be justified by the
existing composition style rather than introduced casually.

---

## Acceptance Criteria

### AC-01 — Development-only activation
Local development authentication is available **only** in the intended
Development environment. It is not reachable in Production or Staging.

### AC-02 — Production safety
The development authentication path **cannot** be enabled accidentally in
production through normal configuration. Enabling it must require an
explicit, development-scoped act (`ASPNETCORE_ENVIRONMENT=Development`
plus its own opt-in switch) — never a default, and never a switch that a
production `appsettings.json` could turn on by itself.

### AC-03 — Standalone browser boot
The frontend boots in a normal browser tab during Development: `App`
reaches `setSessionStatus('authenticated')` and calls
`runtime.initialize(BATTLE_HUB_URL)` without a Discord iframe.

### AC-04 — Discord production boundary
Discord Activity authentication behaviour is unchanged:
`DiscordService`'s SDK integration remains, the iframe path still runs the
Discord flow when framed, and `IDiscordIdentityResolver` /
`UnconfiguredDiscordIdentityResolver` remain the registered
production/default pair. This task does **not** implement the real exchange.

### AC-05 — Development identity
The developer obtains a **deterministic** development identity — stable
across restarts and repeated runs for a given developer/configuration —
suitable for repeatable local testing. It is never derived from
client-supplied input in a way that would let a caller choose an arbitrary
existing Player.

### AC-06 — Application session
Development authentication issues a valid application session through the
**existing** session infrastructure (`ApplicationSessionTokenService`),
carrying the documented `player_id` claim and the D7–D11 security
properties. No second token service, claim shape, or signing path exists.

### AC-07 — Player resolution
The development identity resolves to an **actual persisted Player** through
the normal `IPlayerRepository` create/load path, including the documented
starter-ownership composition on the creation branch. No Player row is
fabricated outside the repository.

### AC-08 — Authorization
Collection and battle endpoints continue to use their **normal**
authorization requirements. No endpoint gains an `[AllowAnonymous]`, an
authorization bypass, or a development-only policy relaxation. An
unauthenticated request to a covered endpoint still returns the documented
`401 { "error": "UNAUTHENTICATED" }`.

### AC-09 — Collection loading
The developer can load Pet/Card/Relic collection data with the development
session, through the existing §5 collection reads.

### AC-10 — Lobby
The developer reaches the Lobby without manually injecting a JWT or editing
client state by hand.

### AC-11 — Battle start
The developer starts the existing MVP battle flow via
`POST /api/battle/start` under the development session.

### AC-12 — SignalR
An authenticated `BattleHub` connection succeeds with the development
session through the documented access-token mechanism, and the hub still
rejects a connection with no valid session.

### AC-13 — Existing gameplay
Match-3 / combat / Boss / Relic / Card / Pet runtime behaviour is
**unchanged**. This task alters no resolution logic, no event shape, and no
game state.

### AC-14 — Session persistence
Expected local behaviour across a page refresh is defined explicitly and
matches the existing architecture: `ADR-015` D5 provides no refresh and no
revocation, and `ApplicationSession` holds the token **in memory only** and
never persists it. The task must state the resulting local behaviour (a
refresh re-authenticates through the development path) rather than
introducing token persistence to work around it.

### AC-15 — Configuration
Local development startup succeeds without the developer inventing an
undocumented secret: the required configuration values and their
ADR-sanctioned source are covered by existing developer documentation, and
the backend still **fails closed with a clear, value-free message** when
the secret is absent. Satisfying this **must not** put a secret in a
tracked file (see "Authoritative Constraint That Overrides A Naive
Reading").

### AC-16 — Security
No production Discord credential, Discord Client Secret, or real secret is
introduced, committed, logged, or returned. No development placeholder is
presented as a usable production value. The Discord `ClientSecret` handling
owned by TASK-036 is untouched.

### AC-17 — Tests
Tests are added or updated for the development-authentication boundary, if
the repository's testing architecture requires them (see Testing
Requirements). At minimum: the development path is asserted to be
**unavailable** outside Development, and a development session is asserted
to be accepted by the normal authorization pipeline.

### AC-18 — Existing tests
Existing authentication, application, and gameplay tests remain passing —
in particular `ApplicationSessionConfigurationTests`,
`ApplicationSessionRESTTests`, and `ApplicationSessionSignalRTests`. No
existing test may be weakened or deleted to make the new path fit
(`AGENTS.md` §15).

### AC-19 — Web smoke test
A developer can execute the documented local flow —

```text
Browser
 → Dev Auth
 → Player
 → Lobby
 → Battle
 → Match-3
 → Combat
 → Victory/Defeat
```

— without manually injecting tokens, and the run is **actually performed
and reported**. Do not claim end-to-end success that was not verified.

### AC-20 — Documentation
The local-development startup and authentication flow is documented in the
appropriate **existing** developer documentation location. Do not create
unnecessary new documentation files. Any behaviour that changes what a
document already describes follows `AGENTS.md` §17.

### Baseline criteria

- [ ] All relevant tests pass at the required validation depth (`core/validation.md` §2)
- [ ] Quality review checklist passes (`quality/review.md` §1)
- [ ] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001)
- [ ] No `ADR-015` D7–D11 value changed, and no secret committed (`ADR-015` D10)

---

## Technical Design Constraints

**Inspect the existing abstractions before introducing any new one.** The
implementation must prefer the existing authentication pipeline, the
existing `ApplicationSessionTokenService`, the existing
`IDiscordIdentityResolver` seam, the existing dependency injection
composition, and the existing environment/configuration model.

Prefer, in rough order:

1. **The existing `IDiscordIdentityResolver` seam** — it is already the
   documented boundary between "resolve an identity" and "issue a session",
   and `AuthController` already consumes it. If a development resolver can
   be substituted at that seam without altering the endpoint's contract,
   that is the smallest correct change.
2. **Environment-conditional DI registration** in the existing composition
   root — noting that this pattern does not exist yet in this backend and
   must therefore be kept minimal and explicit.
3. A development-only frontend bootstrap branch that calls a
   development-scoped endpoint.

Avoid — each of these is a defect, not a shortcut:

- a second authentication framework or a second JWT implementation
- hard-coded controller bypasses or `[AllowAnonymous]` added to any
  existing protected endpoint
- special-case authorization inside individual endpoints
- fake Discord OAuth inside the production code path
- frontend-only fake authentication that does not produce a **real**
  backend session
- a development path that can mint a session for an arbitrary
  caller-chosen `PlayerId`

**Do not prescribe the endpoint shape here.** Determine the smallest
change consistent with `API_CONTRACTS.md` §1/§2.8 coverage. If the chosen
design changes the documented REST surface (a new route), that is a
documentation-consistency question under `AGENTS.md` §17 and must be
handled explicitly rather than left implicit — report it if it cannot be
resolved within this task's authority.

---

## Relationship to Existing Blockers

```text
TASK-181
    ↓
unblocks local browser playability (developer can play the MVP loop
in a normal browser tab, without a Discord Activity session)

TASK-036 + future Discord OAuth exchange task (TASK-035's downstream
implementation)
    ↓
unblocks real Discord Activity authentication
```

**TASK-181 does NOT resolve TASK-036 and does NOT implement the real
Discord OAuth exchange.** The two paths are independent: this task makes
the *local* loop playable while the Discord path stays exactly as it is
today (`UnconfiguredDiscordIdentityResolver` continues to return
`503 DISCORD_UNAVAILABLE`).

---

## Affected Files & Areas

```text
[x] src/backend/GameServer.Api/ (composition root / environment-conditional
                                 registration, authentication registration,
                                 possibly the auth boundary)
[x] src/backend/GameServer.Infrastructure/ (only if the identity seam is
                                            substituted at its existing
                                            registration point)
[x] src/frontend/client/ (bootstrap / development adapter / session
                          acquisition path)
[x] tests/ (development-authentication boundary coverage)
[x] docs/ (developer documentation for the local flow; technical-doc
           reconciliation ONLY if the REST surface actually changes)
[ ] Database schema / migrations — NOT expected. If a schema change appears
    necessary, STOP: that is not this task.
```

---

## Implementation Notes

- The session issuer to reuse: `ApplicationSessionTokenService.Issue`.
  The single endpoint that establishes a session today is
  `AuthController.AuthenticateDiscord`; mirror its three-step shape
  (identity → Player → session) rather than inventing a parallel one.
- The Player path to reuse: `IPlayerRepository.GetOrCreateByDiscordUserIdAsync`,
  including the `composeStarterGrant` callback, so the development Player
  receives the same documented starter ownership as a real one.
- `ApplicationSessionSigningKeys.CreateHmacKey` currently enforces
  `MinimumSecretBytes = 32` and fails closed. Preserve both. A development
  convenience must not become a production weakening.
- `Program.cs` composes CORS for localhost automatically, and
  `vite.config.ts` already proxies `/api`, `/hubs`, and `/health` to
  `http://localhost:5000` — a normal browser tab is already a supported
  transport, so the blocker is authentication/iframe only. Do not add
  tunnel or HTTPS infrastructure.
- `App.tsx` bootstraps through a module-scoped `bootstrapPromise` and
  `GameRuntime` is a singleton. A development adapter must respect that
  ownership rather than adding a competing bootstrap.
- The frontend already stages a `ViewportDebugOverlay` behind
  `import.meta.env.DEV` — the repository's existing precedent for
  development-only frontend behaviour. Prefer that precedent over a new
  mechanism.
- If an unrelated gameplay ambiguity is discovered while working, **record
  it as a note/blocker only** — do not fix it here (`AGENTS.md` §16).
- Do not modify `tasks/completed/`, the six-Relic migration, or any
  canonical gameplay document.

---

## Testing Requirements

### Required Verification

```text
[x] Unit tests         — development-authentication activation logic; the
                         development resolver/endpoint's failure behaviour
                         when the environment is not Development
[x] Integration tests  — a development session is accepted by the REAL
                         authorization pipeline (produces the documented
                         401 when absent, and a normal authenticated
                         response when present); the development path is
                         NOT available outside Development
[ ] Gameplay scenarios — N/A: this task changes no gameplay behaviour.
                         `ApplicationSessionRESTTests` /
                         `ApplicationSessionSignalRTests` already cover the
                         session contract and must keep passing unchanged.
```

The existing suites are the reference for how to substitute external
boundaries without bypassing the real pipeline — `ApplicationSessionRESTTests`
documents that approach explicitly in its own header comment.

### Key Edge Cases

- The development path requested while `ASPNETCORE_ENVIRONMENT` is
  **Production**: must not produce a session and must not be reachable
- A development request for a Player that does not exist: a real Player row
  is created through the normal repository path, with starter ownership
- A development request that attempts to select an arbitrary existing
  `PlayerId`: must not yield that Player's session
- No configured signing secret: startup still fails closed with a
  value-free message naming the configuration key
- The development session is rejected by validation when tampered with,
  expired, or signed with a foreign key (inherited D5/D7/D11 behaviour,
  unchanged)
- A page refresh: defined local behaviour per AC-14, with no new token
  persistence
- `POST /api/auth/discord` still returns `503 DISCORD_UNAVAILABLE` (the
  TASK-035 boundary is untouched)

---

## Stop Conditions

- If satisfying AC-15 appears to require a secret in a tracked file, or any
  change to `ADR-015` D7–D11: **STOP** per `AGENTS.md` §4/§18 — that is an
  ADR change requiring a human decision, not an implementation choice
- If the work requires the real Discord OAuth exchange or the Discord
  `ClientSecret`: **STOP** — that is TASK-035 / TASK-036 scope
- If a database schema change or migration appears necessary: **STOP** —
  report it as an unplanned requirement
- If reaching the Lobby/Battle requires a **gameplay** change: **STOP** per
  `AGENTS.md` §7/§16 and report the exact missing rule
- If the development path cannot be bounded to Development without
  weakening production authorization: **STOP** and report rather than
  shipping the weaker variant
- If the TASK-180 report becomes available and contradicts the verified
  findings table: **STOP** and reconcile before planning
- If this task exceeds 7 skills or crosses multiple uncoupled
  architectural boundaries: STOP & decompose

---

## Completion Evidence

### Changed Files
- `<file path>` — <summary of change>

### Validation Results
- `<test command or suite>` — PASS (<N> tests)

### Local Playability Verification

Report the **actual** observed result, not the intended one. An unverified
claim of success is a failed criterion.

```text
[ ] Backend started from a clean local configuration (state how the
    signing secret was supplied — never the value)
[ ] Frontend started (`npm run dev`) and opened in a normal browser tab
[ ] Dev auth reached an authenticated session with no manual JWT injection
[ ] Player resolved/created
[ ] Collection loaded (Pets / Cards / Relics)
[ ] Lobby reached
[ ] Battle started
[ ] SignalR connected
[ ] At least one board action performed
[ ] Server state update observed (server-authoritative, not client-computed)
[ ] Battle reached a valid terminal or in-progress state
```

### Server Authority & Scope Verification
- [ ] Confirmed zero client-authoritative gameplay logic
- [ ] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
- [ ] Confirmed the development path is unavailable outside Development
- [ ] Confirmed no secret committed, logged, or returned (`ADR-015` D10)
- [ ] Confirmed no `ADR-015` D7–D11 value changed
- [ ] Confirmed no Discord OAuth exchange implemented (TASK-035 boundary)
- [ ] Confirmed no gameplay/schema/migration change

---

## Revision History

**Revision 1 — task created.** Generated from the TASK-180 MVP
playability audit's local-development authentication/iframe blocker.

Two findings shaped the specification rather than being copied from the
request that generated it:

1. The suggested `appsettings.Development.json` secret placement is
   **forbidden** by `ADR-015` D10, which names that exact file. The task
   therefore requires the ADR-sanctioned channel (uncommitted `.env` /
   `dotnet user-secrets`) and reframes AC-15 around making that channel
   discoverable, instead of committing a placeholder secret.
2. The TASK-180 audit report is **not present in the repository** and no
   commit references it. The report's findings were therefore verified
   directly against the code, and the task records that provenance
   explicitly so the implementing agent reconciles rather than trusts.
