---
name: client-state-authority
description: "Use this skill to audit, enforce, and maintain server-authoritative boundaries on the frontend in DCacti. Strictly prohibits client-side game logic, combat formulas, RNG, match resolution, and authoritative state authoring."
---

# skills/client/client-state-authority.md — Skill: Client State Authority & Server Compliance

**Version:** 1.0  
**Status:** Binding  
**Scope:** Strict enforcement of the server-authoritative rule across all frontend code in DCacti.

> Enforces ADR-001 and `GAME_RULES.md` §18 on the client, ensuring the frontend acts strictly as a presentation and interaction layer and never computes authoritative gameplay rules, values, or RNG.

---

## Purpose

1. Prohibit any authoritative gameplay simulation logic on the client.
2. Clearly distinguish between server-authoritative state, optimistic UI feedback, and presentation state.
3. Prevent subtle cheating vectors, synchronization drift, and logic duplication.

---

## When to Use

- Developing or reviewing any frontend code in `src/frontend/client/`.
- Implementing player input handlers (swaps, pet skill activations, card casts).
- Reviewing PRs or changes that calculate numbers, HP changes, or match patterns on the client.
- Creating client state interfaces in `src/frontend/client/src/state/`.

---

## When Not to Use

- Implementing backend domain combat or match resolution logic (server Domain).
- Developing purely visual tweens, particles, or animations that do not store game state.

---

## Authoritative Sources

- `docs/01-game-design/GAME_RULES.md` §18 — Server authority principles
- `docs/02-technical/GAME_STATE.md` §2, §4 — Authoritative BattleState vs Client Presentation Mirror
- `docs/02-technical/TDD.md` §2.1 — Client responsibility boundaries
- `docs/03-decisions/ADR/ADR-001*` — Authoritative server battle engine
- `AGENTS.md` §10 — Server-Authoritative Rule

---

## The Prohibited vs Permitted Matrix

| Category | Forbidden on Client (Server Authoritative) | Permitted on Client (Presentation / Optimistic) |
|---|---|---|
| **Match-3 Board** | Authoritative match detection, 4-in-a-row detection, cascade loops, gravity falls, gem type RNG generation | Local adjacency check (dx+dy=1), visual swap tween, animation lock |
| **Combat & Stats** | Damage formulas, defense mitigation, elemental multipliers, critical hit roll, HP calculation | Floating text rendering, health bar tweening to server-provided target HP |
| **Boss Mechanics** | Boss AI decision, phase transition trigger, boss attack damage, boss status expiration | Rendering boss intent icon, animating attack sprite, updating phase visual skin |
| **Progression** | Power bar calculation, Pet passive charge thresholds, card mana costs, relic triggers | Displaying Power bar fill percentage, playing glowing effect when server emits passive ready |
| **Outcome** | Win / Loss / Draw evaluation, battle rewards, currency drop calculations | Displaying victory / defeat overlay when `BattleConcluded` event is received |
| **Randomness** | `Math.random()` or any client RNG for gameplay outcomes or gem drops | Cosmetic RNG only (e.g. random particle spray angle, ±5px text jitter) |

---

## State Classification

```text
1. Authoritative State (Server Only — Redis / Memory)
   └── Full BattleState: HP, Board gem types, turn counter, RNG seed, combat modifiers.
       NEVER stored or modified on client.

2. Presentation State (Client — GameRuntimeState.ts / View models)
   └── Synchronized mirror received via server events/snapshots. Read-only presentation copy.

3. Ephemeral Interaction State (Client — Phaser Scene memory)
   └── Selected gem index, drag offset, animation lock boolean, camera shake state.
       Discarded when scene resets.
```

---

## Audit Checklist for Client Code

When implementing or reviewing frontend features:

- [ ] Does any code in `client/` calculate `Damage = ATK * Multiplier - DEF`? → **REJECT (Defect)**
- [ ] Does any code in `client/` determine which gems match and remove them before server response? → **REJECT (Defect)**
- [ ] Does any code in `client/` use `Math.random()` to pick a gem color or decide a hit/miss? → **REJECT (Defect)**
- [ ] Does `GameRuntimeState.ts` contain only technical session data and presentation mirrors? → **PASS**
- [ ] Are player actions sent as requests (`SendSwap`, `CastSkill`) rather than direct state mutations? → **PASS**

---

## Do / Don't

| Do | Don't |
|---|---|
| Send player actions as intent commands to the server. | Update player HP or board state optimistically without server confirmation. |
| Use cosmetic randomness only for visual flair (particle scatter, audio pitch variation). | Use client randomness to determine loot, damage, critical hits, or gem spawns. |
| Revert invalid swaps with a smooth return tween when server returns error. | Crash or desynchronize if the server rejects a player swap. |

---

## Stop Conditions

- A task or implementation requirement asks the client to authoritatively resolve game rules, damage, or state.
- Documentation appears to suggest client-side authoritative calculation (report conflict per `AGENTS.md` §4).

---

## Traceability

- **Reads:** `docs/01-game-design/GAME_RULES.md`, `docs/02-technical/GAME_STATE.md`, `docs/02-technical/TDD.md`, `docs/03-decisions/ADR/ADR-001*`, `AGENTS.md`
- **Used by:** `client` agent, `review` agent, `gameplay` agent, `orchestrator` agent
- **Related Skills:** `client/client-event-projection`, `gameplay/authority-determinism-audit`, `quality/architecture-conformance`
