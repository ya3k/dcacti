# .ai/README.md — AI System Documentation

**Version:** 1.0
**Status:** Binding
**Scope:** Defines how `.ai/agents/`, `.ai/skills/`, and `.ai/workflow/`
must behave once they exist. **This task creates no agents, skills, or
workflows** — only this document.

> `.ai/` is the AI coordination layer. It sits **below** `AGENTS.md` and
> **below** `docs/`. It tells AI *how to execute* work using the source of
> truth in `docs/`; it never becomes a source of truth itself.

---

# 1. Position in the Hierarchy

```text
GAME DESIGN                    docs/00-overview/, docs/01-game-design/
        ↓
TECHNICAL DESIGN                 docs/02-technical/
        ↓
ARCHITECTURAL DECISIONS            docs/03-decisions/ADR/
        ↓
AGENTS.md                            global engineering contract (root)
        ↓
.ai/                                    ← this layer
    ├── README.md                          (this file)
    ├── agents/                             (not yet created)
    ├── skills/                              (not yet created)
    └── workflow/                             (not yet created)
        ↓
tasks/                                       work items
        ↓
src/                                          implementation
        ↓
tests/                                          verification
```

```text
docs/       = Source of Truth for game design and technical design
AGENTS.md   = global engineering contract every AI agent must follow
.ai/        = HOW AI executes work against docs/ and AGENTS.md
tasks/      = concrete work items
src/        = implementation
tests/      = verification
```

`.ai/` is pure execution scaffolding. If a file under `.ai/` ever states a
number, a rule, or a decision that isn't traceable to `docs/`, that file is
wrong — not `docs/`.

---

# 2. Structure Overview

```text
.ai/
├── README.md      this document — explains the layer, creates nothing else
├── agents/          role definitions: who does what, what they may read,
│                    what they must never decide themselves
├── skills/           reusable procedures: how to carry out a recurring
│                    kind of work, referencing docs/ for every rule/number
└── workflow/           process definitions: the ordered steps an agent
                       follows to go from task to done
```

None of `agents/`, `skills/`, `workflow/` exist yet. This document defines
the contract they must satisfy when created — it does not mandate a
specific set of agents, skills, or workflows, and it does not assume any
particular count or names are required.

---

# 3. `agents/` — Purpose and Boundaries

An **agent** is a role: a named responsibility scope for a category of
work (e.g. a game-design-focused role, a backend role, a frontend role, a
gameplay-logic role, a database role, a testing role, a review role).
These are illustrative categories only — this document does not require
the project to have exactly these agents, or all of them.

Every agent definition, when created, must specify:

```text
- What the agent is responsible for
- What docs/ sections the agent must read for its domain (see §5)
- What the agent may NOT do (see §6)
- What skills the agent is allowed to invoke
- What workflow(s) the agent operates under
```

An agent file must never restate a game rule, a technical contract, or an
ADR decision as if the agent file were the origin of that information — it
must reference the owning `docs/` document instead.

---

# 4. `skills/` — Purpose and Boundaries

A **skill** is a reusable procedure — a repeatable "how to do X" capability
an agent can invoke (e.g. resolving a Match-3 action, calculating combat
damage, validating a battle state, implementing an API endpoint, running a
database migration, handling a SignalR event, generating test scenarios,
auditing documentation). These are illustrative examples only; none are
created by this task.

A skill differs from an agent: an **agent** is a role (who), a **skill** is
a procedure (how). One agent may use many skills; one skill may be used by
many agents.

Every skill, when created, must define:

```text
- Input
- Output
- Prerequisites
- The source-of-truth doc(s) it reads rules/values from
- Validation it performs
- Failure / stop conditions
```

A skill must never embed a game-design value or business rule that already
has a canonical home in `docs/`. For example, a skill must not contain:

```text
MATCH_5_POWER = 25
```

It must instead say:

```text
Read docs/02-technical/... or the relevant docs/01-game-design/...RULES.md
for the authoritative value; do not hardcode it here.
```

A skill that embeds such a value has become a hidden, undocumented second
source of truth — this is exactly what `.ai/` must never allow (§7).

---

# 5. `workflow/` — Purpose and Boundaries

A **workflow** controls the lifecycle of an AI task — the ordered sequence
an agent follows from receiving a task to reporting it done. None are
created by this task; the standard shape any workflow must follow is:

```text
1. Discover        — identify the task's domain and affected docs (§9)
2. Read relevant docs
3. Validate assumptions against docs
4. Detect conflicts (docs vs docs, docs vs task)
5. Plan
6. Implement
7. Test
8. Review
9. Update docs if required (per policy in §14)
10. Report (per format in §17)
```

A workflow must never let an agent skip step 2 or step 4 to "save time," and
must never let implementation proceed past step 4 while an unresolved
conflict exists (§10).

---

# 6. Source of Truth Rule

**`.ai/agents/`, `.ai/skills/`, and `.ai/workflow/` are never a source of
decision for game rules, technical contracts, or architecture.** They read
from `docs/`; they do not originate anything `docs/` already governs.

```text
Pet stats               → docs/01-game-design/PET_RULES.md
Match behavior            → docs/01-game-design/MATCH3_RULES.md
Element modifiers           → docs/01-game-design/ELEMENT_RULES.md
Damage formula                 → docs/01-game-design/COMBAT_RULES.md
Passive charge/trigger            → docs/01-game-design/PASSIVE_RULES.md
Card behavior                        → docs/01-game-design/CARD_RULES.md
Relic triggers                          → docs/01-game-design/RELIC_RULES.md
Boss behavior                              → docs/01-game-design/BOSS_RULES.md
MVP scope                                     → docs/00-overview/MVP_SCOPE.md
Build phases                                    → docs/00-overview/ROADMAP.md
Runtime battle state                              → docs/02-technical/GAME_STATE.md
Events                                              → docs/02-technical/GAME_EVENTS.md
REST API                                              → docs/02-technical/API_CONTRACTS.md
Realtime protocol                                       → docs/02-technical/SIGNALR_PROTOCOL.md
Active state storage                                       → docs/02-technical/REDIS_STATE.md
Persistent schema                                             → docs/02-technical/DATABASE.md
Architecture                                                    → docs/02-technical/ARCHITECTURE.md
Technical direction                                                → docs/02-technical/TDD.md
Architectural rationale ("why")                                        → docs/03-decisions/ADR/
```

**If an agent/skill/workflow instruction conflicts with `docs/`: `docs/`
wins**, unconditionally. The `.ai/` file is wrong and must be corrected to
match `docs/` — this is a documentation bug in `.ai/`, not a reason to
change game or technical design.

**If two documents inside `docs/` conflict with each other:**

```text
STOP
 → identify the conflicting sources (file + section, both sides)
 → report the conflict
 → propose the smallest documentation correction
 → do NOT silently choose one side
 → do NOT proceed with implementation until resolved
```

This mirrors `AGENTS.md` §4 exactly — `.ai/` does not get its own,
different conflict-resolution process.

---

# 7. Document Discovery

AI does not read the entire repository for every task. The discovery
principle:

```text
Task
 ↓
Identify domain(s) the task touches
 ↓
Identify the required docs for that domain (not a fixed global list —
  derive it from the current docs/ structure, since it may evolve)
 ↓
Read the relevant docs
 ↓
Check cross-document dependencies the docs themselves reference
 ↓
Execute
```

Illustrative examples (current `docs/` structure — re-derive if the
structure changes, do not treat this as a frozen list):

```text
Match-3 task
  Minimum: GAME_RULES.md, MATCH3_RULES.md, GAME_STATE.md, GAME_EVENTS.md
  If damage is involved: + COMBAT_RULES.md, ELEMENT_RULES.md

Backend API task
  TDD.md, ARCHITECTURE.md, API_CONTRACTS.md, GAME_STATE.md, DATABASE.md,
  + whichever domain rule doc(s) the endpoint concerns

SignalR task
  SIGNALR_PROTOCOL.md, GAME_EVENTS.md, GAME_STATE.md, ARCHITECTURE.md
```

This is not an exhaustive or permanent table — see `AGENTS.md` §6 for the
authoritative version of this mapping. This section only states the
*principle* (derive required docs from the task's domain, don't read
everything, don't skip what's relevant).

---

# 8. Agent Responsibility

An agent must:

```text
understand → inspect → plan → implement → verify → report
```

An agent must NOT:

```text
invent game mechanics
invent API contracts
invent database schema
silently change architecture
silently change balance/numbers
silently expand MVP scope
duplicate documentation rules into agent/skill/workflow files
override an ADR decision
```

An agent is not a Product Owner and does not decide game design. If a task
implies a design decision the agent doesn't have documented authority for,
that is a stop condition (§11), not an invitation to decide.

---

# 9. Skill Responsibility

A skill must:

```text
be a reusable procedure with a clear input and output
declare its prerequisites
reference the source-of-truth doc(s) it depends on, by path
perform validation appropriate to what it does
declare its own failure/stop conditions
```

A skill must NOT:

```text
contain duplicated game rules
hard-code a business/balance value that already lives in docs/
become a hidden source of truth
substitute for the domain documentation it depends on
```

---

# 10. Workflow Responsibility

A workflow governs the lifecycle of a task end-to-end (§5's ten steps as a
baseline). A workflow must never:

```text
bypass reading relevant docs
bypass conflict detection
allow implementation to start before conflicts are resolved
skip the report step
```

---

# 11. Task Classification

`.ai/` should let AI classify an incoming task into one or more of these
categories, then apply §7 (document discovery) and choose an appropriate
agent/skill (once those exist) accordingly:

```text
GAME DESIGN
TECHNICAL DESIGN
BACKEND
FRONTEND
GAMEPLAY
DATABASE
REALTIME
TESTING
BUG FIX
REFACTOR
DOCUMENTATION
ARCHITECTURE CHANGE
```

For each category, the eventual `.ai/agents/` and `.ai/skills/` definitions
must specify: which docs to read, which agent role fits, which skills may
be needed, and what validation must run before the task is considered done.
This document defines the requirement that such a mapping must exist and
follow §7's discovery principle — it does not itself create that mapping as
fixed content, since it depends on agent/skill files that don't exist yet.

---

# 12. Change Impact Analysis

A code change is rarely *only* a code change. Before implementing, an agent
must trace the change's impact along its actual chain:

```text
Game rule change
  → owning domain rule doc (docs/01-game-design/...)
  → dependent technical docs (docs/02-technical/...), if state/events/
    contracts reference the changed rule
  → implementation
  → tests
  → possibly a new/updated ADR, if it affects an architectural decision

API contract change
  → docs/02-technical/API_CONTRACTS.md
  → backend implementation
  → frontend implementation
  → tests

Architecture change
  → docs/02-technical/TDD.md
  → docs/02-technical/ARCHITECTURE.md
  → docs/03-decisions/ADR/ (new or superseding ADR)
  → implementation
```

An agent that implements only the last link in one of these chains (code)
without checking the earlier links has done incomplete work, even if the
code itself is correct.

---

# 13. Stop Conditions

AI must STOP — not guess — when it encounters:

```text
docs conflict with each other
a required rule is missing from docs/
architecture is unclear for the task at hand
an API contract is unclear or contradicts another doc
behavior is ambiguous (multiple valid implementations)
the task exceeds MVP scope (docs/00-overview/MVP_SCOPE.md)
the requested change contradicts an existing ADR
a destructive database change has not been explicitly confirmed
a game balance change is requested with no recorded design decision
the task requires a new mechanic that isn't documented anywhere
```

Report format:

```text
STOP CONDITION

Problem:
<one or two sentences>

Relevant sources:
<file paths + section references, both/all sides if a conflict>

Conflict / missing information:
<what exactly is unresolved>

Proposed resolution:
<the smallest change that would resolve it>

Waiting for:
<what kind of approval/input is needed to proceed>
```

---

# 14. MVP Protection

`.ai/` must always check `docs/00-overview/MVP_SCOPE.md` before treating any
new system or content as implementable. AI must not add, on its own
initiative:

```text
PvP, Gacha, Guild, Trading, Marketplace
Microservices, Kubernetes, Kafka
Map mechanics, Terrain mechanics, Weather mechanics
```

or anything else not listed as `IN` in `MVP_SCOPE.md` §1. A "future idea"
recorded in `docs/00-overview/ROADMAP.md` is a direction, not an
implementation requirement — it does not become buildable until it is
promoted into `MVP_SCOPE.md` §1 (see `MVP_SCOPE.md` §4).

---

# 15. Server Authority

`.ai/` must enforce the server-authoritative model (`AGENTS.md` §10,
`docs/01-game-design/GAME_RULES.md` §18, ADR-001) in every gameplay task.
The client must never be treated as the owner of:

```text
damage
HP
boss HP
power
match result
combo result
passive progress
rewards
battle outcome
```

When implementing or reviewing gameplay logic, an agent must be able to
answer, for every piece of state involved:

```text
Who owns this state?
Who calculates it?
Who validates it?
Who broadcasts it?
```

If the answer to any of these is "the client," that is a defect, not an
implementation choice.

---

# 16. Determinism

For any task touching battle simulation, an agent must check:

```text
RNG usage and its seed source
battle action sequence / ordering guarantees
state transition correctness
replay/debug reproducibility, where the docs require it
```

per `docs/02-technical/TDD.md` §6 and `docs/01-game-design/MATCH3_RULES.md`
§7. No randomness affecting gameplay outcome may be introduced on the
frontend.

---

# 17. Testing

`.ai/` must not treat "implementation detail passes" as sufficient for a
gameplay task. A gameplay task's tests must verify the full state
transition, not just the final number:

```text
Input → State transition → Events → Final state
```

Example, matching the fixed resolution order in `GAME_RULES.md` §17:

```text
SWAP → Match → Cascade → Combo → Passive → Resource → Damage
  → Boss response → Turn end
```

Every step in that chain that the task touches must have a verifiable
scenario, not just the endpoint.

---

# 18. Documentation Update Policy

When implementation reveals that docs are missing or wrong, AI must not
silently "fix" docs to match whatever the code happens to do. It must
classify the situation first:

```text
Implementation bug          (docs are correct; code deviates)
Documentation bug            (code reflects the intended, correct behavior;
                              docs are stale)
Design ambiguity               (neither side is clearly wrong; the rule was
                              never fully specified)
Architecture decision required    (the gap is structural, not just a rule)
```

Default assumption when the two disagree: **docs describe intended
behavior; code is the (possibly incorrect) implementation.** Do not default
to "code is right" without evidence — that evidence itself must be reported
(§13's stop-condition format), not applied silently.

---

# 19. AI Memory / Context

No agent may assume it remembers context another agent produced. Every
agent must be able to reconstruct the context it needs purely from:

```text
AGENTS.md
docs/
.ai/
tasks/
relevant source code
relevant tests
```

No hidden, unwritten assumptions may be carried between agents or between
sessions. If context that isn't written down anywhere would be required to
do the task correctly, that is itself a gap to report, not something to
infer from "how things were probably done before."

---

# 20. Multi-Agent Coordination

When multiple agents work on related work:

```text
Documentation
    ↓
Task
    ↓
Implementation Agent
    ↓
Testing Agent
    ↓
Review Agent
```

Each downstream agent reads the actual output of the upstream agent (its
report, per §21) rather than assuming what it probably did. No agent may
overwrite another agent's work without first checking that work — silent
overwrites are a form of the same "silent decision" problem this whole
document exists to prevent.

---

# 21. AI Output Contract

Every agent's final report for a task should use this shape, per
`AGENTS.md` §21, with sections omitted only when genuinely not relevant to
that task (not omitted for brevity):

```text
## Summary
## Changes
## Files Changed
## Docs Consulted
## Tests
## Validation
## Risks
## Documentation Changes
## Remaining Issues
```

This gives every downstream agent (§20) a consistent shape to read from.

---

# 22. Anti-Overengineering

The `.ai/` system must stay strong enough to keep the project disciplined,
without becoming a framework in its own right. It must not accumulate:

```text
deep agent hierarchies (agents managing agents managing agents)
a custom workflow execution engine
unnecessary orchestration layers
duplicate configuration (the same fact declared in two `.ai/` places)
excessive metadata that no workflow step actually reads
automation that isn't tied to a concrete, current task type
```

The target properties are:

```text
Simple
Predictable
Traceable
Documentation-driven
AI-friendly
```

If a proposed addition to `.ai/agents/`, `.ai/skills/`, or `.ai/workflow/`
doesn't clearly serve one of these five properties, it shouldn't be added.

---

# 23. What This Task Does Not Create

This task creates only `.ai/README.md`. It explicitly does not create:

```text
.ai/agents/*
.ai/skills/*
.ai/workflow/*
tasks/*
src/*
tests/*
```

Any future task that creates one of the above must conform to this
document without needing to redefine the principles in it.
