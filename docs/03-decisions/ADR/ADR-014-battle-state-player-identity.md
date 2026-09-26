# ADR-014: `BattleState.PlayerId` as Battle-End Identity Source

**Status:** Accepted
**Date:** 2026-09-26

## Context

`BattleResult` requires `PlayerId` and `PetInstanceId` (`DATABASE.md` §1),
but neither value had a documented authoritative carrier for the
battle-end persistence path: `BattleStartService` uses the requesting
`playerId` only for the pet-ownership check at battle creation and then
discards it, and the serialized `BattleState` record — the only thing that
survives to battle end — contained no identity member beyond `BattleId`.
State kept only in process memory is forbidden
(`REDIS_STATE.md` §7 item 5), and no external keyed carrier or
battle-owner store exists (`ARCHITECTURE.md` §4, `TDD.md` §2/§4). Without
a decision, TASK-041 would have to invent a carrier (`AGENTS.md` §7, §8;
TASK-042 §4.1 Gap A).

A related question: whether `PetState.PetId` (the `PetId / Identity`
staging entry) denotes the Pet **instance** (`Pet.PetInstanceId`, what
`BattleResult` needs) or a definition id, and whether a separate
`PetState.PetInstanceId` member is required.

## Decision

1. **`PlayerId` is added to `BattleState` at the root** — the identity of
   the Player who created the battle (`Player.PlayerId`), recorded from
   the authenticated battle-start request (`API_CONTRACTS.md` §1, §3) at
   battle creation, carried unchanged in the serialized record (round-trips
   with it — `REDIS_STATE.md` §2), and written to `BattleResult.PlayerId`
   on the battle-end path. Owning contract: `GAME_STATE.md` §2.8.
2. **Identity only, no combat meaning.** The Player remains the
   account/owner with no battle-time combat pool (ADR-011): `PlayerId`
   carries no stats, no resource pool, and no gameplay value; it adds no
   `PlayerState` node, no lifecycle/`Status` field
   (`GAME_STATE.md` §2.0.3), and no new `BattleResult` column
   (`DATABASE.md` §1 unchanged in shape).
3. **Not a wire member.** `PlayerId` is excluded from every client-facing
   projection: the `BattleStateUpdated` stage lists (which enumerate §2.0
   members), all Battle Event payloads, and the `GetBattleState` snapshot
   projection (`SIGNALR_PROTOCOL.md` §4 item 4, §7.1). State added does
   not add wire exposure; the client never receives it.
4. **No separate `PetState.PetInstanceId` member.** `PetState.PetId`
   already is the owned Pet **instance** identity (`Pet.PetInstanceId`);
   this denotation is now stated explicitly (`GAME_STATE.md` §2.3), and
   `BattleResult.PetInstanceId` sources from it. A second member inside
   the same record would duplicate a value the record already owns
   (`GAME_STATE.md` §0 item 5).
5. **`BattleResultId` is the battle's own `BattleId`** — one row per
   battle, PK lookup for `GET /api/battle/{battleId}/result`
   (`DATABASE.md` §1, `API_CONTRACTS.md` §4). Recorded here as the related
   identity mapping; it changes no schema, introduces no second
   identifier, and needs no separate ADR.

## Alternatives Considered

### Option A - Runtime-only identity (in-process registries)

Retain `playerId` alongside the existing in-process
`BattleStateService` registries. Rejected: `REDIS_STATE.md` §7 item 5
permits no state living only in process memory — the identity would be
lost on restart, exactly when recovery (`ADR-008`) needs it.

### Option B - External carrier keyed by `BattleId`

A documented battle-creation/lookup record supplies the identities at
battle end. Rejected: implies a new store or carrier outside the
documented battle-end lifecycle (`ARCHITECTURE.md` §4) and an extra lookup
on the result-write path (`TDD.md` §4.3) — an architecture change for no
benefit over carrying the value in the record that already exists.

### Option C - Session-derived `PlayerId` at battle end

Derive the owner from the session/actor performing the completion step.
Rejected: the value must come from authoritative battle state, never from
whatever session happens to call the completion path
(`GAME_RULES.md` §18, ADR-001, `AGENTS.md` §10); session/auth semantics
are TASK-034's scope and are untouched here.

### Option D - Separate `PetState.PetInstanceId` member

Add an explicit instance member beside `PetId`. Rejected as redundant:
`PetState.PetId` denotes the instance (Decision 4); a second member would
carry the same value twice in one record.

## Why

The serialized record is the single surviving authoritative carrier
(`REDIS_STATE.md` §2 item 2), so identity that must reach `BattleResult`
has to live in it. Placing `PlayerId` at the `BattleState` root keeps it
outside every staged gameplay subsystem (§2.0 staging is unchanged — it is
a §2 member added with its owning system) and outside the wire, satisfying
persistence sourcing and the server-authoritative rule without inventing
a store, a column, a field, or a protocol member.

## Consequences

### Positive

- TASK-041 can source `BattleResult.PlayerId` and
  `BattleResult.PetInstanceId` from documented authoritative state with no
  invented carrier and no session lookup.
- One-row-per-battle is guaranteed by construction (`BattleResultId` =
  `BattleId`), which `REDIS_STATE.md` §3 relies on when a battle-end
  delete fails: TTL-only cleanup can never yield a second result row.
- The owner's identity is never exposed to any client (no protocol
  change; `SIGNALR_PROTOCOL.md` §3.2.19 note 3 still holds).

### Negative

- The serialized record gains one member: the Domain `BattleState` and
  `BattleStateJson` must add `playerId` when implemented — an
  implementation prerequisite recorded for TASK-041's re-audit, not part
  of this ADR.
- If the explicit battle-end delete fails, no retry path exists; the key
  lingers until its TTL. Re-emitting `BattleWon` / `BattleLost` for a
  retained key inside that window is an accepted, documented gap —
  reported by TASK-042 as a follow-up concern.

### Trade-offs

- The sliding TTL remains the only cleanup for a failed delete; accepted
  for MVP in exchange for introducing no worker, queue, or retry
  infrastructure (`ARCHITECTURE.md` §5 anti-overengineering,
  `MVP_SCOPE.md`).

## Related Documents

- `GAME_STATE.md` §2.8 (owning contract), §2.0.3 (staging), §2.3
  (`PetId` denotation), §2 tree
- `DATABASE.md` §1 — `BattleResult` identity/reward sourcing
- `REDIS_STATE.md` §2 (round-trip), §3 (TTL / failed-delete behaviour)
- `API_CONTRACTS.md` §1, §3 (requesting Player), §4 (result contract)
- `SIGNALR_PROTOCOL.md` §4 item 4, §7.1 (wire exclusion)
- `ADR-011` — Player = account owner (identity-only consequence)
- `ADR-010` — precedent: missing BattleState member resolved ADR-first
- `ADR-005`, `ADR-006`, `ADR-008` — storage and recovery boundaries
- TASK-042 (decision task), TASK-041 (blocked consumer — must not be
  edited by it), TASK-033 (`RewardSummary` member list remains owned
  there), TASK-034 (session/auth — out of scope)
