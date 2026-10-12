---
name: task-readiness-assessment
description: Assess candidate task readiness and determine the next eligible project task using minimal sufficient evidence.
---

# Task Readiness Assessment (Claude Code Runtime Adapter)

This is a thin project-level skill adapter for Claude Code. All procedures, criteria, and workflow logic are canonically maintained under `.ai/`.

## Canonical Instructions

- **Canonical Skill:** [.ai/skills/discovery/task-readiness-assessment.md](../../.ai/skills/discovery/task-readiness-assessment.md)
- **Canonical Workflow:** [.ai/workflow/core/task-selection.md](../../.ai/workflow/core/task-selection.md)
- **Project State Snapshot:** [.ai/PROJECT_STATE.md](../../.ai/PROJECT_STATE.md)
- **State Collection Helper:** `scripts/collect-project-state.sh`

## Invocation

Execute the assessment following `.ai/workflow/core/task-selection.md`:
1. Collect baseline metadata (`scripts/collect-project-state.sh` or Git status).
2. Choose audit depth (**FAST**, **INCREMENTAL**, or **DEEP**) per `.ai/workflow/core/task-selection.md` §2.
3. Validate candidate tasks in `tasks/backlog/` using `.ai/skills/discovery/task-readiness-assessment.md`.
4. Output the assessment and update `.ai/PROJECT_STATE.md`.
