# ADR-028: Retirement of Special Gems and Match-Tier Multipliers from Battle-State Model and Realtime Contracts

**Status:** Accepted
**Date:** 2026-10-17
**Deciders:** Operator (Product Owner), Backend Architecture, Client Architecture, Systems Designer
**Consulted:** AGENTS.md, docs/AGENTS.md, GAME_RULES.md, MATCH3_RULES.md, COMBAT_RULES.md, PASSIVE_RULES.md, GAME_STATE.md, SIGNALR_PROTOCOL.md, REDIS_STATE.md, ADR-001, ADR-004, ADR-005, ADR-026, ADR-027

## Context

Under operator design decisions OD-1 through OD-7 and reconciliation decision B-1 (recorded in TASK-286 and finalized under TASK-286/TASK-287):
1. Special Gems are removed entirely from gameplay (OD-1).
2. Special Gem creation, placement, activation, chain reactions, area-of-effect clearing, and detonations are retired (OD-2).
3. All match-tier resource multipliers are retired (OD-3).
4. Base resource contribution equals `BaseOutput(type) × unique confirmed matched-gem count(type)` (OD-4, OD-7).
5. Attack qualification depends strictly on the acting actor's confirmed valid ATK-gem match, not on cleared-gem counts (OD-6).
6. Boss AI scoring becomes count-only: `weight × match.Cells.Count` (B-1).

While the four game-design documents (`GAME_RULES.md`, `MATCH3_RULES.md`, `COMBAT_RULES.md`, `PASSIVE_RULES.md`) have been brought into full compliance with OD-1–OD-7, the technical design documents (`docs/02-technical/GAME_STATE.md`, `docs/02-technical/SIGNALR_PROTOCOL.md`, `docs/02-technical/REDIS_STATE.md`), backend runtime types, wire payloads, and frontend deserialization models still define Special Gem constructs:
- `BoardState.Cells[64]` contains an optional `SpecialGem` metadata object (`Type` and `Orientation`).
- SignalR `MatchCreated` event carries an optional `createdSpecialGems` array.
- SignalR `CellPayload` carries an optional `specialGem?: SpecialGemPayload | null` field.
- Redis `BattleStateJson` persists `cell.specialGem` inside the serialized board array.
- REST battle snapshot responses return `BoardState` cells with optional `SpecialGem`.
- Client presentation (`BoardView`, `GemView`, `BattleEventPresenter`) models and renders Special Gems.

An architectural decision is required under `AGENTS.md` §18 to formalize the retirement of Special Gems from technical contracts, define serialization and wire changes, establish the tolerance and deployment policy, and mandate the required gating for runtime implementation.

## Decision

1. **Retire Special Gems from the Authoritative Battle-State Model (`GAME_STATE.md`):**
   - `BoardState.Cells[64]` becomes a pure collection of 64 cell entries, where each entry represents only its `GemType`. The optional `SpecialGem` member is retired.
   - Types `SpecialGem`, `SpecialGemType`, `SpecialGemOrientation`, and transient planning structure `SpecialGemClaim` are retired from the Domain model.
   - Transient Resolution State (`GAME_STATE.md` §3) drops `CreatedSpecialGems` and `ConsumedSpecialGems` bookkeeping.

2. **Retire Special Gems from SignalR Wire Contracts (`SIGNALR_PROTOCOL.md`):**
   - `MatchCreated` (`SIGNALR_PROTOCOL.md` §3.2.6): The `createdSpecialGems` member is retired and removed from the payload schema.
   - `CellPayload` (`SIGNALR_PROTOCOL.md` §3.2.13): The `specialGem` field is retired. A cell is serialized solely with its `gemType`.
   - `SIGNALR_PROTOCOL.md` §3.2.11 (`createdSpecialGems` entry shape) is retired.
   - Wire event emission remains strictly server-authoritative (`ADR-001`).

3. **Redis Persistence Contract and Tolerant-Reader Basis (`REDIS_STATE.md`):**
   - Active battle state JSON in Redis no longer persists `specialGem` within cell entries.
   - **Tolerance basis (verified under Decision B-2):** `BattleStateSerializer` configures `WriteIndented = false` without `JsonUnmappedMemberHandling.Disallow`, and `BattleStateJson` declares zero `[JsonRequired]` properties. Consequently, stored Redis battle records carrying legacy `cell.specialGem` entries deserialize without error and the obsolete member is safely ignored.
   - **Deployment Policy (approved under Decision B-2):** Active battles must be drained before deploying server and client together. Do NOT mutate, discard, or migrate Redis state. Rollback is forward-only for boards already written without the member.

4. **REST Payloads and Serialization:**
   - REST endpoints delivering battle snapshots (`GET /api/battle/{battleId}`) project `BoardState` cells without Special Gem metadata.
   - Deserializers in external or web clients follow tolerant-reader principles and ignore obsolete fields.

5. **Frontend Presentation and Deserialization:**
   - Client TypeScript interfaces (`SpecialGemPayload`, `SpecialGemType`, `SpecialGemOrientation`) and `createdSpecialGems` on `GemMatchedEventPayload` are retired.
   - Presentation logic in `BoardView`, `GemView`, and `BattleEventPresenter` removes Special Gem visual indicators (Burst Gem glow, Line Clear directional orientation, Area Gem badge).

6. **Compatibility & Versioning:**
   - Per `SIGNALR_PROTOCOL.md` §8, no wire protocol version negotiation exists. Server and client are deployed together following the battle-drain policy.

7. **Runtime Implementation Gates:**
   - Production runtime code in `src/backend/` and `src/frontend/client/` must NOT be modified until the technical documentation task (TASK-289) updates `docs/02-technical/` and passes review (`AGENTS.md` §17, §18).
   - Implementation slices are divided into:
     - Technical contract reconciliation (`docs/02-technical/`).
     - Independent ATK-match qualification gate (`Combat/AttackResolution.cs`, `BattleStateService.cs`).
     - Compile-coupled Domain and API Special Gem removal (`GameServer.Domain` and `GameServer.Api`).
     - Frontend presentation and deserialization cleanup (`src/frontend/client`).

## Consequences

- **Positive:**
  - Complete alignment between game design (OD-1–OD-7) and technical contracts.
  - Simplification of board representation, serialization payloads, and event streams.
  - Zero Redis data migration overhead thanks to tolerant-reader serialization.
  - Clean separation between documentation reconciliation and runtime code modifications.

- **Negative / Trade-offs:**
  - Coordinated deployment required (battle drain before joint server/client deployment).
  - Backend Domain and API removal is compile-coupled due to shared `SpecialGem` types in controllers/DTOs, requiring joint backend implementation slices.
