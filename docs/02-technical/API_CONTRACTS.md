# API Contracts

**Version:** 1.0
**Status:** Draft

> This document answers: **"How does the client communicate with the server
> over REST?"** In-battle realtime actions (Swap, Card Cast, Pet Skill Cast)
> and Battle Events are NOT here — see `SIGNALR_PROTOCOL.md`. This document
> does not redefine game rules; it references the owning domain document for
> validation logic.

All endpoints (except `/api/auth/discord`) require an authenticated
session established via Discord Activity authentication (`ADR-007`).

---

# 1. Endpoint Summary

```text
Method  Path                          Purpose
------  ----------------------------  ------------------------------------
POST    /api/auth/discord             Exchange Discord OAuth code for an
                                        authenticated application session
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

---

# 2. POST /api/auth/discord (Authentication Boundary)

Architectural boundary for establishing an authenticated application session
from a Discord Activity client.

```text
Frontend (Discord SDK)
         │  (authorization code)
         ▼
POST /api/auth/discord
         │  (server-side token exchange using Discord Client Secret)
         ▼
Discord OAuth API
         │  (Discord user identity)
         ▼
Application Session (Player authenticated)
```

1. **Responsibility:**
   - Frontend (`DiscordService.ts`) obtains an authorization code via the
     Discord Embedded App SDK.
   - Frontend sends the code to `POST /api/auth/discord`.
   - Backend performs server-side verification and token exchange with the
     Discord OAuth API using the `Discord Client Secret`.
   - Backend matches or creates the persistent `Player` entity (`DATABASE.md`),
     issuing an authenticated application session (e.g. JWT token or session
     cookie).
2. **Client Secret Security:**
   - The `Discord Client Secret` is stored exclusively on the backend
     (environment/secrets configuration).
   - The frontend never receives, stores, or transmits the Client Secret.
3. **Downstream Usage:**
   - All subsequent REST endpoints and SignalR Hub connections use this
     authenticated application session.

---

# 3. POST /api/battle/start

```json
Request:
{
  "petId": "string",
  "bossId": "string",
  "cardLoadout": ["heal", "shield", "power_charge"],
  "relicLoadout": ["relic_id_1", "relic_id_2", "relic_id_3"]
}
```

Validation (delegates to domain rules, does not reimplement them):

```text
petId must be owned by the player                        — PET_RULES.md §2
cardLoadout must be exactly 3 Basic Cards                  — CARD_RULES.md §1
relicLoadout must be 3–5 Relics owned by the player          — RELIC_RULES.md §2
```

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

---

# 4. GET /api/battle/{battleId}/result

```json
Response 200:
{
  "battleId": "string",
  "outcome": "Won" | "Lost",
  "rewards": { "...": "see DATABASE.md for reward data shape" },
  "durationTurns": 0
}
```

```json
Response 404: { "error": "BATTLE_NOT_FOUND" }
```

Only returns data for a battle that has already ended (`BattleWon` /
`BattleLost` emitted — `GAME_EVENTS.md`). While a battle is active, its
state is only available via the SignalR connection, not this endpoint.

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
