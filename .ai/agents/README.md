# .ai/agents/README.md — Agent Layer

**Version:** 1.0
**Status:** Binding
**Scope:** Every agent file under `.ai/agents/`.

> This document answers: **"What are agents, how do they relate to the
> rest of the AI system, and how should they be used?"** It does not
> define game rules, technical contracts, workflows, or skills — those
> belong to their respective owners.

---

# 1. What Is an Agent?

An agent is a **role** — a named responsibility scope for a category of
work. It defines *who* performs a type of task, *what* they may inspect
and modify, and *what* they must never decide on their own.

An agent is NOT:

```text
a source of truth              → docs/ owns every rule and contract
a game designer                → docs/01-game-design/ owns game design
an architecture authority      → docs/02-technical/ + ADRs own architecture
a replacement for documentation
a collection of business rules
a workflow                     → .ai/workflow/ owns process sequence
a skill                        → .ai/skills/ owns reusable procedures
an autonomous decision maker
a generic "AI assistant"
```

---

# 2. Position in the Hierarchy

```text
GAME DESIGN                    docs/00-overview/, docs/01-game-design/
        ↓
TECHNICAL DESIGN                 docs/02-technical/
        ↓
ARCHITECTURAL DECISIONS            docs/03-decisions/ADR/
        ↓
AGENTS.md                            global engineering contract
        ↓
.ai/README.md                          AI system documentation
        ↓
.ai/workflow/                            process definitions
        ↓
.ai/skills/                                reusable procedures
        ↓
.ai/agents/                                  ← this layer (role definitions)
        ↓
tasks/                                        work items
        ↓
src/                                            implementation
        ↓
tests/                                            verification
```

Agents operate *within* every layer above them. They must never override
a higher-level document. If an agent definition appears to conflict with
any document above it in this hierarchy, the agent definition is wrong.

---

# 3. How Agents Differ from Other Layers

```text
Layer       Question it answers     Example
─────────   ─────────────────────   ───────────────────────────────────
docs/       "What is true?"         Damage formula, API contract shape
workflow/   "When/in what order?"   Intake → Discovery → Plan → Implement
skill/      "How is X done?"        How to derive gameplay behavior from
                                     rule docs
agent/      "Who does the work?"    The Gameplay Agent handles domain logic
task/       "What to do right now?" "Implement Match-5 Special Gem creation"
```

- **Agent vs. Skill:** An agent is a role (who); a skill is a procedure
  (how). One agent may use many skills; one skill may be used by many
  agents. An agent must not re-describe a skill's procedure.
- **Agent vs. Workflow:** Agents execute workflows. They do not redefine
  workflow sequence or gates. If a workflow says "stop on conflict,"
  the agent stops — it does not override that gate.
- **Agent vs. Docs:** Agents reference documentation; they never
  originate game rules, technical contracts, or architectural decisions.

---

# 4. How to Select an Agent

See `AGENT_SELECTION.md` for the full matrix. The general principle:

```text
1. Classify the task (core/task-intake.md categories)
2. Identify which domain(s) the task touches
3. Look up the primary agent for that task type
4. The Orchestrator Agent coordinates when multiple agents are involved
```

---

# 5. Authority Boundaries

Every agent defines:

- **Responsibilities** — what it owns
- **Non-Responsibilities** — what it must NOT own
- **Decision Authority** — what decisions it may make
- **Stop Conditions** — when it must halt

See `RESPONSIBILITY_MATRIX.md` for the ownership boundary map that
prevents two agents from believing they own the same concern.

---

# 6. Stop Conditions (Universal)

Every agent inherits these stop conditions from `AGENTS.md` §20 and
`.ai/README.md` §13. When any of these is encountered, the agent must
STOP and report — not guess:

```text
conflicting documentation
missing game rule
missing API contract
architecture conflict
undocumented gameplay behavior
MVP scope conflict
destructive migration
security-sensitive ambiguity
nondeterministic gameplay behavior
server-authority violation
missing required context
```

Individual agent files add domain-specific stop conditions on top of
these universal ones.

---

# 7. Handoff Principles

When one agent's work feeds another:

```text
1. The upstream agent produces a structured report (AGENTS.md §21 /
   .ai/README.md §21 / core/completion.md §2)
2. The downstream agent reads the actual output — never assumes what
   was probably done
3. Handoff information includes: what was completed, what remains,
   known risks, validation status
4. No hidden state may be carried between agents (.ai/README.md §19)
```

---

# 8. Server-Authority Rule

Every implementation-related agent must enforce:

```text
The server is authoritative.
The client sends intent.
The server validates and resolves gameplay.
The client renders the result.
```

No agent may introduce client-authoritative: Damage, HP, Boss HP,
Power, Match results, Combo, Passive progression, Rewards, or Battle
outcomes. See `AGENTS.md` §10, `.ai/README.md` §15, ADR-001.

---

# 9. MVP Protection

Every agent must check `docs/00-overview/MVP_SCOPE.md` before
implementing anything that could be a new system or content. Agents
must not introduce systems listed as OUT (`MVP_SCOPE.md` §2) or absent
from IN (`MVP_SCOPE.md` §1 — treat unlisted as FUTURE). See
`AGENTS.md` §8, `.ai/README.md` §14.

---

# 10. Documentation-First Rule

Before implementation, every agent must:

```text
1. Discover relevant docs (core/context-discovery.md)
2. Verify the rule exists
3. Check for conflicts
4. Identify architecture constraints
5. Determine relevant skills
6. Select the correct workflow
7. Only then implement
```

If required behavior is not documented: STOP. Do not invent behavior.

---

# 11. No Business-Rule Duplication

Agent definitions must NOT contain copies of: damage formulas, element
multipliers, Match-3 rules, card costs, passive thresholds, boss stats,
pet stats, relic effects, API schemas, Redis schemas, or database
schemas. Those belong in authoritative documentation. Agents reference
them by path and section.

---

# 12. No Skill / Workflow Duplication

If a capability exists as a skill under `.ai/skills/`, agents reference
the skill — they do not recreate the procedure. If a process exists in
`.ai/workflow/`, agents reference the workflow — they do not redefine it.

---

# 13. Agent Output Format

Every completed agent task should report using the shape defined in
`.ai/README.md` §21 / `core/completion.md` §2:

```text
## Task
What was requested.

## Context
Relevant docs and decisions inspected.

## Agent
Which agent executed the work.

## Workflow
Which workflow was used.

## Skills
Which skills were used.

## Changes
What was changed.

## Validation
What was validated.

## Tests
What tests were run or added.

## Documentation Impact
Whether documentation needs updating.

## Risks
Known risks or unresolved issues.

## Handoff
What the next agent needs to know.

## Status
DONE / BLOCKED / NEEDS_DECISION
```

This extends `core/completion.md` §2 with agent/workflow/skill
traceability. The core completion report sections (`Summary`, `Changes`,
`Tests`, etc.) must still be present.

---

# 14. When to Add a New Agent

A new agent should only be created when ALL of the following hold:

```text
1. There is a stable responsibility boundary that maps to an
   architectural or domain boundary in docs/02-technical/ or
   docs/01-game-design/.
2. Existing agents cannot reasonably own the responsibility.
3. The responsibility appears repeatedly across workflows/tasks.
4. It requires distinct context or decision authority.
5. It cannot be adequately represented by a skill.
6. Adding the agent reduces ambiguity rather than increasing
   complexity.
```

---

# 15. When NOT to Add a New Agent

Do not create an agent when:

```text
- The responsibility is a technology, not a domain (e.g. "TypeScript
  Agent" — technology expertise is provided through skills)
- It would only wrap a single skill
- It would duplicate another agent's responsibility
- It would create manager-agents-managing-manager-agents
- The responsibility is covered by an existing agent's scope
- It would add complexity without reducing ambiguity
```

---

# 16. Agent Catalog

```text
.ai/agents/
├── README.md                    this file
├── AGENT_SELECTION.md           task type → agent mapping
├── RESPONSIBILITY_MATRIX.md     ownership boundary matrix
├── orchestrator.md              task orchestration and coordination
├── gameplay.md                  gameplay domain logic
├── backend.md                   backend application layer
├── client.md                    frontend / game client
├── realtime.md                  realtime communication + active state
├── persistence.md               durable data persistence
├── testing.md                   testing and verification
└── review.md                    review and documentation consistency
```
