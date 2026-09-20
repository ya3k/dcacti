# development/bug-fix.md — Bug Fix Workflow

**Version:** 1.0

> Use for: behavior that doesn't match documented intent, or a defect
> reported against existing implementation.

---

# 1. Bug Category (Determine First)

```text
Implementation bug      code deviates from documented behavior
Documentation bug         docs are stale/wrong; code reflects the actually
                          intended behavior
Test bug                    the test itself asserts the wrong thing
Data bug                      bad/corrupt data, not a logic defect
Architecture bug                 a structural issue, not a local logic
                                 issue — hand off to
                                 architecture/architecture-change.md if so
Configuration/environment bug       not a code or docs issue at all
```

This mirrors the classification principle in `AGENTS.md` §17 /
`.ai/README.md` §18 — do not skip straight to "fix the code" without first
deciding which category applies.

---

# 2. Flow

```text
Bug report
 ↓
Reproduce
 ↓
Identify expected behavior       (from the authoritative doc — see §3)
 ↓
core/context-discovery.md             (read the authoritative doc(s);
                                       check for conflicts before trusting
                                       either side)
 ↓
Compare expected vs actual behavior
 ↓
Identify root cause + category (§1)
 ↓
Apply minimal fix
 ↓
Add/update a regression test         (quality/testing.md)
 ↓
quality/review.md
 ↓
core/completion.md
```

---

# 3. Default Assumption When Code and Docs Disagree

Per `.ai/README.md` §18: **docs describe intended behavior; code is the
(possibly incorrect) implementation** — this is the default, not an
automatic conclusion. If evidence suggests the documentation itself is
stale, that must be explicitly reported and reasoned, not silently
assumed. Never change docs and code simultaneously to make them agree
without first determining which one was actually correct.

---

# 4. Hard Rule

**Never change behavior merely to make a test pass if that behavior
contradicts the source of truth.** If a test conflicts with a documented
rule, the test is wrong (Test bug, §1) — fix the test, not the rule, unless
the task explicitly authorizes a design change (in which case, hand off to
`development/gameplay-change.md`).

---

# 5. Composition

Uses `core/context-discovery.md` for reading/conflict-checking,
`quality/testing.md` for the regression test, `quality/review.md` for
review, and `core/completion.md` for the final report. Does not redefine
any of these.
