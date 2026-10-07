# API Contracts

**Version:** 1.19 (§5.1's binding member list widened by one member per TASK-213
§5's Signature Skill decision, implemented by TASK-219A — `GET /api/pets` (and
therefore `GET /api/pets/{petId}`, which §5.2 makes the same object) gains
`signatureSkill`: the Pet's **derived Signature Skill reference**, carrying
`cardId` = the Pet's existing `PetDefinition.SignatureSkillCardId` FK,
`name` and `category` = that referenced `CardDefinition`'s own stored values.
The member is always present, never `null`, and never omitted, because
`SignatureSkillCardId` is a required FK and each Pet has exactly one Signature
Skill (`PET_RULES.md` §8, `CARD_RULES.md` §4 item 1). **The Signature Skill is
IDENTIFIED from the Pet that derives it and is not an owned Card**: §5.3's
membership is **unchanged and must not be widened** — a `PetSkill`
`CardDefinition` is still never a `PlayerUnlockedCard` row
(`CARD_RULES.md` §1 item 4, ADR-012 item 9), so no `PetSkill` definition reaches
`/api/cards` and this member is not delivered there; presence in that array
remains the unlocked state. §5.3 gains one sentence stating that boundary and
§5.6 gains one clause stating that `signatureSkill` is not an equip member.
**No new endpoint, request member, query parameter, header, pagination member,
cost/affordability/legality member, `effectDefinition` copy, ownership row,
schema, index, migration, Domain type, SignalR member, event, or Redis contract
is introduced** — the reference is a projection of an existing required FK and
of the definition row it already points at, and the endpoint list in §1 is
unchanged. Prior 1.18: (§5.3 / §5.4 widened additively per TASK-212A's `AMEND BOTH`
contract decision, implemented by TASK-212A-1 — the loadout decision point can
now communicate **what each Card and Relic changes**. `GET /api/cards` gains
one member, `effectDefinition`: the Card's own already-authored structured
effect array (`CardDefinition.EffectDefinition`), element for element and at
`DATABASE.md` §1's stored member names, with §1/§3's present-iff rules
preserved. `GET /api/relics` gains three: `trigger`,
`condition` (nullable, and emitted as an explicit `null` when the Relic
declares none), and `effectDefinition` — the definition's own structured
content (`RelicDefinition.Trigger`/`Condition`/`EffectDefinition`), carried
inline per owned instance exactly as `name` already is. **No new vocabulary, no
authored prose, no cost/affordability/legality member, no realtime member, no
SignalR/state/event/Redis change, no schema, index, query, migration, or Domain
change**: the values were already authored, already stored, and already loaded
by the existing repository reads, so both amendments are projection-only. §5.3's
and §5.4's membership semantics are unchanged (no `unlocked` member; a
`PetSkill` `CardDefinition` is still not an unlock row; the response stays
definition-identity-free). Prior 1.17: (§4.5 `GET /api/battle/history` response
contract defined per
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
    "level": 12,
    "signatureSkill": {
      "cardId": "card-inferno",
      "name": "Inferno",
      "category": "PetSkill"
    }
  }
]
```

The member list above is binding and exhaustive:

```text
member            type    source / semantics
petId             string  Pet.PetInstanceId — the owned instance; the same id
                          submitted as `petId` to POST /api/battle/start (§3)
identity          string  PetDefinition.Identity
element           string  PetDefinition.Element — "Fire" | "Water" | "Earth" |
                          "Wood" | "Metal" (ELEMENT_RULES.md §1)
tier              string  Pet.Tier
star              int     Pet.Star
level             int     Pet.Level
signatureSkill    object  the Pet's DERIVED Signature Skill reference —
                          PetDefinition.SignatureSkillCardId resolved through
                          the CardDefinition it references (below)
```

`signatureSkill` is the response's one non-scalar member, and its own member set
is binding and exhaustive too:

```text
member                    type    source / semantics
signatureSkill.cardId     string  PetDefinition.SignatureSkillCardId — the
                                  CardDefinitionId the Pet's Signature Skill is
                                  expressed as, and the definition the battle
                                  loadout derives its 4th equipped entry from
                                  (CARD_RULES.md §4 item 1, §3)
signatureSkill.name       string  that CardDefinition's Name
signatureSkill.category   string  that CardDefinition's Category, as the §5.3
                                  closed set spells it — "PetSkill" for a Pet's
                                  Signature Skill (CARD_RULES.md §4 item 1)
```

**`signatureSkill` is the Pet's derived Signature Skill, and it states no
ownership.** These are the rules the member fixes, and they are the whole of what
it means:

1. **It is derived from the Pet, and it is not an owned Card.** The member
   reports `PetDefinition.SignatureSkillCardId` — the existing required FK
   (`DATABASE.md` §1, `PET_RULES.md` §8) — and the definition row it references.
   A Signature Skill is never owned and never acquired: it is derived from the
   active Pet at battle start and appended to `PetState.EquippedCards[]` as the
   4th entry, after the 3 submitted Basic Cards (§3, `GAME_STATE.md` §2.3). This
   response reports the reference; it grants nothing and equips nothing.
2. **`§5.3`'s membership is unchanged, and it must not be widened.** A
   `PetSkill` `CardDefinition` is still never a `PlayerUnlockedCard` row
   (`CARD_RULES.md` §1 item 4, ADR-012 item 9), so no `PetSkill` definition
   reaches `/api/cards` and this member is **not** delivered there. Presence in
   that array remains the unlocked state; the reference is delivered by this
   section instead.
3. **It is always present, never `null`, and never omitted.** `SignatureSkillCardId`
   is a required FK and every Pet has exactly one Signature Skill
   (`CARD_RULES.md` §4 item 1), so the member has no absent or "unknown" form and
   no optional-member rule applies. A Pet whose referenced definition does not
   resolve is a broken `DATABASE.md` §1 state rather than a contract case, and is
   refused rather than answered with a fabricated or placeholder reference
   (`AGENTS.md` §7) — the same posture §5.4 takes for an unresolvable
   `RelicDefinition`.
4. **It carries no cost, no affordability state, no cast-legality judgment, and
   no `effectDefinition`.** The composed cast value is `EffectiveCardCost`
   (`CARD_RULES.md` §3.6) and belongs to the realtime projection
   (`SIGNALR_PROTOCOL.md` §4 item 15); what the Skill changes is §5.3's content
   question and is not restated or copied here. This member answers exactly one
   question: **which `CardDefinition` is this Pet's Signature Skill.**
5. **It is the client's identification source for the Signature Skill, and the
   only one.** `SIGNALR_PROTOCOL.md` §4.3 item 13 makes the client identify the
   derived `equippedCards` entry by matching it against this delivered reference,
   rather than by reading a `Category` out of a Card collection that can never
   contain a `PetSkill` row. `CardCast(<signature cardId>)` remains
   protocol-conformant and remains implemented (`SIGNALR_PROTOCOL.md` §2,
   §3.2.20); `PetSkillCast` is the canonical client request for this entry.
6. **§5.2 is the same object**, so the detail read carries the same member — no
   second shape and no wrapper.
7. **No new endpoint, request member, parameter, or definition-identity member.**
   The reference rides this response. `SignatureSkillCardId` is exposed only as
   `signatureSkill.cardId`, and no other definition identity is added — the same
   definition-identity omission §5.4 records for Relics.

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
`petDefinitionId` (`DATABASE.md` §1). `PetDefinition.SignatureSkillCardId` is
persisted and **is** exposed — as `signatureSkill.cardId` and as no other
member.

## 5.2 GET /api/pets/{petId}

```json
Response 200:
{
  "petId": "string",
  "identity": "Xích Lang",
  "element": "Fire",
  "tier": "Common",
  "star": 1,
  "level": 12,
  "signatureSkill": {
    "cardId": "card-inferno",
    "name": "Inferno",
    "category": "PetSkill"
  }
}
```

200 is the same object as one `/api/pets` array element (§5.1) — no wrapper.
It therefore carries §5.1's `signatureSkill` member too, with the same
members and the same rules; this section defines no shape of its own.
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
    "cardId": "card-heal",
    "name": "Heal",
    "category": "Basic",
    "effectDefinition": [
      { "effectType": "Heal", "valueType": "PercentMaxHp", "value": 20 }
    ]
  }
]
```

```text
member            type    source / semantics
cardId            string  CardDefinition.CardDefinitionId — the id submitted in
                          `cardLoadout` (§3)
name              string  CardDefinition.Name
category          string  CardDefinition.Category — "Basic" | "PetSkill"
                          (CARD_RULES.md §1, DATABASE.md §3)
effectDefinition  array   CardDefinition.EffectDefinition — the Card's own
                          structured effect rule, as stored; one object per
                          effect
```

**`effectDefinition` is the definition's own authored content, exposed as
stored.** It is an array of objects whose **member names are `DATABASE.md` §1
item 2's stored names** — the same convention `category` already follows by
referencing `CARD_RULES.md` §1. This section adds no vocabulary of its own and
restates neither the closed sets nor the present-iff rules:

```text
member      type    nullability / presence semantics
effectType  string  always present. The closed identity set is DATABASE.md §1
                    item 1's; the identities themselves are CARD_RULES.md
                    §2/§4.1's.
valueType   string  always present. The closed interpretation set and its
                    meaning are DATABASE.md §1 item 1 / §3's.
value       int     present iff `valueType` interprets one (DATABASE.md §3);
                    absent — not 0 and not null — for "Undetermined"
                    (CARD_RULES.md §2/§4.1 own the magnitudes)
duration    int     present iff `effectType = "Burn"`, in Turns; absent
                    otherwise (CARD_RULES.md §4.1, DATABASE.md §3)
scope       string  present iff `effectType = "Crit"`; absent otherwise
                    (CARD_RULES.md §4.1, DATABASE.md §3)
```

**Presence.** `effectDefinition` is **always present, never null, and never
empty** — `DATABASE.md` §1 stores the column NOT NULL, and a Card states at
least one effect (`CARD_RULES.md` §2/§4.1). A one-effect Card is a
one-element array (TASK-111 D-1b): there is no single-object form and no
arity-dependent shape.

**Source of truth.** `CardDefinition.EffectDefinition`, read by the repository
join this endpoint already performs (`CardRepository.ListUnlockedAsync`
joins `PlayerUnlockedCard → CardDefinition`). The response is a projection of
that row value: nothing is recomposed into prose, transformed, rounded,
defaulted, reordered, or recomputed, and no optional member is interpolated
where the payload omits it.

**Player-facing purpose.** It states **what the Card changes** — the effect
identity, its magnitude, how that magnitude is interpreted, and the
effect-specific parameter the effect defines — so the loadout decision point can
communicate a Card's meaning rather than only its name (`GDD.md` §17, §8). The
client is free to translate the tokens and parameters into display text
(§5.1's Element precedent: a closed wire token is not a display string), but it
may not state a rule the payload does not carry.

**Boundary limitations.** It carries **content only**. It is not a cost, an
affordability state, a cast-legality judgment, a `CardCostModifiers[]`
reconstruction, or a Signature Skill: the composed cast value is
`EffectiveCardCost` (`CARD_RULES.md` §3.6) and belongs to the realtime
projection, which `SIGNALR_PROTOCOL.md` §4 item 15 owns and which forbids the
client to reconstruct a cost modifier from a Card's definition. A `Damage`
element is a Card/Skill base value entering the Damage Pipeline
(`COMBAT_RULES.md` §3 step 1), not a dealt amount; a `PercentMaxHp` is a
proportion, not a resolved HP change; a `Burn` element's `value` is damage per
tick and its `duration` is a Turn count. None of these is a computed result, and
the presentation must not imply one (`GAME_RULES.md` §18, `AGENTS.md` §10).

**Membership is unchanged.** Presence in this array is still the unlocked state:
there is still no `unlocked` member, a `PetSkill` `CardDefinition` is still
never an unlock row (`CARD_RULES.md` §1 item 4, ADR-012 item 9), and the member
is definition-generic — no `PetSkill` row reaches it because none is ever an
unlock row. **The Pet's Signature Skill reference is delivered by §5.1's
`signatureSkill`, never here**, so this response gains no member for it and this
array's membership stays exactly the Player's unlock rows. Persisted or
definition data but **not** exposed: `playerId`,
`powerCost`, `loadoutCopyLimit` — §3 validates these server-side.

## 5.4 GET /api/relics

```json
Response 200:
[
  {
    "relicId": "relic-instance-1",
    "name": "Berserker Core",
    "trigger": "OnMatchCount",
    "condition": { "conditionType": "MatchCountAtLeast", "threshold": 3 },
    "effectDefinition": [
      { "effectType": "ATK", "valueType": "Percentage", "value": 5,
        "target": "Pet", "lifetime": "Battle" }
    ]
  }
]
```

```text
member            type     source / semantics
relicId           string   Relic.RelicInstanceId — the owned instance identity;
                           the value submitted in `relicLoadout` (§3) and
                           snapshotted into PetState.EquippedRelics[] at battle
                           start (RELIC_RULES.md §2.2, GAME_STATE.md §2.3)
name              string   RelicDefinition.Name
trigger           string   RelicDefinition.Trigger — the one primary Trigger
                           identity; the closed value set is RELIC_RULES.md §3's
condition         object?  RelicDefinition.Condition — the structured form plus
                           its threshold (RELIC_RULES.md §8.1); `null` when the
                           Relic declares no extra condition
effectDefinition  array    RelicDefinition.EffectDefinition — the Relic's own
                           structured effect rule, as stored; one object per
                           effect (RELIC_RULES.md §8.2)
```

**The content is the definition's own authored content, exposed as stored**, and
the content member names are the definition's own storage names
(`DATABASE.md` §1) and contract names (`RELIC_RULES.md` §8.3). This section adds
no vocabulary of its own:

```text
member                       type    nullability / presence semantics
condition.conditionType      string  always present when `condition` is not null;
                                     the closed three-form set is
                                     RELIC_RULES.md §8.1's
condition.threshold          int     always present when `condition` is not null;
                                     the form's N (RELIC_RULES.md §8.1 item 1)
effectDefinition[].effectType  string  always present; the closed identity set is
                                       RELIC_RULES.md §8.2 item 1's
effectDefinition[].valueType   string  always present; the interpretation is
                                       fixed per `effectType` by RELIC_RULES.md
                                       §8.2 item 2 / §8.3's table
effectDefinition[].value       int     present iff `valueType` interprets one
                                       (RELIC_RULES.md §8.2 item 3); absent —
                                       not 0 and not null — for "Undetermined"
effectDefinition[].target      string  always present; `RELIC_RULES.md` §8.3
                                       item 1 defines "Pet" as the only target
effectDefinition[].lifetime    string  always present; the closed set and the
                                       allowed per-`effectType` combination are
                                       RELIC_RULES.md §8.3 item 2 / §8.4's
```

**Presence.** `trigger` is **always present and never null** (`DATABASE.md` §1
stores it NOT NULL). `condition` is **nullable — the one genuinely optional
member** — and is emitted as an explicit `null`, not omitted, when the Relic
declares no extra condition (`RELIC_RULES.md` §8.1 item 4): `null` is already
the storage's own spelling of "no condition" (`DATABASE.md` §1), and omission
would be ambiguous between "none" and "unsupported". `effectDefinition` is
**always present, never null, and never empty** (`DATABASE.md` §1 stores the
column NOT NULL; every Relic states at least one effect).

**Source of truth.** `RelicDefinition.Trigger` / `Condition` /
`EffectDefinition`, read by the bulk definition read this endpoint already
performs to resolve `name` (`RelicRepository.ListDefinitionsAsync`). Nothing is
derived from the Relic's `Name`, from `RelicDefinitionId`, from an instance id,
from a category, or from any client-side heuristic — `RELIC_RULES.md` §8.2 item
1 forbids exactly that — and no optional member is defaulted or filled in.

**Delivered per owned instance.** The content is resolved through the instance's
definition reference, exactly as `name` already is, so two owned instances of
one definition repeat the content and remain two elements with distinct
`relicId`s. The response carries **no definition-identity member** on purpose:
`relicId` stays the instance identity §3 submits, and exposing the definition id
would invite a client-side definition→content catalog, which would be a second
source of truth (`AGENTS.md` §7, `GAME_STATE.md` §0 item 5).

**Player-facing purpose.** It states **what the Relic changes and when** — the
Trigger that makes it react, the threshold the reaction waits for, the effect
identity, its magnitude and interpretation, and the target and lifetime the
effect contract requires — so the loadout decision point can communicate a
Relic's meaning rather than only its name (`GDD.md` §17, §10). As in §5.3, the
client may translate the tokens into display text but may not state a rule the
payload does not carry.

**Boundary limitations.** It carries **content only**: no acquisition,
ownership, or unlock semantics (array membership already expresses ownership,
and `PlayerId`/`AcquiredAt` are not exposed), no equip/loadout member (§5.6), no
Relic gameplay, and no per-Relic `Reset`/`Cooldown` value — `DATABASE.md` §1
stores none and `RELIC_RULES.md` §8.4 item 4 adds no per-Relic cooldown, charge,
or reset state. It delivers nothing on the SignalR wire: `RelicTriggered` keeps
its `{ type, relicId }` shape (`SIGNALR_PROTOCOL.md` §3.2.23).

**Not** exposed: `playerId`, `acquiredAt`, `definitionId`.

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
endpoints. **`signatureSkill` (§5.1) is not an equip member**: it reports the
Pet's own derived Signature Skill reference, which follows the Pet rather than
any selection, and it names no slot, no position, and no equipped set.

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
