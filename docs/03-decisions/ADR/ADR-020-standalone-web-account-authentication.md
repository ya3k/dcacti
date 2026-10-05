# ADR-020: Standalone Web Account Authentication (Username/Password) & Retirement of Discord Activity Dependency

**Status:** Accepted
**Date:** 2026-10-06
**Supersedes:** ADR-007 (all), ADR-013 (all), ADR-019 (all)
**Amends:** ADR-003 (platform target is standalone Web browser instead of Discord Activity iframe)

---

## 1. Context

The project originally envisioned deployment as a Discord Activity (an embedded web application running inside an iframe in Discord's desktop/web/mobile clients). Under that model:
1. `ADR-007` required `@discord/embedded-app-sdk` on the frontend and an OAuth authorization-code exchange on the backend (`POST /api/auth/discord`).
2. `ADR-013` specified the exact Discord OAuth2 code exchange contract (`/oauth2/token` and `GET /users/@me`), extracting `DiscordUserId` as the identity anchor.
3. `ADR-019` established secret hygiene for Discord credentials.

However, the team has made the strategic decision to **pivot to a 100% Web-First model and completely retire the Discord dependency**:
- The game will run as a standalone Web application playable in any modern desktop and mobile browser.
- Players authenticate directly using standard username and password credentials.
- No Discord client, Discord Developer Portal app, Discord iframe, or Discord OAuth2 APIs are required to run, develop, test, or play the game.

## 2. Decision

### D1 — Retirement of Discord Components
1. The Discord Embedded App SDK (`@discord/embedded-app-sdk`) is completely removed from frontend dependencies.
2. All Discord frontend services (`DiscordService.ts`) and mock/development resolvers are retired.
3. The backend endpoint `POST /api/auth/discord` is retired.
4. Discord configuration keys (`Discord:ClientId`, `Discord:ClientSecret`) and validation startup gates are removed.

### D2 — Web Account Identity Model (PostgreSQL)
1. A new persistent `Accounts` table is introduced:
   - `AccountId` (UUID, Primary Key)
   - `Username` (VARCHAR(32), Unique index, case-insensitive lowercase comparison)
   - `PasswordHash` (VARCHAR, PBKDF2 with unique cryptographic salt via ASP.NET Core `IPasswordHasher<Account>`)
   - `CreatedAt` (DateTimeOffset, UTC)
2. The `Player` entity is decoupled from Discord:
   - `DiscordUserId` is removed from `Players`.
   - `AccountId` (UUID, Unique, Foreign Key referencing `Accounts(AccountId)`) is added to link a Player 1-to-1 with their Account.
   - `PlayerId` remains the stable primary key and application identity throughout the game server.

### D3 — Authentication Endpoints (`API_CONTRACTS.md` §2)
The authentication surface is replaced by two public endpoints:
1. **`POST /api/auth/register`**:
   - Accepts `{ "username": string, "password": string }`.
   - Validates input format (Username: 3–32 alphanumeric/underscore characters; Password: minimum 6 characters).
   - Verifies username uniqueness (returns `409 Conflict` with `USERNAME_ALREADY_EXISTS` if taken).
   - Hashes password securely.
   - Atomically creates `Account` and `Player` (with default starter grant: Pet, Cards, Relics per existing rules).
   - Issues and returns standard JWT `ApplicationSession` token and `PlayerId`.
2. **`POST /api/auth/login`**:
   - Accepts `{ "username": string, "password": string }`.
   - Verifies username and validates password hash (returns `401 Unauthorized` with `INVALID_CREDENTIALS` on failure).
   - Fetches associated `Player`.
   - Issues and returns standard JWT `ApplicationSession` token and `PlayerId`.

### D4 — Preservation of Application Session & Realtime Architecture (ADR-015, ADR-004)
1. `ADR-015` remains fully in force:
   - The token issued by `/register` and `/login` is the exact same HMAC-SHA256 signed JWT carrying the `player_id` claim.
   - All subsequent game endpoints (`/api/battle/*`, `/api/collections/*`) and SignalR `BattleHub` validate this token unchanged.
2. On the frontend, the session token is persisted in `localStorage` so refreshing the browser tab preserves the authenticated session. A sign-out/logout option clears the token.

### D5 — Frontend Presentation & Game Shell (Amends ADR-003)
1. When unauthenticated, React presents an `AuthScreen` (Login / Register tabs).
2. Once authenticated, the existing `GameShell` mounts Phaser 4 and starts the game loop (`BootScene` → `PreloaderScene` → `MainMenuScene` → `LobbyScene` → `BattleScene`).

---

## 3. Consequences

- **Positive:**
  - Full independence from Discord's ecosystem, APIs, and downtime.
  - Testability: Developers and QA can register and log into arbitrary accounts in multiple tabs/browsers without mock shims.
  - Zero disruption to core game mechanics: Match-3 board, combat pipeline, Pet/Card/Relic systems, Boss AI, and SignalR events are 100% preserved.
- **Negative / Migrations:**
  - Database migration required to create `Accounts` table and update `Players` table.
  - Existing automated tests exercising Discord authentication are updated to exercise the new register/login endpoints.
