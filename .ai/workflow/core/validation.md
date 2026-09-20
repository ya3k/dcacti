# core/validation.md — Validation Depth Selection

**Version:** 1.0

> Purpose: decide how much validation a task needs, based on the risk
> level set during `core/task-intake.md`. This file selects depth; the
> actual test execution is `quality/testing.md` and the actual review is
> `quality/review.md`.

---

# 1. Validation Layers

```text
Documentation validation     (does the change match docs/, per
                              quality/review.md "Correctness")
Architecture validation        (does it follow ARCHITECTURE.md /
                                relevant ADRs)
Build/compile validation
Unit tests
Integration tests
Gameplay scenarios              (Given/When/Then, quality/testing.md)
Final review                     (quality/review.md)
```

Not every task requires every layer.

---

# 2. Depth by Risk

```text
LOW      Build/compile validation + focused unit test(s) + review.
          Typically: typo, isolated refactor, simple UI adjustment.

MEDIUM    + Integration tests where the change crosses a boundary
           (API/DB/SignalR) + documentation validation.
           Typically: API change, DB query change, gameplay
           implementation of an already-documented mechanic, SignalR
           change.

HIGH       + Gameplay scenarios (state-transition-level, not just unit-
            level) + architecture validation + full review across every
            checklist item in quality/review.md.
            Typically: game rule change, combat formula, battle state
            model, architecture change, DB migration, auth/security
            change, persistence strategy change.
```

---

# 3. Choosing Depth Is Not Optional Skipping

A task classified MEDIUM or HIGH in `core/task-intake.md` §3 must run the
corresponding layers — depth is not something implementation decides for
itself after the fact to save time. If a HIGH-risk task's gameplay
scenario validation is skipped, the task is not done (`core/completion.md`
§1), regardless of whether the code looks correct.

---

# 4. Output of This Step

A validation plan (which layers, at what depth) that `quality/testing.md`
and `quality/review.md` execute against.
