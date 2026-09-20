# HARNESS_MAPPING.md — Canonical-to-Runtime Mapping

**Version:** 1.0  
**Status:** Binding  
**Scope:** Defines the exact mapping between canonical `.ai/` components and native harness runtime files.

---

# 1. Overview

This document maps canonical definitions to their runtime adapter implementations.

```text
CANONICAL (.ai/ + docs/ + tasks/)              RUNTIME ADAPTER (.agents/)
==================================              ==========================
AGENTS.md (Root Contract)              ───────► Auto-loaded Workspace Rule
.ai/agents/ (Role Definitions)         ───────► .agents/rules/agent-routing.md
.ai/skills/ (Procedural Skills)        ───────► .agents/skills/<name>/SKILL.md
.ai/workflow/ (Process Runbooks)       ───────► Dynamic On-Demand Execution
tasks/ (Task Management)               ───────► Direct File Manipulation
```

---

# 2. Agent Mappings

| Canonical Agent Definition | Primary Responsibility | Runtime Implementation (Antigravity) |
| :--- | :--- | :--- |
| [.ai/agents/orchestrator.md](file:///e:/dcacti/.ai/agents/orchestrator.md) | Multi-domain coordination, intake, handoff | Dynamic role via `.agents/rules/agent-routing.md` |
| [.ai/agents/backend.md](file:///e:/dcacti/.ai/agents/backend.md) | API contracts, business logic, endpoints | Dynamic role via `.agents/rules/agent-routing.md` |
| [.ai/agents/gameplay.md](file:///e:/dcacti/.ai/agents/gameplay.md) | Authority, match-3, combat, determinism | Dynamic role via `.agents/rules/agent-routing.md` |
| [.ai/agents/client.md](file:///e:/dcacti/.ai/agents/client.md) | Phaser/React rendering, optimistic UX | Dynamic role via `.agents/rules/agent-routing.md` |
| [.ai/agents/realtime.md](file:///e:/dcacti/.ai/agents/realtime.md) | SignalR, event ordering, netcode | Dynamic role via `.agents/rules/agent-routing.md` |
| [.ai/agents/persistence.md](file:///e:/dcacti/.ai/agents/persistence.md) | Redis active battle state, PostgreSQL | Dynamic role via `.agents/rules/agent-routing.md` |
| [.ai/agents/testing.md](file:///e:/dcacti/.ai/agents/testing.md) | Scenario generation, test coverage | Dynamic role via `.agents/rules/agent-routing.md` |
| [.ai/agents/review.md](file:///e:/dcacti/.ai/agents/review.md) | Pre-merge verification, architecture check | Dynamic role via `.agents/rules/agent-routing.md` |

---

# 3. Skill Mappings (12 Canonical Skills)

| Domain | Canonical Skill Definition | Runtime Adapter File (Antigravity) |
| :--- | :--- | :--- |
| **Discovery** | [.ai/skills/discovery/documentation-discovery.md](file:///e:/dcacti/.ai/skills/discovery/documentation-discovery.md) | [.agents/skills/documentation-discovery/SKILL.md](file:///e:/dcacti/.agents/skills/documentation-discovery/SKILL.md) |
| **Discovery** | [.ai/skills/discovery/impact-analysis.md](file:///e:/dcacti/.ai/skills/discovery/impact-analysis.md) | [.agents/skills/impact-analysis/SKILL.md](file:///e:/dcacti/.agents/skills/impact-analysis/SKILL.md) |
| **Discovery** | [.ai/skills/discovery/scope-validation.md](file:///e:/dcacti/.ai/skills/discovery/scope-validation.md) | [.agents/skills/scope-validation/SKILL.md](file:///e:/dcacti/.agents/skills/scope-validation/SKILL.md) |
| **Backend** | [.ai/skills/backend/api-contract-validation.md](file:///e:/dcacti/.ai/skills/backend/api-contract-validation.md) | [.agents/skills/api-contract-validation/SKILL.md](file:///e:/dcacti/.agents/skills/api-contract-validation/SKILL.md) |
| **Backend** | [.ai/skills/backend/persistence-analysis.md](file:///e:/dcacti/.ai/skills/backend/persistence-analysis.md) | [.agents/skills/persistence-analysis/SKILL.md](file:///e:/dcacti/.agents/skills/persistence-analysis/SKILL.md) |
| **Gameplay** | [.ai/skills/gameplay/authority-determinism-audit.md](file:///e:/dcacti/.ai/skills/gameplay/authority-determinism-audit.md) | [.agents/skills/authority-determinism-audit/SKILL.md](file:///e:/dcacti/.agents/skills/authority-determinism-audit/SKILL.md) |
| **Gameplay** | [.ai/skills/gameplay/gameplay-behavior-derivation.md](file:///e:/dcacti/.ai/skills/gameplay/gameplay-behavior-derivation.md) | [.agents/skills/gameplay-behavior-derivation/SKILL.md](file:///e:/dcacti/.agents/skills/gameplay-behavior-derivation/SKILL.md) |
| **Realtime** | [.ai/skills/realtime/realtime-protocol-validation.md](file:///e:/dcacti/.ai/skills/realtime/realtime-protocol-validation.md) | [.agents/skills/realtime-protocol-validation/SKILL.md](file:///e:/dcacti/.agents/skills/realtime-protocol-validation/SKILL.md) |
| **Testing** | [.ai/skills/testing/test-scenario-generation.md](file:///e:/dcacti/.ai/skills/testing/test-scenario-generation.md) | [.agents/skills/test-scenario-generation/SKILL.md](file:///e:/dcacti/.agents/skills/test-scenario-generation/SKILL.md) |
| **Quality** | [.ai/skills/quality/architecture-conformance.md](file:///e:/dcacti/.ai/skills/quality/architecture-conformance.md) | [.agents/skills/architecture-conformance/SKILL.md](file:///e:/dcacti/.agents/skills/architecture-conformance/SKILL.md) |
| **Quality** | [.ai/skills/quality/documentation-consistency.md](file:///e:/dcacti/.ai/skills/quality/documentation-consistency.md) | [.agents/skills/documentation-consistency/SKILL.md](file:///e:/dcacti/.agents/skills/documentation-consistency/SKILL.md) |
| **Quality** | [.ai/skills/quality/implementation-review.md](file:///e:/dcacti/.ai/skills/quality/implementation-review.md) | [.agents/skills/implementation-review/SKILL.md](file:///e:/dcacti/.agents/skills/implementation-review/SKILL.md) |

---

# 4. Workflow Mappings

Workflows in `.ai/workflow/` serve as executable process specifications. Runtime agents read them on demand:

| Workflow Category | Canonical Runbooks | Runtime Invocation |
| :--- | :--- | :--- |
| **Core Lifecycle** | `core/task-intake.md`<br>`core/context-discovery.md`<br>`core/planning.md`<br>`core/implementation.md`<br>`core/validation.md`<br>`core/completion.md` | Executed sequentially during task processing |
| **Development** | `development/feature.md`<br>`development/bug-fix.md`<br>`development/refactor.md`<br>`development/gameplay-change.md` | Selected based on task type |
| **Quality** | `quality/review.md`<br>`quality/testing.md` | Triggered during verification phase |
| **Architecture** | `architecture/adr-lifecycle.md`<br>`architecture/boundary-change.md` | Invoked when architectural decisions shift |
| **Documentation** | `documentation/doc-update.md`<br>`documentation/sync-check.md` | Invoked during doc maintenance |

---

# 5. Task System Mappings

| Canonical Task Directory | State & Semantics | Runtime Behavior |
| :--- | :--- | :--- |
| `tasks/backlog/` | Unstarted, prioritized work | Inspected for upcoming work |
| `tasks/active/` | Work in progress (WIP limit = 1) | Single task executed by current session |
| `tasks/blocked/` | Halted due to stop condition | Recorded with reason & required input |
| `tasks/completed/` | Finished work with full audit trail | Archived upon task completion |
