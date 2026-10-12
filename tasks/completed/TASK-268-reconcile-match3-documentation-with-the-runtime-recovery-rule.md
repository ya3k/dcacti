# TASK-268 — Reconcile Match-3 Documentation With the Runtime Recovery Rule

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  Origin: corrective documentation slice raised by the independent post-commit
  review of TASK-267 (AGENTS.md §16 report-and-follow-up; TASK-229 precedent).
-->

---

## Metadata

```text
Task ID:           TASK-268
Type:              DOCUMENTATION
Status:            DONE
Risk:              MEDIUM
Priority:          HIGH
Primary Agent:     review
Supporting Agents: gameplay, backend
Workflow:          documentation/documentation-change.md
Skills:            quality/documentation-consistency,
                   quality/scope-validation,
                   quality/architecture-conformance
Dependencies:      TASK-267 (DONE)
Declared Files:    docs/01-game-design/MATCH3_RULES.md,
                   docs/01-game-design/GAME_RULES.md,
                   docs/02-technical/GAME_STATE.md,
                   docs/02-technical/GAME_EVENTS.md
```

---

## Objective

Close the documentation-consistency defects left by TASK-267's implementation
slice, without changing any code, rule, contract, or test:

1. `MATCH3_RULES.md` §7.2's closed list of RNG consumers — "These are the **only**
   board operations that consume randomness" — is false for the shipped code
   because §1.6.3 mandates commit-boundary regeneration over the battle's stream.
   The list and its exclusivity claim must record the recovery (and the
   pre-existing `ADR-026` §8 Boss pre-move reshuffle, which strained it earlier).
2. Three citation/consistency defects inside `MATCH3_RULES.md`: §1.6.4 item 3's
   scope citation, §1.6.5 item 1's `GAME_RULES.md` §17 step 18a citation, and
   §2.1.2's untouched "same reason when more than one check could fail" ordering
   versus §2.1.4 item 7's pre-empting terminal guard.
3. `MATCH3_RULES.md` §1.6.3 does not state that the regenerated board is filled
   from §1.1's four Gem types only, so every placed Special Gem on the recovered
   board is discarded.
4. `MATCH3_RULES.md` §1.6.4 item 1's justification ("the Player never acts again")
   is broader than the shipped code, which guards Swap only.
5. `GAME_EVENTS.md` §3 item 8 (cited by `MATCH3_RULES.md` §1.6.6 item 1 and by
   `ADR-026` §8 as the "no event" record) names only the Boss Response phase's
   reshuffle as the documented case.
6. None of the four edited documents bumped its Version header, so their revision
   histories omit the TASK-267 change — contrary to the repository's own
   convention.

---

## Authoritative References

- `docs/AGENTS.md` §4 (conflict resolution: report, do not silently resolve), §17 (documentation change rule)
- `AGENTS.md` §2 (source-of-truth hierarchy), §9 (anti-overengineering), §16 (report, do not fix inline), §17
- `docs/01-game-design/MATCH3_RULES.md` §1.2.1.4, §1.6, §2.1.2, §2.1.4, §4.5, §7.2
- `docs/01-game-design/GAME_RULES.md` §17 (step 18a), §20 (rule change policy)
- `docs/02-technical/GAME_EVENTS.md` §2, §3 item 8
- `docs/02-technical/GAME_STATE.md` §2.6.2, §5.1
- `docs/03-decisions/ADR/ADR-026*` §8; `docs/03-decisions/ADR/ADR-009*`
- `tasks/completed/TASK-267-player-side-deadlock-recovery-and-terminal-action-guard.md` (the slice whose review raised this work)

---

## Scope

### In Scope
- Documentation corrections only, in the four declared files.
- Recording the recovery in §7.2's consumer list and scoping its exclusivity rule.
- Correcting citations, the §2.1.2/§2.1.4 item 7 ordering pointer, the Special Gem
  consequence sentence in §1.6.3, and the §1.6.4 justification wording.
- Extending `GAME_EVENTS.md` §3 item 8 to name the commit-boundary recovery as the
  second documented case of an operation that emits no Battle Event.
- Version-header bumps with a one-line entry each.

### Out of Scope
- Any code, test, rule, contract, wire, RNG, generator, balance, or scope change.
- The Card/Pet-skill terminal-action question (observation 8 of the TASK-267
  review): whether a cast is refused after the battle ends is a **gameplay rule
  decision for the Product Owner**, not a documentation correction, and no
  authoritative document states it. Reported, not decided here.
- `ResolveBoardAsync`'s absent invariant check and the retry-exhausted path's
  missing terminal persistence (observations 9–10): code findings requiring their
  own tasks.

---

## Current State

TASK-267's Phase A slice (`439d34c`) implemented `MATCH3_RULES.md` §1.6 and
§2.1.4 item 7 and updated `GAME_RULES.md` §17, `GAME_STATE.md` §5.1, and
`ADR-026` item 8. `MATCH3_RULES.md` §7.2 was not reconciled with the new runtime
RNG consumer, and the four edited documents did not bump their Version headers.

---

## Declared File Set (P-2 / P-5)

Exact declared implementation files for TASK-268:
```text
docs/01-game-design/MATCH3_RULES.md
docs/01-game-design/GAME_RULES.md
docs/02-technical/GAME_STATE.md
docs/02-technical/GAME_EVENTS.md
```

*(Note: the task manifest file itself is excluded from the implementation declared
file set. `docs/03-decisions/ADR/ADR-026*` is deliberately NOT declared: its item 8
citations remain accurate once `GAME_EVENTS.md` §3 item 8 names both cases.)*

---

## Acceptance Criteria

- [x] `MATCH3_RULES.md` §7.2 lists runtime deadlock recovery as an RNG consumer and
      its exclusivity claim is scoped to that list, so the section is true for the
      shipped code.
- [x] The three `MATCH3_RULES.md` citation/ordering defects are corrected, and the
      `§2.1.2` order text points at `§2.1.4` item 7's carve-out for a battle that
      is no longer in progress.
- [x] `MATCH3_RULES.md` §1.6.3 states that a regenerated board is built from §1.1's
      four Gem types only, so placed Special Gems are discarded.
- [x] `MATCH3_RULES.md` §1.6.4's justification is narrowed to what §2.1.4 item 7 and
      the code actually establish.
- [x] `GAME_EVENTS.md` §3 item 8 names both documented cases.
- [x] All four documents carry a bumped Version header with a one-line entry.
- [x] No code, test, rule, contract, wire, or gameplay behavior changes; the only
      files touched are the declared set.
- [x] Quality review checklist passes (`quality/review.md` §1)
- [x] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001)

---

## Affected Files & Areas

```text
[ ] src/backend/ — not touched
[ ] src/frontend/client/ — not touched
[ ] tests/ — not touched
[x] docs/ (four documents)
```

---

## Implementation Notes

None. Each correction is one to four lines of prose; no rule, magnitude, or
contract is redefined, and every edit cites the owning section it must agree with.

---

## Testing Requirements

### Required Verification
```text
[ ] Unit tests         — not applicable (documentation-only task)
[x] Integration tests  — not applicable (no code)
[x] Gameplay scenarios — not applicable (no behavior)
[x] Documentation validation — grep-based consistency checks: the stale exclusivity
    sentence is gone, the corrected citations resolve to sections that support them,
    §7.2's list matches the code's RNG consumers (cascade spawn, initial generation,
    commit-boundary recovery, ADR-026 §8 pre-move recovery), and the four Version
    headers are bumped
```

### Key Edge Cases
- `MATCH3_RULES.md` §7.2 item 2's "must not consume RNG" list must stay true: none of
  the corrected text may imply that validation, detection, or the guard consumes RNG.

---

## Stop Conditions

- If required behavior cannot be fully derived from Authoritative References: STOP per `AGENTS.md` §7
- If a correction would change a rule rather than a citation: STOP and report per
  `docs/AGENTS.md` §4 (`AGENTS.md` §20 rule conflict) instead of editing it

---

## Completion Evidence

### Commit
- Implementation slice (Phase A): `0947418bf1b0aec90b99cbc3d74cf7f109c9caf8`
- Subject: `docs(match3): reconcile the rule docs with the runtime recovery rule`
- Owns: `TASK-268`
- Files: `docs/01-game-design/MATCH3_RULES.md`, `docs/01-game-design/GAME_RULES.md`,
  `docs/02-technical/GAME_STATE.md`, `docs/02-technical/GAME_EVENTS.md`
- Shared with: `none`
- Unowned / pre-existing: `.ai/HARNESS_COMPATIBILITY.md`, `.ai/HARNESS_MAPPING.md`,
  `.ai/agents/AGENT_SELECTION.md`, `.ai/agents/orchestrator.md`, `.ai/skills/README.md`,
  `.ai/skills/SKILL_REGISTRY.md`, `.ai/workflow/README.md`, `.gitignore`, `.agents/`,
  `.ai/PROJECT_STATE.md`, `.ai/skills/discovery/task-readiness-assessment.md`,
  `.ai/workflow/core/task-selection.md`, `.claude/` — disclosed in the Phase A commit
  body and **not staged**; none was edited by this task.

### Changed Files
- `docs/01-game-design/MATCH3_RULES.md` — §7.2's consumer list gains runtime deadlock
  recovery and its exclusivity claim is scoped to that list (with the `ADR-026` §8
  pre-move recovery named as the same mechanism at a second evaluation point);
  §1.6.3 item 5 now states that a replacement board is filled from §1.1's types only and
  therefore preserves no Special Gem, and item 4 distinguishes a recovery failure
  (refusing the Round) from §1.5 item 3's creation failure (refusing the battle);
  §1.6.4 item 1's justification is narrowed to what §2.1.4 item 7 and the code
  establish, with a new item 4 recording that the exemption decides nothing about Card
  or Pet Skill casts; §1.6.5 item 1's citation no longer attributes the recovery to
  §17 step 18a's own text; §2.1.2's ordering paragraph now points at §2.1.4 item 7's
  terminal pre-emption; Version 1.7 → 1.8.
- `docs/01-game-design/GAME_RULES.md` — Version 3.5 → 3.6 recording the §17
  playable-board paragraph (added by TASK-267).
- `docs/02-technical/GAME_STATE.md` — Version 2.26 → 2.27 recording the §5.1 item 8
  and diagram line (added by TASK-267).
- `docs/02-technical/GAME_EVENTS.md` — §3 item 8 now names `MATCH3_RULES.md` §1.6's
  commit-boundary recovery as the **second** documented case of an operation that emits
  no Battle Event, with the item's rule and the closed discriminator set unchanged;
  Version 2.13 → 2.14.

### Validation Results
This task changes documentation only — no production, test, or schema file is touched
(`git diff --cached --name-status` = 4 modified docs; `git status` for `src/` and
`tests/` is empty). Unit, integration, and gameplay-scenario layers are therefore not
applicable, and none is claimed. What was verified, by direct inspection of the
committed text:

- **The MAJOR defect is closed.** `MATCH3_RULES.md` §7.2's list now names three
  consumers — cascade spawn (§4.5 item 4), initial board generation (§1.2.1.4), and
  runtime deadlock recovery (§1.6.3 item 3) — and item 1's "only" claim is scoped to
  that list, so the section is true for the shipped code. The list matches the code's
  RNG consumers exactly: `CascadeResolver`'s spawn, `BoardGenerator.Generate` at battle
  creation, the same generator in the commit-boundary recovery, and (per `ADR-026` §8)
  the Boss's pre-move recovery, which the entry's own text names.
- §7.2 item 2's "must not consume RNG" list is byte-unchanged, so the corrected text
  implies no RNG consumption by validation, detection, the terminal guard, or any
  cascade-free step.
- Each corrected citation resolves to a section that supports it: §2.1.4 item 7 exists
  and fixes the terminal-battle rejection; §1.6.1 item 3 is the invariant's scope
  statement; §1.6.3 items 3–5 exist and carry the mechanism, the bound and the
  Special-Gem consequence; `ADR-026` §8 owns the pre-move recovery and the server-internal
  reporting.
- All four Version headers are bumped with a one-line entry naming TASK-267's change,
  and each retains its "Prior …" chain.
- No rule, magnitude, formula, contract, wire member, event, rejection code, RNG
  algorithm, generator algorithm, retry bound, or implementation behavior is changed;
  the only semantic additions are statements of behavior the shipped code already had.

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic: no client, protocol, or
      gameplay text was weakened, and nothing here describes a client-computed value.
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1): no new system, content,
      mechanic, event, or wire member is introduced or promised.
- [x] Confirmed P-1…P-6 commit policy compliance: Phase A staged exactly the four
      declared files (`git diff --cached --name-status`), in one commit for the slice,
      with a conventional subject carrying no task ID and a body carrying
      `owns:`/`files:`/`shared with:`/`unowned/pre-existing:`; the 13 unowned/pre-existing
      paths are disclosed and were not staged, and `tasks/artifacts/` was clean at
      staging time (0 modified).

### Review Pass (`quality/review.md` §1)

- **Correctness — PASS.** Each edit was checked against the section it must agree with
  and against the shipped code: §7.2's list equals the code's RNG consumers, the
  corrected citations resolve to sections that state what is attributed to them, and no
  corrected sentence asserts behavior the code does not have.
- **Architecture — PASS.** No layer, boundary, contract, or state model is described
  differently; the ownership recorded by TASK-267 (`MATCH3_RULES.md` §1.6 as the rule's
  owner, `ADR-026` §8 for the server-internal rationale) is unchanged.
- **Scope — PASS.** Four declared files, documentation only; `src/` and `tests/` are
  untouched, and the Card/Pet-cast question was left for a Product Owner decision rather
  than answered by prose.
- **Tests — not applicable.** No behavior changes, so no test layer applies; the
  verification performed is documentation validation (above) and is described as such.
- **Documentation — PASS.** This task exists to remove an unresolved documentation
  conflict; the conflict is closed, and the four revision histories now record
  TASK-267's change.
- **Security — PASS.** Nothing touched.
- **Performance — PASS.** Nothing touched.
- **Maintainability — PASS.** Prose only; the corrected text points at the owning
  sections instead of restating rules, so no second source of truth is created.
- **Determinism — PASS.** The RNG-consumer list is the section that bounds determinism;
  making it exhaustive and true strengthens rather than weakens it.

### Remaining Issues
1. **Reported, not decided (needs a Product Owner call).** Whether a **Card cast** or a
   **Pet Skill cast** is refused after the battle has ended is a gameplay rule no
   authoritative document states, and `ExecuteCardCastAsync` / `ExecutePetSkillCastAsync`
   have no terminal guard (`BattleStateService.cs`). TASK-267's D4 scope is the Swap, and
   such a cast resolves no Swap, reaches no cascade, and never writes `BoardState`, so no
   deadlock or invariant violation arises. `MATCH3_RULES.md` §1.6.4 item 4 now records
   explicitly that the exemption decides nothing about casts.
2. **Code observations from the same review, left for their own tasks (reported, not
   fixed — `AGENTS.md` §16).** (a) `ExecuteSwapAsync`'s retry-exhausted path commits a
   terminal result without calling `PersistTerminalResultAsync`, which is what can leave
   a terminal record in Redis; (b) `ResolveBoardAsync` commits a board with no invariant
   check (test-only callers today); (c) the test-rigor findings recorded in TASK-267's
   completion record remain follow-up work.
3. **`.gitignore` residue (unowned, not this task's).** The pre-existing unowned
   `.gitignore` change adds `scripts`, which hides new files in a directory `CLAUDE.md`
   documents as part of the repository (`scripts/standalone-web-smoke.mjs` is tracked).
   Its owner should justify or drop it; this task did not touch it.
