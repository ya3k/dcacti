# TASK-XXX — <Title>

<!--
  INSTRUCTIONS (delete this block before saving)

  1. Replace TASK-XXX with the next sequential ID (highest existing + 1).
  2. Replace <Title> with a short imperative title (e.g. "Implement
     Match-3 Cascade Resolution").
  3. Fill in every section. Mark optional sections "N/A" rather than
     deleting them — this prevents accidental omission.
  4. Do NOT copy game rules, formulas, API schemas, or database
     schemas into this file. Reference docs/ by path + section only.
  5. Save to tasks/backlog/<TASK-NNN-short-title>.md with Status: BACKLOG.
  6. Change Status to READY only after the READY checklist in
     TASK_LIFECYCLE.md §3 is satisfied.
-->

---

## Metadata

```text
Task ID:           TASK-XXX
Type:              FEATURE | BUG | GAMEPLAY-CHANGE | REFACTOR |
                   ARCHITECTURE | DOCUMENTATION
Status:            BACKLOG | READY | IN PROGRESS | IN REVIEW |
                   BLOCKED | DONE
Risk:              LOW | MEDIUM | HIGH
Priority:          LOW | MEDIUM | HIGH | CRITICAL
Primary Agent:     orchestrator | gameplay | backend | client |
                   realtime | persistence | testing | review
Supporting Agents: <list or N/A>
Workflow:          development/feature.md | development/bug-fix.md |
                   development/gameplay-change.md |
                   development/refactor.md |
                   architecture/architecture-change.md |
                   documentation/documentation-change.md
Skills:            <list of .ai/skills/ skills needed — from
                   .ai/skills/README.md §5 selection guide>
Dependencies:      <TASK-NNN or N/A>
```

---

## Objective

<!--
  One paragraph. What must be accomplished. Be specific enough that an
  agent can confirm when it is done.
-->

<Describe the work to be done — imperative, outcome-focused.>

---

## Context

<!--
  Why this task exists. What triggered it. What problem it solves.
  Do NOT paste game rules here — reference them by doc + section.
-->

<Why this work is needed.>

---

## Authoritative Sources

<!--
  List the specific docs/ files and sections the agent MUST read before
  starting. Use AGENTS.md §6 to derive the correct set for the domain.
  Do NOT copy their content here.
-->

- `docs/00-overview/MVP_SCOPE.md` §1 — confirm feature is IN scope
- `docs/01-game-design/GAME_RULES.md` §<N> — <reason>
- `docs/01-game-design/<DOMAIN>_RULES.md` §<N> — <reason>
- `docs/02-technical/GAME_STATE.md` — <reason>
- `docs/02-technical/GAME_EVENTS.md` — <reason>
- `docs/02-technical/ARCHITECTURE.md` §<N> — <reason>
- `docs/03-decisions/ADR-<NNN>*` — <reason>

<!--
  Remove lines that do not apply. Add lines for any other docs the
  specific domain requires.
-->

---

## Scope

### In Scope

<!--
  What is explicitly included in this task. Be narrow and precise.
-->

- <item>

### Out of Scope

<!--
  What is explicitly excluded. Call out adjacent areas the agent
  might be tempted to touch.
-->

- <item>
- Any system listed in `docs/00-overview/MVP_SCOPE.md` §2 (OUT)

---

## Current State

<!--
  What exists right now. What is the starting condition.
  Reference existing code by path if useful.
-->

<Describe the current state of the relevant code/docs.>

---

## Acceptance Criteria

<!--
  Testable, binary conditions. Each criterion must be verifiable
  by the Testing Agent from authoritative docs.

  Format: "Given X, when Y, then Z." or "The system must X."

  Do NOT write acceptance criteria that require game values not in
  docs/ — reference the owning doc + section instead.
-->

- [ ] <Criterion 1 — specific and testable>
- [ ] <Criterion 2>
- [ ] All relevant tests pass at the depth required by
      `core/validation.md §2` for the task's Risk level
- [ ] `quality/review.md §1` checklist passes
- [ ] Documentation impact addressed (§ Documentation Impact below)

---

## Affected Areas

<!--
  Check every area the task touches. Used by the agent for impact
  analysis (core/planning.md §3) and by the Review Agent.
-->

```text
[ ] Domain (GameServer.Domain/) — which module(s)?
[ ] Application (GameServer.Application/)
[ ] API (GameServer.Api/)
[ ] Client (client/)
[ ] SignalR / Redis (GameServer.Infrastructure/SignalR/, /Redis/)
[ ] PostgreSQL (GameServer.Infrastructure/Postgres/)
[ ] Tests (tests/)
[ ] Documentation (docs/)
[ ] ADR (docs/03-decisions/ADR/)
```

---

## Implementation Notes

<!--
  Task-specific implementation guidance ONLY.

  Rules:
  - Do NOT restate game rules from docs/. Reference them.
  - Do NOT copy API/state/event contracts. Reference them.
  - Do NOT specify how a workflow or skill works. Reference them.
  - Use this section for things specific to THIS task that an agent
    would not find simply by reading the relevant docs.

  Examples of appropriate content:
  - "Note that the existing PassiveTracker expects X interface — see
    src/GameServer.Domain/Pets/PassiveTracker.cs"
  - "The cascade loop has a pre-existing implementation at X; this
    task only adds the special-gem-spawning step"
  - "ADR-005 already covers the Redis key pattern — use it"
-->

<Task-specific notes. Reference docs by path+section, never copy them.>

---

## Testing Requirements

<!--
  Specify what must be tested at what depth, derived from the task's
  Risk level (core/validation.md §2). Do NOT hard-code expected values
  — reference the authoritative doc + section instead.

  Required test types per risk:
  LOW:    unit tests + focused review
  MEDIUM: + integration tests + documentation validation
  HIGH:   + gameplay scenarios (Given/When/Then full state-transition
            chain, quality/testing.md §3) + architecture validation
-->

### Test Types Required

```text
[ ] Unit tests         — <which components/functions>
[ ] Integration tests  — <which boundaries>
[ ] Gameplay scenarios — <which state-transition chain, per
                          GAME_RULES.md §17 order where applicable>
[ ] API tests          — <which endpoints, per API_CONTRACTS.md>
[ ] Realtime tests     — <which Hub methods/events, per
                          SIGNALR_PROTOCOL.md>
[ ] Persistence tests  — <which schema constraints, per DATABASE.md>
```

### Key Edge Cases

<!--
  List edge cases explicitly called out in the domain rule docs.
  Reference doc + section. Do NOT invent edge cases.
  Examples: MATCH3_RULES.md §5.3 (Match-6+),
            RELIC_RULES.md §5 (anti-infinite-chain).
-->

- See `docs/01-game-design/<DOMAIN>_RULES.md` §<N> — <edge case name>

---

## Documentation Impact

<!--
  One of the following options. Justify the choice.
-->

**Option A — None:**
> This task implements already-documented behavior. No doc changes required.

**Option B — Update existing doc:**
> `docs/<path>` §<N> must be updated because <reason>.
> Use `documentation/documentation-change.md`.

**Option C — Create documentation:**
> A new document is required at `docs/<path>` because <reason>.

**Option D — ADR required:**
> A new or updated ADR is required because <reason>.
> Use `architecture/adr-change.md`. Assign next sequential ADR number.

<!--
  Delete the options that do not apply. Keep exactly one.
-->

---

## Stop Conditions

<!--
  Conditions specific to this task that will block execution.
  Universal stop conditions (AGENTS.md §20) always apply in addition.

  Use .ai/README.md §13's STOP CONDITION format when one fires:

  STOP CONDITION
  Problem: <one or two sentences>
  Relevant sources: <file paths + sections>
  Conflict / missing information: <what exactly is unresolved>
  Proposed resolution: <smallest change that would resolve it>
  Waiting for: <what kind of approval/input is needed>
-->

- If the required behavior cannot be fully derived from the
  Authoritative Sources listed above: STOP per `AGENTS.md §7`
- <Task-specific condition>

---

## Dependencies

<!--
  Other tasks that must be DONE before this task can be READY.
  List TASK-NNN IDs or "None".
-->

- None

---

## Completion Evidence

<!--
  TO BE FILLED BY THE AGENT after the task reaches DONE.
  Maps to core/completion.md §2 / .ai/README.md §21.
  Do not pre-fill this section.
-->

### Summary
<What was accomplished.>

### Changes
<Files modified/created/deleted.>

### Tests
<Tests added or updated, and their results.>

### Documentation Consulted
<Docs read during execution, with sections.>

### Documentation Changed
<Docs updated as part of this task, or "None".>

### Validation
<What was validated and at what depth (core/validation.md).>

### Risks
<Known risks or unresolved issues remaining.>

### Remaining Issues
<Issues discovered but not in scope of this task, per AGENTS.md §16.>

### Agent
<Which agent(s) executed the task.>

### Workflow Used
<Which workflow was used.>

### Skills Used
<Which skills were invoked.>

### Status
DONE | BLOCKED

---

## Handoff

<!--
  TO BE FILLED BY THE AGENT if the task is handed off mid-execution
  or if the next agent needs specific context.
-->

<What was completed, what remains, known risks, validation status.>
