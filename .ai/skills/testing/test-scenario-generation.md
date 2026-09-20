# skills/testing/test-scenario-generation.md — Skill: Test Scenario Generation

**Version:** 1.0

> Derive test scenarios — including regression scenarios — whose expected
> results come from the documentation, not from the implementation, covering
> state transitions, event sequences, boundaries, and documented edge cases.

## Purpose

Produce a set of scenarios (Given/When/Then for gameplay; request/response,
event-delivery, or persistence scenarios for technical areas) that a test
author can turn into automated tests, so that the chain
`Rule → Scenario → Automated test` (`AGENTS.md` §15) is real. It also
performs the **regression** derivation for bug fixes.

## When to Use

- `quality/testing.md` (any test type selected by `core/validation.md`).
- `development/bug-fix.md`: the regression scenario that would fail before the
  fix and pass after (`quality/testing.md` §4).
- `development/gameplay-change.md` and HIGH-risk gameplay work, where
  state-transition-level scenarios are required (`core/validation.md` §2).
- `development/refactor.md`: scenarios that pin existing behavior.

## When Not to Use

- To *write or run* the tests (that is implementation/testing workflow work).
- To decide validation depth (`core/validation.md`).
- To review whether existing tests are adequate — use `implementation-review`
  (which may call this skill to find gaps).

## Inputs

```text
The behavior under test (mechanic, endpoint, event flow, storage behavior)
Expected-Behavior Record (gameplay-behavior-derivation) for gameplay behavior
Contract reports/context for API / realtime / persistence behavior
Validation depth selected by core/validation.md
For bug fixes: the bug report and the identified expected behavior
```

## Prerequisites & Required Context

- The owning documents for each expected value/behavior are identified
  (Documentation Context).
- For gameplay: the fixed order in `GAME_RULES.md` §17 and the event order in
  `GAME_EVENTS.md` §1.

## Authoritative Sources

```text
AGENTS.md §15                      testing requirement, Given/When/Then style, edge cases
.ai/README.md §17                  Input → State transition → Events → Final state
quality/testing.md §2–§4           expected-value rule, scenario shape, regression rule
GAME_RULES.md §2, §11, §17         actions and resolution order (scenario starting points)
Domain rule docs                   expected results and their documented edge cases
GAME_STATE.md, GAME_EVENTS.md      state shapes and events to assert
API_CONTRACTS.md, SIGNALR_PROTOCOL.md, DATABASE.md, REDIS_STATE.md
                                   expected behavior for API/realtime/persistence tests
```

## Procedure

1. **Collect expected behavior from documents.** Use the Expected-Behavior
   Record or the relevant contract report. If there is none, derive it via
   the appropriate skill first — never from the implementation under test.
2. **Choose scenario shapes by test type** (`quality/testing.md` §1), matched to
   the depth selected by `core/validation.md`:
   unit (isolated rule) · integration (crosses a boundary) · gameplay
   scenario (state-transition level) · API · realtime · persistence ·
   end-to-end.
3. **Write each gameplay scenario as** `Given` (initial state in
   `GAME_STATE.md` shapes) · `When` (a documented action:
   Swap / Card Cast / Pet Skill Cast) · `Then` (resulting state **and** events
   **and** order). Expected numbers appear as
   `<value from COMBAT_RULES.md §x>` in the scenario; the test author resolves
   them by reading that document at authoring time, never from the code.
4. **Cover the chain, not just the endpoint.** For each step of
   `GAME_RULES.md` §17 the change touches, include an assertion on the
   transition and its events — a final-number-only check is insufficient for
   HIGH-risk gameplay work (`quality/testing.md` §3).
5. **Derive boundary and edge cases from documents:** limits and thresholds
   stated by the owning rule, explicit edge-case notes in domain documents
   (special-match handling, anti-infinite-chain rule, multiple matches in one
   cascade, and similar), invalid-input rejection paths (rejection result,
   no events emitted), documented lifecycle boundaries (state expiry,
   reconnect). Do not invent edge cases; list suspected undocumented ones as
   *gaps*.
6. **State transitions and event sequences.** For each scenario record the
   before-state, after-state, and ordered events by reference (state
   documents, `GAME_EVENTS.md`).
7. **Determinism scenarios.** Where RNG or ordering matters, include a
   scenario that fixes the seed/inputs and asserts reproducible results, and
   one that shows no client-originated value alters the outcome
   (`authority-determinism-audit` supplies the risk points).
8. **Regression mode (bug fix).** Write the scenario from the documented
   expected behavior, then state explicitly: *why it fails against the buggy
   behavior* and *why it passes after the fix*. If it would already pass, it is
   not a regression test. Include an adjacent-behavior scenario for anything
   the impact analysis marked at risk.
9. **Refactor mode.** Scenarios pin *existing documented* behavior; they must
   not assert new behavior, and existing tests keep their intent.
10. **Test-validity check.** For every scenario, name the document that
    justifies the expected result. A scenario whose only justification is
    "the code returns this" is rejected. If an existing test disagrees with a
    document, classify it (`documentation-consistency`): implementation bug,
    test bug, documentation bug, or design ambiguity — do not edit the test to
    match the code.

## Outputs

```text
Test Scenario Set
For each scenario:
- Id / title / test type
- Given / When / Then (state + events + order)
- Source of every expectation (document + section)
- Boundary / edge-case tag (documented) or "gap" flag
- Regression pair (before-fail / after-pass) for bug fixes
Plus:
- Coverage map: rule/step → scenarios
- Documented-but-untestable or ambiguous items (gaps)
- Regression risks handed over from impact-analysis
```

## Validation

```text
[ ] Every expected result cites a document; none is derived from the implementation
[ ] No value/formula is copied into the scenario as an authoritative number
[ ] Gameplay scenarios assert state, events, and order — not only the end value
[ ] Every touched step of GAME_RULES.md §17 has at least one scenario
[ ] Documented edge cases in the owning documents are covered or listed as gaps
[ ] Regression scenarios demonstrably fail before / pass after
```

## Stop Conditions

- The expected behavior cannot be derived from documents (missing or
  ambiguous rule) — do not write a scenario around what the code does.
- Documents conflict about the expected result (`AGENTS.md` §4).
- A test would need to assert behavior contradicting the source of truth to
  pass (`development/bug-fix.md` §4 hard rule).
- The scenario would require an out-of-scope feature to be exercised.

Baseline stop conditions in `.ai/skills/README.md` §6 also apply.

## Common Failure Modes

- Deriving expectations by running the code and recording the output.
- Asserting only final HP/damage.
- Skipping the rejection path (invalid swap, insufficient resources).
- Inventing edge cases the docs never mention, then "fixing" the docs.
- Regression tests that would have passed before the fix.
- Altering a test to make an incorrect implementation pass.

## Traceability

```text
Used by:    quality/testing.md; quality/review.md (test-gap detection);
            development/bug-fix.md (regression), gameplay-change.md,
            feature.md, refactor.md
Reads:      AGENTS.md §15; .ai/README.md §17; quality/testing.md;
            GAME_RULES.md; domain rule docs; GAME_STATE.md; GAME_EVENTS.md;
            contract docs
Produces:   Test Scenario Set
Depends on: gameplay-behavior-derivation (gameplay); contract skills'
            reports (API/realtime/persistence); optionally impact-analysis
            (regression risks)
```
