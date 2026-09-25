# TASK-023 — Player Persistence & Authentication Ownership

---

## Metadata

```text
Task ID:           TASK-023
Type:              FEATURE
Status:            DONE
Risk:              MEDIUM
Priority:          CRITICAL
Primary Agent:     persistence
Supporting Agents: backend, testing, review
Workflow:          development/feature.md
Skills:            discovery/documentation-discovery, backend/persistence-analysis, backend/api-contract-validation, testing/test-scenario-generation, quality/scope-validation
Dependencies:      TASK-035 (DONE) — the Discord authorization-code → verified
                   DiscordUserId contract is now defined in ADR-013 and
                   API_CONTRACTS.md §2. This task consumes that contract.
                   TASK-033 and TASK-034 depend on this task, not the reverse;
                   see Authentication Dependency.
```

---

## Objective

Persist the Player as the account/owner entity with its `Level` attribute per
`DATABASE.md` §1, match-or-create that Player during Discord authentication,
and establish `Player.Level` as the persistent account progression value in the
documented `[1, 50]` range, with a newly created Player beginning at the
documented initial value (`PET_RULES.md` §5 item 8). Player Level **increases
through battle Rewards** is explicitly **not** part of this task — that requires
progression amounts the authoritative documentation does not define, and is
owned by TASK-033.

---

## Execution Status — Dependency on TASK-035 (RESOLVED)

**TASK-035 is DONE.** The Discord authorization-code → verified `DiscordUserId`
contract is now defined and this task may proceed against it:

```text
ADR-013                                    the design decision
docs/02-technical/API_CONTRACTS.md §2      the wire contract (§2.1–§2.7)
```

TASK-023 depends on TASK-035 for the authoritative
Discord authorization-code → verified `DiscordUserId` contract.

```text
TASK-035   Discord authorization code → verified Discord identity → DiscordUserId
    ↓        (defined: ADR-013 + API_CONTRACTS.md §2)
TASK-023   DiscordUserId → Player match/create  (this task)
```

**The contract this task consumes:**

```text
POST /api/auth/discord  { "code": "<discord authorization code>" }
        ↓
POST https://discord.com/api/oauth2/token
     application/x-www-form-urlencoded, HTTP Basic credentials,
     grant_type=authorization_code
        ↓
GET https://discord.com/api/users/@me
     Authorization: Bearer <access_token>, scope identify
        ↓
DiscordUserId = the User object's `id`  (a snowflake string)
        ↓
TASK-023 matches or creates the Player on DiscordUserId (DATABASE.md §1)
```

**Why the dependency was real.** This task's match-or-create keys on
`DiscordUserId` (`DATABASE.md` §1, unique), which the authentication flow did
not previously produce: `AuthController.AuthenticateDiscord` received an
authorization code, validated only that it was non-empty, never dereferenced it,
and returned a hard-coded `PlayerId: "player_dev"`. TASK-035 now defines how to
obtain it.

**Do not implement OAuth inside TASK-023.** The code exchange is TASK-035's
scope; this task consumes its result. The token endpoint, grant type, request
encoding, identity endpoint, response field, and `DiscordUserId` source field
are all now fixed by `API_CONTRACTS.md` §2 — read them there rather than
re-deriving them. Do not choose, restate, or vary them (`AGENTS.md` §7, §18).

An earlier revision of this task incorrectly asserted that the Discord identity
"already available in current auth flow" — see Revision History, Revision 3.

---

## Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1 — Player account and Player Level (1–50) listed IN
- `docs/03-decisions/ADR/ADR-011-player-owner-pet-combat.md` — Player = account/owner; no Player combat stats
- `docs/03-decisions/ADR/ADR-012-player-level-pet-level-ownership.md` items 1, 4, 12 — Player Level definition, `[1, 50]` range, MVP scope closure
- `docs/02-technical/DATABASE.md` §1 — Player entity fields; §2 relationships; §3 constraints (including the initial `Player.Level` value)
- `docs/02-technical/API_CONTRACTS.md` §2 — POST /api/auth/discord contract; §6 error convention
- `docs/01-game-design/PET_RULES.md` §5 item 3 — Player Level carries no combat stats
- `docs/01-game-design/PET_RULES.md` §5 item 8, §5.1 item 4 — the authoritative initial Player Level value (a newly created Player starts at Level 1)
- `docs/03-decisions/ADR/ADR-007-discord-activity-authentication.md` — server-side auth boundary (the *boundary* only; the identity exchange protocol is **not** defined here — see Execution Status and TASK-035)
- `tasks/backlog/TASK-035-discord-identity-exchange-contract.md` — the required upstream contract that produces `DiscordUserId`

---

## Scope

### In Scope
- Player entity (per `DATABASE.md` §1: `PlayerId`, `DiscordUserId` unique, `Level`, `CreatedAt`) registered on `GameDbContext` with EF migration
- `Player.Level` persisted, constrained to the documented `[1, 50]` range (`DATABASE.md` §3), initialized to the documented starting value `1` (`PET_RULES.md` §5 item 8)
- Match-or-create Player on POST /api/auth/discord success path, returning that Player's `PlayerId` — **consuming** the `DiscordUserId` that TASK-035 produces
- Update the existing authentication integration test that asserts the hard-coded `player_dev` stub
- Unit/integration tests for persistence, match-or-create, and Level range constraints

### Out of Scope
- **The Discord authorization-code → `DiscordUserId` exchange** (owned by TASK-035). Do not implement OAuth, choose endpoints, or extract identity fields here.
- **Battle Reward → Player Level progression of any kind** (owned by TASK-033)
- **Player XP persistence, XP column, XP amount, XP curve, level-up threshold, level-up formula** (owned by TASK-033; not defined by any authoritative document)
- Player combat stats of any kind (HP/ATK/DEF/Power/Crit) — forbidden (ADR-011 item 5)
- The application session mechanism in any form — session type, token format, claims, expiration, validation, ASP.NET Core authentication scheme, authorization behavior (owned by TASK-034)
- Pet, Card, Relic persistence (TASK-024, TASK-027, TASK-028)
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2

---

## Current State

`GameDbContext` (`src/backend/GameServer.Infrastructure/Postgres/GameDbContext.cs`)
has no entity sets and no migrations. `AuthController`
(`src/backend/GameServer.Api/Controllers/AuthController.cs`) validates that a
code is non-empty, never dereferences it, and returns a stub `player_dev`
PlayerId with no persistence. No component converts an authorization code into a
Discord identity — that missing contract is TASK-035.

---

## Acceptance Criteria

- [x] Player entity exists per `DATABASE.md` §1, is registered on `GameDbContext`, and an EF migration applies cleanly
- [x] `Player.Level` is persisted and constrained to the documented `[1, 50]` range (`DATABASE.md` §3); a newly created Player begins at Level `1` (`PET_RULES.md` §5 item 8)
- [x] POST /api/auth/discord creates or retrieves the Player row for the authenticated Discord identity and returns that Player's `PlayerId`, preserving the `API_CONTRACTS.md` §2 response shape (`sessionToken`, `playerId`) — the identity is obtained via the TASK-035 contract, not reimplemented here
- [x] Repeated authentication for the same Discord identity does not create a duplicate Player row (`DATABASE.md` §3)
- [x] Zero combat-stat columns exist on Player (ADR-011 item 5, `DATABASE.md` §3)
- [x] **No XP column, XP amount, XP curve, level-up threshold, or reward amount is introduced by this task** (TASK-033 boundary)
- [x] The existing `AuthDiscord_WithValidCode_ShouldReturnSessionToken` test is updated to assert the new documented behavior rather than the `player_dev` stub, without weakening its assertions
- [x] No session mechanism, token format, claim, or expiry is introduced (TASK-034 boundary)
- [x] All relevant tests pass at the required validation depth (`core/validation.md` §2)
- [x] Quality review checklist passes (`quality/review.md` §1)
- [x] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001)

---

## Affected Files & Areas

```text
[x] src/backend/ (Domain / Application / Infrastructure / Api)
[ ] src/frontend/client/ (scenes / runtime / services / state / ui)
[x] tests/ (unit / integration / gameplay scenarios)
[ ] docs/ (no documentation change expected — see Implementation Notes)
```

---

## Implementation Notes

- Start point: `AuthController.AuthenticateDiscord` currently stubs
  `PlayerId: "player_dev"`; replace with real match-or-create while preserving
  the `API_CONTRACTS.md` §2 response shape.
- **Consume `DiscordUserId` from TASK-035's contract.** Do not implement the
  Discord OAuth exchange, and do not treat the incoming authorization code as an
  identity. Until TASK-035 defines the contract, this task cannot be implemented.
- `DATABASE.md` §1 defines Player's field set as exactly `PlayerId`,
  `DiscordUserId` (unique), `Level`, `CreatedAt`. **Do not add any further
  column** — in particular, no XP column. If a persistence need appears to
  require one, that is a TASK-033 concern or a stop condition, not an
  in-task schema addition (`AGENTS.md` §17).
- `Player.Level`'s initial value and range are both authoritative:
  `PET_RULES.md` §5 item 8 (initial value) and `DATABASE.md` §3 / ADR-012 item 4
  (range). Both are enforced; neither is inferred.
- No documentation update is expected: this task implements what `DATABASE.md`
  §1/§3 and `PET_RULES.md` §5 already define. If implementation appears to
  require a documentation change, treat it as a stop condition (`AGENTS.md` §17).
- The battle-side Reward hook (`BattleStateService.cs` `BattleWon` path) is
  **not** touched by this task.
- Do not create an ADR for the Player entity, the Level range, or the initial
  Level value: all three are already owned by `DATABASE.md` and `PET_RULES.md`,
  and `docs/03-decisions/README.md` §2 says a decision already fully owned by a
  technical document is not a reason to create one.
- Completed tasks affected in spirit only (do not edit `tasks/completed/`):
  TASK-015, TASK-016, TASK-017, TASK-019.

---

## Testing Requirements

### Required Verification
```text
[x] Unit tests         — Player entity mapping; Level range constraint; match-or-create logic
[x] Integration tests  — auth flow persists Player; repeated auth is idempotent; migration round-trip
[ ] Gameplay scenarios — N/A: this task introduces no gameplay behavior. Reward/Level
                         progression scenarios belong to TASK-033.
```

### Key Edge Cases
- Repeated auth for the same Discord identity does not create duplicate Player rows (`DATABASE.md` §3)
- `Player.Level` bounds: the documented `[1, 50]` range is enforced (`DATABASE.md` §3, ADR-012 item 4)
- New Player starts at Level `1` — the authoritative initial value (`PET_RULES.md` §5 item 8, `DATABASE.md` §3). Do not derive this from the `[1, 50]` range: a range constrains the legal values, it does not state the creation value.

---

## Authentication Dependency

**The identity contract is now available; the session mechanism is still not
defined.**

> **Ownership note (Revision 4):** the identity contract is owned by
> **TASK-035 — Discord Identity Exchange Contract** (**DONE** — `ADR-013`,
> `API_CONTRACTS.md` §2); the session mechanism is owned by
> **TASK-034 — Application Authentication Session Mechanism** (BLOCKED).
> TASK-023 implements neither.

`ADR-007` item 2 defines the flow as: frontend passes the Discord authorization
code → backend exchanges it server-side with the Discord OAuth API for the
verified user identity → "link/create the player (`DATABASE.md`), issuing an
authenticated application session token."

That sentence describes two distinct obligations, and **both are outside this
task**:

```text
Discord authorization code → verified DiscordUserId
    → OWNED BY TASK-035 (DONE).
      Defined in ADR-013 + API_CONTRACTS.md §2. This task CONSUMES it.

DiscordUserId → Player match/create
    → THIS TASK (TASK-023). Depends on the half above.

session → authenticated application session
    → OWNED BY TASK-034 (BLOCKED). Not this task's to define.
```

### Corrected finding (Revision 3)

An earlier revision of this task asserted:

```text
Discord identity (DiscordUserId) already available in current auth flow
```

**That assertion was false and has been removed.** Verified against the code:

- `DiscordAuthRequest` carries a Discord **authorization code**, not an identity.
- `AuthController.AuthenticateDiscord` checks the code is non-empty, then
  **never reads it again** — no exchange, no identity, no `DiscordUserId`.
- The response hard-codes `PlayerId: "player_dev"`.
- No Discord OAuth client exists in `src/`, and no code consumes the
  `Discord:ClientId` / `Discord:ClientSecret` configuration keys.

An authorization code is also not an identity in principle: it is a
short-lived, single-use credential, not the stable per-user identifier that
`DATABASE.md` §1's unique `DiscordUserId` column requires. Match-or-create
cannot key on it.

`ADR-007` originally defined the exchange **boundary** but decided no protocol
mechanics. That gap is now closed by **`ADR-013`** and **`API_CONTRACTS.md` §2**
(TASK-035, DONE), so `DiscordUserId` has a defined source: the `id` field of the
Discord User object returned by `GET https://discord.com/api/users/@me`.

### What this means for TASK-023

- **Do not implement the exchange here** (`AGENTS.md` §7, §18). Consume
  TASK-035's result as specified in `API_CONTRACTS.md` §2.
- **Do not invent a session mechanism here.** `ADR-007` names no format, claims,
  lifetime, or validation strategy; the stub returns an opaque `session_{guid}`
  that nothing validates. TASK-023 continues to return that **opaque,
  unvalidated placeholder**, preserving the `API_CONTRACTS.md` §2.5 response
  shape. TASK-034 owns replacing it.
- **Identity must be verified before any Player write.** Per `API_CONTRACTS.md`
  §2.6 rule 5 and `ADR-013` item 12, a Player row is written only after §2.4's
  `DiscordUserId` is successfully obtained; every failure path produces no
  Player row.

---

## Stop Conditions

- **The `DiscordUserId` contract is defined** (`ADR-013`, `API_CONTRACTS.md` §2.4). Do not re-derive, vary, or substitute an endpoint, parameter, field, or identity value; do not implement the exchange here.
- If required behavior cannot be fully derived from Authoritative References: STOP per `AGENTS.md` §7
- If task requires client-authoritative game logic calculation: STOP per `AGENTS.md` §10
- If implementation requires an XP column, XP amount, XP curve, or reward amount: STOP — that is TASK-033 scope
- If implementation requires defining the application session mechanism rather than the identity→Player mapping: STOP — that is TASK-034 scope
- If implementation appears to require adding a Player column beyond `DATABASE.md` §1's four fields: STOP per `AGENTS.md` §17
- If the initial `Player.Level` value is not authoritative: STOP — do not infer it from the `[1, 50]` range
- If task exceeds 7 skills or crosses multiple uncoupled architectural boundaries: STOP & decompose

---

## Completion Evidence

### Changed Files

**New — Domain / Application**
- `src/backend/GameServer.Domain/Players/Player.cs` — the Player entity: exactly `DATABASE.md` §1's four fields (`PlayerId`, `DiscordUserId`, `Level`, `CreatedAt`), plus the documented `MinLevel`/`MaxLevel` (`1`/`50`) and `InitialLevel` (`1`) constants. No combat stat, no XP column, no reward field.
- `src/backend/GameServer.Application/Players/IPlayerRepository.cs` — the ownership boundary the auth flow consumes: `GetOrCreateByDiscordUserIdAsync(DiscordUserId)`.
- `src/backend/GameServer.Application/Identity/DiscordIdentityResolution.cs` — `DiscordIdentity` (the `DiscordUserId` of `API_CONTRACTS.md` §2.4), `DiscordIdentityResolution` (success **or** a §2.6 failure that carries **no** identity), and `IDiscordIdentityResolver` — the seam the auth boundary consumes.

**New — Infrastructure**
- `src/backend/GameServer.Infrastructure/Postgres/Configurations/PlayerConfiguration.cs` — the Player mapping: PK, `DiscordUserId` unique index, `Level` `[1, 50]` check constraint, `Level` default `1`.
- `src/backend/GameServer.Infrastructure/Postgres/Repositories/PlayerRepository.cs` — find → create → save, with the documented unique constraint handling the concurrent-first-login race.
- `src/backend/GameServer.Infrastructure/Postgres/Migrations/20260924130701_AddPlayerPersistence.cs` (+ `.Designer.cs`, `GameDbContextModelSnapshot.cs`) — creates the `Player` table **only**.
- `src/backend/GameServer.Infrastructure/Discord/UnconfiguredDiscordIdentityResolver.cs` — the exchange client's registration point; until the TASK-035 exchange implementation is registered it reports the §2.6 transient failure and produces no identity.

**Modified — source**
- `src/backend/GameServer.Infrastructure/Postgres/GameDbContext.cs` — added `DbSet<Player> Players`; configuration discovery via `ApplyConfigurationsFromAssembly`.
- `src/backend/GameServer.Infrastructure/DependencyInjection.cs` — registered `IPlayerRepository` and the `IDiscordIdentityResolver` seam.
- `src/backend/GameServer.Api/Controllers/AuthController.cs` — replaced the hard-coded `player_dev` stub with identity → match-or-create → the Player's own `PlayerId`; the `session_{guid}` opaque placeholder is retained unchanged.
- `src/backend/GameServer.Api/GameServer.Api.csproj`, `src/backend/GameServer.Infrastructure/GameServer.Infrastructure.csproj` — added `Microsoft.EntityFrameworkCore.Design` (`PrivateAssets=all`), the tooling the existing EF Core migration workflow requires (`ADR-006`).

**Modified / new — tests**
- `tests/backend/GameServer.Api.Tests/ApiIntegrationTests.cs` — `AuthDiscord_WithValidCode_ShouldReturnSessionToken` updated: the `player_dev` assertion is replaced by the documented no-verified-identity outcome (`503 DISCORD_UNAVAILABLE`, no `playerId`). No assertion was weakened — the stub assertion was removed, not loosened.
- `tests/backend/GameServer.Api.Tests/AuthPlayerOwnershipTests.cs` — **new**: create-at-Level-1, reuse-same-Player-and-preserve-Level, no Player on a missing code, no Player on a rejected identity.
- `tests/backend/GameServer.Infrastructure.Tests/PlayerPersistenceTests.cs` — **new**: field set, no combat fields, no XP/reward field, table/key mapping, unique index, Level range + default.
- `tests/backend/GameServer.Infrastructure.Tests/PlayerMatchOrCreateTests.cs` — **new**: create, idempotent match, Level preservation, distinct identities, opaque-string identity, `CreatedAt` stability.
- `tests/backend/GameServer.Infrastructure.Tests/PlayerPostgresConstraintTests.cs` — **new**: the constraints as real PostgreSQL enforces them (unique `DiscordUserId`, `[1,50]` range, creation Level `1`); skipped when no database is reachable.
- Test `.csproj` files — added `Microsoft.EntityFrameworkCore.InMemory` for isolated stores.

**No documentation was changed.** The implementation realizes what `DATABASE.md` §1/§3 and `PET_RULES.md` §5 item 8 already define.

### Validation Results
- `dotnet build src/backend/GameServer.sln` — PASS (0 errors)
- `dotnet test src/backend/GameServer.sln` — PASS (**1032** tests: Domain 830, Application 100, Infrastructure 19, Api 83), 0 failed
- `dotnet ef migrations script` — PASS; renders exactly one `CREATE TABLE "Player"` (4 columns, PK, `CK_Player_Level_Range`) + `CREATE UNIQUE INDEX "IX_Player_DiscordUserId"`
- `dotnet ef database update` against local PostgreSQL — PASS (applied cleanly)
- Live-schema constraint verification — PASS (duplicate `DiscordUserId` rejected; Level `51`/`0` rejected; creation Level `1`)
- Development database left clean (0 residual `Player` rows)

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic (no gameplay code touched; no frontend change)
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1 — "Player account (Discord identity, collection owner)" / "Player Level (1–50)")
- [x] Confirmed no Reward/XP/Level-progression logic implemented (TASK-033 boundary)
- [x] Confirmed `DiscordUserId` uniqueness enforced (model + live PostgreSQL)
- [x] Confirmed a new Player starts at Level `1`, and an existing Player's Level is preserved
- [x] Confirmed no Player combat stats introduced
- [x] Confirmed no JWT/session mechanism introduced (TASK-034 remains separate)
- [x] Confirmed no Pet persistence introduced (TASK-024+)
- [x] Confirmed only the `Player` table was created

---

## Revision History

**Revision 1 — decomposition revision.** TASK-023 was BLOCKED because its
Reward → Player Level progression deliverable could not be derived from the
authoritative documentation: `ADR-012` item 2, `PET_RULES.md` §5 items 2 and 6,
and `MVP_SCOPE.md` §1 each state that exact XP amounts and curves are a
balance/config concern that no document defines. The task also required
resolving whether XP persists across sessions, where both branches were
unsatisfiable (`DATABASE.md` §1 defines no XP column; session-scoped XP could
never accumulate to raise a `[1, 50]` Level).

Resolution: the Reward/XP/Level-progression responsibility was **removed** from
TASK-023 and moved to TASK-033, which remains BLOCKED until those design rules
are explicitly defined. TASK-023 retains only the fully specified persistence
foundation. No XP value, curve, threshold, amount, or formula was invented.

**Revision 2 — dependency/reference coherence.** The authentication session
mechanism identified in this task's Authentication Blocker was extracted into
**TASK-034 — Application Authentication Session Mechanism** (BLOCKED), which
now owns the session type, format, claims, lifetime, validation strategy,
ASP.NET Core authentication scheme, and authorization behavior. TASK-023's
scope is unchanged: it still implements identity → Player persistence and
returns the opaque session placeholder. Only the blocker's ownership reference,
the dependency note, and the matching stop condition were updated. No
authentication mechanism was invented, and no implementation scope moved
between the two tasks.

**Revision 3 — false premise corrected; TASK-035 dependency added.** An
execution attempt on TASK-023 stopped at a mandated stop condition and reported
that this task rested on a false premise:

```text
"Discord identity (DiscordUserId) already available in current auth flow"
```

Verified against the code, this is **not true**: `AuthenticateDiscord` receives
an authorization code, never dereferences it, and returns a hard-coded
`player_dev`. No Discord OAuth client exists, and `ADR-007` item 2 /
`API_CONTRACTS.md` §2 / `TDD.md` §2.1 define the exchange *boundary* but no
protocol mechanics. Match-or-create cannot key a unique-matching Player row on a
value that is neither produced nor defined.

Resolution: the missing contract was extracted into **TASK-035 — Discord
Identity Exchange Contract**, which owns the authorization-code →
verified `DiscordUserId` exchange. TASK-023 now **depends on TASK-035**
(`TASK-035 → TASK-023`) and must not proceed independently of it.

Three corrections were applied:

```text
1. The false "identity is already available" claim was removed and replaced
   with the accurate finding plus the TASK-035 dependency.
2. Dependencies changed from "None" to TASK-035.
3. The initial Player Level value was made authoritative rather than inferred:
   "a newly created Player starts at Level 1" is now recorded in
   PET_RULES.md §5 item 8 (§5.1 item 4) and referenced by DATABASE.md §3.
   TASK-023 previously asserted the range floor [1, 50] proved the starting
   value; a range constrains legal values, it does not state a creation value.
```

No OAuth endpoint, grant type, request encoding, identity field name, response
shape, token format, session mechanism, XP amount, or level-up curve was
invented. No ADR was created at that time: `docs/03-decisions/README.md` §1/§5
and `.ai/workflow/architecture/adr-change.md` §2 forbid recording a decision
that has not actually been made.

**Revision 4 — TASK-035 resolved; dependency satisfied.** TASK-035 is now
**DONE**: the Discord authorization-code → verified `DiscordUserId` contract is
recorded in **`ADR-013`** and **`API_CONTRACTS.md` §2** (§2.1–§2.7). This task's
dependency is therefore satisfied, and the Execution Status / Authentication
Dependency sections were updated to reference the defined contract rather than
a missing one.

Recorded for this task's consumer side:

```text
DiscordUserId  = the `id` field of the Discord User object returned by
                 GET https://discord.com/api/users/@me
                 (API_CONTRACTS.md §2.4, ADR-013 item 6)
```

Two boundaries are unchanged: this task still does **not** implement the
exchange (TASK-035's scope), and still does **not** define the application
session (TASK-034's scope — `API_CONTRACTS.md` §2.5 leaves it explicitly open,
so the opaque `session_{guid}` placeholder continues). No OAuth value, session
mechanism, or Player schema element was added by this revision.

**Revision 5 — implemented; task DONE.** TASK-023 was implemented against the
Revision 4 contract and validated. The Player entity, its EF Core mapping and
migration, and the identity → match-or-create ownership flow now exist; the
hard-coded `player_dev` stub is gone from both the implementation and the test
that asserted it.

One boundary question was resolved explicitly before implementation, because
the repository contained no implementation of the TASK-035 exchange:

```text
TASK-035 (DONE)    owns the contract AND its downstream exchange-client
                   implementation ("the server-side exchange client and its
                   configuration binding", In Scope). No source file
                   implements it yet.
TASK-023 (this)    consumes a verified DiscordUserId. Its Scope §Out of Scope
                   states plainly: "The Discord authorization-code →
                   DiscordUserId exchange (owned by TASK-035). Do not
                   implement OAuth, choose endpoints, or extract identity
                   fields here."
```

Implementing the exchange client here would have contradicted this task's own
Out of Scope clause, so this revision **implements the seam and not the
exchange**: `IDiscordIdentityResolver` (`API_CONTRACTS.md` §2.2–§2.4's
contract boundary) plus its registration point. Until TASK-035's exchange
client is registered there, the resolver reports the §2.6 transient condition
and yields no identity, so `POST /api/auth/discord` returns `503
DISCORD_UNAVAILABLE` and writes no Player row. A fabricated identity was
never an option: §2.6 rule 5 and `ADR-013` item 12 forbid a Player write
without a verified identity.

The endpoint's ownership behaviour is nonetheless fully implemented and
verified, by supplying a verified identity through that seam in
`AuthPlayerOwnershipTests` — the same production `AuthController` and the same
`PlayerRepository`, with only the exchange stubbed. So the criterion "POST
/api/auth/discord creates or retrieves the Player row for the authenticated
Discord identity and returns that Player's PlayerId" is satisfied against a
verified identity, while the exchange protocol itself remains TASK-035's.

No XP column, reward amount, level-up curve, threshold, combat stat, session
mechanism, token format, claim, or expiry was introduced. `Player.Level`'s
range and initial value are read from the authoritative `PET_RULES.md` §5 /
`DATABASE.md` §3 rules and are not inferred. No documentation was modified.
