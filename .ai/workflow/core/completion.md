# core/completion.md — Completion

**Version:** 1.0

> Purpose: define when a task is actually done, and the report shape every
> workflow produces at the end.

---

# 1. Definition of Done

A task is complete only when **all** of the following hold:

```text
Implementation completed
AND relevant validation passed (per core/validation.md's selected depth)
AND no unresolved documentation conflict remains
   (core/context-discovery.md §3)
AND no unintended scope expansion occurred (core/implementation.md §2)
AND documentation impact was checked (core/planning.md §3, and
   documentation/documentation-change.md applied if docs actually needed
   updating)
AND architecture impact was checked (architecture/architecture-change.md
   or architecture/adr-change.md applied if the change was architectural)
```

This mirrors `AGENTS.md` §22 exactly — completion does not define a
separate, looser bar.

---

# 2. Final Report

```text
## Summary
## Changes
## Tests
## Documentation Consulted
## Documentation Changed
## Validation
## Risks
## Remaining Issues
```

Sections may be omitted only when genuinely irrelevant to the task (e.g. a
pure documentation task may have no "Tests" section) — not omitted for
brevity. This is the same shape as `.ai/README.md` §21's AI Output
Contract; completion does not invent a second report format.

---

# 3. If Completion Cannot Be Reached

If any Definition of Done item in §1 cannot be satisfied — most commonly
because a stop condition (`.ai/README.md` §13) fired during an earlier
phase and was never resolved — the task is reported as **blocked**, not
completed:

```text
## Status: Blocked

## Blocking Condition
<the STOP CONDITION report from the phase that halted>

## Completed So Far
<what was actually done before the block>

## What Is Needed to Unblock
<the decision/information required>
```

A blocked task must never be silently closed out as done.
