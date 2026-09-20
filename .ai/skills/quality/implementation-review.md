# skills/quality/implementation-review.md — Skill: Implementation Review

**Version:** 1.0

> Review a change against its authoritative sources by executing the review
> checklist with the right specialist skills, and emit severity-rated,
> source-cited findings.

## Purpose

`quality/review.md` defines **when** review happens and **which checklist
items exist**. This skill defines **how to perform the review**: assemble the
change and its sources, evaluate each checklist item with the appropriate
capability, judge test validity, and produce structured findings a person or
downstream agent can act on. It does not restate the checklist.

## When to Use

- The review step of any `development/*`, `architecture/*`, or
  `documentation/*` workflow (`quality/review.md` §2 — never optional).
- Reviewing another agent's output (`.ai/README.md` §20: read its actual
  report and changes, do not assume).

## When Not to Use

- As a substitute for running tests or for validation-depth selection
  (`quality/testing.md`, `core/validation.md`).
- To redesign the solution. Review reports; it does not rewrite.

## Inputs

```text
Change set (diff/files) and the author's report (.ai/README.md §21 shape)
Documentation Context used for the task (core/context-discovery.md output)
Task classification and risk level (core/task-intake.md)
Validation plan (core/validation.md) and test results
```

## Prerequisites & Required Context

- The exact checklist in `quality/review.md` §1 (read it; do not rely on
  memory of it).
- Documentation Context for the touched domains; if absent, run
  `documentation-discovery`.

## Authoritative Sources

```text
quality/review.md §1–§3            checklist, non-optionality, output
core/validation.md §2              depth by risk
AGENTS.md §14–§16, §22             change process, testing, discipline, definition of done
.ai/README.md §13, §18, §21        stop format, classification, output contract
The documents the change was built against (per Documentation Context)
```

## Procedure

1. **Assemble the review set:** the change, the author's report, the
   Documentation Context, the risk level, the validation plan, and test
   results. Note anything the report claims that the change does not show.
2. **Determine depth** from `core/validation.md` §2; depth scales each
   checklist item, it never removes the checklist.
3. **Evaluate each `quality/review.md` §1 item**, delegating to the
   specialist skill where one exists:

   | Checklist item | How to evaluate |
   | --- | --- |
   | Correctness | `gameplay-behavior-derivation` (gameplay) or the contract skills (`api-contract-validation`, `realtime-protocol-validation`, `persistence-analysis`), compared with the change |
   | Architecture / Maintainability | `architecture-conformance` |
   | Scope | `scope-validation` (task-boundary mode) |
   | Tests | Judge against the validation depth; use `test-scenario-generation` to find missing scenarios; apply step 4 |
   | Documentation | `documentation-consistency` (Mode B/C) — does documentation still match the change, or was it changed correctly? |
   | Determinism (gameplay/battle only) | `authority-determinism-audit` |
   | Security / Performance | Evaluate directly against the specific concerns named in `quality/review.md` and the documents it cites (e.g. the persistence hot-path rule in `TDD.md` §4; authentication remains an open item per ADR-007) |

4. **Test-validity review.** For each new/changed test: does its expected
   result trace to a document (not to the implementation)? Was any test
   modified so the change passes? If a test and documentation disagree,
   classify per `.ai/README.md` §18 instead of picking a side.
5. **Reuse, do not repeat.** Consume existing skill reports from earlier
   steps; run a specialist skill only where no fresh report exists.
6. **Rate findings** (severity below), each with the affected file/section,
   the source-of-truth reference that makes it a finding, and the smallest
   recommended correction.
7. **Report unrelated problems separately** in the format from
   `AGENTS.md` §16 (issue · location · impact · suggested follow-up task); do
   not fold them into blocking findings.
8. **Conclude** per checklist item: pass / fail / not applicable (with
   reason), plus overall disposition: `Pass`, `Pass with follow-ups`,
   `Changes required`, or `Blocked` (a stop condition applies).

**Severity**

```text
Blocker   Violates a source-of-truth rule, server authority, MVP scope,
          determinism requirement, contract, or ADR; or hides a stop condition.
          Task cannot complete (core/completion.md §1).
Major     Likely incorrect behavior, missing required tests for the selected
          depth, boundary/architecture defect, undocumented behavior change.
Minor     Maintainability/clarity issue with no behavior or contract impact.
Note      Observation or follow-up candidate outside the task.
```

## Outputs

```text
Review Report
- Disposition: Pass / Pass with follow-ups / Changes required / Blocked
- Checklist result per quality/review.md §1 item: pass / fail / n/a (+ reason)
- Findings:
  | # | Severity | Affected file/section | Finding | Source-of-truth reference | Recommended correction |
- Test-validity findings
- Documentation status: consistent / update needed (which documents)
- Unrelated issues (AGENTS.md §16 format)
- Blocking stop condition (.ai/README.md §13 block) if any
```

This feeds the "Validation" and "Risks" sections of the final report
(`core/completion.md` §2; `quality/review.md` §3).

## Validation

```text
[ ] Every item of quality/review.md §1 was addressed (or marked n/a with reason)
[ ] Every finding cites a source-of-truth reference, not a preference
[ ] Severity assigned by the scale above, not by convenience
[ ] The reviewer verified claims in the author's report against the change
[ ] No finding recommends changing behavior only to satisfy a test
[ ] Unrelated problems are reported separately, not blocking
```

## Stop Conditions

- Any unresolved documentation conflict, missing rule, or scope violation
  surfaces during review → disposition `Blocked`; produce the STOP block.
- Client authority for a protected value is found — Blocker, and reported as a
  defect (`AGENTS.md` §10).
- The review set is incomplete (no change set, no Documentation Context, no
  tests where required) — report what is missing rather than reviewing on
  assumptions.
- The change contradicts an Accepted ADR or embeds an architecture decision
  without an ADR.

Baseline stop conditions in `.ai/skills/README.md` §6 also apply.

## Common Failure Modes

- Reviewing against the reviewer's taste instead of the documents.
- Trusting the author's summary instead of the change.
- Approving because tests pass while tests encode the implementation.
- Skipping checklist items at low risk (only depth may shrink).
- Restating game rules or values while explaining a finding.
- Letting scope creep through as "small improvements".
- Mixing unrelated issues into blocking findings.

## Traceability

```text
Used by:    quality/review.md (and therefore every development/*,
            architecture/*, and documentation/* workflow)
Reads:      quality/review.md; core/validation.md; AGENTS.md §14–§16/§22;
            .ai/README.md §13/§18/§21; docs per Documentation Context
Produces:   Review Report
Depends on: gameplay-behavior-derivation, authority-determinism-audit,
            api-contract-validation, realtime-protocol-validation,
            persistence-analysis, architecture-conformance, scope-validation,
            documentation-consistency, test-scenario-generation
            (consumed as existing reports; run only where none exists)
```
