# TASK-047 — Synchronize Boss Technical Identity in Source and Tests

---

## Metadata

```text
Task ID:           TASK-047
Type:              BUG
Status:            DONE
Risk:              MEDIUM
Priority:          HIGH
Primary Agent:     gameplay
Supporting Agents: backend, testing, review
Workflow:          development/bug-fix.md
Skills:            documentation-discovery, impact-analysis,
                   test-scenario-generation, documentation-consistency
Dependencies:      TASK-046 (DONE)
Blocks:            TASK-044 (conditional — P1 conflict RESOLVED, see below)
```

**Status Note (at creation):** A pre-detected STOP condition exists: the
Thủy Ma and Mộc Yêu `BossId` literals are display names, and TASK-046
deliberately left their technical IDs UNRESOLVED. Full synchronization of
those two requires a human decision (see Stop Conditions, conflict P1).
The Hỏa Long portion and all generic comment/wire synchronization are
executable without it.

**Status Note (final, 2026-09-26):** Executed to `DONE`. The P1 conflict was
**resolved by explicit human decision** — the human supplied
`boss-thuy-ma` and `boss-moc-yeu` (with `boss-hoa-long`), so option (a) of
Stop Conditions P1 applied and full synchronization proceeded; no deferral and
no invented value. Full backend suite green (1340 tests, 0 failed, 0 skipped).
No `docs/`, `tasks/`, schema, migration, Redis-architecture, or SignalR-shape
change. File moved to `tasks/completed/` per `tasks/TASK_LIFECYCLE.md`.

---

## Objective

Synchronize stale display-name Boss identity usage in `src/backend` and
`tests/` with the TASK-046 contract: the Hỏa Long `BossId` literal becomes
`boss-hoa-long`, boss-sourced wire `sourceId` and request/response `bossId`
values become canonical technical identities, code comments that claim
`BossId` is a display name are corrected, and test assertions are updated
to the documented values without weakening any assertion. Smallest existing
change only — no redesign, no new abstractions, no documentation edits, no
TASK-044/TASK-041 implementation work.

Dependency chain note: TASK-046 → **TASK-047** → TASK-044 → TASK-041.
TASK-044's insert/read-back persistence round-trip maps `BossDefinition`
fields onto the `Identity` column; with display-name literals in code, that
mapping would write a non-canonical `Identity`, so TASK-044 must wait for
this task (see Acceptance Criteria + Stop Conditions).

---

## Authoritative References

- `docs/01-game-design/BOSS_RULES.md` §6 / §6.4 — identity contract:
  `boss-hoa-long`; convention `boss-<ascii-kebab-case-name>`; Thủy Ma /
  Mộc Yêu technical IDs UNRESOLVED; "never a display name"
- `docs/02-technical/GAME_STATE.md` §2.4 — `BossState.BossId` denotes the
  canonical technical Boss Identity
- `docs/02-technical/GAME_EVENTS.md` §2 — `BattleStarted.BossId`;
  `PassiveCharged` / `PassiveTriggered` / `BossSkillCast` `sourceId`
- `docs/02-technical/SIGNALR_PROTOCOL.md` §3.2.17, §3.2.18 — `sourceId` is
  the canonical technical identity, "never a display name"
- `docs/02-technical/API_CONTRACTS.md` §3 — request example
  `"bossId": "boss-hoa-long"` and bossId validation
- `docs/02-technical/DATABASE.md` §1 — `Identity` column = canonical
  technical Boss Identity (context for the TASK-044 dependency only; no
  schema work here)
- `tasks/completed/TASK-046-resolve-canonical-boss-identity-contract.md`
  §5A — decisions A–F (read-only; never modify)
- `AGENTS.md` §7 (no invented rules), §15 (tests follow rules), §16 (task
  discipline), §17 (documentation change rule), §20 (stop conditions)

---

## Scope

### In Scope

1. Change `BossDefinitions.HoaLong`'s `BossId` literal to `boss-hoa-long`
   (`BossDefinitions.cs` L82) and correct the file's header comment
   (L44–47) which claims display-name semantics.
2. Correct every identity-semantic doc-comment in `src/backend` that
   claims `BossId` is a display name or has no documented format (full
   line-verified list in Current State).
3. Update test requests, constants, `InlineData`, and assertions for
   Hỏa Long to `boss-hoa-long`, and boss-sourced `sourceId` assertions to
   technical-identity values, across `tests/backend` (line-verified list
   in Current State).
4. Update comment-only test references whose wording asserts identity
   semantics (e.g. `PassiveEventSourceTests.cs` L126, L187;
   `BossResponseTests.cs` L105).
5. Thủy Ma / Mộc Yêu literals and their tests — **only** after the human
   supplies technical IDs or approves an explicit deferral (Stop
   Conditions P1). Never invent `boss-thuy-ma` / `boss-moc-yeu`.
6. Verify the Redis round-trip of the changed value
   (`BattleStateJson.cs` L77 key `bossId` unchanged — value only).

### Out of Scope

- Any edit under `docs/` — the six TASK-046 documents are correct and
  immutable for this task.
- Any edit to `tasks/completed/` or to TASK-041 / TASK-043 / TASK-044 /
  TASK-045 / TASK-046 files.
- `BossDefinitionId` semantics; putting `DisplayName` into `BattleState`;
  localization of display names.
- Database schema, migrations, `BattleResult`, row provisioning, any
  PostgreSQL work (TASK-044 / TASK-041).
- Redis key/schema structure; SignalR protocol shape (event names, payload
  members, §-numbering) — value strings only may change.
- New Bosses, gameplay rules, gameplay behavior changes, new abstractions
  (`BossIdentityService`, `BossIdResolver`, `BossNameMapper`,
  `LocalizationService`).
- Frontend — verified clean: no `bossId` / `sourceId` / display-name
  identity literals in `src/frontend` (`tests/RuntimeBoundaries.test.ts`
  L206 is an event-name allowlist, unaffected).
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2.

---

## Current State — Stale Identity Evidence (line-verified)

### Source literals (root cause)

| Location | Content | Action |
|---|---|---|
| `GameServer.Domain/Bosses/BossDefinitions.cs` L82 | `new BossId("Hỏa Long")` | → `new BossId("boss-hoa-long")` |
| `GameServer.Domain/Bosses/BossDefinitions.cs` L105 | `new BossId("Thủy Ma")` | BLOCKED (P1) |
| `GameServer.Domain/Bosses/BossDefinitions.cs` L129 | `new BossId("Mộc Yêu")` | BLOCKED (P1) |
| `GameServer.Domain/Bosses/BossDefinitions.cs` L44–47 | header claims display-name semantics | correct per §6.4 |

### Stale identity doc-comments in `src/backend`

- `GameServer.Domain/Bosses/BossId.cs` L31–34 — "no id format, scheme, or
  validation rule in any document" (false since TASK-046)
- `GameServer.Domain/Match3/BattleEvent.cs` L210, L229, L707
- `GameServer.Domain/Passives/PassiveEvents.cs` L61, L139
- `GameServer.Application/Battle/BattleStartRequest.cs` L40–41
- `GameServer.Application/Battle/BattleStartService.cs` L344 (comment on
  the `ResolveBoss` comparison)
- `GameServer.Application/Battle/BattleStateService.cs` L989
- `GameServer.Api/Hubs/BattleEventWireProjection.cs` L166, L335, L371,
  L398, L692

### Value flow (values change, structure does not)

- `BattleStateService.cs` L1017 / L1021 — `SourceId = bossState.BossId.Value`;
  L1074 — `BattleEvent.ForBossSkillCast`
- `BattleStartService.cs` L226 / L356 — `ResolveBoss` ordinal-compares
  submitted `bossId` against `BossId.Value`
- `Api/Controllers/BattleStartStateSummary.cs` L112 and
  `BattleStartResponse.cs` L193 — response `bossId` pass-through
- `GameServer.Infrastructure` `BattleStateJson.cs` L77 — const key
  `bossId` (Redis round-trip; no schema impact)

### Tests asserting display-name identities (84 quoted-literal matches)

- `GameServer.Api.Tests/BattleStartEndpointTests.cs` — requests L71, L93,
  L120, L143, L166, L188, L217, L233, L251, L267, L290, L306, L322, L341,
  L356, L402, L404, L405, L441, L497, L525, L572; assertion L172
  (`"Mộc Yêu"`) — P1
- `GameServer.Api.Tests/BattleStartSmokeTest.cs` L80, L167
- `GameServer.Api.Tests/RedisBattleStateSmokeTest.cs` L108
- `GameServer.Application.Tests/BattleStartServiceTests.cs` L53
  (`ValidBossId`), L153 + L159 (Thủy Ma — P1), L306–308 (`InlineData`)
- `GameServer.Application.Tests/BossResponseTests.cs` L105 (comment),
  L130, L279
- `GameServer.Api.Tests/BossResponseWireTests.cs` L151, L207
- `GameServer.Api.Tests/BossWireProjectionTests.cs` L60, L67, L82, L89,
  L161, L165, L173–175, L194, L298, L320, L358, L366, L388
- `GameServer.Domain.Tests/BossStateTests.cs` L321, L338, L355, L374,
  L401–403, L415–417, L435–437, L447–449, L550–553, L595
- `GameServer.Domain.Tests/BattleStateSerializationTests.cs` L122, L607
  (`new BossId("Thủy Ma")` — P1)
- `GameServer.Domain.Tests/PassiveEventSourceTests.cs` L126 (comment),
  L133, L136, L150, L153, L160–162, L187 (comment), L192
- Comment-only references (wording accuracy, no assertion change):
  `ApiIntegrationTests.cs` L1346, L2485; `BattleStateServiceTests.cs` L52;
  `BattleStatePersistenceContractTests.cs` L61; `BattleStateTests.cs`
  L293; `DamagePipelineTests.cs` L776

"P1" marks items that require the unresolved Thủy Ma / Mộc Yêu technical
IDs — see Stop Conditions.

---

## Acceptance Criteria

- [ ] `BossDefinitions.HoaLong.BossId.Value == "boss-hoa-long"` and no
      display-name string is constructed as a `BossId` anywhere in
      `src/backend` (Thủy Ma / Mộc Yêu per the P1 decision recorded below)
- [ ] `POST /api/battle/start` request literals and the response/initial
      state `bossId` for Hỏa Long follow `API_CONTRACTS.md` §3 /
      `GAME_STATE.md` §2.4
- [ ] Boss-sourced event `sourceId` values (wire projection, unit and
      integration tests) equal the technical identity per
      `SIGNALR_PROTOCOL.md` §3.2.17/§3.2.18 and `GAME_EVENTS.md` §2
- [ ] Zero remaining code comments in `src/backend` claim `BossId` is a
      display name or undocumented in format
- [ ] All listed test assertions/constants/InlineData are synchronized;
      no assertion removed or weakened (`AGENTS.md` §15)
- [ ] PassiveId values (`boss-hoa-long-rage`, `boss-thuy-ma-heal`,
      `boss-moc-yeu-regen`) and SkillId values are untouched
- [ ] P1 decision recorded in Completion Evidence: human-supplied IDs
      applied, OR explicit deferral approved — never an invented value
- [ ] Full backend test suite green at MEDIUM validation depth
      (`core/validation.md` §2: build + units + API/SignalR/Redis
      integration tests)
- [ ] Zero files changed under `docs/` and `tasks/`; no schema, Redis
      key, or SignalR shape change; no new abstractions

---

## Affected Files & Areas

```text
[x] src/backend/ (Domain: Bosses, Match3, Passives /
                  Application: Battle / Api: Hubs, Controllers)
[ ] src/frontend/client/ (verified clean — no changes)
[x] tests/ (Api / Application / Domain test projects)
[ ] docs/ (no updates — TASK-046 documents are authoritative and correct)
```

---

## Implementation Notes

- Follow the exact existing literal at `BossDefinitions.cs` L82 as the
  edit pattern for any subsequent P1-approved entries.
- `ResolveBoss` (BattleStartService.cs L356) compares strings ordinally —
  no logic change is needed or allowed; only the constants flowing into
  it change.
- `BattleEvent.ForBossSkillCast`, `PassiveCharged` / `PassiveTriggered`
  construction sites take `sourceId` as a parameter — they need comment
  fixes only, not code changes.
- Do not touch `boss-hoa-long-rage` when replacing identity strings —
  it is a PassiveId, correct per `BOSS_RULES.md` §6.4.
- `BattleStarted` is not constructed in code yet (comments/frontend test
  list only) — nothing to sync there beyond comment wording.
- Line numbers above are creation-time evidence; re-verify with a search
  before editing (files may shift).

---

## Testing Requirements

### Required Verification

```text
[ ] Unit tests         — BossStateTests, PassiveEventSourceTests,
                          BattleStateSerializationTests, BossResponseTests,
                          BattleStartServiceTests synchronized to §6.4
[ ] Integration tests  — BattleStartEndpointTests, BattleStartSmokeTest,
                          RedisBattleStateSmokeTest (Redis value
                          round-trip), BossResponseWireTests,
                          BossWireProjectionTests (wire sourceId)
[ ] Gameplay scenarios — N/A: no rule or state-transition change; identity
                          value synchronization only
```

### Key Edge Cases

- `bossId` ordinal comparison remains case- and culture-sensitive as today
  — do not "improve" it (task discipline, `AGENTS.md` §16).
- Redis existing-key compatibility: value changes only; a stale key
  holding a display-name value is out of scope (no migration).
- See `docs/01-game-design/BOSS_RULES.md` §6.4 for the identity contract.

---

## Stop Conditions

Universal stops in `AGENTS.md` §20 always apply. Task-specific stops:

**P1 — PRE-DETECTED CONFLICT (fires on execution; human decision required)**

1. Detect: `BossDefinitions.cs` L105 `new BossId("Thủy Ma")` and L129
   `new BossId("Mộc Yêu")` are display-name identity values, mirrored in
   ~15 test locations (marked P1 above).
2. Sources: `BossDefinitions.cs` L105/L129 + P1 test lines vs.
   `BOSS_RULES.md` §6.4 + `tasks/completed/TASK-046-…` §5A (Thủy Ma /
   Mộc Yêu technical IDs deliberately UNRESOLVED).
3. Plain terms: the code cannot be fully synchronized because the
   contract does not yet define the two missing technical IDs.
4. Owning document: `docs/01-game-design/BOSS_RULES.md` §6.4 (domain rule
   doc owns identity values, `AGENTS.md` §2).
5. Smallest correction options: (a) human supplies the two technical IDs
   (convention-derived or other), then full sync proceeds; or (b) human
   approves an explicit deferral of the two Bosses, Hỏa Long portion
   completes, and the P1 items remain flagged.
6. **STOP** — do not implement a guessed version. Never write
   `boss-thuy-ma` / `boss-moc-yeu` or any derived value without human
   approval. The Hỏa Long portion may proceed while awaiting the
   decision.

**Other task-specific stops:**

- If synchronization would require changing an external API contract,
  SignalR payload member, or event name beyond value strings — STOP
  (scope is value/comment/test synchronization only).
- If a DB schema or Redis key structure change appears necessary — STOP
  (none is; `BattleStateJson` key is unchanged).
- If any existing ADR (esp. ADR-001 server-authoritative, ADR-005 Redis,
  ADR-006 PostgreSQL) would be contradicted — STOP per `AGENTS.md` §18.
- If the work expands into TASK-044 (table/migration) or TASK-041
  (BattleResult) — STOP per `AGENTS.md` §16.
- If > 7 skills or multiple uncoupled architectural boundaries are
  needed — STOP & decompose.

---

## Completion Evidence

**Status: DONE** (executed 2026-09-26). The pre-detected P1 STOP condition was
resolved by explicit human decision rather than deferral: the human supplied the
two previously UNRESOLVED technical identities. See **P1 Decision Record**.

### Changed Files

Source (7):

- `src/backend/GameServer.Domain/Bosses/BossDefinitions.cs` — the three
  `BossId` literals changed to `boss-hoa-long` / `boss-thuy-ma` /
  `boss-moc-yeu`; header comment corrected from "the `BossId` is the display
  name (`"Hỏa Long"`), not a slug" to the §6.4 canonical-technical-Identity
  wording (convention, ASCII/lowercase/kebab, "not the display name"); each of
  the three XML `<summary>` blocks now state the canonical Identity and keep the
  display name as content. `PassiveId` and `SkillId` values untouched.
- `src/backend/GameServer.Domain/Bosses/BossId.cs` — removed the stale "There is
  no id format, scheme, or validation rule in any document" paragraph; now
  states the §6.4 value form and that the value is the canonical technical
  Identity, not a display name.
- `src/backend/GameServer.Domain/Match3/BattleEvent.cs` (3 sites) —
  `BossSkillCastEvent` doc comment, its `SourceId` param, the
  `ForBossSkillCast` `sourceId` param, and the `ToString()` diagnostic sample.
- `src/backend/GameServer.Domain/Passives/PassiveEvents.cs` (2 sites) —
  `PassiveChargedEvent.SourceId` and `PassiveTriggeredEvent.SourceId` param
  docs ("display-name BossId … never a slug" → canonical technical Identity).
- `src/backend/GameServer.Application/Battle/BattleStartRequest.cs` —
  `BossId` param doc (display-name identity → canonical technical Identity).
- `src/backend/GameServer.Application/Battle/BattleStartService.cs` —
  `ResolveBoss` doc comment (comparison target and ordinal rationale).
- `src/backend/GameServer.Application/Battle/BattleStateService.cs` — Boss
  Passive step comment above the `sourceId = bossState.BossId.Value` emission.
- `src/backend/GameServer.Api/Hubs/BattleEventWireProjection.cs` (5 sites) —
  `BattleEventWireDto.SourceId` param, `PassiveCharged` / `PassiveTriggered` /
  `BossSkillCast` factory param docs, and the `ProjectPassiveCharged` inline
  comment.

Tests (10):

- `tests/backend/GameServer.Domain.Tests/BossStateTests.cs` — 3 definition
  assertions, the `All` id list, the Element dictionary keys, the identity and
  the Passive-threshold and Skill-timing `InlineData` sets, the `BossId` wrapper
  test, and the `BattleStateCreate` BossId assertion; identity-semantic comments
  corrected.
- `tests/backend/GameServer.Domain.Tests/BattleStateSerializationTests.cs` —
  the representative `BossState` fixture `new BossId(...)` and its round-trip
  assertion.
- `tests/backend/GameServer.Domain.Tests/PassiveEventSourceTests.cs` — charge
  and trigger `SourceId` values + assertions, the `InlineData` pairs, both
  identity-semantic comments, and the renamed
  `BossReports_ShouldUseTheCanonicalIdentityNotADisplayName` (retains the
  `NotEqual("hoa-long")` exclusion and adds `NotEqual("Hỏa Long")`).
- `tests/backend/GameServer.Application.Tests/BattleStartServiceTests.cs` —
  `ValidBossId` constant, the Thủy Ma request + state assertion, and the
  three-Boss acceptance `InlineData` set.
- `tests/backend/GameServer.Application.Tests/BossResponseTests.cs` — Passive
  `sourceId` assertion, `BossSkillCast.SourceId` assertion, test name and
  comment.
- `tests/backend/GameServer.Api.Tests/BattleStartEndpointTests.cs` — 22 request
  `bossId` values and the Mộc Yêu response `bossId` assertion.
- `tests/backend/GameServer.Api.Tests/BattleStartSmokeTest.cs` — request
  `bossId` and the authoritative `BossState.BossId` assertion.
- `tests/backend/GameServer.Api.Tests/RedisBattleStateSmokeTest.cs` — request
  `bossId`.
- `tests/backend/GameServer.Api.Tests/BossResponseWireTests.cs` — the boss
  `sourceId` assertions and the comment describing them.
- `tests/backend/GameServer.Api.Tests/BossWireProjectionTests.cs` — the
  `sourceId` fixtures, assertions, `InlineData` set, and the §3.2.18 comment.

No assertion was removed, weakened, or re-intended; no test was skipped.

### Validation Results

| Depth | Command | Result |
|---|---|---|
| Build | `dotnet build GameServer.sln` | **succeeded, 0 errors** |
| Targeted units | `--filter BossStateTests\|PassiveEventSourceTests\|BattleStateSerializationTests` | **PASS — 107 (Domain.Tests)** |
| Targeted units | `--filter BossResponseTests\|BattleStartServiceTests` | **PASS — 66 (Application.Tests)** |
| Targeted integration | `--filter BattleStartEndpointTests\|BattleStartSmokeTest\|RedisBattleStateSmokeTest\|BossResponseWireTests\|BossWireProjectionTests` | **PASS — 53 (Api.Tests)** |
| Full backend | `dotnet test GameServer.sln` | **PASS — 1340 tests, 0 failed, 0 skipped** |

Per-project full-suite totals: Domain 911, Application 210, Api 115,
Infrastructure 104.

- **Redis** — `RedisBattleStateSmokeTest` ran against a real Redis at
  `127.0.0.1:6379` (reported `PING`, `FAIL`-on-unreachable by design, TTL and
  CAS assertions included) and passed.
- **SignalR/API** — `BossResponseWireTests`, `BossWireProjectionTests`, and the
  SignalR push leg of `RedisBattleStateSmokeTest` passed; event names, payload
  member names, and protocol shape unchanged (comment-only diff in
  `BattleEventWireProjection.cs`).
- **Redis value round-trip (verified, then probe removed)** — a temporary probe
  reusing the smoke test's real-Redis host observed
  `response initialState.bossState.bossId = boss-hoa-long` and
  `redis battle:{battleId}:state → bossState.bossId = boss-hoa-long`, matching
  `BossDefinitions.HoaLong.BossId.Value`. The probe file and its temporary
  accessor widening were deleted; `RedisBattleStateSmokeTest.cs` was restored to
  its original `private sealed class SmokeFactory` form. The permanent
  serializer round-trip is covered by
  `BattleStateSerializationTests.RoundTrip_ShouldPreserveEveryBossMember`.

### Repository Search

- **Before:** 84 quoted display-name literals across `tests/backend` and 3
  `new BossId("…")` source literals; `BossId("Hỏa Long")` at
  `BossDefinitions.cs` L82, `BossId("Thủy Ma")` L105, `BossId("Mộc Yêu")` L129.
- **After:** `BossId("Hỏa Long"|"Thủy Ma"|"Mộc Yêu")` → **0 matches**. No
  display-name value remains as `sourceId`, `sourceId =`, `bossId =`, or a
  `ForBossSkillCast` argument (all four patterns searched → 0 matches).
- Remaining display-name occurrences are all human-readable comment/content
  prose (e.g. `BattleStateService.cs` L993, `BossState.cs`, `BossDefinition.cs`,
  test fixture comments) plus one intentional negative assertion
  (`PassiveEventSourceTests.cs` — `Assert.NotEqual("Hỏa Long", …)`).
- Zero remaining source comments claim `BossId` is a display name or is
  undocumented in format.

### P1 Decision Record

- **Not deferred — resolved.** Human decision (this execution):

  ```text
  Hỏa Long  → boss-hoa-long
  Thủy Ma   → boss-thuy-ma
  Mộc Yêu   → boss-moc-yeu
  ```

  These are the values a human supplied; no value was invented or derived by the
  agent. They follow the TASK-046 convention
  `boss-<ascii-kebab-case-name>` (ASCII, lowercase, kebab-case, no diacritics).
  The §5A TASK-046 record left Thủy Ma / Mộc Yêu `UNRESOLVED` in
  `BOSS_RULES.md` §6.4; per the task's Stop Conditions P1 option (a), the human
  supplied the two IDs and full synchronization proceeded. **No `docs/` edit was
  made** — the task scope forbids it, and the identity values were supplied
  directly by the human for source/test synchronization.

### Server Authority & Scope Verification

- [x] Confirmed zero client-authoritative gameplay logic (identity values only;
      no `src/frontend` change)
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1) — no new Boss, system,
      content, or localization
- [x] Confirmed no `docs/` or `tasks/` files modified (verified by modification
      timestamps: docs/tasks 6:02–6:11 PM, task edits 6:41 PM+)
- [x] No TASK-041 / TASK-043 / TASK-044 / TASK-045 / TASK-046 file modified
- [x] No DB schema or migration; no `BossDefinition` persistence; no
      `BattleResult`
- [x] No Redis architecture change — key `battle:{battleId}:state`, 30-minute
      TTL, CAS, repository API, and serializer member names unchanged
- [x] No SignalR shape change — event names and payload member names unchanged;
      no `displayName` member added
- [x] Identity flow structurally unchanged (`BossDefinition.BossId` →
      `BossState.BossId` → `sourceId` → API `bossId` → Redis `bossId`); only the
      stale values changed
- [x] No resolver, mapper, service, or new identity abstraction introduced
- [x] `PassiveId` values (`boss-hoa-long-rage`, `boss-thuy-ma-heal`,
      `boss-moc-yeu-regen`) and all `SkillId` values untouched

