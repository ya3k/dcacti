# ADR-007: Discord Embedded App SDK Integration & Server-Side Authentication Boundary

**Status:** Accepted
**Date:** 2026-09-19

## Context

The game runs as a Discord Activity — a client-side web application embedded
inside Discord's client. Every REST endpoint and SignalR Hub connection requires
an authenticated session (`API_CONTRACTS.md`, `SIGNALR_PROTOCOL.md`).

A clear architectural boundary is required to define:
1. Where the Discord Embedded App SDK lives and operates.
2. How Discord authentication is performed without exposing credentials.
3. How authenticated sessions flow to REST endpoints and the SignalR `BattleHub`.

## Decision

Establish a strict separation between frontend Discord SDK operations and
backend authentication verification:

1. **Frontend SDK Ownership:**
   - The **Discord Embedded App SDK** runs exclusively on the frontend inside
     `client/src/services/discord/DiscordService.ts`.
   - The frontend initializes the Activity SDK, accesses client-side Discord
     context, and requests the user's authorization code.
   - The SDK is NOT executed on the backend and is NOT imported into Phaser
     game scenes or combat domain code.

2. **Backend Authentication & Token Exchange (`POST /api/auth/discord`):**
   - The backend exposes an architectural authentication boundary
     (`POST /api/auth/discord`, `API_CONTRACTS.md` §2).
   - The frontend passes the Discord authorization code to this endpoint.
   - The ASP.NET Core backend securely contacts the Discord OAuth API
     server-side to exchange the code for the verified user identity and
     link/create the player (`DATABASE.md`), issuing an authenticated
     application session token.

3. **Client Secret Security Boundary:**
   - **Frontend:** Contains `Discord Client ID` only. Must NEVER contain or
     expose `Discord Client Secret`.
   - **Backend:** Holds `Discord Client ID` and `Discord Client Secret` via
     server-side environment/secret configuration.
   - Client Secret is never exposed via Vite client bundles, environment
     variables exposed to browsers, React config, Phaser code, or Git.

4. **SignalR & REST Application Session:**
   - All subsequent REST endpoints and SignalR Hub connections (`BattleHub`)
     authenticate using the application session token.
   - SignalR and domain game logic interact with application user identities,
     with zero direct coupling to Discord SDK internals.

## Alternatives Considered

### Option A — Custom Account System (Username/Password)
Build an independent account system. Rejected: Redundant and poor UX for an
embedded Discord Activity where the user is already authenticated with Discord.

### Option B — Client-Side OAuth Token Exchange
Perform the token exchange directly in the browser. Rejected: Requires exposing
the `Discord Client Secret` to the client bundle, creating a severe security
vulnerability.

### Option C — Frontend SDK + Backend OAuth Token Exchange (Chosen)
Frontend obtains authorization code via Discord Embedded App SDK; ASP.NET Core
backend exchanges the code server-side using the protected Client Secret.

## Why

- **Security:** Guarantees that `Discord Client Secret` never leaks into the
  browser or client build artifacts.
- **Separation of Concerns:** Keeps platform-specific Discord SDK code isolated
  behind `DiscordService.ts`, preventing platform leaks into the Phaser game
  canvas, SignalR hub, or battle domain logic.
- **Uniform Authentication:** Allows REST API and SignalR Hub connections to
  share a consistent application session model.

## Consequences

### Positive
- Client secret is completely secure on the backend.
- Clean platform abstraction (`services/discord/`) keeps Phaser and Game Server
  domain clean.
- Consistent application session across REST and SignalR.

### Trade-offs
- Requires a backend token exchange step (`POST /api/auth/discord`) during
  initial client startup before entering gameplay or connecting to SignalR.

## Related Documents

- `docs/00-overview/GDD.md` (Platform: Discord Activity)
- `docs/02-technical/TDD.md` (§1, §2.1)
- `docs/02-technical/ARCHITECTURE.md` (§1, §2.2, §2.3)
- `docs/02-technical/API_CONTRACTS.md` (§2)
- `docs/02-technical/SIGNALR_PROTOCOL.md` (§1)
