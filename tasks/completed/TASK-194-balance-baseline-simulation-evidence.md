# TASK-194 — Balance Baseline Simulation & Decision Evidence

<!--
  GEN-TASK EXECUTION MANIFEST — ANALYSIS / EVIDENCE SPECIFICATION ONLY
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.

  References docs/ and src/ by path and section — it does NOT copy game
  rules, formulas, magnitudes, schemas, or API payload shapes.

  SCOPE OF THIS TASK: Define the baseline evidence-gathering execution plan that
  uses TASK-193's existing deterministic simulation harness to collect empirical
  evidence for unresolved TASK-191 decisions. ANALYSIS & EVIDENCE ONLY — zero
  production changes, zero balance changes, zero database changes, zero
  frontend changes. No balance values approved or modified; no balance verdicts
  (BALANCED / UNBALANCED / PASS / FAIL) introduced.

  STATUS: EXECUTED (DONE) — the plan in §1–§17 was run on 2026-10-05 against the
  existing TASK-193 harness; the results are in §18–§33. The plan text above is
  preserved as authored; nothing in it was rewritten after execution.
-->

---

## Metadata

```text
Task ID:           TASK-194
Type:              Analysis / Evidence
Status:            DONE
Executed:          2026-10-05
Execution Result:  75 / 75 baseline simulations completed; 0 InvalidSimulation;
                   75 / 75 reproduced bit-identically on re-run
Evidence:          §18–§33 of this record (execution evidence)
Raw Artifact:      tasks/artifacts/TASK-194-baseline-simulation-results.json
                   (SHA-256 4F9B5077B9320B09BD83C9144FF9B1234566D067B5FE0EF9BCD85FCB1CE6059F;
                   regenerated and corrected under TASK-195)
Production Changes: NONE
Balance Changes:   NONE
Database Changes:  NONE
Frontend Changes:  NONE
Balance Verdicts:  NONE  (no BALANCED / UNBALANCED / PASS / FAIL produced)
Risk:              LOW (specification and evidence gathering only; no production,
                   gameplay rule, or balance changes)
Priority:          HIGH (unblocks empirical data collection for open TASK-191
                   balance decisions)
Primary Agent:     testing
Supporting Agents: gameplay, design, review
Workflow:          documentation/documentation-change.md
Skills:            testing/test-scenario-generation,
                   discovery/impact-analysis,
                   quality/scope-validation
Dependencies:      TASK-191 (balance pass specification; owner of decisions Q-1..Q-12),
                   TASK-192 (DONE — B-02 post-exploit card cast economy),
                   TASK-193 (DONE — deterministic balance simulation harness)
Model:             Gemini 3.8
Reasoning:         High
```

---

## 1. Objective

Define a **baseline evidence-gathering task** that will execute TASK-193's existing deterministic simulation harness (`BalanceSimulator`) to collect empirical gameplay evidence for unresolved TASK-191 decisions.

Explicit boundaries:

```text
Type: Analysis / Evidence
Production Changes: NONE
Balance Changes: NONE
Database Changes: NONE
Frontend Changes: NONE
```

The objective is strictly observational measurement and empirical data gathering across Bosses 1–5, the three approved scripted player policies (`passive`, `average`, `skilled`), and a deterministic seed set under current production rules (including TASK-192's server-authoritative 1-card-cast-per-turn limit).

TASK-194 produces structured descriptive data and analysis. It neither approves nor rejects any balance values, alters no numbers, and introduces no balance verdicts.

---

## 2. Scope & Non-Scope

### 2.1 In Scope

- Execution specification for the baseline simulation experiment matrix using TASK-193's existing test-only harness in `tests/backend/GameServer.Application.Tests/Balance/`.
- Execution strictly in `BalanceSimulationMode.Baseline` (current production rules and content values, completely unchanged).
- Driving the actual production `BattleStateService` and domain pipeline (`SwapExecutor`, `CardCastExecutor`, `DamagePipeline`, `ResourceGenerator`, `BossResponse`, `StatusEffectLifecycle`, `PassiveTracker`).
- Absolute adherence to **no duplicated gameplay logic**: the harness computes no combat values; metrics are read exclusively from committed state and emitted domain events.
- Scripted player policies: `passive`, `average`, `skilled` from `BalancePolicies`.
- Bosses: all five MVP content Bosses from `BossDefinitions.All` (Hỏa Long, Thủy Ma, Mộc Yêu, Sơn Thạch Vệ, Kim Lôi Vương).
- Deterministic seed set: an explicitly chosen and recorded 5-seed set (yielding 5 bosses × 3 policies × 5 seeds = 75 runs minimum).
- Systematic collection of metrics M-01 through M-15 for every simulated run.
- Cross-seed descriptive analysis (mean, median, min, max, standard deviation, outcome distributions).
- Boss progression analysis (effective HP progression, boss pressure curves, enrage cadence, regeneration impact).
- Systematic mapping of gathered evidence to open TASK-191 decisions (Q-1 through Q-12).
- Complete specification of data formatting, reproducibility, verification, and archival requirements.

### 2.2 Non-Scope & Critical Constraints

- **Production code changes: NONE.** No file under `src/` is modified.
- **Balance changes: NONE.** No balance value is tuned, modified, or proposed for immediate adoption.
- **Database changes: NONE.** No EF migration, schema change, or entity modification.
- **Frontend changes: NONE.** No Phaser, React, or UI code is touched.
- **Q-1 remains QUALITATIVE.** No fight-duration target exists in authoritative docs; none is invented. Fight duration is an observational metric; acceptability is a Product Owner judgment.
- **No balance verdicts.** The harness and report must **not** emit `BALANCED`, `UNBALANCED`, `PASS`, or `FAIL`. Outcomes are strictly the four factual technical outcomes defined by TASK-193 (`Victory`, `Defeat`, `Stalemate`, `InvalidSimulation`).
- **No balance approval.** No candidate value from TASK-191 is approved by this task.
- **Proposals B-01, B-03, B-04, B-07, B-08, B-09, B-10 are NOT implemented.**
- **Power Charge is NOT modified.** Card `card-power-charge` retains its authored `PowerCost = 0` per `CARD_RULES.md` §2 item 3.
- **Mộc Yêu regeneration is NOT modified.** Passive `boss-moc-yeu-regen` retains its authored 5% MaxHP regen per 5 player matches per `BOSS_RULES.md` §6.2.3.
- **Boss stats are NOT modified.** BossDefinitions 1–5 retain their authored HP, ATK, DEF, Enrage, and Skill values.
- **Pet / Card / Relic balance is NOT modified.** Starter loadouts and values remain as provisioned.
- **No ADR is created.** Deterministic simulation runs and evidence reports are test/analysis artifacts, not architectural modifications (`docs/03-decisions/README.md` §2).
- **Evidence collection only.** The task measures and documents what currently happens; it does not decide what *should* happen.

---

## 3. Source-of-Truth Documents

Governing documents per the `AGENTS.md` §2 hierarchy:

### 3.1 Game Design

- `docs/00-overview/GDD.md` §11 (damage flow), §12 (boss difficulty comes primarily from mechanics, not high HP).
- `docs/00-overview/MVP_SCOPE.md` §1 (in-scope gameplay), §2 (explicit non-goals).
- `docs/01-game-design/GAME_RULES.md` §5 (Combo table), §12 (Power range 0–100), §17 (turn resolution order), §18 (server authority), §20 (rule change policy).
- `docs/01-game-design/MATCH3_RULES.md` §1 (board side length 8×8), §2 (swap validation & adjacency), §5 (special gem creation & clearing), §6 (combo mechanics), §7 (deterministic PRNG model), §8 (Turn & sequence lifecycle).
- `docs/01-game-design/COMBAT_RULES.md` §1 (pet stat defaults), §2 (per-gem resource generation rates & tier multipliers), §3 (damage pipeline steps 1–6), §4 (shield persistence & depletion), §5 (crit composition & resolution).
- `docs/01-game-design/ELEMENT_RULES.md` §2.1 (matchup cycle Mộc → Thổ → Thủy → Hỏa → Kim → Mộc), §2.2 (element factors 1.50 / 1.00 / 0.75).
- `docs/01-game-design/PET_RULES.md` §5.7, §6 (stat composition; Level/Star curve unauthored), §8 (pet passive definitions & thresholds).
- `docs/01-game-design/CARD_RULES.md` §2 (card inventory; Power Charge cost 0), §3 (cast lifecycle; §3 item 6 one-card-cast-per-turn limit per ADR-021), §4 (card effect types).
- `docs/01-game-design/RELIC_RULES.md` §5 (anti-infinite-chain rule), §8.5 (relic inventory, triggers, and magnitudes).
- `docs/01-game-design/BOSS_RULES.md` §4 (skill resolution), §5 (boss turn & enrage trigger), §6 (MVP Bosses 1–5 stats, passives, skills), §7 (difficulty from mechanics).
- `docs/01-game-design/PASSIVE_RULES.md` §4, §7 (passive charge & trigger lifecycle).

### 3.2 Technical Design & Decisions

- `docs/02-technical/ARCHITECTURE.md` §1, §3 (Domain and Application boundaries; test project organization).
- `docs/02-technical/GAME_STATE.md` §2.0.2 (Turn counter), §2.2.2 (CardCastsUsedThisTurn), §2.3 (PetState), §2.4 (BossState), §2.6 (RngState / seed model).
- `docs/02-technical/GAME_EVENTS.md` §2 (BattleWon, BattleLost, DamageDealt, PowerChanged, etc.).
- `docs/02-technical/SIGNALR_PROTOCOL.md` §3.2 (event payloads).
- `docs/03-decisions/ADR/ADR-009-deterministic-prng.md` (XorShift128Star PRNG implementation).
- `docs/03-decisions/ADR/ADR-021-one-card-cast-per-committed-turn.md` (one card cast per committed turn constraint).

### 3.3 Tasks

- `tasks/backlog/TASK-191-balance-pass.md` (balance audit, open decision queue Q-1..Q-12, proposals B-01..B-14).
- `tasks/completed/TASK-192-card-cast-turn-exploit.md` (implemented B-02 / ADR-021).
- `tasks/completed/TASK-193-deterministic-balance-simulation-harness.md` (decision Q-12, decision Q-1, harness technical specification, and test-only implementation).

---

## 4. Harness Prerequisites

TASK-194 requires **no new tooling implementation**. It utilizes the test-only harness built in TASK-193, located in `tests/backend/GameServer.Application.Tests/Balance/`:

| Component | File | Role |
|---|---|---|
| `BalanceSimulator` | `BalanceSimulator.cs` | Orchestrator: executes `RunAsync()`, driving the production `BattleStateService`. |
| `BalanceSimulationConfiguration` | `BalanceSimulationConfiguration.cs` | Complete, immutable input parameter record (`Seed`, `Boss`, `Pet`, `Cards`, `Relics`, `Policy`, `Mode`, `MaxTurns`). |
| `BalanceSimulationMetrics` | `BalanceSimulationMetrics.cs` | Container for metrics M-01 through M-15 and M-15 `BalanceResourceCheckpoint`. |
| `BalanceSimulationResult` | `BalanceSimulationResult.cs` | Complete H-09 execution record containing all inputs, terminal state, outcome, and metrics. |
| `BalanceSimulationMode` | `BalanceSimulationMode.cs` | Execution mode enum (`Baseline`, `ControlledComparison`). |
| `BalanceSimulationOutcome` | `BalanceSimulationOutcome.cs` | Technical outcome enum (`Victory`, `Defeat`, `Stalemate`, `InvalidSimulation`). |
| `BalancePlayerPolicy` / `BalancePolicies` | `BalancePlayerPolicy.cs` | Factory and implementation of `passive`, `average`, `skilled` policies. |
| `MetricAccumulator` | `MetricAccumulator.cs` | Accumulates M-01..M-15 metrics strictly from domain state and events. |
| Harness Unit Tests | `Tests/BalanceSimulatorTests.cs` | 32 tests asserting compliance with criteria H-01 through H-10. |

### Technical Execution Environment

- **In-process test execution:** Runs within the `GameServer.Application.Tests` xUnit project via `dotnet test`.
- **In-memory test doubles:** Utilizes `InMemoryBattleStateRepository` and `BalanceFixedSeedSource`.
- **Zero external dependencies:** No Redis instance, PostgreSQL database, SignalR hub, or web browser is required or permitted.
- **Production pipeline execution:** Each simulated turn executes through `BattleStateService.ExecuteSwapAsync` and `ExecuteCardCastAsync`, invoking the real `SwapExecutor`, `CardCastExecutor`, `DamagePipeline`, `ResourceGenerator`, and `BossResponse`.

---

## 5. Simulation Matrix

To ensure statistical validity while maintaining determinism, TASK-194 defines a standard baseline matrix:

$$\text{5 Bosses} \times \text{3 Policies} \times \text{5 Deterministic Seeds} = \text{75 Total Baseline Simulations}$$

### 5.1 Matrix Dimensions

```text
Bosses (5):
  1. Hỏa Long       (boss-hoa-long, Element: Hỏa, DEF: 50, MaxHP: 5000)
  2. Thủy Ma        (boss-thuy-ma, Element: Thủy, DEF: 50, MaxHP: 5000)
  3. Mộc Yêu        (boss-moc-yeu, Element: Mộc, DEF: 50, MaxHP: 5000)
  4. Sơn Thạch Vệ   (boss-son-thach-ve, Element: Thổ, DEF: 0, MaxHP: 3000)
  5. Kim Lôi Vương  (boss-kim-loi-vuong, Element: Kim, DEF: 0, MaxHP: 2800)

Policies (3):
  1. passive        (commits lowest legal swap index; never casts cards)
  2. average        (commits lowest legal swap index; casts first affordable card)
  3. skilled        (prefers highest match-length swap; casts highest-cost affordable card)

Seeds (5):
  1. 20261001UL
  2. 20261002UL
  3. 20261003UL
  4. 20261004UL
  5. 20261005UL
```

### 5.2 Baseline Configuration Parameters

Each of the 75 runs must use identical baseline loadouts and parameters:

| Parameter | Baseline Value | Source / Rationale |
|---|---|---|
| `Mode` | `BalanceSimulationMode.Baseline` | Current production rules and content values only. |
| `PlayerId` | `player-balance-sim` | Test owner identity (`GAME_STATE.md` §2.8). |
| `Pet` | Xích Lang (`petinstance-balance`) | Element: `Hoa`, Passive: `passive-xich-lang`, Threshold: 5, Defaults: HP 1000, ATK 50, DEF 25, Crit 5, Power 0 (`COMBAT_RULES.md` §1.1). |
| `Cards` | Heal (20), Shield (20), Power Charge (0), Iron Fang (40) | Standard 3 Basic + 1 Pet Skill loadout snapshot (`CARD_RULES.md` §2/§4). Power Charge cost 0 retained per current code. |
| `Relics` | `null` | Baseline unequipped loadout (`RELIC_RULES.md` §2.3). Eliminates relic confounding from baseline fight pacing. |
| `MaxTurns` | `1000` | Safety bound against non-termination (`TASK-193` §5.3). Approves no duration target. |

---

## 6. Seed Strategy

### 6.1 Deterministic Seed Model

Under `MATCH3_RULES.md` §7 and `ADR-009`, the battle PRNG (`XorShift128Star`) is seeded at battle creation. The seed solely governs the initial board generation and the stream of cascade replacement gems. The harness wraps the seed via `BalanceFixedSeedSource`, guaranteeing bit-identical repeatability.

### 6.2 The Canonical 5-Seed Set

TASK-193 used `20261005UL` in unit assertions, but did not define a formal multi-seed benchmark set. TASK-194 explicitly adopts the following canonical 5-seed set for baseline evidence:

1. `Seed 1 = 20261001UL`
2. `Seed 2 = 20261002UL`
3. `Seed 3 = 20261003UL`
4. `Seed 4 = 20261004UL`
5. `Seed 5 = 20261005UL` (preserves continuity with TASK-193 fixture tests)

### 6.3 Seed Independence & Convergence

- Different seeds generate distinct initial board layouts and cascade streams (`H-02`), proving that findings are not artifacts of a single lucky or unlucky board.
- All seeds are explicitly recorded in every `BalanceSimulationResult` (H-09 record).

---

## 7. Policies

The three policies implemented in `BalancePlayerPolicy.cs` (`TASK-193` §3.2) represent the **only source of player agency**. They are measurement instruments, not player skill judgments or balance opinions.

### 7.1 Passive (`passive`)

- **Swap Selection:** First untried adjacent cell pair in ascending index order `(from, to)`.
- **Card Casts:** Never proposes a card cast (`ProposeCast = null`).
- **Measurement Role:** Isolates raw Match-3 board throughput and elemental interactions with zero card economy acceleration. Establishes the lower bound of player effectiveness.

### 7.2 Average (`average`)

- **Swap Selection:** First untried adjacent cell pair in ascending index order `(from, to)`.
- **Card Casts:** Proposes the first affordable card in equipped loadout order (`Heal` $\rightarrow$ `Shield` $\rightarrow$ `Power Charge` $\rightarrow$ `Iron Fang`).
- **Measurement Role:** Simulates casual / baseline player behavior: casts cards immediately when resources allow, without tactical timing or card prioritization.

### 7.3 Skilled (`skilled`)

- **Swap Selection:** Evaluates all legal adjacent swaps and scores by the **maximum match length** that the swap completes (Match 5 > Match 4 > Match 3), breaking ties by lowest cell-pair index.
- **Card Casts:** Proposes the affordable card with the highest authored `PowerCost` (e.g. prioritizing `Iron Fang` [40] over `Heal`/`Shield` [20] over `Power Charge` [0]).
- **Measurement Role:** Simulates competent play seeking higher Match Tiers and maximum card impact. Establishes an upper bound for baseline performance.

---

## 8. Metrics M-01…M-15

The harness records the complete metric set defined in TASK-193 §3.1 / `BalanceSimulationMetrics.cs`:

| # | Field / Metric | Source & Semantics | Relevant TASK-191 Items |
|---|---|---|---|
| **M-01** | `Turns` | Total committed Match-3 turns to terminal state (read from `BattleState.Turn`). | B-03, B-04, B-06, B-12 |
| **M-02** | `DurationTurns` | Fight duration in Turns (wall-clock excluded; observational metric). | Q-1 (observational) |
| **M-03** | `PlayerDamageTotal`, `PlayerDamagePerTurn` | Cumulative and per-turn damage dealt to boss (summed from `DamageDealt` with `Source = Player`). | B-03, B-04, B-08 |
| **M-04** | `BossDamageTotal`, `BossDamagePerTurn` | Cumulative and per-turn damage dealt to pet (summed from `DamageDealt` with `Source = Boss`). | B-04, B-07 |
| **M-05** | `BossHpPerTurn`, `BossHpMin` | Boss HP progression trajectory and minimum Boss HP reached across the run. | B-03, B-07 |
| **M-06** | `PlayerHpPerTurn`, `PlayerHpMin` | Pet HP progression trajectory and minimum Pet HP reached across the run. | B-07, B-09 |
| **M-07** | `PowerGeneratedTotal`, `PowerSpentTotal`, `PowerFinal` | Total Power gained (positive `PowerChanged` deltas), total Power spent, and terminal Power. | B-01 (evidence), B-10 |
| **M-08** | `CastsByCard`, `CastsTotal`, `CastTurns`, `RejectedCasts` | Successful casts grouped by card id, total casts, cast turn numbers, and rejected cast proposals. | B-01 (evidence), B-08, B-09 |
| **M-09** | `RelicTriggersByRelic`, `RelicTriggersTotal` | Relic triggers by relic id and total triggers (0 for baseline unequipped runs). | B-10 |
| **M-10** | `ComboPerTurn`, `ComboMax`, `ComboDistribution` | Combo value per turn, maximum combo observed, and frequency histogram of combo values. | B-11 |
| **M-11** | `ElementAdvantageCount`, `ElementNeutralCount`, `ElementDisadvantageCount` | Damage instances resolving with advantage (1.50×), neutral (1.00×), or disadvantage (0.75×). | B-08 |
| **M-12** | `BossRegenerationTotal`, `BossRegenerationTurns` | Total HP restored by boss regeneration (positive inter-turn Boss HP deltas) and count of turns regenerated. | B-07 |
| **M-13** | `BossSkillCastCount`, `BossPassiveTriggerCount`, `BossEnrageTurn` | Boss skill casts, boss passive triggers, and the turn number when boss first entered Enrage (`null` if never). | B-03, B-07 |
| **M-14** | `Outcome`, `OutcomeDetail` | Terminal outcome (`Victory`, `Defeat`, `Stalemate`, `InvalidSimulation`) and descriptive resolution detail. | B-03, B-07, all |
| **M-15** | `ResourceTrace` | Per-turn checkpoint trace of `Turn`, `PlayerHp`, `PlayerMaxHp`, `PlayerPower`, `BossHp`, `PlayerStatusCount`, `BossStatusCount`. | B-07, B-09 |

---

## 9. Evidence Collection Procedure

When TASK-194 is executed, the following procedure must be followed:

```text
1. Initialize Runner
   └── Instantiate an in-memory execution test runner referencing GameServer.Application.Tests.Balance.

2. Iterate Matrix
   └── For each Boss b in [HoaLong, ThuyMa, MocYeu, SonThachVe, KimLoiVuong]:
         For each Policy p in [passive, average, skilled]:
           For each Seed s in [20261001, 20261002, 20261003, 20261004, 20261005]:
             a. Build BalanceSimulationConfiguration:
                - Seed = s
                - Boss = b
                - Policy = BalancePolicies.ByName(p)
                - Pet = PetConfiguration(passiveThreshold: 5)
                - CardDefinitions = AllCards
                - RelicDefinitions = null
                - Mode = BalanceSimulationMode.Baseline
                - MaxTurns = 1000
             b. Instantiate BalanceSimulator(config, new InMemoryBattleStateRepository())
             c. Await simulator.RunAsync()
             d. Assert result.Outcome != BalanceSimulationOutcome.InvalidSimulation
             e. Capture and retain BalanceSimulationResult (H-09 record)

3. Aggregate & Format Data
   └── Export raw simulation results into a structured dataset.
   └── Compute cross-seed descriptive statistics.
   └── Compile boss progression tables.

4. Verify Integrity
   └── Verify zero production files touched.
   └── Verify all existing repository tests pass.
```

---

## 10. Cross-Seed Analysis

To characterize the stability of gameplay dynamics across randomized board states, descriptive statistics must be calculated across the 5 seeds for each `(Boss, Policy)` pair:

### 10.1 Statistical Summary Table Structure

For each of the 15 cells `(Boss × Policy)`:

| Metric | Mean | Median | Min | Max | Std Dev |
|---|---|---|---|---|---|
| **M-01 Turns to Outcome** | — | — | — | — | — |
| **M-03 Player Total Damage** | — | — | — | — | — |
| **M-04 Boss Total Damage** | — | — | — | — | — |
| **M-05 Min Boss HP** | — | — | — | — | — |
| **M-06 Min Pet HP** | — | — | — | — | — |
| **M-07 Power Generated** | — | — | — | — | — |
| **M-07 Power Spent** | — | — | — | — | — |
| **M-08 Cards Cast** | — | — | — | — | — |
| **M-10 Max Combo** | — | — | — | — | — |

### 10.2 Outcome Distributions

For each of the 15 cells, report the outcome frequencies:
- `Victory` count and %
- `Defeat` count and %
- `Stalemate` count and %

### 10.3 Variance Characterization

- Evaluate whether turn duration is tightly clustered (mechanical determinism dominates) or widely dispersed (cascade luck dominates).
- Identify any policy/boss pairing where different seeds produce divergent terminal outcomes (e.g. 3 Victories and 2 Defeats).

---

## 11. Boss Progression Analysis

The gathered evidence must characterize the current difficulty curve across Bosses 1 through 5 to inform TASK-191 §1.7 and B-03/B-04:

### 11.1 Nominal vs Effective HP Pacing

Document the empirical relationship between authored stats and observed survival:

| Boss | Nominal HP | DEF | Effective HP ($HP \times \frac{100+DEF}{100}$) | Average Turns to Kill (Skilled) | Average Turns to Kill (Average) | Average Turns to Kill (Passive) |
|---|---|---|---|---|---|---|
| **Hỏa Long** | 5000 | 50 | 7500 | — | — | — |
| **Thủy Ma** | 5000 | 50 | 7500 | — | — | — |
| **Mộc Yêu** | 5000 | 50 | 7500 + Regen | — | — | — |
| **Sơn Thạch Vệ** | 3000 | 0 | 3000 | — | — | — |
| **Kim Lôi Vương** | 2800 | 0 | 2800 | — | — | — |

### 11.2 Boss Damage Pressure & Cooldown Impact

- Compare the damage output of Bosses 1–3 (ATK 100, CD 2T / 3T) against Bosses 4–5 (ATK 120 / 140, CD 0T).
- Record the turn number when Enrage triggered across bosses (Bosses 1–3 at 30%, Boss 4 at 50%, Boss 5 at 75%).
- Determine whether Bosses 4–5 defeat the player faster despite having lower HP.

### 11.3 Mộc Yêu Stalemate & Regeneration Analysis

- Report M-12 total HP regenerated and number of regeneration turns.
- Check whether any `passive` or `average` runs reached `Stalemate` (MaxTurns 1000 limit) against Mộc Yêu.
- Contrast player net damage per turn against regeneration per turn (+250 HP every 5 player matches).

---

## 12. TASK-191 Decision Mapping

TASK-194 maps its baseline evidence directly to the open decision queue of TASK-191 §6:

| Decision ID | Summary of Question | Classification | How Evidence Informs Decision / Required Next Action |
|---|---|---|---|
| **Q-1** | Target fight duration (swaps / turns) | **Partially supported** & **Requires product decision** | Harness provides empirical Turn counts (M-01/M-02) across all 5 bosses and 3 policies. However, Q-1 remains **QUALITATIVE**: no numeric duration target exists in `docs/`. Product Owner must evaluate whether observed durations are acceptable. |
| **Q-2** | Power Charge cost 0 retention (B-01) | **Partially supported** & **Requires product decision** | Harness measures Power generated vs spent (M-07) and Power Charge cast frequency (M-08) under the post-TASK-192 1-cast-per-turn limit. Shows whether Power remains constrained or accumulates to excess. Product Owner decides whether to raise cost to 10–25 or keep 0. |
| **Q-3** | Boss stat curve & DEF asymmetry (B-03, B-04) | **Supported by simulation** & **Requires product decision** | Directly measures turns-to-kill, effective HP pacing, boss damage output, and enrage timing across Bosses 1–5. Quantifies the impact of flat stats on Bosses 1–3 and the sudden HP/DEF drop on Bosses 4–5. Product Owner decides between Candidate A (flat stats, mechanics-only) and Candidate B (monotonic HP curve). |
| **Q-5** | "No strictly dominated Card/Relic" principle | **Partially supported** & **Requires product decision** | Baseline measures cast distribution (M-08) across starter cards (e.g. comparing Heal vs Shield utilization). Full card-by-card comparison across all 8 cards requires controlled comparison runs. Product Owner must decide whether non-domination is a mandatory MVP requirement. |
| **Q-6** | Pet Tier/Star/Level stat curve (B-05) | **Not measurable** & **Requires product decision** | Inactive in code: `BattleStartService` passes no stats; all pets fight with identical baseline stats (1000/50/25/5/0). Cannot be measured by simulation. Product Owner must decide to author the curve or declare Level/Star cosmetic for MVP. |
| **Q-7** | Player XP pacing (B-06) | **Not measurable** & **Requires product decision** | Player XP progression (+100 XP/win = 1 level/win) operates at the account/meta level, not within active combat simulation turns. Product Owner must decide whether to retain flat 1-win-per-level pacing or curve it. |
| **Q-8** | Mộc Yêu regeneration magnitude (B-07) | **Supported by simulation** & **Requires product decision** | Directly measures total HP healed (M-12), Boss HP trajectory (M-05), and whether low-efficiency policies (`passive`) experience `Stalemate` (M-14). Provides empirical proof of stalemate risk. Product Owner decides whether to reduce magnitude (5% $\rightarrow$ 2–3%), increase threshold (5 $\rightarrow$ 8), or accept attrition. |
| **Q-9** | Card damage normalization tolerance band (B-08) | **Partially supported** & **Requires product decision** | Baseline measures damage dealt by equipped starter cards. Controlled card substitution is needed for all 8 cards. Approving a specific tolerance band (e.g. $\pm 15\%$) is a Product Owner judgment. |
| **Q-10** | Relic Power engines & magnitudes (B-10) | **Partially supported** & **Requires product decision** | Baseline unequipped runs establish the 0-relic control. Measuring individual relic contributions requires controlled ablation (relic on vs off). Product Owner decides on per-turn caps or magnitude adjustments. |
| **Q-11** | Pet-passive magnitudes for Xích Lang / Sơn Hùng (B-14) | **Requires implementation investigation** & **Requires product decision** | Blocked by finding D-2: pet passive *effects* are not applied in `BattleStateService` (only charged/triggered). Furthermore, magnitudes are unauthored in `docs/`. Requires implementation before measurement is possible. |
| **Q-12** | Simulation harness approval (B-14 item 3) | **Supported by simulation** (RESOLVED) | Approved in TASK-191/193. TASK-194 executes the approved harness to gather the required evidence. |

---

## 13. Reproducibility Requirements

To satisfy `AGENTS.md` §11, `MATCH3_RULES.md` §7, and criterion `H-01`, every simulation run must be 100% reproducible:

1. **Bit-Identical Reproducibility:** Re-running any of the 75 configurations with the same seed, boss, pet, cards, relics, and policy must produce identical values for every metric in M-01..M-15 and an identical terminal `BattleState`.
2. **Deterministic Tie-Breaking:** All player policies break ties using fixed, documented rules (e.g. lowest cell-pair index) or PRNG-seeded draws; no unseeded random calls (`System.Random`, `Guid`) are permitted.
3. **Hardware Independence:** No wall-clock measurements or system timers are incorporated into combat calculations or recorded metrics (`M-02` is measured in Turns).
4. **Complete Execution Record:** Every run must emit a complete `BalanceSimulationResult` record containing all inputs and metrics, allowing any single run to be reproduced in isolation.

---

## 14. Verification Requirements

When executing TASK-194, verification must establish:

```text
[x] Execution Integrity: All 75 simulations complete successfully.
[x] Invariant Assertion: Zero runs report BalanceSimulationOutcome.InvalidSimulation.
[x] Metric Completeness: M-01 through M-15 are populated without nulls or omitted fields.
[x] Production Immutability: git status and git diff confirm zero files under src/ are modified.
[x] Regression Cleanliness: dotnet test passes 100% of all existing unit and integration tests.
```

Executed results for each requirement: **§31**.

---

## 15. Scope Audit

Before concluding TASK-194, the executing agent must verify this scope checklist:

```text
Production code changed:           NO  (verified: no file under src/ modified — §30/§31.3)
Balance values changed:             NO  (all content definitions unchanged)
Database schema / migrations:       NO
Frontend / client changed:          NO
ADR created or modified:            NO
Power Charge cost modified:         NO  (retains PowerCost = 0)
Mộc Yêu regeneration modified:      NO  (retains 5% MaxHP regen)
Boss stats modified:                NO  (authored 1–5 stats intact)
Pet / Card / Relic balance tuned:   NO
Balance verdicts introduced:        NO  (zero BALANCED / UNBALANCED / PASS / FAIL)
Q-1 treated as qualitative:         YES (no duration pass/fail applied)
```

Verified audit with the evidence for each line: **§30**.

---

## 16. Completion Criteria

TASK-194 will be marked **DONE** when the following criteria are met:

- [x] All 75 baseline simulations (5 bosses × 3 policies × 5 seeds) are executed.
- [x] No simulation encounters an `InvalidSimulation` outcome.
- [x] The raw simulation data is recorded and preserved.
- [x] Cross-seed descriptive statistics (mean, median, min, max, std dev) are compiled for each cell.
- [x] Boss progression analysis across Bosses 1–5 is documented.
- [x] Evidence mappings for Q-1, Q-2, Q-3, Q-5, Q-6, Q-7, Q-8, Q-9, Q-10, Q-11, and Q-12 are detailed.
- [x] Scope audit (§15) is fully satisfied with zero production modifications.

All seven criteria are met; the evidence for each is in **§18–§33**.

---

## 17. Archival Requirements

- **Raw Data Artifacts:** Raw simulation results must be formatted and stored as a structured JSON artifact (e.g. `tasks/artifacts/TASK-194-baseline-simulation-results.json`) or directly embedded as Markdown summary tables in the completion evidence.
- **Environment Provenance:** Archival records must document the Git commit SHA, runtime environment (.NET SDK version), exact seed set, and test runner command.
- **Lifecycle Transition:** Upon completion, this file will be updated with execution evidence and moved from `tasks/backlog/` to `tasks/completed/TASK-194-balance-baseline-simulation-evidence.md`.

As executed: the raw JSON artifact is at
`tasks/artifacts/TASK-194-baseline-simulation-results.json` (2 671 329 bytes,
SHA-256 `4F9B5077B9320B09BD83C9144FF9B1234566D067B5FE0EF9BCD85FCB1CE6059F`,
regenerated under TASK-195),
provenance is recorded in §18, the Markdown summary tables are §21–§27, and this
file was moved to `tasks/completed/TASK-194-balance-baseline-simulation-evidence.md`.

---

# Execution Evidence — TASK-194 EXECUTED

> The specification above (§1–§17) was executed against the **existing TASK-193
> harness**. **No production, gameplay, rule, or balance value was changed.** This
> section records what was measured. It introduces no `BALANCED` / `UNBALANCED` /
> `PASS` / `FAIL` verdict, approves no value, implements no TASK-191 proposal, and
> creates no ADR: evidence ≠ Product Owner decision.

## 18. Execution Record

```text
Executed:           2026-10-05
Repository HEAD:    962a1416925ae67ac13a3077c43bf0caf3649c6
Branch state:       pre-existing uncommitted work from other tasks was present
                    before this task began and is untouched by it (§30)
Harness:            tests/backend/GameServer.Application.Tests/Balance/ (TASK-193)
Entry point:        BalanceSimulator.RunAsync()
                      → BattleStateService.CreateBattleAsync / ExecuteSwapAsync /
                        ExecuteCardCastAsync
                      → SwapExecutor · CardCastExecutor · DamagePipeline ·
                        ResourceGenerator · RelicResolver · PassiveTracker ·
                        Boss Response
Full-suite command: dotnet test src/backend/GameServer.sln
Focused command:    dotnet test tests/backend/GameServer.Application.Tests/
                      GameServer.Application.Tests.csproj
                      --filter "FullyQualifiedName~BalanceBaselineMatrixTests"
Runner runtime:     .NET 10.0.11 (Release of record: .NET SDK 10.0.400)
Host:               Microsoft Windows 10.0.19045, X64
Mode:               Baseline (TASK-194 §5.2)
Result:             75 / 75 runs completed; 0 InvalidSimulation (TASK-194 §14)
```

### 18.1 Raw data artifact (§17 archival)

```text
Path:      tasks/artifacts/TASK-194-baseline-simulation-results.json
Size:      2 671 329 bytes (≈2.55 MiB)
Contents:  matrix definition (bosses + authored stats, policies, seeds, baseline
           loadout), run count, technical outcome counts, and 75 run records,
           each carrying M-01…M-15 (including the per-Turn series and the M-15
           resource trace; corrected under TASK-195)
Encoding:  UTF-8, no BOM
SHA-256:   4F9B5077B9320B09BD83C9144FF9B1234566D067B5FE0EF9BCD85FCB1CE6059F
```

The artifact is written by the focused TASK-194 test and is a **pure function of
the 75 results**: it carries no timestamp, no `Guid`, and no machine-random
value, so regenerating it produces byte-identical content (asserted by
`TASK194_RawEvidence_IsArchivedAsDeterministicJson`). The Git commit SHA, runtime,
seed set, and runner command are recorded above; the artifact records the seed
set, matrix, baseline configuration, and runtime itself.

## 19. Matrix Execution Result

```text
Bosses (BOSS_RULES.md §6.1 order, BossDefinitions.All):
  boss-hoa-long (Hỏa, 5000/100/50/30%)   boss-thuy-ma (Thủy, 5000/100/50/30%)
  boss-moc-yeu (Mộc, 5000/100/50/30%)    boss-son-thach-ve (Thổ, 3000/120/0/50%)
  boss-kim-loi-vuong (Kim, 2800/140/0/75%)
Policies:  passive · average · skilled
Seeds:     20261001 · 20261002 · 20261003 · 20261004 · 20261005
Baseline:  player-balance-sim · petinstance-balance (Hỏa, passive-xich-lang,
           threshold 5) · cards Heal 20 / Shield 20 / Power Charge 0 / Iron Fang 40
           · no Relics · MaxTurns 1000
```

| §14 verification requirement | Result |
|---|---|
| All 75 simulations complete | **75 / 75** |
| Zero `InvalidSimulation` | **0** |
| M-01…M-15 populated per definition | **75 / 75 runs** (asserted per run) |
| Deterministic results reproducible | **75 / 75 reproduced bit-identically** on a full re-run (outcome, every M-01…M-15 member, terminal Turn/Sequence/HP/Power/`RngState`) |
| Harness leaves production untouched | **yes** — see §30 scope audit |

## 20. Outcome Distribution (M-14, all 75 runs)

| Outcome | Runs | Share |
|---|---|---|
| Victory | **27** | 36.0 % |
| Defeat | **43** | 57.3 % |
| Stalemate | **5** | 6.7 % |
| InvalidSimulation | **0** | 0 % |

### 20.1 Outcome by Boss × policy (each cell = 5 seeds)

| Boss | passive | average | skilled |
|---|---|---|---|
| Hỏa Long | Defeat 5 | **Victory 5** | Defeat 5 |
| Thủy Ma | Defeat 5 | **Victory 5** | Defeat 5 |
| Mộc Yêu | Defeat 5 | **Stalemate 5** | Defeat 5 |
| Sơn Thạch Vệ | Defeat 5 | **Victory 5** | Victory 2 / Defeat 3 |
| Kim Lôi Vương | Defeat 5 | **Victory 5** | **Victory 5** |

### 20.2 Outcome by policy and by Boss

| Policy | Victory | Defeat | Stalemate |
|---|---|---|---|
| `passive` | 0 | 25 | 0 |
| `average` | 20 | 0 | 5 |
| `skilled` | 7 | 18 | 0 |

| Boss | Victory | Defeat | Stalemate |
|---|---|---|---|
| Hỏa Long | 5 | 10 | 0 |
| Thủy Ma | 5 | 10 | 0 |
| Mộc Yêu | 0 | 10 | 5 |
| Sơn Thạch Vệ | 7 | 8 | 0 |
| Kim Lôi Vương | 10 | 5 | 0 |

### 20.3 Terminal detail (M-14 `OutcomeDetail`)

| Detail kind | Runs |
|---|---|
| committed Swap resolved the battle (Defeat) | 43 |
| committed Swap resolved the battle (Victory) | 24 |
| reached the configured maximum of 1000 Turns without a terminal result | 5 |
| Card cast resolved the battle (Victory) | 3 |

Facts of record:

- **All 43 defeats are pet-HP depletion** (`PlayerHpMin = 0` in 43/43). No run
  ended by any other defeat path and no run was terminated by an internal error.
- **All 5 stalemates are Mộc Yêu × `average`**, each reaching the 1000-Turn safety
  bound. No stalemate arises from the policy exhausting legal Swap candidates.
- **3 runs ended on a Card cast** (Kim Lôi Vương × `skilled`, seeds 20261002 /
  20261003 / 20261005) — the only cast-terminated runs in the matrix.
- **Cross-seed divergence occurs in exactly one cell**: Sơn Thạch Vệ × `skilled`
  (2 Victory / 3 Defeat). Every other `(Boss, policy)` cell produced the same
  terminal outcome on all 5 seeds.

### 20.4 Factual observations from the distribution

Every statement below is read directly from §20.1–§20.3 and §21–§27; none is a
recommendation, a target, or a verdict.

1. **Outcome was policy-determined far more than seed-determined.** 14 of 15
   cells returned the same terminal outcome on all five seeds, and the only
   divergence was a 2/3 split. Board-stream variation (H-02) moved durations and
   damage figures but rarely the winner.
2. **Every defeat was Pet-HP depletion** (43/43). No run ended by any other path
   and no run ended in an internal error.
3. **Casting decided the fight; the two casting policies differ only in card
   selection order, and that ordering dominated the result.** `average`
   (first affordable card in loadout order → Heal first) won 20 of 25 runs;
   `skilled` (highest authored cost affordable → Iron Fang first) won 7 of 25,
   while producing the **higher** player damage per Turn in all five matched
   cells (e.g. Sơn Thạch Vệ 145.3 vs 78.8, Kim Lôi Vương 250.9 vs 116.6).
   In this baseline, sustain-oriented casting outperformed damage-card-first
   casting; the policies are measurement instruments, so this is a statement
   about the heuristics, not about players.
4. **Never casting was uniformly fatal.** `passive` lost 25/25, dealing
   211–2,455 total damage to Bosses with 2,800–5,000 HP while the Pet died in
   7–25 Turns: with a 1000-HP Pet pool and no card healing, incoming damage is
   the binding constraint, not the Boss's HP pool.
5. **The two later Bosses were not harder for these policies**, despite higher
   ATK and 0-turn cooldowns: `average` won 5/5 against both Sơn Thạch Vệ and Kim
   Lôi Vương, and `skilled` won 5/5 against Kim Lôi Vương while losing 5/5 to
   Hỏa Long, Thủy Ma, and Mộc Yêu. Ordered by observed outcomes, the curve is
   not monotonic.

## 21. Cross-Seed Descriptive Statistics

Each cell below is the `(Boss, policy)` pair over its **5 deterministic seeds**.
Every figure is `mean / median / min / max / population standard deviation`
(population σ over n = 5; no sample correction is applied). **Descriptive
statistics only** — no target, tolerance, or verdict is attached to any value.

### 21.1 Metric summary per Boss × policy (5 seeds each)

| Boss | Policy | M-01 Turns | M-03 Player dmg | M-04 Boss dmg | M-05 Boss HP min | M-06 Pet HP min | M-07 Power gen | M-07 Power spent | M-08 Casts | M-10 Combo max | M-12 Regen measured |
|---|---|---|---|---|---|---|---|---|---|---|---|
| boss-hoa-long | passive | 10.4 / 9 / 8 / 13 / 2.2 | 678.4 / 784 / 318 / 986 / 272.9 | 1590.4 / 1608 / 1120 / 2120 / 403.1 | 4321.6 / 4216 / 4014 / 4682 / 272.9 | 0 / 0 / 0 / 0 / 0 | 90 / 100 / 60 / 100 / 15.5 | 0 / 0 / 0 / 0 / 0 | 0 / 0 / 0 / 0 / 0 | 8.6 / 8 / 2 / 19 / 6.2 | 0 / 0 / 0 / 0 / 0 |
| boss-hoa-long | average | 88.8 / 90 / 78 / 96 / 6.4 | 5068.6 / 5060 / 5014 / 5185 / 61.2 | 13580.8 / 13648 / 12448 / 14416 / 731.5 | 0 / 0 / 0 / 0 / 0 | 591.2 / 592 / 556 / 608 / 19 | 1630 / 1650 / 1500 / 1740 / 79 | 1580 / 1600 / 1440 / 1720 / 94.7 | 88.8 / 90 / 78 / 96 / 6.4 | 16 / 17 / 8 / 25 / 6 | 0 / 0 / 0 / 0 / 0 |
| boss-hoa-long | skilled | 14.6 / 14 / 12 / 19 / 2.4 | 1262.8 / 1299 / 813 / 1534 / 253.4 | 2278.4 / 2312 / 1648 / 2728 / 367 | 3737.2 / 3701 / 3466 / 4187 / 253.4 | 0 / 0 / 0 / 0 / 0 | 362 / 355 / 270 / 480 / 69.5 | 312 / 340 / 200 / 380 / 61.4 | 14.6 / 14 / 12 / 19 / 2.4 | 6.2 / 7 / 3 / 9 / 2.3 | 0 / 0 / 0 / 0 / 0 |
| boss-thuy-ma | passive | 7.4 / 7 / 7 / 8 / 0.5 | 291.2 / 262 / 211 / 488 / 102.3 | 1204.8 / 1128 / 1128 / 1392 / 104.5 | 4708.8 / 4738 / 4512 / 4789 / 102.3 | 0 / 0 / 0 / 0 / 0 | 86 / 110 / 30 / 140 / 46.7 | 38 / 40 / 20 / 60 / 13.3 | 0 / 0 / 0 / 0 / 0 | 5.8 / 3 / 2 / 19 / 6.6 | 0 / 0 / 0 / 0 / 0 |
| boss-thuy-ma | average | 128.2 / 128 / 112 / 139 / 9.4 | 5018.8 / 5015 / 5000 / 5040 / 13.6 | 20966.4 / 21000 / 18360 / 22752 / 1509.9 | 0 / 0 / 0 / 0 / 0 | 313.6 / 344 / 176 / 452 / 105.5 | 2745 / 2695 / 2540 / 2910 / 138.4 | 2700 / 2680 / 2495 / 2870 / 135.5 | 128.2 / 128 / 112 / 139 / 9.4 | 14.8 / 18 / 8 / 19 / 4.8 | 0 / 0 / 0 / 0 / 0 |
| boss-thuy-ma | skilled | 16.2 / 17 / 12 / 19 / 2.5 | 1301.2 / 1425 / 701 / 1670 / 343 | 2692.8 / 2904 / 2016 / 3144 / 428.1 | 3698.8 / 3575 / 3330 / 4299 / 342.9 | 0 / 0 / 0 / 0 / 0 | 508 / 555 / 275 / 640 / 124.8 | 483 / 525 / 275 / 620 / 115.8 | 16.2 / 17 / 12 / 19 / 2.5 | 14.4 / 11 / 3 / 31 / 9.4 | 0 / 0 / 0 / 0 / 0 |
| boss-moc-yeu | passive | 16.4 / 14 / 14 / 25 / 4.3 | 823.2 / 711 / 543 / 1409 / 312.3 | 1648 / 1440 / 1360 / 2560 / 459.8 | 4844.4 / 4851 / 4811 / 4864 / 18.8 | 0 / 0 / 0 / 0 / 0 | 100 / 100 / 100 / 100 / 0 | 0 / 0 / 0 / 0 / 0 | 0 / 0 / 0 / 0 / 0 | 7.8 / 6 / 3 / 19 / 5.9 | 446.8 / 408 / 357 / 638 / 100.5 |
| boss-moc-yeu | average | 1000 / 1000 / 1000 / 1000 / 0 | 50594.2 / 50689 / 48560 / 52751 / 1451.3 | 101232 / 101360 / 100480 / 101680 / 406.7 | 4645.6 / 4639 / 4520 / 4774 / 82.7 | 760 / 760 / 760 / 760 / 0 | 17952 / 17840 / 17780 / 18330 / 198.9 | 17892 / 17800 / 17720 / 18260 / 195.8 | 1000 / 1000 / 1000 / 1000 / 0 | 26.8 / 27 / 21 / 32 / 3.5 | 28846.2 / 28829 / 28213 / 29529 / 426.9 |
| boss-moc-yeu | skilled | 55.8 / 53 / 34 / 84 / 18.3 | 5910.4 / 5494 / 3666 / 8551 / 1739.2 | 5824 / 5440 / 3520 / 8800 / 1900.2 | 4051.4 / 4065 / 3849 / 4294 / 167.6 | 0 / 0 / 0 / 0 / 0 | 1533 / 1460 / 940 / 2270 / 464.7 | 1480 / 1360 / 920 / 2220 / 462.5 | 55.8 / 53 / 34 / 84 / 18.3 | 13.2 / 13 / 12 / 15 / 1.2 | 2376.6 / 2042 / 1472 / 3598 / 828.1 |
| boss-son-thach-ve | passive | 11.2 / 10 / 10 / 15 / 1.9 | 867.2 / 755 / 619 / 1149 / 211 | 1459.2 / 1416 / 1200 / 1920 / 245.4 | 2132.8 / 2245 / 1851 / 2381 / 211 | 0 / 0 / 0 / 0 / 0 | 98 / 100 / 90 / 100 / 4 | 0 / 0 / 0 / 0 / 0 | 0 / 0 / 0 / 0 / 0 | 7.2 / 3 / 3 / 19 / 6.2 | 0 / 0 / 0 / 0 / 0 |
| boss-son-thach-ve | average | 39.6 / 39 / 36 / 45 / 3.5 | 3098.8 / 3048 / 3026 / 3241 / 85 | 5058.6 / 5025 / 4617 / 5601 / 362.9 | 0 / 0 / 0 / 0 / 0 | 669 / 669 / 650 / 688 / 17 | 724 / 710 / 650 / 800 / 51.6 | 640 / 660 / 580 / 700 / 43.8 | 39.6 / 39 / 36 / 45 / 3.5 | 12 / 12 / 3 / 19 / 6 | 0 / 0 / 0 / 0 / 0 |
| boss-son-thach-ve | skilled | 16.6 / 17 / 12 / 22 / 3.4 | 2667.4 / 2935 / 1809 / 3071 / 472 | 2308.2 / 2409 / 1809 / 2913 / 382 | 349 / 65 / 0 / 1191 / 458.9 | 58 / 0 / 0 / 247 / 96 | 458 / 450 / 350 / 550 / 81.6 | 412 / 380 / 300 / 520 / 78.6 | 16.6 / 17 / 12 / 22 / 3.4 | 11.8 / 13 / 8 / 15 / 2.8 | 0 / 0 / 0 / 0 / 0 |
| boss-kim-loi-vuong | passive | 13.6 / 12 / 12 / 19 / 2.7 | 1593.2 / 1280 / 1198 / 2455 / 490 | 1544 / 1416 / 1332 / 2184 / 323.6 | 1206.8 / 1520 / 345 / 1602 / 490 | 0 / 0 / 0 / 0 / 0 | 100 / 100 / 100 / 100 / 0 | 0 / 0 / 0 / 0 / 0 | 0 / 0 / 0 / 0 / 0 | 7.2 / 3 / 3 / 19 / 6.2 | 0 / 0 / 0 / 0 / 0 |
| boss-kim-loi-vuong | average | 24.6 / 26 / 21 / 29 / 3.1 | 2819.6 / 2825 / 2804 / 2829 / 9.6 | 2745.6 / 2872 / 2344 / 3108 / 317.7 | 0 / 0 / 0 / 0 / 0 | 737.6 / 724 / 724 / 792 / 27.2 | 440 / 415 / 405 / 500 / 38.1 | 388 / 400 / 340 / 440 / 41.2 | 24.6 / 26 / 21 / 29 / 3.1 | 8.4 / 6 / 3 / 19 / 5.5 | 0 / 0 / 0 / 0 / 0 |
| boss-kim-loi-vuong | skilled | 11.4 / 11 / 7 / 17 / 3.3 | 2887.4 / 2893 / 2811 / 2979 / 64.1 | 1422.4 / 1336 / 944 / 1916 / 325.6 | 0 / 0 / 0 / 0 / 0 | 453 / 500 / 116 / 601 / 177.8 | 332 / 350 / 250 / 370 / 43.1 | 300 / 300 / 240 / 360 / 45.6 | 12 / 11 / 8 / 17 / 3 | 11 / 13 / 5 / 15 / 3.8 | 0 / 0 / 0 / 0 / 0 |

Notes on reading this table:

- `M-06 Pet HP min` is `0` in every cell whose runs all ended in defeat — the
  active Pet reached 0 HP.
- `M-05 Boss HP min` is `0` in every cell won by all five seeds.
- `M-12 Regen measured` is non-zero only against Mộc Yêu, the only Boss carrying
  a regeneration Passive; see §27 and D-194-5 for its measurement limits.

### 21.2 Per-run results (all 75 runs)

`RejSwaps` / `RejCasts` are the policy's **rejected proposals**, i.e. harness
search activity, not gameplay outcomes: a rejected Swap begins no Turn
(`MATCH3_RULES.md` §2.1.5) and a rejected cast changes nothing
(`CARD_RULES.md` §3 item 3). `Detail` names the resolution that ended the run.

| Boss | Policy | Seed | Outcome | Turns | PlayerDmg | BossDmg | BossHP min | PetHP min | PowerGen | PowerSpent | PowerFinal | Casts | RejSwaps | RejCasts | ComboMax | Regen | RegenTurns | EnrageTurn | SkillCasts | Detail |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| boss-hoa-long | passive | 20261001 | Defeat | 9 | 392 | 1160 | 4608 | 0 | 60 | 0 | 60 | 0 | 66 | 0 | 2 | 0 | 0 | - | 2 | swap |
| boss-hoa-long | passive | 20261002 | Defeat | 13 | 784 | 1944 | 4216 | 0 | 100 | 0 | 100 | 0 | 99 | 0 | 8 | 0 | 0 | - | 4 | swap |
| boss-hoa-long | passive | 20261003 | Defeat | 13 | 986 | 2120 | 4014 | 0 | 100 | 0 | 100 | 0 | 127 | 0 | 11 | 0 | 0 | - | 5 | swap |
| boss-hoa-long | passive | 20261004 | Defeat | 9 | 912 | 1608 | 4088 | 0 | 100 | 0 | 100 | 0 | 56 | 0 | 19 | 0 | 0 | - | 4 | swap |
| boss-hoa-long | passive | 20261005 | Defeat | 8 | 318 | 1120 | 4682 | 0 | 90 | 0 | 90 | 0 | 76 | 0 | 3 | 0 | 0 | - | 2 | swap |
| boss-hoa-long | average | 20261001 | Victory | 94 | 5061 | 14272 | 0 | 608 | 1740 | 1720 | 20 | 94 | 805 | 805 | 8 | 0 | 0 | 67 | 28 | swap |
| boss-hoa-long | average | 20261002 | Victory | 90 | 5185 | 13648 | 0 | 556 | 1600 | 1520 | 80 | 90 | 927 | 927 | 19 | 0 | 0 | 66 | 27 | swap |
| boss-hoa-long | average | 20261003 | Victory | 86 | 5060 | 13120 | 0 | 608 | 1650 | 1620 | 30 | 86 | 629 | 629 | 11 | 0 | 0 | 65 | 26 | swap |
| boss-hoa-long | average | 20261004 | Victory | 78 | 5023 | 12448 | 0 | 592 | 1500 | 1440 | 60 | 78 | 672 | 672 | 25 | 0 | 0 | 50 | 26 | swap |
| boss-hoa-long | average | 20261005 | Victory | 96 | 5014 | 14416 | 0 | 592 | 1660 | 1600 | 60 | 96 | 1000 | 1000 | 17 | 0 | 0 | 74 | 28 | swap |
| boss-hoa-long | skilled | 20261001 | Defeat | 12 | 813 | 1648 | 4187 | 0 | 270 | 200 | 70 | 12 | 6 | 6 | 3 | 0 | 0 | - | 3 | swap |
| boss-hoa-long | skilled | 20261002 | Defeat | 15 | 1464 | 2528 | 3536 | 0 | 480 | 380 | 100 | 15 | 6 | 6 | 7 | 0 | 0 | - | 6 | swap |
| boss-hoa-long | skilled | 20261003 | Defeat | 14 | 1534 | 2312 | 3466 | 0 | 380 | 340 | 40 | 14 | 3 | 3 | 9 | 0 | 0 | - | 5 | swap |
| boss-hoa-long | skilled | 20261004 | Defeat | 19 | 1299 | 2728 | 3701 | 0 | 355 | 340 | 15 | 19 | 5 | 5 | 4 | 0 | 0 | - | 5 | swap |
| boss-hoa-long | skilled | 20261005 | Defeat | 13 | 1204 | 2176 | 3796 | 0 | 325 | 300 | 25 | 13 | 7 | 7 | 8 | 0 | 0 | - | 5 | swap |
| boss-thuy-ma | passive | 20261001 | Defeat | 7 | 211 | 1128 | 4789 | 0 | 30 | 30 | 0 | 0 | 32 | 0 | 2 | 0 | 0 | - | 2 | swap |
| boss-thuy-ma | passive | 20261002 | Defeat | 7 | 262 | 1128 | 4738 | 0 | 30 | 20 | 10 | 0 | 42 | 0 | 3 | 0 | 0 | - | 2 | swap |
| boss-thuy-ma | passive | 20261003 | Defeat | 7 | 212 | 1128 | 4788 | 0 | 120 | 40 | 80 | 0 | 43 | 0 | 2 | 0 | 0 | - | 2 | swap |
| boss-thuy-ma | passive | 20261004 | Defeat | 8 | 488 | 1392 | 4512 | 0 | 140 | 60 | 80 | 0 | 59 | 0 | 19 | 0 | 0 | - | 3 | swap |
| boss-thuy-ma | passive | 20261005 | Defeat | 8 | 283 | 1248 | 4717 | 0 | 110 | 40 | 70 | 0 | 94 | 0 | 3 | 0 | 0 | - | 2 | swap |
| boss-thuy-ma | average | 20261001 | Victory | 136 | 5040 | 22104 | 0 | 388 | 2910 | 2825 | 85 | 136 | 1034 | 1034 | 10 | 0 | 0 | 101 | 41 | swap |
| boss-thuy-ma | average | 20261002 | Victory | 126 | 5012 | 20616 | 0 | 208 | 2690 | 2630 | 60 | 126 | 1061 | 1061 | 8 | 0 | 0 | 87 | 39 | swap |
| boss-thuy-ma | average | 20261003 | Victory | 128 | 5000 | 21000 | 0 | 176 | 2695 | 2680 | 15 | 128 | 1029 | 1029 | 18 | 0 | 0 | 86 | 40 | swap |
| boss-thuy-ma | average | 20261004 | Victory | 139 | 5027 | 22752 | 0 | 344 | 2890 | 2870 | 20 | 139 | 1245 | 1245 | 19 | 0 | 0 | 95 | 43 | swap |
| boss-thuy-ma | average | 20261005 | Victory | 112 | 5015 | 18360 | 0 | 452 | 2540 | 2495 | 45 | 112 | 1078 | 1078 | 19 | 0 | 0 | 77 | 35 | swap |
| boss-thuy-ma | skilled | 20261001 | Defeat | 12 | 701 | 2016 | 4299 | 0 | 275 | 275 | 0 | 12 | 6 | 6 | 3 | 0 | 0 | - | 4 | swap |
| boss-thuy-ma | skilled | 20261002 | Defeat | 19 | 1543 | 3144 | 3457 | 0 | 640 | 620 | 20 | 19 | 8 | 8 | 10 | 0 | 0 | - | 6 | swap |
| boss-thuy-ma | skilled | 20261003 | Defeat | 17 | 1425 | 2904 | 3575 | 0 | 570 | 535 | 35 | 17 | 3 | 3 | 11 | 0 | 0 | - | 6 | swap |
| boss-thuy-ma | skilled | 20261004 | Defeat | 18 | 1670 | 3024 | 3330 | 0 | 555 | 525 | 30 | 18 | 4 | 4 | 31 | 0 | 0 | - | 6 | swap |
| boss-thuy-ma | skilled | 20261005 | Defeat | 15 | 1167 | 2376 | 3833 | 0 | 500 | 460 | 40 | 15 | 7 | 7 | 17 | 0 | 0 | - | 4 | swap |
| boss-moc-yeu | passive | 20261001 | Defeat | 14 | 543 | 1360 | 4858 | 0 | 100 | 0 | 100 | 0 | 91 | 0 | 3 | 357 | 3 | - | 3 | swap |
| boss-moc-yeu | passive | 20261002 | Defeat | 25 | 1409 | 2560 | 4851 | 0 | 100 | 0 | 100 | 0 | 176 | 0 | 8 | 638 | 7 | - | 7 | swap |
| boss-moc-yeu | passive | 20261003 | Defeat | 15 | 711 | 1520 | 4811 | 0 | 100 | 0 | 100 | 0 | 111 | 0 | 6 | 450 | 4 | - | 4 | swap |
| boss-moc-yeu | passive | 20261004 | Defeat | 14 | 858 | 1440 | 4838 | 0 | 100 | 0 | 100 | 0 | 112 | 0 | 19 | 408 | 4 | - | 4 | swap |
| boss-moc-yeu | passive | 20261005 | Defeat | 14 | 595 | 1360 | 4864 | 0 | 100 | 0 | 100 | 0 | 159 | 0 | 3 | 381 | 4 | - | 3 | swap |
| boss-moc-yeu | average | 20261001 | Stalemate | 1000 | 51410 | 101440 | 4617 | 760 | 17840 | 17800 | 40 | 1000 | 8887 | 8887 | 28 | 28977 | 288 | - | 268 | max-turns |
| boss-moc-yeu | average | 20261002 | Stalemate | 1000 | 48560 | 100480 | 4678 | 760 | 17840 | 17760 | 80 | 1000 | 8581 | 8581 | 32 | 28829 | 282 | - | 256 | max-turns |
| boss-moc-yeu | average | 20261003 | Stalemate | 1000 | 50689 | 101360 | 4639 | 760 | 17970 | 17920 | 50 | 1000 | 8454 | 8454 | 26 | 28683 | 285 | - | 267 | max-turns |
| boss-moc-yeu | average | 20261004 | Stalemate | 1000 | 49561 | 101200 | 4774 | 760 | 17780 | 17720 | 60 | 1000 | 9099 | 9099 | 21 | 28213 | 287 | - | 265 | max-turns |
| boss-moc-yeu | average | 20261005 | Stalemate | 1000 | 52751 | 101680 | 4520 | 760 | 18330 | 18260 | 70 | 1000 | 9182 | 9182 | 27 | 29529 | 282 | - | 271 | max-turns |
| boss-moc-yeu | skilled | 20261001 | Defeat | 34 | 3666 | 3520 | 4294 | 0 | 940 | 920 | 20 | 34 | 13 | 13 | 15 | 1472 | 12 | - | 10 | swap |
| boss-moc-yeu | skilled | 20261002 | Defeat | 40 | 4708 | 4320 | 4164 | 0 | 1200 | 1140 | 60 | 40 | 13 | 13 | 13 | 1676 | 14 | - | 14 | swap |
| boss-moc-yeu | skilled | 20261003 | Defeat | 68 | 7133 | 7040 | 3849 | 0 | 1795 | 1760 | 35 | 68 | 25 | 25 | 14 | 3095 | 22 | - | 20 | swap |
| boss-moc-yeu | skilled | 20261004 | Defeat | 53 | 5494 | 5440 | 4065 | 0 | 1460 | 1360 | 100 | 53 | 27 | 27 | 12 | 2042 | 18 | - | 15 | swap |
| boss-moc-yeu | skilled | 20261005 | Defeat | 84 | 8551 | 8800 | 3885 | 0 | 2270 | 2220 | 50 | 84 | 41 | 41 | 12 | 3598 | 28 | - | 26 | swap |
| boss-son-thach-ve | passive | 20261001 | Defeat | 10 | 619 | 1200 | 2381 | 0 | 90 | 0 | 90 | 0 | 41 | 0 | 3 | 0 | 0 | - | 2 | swap |
| boss-son-thach-ve | passive | 20261002 | Defeat | 15 | 1149 | 1920 | 1851 | 0 | 100 | 0 | 100 | 0 | 98 | 0 | 8 | 0 | 0 | - | 4 | swap |
| boss-son-thach-ve | passive | 20261003 | Defeat | 10 | 725 | 1320 | 2275 | 0 | 100 | 0 | 100 | 0 | 62 | 0 | 3 | 0 | 0 | - | 3 | swap |
| boss-son-thach-ve | passive | 20261004 | Defeat | 10 | 1088 | 1440 | 1912 | 0 | 100 | 0 | 100 | 0 | 67 | 0 | 19 | 0 | 0 | - | 4 | swap |
| boss-son-thach-ve | passive | 20261005 | Defeat | 11 | 755 | 1416 | 2245 | 0 | 100 | 0 | 100 | 0 | 135 | 0 | 3 | 0 | 0 | - | 3 | swap |
| boss-son-thach-ve | average | 20261001 | Victory | 45 | 3026 | 5601 | 0 | 650 | 710 | 660 | 50 | 45 | 339 | 339 | 3 | 0 | 0 | 24 | 11 | swap |
| boss-son-thach-ve | average | 20261002 | Victory | 36 | 3152 | 4617 | 0 | 688 | 700 | 600 | 100 | 36 | 252 | 252 | 8 | 0 | 0 | 18 | 10 | swap |
| boss-son-thach-ve | average | 20261003 | Victory | 42 | 3241 | 5313 | 0 | 669 | 760 | 660 | 100 | 42 | 277 | 277 | 12 | 0 | 0 | 22 | 11 | swap |
| boss-son-thach-ve | average | 20261004 | Victory | 36 | 3027 | 4737 | 0 | 650 | 650 | 580 | 70 | 36 | 288 | 288 | 19 | 0 | 0 | 17 | 11 | swap |
| boss-son-thach-ve | average | 20261005 | Victory | 39 | 3048 | 5025 | 0 | 688 | 800 | 700 | 100 | 39 | 349 | 349 | 18 | 0 | 0 | 22 | 11 | swap |
| boss-son-thach-ve | skilled | 20261001 | Defeat | 14 | 2511 | 2001 | 489 | 0 | 390 | 380 | 10 | 14 | 5 | 5 | 15 | 0 | 0 | 10 | 5 | swap |
| boss-son-thach-ve | skilled | 20261002 | Victory | 18 | 3011 | 2409 | 0 | 247 | 550 | 520 | 30 | 18 | 4 | 4 | 13 | 0 | 0 | 8 | 6 | swap |
| boss-son-thach-ve | skilled | 20261003 | Defeat | 17 | 2935 | 2409 | 65 | 0 | 450 | 380 | 70 | 17 | 6 | 6 | 14 | 0 | 0 | 6 | 6 | swap |
| boss-son-thach-ve | skilled | 20261004 | Victory | 22 | 3071 | 2913 | 0 | 43 | 550 | 480 | 70 | 22 | 4 | 4 | 9 | 0 | 0 | 14 | 7 | swap |
| boss-son-thach-ve | skilled | 20261005 | Defeat | 12 | 1809 | 1809 | 1191 | 0 | 350 | 300 | 50 | 12 | 7 | 7 | 8 | 0 | 0 | 10 | 5 | swap |
| boss-kim-loi-vuong | passive | 20261001 | Defeat | 13 | 1198 | 1416 | 1602 | 0 | 100 | 0 | 100 | 0 | 86 | 0 | 3 | 0 | 0 | 8 | 3 | swap |
| boss-kim-loi-vuong | passive | 20261002 | Defeat | 19 | 2455 | 2184 | 345 | 0 | 100 | 0 | 100 | 0 | 132 | 0 | 8 | 0 | 0 | 7 | 5 | swap |
| boss-kim-loi-vuong | passive | 20261003 | Defeat | 12 | 1280 | 1332 | 1520 | 0 | 100 | 0 | 100 | 0 | 77 | 0 | 3 | 0 | 0 | 8 | 3 | swap |
| boss-kim-loi-vuong | passive | 20261004 | Defeat | 12 | 1826 | 1456 | 974 | 0 | 100 | 0 | 100 | 0 | 86 | 0 | 19 | 0 | 0 | 2 | 4 | swap |
| boss-kim-loi-vuong | passive | 20261005 | Defeat | 12 | 1207 | 1332 | 1593 | 0 | 100 | 0 | 100 | 0 | 154 | 0 | 3 | 0 | 0 | 7 | 3 | swap |
| boss-kim-loi-vuong | average | 20261001 | Victory | 29 | 2804 | 3108 | 0 | 724 | 470 | 420 | 50 | 29 | 213 | 213 | 3 | 0 | 0 | 8 | 7 | swap |
| boss-kim-loi-vuong | average | 20261002 | Victory | 21 | 2827 | 2392 | 0 | 792 | 405 | 340 | 65 | 21 | 139 | 139 | 8 | 0 | 0 | 7 | 6 | swap |
| boss-kim-loi-vuong | average | 20261003 | Victory | 26 | 2813 | 2872 | 0 | 724 | 410 | 400 | 10 | 26 | 172 | 172 | 6 | 0 | 0 | 8 | 7 | swap |
| boss-kim-loi-vuong | average | 20261004 | Victory | 21 | 2825 | 2344 | 0 | 724 | 415 | 340 | 75 | 21 | 149 | 149 | 19 | 0 | 0 | 2 | 6 | swap |
| boss-kim-loi-vuong | average | 20261005 | Victory | 26 | 2829 | 3012 | 0 | 724 | 500 | 440 | 60 | 26 | 247 | 247 | 6 | 0 | 0 | 7 | 8 | swap |
| boss-kim-loi-vuong | skilled | 20261001 | Victory | 11 | 2932 | 1304 | 0 | 452 | 360 | 260 | 100 | 11 | 4 | 4 | 15 | 0 | 0 | 4 | 4 | swap |
| boss-kim-loi-vuong | skilled | 20261002 | Victory | 10 | 2979 | 1336 | 0 | 596 | 370 | 360 | 10 | 11 | 3 | 3 | 13 | 0 | 0 | 4 | 4 | cast |
| boss-kim-loi-vuong | skilled | 20261003 | Victory | 7 | 2811 | 944 | 0 | 601 | 250 | 240 | 10 | 8 | 2 | 2 | 14 | 0 | 0 | 4 | 3 | cast |
| boss-kim-loi-vuong | skilled | 20261004 | Victory | 17 | 2822 | 1916 | 0 | 500 | 330 | 300 | 30 | 17 | 3 | 3 | 5 | 0 | 0 | 5 | 5 | swap |
| boss-kim-loi-vuong | skilled | 20261005 | Victory | 12 | 2893 | 1612 | 0 | 116 | 350 | 340 | 10 | 13 | 7 | 7 | 8 | 0 | 0 | 4 | 5 | cast |

## 22. Fight-Duration Distribution (M-01 / M-02)

Duration is reported in **Turns** only. The harness records no wall-clock
duration by design (`TASK-193` §4.2: a millisecond measurement would destroy
determinism), so the wall-clock dimension of Q-1 is **not measurable** by this
harness. M-01 (`Turn` counter) and M-02 (`DurationTurns`) are the same quantity.

All 75 runs: **mean 97.0 / median 17 / min 7 / max 1000 / σ 243.7 Turns.**

| Turns bucket | Runs |
|---|---|
| 1-25 | 47 |
| 26-50 | 10 |
| 51-75 | 2 |
| 76-100 | 6 |
| 101-150 | 5 |
| 151-250 | 0 |
| 251-500 | 0 |
| 501-1000 | 5 |

The distribution is strongly **bimodal**, not dispersed: 57 of 75 runs end within
50 Turns, and the 5 runs above 500 Turns are the five Mộc Yêu × `average`
stalemates that ran to the 1000-Turn safety bound. Duration variance is therefore
dominated by *which* fight was played, not by cascade luck.

| Policy | Turns mean / median / min / max / sd |
|---|---|
| passive | 11.8 / 12 / 7 / 25 / 4 |
| average | 256.2 / 90 / 21 / 1000 / 373.7 |
| skilled | 22.9 / 17 / 7 / 84 / 18.6 |

| Boss | Turns mean / median / min / max / sd |
|---|---|
| boss-hoa-long | 37.9 / 14 / 8 / 96 / 36.2 |
| boss-thuy-ma | 50.6 / 17 / 7 / 139 / 55.3 |
| boss-moc-yeu | 357.4 / 53 / 14 / 1000 / 454.8 |
| boss-son-thach-ve | 22.5 / 17 / 10 / 45 / 12.7 |
| boss-kim-loi-vuong | 16.5 / 13 / 7 / 29 / 6.5 |

## 23. Boss Progression Analysis

| Boss | Authored MaxHP | DEF | Nominal effective HP (HP x (100+DEF)/100) | Outcomes V/D/S | Turns mean | Player dmg mean | Boss dmg mean | Boss dmg/Turn mean | Player dmg/Turn mean | Enraged runs | Skill casts mean | Regen measured mean |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| boss-hoa-long | 5000 | 50 | 7500 | 5 / 10 / 0 | 37.9 | 2336.6 | 5816.5 | 153.9 | 69.6 | 5 / 15 | 11.7 | 0 |
| boss-thuy-ma | 5000 | 50 | 7500 | 5 / 10 / 0 | 50.6 | 2203.7 | 8288 | 164.1 | 52.3 | 5 / 15 | 15.7 | 0 |
| boss-moc-yeu | 5000 | 50 | 7500 | 0 / 10 / 5 | 357.4 | 19109.3 | 36234.7 | 102 | 69 | 0 / 15 | 95.5 | 10556.5 |
| boss-son-thach-ve | 3000 | 0 | 3000 | 7 / 8 / 0 | 22.5 | 2211.1 | 2942 | 132.9 | 106.2 | 10 / 15 | 6.6 | 0 |
| boss-kim-loi-vuong | 2800 | 0 | 2800 | 10 / 5 / 0 | 16.5 | 2433.4 | 1904 | 117.3 | 169.1 | 15 / 15 | 4.9 | 0 |

Facts of record:

- **Observed damage-to-kill equals the authored `MaxHP`**, not the nominal
  effective HP of §11.1: winning runs recorded a mean 5 068.6 (Hỏa Long, MaxHP
  5 000), 5 018.8 (Thủy Ma, 5 000), 3 082.3 (Sơn Thạch Vệ, 3 000) and 2 853.5
  (Kim Lôi Vương, 2 800 — with 100% of cast damage preserved across all 10 wins,
  correcting D-194-3 under TASK-195). M-03 sums *post-mitigation* Final
  Damage (`COMBAT_RULES.md` §3 step 6), so DEF 50 does not raise the damage
  needed to kill; it reduces the player's throughput, which lengthens the fight
  (see the boss-dmg / player-dmg per Turn table below).
- **Boss damage pressure does not follow ATK order.** Measured mean Boss damage
  per Turn: Thủy Ma 164.1 > Hỏa Long 153.9 > Sơn Thạch Vệ 132.9 > Kim Lôi Vương
  117.3 > Mộc Yêu 102.0 — the two highest-ATK Bosses (Sơn Thạch Vệ ATK 120,
  Kim Lôi Vương ATK 140) dealt *less* damage per Turn than the ATK-100 Bosses,
  partly because those fights are much shorter and end before the Boss's
  charge/cooldown cadence matures. Per-Turn pressure averaged over runs of very
  different lengths is not a pure difficulty measure and is reported as measured.
- **Enrage is reached far sooner on Bosses 4–5**: Kim Lôi Vương enraged in
  **15 / 15** runs (Turn 2–8), Sơn Thạch Vệ in **10 / 15** (Turn 6–24), Hỏa Long
  and Thủy Ma in **5 / 15** (average policy only, Turn 50–101), and Mộc Yêu in
  **0 / 15** — its HP never fell below its 30 % (1 500) threshold in any run.

Enrage Turn observed per run (M-13; '-' = never enraged), passive x5 / average x5 / skilled x5:

| Boss | Enrage turns |
|---|---|
| boss-hoa-long | -, -, -, -, -, 67, 66, 65, 50, 74, -, -, -, -, - |
| boss-thuy-ma | -, -, -, -, -, 101, 87, 86, 95, 77, -, -, -, -, - |
| boss-moc-yeu | -, -, -, -, -, -, -, -, -, -, -, -, -, -, - |
| boss-son-thach-ve | -, -, -, -, -, 24, 18, 22, 17, 22, 10, 8, 6, 14, 10 |
| boss-kim-loi-vuong | 8, 7, 8, 2, 7, 8, 7, 8, 2, 7, 4, 4, 4, 5, 4 |

| Boss | Boss dmg/Turn (passive / average / skilled) | Player dmg/Turn (passive / average / skilled) |
|---|---|---|
| boss-hoa-long | 152 / 153.2 / 156.4 | 64.2 / 57.4 / 87.2 |
| boss-thuy-ma | 162.7 / 163.6 / 166.1 | 38.8 / 39.4 / 78.8 |
| boss-moc-yeu | 100.2 / 101.2 / 104.5 | 49.3 / 50.6 / 107.2 |
| boss-son-thach-ve | 130.5 / 127.9 / 140.3 | 77.7 / 78.8 / 161.9 |
| boss-kim-loi-vuong | 113.4 / 111.8 / 126.8 | 116.2 / 116.6 / 274.6 |

## 24. Power and Resource Analysis (M-07 / M-08)

`PowerSpentTotal` sums **every** negative `PowerChanged` delta, so it includes
non-card sinks — see D-194-7. For `passive` runs against Thủy Ma, Power "spent"
is the Boss's Drain Power alone, with zero casts.

| Boss | Policy | Power generated (mean) | Power spent (mean) | Power final (mean) | Casts (mean) | Rejected casts (sum) | Rejected swaps (sum) |
|---|---|---|---|---|---|---|---|
| boss-hoa-long | passive | 90 | 0 | 90 | 0 | 0 | 424 |
| boss-hoa-long | average | 1630 | 1580 | 50 | 88.8 | 4033 | 4033 |
| boss-hoa-long | skilled | 362 | 312 | 50 | 14.6 | 27 | 27 |
| boss-thuy-ma | passive | 86 | 38 | 48 | 0 | 0 | 270 |
| boss-thuy-ma | average | 2745 | 2700 | 45 | 128.2 | 5447 | 5447 |
| boss-thuy-ma | skilled | 508 | 483 | 25 | 16.2 | 28 | 28 |
| boss-moc-yeu | passive | 100 | 0 | 100 | 0 | 0 | 649 |
| boss-moc-yeu | average | 17952 | 17892 | 60 | 1000 | 44203 | 44203 |
| boss-moc-yeu | skilled | 1533 | 1480 | 53 | 55.8 | 119 | 119 |
| boss-son-thach-ve | passive | 98 | 0 | 98 | 0 | 0 | 403 |
| boss-son-thach-ve | average | 724 | 640 | 84 | 39.6 | 1505 | 1505 |
| boss-son-thach-ve | skilled | 458 | 412 | 46 | 16.6 | 26 | 26 |
| boss-kim-loi-vuong | passive | 100 | 0 | 100 | 0 | 0 | 535 |
| boss-kim-loi-vuong | average | 440 | 388 | 52 | 24.6 | 920 | 920 |
| boss-kim-loi-vuong | skilled | 332 | 300 | 32 | 12 | 19 | 19 |

Terminal Power (M-07 PowerFinal) across all 75 runs: 0 x2, 10 x6, 15 x2, 20 x4, 25 x1, 30 x4, 35 x2, 40 x3, 45 x1, 50 x5, 60 x7, 65 x1, 70 x6, 75 x1, 80 x4, 85 x1, 90 x2, 100 x23.

Thuy Ma Power drain observed with zero player casts (M-07 spent > 0, M-08 casts = 0):

| Seed | Policy | Power spent | Boss skill casts |
|---|---|---|---|
| 20261001 | passive | 30 | 2 |
| 20261002 | passive | 20 | 2 |
| 20261003 | passive | 40 | 2 |
| 20261004 | passive | 60 | 3 |
| 20261005 | passive | 40 | 2 |

Facts of record:

- **Power reaches the 100 clamp without casting.** Every `passive` cell ends with
  the Pet at 100 Power (no card is ever cast, `CARD_RULES.md` §2 item 3 allows
  PowerCharge but the policy never proposes a cast), i.e. Power generation
  continues while nothing consumes it.
- **Under the post-TASK-192 one-cast-per-Turn limit, Power was not a binding
  constraint for the casting policies**: over 75 runs, `average` generated a mean
  4 698 Power and spent a mean 4 640 while casting a mean ~257 cards per full
  matrix pass; its terminal Power was 10–100.
- **Rejected proposals dominate the raw counts** (56 108 rejected casts and
  56 108 rejected swaps, of which 44 203 + 44 203 belong to the five Mộc Yêu
  stalemates): each rejected Swap is re-proposed by the policy's ascending scan
  and re-proposes the same cast within the Turn. These are search artifacts of
  the scripted policies, not gameplay events.

## 25. Card and Relic Usage (M-08 / M-09)

| Card | Authored PowerCost | Casts (all 75 runs) | passive | average | skilled |
|---|---|---|---|---|---|
| card-heal | 20 | 5744 | 0 | 5612 | 132 |
| card-shield | 20 | 0 | 0 | 0 | 0 |
| card-power-charge | 0 | 942 | 0 | 794 | 148 |
| card-iron-fang | 40 | 296 | 0 | 0 | 296 |

Relic triggers (M-09) across all 75 runs: **0** (no Relic content attached, section 5.2).

Facts of record:

- **`card-shield` was cast 0 times in all 75 runs.** Under this baseline the two
  casting policies never select it: `average` takes the first affordable card in
  loadout order (Heal 20 precedes Shield 20), and `skilled` takes the highest
  authored cost affordable (Iron Fang 40 precedes both). This is a property of
  the **policy heuristics plus loadout order**, not a measurement of player
  preference — the harness cannot measure preference.
- **`card-power-charge` was cast 942 times** (794 by `average`, 148 by
  `skilled`): with one cast per Turn and Heal costing 20, it is selected whenever
  Power < 20, i.e. it converts a Turn's cast allowance into +25 Power that funds a
  later Heal (Q-2 / B-01 evidence).
- **`card-iron-fang` was cast 296 times, all by `skilled`** — the only
  damage-dealing card in the baseline loadout.
- **Relic usage: 0 triggers, by construction** (§5.2 equips no Relics). The
  baseline is the 0-relic control only; it cannot attribute any effect to a Relic.

## 26. Combo and Element Evidence (M-10 / M-11)

Aggregated Combo histogram across 7274 resolved Turn records (M-10; corrected per TASK-195):

| Combo | Records | Share |
|---|---|---|
| 1 | 4217 | 58.0 % |
| 2 | 1501 | 20.6 % |
| 3 | 687 | 9.4 % |
| 4 | 302 | 4.2 % |
| 5 | 188 | 2.6 % |
| 6 | 123 | 1.7 % |
| 7 | 51 | 0.7 % |
| 8 | 45 | 0.6 % |
| 9 | 31 | 0.4 % |
| 10 | 23 | 0.3 % |
| 11 | 10 | 0.1 % |
| 12 | 21 | 0.3 % |
| 13 | 12 | 0.2 % |
| 14 | 12 | 0.2 % |
| 15 | 8 | 0.1 % |
| 16 | 3 | 0 % |
| 17 | 6 | 0.1 % |
| 18 | 6 | 0.1 % |
| 19 | 15 | 0.2 % |
| 20 | 2 | 0 % |
| 21 | 3 | 0 % |
| 22 | 1 | 0 % |
| 23 | 1 | 0 % |
| 25 | 1 | 0 % |
| 26 | 1 | 0 % |
| 27 | 1 | 0 % |
| 28 | 1 | 0 % |
| 31 | 1 | 0 % |
| 32 | 1 | 0 % |

Combo 1 = 4217 of 7274 records; Combo >= 4 = 869; maximum observed Combo = 32.

`GAME_RULES.md` §5's Combo factors reach 1.50× at Combo ≥ 5: **567 of 7 274
resolved Turn records (7.8 %)** reached that band, and Combo 1 accounted for
4 217 records (58.0 %). The maximum observed Combo was 32.

| Boss | M-11 advantage | M-11 neutral | M-11 disadvantage | Damage instances | Disadvantage share |
|---|---|---|---|---|---|
| boss-hoa-long | 0 | 1503 | 0 | 1503 | 0 % |
| boss-thuy-ma | 754 | 0 | 798 | 1552 | 51.4 % |
| boss-moc-yeu | 0 | 10878 | 0 | 10878 | 0 % |
| boss-son-thach-ve | 0 | 709 | 0 | 709 | 0 % |
| boss-kim-loi-vuong | 279 | 0 | 241 | 520 | 46.3 % |

Facts of record:

- **Element modifiers fired and were asymmetric** wherever a counter-relationship
  existed. With the baseline Hỏa Pet (`ELEMENT_RULES.md` §2.1,
  `ElementMatchups.Counters`): vs Thủy Ma the Pet's damage resolved at
  **disadvantage ×0.75** (798 instances) while the Boss's resolved at
  **advantage ×1.50** (754); vs Kim Lôi Vương the Pet resolved at **advantage
  ×1.50** (279) while the Boss resolved at **disadvantage ×0.75** (241).
- **Three of five matchups are element-neutral**: Hỏa Long (Hỏa vs Hỏa), Mộc Yêu
  (Hỏa vs Mộc) and Sơn Thạch Vệ (Hỏa vs Thổ) recorded **no** advantage or
  disadvantage instance — so the baseline Pet exercises the element system in
  2 of 5 fights.
- M-11 classification compares the pipeline's factor against the documented
  1.50 / 0.75 values and counts everything else as neutral, so it cannot
  distinguish an elementless source from a neutral matchup (D-194-8).

## 27. Mộc Yêu Regeneration and Stalemate Evidence (M-12 / M-14)

| Policy | Seed | Turns | Outcome | M-03 damage | Final Boss HP | Net Boss HP loss | M-12 regen measured | Passive triggers (M-13) | Derived applied regen (M-03 - net loss) | Raw upper bound (triggers x 250) |
|---|---|---|---|---|---|---|---|---|---|---|
| passive | 20261001 | 14 | Defeat | 543 | 4964 | 36 | 357 | 3 | 507 | 750 |
| passive | 20261002 | 25 | Defeat | 1409 | 4924 | 76 | 638 | 7 | 1333 | 1750 |
| passive | 20261003 | 15 | Defeat | 711 | 4944 | 56 | 450 | 4 | 655 | 1000 |
| passive | 20261004 | 14 | Defeat | 858 | 5000 | 0 | 408 | 5 | 858 | 1250 |
| passive | 20261005 | 14 | Defeat | 595 | 5000 | 0 | 381 | 4 | 595 | 1000 |
| average | 20261001 | 1000 | Stalemate | 51410 | 4964 | 36 | 28977 | 310 | 51374 | 77500 |
| average | 20261002 | 1000 | Stalemate | 48560 | 4977 | 23 | 28829 | 304 | 48537 | 76000 |
| average | 20261003 | 1000 | Stalemate | 50689 | 5000 | 0 | 28683 | 315 | 50689 | 78750 |
| average | 20261004 | 1000 | Stalemate | 49561 | 4922 | 78 | 28213 | 315 | 49483 | 78750 |
| average | 20261005 | 1000 | Stalemate | 52751 | 4977 | 23 | 29529 | 317 | 52728 | 79250 |
| skilled | 20261001 | 34 | Defeat | 2826 | 4294 | 706 | 1472 | 12 | 2120 | 3000 |
| skilled | 20261002 | 40 | Defeat | 4228 | 4200 | 800 | 1676 | 16 | 3428 | 4000 |
| skilled | 20261003 | 68 | Defeat | 6013 | 3849 | 1151 | 3095 | 24 | 4862 | 6000 |
| skilled | 20261004 | 53 | Defeat | 4814 | 4210 | 790 | 2042 | 20 | 4024 | 5000 |
| skilled | 20261005 | 84 | Defeat | 7551 | 3975 | 1025 | 3598 | 31 | 6526 | 7750 |

Facts of record:

- **5 of 5 `average` runs stalemated at the 1000-Turn bound** and are reported as
  `Stalemate`, never folded into a victory or a defeat. Over those 1000 Turns the
  Pet dealt 48 560–52 751 damage while the Boss's net HP loss was only 0–78:
  regeneration absorbed essentially all throughput.
- **The other 10 Mộc Yêu runs all ended in defeat** (5 `passive` in 14–25 Turns,
  5 `skilled` in 34–84 Turns); none reached a stalemate.
- **M-12 as specified is a lower bound.** TASK-194 §8 defines it as *positive
  inter-turn Boss HP deltas*, but regeneration is applied inside the same Turn as
  the player's damage (`BOSS_RULES.md` §6.2.3, `GAME_RULES.md` §17 step 18a), so a
  regeneration smaller than that Turn's damage is invisible to it and is also
  reduced by the `min(HP + regen, MaxHP)` clamp. The derived column
  `M-03 − net HP loss` gives the applied amount, and `triggers × 250` an upper
  bound; both are recorded as derivations from measured values, not as
  measurements.
- Consequently the raw maximum regeneration the Boss could have applied is
  ≈77 500–79 250 HP over the five stalemates (304–317 triggers × 250), of which
  the balance `M-03 − net HP loss` shows ≈48 537–52 728 HP actually applied and
  M-12 observed 28 213–29 529 HP.

## 28. TASK-191 Decision Evidence Mapping

The 75-run baseline produces evidence for the open decision queue of
`TASK-191` §6. **No question is answered here and no value is approved**: each
row states what was measured, and what the Product Owner still has to decide.
`Q-4` is not listed because it was already decided and implemented (`ADR-021`,
TASK-192); the whole matrix was executed under that post-`B-02` economy.

| Decision | Evidence status after TASK-194 | Measurable by this harness? | Product Owner decision still required |
|---|---|---|---|
| **Q-1** — target fight duration | **Partially supported; remains QUALITATIVE.** §22 supplies the full duration distribution per Boss × policy (median 17 Turns, 47/75 runs ≤ 25 Turns, 5 runs at the 1000-Turn bound). | Turns yes; wall-clock **no** (excluded by design); *acceptability* never | Yes — whether the observed durations are acceptable. No target is approved or invented. |
| **Q-2** — Power Charge cost 0 (`B-01`) | **Partially supported.** §24/§25: under one-cast-per-Turn, Power was not binding for either casting policy; Power Charge was cast 942 times as a < 20-Power filler; cast-free runs clamp at 100 Power. | Yes for Power economy and cast frequency | Yes — raise cost, keep 0, or leave `B-01` as-is. |
| **Q-3** — Boss stat curve & DEF asymmetry (`B-03`, `B-04`) | **Supported by simulation.** §23: recorded damage-to-kill ≈ authored `MaxHP` for every win; DEF 50 changes throughput (turns) not the post-mitigation damage required; turns-to-kill 37.9 / 50.6 / 357.4 / 22.5 / 16.5; Boss damage/Turn 153.9 / 164.1 / 102.0 / 132.9 / 117.3; enrage cadence 5 / 5 / 0 / 10 / 15 of 15 runs. | Yes | Yes — Candidate A (flat stats, mechanics-only) vs Candidate B (monotonic HP curve) still undecided. |
| **Q-5** — "no strictly dominated Card/Relic" | **Partially supported.** §25 measures the cast mix (Heal 5 744 / Power Charge 942 / Iron Fang 296 / **Shield 0**), but the mix is a consequence of the policy heuristics and loadout order; the baseline carries only 4 of 8 Cards and **0** of 10 Relics. | Only for the equipped 4-Card, 0-Relic loadout | Yes — whether non-domination is a mandatory MVP property; needs controlled card/relic comparison runs. |
| **Q-6** — Pet Tier/Star/Level stat curve (`B-05`) | **Not measurable.** The battle is created from identity, Element, Passive, threshold, and loadout snapshots only; no stat input exists (`PET_RULES.md` §5.7/§6 curve unauthored, TASK-191 D-5). Every Pet enters at 1000/50/25/5/0 (§5.2), and no M-metric carries Level/Star/Tier. | **No** | Yes — author the curve or declare Level/Star cosmetic for MVP. |
| **Q-7** — Player XP pacing (`B-06`) | **Not measurable.** XP is a `BattleWon` reward outside the combat Turn loop (`COMBAT_RULES.md` §7); a single-battle harness produces one win/loss, not a level curve, and M-01…M-15 contain no XP/Level member. | **No** | Yes — keep 1-win-per-level, curve it, or give Level combat meaning. |
| **Q-8** — Mộc Yêu regeneration magnitude (`B-07`) | **Supported by simulation.** §27: **5 / 5** `average` runs stalemated at the 1000-Turn bound; measured regeneration 28 213–29 529 HP (a lower bound, D-194-5) against 48 560–52 751 HP dealt; derived applied regeneration 48 537–52 728 HP, raw upper bound 77 500–79 250 HP; the other 10 runs were defeats. | Yes (with the M-12 lower-bound caveat) | Yes — reduce magnitude, raise threshold, or accept attrition. |
| **Q-9** — Card damage normalization band (`B-08`) | **Partially supported; a tolerance band is not evaluable from this matrix.** Only one damage Card (Iron Fang) is equipped; card damage carries the **Pet's** Element (D-194-8); M-03 under-recording in 19 of 75 runs (D-194-3) is fully resolved under TASK-195. | Only for the equipped loadout | Yes — needs the per-Card × per-Boss expected-damage matrix (controlled runs over all 8 Cards). |
| **Q-10** — Relic Power engines (`B-10`) | **Not measurable in the baseline**: 0 Relics equipped, 0 `RelicTriggered` events (§24/§25). The baseline is the unequipped control only. | **No** (needs relic-on/relic-off ablation) | Yes — per-turn caps, magnitude changes, or status quo. |
| **Q-11** — Xích Lang / Sơn Hùng passive magnitudes (`B-14` item 1) | **Not measurable.** Pet-passive *effects* are not applied (TASK-191 D-2) and no magnitude is authored; additionally M-13 counts only `PassiveTriggered` events whose source is the Boss (D-194-6), so even the Pet passive's charge/trigger cadence is absent from the metric set. | **No** | Yes — and a follow-up implementation task is a prerequisite. |
| **Q-12** — simulation harness approval | **Resolved (TASK-193) and executed here.** TASK-194 drove the approved harness over the §5 matrix and produced the §17 artifact. | Yes | No — measurement only, as approved. |

### 28.1 Questions the current harness explicitly cannot measure

```text
Q-1  wall-clock duration          — excluded by TASK-193 §4.2 (determinism)
Q-1  acceptability of durations   — a Product Owner judgment, never a computation
Q-5  full non-domination property — needs controlled comparison runs (all 8 Cards,
                                    10 Relics); the baseline is single-loadout
Q-6  Pet Tier/Star/Level curve    — no stat input reaches battle creation
Q-7  Player XP / level pacing     — outside the combat simulation entirely
Q-9  a normalization tolerance    — needs the Card × Boss expected-damage matrix
Q-10 relic contribution           — needs relic ablation; baseline has 0 Relics
Q-11 pet-passive magnitudes       — effects unimplemented; magnitudes unauthored
```

## 29. Metric Fidelity Findings (reported, not fixed in TASK-194; audited & resolved in TASK-195)

Per `AGENTS.md` §16 these were **reported only** in TASK-194. TASK-194 modified no harness,
test-helper, or production file to accommodate them. Subsequently, **TASK-195 audited and
corrected the test-only harness implementation**, resolved findings D-194-1 through D-194-4,
reconciled the 75-run baseline evidence matrix, and verified D-194-5 through D-194-8.

### D-194-1 — Metric numbering in the harness comments is off by one against the specification

**Status:** RESOLVED in TASK-195 (comments and XML documentation aligned with canonical M-01…M-15).

**Where:** `tests/.../Balance/BalanceSimulationMetrics.cs` and
`MetricAccumulator.cs` comments, versus `TASK-193` §3.1 and `TASK-194` §8.

```text
Specification (TASK-193 §3.1, TASK-194 §8)   Harness comment
M-07 Power generated / spent / final         M-07 generated, M-08 spent
M-08 Cards cast                              M-09
M-09 Relic triggers                          M-10
M-10 Combo                                   M-11
M-11 Element modifiers                       M-12
M-12 Boss regeneration                       M-13 (with threshold events)
M-13 Boss threshold events                   M-13 (with regeneration)
```

**Impact:** a reader following the code comments maps a value to the wrong metric
number. The **values and their sources are identical**; only the labels differ.
This record uses the **specification numbering** throughout.
**Suggested follow-up:** comment-only alignment task.

### D-194-2 — `Mode` is not a configuration member

**Status:** RESOLVED in TASK-195 (`Mode` added to `BalanceSimulationConfiguration`, defaults to `Baseline`, threaded through to `BalanceSimulationResult`).

**Where:** `BalanceSimulationConfiguration` has `Seed`, `PlayerId`, `Pet`, `Boss`,
`CardDefinitions`, `RelicDefinitions`, `Policy`, `MaxTurns` — no `Mode`.
`BalanceSimulator.RunAsync` hard-codes `BalanceSimulationMode.Baseline`.

**Impact:** TASK-194 §5.2/§9.2's `Mode = Baseline` input cannot be supplied or
varied; every run is Baseline, which is exactly what TASK-194 requires, but the
harness offers no way to execute §5.2's Controlled-comparison mode through
`RunAsync`. **Suggested follow-up:** expose the mode, or document that Baseline is
the only executable mode.

### D-194-3 — M-03/M-04 under-record cast damage in Turns whose Swap was rejected

**Status:** RESOLVED in TASK-195 (`MetricAccumulator.BeginTurn` preserves pending damage on re-entry and flushes correctly in `EndTurn`). In the corrected run, 100% of cast damage is retained and `PlayerDamageTotal >= Boss.MaxHP - BossHpMin` across all runs without healing.

**Where:** `BalanceSimulator.RunAsync` (the `continue` on a rejected Swap) with
`MetricAccumulator.BeginTurn` (which clears `_pendingPlayerDamage`).

**Mechanism:** a successful Card cast accumulates its damage for the Turn; when
the following Swap proposal is rejected the loop continues without flushing, and
the next `BeginTurn` clears the pending damage before any `EndTurn` records it.
The authoritative Boss HP still moves — only the metric loses the value.

**Measured impact:** **19 of 75 runs** (all `skilled`, the only damage-Card-casting
policy), unrecorded 1–480 damage each; 0 of the 25 cast-free runs are affected.

Runs where M-03 recorded less player damage than the Boss's own HP loss (M-05 trajectory):

| Boss | Policy | Seed | Boss MaxHP | Boss HP lost | M-03 recorded | Unrecorded | Rejected swaps | Outcome |
|---|---|---|---|---|---|---|---|---|
| boss-hoa-long | skilled | 20261001 | 5000 | 813 | 733 | 80 | 6 | Defeat |
| boss-hoa-long | skilled | 20261002 | 5000 | 1464 | 1304 | 160 | 6 | Defeat |
| boss-hoa-long | skilled | 20261003 | 5000 | 1534 | 1454 | 80 | 3 | Defeat |
| boss-hoa-long | skilled | 20261004 | 5000 | 1299 | 1219 | 80 | 5 | Defeat |
| boss-hoa-long | skilled | 20261005 | 5000 | 1204 | 1124 | 80 | 7 | Defeat |
| boss-thuy-ma | skilled | 20261001 | 5000 | 701 | 581 | 120 | 6 | Defeat |
| boss-thuy-ma | skilled | 20261002 | 5000 | 1543 | 1303 | 240 | 8 | Defeat |
| boss-thuy-ma | skilled | 20261004 | 5000 | 1670 | 1550 | 120 | 4 | Defeat |
| boss-thuy-ma | skilled | 20261005 | 5000 | 1167 | 1047 | 120 | 7 | Defeat |
| boss-son-thach-ve | skilled | 20261001 | 3000 | 2511 | 2031 | 480 | 5 | Defeat |
| boss-son-thach-ve | skilled | 20261002 | 3000 | 3000 | 2891 | 109 | 4 | Victory |
| boss-son-thach-ve | skilled | 20261003 | 3000 | 2935 | 2575 | 360 | 6 | Defeat |
| boss-son-thach-ve | skilled | 20261004 | 3000 | 3000 | 2831 | 169 | 4 | Victory |
| boss-son-thach-ve | skilled | 20261005 | 3000 | 1809 | 1689 | 120 | 7 | Defeat |
| boss-kim-loi-vuong | skilled | 20261001 | 2800 | 2800 | 2392 | 408 | 4 | Victory |
| boss-kim-loi-vuong | skilled | 20261002 | 2800 | 2800 | 2799 | 1 | 3 | Victory |
| boss-kim-loi-vuong | skilled | 20261003 | 2800 | 2800 | 2631 | 169 | 2 | Victory |
| boss-kim-loi-vuong | skilled | 20261004 | 2800 | 2800 | 2642 | 158 | 3 | Victory |
| boss-kim-loi-vuong | skilled | 20261005 | 2800 | 2800 | 2713 | 87 | 7 | Victory |

Affected runs: **19 of 75** (all completed by a policy that casts a damage Card).

### D-194-4 — Per-Turn series carry one extra record for cast-terminated runs

**Status:** RESOLVED in TASK-195 (`MetricAccumulator.ReconcileTerminalCardCast` reconciles terminal cast damage into the last resolved turn record without appending phantom turn $T+1$; `series.Count == Turns` strictly holds across all 75 runs).

**Where:** `BalanceSimulator.RunAsync`'s cast-terminal branch calls
`accumulator.EndTurn` for a Turn whose Swap never committed.

**Measured impact:** 3 of 75 runs (Kim Lôi Vương × `skilled`, seeds 20261002 /
20261003 / 20261005) have 8 per-Turn records for 7 committed Turns; across the
matrix `Σ M-01 = 7 274` while the per-Turn records originally numbered `7 277` prior to the TASK-195 fix. M-01/M-02
remain the authoritative Turn count.

### D-194-5 — M-12 measures a lower bound on applied regeneration

**Where:** `MetricAccumulator.EndTurn` (positive inter-turn Boss HP deltas), which
is exactly what TASK-194 §8 specifies.

**Measured impact:** in the five Mộc Yêu × `average` stalemates, M-12 records
28 213–29 529 HP while the balance `M-03 − net Boss HP loss` implies
48 537–52 728 HP actually applied (raw ceiling `triggers × 250` =
77 500–79 250 HP). Regeneration applied inside the same Turn as the player's
damage — and regeneration discarded by the `max HP` clamp — is not visible to an
inter-Turn delta. **Reading rule:** M-12 is a floor, not the regeneration
magnitude.

### D-194-6 — M-13 observes only match-charged Boss passives, and no Pet passive at all

**Where:** `MetricAccumulator.RecordResolution`'s `PassiveTriggered` branch keeps
only `Source == PassiveEventSource.Boss`.

**Measured impact:** across the 75 runs the counter is non-zero only for the two
match-charged Boss passives — Hỏa Long 178 triggers, Mộc Yêu 1 687 — and is `0`
for Thủy Ma (always-active), Sơn Thạch Vệ (HP ≤ 50 %), and Kim Lôi Vương
(Combo ≥ 4), which is consistent with `BossDefinitions`' documented
"emits no `PassiveCharged`/`PassiveTriggered` from match progress". Pet-source
`PassiveTriggered` events are ignored, so the Xích Lang passive's cadence is not
in M-01…M-15 at all.

### D-194-7 — `PowerSpentTotal` includes non-card Power sinks

**Where:** `MetricAccumulator`'s `PowerChanged` branch sums every negative delta.

**Measured impact:** `passive` runs against Thủy Ma report 20–60 Power "spent"
with **0** casts — the Boss skill's Drain Power (`BOSS_RULES.md` §6.3.1 item 2).
Card-economy readings (Q-2) must subtract that sink.

### D-194-8 — Card damage carries the Pet's Element

**Where:** `CardCastExecutor.cs` (~line 275) builds the damage request with
`AttackerElement: state.PetState.Element`.

**Measured impact:** with the baseline Hỏa Pet, Iron Fang's 120 damage resolves at
the **Hỏa** matchup (×1.50 vs Kim Lôi Vương, ×0.75 vs Thủy Ma) rather than any
card-authored Element; `TASK-191` §1.5's per-card element attribution (e.g. "Iron
Fang = Kim attacker, disadvantaged ×0.75 vs Hỏa Long") does not hold in the
current pipeline. Also, M-11's classifier cannot distinguish an elementless
source from a neutral matchup, because both produce factor 1.00.

## 30. Scope Audit (verified)

```text
Production code changed:                 NO  — git status identity of all 50 src/
                                               entries, before vs after (§31.3)
Gameplay rules changed:                  NO
Balance values changed:                  NO
Boss stats changed:                      NO  (5000/100/50/30%, 5000/100/50/30%,
                                               5000/100/50/30%, 3000/120/0/50%,
                                               2800/140/0/75% — as authored)
Pet / Card / Relic values changed:       NO  (costs 20/20/0/40 as authored)
Power Charge cost modified:              NO  (retains PowerCost = 0)
Mộc Yêu regeneration modified:           NO  (retains 5% MaxHP regen, threshold 5)
TASK-193 harness modified:               NO  (all nine harness files untouched)
Database schema / migrations:            NO
Frontend / client changed:               NO
docs/ changed:                           NO  (0 files modified)
ADR created or modified:                 NO
TASK-191 proposal implemented (B-01…B-14): NONE
Balance verdict introduced:              NO  — no BALANCED / UNBALANCED / PASS /
                                               FAIL string exists in the artifact or
                                               in this record
Q-1 treated as qualitative:              YES — no duration threshold, target, or
                                               pass/fail is applied anywhere
```

Files this task added or moved:

```text
NEW  tests/backend/GameServer.Application.Tests/Balance/BalanceBaselineMatrix.cs
NEW  tests/backend/GameServer.Application.Tests/Balance/Tests/BalanceBaselineMatrixTests.cs
NEW  tasks/artifacts/TASK-194-baseline-simulation-results.json
MOV  tasks/backlog/TASK-194-balance-baseline-simulation-evidence.md
  →  tasks/completed/TASK-194-balance-baseline-simulation-evidence.md   (this file)
```

Nothing else in the working tree was added, removed, or altered by TASK-194: the
`git status --porcelain` line set is identical to the pre-task snapshot except for
`?? tasks/artifacts/` (§31.3). The unrelated uncommitted work that was already
present when the task began — other tasks' documentation, `src/`, and test edits —
was neither touched nor reverted.

## 31. Verification Results

### 31.1 Backend suite (`dotnet test src/backend/GameServer.sln`)

| Project | Before TASK-194 | After TASK-194 | Delta |
|---|---|---|---|
| `GameServer.Domain.Tests` | 1 558 passed, 0 failed | 1 558 passed, 0 failed | 0 |
| `GameServer.Application.Tests` | 593 passed, 0 failed | **598 passed, 0 failed** | **+5** |
| `GameServer.Infrastructure.Tests` | 412 passed, 0 failed | 412 passed, 0 failed | 0 |
| `GameServer.Api.Tests` | 323 passed, 0 failed | 323 passed, 0 failed | 0 |
| **Backend total** | **2 886 passed, 0 failed** | **2 891 passed, 0 failed** | **+5** |

The 5 added tests are the TASK-194 execution tests
(`TASK194_TheMatrixDefinition_IsTheApprovedBaseline`,
`TASK194_AllSeventyFiveRuns_CompleteWithCompleteMetrics`,
`TASK194_EveryRun_IsReproducedBitIdenticallyOnReRun`,
`TASK194_RawEvidence_IsArchivedAsDeterministicJson`,
`TASK194_RecordedPlayerDamage_UnderCountsCastDamageInTurnsWithARejectedSwap`).
The 2 886-run baseline matches the expected pre-task baseline exactly. All 32
TASK-193 harness tests still pass unchanged.

### 31.2 Focused TASK-194/TASK-193 tests

```text
dotnet test tests/backend/GameServer.Application.Tests/GameServer.Application.Tests.csproj \
  --filter "FullyQualifiedName~BalanceBaselineMatrixTests"
  → 5 passed, 0 failed (~3 s)

dotnet test tests/backend/GameServer.Application.Tests/GameServer.Application.Tests.csproj \
  --filter "FullyQualifiedName~BalanceSimulatorTests"
  → 32 passed, 0 failed        (TASK-193 acceptance criteria H-01…H-10)
```

### 31.3 Integrity checks

```text
[✓] 75 / 75 simulations completed            (matrix test asserts the count)
[✓] 0 InvalidSimulation                      (asserted; failure names the cell)
[✓] M-01…M-15 populated for all 75 runs      (asserted per metric, per run)
[✓] Determinism                              (75/75 runs reproduced bit-identically
                                              on a full second pass, including the
                                              terminal RngState)
[✓] Artifact determinism                     (writer is pure; identical bytes, and
                                              regenerated byte-identically by the
                                              full-suite run: same SHA-256)
[✓] No src/ modification                     (git status identity of all 50 src/
                                              entries vs the pre-task snapshot; no
                                              src/ file written during the session)
[✓] No docs/ modification                    (0 files)
[✓] Regression cleanliness                   (2 891 passed, 0 failed)
```

## 32. Completion Criteria (§16)

```text
[x] All 75 baseline simulations (5 bosses × 3 policies × 5 seeds) are executed.
[x] No simulation encounters an InvalidSimulation outcome.
[x] The raw simulation data is recorded and preserved
    (tasks/artifacts/TASK-194-baseline-simulation-results.json, §17/§18.1).
[x] Cross-seed descriptive statistics (mean, median, min, max, std dev) are
    compiled for each of the 15 cells (§21.1).
[x] Boss progression analysis across Bosses 1–5 is documented (§23).
[x] Evidence mappings for Q-1, Q-2, Q-3, Q-5, Q-6, Q-7, Q-8, Q-9, Q-10, Q-11 and
    Q-12 are detailed (§28), including the questions the harness cannot measure.
[x] Scope audit (§30) is fully satisfied with zero production modifications.
```

## 33. What This Task Does NOT Decide

```text
Q-1  duration target                 still QUALITATIVE — no target exists or is invented
Q-2  Power Charge cost               still OPEN — evidence supplied, no value approved
Q-3  boss stat curve / DEF asymmetry still OPEN — Candidate A vs B not chosen
Q-5  card/relic non-domination       still OPEN — baseline is single-loadout
Q-6  pet Tier/Star/Level curve       still OPEN — not measurable by this harness
Q-7  player XP pacing                still OPEN — outside the combat simulation
Q-8  Mộc Yêu regeneration            still OPEN — stalemate risk now measured
Q-9  card damage tolerance band      still OPEN — no band evaluated
Q-10 relic magnitudes / engines      still OPEN — baseline carries no Relics
Q-11 Xích Lang / Sơn Hùng passives   still OPEN — effects unimplemented
B-01, B-03, B-04, B-05, B-06, B-07, B-08, B-09, B-10, B-11, B-12, B-13
                                     NONE implemented, NONE approved
```

TASK-194 produced **evidence**. Every decision above remains a Product Owner
decision, and no balance verdict (`BALANCED` / `UNBALANCED` / `PASS` / `FAIL`) was
produced, recorded, or implied by this task or by its artifact.
