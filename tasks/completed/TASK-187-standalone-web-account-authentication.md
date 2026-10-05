# TASK-187 — Standalone Web Account Authentication & Discord Retirement

<!--
  GEN-TASK EXECUTION MANIFEST — ARCHITECTURE / FEATURE (Security & Infrastructure)
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.

  References docs/ and src/ by path and section — it does NOT copy game
  rules, formulas, magnitudes, schemas, or API payload shapes.

  SCOPE OF THIS TASK: transition the game's identity and authentication
  boundary from Discord Activity iframe OAuth2 to standalone Web username/password
  authentication backed by PostgreSQL, per ADR-020.
-->

---

## Metadata

```text
Task ID:           TASK-187
Type:              ARCHITECTURE / FEATURE — TASK_TYPES.md §2; crosses
                   database schema + security/auth boundary + frontend bootstrap,
                   so core/validation.md §2's integration depth applies.
Status:            DONE
Risk:              HIGH (authentication/security boundary, database schema
                   migration, player entity identity decoupling)
Priority:          HIGH
Primary Agent:     backend (owns database persistence, account repository,
                   password hashing, AuthController, and JWT session issuance)
Supporting Agents: client (AuthScreen UI, ApiService, ApplicationSession
                   localStorage carriage), review (security boundary review),
                   testing (full regression across backend and frontend suites)
Workflow:          development/feature.md + architecture/adr-change.md
Skills:            discovery/documentation-discovery,
                   discovery/impact-analysis,
                   quality/architecture-conformance,
                   quality/scope-validation,
                   backend/api-contract-validation,
                   backend/persistence-analysis,
                   testing/test-scenario-generation
Dependencies:      None (Supersedes TASK-036 and TASK-181; builds on ADR-020,
                   ADR-015, and ADR-006)
```

---

## Objective

Replace the Discord Activity authentication and embedded SDK dependency with a
first-class, standalone web username/password authentication system. Persist
user accounts in a PostgreSQL `Accounts` table, link `Player` 1-to-1 with
`Account`, provide public `POST /api/auth/register` and `POST /api/auth/login`
endpoints, issue standard HMAC-SHA256 signed JWT application session tokens per
`ADR-015`, and provide a React `AuthScreen` in the frontend client.

---

## Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1 — Target platform: standalone Web browser
- `docs/03-decisions/ADR/ADR-020-standalone-web-account-authentication.md` — Authoritative decision superseding ADR-007, ADR-013, ADR-019
- `docs/02-technical/API_CONTRACTS.md` §2 — Wire schemas for `/api/auth/register` and `/api/auth/login`, JWT session contract
- `docs/02-technical/DATABASE.md` §1 — `Accounts` entity schema, `Player.AccountId` foreign key constraint
- `docs/02-technical/ARCHITECTURE.md` §1, §3 — `Accounts` domain, application, and infrastructure module boundaries
- `docs/02-technical/TDD.md` §1 — Server-authoritative boundary for player identity and authentication

---

## Scope

### In Scope
- PostgreSQL database migration creating `Accounts` table (`AccountId`, `Username`, `PasswordHash`, `CreatedAt`) and modifying `Players` (`AccountId` FK, removing `DiscordUserId`).
- Domain and Application layer models for `Account`, `IAccountRepository`, and `IPasswordHasher<Account>`.
- Infrastructure implementation `AccountRepository` and PBKDF2 password hasher (`Pbkdf2PasswordHasher`) with cryptographic salting.
- `AuthController` implementation exposing `POST /api/auth/register` and `POST /api/auth/login` returning `sessionToken`, `playerId`, and `username`.
- Atomic Player and starter-grant provisioning upon registration.
- Frontend React `AuthScreen` offering login and registration tabs with error display.
- Frontend `ApplicationSession` updating session persistence to `localStorage` and adding sign-out support.
- Complete removal of `@discord/embedded-app-sdk`, `DiscordService.ts`, and development mock auth shims.
- Updating all existing test fixtures and suites to validate the new authentication surface.

### Out of Scope
- Gameplay changes (Match-3 board, combat pipeline, Pet/Card/Relic systems, Boss AI).
- Modifying SignalR `BattleHub` protocol or JWT validation logic (preserved per ADR-015).
- Third-party OAuth providers, email verification, or password reset (deferred beyond MVP).

---

## Current State

The implementation and verification of ADR-020 are complete:
- EF Core migration `20261005120735_AddAccountsTableAndDropDiscordUserId`
- Backend domain, application, and infrastructure account modules
- Updated `AuthController` with `/api/auth/register` and `/api/auth/login`
- Frontend React `AuthScreen` and updated `ApplicationSession` with `localStorage`
- All 2,835 backend tests and 552 frontend tests passing cleanly.

---

## Acceptance Criteria

- [x] AC-01: `Accounts` table defined with UUID `AccountId` (PK), `Username` (unique, lowercase), `PasswordHash`, and `CreatedAt` (UTC).
- [x] AC-02: `Players` table updated to decouple from Discord: `DiscordUserId` dropped, `AccountId` added with unique constraint and FK referencing `Accounts(AccountId)`.
- [x] AC-03: `POST /api/auth/register` validates username (3–32 alphanumeric/underscore characters) and password (minimum 6 characters), enforces uniqueness (returns 409 `USERNAME_ALREADY_EXISTS`), hashes password with PBKDF2, provisions starter grants, and returns JWT session token.
- [x] AC-04: `POST /api/auth/login` verifies credentials, returning 401 `INVALID_CREDENTIALS` on unknown username or mismatched password, and returns JWT session token on success.
- [x] AC-05: Issued JWT session token satisfies `ADR-015` and carries `player_id` claim, preserving authorization across all game endpoints and SignalR `BattleHub`.
- [x] AC-06: Frontend React shell renders `AuthScreen` when unauthenticated, transitions to game loop upon authentication, and persists token in `localStorage`.
- [x] AC-07: Discord Embedded App SDK, `DiscordService.ts`, and development mock shims are completely removed.
- [x] AC-08: All backend test suites compile and pass (2,835 tests passing).
- [x] AC-09: All frontend test suites compile and pass (552 tests passing).

---

## Verification & Completion Evidence

```text
Backend Test Execution:
  GameServer.Application.Tests.dll: 557 passed, 0 failed
  GameServer.Domain.Tests.dll: 1543 passed, 0 failed
  GameServer.Infrastructure.Tests.dll: 412 passed, 0 failed
  GameServer.Api.Tests.dll: 323 passed, 0 failed
  Total: 2,835 tests passed, 0 failed

Frontend Test Execution:
  17 test suites, 552 tests passed, 0 failed
```
