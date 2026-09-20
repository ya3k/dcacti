# quality/review.md — Review Workflow

**Version:** 1.0

> Use whenever `core/implementation.md`, `development/*.md`,
> `architecture/architecture-change.md`, or
> `documentation/documentation-change.md` reaches its review step.

---

# 1. Checklist

## Correctness
Does the implementation match the authoritative documentation it was built
against (the specific docs identified in `core/context-discovery.md`)?

## Architecture
Does it follow `docs/02-technical/ARCHITECTURE.md` (layering, dependency
direction, module boundaries per `AGENTS.md` §12-13)?

## Scope
Did the change stay within the task's defined boundary
(`core/implementation.md` §2)? Was anything unrelated refactored, added,
or "improved"?

## Tests
Is the important behavior covered, at the depth `core/validation.md`
selected for this task's risk level?

## Documentation
Do any documentation updates still need to be made
(`documentation/documentation-change.md`), or were they already made and
are they consistent with the code as implemented?

## Security
Are there any security concerns raised by this specific change (e.g. does
it touch authentication, per ADR-007's open item, or expose data it
shouldn't)?

## Performance
Is there an obvious regression (e.g. a new query on the hot resolution
path that `docs/02-technical/TDD.md` §4.3 says must not happen)?

## Maintainability
Was unnecessary complexity introduced — any of the anti-patterns listed in
`AGENTS.md` §9 (unnecessary interfaces, factories, event buses, generic
repositories, modules, infrastructure)?

## Determinism (Gameplay/Battle Logic Only)
```text
Is server authority preserved? (.ai/README.md §15 — who owns/calculates/
  validates/broadcasts each piece of state touched)
Is RNG controlled? (server-seeded, per docs/02-technical/TDD.md §6)
Are state transitions deterministic where the docs require it?
Is client authority avoided for Damage/HP/Boss HP/Power/Match/Combo/
  Passive/Rewards/Battle outcome?
```

---

# 2. Review Is Not Optional for Any Workflow

Every `development/*.md` and `architecture/*.md` workflow routes through
this checklist before `core/completion.md`. A task is not complete
(`core/completion.md` §1) without it, regardless of risk level — only the
*depth* of each checklist item scales with risk (`core/validation.md` §2),
not whether the checklist runs at all.

---

# 3. Output

A pass/fail (with specifics) against each relevant checklist item, feeding
into `core/completion.md`'s final report — specifically the "Validation"
and "Risks" sections.
