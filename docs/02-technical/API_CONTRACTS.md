# API Contracts

**Version:** 1.17 (§4.5 `GET /api/battle/history` response contract defined per
TASK-163's 24 recorded Product Owner decisions, applied by TASK-164 — the last
endpoint in §1's summary that had no defining section now has one: a bare JSON
array (no wrapper), each element carrying the full §4 result members
[`battleId`, `outcome`, `rewards` with the complete 8-member `RewardSummary`,
`durationTurns`] plus the additional `completedAt` member; ordering
`CompletedAt` DESC with a `BattleResultId` DESC tie-break, explicitly
client-reliable; no pagination, filter, sort, or search parameter in MVP (the
array is the full history); authenticated session required with `401
UNAUTHENTICATED` for a caller presenting none; history scoped to the
server-derived authenticated `PlayerId` and never a client-supplied one; empty
history is `200 []`; an active battle and a failed durable write are both
absent; no Boss/Pet identifying member is exposed. **No endpoint behaviour,
gameplay rule, schema, index, SignalR member, or Redis contract changes** —
the contract is a projection of the existing `BattleResult` row and the
existing `BattleResult(PlayerId, CompletedAt DESC)` index, so `DATABASE.md` is
unmodified and no ADR is required. §1's summary gains a §4.5 pointer and §4's
own semantics are untouched; §5.5 retains its §5.1/§5.3/§5.4 scope. Prior 1.16:
(§4 `GET /api/battle/{battleId}/result` response example and contract
notes synchronized with the landed 8-member `RewardSummary` contract per TASK-089 —
replaces `"rewards": {}` with the full 8-member object projection [`playerXpGained`,
`newPlayerXp`, `playerLeveledUp`, `newPlayerLevel`, `petXpGained`, `newPetXp`,
`petLeveledUp`, `newPetLevel`]; removes obsolete `{}` staging phrases and deferred Pet
member list wording; preserves symmetrical victory/defeat shape per TASK-068 Option A).
Prior 1.15: (§5.1's Element wire value set **scoped and made
non-duplicable** per TASK-073 — the set is now stated as the Element wire set
for this document's whole REST surface, and the recall pointer in §3 binds
`POST /api/battle/start`'s `initialState.petState.element` /
`initialState.bossState.element` to it by reference, so the battle-start
response is no longer silent about its Element representation; the former
unqualified sentence "wire payloads always carry the English form bound above"
is replaced by that explicit scope plus an explicit statement that the set does
**not** bind the Redis record's encoding, which `REDIS_STATE.md` §2 owns. No
value in the set changed, no endpoint changed, and the set is still defined
here exactly once. Prior 1.14: §5.1 element wire value set defined per TASK-072 —
`element` is now bound to `"Fire" | "Water" | "Earth" | "Wood" | "Metal"`
(the Element names from `ELEMENT_RULES.md` §1), the §5.1/§5.2 examples
corrected from `"Hỏa"` to `"Fire"`, and the Vietnamese design names (Mộc,
Hỏa, Thổ, Kim, Thủy) recorded as display values only — presentation
text, never API wire values. Prior 1.13: §5 collection read contracts
defined per TASK-070 human
decisions D1–D4 plus derivations D5–D7 — all four documented routes are now
implementation-ready: `/api/cards` = `cardId, name, category` (unlock state =
array membership, no `Card` entity exists — ADR-012, `DATABASE.md` §2),
`/api/relics` = `relicId, name`, `/api/pets`' example made the binding
exhaustive member list (`petId` = `PetInstanceId`; `xp`/`acquiredAt`/`playerId`
explicitly not exposed), and `GET /api/pets/{petId}` gained its own §5 section
(200 = same bare object as one array element; missing and foreign `petId` both
→ `404 PET_NOT_FOUND`, extending §4 note 7's no-existence-disclosure pattern);
D5 list semantics recorded (empty → `200 []`, no pagination, no defined
ordering), D6 recorded that no collection response carries equip/loadout
members (`DATABASE.md` §2, ADR-011, `GAME_STATE.md` §2.3); D7 corrected §4
note 7's stale "`API_CONTRACTS.md` §7 item 1" pointer to §2.8; §1's
detail-route pointer retargeted from `PET_RULES.md` (defines no response) to
§5. Prior 1.12: §4 note 1 updated per TASK-068 human decision (**Option A**)
— the `RewardSummary` defeat-shape contradiction in `DATABASE.md` §1 is
resolved: the Player-track member set applies to **both** outcomes, so a
`"defeat"` carries `playerXpGained = 0` rather than no line items; note 1 no
longer restates a defeat-specific empty shape, and note 3's "value in force"
clause now names both the staging value and the landed shape. Prior 1.11: §4 note 1 updated per TASK-062 — the Pet XP reward
semantics are now decided (`PET_RULES.md` §5.3), so the note no longer
describes the Pet track as "not finalizable"; the Pet member list remains
deferred to the implementation task. Prior 1.10: §4 notes 1 and 3 updated per TASK-059 — `rewards`
references the single `RewardSummary` contract owned by `DATABASE.md` §1
("Reward semantics for `RewardSummary`"), which is now the member-list
owner instead of TASK-033; the Player track is decided
(`COMBAT_RULES.md` §7) and the Pet track is explicitly not finalizable
until the Pet XP reward decisions (`PET_RULES.md` §5.2 items 5–9) are made,
so no Pet reward field is frozen and REST/event do not contradict each
other. Prior 1.9: §2.8 application session mechanism defined per `ADR-015`
human decisions D1–D6 — self-contained signed JWT carrying the `player_id`
claim, stateless (no session storage; `ADR-005`/`ADR-006` boundaries
unchanged), `Authorization: Bearer <sessionToken>` on REST plus SignalR's
standard access-token mechanism for `BattleHub`, 24h absolute expiry with no
idle timeout, renewal, refresh, or revocation in MVP, and missing/invalid/
tampered/expired sessions all → `401 UNAUTHENTICATED` under one public code;
§1 preamble, §2.4, §2.5 and §4 note 6 references updated from the open
TASK-034 decision to `ADR-015`; prior 1.8: §4 result-endpoint authorization
made explicit per TASK-051 human decisions — the endpoint now documents
`401 UNAUTHENTICATED` for a caller
presenting no authenticated session, and note 7 fixes caller ownership: only the
authenticated owner (`BattleResult.PlayerId`, sourced from `BattleState.PlayerId`)
may read a result, a foreign or missing battle both returning the existing
`404 BATTLE_NOT_FOUND`, and ownership never being established from
client-supplied input. The session **mechanism** remains `ADR-007` item 4 /
TASK-034's open decision and is not defined here; prior 1.7: §4 `outcome` vocabulary changed to `"victory" |
"defeat"` per TASK-050 human Decision C — the single battle-outcome
vocabulary owned by `GAME_EVENTS.md` §2 and shared with `DATABASE.md`
§1 and `SIGNALR_PROTOCOL.md` §3.2.19; §4 contract notes added —
`outcome` vocabulary reference and `durationTurns` derivation pointer;
prior 1.6: §3 `bossId` semantics fixed per TASK-046 — the request
member is the Boss's canonical technical Identity (e.g. `"boss-hoa-long"`),
never a display name, per `BOSS_RULES.md` §6/§6.4; prior 1.5: §4 battle-result response contract notes — `rewards`
present for both outcomes (staging value `{}`; member list and shape:
DATABASE.md §1 — reassigned from TASK-033 to `DATABASE.md` §1 by TASK-059),
`battleId` = result row key (`BattleResultId` =
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

All endpoints (except `/api/auth/register` and `/api/auth/login`) require an authenticated
session established via web account authentication (`ADR-020`). The
application session mechanism itself is specified in §2.3 (a self-contained
signed JWT, decided in `ADR-015`).

---

# 1. Endpoint Summary

```text
Method  Path                          Purpose
------  ----------------------------  ------------------------------------
POST    /api/auth/register            Register a new user account, create Player,
                                        and establish an application session (§2.1, ADR-020)
POST    /api/auth/login               Authenticate with username/password, and
                                        establish an application session (§2.2, ADR-020)
GET     /api/pets                      List the player's owned Pets
GET     /api/pets/{petId}               Get one Pet's detail (§5)
GET     /api/cards                       List the player's owned Cards
GET     /api/relics                       List the player's owned Relics
POST    /api/battle/start                 Start a battle session, returns a
                                            BattleId + SignalR connection info
GET     /api/battle/{battleId}/result       Get the final result of a
                                            completed battle (§4)
GET     /api/battle/history                 List past Battle Results (§4.5)
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

# 2. Authentication Endpoints: POST /api/auth/register & POST /api/auth/login (ADR-020)

Architectural boundary for establishing an authenticated application session
via standalone web username/password credentials (`ADR-020`).

```text
Frontend (Web Auth Screen)
         │  (username, password)
         ▼
POST /api/auth/register  OR  POST /api/auth/login
         │  (server-side password hashing / verification)
         ▼
Account & Player match/create (DATABASE.md §1)
         │
         ▼
Application Session (Player authenticated with JWT token)
```

## 2.1 POST /api/auth/register

Registers a new user account, creates the persistent Player record initialized at Level 1, provisions the starter grant (Pet, Cards, Relics per `DATABASE.md` §2 item 1), and issues an authenticated application session token.

```text
Method   POST
Path     /api/auth/register
Auth     None — public endpoint
```

```json
Request:
{
  "username": "string (3–32 characters, [a-zA-Z0-9_])",
  "password": "string (minimum 6 characters)"
}
```

Validation rules:
1. `username` is required, trimmed length between 3 and 32 characters, regex `^[a-zA-Z0-9_]{3,32}$`. Normalized to lowercase.
2. `password` is required, minimum length 6 characters.
3. If username is already registered, returns `409 Conflict` with `USERNAME_ALREADY_EXISTS`.

Successful response (200 OK):
```json
{
  "sessionToken": "string",
  "playerId": "string",
  "username": "string"
}
```

## 2.2 POST /api/auth/login

Authenticates an existing user account using username and password, returning an application session token for the associated Player.

```text
Method   POST
Path     /api/auth/login
Auth     None — public endpoint
```

```json
Request:
{
  "username": "string",
  "password": "string"
}
```

Validation & Verification rules:
1. `username` and `password` are required non-empty strings.
2. If account does not exist or password hash does not match, returns `401 Unauthorized` with `INVALID_CREDENTIALS`.

Successful response (200 OK):
```json
{
  "sessionToken": "string",
  "playerId": "string",
  "username": "string"
}
```

## 2.3 Application Session Mechanism (`ADR-015`)

The application session issued by §2.5 is a **self-contained signed JWT**
(`ADR-015`). This subsection is the authoritative wire/behavior contract for
that session — why it was chosen is `ADR-015`, how it is implemented is
TASK-034. Nothing here may be re-derived from client input.

**Session artifact**

```text
application session  =  self-contained signed JWT, issued by the backend
                        after §2's identity exchange succeeds
session storage      =  none (stateless) — not in Redis (ADR-005), not in
                        PostgreSQL (ADR-006), not in in-memory state
```

The Discord access token is not the session (§2.7 item 4).

**Identity**

```text
claim:  player_id
value:  PlayerId
```

Resolution: `verified DiscordUserId → Player match/create → PlayerId → JWT
player_id → authenticated request → PlayerId`. The server treats this
identity as authoritative; the client never supplies or overrides `PlayerId`
or `DiscordUserId` for authentication or ownership (§4 note 7).
`GameServer.PlayerId` may be used as the server-internal request-context
representation of that identity; it is never a client input. `DiscordUserId`
is not carried as an authoritative ownership claim (§2.4) — `PlayerId` is
sufficient.

**Transport**

```text
REST       Authorization: Bearer <sessionToken>
SignalR    the same JWT via SignalR's standard access-token mechanism
           (SIGNALR_PROTOCOL.md §1)
```

No application-session cookie. A Discord access token is never accepted as a
`BattleHub` authentication credential.

**Coverage**

§1's global rule applies: every endpoint except
`POST /api/auth/register` and `POST /api/auth/login` requires an authenticated session;
those two remain the unauthenticated endpoints (§2.1, §2.2).
`BattleHub` requires the same session (`SIGNALR_PROTOCOL.md` §1).

**Failure behavior**

```text
missing session  |  invalid/tampered session  |  expired session
        →  401  +  { "error": "UNAUTHENTICATED" }   (§6 envelope)
```

All three conditions share this one public response: no distinct error code
distinguishes them, and no token-validation detail is disclosed. This is the
outcome §4 note 6 states, applied to every covered endpoint.

**Lifecycle (MVP)**

```text
absolute expiry   24 hours
idle timeout      none
renewal/refresh   none
revocation        none (no logout, no server-side revocation state)
```

A new successful §2 exchange may issue a new JWT; it does not invalidate
previously issued tokens. No refresh-token persistence and no revocation
store are introduced (`ADR-015` D2/D5).

**Ownership (unchanged)**

Authenticated owner → `200`; foreign or missing battle →
`404 BATTLE_NOT_FOUND`; a `BattleResult` lookup happens only after ownership
authorization (§4 notes 6–7). Authenticated identity is server-derived from
this session; client input can never establish it.

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

**`initialState`'s `Element` members are wire values.** `petState.element` and
`bossState.element` (`GAME_STATE.md` §2.3, §2.4) are members of a client-facing
REST response body, so they carry the Element wire value set bound in §5.1 —
and not a display name, not an enum member name, and not a localized form.
This endpoint's response is a wire payload like any other on this document's
REST surface, so the §5.1 scope statement applies to it; the value set is
defined there and is not restated here (one concept, one owner —
`docs/AGENTS.md` §2).

---

# 4. GET /api/battle/{battleId}/result

```json
Response 200:
{
  "battleId": "string",
  "outcome": "victory" | "defeat",
  "rewards": {
    "playerXpGained": 100,
    "newPlayerXp": 100,
    "playerLeveledUp": false,
    "newPlayerLevel": 1,
    "petXpGained": 100,
    "newPetXp": 100,
    "petLeveledUp": false,
    "newPetLevel": 1
  },
  "durationTurns": 0
}
```

```json
Response 404: { "error": "BATTLE_NOT_FOUND" }
```

```json
Response 401: { "error": "UNAUTHENTICATED" }
```

Only returns data for a battle that has already ended (`BattleWon` /
`BattleLost` emitted — `GAME_EVENTS.md`). While a battle is active, its
state is only available via the SignalR connection, not this endpoint.

**Contract notes:**

1. **`rewards` is always present** in this response — for both `"victory"`
   and `"defeat"`, never absent or optional. Its value is
   `BattleResult.RewardSummary` exactly as `DATABASE.md` §1 documents it
   (the 8-member structure serialized for **both** outcomes —
   `playerXpGained = 100` and `petXpGained = 100` on `"victory"`,
   `playerXpGained = 0` and `petXpGained = 0` on `"defeat"`, with
   `playerLeveledUp`/`petLeveledUp` `false` and unchanged XP/Level values on
   defeat). The `RewardSummary` member list is owned by `DATABASE.md` §1,
   "Reward semantics for `RewardSummary`": the **Player track**
   (`COMBAT_RULES.md` §7 — `BattleWon` grants `+100` Player XP, `BattleLost`
   `+0`) and the **Pet track** (`PET_RULES.md` §5.3–§5.5 — `BattleWon` grants
   the active combat Pet `+100` Pet XP, `BattleLost` `+0`, every inactive owned
   Pet `+0`, hard-capped at 4900).
2. **`battleId` is the result row's primary key** — `BattleResultId` is
   the battle's own `BattleId`, one row per battle (`DATABASE.md` §1).
3. **Event vs REST:** `GAME_EVENTS.md` §2 documents the reward summary as
   `BattleWon`-only because that rule governs the **event payload**; this
   endpoint's `rewards` field covers both outcomes per note 1. The two
   documents describe **one** `RewardSummary` contract, owned by
   `DATABASE.md` §1: neither the event payload nor this response defines a
   member list of its own, and neither contradicts the other on the value
   that is in force (the symmetrical 8-member structure for both outcomes).
4. **`outcome` values** — `"victory"` | `"defeat"`. The value set and its
   semantics are owned by `GAME_EVENTS.md` §2 (BattleWon / BattleLost);
   the persisted `BattleResult.Outcome` (`DATABASE.md` §1) and the
   SignalR wire member (`SIGNALR_PROTOCOL.md` §3.2.19) use the same two
   values for the same battle.
5. **`durationTurns` is `BattleResult.DurationTurns`** — see `DATABASE.md`
   §1, "Duration and completion sourcing for `BattleResult`", for the
   derivation (source, terminal-Turn rule, and edge values).
6. **Authentication is required, and the unauthenticated response is
   explicit.** (TASK-051) This endpoint is subject to §1's global rule that
   all endpoints except `/api/auth/register` and `/api/auth/login` require an authenticated session
   (ADR-020). A caller that presents no authenticated session receives
   `401` with the §6 envelope and the error code `UNAUTHENTICATED` — **not**
   `404 BATTLE_NOT_FOUND`. `BATTLE_NOT_FOUND` describes only the case of an
   authenticated caller asking for a battle result that does not exist or is
   not theirs (note 7); using it for an unauthenticated caller would make an
   authorization failure indistinguishable from a missing row and would
   confirm nothing about the battle's existence. The **mechanism** that
   establishes and validates the session is defined in §2.8 (`ADR-015`):
   a missing, invalid/tampered, or expired session all resolve to this same
   `401 UNAUTHENTICATED` response, with no distinct code and no
   token-validation detail disclosed. This note fixes the *outcome* for an
   unauthenticated caller, and the invariant that such a caller never
   receives `BattleResult` data.
7. **Only the authenticated owner may read a result.** (TASK-051) The caller
   may read a `BattleResult` only when the identity resolved from their
   authenticated session equals `BattleResult.PlayerId` (whose value comes
   from `BattleState.PlayerId` — `DATABASE.md` §1). A caller requesting a
   battle they do not own receives the same `404 BATTLE_NOT_FOUND` as a
   battle that does not exist, so the endpoint never discloses the existence
   of another Player's battle. **Ownership is never established from
   client-supplied input:** no `playerId` request member, query parameter,
   header, or body field may select, override, or stand in for the caller's
   identity (`GAME_RULES.md` §18, ADR-001, ADR-014). The authenticated
   identity is derived server-side from the session (§1, ADR-007 item 4) and
   is never re-derived from client input at read time (§2.8).

## 4.5 GET /api/battle/history

The battle-history read. It returns the authenticated Player's completed
battle results, most recent first. Its response contract is defined here
and nowhere else (one concept, one owner — `docs/AGENTS.md` §2); the
persisted row it reads is `BattleResult` (`DATABASE.md` §1), and the
contract is a projection of that row plus the ordering rule below.

```json
Response 200:
[
  {
    "battleId": "string",
    "outcome": "victory" | "defeat",
    "rewards": {
      "playerXpGained": 100,
      "newPlayerXp": 100,
      "playerLeveledUp": false,
      "newPlayerLevel": 1,
      "petXpGained": 100,
      "newPetXp": 100,
      "petLeveledUp": false,
      "newPetLevel": 1
    },
    "durationTurns": 0,
    "completedAt": "string"
  }
]
```

```json
Response 401: { "error": "UNAUTHENTICATED" }
```

**Contract notes:**

1. **The response is a bare JSON array — never a wrapper object.** No
   `{ "battles": [...] }` envelope, no `total`, no `nextCursor`, and no
   other metadata member exists. The array **is** the response body.

2. **Each element is the full §4 result shape.** The four members `battleId`,
   `outcome`, `rewards`, and `durationTurns` carry exactly the same meaning,
   source, type, and value vocabulary as the `GET /api/battle/{battleId}/result`
   200 response (§4 and its notes 1–5) — they are **not** restated here, and no
   reduced history-summary member list exists. A history element is a §4 result
   object plus exactly one further member (note 3).

3. **`completedAt` is the history element's own additional member.** It is
   `BattleResult.CompletedAt` — the server clock reading captured on the
   battle-end path (`DATABASE.md` §1, "Duration and completion sourcing for
   `BattleResult`" item 2). It is server-authoritative and is never
   client-sourced or re-derived. The §4 single-result response does **not**
   expose this member; this endpoint does, because it is the member that makes
   the ordering in note 4 observable to a client. This is an additive,
   deliberate extension of the §4 shape for the history element only, and it
   does not change §4. Its type and exact serialization are the persisted
   column's, whose exact column type `DATABASE.md` §1 explicitly leaves to
   implementation; no timezone is asserted (`DATABASE.md` §1 item 2).

4. **Ordering — most recent first, and it is part of this contract.** Elements
   are ordered by `CompletedAt` **descending** (newest completed battle first).
   When two results share a `CompletedAt`, the tie is broken by `BattleResultId`
   **descending** (the higher `BattleResultId` first), so the total order is
   deterministic even though `DATABASE.md` §1 does not require `CompletedAt` to
   be unique. `BattleResultId` is the battle's own `BattleId` and is unique per
   row (`DATABASE.md` §1). **Clients MAY rely on this ordering** — it is a
   documented contract, not an incidental storage order, and the
   `BattleResult(PlayerId, CompletedAt DESC)` index (`DATABASE.md` §4) has this
   endpoint as its documented consumer. This ordering is deliberately **not**
   §5.5's: that subsection's "ordering none defined" line is scoped by its own
   heading to §5.1 / §5.3 / §5.4 and does not govern this endpoint.

5. **No pagination in MVP — the array is the full history.** This endpoint
   accepts no `page`, `limit`, `offset`, `cursor`, or any other bounding
   parameter, and the response carries no `total` or `nextCursor` member (note
   1). The response is the authenticated Player's **complete** history, however
   long it is; an unbounded array is the accepted MVP contract, and any cap or
   bounding is deferred to post-MVP. This matches §5.5's "none in MVP" pagination
   convention, whose pagination statement is the one part of §5.5 that is not
   scoped away by note 4.

6. **No filter, sort, or search parameter is accepted.** There is no `bossId`
   filter, no `outcome` filter, no date-range filter, no sort parameter, and no
   search parameter. The endpoint has no query parameters at all. Selecting a
   subset of history is not part of the MVP contract.

7. **Authentication is required, and the unauthenticated response is
   explicit.** This endpoint is subject to §1's global rule that all endpoints
   except `/api/auth/register` and `/api/auth/login` require an authenticated session (`ADR-020`,
   `ADR-015`). A caller presenting no authenticated session receives
   `401` with the §6 envelope and the error code `UNAUTHENTICATED` — exactly as
   §4 note 6 establishes, and never a `404`. The §2.8 failure contract applies
   unchanged: a missing, invalid/tampered, or expired session all resolve to
   this same response, with no distinct code and no token-validation detail
   disclosed.

8. **History is scoped to the authenticated PlayerId, derived server-side.**
   The caller reads only their own `BattleResult` rows: the identity resolved
   from the authenticated session must equal `BattleResult.PlayerId` (whose
   value comes from `BattleState.PlayerId` — `DATABASE.md` §1, `ADR-014`).
   **`PlayerId` is never client-supplied:** no `playerId` request member, query
   parameter, header, or body field may select, override, or stand in for the
   caller's identity, and there is no route parameter for ownership — reading
   another Player's history is not expressible in this contract
   (`GAME_RULES.md` §18, `ADR-001`, `ADR-014`; same rule as §4 note 7).
   Because the scope is fixed by the session, the endpoint discloses nothing
   about whether another Player has any history.

9. **Empty history is `200` with an empty array.** A player with no completed
   battles receives `200` and `[]` — not `404`, not `204`, and not an error.
   This matches §5.5's "empty collection `200` with `[]`" convention.

10. **A still-active battle never appears.** Only durable `BattleResult` rows
    are returned, and such a row exists only after terminal persistence
    (`DATABASE.md` §1) — the same boundary §4 states when it says it "only
    returns data for a battle that has already ended". While a battle is
    active, its state is available only via the SignalR connection, not through
    this endpoint.

11. **A battle whose durable result write failed is simply absent.** If the
    terminal `BattleResult` write does not succeed, there is no history entry —
    no error member, no placeholder, and no partial or pending entry. Absence is
    reported as absence, consistent with §4 and `DATABASE.md` §1's sourcing
    rules. There is no `pending`, `partial`, or `failed` history record of any
    kind.

12. **No Boss or Pet identifying member is exposed.** Neither
    `bossDefinitionId`/`bossId` nor `petInstanceId`/`petId` is a member of a
    history element, even though `BattleResult` persists both
    (`DATABASE.md` §1, §2). The element member set is exactly the five members
    in the response block above and no more.

13. **This endpoint is REST-only and introduces no other contract.** It adds no
    Battle Event, no SignalR method, member, or event, and no Redis key: it
    reads durable `BattleResult` data through the existing PostgreSQL
    persistence boundary (`ARCHITECTURE.md` §3) and nothing else. No new
    database column, table, or index is required — the contract is served
    entirely by the existing `BattleResult` row and the existing
    `BattleResult(PlayerId, CompletedAt DESC)` index (`DATABASE.md` §1, §4).

---

# 5. GET /api/pets, /api/pets/{petId}, /api/cards, /api/relics

The four collection read endpoints. Ownership comes solely from the
authenticated session (§1, §2.8): a caller reads only their own collection,
and no request member, query parameter, or header selects a `playerId`.
`401 UNAUTHENTICATED` applies as in §1 and is not repeated below. Response
shapes are fixed per endpoint — which persisted members are exposed is
stated per route (TASK-070 human decisions D1–D4, derivations D5–D7).

## 5.1 GET /api/pets

```json
Response 200:
[
  {
    "petId": "string",
    "identity": "Xích Lang",
    "element": "Fire",
    "tier": "Common",
    "star": 1,
    "level": 12
  }
]
```

The member list above is binding and exhaustive:

```text
member    type    source / semantics
petId     string  Pet.PetInstanceId — the owned instance; the same id
                  submitted as `petId` to POST /api/battle/start (§3)
identity  string  PetDefinition.Identity
element   string  PetDefinition.Element — "Fire" | "Water" | "Earth" |
                  "Wood" | "Metal" (ELEMENT_RULES.md §1)
tier      string  Pet.Tier
star      int     Pet.Star
level     int     Pet.Level
```

The Vietnamese Element names (`Mộc`, `Hỏa`, `Thổ`, `Kim`, `Thủy` —
`ELEMENT_RULES.md` §1) are display values only — presentation text,
never API wire values.

**The value set above is the Element wire value set for this document's whole
REST surface, and it is defined here once.** `element` is a wire member
wherever an Element is serialized into a REST request or response body — the
§5 collection responses, and `POST /api/battle/start`'s
`initialState.petState.element` / `initialState.bossState.element` (§3) — so
every one of them carries the English form bound above. The set is stated
only in this section; no other section, endpoint, or document restates it
(one concept, one owner — `docs/AGENTS.md` §2).

**It does not bind the Redis record's encoding.** The active-state record's
Element representation is owned by `REDIS_STATE.md` §2, which is the
serialization contract `GAME_STATE.md`'s stated purpose points at; that
document states whether its encoding is bound or free, and this section does
not decide it.

Persisted but **not** exposed: `xp`, `acquiredAt`, `playerId`,
`petDefinitionId` (`DATABASE.md` §1).

## 5.2 GET /api/pets/{petId}

```json
Response 200:
{
  "petId": "string",
  "identity": "Xích Lang",
  "element": "Fire",
  "tier": "Common",
  "star": 1,
  "level": 12
}
```

200 is the same object as one `/api/pets` array element (§5.1) — no wrapper.
A `petId` that does not exist and a `petId` owned by another Player return
the identical response, so the endpoint never discloses whether a Pet
exists (same rationale as §4 note 7):

```json
Response 404:
{ "error": "PET_NOT_FOUND", "message": "human-readable detail" }
```

## 5.3 GET /api/cards

```json
Response 200:
[
  {
    "cardId": "string",
    "name": "string",
    "category": "Basic"
  }
]
```

```text
member    type    source / semantics
cardId    string  CardDefinition.CardDefinitionId — the id submitted in
                  `cardLoadout` (§3)
name      string  CardDefinition.Name
category  string  CardDefinition.Category — "Basic" | "PetSkill"
                  (CARD_RULES.md §1, DATABASE.md §3)
```

MVP Cards have no progression (`DATABASE.md` §2, ADR-012): presence in this
array **is** the unlocked state — there is no `unlocked` member. Persisted
or definition data but **not** exposed: `playerId`, `powerCost`,
`loadoutCopyLimit`, `effectDefinition` — §3 validates these server-side.

## 5.4 GET /api/relics

```json
Response 200:
[
  {
    "relicId": "string",
    "name": "string"
  }
]
```

```text
member   type    source / semantics
relicId  string  Relic.RelicInstanceId — the owned instance identity; the
                 value submitted in `relicLoadout` (§3) and snapshotted
                 into PetState.EquippedRelics[] at battle start
                 (RELIC_RULES.md §2.2, GAME_STATE.md §2.3)
name     string  RelicDefinition.Name
```

**Not** exposed: `playerId`, `acquiredAt`, `definitionId`, and
`Trigger`/`Condition`/`EffectDefinition` (rule texts owned by
`RELIC_RULES.md`).

## 5.5 List semantics (§5.1, §5.3, §5.4)

```text
empty collection   200 with []
ordering           none defined — clients must not rely on any order
pagination         none in MVP — no page/limit/cursor/sort/filter/search
                   parameters; the array is the full collection
```

## 5.6 No equip/loadout state

No §5 response carries `isEquipped`, `equipped`, `slot`, `loadoutPosition`,
or `active` members: equip state is battle-scoped and unpersisted
(`DATABASE.md` §2, ADR-011); it exists only in the battle-start snapshot
`PetState.EquippedCards[]` / `EquippedRelics[]` (`GAME_STATE.md` §2.3,
`RELIC_RULES.md` §2.1). Clients must not read equip state from these
endpoints.

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
