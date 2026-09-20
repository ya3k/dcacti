# skills/quality/architecture-conformance.md — Skill: Architecture Conformance

**Version:** 1.0

> Check that a change respects layer direction, module and domain ownership,
> technical boundaries, anti-overengineering limits, and existing ADRs — and
> detect when a change is really an architecture decision.

## Purpose

Give a repeatable answer to "does this fit the documented architecture, and if
not, is it an ordinary defect or an architecture change requiring an ADR?"
It does not define the architecture; `TDD.md`, `ARCHITECTURE.md`, and the ADRs
do.

## When to Use

- `core/implementation.md` §2 (boundaries) while writing code that adds
  types, modules, dependencies, or infrastructure.
- `architecture/architecture-change.md` (identify impact; does an Accepted ADR
  become invalid?).
- `architecture/adr-change.md` (is this decision ADR-worthy; does it contradict
  or supersede an existing ADR?).
- `quality/review.md` "Architecture" and "Maintainability".
- `core/validation.md` "Architecture validation" layer.

## When Not to Use

- Judging gameplay correctness or contract shape (other skills).
- Choosing a new architecture: that is a human-approved decision
  (`AGENTS.md` §18), not an output of this skill.

## Inputs

```text
The change set (diff/files) or a proposal
Documentation Context (documentation-discovery output)
```

## Prerequisites & Required Context

- Layer and module structure and the component-to-owner table
  (`ARCHITECTURE.md` §1–§3).
- Domain and technical boundaries (`AGENTS.md` §12–§13).
- The ADR index (`docs/03-decisions/README.md` §7) and the ADRs in the touched
  area.

## Authoritative Sources

```text
TDD.md                             technology, responsibility split, runtime model, non-goals
ARCHITECTURE.md §1–§5              structure, dependency direction, components, communication,
                                   anti-overengineering notes
AGENTS.md §9, §12, §13, §18        anti-overengineering; boundaries; architecture change rule
docs/03-decisions/README.md §1–§3, §5, §8    when an ADR is needed; status; open items
docs/03-decisions/ADR/ADR-001 … ADR-008      the decisions themselves (why, not what)
architecture/adr-change.md §1      what does and does not warrant an ADR
```

Runtime/framework specifics come only from the documents. Where a document
marks something as an ASSUMPTION or open item (e.g. `TDD.md` §0), report it as
unconfirmed; do not assert versions or frameworks the documents do not state.

## Procedure

1. **Map the change to layers and modules.** For each new/changed type or file,
   determine its layer (Domain / Application / Infrastructure / Api / client)
   and domain module (`ARCHITECTURE.md` §1, §3; `AGENTS.md` §12).
2. **Dependency direction.** Verify the documented direction is not violated
   (e.g. Domain referencing infrastructure or transport types)
   (`ARCHITECTURE.md` §2).
3. **Responsibility placement.** Verify logic sits with its owner: game rules
   in Domain, sequencing in Application, transport/persistence in
   Infrastructure, wiring in Api, presentation in the client
   (`TDD.md` §2, `ARCHITECTURE.md` §2–§4). Check that domain logic isn't
   placed in the wrong domain module for convenience (`AGENTS.md` §12).
4. **Communication rules.** Verify the documented flow (e.g. only the
   documented component writes state during a battle) is preserved
   (`ARCHITECTURE.md` §4).
5. **Technical boundaries.** Verify no responsibility moved across a boundary
   (e.g. Redis/PostgreSQL/SignalR/Phaser/React roles) (`AGENTS.md` §13).
6. **Anti-overengineering.** Flag added interfaces, factories, event buses,
   generic repositories, plugin/scripting layers, message queues, CQRS,
   service splits, or modules that neither the architecture document nor the
   task requires (`ARCHITECTURE.md` §5, `AGENTS.md` §9).
7. **ADR check.** Read the ADRs in the touched area. Does the change conflict
   with an Accepted ADR? Is it already covered? Does it introduce a decision
   worth an ADR under `architecture/adr-change.md` §1 (architectural decision,
   technology choice, ownership boundary, persistence/realtime strategy,
   authoritative state model, major infrastructure)? Ordinary implementation
   work, typos, trivial fixes and refactors are not ADR-worthy.
8. **Classify the result:**
   - `Conforms`
   - `Defect` — violates a documented rule; fix locally
   - `Architecture change` — the change itself alters a documented
     structure/decision; must go through `architecture/architecture-change.md`
     (docs + ADR first)
   - `Undocumented` — architecture is unclear for this case (stop)

## Outputs

```text
Architecture Conformance Report
| Element | Layer/module | Rule checked (doc §) | Verdict |
- Dependency-direction findings
- Responsibility/boundary findings
- Overengineering findings
- ADR relationships: consistent / conflicts with ADR-N / ADR-worthy (yes/no, why)
- Classification: Conforms / Defect / Architecture change / Undocumented
- Assumption-level dependencies
- Recommended smallest correction or required approval
```

## Validation

```text
[ ] Every verdict cites ARCHITECTURE.md / TDD.md / AGENTS.md / ADR section
[ ] The relevant ADR(s) were actually read, not assumed
[ ] Anti-overengineering findings compare against what documents require,
    not against personal preference
[ ] No technology/version claim beyond what documents state
[ ] "Architecture change" classifications are not silently downgraded to defects
```

## Stop Conditions

- The change conflicts with an Accepted ADR → follow
  `architecture/adr-change.md` §3 (supersede), do not edit around it
  (`AGENTS.md` §18).
- The change implies an architectural decision with no ADR → propose ADR and
  wait; do not implement inline.
- Architecture is unclear or documents conflict (TDD vs ARCHITECTURE vs ADR)
  → `documentation-consistency` → stop.
- The change requires an excluded technology or structure
  (`MVP_SCOPE.md` §2 / `TDD.md` §7) → scope violation.
- The runtime/platform detail needed is only an assumption in the documents.

Baseline stop conditions in `.ai/skills/README.md` §6 also apply.

## Common Failure Modes

- Approving "temporary" boundary violations.
- Treating an ADR as the specification of *how* (ADRs own *why*).
- Treating an ADR-worthy change as a plain implementation detail.
- Adding a generic abstraction "because more Pets/Cards will come".
- Asserting framework versions or patterns the documents do not state.
- Editing an existing ADR's content in place instead of superseding it.

## Traceability

```text
Used by:    core/implementation.md §2; core/validation.md (architecture layer);
            architecture/architecture-change.md, adr-change.md;
            quality/review.md (Architecture, Maintainability);
            development/* via review
Reads:      TDD.md; ARCHITECTURE.md; AGENTS.md §9/§12/§13/§18;
            docs/03-decisions/** ; architecture/adr-change.md
Produces:   Architecture Conformance Report
Depends on: documentation-discovery (only if context not already supplied)
```
