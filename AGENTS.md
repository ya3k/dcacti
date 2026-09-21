# AGENTS.md

**Version:** 1.0
**Status:** Binding
**Scope:** Every AI agent (design, code, docs, review) working in this
repository.

> This file is the **behavior contract** for AI agents. It does not
> restate the project's design or technical content — it tells an agent
> where to find that content, how to use it, and when to stop. If anything
> below appears to conflict with `docs/`, `docs/` wins; report the
> conflict per §4 rather than trusting this file's wording over it.

---

# 1. Purpose

This file answers:

```text
What must AI read before coding?
Which documents are authoritative?
How should AI handle conflicts?
How should AI plan work?
When must AI stop?
What can AI change?
What must AI never invent?
How should AI test changes?
How should AI update documentation?
How should AI handle game-rule changes?
```

It is a contract, not a copy of `docs/`. If you find yourself pasting a
game rule, a formula, or an architecture diagram into this file to "make it
clearer," stop — that content belongs in its owning document, referenced
from here.

---

# 2. Source-of-Truth Hierarchy

```text
GAME DESIGN            docs/00-overview/, docs/01-game-design/
        ↓
TECHNICAL DESIGN         docs/02-technical/
        ↓
ARCHITECTURAL DECISIONS    docs/03-decisions/ADR/
        ↓
AI AGENTS / SKILLS / WORKFLOW   .ai/agents/, .ai/skills/, .ai/workflow/
        ↓
TASKS
        ↓
CODE
        ↓
TESTS
```

**Conflict precedence (most specific governs):**

```text
Specific Domain Rule  >  GAME_RULES.md  >  GDD.md
  >  TDD.md / ARCHITECTURE.md / other technical docs  >  ADR
  >  Task  >  Code
```

Within game design, the more specific domain document owns the detailed
rule:

```text
docs/01-game-design/GAME_RULES.md
        ↓
docs/01-game-design/MATCH3_RULES.md (or ELEMENT_/COMBAT_/PASSIVE_/PET_/
                                       CARD_/RELIC_/BOSS_RULES.md)
        ↓
implementation
```

Within technical design:

```text
docs/02-technical/TDD.md
        ↓
docs/02-technical/ARCHITECTURE.md
        ↓
docs/02-technical/GAME_STATE.md / GAME_EVENTS.md / API_CONTRACTS.md /
  SIGNALR_PROTOCOL.md / REDIS_STATE.md / DATABASE.md
        ↓
implementation
```

`docs/03-decisions/ADR/` explains **why** a technical choice was made. It
never overrides the **what**/**how** owned by `docs/02-technical/` — see
`docs/03-decisions/README.md` §3.

---

# 3. Documentation Map (Reference Only)

```text
docs/00-overview/    GDD.md, MVP_SCOPE.md, ROADMAP.md
docs/01-game-design/ GAME_RULES.md + 8 domain rule docs
docs/02-technical/   TDD.md, ARCHITECTURE.md, GAME_STATE.md, GAME_EVENTS.md,
                      API_CONTRACTS.md, SIGNALR_PROTOCOL.md, REDIS_STATE.md,
                      DATABASE.md
docs/03-decisions/   README.md (ADR index), ADR/ (9 ADRs)
```

Do not assume any content beyond what these documents actually say. If a
detail isn't there, it's missing information (§21), not something to infer.

---

# 4. Conflict Resolution

AI must never silently resolve a documentation conflict, and must never
pick whichever rule is easier to implement.

```text
1. Detect the conflict
2. Identify both sources (file + section)
3. Explain the conflict in plain terms
4. Determine which document SHOULD own the decision, per §2
5. Propose the smallest correction
6. STOP — do not change behavior until a human approves the correction
```

---

# 5. Documentation-First Development

```text
Task
 ↓
Identify affected domain(s)
 ↓
Read relevant Game Rules (docs/01-game-design/)
 ↓
Read relevant Technical Docs (docs/02-technical/)
 ↓
Read relevant ADRs (docs/03-decisions/ADR/)
 ↓
Understand existing implementation
 ↓
Plan
 ↓
Implement
```

Never start coding directly from a vague request. If the task is
underspecified relative to the docs, resolve that first (§21).

---

# 6. Required Reading by Task Type

Read only what's relevant — not the whole `docs/` tree for every task.

```text
Task touches...          Read
------------------------ --------------------------------------------------
Match-3 board/swap/       GAME_RULES.md, MATCH3_RULES.md, GAME_STATE.md,
  cascade                  GAME_EVENTS.md

Combat/damage/Elements    GAME_RULES.md, COMBAT_RULES.md, ELEMENT_RULES.md,
                            GAME_STATE.md, GAME_EVENTS.md

Pet/Passive               GAME_RULES.md, PET_RULES.md, PASSIVE_RULES.md,
                            GAME_STATE.md

Card/Relic                GAME_RULES.md, CARD_RULES.md, RELIC_RULES.md,
                            GAME_EVENTS.md

Boss                       GAME_RULES.md, BOSS_RULES.md, COMBAT_RULES.md,
                            GAME_EVENTS.md

Backend structure/layers    TDD.md, ARCHITECTURE.md, relevant ADR(s)

Realtime / connection         SIGNALR_PROTOCOL.md, GAME_EVENTS.md,
                               ADR-004, ADR-008

Active battle state storage    REDIS_STATE.md, GAME_STATE.md, ADR-005

Persistent data / schema        DATABASE.md, ADR-006

REST endpoints                   API_CONTRACTS.md, GAME_STATE.md,
                                   GAME_EVENTS.md, relevant domain rules

Whether something is in scope     MVP_SCOPE.md (always check this first if
                                    the task adds any new system/content)
```

---

# 7. Game Rule Protection

Game rules are authoritative. AI must NOT invent new gameplay mechanics,
rules, Pets, Bosses, Cards, Relics, currencies, systems, elemental
interactions, or progression mechanics — unless the task explicitly
requests a design change.

If implementation requires a rule that doesn't exist yet:

```text
STOP
 ↓
Identify the missing rule precisely (what question can't be answered)
 ↓
Report it
 ↓
Propose a design change (smallest possible)
 ↓
Wait for approval — do not implement a guessed version in the meantime
```

---

# 8. MVP Protection

Authoritative scope: `docs/00-overview/MVP_SCOPE.md` (IN / OUT / FUTURE).
Check it before implementing anything that could be a new system or
content, not just before implementing something that "sounds big."

Do not introduce PvP, Gacha, Guild, Trading, Map mechanics, Terrain,
Weather, Microservices, Kubernetes, Kafka, or any other system
`MVP_SCOPE.md` §2 lists as OUT, or anything not listed anywhere in
`MVP_SCOPE.md` §1 (treat unlisted as FUTURE, not IN — see
`MVP_SCOPE.md` §4).

If a task appears to require an out-of-scope system: **stop and report
it** rather than implementing a scoped-down version.

---

# 9. Anti-Overengineering

Prefer simple, explicit, deterministic, testable, maintainable code over
generic, abstract, framework-heavy, speculative, or "future-proof" code.

Do not add an abstraction merely because it might be useful later. Do not
create unnecessary interfaces, factories, event buses, generic
repositories, modules, or infrastructure unless the existing architecture
(`ARCHITECTURE.md`) or the task itself actually requires it. See
`ARCHITECTURE.md` §5 for this project's own anti-overengineering notes —
follow the same standard for anything new.

---

# 10. Server-Authoritative Rule

The battle server is authoritative (`GAME_RULES.md` §18, ADR-001). The
client may only **request** actions (e.g. Swap, Card Cast, Pet Skill
Cast). The client must never authoritatively determine Damage, HP, Boss
HP, Power, Match results, Combo, Passive progression, Rewards, RNG
results, or final battle state. The server determines authoritative
state; the client renders the resulting events (`GAME_EVENTS.md`,
`SIGNALR_PROTOCOL.md`).

Any code that computes one of the above values on the client for anything
beyond optimistic, discardable prediction is a defect — report it, don't
ship it.

---

# 11. Determinism & RNG

Game logic must be deterministic wherever the game design requires it
(e.g. `GAME_RULES.md` §17's fixed resolution order). Where randomness is
involved (e.g. Gem spawns, `MATCH3_RULES.md` §7):

- Follow the documented RNG strategy (`TDD.md` §6 — server-seeded, part of
  Active Battle State).
- Never use uncontrolled client-side randomness for anything
  authoritative.
- Preserve reproducibility where the docs require it (e.g. so a recovered
  session behaves consistently).
- Do not introduce a second randomization mechanism alongside the
  documented one.

If a task seems to need RNG behavior not covered by existing docs: stop
and report per §7, don't invent one.

---

# 12. Domain Boundaries

```text
Match-3   → board / swap / match / cascade / special gems
Element    → elemental relationship and modifiers
Combat     → damage / HP / ATK / DEF / Power / status
Passive    → passive charge / trigger / reset
Pet         → pet identity / progression / stats
Card         → card behavior and resolution
Relic         → passive build modifiers and triggers
Boss           → boss mechanics
```

Each maps to its own domain rule document (§3) and its own module in
`ARCHITECTURE.md` §1/§3. Do not place logic in the wrong domain module
just because it's convenient at the call site — e.g. a Boss-reactive
effect belongs in the Boss module even if it's easiest to bolt onto the
Combat module mid-task.

---

# 13. Technical Boundaries

```text
Backend      → authoritative game logic (ARCHITECTURE.md: Domain/
                Application layers)
Frontend      → presentation / interaction / rendering
Phaser         → game rendering and client-side battle presentation (ADR-003)
React           → application UI (menus, collections)
PostgreSQL       → persistent data (ADR-006, DATABASE.md)
Redis             → active battle state only (ADR-005, REDIS_STATE.md)
SignalR             → realtime communication (ADR-004, SIGNALR_PROTOCOL.md)
```

Do not move a responsibility across one of these boundaries (e.g. writing
active battle state to PostgreSQL, or computing damage in the client)
without a documented architectural reason — which means proposing/updating
an ADR first (§17), not doing it inline in a task.

---

# 14. Code Change Process

For every non-trivial task:

```text
1. Understand
2. Plan
3. Implement
4. Test
5. Review
6. Update documentation if necessary
```

Before modifying code: locate the existing implementation, understand its
dependencies, inspect related tests, check relevant docs (§6), and
identify which domain/technical boundaries (§12, §13) it touches.

Do not rewrite existing code unnecessarily. Prefer the smallest correct
change.

---

# 15. Testing Requirements

Every gameplay behavior must be testable:

```text
Rule (docs/01-game-design/...)
 ↓
Scenario (Given/When/Then, derived from the rule)
 ↓
Automated test
```

When implementing a game mechanic:

1. Find the relevant scenario, or write one if it doesn't exist.
2. Implement the behavior.
3. Run relevant tests.
4. Verify edge cases called out in the owning domain rule document (e.g.
   `MATCH3_RULES.md` §5.3's Match-6+ note, `RELIC_RULES.md` §5's
   anti-infinite-chain rule).

Example scenario style:

```text
Given an 8x8 board
And a valid adjacent swap
When the player swaps two gems
Then the server validates the swap
And a valid match is created
And the turn begins
And the match is resolved
And cascade resolution continues until stable
```

Do not modify a test simply to make an incorrect implementation pass. If
implementation and test disagree, determine whether the implementation is
wrong, the test is outdated, or the documentation is inconsistent — and
handle it via §4 if it's a documentation conflict, not by editing
whichever is more convenient.

---

# 16. Task Discipline

Work only on the requested task.

```text
Task A
 ↓
implement A
 ↓
refactor B         ✗ not requested
 ↓
redesign C          ✗ not requested
 ↓
change architecture D   ✗ not requested
```

If unrelated problems are discovered while working, report them — don't
fix them inline:

```text
Report:
- issue
- location
- impact
- suggested follow-up task
```

---

# 17. Documentation Change Rule

If code changes behavior that existing docs describe, determine whether
the **code** is wrong or the **documentation** is outdated. Do not
silently change both to match each other without deciding which one was
actually correct.

If the design is intentionally changing:

```text
1. Update/approve the design
2. Update the source-of-truth documentation (owning domain/technical doc)
3. Update dependent technical documentation
4. Update tests
5. Implement code
```

Documentation must remain consistent with implementation when a task is
marked done (§23).

---

# 18. Architecture Change Rule

If implementation requires changing architecture, database strategy,
realtime strategy, the battle-state model, the authoritative model,
module boundaries, or major infrastructure:

1. Check existing ADRs (`docs/03-decisions/ADR/`) first — the change may
   already be covered, or may directly contradict one (handle via §4 if
   so).
2. If no ADR exists for the area being changed:
   ```text
   Propose ADR → Get approval → Update architecture docs → Implement
   ```
3. Never introduce an architectural decision silently inside a code
   change — if it's worth an ADR, it's worth writing one before the code
   lands.

---

# 19. AI Agent / Skill / Workflow Boundaries

*(`.ai/agents/`, `.ai/skills/`, `.ai/workflow/` do not exist yet in this
repository as of this document's creation — this section defines the
boundary for when they do.)*

```text
.ai/agents/     Must follow this file. Must NOT redefine repository-wide
                 rules already stated here.
.ai/skills/      Provide domain-specific working knowledge and references.
                 Must NOT become an alternative source of truth — a skill
                 that restates a game rule instead of linking to
                 GAME_RULES.md (or a domain rule doc) is a documentation
                 bug.
.ai/workflow/     Define processes (e.g. how to run a specific check).
                  Must not contradict §14 (Code Change Process) or §15
                  (Testing Requirements).
```

Full hierarchy including this layer:

```text
AGENTS.md
    ↓
docs/
    ↓
.ai/agents/ · .ai/skills/ · .ai/workflow/
    ↓
Tasks
    ↓
Code
```

---

# 20. When AI Must Stop

Stop and report — do not guess — when encountering:

```text
Rule conflict          Two authoritative documents disagree (§4)
Missing rule            Implementation needs a gameplay rule that isn't
                          documented (§7)
Architecture conflict     Implementation conflicts with an ADR or a
                          technical doc (§18)
Scope violation           Task requires an out-of-scope feature (§8)
Ambiguous requirement       Multiple materially different implementations
                            are all consistent with the docs
Data contract conflict        API / state / event / Redis / database
                              contracts disagree with each other
Destructive change              A change could cause data loss or break
                                existing behavior
```

---

# 21. Output Discipline

For implementation tasks, report:

```text
## Understanding
<what the task means>

## Relevant Documentation
- ...

## Plan
1. ...

## Changes
- ...

## Tests
- ...

## Documentation
- ...

## Remaining Issues
- ...
```

Keep it concise and factual. Do not pad with unrequested explanation or
unsolicited opinions on the design.

---

# 22. Definition of Done

```text
[ ] Requirement understood
[ ] Relevant docs checked (§6)
[ ] Scope checked (§8)
[ ] Existing implementation checked
[ ] Plan created
[ ] Code implemented
[ ] Relevant tests added/updated
[ ] Tests pass
[ ] No unrelated behavior changed (§16)
[ ] Documentation updated if required (§17)
[ ] No source-of-truth conflict introduced (§4)
```

---

# 23. Final Principle

The AI's job is **not**:

> "Make the project better according to its own opinion."

The AI's job is:

> "Implement the project's documented intent correctly."

```text
DOCUMENTED INTENT → UNDERSTAND → PLAN → IMPLEMENT → TEST → VERIFY
```

Never:

```text
REQUEST → AI ASSUMPTION → NEW DESIGN → CODE
```

If documented intent is missing or ambiguous, that is a §20 stop
condition — not license to design on the project's behalf.
