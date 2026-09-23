# TASK-XXX — <Title>

<!--
  GEN-TASK EXECUTION MANIFEST TEMPLATE
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.

  Instructions:
  1. Replace TASK-XXX with the next sequential ID (highest existing + 1).
  2. Replace <Title> with a short imperative title (e.g. "Implement BoardView Gem Swap Presentation").
  3. Reference docs/ by path and section. Do NOT copy game rules, formulas, or schemas.
  4. Select skills within budget (Simple: 2-4, Normal: 3-5, Complex: 5-7). Decompose if > 7.
  5. Save to tasks/backlog/<TASK-NNN-short-title>.md with Status: BACKLOG.
-->

---

## Metadata

```text
Task ID:           TASK-XXX
Type:              FEATURE | BUG | GAMEPLAY-CHANGE | REFACTOR | ARCHITECTURE | DOCUMENTATION
Status:            BACKLOG | READY | IN PROGRESS | IN REVIEW | BLOCKED | DONE
Risk:              LOW | MEDIUM | HIGH
Priority:          LOW | MEDIUM | HIGH | CRITICAL
Primary Agent:     orchestrator | gameplay | backend | client | realtime | persistence | testing | review
Supporting Agents: <list or N/A>
Workflow:          development/feature.md | development/bug-fix.md | development/gameplay-change.md | development/refactor.md | architecture/architecture-change.md | documentation/documentation-change.md
Skills:            <list of 2-7 skills from .ai/skills/SKILL_REGISTRY.md within skill count budget>
Dependencies:      <TASK-NNN or None>
```

---

## Objective

<One concise paragraph describing the required outcome — imperative, testable, and focused on what must be built or fixed.>

---

## Authoritative References

<!--
  List authoritative docs and sections from docs/ that govern this task.
  Reference docs by path and section — do NOT copy their contents here.
-->

- `docs/00-overview/MVP_SCOPE.md` §1 — Confirm feature is in MVP scope
- `docs/01-game-design/<DOMAIN>_RULES.md` §<N> — <Specific rule / formula reference>
- `docs/02-technical/<DOC>.md` §<N> — <Technical contract / wire schema / state reference>
- `docs/03-decisions/ADR/<ADR-NNN>*` — <Architectural rationale if applicable>

---

## Scope

### In Scope
- <Specific deliverable or behavior 1>
- <Specific deliverable or behavior 2>

### Out of Scope
- <Explicitly excluded adjacent area or server logic>
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2

---

## Current State

<Brief 1-2 sentence description of existing implementation, file locations, or starting conditions.>

---

## Acceptance Criteria

<!--
  Binary, testable verification conditions. Derived from authoritative docs.
-->

- [ ] <Criterion 1 — specific, binary, testable>
- [ ] <Criterion 2 — specific, binary, testable>
- [ ] All relevant tests pass at the required validation depth (`core/validation.md` §2)
- [ ] Quality review checklist passes (`quality/review.md` §1)
- [ ] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001)

---

## Affected Files & Areas

```text
[ ] src/backend/ (Domain / Application / Infrastructure / Api)
[ ] src/frontend/client/ (scenes / runtime / services / state / ui)
[ ] tests/ (unit / integration / gameplay scenarios)
[ ] docs/ (documentation updates if applicable)
```

---

## Implementation Notes

<!--
  TASK-SPECIFIC implementation guidance only (e.g. existing method names, file locations).
  Do NOT restate game rules, API payloads, or skill procedures.
-->

<Task-specific pointers or file references. Otherwise "None".>

---

## Testing Requirements

### Required Verification
```text
[ ] Unit tests         — <components / functions to test>
[ ] Integration tests  — <boundaries to test>
[ ] Gameplay scenarios — <Given/When/Then scenarios derived from rule docs>
```

### Key Edge Cases
- See `docs/01-game-design/<DOMAIN>_RULES.md` §<N> — <edge case reference>

---

## Stop Conditions

<!--
  Universal stop conditions in AGENTS.md §20 always apply.
  List any task-specific blocking conditions below.
-->

- If required behavior cannot be fully derived from Authoritative References: STOP per `AGENTS.md` §7
- If task requires client-authoritative game logic calculation: STOP per `AGENTS.md` §10
- If task exceeds 7 skills or crosses multiple uncoupled architectural boundaries: STOP & decompose

---

## Completion Evidence

<!--
  TO BE COMPLETED BY THE EXECUTING AGENT after reaching DONE.
  Keep concise and factual.
-->

### Changed Files
- `<file path>` — <summary of change>

### Validation Results
- `<test command or suite>` — PASS (<N> tests)

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
