# skills/quality/documentation-consistency.md — Skill: Documentation Consistency

**Version:** 1.0

> Detect and classify discrepancies between documents, and between documents
> and implementation or tests — and report them; never resolve them silently.

## Purpose

Perform the **conflict check** (`core/context-discovery.md` §3), the
**docs-vs-code classification** (`.ai/README.md` §18), and the **post-change
consistency validation** (`documentation/documentation-change.md` §1) as one
capability: compare claims, decide which document *should* own the truth,
classify the discrepancy, and propose the smallest correction — then stop for
a human decision where the rules require it.

## When to Use

- `core/context-discovery.md` §3, after documents are read.
- `development/bug-fix.md` §1–§3 (which side is wrong?).
- `development/gameplay-change.md` §3 (dependent docs after a design change).
- `architecture/adr-change.md` (ADR index/status coherence).
- `documentation/documentation-change.md` (validate no duplicate definition
  or stale reference was introduced).
- `quality/review.md` "Documentation".
- When implementation reveals a gap (`core/implementation.md` §3).

## When Not to Use

- To decide game design or architecture (that needs human approval:
  `GAME_RULES.md` §20, `AGENTS.md` §17–§18).
- To judge scope (`scope-validation`).

## Inputs

```text
Documentation Context (documentation-discovery output, incl. flagged candidates)
Optionally: implementation/test locations and observed behavior
Optionally: a proposed documentation change (before applying it)
```

## Prerequisites & Required Context

- Precedence rules (`AGENTS.md` §2; `GAME_RULES.md` §21).
- The purpose line ("This document answers…") of each document involved, used
  to decide canonical ownership (`documentation/documentation-change.md` §3).

## Authoritative Sources

```text
AGENTS.md §2, §4, §17, §20         precedence; conflict handling; doc-change rule; stop list
.ai/README.md §13, §18             STOP report format; classification of code-vs-docs gaps
core/context-discovery.md §3       the categories of conflicts to look for
documentation/documentation-change.md §2–§3   no duplication; canonical owner
GAME_RULES.md §20–§21              rule change policy; source-of-truth hierarchy
docs/03-decisions/README.md §3, §6, §7   ADR vs doc roles; index
The documents actually under comparison
```

## Procedure

**Mode A — documents vs documents (conflict check)**

1. **List the concepts** in play and, for each, its owning document by
   precedence and purpose line.
2. **Collect every statement** about that concept from all documents read.
   Compare *meaning*, not wording. Categories to test:
   conflicting rules · missing rules · contradictory architecture ·
   inconsistent contracts (API/state/event/Redis/database) · stale
   assumptions/references · unclear behavior
   (`core/context-discovery.md` §3).
3. **Check derived statements against the owner.** A referencing document
   may cite the owner; it must not restate or diverge from it. Restated
   definitions are duplication findings
   (`documentation/documentation-change.md` §2).
4. **Check references resolve:** cited section numbers, cited files, cited
   ADRs. Unresolvable ones are *stale reference* findings.
5. **Check ADR coherence:** an ADR records *why* only; if it contradicts a
   technical document, the technical document owns the *what*
   (`docs/03-decisions/README.md` §3); status/index consistent (§7).

**Mode B — documents vs implementation / tests (discrepancy classification)**

6. Compare the expected behavior (from documents) with the implementation or
   test behavior. **Default assumption: documents describe intended behavior;
   code is the possibly-incorrect implementation** (`.ai/README.md` §18).
7. Classify: `Implementation bug` · `Documentation bug` · `Design ambiguity` ·
   `Architecture decision required`. Choosing `Documentation bug` requires
   stated evidence that the code reflects the intended behavior; otherwise it
   is reported, not applied. (Bug-specific categories such as test bug, data
   bug, or configuration bug stay in `development/bug-fix.md` §1.)

**Mode C — validating a documentation change**

8. After a proposed or applied change: re-read the owner and every referencing
   document together; confirm no duplicate definition was introduced, no
   reference is now stale, and no other document contradicts the new text.

**All modes**

9. **Decide the owner.** Use precedence (`AGENTS.md` §2) and, for ties, the
   document purpose lines. If two purposes both plausibly cover the concept,
   report a structural ambiguity — do not guess.
10. **Propose the smallest correction** (which document, which section, what
    kind of change). Do not apply it unless the calling workflow explicitly
    authorizes documentation changes for this task.

## Outputs

```text
Consistency Report
| # | Category | Concept | Source A (file §) | Source B (file § / code) | Nature | Owner by precedence | Classification | Severity |
- Duplicate definitions
- Stale references
- Missing rules (question the docs cannot answer)
- Smallest proposed correction per finding
- Decision required from a human (yes/no, and what)
- If a stop applies: the STOP CONDITION block (.ai/README.md §13 format)
```

## Validation

```text
[ ] Each finding cites both sides with file and section
[ ] Owner determination follows AGENTS.md §2 / purpose lines, not convenience
[ ] "Documentation bug" classifications carry explicit evidence
[ ] No finding was resolved by silently choosing a side
[ ] No document was modified unless the calling workflow authorized it
[ ] Proposed corrections are minimal and touch only the canonical owner
```

## Stop Conditions

- **Any genuine conflict** between authoritative documents → STOP: produce
  the report; do not proceed with implementation (`AGENTS.md` §4).
- A required rule is missing (`AGENTS.md` §7) or the case is ambiguous.
- Data contracts disagree with each other.
- The correction would change game rules or architecture — requires human
  approval and, for rules, the Rule Change Policy (`GAME_RULES.md` §20);
  for architecture, an ADR (`AGENTS.md` §18).
- The situation would require changing code and documentation
  simultaneously to make them agree without deciding which was correct.

Baseline stop conditions in `.ai/skills/README.md` §6 also apply.

## Common Failure Modes

- Picking whichever reading is easier to implement.
- Assuming "code is right" without evidence.
- "Fixing" a low-level document to match a high-level one (or code) without
  approval.
- Missing conflicts that only appear when comparing a domain document with a
  technical one (state/events/contracts).
- Reporting wording differences as conflicts when the meaning is the same
  (noise).
- Editing the referencing documents instead of the owner.

## Traceability

```text
Used by:    core/context-discovery.md §3; development/bug-fix.md,
            gameplay-change.md; architecture/adr-change.md;
            documentation/documentation-change.md; quality/review.md
Reads:      AGENTS.md §2/§4/§17/§20; .ai/README.md §13/§18;
            GAME_RULES.md §20/§21; docs/03-decisions/README.md;
            documents under comparison
Produces:   Consistency Report (and a STOP CONDITION block when required)
Depends on: documentation-discovery (context and flagged candidates)
```
