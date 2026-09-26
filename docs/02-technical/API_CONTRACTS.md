# API Contracts

**Version:** 1.6 (§3 `bossId` semantics fixed per TASK-046 — the request
member is the Boss's canonical technical Identity (e.g. `"boss-hoa-long"`),
never a display name, per `BOSS_RULES.md` §6/§6.4; prior 1.5: §4 battle-result response contract notes — `rewards`
present for both `"Won"`/`"Lost"` (staging value `{}` until TASK-033,
shape: DATABASE.md §1), `battleId` = result row key (`BattleResultId` =
`BattleId`), event-vs-REST reward distinction per GAME_EVENTS.md §2
(TASK-042, ADR-014); prior 1.4: §3 `cardLoadout` made deterministic — 4-step validation
(count, ownership, category, per-CardDefinition `LoadoutCopyLimit`),
rejected with `INVALID_LOADOUT`; Signature Skill Card derived, not
submitted; prior 1.3: §3 `relicLoadout` made deterministic — request
array order is the equip slot order, duplicate instance selection
rejected as `INVALID_LOADOUT`, per RELIC_RULES.md §2.3–§2.5. Prior 1.2: §2 Discord
authorization-code → identity exchange contract
defined — token endpoint, exchange request, identity request, `DiscordUserId`
extraction, failure contract, security requirements; per ADR-013. Prior 1.1:
§3 ownership vs. equip clarified per ADR-011 — Player owns collection; loadout
is battle-scoped for the active Pet)
**Status:** Draft

> This document answers: **"How does the client communicate with the server
> over REST?"** In-battle realtime actions (Swap, Card Cast, Pet Skill Cast)
> and Battle Events are NOT here — see `SIGNALR_PROTOCOL.md`. This document
> does not redefine game rules; it references the owning domain document for
> validation logic.

All endpoints (except `/api/auth/discord`) require an authenticated
session established via Discord Activity authentication (`ADR-007`). The
identity exchange that `POST /api/auth/discord` performs is specified in §2
(mechanics decided in `ADR-013`); the application session mechanism itself
remains undecided (`ADR-007` item 4, TASK-034).

---

# 1. Endpoint Summary

```text
Method  Path                          Purpose
------  ----------------------------  ------------------------------------
POST    /api/auth/discord             Exchange Discord OAuth code for the
                                        Discord identity, match/create the
                                        Player, and establish an application
                                        session (§2, ADR-013)
GET     /api/pets                      List the player's owned Pets
GET     /api/pets/{petId}               Get one Pet's detail (PET_RULES.md)
GET     /api/cards                       List the player's owned Cards
GET     /api/relics                       List the player's owned Relics
POST    /api/battle/start                 Start a battle session, returns a
                                            BattleId + SignalR connection info
GET     /api/battle/{battleId}/result       Get the final result of a
                                            completed battle
GET     /api/battle/history                 List past Battle Results
```

No endpoint accepts Damage, HP, Power, Match, or Combo values from the
client (`GAME_RULES.md` §18) — none of the above are write endpoints for
gameplay values.

**There is no REST endpoint for a player Swap.** Board actions are realtime
only: `Swap` is a Hub method (`SIGNALR_PROTOCOL.md` §2), its validation is
owned by `MATCH3_RULES.md` §2.1, and its resolution result reaches the client
through the event batch and the state push, not through this document. No
endpoint that submits a Swap, a cell pair, a board, a Match, a Cascade, or a
board state is added here or may be added — this document's endpoint list is
the complete REST surface of a battle.

---

# 2. POST /api/auth/discord (Authentication Boundary)

Architectural boundary for establishing an authenticated application session
from a Discord Activity client, and for resolving the caller's Discord identity.

```text
Frontend (Discord SDK)
         │  (authorization code)
         ▼
POST /api/auth/discord
         │  (server-side OAuth2 token exchange — Client Secret, never client-side)
         ▼
Discord OAuth2 Token Endpoint
         │  (access token)
         ▼
Discord Identity Endpoint (GET /users/@me)
         │  (Discord User object)
         ▼
DiscordUserId  ──►  Player match/create (DATABASE.md §1)
         │
         ▼
Application Session (Player authenticated)
```

The exchange mechanics are fixed by `ADR-013`. The protocol facts below are the
official Discord OAuth2 contract; the repository decisions are the selection of
that grant, the scope, and the failure mapping.

## 2.1 Endpoint

```text
Method   POST
Path     /api/auth/discord
Auth     None — this endpoint establishes the session
```

```json
Request:
{
  "code": "string (Discord authorization code)"
}
```

`code` is obtained by the frontend via the Discord Embedded App SDK
(`DiscordService.ts`, `ADR-007` item 1) and is the only request member.

## 2.2 Step 1 — Authorization-code exchange

The backend exchanges the code for an access token:

```text
Method   POST
URL      https://discord.com/api/oauth2/token
Headers  Content-Type: application/x-www-form-urlencoded
         Authorization: Basic base64(client_id:client_secret)
Body     grant_type=authorization_code
         &code=<authorization code>
         &redirect_uri=<registered redirect URI, url-encoded>
```

1. **Grant type.** `authorization_code`.
2. **Encoding.** The token endpoint accepts **only**
   `application/x-www-form-urlencoded`. JSON is not permitted and returns an
   error — this is a Discord protocol constraint, not a repository choice.
3. **Credentials.** `client_id` and `client_secret` are supplied via **HTTP
   Basic** authentication, so the Client Secret never appears in the request
   body.
4. **`redirect_uri`.** Must match the URI registered for the application and
   used during authorization. A mismatch is an upstream rejection (§2.6).
5. **Scopes are not sent here.** The scope set is determined by the
   authorization request that produced the code (§2.4).

Successful response:

```json
{
  "access_token": "string",
  "token_type": "Bearer",
  "expires_in": 604800,
  "refresh_token": "string",
  "scope": "identify"
}
```

The response carries **no user identity**. Identity is a separate request
(§2.3).

## 2.3 Step 2 — Identity retrieval

Using the access token from §2.2, the backend requests the authenticated user:

```text
Method   GET
URL      https://discord.com/api/users/@me
Headers  Authorization: Bearer <access_token>
```

Returns the Discord **User object**. The members this contract relies on:

```json
{
  "id": "80351110224678912",
  "username": "Nelly",
  "global_name": null,
  "avatar": "8342729096ea3675442027381ff50dfe"
}
```

1. **Required scope:** `identify`. Discord documents `identify` as allowing
   `/users/@me` *without* `email`. The `email` scope is deliberately **not**
   requested: identity mapping needs only the user id.
2. **`DiscordUserId` source field: `id`.** This field — documented as "the
   user's id" — is what populates `Player.DiscordUserId` (`DATABASE.md` §1).
3. **`id` is a snowflake, serialized as a string.** Discord returns IDs as
   strings to avoid integer overflow, so `id` must be handled as an opaque
   string, never parsed into a numeric type.
4. **The response is an external contract.** Discord may add fields; unknown
   additional members must be ignored rather than treated as an error.
5. **The deprecated `discriminator` is not used for identity.** It is not
   unique across the platform and is not stable. `id` is the identity.

## 2.4 Identity output — `DiscordUserId`

```text
DiscordUserId = Discord User object . id      (a snowflake string)
```

This is the contract's sole output to Player persistence. It is:

- **stable** — the same Discord account yields the same `id` on every
  successful exchange, so repeated authentication resolves to one Player row;
- **unique per account** — matching `DATABASE.md` §1's unique constraint;
- **verified server-side** — it comes from an authenticated `/users/@me` call
  made with a token the backend obtained using the protected Client Secret.

**`DiscordUserId` is not any of the following, and none of them may be
substituted for it:**

```text
DiscordUserId  ≠  the Discord authorization code     (short-lived, single-use)
DiscordUserId  ≠  the Discord access token           (a credential, not an identity)
DiscordUserId  ≠  the application session token      (ADR-007 item 4, TASK-034)
```

## 2.5 Response

```json
Response 200:
{
  "sessionToken": "string",
  "playerId": "string"
}
```

The response shape is unchanged. `playerId` is the matched-or-created Player's
`PlayerId` (`DATABASE.md` §1).

**The application session mechanism is not defined by this contract.** `ADR-007`
item 4 requires an authenticated application session but decides no format,
claims, lifetime, or validation strategy; that decision is owned by
`TASK-034`. Until it exists, `sessionToken` remains an opaque, unvalidated
placeholder. This section does not define, and must not be read as defining, a
token format.

## 2.6 Failure contract

All failures return the §6 error envelope and **never** include upstream Discord
error detail, the Client Secret, the access token, or the authorization code.

```text
Condition                              Response   error code
─────────────────────────────────────  ─────────  ──────────────────────
Missing/empty `code` in the request    400        INVALID_CODE
Authorization code missing/undefined   400        INVALID_CODE
Authorization code invalid             401        DISCORD_AUTH_FAILED
Authorization code expired             401        DISCORD_AUTH_FAILED
Authorization code already used        401        DISCORD_AUTH_FAILED
Authorization code revoked/rejected    401        DISCORD_AUTH_FAILED
redirect_uri mismatch                  401        DISCORD_AUTH_FAILED
Discord token exchange transport error 503        DISCORD_UNAVAILABLE
Discord token endpoint 5xx / 429       503        DISCORD_UNAVAILABLE
Identity request transport error       503        DISCORD_UNAVAILABLE
Identity endpoint 5xx / 429            503        DISCORD_UNAVAILABLE
Malformed / unparseable Discord body   502        DISCORD_BAD_RESPONSE
Identity response missing/empty `id`   502        DISCORD_BAD_RESPONSE
Identity response `id` not a string    502        DISCORD_BAD_RESPONSE
```

Rules:

1. **`INVALID_CODE` (400)** is the existing, already-implemented precondition
   for a missing code and is retained unchanged.
2. **`DISCORD_AUTH_FAILED` (401)** covers every case where Discord rejects the
   authorization code. The client's remedy is to obtain a new code; the
   specific upstream reason is not disclosed.
3. **`DISCORD_BAD_RESPONSE` (502)** covers a reachable Discord that returned
   something this contract cannot use. A missing or empty `id` **must** be
   treated as a failure, never coerced into an empty `DiscordUserId`.
4. **`DISCORD_UNAVAILABLE` (503)** covers transport failure and upstream
   `5xx`/`429` — a transient upstream condition, distinguishable from a rejected
   code so the client can retry rather than re-authorize.
5. **No Player is created or matched on any failure path.** A Player row may be
   written only after §2.4's identity is successfully verified (§2.4, and
   `ADR-013` item 12).
6. **Codes are grouped, not enumerated per upstream error.** Distinct upstream
   causes that are indistinguishable to the client share one code; Discord's
   numeric JSON error codes are upstream detail and are not part of this
   contract's surface.

## 2.7 Security requirements

1. **The exchange is server-side.** The authorization code, the Client Secret,
   and the resulting access token never reach the frontend (`ADR-007` items
   2–3).
2. **The Client Secret is backend-only configuration.** It is never sent to the
   frontend, never returned in any response, and never committed (`ADR-007`
   item 3).
3. **Neither the code nor the access token becomes the player identity.** Only
   §2.4's verified `DiscordUserId` is (see the inequality list in §2.4).
4. **The access token is not the application session.** It is used solely for
   the §2.3 identity request within this request and is not issued to the
   client.
5. **Secrets and tokens are never logged.** The Client Secret, access token,
   refresh token, and authorization code must not appear in logs, error
   messages, or telemetry.
6. **The identity response is validated before any Player write.** A response
   that fails §2.6's `DISCORD_BAD_RESPONSE` conditions produces no Player row.
7. **Scope is minimal.** Only `identify` is requested.
8. **The frontend never performs any part of §2.2 or §2.3** (`ADR-007` item 1).

---

# 3. POST /api/battle/start

```json
Request:
{
  "petId": "string",
  "bossId": "boss-hoa-long",
  "cardLoadout": ["heal", "shield", "power_charge"],
  "relicLoadout": ["relic_id_1", "relic_id_2", "relic_id_3"]
}
```

Validation (delegates to domain rules, does not reimplement them):

```text
petId must be owned by the player                        — PET_RULES.md §2
bossId must be a valid MVP Boss's canonical technical    — BOSS_RULES.md §6/§6.4
  Identity (e.g. "boss-hoa-long"), never a display name
cardLoadout must be exactly 3 Basic Cards                  — CARD_RULES.md §1
relicLoadout must be 3–5 Relics owned by the player          — RELIC_RULES.md §2
  with no repeated Relic instance, and its array order is
  the equip slot order (slot = position + 1)               — RELIC_RULES.md §2.3–§2.4

Ownership vs. equip: the Player owns the Pet, Card, and Relic collection
(Player FKs in DATABASE.md §2); `cardLoadout`/`relicLoadout` select the
battle-scoped set equipped for the one active Pet for this battle — there is
no global Player relic/card equip slot (ADR-011). Validation checks Player
ownership of the selected instances and, for the Pet, that `petId` is the
active Pet; combat stats for the battle are `PetState`, not `PlayerState`
(GAME_STATE.md §2.3).
```

**`relicLoadout` validation and slot order are determined.**
`relicLoadout` is an **ordered** array, and its order is authoritative:
position *i* (0-based) is equip slot *i + 1* (`RELIC_RULES.md` §2.3). The
server must not re-sort the selection by any `RelicInstanceId`,
`RelicDefinitionId`, acquisition date, or database order. Validation runs in
this order:

```text
1. count        3–5 elements                             — RELIC_RULES.md §2.1
2. ownership    every element is an owned Relic instance  — RELIC_RULES.md §2.1
                of the requesting Player                  (DATABASE.md §2)
3. distinctness no RelicInstanceId repeats in the array   — RELIC_RULES.md §2.4
```

A selection failing any of the three is rejected with `INVALID_LOADOUT`
(below). The same Relic instance may occupy **at most one** slot; two
**distinct** instances that reference the same `RelicDefinition` **may** be
equipped together (`RELIC_RULES.md` §2.4 items 1–3). A rejected request
equips nothing and writes no battle state. The selected instances are
snapshotted into `PetState.EquippedRelics[]` in this same order
(`RELIC_RULES.md` §2.5, `GAME_STATE.md` §2.3).

**`cardLoadout` validation is determined.**
`cardLoadout` is the array of exactly 3 submitted Basic Card
`CardDefinitionId` values. The active Pet's Signature Skill Card is
**derived**, not submitted, and is never part of this array
(`CARD_RULES.md` §1, §4). Validation runs in this order:

```text
1. count        exactly 3 elements                          — CARD_RULES.md §1
2. ownership    every CardDefinitionId has a                 — CARD_RULES.md §1
                PlayerUnlockedCard row for the
                requesting Player                          (DATABASE.md §2)
3. category     every element is Category = Basic           — CARD_RULES.md §1
4. copy limit   each element's occurrence count in the       — CARD_RULES.md §1
                array ≤ that CardDefinition's
                LoadoutCopyLimit (explicit value
                required, no default)                      (DATABASE.md §1)
```

A selection failing any of the four is rejected with `INVALID_LOADOUT`
(below — the same documented code this endpoint uses for
`relicLoadout`). A rejected request equips nothing and writes no battle
state. An accepted selection is snapshotted at battle start into
`PetState.EquippedCards[]` as 4 `CardDefinitionId` entries — the 3
submitted Basics plus the derived Signature Skill; repeated Basic
entries repeat the same `CardDefinitionId` and are not instances
(`GAME_STATE.md` §2.3).

```json
Response 200:
{
  "battleId": "string",
  "signalrHub": "string (hub URL/path)",
  "initialState": { "...": "BattleState summary, GAME_STATE.md §2" }
}
```

```json
Response 400: { "error": "INVALID_LOADOUT" | "PET_NOT_OWNED" | "..." }
```

This endpoint is **unchanged** by Battle State Foundation
(`GAME_STATE.md` §0, §2.0). It remains the only documented way to create a
battle, and it still requires the full gameplay loadout above — no
gameplay-free variant of this endpoint exists, and none is introduced. The
`initialState` it returns is a summary of the full `BattleState`
(`GAME_STATE.md` §2); at the foundation stage no such battle can be created
yet, because the loadout systems it validates do not exist (`ROADMAP.md` §1
Phase 2).

---

# 4. GET /api/battle/{battleId}/result

```json
Response 200:
{
  "battleId": "string",
  "outcome": "Won" | "Lost",
  "rewards": {},
  "durationTurns": 0
}
```

```json
Response 404: { "error": "BATTLE_NOT_FOUND" }
```

Only returns data for a battle that has already ended (`BattleWon` /
`BattleLost` emitted — `GAME_EVENTS.md`). While a battle is active, its
state is only available via the SignalR connection, not this endpoint.

**Contract notes:**

1. **`rewards` is always present** in this response — for both `"Won"`
   and `"Lost"`, never absent or optional. Its value is
   `BattleResult.RewardSummary` exactly as `DATABASE.md` §1 documents it
   (staging value `{}` with no reward line items until TASK-033 owns the
   member list; a `"Lost"` outcome carries no line items).
2. **`battleId` is the result row's primary key** — `BattleResultId` is
   the battle's own `BattleId`, one row per battle (`DATABASE.md` §1).
3. **Event vs REST:** `GAME_EVENTS.md` §2 documents the reward summary as
   `BattleWon`-only because that rule governs the **event payload**; this
   endpoint's `rewards` field covers both outcomes per note 1.

---

# 5. GET /api/pets, /api/cards, /api/relics

Standard list endpoints returning the player's owned collection. Response
shape mirrors the persistent entities in `DATABASE.md` (Pet, Card, Relic)
plus progression fields (Tier/Star/Level for Pets — `PET_RULES.md`).

```json
Response 200 (example, /api/pets):
[
  {
    "petId": "string",
    "identity": "Xích Lang",
    "element": "Hỏa",
    "tier": "Common",
    "star": 1,
    "level": 12
  }
]
```

---

# 6. Error Convention

```json
{ "error": "MACHINE_READABLE_CODE", "message": "human-readable detail" }
```

Error codes are defined per-endpoint as needed; this document does not
enumerate an exhaustive global error list to avoid speculative scope.

---

# 7. What Is Not Here

1. Any endpoint that would let a client submit a gameplay result directly —
   forbidden by `GAME_RULES.md` §18.
2. Custom username/password registration flows — delegated entirely to
   Discord Activity OAuth token exchange (§2, ADR-007).
3. Admin/ops endpoints — not required by MVP scope.
