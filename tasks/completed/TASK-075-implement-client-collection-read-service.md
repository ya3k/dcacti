# TASK-075 — Implement Client Collection Read Service

---

## Metadata

```text
Task ID:           TASK-075
Type:              FEATURE
Status:            DONE
Risk:              LOW
Priority:          MEDIUM
Primary Agent:     client
Supporting Agents: N/A
Workflow:          development/feature.md
Skills:            discovery/documentation-discovery, backend/api-contract-validation, testing/test-scenario-generation, quality/scope-validation
Dependencies:      TASK-071, TASK-074
```

---

## Objective

Implement the client-side collection read service and typed response models for fetching the authenticated player's owned Pets, single Pet detail, unlocked Cards, and owned Relics according to the authoritative contracts defined in `API_CONTRACTS.md` §5.

---

## Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1 — Collection system (owned Pets, Cards, Relics read-only viewing)
- `docs/02-technical/API_CONTRACTS.md` §1 — Endpoint summary (`GET /api/pets`, `GET /api/pets/{petId}`, `GET /api/cards`, `GET /api/relics`)
- `docs/02-technical/API_CONTRACTS.md` §2.8 — Application session transport (`Authorization: Bearer <sessionToken>`) and failure behavior (`401 UNAUTHENTICATED`)
- `docs/02-technical/API_CONTRACTS.md` §5 — Collection read contracts (§5.1 `GET /api/pets`, §5.2 `GET /api/pets/{petId}`, §5.3 `GET /api/cards`, §5.4 `GET /api/relics`, §5.5 list semantics)
- `docs/03-decisions/ADR/ADR-011-loadout-and-ownership-boundaries.md` — Collection ownership boundaries
- `docs/03-decisions/ADR/ADR-012-card-acquisition-and-progression.md` — Card unlock presence model
- `docs/03-decisions/ADR/ADR-015-application-session-authentication-mechanism.md` — Application session token propagation

---

## Scope

### In Scope

- Define client-side TypeScript interfaces/types for collection responses matching `API_CONTRACTS.md` §5:
  - `PetCollectionItem` / `PetResponse`: `petId`, `identity`, `element` (`"Fire" | "Water" | "Earth" | "Wood" | "Metal"`), `tier`, `star`, `level`
  - `CardCollectionItem` / `CardResponse`: `cardId`, `name`, `category` (`"Basic" | "PetSkill"`)
  - `RelicCollectionItem` / `RelicResponse`: `relicId`, `name`
- Implement client collection read methods on the API service layer (`src/frontend/client/src/services/api/`):
  - `getPets(): Promise<PetResponse[]>` -> `GET /api/pets`
  - `getPet(petId: string): Promise<PetResponse>` -> `GET /api/pets/{petId}`
  - `getCards(): Promise<CardResponse[]>` -> `GET /api/cards`
  - `getRelics(): Promise<RelicResponse[]>` -> `GET /api/relics`
- Ensure authenticated session header (`Authorization: Bearer <sessionToken>`) is propagated using `ApplicationSession`.
- Handle error responses (e.g. `401 UNAUTHENTICATED`, `404 PET_NOT_FOUND`) consistently with the `API_CONTRACTS.md` §6 envelope.
- Unit tests validating request URLs, headers, payload parsing, and error conditions.

### Out of Scope

- Inventing new endpoints (e.g. `GET /api/bosses` does NOT exist; Boss definitions are not player collections).
- Modifying the documented wire fields (e.g. `identity` must NOT be renamed to `name`; `star` must NOT be omitted; `xp`, `playerId`, `acquiredAt` must NOT be added).
- UI presentation components or React collection views (reserved for follow-up UI tasks).
- Battle initialization flow (`POST /api/battle/start`) or loadout assembly UI.
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2 (e.g. Gacha, Trading, Card progression).

---

## Current State

The backend collection read endpoints (`CollectionController.cs`, `CollectionQueryService.cs`) were implemented and tested in TASK-070 and TASK-071. On the frontend, `ApiService.ts` provides low-level `get<T>` and `post<T>` primitives with `ApplicationSession` bearer token propagation, but no domain-specific collection methods or typed models exist yet.

---

## Acceptance Criteria

- [x] Typed TypeScript interfaces defined matching `API_CONTRACTS.md` §5.1–§5.4 wire structures exactly.
- [x] `getPets()` issues `GET /api/pets` with bearer token and returns typed array of `PetResponse`.
- [x] `getPet(petId)` issues `GET /api/pets/{petId}` with bearer token and returns typed `PetResponse` on 200, throwing on 404 `PET_NOT_FOUND`.
- [x] `getCards()` issues `GET /api/cards` with bearer token and returns typed array of `CardResponse`.
- [x] `getRelics()` issues `GET /api/relics` with bearer token and returns typed array of `RelicResponse`.
- [x] Empty collections deserialize to `[]` per §5.5 list semantics without error.
- [x] All unit tests pass at required validation depth (`npm test` in `src/frontend/client`).
- [x] Quality review checklist passes (`quality/review.md` §1).
- [x] Zero server authority or architectural boundary violations (`AGENTS.md` §10 / ADR-001).

---

## Affected Files & Areas

```text
[x] src/frontend/client/src/services/api/ (CollectionService or ApiService methods and response types)
[x] src/frontend/client/tests/ (Unit tests for collection API consumption)
```

---

## Implementation Notes

- Locate existing API layer in `src/frontend/client/src/services/api/ApiService.ts` and `ApplicationSession.ts`.
- Collection types can be placed in `src/frontend/client/src/services/api/CollectionModels.ts` (or `CollectionService.ts`).
- Reuse `ApiService.getInstance().get<T>(...)` so bearer token injection via `ApplicationSession` is handled transparently.

---

## Testing Requirements

### Required Verification

```text
[x] Unit tests — verify each collection method issues correct HTTP method, endpoint path, bearer token header, parses success responses, and propagates HTTP error statuses (401, 404).
```

### Key Edge Cases

- `GET /api/pets` with empty array `[]` (new player with no pets).
- `GET /api/pets/{petId}` when `petId` is unknown or unowned returning `404` with `PET_NOT_FOUND`.
- Unauthenticated requests returning `401` with `UNAUTHENTICATED`.

---

## Stop Conditions

- If required behavior cannot be fully derived from Authoritative References: STOP per `AGENTS.md` §7
- If task requires client-authoritative game logic calculation: STOP per `AGENTS.md` §10
- If task exceeds 7 skills or crosses multiple uncoupled architectural boundaries: STOP & decompose

---

## Completion Evidence

### Changed Files

**Created — client**
- `src/frontend/client/src/services/api/CollectionModels.ts` — `PetResponse` (§5.1/§5.2: `petId`, `identity`, `element`, `tier`, `star`, `level`), `CardResponse` (§5.3: `cardId`, `name`, `category`), `RelicResponse` (§5.4: `relicId`, `name`), plus the `Element` (`"Fire" | "Water" | "Earth" | "Wood" | "Metal"`) and `CardCategory` (`"Basic" | "PetSkill"`) wire aliases. Exactly the documented members; no `xp`/`playerId`/`acquiredAt`/`unlocked`/equip members.

**Modified — client**
- `src/frontend/client/src/services/api/ApiService.ts` — added `getPets()`, `getPet(petId)`, `getCards()`, `getRelics()` delegating to the existing `get<T>()` transport, and re-exported the three response types.

**Created — tests**
- `src/frontend/client/tests/CollectionService.test.ts` — 27 tests covering all four endpoints (§5.1–§5.6, §2.8 "Failure behavior").

**Deliberately NOT changed:** every `docs/` file, every `src/backend/` file, `BattleScene`/`GameRuntime`/`LobbyScene`, `SignalRService`, `ApplicationSession`, `GameRuntime.test.ts`.

### Validation Results

```text
Baseline (before):  files 13 | tests 228 passing, 0 failed
Final (after):      files 14 | tests 255 passing, 0 failed
Delta:              +27 new tests; 0 existing tests changed or weakened
```

- `npm run test:run` (vitest) in `src/frontend/client` — **PASS (255 tests, 14 files)**
- `npm run build` (`tsc && vite build`) in `src/frontend/client` — **PASS**, `tsc` clean, 71 modules transformed, built in 3.70s. The Rollup `/*#__PURE__*/` and chunk-size messages on stderr come from `node_modules/@microsoft/signalr` and are pre-existing, unrelated to this change.
- Backend suite — not run: no backend file was touched (see Scope Verification).

### Server Authority & Scope Verification

- [x] Confirmed zero client-authoritative gameplay logic — the four methods are read-only GETs that parse and return the server's response verbatim; no value is computed, defaulted, clamped, or derived, and no collection state is held or mutated locally.
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1) — Pets, Cards, and Relics collections are listed IN, and the Player account is the documented "collection owner".
- [x] No backend changes. No API contract changes. No UI. No Battle Start. No loadout flow. No boss selection. No gameplay. No client-authoritative state.

### Notes on decisions taken inside the contract

1. **`ApiService` was extended; no `CollectionService` class was created.** The task permits either ("Prefer extending the existing `ApiService` if that is the established pattern"). It is the established pattern: `ApiService` already owns `checkHealth`, `authenticateDiscord`, and the `get`/`post` primitives, and `SignalRService` shows the repository's convention of one transport-focused service per transport. A separate `CollectionService` would have wrapped four one-line delegates — a speculative layer (`AGENTS.md` §9, task §16).
2. **`tier` and `category` are typed differently, deliberately.** `category` is `CardCategory`, a two-value union, because §5.3 names both values inline. `tier` stays `string` because §5.1 states `tier` as `Pet.Tier` and does not enumerate the tier vocabulary — the permitted names are owned by `PET_RULES.md` §3, so re-listing them here would be a second copy that could drift (`docs/AGENTS.md` §2, one concept one owner).
3. **`encodeURIComponent` on `petId`.** §5.2's route places the id in a path segment. Encoding uses the standard JS encoder so a non-URL-safe id cannot break out of the segment into a query string or a different path; the test asserts the separators stay escaped.
4. **Empty lists are returned as `[]`.** §5.5 defines empty as `200 []`, and `ApiService.get` returns `response.json()` unchanged, so `[]` passes through as an empty array rather than being collapsed to `null`/`undefined`. Tests assert `Array.isArray` explicitly for all three list endpoints.

### Scoping Note (recorded, NOT fixed here)

- `tests/GameRuntime.test.ts:751` carries `element: 'Hoa'` on the **battle-start / SignalR** path. That is a different contract from §5: TASK-073/TASK-074 own the battle-state element value set, and TASK-071's Known Follow-up records the battle-start/Redis serialization convergence as separate work. The §5 `Element` alias lives only in `CollectionModels.ts` and is imported by nothing else, so the two contracts do not collide. Not touched by this task.
