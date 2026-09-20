# skills/quality/scope-validation.md — Skill: Scope Validation

**Version:** 1.0

> Decide whether every system, content item, and piece of infrastructure a
> task adds or touches is inside MVP scope, and whether a change stays within
> its task boundary.

## Purpose

Apply `MVP_SCOPE.md` (the single source of truth for IN / OUT / FUTURE) and
the task-discipline rules (`AGENTS.md` §16) as one repeatable check. It
prevents scope creep in two forms: **product scope** (adding an out-of-scope
system) and **task scope** (changing things the task did not ask for).

## When to Use

- `development/feature.md` §2 — the mandatory first step.
- `core/task-intake.md` / `core/planning.md` when a task adds any new system,
  content, or infrastructure.
- `development/gameplay-change.md` §4 — re-check at every point of a design
  change.
- `architecture/architecture-change.md` — technology or structure choices.
- `core/implementation.md` §2 and `quality/review.md` "Scope" — task
  boundary.

## When Not to Use

- Pure typo/isolated-refactor work with no new system or content and a
  trivially bounded diff.
- To decide *how* to implement an in-scope item.

## Inputs

```text
Task text (and any linked design change)
List of systems / content / infrastructure the task adds or touches
   (or the change set/diff, for the task-boundary mode)
```

## Prerequisites & Required Context

- The current `MVP_SCOPE.md` (§1 IN, §2 OUT, §3 FUTURE, §4 classification
  authority) — read fresh; do not rely on remembered lists.

## Authoritative Sources

```text
docs/00-overview/MVP_SCOPE.md      §1 IN, §2 OUT, §3 FUTURE, §4 classification authority
docs/00-overview/ROADMAP.md        direction only — NOT authorization (ROADMAP.md §2)
GAME_RULES.md §19, §20             MVP scope rules; rule change policy
AGENTS.md §8, §9, §16              MVP protection, anti-overengineering, task discipline
.ai/README.md §14                  MVP protection at the .ai layer
```

## Procedure

1. **Extract the items.** List every system, mechanic, content type, and
   infrastructure/technology the task adds or changes — including *implied*
   ones (a "small tweak" that introduces a modifier from an excluded system
   is an excluded system).
2. **Classify each against `MVP_SCOPE.md`:**
   - listed in §1 → `IN`
   - listed in §2 → `OUT`
   - listed in §3 → `FUTURE`
   - listed nowhere → `FUTURE` by default (§4) — never `IN` "to be helpful"
3. **Check partial and scaffolded forms.** Partial implementation, feature
   flags, stubs, or "scaffolding for later" of an `OUT`/`FUTURE` item is still
   a violation (`MVP_SCOPE.md` §3).
4. **Check that ROADMAP is not used as authorization.** An item in
   `ROADMAP.md` future direction is not buildable until promoted into
   `MVP_SCOPE.md` §1 through the Rule Change Policy (`GAME_RULES.md` §20).
5. **Check design changes.** A requested rule change is not permission to
   cross into `OUT` territory; if the smallest documentation change needed
   would do so, that is a scope violation (`development/gameplay-change.md`
   §4).
6. **Technology/infrastructure check.** Compare technology and infrastructure
   choices to the IN infrastructure list and the excluded ones; adding an
   excluded technology is a violation even if "only internal".
7. **Anti-overengineering check.** New abstractions, interfaces, factories,
   event buses, generic repositories, modules, or infrastructure not required
   by `ARCHITECTURE.md` or the task are scope additions (`AGENTS.md` §9);
   hand structural judgment to `architecture-conformance`.
8. **Task-boundary mode.** Compare the change set to the task: list every
   change (refactor, "improvement", unrequested feature, balance change)
   not required by the task (`AGENTS.md` §16,
   `core/implementation.md` §2). Report them as follow-up candidates
   (issue · location · impact · suggested follow-up task); do not fix them.

## Outputs

```text
Scope Validation Report
| Item | Classification (IN/OUT/FUTURE/unlisted→FUTURE) | Source (MVP_SCOPE.md §) | Verdict |
- Violations: item, why, smallest in-scope alternative (if one exists in docs)
- Task-boundary findings: out-of-task changes, with report-format entries
- Ambiguities in classification (for §4 reporting)
```

## Validation

```text
[ ] Every extracted item has a classification with a cited section
[ ] Implied and partial forms were considered, not only headline features
[ ] No item was classified IN without appearing in MVP_SCOPE.md §1
[ ] ROADMAP.md was not used as evidence of IN status
[ ] In task-boundary mode, every changed file maps to a task requirement or is reported
```

## Stop Conditions

- Any item is `OUT`, `FUTURE`, or unlisted → **scope violation**: stop and
  report; do not implement a scoped-down variant (`AGENTS.md` §8).
- The classification of an item is genuinely ambiguous
  (`MVP_SCOPE.md` §4: report the ambiguity, do not assume IN).
- A design change requires crossing into `OUT` territory.

Baseline stop conditions in `.ai/skills/README.md` §6 also apply.

## Common Failure Modes

- Treating "obviously small" additions as exempt.
- Treating anything in `ROADMAP.md` or a GDD future section as approved.
- Approving scaffolding, flags, or empty tables "for later".
- Treating an unlisted item as `IN`.
- Re-listing `MVP_SCOPE.md` content inside the report instead of citing it.
- Fixing unrelated problems found during the task-boundary check instead of
  reporting them.

## Traceability

```text
Used by:    core/task-intake.md; core/planning.md; core/implementation.md §2;
            development/feature.md §2, gameplay-change.md §4;
            architecture/architecture-change.md; quality/review.md (Scope)
Reads:      MVP_SCOPE.md; ROADMAP.md (context only); GAME_RULES.md §19/§20;
            AGENTS.md §8/§9/§16
Produces:   Scope Validation Report
Depends on: none
```
