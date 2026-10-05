# TASK-193 — Deterministic Balance Simulation Harness (Q-12 + Q-1 Specification)

<!--
  GEN-TASK EXECUTION MANIFEST — DECISION + IMPLEMENTED SPECIFICATION

  Records the Product Owner decisions Q-12 (harness approved) and Q-1
  (QUALITATIVE duration) from TASK-191 §6, and specifies the harness — which
  was then IMPLEMENTED as test-only tooling (§11).

  References docs/ and src/ by path and section — it does NOT copy game rules,
  formulas, magnitudes, schemas, or API payload shapes.

  SCOPE: test-only tooling + this specification. No gameplay rule changed, no
  balance value approved or changed, no production/API/frontend/database change.
-->

---

## Metadata

```text
Task ID:           TASK-193
Type:              DOCUMENTATION (decision + technical specification),
                   implemented as TEST INFRASTRUCTURE
Status:            DONE
Risk:              LOW (specification changes no behavior; the harness it
                   specifies is test-only tooling)
Priority:          HIGH (unblocks measurement for B-03, B-04, B-07, B-08,
                   B-10, B-13)
Primary Agent:     testing
Supporting Agents: gameplay, review
Workflow:          documentation/documentation-change.md
Skills:            testing/test-scenario-generation,
                   discovery/impact-analysis,
                   quality/scope-validation
Dependencies:      TASK-191 (the balance specification; owner of §6 Q-1/Q-12),
                   TASK-192 (DONE — B-02, so the harness measures the
                   post-B-02 cast economy)
Model:             Gemini 3.8
Reasoning:         High
```

---

## 1. Decision

```text
Q-12 = APPROVED
Q-1  = QUALITATIVE
```

**Q-12 — APPROVED.** A deterministic balance simulation harness is approved as
**test-only tooling**. It is not a gameplay feature, not a service, not a
production API, not a frontend feature, and not a database feature. Its purpose
is to repeatedly simulate the **existing** combat rules and report measurable
outcomes.

**Q-1 — QUALITATIVE.** No numeric fight-duration target is approved, and none is
invented. An exhaustive search of `docs/` found no authored duration, swap-count,
or wall-clock target (`GDD.md` §11–§12 describes damage flow and states difficulty
comes from mechanics; `MVP_SCOPE.md`, `GAME_RULES.md` §2, `COMBAT_RULES.md`, and
`BOSS_RULES.md` §7 are all silent on fight length). MVP balance therefore does
**not** enforce a numeric duration target: the harness reports duration as an
**observational metric**, and the Product Owner judges whether an outcome is
acceptable.

> **Why QUALITATIVE and not an invented number.** `AGENTS.md` §7 forbids
> inventing a gameplay rule, and a duration target *is* a gameplay/UX acceptance
> rule. `TASK-191` §5.4 and its Stop Conditions forbid presenting a candidate
> number as approved. Choosing a plausible-looking range (e.g. "40–60 swaps",
> which `TASK-191` §6 listed only as an *example option*) would have manufactured
> an approval the Product Owner never gave.

---

## 2. Rationale

`TASK-191` left ten items needing measurement before they could be judged:
B-03, B-04, B-07, B-08, B-10, B-13 (directly) plus B-11's evidence base, and
B-05/B-06's pacing questions. Each was marked `REQUIRES PRODUCT OWNER DECISION`
with a **Validation** method that begins "simulate …". No simulation capability
exists in the repository, so every one of those decisions currently resolves into
opinion rather than evidence.

The harness is the lowest-risk item in the set: it changes no rule and no balance
value, and it is test-only (`AGENTS.md` §15 — test-only tooling does not ship
gameplay logic). Approving it first means the remaining decisions can be made
against measured behaviour instead of estimates.

**TASK-191 §1.8's throughput figures are estimates, not measurements** — the
document says so itself ("Assumptions stated explicitly; every figure is a
candidate for simulation-based validation"). They must not be treated as
findings. Two of them have already proven directionally wrong under closer
reading: `TASK-192` changed the cast economy that §1.8 assumed, and the TASK-191
audit found Mộc Yêu's regeneration is applied per threshold crossing, which
§1.7/§1.8 did not account for. This is exactly the class of error the harness
exists to remove.

### Why no ADR

Determined against `docs/03-decisions/README.md` §2. The harness is **not**
architecturally important, not cross-cutting, not infrastructure-sensitive, and
not difficult to reverse: it is test-only code that no production, API, frontend,
or persistence layer depends on, and removing it would change no shipped
behaviour. The repository has never ADR'd its test infrastructure — there is no
ADR for the four existing test projects, `TestBoard`, `FixedRngSeedSource`, or
`InMemoryBattleStateRepository` — and `README.md` §2 excludes "a trivial
implementation detail" and anything duplicating a technical document. The
specification below is the record, per that rule. **No ADR is created.**

---

## 3. Harness Scope

### 3.1 What must be measured

Keep the metric set **minimal and sufficient for TASK-191**. Each metric below
exists because a specific TASK-191 item's validation method names it; nothing is
added because it is interesting.

| # | Metric | Serves |
|---|---|---|
| M-01 | Total Turns (swaps) to terminal outcome | B-03, B-04, B-06, B-12 |
| M-02 | Fight duration (Turns; wall-clock explicitly **excluded** — see §4) | Q-1 (observational) |
| M-03 | Player damage per Turn, and cumulative | B-03, B-04, B-08 |
| M-04 | Boss damage per Turn, and cumulative | B-04, B-07 |
| M-05 | Boss HP progression (per Turn, plus min/max) | B-03, B-07 |
| M-06 | Player (active Pet) HP progression | B-07, B-09 |
| M-07 | Power generated vs spent, and end-of-battle Power | B-01 (evidence), B-10 |
| M-08 | Cards cast, by Card and by Turn | B-01 (evidence), B-08, B-09 |
| M-09 | Relic trigger counts, by Relic | B-10 |
| M-10 | Combo distribution and Combo-threshold firings | B-11 |
| M-11 | Element modifier applications (advantage/neutral/disadvantage counts) | B-08 |
| M-12 | Boss regeneration applied (total HP restored) | B-07 |
| M-13 | Boss threshold events (enrage, HP-threshold passives, skill cadence) | B-03, B-07 |
| M-14 | Outcome: VICTORY / DEFEAT / STALEMATE / INVALID | B-03, B-07, all |
| M-15 | Resource state trace: HP/Power/status at chosen checkpoints | B-07, B-09 |

`TASK-191`'s B-14 item 3 also names an **expected-damage matrix** (Card × Boss)
for B-08 and a **relic ablation** (relic on vs off) for B-10. Both are
**aggregations over M-03/M-08/M-09**, not new metrics: they are reported by
running the same scenario with the relevant parameter varied (§5.2), not by
adding instrumentation.

### 3.2 Scripted player policies

`TASK-191` B-14 item 3 requires three policies. They are the *only* source of
player choice, and each must be deterministic:

```text
passive    commits the first valid swap found; never casts
average    commits a valid swap; casts when a Card is affordable
skilled    prefers high-tier / high-combo swaps; times casts and the Pet Skill
```

Selection is by a **fixed, documented rule** (e.g. lowest legal cell-pair index,
then a stated scoring criterion) — never by an uncontrolled random draw. A policy
that needs a tie-break draws it from the battle's own `RngState` so the run stays
reproducible.

> A policy is a **measurement instrument**, not a balance opinion. No policy may
> encode "the intended way to play" as an acceptance criterion.

### 3.3 Explicitly out of scope for the harness

- Adding or tuning any balance value.
- Any gameplay rule change.
- Any production/runtime, API, SignalR, frontend, or database change.
- Deciding whether a measured outcome is *acceptable* — that is §6's Q-1
  interaction and remains a Product Owner judgment.

---

## 4. Determinism Model

### 4.1 Inputs that fully determine a run

```text
seed
battle configuration      (BattleId-independent; boss identity, Pet identity)
player configuration
pet configuration
card configuration        (the 3 Basic + 1 Pet Skill loadout snapshot)
relic configuration       (3–5 equipped Relic definitions)
boss configuration
player policy             (§3.2)
```

Given identical values for all of these, the harness **must** produce an
identical result. This rests on machinery that already exists and is already
contracted:

- `MATCH3_RULES.md` §7 / §7.1 — the reproducibility guarantee: "same initial
  state + same ordered sequence of accepted actions = same final board + same
  `RngState` + same Match count + same Combo".
- `MATCH3_RULES.md` §7.2 — the closed list of what consumes RNG (cascade spawn,
  initial generation) and what must not.
- `GAME_STATE.md` §2.6 — the seed/state pair; `ADR-009` — the PRNG.
- `BattleState.CreateWith(battleId, rngSeed)` and the existing
  `FixedRngSeedSource` test double already pin the seed in tests.

### 4.2 Forbidden dependencies

The harness must **not** depend on:

- wall-clock timing, or any clock (`M-02` is measured in **Turns**, never
  milliseconds — a wall-clock duration would be non-deterministic and is
  therefore excluded from the contract);
- network or SignalR delivery timing;
- browser rendering or Phaser;
- uncontrolled randomness (`System.Random`, `Random.Shared`, `Guid`,
  hash-order-dependent iteration — `MATCH3_RULES.md` §7.2 item 4, `ADR-009` §2);
- any external service, including Redis and PostgreSQL.

`H-10` (§7) therefore requires the harness to be runnable entirely in-process.
If it needs battle state across steps, it uses the existing in-memory repository
test double, never the real Redis-backed store.

### 4.3 No second randomization mechanism

The harness introduces **no** new RNG. It consumes the battle's own `RngState`
through the existing Domain code (`AGENTS.md` §11, `ADR-009` §1). A separate
harness-level generator would make results unreproducible against production and
is prohibited.

---

## 5. Simulation Lifecycle

### 5.1 Per-run flow

```text
Initialize
  └── resolve configuration (§4.1) into a BattleState + loadout snapshots
        ↓
Configure deterministic state
  └── pin the seed; assert the initial invariants (Turn = 0, Sequence = 0,
      BoardState present, Pet/Boss at documented initial values)
        ↓
Simulate Turn
  └── choose an action per the policy (§3.2)
      → execute through the EXISTING resolution path (SwapExecutor /
        CardCastExecutor and the Application battle pipeline)
      → a rejected action is recorded and re-chosen per the policy, never
        forced through
        ↓
Collect metrics
  └── accumulate §3.1 metrics from the resolution's OWN outputs
      (resulting state + emitted events) — the harness computes no gameplay
      value of its own
        ↓
Continue
  └── loop until a terminal outcome or the safety limit (§5.3)
        ↓
Terminal outcome
  └── VICTORY | DEFEAT | STALEMATE | INVALID_SIMULATION
        ↓
Emit result
  └── the H-09 record (§7)
```

### 5.2 Modes

**Baseline.** Current production rules and current content values, unchanged.
This is the reference every comparison is made against. `H-03` requires it to
exercise the **actual** authoritative rules — the harness owns no re-implementation
of damage, resource generation, turn order, or any other rule
(`GAME_RULES.md` §17, §18; `AGENTS.md` §10). If the harness computes a combat
value itself, it is wrong by construction and its numbers are void.

**Controlled comparison.** Identical configuration except for **one** explicitly
selected parameter (`H-07`). This is how a future balance task produces
```
baseline vs candidate
```
evidence without disturbing unrelated variables. The harness must **not** contain
any candidate value: the varied parameter is supplied by the caller, so no
balance proposal is pre-approved or pre-judged by its existence.

### 5.3 Stalemate and the safety limit

The harness must distinguish exactly four outcomes, and **must never silently
treat `STALEMATE` as victory or defeat**:

```text
VICTORY             Boss HP reached 0
DEFEAT              active Pet HP reached 0
STALEMATE           the configured maximum Turn count was reached without either
INVALID_SIMULATION  the run could not proceed (malformed configuration, an
                    unrecoverable resolution error, or an invariant violation)
```

`H-08` requires a **hard maximum Turn/step limit** so a broken balance
configuration cannot hang. The limit must be **configurable per run** with a
documented safe default chosen by the implementing task.

> **The limit is a technical safety bound, not a gameplay balance value.** It is
> chosen for termination, not for difficulty, and it approves nothing. A run that
> hits it reports `STALEMATE`, which is *evidence for* B-07 — not a decision
> about it.

---

## 6. Q-1 Interaction

Because **Q-1 = QUALITATIVE**, the interaction is:

```text
simulation result
    ↓
observational evidence (M-01/M-02 and the §3.1 metric set)
    ↓
Product Owner decides whether the outcome is acceptable
```

The harness must therefore **not** encode any pass/fail duration threshold, any
target range, or any automated accept/reject judgement. It reports; a human
decides.

Had Q-1 been an explicit target, the corresponding flow would have been
`simulation result → compare against the approved target → pass/fail`. That
comparison is **not** implemented, because no target was approved.

This keeps the harness on the correct side of `AGENTS.md` §7: it introduces no
gameplay rule and approves no value.

---

## 7. Acceptance Criteria

For the **future implementation task**. Every criterion is checkable.

```text
[ ] H-01  Same seed + same configuration ⇒ byte-identical result across runs
          (and across processes).
[ ] H-02  A different seed may produce a different board sequence where
          randomness is relevant — proving the seed actually drives generation
          and the harness is not accidentally fixed.
[ ] H-03  The baseline simulation exercises the actual authoritative production
          rules: it calls the existing Domain/Application resolution paths and
          re-implements no damage, resource, turn-order, or effect rule.
[ ] H-04  The harness does not modify production gameplay behavior: no
          production source file changes, and the full existing test suite
          still passes unchanged.
[ ] H-05  The harness can execute enough Turns to reach and distinguish
          victory, defeat, and stalemate/non-termination.
[ ] H-06  The harness reports every §3.1 metric (M-01…M-15) for a run.
[ ] H-07  A comparison run changes only the explicitly selected variable;
          every other input is provably identical to the baseline.
[ ] H-08  A hard, per-run-configurable maximum Turn/step limit exists, with a
          documented safe default, so a broken configuration cannot hang.
[ ] H-09  Each result identifies: seed, configuration, policy, outcome, Turns,
          duration metric, and the relevant combat metrics — enough for a human
          to reproduce the run from the record alone.
[ ] H-10  The harness runs from the repository's existing test/tooling workflow
          (e.g. `dotnet test` on a test project) with no production dependency,
          no external service, and no network access.
```

---

## 8. Non-Goals

Explicitly excluded, and each is a scope violation if the implementation does it:

- **Balance tuning.** The harness measures; it never changes or proposes a value.
- **Changing gameplay rules.** `GAME_RULES.md` §17/§18 and every domain rule are
  consumed as-is, never modified.
- **Production runtime changes.** No production source file is touched (`H-04`).
- **Frontend changes.** No Phaser, React, or presentation code.
- **Database changes.** No schema, no migration, no EF entity.
- **B-01.** Not implemented, not evaluated, not re-scoped. Power Charge's cost is
  untouched and remains a rule-change decision (`CARD_RULES.md` §2 item 3).
- **B-03 and every later balance proposal.** Not implemented, not evaluated, not
  approved. The harness may *support* analysis of B-03/B-04/B-07/B-08/B-10/B-13,
  but this specification concludes nothing about any of them.
- **Automatic approval or rejection of future balance values.** No thresholds, no
  pass/fail gates, no automated verdicts (§6).
- **A balance simulation "service" or dashboard.** Out of scope; `MVP_SCOPE.md`
  §2 governs, and this is test tooling, not a product surface.

---

## 9. TASK-191 Updates Made

Only the Q-1 and Q-12 records (and the directly-dependent BT-6 / B-14 item 3
lines) were updated in `tasks/backlog/TASK-191-balance-pass.md`:

```text
§6 Q-1   → DECIDED — OPTION B: QUALITATIVE
§6 Q-12  → APPROVED
§2 BT-6  → DECIDED — QUALITATIVE
§3 B-14 item 3 → ✅ APPROVED (Q-12), pointing here
```

**Not changed:** B-01, B-03, B-04, B-05, B-06, B-07, B-08, B-09, B-10, B-11,
B-12, B-13, and B-14 items 1–2. None is approved, no proposed value is modified,
and no classification is altered. Q-2, Q-3, Q-5, Q-6, Q-7, Q-8, Q-9, Q-10, and
Q-11 remain **OPEN**.

---

## 10. Remaining Decision Queue (unchanged by this task)

```text
Q-2 / B-01   OPEN — rule-change decision; Power Charge unchanged
Q-3 / B-03   OPEN — boss stat curve
Q-3 / B-04   OPEN — boss DEF asymmetry
Q-5 / B-08   OPEN — Card/Relic normalization requirement
Q-5 / B-09   OPEN — Heal/Shield pairing
Q-5 / B-10   OPEN — relic magnitudes
Q-6 / B-05   OPEN — pet Tier/Star/Level curve
Q-7 / B-06   OPEN — player XP pacing
Q-8 / B-07   OPEN — Mộc Yêu regeneration
Q-9          OPEN — damage tolerance band
Q-10         OPEN — relic Power engines
Q-11         OPEN — Xích Lang / Sơn Hùng passive magnitudes
B-11, B-12, B-13   DEFERRED (no evidence of a problem)
B-14 items 1–2     OPEN — pet-passive effect implementation (D-2); D-1 comments
```

Q-1 and Q-12 are the **only** items this task resolves. Note the interaction:
Q-12 unblocks *measurement* for B-03/B-04/B-07/B-08/B-10/B-13, but each of those
still requires its own Product Owner decision before any value changes — the
harness is evidence, not approval.

---

## Completion Evidence

### Deliverables
- `tasks/backlog/TASK-193-deterministic-balance-simulation-harness.md` — NEW:
  this decision and specification.
- `tasks/backlog/TASK-191-balance-pass.md` — Q-1, Q-12, BT-6, and B-14 item 3
  records only.

### Scope
- Zero gameplay, production, API, SignalR, auth, frontend, database, or migration
  changes. No ADR created (§2).
- No balance value approved, changed, or proposed. No duration target invented.
- B-01 and B-07 not touched.

### Next Step
The harness implementation is now unblocked as its own task. It must be executed
per §3–§7 above, and it reports evidence only.

---

## 11. Implementation Evidence

The harness specified above was implemented by this task as **test-only tooling**
(TASK-193 §8's non-goals all hold; see the scope audit below).

### Location and entry point

```text
tests/backend/GameServer.Application.Tests/Balance/
```

| File | Role |
|---|---|
| `BalanceSimulator.cs` | Entry point: `RunAsync()`. Drives the production `BattleStateService`. |
| `BalanceSimulationConfiguration.cs` | The complete, immutable input set (§4.1). |
| `BalanceSimulationMetrics.cs` | M-01…M-15 plus the M-15 checkpoint record. |
| `BalanceSimulationResult.cs` | The H-09 record. |
| `BalanceSimulationMode.cs` | Baseline / ControlledComparison (§5.2). |
| `BalanceSimulationOutcome.cs` | The four §5.3 outcomes. |
| `BalancePlayerPolicy.cs` | The three §3.2 policies. |
| `MetricAccumulator.cs` | Sums and counts the pipeline's own outputs. |
| `Tests/BalanceSimulatorTests.cs` | H-01…H-10 (32 tests). |

**H-10:** the harness lives in the existing `GameServer.Application.Tests`
project, which already owns the deterministic infrastructure it needs
(`InMemoryBattleStateRepository`, `FixedRngSeedSource`). **No new test project and
no new production dependency was introduced.**

### Architectural rule honoured

The harness **reimplements no gameplay**. It calls
`BattleStateService.CreateBattleAsync` / `ExecuteSwapAsync` /
`ExecuteCardCastAsync`, which run the real `SwapExecutor`, `CardCastExecutor`,
`DamagePipeline`, `ResourceGenerator`, `RelicResolver`, `PassiveTracker`, and
Boss Response. Every metric is summed or counted from the resolutions' committed
state and emitted events — never recomputed. Damage is summed from `DamageDealt`
alone, because `GAME_EVENTS.md` §2 makes `DamageDealt`/`DamageTaken` two reports
of **one** instance; summing both would double every value.

### Outcome determination

`VICTORY`/`DEFEAT` are read from the production pipeline's own `BattleWon`/
`BattleLost` outcomes (`GAME_EVENTS.md` §2) rather than inferred from HP, so the
harness cannot disagree with the rules. `STALEMATE` is the configured Turn limit
reached without either, and is never folded into victory or defeat.
`INVALID_SIMULATION` is reserved for a configuration the simulation cannot
legally run and is validated before any battle state exists.

### The Turn limit

`MaxTurns` defaults to 1000 and is configurable per run. It is a **technical
safety bound, not a gameplay balance value** — documented as such in
`BalanceSimulationConfiguration.MaxTurns` — and it approves nothing.

### Per-Turn aggregation (a correctness property worth recording)

A committed Match-3 Turn can contain **two** resolutions: one auxiliary Card cast
(`CARD_RULES.md` §3 item 6) and the committed Swap. Metrics are therefore
accumulated per resolution (`RecordResolution`) and flushed once per Turn
(`EndTurn`), so a Turn with both reports one entry per per-Turn series and counts
each damage instance once.

### Verification

```text
Focused harness tests        32 passed, 0 failed
Domain.Tests               1558 passed, 0 failed
Application.Tests           593 passed, 0 failed   (561 baseline + 32 new)
Infrastructure.Tests        412 passed, 0 failed
Api.Tests                   323 passed, 0 failed
                          -----
Backend total              2886 passed, 0 failed

Frontend tests              615 passed (18 files)
Backend build               succeeded (Domain, Application, Infrastructure)
```

Acceptance criteria: **H-01…H-10 all pass** (§7), each covered by at least one
dedicated test.

### Scope audit

```text
Gameplay production code changed:  NO  (verified: no file under src/ modified)
Production API changed:            NO
Frontend changed:                  NO
Database / migrations changed:     NO
Balance values changed:            NO
B-01 changed:                      NO  (card-power-charge still PowerCost 0)
B-07 changed:                      NO  (Mộc Yêu 5% MaxHP regen untouched)
Other TASK-191 balance proposals:  NOT IMPLEMENTED
Authoritative gameplay docs:       UNCHANGED
ADR:                               none created (§2) or modified
```

The only files this task created are the nine harness files above plus this task
record. Verified by modification timestamp: **no file under `src/` was touched.**

### What this does NOT do

The harness **measures**; it does not decide. Q-1 is QUALITATIVE, so there is no
approved duration target, no pass/fail threshold, and no
`BALANCED`/`UNBALANCED`/`PASS`/`FAIL` output — the outcome enum holds exactly the
four §5.3 values, asserted by `H09_TheResultReportsNoBalanceVerdict`. B-01, B-03,
B-04, B-07, B-08, B-10, B-13 and every other TASK-191 proposal remain **OPEN**;
this task approves none of them.
