# core/planning.md — Planning

**Version:** 1.0

> Purpose: turn a conflict-free understanding of the task
> (`core/context-discovery.md` output) into a concrete implementation plan,
> sized to the task (`core/task-intake.md` output).

---

# 1. What a Plan Considers

```text
Goal
Scope
Relevant documentation (already identified by context-discovery.md)
Files likely affected
Architecture impact
Data impact
API impact
Realtime/event impact
Testing strategy
Documentation impact
Risks
```

Not every task requires a written answer for every item — a LOW-risk task
may only need Goal/Scope/Files/Testing strategy stated briefly. A
HIGH-risk task should have an explicit line for each item above, even if
the answer is "none."

---

# 2. Sizing the Plan

```text
Small    Goal, Scope, Files affected, Testing approach — a few lines each
Medium    + Impact analysis (§3 below) written out explicitly
Large      + the extended steps in .ai/workflow/README.md §4 (research,
             design review, architecture review, ADR, incremental
             implementation plan)
```

Size comes from `core/task-intake.md`'s risk level, not from how the task
feels subjectively. A MEDIUM-risk task always gets the impact analysis in
§3 even if it "seems simple."

---

# 3. Impact Analysis (Medium and Large Tasks)

Trace the actual dependency chain for the kind of change being made —
mirrors `AGENTS.md` §10 / `.ai/README.md` §12:

```text
Game rule change
  → owning domain rule doc
  → dependent technical docs (state/events/contracts referencing it)
  → implementation
  → tests
  → possibly a new/updated ADR

API contract change
  → API_CONTRACTS.md
  → backend implementation
  → frontend implementation
  → tests

Architecture change
  → TDD.md
  → ARCHITECTURE.md
  → docs/03-decisions/ADR/ (new or superseding ADR — see
    architecture/adr-change.md)
  → implementation
```

A plan that only addresses the last link in the relevant chain (code) is
incomplete, even for a plan that otherwise looks thorough.

---

# 4. What Planning Does Not Do

Planning does not re-derive documentation content (that already happened
in `core/context-discovery.md`) and does not itself resolve conflicts
(those already halted the workflow before reaching this step). Planning
only sequences the work required, given a conflict-free understanding.

---

# 5. Output of This Step

A written plan, sized per §2, that `core/implementation.md` follows without
re-deciding scope.
