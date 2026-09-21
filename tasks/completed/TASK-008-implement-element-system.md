# TASK-008 — Implement Element System

---

## Metadata

```text
Task ID:           TASK-008
Type:              FEATURE
Status:            DONE
Risk:              LOW
Priority:          MEDIUM
Primary Agent:     gameplay
Supporting Agents: backend, testing
Workflow:          development/feature.md
Skills:            gameplay-behavior-derivation, test-scenario-generation
Dependencies:      None
```

---

## Objective

Implement the Element System defined by `docs/01-game-design/ELEMENT_RULES.md`:
the five Elements, the Tương Khắc overcoming cycle, matchup resolution, and
the element damage modifier lookup. This produces a pure Domain module with no
infrastructure dependencies, consumed downstream by the Combat pipeline.

---

## Context

Phase 1 (Core Loop Vertical Slice) requires Damage calculation that includes
an Element Modifier (`ROADMAP.md` Phase 1, `COMBAT_RULES.md` damage pipeline).
The Element System is the smallest documented-but-unbuilt responsibility that
unblocks Combat, Pets, and Bosses. It has no dependencies on other unbuilt
systems and is explicitly in MVP scope (`MVP_SCOPE.md` §1).

---

## Authoritative Sources

- `docs/00-overview/MVP_SCOPE.md` §1 — confirm Elements are IN scope
- `docs/01-game-design/GAME_RULES.md` §8 — Element overview, Tương Khắc
- `docs/01-game-design/ELEMENT_RULES.md` — the primary contract (all sections)
- `docs/01-game-design/COMBAT_RULES.md` §3 — where Element Modifier sits in
  the damage pipeline (reference only; Combat is not implemented here)
- `docs/02-technical/GAME_STATE.md` — no Element state on BoardState or
  PlayerState (Elements belong to Pet/Boss/Skill/Effect, not to the board)
- `docs/02-technical/ARCHITECTURE.md` §1 — `Elements/` is its own domain
  module

---

## Scope

### In Scope

- Domain: `Element` enum (5 values)
- Domain: Tương Khắc overcoming cycle (Mộc→Thổ→Thủy→Hỏa→Kim→Mộc)
- Domain: Matchup resolution (Advantage / Neutral / Disadvantage)
- Domain: Element Modifier lookup (1.50× / 1.00× / 0.75×)
- Domain: Elementless attack handling (Neutral against any element)
- Tests: unit tests for matchup resolution, modifier values, and
  elementless edge cases

### Out of Scope

- Combat damage pipeline (`COMBAT_RULES.md` §3) — Element Modifier is
  consumed by Combat, not implemented here
- Pet/Boss/Skill/Effect Element ownership — those data models belong to
  their own tasks
- Element assignments on entities (§6) — content data, not this task
- Tương Sinh, dual-element, environment modifiers (`ELEMENT_RULES.md` §7)
- Any system listed in `docs/00-overview/MVP_SCOPE.md` §2 (OUT)

---

## Current State

No Element types, enums, or matchup logic exist anywhere in the codebase.
`BoardState.Cells` carries `GemType` (ATK/DEF/HP/POWER) which is not an
Element. `PlayerState` carries only `MatchCount` and `Combo`. No downstream
consumer of Element data exists yet.

---

## Acceptance Criteria

- [x] An `Element` enum exists with exactly five values: `Moc`, `Hoa`,
      `Tho`, `Kim`, `Thuy`
- [x] A matchup function returns `Advantage` when the attacking Element
      counters the defending Element per the Tương Khắc cycle
      (`ELEMENT_RULES.md` §2)
- [x] A matchup function returns `Disadvantage` when the defending Element
      counters the attacking Element
- [x] A matchup function returns `Neutral` when Elements are equal or
      either is absent
- [x] An element modifier function returns 1.50× for Advantage, 1.00× for
      Neutral, 0.75× for Disadvantage (`ELEMENT_RULES.md` §2.2)
- [x] An elementless attack against any defending Element resolves as
      Neutral (`ELEMENT_RULES.md` §3)
- [x] An attack against an elementless target resolves as Neutral
- [x] Domain has no dependency on Application/Infrastructure/Api types
      (`ARCHITECTURE.md` §2.1)
- [x] All relevant tests pass at the depth required by `core/validation.md §2`
      for Risk LOW
- [x] `quality/review.md §1` checklist passes
- [x] Documentation impact addressed (§ Documentation Impact below)

---

## Affected Areas

```text
[x] Domain (GameServer.Domain/) — new Elements module
[ ] Application (GameServer.Application/)
[ ] API (GameServer.Api/)
[ ] Client (client/)
[ ] SignalR / Redis
[ ] PostgreSQL
[x] Tests (tests/)
[ ] Documentation (docs/)
[ ] ADR (docs/03-decisions/ADR/)
```

---

## Implementation Notes

1. **Domain module location.** Place under `src/backend/GameServer.Domain/Elements/`
   per `ARCHITECTURE.md` §1 (Elements is its own domain module).
2. **Enum casing.** Use PascalCase enum values matching the Vietnamese names
   (`Moc`, `Hoa`, `Tho`, `Kim`, `Thuy`). The wire representation is not
   defined by this task — no serialization contract exists for Element yet.
3. **Modifier values are configuration.** `ELEMENT_RULES.md` §2.2 states these
   are configuration values, not hardcoded constants. Expose them as
   configurable (e.g. static properties or a small config record) rather than
   bare literals, so a future balance change does not require a code change.
4. **No Entity ownership yet.** This task defines the Element type and matchup
   logic only. Pet/Boss/Skill/Effect Element ownership is a separate task.
   The module must not reference Pet, Boss, or any other entity type.
5. **Tests must cover the full matchup matrix.** All 5×5 attacking-vs-defending
   combinations, plus elementless-vs-element, element-vs-elementless, and
   elementless-vs-elementless.

---

## Testing Requirements

### Test Types Required

```text
[x] Unit tests         — matchup resolution matrix (25 combinations + 3
                         elementless cases), modifier values, elementless
                         attack handling
[ ] Integration tests  — n/a (no integration boundary yet)
[ ] Gameplay scenarios — n/a (no gameplay pipeline consumes Element yet)
[ ] API tests          — n/a
[ ] Realtime tests     — n/a
[ ] Persistence tests  — n/a
```

### Key Edge Cases

- `ELEMENT_RULES.md` §3 — elementless attack against any element = Neutral
- `ELEMENT_RULES.md` §3 — attack against elementless target = Neutral
- `ELEMENT_RULES.md` §2.1 — A==D (same element) = Neutral, not Advantage
- `ELEMENT_RULES.md` §4 — Element does not restrict builds (no validation
  logic needed here; just ensure the module does not impose build constraints)

---

## Documentation Impact

**Option A — None:**

> This task implements already-documented behavior. `ELEMENT_RULES.md`
> defines the full contract. No doc changes are required.

---

## Stop Conditions

- If the required behavior cannot be fully derived from the
  Authoritative Sources listed above: STOP per `AGENTS.md §7`.
- If implementation needs to reference Pet, Boss, or any entity type:
  STOP — Element ownership is a separate task.
- If two authoritative documents are found to conflict: STOP per
  `AGENTS.md §4`.

---

## Dependencies

- None.

---

## Completion Evidence

### Summary

Implemented the Element System as a pure Domain module under
`src/backend/GameServer.Domain/Elements/`: the five MVP Elements, the Tương Khắc
overcoming cycle, matchup resolution into Advantage / Neutral / Disadvantage, and
the element damage modifier lookup with centrally configurable factors.

Bounded exactly as the task requires. Elementless is represented as an absent
(`null`) `Element` input on either side of a matchup; no `None` member was added
to the enum. No Combat integration, no Element assignment to Pet/Boss/Skill/Effect,
no serialization contract, no wire type. The module references no other domain
entity and no Application/Infrastructure/Api/framework type.

The documented behavior was fully derivable from `ELEMENT_RULES.md`, which is the
owning document; no contradiction was found between it and `GAME_RULES.md` §8
(both state `Mộc → Thổ → Thủy → Hỏa → Kim → Mộc`), so no stop condition fired and
no new documentation or ADR was created.

---

### Changes

**New — Domain module (`src/backend/GameServer.Domain/Elements/`)**

```text
Element.cs             new  the five MVP Elements (ELEMENT_RULES.md §1)
ElementMatchup.cs      new  the matchup outcome: Advantage / Neutral / Disadvantage (§2.1)
ElementMatchups.cs     new  Tương Khắc resolution — Resolve / Counters (§2, §2.1, §3)
ElementModifiers.cs    new  the three configurable damage factors + lookup (§2.2)
```

| Type | What it is | Source |
| --- | --- | --- |
| `Element` enum | Exactly `Moc`, `Tho`, `Thuy`, `Hoa`, `Kim`. **No `None` member.** | §1, §1.2 |
| `ElementMatchup` enum | Exactly `Advantage`, `Neutral`, `Disadvantage`. | §2.1 |
| `ElementMatchups.Resolve(Element?, Element?)` | The matchup of an attacking Element against a defending Element. | §2.1, §3 |
| `ElementMatchups.Counters(Element, Element)` | The cycle's five relationships, in one place. | §2 |
| `ElementModifiers` record | `Advantage` / `Neutral` / `Disadvantage` factors + `Default` + `For(matchup)`. | §2.2 |

**Design decisions inside documented intent**

1. **Member order follows the documented cycle.** Enum values are ordered
   `Moc, Tho, Thuy, Hoa, Kim` so each member's successor is the Element it counters
   (§2, `GAME_RULES.md` §8). The numeric values carry no documented meaning and no
   rule depends on them.

2. **Elementless is `null`, not a sixth member.** §3 defines elementless as damage
   sources having "no Element" and §1's set is closed at five, so `Resolve` takes
   `Element?` on both sides and returns Neutral when either is absent. This is the
   only representation that keeps elementless outside §1's set. Asserted by
   `Element_ShouldNotDeclareANoneOrElementlessMember`.

3. **Same-element Neutral needs no branch.** §2 lists no self-relationship, so an
   Element never counters itself and the `A == D` case falls out of the two
   directional checks — which is also why elementless falls out of the same two
   checks. The cycle appears once, in `Counters`; `Resolve` is not a materialized
   25-cell table.

4. **Modifiers are a supplied value, not `const` fields and not global state.**
   §2.2 states the factors "are configuration values ... not hardcoded constants",
   so they are an `ElementModifiers` record with `Default => new(1.50, 1.00, 0.75)`
   and a `For(matchup)` lookup. There is deliberately no static mutable "current"
   instance — a caller passes the set it means to use, so one resolution's balance
   edit cannot affect another's. A `const` was rejected because it compiles into
   every call site and can only be changed by editing consuming code, which is
   exactly the hardcoded constant §2.2 rules out. The Tương Khắc *cycle* stays
   hardcoded: §2.2 keeps a change to the relationship structure inside
   `GAME_RULES.md` §20's Rule Change Policy, so it is a rule and not a balance
   value. Asserted by `For_ShouldReturnSuppliedFactors_BecauseTheyAreConfiguration`
   and `ElementModifiers_ShouldExposeNoStaticMutableMember`.

5. **Undocumented values throw rather than resolve.** A value outside §1's five
   Elements has no Tương Khắc relationship and is not silently Neutral; a value
   outside §2.1's three outcomes has no §2.2 factor and is not silently priced.

**Not implemented (explicitly out of scope, per the task)**

```text
Combat damage pipeline integration          — COMBAT_RULES.md §3 owns it
Pet / Boss / Skill / Effect Element fields   — separate tasks (§1.1)
Element assignments (content data)           — §6; BOSS_RULES.md owns Bosses
Tương Sinh, dual-element, resistances,       — §7 / MVP_SCOPE.md §2 (OUT)
  environment/terrain modifiers
Element serialization / wire contract        — none is defined by any document
New documentation or ADR                     — none required (see Documentation Changed)
```

**No existing file was modified.** The change is purely additive: `git status`
shows only the new `Elements/` directory and the two new test files, and
`git diff --stat` against tracked files is empty. Nothing in `Match3/`, `Battle/`,
`Application/`, `Api/`, or the client was touched (`AGENTS.md` §16).

---

### Tests

**New — `tests/backend/GameServer.Domain.Tests/`**

```text
ElementMatchupTests.cs     new  32 tests — the matchup matrix and the enum
ElementModifierTests.cs    new  13 tests — the configurable factors
                                ─────
                                 45 tests
```

Coverage against the task's required cases:

| Required case | Test |
| --- | --- |
| All 25 Element × Element combinations | `Resolve_ShouldMatchTheTươngKhắcMatrixForEveryElementPair` — every cell asserted against an independently written expectation table, so a wrong cell fails naming the exact pair. Plus `Resolve_ShouldProduceEachOutcomeTheDocumentedNumberOfTimes` (5 Advantage / 5 Disadvantage / 15 Neutral) |
| Elementless attacker vs. each Element | `Resolve_ShouldBeNeutral_WhenAttackerIsElementless` — all five, including the 1.00× factor |
| Element attacker vs. elementless defender | `Resolve_ShouldBeNeutral_WhenDefenderIsElementless` — all five |
| Elementless vs. elementless | `Resolve_ShouldBeNeutral_WhenBothSidesAreElementless` |
| Same-element Neutral | `Resolve_ShouldBeNeutral_WhenElementsAreEqual` (5) + `..._ForEverySameElementPair`, asserting Neutral **and not** Advantage |
| Modifier values | `Default_ShouldCarryTheDocumentedFactors`, `For_ShouldReturnTheDocumentedFactorForEachMatchup` (1.50 / 1.00 / 0.75) |
| Configurability (§2.2) | `For_ShouldReturnSuppliedFactors_BecauseTheyAreConfiguration`, `Modifiers_ShouldBeSuppliedPerCall_NotHeldAsMutableSharedState`, `ElementModifiers_ShouldExposeNoStaticMutableMember` |
| Cycle integrity | `Counters_ShouldDescribeAClosedFiveElementCycle`, `..._ExactlyOneTargetPerElement`, `..._NeverBeReflexive`, `Resolve_ShouldBeTheInverseOfItselfForCounteringPairs` |
| Enum set is closed; no `None` | `Element_ShouldDeclareExactlyTheFiveDocumentedMembers`, `Element_ShouldNotDeclareAMemberOutsideTheDocumentedSet`, `Element_ShouldNotDeclareANoneOrElementlessMember` |
| Domain purity (no Pet/Boss/Skill/Effect, no foreign layer) | `ElementsModule_ShouldReferenceNoOtherLayerOrDomainEntity` |

Every expected value is read from `ELEMENT_RULES.md` §2/§2.1/§2.2/§3, not from the
implementation (`quality/testing.md` §2). The matrix table in the test is written
from the five documented relationships; the test does not call the code to build
its expectations.

**Mutation check (test validity).** The tests were confirmed to be sensitive to
rule errors, not merely green. Three deliberate mutations were applied, each
caught, then reverted and re-verified:

```text
Moc counters Kim instead of Tho        → 8 failures (matrix, both Theory cases, cycle)
elementless returns Disadvantage       → 5 failures (all elementless cases)
factors 1.60 / 1.00 / 0.70             → 6 failures (all modifier-value cases)
```

---

### Documentation Consulted

```text
docs/01-game-design/ELEMENT_RULES.md   §1, §1.1, §1.2, §2, §2.1, §2.2, §3, §4,
                                       §5, §6, §7  — the primary contract
docs/01-game-design/GAME_RULES.md      §8 (Element overview, cycle, "Element does
                                       not determine ... compatibility"), §9
                                       (Pet ↔ Element), §20 (Rule Change Policy)
docs/01-game-design/COMBAT_RULES.md    §3 step 3 (where the Element Modifier sits),
                                       §3.1, §3.3, §5.1 (Burn = Hỏa; an elementless
                                       Shield) — reference only, not implemented
docs/00-overview/MVP_SCOPE.md          §1 Elements IN, §2 OUT (Tương Sinh,
                                       Terrain, Weather), §3 FUTURE
docs/02-technical/ARCHITECTURE.md      §1 (Elements/ is its own domain module),
                                       §2.1 (Domain has zero framework dependencies)
docs/02-technical/GAME_STATE.md        §2 — no Element state on BoardState or
                                       PlayerState (confirmed; nothing added)
docs/02-technical/GAME_EVENTS.md       §2 — confirmed no Element event exists to emit
docs/02-technical/SIGNALR_PROTOCOL.md  confirmed no Element wire contract is defined
docs/01-game-design/MATCH3_RULES.md    §1.1 — a Gem type is not an Element
AGENTS.md                              §2, §4, §6, §7, §8, §9, §12, §14–§17, §20–§22
.ai/workflow/development/feature.md    the assigned workflow
.ai/workflow/core/validation.md        §2 — LOW depth selection
.ai/workflow/quality/testing.md        §2, §3 — expected-value and scenario rules
.ai/workflow/quality/review.md         §1 — the review checklist
.ai/workflow/core/completion.md        §1, §2 — Definition of Done and report shape
.ai/agents/gameplay.md                 scope, decision authority, stop conditions
.ai/skills/gameplay/gameplay-behavior-derivation.md
.ai/skills/testing/test-scenario-generation.md
tasks/TASK_LIFECYCLE.md                state transitions
```

**Conflicts checked.** `ELEMENT_RULES.md` §2 and `GAME_RULES.md` §8 state the same
cycle (`Mộc → Thổ → Thủy → Hỏa → Kim → Mộc`) and the same scope decisions; no
conflict exists to report (`AGENTS.md` §4). §2.2's "configuration, not hardcoded
constants" and TASK-008's Implementation Note 3 agree with each other. No
contradiction or missing-contract condition fired (`AGENTS.md` §20).

---

### Documentation Changed

**None.** Documentation Impact Option A applies: this task implements
already-documented behavior and `ELEMENT_RULES.md` defines the full contract.

One point was checked explicitly rather than assumed — `ElementModifiers` chooses
a supplied-value representation for the §2.2 factors, which is an implementation
detail of the module's own API. `ELEMENT_RULES.md` §2.2 fixes the three values and
requires them to be configuration; it does not specify how configuration is
supplied, and no protocol or state document binds the Element path, so there is no
missing gameplay contract and nothing to document. No ADR was created: the module
sits inside the `Elements/` boundary `ARCHITECTURE.md` §1 already defines and
introduces no architectural decision (`AGENTS.md` §18).

---

### Validation

Validation depth for Risk LOW per `core/validation.md` §2 — build/compile
validation + focused unit tests + review:

```text
[PASS] Build — GameServer.Domain
       dotnet build src/backend/GameServer.Domain → Build succeeded, 0 Warning(s), 0 Error(s)

[PASS] Build — full backend solution
       dotnet build src/backend/GameServer.sln → Build succeeded, 0 Warning(s), 0 Error(s)

[PASS] Unit tests — Domain suite (LOW-risk focused tests)
       dotnet test tests/backend/GameServer.Domain.Tests
       Passed! Failed: 0, Passed: 579, Skipped: 0, Total: 579

[PASS] Unit tests — new Element tests alone
       dotnet test --filter FullyQualifiedName~Element
       Passed! Failed: 0, Passed: 45, Skipped: 0, Total: 45

[PASS] No regression — the 534 pre-existing Domain tests still pass; the change is
       purely additive and no tracked file was modified (git diff --stat empty)

[PASS] Documentation validation — every implemented rule traces to a specific
       ELEMENT_RULES.md section; no value originates in code rather than docs
```

**`quality/review.md` §1 checklist**

```text
[PASS] Correctness
       Cycle, outcomes, elementless rules, and modifier values match
       ELEMENT_RULES.md §2/§2.1/§2.2/§3 exactly. The 5×5 matrix is asserted cell by
       cell against an independently written expectation table, and the mutation
       check shows the tests fail when a rule is broken.

[PASS] Architecture
       ARCHITECTURE.md §1 places Elements/ as its own Domain module — done.
       §2.1 item 1 requires Domain to have zero framework dependencies: the module
       references only System and its own namespace (verified by reflection test and
       by a source scan). No Pet/Boss/Skill/Effect reference (task stop condition).

[PASS] Scope
       Only the four Domain files and two test files were added. No Combat
       integration, no Element ownership, no serialization, no wire type, no Tương
       Sinh, no build constraints (§4 — an Element never gates a Card/Relic/Passive).
       git status shows no modified tracked file.

[PASS] Tests
       LOW depth met and exceeded: all 25 combinations, both elementless directions,
       elementless-vs-elementless, same-element Neutral, the three modifier values,
       configurability, cycle integrity, the closed enum set, and Domain purity.
       Integration/gameplay-scenario types not required — no integration boundary
       exists yet and no pipeline consumes Element (TASK-008 Testing Requirements).

[PASS] Documentation
       Checked; none required. See Documentation Changed.

[PASS] Security
       No authentication, authorization, persistence, or data-exposure surface. The
       module is a pure function of its two arguments and reads no external input.

[PASS] Performance
       O(1) resolution and lookup with no allocation on the hot path; no I/O, no
       query, no RNG. Nothing added to any resolution path.

[PASS] Maintainability
       No unnecessary abstraction: no interface, factory, service, DI registration,
       event bus, or cache. AGENTS.md §9 satisfied. The cycle is stated once, and
       the factors live in one record rather than as duplicated literals.

[PASS] Determinism (gameplay logic)
       Server-authoritative: a pure, deterministic function with no RNG, no time,
       and no external state, so a recovered session resolves identically. No client
       input is consulted. No client-authoritative value is introduced (AGENTS.md
       §10). Nothing computes or mutates damage here — COMBAT_RULES.md §3 does.
```

**Task-specific stop conditions — none fired**

```text
Behavior derivable from the Authoritative Sources?   YES — no stop (AGENTS.md §7)
Required referencing Pet/Boss/any entity type?        NO  — no stop
Two authoritative documents in conflict?              NO  — no stop (AGENTS.md §4)
MVP scope violation?                                  NO  — §1 lists Elements as IN
```

---

### Risks

1. **Numeric enum values are unpinned by documentation (low).** No document defines
   a numeric or wire representation for Element, so `Moc = 0 … Kim = 4` is a local
   choice. No rule depends on it and no serialization exists yet. If a wire contract
   is later defined, it should state the values explicitly rather than inherit them.

2. **The cycle is intentionally hardcoded (low, by design).** §2.2 puts any change
   to the relationship structure behind `GAME_RULES.md` §20, so `Counters` hardcodes
   the five edges instead of loading them from configuration, while the three
   factors are configurable. A future authorized rule change edits `Counters`
   deliberately — which is the intent, not an oversight.

3. **`ElementModifiers` is supplied per call (low).** Configurability currently
   means a caller constructs the set it wants; there is no configuration-file or DI
   binding, because no document requests one and `AGENTS.md` §9 forbids speculative
   infrastructure. When Combat is implemented, its owning task decides whether a
   composition-root binding is warranted.

4. **Elementless — defender is forward-looking (informational).** §3 item 2 covers
   an elementless *target* parenthetically ("if any such target exists"). In MVP
   every Pet and Boss has exactly one Element (§1.2), so this path is implemented
   and tested as §3 specifies but has no live consumer yet.

---

### Remaining Issues

None blocking. Follow-up work, explicitly outside this task's scope:

```text
Combat integration            COMBAT_RULES.md §3 step 3 consuming ElementModifiers
                              — a later task; COMBAT_RULES.md §3.2 and §3.3 still
                              have unimplemented placeholder formulas of their own
Pet/Boss/Skill/Effect Element ownership — their own tasks (§1.1); §6's Pet
                              assignments and BOSS_RULES.md's Boss assignments are
                              content data, deliberately not added here
Element wire contract         none defined; when SIGNALR_PROTOCOL.md or
                              API_CONTRACTS.md declares one, the spelling and
                              numeric values must be stated there, not inferred
```

---

### Agent

`gameplay` (Primary), with `backend` and `testing` as supporting roles per the task
metadata. Work was executed in a single agent session; no sub-agents were used.

---

### Workflow Used

`development/feature.md`, executed as:

```text
MVP scope check            MVP_SCOPE.md §1 — Elements listed IN (no stop)
core/task-intake.md        FEATURE, Risk LOW (confirmed against the task metadata)
core/context-discovery.md  authoritative docs read; conflict check clean
core/planning.md           implementation boundary fixed (Domain Elements module only)
core/implementation.md     four files added under Elements/
quality/testing.md         45 unit tests written from documented expectations
quality/review.md          §1 checklist run — all items pass
core/validation.md         §2 LOW depth confirmed
core/completion.md         §1 Definition of Done satisfied; report below
```

---

### Skills Used

```text
gameplay-behavior-derivation    derived the expected behavior for every matchup and
                                the elementless cases from ELEMENT_RULES.md, with the
                                ownership question answered per §5 (the modifier
                                applies to damage instances; ownership of the
                                Entities is not this module's concern)
test-scenario-generation        derived the test set from the documents rather than
                                from the implementation, including the boundary and
                                edge cases §2.1 and §3 call out (same-element,
                                elementless both directions), and the enum-set checks
```

---

### Status

**DONE** — all acceptance criteria met, LOW-risk validation passed, review checklist
passed, documentation impact checked (none required), no unresolved conflict, no
scope expansion.

---

## Handoff

```text
Gameplay → Testing:
  Expected behavior for the Element System is implemented and covered by 45 passing
  unit tests in tests/backend/GameServer.Domain.Tests/ElementMatchupTests.cs and
  ElementModifierTests.cs. Expected values come from ELEMENT_RULES.md §2/§2.1/§2.2/§3.
  Edge cases covered: same-element Neutral (§2.1), elementless attacker (§3 item 1),
  elementless defender (§3 item 2), elementless vs elementless, the closed enum set
  (no None member), and Domain purity. No integration or gameplay-scenario tests are
  warranted yet — no pipeline consumes Element.

Gameplay → Backend (for the later Combat task):
  Available Domain API, all pure and side-effect free:

      GameServer.Domain.Elements.Element                 the five-value enum (nullable)
      GameServer.Domain.Elements.ElementMatchup          Advantage / Neutral / Disadvantage
      GameServer.Domain.Elements.ElementMatchups.Resolve(Element? attacker, Element? defender)
                                                         → ElementMatchup
      GameServer.Domain.Elements.ElementMatchups.Counters(Element attacker, Element defender)
                                                         → bool
      GameServer.Domain.Elements.ElementModifiers        configurable factors
          ElementModifiers.Default                        → 1.50 / 1.00 / 0.75 (§2.2)
          ElementModifiers.For(ElementMatchup)            → double

  Consumption contract for COMBAT_RULES.md §3 step 3:
    - pass a damage source's Element as `attacker` and the target's as `defender`;
    - pass null for an elementless source or target (§3) — there is no None member;
    - Resolve() yields the matchup and For() yields the factor, which is multiplied
      in at step 3 and nowhere else;
    - supply the ElementModifiers instance explicitly rather than relying on a
      global, so a balance change stays contained;
    - this module assigns no Element to any entity and stores no Element state —
      Pet/Boss/Skill/Effect ownership is a separate task.

Gameplay → Review:
  What was implemented: the Element Domain module only (four files), plus 45 tests.
  Which docs were consulted: listed under Documentation Consulted. Documentation
  impact: none required. Validation: build clean (0 warnings), 579/579 Domain tests
  pass, review §1 checklist passes. No doc gaps found.
```

