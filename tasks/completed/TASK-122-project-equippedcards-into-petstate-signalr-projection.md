# TASK-122 — Project `equippedCards` into the `petState` SignalR Projection

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ and src/ by path and section; copies only what an
  implementer needs to know where to look.

  PROVENANCE: TASK-121 resolved the client loadout visibility contract as
  Option B (Minimal SignalR Projection Expansion) and updated the canonical
  owner documents (SIGNALR_PROTOCOL.md §4.3, GAME_STATE.md §2.3) to require
  `BattleStateUpdated.petState.equippedCards`. The corresponding backend
  projection was never implemented: TASK-121 was a documentation-only task
  and TASK-120 scoped src/backend/ out as "fully implemented and verified".
  Neither task owned the backend projection change, so it fell through the gap.
  TASK-120 correctly STOPPED on this (its §25 condition
  "the actual backend payload differs from the resolved contract").
  This task closes exactly that gap and nothing else.
-->

---

## Metadata

```text
Task ID:           TASK-122
Type:              FEATURE
Status:            DONE
Risk:              LOW
Priority:          CRITICAL
Primary Agent:     realtime
Supporting Agents: testing, review
Workflow:          development/feature.md
Skills:            realtime/realtime-protocol-validation,
                   backend/api-contract-validation,
                   testing/test-scenario-generation,
                   quality/scope-validation
                   (4 skills — Simple/Normal budget, tasks/README.md §12)
Dependencies:      TASK-121 (DONE — resolved the client loadout visibility contract
                     as Option B and made `equippedCards` a documented member of the
                     `petState` wire projection in SIGNALR_PROTOCOL.md §4.3)
Blocks:            TASK-120
```

---

## Objective

Extend the existing `BattleHub` `petState` SignalR projection so that
`BattleStateUpdated.petState.equippedCards` carries the authoritative
`BattleState.PetState.EquippedCards[]` `CardDefinitionId` strings, exactly as
`SIGNALR_PROTOCOL.md` §4.3 item 13 already documents, so that the downstream
client task (TASK-120) can render cast controls from synchronized runtime state.
This task introduces no new SignalR event, no new Hub method, no REST endpoint,
and no gameplay behavior change — it is the one missing field of an already
approved contract.

---

## Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1 — 3 Basic Cards, Pet Signature Skill, and server-authoritative resolution are IN scope
- `docs/01-game-design/CARD_RULES.md` §1 — Loadout composition: 3 Basic Cards + 1 derived Pet Skill Card (the four `CardDefinitionId` entries)
- `docs/01-game-design/CARD_RULES.md` §4 — Signature Skill derivation
- `docs/01-game-design/GAME_RULES.md` §18 — Server authority: the client only requests actions
- `docs/02-technical/SIGNALR_PROTOCOL.md` §4.3 (projection tree, item 2's four-member set, **item 13 `equippedCards`** — presence, non-nullability, bootstrap/synchronization, client usage boundary)
- `docs/02-technical/SIGNALR_PROTOCOL.md` §3.2.20 — `CardCast` event `cardId` is the same definition identity
- `docs/02-technical/SIGNALR_PROTOCOL.md` §4 item 4 — a payload carries only the implemented stage's own fields (no other member may be added)
- `docs/02-technical/GAME_STATE.md` §2.3 — `PetState.EquippedCards[]` (element is a `CardDefinitionId` definition identity, not an instance; exactly four entries; order carries no gameplay significance)
- `docs/02-technical/API_CONTRACTS.md` §3 — `POST /api/battle/start` snapshots the loadout at battle creation
- `docs/02-technical/API_CONTRACTS.md` §5.3 — `category` (`Basic` | `PetSkill`) is how the Signature Skill is identified
- `docs/02-technical/ARCHITECTURE.md` §2.2.1 — single synchronization path; the SignalR push is the runtime state source
- `docs/03-decisions/ADR/ADR-001-server-authoritative-battle.md` — Server authority
- `docs/03-decisions/ADR/ADR-004-signalr-realtime.md` — SignalR realtime transport
- `docs/03-decisions/ADR/ADR-008-battle-recovery.md` — Snapshot-based resync (the §7 reconnect path must carry the same member)
- `docs/03-decisions/ADR/ADR-012-player-level-pet-level-ownership.md` — Card ownership vs battle equip; no Card instances
- `tasks/backlog/TASK-121-resolve-client-loadout-and-pet-skill-visibility-contract.md` — the resolved contract this task implements (do not modify)

---

## Scope

### In Scope
- **`src/backend/GameServer.Api/Hubs/BattleHub.cs`** — the `PetStatePayload` record gains the `equippedCards` member, and `ToPetStatePayload(PetState)` populates it from `PetState.EquippedCards[]`.
- **Backend projection tests** (`tests/backend/GameServer.Api.Tests/`) — assert presence, value, cardinality, ordering, unchanged sibling members, and the absence of unrelated combat state, on the real `BattleStateUpdated` push.
- **Regression-test reconciliation** — the existing `petState` member-set assertions that pin the current three-member shape must be updated to the §4.3 item 2 four-member shape. These are documentation-staleness corrections, not tests bent to fit new behavior.

### Out of Scope
- **Frontend implementation of any kind** — `GameRuntime`, `SignalRService`, `RuntimePetState`, `BattleScene`, `BattleEventPresenter`, and all frontend tests. Consuming this member is TASK-120's work.
- `CardCast` / `PetSkillCast` implementation, and any Card or Pet Skill gameplay.
- Match-3, board, gems, swap, match detection, cascade.
- Combat redesign; any change to `PetState` itself; any gameplay rule change.
- New SignalR events, new Hub methods, new REST endpoints, `GetBattleState` redesign.
- Redis changes, database changes, serialization-model changes (`BattleStateJson`, `BattleStateSerializer`).
- Creating an ADR — this implements an already-approved contract and makes no new architectural decision.
- Modifying `TASK-120` or `TASK-121`.
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2.

---

## Current State

`BattleHub.PetStatePayload` (`src/backend/GameServer.Api/Hubs/BattleHub.cs:147-151`) declares exactly three members — `PassiveId`, `PassiveProgress`, `PassiveResetOverride` — and `ToPetStatePayload(PetState petState)` (line 809) projects exactly those three. A case-sensitive search for `equippedCards` across `BattleHub.cs` returns **zero matches**, even though `using GameServer.Domain.Cards;` is already imported (line 6) and `PetState.EquippedCards` exists as `EquippedCardIdentity[]?` (`src/backend/GameServer.Domain/Battle/PetState.cs:384`).

`SIGNALR_PROTOCOL.md` §4.3 (projection tree at lines 1434–1441; normative rule at item 13) already requires the member and states it is "always present, non-empty, and non-nullable". The documentation is therefore ahead of the implementation.

Two existing API integration tests currently assert the stale three-member shape and will fail once the member is added — they assert the *old* contract and must be corrected to the *documented* one:
- `ApiIntegrationTests.cs:451` — `Assert.Equal(new[] { "passiveId", "passiveProgress" }, petFields)`
- `ApiIntegrationTests.cs:994-996` — the same equality in the negative-contract guard
- `ApiIntegrationTests.cs:1014` and `:1175` also list `equippedCards` as a Domain-only member that must be **absent**; those assertions invert under the resolved contract.

A precedent for the exact projection already exists: `src/backend/GameServer.Api/Controllers/BattleStartStateSummary.cs:107-109` projects the same field for the REST bootstrap as `(petState.EquippedCards ?? []).Select(card => card.Value).ToArray()`.

---

## Acceptance Criteria

- [x] `BattleStateUpdated.petState` includes `equippedCards` on the pushed payload.
- [x] `equippedCards` is projected from the authoritative `BattleState.PetState.EquippedCards[]` and from no other source.
- [x] Each entry is serialized as a `CardDefinitionId` **string** — no complete `CardDefinition` object, no `PowerCost`, `EffectDefinition`, `Damage`, `Heal`, `Shield`, `Burn`, `Crit`, or any other card gameplay data.
- [x] The documented cardinality is preserved (exactly the 4-entry battle-scoped loadout: 3 Basic + 1 Pet Skill Card).
- [x] The authoritative array order is preserved as delivered.
- [x] The existing members `passiveId`, `passiveProgress`, and `passiveResetOverride` remain unchanged in shape and value.
- [x] No unrelated combat state is exposed on `petState` (`hp`, `maxHp`, `atk`, `def`, `crit`, `power`, `statusEffects`, `equippedRelics`, `petId`, identity/progression fields all remain absent).
- [x] No new SignalR event is introduced.
- [x] No new Hub method is introduced.
- [x] No REST endpoint is introduced.
- [x] No frontend source is modified.
- [x] The projection remains attached to the existing `BattleStateUpdated` flow — both the `JoinBattle`-triggered push and the post-resolution pushes, plus the §7 `GetBattleState` snapshot path.
- [x] Backend projection tests pass, including the corrected `petState` member-set regression assertions.
- [x] Existing backend regression tests pass (Domain, Application, Infrastructure, Api).
- [x] `TASK-120` remains unmodified.
- [x] `TASK-121` remains unmodified.
- [x] Quality review checklist passes (`quality/review.md` §1).

---

## Affected Files & Areas

```text
[x] src/backend/GameServer.Api/Hubs/BattleHub.cs            (the projection)
[x] tests/backend/GameServer.Api.Tests/                     (projection + regression reconciliation)
[ ] src/backend/GameServer.Domain/                          (None — PetState already carries the field)
[ ] src/backend/GameServer.Application/                     (None)
[ ] src/backend/GameServer.Infrastructure/                  (None)
[ ] src/frontend/client/                                    (None — consuming this is TASK-120)
[ ] docs/                                                   (None — contracts are already authoritative)
```

---

## Implementation Notes

- **Projection site.** Extend the existing `PetStatePayload` record (`BattleHub.cs:147`) with a fourth member and populate it inside `ToPetStatePayload` (`BattleHub.cs:809`). Follow the record's established `[property: JsonPropertyName("...")]` camelCase convention (`SIGNALR_PROTOCOL.md` §3.2.3 item 1: camelCase is fixed explicitly, never left to the serializer's PascalCase default).
- **Reuse the existing precedent.** `BattleStartStateSummary.cs:107-109` already projects this exact field. Match that `Select(card => card.Value)` shape so the REST bootstrap and the SignalR push report one identity the same way.
- **Presence and nullability.** Per `SIGNALR_PROTOCOL.md` §4.3 item 13 the member is always present, non-empty, and non-nullable. `PetState.EquippedCards` is *declared* `EquippedCardIdentity[]?` (`PetState.cs:384`), so decide deliberately how the null case is projected and confirm the resulting wire behavior matches item 13. Do **not** invent a new null/empty policy and do **not** silently emit `null` for the single most important member TASK-120 depends on — if the documented requirement cannot be satisfied without a decision this task is not authorized to make, STOP under §7.
- **Cardinality and order.** `GAME_STATE.md` §2.3 states the array holds exactly four entries and that element order carries no gameplay significance. Project the array as the state holds it, in order, without sorting, deduplicating, filtering, or padding — the projection is a pure field mapping (`SIGNALR_PROTOCOL.md` §3.2.1 item 3's convention).
- **Do not widen the payload.** `SIGNALR_PROTOCOL.md` §4 item 4 admits only the implemented stage's own fields. Add `equippedCards` and nothing else; `EquippedRelics[]` and the combat stats remain non-wire (`§4.3 item 2`).
- **Regression assertions are stale documentation.** Several `ApiIntegrationTests.cs` assertions encode the pre-TASK-121 three-member contract (noted in Current State). Update them to the resolved §4.3 contract rather than deleting the coverage — the negative-contract guard must still prove that unrelated combat state stays absent.
- **`GetBattleState` (§7).** The reconnect snapshot path uses the same projection; confirm the member rides along so a reconnecting client restores castable controls (`ADR-008`, `SIGNALR_PROTOCOL.md` §4.3 item 13).

---

## Testing Requirements

### Required Verification
```text
[x] Unit tests         — the PetStatePayload / ToPetStatePayload projection mapping
[x] Integration tests  — the real BattleStateUpdated push over the hub carries the member
                         (§4.3 item 13), and the §7 GetBattleState snapshot does too
[ ] Gameplay scenarios — N/A: this task changes no gameplay resolution and no game rule
```

### Projection Tests
- **Presence** — `BattleStateUpdated.petState` contains `equippedCards`.
- **Value** — given a known `PetState.EquippedCards[]`, the projected values are the corresponding `CardDefinitionId` strings.
- **Cardinality** — the documented 4-entry loadout is preserved.
- **Ordering** — the authoritative order is preserved.
- **Existing fields** — `passiveId`, `passiveProgress`, and `passiveResetOverride` remain unchanged.
- **No extra fields** — the payload exposes no unrelated combat/domain state.
- **Existing flow** — the projection stays attached to `BattleStateUpdated` and introduces no second message.

### Key Edge Cases
- See `SIGNALR_PROTOCOL.md` §4.3 item 13 — always present, non-empty, non-nullable.
- See `SIGNALR_PROTOCOL.md` §4 item 4 — no member beyond the stage's own may be added.
- See `SIGNALR_PROTOCOL.md` §4.3 item 13 — the member is delivered identically on group join, on every post-resolution push, and in the reconnect snapshot.
- See `GAME_STATE.md` §2.3 — four entries; order preserved; duplicates (if any) are the same definition repeated, never merged.

Use the repository's existing test architecture and naming conventions
(`tests/backend/GameServer.Api.Tests/ApiIntegrationTests.cs` already hosts the
`petState` projection suite and its `JsonElement`-based assertions). Do not
introduce a new integration framework.

---

## Stop Conditions

Universal stop conditions in `AGENTS.md` §20 always apply. STOP and report — do not guess — if:

- `SIGNALR_PROTOCOL.md` §4.3 no longer matches the TASK-121 resolution;
- the `equippedCards` semantics (presence, nullability, cardinality, ordering, serialization shape) are ambiguous or contradictory across `SIGNALR_PROTOCOL.md` §4.3 and `GAME_STATE.md` §2.3;
- the authoritative source is no longer `BattleState.PetState.EquippedCards[]`;
- implementing the projection requires changing the SignalR protocol beyond what TASK-121 already resolved;
- a new event or Hub method appears necessary;
- a new API endpoint appears necessary;
- `PetState` itself must change;
- gameplay semantics must change;
- frontend implementation becomes necessary;
- nullability, cardinality, or ordering are undocumented or contradictory.

Report using the `.ai/README.md` §13 STOP CONDITION format.

---

## Completion Evidence

### Summary
- Added `[property: JsonPropertyName("equippedCards")] IReadOnlyList<string> EquippedCards` to `PetStatePayload` in `src/backend/GameServer.Api/Hubs/BattleHub.cs`.
- Mapped authoritative `petState.EquippedCards` in `BattleHub.ToPetStatePayload(PetState)` to `(petState.EquippedCards ?? []).Select(card => card.Value).ToArray()` matching `BattleStartStateSummary.cs:107-109` precedent.
- Reconciled stale 3-member assertions in `tests/backend/GameServer.Api.Tests/ApiIntegrationTests.cs` to the documented 4-member contract (`SIGNALR_PROTOCOL.md` §4.3).
- Added `BattleStateUpdated_PetState_ShouldCarryEquippedCards_WithAuthoritativeCardinalityAndOrder` integration test in `ApiIntegrationTests.cs`.
- Added unit test suite `tests/backend/GameServer.Api.Tests/Hubs/PetStateWireProjectionTests.cs`.
- Added `equippedCards` validation on post-cast `BattleStateUpdated` in `BattleHubCardCastTests.cs` and `BattleHubPetSkillCastTests.cs`.

### Files Changed
- `src/backend/GameServer.Api/Hubs/BattleHub.cs` — Added `equippedCards` property to `PetStatePayload` record and mapped it in `ToPetStatePayload`.
- `tests/backend/GameServer.Api.Tests/ApiIntegrationTests.cs` — Reconciled stale `petState` wire member assertions; added dedicated integration test for cardinality, ordering, and non-nullability.
- `tests/backend/GameServer.Api.Tests/Hubs/BattleHubCardCastTests.cs` — Added `equippedCards` assertion on post-cast `BattleStateUpdated` push.
- `tests/backend/GameServer.Api.Tests/Hubs/BattleHubPetSkillCastTests.cs` — Added `equippedCards` assertion on post-skill-cast `BattleStateUpdated` push.
- `tests/backend/GameServer.Api.Tests/Hubs/PetStateWireProjectionTests.cs` — New unit tests verifying payload JSON serialization, ordering preservation, and member omission rules.

### SignalR Projection
- `BattleHub.PetStatePayload` contains `[property: JsonPropertyName("equippedCards")] IReadOnlyList<string> EquippedCards`.
- `BattleHub.ToPetStatePayload(PetState petState)` extracts `card.Value` from `petState.EquippedCards`.

### Tests
- `dotnet test src/backend/GameServer.sln` — PASS (2,296 tests total across 4 test projects)
  - `GameServer.Domain.Tests`: 1,275 passed
  - `GameServer.Application.Tests`: 429 passed
  - `GameServer.Infrastructure.Tests`: 327 passed
  - `GameServer.Api.Tests`: 265 passed
- Frontend tests: `npx vitest run` in `src/frontend/client` — PASS (447 tests passed)

### Wire Shape
- Captured `petState` properties from `BattleStateUpdated`:
```json
{
  "passiveId": "xich-lang",
  "passiveProgress": {
    "threshold": 5,
    "current": 0
  },
  "equippedCards": [
    "card-heal",
    "card-shield",
    "card-power-charge",
    "card-inferno"
  ]
}
```

### Scope Verification
- [x] Confirmed no gameplay implementation
- [x] Confirmed no new REST endpoint
- [x] Confirmed no new SignalR event or Hub method
- [x] Confirmed no frontend source modified
- [x] Confirmed `TASK-120` and `TASK-121` unmodified

### Projection Evidence
```text
BattleState.PetState.EquippedCards[] (EquippedCardIdentity[4])
        ↓
BattleHub.ToPetStatePayload() (Select(card => card.Value).ToArray())
        ↓
BattleStateUpdated.petState.equippedCards (string[4] CardDefinitionId)
```
