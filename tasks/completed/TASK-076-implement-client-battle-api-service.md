# TASK-076 — Implement Client Battle API Service

---

## Metadata

```text
Task ID:           TASK-076
Type:              FEATURE
Status:            DONE
Risk:              LOW
Priority:          HIGH
Primary Agent:     client
Supporting Agents: N/A
Workflow:          development/feature.md
Skills:            discovery/documentation-discovery, backend/api-contract-validation, client/client-state-authority, testing/test-scenario-generation, quality/scope-validation
Dependencies:      TASK-030, TASK-041, TASK-074, TASK-075
```

---

## Objective

Implement the client-side typed models and API methods on `ApiService` for `POST /api/battle/start` (initiating an authoritative battle session with Pet, Boss, and loadouts) and `GET /api/battle/{battleId}/result` (fetching completed battle outcome and rewards) in accordance with the authoritative contracts in `API_CONTRACTS.md` §3 and §4.

---

## Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1 — Battle Loop (Single-Player vs. Boss) and REST API boundary
- `docs/02-technical/API_CONTRACTS.md` §1 — Endpoint summary (`POST /api/battle/start`, `GET /api/battle/{battleId}/result`)
- `docs/02-technical/API_CONTRACTS.md` §2.8 — Application session transport (`Authorization: Bearer <sessionToken>`) and failure behavior (`401 UNAUTHENTICATED`)
- `docs/02-technical/API_CONTRACTS.md` §3 — `POST /api/battle/start` request validation, response payload (`battleId`, `signalrHub`, `initialState`), and wire Element values
- `docs/02-technical/API_CONTRACTS.md` §4 — `GET /api/battle/{battleId}/result` response payload (`battleId`, `outcome`, `rewards`, `durationTurns`), outcome vocabulary (`"victory" | "defeat"`), and error mapping
- `docs/02-technical/API_CONTRACTS.md` §5.1 — Canonical Element wire value set (`"Fire" | "Water" | "Earth" | "Wood" | "Metal"`)
- `docs/02-technical/API_CONTRACTS.md` §6 — REST error envelope (`error`, `message`)
- `docs/02-technical/GAME_STATE.md` §2 — Authoritative BattleState definitions
- `docs/02-technical/DATABASE.md` §1 — `RewardSummary` shape (`playerXpGained`, `petXpGained`)
- `docs/03-decisions/ADR/ADR-001-server-authoritative-battle.md` — Server-authoritative state
- `docs/03-decisions/ADR/ADR-011-player-owner-pet-combat.md` — Loadout and combat boundaries
- `docs/03-decisions/ADR/ADR-014-battle-state-player-identity.md` — Battle identity carriage
- `docs/03-decisions/ADR/ADR-015-application-session-authentication-contract.md` — Application session token propagation

---

## Scope

### In Scope

- Define client-side TypeScript interfaces/types for Battle REST request and response payloads matching `API_CONTRACTS.md` §3 and §4:
  - `BattleStartRequest`: `petId: string`, `bossId: string`, `cardLoadout: string[]` (3 Basic Card IDs), `relicLoadout: string[]` (3–5 Relic instance IDs).
  - `BattleStartResponse`: `battleId: string`, `signalrHub: string`, `initialState: BattleStartInitialState`.
  - `BattleStartInitialState` and nested types: `battleId`, `turn`, `sequence`, `rngSeed`, `rngState: { state, increment }`, `board: { cells: Array<{ gemType, specialGem?: { type, orientation? } | null }> }`, `combo`, `matchCount`, `petState: BattleStartPetState`, `bossState: BattleStartBossState`.
  - `BattleStartPetState`: `hp`, `maxHP`, `atk`, `def`, `crit`, `power`, `element: Element`, `passiveId`, `passiveThreshold`, `passiveCurrent`, `equippedCards: string[]`, `equippedRelics: string[]`.
  - `BattleStartBossState`: `bossId`, `element: Element`, `hp`, `maxHP`, `atk`, `def`, `state: string`.
  - `BattleResultResponse`: `battleId: string`, `outcome: "victory" | "defeat"`, `rewards: RewardSummaryResponse`, `durationTurns: number`.
  - `RewardSummaryResponse`: `playerXpGained: number`, `petXpGained?: number`.
- Implement client battle methods on `ApiService` (`src/frontend/client/src/services/api/ApiService.ts`):
  - `startBattle(request: BattleStartRequest): Promise<BattleStartResponse>` -> `POST /api/battle/start`
  - `getBattleResult(battleId: string): Promise<BattleResultResponse>` -> `GET /api/battle/{battleId}/result`
- Ensure authenticated session header (`Authorization: Bearer <sessionToken>`) is propagated via existing `ApplicationSession`.
- Handle HTTP error responses (`400 INVALID_LOADOUT`, `400 PET_NOT_OWNED`, `400 BOSS_NOT_FOUND`, `401 UNAUTHENTICATED`, `404 BATTLE_NOT_FOUND`) consistently with the `API_CONTRACTS.md` §6 envelope.
- Unit tests covering URL construction, serialization of request bodies, deserialization of responses, bearer token headers, and error status propagation.

### Out of Scope

- No gameplay.
- No PostgreSQL BattleState persistence.
- No undocumented Redis behavior.
- No undocumented SignalR methods.
- No client-authoritative state.
- No speculative architecture.
- No UI components or Phaser scene integration (reserved for subsequent scene wiring tasks).
- No reconnection snapshot fetching (`GetBattleState` is handled via SignalR protocol §7 / ADR-008 when implemented).
- No modification of backend endpoints or schemas.
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2.

---

## Current State

The backend endpoints for `POST /api/battle/start` (`BattleController.Start`) and `GET /api/battle/{battleId}/result` (`BattleController.Result`) are fully implemented and verified against Redis active state and PostgreSQL result persistence. In `src/frontend/client/src/services/api/`, `ApiService.ts` provides `getPets`, `getCards`, `getRelics`, but lacks `startBattle` and `getBattleResult` methods and typed request/response models for the battle REST endpoints.

---

## Acceptance Criteria

- [x] Typed TypeScript interfaces defined in `src/frontend/client/src/services/api/BattleModels.ts` (or re-exported from `ApiService.ts`) matching `API_CONTRACTS.md` §3 and §4 wire structures exactly.
- [x] `startBattle(request)` issues `POST /api/battle/start` with the JSON request body and session Bearer token, returning typed `BattleStartResponse` on 200.
- [x] `getBattleResult(battleId)` issues `GET /api/battle/{battleId}/result` with session Bearer token, returning typed `BattleResultResponse` on 200.
- [x] `startBattle` propagates 400 errors (`INVALID_LOADOUT`, `PET_NOT_OWNED`, `BOSS_NOT_FOUND`) and 401 `UNAUTHENTICATED` without mutating client state.
- [x] `getBattleResult` propagates 404 `BATTLE_NOT_FOUND` and 401 `UNAUTHENTICATED` without mutating client state.
- [x] All unit tests pass at required validation depth (`npm test` in `src/frontend/client`).
- [x] Quality review checklist passes (`quality/review.md` §1).
- [x] No client-authoritative gameplay logic or state calculation (`AGENTS.md` §10 / ADR-001).

---

## Affected Files & Areas

```text
[x] src/frontend/client/src/services/api/ (BattleModels.ts, ApiService.ts)
[x] src/frontend/client/tests/ (BattleService.test.ts or ApiService.test.ts)
```

---

## Implementation Notes

- Locate existing API layer in `src/frontend/client/src/services/api/ApiService.ts` and `ApplicationSession.ts`.
- Define models in `src/frontend/client/src/services/api/BattleModels.ts` and re-export from `ApiService.ts`.
- Re-use the existing `Element` alias (`"Fire" | "Water" | "Earth" | "Wood" | "Metal"`) from `CollectionModels.ts` or declare a shared export.
- Delegate to `ApiService.getInstance().post<BattleStartResponse>('/api/battle/start', request)` and `ApiService.getInstance().get<BattleResultResponse>(`/api/battle/${encodeURIComponent(battleId)}/result`)`.
- Keep the service strictly transport-focused: no local state storage or caching.

---

## Testing Requirements

### Required Verification

```text
[x] Unit tests — verify startBattle and getBattleResult issue correct HTTP method, endpoint path, headers, serialize request payloads, deserialize response payloads, and propagate HTTP error statuses (400, 401, 404).
```

### Key Edge Cases

- `POST /api/battle/start` with minimum 3 relics vs 5 relics in `relicLoadout`.
- `POST /api/battle/start` rejected with `400 INVALID_LOADOUT` or `400 PET_NOT_OWNED`.
- `GET /api/battle/{battleId}/result` for `"victory"` vs `"defeat"` outcome payloads.
- `GET /api/battle/{battleId}/result` returning `404 BATTLE_NOT_FOUND` for non-existent or foreign battle.
- Unauthenticated calls returning `401 UNAUTHENTICATED`.

---

## Stop Conditions

- If required behavior cannot be fully derived from Authoritative References: STOP per `AGENTS.md` §7
- If task requires client-authoritative game logic calculation: STOP per `AGENTS.md` §10
- If task exceeds 7 skills or crosses multiple uncoupled architectural boundaries: STOP & decompose

---

## Completion Evidence

### Status

```text
DONE
```

### Changed Files

**Created — client**

- `src/frontend/client/src/services/api/BattleModels.ts` — the battle wire
  models for `API_CONTRACTS.md` §3 and §4: `BattleStartRequest`,
  `BattleStartResponse`, `BattleStartInitialState`, `BattleStartRngState`,
  `BattleStartBoard`, `BattleStartCell`, `BattleStartSpecialGem`,
  `BattleStartPetState`, `BattleStartBossState`, `BattleResultResponse`,
  `BattleOutcome`, `RewardSummaryResponse`. `Element` is **re-exported from**
  `CollectionModels.ts` (no second Element union).

**Modified — client**

- `src/frontend/client/src/services/api/ApiService.ts` — added `startBattle`
  (`POST /api/battle/start`) and `getBattleResult`
  (`GET /api/battle/{battleId}/result`, `encodeURIComponent`-encoded path
  segment) on the existing transport, plus the battle type re-exports.
  `git diff` is **+143 / −0**: no existing line was changed, so
  `get`, `post`, `getPets`, `getPet`, `getCards`, `getRelics`, and
  `authenticateDiscord` are byte-identical.

**Created — tests**

- `src/frontend/client/tests/BattleService.test.ts` — 37 tests covering both
  endpoints, the request/response contracts, Bearer propagation, 400/401/404
  propagation, the loadout boundaries, both outcomes, and the scope guards.

**Deliberately NOT changed:** every `docs/` file, every `src/backend/` file,
`ApplicationSession.ts`, `CollectionModels.ts`, `CollectionService.test.ts`,
`SignalRService.ts`, `GameRuntime*`, `BattleScene`, `GameRuntimeState.ts`,
TASK-036, and every completed task.

### Validation

```text
Frontend tests:
Frontend build:
```

- `npm run test:run` (vitest) in `src/frontend/client` —
  **PASS (292 tests, 15 files)**
  - Baseline before this task: 255 tests / 14 files → **+37 tests, 0 existing
    tests changed or weakened**.
  - `tests/BattleService.test.ts` — 37 passed.
- `npm run build` (`tsc && vite build`) in `src/frontend/client` —
  **PASS**: `tsc` **0 errors**, 71 modules transformed, built in 2.60s.
  The Rollup `/*#__PURE__*/` and chunk-size messages on stderr come from
  `node_modules/@microsoft/signalr` and are pre-existing and unrelated
  (identical on the pre-change build).
- `npx tsc --noEmit` — **PASS (exit 0)**.
- Backend suite — **not run, deliberately**: no `src/backend/` file was
  touched (see Scope Verification), so no backend behaviour can depend on
  this change.

**Non-vacuity (mutation) check.** Three mutations were applied to the
implementation and reverted, to confirm the tests fail against wrong code
rather than merely passing against right code:

```text
1. startBattle POST → GET             → 9 tests FAIL (method/path guards)
2. Element models → Vietnamese names  → tsc FAILS (5+ compile errors:
                                        'Fire' not assignable to the drifted
                                        union) — caught by the type system
3. petState/bossState.element forced  → Element theory test FAILS
   to 'Fire' on deserialization
```

Mutation 2 is the notable one: an incompatible Element set is rejected at
**compile time**, because `Element` is a single shared union rather than a
per-model `string`.

### API Contract

Confirmed against the current authoritative documents and against the
implemented backend wire records:

```text
POST /api/battle/start              → API_CONTRACTS.md §3
GET  /api/battle/{battleId}/result  → API_CONTRACTS.md §4
```

- **Request (§3).** Exactly `petId`, `bossId`, `cardLoadout` (3 Basic Cards),
  `relicLoadout` (3–5 Relic instances, array order = equip slot order,
  `RELIC_RULES.md` §2.3). No `playerId` / `discordUserId` / `sessionToken`
  (`§2.8` "Identity", ADR-015 D3) and no `battleId` / `turn` / `sequence`
  (`GAME_RULES.md` §18, ADR-001).
- **Start response (§3).** Exactly `battleId`, `signalrHub`, `initialState`;
  `initialState` carries `battleId`, `turn`, `sequence`, `rngSeed`,
  `rngState{state,increment}`, `board{cells[{gemType,specialGem?}]}`, `combo`,
  `matchCount`, `petState`, `bossState`. Verified member-for-member against
  `BattleStartResponse.cs` / `BattleStartStateSummary.cs` and
  `GAME_STATE.md` §2. `playerId` is present in `§2` but is **not** a wire
  member (`GAME_STATE.md` §2.8 item 3, ADR-014 item 3) and is deliberately
  absent. `petState` is `hp, maxHP, atk, def, crit, power, element,
  passiveId, passiveThreshold, passiveCurrent, equippedCards,
  equippedRelics`; `bossState` is `bossId, element, hp, maxHp, atk, def,
  state`. `StatusEffects[]` is excluded on **both** because
  `GAME_STATE.md` §2.3/§2.4.1 record it as not yet implemented; the Boss's
  `PassiveId`/`PassiveProgress`/`SkillCharge`/`SkillCooldown` are excluded
  because they are resolution state and starting a battle resolves no action.
- **Result response (§4).** Exactly `battleId`, `outcome`, `rewards`,
  `durationTurns`; `outcome` is `"victory" | "defeat"` only (`§4` note 4,
  `GAME_EVENTS.md` §2) — no `won`/`lost`/`success`/`failed`.
- **`RewardSummaryResponse`.** All **eight** members are **required**:
  `playerXpGained`, `newPlayerXp`, `playerLeveledUp`, `newPlayerLevel`,
  `petXpGained`, `newPetXp`, `petLeveledUp`, `newPetLevel`. Verified against
  `DATABASE.md` §1 "Reward semantics for `RewardSummary`" items 1, 2 and 5
  **and** `BattleResultService.BuildRewardSummary` (whose member-name
  constants match exactly). `petXpGained` is **not** optional — the Pet-track
  member list that §1 item 2 deferred to the implementation has landed, so
  the `petXpGained?: number` shape is not the contract in force. The four
  resulting-value members are typed `number | null` / `boolean | null`
  because `BattleResultService` writes JSON `null` for a track with no owning
  row (§1 item 2), and the client must not substitute a zero for that.
- **Element (§5.1).** `'Fire' | 'Water' | 'Earth' | 'Wood' | 'Metal'`, reused
  from `CollectionModels.ts`; §3 binds both `initialState.petState.element`
  and `initialState.bossState.element` to it by reference. No Vietnamese
  display name and no Domain enum member name (`Moc`/`Hoa`/`Tho`/`Thuy`/`Kim`)
  appears in the contract — the values TASK-074 removed from this endpoint.
- **Errors (§6).** Start: `400 INVALID_LOADOUT` / `400 PET_NOT_OWNED` /
  `400 BOSS_NOT_FOUND` / `401 UNAUTHENTICATED`. Result:
  `401 UNAUTHENTICATED` / `404 BATTLE_NOT_FOUND`. Verified against
  `BattleController.ToErrorCode` and `BattleController.cs`'s
  `BattleNotFoundErrorCode`.

### Authority

- [x] No client-authoritative `BattleState` — the methods return the typed
      response and keep nothing: no field, no cache, no module-level store.
      A test asserts the instance's own key set is unchanged after a call.
- [x] No gameplay logic — no damage, HP, Power, Match, Combo, Passive, reward,
      or outcome computation; no `Math.random()`; no value is defaulted,
      clamped, recomputed, or derived. A test asserts a non-default payload is
      returned byte-for-byte.
- [x] No local battle-state ownership — `BattleState` remains server-owned
      (`GAME_STATE.md` §2, ADR-001). No `GameRuntime`, `BattleScene`,
      `GameRuntimeState`, or `SignalRService` reference exists in either file.
- [x] No local loadout validation — the client transports the selection; 3/5
      relic boundaries and the 3-card count are asserted as *transported*
      values, never accepted/rejected locally.
- [x] All eight reward members are read, never applied: no method updates
      Player or Pet XP (`COMBAT_RULES.md` §7, `PET_RULES.md` §5.3).

### Scope

- [x] **No backend changes** — `git status` confirms zero `src/backend/` files
      touched by this task.
- [x] **No TASK-036 changes** — untouched (still `tasks/blocked/`).
- [x] **No documentation changes** — zero `docs/` files touched. No
      documentation/source contradiction was discovered, so no §4/§16 stop
      condition fired.
- [x] **No SignalR implementation** — `signalrHub` is read as a value only.
- [x] **No GameRuntime integration.**
- [x] **No BattleScene integration.**
- [x] Zero `src/frontend/client/src/state/` changes. Zero completed-task
      changes. `CollectionModels.ts` (154 lines) and
      `CollectionService.test.ts` (479 lines) are byte-identical to their
      TASK-075 state; there is no reverse dependency from
      `CollectionModels.ts` to `BattleModels.ts`.

### Notes on decisions taken inside the contract

1. **`ApiService` was extended; no `BattleService` class was created.** Both
   the task (§Implementation Notes) and TASK-075's established pattern put
   the methods on `ApiService` over the existing `get`/`post` transport. A
   separate class would have wrapped two one-line delegates
   (`AGENTS.md` §9).
2. **`BattleStartRequest`'s loadout arrays are `readonly string[]`.** The
   contract's cardinalities are documented in JSDoc but deliberately **not**
   enforced by a tuple type: `relicLoadout` is a runtime 3–5, which a tuple
   cannot express, and a `[string, string, string]` card tuple would push
   client-side validation of a server-owned rule into the type system.
3. **`Element` is re-exported, not redeclared.** `CollectionModels.ts` stays
   the single definition; `BattleModels.ts` imports and re-exports it, and
   `ApiService.ts` re-exports it from `BattleModels.ts` — so the compile-time
   rejection demonstrated in mutation 2 holds.
4. **`gemType`, `specialGem.type`, `specialGem.orientation`, and
   `bossState.state` are typed `string`, not local unions.** Their permitted
   values are owned by `MATCH3_RULES.md` §1.1/§5.2–§5.4 and `BOSS_RULES.md`
   §1/§5; re-listing them here would be a second copy that could drift — the
   same reasoning TASK-075 recorded for `tier`.
5. **`rewardSummary`'s nullable members are contract-driven, not
   convenience.** See the API Contract section above.

### Remaining Issues

None affecting this task's scope. Two pre-existing, report-only items already
recorded by earlier tasks and **not** touched here (`AGENTS.md` §16):

1. `src/frontend/client/tests/GameRuntime.test.ts:751` carries
   `element: 'Hoa'` inside a payload that test asserts is *dropped*. It is on
   the battle-start/SignalR payload path, not the REST contract this task
   implements, and TASK-074 already recorded it as report-only.
2. `src/backend/GameServer.Domain/Elements/Element.cs` has a stale comment
   claiming no API Element serialization contract exists (TASK-074 Known
   Follow-up #1, gameplay-agent ownership).
