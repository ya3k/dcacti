# skills/discovery/impact-analysis.md — Skill: Impact Analysis

**Version:** 1.0

> Trace a proposed or made change along its actual dependency chain —
> game, technical, architecture, API, database, realtime, testing,
> documentation — without inventing requirements.

## Purpose

Determine **what a change touches and what must be checked or updated**, so a
plan is never limited to "the last link" (code). It answers "what else is
affected?", not "what should the design be?".

## When to Use

- `core/planning.md` §3 (Medium and Large tasks).
- `development/gameplay-change.md` §3 ("review downstream implications").
- `development/refactor.md` §2 (inventory what must **not** change).
- `architecture/architecture-change.md` (identify impact).
- `documentation/documentation-change.md` (which references may go stale).

## When Not to Use

- Trivial LOW-risk changes where `core/planning.md` §2 requires only a brief
  plan.
- To decide *whether* a change is allowed (`scope-validation`) or *whether
  the docs are consistent* (`documentation-consistency`).
- To propose new requirements. Impact analysis lists affected things; it does
  not add work the docs and the task do not imply.

## Inputs

```text
A change description (task text, diff, or proposed doc/contract change)
Documentation Context (documentation-discovery output)
Risk level (core/task-intake.md §3) — consumed, not re-decided
```

## Prerequisites & Required Context

- Documentation Context for the change's domain(s).
- Access to the code/test layout for the affected components.

## Authoritative Sources

```text
.ai/README.md §12                  the required impact chains
AGENTS.md §6, §12, §13             domain and technical boundaries
docs/02-technical/ARCHITECTURE.md  §1 structure, §3 component ↔ owning document,
                                   §4 communication
docs/02-technical/GAME_STATE.md, GAME_EVENTS.md, API_CONTRACTS.md,
SIGNALR_PROTOCOL.md, REDIS_STATE.md, DATABASE.md
                                   contracts a change may ripple into
docs/03-decisions/ADR/             decisions that a change may touch
```

## Procedure

1. **Classify the change kind.** Game rule, API contract, realtime/event,
   state/persistence, architecture, or behavior-preserving refactor. A change
   may be several; follow every chain that applies.
2. **Start at the owner.** Identify the owning document for the changed
   concept (`AGENTS.md` §2/§6). That is link 1.
3. **Walk downstream, one hop at a time**, using the chains in
   `.ai/README.md` §12:
   - *Game rule* → owning domain doc → technical docs that reference it
     (state, events, contracts) → implementation → tests → possibly an ADR.
   - *API contract* → `API_CONTRACTS.md` → backend → frontend → tests.
   - *Architecture* → `TDD.md` → `ARCHITECTURE.md` → ADR (new/superseding) →
     implementation.
   - *Realtime/event* → `GAME_EVENTS.md` ↔ `SIGNALR_PROTOCOL.md` ↔
     `GAME_STATE.md` sequencing → client consumption → tests.
   - *Persistence* → `DATABASE.md` / `REDIS_STATE.md` ↔ `GAME_STATE.md` →
     repositories → tests.
4. **Find dependents by reference, not by hunch.** Search the docs for the
   changed document/section name to find who cites it; use
   `ARCHITECTURE.md` §3 to map documents to components and code locations.
5. **Fill each impact dimension** (see Outputs). "None" is a valid answer only
   when a link was actually checked; otherwise record `unverified`.
6. **Refactor mode.** Produce the *must-not-change inventory* required by
   `development/refactor.md` §2: existing tests, public contracts, domain
   behavior, event order/payload meaning, state shape, persistence meaning,
   wire behavior. Any item that cannot be preserved is reported as a behavior
   change, not silently accepted.
7. **Documentation mode.** List every document that references the changed
   concept and whether the reference (not the content) may need updating —
   never propose copying content into referencing documents.
8. **Risk.** Map the result onto the level scheme in
   `core/task-intake.md` §3 (do not invent a new scale). Raise the level if
   the impact reaches a HIGH item (rule change, battle state model,
   architecture, migration, auth, persistence strategy).

## Outputs

```text
Impact Report
- Change kind(s)
- Chain followed (owner → … → tests), each link marked checked / unverified
- Game impact:           domain rule docs, dependent rules
- Technical impact:      state (GAME_STATE), events (GAME_EVENTS)
- Architecture impact:   modules/layers/ADRs touched
- API impact:            endpoints/contracts
- Database impact:       PostgreSQL entities/constraints; Redis keys/state
- Realtime impact:       hub methods, event ordering, resync
- Testing impact:        tests to add/update/keep; scenario areas
- Documentation impact:  documents to update / references to re-check
- Affected files/components (paths)
- Must-not-change inventory (refactor mode)
- Risk level and why
- Open questions / unverified links
```

## Validation

```text
[ ] The chain starts at the owning document, not at code
[ ] Every downstream link is marked checked or explicitly unverified
[ ] No impact item is justified only by "probably"
[ ] No new requirement was introduced; every item traces to a document,
    a contract, or the task
[ ] Risk level uses the existing scheme and is not lower than any single
    HIGH item present
```

## Stop Conditions

- The owning document for the changed concept cannot be identified.
- The change would require touching an out-of-scope system (`scope-validation`).
- A downstream contract contradicts the changed document (data-contract
  conflict → `documentation-consistency` → stop).
- The change would contradict an Accepted ADR (`architecture-conformance` /
  `architecture/adr-change.md` §3) — report, do not plan around it.

Baseline stop conditions in `.ai/skills/README.md` §6 also apply.

## Common Failure Modes

- Analyzing only code (the "last link").
- Forgetting event/state documents when a rule changes.
- Forgetting the client side of a contract change.
- Marking things "unaffected" without having checked the link.
- Growing the change into a redesign ("while we're here").
- Rating a rule/formula/state-model change below HIGH.

## Traceability

```text
Used by:    core/planning.md §3; development/gameplay-change.md,
            refactor.md, feature.md; architecture/architecture-change.md;
            documentation/documentation-change.md
Reads:      .ai/README.md §12; AGENTS.md §6/§12/§13; ARCHITECTURE.md §1/§3/§4;
            contract docs and ADRs relevant to the change
Produces:   Impact Report
Depends on: Documentation Context (documentation-discovery output)
```
