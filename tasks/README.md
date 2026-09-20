# tasks/README.md — Task System

**Version:** 1.0
**Status:** Binding
**Scope:** Every task file under `tasks/`.

> This document answers: **"What is a task, how is the task system
> structured, and how does it connect to the existing AI architecture?"**
> It does not define game rules, technical contracts, workflows, skills,
> or agents — those belong to their respective layers above.

---

# 1. Position in the Hierarchy

```text
GAME DESIGN                    docs/00-overview/, docs/01-game-design/
        ↓
TECHNICAL DESIGN                 docs/02-technical/
        ↓
ARCHITECTURAL DECISIONS            docs/03-decisions/ADR/
        ↓
AGENTS.md                            global engineering contract
        ↓
.ai/                                   AI coordination layer
    ├── workflow/                       process definitions
    ├── skills/                         reusable procedures
    └── agents/                          role definitions
        ↓
tasks/                                    ← this layer
        ↓
src/                                       implementation
        ↓
tests/                                       verification
```

`tasks/` is execution scaffolding. A task file connects a concrete work
request to the documentation, agent, workflow, and skills required to
execute it. It never becomes a source of truth for game rules,
architecture, or contracts.

---

# 2. What Is a Task?

A task is a **concrete, executable unit of work** assigned to an agent.

A task is NOT:

```text
a game-design document      → docs/01-game-design/ owns game rules
an architecture document    → docs/02-technical/ owns contracts
an agent                    → .ai/agents/ owns role definitions
a skill                     → .ai/skills/ owns reusable procedures
a workflow                  → .ai/workflow/ owns process sequence
a source-of-truth rule
a vague feature request
a TODO list item
```

A task connects:

```text
Work Request
    ↓
Authoritative Documentation (referenced, not copied)
    ↓
Impact Analysis
    ↓
Acceptance Criteria (derived from docs/)
    ↓
Assigned Agent (.ai/agents/)
    ↓
Workflow (.ai/workflow/)
    ↓
Skills (.ai/skills/)
    ↓
Implementation → Tests → Review → Completion
```

Tasks must contain enough information for an agent to execute without
repeatedly rediscovering the entire project — but must NOT duplicate
authoritative documentation. Reference docs/ by path and section;
never copy rules or values into a task file.

---

# 3. Task ID Convention

```text
TASK-NNN
```

- `NNN` is a sequential, zero-padded 3-digit number starting at 001.
- Numbers are never reused, even if a task is cancelled.
- IDs are assigned at task creation time (when added to `backlog/`).
- The next ID is the highest existing ID + 1 across all folders.
- Example: `TASK-001`, `TASK-042`, `TASK-100`.

Do not encode status, type, domain, or date in the ID — those are
fields inside the task file, not part of the identifier.

---

# 4. File Naming

```text
tasks/<folder>/TASK-NNN-short-kebab-case-title.md
```

Examples:

```text
tasks/backlog/TASK-001-implement-match3-cascade.md
tasks/active/TASK-002-fix-combo-damage-calculation.md
tasks/completed/TASK-003-refactor-passive-tracker.md
```

Use the same filename across all folders — only the folder changes when
a task moves between lifecycle stages.

---

# 5. Folder Structure

```text
tasks/
├── README.md              this file — system overview
├── TASK_LIFECYCLE.md      states, transitions, who triggers them
├── TASK_TYPES.md          type → workflow → agent → skills mapping
├── TASK_TEMPLATE.md       canonical task file template
├── backlog/               BACKLOG and READY tasks (not yet started)
├── active/                IN PROGRESS and IN REVIEW tasks
├── blocked/               BLOCKED tasks
└── completed/             DONE tasks
```

### Why two statuses per folder?

Status transitions within a folder (e.g. READY → IN PROGRESS within
`active/`) are reflected in the task file's `Status:` field — not by
moving the file. Moving files on every minor status change creates
unnecessary churn. Files are only moved when crossing a major boundary:

```text
backlog/   → active/     when an agent picks up the task
active/    → blocked/    when a stop condition fires
blocked/   → active/     when the block is resolved
active/    → completed/  when core/completion.md §1 is satisfied
```

See `TASK_LIFECYCLE.md` for the full transition rules.

---

# 6. How to Create a Task

```text
1. Verify the work is in MVP scope (docs/00-overview/MVP_SCOPE.md)
2. Verify the relevant documentation exists in docs/
3. Assign the next available TASK-NNN ID
4. Copy TASK_TEMPLATE.md
5. Fill in all required sections (mark optional sections N/A, never
   omit required sections)
6. Set Status: BACKLOG
7. Save to tasks/backlog/<TASK-NNN-short-title>.md
```

---

# 7. How an Agent Picks Up a Task

```text
1. Read the task file
2. Confirm Status is READY (not BACKLOG — see TASK_LIFECYCLE.md §3)
3. Change Status to IN PROGRESS
4. Move file from backlog/ to active/
5. Execute the assigned Workflow, using the assigned Skills
6. Fill in Completion Evidence when done
7. Change Status to IN REVIEW
8. If all review checks pass → IN REVIEW → DONE → move to completed/
```

---

# 8. Relationship to Workflows, Skills, and Agents

A task file specifies *which* workflow, agent, and skills apply — but
it does not redefine them. The actual process is always governed by
`.ai/workflow/`, the role always governed by `.ai/agents/`, and the
procedures always governed by `.ai/skills/`.

```text
Task file says:   "Use development/feature.md, Gameplay Agent,
                   gameplay-behavior-derivation skill"
Workflow governs: the exact steps, gates, and stop conditions
Agent governs:    the role boundaries and decision authority
Skill governs:    the reusable procedure
```

If a task file's instructions appear to conflict with a workflow, the
workflow wins — report the conflict rather than following the task.

---

# 9. No Business-Rule Duplication

Task files must NOT contain:

```text
damage formulas             → COMBAT_RULES.md
element multipliers         → ELEMENT_RULES.md
match scoring values        → MATCH3_RULES.md
card costs or effects       → CARD_RULES.md
passive thresholds          → PASSIVE_RULES.md
pet stats or tier rules     → PET_RULES.md
relic trigger conditions    → RELIC_RULES.md
boss stat values            → BOSS_RULES.md
API payload shapes          → API_CONTRACTS.md
Redis key schemas           → REDIS_STATE.md
database column lists       → DATABASE.md
SignalR message shapes      → SIGNALR_PROTOCOL.md
```

Reference these documents by path and section. Never copy their
content into a task file.

---

# 10. Stop Conditions

Tasks inherit all stop conditions from `AGENTS.md` §20 and
`.ai/README.md` §13. When any fires, the agent must:

```text
1. Change Status to BLOCKED
2. Move the file from active/ to blocked/
3. Write the STOP CONDITION report in the task's Stop Conditions
   section (using .ai/README.md §13's exact format)
4. Do not proceed with implementation
```

Task-specific stop conditions (documented in each task file) are in
addition to — not a replacement for — the universal ones.
