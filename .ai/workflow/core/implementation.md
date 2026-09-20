# core/implementation.md — Implementation

**Version:** 1.0

> Purpose: carry out the plan (`core/planning.md` output) with the
> smallest correct change, without expanding scope.

---

# 1. Sequence

```text
Understand   (already done — context-discovery.md, planning.md)
 ↓
Plan          (already done — planning.md output)
 ↓
Implement
 ↓
Run focused validation      (see quality/testing.md and core/validation.md
                             for depth)
 ↓
Review                        (quality/review.md)
```

Implementation starts only after a plan exists (§1 of `core/planning.md`),
even for a Small task's brief version of one.

---

# 2. Boundaries

AI must NOT, while implementing:

```text
refactor unrelated code
introduce unrelated architecture changes
add unrequested features
change game balance without an explicit design requirement
introduce unnecessary abstractions
"improve" unrelated code while implementing the task
```

Prefer the smallest correct change over the most elegant one. If an
unrelated problem is noticed during implementation, do not fix it inline —
report it per `AGENTS.md` §16 (issue, location, impact, suggested
follow-up task) and continue with the original scope only.

---

# 3. When Implementation Reveals a Gap

If, mid-implementation, it turns out the plan was based on an incomplete
reading of the docs (a rule wasn't actually covered, a contract doesn't
say what was assumed), do not improvise a resolution. Return to
`core/context-discovery.md` §3 (Conflict Check) and treat this exactly like
a conflict found during discovery — STOP, report, wait for resolution.
Implementation does not have its own, looser standard for handling gaps
than discovery does.

---

# 4. Server Authority Check (Gameplay/Backend Implementation)

For any implementation touching gameplay state, explicitly confirm, per
`.ai/README.md` §15:

```text
Who owns this state?
Who calculates it?
Who validates it?
Who broadcasts it?
```

If any answer is "the client" for Damage, HP, Boss HP, Power, Match
result, Combo result, Passive progress, Rewards, or Battle outcome — this
is a defect, not an implementation choice. Stop and report it rather than
finishing it.

---

# 5. Output of This Step

Implemented change, ready for `quality/testing.md` and `quality/review.md`.
