# .ai/skills/README.md — Skill Layer

**Version:** 1.0
**Status:** Binding
**Scope:** Every skill file under `.ai/skills/`.

> This document answers: **"How is a reusable capability performed by an AI
> in this repository?"** It does not answer "what are the game rules"
> (`docs/01-game-design/`), "how does the system work" (`docs/02-technical/`),
> "why was it built this way" (`docs/03-decisions/ADR/`), or "when does this
> happen" (`.ai/workflow/`). Skills read those sources; they never become one.

---

# 1. What Is a Skill?

A skill is a **focused, reusable procedure** for one capability, invoked by an
agent while it executes a workflow step.

A skill answers:

```text
What capability does this provide?      → Purpose
When is it used / not used?             → When to Use / When Not to Use
What does it need?                      → Inputs, Prerequisites & Required Context
Which documents govern it?              → Authoritative Sources
What does it do?                        → Procedure
What does it produce?                   → Outputs
How does it check itself?               → Validation
When must it stop?                      → Stop Conditions
```

Each skill has exactly **one** primary capability and is composable with
others. A skill is a *procedure for finding and applying* the truth, never
the truth itself.

---

# 2. What a Skill Is NOT

```text
Not a source of truth     → docs/ owns every rule, contract, and decision
Not a workflow            → workflows own sequence and gating
Not an agent              → agents own a role; skills are capabilities they use
Not a task                → tasks say WHAT to do; skills say HOW a capability works
Not a tool wrapper        → "read a file", "run a command" are agent/tool
                            basics, not project skills
Not a checklist of rules  → if a skill lists game values, formulas, or
                            contract fields, it is a documentation bug
```

The layering (from `AGENTS.md` §2 / §19, `.ai/README.md` §1):

```text
Documentation :  "What is true?"
Workflow      :  "When, and in what order, does a capability happen?"
Skill         :  "How does the AI determine or apply what is true?"
Agent         :  "Who performs the work?"
```

Example: `COMBAT_RULES.md` owns the damage pipeline. A skill only says *how to
read that document, locate the ordered steps, map them to state and events,
and report gaps* — it never restates the pipeline.

---

# 3. Relationship to Workflows

```text
Workflow ──selects──▶ Skill ──references──▶ Source-of-truth documentation
```

- A workflow step (e.g. `core/context-discovery.md` → "read the relevant
  docs, check conflicts") **calls** skills. The workflow controls sequence,
  gating, and depth (`core/validation.md`). The skill performs the capability.
- A skill never decides *whether* a phase runs, never skips a workflow gate,
  and never re-orders a workflow. It halts itself on a stop condition and the
  calling workflow halts with it (`.ai/workflow/README.md` §6).
- **Binding direction:** the "Used by" list in each skill and the mapping in
  §5 below are the traceability record. The existing workflow files do not
  yet name skills (workflows were created first and are not modified by this
  layer). Until a follow-up task adds references on the workflow side, this
  README §5 and each skill's `Traceability` section are the mapping of record.

---

# 4. Relationship to Agents

- An agent (`.ai/agents/`, not yet created) owns a role and lists the skills
  it may invoke (`.ai/README.md` §3).
- One agent may use many skills; one skill may be used by many agents.
  **No skill is bound to a single agent or role.**
- A skill contains no role language ("the backend agent must…"). It is
  written for whichever agent executes it.
- Agents must not re-describe a skill's procedure; they reference the skill.

---

# 5. Skill Catalog and Workflow Mapping

Skills were derived **from the workflows**, not from a category list: each
exists because at least two workflows (or one mandatory, load-bearing step)
need the same capability.

| Skill | Path | Capability |
| --- | --- | --- |
| documentation-discovery | `discovery/documentation-discovery.md` | Find, read, and bound the authoritative documents for a task |
| impact-analysis | `discovery/impact-analysis.md` | Trace a change along its real dependency chain |
| gameplay-behavior-derivation | `gameplay/gameplay-behavior-derivation.md` | Derive expected gameplay behavior (steps, state, events) from the rule docs |
| authority-determinism-audit | `gameplay/authority-determinism-audit.md` | Audit state ownership, server authority, RNG, ordering |
| api-contract-validation | `backend/api-contract-validation.md` | Validate REST behavior against `API_CONTRACTS.md` |
| persistence-analysis | `backend/persistence-analysis.md` | Analyze Redis/PostgreSQL usage against the storage docs |
| realtime-protocol-validation | `realtime/realtime-protocol-validation.md` | Validate SignalR/event delivery, ordering, resync |
| test-scenario-generation | `testing/test-scenario-generation.md` | Derive doc-sourced test scenarios (incl. regression) |
| scope-validation | `quality/scope-validation.md` | Check MVP scope and task boundary |
| documentation-consistency | `quality/documentation-consistency.md` | Detect and classify docs↔docs and docs↔code discrepancies |
| architecture-conformance | `quality/architecture-conformance.md` | Check layering, module ownership, ADR consistency |
| implementation-review | `quality/implementation-review.md` | Perform a structured review and emit severity-rated findings |

## Workflow → Skill mapping

Contract and gameplay skills are candidates selected by the task's domain
(see the selection guide below); the mapping lists every workflow where a
skill can legitimately be invoked. Depth still comes from `core/validation.md`.

```text
core/task-intake.md            scope-validation (when the task adds any system/content)
core/context-discovery.md      documentation-discovery, documentation-consistency
core/planning.md               impact-analysis, scope-validation
core/implementation.md         authority-determinism-audit (§4), architecture-conformance,
                               gameplay-behavior-derivation, scope-validation (§2 boundary),
                               documentation-discovery (if the domain changes, §3)
core/validation.md             architecture-conformance (the "architecture validation"
                               layer); depth selection itself uses no skill
core/completion.md             (none — reporting only)

development/feature.md         scope-validation, documentation-discovery, impact-analysis,
                               gameplay-behavior-derivation, authority-determinism-audit,
                               api-contract-validation, persistence-analysis,
                               realtime-protocol-validation, test-scenario-generation
                               (contract/gameplay skills as the feature category requires)
development/bug-fix.md         documentation-discovery, gameplay-behavior-derivation,
                               documentation-consistency, test-scenario-generation,
                               authority-determinism-audit, api-contract-validation,
                               realtime-protocol-validation, persistence-analysis
                               (as the bug's domain requires)
development/refactor.md        impact-analysis (must-not-change inventory),
                               gameplay-behavior-derivation, test-scenario-generation,
                               api-contract-validation, realtime-protocol-validation,
                               persistence-analysis (whichever contracts §2 lists apply)
development/gameplay-change.md scope-validation, documentation-discovery,
                               gameplay-behavior-derivation, impact-analysis,
                               documentation-consistency, authority-determinism-audit,
                               test-scenario-generation

architecture/architecture-change.md  documentation-discovery, architecture-conformance,
                                     impact-analysis, scope-validation,
                                     persistence-analysis (persistence-strategy changes)
architecture/adr-change.md           architecture-conformance, documentation-consistency
documentation/documentation-change.md  documentation-discovery (canonical owner),
                                       impact-analysis (documentation mode),
                                       documentation-consistency

quality/testing.md             test-scenario-generation, gameplay-behavior-derivation,
                               api-contract-validation, realtime-protocol-validation,
                               persistence-analysis (expected behavior for API /
                               realtime / persistence tests)
quality/review.md              implementation-review — which invokes, per checklist item:
                               gameplay-behavior-derivation, authority-determinism-audit,
                               api-contract-validation, realtime-protocol-validation,
                               persistence-analysis, architecture-conformance,
                               scope-validation, documentation-consistency,
                               test-scenario-generation
```

## Selection guide by task category

Task categories are the ones in `core/task-intake.md` §2. Depth still comes
from `core/validation.md` — this table only says which skills are *candidates*.

```text
GAMEPLAY               gameplay-behavior-derivation, authority-determinism-audit,
                       test-scenario-generation
BACKEND                api-contract-validation, persistence-analysis,
                       architecture-conformance, authority-determinism-audit
REALTIME               realtime-protocol-validation, authority-determinism-audit
DATABASE               persistence-analysis
FRONTEND               authority-determinism-audit (client must not own state),
                       realtime-protocol-validation (client side of the protocol)
TESTING                test-scenario-generation
BUG FIX / REFACTOR     see workflow mapping above
ARCHITECTURE CHANGE    architecture-conformance, impact-analysis
DOCUMENTATION          documentation-consistency, documentation-discovery
GAME DESIGN /
TECHNICAL DESIGN       documentation-discovery, documentation-consistency,
                       impact-analysis, scope-validation
(all categories)       documentation-discovery first; scope-validation if new
                       system/content
```

---

# 6. Baseline Stop Conditions and Report Format

Every skill inherits the stop conditions in `AGENTS.md` §20 and
`.ai/README.md` §13 and **restates only the ones that are specific to its
capability**. Baseline (applies to every skill, in addition to what its file
lists):

```text
Required documentation is missing
Two authoritative documents conflict
A required rule is missing or ambiguous
Architecture is unclear for the task
An API / state / event / Redis / database contract is unclear or inconsistent
The task exceeds documented (MVP) scope
A destructive or data-losing operation is uncertain
```

On stopping: **do not guess, do not invent, do not silently change the
source of truth.** Report using the exact format in `.ai/README.md` §13
(`STOP CONDITION` block: Problem / Relevant sources / Conflict or missing
information / Proposed resolution / Waiting for). Skills add, inside that
block and without altering its shape:

```text
Problem:                 name the skill that stopped and which step
Conflict / missing
information:             state the impact (what cannot be concluded or built)
```

Skills never invent a second report format (the same rule
`core/context-discovery.md` §3 applies to workflows).

---

# 7. How Skills Reference Documentation

1. **By path and section, never by value.** Write "read `COMBAT_RULES.md`
   §3 and use its current formula", never the formula.
2. **Derive the document list from the current `docs/` tree** using
   `AGENTS.md` §6 as the authoritative task→document mapping. A skill's
   `Authoritative Sources` section is the *minimum* starting set for its
   capability, not a frozen list; if the tree changed, re-derive.
3. **Precedence** is `AGENTS.md` §2 (specific domain rule > `GAME_RULES.md` >
   `GDD.md` > technical docs > ADR > task > code). ADRs explain *why* only
   (`docs/03-decisions/README.md` §3).
4. **Examples must be labelled.** A number or name that appears only to
   illustrate a procedure is marked `(illustrative — not authoritative)`.
   Prefer placeholders (`<value from COMBAT_RULES.md §x>`).
5. **Assumption-marked content stays assumption-marked.** Anything a document
   itself marks ASSUMPTION or "Open Item" (e.g. `TDD.md` §0,
   `docs/03-decisions/README.md` §8) must be reported as unconfirmed, never
   promoted to fact by a skill. A skill never asserts a specific runtime/framework
   version that the docs do not state.
6. **Reuse upstream context.** When the calling workflow already produced
   documentation context (`core/context-discovery.md` §4), a skill consumes
   it rather than re-running discovery.

---

# 8. How Skills Are Discovered

- By workflow: the mapping in §5 (and each skill's `Used by`).
- By agent: an agent definition lists the skills it may use (`.ai/README.md` §3).
- By task category: the selection guide in §5.
- By path: `.ai/skills/<category>/<skill>.md`. Categories group by *domain of
  the capability* (`discovery`, `gameplay`, `backend`, `realtime`, `testing`,
  `quality`), not by ownership. A category exists only while it holds a
  justified skill.

---

# 9. Skill File Contract

Every skill uses this structure (sections may not be dropped; a section that
genuinely has nothing to say states "None"):

```markdown
# skills/<category>/<name>.md — Skill: <Name>
**Version:** 1.0
> one-sentence purpose

## Purpose
## When to Use
## When Not to Use
## Inputs
## Prerequisites & Required Context
## Authoritative Sources
## Procedure          (numbered steps)
## Outputs
## Validation
## Stop Conditions
## Common Failure Modes
## Traceability       (Used by / Reads / Produces / Depends on)
```

This satisfies the input / output / prerequisites / source-of-truth /
validation / stop-condition requirements of `.ai/README.md` §4 and §9.

**Composition rules**

- Prefer passing outputs between skills over one skill invoking another.
- Composition depth is at most two levels, and the only skill allowed at the
  bottom of a chain is `documentation-discovery`.
- `implementation-review` is the only skill that fans out across many others,
  because review is the one workflow step (`quality/review.md`) that spans
  every checklist item.
- No skill may call a workflow, and no skill may bypass a workflow gate.

```text
documentation-discovery
        ↓
gameplay-behavior-derivation ──▶ authority-determinism-audit
        ↓
impact-analysis
        ↓ (implementation happens in core/implementation.md — not a skill)
test-scenario-generation
        ↓
implementation-review
```

---

# 10. Versioning and Change Policy

Skills evolve, but silently changing one changes how every workflow behaves.
Before changing a skill:

```text
1. List the workflows that use it (its `Used by` and §5 above).
2. Consider which agents will invoke it (once .ai/agents/ exists).
3. Decide whether the change alters AI PROCEDURE or PROJECT BEHAVIOR.
     Procedure change  → edit the skill, bump its Version.
     Behavior change   → the rule/contract belongs in docs/. Change the
                         owning document via the proper workflow instead
                         (documentation-change / gameplay-change /
                         architecture-change). Never encode a behavior
                         change only in a skill.
4. Check the skill's Authoritative Sources still exist and section
   references still resolve.
5. Update this README's catalog and mapping if the capability, inputs,
   outputs, or users changed.
6. Do not break a workflow silently: if an output shape changes, check the
   consumers listed in `Traceability`.
```

Version numbers: patch-level wording fix → `1.0.x`; procedure/output change →
minor bump; capability split/merge/removal → major bump and a README update.

Adding a skill requires: two or more workflow users (or one load-bearing
mandatory step), one primary capability, no overlap with an existing skill,
and an entry here. A proposed skill that is really a workflow step, a
document section, or a tool operation is rejected.

---

# 11. How Skills Are Validated

A skill is valid only if **all** of the following hold:

```text
[ ] Follows the §9 file contract; exactly one primary capability
[ ] Every Authoritative Source path exists in the current repository
[ ] Every cited section number/heading exists in the cited document
[ ] Contains no game value, formula, threshold, contract field list, or
    technology/version claim that has a canonical home in docs/
    (only labelled illustrative examples; quick check: search for digits and
    "=" / "x" multipliers in the skill and justify each hit)
[ ] Does not redefine a workflow's sequence or gate
[ ] Does not name or bind to a specific agent
[ ] Outputs are structured and name their consumer(s)
[ ] Stop conditions cover the baseline (§6) and the skill's own hazards,
    and use the .ai/README.md §13 format
[ ] Traceability matches the workflows that actually reference it
[ ] Does not create, require, or scaffold anything OUT of MVP scope
    (MVP_SCOPE.md §2) or FUTURE (§3)
```

Re-validate whenever a cited document is renamed, renumbered, or superseded.

---

# 12. Candidates Considered and Rejected

```text
match3-analysis, combat-analysis, passive/pet/boss analysis
    → folded into gameplay-behavior-derivation as domain lenses. The
      procedure is identical (locate rule → place in §17 order → map to
      state/events); only the source documents differ, and those are
      already routed by AGENTS.md §6.
domain-implementation
    → implementation is a workflow step (core/implementation.md); layer and
      module rules are checked by architecture-conformance. A "how to write
      code" skill would restate ARCHITECTURE.md.
regression-analysis
    → the bug-fix regression test is produced by test-scenario-generation;
      the "what could break" question is impact-analysis's testing impact.
state-transition-validation (standalone)
    → part of gameplay-behavior-derivation and authority-determinism-audit.
task-classification / validation-depth selection / completion reporting
    → each is used by exactly one workflow file and is fully defined there.
ADR authoring
    → a process owned by architecture/adr-change.md; conformance checking is
      in architecture-conformance.
read-file / search / run-command / create-file
    → basic agent/tool capabilities, not project skills.
PvP, Gacha, Guild, Trading, Marketplace, Microservices, Kubernetes, Kafka,
Map/Terrain/Weather
    → OUT of MVP (MVP_SCOPE.md §2); no skill is created for them.
```

---

# 13. What This Layer Does Not Contain

No agents, no workflow changes, no game rules, no API/schema/event
definitions, no tasks, no code, no tests. Skills describe procedure only.
