# HARNESS_COMPATIBILITY.md — AI Harness Capability & Compatibility Matrix

**Version:** 1.0  
**Status:** Binding  
**Scope:** Defines the compatibility between the project's canonical AI architecture (`.ai/`, `AGENTS.md`, `tasks/`, `docs/`) and supported runtime harnesses.

---

# 1. Architectural Principle

```text
CANONICAL PROJECT SYSTEM (Source of Truth)
    ├── docs/                  (Game design, technical specs, ADRs)
    ├── AGENTS.md              (Global engineering contract)
    ├── .ai/                   (Canonical AI architecture: agents, skills, workflows)
    └── tasks/                 (Executable work units and lifecycle)
           │
           ▼
HARNESS ADAPTER LAYER          (Thin runtime bridges, no duplicate logic)
           │
           ▼
NATIVE HARNESS RUNTIMES        (Antigravity IDE, Claude Code, etc.)
```

The canonical `.ai/` system remains the single project-level AI architecture. Native harness directories (e.g., `.agents/`) are runtime adapters only.

---

# 2. Harness Capability Matrix

| Capability | Antigravity IDE (`.agents/`) | Claude Code (`.claude/`) | Generic CLI / OpenCode | Notes |
| :--- | :--- | :--- | :--- | :--- |
| **Status in Repo** | **Active & Configured** | *Not Configured* | *Not Configured* | Only active harnesses have physical adapters. |
| **Root Instructions** | Supported (`AGENTS.md`, `.agents/rules/*.md`) | Supported (`CLAUDE.md`) | Supported (System prompt / config) | Auto-injected at session start. |
| **Agents / Roles** | Virtual (Role adoption via instructions) | Subagents (`.claude/agents/`) | System prompt config | Antigravity uses single-agent role-shifting driven by `.ai/agents/`. |
| **Skills Format** | Directory (`skills/<name>/SKILL.md`) | Tool/Skill definitions | Varies | Antigravity auto-discovers YAML frontmatter skills in `.agents/skills/`. |
| **Workflows** | Referenced (`.ai/workflow/`) | Referenced (`.claude/`) | Referenced | Runbooks loaded dynamically during execution. |
| **Subagent Delegation** | Supported via subagent tools | Supported natively | Varies | Subagents inherit workspace rules and skills. |
| **Task Discovery** | File-based (`tasks/active/`, etc.) | File-based (`tasks/`) | File-based (`tasks/`) | Direct inspection of `tasks/` directory. |
| **Auto Skill Loading** | Keyword & description matching | Trigger based | Manual / Triggered | Driven by `name` & `description` in YAML frontmatter. |
| **Auto Role Selection** | Rule-driven (`.agents/rules/agent-routing.md`) | Slash/Subagent dispatch | Manual / System prompt | Guided by task type and `RESPONSIBILITY_MATRIX.md`. |

---

# 3. Detailed Capability Analysis for Active Harnesses

## 3.1 Antigravity IDE (`.agents/`)

* **Discovery Root:** `.agents/` directory in workspace root.
* **Global Rules:** Automatically loads `AGENTS.md` at workspace root and all `.md` files in `.agents/rules/`.
* **Skill Discovery:** Discovers `.agents/skills/<skill-name>/SKILL.md` where YAML frontmatter defines:
  ```yaml
  ---
  name: <skill-name>
  description: <trigger conditions and summary>
  ---
  ```
* **Agent Mechanism:** Antigravity operates as a unified agent that can dynamically adopt specialized personas defined in `.ai/agents/` based on context and task type.
* **Workflows:** Read on demand from `.ai/workflow/` when executing standard processes (features, bug fixes, refactoring, reviews).

---

# 4. Adapter Strategy Applied

| Component | Canonical Location | Adapter Strategy | Runtime Target |
| :--- | :--- | :--- | :--- |
| **Global Contract** | `AGENTS.md` | Direct Root Load | Root `AGENTS.md` (Native Auto-discovery) |
| **Agent Roles** | `.ai/agents/*.md` | Strategy A (Reference) | `.agents/rules/agent-routing.md` |
| **Skills** | `.ai/skills/*/*.md` | Strategy B (Generated Adapter) | `.agents/skills/<name>/SKILL.md` |
| **Workflows** | `.ai/workflow/*/*.md` | Strategy A (Reference) | Referenced via skills & routing rules |
| **Tasks** | `tasks/**` | Strategy A (Direct Operation) | `tasks/` file system |

---

# 5. Adding New Harnesses

When adding support for a new harness:
1. Do **not** duplicate game rules, business logic, or technical design.
2. Determine if the harness supports direct referencing (Strategy A) or requires generated wrappers (Strategy B).
3. If wrappers are required, generate thin adapters pointing to `.ai/` sources.
4. Update this compatibility matrix and [HARNESS_MAPPING.md](file:///e:/dcacti/.ai/HARNESS_MAPPING.md).
