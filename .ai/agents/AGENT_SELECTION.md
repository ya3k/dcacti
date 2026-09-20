# Agent Selection Matrix

**Version:** 1.0

> This document answers: **"Given a task type, which agent handles it,
> which agents support it, and which skills/workflows apply?"**

---

# 1. Selection Matrix

| Task Type | Primary Agent | Supporting Agents | Relevant Skills | Relevant Workflow |
| --- | --- | --- | --- | --- |
| Gameplay feature | Gameplay | Backend, Testing, Review | gameplay-behavior-derivation, authority-determinism-audit, test-scenario-generation | development/feature.md |
| Backend feature | Backend | Testing, Review | api-contract-validation, architecture-conformance, persistence-analysis | development/feature.md |
| Frontend feature | Client | Testing, Review | authority-determinism-audit, realtime-protocol-validation | development/feature.md |
| Match-3 change | Gameplay | Backend, Testing, Review | gameplay-behavior-derivation, authority-determinism-audit, test-scenario-generation | development/gameplay-change.md |
| Combat change | Gameplay | Backend, Testing, Review | gameplay-behavior-derivation, authority-determinism-audit, test-scenario-generation | development/gameplay-change.md |
| Card change | Gameplay | Backend, Testing, Review | gameplay-behavior-derivation, authority-determinism-audit, test-scenario-generation | development/gameplay-change.md |
| Pet change | Gameplay | Backend, Testing, Review | gameplay-behavior-derivation, authority-determinism-audit, test-scenario-generation | development/gameplay-change.md |
| Relic change | Gameplay | Backend, Testing, Review | gameplay-behavior-derivation, authority-determinism-audit, test-scenario-generation | development/gameplay-change.md |
| Boss change | Gameplay | Backend, Testing, Review | gameplay-behavior-derivation, authority-determinism-audit, test-scenario-generation | development/gameplay-change.md |
| API change | Backend | Client, Testing, Review | api-contract-validation, impact-analysis | development/feature.md |
| SignalR change | Realtime | Backend, Client, Testing, Review | realtime-protocol-validation, authority-determinism-audit | development/feature.md |
| Redis state change | Realtime | Backend, Testing, Review | persistence-analysis, authority-determinism-audit | development/feature.md |
| Database change | Persistence | Backend, Testing, Review | persistence-analysis, architecture-conformance | development/feature.md |
| Bug fix | (determined by domain) | Testing, Review | documentation-discovery, documentation-consistency, test-scenario-generation | development/bug-fix.md |
| Refactor | (determined by domain) | Testing, Review | impact-analysis, architecture-conformance | development/refactor.md |
| Gameplay rule change | Gameplay | Review | gameplay-behavior-derivation, scope-validation, documentation-consistency | development/gameplay-change.md |
| Architecture change | Backend | Review, Realtime, Persistence | architecture-conformance, impact-analysis | architecture/architecture-change.md |
| Documentation change | Review | (determined by domain) | documentation-discovery, documentation-consistency, impact-analysis | documentation/documentation-change.md |
| Testing | Testing | (determined by domain) | test-scenario-generation, gameplay-behavior-derivation | quality/testing.md |
| Code review | Review | (determined by domain) | implementation-review, scope-validation | quality/review.md |

---

# 2. Selection Procedure

```text
1. Classify the task (core/task-intake.md §2)
2. Find the matching row in the matrix above
3. The Primary Agent owns the task
4. Supporting Agents are consulted as needed
5. The Orchestrator Agent coordinates multi-agent tasks
6. Skills listed are candidates — depth is determined by
   core/validation.md based on the task's risk level
```

---

# 3. Bug Fix Agent Selection

Bug fixes do not have a fixed primary agent — the primary agent is
determined by the domain the bug touches:

```text
Bug in Match-3 logic         → Gameplay Agent
Bug in API response          → Backend Agent
Bug in UI rendering          → Client Agent
Bug in SignalR delivery      → Realtime Agent
Bug in database persistence  → Persistence Agent
Bug in test assertions       → Testing Agent
```

The Orchestrator Agent determines which domain a bug belongs to during
`core/task-intake.md`.

---

# 4. Refactor Agent Selection

Like bug fixes, refactors use the domain-appropriate agent:

```text
Domain module refactor       → Gameplay Agent
Application layer refactor   → Backend Agent
Client code refactor         → Client Agent
Infrastructure refactor      → Realtime or Persistence Agent
Test refactor                → Testing Agent
```

---

# 5. Multi-Domain Tasks

When a task spans multiple domains (e.g. "implement the Card Cast
feature" touches Gameplay for card logic, Backend for the use case,
Realtime for SignalR event delivery, and Client for rendering):

```text
1. Orchestrator Agent classifies and decomposes
2. Primary agent is determined by where the core logic lives
   (Gameplay Agent for Card logic)
3. Supporting agents handle their respective boundaries
4. Testing Agent validates the full chain
5. Review Agent checks cross-boundary consistency
```

---

# 6. Workflow Selection

The workflow is determined by task type, not by agent:

```text
Task Type                     Workflow
────────────────────────────  ─────────────────────────────────────
New feature                   development/feature.md
Bug fix                       development/bug-fix.md
Refactor                      development/refactor.md
Gameplay rule change          development/gameplay-change.md
Architecture change           architecture/architecture-change.md
ADR creation/update           architecture/adr-change.md
Documentation change          documentation/documentation-change.md
```

Every workflow uses `core/` steps and `quality/` steps as defined in
`.ai/workflow/README.md` §3. Agents execute workflows — they do not
choose to skip workflow steps.
