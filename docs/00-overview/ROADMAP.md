# Roadmap

**Version:** 1.0
**Status:** Planning

> This document answers: **"What are we planning to build, and in what
> order?"** It does not define detailed rules — see `MVP_SCOPE.md` for
> IN/OUT/FUTURE classification and the domain rule docs for exact mechanics.

---

# 1. Phases

## Phase 0 — Design Foundation (current)
```text
Status: IN PROGRESS
```
- GDD, GAME_RULES, and all domain rule documents defined and standardized.
- MVP_SCOPE locked.
- Technical docs (TDD, ARCHITECTURE, GAME_STATE, GAME_EVENTS,
  API_CONTRACTS, SIGNALR_PROTOCOL, REDIS_STATE, DATABASE) drafted.

## Phase 1 — Core Loop Vertical Slice
```text
Goal: One playable battle, start to finish, against one Boss.
```
- Match-3 board (swap, match, cascade, combo) — `MATCH3_RULES.md`
- One Pet fully implemented (Element, Passive, Signature Skill)
- 3 Basic Cards
- Damage pipeline incl. Element Modifier — `COMBAT_RULES.md`,
  `ELEMENT_RULES.md`
- One Boss (Passive + Skill)
- Server-authoritative resolution over SignalR
- No Relics yet, no rewards/persistence beyond a single battle

## Phase 2 — Content Complete (MVP)
```text
Goal: Full MVP scope, see MVP_SCOPE.md §1.
```
- All 5 Pets (including the 2 Signature Skills not yet content-defined —
  see PET_RULES.md §8 note)
- All 5 Bosses (2 not yet content-defined — see BOSS_RULES.md §6 note)
- All ~10 Relics + trigger/stacking system
- Pet Tier / Star / Level progression
- Persistent storage: Pet Collection, Battle Results, Rewards
  (`DATABASE.md`)
- Active battle state in Redis (`REDIS_STATE.md`)

## Phase 3 — Discord Activity Integration & Polish
```text
Goal: Shippable inside Discord.
```
- Discord Activity SDK integration on the client
- Reconnect / resync behavior (`SIGNALR_PROTOCOL.md`)
- Meta progression UI (Pet Collection, Card Collection, Relic Collection)
- Balance pass on all configurable values

---

# 2. Post-MVP — Future Direction (Not Designed)

Per `MVP_SCOPE.md` §3, the following are direction only — no rules exist yet
and none may be implemented without first updating `MVP_SCOPE.md`:

```text
MVP
 ↓
More Pets
 ↓
More Cards / Relics
 ↓
Boss Phases
 ↓
Map (gameplay-affecting)
 ↓
Environment
 ↓
Dynamic Board
 ↓
Advanced Element System (e.g. Tương Sinh)
 ↓
PvP / Multiplayer
```

Each step above requires its own design pass and Rule Change Policy approval
(`GAME_RULES.md` §20) before it can move from this list into `MVP_SCOPE.md`
§1 (IN).

---

# 3. Non-Goals (for any phase currently planned)

```text
Gacha, Guild, Trading, complex equipment, microservices, Kubernetes, Kafka
```

These are not on this roadmap at all — see `MVP_SCOPE.md` §2 for the full
exclusion list.
