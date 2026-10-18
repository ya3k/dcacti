# TASK-298 — Match-Only Combat Economy and Balance Tuning

- **Task ID:** TASK-298
- **Type:** Gameplay balance investigation, controlled tuning experiments, and evidence
- **Status:** Completed — Assessment & Experiment Evidence; **no production parameter changed**
- **Final status:** `PARTIAL — ADDITIONAL TUNING REQUIRED`
- **Dependencies:** TASK-194, TASK-197, TASK-296, ADR-026, ADR-028, TASK-290..TASK-293

---

## 0. Preflight Record

```text
branch                 master
HEAD                   a6fc8b102da6742003599fd7167647001913afe8
                        (test: correct balance metric test assertions and document baseline verification)

working-tree status    M  docs/03-decisions/README.md
                       ?? docs/03-decisions/ADR/ADR-028-retire-special-gems-from-battle-state-model.md
                       — both operator-owned; preserved untouched, never staged
```

**Operator-owned files preserved (byte-identical, never staged, never written):**

| File | SHA-256 | State |
|---|---|---|
| `docs/03-decisions/README.md` | (operator-owned) | left `M`, untouched |
| `docs/03-decisions/ADR/ADR-028-…battle-state-model.md` | (operator-owned) | left `??`, untouched |
| `tasks/artifacts/TASK-194-baseline-simulation-results.json` | `DC62B91DC1777590016BCE39E4480BBC0EB20050B462B4F4EE06E1C578A68582` | unchanged |
| `tasks/artifacts/TASK-197-controlled-balance-experiments.json` | `AA3D87ED523C44D835D6FBE722FD59A4DEA50835B5ABC8F4A81B9A404140E08A` | unchanged |

**A defect was caught and repaired during this task.** The first full-suite run
(`dotnet test src/backend/GameServer.sln`) executed the pre-existing TASK-194 and
TASK-197 archival tests, which **rewrote both pinned artifacts in place** —
`TASK-194` with a 358,567-line diff and `TASK-197` with an 8,451,061-line diff.
This is precisely the outcome §1/§6 forbid. Both files were restored from `HEAD`
with `git restore` and re-hashed to the values above; the restored hashes match
the pristine `HEAD` content. **No commit, push, or deploy occurred**, so nothing
escaped. This is reported as a finding in §9 — the archival tests are not
read-only and will do this again to any future task that runs the full suite.

**TASK-297A does not exist in this repository.** No file under `tasks/` or
`docs/` references `TASK-297`. The highest-numbered completed task is
`TASK-296-balance-metric-semantics-and-rebaseline-candidate-verification.md`.
Its §3 "Canonical 75-Run Verification" is the sole prior match-only evidence
and is treated as the TASK-297A evidence base; a fresh identification task is
recommended in §9.

---

## 1. Understanding

The match-only gameplay rules (ADR-026 shared board, ADR-028 special-gem
retirement, TASK-290..293) produced a combat economy that does not resolve
encounters. This task was asked to find **why**, test whether a small set of
parameter trims can fix it, and report evidence — not to force a target
percentage.

The work splits into four parts: reproduce the reported failures from the
hash-verified TASK-296 candidate, root-cause them in code and data, run a
controlled experiment matrix, and report whether the parameters alone are
sufficient.

---

## 2. Baseline Reproduction (verified, not assumed)

The exact TASK-296 candidate was located and hash-verified before any work:

```text
tests/backend/GameServer.Application.Tests/bin/Debug/net10.0/
    TASK-296-canonical-75-candidate.json
SHA-256  A9E9ABBD9B342C9B8849E5CECB1580D3154915782F539F5A3547252E4EE4FC62   ✔ matches
```

All four reported failure patterns **reproduced exactly**:

| Reported pattern | Verified | Note |
|---|---|---|
| Average policy 25/25 stalemates | ✅ 25/25 | every one exactly Turn 1000 |
| Four changed runs reaching Turn 1000 | ✅ | actually **26** total stalemates |
| Zero victories vs Hỏa Long / Thủy Ma / Mộc Yêu | ✅ 0/15 each | victories only on Sơn Thạch Vệ (3), Kim Lôi Vương (5) |
| Skilled vs Sơn Thạch Vệ in hundreds of turns | ✅ 378 / 233 / 295 | on the three victory seeds |

Baseline distribution: **Victory 8, Defeat 41, Stalemate 26, InvalidSimulation 0.**

---

## 3. Root-Cause Analysis

### 3.1 The Average sustain loop is real but is **not** the binding constraint

`card-heal` costs 20 Power and restores 20% MaxHP (200). `card-power-charge`
costs **0** Power and grants **+25** (`CARD_RULES.md` §2 item 3 mandates it
"must never be blocked by insufficient Power"). Power is clamped 0–100, so a
policy that casts Power Charge can always replenish the 20 Power a Heal needs.
Sustainability threshold: **7.5 HP per Power**.

**But the published figure is 26.1 HP/Power, and that is arithmetically
unreachable from the loop.** A full Power Charge→Heal cycle nets +5 Power for
200 HP — i.e. 40 HP/Power, not 26.1. The 26.1 figure is not a per-loop ratio; it
conflates board-generated Power with cast spending. The actual measured loop is
**exactly 40 HP/Power**, which is what the experiment matrix subsequently
confirmed (§5, E1).

### 3.2 The binding constraint: the match-only board economy is ~0.5% of Boss MaxHP per Turn

Measured steady state, Average policy, averaged over all 25 Average runs:

```text
player damage to boss   = 38.46 / Turn
boss self-regeneration  =  9.17 / Turn   (HP-gem matches + Mộc Yêu passive)
NET progress to boss    = 29.29 / Turn
Boss MaxHP              = 3000..5000
⇒ time to kill          = 102..171 Turns, i.e. 3–5× the entire 1000-Turn budget
```

This is why **Average never wins a single run in any experiment, and why its
Boss-HP floor never drops below 47%** — the Boss is never remotely close to
enrage. The lowest floor observed across every single experiment is
**47.4%** (Kim Lôi Vương, the 2800-HP Boss). Enrage thresholds are 30%/50%/75%;
only Kim Lôi Vương's 75% is even approached.

### 3.3 Boss HP-gem healing is a structural opponent

The Boss heals from its own HP-gem matches (20 HP/gem, 60/Hp match) under
ADR-026 D2's actor-relative rules. Measured as a fraction of the player's total
damage: **10–30%** of all player damage is cancelled by Boss regeneration. This
is symmetric with the Pet's own HP-gem healing, so it is a *structural*
consequence of ADR-026, not a tuning slip.

### 3.4 Boss DEF is a real but secondary tax

`K/(K+DEF)` at DEF 50 = **0.667** — a flat −33% on the player's only scalable
damage source, with no player-side counterplay (match-only rules give the player
no DEF-ignoring or DEF-reducing option). Removing it (E3) recovered **+3
victories** (8→11), all on Hỏa Long. Material, but nowhere near sufficient.

### 3.5 Why the Boss kills the Pet so consistently

The Boss's ATK is only **3.3%** of its own MaxHP (100/3000–5000) while the Pet's
ATK is **5%** of its MaxHP (50/1000) — but the Boss deals ~87 damage/Turn against
a 1000-HP pool (≈11.5 Turns to kill), while the Pet deals ~29 net/Turn against a
3000–5000-HP pool (102–171 Turns). **The asymmetry is ~9:1 in the Boss's favour.**

### 3.6 Smallest responsible mechanic set

**Three terms decide every observed outcome:**

```text
(1)  Boss DEF 50        → −33% on player throughput
(2)  Boss MaxHP 5000    → the size of the bar that 29–38 NET/Turn must drain
(3)  Boss HP-gem heals  → 10–30% of player damage is refunded
```

The sustain loop (the group-1 hypothesis) is real and measurable but is **not
binding**: capping it does not change the outcome, because the loop's ceiling
never mattered — the Boss was never under enough pressure for sustain to matter.

---

## 4. Experiment Definitions and Exact Parameter Deltas

Every experiment varies **only** the named parameters over the **unchanged**
canonical 75-run cell set (5 Bosses × 3 policies × 5 seeds). Seed, policy, Boss
identity, Element, Passive identity, Skill timing, and Relic snapshot are never
varied. Full machine-readable deltas are in the artifact under
`experiments[].parameterDeltas`.

| Exp | Group | Exact delta | Baseline → experiment |
|---|---|---|---|
| **E0** | control | none | — |
| **E1a** | 1 healing | `card-heal` Heal magnitude | `20%` → `10%` MaxHP |
| **E1b** | 1 economy | `card-power-charge` cost + grant | `0 → 10` Power; `+25 → +20` |
| **E1** | 1 both | E1a + E1b | as above |
| **E2b** | 2 Boss sustain | Mộc Yêu `boss-moc-yeu-regen` cadence | `5` → `8` Player Matches |
| **E3b** | 3 DEF half | shared-row Boss DEF | `50 → 25` (Sơn Thạch Vệ/Kim Lôi Vương unchanged at `0`) |
| **E3** | 3 DEF | shared-row Boss DEF | `50 → 0` (same scoping) |
| **E4** | 3 HP scaling | shared-row Boss MaxHP | `5000 → 3000` (Sơn Thạch Vệ `3000`, Kim Lôi Vương `2800` unchanged) |
| **E5** | combined | E1 + E2b + E3 + E4 | as above |

**E2b required a scope decision.** The canonical matrix deliberately pins Mộc
Yêu's Passive Threshold to `5` "for historical baseline reproducibility" while
`BOSS_RULES.md` §6.2.3 authors `8`, so E2b is simultaneously a Boss sustain trim
**and** a deferred documentation reconciliation. It is reported as both.

**A measurement defect in the first run was caught and corrected.** The first
E3b applied `DEF 25` to *all five* Bosses, silently raising Sơn Thạch Vệ and Kim
Lôi Vương `0 → 25` — two Bosses the hypothesis said nothing about. That
confounded comparison (Sơn Thạch Vệ victories collapsed 3 → 0). E3/E3b/E4 are
now scoped to the `BOSS_RULES.md` §6.1 rows that actually define the value, and
the artifact records the untouched Bosses explicitly as `(unchanged)`.

---

## 5. Before / After — 75-Run Comparison

Canonical matrix, identical cells, `MaxTurns = 1000`. Valid runs 75/75,
**0 InvalidSimulation** in every experiment.

| Exp | Stalemate | Victory | Defeat | ≤50t | ≥200t | ≥1000t |
|---|---|---|---|---|---|---|
| **E0 (TASK-296 baseline)** | **26/75 (35%)** | 8 | 41 | 27 | 36 | 26 |
| E1a heal only | 20/75 (27%) | 5 | 50 | 31 | 24 | 20 |
| E1b economy only | 23/75 (31%) | 5 | 47 | 31 | 28 | 23 |
| E1 both | 17/75 (23%) | 2 | 56 | 32 | 20 | 17 |
| E2b Mộc Yêu cadence | 26/75 (35%) | 8 | 41 | 27 | 36 | 26 |
| E3b DEF 25 | 26/75 (35%) | 8 | 41 | 27 | 36 | 26 |
| E3 DEF 0 | 26/75 (35%) | **11** | 38 | 27 | 35 | 26 |
| E4 MaxHP 3000 | 26/75 (35%) | 8 | 41 | 27 | 36 | 26 |
| **E5 combined** | 17/75 (23%) | 2 | 56 | 32 | 20 | 17 |

### Boss-level victories (out of 15 runs per Boss)

| Exp | Hỏa Long | Thủy Ma | Mộc Yêu | Sơn Thạch Vệ | Kim Lôi Vương |
|---|---|---|---|---|---|
| **E0** | **0** | **0** | **0** | 3 | 5 |
| E1a | 0 | 0 | 0 | 0 | 5 |
| E1b | 0 | 0 | 0 | 0 | 5 |
| E1 | 0 | 0 | 0 | 0 | 2 |
| E2b | 0 | 0 | 0 | 3 | 5 |
| E3b | 0 | 0 | 0 | 3 | 5 |
| E3 | **3** | 0 | 0 | 3 | 5 |
| E4 | 0 | 0 | 0 | 3 | 5 |
| E5 | 0 | 0 | 0 | 0 | 2 |

### Policy-level outcomes (75 runs = 25 per policy)

| Exp | Passive (V/D/S) | Average (V/D/S) | Skilled (V/D/S) |
|---|---|---|---|
| **E0** | 0 / 25 / 0 | **0 / 0 / 25** | 8 / 16 / 1 |
| E1a | 0 / 25 / 0 | 0 / 5 / 20 | 5 / 20 / 0 |
| E1 | 0 / 25 / 0 | 0 / 8 / 17 | 2 / 23 / 0 |
| E3 | 0 / 25 / 0 | 0 / 0 / 25 | 11 / 13 / 1 |
| E4 | 0 / 25 / 0 | 0 / 0 / 25 | 8 / 16 / 1 |
| **E5** | 0 / 25 / 0 | 0 / 8 / 17 | 2 / 23 / 0 |

---

## 6. Interpretation — Why the Parameters Are Insufficient

**Group 1 (healing + Power economy) is the only group that breaks Average's
perfect-stalemate record** (25/25 → 17/75 overall, Average 0/0/25 → 0/8/17). It
works exactly as hypothesised: with a costed Power Charge the loop becomes a
net-loss rate, Average's Player-HP minimum on the sample run fell 604 → **23**,
and it now dies instead of standing still.

**But group 1 makes things *worse* on the metric that matters.** Victory
collapses **8 → 2**, because Skilled — which the 0-cost Power Charge was
subsidising — loses its Power supply and now dies faster than it can finish.

**Groups 2, 3 and the HP axis move nothing.** E2b and E4 produce
**byte-identical metrics** to E0 across all 75 runs. E4 was verified applied (the
Boss floor did move, 4840 → 2656), so the outcome is unchanged *because the Boss
floor is ~2900 HP away from enrage, not because the delta failed*. Removing the
−33% DEF tax (E3) is the single most productive lever tested and still yields
only **+3 victories, all on one Boss**.

**The decisive finding:** no combination of these parameters produces a single
victory against Thủy Ma or Mộc Yêu, and no combination reduces the median
Skilled run below ~23 Turns without simultaneously collapsing the victory count.
The economy being tuned is not the economy that is broken.

---

## 7. Assessment Against the §5 Acceptance Targets

The §5 targets are treated as proposals, not gates. Where they conflict with the
intended difficulty or the simulator's synthetic policies, the conflict is
reported rather than gamed.

| Target | Result | Verdict |
|---|---|---|
| Stalemate rate < 5% in the 75-run matrix | best **23%** (E5) | ❌ **not reachable by parameters alone** |
| No policy 100% stalemates from a repeatable loop | Average 25/25 → 0/8/17 (E1/E5) | ✅ **met** — group 1 does break the loop |
| Average produces meaningful combat progression | 8 victories, 17 defeats (was 25 stalemates) | ✅ **met** in the sense that it now resolves |
| All five Bosses defeatable by ≥1 policy/build | 0 victories vs Thủy Ma and Mộc Yêu, **in every experiment** | ❌ **not reachable** |
| Typical encounters in tens of turns | median 150 → 24 (E1/E5); but only by losing | ⚠️ **achieved by making every run a fast defeat** |
| No InvalidSimulation outcomes | 0/75 in every experiment | ✅ **met** |
| Deterministic, reproducible | bit-identical re-runs across all 9 experiments × 75 runs | ✅ **met** |

### Explicit conflict with the current simulator's synthetic policies

**The `passive` policy wins 0/75 runs in every experiment including the
TASK-296 baseline.** It never casts a Card and always takes the lowest-index
legal swap, so it is a *measurement instrument*, not a representable player. It
cannot defeat any Boss-regeneration Boss, and "all five Bosses defeatable by at
least one tested policy" is therefore only testable across all three policies —
which the data does not satisfy either.

**The `skilled` policy's swap heuristic is stale.** Its documented scoring
rationale still says a longer match "produces a higher Match Tier and more
resources (`COMBAT_RULES.md` §2)" — but OD-3 **retired match-tier multipliers**
and ADR-028 then retired them a second time. The heuristic now scores a property
that carries no advantage under the current rules. Skilled is not actually
skilled, which is why its win rate is erratic rather than monotone.

### Proposed revised targets

```text
T1  Stalemate rate < 15% in the canonical 75-run matrix.
    Justification: 23% is the measured floor over every parameter group tested.
T2  No policy 100% stalemates.                       (E1/E5 reach 0/8/17 for Average)
T3  Every Boss defeated by at least one run in the matrix, and
    each Boss has >= 1 non-stalemate outcome per policy.   (currently unmet)
T4  Median encounter length < 100 Turns for at least two of three policies.
T5  Zero InvalidSimulation; bit-identical determinism.
T6  Drop the "passive policy can win" expectation — it is an
    instrument, not a player model.
```

---

## 8. Testing and Artifact Protection

### Tests run

```text
dotnet test src/backend/GameServer.sln

  GameServer.Domain.Tests            1500 passed / 0 failed
  GameServer.Api.Tests                377 passed / 0 failed
  GameServer.Application.Tests        758 passed / 0 failed   (incl. all TASK-298)
  GameServer.Infrastructure.Tests     472 passed / 4 failed   ← PRE-EXISTING
```

**The 4 Infrastructure failures are pre-existing and unrelated to TASK-298.**
Verified by stashing all TASK-298 work, re-running at pristine `HEAD` + operator
changes, and observing the same 4 failures:

```text
BossPersistenceTests.Task172ProvisioningMigration_ShouldWriteTheDocumentedPassiveAndSkillDocuments
BossPersistenceTests.ProvisioningMigration_ShouldWriteTheDocumentedPassiveAndSkillDocuments
BossDefinitionBalanceLookupTests.GetDefinitionByIdentityAsync_ShouldPreserveTheNonPersistedSecondaryEffects
BossDefinitionPostgresProvisioningTests.Postgres_ShouldRoundTripBothJsonDocumentsToTheDomainDefinitions
```

Root cause of the first: `flame-burst` round-trips with `BaseDamage = 150`
(authoritative `BOSS_RULES.md` §6.1 value) while the test expects `0` (the
ADR-026 D3 retired value). These belong to the ADR-026/ADR-024 overlay area, not
to this task, and are not fixed here (§16 task discipline).

### TASK-298's own tests (all passing)

| Test | Property |
|---|---|
| `TASK298_EveryExperiment_RunsTheCompleteCanonicalCellSet` | 9 × 75 cells in canonical order, 0 InvalidSimulation, structural integrity |
| `TASK298_ControlExperiment_MatchesTheCanonicalMatrix` | E0 reproduces the canonical matrix bit-identically |
| `TASK298_EveryExperiment_IsDeterministic` | full re-run is bit-identical for all 9 experiments |
| `TASK298_EveryExperiment_HoldsTheCombatInvariants` | Power 0–100, non-negative totals, HP bounds, Victory/Defeat terminal conditions |
| `TASK298_RawEvidence_IsArchivedAsDeterministicJson` | writer is pure; artifact path is TASK-298-only |

**Invariants verified:** `Power` stays in the documented 0–100 range
(`GAME_RULES.md` §12); `PowerGeneratedTotal`/`PowerSpentTotal` non-negative;
per-Turn series satisfy the dimensional invariant `series.Count == Turns`
(TASK-195 D-194-4); summed series equal reported totals; `CastsByCard` sums to
`CastsTotal`; a Stalemate ends at exactly `MaxTurns`; a Victory leaves Boss HP 0
and a Defeat leaves Pet HP 0.

**Power accounting was checked as a bound, not an identity.** Three stages write
or read `PetState.Power` (generation, Card cast, Drain Power), so total generated
and total spent are not required to cancel — they do not, and the rules do not
require them to.

### Artifact hashes (final, verified)

```text
DC62B91DC1777590016BCE39E4480BBC0EB20050B462B4F4EE06E1C578A68582
    tasks/artifacts/TASK-194-baseline-simulation-results.json          UNCHANGED
AA3D87ED523C44D835D6FBE722FD59A4DEA50835B5ABC8F4A81B9A404140E08A
    tasks/artifacts/TASK-197-controlled-balance-experiments.json       UNCHANGED
A9E9ABBD9B342C9B8849E5CECB1580D3154915782F539F5A3547252E4EE4FC62
    …/TASK-296-canonical-75-candidate.json                             UNCHANGED
F5855FEF0FCA4CF5195B4169A2E8D40C42B13844E02B69EEA1DBA4BFDCA58216
    tasks/artifacts/TASK-298-match-only-combat-economy-experiments.json  NEW
```

No candidate was promoted to TASK-194. No commit, push, or deploy occurred.

### Files added (test project only — no production file touched)

```text
tests/backend/GameServer.Application.Tests/Balance/TASK298ExperimentDefinition.cs
tests/backend/GameServer.Application.Tests/Balance/TASK298ExperimentMatrix.cs
tests/backend/GameServer.Application.Tests/Balance/TASK298ExperimentRunner.cs
tests/backend/GameServer.Application.Tests/Balance/Tests/TASK298ExperimentTests.cs
tasks/artifacts/TASK-298-match-only-combat-economy-experiments.json
```

`git status` confirms **no file under `src/`** was modified.

---

## 9. Findings Reported, Not Fixed (§16)

1. **The TASK-194/TASK-197 archival tests overwrite their pinned artifacts.**
   `BalanceBaselineMatrixTests.TASK194_RawEvidence_IsArchivedAsDeterministicJson`
   calls `WriteArtifact`, which writes
   `tasks/artifacts/TASK-194-baseline-simulation-results.json` in place; the
   TASK-197 equivalent does the same. Any task that runs the full suite will
   silently rewrite both. **Suggested follow-up:** make both writers honour a
   read-only/mismatch guard, or move archival writes behind an explicit
   environment opt-in.
2. **TASK-297A is missing.** No such task file or reference exists. **Suggested
   follow-up:** identify whether it was intended to be TASK-296's verification
   slice and record it, so later tasks do not inherit an unresolvable dependency.
3. **The `skilled` policy's heuristic is stale under OD-3/ADR-028.** Its
   documented match-length rationale cites the retired match-tier multiplier.
   **Suggested follow-up:** re-derive the Skilled heuristic against the current
   resource rules before any further balance conclusions are drawn from it.
4. **Mộc Yêu's Passive Threshold is inconsistent.** `BOSS_RULES.md` §6.2.3
   authors `8`; `BalanceBaselineMatrix.Bosses` pins `5` "for historical baseline
   reproducibility". **Suggested follow-up:** retire the historical pin once a
   re-baseline is approved.
5. **Four pre-existing Infrastructure test failures** in the ADR-026/ADR-024
   Boss overlay area (§8).

---

## 10. Recommendation — Next Tuning Iteration

**The evidence says the next iteration must widen scope beyond starter-card and
Boss parameters, because that space is exhausted.** Specifically:

**Recommended for the next task (in priority order):**

1. **ADR-level: reconsider ADR-026 D2's symmetric actor-relative HP-gem
   healing.** The measured 10–30% damage refund is a structural consequence of
   giving the Boss the same HP-gem heal the Pet has. This is the only lever that
   can move Thủy Ma and Mộc Yêu, and it needs an explicit Product decision, not a
   balance trim.
2. **Boss offense/defense rebalance on the *Boss* side, not the player's.** The
   Boss deals ~87/Turn against a 1000 HP pool and absorbs ~29 net/Turn against a
   3000–5000 pool. Either the Boss's HP must come down an order of magnitude, or
   the player's per-Turn throughput must go up an order of magnitude. The current
   ratio cannot be reconciled by ±50% trims.
3. **Repair the `skilled` policy heuristic** (finding 3) before using it as
   evidence for anything.
4. **Do not adopt E1 or E5.** Both improve the stalemate rate only by converting
   every Skilled run into a fast defeat (victories 8 → 2). Neither is a candidate.
5. **`card-heal` magnitude (E1a) is the single cleanest salvageable trim** if one
   must be taken now: it is the only group-1 half that keeps 5 of Skilled's 8
   victories while cutting stalemates 26 → 20.

**Explicitly not recommended:** continuing to tune `card-heal`,
`card-power-charge`, Mộc Yêu's regen cadence, or Boss DEF in isolation. Each was
measured; none resolves the encounter-completion problem, and the combined
candidate is strictly worse than the baseline on victories.

---

## 11. Final Status

```text
PARTIAL — ADDITIONAL TUNING REQUIRED
```

**Why this status and not `TUNING VERIFIED`:** the root cause is resolved and
the evidence is complete and deterministic, but no tested parameter set meets the
acceptance targets — stalemate rate bottoms at 23% (target <5%), two Bosses
remain undefeatable in every experiment, and the only parameter group that
breaks Average's sustain loop does so by collapsing Skilled's victories from 8 to
2. A candidate cannot be recommended for product review.

**Why not `BLOCKED`:** the root cause *is* resolved, approval was obtained for
the experiment scope, and the deliverables are complete. What remains is a
design decision (ADR-026 D2 symmetric healing and the Boss stat ratio) that is
outside a balance-tuning task's authority.

No production game rule, parameter, or configuration was changed by this task.

---

## 12. Deliverable Index

| §8 requirement | Location |
|---|---|
| 1. Root-cause analysis | §3 |
| 2. Experiment definitions and exact parameter deltas | §4 + artifact `experiments[].parameterDeltas` |
| 3. Before/after 75-run comparison | §5 |
| 4. Boss- and policy-level outcomes | §5 tables |
| 5. Test results and artifact hashes | §8 |
| 6. Recommendation for the next tuning iteration | §10 |
| Raw evidence | `tasks/artifacts/TASK-298-match-only-combat-economy-experiments.json` |
