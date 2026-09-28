# TASK-035 — Discord Identity Exchange Contract

---

## Metadata

```text
Task ID:           TASK-035
Type:              ARCHITECTURE
Status:            DONE
Risk:              HIGH
Priority:          CRITICAL
Primary Agent:     backend
Supporting Agents: review, testing, persistence
Workflow:          architecture/architecture-change.md, gated by
                   architecture/adr-change.md and
                   documentation/documentation-change.md
Skills:            discovery/documentation-discovery,
                   backend/api-contract-validation,
                   quality/documentation-consistency,
                   quality/architecture-conformance,
                   testing/test-scenario-generation
Dependencies:      None
```

---

## Resolution — Contract Defined (Revision 2)

This task is **DONE**. Every required decision is now recorded in authoritative
documents, and `TASK-023` has a concrete identity contract to depend on.

```text
Design decision recorded in   docs/03-decisions/ADR/ADR-013-discord-identity-exchange-contract.md
Wire contract recorded in     docs/02-technical/API_CONTRACTS.md §2 (§2.1–§2.7)
ADR registered in             docs/03-decisions/README.md §7 (index), version 1.3
```

### The decided contract

```text
POST /api/auth/discord            { "code": "<discord authorization code>" }
        ↓
POST https://discord.com/api/oauth2/token
     Content-Type: application/x-www-form-urlencoded
     Authorization: Basic base64(client_id:client_secret)
     grant_type=authorization_code & code=<code> & redirect_uri=<registered>
        ↓
     { access_token, token_type, expires_in, refresh_token, scope }
        ↓
GET https://discord.com/api/users/@me
     Authorization: Bearer <access_token>          (scope: identify)
        ↓
     Discord User object  ──►  DiscordUserId = user.id   (snowflake string)
        ↓
TASK-023 matches or creates the Player on DiscordUserId (DATABASE.md §1)
```

### Resulting Discord identity output

```text
DiscordUserId = the `id` member of the Discord User object returned by
                GET https://discord.com/api/users/@me
                — a snowflake, serialized by Discord as a string.
```

Three values are explicitly **not** the identity (API_CONTRACTS.md §2.4):

```text
DiscordUserId  ≠  the authorization code       (short-lived, single-use)
DiscordUserId  ≠  the Discord access token     (a credential, not an identity)
DiscordUserId  ≠  the application session      (ADR-007 item 4, TASK-034)
```

### Decisions resolved

| # | Required decision | Resolution | Source |
|---|---|---|---|
| 1 | OAuth token endpoint | `POST https://discord.com/api/oauth2/token` | `API_CONTRACTS.md` §2.2 |
| 2 | Exchange request | `application/x-www-form-urlencoded`, HTTP Basic credentials, `grant_type`/`code`/`redirect_uri` | §2.2 |
| 3 | Required parameters | `grant_type=authorization_code`, `code`, `redirect_uri`; credentials via Basic auth | §2.2 |
| 4 | Server-side client config | `Discord:ClientId` / `Discord:ClientSecret`, backend-only, never committed | §2.7, `ADR-013` item 11 |
| 5 | Identity endpoint | `GET https://discord.com/api/users/@me` with `Authorization: Bearer` | §2.3 |
| 6 | Identity response contract | The Discord User object; unknown extra fields ignored | §2.3 |
| 7 | `DiscordUserId` extraction | The `id` field (snowflake string) | §2.4, `ADR-013` item 6 |
| 8 | Invalid/expired code | `401 DISCORD_AUTH_FAILED` (invalid, expired, reused, revoked, redirect mismatch) | §2.6 |
| 9 | OAuth failure behavior | `503 DISCORD_UNAVAILABLE` (transport, 5xx, 429); `502 DISCORD_BAD_RESPONSE` (malformed, missing `id`) | §2.6 |
| 10 | Security constraints | §2.7 items 1–8; scope limited to `identify` | §2.7 |

### Provenance of the protocol values

The protocol facts are taken from the **official Discord documentation**, not
from memory or inference:

```text
docs.discord.com/developers/topics/oauth2      token URL, form-encoding
                                               constraint, HTTP Basic, grant
                                               type, parameters, token response
docs.discord.com/developers/resources/user     /users/@me, User object `id`,
                                               `identify` scope
docs.discord.com/developers/reference          base URL, versioning, Basic/Bearer
                                               header formats, snowflake-as-string
```

Repository design decisions (grant selection, `identify`-only scope, HTTP Basic
over body credentials, and the failure-code mapping) are distinguished from
those protocol facts in `ADR-013`.

### Remaining blockers

**None for this contract.** Two adjacent areas remain open by design and are
**not** blockers for this task:

```text
Application session mechanism   TASK-034 (BLOCKED) — ADR-007 item 4. This
                                contract deliberately does not define it;
                                POST /api/auth/discord continues to return the
                                opaque development session placeholder.
Credential storage/hygiene      TASK-036 (BLOCKED) — how the Client Secret is
                                stored locally. This contract only fixes how
                                the credential is supplied (config-bound) and
                                never exposed.
```

`TASK-023` is now unblocked on the identity contract and may key Player
match/create on a verified `DiscordUserId`.

---

## Original Blocker Record (Revision 1, superseded)

The following records the blocked state this task was created in. It is retained
for history; the contract above now resolves it.

This task is **BLOCKED**. The concrete contract that converts a Discord
authorization code into a verified `DiscordUserId` is **not defined by any
authoritative document**, and this task **must not choose it** (`AGENTS.md` §7,
§18, §20; `.ai/skills/backend/api-contract-validation.md` Stop Conditions).

This task exists because TASK-023 was blocked by a **false premise**. TASK-023
asserted:

```text
Discord identity (DiscordUserId) already available in current auth flow
```

**That statement is not true.** The current authentication flow receives an
authorization code and produces no identity at all. This task owns the missing
contract so that TASK-023 can key Player match/create on a real `DiscordUserId`.

### Verified current state (inspected, not assumed)

| Element | Actual state |
|---|---|
| Request payload | `DiscordAuthRequest(string Code)` — `src/backend/GameServer.Api/Controllers/AuthController.cs` |
| Identity produced | **None.** `request.Code` is validated for non-emptiness and then never used |
| Response | Hard-coded `PlayerId: "player_dev"` |
| Discord OAuth client | **Does not exist** anywhere in `src/` |
| Configuration read | **No code reads `Discord:ClientId` or `Discord:ClientSecret`** |
| Server-side exchange | **Not performed** |

The endpoint currently validates that a code is *present* and then ignores it.
No component in the repository has ever dereferenced an authorization code.

### What the authoritative documents do and do not say

The documents define the **architectural boundary** consistently and clearly:

```text
ADR-007 item 2        frontend passes the Discord authorization code;
                      "the ASP.NET Core backend securely contacts the Discord
                      OAuth API server-side to exchange the code for the
                      verified user identity and link/create the player
                      (DATABASE.md), issuing an authenticated application
                      session token"

API_CONTRACTS.md §2   the same flow diagram: authorization code →
                      server-side token exchange using Discord Client Secret →
                      Discord OAuth API → "(Discord user identity)" →
                      "Application Session (Player authenticated)"

TDD.md §2.1 item 2    the same exchange flow; "Returns Discord user identity"
```

Every one of these describes **that** an exchange happens and **why** it is
server-side. **None defines the protocol mechanics.** A repository-wide search
across `docs/` for the concrete contract returns no matches for any of:

```text
oauth2          /api/oauth        discord.com/api      grant_type
authorization_code                Bearer               client_secret
```

ADR-007 is explicit that its own scope is the **boundary**, not the mechanism —
it decides where the exchange happens and how the Client Secret is protected. It
does not decide what the exchange *is*.

---

## Objective

Once the required decisions below are recorded in an authoritative document,
define and implement the server-side Discord identity exchange so that
`POST /api/auth/discord` resolves a Discord authorization code to a **verified
`DiscordUserId`**:

```text
Discord authorization code
        ↓
Discord OAuth server-side exchange
        ↓
verified Discord identity
        ↓
DiscordUserId
```

This task owns **identity only**. It does not own Player persistence, and it
does not own the application session.

---

## Required decisions before this task may start

> **Resolved (Revision 2).** Every item below is now decided and recorded in
> `ADR-013` / `API_CONTRACTS.md` §2. See the **Resolution** section at the top
> of this file and the table of resolutions there. The checklist is retained
> below to show what this task originally had to resolve.

~~Every item below is currently **undefined**. None may be marked complete until
an authoritative document actually defines it.~~

```text
[x] OAuth token endpoint         — POST https://discord.com/api/oauth2/token
                                   (API_CONTRACTS.md §2.2)
[x] Authorization-code exchange  — POST, application/x-www-form-urlencoded,
    request                        HTTP Basic credentials (§2.2)
[x] Required parameters          — grant_type=authorization_code, code,
                                   redirect_uri (§2.2)
[x] Server-side client config    — Discord:ClientId / Discord:ClientSecret,
                                   backend-only, never committed (§2.7)
[x] Identity/user endpoint       — GET https://discord.com/api/users/@me (§2.3)
[x] Identity response contract   — the Discord User object; unknown extra
                                   fields ignored (§2.3)
[x] DiscordUserId extraction     — the User object's `id` field, a snowflake
                                   string (§2.4, ADR-013 item 6)
[x] Error handling               — 503 DISCORD_UNAVAILABLE / 502
                                   DISCORD_BAD_RESPONSE (§2.6)
[x] Invalid/expired code         — 401 DISCORD_AUTH_FAILED (§2.6)
[x] Security constraints         — §2.7 items 1–8; scope limited to identify
```

Original (superseded) wording of the same checklist:

```text
[ ] OAuth token endpoint         — the URL the authorization code is exchanged
                                   at. NOT DECIDED.
[ ] Authorization-code exchange  — the request the backend sends: method,
    request                        required parameters, and request encoding
                                   (form-encoded vs JSON). NOT DECIDED.
[ ] Required parameters          — the exact parameter set (e.g. client
                                   credentials, code, redirect URI, grant
                                   type). NOT DECIDED.
[ ] Server-side client config    — how ClientId/ClientSecret are bound and
                                   injected (already-present appsettings keys
                                   are convention only, not a contract).
                                   NOT DECIDED.
[ ] Identity/user endpoint       — the URL that returns the Discord user.
                                   NOT DECIDED.
[ ] Identity response contract   — the response shape and content type.
                                   NOT DECIDED.
[ ] DiscordUserId extraction     — WHICH response field is the Discord user id.
                                   This is the single most important missing
                                   decision: DATABASE.md §1 defines
                                   `DiscordUserId (unique)` but never states
                                   which Discord API field supplies it.
                                   NOT DECIDED.
[ ] Error handling               — transport failure, non-2xx upstream
                                   response, malformed/unexpected body.
                                   NOT DECIDED.
[ ] Invalid/expired code         — what the endpoint returns to the client and
                                   under which error code. API_CONTRACTS.md §6
                                   defines the error *envelope*
                                   (`{ "error": "...", "message": "..." }`)
                                   but names no code for this case. NOT DECIDED.
[ ] Security constraints         — anything beyond ADR-007 item 3's existing
                                   Client Secret boundary. NOT DECIDED.
```

---

## Relationship to ADR-007

**ADR-007 is insufficient, and it is not wrong.** It remains `Accepted`, its
boundary decisions stand, and it must **not** be superseded, rewritten, or
duplicated.

The gap was that ADR-007 decides the *boundary* and never the *protocol*. Per
`AGENTS.md` §18, a protocol contract with security consequences needs an
approved ADR before code.

**Resolved (Revision 2):** the protocol decision is recorded as **ADR-013 —
Discord Authorization-Code → Identity Exchange Contract**, registered in
`docs/03-decisions/README.md` §7.

### Why ADR-013 is `Accepted`, and not `Proposed`

`docs/03-decisions/README.md` §1/§5 and
`.ai/workflow/architecture/adr-change.md` §2 forbid recording a decision that
has not actually been made. Conversely, `adr-change.md` §2 directs the author to
"Confirm the decision is actually confirmed in existing docs/ **(or is being
confirmed right now as part of this task)**", and `README.md` §5 defines
`Accepted` as "confirmed and currently in effect".

This task **is** the act of making the decision, and it recorded that decision
in `API_CONTRACTS.md` §2 in the same change. ADR-013 therefore documents a
decision that is genuinely established and currently in effect — not a
speculative proposal. Its content was additionally verified against the official
Discord documentation rather than inferred, and no protocol value was invented.

### Verification of the decision's basis

The earlier revision of this task could not create the ADR because the protocol
values were unknown. They are now verified from primary sources
(`docs.discord.com/developers/topics/oauth2`, `/resources/user`, `/reference`),
so the ADR records established facts plus explicit repository choices, with the
two clearly distinguished.

Original (superseded) reasoning recorded while the task was BLOCKED:

```text
docs/03-decisions/README.md §1   "ADRs are historical records, not design
                                  proposals. Writing an ADR does not create or
                                  change a decision."
docs/03-decisions/README.md §5   "Accepted is only used when the decision is
                                  genuinely established ... never for a decision
                                  that is merely implied or still under
                                  discussion."
.ai/workflow/architecture/adr-change.md §2
                                 "do not create a Proposed ADR for a decision
                                  nobody has actually made yet"
```

Writing an ADR-013 now would mean inventing the very contract this task is
blocked on. Creating the ADR is the **first step after** a human supplies the
decision.

### Expected shape once unblocked

```text
Step 1  Record the decision as ADR-013 (Status: Accepted once approved),
        or a narrowly scoped amendment recorded per README §8
Step 2  Register it in docs/03-decisions/README.md §7's index
Step 3  Record the wire contract in API_CONTRACTS.md §2 — the exchange
        request, the identity response, and the failure response
Step 4  Only then implement, and only then unblock TASK-023
```

Note the ordering constraint: the contract is owned by `docs/02-technical/`
(`API_CONTRACTS.md`), and the ADR explains *why*. An ADR alone does not satisfy
this task — the technical document owns the *what* (`AGENTS.md` §2).

---

## Authoritative References

- `docs/03-decisions/ADR/ADR-007-discord-activity-authentication.md` — items 2–4: the authentication boundary, the server-side exchange requirement, and the Client Secret security boundary (all three stand; none defines the protocol)
- `docs/02-technical/API_CONTRACTS.md` §1, §2, §6 — the auth endpoint boundary, its flow diagram, and the error envelope
- `docs/02-technical/ARCHITECTURE.md` §2.3 — Discord SDK and authentication boundaries
- `docs/02-technical/TDD.md` §2.1 items 2, 4 — the exchange flow and SignalR authentication sequencing
- `docs/02-technical/DATABASE.md` §1 — `Player.DiscordUserId (unique)`, the value this task must produce
- `docs/03-decisions/README.md` §1, §2, §5, §7, §8 — ADR conventions, status values, index, and open-item registry
- `docs/03-decisions/ADR/ADR-002-modular-monolith.md` — single ASP.NET Core service boundary
- `docs/00-overview/MVP_SCOPE.md` §1 — "Player account (Discord identity, collection owner)" listed IN

---

## Scope

### In Scope (once unblocked)

- The authoritative decision for the Discord authorization-code → identity exchange
- The server-side exchange client and its configuration binding
- Extraction of a verified `DiscordUserId` from the identity response
- The failure contract for invalid, expired, or rejected authorization codes
- Unit/integration tests for the exchange boundary and its failure paths

### Out of Scope

- **Player entity, Player match/create, `Player.Level`** — owned by TASK-023
- **The application session mechanism** — owned by TASK-034: session type, token/session format, claims, expiration, validation strategy, ASP.NET Core authentication scheme, and authorization behavior
- **Player XP, Reward → XP, XP curve, level-up threshold** — owned by TASK-033
- Pet persistence (TASK-024), Cards (TASK-028), Relics (TASK-027)
- Any Discord Embedded App SDK change on the frontend — the client already obtains and sends a code (`DiscordService.getAuthorizationCode`, `ApiService.authenticateDiscord`)
- Any gameplay rule or gameplay value of any kind
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2

---

## Current State

`src/backend/GameServer.Api/Controllers/AuthController.cs` — `AuthenticateDiscord`
validates `request.Code` for non-emptiness, returns
`SessionToken: $"session_{Guid.NewGuid():N}"` and `PlayerId: "player_dev"`, and
never dereferences the code. No Discord OAuth client, no HTTP client
registration for Discord, and no consumer of the `Discord:*` configuration keys
exists anywhere in `src/`.

---

## Acceptance Criteria

**This task's deliverable is the contract (design/documentation).** The
implementation criteria below are the *downstream* work the contract unblocks;
they are listed so the implementing task inherits them, and they are not
satisfied by this task.

### Contract deliverable (this task)

- [x] The exchange contract is defined by authoritative documents — `ADR-013` and `API_CONTRACTS.md` §2 (§2.1–§2.7) — not by implementation convention
- [x] The token endpoint, exchange request, encoding, credentials, and parameters are specified (`API_CONTRACTS.md` §2.2)
- [x] The identity endpoint, authorization header, required scope, and response contract are specified (§2.3)
- [x] `DiscordUserId` extraction is specified as the User object's `id` field, and explicitly distinguished from the authorization code, the access token, and the application session (§2.4)
- [x] The failure contract covers invalid/expired/reused/revoked codes, redirect mismatch, transport failure, upstream 5xx/429, malformed bodies, and missing `id` (§2.6)
- [x] Security requirements are specified, including server-side handling, no secret exposure, no credential logging, and identity validation before any Player write (§2.7)
- [x] Every protocol value is verified against official Discord documentation; no endpoint, parameter, field name, grant type, or scope was invented
- [x] `ADR-013` is created and registered in `docs/03-decisions/README.md` §7
- [x] No session mechanism, token format, claim, or expiry is introduced (`ADR-007` item 4 / TASK-034 boundary)
- [x] No Player row is created or matched, and no schema, migration, or source file is changed (TASK-023 boundary)

### Downstream implementation criteria (owned by the implementing task)

- [ ] The backend exchanges the authorization code server-side per §2.2 and obtains a verified Discord identity
- [ ] A verified `DiscordUserId` is extracted per §2.4
- [ ] The Client Secret never leaves the backend and never reaches the frontend (`ADR-007` item 3)
- [ ] Invalid, expired, or rejected authorization codes return the §2.6 error, using the `API_CONTRACTS.md` §6 envelope
- [ ] `POST /api/auth/discord` continues to satisfy §2.5's response shape
- [ ] All relevant tests pass at the required validation depth (`core/validation.md` §2)
- [ ] Quality review checklist passes (`quality/review.md` §1)
- [ ] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001)

---

## Affected Files & Areas

```text
[ ] src/backend/ (Api — Controllers; Infrastructure — Discord OAuth client)
    Not changed by this task: the contract is defined first, implementation
    follows in the downstream task.
[ ] src/frontend/client/ (no change expected — the client already sends a code)
[ ] tests/ (integration tests for the exchange and its failure paths)
[x] docs/ (ADR-013 created; API_CONTRACTS.md §2 rewritten;
           docs/03-decisions/README.md §7 index updated) — DONE
```

---

## Implementation Notes

- Start point: `AuthController.AuthenticateDiscord`
  (`src/backend/GameServer.Api/Controllers/AuthController.cs`). The code is
  received today and discarded; that is the gap.
- `DiscordAuthResponse(string SessionToken, string PlayerId)` is the current wire
  record. `API_CONTRACTS.md` §2 does not name its members, so its shape is
  implementation convention — preserve it unless the recorded decision changes it.
- Configuration keys `Discord:ClientId` / `Discord:ClientSecret` already exist in
  `appsettings.json` (tracked, `ClientSecret` empty) and
  `appsettings.Development.json` (gitignored). Their binding is now fixed by
  `API_CONTRACTS.md` §2.7 item 2 / `ADR-013` item 11: backend-only, bound from
  those keys, never hard-coded and never committed. Local secret *storage* is a
  separate concern (TASK-036).
- `ARCHITECTURE.md` §2.1's layer direction applies: the exchange is an
  Api/Infrastructure concern and must not leak into Domain. `ADR-013` item 13
  sanctions the Discord exchange client as a new Infrastructure module, sibling
  to the existing `Postgres/`, `Redis/`, and `SignalR/` modules.
- `ADR-007` item 1's frontend SDK boundary stands: the Discord SDK never runs on
  the backend, and `DiscordService.ts` is untouched by this task.
- See also **TASK-036 — Discord Credential Secret Hygiene**, which covers the
  credential-handling side of this boundary.
- Do not modify `tasks/completed/`.

---

## Testing Requirements

### Required Verification

**This task adds no tests.** Its deliverable is the contract; the tests below
belong to the downstream implementation task that builds against it.

```text
[ ] Unit tests         — exchange request construction, identity extraction,
                         and failure mapping, against the recorded contract
[ ] Integration tests  — valid code resolves an identity; invalid/expired code
                         returns the documented error; Client Secret never
                         appears in any response body
[ ] Gameplay scenarios — N/A: this task introduces no gameplay behavior
```

### Key Edge Cases

- An expired or already-consumed authorization code is rejected with the documented error
- A rejected/revoked code is rejected with the documented error
- An upstream non-2xx response does not leak upstream detail to the client
- A malformed or unexpected identity response fails safely rather than producing an empty `DiscordUserId`
- The same Discord identity presented twice resolves to the same `DiscordUserId` (the stability TASK-023's match/create depends on)
- The Client Secret is never serialized into a response, log line, or error message (`ADR-007` item 3)

---

## Stop Conditions

- ~~While BLOCKED: any attempt to implement is itself the violation.~~ Resolved — the contract is now recorded, so implementation is permitted by the downstream task.
- If any required decision above is still undefined when work resumes: STOP per `AGENTS.md` §7 — **not applicable; all decisions are resolved**
- If implementation requires inventing the identity response contract or the `DiscordUserId` source field: STOP per `AGENTS.md` §7 — the contract is now `API_CONTRACTS.md` §2.3/§2.4
- If the resolution requires changing an existing ADR decision rather than adding one: STOP per `AGENTS.md` §4 — `ADR-007` was not changed; `ADR-013` was added alongside it
- If the work expands into Player persistence (TASK-023) or the session mechanism (TASK-034): STOP & decompose
- If task exceeds 7 skills or crosses multiple uncoupled architectural boundaries: STOP & decompose

---

## Completion Evidence

### Changed Files
- `docs/03-decisions/ADR/ADR-013-discord-identity-exchange-contract.md` — **created**; the design decision (grant, endpoints, request/response, `DiscordUserId` source, failure and security rules, with protocol facts separated from repository choices)
- `docs/02-technical/API_CONTRACTS.md` — **modified**; §2 rewritten into the complete wire contract (§2.1 endpoint, §2.2 exchange, §2.3 identity, §2.4 `DiscordUserId`, §2.5 response, §2.6 failure, §2.7 security); §1 summary row and version header updated to 1.2
- `docs/03-decisions/README.md` — **modified**; ADR-013 registered in the §7 index, version header → 1.3
- `tasks/backlog/TASK-035-discord-identity-exchange-contract.md` — **modified**; status → DONE with resolution and completion evidence

### Validation Results
- No test suite was run: this is a design/documentation task and **no source, test, migration, or configuration file was modified** (verified — `git status` shows `src/` and `tests/` clean).
- Contract self-verification: all ten required decisions resolved (see the resolution table above).
- Protocol verification: every Discord value traced to official documentation (see Provenance above).
- ADR numbering verified: highest pre-existing ADR is ADR-012, so ADR-013 is the next sequential number and is not reused.

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic (no gameplay touched)
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1 — "Player account (Discord identity, collection owner)")
- [x] Confirmed no Player persistence implemented (TASK-023 boundary)
- [x] Confirmed no session mechanism implemented (TASK-034 boundary)

---

## Revision History

**Revision 1 — task created (BLOCKED).** TASK-035 was created by the TASK-023
decomposition revision, after TASK-023 was found to rest on a false premise:
that `DiscordUserId` is "already available in current auth flow." Inspection
confirmed the endpoint receives an authorization code, never dereferences it,
and returns a hard-coded `player_dev`. `ADR-007` item 2, `API_CONTRACTS.md` §2,
and `TDD.md` §2.1 all define the exchange **boundary** but decide no protocol
mechanics — no token endpoint, grant type, request encoding, identity endpoint,
response shape, or identity field name exists in any document. The task is
therefore BLOCKED pending an explicit design decision. No endpoint, parameter,
field name, token format, or session mechanism was invented, and no ADR was
created, because the repository's ADR convention forbids recording a decision
that has not been made.

**Revision 2 — contract defined; task DONE.** The Discord OAuth2 protocol was
verified against the official Discord documentation
(`docs.discord.com/developers/topics/oauth2`, `/resources/user`, `/reference`),
and the resulting contract was recorded:

```text
ADR-013                the design decision (how the exchange is performed and
                       why that grant/scope/credential mechanism was chosen)
API_CONTRACTS.md §2    the wire contract (endpoint, requests, responses,
                       DiscordUserId extraction, failure codes, security rules)
README.md §7           ADR-013 registered in the index
```

Decided: OAuth2 authorization-code grant; `POST /api/oauth2/token` with
`application/x-www-form-urlencoded` and HTTP Basic credentials; the `identify`
scope; `GET /users/@me` with a Bearer token; `DiscordUserId` = the User object's
`id` (a snowflake string); and an explicit failure contract distinguishing a
rejected code (`401 DISCORD_AUTH_FAILED`) from a transient upstream failure
(`503 DISCORD_UNAVAILABLE`) and an unusable response
(`502 DISCORD_BAD_RESPONSE`).

Scope discipline observed: no OAuth code, controller, middleware, session
handling, database schema, migration, test, or frontend file was modified. The
application session mechanism remains undecided (`ADR-007` item 4 / TASK-034),
and `POST /api/auth/discord` continues to return the opaque development session
placeholder until that decision exists. No protocol value was invented; every
one is cited to official Discord documentation.
