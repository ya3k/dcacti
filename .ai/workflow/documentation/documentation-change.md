# documentation/documentation-change.md — Documentation Change Workflow

**Version:** 1.0

> Use for: a task whose primary output is a change to `docs/` itself —
> not a code change that happens to touch docs as a side effect (that
> case is handled within `development/*.md`'s "documentation impact"
> steps, which call into this workflow when needed).

---

# 1. Flow

```text
Documentation request
 ↓
Identify the authoritative (canonical owner) document
   for the concept being changed — see docs/AGENTS.md §2 / the "one
   concept, one owner" mapping in .ai/README.md §6
 ↓
Read related documents
   (anything that references the concept, to find what else might need a
   corresponding reference update)
 ↓
Check for conflicts
   (does the requested change contradict another document? If so, this
   becomes a core/context-discovery.md §3 STOP, not a normal doc edit)
 ↓
Update the smallest authoritative source
   (edit only the canonical owner's content — do not also edit every
   place that references it)
 ↓
Update dependent references if required
   (only if a reference's wording is now stale — e.g. a section number
   changed — not to duplicate the updated content into them)
 ↓
Validate consistency
   (re-read the canonical doc and its referencing docs together; confirm
   no duplicate definition was accidentally introduced)
```

---

# 2. No Duplication, Ever

**Do not duplicate a rule simply to make it easier to find.** Example: if
a damage formula belongs to `COMBAT_RULES.md`, other documents reference
it —

```text
"See COMBAT_RULES.md §3 for the damage formula."
```

— they do not restate the formula, even partially, even "just for
context." This is the single most important rule in this workflow; the
entire `docs/` tree's value depends on every concept having exactly one
owner (see `docs/AGENTS.md` §2 and the audit principles that originally
built this doc set).

---

# 3. Determining the Canonical Owner

If it's unclear which document should own a piece of information, use each
document's own stated purpose (the "This document answers..." line at the
top of every file in `docs/`) to decide — the owner is whichever document's
question the information actually answers. If two documents' stated
purposes both plausibly cover it, that itself is worth reporting as a
structural ambiguity (per `core/context-discovery.md` §3) rather than
guessing.

---

# 4. Composition

This workflow does not include its own testing/review steps — a pure
documentation change still goes through `quality/review.md` (skipping the
review checklist items that only apply to code) and `core/completion.md`
for the final report.
