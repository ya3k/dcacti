# TASK-074 — Converge Battle-Start `initialState` Element onto the Documented Wire Value Set

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ by path and section; copies only what an implementer
  needs to know where to look.

  This is the convergence implementation task that TASK-071, TASK-072 and
  TASK-073 each recorded as a future follow-up and deliberately did NOT
  create. The contract is now written (API_CONTRACTS.md v1.15 §3 + §5.1);
  this task makes the code match it.

  BUG category (development/bug-fix.md §1): implementation bug — docs
  describe intended behavior, code deviates. No document changes.
  No gameplay. No Redis. No SignalR. No docs/ edit.
-->

---

## Metadata

```text
Task ID:           TASK-074
Type:              BUG (development/bug-fix.md §1 — implementation bug:
                   API_CONTRACTS.md §3/§5.1 state the intended value set,
                   BattleStartStateSummary.cs emits the enum member name)
Status:            DONE
Risk:              MEDIUM (baseline for BUG is LOW–MEDIUM; raised because the
                   fix changes a client-visible wire value on an existing
                   endpoint — core/validation.md §2 MEDIUM = integration tests
                   across the API boundary + documentation validation)
Priority:          HIGH (the contract states the behavior and the code does
                   not implement it — the documented intent of TASK-071/072/073
                   is unimplemented; the REST surface currently answers with a
                   value §5.1 explicitly excludes)
Primary Agent:     backend (bug is in the Api layer — AGENT_SELECTION:
                   "Bug in API response → Backend Agent"; backend.md
                   "Can modify: src/GameServer.Api/*")
Supporting Agents: testing (regression + contract coverage),
                   review (contract conformance + scope verification)
Workflow:          development/bug-fix.md
Skills:            discovery/documentation-discovery, quality/documentation-consistency,
                   backend/api-contract-validation, quality/architecture-conformance,
                   testing/test-scenario-generation   (5 — Normal budget 3–5)
Dependencies:      TASK-071 — DONE (records this divergence as Known Follow-up #1),
                   TASK-072 — DONE (owns the REST element value set in §5.1),
                   TASK-073 — DONE (binds §3's initialState Element members to
                   that set and fixes Redis encoding as unbound — this task is
                   the implementation TASK-073 explicitly deferred)
```

---

## Objective

Make `POST /api/battle/start`'s `initialState.petState.element` and
`initialState.bossState.element` emit the Element wire value set already bound
by `API_CONTRACTS.md` §3 and §5.1 — projecting through the existing
`ElementWireValues.ToWireValue` — instead of `Element.ToString()`, so that the
battle-start REST response stops emitting a value the contract excludes
(`"Hoa"`, `"Thuy"`, …) and every `element` member on the documented REST
surface carries the English form.

---

## Authoritative References

- `docs/02-technical/API_CONTRACTS.md` **§3** (the paragraph
  "`initialState`'s `Element` members are wire values" — `petState.element`
  and `bossState.element` carry the §5.1 set, "not a display name, not an enum
  member name, and not a localized form") and **§5.1** (the binding member
  table `element = "Fire" | "Water" | "Earth" | "Wood" | "Metal"`; the scope
  statement that this set is the Element wire value set for the whole REST
  surface and names `initialState.petState.element` /
  `initialState.bossState.element` explicitly; the Vietnamese names are
  display values only; and the explicit carve-out that §5.1 does **not** bind
  the Redis record's encoding)
- `docs/02-technical/GAME_STATE.md` §2.3 (`PetState` composition), §2.4
  (`BossState` composition) — member ownership only; GAME_STATE defines state
  composition, not serialization
- `docs/02-technical/REDIS_STATE.md` §2 (active-state record Element encoding
  is **unbound** — constrains what must NOT change)
- `docs/01-game-design/ELEMENT_RULES.md` §1 (five Elements), §6 (MVP Pet
  Element assignments)
- `docs/01-game-design/BOSS_RULES.md` §6 (exactly three MVP Bosses and their
  Elements), §6.4 (canonical technical Boss Identity)
- `docs/01-game-design/PET_RULES.md` §1 (a Pet has exactly one Element)
- `docs/02-technical/ARCHITECTURE.md` §2.1 (layers, dependency direction, thin
  controller), §5 (anti-overengineering)
- `docs/00-overview/MVP_SCOPE.md` §1 (battle flow / Pets / Bosses IN), §2/§4
- `docs/03-decisions/ADR/ADR-001` (server authority — the server alone decides
  what is on the wire)
- `tasks/completed/TASK-072-define-canonical-element-wire-values.md` —
  canonical value set + display-only semantics (immutable)
- `tasks/completed/TASK-073-resolve-battle-state-element-value-set.md` —
  decisions D1–D3, including "D2 requires changing **only**
  `BattleStartStateSummary.cs` (L92, L113)" and "D3 is Branch B —
  `BattleStateSerializer.cs` keeps `Element.ToString()`" (immutable)

---

## Bug Classification (development/bug-fix.md §1)

```text
Category      Implementation bug
Expected      API_CONTRACTS.md §3: petState.element / bossState.element carry
              the §5.1 Element wire value set ("Fire" | "Water" | "Earth" |
              "Wood" | "Metal") — never an enum member name.
Actual        BattleStartStateSummary.cs L92 / L113 call Element.ToString(),
              which emits the Domain member names ("Moc", "Tho", "Thuy",
              "Hoa", "Kim").
Root cause    The summary predates TASK-072/073 and was written as a literal
              "pure field mapping"; no explicit Element projection existed in
              the Api layer until TASK-072 created ElementWireValues (then
              used only by the §5 collection responses).
Docs or code wrong?   CODE. §3 and §5.1 were both written after the code and
              state the intended behavior. No document conflict exists
              (§3 → §5.1 owns the set; §5.1 → REDIS_STATE.md §2 owns Redis),
              so no §4 stop condition fires.
```

---

## Scope

### In Scope

- `src/backend/GameServer.Api/Controllers/BattleStartStateSummary.cs`:
  - **L92** `Element: petState.Element.ToString()` →
    `Element: ElementWireValues.ToWireValue(petState.Element)`
  - **L113** `Element: bossState.Element.ToString()` →
    `Element: ElementWireValues.ToWireValue(bossState.Element)`
  - the `using GameServer.Application.Collection;` directive this requires
  - correction of the comments this edit makes false — L90–91 ("as the
    documented contract name") and the class summary's absolute "pure field
    mapping … read one-to-one … nothing is computed" wording, which must now
    record the Element member as the one explicit wire projection
- `src/backend/GameServer.Application/Collection/ElementWireValues.cs` —
  doc-comment correction only: L39–44 states the battle-start convergence
  "is a separate task and is not done here", which becomes false on landing
  this task. **No change to `ToWireValue`'s behaviour.**
- `tests/backend/GameServer.Api.Tests/BattleStartEndpointTests.cs` — new
  coverage per Testing Requirements (the factory's hardcoded
  `Element = Element.Moc` at L694 must be parameterized to seed the five
  Elements)
- `tests/backend/GameServer.Application.Tests/ElementWireValuesTests.cs` —
  rationale-comment refresh only (TASK-073 §Note item 3 invites it); the
  quoted §5.1 wording was superseded by v1.15. **No assertion may change.**
- Backend test execution and the MEDIUM validation plan
  (`core/validation.md` §2: integration tests across the API boundary +
  documentation validation).

### Out of Scope

- **`src/backend/GameServer.Domain/**` — untouched, entirely.** In particular
  `Battle/Serialization/BattleStateSerializer.cs` MUST keep `Element.ToString()`
  (TASK-073 D3 Branch B; `REDIS_STATE.md` §2 leaves the Redis encoding unbound,
  so it is not a defect). See also Known Follow-up #1 for the stale comment in
  `Domain/Elements/Element.cs`.
- **Any `docs/` edit of any kind.** §3 and §5.1 are final as of TASK-073. If a
  documentation change appears necessary → STOP (§Stop Conditions).
- `REDIS_STATE.md`, `GAME_STATE.md`, `SIGNALR_PROTOCOL.md`, `GAME_EVENTS.md`
  — no contract, no state shape, no event payload changes.
- SignalR / `BattleEventWireProjection` / any event payload — no documented
  event carries an `element` member (asserted by
  `BossWireProjectionTests.cs` L198).
- Redis serialization, `BattleState` model, battle creation, `BattleStartService`.
- `src/frontend/client/**` — the REST `initialState` element members are not
  read anywhere in the client (see Known Follow-up #2).
- **A second element mapping** (`BattleElementWireValues`,
  `ElementWireMapper`, a Domain `ElementExtensions.ToWireString()`, …). The
  mapping exists; reuse it or STOP.
- A fourth MVP Boss or a sixth Element invented to widen test coverage
  (`BOSS_RULES.md` §6, `ELEMENT_RULES.md` §1, `AGENTS.md` §7).
- `bossState.state` / `specialGem` / `orientation` projections (L118, L130) —
  different members, different rules, not element values.
- New ADR, new abstraction, new DI registration, new project reference, new
  test project, changes to TASK-071 / TASK-072 / TASK-073 (immutable).
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2.

---

## Current State

`BattleStartStateSummary.ForAsync` (called from `BattleController.cs` L159)
projects the created `BattleState` onto the §3 `initialState`. Two of its
members are produced with `Element.ToString()` — L92 in `ToPetState`, L113 in
`ToBossState` — so a battle started with the Mộc Pet (the seeded fixture) and
Hỏa Long answers `petState.element: "Moc"` and `bossState.element: "Hoa"`,
while `GET /api/pets` already answers `"Wood"`/`"Fire"` from the same
`Element` values. `ElementWireValues.ToWireValue` already exists in
`GameServer.Application/Collection/` (public, total over the five Elements,
refuses anything outside them) and the Api layer already references
Application for exactly this purpose (`CollectionResponses.cs` L37). No backend
test currently asserts the battle-start element value, and no backend source or
test file contains the string `"Hoa"` — so nothing asserts today's incorrect
behaviour, and no existing assertion has to be relaxed.

---

## Acceptance Criteria

### Wire values

- [x] `POST /api/battle/start` → 200: `initialState.petState.element` is one of
      `"Fire" | "Water" | "Earth" | "Wood" | "Metal"`.
- [x] The same response's `initialState.bossState.element` is one of
      `"Fire" | "Water" | "Earth" | "Wood" | "Metal"`.
- [x] A REST test theory asserts **all five mappings** on
      `initialState.petState.element` — `Moc→Wood`, `Tho→Earth`, `Thuy→Water`,
      `Hoa→Fire`, `Kim→Metal` — by seeding a `PetDefinition` with each
      `Element`.
- [x] A REST test theory asserts the mapping on
      `initialState.bossState.element` for **each of the three canonical MVP
      Bosses**: `boss-hoa-long→Fire`, `boss-thuy-ma→Water`, `boss-moc-yeu→Wood`.
- [x] Tests additionally assert that neither `petState.element` nor
      `bossState.element` ever equals one of the Domain enum member names
      (`"Moc"`, `"Tho"`, `"Thuy"`, `"Hoa"`, `"Kim"`).
- [x] No `Element.ToString()` call remains in `BattleStartStateSummary.cs`.

### Contract & layering

- [x] Both members are produced by the single existing
      `ElementWireValues.ToWireValue`; no second element mapping type, helper,
      or constant set is created anywhere.
- [x] `src/backend/GameServer.Domain/Battle/Serialization/BattleStateSerializer.cs`
      is byte-for-byte unchanged (`git diff` empty for that path), and the
      existing Domain serialization tests pass unchanged.
- [x] The fix is confined to the Api projection (plus tests and the two
      doc-comment corrections named in In Scope); no project reference, DI
      registration, or ADR is added.
- [x] The now-false `ElementWireValues.cs` L39–44 comment ("separate task … not
      done here") and `BattleStartStateSummary.cs` L90–91 / class-summary
      wording are corrected in the same change.
- [x] Zero `docs/` files modified.
- [x] Zero `src/frontend/` files modified.

### Verification

- [x] No pre-existing test assertion is weakened or deleted (comment-only
      edits are permitted and must be reported as such).
- [x] Full backend suite passes at MEDIUM depth (`core/validation.md` §2):
      build 0 errors, `dotnet test src/backend/GameServer.sln` green.
- [x] Quality review checklist passes (`quality/review.md` §1).
- [x] No authoritative rule or contract violated (`AGENTS.md` §10 / ADR-001).

---

## Affected Files & Areas

```text
[x] src/backend/        Api only — BattleStartStateSummary.cs (2 call sites,
                        using directive, comments) + ElementWireValues.cs
                        (doc comment only)
[ ] src/frontend/client/ (no change — no client reads these members)
[x] tests/              Api.Tests (battle-start element coverage) +
                        Application.Tests (comment refresh only)
[ ] docs/               (no change — §3/§5.1 are final; doc edit required → STOP)
```

---

## Implementation Notes

- **Exact edit sites.** `BattleStartStateSummary.cs` L92 and L113. Nothing
  else in that file changes behaviour. `bossState.State.ToString()` (L118),
  `GemTypes.ToContractName` (L127) and the Special Gem projections (L130) are
  different members and stay as they are.
- **Reuse, do not duplicate.** `ElementWireValues` is in
  `GameServer.Application.Collection`; add the `using`. `CollectionResponses.cs`
  already references it, so the Api → Application dependency exists and no
  layering question is open. The summary already contains an explicit
  contract-name projection precedent (`GemTypes.ToContractName` at L127), so
  "pure field mapping" never meant "enum member names on the wire".
- **If `ElementWireValues` cannot be referenced from Api as-is → STOP.** Do
  not create a second mapping, do not move the type, do not add a Domain
  helper (§Stop Conditions).
- **`bossState.element` coverage is deliberately 3, not 5.**
  `BattleStartService.ResolveBoss` (L364–380) resolves against the fixed
  `BossDefinitions.All` = `HoaLong`, `ThuyMa`, `MocYeu`, and `BOSS_RULES.md`
  §6 defines exactly those three — so `bossState.element` can only ever be
  Fire/Water/Wood on this endpoint. Asserting Earth/Metal there would require
  inventing a fourth MVP Boss (`AGENTS.md` §7). Earth/Metal coverage for the
  Boss field is provided by the shared, exhaustively-tested
  `ElementWireValues.ToWireValue` plus the never-enum-name and
  in-set guards; the five-pair coverage requirement is satisfied on
  `petState.element`, whose Element is data (`PetDefinition.Element`) and
  therefore seedable five ways. Record this reasoning in the completion
  report.
- **Test fixture.** `BattleStartFactory.SeedPlayerAsync` hardcodes
  `Element = Element.Moc` (`BattleStartEndpointTests.cs` L694); parameterize
  it (the existing `PetOwnerIsAnotherPlayer`/`OwnedRelicInstanceIds` init-only
  properties are the local precedent) so the theory can seed each Element.
  Boss coverage needs no seeding change — the three Bosses resolve from
  `BossDefinitions.All`.
- **Style precedent for the value pairs**: `CollectionEndpointTests.cs`
  L187–191 (`[InlineData(Element.Moc, "Wood")]` …).
- **Doc comments are part of this fix.** `ElementWireValues.cs` L39–44 and
  `BattleStartStateSummary.cs` L90–91 become false statements the moment the
  two call sites change; leaving them would be a documentation defect
  introduced by this task (`AGENTS.md` §17).
- **Do not "fix" anything else you notice.** Report unrelated findings per
  `AGENTS.md` §16 (see Known Follow-up).

---

## Testing Requirements

### Required Verification

```text
[ ] Unit tests         — ElementWireValuesTests (all five pairs, never-the-enum-
                         name, out-of-set refusal) still pass unchanged; new
                         coverage for BattleStartStateSummary's two call sites
                         as reachable through the endpoint
[ ] Integration tests  — REST layer, POST /api/battle/start 200 body:
                         petState.element theory over the five Elements;
                         bossState.element theory over the three canonical
                         Boss identities; neither member ever a Domain enum
                         member name; both members present and string-typed
[ ] Gameplay scenarios — N/A: this is a wire projection of an existing state
                         member; no gameplay rule, formula, modifier or
                         resolution order is exercised (document this in the
                         completion report)
```

### Key Edge Cases

- Pet seeded with each of the five Elements → correct English value, in the
  exact case (`Fire`, not `fire`/`Hỏa`).
- Each of the three Boss identities → its documented Element
  (`BOSS_RULES.md` §6).
- The trap cases `Moc→Wood` and `Kim→Metal` are asserted explicitly (they are
  the pairs a naive translation gets wrong).
- `ElementWireValues.ToWireValue` still throws for a value outside the five
  (defence kept — a bad value must be refused, never passed through).

### Regression

- [ ] Full existing backend suite passes unchanged
      (`dotnet test src/backend/GameServer.sln`) — no existing test may be
      weakened.
- [ ] Domain serialization tests (`BattleStateSerializationTests`) pass with
      `BattleStateSerializer.cs` untouched.
- [ ] `git status` confirms `src/frontend/` and `docs/` are untouched; the
      frontend suite is therefore not required by this task.

---

## Stop Conditions

Universal stop conditions in `AGENTS.md` §20 and `.ai/README.md` §13 always
apply. Task-specific:

- If `API_CONTRACTS.md` §3/§5.1 conflict with another authoritative document →
  STOP per `AGENTS.md` §4; do not pick the easier reading.
- If `ElementWireValues.ToWireValue` cannot be reused from the Api layer →
  STOP and report; **do not create a second element mapping**.
- If any change to `BattleStateSerializer.cs`, the Redis record, or
  `REDIS_STATE.md`'s encoding status appears necessary → STOP (TASK-073 D3).
- If any `docs/` edit appears necessary → STOP (the contract is final).
- If a new ADR appears required → STOP (`AGENTS.md` §18).
- If a fourth MVP Boss, a sixth Element, or any new gameplay content appears
  necessary to satisfy coverage → STOP (`AGENTS.md` §7, `BOSS_RULES.md` §6).
- If the scope cannot be isolated from SignalR, Redis, battle creation, or the
  client → STOP & decompose.
- If implementation would modify TASK-071, TASK-072 or TASK-073 → STOP
  (all immutable).
- If implementation would edit `GameServer.Domain/` → STOP (`backend.md`
  Non-Responsibilities); hand off per Known Follow-up #1.
- If the task would exceed 5 skills or cross multiple uncoupled architectural
  boundaries → STOP & decompose.
- Do not guess.

---

## Known Follow-up (recorded, deliberately NOT part of this task)

1. **Stale comment in `src/backend/GameServer.Domain/Elements/Element.cs`**
   claiming no API Element serialization contract exists — outdated since
   TASK-072 and further outdated by this task. It is **outside this task's
   scope**: the file lives in `GameServer.Domain/`, which `backend.md`
   ("Non-Responsibilities": *Must NOT modify GameServer.Domain/*) bars the
   Backend agent from editing, ownership is the Gameplay agent, and a comment
   correction there is not required to make the battle-start wire correct.
   TASK-071 already recorded it as excluded and "may be folded into follow-up
   #1". **Suggested follow-up task**: a small comment-only correction assigned
   to the Gameplay agent — do not implement it from this task
   (`AGENTS.md` §16).
2. **`src/frontend/client/tests/GameRuntime.test.ts` L751 `element: 'Hoa'`**
   sits inside a payload that test asserts is *dropped* (L735–792), and the
   client never reads REST `initialState.petState.element`. Not a wire
   dependency; report only (TASK-073 §Note item 4).
3. **No fourth MVP Boss exists**, so `bossState.element` can only reach three
   of the five values. If product design ever adds Bosses, the existing
   theory extends without code change; nothing to do now.

---

## Completion Evidence

### Changed Files

- `src/backend/GameServer.Api/Controllers/BattleStartStateSummary.cs` — the fix.
  Added `using GameServer.Application.Collection;`. **L92**
  `Element: petState.Element.ToString()` →
  `Element: ElementWireValues.ToWireValue(petState.Element)`; **L113**
  `Element: bossState.Element.ToString()` →
  `Element: ElementWireValues.ToWireValue(bossState.Element)`. Comment refresh:
  the class summary's "It is a pure field mapping … Nothing is computed" wording
  now records the Element as the one explicit wire projection (with the existing
  `GemTypes.ToContractName` precedent named), and the two now-false
  "the documented contract name" comments state the §3/§5.1 binding instead.
- `src/backend/GameServer.Application/Collection/ElementWireValues.cs` —
  **doc-comment correction only** (L39–44). Its "Converging that path on this
  vocabulary is a separate task and is not done here" claim became false on
  landing this task; it now records that the battle-start REST summary uses this
  type while `BattleStateSerializer` deliberately does not (unbound Redis
  encoding, TASK-073 D3 Branch B). **`ToWireValue`'s behaviour is unchanged.**
- `tests/backend/GameServer.Api.Tests/BattleStartEndpointTests.cs` — new
  coverage + fixture parameterization. `BattleStartFactory.SeedPlayerAsync`'s
  hardcoded `Element = Element.Moc` is now the init-only `PetElement` property
  (defaulting to `Element.Moc`, so every existing test is unaffected). Three new
  tests: a five-pair petState theory, a three-Boss bossState theory, and a
  never-a-Domain-enum-name regression guard over all three MVP Bosses.
- `tests/backend/GameServer.Application.Tests/ElementWireValuesTests.cs` —
  **comment-only refresh** (L56–57). The rationale comment quoted the superseded
  pre-v1.15 §5.1 sentence; it now states the binding in the current wording.
  **No assertion changed** (TASK-073 §Note item 3 invite).
- `tasks/backlog/TASK-074-…md` → `tasks/completed/TASK-074-…md` — this evidence;
  Status `BACKLOG` → `DONE`.

**Not changed (verified by `git diff`/`git status`):**
`src/backend/GameServer.Domain/**` (zero files — `BattleStateSerializer.cs`
included), `src/frontend/**` (zero files), `docs/**` (zero files),
`src/backend/GameServer.Application/Battle/**`,
`GameServer.Infrastructure/**`, and TASK-071/072/073.

### Element Value Mapping

```text
Moc  → Wood
Tho  → Earth
Thuy → Water
Hoa  → Fire
Kim  → Metal
```

### Validation Results

- `dotnet build src/backend/GameServer.sln --no-incremental` — **0 errors**
- `dotnet test src/backend/GameServer.sln` — **PASS (1760 tests, 0 failed,
  0 skipped)**:
  - `GameServer.Api.Tests` — 242 passed
  - `GameServer.Domain.Tests` — 951 passed
  - `GameServer.Application.Tests` — 350 passed
  - `GameServer.Infrastructure.Tests` — 217 passed
- Focused `BattleStartEndpointTests` — **32 passed** (23 pre-existing + 9 new
  cases). Focused `ElementWireValuesTests` — **17 passed, unchanged**.
- Focused `BattleStateSerializationTests` — **48 passed** with
  `BattleStateSerializer.cs` untouched (`git diff` empty for
  `GameServer.Domain/`).
- **Regression proof (bug-fix workflow requirement).** The three new tests were
  run against the pre-fix implementation (`Element.ToString()` restored
  temporarily): **all 9 cases FAILED** (e.g. `Expected: "Earth"`, `Actual:
  "Tho"`). Restored and re-run: **all 9 pass**. The tests therefore fail before
  the fix and pass after it, rather than encoding current behaviour.
- Frontend suite — **not run, deliberately.** `git status` confirms
  `src/frontend/` is untouched, and `grep` of `src/frontend/client/src` finds no
  reader of `initialState` / `petState.element` / `bossState` — so no client
  behaviour can depend on the changed value, exactly as the task's Regression
  section states. `GameRuntime.test.ts` L751's `element: 'Hoa'` is a stripping
  fixture asserted *dropped* and is left unchanged.

### Server Authority & Scope Verification

- [x] Confirmed zero client-authoritative gameplay logic
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
- [x] No `GameServer.Domain/` file modified; `BattleStateSerializer.cs` unchanged
- [x] No Redis, SignalR, database, migration, authentication, or rule change
- [x] No `docs/` file modified; no `src/frontend/` file modified
- [x] One element mapping only — `ElementWireValues.ToWireValue`

### Required Completion Report Fields

```text
Status:                  DONE
Files Changed:           BattleStartStateSummary.cs (fix + comments);
                         ElementWireValues.cs (doc comment only);
                         BattleStartEndpointTests.cs (tests + fixture param);
                         ElementWireValuesTests.cs (comment only);
                         TASK-074 file (evidence + status)
Element Values Emitted:  petState.element  → "Fire" | "Water" | "Earth" |
                                            "Wood" | "Metal" (all five
                                            reachable; each asserted)
                         bossState.element → "Fire" | "Water" | "Wood" (the
                                            three canonical MVP Bosses)
Mapping Used:            ElementWireValues.ToWireValue (reused, not duplicated)
Tests:                   backend suite before: 1751 passed / 0 failed
                         backend suite after:  1760 passed / 0 failed (0 weakened;
                         9 added; 2 files comment-only)
Build:                   0 errors (clean, --no-incremental)
Redis / BattleStateSerializer: unchanged
Regression:              9 new cases fail pre-fix, pass post-fix
Scope Verification:      Out of Scope list confirmed — Domain, Redis,
                         BattleStateSerializer, SignalR, frontend, docs,
                         gameplay, migrations all untouched; no second mapping,
                         no new Boss/Element, no new ADR or abstraction
Known Follow-up:         Domain/Elements/Element.cs stale comment (gameplay
                         agent) — NOT done here; frontend GameRuntime.test.ts
                         L751 "Hoa" fixture — report only
Documentation Changes:   none (comments corrected in code only)
Next Step:               None for this task. The two reported follow-ups are
                         report-only and create no task here.
```

### Boss Coverage Rationale (3 of 5, by design)

`bossState.element` coverage is deliberately three values, not five.
`BattleStartService.ResolveBoss` resolves against the fixed
`BossDefinitions.All` = `HoaLong`, `ThuyMa`, `MocYeu`, and `BOSS_RULES.md` §6
defines exactly those three — so the endpoint can only ever emit Fire/Water/Wood
there. Asserting Earth/Metal on this member would require inventing a fourth MVP
Boss (`AGENTS.md` §7), which this task explicitly forbids. Earth/Metal remain
covered for the shared mapping by the exhaustively-tested
`ElementWireValues.ToWireValue` (all five pairs) and, at the REST boundary, by
the five-pair `petState.element` theory — the Pet's Element is data
(`PetDefinition.Element`) and is therefore seedable five ways. The
never-an-enum-name and in-set guards run on **both** members for all three
Bosses.

### Gameplay Scenarios — N/A (documented)

No gameplay scenario test is required: this is a wire projection of an existing
state member. No gameplay rule, formula, modifier, resolution order, RNG draw, or
state transition is exercised or altered — the created `BattleState` is
byte-identical and only its REST *spelling* of two members changed. Confirmed by
the full Domain/Application suites passing unchanged (gameplay behaviour is
untouched) and by the `--no-incremental` clean build.

