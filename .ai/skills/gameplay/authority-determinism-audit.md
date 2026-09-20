# skills/gameplay/authority-determinism-audit.md — Skill: Authority & Determinism Audit

**Version:** 1.0

> For every piece of gameplay state a change touches, establish who owns,
> calculates, validates, and broadcasts it, and whether randomness and
> ordering are controlled — and report any client authority as a defect.

## Purpose

Enforce the server-authoritative model (ADR-001, `GAME_RULES.md` §18) and the
determinism requirements (`TDD.md` §6) as a repeatable check. It answers the
four mandatory questions of `.ai/README.md` §15 and the determinism checks of
§16.

## When to Use

- `core/implementation.md` §4 (mandatory for any gameplay/backend
  implementation).
- `quality/review.md` "Determinism (Gameplay/Battle Logic Only)".
- Any task touching battle simulation, hub methods that accept player input,
  client code that displays gameplay values, or RNG.

## When Not to Use

- Non-gameplay code with no battle state, RNG, or player-input handling.
- To derive *what* the behavior should be (`gameplay-behavior-derivation`).

## Inputs

```text
The change (diff, files, or proposed design)
The list of gameplay state/values involved (or derive it from the change)
Expected-Behavior Record (optional, from gameplay-behavior-derivation)
```

## Prerequisites & Required Context

- Knowledge of the three state categories in `GAME_STATE.md` §1–§4
  (authoritative battle state, transient resolution state, client
  presentation state).
- Layer boundaries from `ARCHITECTURE.md` §2–§4.

## Authoritative Sources

```text
AGENTS.md §10, §11, §13            authority, determinism, technical boundaries
.ai/README.md §15, §16             the four questions; determinism checks
GAME_RULES.md §17, §18             resolution order; what the client may send
GAME_STATE.md                      what state exists and who it belongs to; Sequence
TDD.md §2, §6                      responsibility split; RNG strategy
MATCH3_RULES.md §7 (Determinism), COMBAT_RULES.md §7, BOSS_RULES.md §8,
RELIC_RULES.md §4                  domain-level determinism/authority statements
SIGNALR_PROTOCOL.md §2, API_CONTRACTS.md §6   what requests may carry
ADR-001, ADR-004, ADR-005          decisions behind the model
```

## Procedure

1. **Enumerate state and values** the change reads, writes, computes, or
   displays. Classify each as authoritative state, transient resolution
   state, or client presentation state (`GAME_STATE.md`).
2. **Answer the four questions per item**, with evidence (file/line or
   document section):

   ```text
   Who owns this state?        (where does the source of truth live?)
   Who calculates it?
   Who validates the input that changes it?
   Who broadcasts it?
   ```

3. **Check the protected set** in `AGENTS.md` §10 (damage, HP, Boss HP, power,
   match result, combo, passive progress, rewards, RNG results, battle
   outcome). Any answer of "the client" for any of these is a **defect**.
   The only client-side gameplay computation the TDD permits is discardable,
   non-authoritative prediction for responsiveness (`TDD.md` §2.1); verify it
   is discarded on the server result and never sent as an authoritative value.
4. **Inspect the inputs.** Hub methods and REST endpoints must carry only
   *requests* (`GAME_RULES.md` §18); confirm none accepts or trusts an
   authoritative value from the client, and that every request is validated
   against its owning domain document before state changes.
5. **RNG audit** (`TDD.md` §6): identify every source of randomness that can
   affect gameplay; confirm it is the documented server-seeded mechanism,
   that the seed is part of active battle state, that no second randomization
   mechanism was introduced, and that nothing gameplay-relevant is random on
   the client.
6. **Ordering audit:** confirm resolution follows the fixed order
   (`GAME_RULES.md` §17); confirm trigger orders documented as deterministic
   (e.g. `RELIC_RULES.md` §4) do not depend on an unordered or
   environment-dependent source; confirm one action per battle is resolved at a
   time and `Sequence` semantics are preserved (`GAME_STATE.md` §5).
7. **Reproducibility:** confirm that seed + state + action sequence suffice to
   reproduce a resolution where the docs require it (recovered/replayed
   sessions, `TDD.md` §6).
8. **Layer check:** confirm the gameplay logic lives in the Domain/Application
   layers, not in Hub/Controller/Infrastructure or client code
   (`ARCHITECTURE.md` §2; hand structural findings to `architecture-conformance`).
9. **Verdict per item:** `OK`, `DEFECT` (client authority / uncontrolled RNG /
   nondeterministic order), or `UNKNOWN` (documents do not say who owns it).

## Outputs

```text
Authority & Determinism Report
| State / value | Category | Owner | Calculated by | Validated by | Broadcast by | Verdict | Evidence |
- Protected-set defects (client authority)
- RNG audit: sources, seed location, second-mechanism check, client RNG check
- Ordering audit: §17 conformance, deterministic-order dependencies, sequence handling
- Reproducibility assessment
- UNKNOWN items (document gap)
```

## Validation

```text
[ ] Every state/value touched by the change has all four answers or an UNKNOWN
[ ] Every protected-set item was explicitly checked (not only "changed" ones)
[ ] Every RNG source found in the change was traced to the documented mechanism
[ ] Every DEFECT cites the rule it violates (AGENTS.md §10 / GAME_RULES.md §18 / TDD.md §6)
[ ] No verdict rests on "the code probably…" — evidence recorded
```

## Stop Conditions

- **Client authority found** for a protected value → report as a defect;
  do not finish or "wire around" it (`core/implementation.md` §4).
- Ownership of a state item is undocumented (`UNKNOWN`) and the change depends
  on it → missing documentation; report, do not decide.
- RNG behavior is needed that the docs do not cover (`AGENTS.md` §11) —
  stop; do not invent a mechanism.
- The change would move a responsibility across a technical boundary
  (`AGENTS.md` §13) without an ADR (`architecture-conformance` /
  `architecture/adr-change.md`).

Baseline stop conditions in `.ai/skills/README.md` §6 also apply.

## Common Failure Modes

- Accepting client-computed damage/HP "because the server also computes it".
- Treating the optimistic-prediction allowance as license for client
  authority.
- Missing incidental RNG (crits, spawn, effect selection) outside the spawn
  path.
- Using wall-clock time or unordered collections in a place where ordering
  must be deterministic.
- Checking only the final value and not the intermediate trigger order.
- Logging an authority defect as a "TODO" instead of reporting it.

## Traceability

```text
Used by:    core/implementation.md §4; quality/review.md (Determinism);
            development/feature.md, bug-fix.md, gameplay-change.md
            (gameplay/backend/realtime tasks)
Reads:      AGENTS.md §10/§11/§13; .ai/README.md §15/§16; GAME_RULES.md §17/§18;
            GAME_STATE.md; TDD.md §2/§6; domain determinism sections; ADR-001
Produces:   Authority & Determinism Report
Depends on: documentation-discovery (only if context not already supplied);
            may consume Expected-Behavior Record
```
