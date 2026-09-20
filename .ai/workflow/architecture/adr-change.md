# architecture/adr-change.md — ADR Change Workflow

**Version:** 1.0

> Use for: recording a new architectural decision, or changing the status
> of an existing one. This workflow governs the ADR *process*; the ADR
> content requirements themselves are defined in
> `docs/03-decisions/README.md`.

---

# 1. When This Workflow Applies

Use for changes involving:

```text
architectural decisions
technology choices
ownership boundaries
persistence strategy
realtime strategy
authoritative state model
major infrastructure decisions
```

Do **not** create an ADR for:

```text
typos
trivial bug fixes
normal implementation work
ordinary refactoring
```

## Decision vs. Implementation

```text
Decision         WHY a technology/approach was chosen — belongs in an ADR
Implementation     HOW that decision is realized in code — belongs in
                    docs/02-technical/ + the code itself, never in the ADR
```

If a proposed ADR would mostly describe *how* something works rather than
*why* it was chosen, it does not belong here — that content belongs in the
relevant `docs/02-technical/` file instead, per
`docs/03-decisions/README.md` §1.

---

# 2. Creating a New ADR

```text
Confirm the decision is actually confirmed in existing docs/ (or is being
  confirmed right now as part of this task) — do not create a Proposed
  ADR for a decision nobody has actually made yet, per
  docs/03-decisions/README.md §5
 ↓
Assign the next sequential number (never reuse a number, even for a
  deprecated/superseded ADR)
 ↓
Write the ADR using the exact template in docs/03-decisions/README.md's
  referenced format (Context / Decision / Alternatives Considered / Why /
  Consequences / Related Documents)
 ↓
List only alternatives that are responsibly inferable from the existing
  architecture — do not invent a fabricated history of options that were
  never actually relevant
 ↓
Add the ADR to the index table in docs/03-decisions/README.md §7
 ↓
Set Status per §4 below
```

---

# 3. Superseding an Existing ADR

If a new decision replaces a prior `Accepted` ADR:

```text
Create the new ADR as normal (§2)
 ↓
In the new ADR's Context, explicitly name the prior ADR being replaced
 ↓
Set the new ADR's Status to Accepted
 ↓
Update the PRIOR ADR's Status to Superseded, with a note pointing to the
  new ADR's number — do not delete or silently rewrite the prior ADR's
  content
 ↓
Update the index table in docs/03-decisions/README.md §7 for both entries
```

---

# 4. ADR Status Values

```text
Proposed    drafted, not yet confirmed by existing documentation/decision
Accepted    confirmed and currently in effect
Deprecated  no longer recommended, no replacement decided yet
Superseded  replaced by a later ADR (must reference the new ADR's number)
```

Only use `Accepted` when the decision is genuinely established — never for
a decision still under discussion, matching
`docs/03-decisions/README.md` §5.

---

# 5. Composition

This workflow is typically invoked from `architecture/architecture-change.md`
§1 or `development/gameplay-change.md` §3, not directly from task intake —
an ADR records a decision that some other change made necessary, it is
rarely the entire task on its own.
