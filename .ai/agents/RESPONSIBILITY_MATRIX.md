# Responsibility Boundary Matrix

**Version:** 1.0

> This document answers: **"Which agent owns which concern, who supports
> it, and what document has authority?"** Its purpose is to prevent two
> agents from believing they own the same decision.

---

# 1. Ownership Matrix

| Concern | Owner | Supporting Agent(s) | Authoritative Source |
| --- | --- | --- | --- |
| Match-3 logic (board, swap, match, cascade, special gems) | Gameplay | Backend (sequencing) | GAME_RULES.md, MATCH3_RULES.md |
| Element resolution | Gameplay | Backend (damage pipeline) | ELEMENT_RULES.md |
| Combat / damage pipeline | Gameplay | Backend (sequencing) | COMBAT_RULES.md |
| Passive charge / trigger / reset | Gameplay | Backend (sequencing) | PASSIVE_RULES.md |
| Pet identity / stats / progression | Gameplay | Persistence (storage) | PET_RULES.md |
| Card behavior / resolution | Gameplay | Backend (use case) | CARD_RULES.md |
| Relic triggers / evaluation | Gameplay | Backend (sequencing) | RELIC_RULES.md |
| Boss mechanics / AI | Gameplay | Backend (sequencing) | BOSS_RULES.md |
| Event resolution order (§17 pipeline) | Backend | Gameplay (domain calls) | GAME_RULES.md §17 |
| Application use cases (StartBattle, ResolveSwap, etc.) | Backend | Gameplay (domain logic) | ARCHITECTURE.md §1–§3 |
| REST API endpoints | Backend | Client (consumer) | API_CONTRACTS.md |
| Clean Architecture layering | Backend | Review (verification) | ARCHITECTURE.md §2, ADR-002 |
| Server authority enforcement | Backend | Gameplay, Client, Realtime | GAME_RULES.md §18, ADR-001 |
| SignalR hub / protocol | Realtime | Backend (caller), Client (consumer) | SIGNALR_PROTOCOL.md, ADR-004 |
| Battle event delivery / ordering | Realtime | Backend (producer) | GAME_EVENTS.md, SIGNALR_PROTOCOL.md |
| Redis active battle state | Realtime | Backend (writer via Application) | REDIS_STATE.md, ADR-005 |
| Reconnection / resynchronization | Realtime | Backend (state recovery) | ADR-008, REDIS_STATE.md |
| PostgreSQL schema / mappings | Persistence | Backend (consumer) | DATABASE.md, ADR-006 |
| EF Core repositories / migrations | Persistence | Backend (consumer) | DATABASE.md |
| Battle result persistence | Persistence | Backend (trigger) | DATABASE.md |
| Player / collection data persistence | Persistence | Backend (use cases) | DATABASE.md |
| Board rendering / Phaser scenes | Client | — | ARCHITECTURE.md §1 (client/) |
| React UI (menus, collections) | Client | Backend (API consumer) | ARCHITECTURE.md §1 (client/) |
| Client-side SignalR consumption | Client | Realtime (protocol) | SIGNALR_PROTOCOL.md |
| Client presentation state | Client | — | ARCHITECTURE.md §1 (client/state/) |
| Unit tests | Testing | (domain-relevant agent) | quality/testing.md |
| Integration tests | Testing | (domain-relevant agent) | quality/testing.md |
| Gameplay scenario tests | Testing | Gameplay | quality/testing.md |
| API / realtime / persistence tests | Testing | Backend, Realtime, Persistence | quality/testing.md |
| Deterministic verification | Testing | Gameplay | TDD.md §6 |
| Documentation consistency | Review | (all agents) | docs/ (all) |
| Architecture review | Review | Backend | ARCHITECTURE.md, ADRs |
| ADR impact detection | Review | Backend | docs/03-decisions/ |
| Implementation correctness review | Review | (domain-relevant agent) | quality/review.md |
| Documentation impact analysis | Review | (domain-relevant agent) | documentation/documentation-change.md |
| Task intake / classification | Orchestrator | — | core/task-intake.md |
| Context discovery / conflict detection | Orchestrator | — | core/context-discovery.md |
| Workflow selection | Orchestrator | — | .ai/workflow/README.md |
| Agent coordination / handoff | Orchestrator | — | this document |
| Completion verification | Orchestrator | — | core/completion.md |

---

# 2. Boundary Rules

## 2.1 No Dual Ownership

Every concern has exactly one owning agent. The "Supporting Agent(s)"
column indicates agents that contribute to or consume the concern, but
they do not own it and cannot unilaterally change it.

## 2.2 Authority Always Traces to Documentation

The "Authoritative Source" column shows which document governs the
concern. The owning agent implements or analyzes based on that source —
it does not originate rules independently.

## 2.3 Cross-Boundary Changes

When a task requires changing something across multiple ownership
boundaries (e.g. adding a new event type touches Gameplay, Backend,
Realtime, and Client), the Orchestrator Agent coordinates, and each
agent handles only its owned portion.

## 2.4 Escalation

If an agent discovers that the correct fix or implementation requires
changing something outside its ownership boundary, it must report the
cross-boundary impact and hand off to the owning agent — not make the
change itself.

---

# 3. Server Authority Ownership

The following are server-authoritative values. The Backend Agent
(through domain logic owned by the Gameplay Agent) is the sole
calculator. No other agent may make the client authoritative for these:

```text
Damage
HP / Boss HP
Power
Match results
Combo
Passive progression
Rewards
Battle outcome
RNG results
```

Per `AGENTS.md` §10, `.ai/README.md` §15, ADR-001.
