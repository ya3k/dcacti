# core/context-discovery.md — Context Discovery & Conflict Check

**Version:** 1.0

> Purpose: identify and read the documentation actually relevant to the
> classified task (`core/task-intake.md`), then check what was read for
> conflicts, before any plan is written.

---

# 1. Discovery Principle

```text
Task (classified)
 ↓
Identify domain(s)                  (from task-intake.md output)
 ↓
Identify authoritative documents      (per AGENTS.md §6 / .ai/README.md §7 —
                                       derive from current docs/ structure,
                                       do not rely on a memorized fixed list)
 ↓
Read the relevant documents
 ↓
Identify cross-document dependencies    (what those docs themselves
                                         reference)
 ↓
Inspect relevant existing implementation
```

Do not read the entire repository. Do not skip a document the task's
domain(s) point to. Both are failures of this step.

---

# 2. Illustrative Mappings

These mirror `AGENTS.md` §6 and are restated here only as workflow-level
examples — `AGENTS.md` §6 remains the authoritative mapping if the two ever
appear to differ (report per §3 below if they do).

```text
Match-3 task           GAME_RULES.md, MATCH3_RULES.md, GAME_STATE.md,
                         GAME_EVENTS.md (+ COMBAT_RULES.md, ELEMENT_RULES.md
                         if damage is touched)
Backend/API task          TDD.md, ARCHITECTURE.md, API_CONTRACTS.md,
                          GAME_STATE.md, DATABASE.md, relevant domain rules
Realtime/SignalR task        SIGNALR_PROTOCOL.md, GAME_EVENTS.md,
                             GAME_STATE.md, ARCHITECTURE.md
Persistence/database task      DATABASE.md, ADR-006, relevant domain rules
                               for what's being persisted
Architecture-adjacent task       TDD.md, ARCHITECTURE.md, relevant ADR(s)
```

---

# 3. Conflict Check

After reading, check what was read for:

```text
conflicting rules (two docs define the same concept differently)
missing rules (the task needs a rule no document states)
contradictory architecture (implementation implied by one doc conflicts
  with another)
inconsistent contracts (API/state/event/Redis/database disagree)
stale assumptions (an ADR or doc references something that no longer
  matches another doc)
unclear behavior (the docs are silent on the specific case the task needs)
```

If any of these is found:

```text
STOP
```

Do not silently choose an interpretation. Produce this report and halt the
workflow at this step:

```text
Problem:
...

Sources:
...

Conflict:
...

Impact:
...

Suggested resolution:
...
```

This is the same shape as the STOP CONDITION format in `.ai/README.md`
§13 — use that exact format, not a variant.

---

# 4. Output of This Step

If no conflict is found: a list of documents read, the specific
sections/rules relied on, and any dependencies/existing implementation
notes — this feeds `core/planning.md`.

If a conflict is found: the STOP report above, and the workflow does not
proceed to planning until the conflict is resolved by a human decision.
