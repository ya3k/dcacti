# architecture/architecture-change.md — Architecture Change Workflow

**Version:** 1.0

> Use for: a task that changes project structure, layering, technology
> choice, persistence strategy, realtime strategy, or the
> authoritative-state model — anything `docs/02-technical/ARCHITECTURE.md`
> or `docs/02-technical/TDD.md` currently governs.

---

# 1. Flow

```text
Architecture request
 ↓
Read TDD.md
 ↓
Read ARCHITECTURE.md
 ↓
Read relevant ADR(s)                (docs/03-decisions/ADR/ — check
                                     whether this area already has a
                                     recorded decision)
 ↓
Identify impact                       (which modules/layers/boundaries
                                       from ARCHITECTURE.md §1-§4 are
                                       affected)
 ↓
Determine whether an existing decision becomes invalid
                                          (does this change contradict or
                                          supersede an Accepted ADR? —
                                          see architecture/adr-change.md
                                          §3 for Superseded handling)
 ↓
Define proposed change
 ↓
Update architecture documentation           (TDD.md and/or
                                             ARCHITECTURE.md — the owning
                                             section per their own
                                             headers)
 ↓
architecture/adr-change.md                     (create/update the ADR)
 ↓
core/planning.md
 ↓
core/implementation.md
 ↓
quality/testing.md
 ↓
quality/review.md
 ↓
core/completion.md
```

Architecture changes start with documentation analysis, not code — the
first five steps above happen before any plan is written.

---

# 2. Explicit Reasoning and Traceability

Every architecture change must be traceable: the updated
`TDD.md`/`ARCHITECTURE.md` section and its corresponding ADR (§ above)
must both exist before implementation begins, not be backfilled afterward.
This is what distinguishes an architecture change from an ordinary
implementation task — see `architecture/adr-change.md` §1 for exactly
which kinds of changes require this.

---

# 3. If an Existing ADR Would Be Contradicted

If the proposed change would make an existing `Accepted` ADR's decision no
longer true (e.g. changing the realtime transport away from SignalR would
contradict ADR-004), this is not a normal architecture change — it is a
decision reversal. Follow `architecture/adr-change.md` §3 (Superseding an
Existing ADR) explicitly; do not just edit the old ADR's content in place.

---

# 4. Composition

Reuses `core/planning.md` through `core/completion.md` for the
implementation portion, and hands the decision-recording portion to
`architecture/adr-change.md`.
