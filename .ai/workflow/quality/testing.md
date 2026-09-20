# quality/testing.md — Testing Workflow

**Version:** 1.0

> Use whenever `core/implementation.md`, `development/*.md`, or
> `architecture/architecture-change.md` reaches its testing step.

---

# 1. Test Types

```text
Unit tests            isolated logic (e.g. a single Domain function)
Integration tests        crosses a boundary (e.g. Application layer calling
                          a Redis repository)
Gameplay scenarios          state-transition-level, Given/When/Then (§3)
API tests                      request/response against API_CONTRACTS.md
Realtime tests                    Hub method / event delivery against
                                  SIGNALR_PROTOCOL.md
Persistence tests                    schema/constraint behavior against
                                      DATABASE.md
End-to-end tests                        full flow across multiple layers
```

Which types apply is determined by `core/validation.md`'s risk-based
depth selection — this file does not re-decide depth, only how each
selected type is written.

---

# 2. Source of Truth for Expected Values

**Do not hard-code an expected value without consulting the source-of-truth
documentation.** A test's expected damage, Power gain, threshold, or
timing must be read from the owning domain rule doc
(`docs/01-game-design/...RULES.md`) or technical doc
(`docs/02-technical/...`), not invented or assumed from the implementation
itself — a test that just encodes "whatever the code currently does" is
not testing anything.

---

# 3. Gameplay Scenarios (Given/When/Then)

Gameplay behavior should preferably be expressed as scenarios:

```text
Given <initial state, matching docs/02-technical/GAME_STATE.md shapes>
When <an action, matching docs/01-game-design/GAME_RULES.md §2/§11 —
      a Swap, a Card Cast, etc.>
Then <the resulting state/events, matching the owning domain rule doc>
```

Example:

```text
Given:
  The player has a specific Power value.
When:
  A POWER Gem is matched.
Then:
  Power changes according to the authoritative combat rules
  (docs/02-technical/COMBAT_RULES.md §2 — the value itself comes from
  that document, not from this scenario).
```

For battle systems specifically, validate the full state-transition
chain where the task's scope touches it, matching the fixed order in
`docs/01-game-design/GAME_RULES.md` §17:

```text
SWAP → Match → Cascade → Combo → Passive → Resource → Damage
  → Boss response → Turn end
```

A test that only checks the final number (e.g. final HP) without verifying
the events/order that produced it is insufficient for a HIGH-risk gameplay
change (`core/validation.md` §2).

---

# 4. Regression Tests (Bug Fixes)

Every bug fix (`development/bug-fix.md`) must add or update a regression
test that would have failed before the fix and passes after — sourced from
the authoritative expected behavior identified during that workflow's
"Identify expected behavior" step, not from the buggy implementation being
replaced.

---

# 5. Output

Passing tests appropriate to the validation depth selected by
`core/validation.md`, feeding into `quality/review.md`.
