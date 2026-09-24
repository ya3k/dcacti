# ADR-013: Discord Authorization-Code → Identity Exchange Contract

**Status:** Accepted
**Date:** 2026-09-24

## Context

`ADR-007` item 2 established the *architectural boundary* for Discord
authentication: the frontend obtains an authorization code, and "the ASP.NET
Core backend securely contacts the Discord OAuth API server-side to exchange the
code for the verified user identity and link/create the player (`DATABASE.md`),
issuing an authenticated application session token."

That decision fixes **where** the exchange happens and **why** it is
server-side. It does not fix **what the exchange is**. No document defines:

1. the token endpoint the code is exchanged at,
2. the exchange request (method, encoding, parameters),
3. how the application's Discord credentials are supplied,
4. the identity endpoint,
5. the identity response contract,
6. which response field becomes `DiscordUserId`,
7. the failure behavior for invalid/expired codes or upstream errors.

The gap is not theoretical. `POST /api/auth/discord`
(`src/backend/GameServer.Api/Controllers/AuthController.cs`) accepts
`DiscordAuthRequest(string Code)`, validates only that the code is non-empty,
**never dereferences it**, and returns a hard-coded
`DiscordAuthResponse(SessionToken: "session_{guid}", PlayerId: "player_dev")`.
No Discord OAuth client exists in `src/`, and no code reads the
`Discord:ClientId` / `Discord:ClientSecret` configuration keys.

This blocked `TASK-023` (Player persistence), which must match-or-create a
`Player` row keyed on the unique `DiscordUserId` column of `DATABASE.md` §1 —
a value the current flow never produces. An authorization code cannot serve as
that key: it is a short-lived, single-use credential, not a stable per-user
identifier.

Resolving this requires a protocol contract with security consequences. Per
`AGENTS.md` §18, such a decision is recorded as an ADR before code lands. This
ADR records the decision; `API_CONTRACTS.md` §2 owns the wire contract
(`AGENTS.md` §2 — technical documents own the *what*, ADRs explain the *why*).

## Decision

Adopt the **OAuth2 authorization-code grant** against the documented Discord
token endpoint, followed by an authenticated user-identity request, producing a
stable `DiscordUserId`.

The protocol facts below are taken from the official Discord documentation
(`docs.discord.com/developers/topics/oauth2`, `/resources/user`,
`/reference`); they are external protocol facts, not repository choices.

1. **Grant type.** The authorization-code grant.
   Authorization requests use `response_type=code`. The exchange request uses
   `grant_type=authorization_code`.
   *(Rejected: implicit grant — returns the access token in a URI fragment and
   issues no refresh token; client-credentials grant — issues a token for the
   application, not a user, so it cannot identify a player.)*

2. **Token endpoint.** `POST https://discord.com/api/oauth2/token`.
   The token and revocation URLs accept **only**
   `application/x-www-form-urlencoded`; JSON is not permitted and returns an
   error.

3. **Exchange request.** Form-encoded body carrying `grant_type`,
   `code`, and `redirect_uri`. Application credentials are supplied with
   **HTTP Basic authentication** (`client_id:client_secret`, Base64-encoded) in
   the `Authorization` header — the mechanism Discord documents for its OAuth2
   token endpoints, and the one that keeps the secret out of the request body.
   *(Discord also permits `client_id` + `client_secret` in the form body; HTTP
   Basic is chosen so the secret never appears in a body that could be logged or
   captured as form data.)*

4. **Scope.** `identify`. Discord documents `identify` as the scope that allows
   `GET /users/@me` without `email`. This is the minimum scope needed to obtain
   the user's `id`, and no broader scope is requested.
   *(Rejected: `email` — not needed for identity, and it would request more
   personal data than the task requires. `guilds` — the identity flow does not
   consume it.)*

5. **Identity endpoint.** `GET https://discord.com/api/users/@me`, authorized
   with `Authorization: Bearer <access_token>` using the `access_token` and
   `token_type` returned by step 3. Returns the Discord **User object**.

6. **`DiscordUserId` source field.** The User object's **`id`** field — a
   snowflake, documented as "the user's id" and required to be serialized as a
   **string** (Discord returns IDs as strings to avoid integer overflow). This
   value is what populates `Player.DiscordUserId` (`DATABASE.md` §1) and is the
   stable, per-user key TASK-023 matches on.

7. **Determinism of the identity output.** For one Discord account, the
   identity endpoint returns the same `id` on every successful exchange.
   Therefore the contract's output is stable and repeatable: repeated
   authentication for the same Discord account yields the same `DiscordUserId`.
   This is the property TASK-023's idempotent match-or-create depends on.

8. **Server-side only.** Both requests are made by the backend. The
   authorization code, the Client Secret, and the resulting access token never
   reach the frontend (`ADR-007` items 2–3).

9. **Failure is a contract, not an implementation detail.** Invalid, expired,
   reused, or revoked codes; upstream transport failures; non-2xx upstream
   responses; malformed or unexpected bodies; and a missing/empty `id` are
   defined as contract-level failure cases in `API_CONTRACTS.md` §2. The client
   receives the `API_CONTRACTS.md` §6 error envelope and never receives upstream
   error detail.

10. **The access token is not an identity and is not the application session.**
    The Discord access token is used solely to call the identity endpoint during
    the request. It does not become `PlayerId`, and it does not become the
    application session issued to the client — that remains `ADR-007` item 4's
    separate, still-undecided session mechanism.

11. **Credentials are configuration, never literals.** Client ID and Client
    Secret are supplied through server-side configuration bound from the
    existing `Discord:ClientId` / `Discord:ClientSecret` keys, never hard-coded
    and never committed (`ADR-007` item 3).

12. **A verified identity is required before any Player write.** The backend
    validates that the identity response carries a non-empty `id` before any
    Player is created or matched. A response that cannot be validated produces
    the documented failure and no Player row.

13. **HTTP client placement.** The Discord exchange client is an Infrastructure
    concern (a new sibling module under `GameServer.Infrastructure`, per the
    existing `Postgres/`, `Redis/`, `SignalR/` convention) and must not leak into
    Domain. `AuthController` stays a thin boundary that delegates to the
    Application/Infrastructure layer (`ARCHITECTURE.md` §2.1).

## Alternatives Considered

### Option A — Implicit grant
Rejected: Discord's implicit grant returns the access token in a URI fragment
rather than a secure server-side HTTP body, and issues no refresh token. It is
also inherently client-side, contradicting `ADR-007` item 2's server-side
exchange boundary and re-exposing credentials the current design keeps on the
backend.

### Option B — Client-credentials grant
Rejected: it issues an access token for the **application**, not an
authenticated user. It has no user identity to return, so it cannot produce a
`DiscordUserId` at all.

### Option C — Treat the authorization code as the player identity
Rejected: an authorization code is short-lived and single-use. It is not stable
per user, so it cannot key `Player.DiscordUserId (unique)` (`DATABASE.md` §1),
and persisting it would store an authentication credential as a durable
identifier — the exact conflation this ADR exists to prevent.

### Option D — Let the frontend perform the exchange
Rejected by `ADR-007` Option B: it requires exposing the `Discord Client
Secret` to the client bundle.

### Option E — Decode an identity from the Discord ID token instead of calling the User endpoint
Rejected for this contract: the authorization-code grant's documented token
response returns `access_token`, `token_type`, `expires_in`, `refresh_token`,
and `scope` — no user identity. Obtaining the user requires the separate
authenticated `GET /users/@me` call this ADR specifies. (`/oauth2/@me` returns
the authorization and, with `identify`, a `user` object, but that is an
authorization-introspection endpoint rather than the canonical user resource,
and `GET /users/@me` is what Discord documents as returning the user object of
the requester's account.)

### Option F — Request the `email` scope for additional identity material
Rejected: identity and Player ownership need only the user `id`. Requesting
`email` would collect more personal data than the contract requires, and
`ADR-007` does not authorize storing it.

## Why

- **A stable key is required.** `Player.DiscordUserId` is unique and durable
  (`DATABASE.md` §1); only the User object's `id` satisfies both properties.
- **The boundary already exists.** `ADR-007` items 2–3 decide that the exchange
  is server-side and that the Client Secret stays on the backend. This ADR
  supplies the missing protocol mechanics without changing that boundary.
- **The minimum scope principle.** `identify` returns the user object without
  `email`, which is exactly the data this contract needs and no more.
- **HTTP Basic is the documented OAuth2 mechanism** for Discord's token
  endpoints, and it keeps the secret out of the request body.
- **Failures must be contractual.** Without a defined failure contract, an
  invalid or expired code would either leak upstream detail or be silently
  indistinguishable from success — and TASK-023 must never create a Player from
  an unverified identity.

## Consequences

### Positive
- `TASK-023` has a concrete, deterministic upstream contract: verified
  `DiscordUserId` from a Discord authorization code.
- The identity boundary is testable in isolation, with the token exchange and
  identity request as two documented, separately verifiable steps.
- The contract names only documented Discord protocol facts, so it can be
  re-verified against upstream documentation rather than trusted from memory.
- Failure behavior is explicit, so an unverified identity can never reach
  Player persistence.

### Negative
- **Two sequential upstream calls** are required per authentication (token
  exchange, then identity), making the endpoint dependent on two Discord
  round-trips.
- **A `redirect_uri` must be configured and must match** the value used in the
  authorization request; a mismatch is an upstream rejection the failure
  contract must surface.
- The identity response is an external contract that Discord may evolve; the
  backend must tolerate unknown additional fields rather than assume a fixed
  field set.
- This ADR does not decide the **application session** mechanism, which stays
  open under `ADR-007`/`TASK-034`; `POST /api/auth/discord` therefore continues
  to return the opaque development session placeholder until that decision
  exists.

### Trade-offs
- **HTTP Basic over body credentials:** slightly less discoverable when reading
  the request body, but keeps the Client Secret out of a body that may be
  logged.
- **`identify` over also requesting `guilds`:** fewer scopes and less personal
  data, at the cost of requiring a second call later if guild data is ever
  needed. That future need is not part of this contract.

## Related Documents

- `docs/03-decisions/ADR/ADR-007-discord-activity-authentication.md` (items 2–4
  — the boundary this ADR implements against, and the session mechanism it
  deliberately leaves open)
- `docs/02-technical/API_CONTRACTS.md` (§1, §2, §6 — the wire contract this ADR
  motivates)
- `docs/02-technical/ARCHITECTURE.md` (§1, §2.1, §2.3 — layer direction and the
  authentication boundary)
- `docs/02-technical/TDD.md` (§2.1 items 2, 4 — the exchange flow and downstream
  session usage)
- `docs/02-technical/DATABASE.md` (§1 — `Player.DiscordUserId (unique)`, the
  field this contract populates)
- `docs/00-overview/MVP_SCOPE.md` (§1 — "Player account (Discord identity,
  collection owner)")
- `tasks/backlog/TASK-035-discord-identity-exchange-contract.md`
- `tasks/backlog/TASK-023-player-persistence-and-authentication-ownership.md`
- `tasks/backlog/TASK-034-application-authentication-session-mechanism.md`
  (the session mechanism, still open)
- `tasks/backlog/TASK-036-discord-credential-secret-hygiene.md` (credential
  storage, separate from this contract)
- External: Discord OAuth2 documentation
  (`https://docs.discord.com/developers/topics/oauth2`), User resource
  (`https://docs.discord.com/developers/resources/user`), API reference
  (`https://docs.discord.com/developers/reference`)
