# .ai/workflow/README.md — Workflow Layer

**Version:** 1.0
**Status:** Binding
**Scope:** Every workflow file under `.ai/workflow/`.

> This document answers: **"How should an AI execute a task from start to
> completion?"** It does not answer "how does the game work" (that's
> `docs/01-game-design/`) or "how does the system work" (that's
> `docs/02-technical/`) or "why was this built this way" (that's
> `docs/03-decisions/ADR/`). Workflows orchestrate work around those
> sources of truth — they never become one themselves.

---

# 1. What Is a Workflow?

A workflow is an ordered sequence of steps an AI follows to take a task
from intake to completion. It defines **when** and **in what order** work
happens. It contains no game rules, no technical specifications, and no
architectural decisions — only process.

## Workflow vs. Agent vs. Skill

```text
Agent    = WHO performs the work        (role, not created in this task)
Skill    = HOW a capability is done      (reusable procedure, not created
                                          in this task)
Workflow = WHEN / IN WHAT ORDER work happens  (this layer)
Docs     = WHAT is authoritative
```

A workflow may reference which agent role or which skill is typically
involved at a given step (§7 of `.ai/README.md`'s integration model), but
it does not define agents or skills itself — those are separate, not-yet-
created layers.

## When Should Each Workflow Be Used?

```text
core/            The baseline lifecycle every other workflow builds on.
                  Not usually invoked directly by a task — referenced by
                  the other workflows below.

development/      Chosen based on task type: a new feature, a bug fix, a
                  refactor, or a gameplay-rule change.

architecture/       Chosen when the task changes architecture, technology
                    choice, or requires a new/updated ADR.

documentation/         Chosen when the task's primary output is a
                       documentation change (no code).

quality/                Not usually invoked directly — referenced by
                        `development/` and `architecture/` workflows for
                        their testing and review steps.
```

---

# 2. General Lifecycle

```text
TASK
 ↓
INTAKE                    (core/task-intake.md)
 ↓
CONTEXT DISCOVERY           (core/context-discovery.md)
 ↓
CONFLICT CHECK                (part of context-discovery.md)
 ↓
PLAN                            (core/planning.md)
 ↓
IMPLEMENT                         (core/implementation.md)
 ↓
TEST                                (quality/testing.md)
 ↓
REVIEW                                (quality/review.md)
 ↓
DOCUMENTATION CHECK                     (core/validation.md +
                                         documentation/documentation-change.md
                                         if docs actually need updating)
 ↓
COMPLETION                                (core/completion.md)
```

Not every task executes every phase at full depth. A LOW-risk task (see
`core/task-intake.md` §3) may compress Plan/Implement/Test/Review into a
single pass; a HIGH-risk task executes each phase explicitly and produces
artifacts for each (a written plan, a written conflict report if any, a
documented validation result).

---

# 3. How Workflows Compose

Workflows reference other workflows instead of duplicating their steps.
`development/feature.md`, for example, does not restate the review
checklist — it points to `quality/review.md`.

```text
Feature Workflow
    ↓ (reuses)
core/task-intake.md → core/context-discovery.md → core/planning.md
    ↓ (reuses)
core/implementation.md
    ↓ (reuses)
quality/testing.md
    ↓ (reuses)
quality/review.md
    ↓ (reuses)
core/completion.md
```

```text
Bug Fix Workflow
    ↓ (reuses)
core/context-discovery.md → core/planning.md (lightweight)
    ↓
development/bug-fix.md's own root-cause steps
    ↓ (reuses)
quality/testing.md → quality/review.md → core/completion.md
```

```text
Gameplay Change Workflow
    ↓ (reuses)
core/* for intake/discovery/planning
    ↓
development/gameplay-change.md's own design-change branch
    ↓ (reuses)
architecture/adr-change.md   (only if the change is architectural, not just
                              a rule value)
    ↓ (reuses)
quality/testing.md → quality/review.md → core/completion.md
```

A workflow file must never inline another workflow's checklist. If a step
is shared, it is referenced by path, not copied.

---

# 4. Task Size Scales Workflow Depth

```text
Small    Context → Plan → Implement → Test → Review
Medium    + Impact analysis (core/planning.md §3)
Large      + Research, design review, architecture review, ADR,
             detailed implementation plan, incremental implementation,
             extended validation
```

See `core/task-intake.md` §3 for how size and risk are determined, and
`core/validation.md` for how validation depth scales with them.

---

# 5. Directory Map

```text
.ai/workflow/
├── README.md                          this file
├── core/
│   ├── task-intake.md                  classify task type/scope/risk
│   ├── context-discovery.md            find + read the relevant docs,
│   │                                    detect conflicts
│   ├── planning.md                     produce a plan sized to the task
│   ├── implementation.md               how implementation itself proceeds
│   ├── validation.md                   how validation depth is chosen
│   └── completion.md                   definition of done + final report
├── development/
│   ├── feature.md
│   ├── bug-fix.md
│   ├── refactor.md
│   └── gameplay-change.md
├── architecture/
│   ├── architecture-change.md
│   └── adr-change.md
├── documentation/
│   └── documentation-change.md
└── quality/
    ├── testing.md
    └── review.md
```

---

# 6. Stop Conditions (Inherited)

Every workflow inherits the stop conditions defined in `AGENTS.md` §20 and
`.ai/README.md` §13, without redefining them. When any workflow step hits
one of those conditions, the workflow halts at that step and produces the
STOP CONDITION report format from `.ai/README.md` §13 — it does not
continue to the next phase.
