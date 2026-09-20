# tasks/TASK_TYPES.md — Task Types

**Version:** 1.0

> This document answers: **"What type should a task be, and what does
> that type imply about workflow, agent, skills, and risk?"**

---

# 1. Type Overview

There are exactly six task types — one per existing workflow under
`.ai/workflow/development/` and `.ai/workflow/architecture/` and
`.ai/workflow/documentation/`. No type is created for a workflow that
does not exist.

```text
FEATURE            development/feature.md
BUG                development/bug-fix.md
GAMEPLAY-CHANGE    development/gameplay-change.md
REFACTOR           development/refactor.md
ARCHITECTURE       architecture/architecture-change.md
                   (+ architecture/adr-change.md when needed)
DOCUMENTATION      documentation/documentation-change.md
```

---

# 2. Type Definitions

## FEATURE

> Implement a documented mechanic, capability, or system that already
> has a home in `docs/` but has not yet been built.

**Use when:** The mechanic/contract/system exists in docs/; the task is
to build it. If the required mechanic does NOT exist in docs/, this is
not a FEATURE — it requires a GAMEPLAY-CHANGE or ARCHITECTURE task
first.

**Do NOT use when:** The implementation requires inventing behavior not
in docs/ (see development/feature.md §4).

| Field | Value |
|---|---|
| Workflow | `development/feature.md` |
| Primary Agent | Depends on domain (see AGENT_SELECTION.md) |
| Supporting Agents | Testing, Review; plus domain-adjacent agents |
| Risk Baseline | MEDIUM (gameplay feature), MEDIUM (backend/realtime/db feature) |
| First Check | `docs/00-overview/MVP_SCOPE.md §1` — must be IN |

---

## BUG

> Fix behavior that deviates from documented intent.

**Use when:** Code behavior does not match what `docs/` says it should.
This includes implementation bugs, documentation bugs (docs are stale),
and test bugs (test asserts the wrong thing). The bug category must be
determined first (development/bug-fix.md §1) before fixing anything.

| Field | Value |
|---|---|
| Workflow | `development/bug-fix.md` |
| Primary Agent | Depends on which domain the bug is in |
| Supporting Agents | Testing (regression test), Review |
| Risk Baseline | LOW–MEDIUM (depends on what the bug touches) |
| First Check | Identify expected behavior from docs/ before touching code |

---

## GAMEPLAY-CHANGE

> Change a game rule in `docs/01-game-design/` and propagate the change
> through implementation and tests.

**Use when:** A gameplay mechanic needs to behave differently than the
documentation currently says, OR a new undocumented mechanic is being
authorized.

**Critical:** A GAMEPLAY-CHANGE task must always update authoritative
documentation *before* implementation. The task does not authorize
inventing rules — it authorizes *changing* existing ones or *adding*
new ones through the proper design-change branch
(development/gameplay-change.md §3).

| Field | Value |
|---|---|
| Workflow | `development/gameplay-change.md` |
| Primary Agent | Gameplay Agent (domain rule change) |
| Supporting Agents | Backend, Testing, Review; Realtime if events change |
| Risk Baseline | HIGH (game rule change) |
| First Check | `docs/00-overview/MVP_SCOPE.md` — change must stay in scope |

---

## REFACTOR

> Restructure existing code without any intended behavior change.

**Use when:** The code needs to be reorganized, renamed, or cleaned up,
but the externally observable behavior (APIs, events, state, game rules)
must remain identical.

**Critical:** If a refactor cannot preserve behavior without also
changing it, the task must be split (development/refactor.md §1). A
REFACTOR task that silently changes behavior is a defect.

| Field | Value |
|---|---|
| Workflow | `development/refactor.md` |
| Primary Agent | Depends on which layer is being refactored |
| Supporting Agents | Testing (existing tests must still pass), Review |
| Risk Baseline | LOW–MEDIUM (depends on what's being refactored) |
| First Check | Inventory of contracts that must not move (refactor.md §2) |

---

## ARCHITECTURE

> Change the project's structure, layering, technology choice,
> persistence strategy, realtime strategy, or authoritative-state model.

**Use when:** The task changes something `docs/02-technical/ARCHITECTURE.md`
or `docs/02-technical/TDD.md` currently governs, or requires a new/updated
ADR.

**Critical:** Documentation and ADR must be updated *before*
implementation begins (architecture/architecture-change.md §2).

| Field | Value |
|---|---|
| Workflow | `architecture/architecture-change.md` (+ `architecture/adr-change.md`) |
| Primary Agent | Backend Agent (architecture owner) |
| Supporting Agents | Review; plus agents affected by the change |
| Risk Baseline | HIGH |
| First Check | Check existing ADRs for conflict; check MVP_SCOPE.md |

---

## DOCUMENTATION

> Change `docs/` content — documentation is the primary output, not
> code.

**Use when:** A document in `docs/` needs to be created, updated, or
corrected, and no code change is required. Code changes that also
update docs as a side effect do not use this type — they use the
appropriate code-change type and include the doc update in the same task.

**Critical:** Only the canonical owner of a concept may define it.
No duplication (documentation/documentation-change.md §2).

| Field | Value |
|---|---|
| Workflow | `documentation/documentation-change.md` |
| Primary Agent | Review Agent (documentation consistency) |
| Supporting Agents | Domain-relevant agent for content accuracy |
| Risk Baseline | LOW–MEDIUM (depends on what's changing) |
| First Check | Identify the canonical owner document before editing |

---

# 3. Type Selection Guide

```text
"I need to build something documented but not yet built"
    → FEATURE

"Something is broken / not matching docs"
    → BUG

"A game rule needs to change"
    → GAMEPLAY-CHANGE

"The code structure needs cleaning without behavior change"
    → REFACTOR

"The architecture / technology / ADR needs to change"
    → ARCHITECTURE

"A document needs to be corrected or added"
    → DOCUMENTATION
```

If a task description spans multiple types (e.g. a gameplay rule change
that also requires architecture changes), split into separate tasks.
Do not blend types — each task has exactly one type.

---

# 4. Risk Baseline by Type

```text
Type               Risk Baseline   Notes
─────────────────  ──────────────  ────────────────────────────────────
FEATURE            MEDIUM          Can be HIGH if it touches combat /
                                   battle state / auth
BUG                LOW–MEDIUM      Classify per what the bug touches
GAMEPLAY-CHANGE    HIGH            Always HIGH — game rule changes are
                                   the riskiest category
REFACTOR           LOW–MEDIUM      LOW for isolated, MEDIUM if it
                                   crosses a contract boundary
ARCHITECTURE       HIGH            Always HIGH
DOCUMENTATION      LOW–MEDIUM      LOW for corrections, MEDIUM if it
                                   affects a cross-referenced contract
```

Risk determines validation depth per `core/validation.md §2`. When in
doubt, classify at the higher level.

---

# 5. Domain × Type Matrix

Use with `AGENT_SELECTION.md` to identify the primary agent:

```text
Domain            FEATURE         BUG             GAMEPLAY-CHANGE   REFACTOR
────────────────  ──────────────  ──────────────  ────────────────  ────────
Match-3/Combat/   Gameplay        Gameplay        Gameplay          Gameplay
Pet/Card/Relic/
Boss/Element

Application/API   Backend         Backend         —                 Backend

Frontend/Phaser   Client          Client          —                 Client

SignalR/Redis     Realtime        Realtime        —                 Realtime

PostgreSQL        Persistence     Persistence     —                 Persistence

Documentation     —               Review          Review            —

Architecture      Backend         Backend         —                 Backend
                  (+ Review ADR)
```
