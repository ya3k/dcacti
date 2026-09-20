# skills/discovery/documentation-discovery.md — Skill: Documentation Discovery

**Version:** 1.0

> Find, read, and bound the authoritative documents a task depends on —
> source of truth first, dependencies second, implementation last — without
> scanning the whole repository.

## Purpose

Turn a classified task into a **documentation context**: which documents are
authoritative, which sections matter, what they depend on, and what looks
conflicting, missing, or assumption-level. Every other skill consumes this
output.

## When to Use

- The workflow reaches `core/context-discovery.md` (all `development/*`,
  `architecture/*`, `documentation/*` flows).
- A skill or agent needs documents it was not handed (e.g. a bug touches a
  domain nobody has read yet).
- The task's domain changed mid-work (`core/implementation.md` §3).

## When Not to Use

- Documentation context already exists from `core/context-discovery.md` and
  the domain has not changed — reuse it.
- To *judge* conflicts. Discovery **flags** candidates; `documentation-consistency`
  decides and classifies them.
- To find a single known fact in a known file (just read the file).

## Inputs

```text
Task classification (type, scope, domain(s), risk) from core/task-intake.md
   — if missing, derive the domain(s) from the task text and say so
Optionally: the specific concept, action, or contract in question
```

## Prerequisites & Required Context

- The current `docs/` tree (list it; do not rely on a remembered file list).
- `AGENTS.md` §6 (task → required reading) and §2 (precedence).

## Authoritative Sources

```text
AGENTS.md §2, §3, §6               precedence, documentation map, required reading
.ai/README.md §7                   discovery principle
docs/00-overview/MVP_SCOPE.md      always first when the task adds a system/content
docs/01-game-design/*, docs/02-technical/*, docs/03-decisions/*
                                   whichever the domain routes to
```

The document list for a task is **derived** from `AGENTS.md` §6 against the
current tree. This skill contains no fixed per-domain list on purpose.

## Procedure

1. **Confirm the domain(s).** Use the intake classification; a task may span
   several. Domains and their boundaries: `AGENTS.md` §12–§13.
2. **Scope first.** If the task adds any system, content, or infrastructure,
   read `MVP_SCOPE.md` §1–§4 before anything else (hand the IN/OUT/FUTURE
   decision to `scope-validation`).
3. **Route to owning documents** using `AGENTS.md` §6. Verify each file exists
   in the tree; if a routed file is missing, note it as missing
   documentation (stop condition below).
4. **Read purpose lines, then relevant sections.** Each doc states what
   question it answers at the top; use it to confirm ownership, then read only
   the sections the task touches. Do not read documents wholesale "for safety".
5. **Follow cross-references one hop.** Read what the documents themselves
   reference for the concept (e.g. a domain rule pointing at `GAME_STATE.md`
   or `GAME_EVENTS.md`). Stop when references become irrelevant or circular.
6. **Add ADRs by area.** Use the ADR index (`docs/03-decisions/README.md` §7)
   and `AGENTS.md` §6 rows (realtime → ADR-004/008, active state → ADR-005,
   persistence → ADR-006, authority → ADR-001, etc.). ADRs give *why*, never
   override the *what* (`docs/03-decisions/README.md` §3).
7. **Mark assumption-level content.** Any text a document itself labels
   ASSUMPTION, "Open Item", "UNDECIDED", "Draft", or "Potential Future Idea"
   (e.g. `TDD.md` §0, `docs/03-decisions/README.md` §8) is recorded as
   *unconfirmed*, not as fact.
8. **Inspect implementation last.** Only after the docs, locate relevant
   existing code and tests using the component-to-document table in
   `ARCHITECTURE.md` §3. Order is fixed: source of truth → dependencies →
   implementation.
9. **Flag candidates.** Note apparent conflicts (two docs defining the same
   concept differently), missing rules (question the docs cannot answer), and
   stale references (section numbers that no longer resolve). Do not resolve
   them here.

## Outputs

```text
Documentation Context
- Task domains
- Documents read: path + sections relied on + why (which question they answer)
- Precedence notes: which document owns which concept (AGENTS.md §2)
- Dependencies: documents/sections referenced by the above (one hop)
- ADRs consulted: number + relevance
- Assumption-level content: location, marked unconfirmed
- Existing implementation/tests located (paths), if any
- Candidate conflicts / gaps / stale references (unresolved, for
  documentation-consistency)
```

Consumers: `documentation-consistency`, `impact-analysis`,
`gameplay-behavior-derivation`, all contract skills, `core/planning.md`.

## Validation

```text
[ ] Every listed document exists and was actually read for the cited sections
[ ] Every domain in the task has at least one owning document (or a stated gap)
[ ] MVP_SCOPE.md was read if the task adds a system/content
[ ] No document outside the task's domain was read without a reason
[ ] Nothing assumption-level was reported as confirmed
[ ] No rule, value, or contract content was copied into the output
    (paths + section references only)
```

## Stop Conditions

- A routed authoritative document (or the section a rule should live in) does
  not exist → missing documentation.
- The task's domain cannot be determined without guessing.
- Two documents give different owners for the same concept (structural
  ambiguity — report, do not pick).
- Required information exists only in code or in someone's memory
  (`.ai/README.md` §19) → gap to report, not to infer.

Baseline stop conditions in `.ai/skills/README.md` §6 also apply.

## Common Failure Modes

- Reading the whole `docs/` tree (or only `GAME_RULES.md`) instead of the
  routed set.
- Using a memorized document list that no longer matches the tree.
- Starting from code and back-fitting documents to it.
- Treating `ROADMAP.md` or an ADR "Consequences" section as a rule.
- Treating the TDD's ASP.NET Core assumption, or any unstated runtime
  version, as confirmed.
- Silently choosing a side when two documents differ.

## Traceability

```text
Used by:    core/context-discovery.md; core/implementation.md §3 (domain changed
            mid-work); development/feature.md, bug-fix.md,
            gameplay-change.md; architecture/architecture-change.md;
            documentation/documentation-change.md (canonical-owner lookup)
Reads:      AGENTS.md §2/§3/§6/§12/§13; .ai/README.md §7; docs/** (routed)
Produces:   Documentation Context
Depends on: none (bottom of every composition chain)
```
