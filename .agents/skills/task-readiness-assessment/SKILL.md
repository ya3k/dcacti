---
name: task-readiness-assessment
description: Assess candidate task readiness and determine the next eligible project task using minimal sufficient evidence.
---

# Task Readiness Assessment (Antigravity Runtime Adapter)

This is a thin runtime adapter for Antigravity IDE. All procedures, rules, and logic are canonically maintained under `.ai/`.

## Canonical Instructions

- **Canonical Skill:** [.ai/skills/discovery/task-readiness-assessment.md](../../../.ai/skills/discovery/task-readiness-assessment.md)
- **Canonical Workflow:** [.ai/workflow/core/task-selection.md](../../../.ai/workflow/core/task-selection.md)
- **Project State Snapshot:** [.ai/PROJECT_STATE.md](../../../.ai/PROJECT_STATE.md)
- **State Collection Script:** `scripts/collect-project-state.sh`

## Runtime Execution

1. Run `scripts/collect-project-state.sh` to obtain the current repository baseline.
2. Select audit mode (**FAST**, **INCREMENTAL**, or **DEEP**) per `.ai/workflow/core/task-selection.md` §2.
3. Assess candidate task readiness per `.ai/skills/discovery/task-readiness-assessment.md`.
4. Update the navigation cache in `.ai/PROJECT_STATE.md` with the verified findings.
