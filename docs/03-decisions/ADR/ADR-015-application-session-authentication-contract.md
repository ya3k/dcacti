# ADR-015: Application Session Authentication Contract

**Status:** Accepted
**Date:** 2026-09-26

## Context

`ADR-007` item 4 requires every REST endpoint and every `BattleHub`
connection to authenticate using the application session token, and
`ADR-007` item 2 has the backend issue that token after the Discord
identity exchange — but `ADR-007` decides no format, claims, lifetime,
storage, propagation, or enforcement. `API_CONTRACTS.md` recorded the gap
outright (§2.5: the mechanism "is not defined by this contract ... owned by
`TASK-034`"), and no document in `docs/00-overview/`, `docs/01-game-design/`,
or `docs/02-technical/` defines one. `docs/03-decisions/README.md` §7 had no
ADR for it either.

The code matched the gap rather than closing it: the issued token was an
unvalidated `session_{guid}` placeholder, no authentication scheme was
registered in the pipeline, the `GameServer.PlayerId` request-context key
that `BattleController` reads has no writer (so every
`POST /api/battle/start` currently fails `401 UNAUTHENTICATED`), and the
client holds a `sessionToken` it never transmits. Implementing any of this
without a recorded contract would have required inventing a mechanism,
format, claim names, lifetime, storage location, and scheme — a stop
condition under `AGENTS.md` §7 and §20 (ambiguous requirement: several
materially different implementations were equally consistent with the
existing documents).

The decision was therefore taken explicitly by the human (TASK-054 §13.1,
decisions D1–D6) and is recorded here (why) and in `API_CONTRACTS.md` §2.8
(the what). The five security-configuration items this ADR initially left
open were supplied by the human in a second decision round (TASK-055,
2026-09-27) and are recorded below as D7–D11. This ADR does not supersede
`ADR-007` or `ADR-013`; it fills the one item those ADRs deliberately left
open.

## Decision

1. **D1 — the session is a self-contained signed JWT.** The backend
   issues it after the `POST /api/auth/discord` Discord identity exchange
   succeeds. The Discord access token is **not** the application session
   and is never issued to the client (`API_CONTRACTS.md` §2.7 item 4).
2. **D2 — authentication is stateless.** There is no server-side session
   record. Application sessions are not stored in Redis, PostgreSQL, or
   in-memory state. `ADR-005` (Redis = active battle state only) and
   `ADR-006` (PostgreSQL = persistent data only) are **not** extended for
   sessions; both boundaries remain exactly as they are.
3. **D3 — the JWT carries the PlayerId claim.**

   ```text
   claim: player_id
   value: PlayerId
   ```

   The authenticated request identity resolves `JWT player_id → PlayerId`,
   and the server treats that identity as authoritative. The full chain
   stays `verified DiscordUserId → Player match/create → PlayerId → JWT
   player_id → authenticated request → PlayerId`. The client never supplies
   or overrides `PlayerId` or `DiscordUserId` for authentication or
   ownership decisions. `GameServer.PlayerId` may remain the internal
   request-context representation of that identity (server-internal, never
   client input). `DiscordUserId` is not carried as an authoritative
   ownership claim — the exchange already mapped it to `PlayerId`, which is
   sufficient.
4. **D4 — propagation is Bearer over REST and SignalR's standard
   access-token mechanism.** REST requests carry
   `Authorization: Bearer <sessionToken>`; no application-session cookie is
   used. `BattleHub` receives the same JWT through SignalR's standard
   access-token mechanism, and a Discord access token is never accepted as
   a `BattleHub` authentication credential. Transmitting the token from the
   frontend is implementation work owned by TASK-034
   (`API_CONTRACTS.md` §2.8, `SIGNALR_PROTOCOL.md` §1).
5. **D5 — MVP lifecycle: 24 hours absolute expiry, nothing else.** No idle
   timeout, no renewal, no refresh token, no logout/revocation in MVP. A
   new successful Discord authentication exchange may issue a new JWT; it
   does not require server-side revocation state. For REST, a **missing,
   invalid/tampered, or expired** session all resolve to:

   ```http
   401
   ```
   ```json
   { "error": "UNAUTHENTICATED" }
   ```

   The three conditions share this one public response; no distinct error
   code distinguishes them, and no token-validation detail is disclosed.
   Ownership responses are unchanged: authenticated owner → `200`,
   foreign or missing battle → `404 BATTLE_NOT_FOUND`.
6. **D6 — enforcement is the ASP.NET Core authentication/authorization
   pipeline with a JWT Bearer authentication scheme.** Exactly **one**
   application authentication mechanism exists. All REST endpoints except
   `POST /api/auth/discord` require an authenticated session
   (`API_CONTRACTS.md` §1); `/api/auth/discord` remains the unauthenticated
   exchange endpoint, enforced with ASP.NET Core authorization at the API
   boundary. `BattleHub` requires the same authenticated JWT session; a
   connection without a valid application session is rejected by the
   authentication/authorization boundary. `BattleHub` grows no second,
   custom authentication system — it remains transport-focused.

### Session security configuration (D7–D11) — added 2026-09-27

The five items this ADR initially left open were supplied by the human in a
second decision round (TASK-055) and are recorded here as D7–D11. They are
server-side security configuration for issuing and validating the D1 token;
they change no claim shape, endpoint, or message, so `API_CONTRACTS.md` §2.8
and `SIGNALR_PROTOCOL.md` §1 are unaffected. Their scope is the **JWT
signing key only** — they do not decide Discord credential hygiene, which
remains `TASK-036` (BLOCKED).

**Decision**

7. **D7 — the signing algorithm is HS256.** The session JWT is signed and
   validated with HS256 (HMAC-SHA-256) using one symmetric signing secret.
   Validation must reject any other algorithm, including `alg=none` and any
   asymmetric `alg` header.
8. **D8 — no issuer claim.** The token carries no `iss` claim, and issuer
   validation is disabled. No canonical issuer value exists to check
   against: the project owns no domain name, and the same process both
   issues and validates (`ADR-002` modular monolith).
9. **D9 — audience is `dcacti-backend`.** The token carries

   ```text
   claim: aud
   value: dcacti-backend
   ```

   and audience validation is required with an exact match. One value
   covers both consumers — REST and the `BattleHub` access token are the
   same token under one scheme (D4/D6). The value was chosen by the human.
10. **D10 — the signing secret comes from configuration, never from
    tracked files.** In production it is read from a host environment
    variable; in local development from an uncommitted `.env` file or a
    `dotnet user-secrets` store. It must never appear in `appsettings.json`,
    `appsettings.Development.json`, `.env.example`, source code, a test
    fixture, a log line, or any committed artifact — the same boundary
    `API_CONTRACTS.md` §2.7 and `src/backend/.env.example` already state
    for secrets. The exact configuration key name is TASK-034's
    implementation detail and is not fixed here.
11. **D11 — rotation is manual with overlap.** Every token carries a `kid`
    header, and validation accepts exactly two keys: the current signing
    key and the previous one. Rotation is an operator-driven configuration
    change; tokens issued under the old key remain valid until their
    24-hour absolute expiry (D5), after which the previous key is removed
    from the validation set. No JWKS, no KMS, no automated rotation in
    MVP.

**Rationale**

- HS256 keeps one secret in one service: the monolith both issues and
  validates, so asymmetric key distribution (RS256/ES256) would add
  key-pair generation and storage with no second party that needs the
  public key.
- Omitting `iss` avoids recording a claim nobody can meaningfully check
  today; an explicit but unverifiable value would be decorative.
- A single explicit `aud` keeps audience validation real (a token minted
  under this session contract is rejected elsewhere) at the cost of one
  fixed string, while matching the one-token/one-scheme contract (D4/D6).
- Configuration-supplied storage extends the repository's existing secret
  convention instead of introducing a new mechanism
  (`ARCHITECTURE.md` §5 anti-overengineering).
- Overlap rotation bounds the damage of a key change: with no revocation
  in MVP (D5), a no-overlap rotation would instantly kill every live
  session, while overlap caps the old key's usefulness at the remaining
  lifetime of tokens already issued.

**Consequences**

- TASK-034 can wire JWT Bearer issuance and validation with every security
  value recorded; no default may be substituted silently at implementation.
- HS256 means anyone who obtains the single secret can forge sessions
  until the key is rotated; under D11's overlap, a compromised *old* key
  also stays accepted until it is dropped from the validation set.
- Rotation remains the only early-invalidation tool (D5 has no
  revocation), and it is manual: a key change forces re-authentication of
  every session not yet expired.
- Changing `dcacti-backend` later has the same effect as a key change —
  all outstanding tokens fail audience validation.

**Implementation constraints (TASK-034)**

- Validate: algorithm exactly HS256; `aud` exactly `dcacti-backend`; `iss`
  not validated; `kid` resolves only to the current or the previous key.
- Obtain the secret from configuration (D10); never hard-code, commit,
  log, or return it. If it is absent, do not silently substitute a
  generated or default key — STOP per `AGENTS.md` §7 rather than invent
  one.
- Any proposed change to D7–D11 is a change to this ADR: STOP per
  `AGENTS.md` §4 and record a human decision before implementing.

## Alternatives Considered

### Option A — Opaque server-side session id (with session storage)

A random server-held token validated by lookup, with session state kept in
Redis, in memory, or PostgreSQL. Rejected: D2 fixes authentication as
stateless — a session store would deliberately extend `ADR-005`/`ADR-006`
(or keep state in process memory, which `REDIS_STATE.md` §7 item 5
forbids), and would introduce revocation-shaped infrastructure the chosen
MVP lifecycle (D5: no refresh, no revocation) never uses.

### Option B — HTTP-only application-session cookie

Propagate the session as a cookie (CORS already sets `AllowCredentials()`).
Rejected: D4 fixes the standard `Authorization: Bearer` header for REST and
an application-session cookie is explicitly excluded; the same artifact must
also reach SignalR's access-token mechanism, which carries a token rather
than a cookie.

### Option C — Discord access token as the session

Passthrough of the OAuth access token as the session credential. Not a new
option — already excluded by `API_CONTRACTS.md` §2.7 item 4 and `ADR-013`
item 10: the access token is a credential used only for the §2.3 identity
request within the exchange and is never issued to the client.

## Why

- One artifact and one scheme satisfy `ADR-007`'s requirement of a
  consistent application session shared by REST and SignalR.
- Stateless validation needs no session table, session key, session TTL, or
  revocation store — nothing is added to Redis or PostgreSQL, so both
  storage boundaries remain exactly as `ADR-005`/`ADR-006` define them.
- A 24-hour absolute lifetime with no refresh and no revocation (D5) is
  fully enforceable by a self-validating token; the MVP consciously trades
  revocation capability for zero session infrastructure
  (`ARCHITECTURE.md` §5 anti-overengineering, `MVP_SCOPE.md` §1).
- The `player_id` claim holds the identity the exchange already verified
  and mapped (`ADR-013`), so every ownership check reads the same
  server-derived value — never a client-supplied one
  (`GAME_RULES.md` §18, ADR-001).

## Consequences

### Positive

- TASK-034 implements against a recorded contract instead of inventing one:
  artifact, claim, propagation, lifetime, failure responses, coverage, and
  enforcement placement are all fixed (TASK-054 §8).
- `401 UNAUTHENTICATED` remains the single unauthenticated outcome for
  every covered endpoint (TASK-051 B2), and owner-only
  `404 BATTLE_NOT_FOUND` remains unchanged (TASK-051 B3).
- No storage boundary is touched: `REDIS_STATE.md` and `DATABASE.md`
  required no change for this decision.
- One failure code for missing/invalid/expired sessions gives attackers no
  validation oracle and keeps the `API_CONTRACTS.md` §6 envelope unchanged.

### Negative

- No server-side revocation exists in MVP: a token stays valid until its
  24-hour absolute expiry even after the player authenticates again — a new
  exchange issues a new JWT without invalidating the old one. Accepted per
  D5, because revocation would require exactly the server-side session
  state D2 excludes.
- Early invalidation (logout, ban, compromised signing key) is therefore
  unavailable without a future decision.

### Trade-offs

- Algorithm, issuer, audience, key storage, and rotation were initially
  bounded and explicit rather than invented; they are now decided and
  recorded as D7–D11 (2026-09-27, TASK-055), so no consumer of this
  contract may substitute a default for them.

## Related Documents

- `docs/02-technical/API_CONTRACTS.md` §1 (coverage), §2.4 (identity),
  §2.5 (issuance), §2.7 (security requirements), **§2.8 (owning WHAT
  contract)**, §4 notes 6–7 (401 / owner-only read), §6 (error envelope)
- `docs/02-technical/SIGNALR_PROTOCOL.md` §1 (connection authentication)
- `docs/02-technical/ARCHITECTURE.md` §2.3 (session issuance, hub connect)
- `docs/02-technical/TDD.md` §2.1 (session issued after exchange; hub
  connects only once the session is authenticated)
- `ADR-007` — unchanged; item 4's application session is the one defined
  here
- `ADR-013` — unchanged; the exchange contract that produces the
  `PlayerId` this session carries
- `ADR-005`, `ADR-006` — unchanged storage boundaries (no session storage)
- `ADR-014` — unchanged; battle-end owner identity still comes from
  `BattleState.PlayerId`, never from a session
- TASK-054 (decision task — D1–D6 answered and recorded), TASK-055
  (decision task — D7–D11 answered and recorded), TASK-034
  (implementation consumer — its references reconciled to D7–D11),
  TASK-041 (untouched)
