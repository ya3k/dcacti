# TASK-196 — Balance Decision Evidence Analysis & Decision-Support Audit

<!--
  GEN-TASK EXECUTION MANIFEST — ANALYSIS & SPECIFICATION ONLY
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.

  References docs/, tasks/, and tests/ by path and section — does NOT copy
  formulas, schemas, or payload shapes verbatim.

  SCOPE OF THIS TASK: Perform a decision-support audit for TASK-191 using the
  corrected TASK-194 baseline evidence (audited and verified under TASK-195).
  ANALYSIS ONLY — zero production code changes, zero gameplay rule changes, zero
  balance values changed, zero database changes, zero frontend changes, zero ADRs.
  Evaluates questions Q-1 through Q-12, categorizing each by resolution status,
  identifying policy artifacts versus balance behaviors, and specifying minimal
  controlled experiments where empirical gaps remain.
-->

---

## Metadata

```text
Task ID:           TASK-196
Type:              Analysis / Decision Support (Specification Only)
Status:            READY
Date:              2026-10-05
Evidence Base:     TASK-194 baseline simulation results (tasks/artifacts/TASK-194-baseline-simulation-results.json;
                   SHA-256 4F9B5077B9320B09BD83C9144FF9B1234566D067B5FE0EF9BCD85FCB1CE6059F),
                   corrected and audited under TASK-195
Governing Task:    TASK-191 (Balance Pass Specification)
Harness Ref:       TASK-193 (Deterministic Balance Simulation Harness)
Audit Ref:         TASK-195 (Balance Evidence Measurement Audit & Correction)
Production Change: NONE (0 files in src/ modified)
Gameplay Rules:    NONE (0 rules modified)
Balance Values:    NONE (no numbers changed or approved)
Balance Verdicts:  NONE (zero BALANCED / UNBALANCED / PASS / FAIL labels emitted)
Risk:              LOW (analysis and experiment specification only)
Priority:          HIGH (gates resolution of open TASK-191 balance questions)
Primary Agent:     design
Supporting Agents: review, testing
Workflow:          documentation/documentation-change.md
Model:             Gemini 3.8
Reasoning:         High
```

---

## 1. Executive Summary

This document conducts a rigorous **decision-support audit of TASK-191** using the **corrected TASK-194 baseline simulation evidence** (reconciled and verified under `TASK-195`). It evaluates open decision questions **Q-1 through Q-12** against 75 deterministic simulation runs (5 Bosses × 3 Policies × 5 Seeds) executing against authoritative domain logic (`BattleStateService`, `SwapExecutor`, `CardCastExecutor`, `DamagePipeline`, `ResourceGenerator`, `BossResponse`, `StatusEffectLifecycle`, `PassiveTracker`).

### 1.1 Core Findings

1. **Policy Artifacts vs. Authored Balance**:
   A primary finding of this audit is that observed battle outcomes and card usage in the baseline matrix are heavily driven by **scripted policy heuristics and loadout ordering** rather than authored balance alone:
   - The sustain-first policy (`average`: first affordable card in loadout order $\to$ Heal) won 20 of 25 runs and stalemated 5, whereas the damage-first policy (`skilled`: highest authored cost $\to$ Iron Fang) won only 7 of 25 runs despite dealing 1.5× to 2.5× higher player damage per turn.
   - `card-shield` recorded **0 casts across all 75 runs** exclusively because both casting policies prioritized other cards (`Heal` at identical cost 20 in `average`; `Iron Fang` at cost 40 in `skilled`). Shield was never evaluated in isolation; its 0-cast metric is a policy selection artifact, not evidence of functional defect or lack of viability.
   - Never casting cards (`passive`) was uniformly fatal across all 5 Bosses (0/25 wins; 7–25 turns duration), demonstrating that active sustain or burst via cards is mandatory under baseline combat tunings.

2. **Empirical Resolutions & Systemic Gaps**:
   - **Q-4 (One card cast per turn)**: Fully resolved, implemented under `ADR-021` / `TASK-192`, and empirically validated. Zero infinite loops occurred; maximum 1 cast per turn was enforced across all 7,274 resolved turn records.
   - **Q-8 (Mộc Yêu regeneration)**: Conclusively proven to cause stalemates under sustain policy (5/5 runs reached the 1000-turn cap with $\ge 4922$ remaining Boss HP after absorbing 48,560–52,751 player damage). Regeneration absorbs $\approx 99.8\%$ of sustain throughput and defeats damage-first policy (0/5 wins).
   - **Q-3 (Boss progression curve)**: Empirically confirmed to be **stat-flat across Bosses 1–3 and HP/DEF-inverted across Bosses 4–5**. Fights against Bosses 4 and 5 are shorter (mean 22.5 and 16.5 turns vs 37.9 and 50.6 turns) and yield higher player win rates (7/15 and 10/15 vs 5/15 and 5/15) because Bosses 4 and 5 have 0 DEF and lower HP, ending before high ATK and 0-CD skills can overwhelm the player.
   - **Q-12 (Simulation harness)**: Fully resolved and delivered (`TASK-193`, `TASK-194`, `TASK-195`).

3. **Classification of Open Questions (Q-1…Q-12)**:
   - **`EVIDENCE-SUPPORTED` (Resolved from existing evidence)**: **Q-4**, **Q-12**.
   - **`NEEDS CONTROLLED EXPERIMENT` (Partially informed, requires isolated runs)**: **Q-2**, **Q-5**, **Q-8**, **Q-9**, **Q-10**.
   - **`NOT MEASURABLE` (Outside current harness capability)**: **Q-1 (wall-clock)**, **Q-6**, **Q-7**, **Q-10 (from baseline alone)**, **Q-11**.
   - **`DESIGN DECISION` (Requires Product Owner policy choice)**: **Q-1**, **Q-2**, **Q-3**, **Q-5**, **Q-6**, **Q-7**, **Q-8**, **Q-9**, **Q-11**.

### 1.2 Summary Classification Table

| Question ID | Subject | Baseline Evidence Status | Primary Recommendation | Required Next Action |
|---|---|---|---|---|
| **Q-1** | Fight duration target (Turns & wall-clock) | Turn distribution characterized (median 17 turns, bimodal); wall-clock excluded by design | `DESIGN DECISION` (Turn baseline `EVIDENCE-SUPPORTED`; wall-clock `NOT MEASURABLE`) | Product Owner must define acceptable Turn duration bands. |
| **Q-2** | Power Charge cost 0 retention (`B-01`) | 1-card/turn limit prevents free-action exploit; cast 942× as fuel; Power non-binding in baseline | `NEEDS CONTROLLED EXPERIMENT` & `DESIGN DECISION` | PO decides whether 0-cost generator is intended; simulate cost sensitivity (10 vs 20 vs 0). |
| **Q-3** | Boss stat curve & DEF asymmetry (`B-03`/`B-04`) | Bosses 1–3 flat (EHP 7500); Bosses 4–5 inverted (EHP 3000/2800, shorter turns, higher win rate) | `DESIGN DECISION` (behavior is `EVIDENCE-SUPPORTED`) | PO decides between Candidate A (mechanics-driven) vs Candidate B (monotonic HP curve). |
| **Q-4** | One card cast per turn limit (`B-02`) | Implemented (`ADR-021`/`TASK-192`); verified across 75 runs; 0 infinite loops; 3 cast victories | `EVIDENCE-SUPPORTED` | None. Resolved and validated. |
| **Q-5** | "No strictly dominated Card/Relic" property | Shield 0 casts due to policy heuristics/cost tie; baseline has 4/8 cards, 0/10 relics | `DESIGN DECISION` & `NEEDS CONTROLLED EXPERIMENT` | PO defines MVP design requirement; run loadout permutation & relic ablation matrix. |
| **Q-6** | Pet Tier/Star/Level stat curve (`B-05`) | Formula unauthored in `PET_RULES.md`; stats unpassed to `PetState`; identical defaults (1000/50/25) | `NOT MEASURABLE` & `DESIGN DECISION` | PO authors formula $f(\text{Tier}, \text{Level}, \text{Star})$ or declares Level/Star cosmetic for MVP. |
| **Q-7** | Player XP pacing curve (`B-06`) | Out-of-battle meta reward; combat harness measures in-battle loop only; Level has 0 combat stats | `NOT MEASURABLE` & `DESIGN DECISION` | PO confirms 50 wins to cap (1 win = 1 level) or approves non-linear XP curve. |
| **Q-8** | Mộc Yêu regeneration magnitude (`B-07`) | 5/5 stalemates under sustain (absorbed 48k–52k HP; net loss $\le 78$ HP); 0/15 wins across all policies | `NEEDS CONTROLLED EXPERIMENT` & `DESIGN DECISION` | PO selects tuning option (magnitude vs threshold); run controlled sweep (2% vs 3% vs threshold 8). |
| **Q-9** | Card damage normalization band (`B-08`) | Baseline carried 1 damage card (Iron Fang, 296 casts); card damage uses Pet element (Hỏa) | `NEEDS CONTROLLED EXPERIMENT` & `DESIGN DECISION` | PO defines tolerance policy; run 8-card sweep across element matchups. |
| **Q-10** | Relic Power engines & balance (`B-10`) | 0 relics equipped in baseline; M-09 = 0; Combo $\ge 4$ occurs in 12.0% of turns; cascades frequent | `NEEDS CONTROLLED EXPERIMENT` (baseline alone is `NOT MEASURABLE`) | Run relic single-ablation experiment across all 10 canonical relics. |
| **Q-11** | Pet Passive magnitudes for Xích Lang / Sơn Hùng | Magnitudes unauthored in `PET_RULES.md` §8; effects unimplemented in code (D-2); M-13 counts Boss only | `NOT MEASURABLE` & `DESIGN DECISION` | PO authors magnitudes; implement Pet passive effects in follow-up task before simulating. |
| **Q-12** | Simulation harness prerequisite (`B-14` item 3) | Implemented in `TASK-193`; executed in `TASK-194`; audited & verified in `TASK-195` (2903 tests pass) | `EVIDENCE-SUPPORTED` | None. Resolved and operational. |

---

## 2. Evidence Baseline

The empirical baseline consists of the **corrected TASK-194 simulation results** (`tasks/artifacts/TASK-194-baseline-simulation-results.json`), audited and verified by `TASK-195`.

### 2.1 Technical Execution & Integrity

- **Determinism**: 75 of 75 simulations reproduced bit-identically across independent executions. The artifact SHA-256 is `4F9B5077B9320B09BD83C9144FF9B1234566D067B5FE0EF9BCD85FCB1CE6059F`.
- **Harness Corrections Applied (`TASK-195`)**:
  1. *Issue 1 / Finding D-194-3*: Restored 100% of player card damage across rejected Swap proposals in `MetricAccumulator`. Across all non-healing runs, `M-03 PlayerDamageTotal >= Boss.MaxHP - BossHpMin` is strictly satisfied.
  2. *Issue 2 / Finding D-194-4*: Reconciled terminal card casts via `MetricAccumulator.ReconcileTerminalCardCast`, ensuring `series.Count == metrics.Turns` holds strictly for all per-turn series across all 75 runs.
  3. *Issue 3 / Finding D-194-5*: Established `M-12 BossRegenerationTotal` as an authoritative empirical lower bound reflecting post-mitigation inter-turn HP deltas without synthesizing domain events.
  4. *Issue 5 / Finding D-194-2*: Exposed `Mode = BalanceSimulationMode.Baseline` as an immutable configuration property in `BalanceSimulationConfiguration`.
- **Execution Matrix Dimensions**:
  $$\text{5 Bosses} \times \text{3 Scripted Policies} \times \text{5 PRNG Seeds} = \text{75 Total Runs}$$
  - **Bosses**: Hỏa Long, Thủy Ma, Mộc Yêu, Sơn Thạch Vệ, Kim Lôi Vương (`BossDefinitions.All`).
  - **Policies**: `passive` (lowest legal swap index; 0 card casts), `average` (lowest legal swap index; first affordable card in loadout order), `skilled` (highest match length swap; highest authored cost affordable card).
  - **Seeds**: `20261001UL`, `20261002UL`, `20261003UL`, `20261004UL`, `20261005UL`.
  - **Baseline Loadout**: Active Pet Xích Lang (Hỏa, default stats: HP 1000, ATK 50, DEF 25, Crit 5, Power 0); Cards: Heal (20), Shield (20), Power Charge (0), Iron Fang (40); Relics: None (`null`); MaxTurns: 1000.

### 2.2 Aggregate Outcome Baseline

- **Total Outcomes (M-14)**:
  - Victory: **27 runs** (36.0%)
  - Defeat: **43 runs** (57.3%) — 100% resulting from Pet HP exhaustion (`PlayerHpMin = 0`)
  - Stalemate: **5 runs** (6.7%) — 100% resulting from Mộc Yêu × `average` reaching 1000 turns
  - InvalidSimulation: **0 runs** (0%)
- **Cross-Seed Divergence**: Occurred in exactly 1 cell out of 15: Sơn Thạch Vệ × `skilled` (2 Victory / 3 Defeat). In all other 14 cells, all 5 seeds produced identical terminal outcomes.
- **Duration Across All 75 Runs (M-01/M-02)**:
  - Mean: **97.0 Turns** | Median: **17 Turns** | Min: **7 Turns** | Max: **1000 Turns** | Population $\sigma$: **243.7 Turns**
  - Bimodal distribution: 47 runs $\le 25$ turns; 10 runs 26–50 turns; 2 runs 51–75 turns; 6 runs 76–100 turns; 5 runs 101–150 turns; 5 runs 1000 turns.

---

## 3. Q-1 Through Q-12 Decision Matrix

Each open question from `TASK-191` §6 is analyzed below with complete separation of factual observation from interpretation.

---

### Q-1: Target Fight Duration

#### 1. Question Restatement
> Approve a **target fight duration** (swaps and/or wall-clock per boss) for MVP? (`TASK-191` §6 Q-1; blocks B-03, B-04, B-12, B-13 from having pass/fail criteria).

#### 2. Relevant Evidence & Citations
- **TASK-194 §22 / M-01 / M-02**: All 75 runs report mean 97.0, median 17, min 7, max 1000, $\sigma$ 243.7 Turns.
- **Turns by Policy**:
  - `passive`: mean 11.8 / median 12 / min 7 / max 25 / $\sigma$ 4.0 Turns.
  - `average`: mean 256.2 / median 90 / min 21 / max 1000 / $\sigma$ 373.7 Turns.
  - `skilled`: mean 22.9 / median 17 / min 7 / max 84 / $\sigma$ 18.6 Turns.
- **Turns by Boss (Mean / Median)**:
  - Hỏa Long: 37.9 / 14 Turns (average wins: 78–96 turns; passive: 8–13; skilled: 12–19).
  - Thủy Ma: 50.6 / 17 Turns (average wins: 112–139 turns; passive: 7–8; skilled: 12–19).
  - Mộc Yêu: 357.4 / 53 Turns (average: 1000 turns; passive: 14–25; skilled: 34–84).
  - Sơn Thạch Vệ: 22.5 / 17 Turns (average wins: 36–45 turns; passive: 10–15; skilled: 12–22).
  - Kim Lôi Vương: 16.5 / 13 Turns (average wins: 21–29 turns; skilled wins: 7–17; passive: 12–19).
- **Wall-clock Duration**: 0 ms recorded (explicitly excluded by `TASK-193` §4.2 to preserve bit-identical determinism).

#### 3. Observation vs. Interpretation
- **Observation**:
  Turn duration is strongly bimodal. Fights without card healing end within 7–25 turns. Fights with sustained healing (`average`) last 78–139 turns on Bosses 1–2, 21–45 turns on Bosses 4–5, and 1000 turns on Boss 3. Fights where the player emphasizes damage cards (`skilled`) last 7–22 turns when victorious and 12–84 turns when defeated.
- **Interpretation**:
  Fight duration is driven primarily by player policy (specifically the presence and frequency of `Heal` casts) and Boss DEF/mechanics, not by cascade PRNG luck. Whether 80–140 turns for an attrition victory or 10–25 turns for a burst victory is desirable is a subjective player experience (UX) consideration.

#### 4. Evidence Sufficiency
The baseline evidence thoroughly characterizes the **observational Turn distribution** under current tunings. However, it cannot provide wall-clock duration, nor can it determine whether any duration is acceptable without an authored standard.

#### 5. Recommendation
**`DESIGN DECISION`** (Turn baseline is `EVIDENCE-SUPPORTED`; wall-clock is `NOT MEASURABLE`).
Per `TASK-191` §6 and `TASK-193` §2, Q-1 was approved as **Qualitative (Option B)**. Duration remains an observational metric; the Product Owner must decide acceptable Turn bands for MVP content before any balance pass can evaluate numeric duration criteria.

#### 6. Minimal Deterministic Experiment
None required for measurement. If the Product Owner provides candidate target Turn ranges (e.g., target 15–30 turns for normal play), the existing harness can evaluate the percentage of runs falling within that band via a configuration assertion.

---

### Q-2: Power Charge Cost 0 Retention (`B-01`)

#### 1. Question Restatement
> Is **Power Charge cost 0** retained? If not, which option: (a) raise cost to 10–25; (b) per-Turn cast limit; (c) keep? (`TASK-191` §6 Q-2; proposal B-01).

#### 2. Relevant Evidence & Citations
- **Implementation State**: Option (b) (per-Turn cast limit of 1) was approved and implemented under `ADR-021` / `TASK-192` (`CARD_RULES.md` §3 item 6).
- **TASK-194 §24 / §25 (M-07 / M-08)**:
  - `card-power-charge` was cast **942 times total** (794 times by `average`, 148 times by `skilled`).
  - In `average` runs, whenever Power was $< 20$ (cost of Heal), `Power Charge` was cast for 0 cost, granting $+25$ Power, which then funded a `Heal` on the subsequent turn.
  - In `passive` runs (0 casts), Pet Power reached the 100 maximum clamp in 23 of 25 runs (and 90 in 2 runs), purely through Match-3 gem generation.
  - Across all 75 runs, terminal Power was $\ge 50$ in 50 runs.
  - Zero infinite loops occurred; no turn executed more than 1 card cast.

#### 3. Observation vs. Interpretation
- **Observation**:
  With the 1-card-per-turn limit enforced, `Power Charge` cannot produce an infinite free-action cycle. In practice, `average` policy uses it as an alternating resource bridge to maintain near-continuous healing. In Match-3 play, Power generated from gems alone fills the meter to cap when no cards are cast.
- **Interpretation**:
  `Power Charge` at cost 0 acts as a reliable resource accelerator that smooths out dry spells in gem matching. Under 1-cast-per-turn, its power is bounded by the turn count. Raising its cost to 10–25 would convert it from a net $+25$ gain into a net $+15$ or $0$ gain, potentially slowing down heal availability.

#### 4. Evidence Sufficiency
Sufficient to demonstrate how `Power Charge` functions under current rules as a fuel source. Insufficient to predict the gameplay effect of raising its cost without a comparative simulation.

#### 5. Recommendation
**`NEEDS CONTROLLED EXPERIMENT` & `DESIGN DECISION`**.
The Product Owner must decide whether a 0-cost generator fits design intent. If cost increase options (e.g. cost 10 or 15) are considered, a controlled comparison against baseline is required to measure impact on survivability.

#### 6. Minimal Deterministic Experiment
**EXP-05 (Power Charge Cost Sensitivity)**:
- Modify `CardDefinition.PowerCost` for `card-power-charge` across 3 test variants: `0` (baseline), `10`, `20`.
- Execute 5 Bosses × 2 casting policies (`average`, `skilled`) × 5 canonical seeds = 50 runs per variant (150 total runs) in `ControlledComparison` mode.
- Measure delta in `M-01 Turns`, `M-07 PowerSpentTotal`, `M-08 CastsByCard`, and `M-14 Outcome`.

---

### Q-3: Boss Stat Curve & DEF Asymmetry (`B-03` / `B-04`)

#### 1. Question Restatement
> Accept **flat boss stats 1–3** with mechanics-only differentiation (doc-aligned), or make HP/DEF curve monotonic? (`TASK-191` §6 Q-3; proposals B-03, B-04).

#### 2. Relevant Evidence & Citations
- **Authored Configuration (`BOSS_RULES.md` §6.1)**:
  - Bosses 1–3 (Hỏa Long, Thủy Ma, Mộc Yêu): HP 5000, ATK 100, DEF 50, Enrage 30%.
  - Boss 4 (Sơn Thạch Vệ): HP 3000, ATK 120, DEF 0, Enrage 50%.
  - Boss 5 (Kim Lôi Vương): HP 2800, ATK 140, DEF 0, Enrage 75%.
- **TASK-194 §23 Progression Evidence**:
  - Nominal Effective HP ($HP \times (100+DEF)/100$): Bosses 1–3 = 7,500; Boss 4 = 3,000; Boss 5 = 2,800.
  - Measured post-mitigation damage to kill: Hỏa Long 5,068.6; Thủy Ma 5,018.8; Sơn Thạch Vệ 3,098.8; Kim Lôi Vương 2,853.5 (equal to authored MaxHP).
  - Player Damage per Turn (`average`): Hỏa Long 57.4; Thủy Ma 39.4; Mộc Yêu 50.6; Sơn Thạch Vệ 78.8; Kim Lôi Vương 116.6.
  - Player Damage per Turn (`skilled`): Hỏa Long 87.2; Thủy Ma 78.8; Mộc Yêu 107.2; Sơn Thạch Vệ 161.9; Kim Lôi Vương 274.6.
  - Boss Damage per Turn (Mean): Thủy Ma 164.1 > Hỏa Long 153.9 > Sơn Thạch Vệ 132.9 > Kim Lôi Vương 117.3 > Mộc Yêu 102.0.
  - Enrage Cadence (M-13): Kim Lôi Vương enraged in **15/15 runs** (turns 2–8); Sơn Thạch Vệ in **10/15 runs** (turns 6–24); Hỏa Long in **5/15 runs** (turns 50–74); Thủy Ma in **5/15 runs** (turns 77–101); Mộc Yêu in **0/15 runs**.
  - Victories by Boss: Kim Lôi Vương **10/15**; Sơn Thạch Vệ **7/15**; Hỏa Long **5/15**; Thủy Ma **5/15**; Mộc Yêu **0/15**.

#### 3. Observation vs. Interpretation
- **Observation**:
  Bosses 1–3 are stat-identical in base stats, requiring 5,000 post-mitigation damage with DEF 50 mitigating incoming damage by ×0.667. Bosses 4–5 have 0 DEF and substantially lower HP (3,000 and 2,800), resulting in 2× to 3× higher player damage per turn. Consequently, fights against Bosses 4–5 are much shorter (16.5 and 22.5 turns mean vs 37.9 and 50.6 turns mean on Bosses 1–2) and yielded higher player win rates (Kim Lôi Vương 10/15, Sơn Thạch Vệ 7/15 vs Bosses 1–2 at 5/15). Mean Boss damage per turn was lower on Bosses 4–5 because battles terminated before skill charge cadences compounded.
- **Interpretation**:
  The curve is non-monotonic and practically inverted in fight duration and win rate for the tested loadout. Bosses 4–5 function as "glass cannons": although they have higher ATK (120 and 140) and 0-turn cooldown skills, their lack of DEF and low HP allow the player to burst them down quickly, especially when combined with Kim Lôi Vương's elemental disadvantage vs Hỏa (Pet advantage ×1.50).

#### 4. Evidence Sufficiency
The evidence completely quantifies the empirical divergence between Bosses 1–3 and Bosses 4–5.

#### 5. Recommendation
**`DESIGN DECISION`** (Behavior is `EVIDENCE-SUPPORTED`).
The choice between **Candidate A** (accept flat stats for Bosses 1–3 and glass-cannon stats for 4–5, documenting that difficulty is mechanics-only per `GDD.md` §12) and **Candidate B** (re-scale HP/DEF to be gently monotonic, e.g. smoothing DEF to 25/50 or scaling HP) is a pure design choice for the Product Owner.

#### 6. Minimal Deterministic Experiment
Once the Product Owner selects a candidate progression curve (e.g. monotonic HP: 4000 $\to$ 4500 $\to$ 5000 $\to$ 5500 $\to$ 6000 with DEF 25 across all), run the 75-run matrix in `ControlledComparison` mode to verify that Turns-to-kill and win-rates scale monotonically.

---

### Q-4: Anti-Free-Chain Card Cast Rule (`B-02`)

#### 1. Question Restatement
> Extend `RELIC_RULES.md` §5's anti-free-chain principle to **card casts** (i.e. every card cast consumes a Turn or is limited)? (`TASK-191` §6 Q-4; proposal B-02).

#### 2. Relevant Evidence & Citations
- **Decision & Rule**: Approved as **Option B (One Card Cast Per Turn)** in `ADR-021` and implemented in `TASK-192` (`CARD_RULES.md` §3 item 6, `GAME_RULES.md` §11 item 4).
- **TASK-194 Evidence**:
  - Baseline ran all 75 simulations under this exact rule.
  - Metric `CastsTotal` (M-08) was strictly $\le \text{Turns}$ across all 75 runs.
  - Zero infinite loops occurred; zero runs executed multiple card casts in a single committed Turn.
  - 3 runs ended in terminal victory via a Card cast (`MetricAccumulator.ReconcileTerminalCardCast` verified under `TASK-195`).

#### 3. Observation vs. Interpretation
- **Observation**:
  Enforcing a maximum of 1 card cast per committed Match-3 turn completely eliminated zero-risk free-action infinite loops while preserving the independent turn resolution structure.
- **Interpretation**:
  The rule functions as designed. The card economy is bounded by Match-3 turn progression.

#### 4. Evidence Sufficiency
Completely sufficient.

#### 5. Recommendation
**`EVIDENCE-SUPPORTED`**.
Fully resolved, documented in authoritative rules, implemented, and validated.

#### 6. Minimal Deterministic Experiment
None required.

---

### Q-5: "No Strictly Dominated Card/Relic" Property

#### 1. Question Restatement
> Is **"no strictly dominated Card/Relic"** a required MVP property? (`TASK-191` §6 Q-5; blocks B-08, B-09, B-10).

#### 2. Relevant Evidence & Citations
- **TASK-194 §25 (M-08 / M-09)**:
  - Equipped cards: `card-heal` (20), `card-shield` (20), `card-power-charge` (0), `card-iron-fang` (40).
  - Cast counts across all 75 runs: Heal = 5,744; Power Charge = 942; Iron Fang = 296; **Shield = 0**.
  - Relics equipped: None (0 relics; baseline control). Relic triggers = 0.
- **Card Mechanics (`CARD_RULES.md` §2/§4)**:
  - Heal restores 20% MaxHP (200 HP); overheal discarded.
  - Shield adds 20% MaxHP shield (200 pool); absorbs damage before HP; persists until depleted.
  - Both cost 20 Power.

#### 3. Observation vs. Interpretation
- **Observation**:
  `card-shield` was cast 0 times in 75 runs. This occurred because `average` policy scans cards in loadout order (`Heal` appears before `Shield`) and immediately casts Heal when Power $\ge 20$. Meanwhile, `skilled` policy sorts by highest authored cost (`Iron Fang` [40] $\to$ `Heal`/`Shield` [20]) and evaluates Heal before Shield when Power is 20–39. 4 of 8 cards and 10 of 10 relics were not equipped in the baseline run.
- **Interpretation**:
  Shield was never cast due to the **policy selection priority and loadout ordering**, not because Shield is mathematically unusable or rejected by human players. However, because both Heal and Shield cost identical Power (20) for identical magnitude (200 HP), and because incoming boss damage is sustained attrition where healing depleted HP restores real health, Heal generally dominates Shield in long attrition fights unless the player is at full HP facing imminent burst damage.

#### 4. Evidence Sufficiency
The baseline evidence demonstrates that identical costs between Heal and Shield create a policy tie-break where one card completely eclipses the other under greedy heuristics. It does not measure Shield in isolation, nor does it measure the remaining 4 cards or 10 relics.

#### 5. Recommendation
**`DESIGN DECISION` & `NEEDS CONTROLLED EXPERIMENT`**.
1. The Product Owner must decide whether "no strictly dominated Card/Relic" is an explicit MVP design requirement or if situational/thematic variation is acceptable.
2. If non-domination is required, controlled experiments isolating Shield, unequipped cards, and relics are necessary.

#### 6. Minimal Deterministic Experiment
**EXP-02 (Heal vs. Shield Isolation & Ordering)**:
- Run 3 loadout variants against Bosses 1–5 across 5 seeds with `average` policy:
  - Variant A: Shield-only (`card-shield`, `card-power-charge`, `card-iron-fang`).
  - Variant B: Heal-only (`card-heal`, `card-power-charge`, `card-iron-fang`).
  - Variant C: Inverted loadout order (`card-shield` placed before `card-heal`).
- Measure delta in effective HP contributed, turns survived, and win rates.

---

### Q-6: Pet Tier / Star / Level Stat Curve (`B-05`)

#### 1. Question Restatement
> Pet Tier/Star/Level: **author the stat curve** or declare cosmetic for MVP? (`TASK-191` §6 Q-6; proposal B-05).

#### 2. Relevant Evidence & Citations
- **Authoritative Rules**:
  - `PET_RULES.md` §5.7 item 2: "Level scales base stats (HP/ATK/DEF) via a stat curve. The curve itself is a balance concern, not defined here."
  - `PET_RULES.md` §6: "$\text{Final Stat} = f(\text{Base Stat}[\text{Tier}], \text{Level Curve}[\text{Level}], \text{Star Bonus}[\text{Star}])$. The exact function $f$ is a balance/config concern."
- **Code & Runtime State**:
  - `BattleStartService` / `BattleStateService` / `PetState.AtBattleCreation`: No stat calculation exists. Every Pet enters battle with hardcoded constants: HP 1000, ATK 50, DEF 25, Crit 5, Power 0 (`COMBAT_RULES.md` §1.1).
  - All 5 Pets in battle are **stat-identical**; Tier, Star, and Level contribute 0.
  - Harness metrics M-01…M-15 carry no Level/Star/Tier fields because they do not exist in active combat state (`GAME_STATE.md` §2.3).

#### 3. Observation vs. Interpretation
- **Observation**:
  Meta progression attributes (Level, Star, Tier) do not reach the combat pipeline. The combat simulation runs with identical base stats across all runs.
- **Interpretation**:
  Currently, Pet progression is purely collection display metadata with zero combat impact.

#### 4. Evidence Sufficiency
**`NOT MEASURABLE`**.
The combat simulation harness cannot measure the effect of a formula that has not been authored or implemented.

#### 5. Recommendation
**`DESIGN DECISION`** (`NOT MEASURABLE` in combat harness).
The Product Owner must decide between:
- Option (a): Author the stat scaling curve $f(\text{Tier}, \text{Level}, \text{Star})$ in `PET_RULES.md` §6 and implement it in `BattleStartService`.
- Option (b): Formally document in `PET_RULES.md` that Pet Tier/Star/Level are cosmetic/display-only for MVP scope.

#### 6. Minimal Deterministic Experiment
None possible until a candidate formula is authored. Once authored, simulate Level 1 vs Level 25 vs Level 50 against Bosses 1–5 to verify stat progression scaling.

---

### Q-7: Player XP Pacing Curve (`B-06`)

#### 1. Question Restatement
> Player XP: keep 1-win-per-level to 50, curve it, or give Level combat meaning? (`TASK-191` §6 Q-7; proposal B-06).

#### 2. Relevant Evidence & Citations
- **Authoritative Rules**:
  - `COMBAT_RULES.md` §7.2: `BattleWon` grants +100 Player XP, `BattleLost` grants +0.
  - `COMBAT_RULES.md` §7.4: $\text{Player.Level} = \min(\lfloor\text{Player.XP} / 100\rfloor + 1, 50)$.
  - `COMBAT_RULES.md` §7.1 / `GDD.md` §14: Player Level carries no combat stats.
- **Harness Scope**:
  - The simulation harness executes single battles from creation to terminal state (`Victory`, `Defeat`, `Stalemate`). It does not run out-of-battle meta reward processing or account persistence (`DATABASE.md` §3).

#### 3. Observation vs. Interpretation
- **Observation**:
  Player XP and Level are out-of-battle meta progression variables that do not participate in or affect the in-battle combat loop.
- **Interpretation**:
  Pacing (50 wins to cap at 1 win per level) is an account progression pacing choice, completely decoupled from battle balance.

#### 4. Evidence Sufficiency
**`NOT MEASURABLE`**.
The in-battle simulation harness has no interaction with Player XP.

#### 5. Recommendation
**`DESIGN DECISION`** (`NOT MEASURABLE` in combat harness).
The Product Owner must decide whether 50 wins to reach cap is satisfactory for MVP or if a curved progression constant is preferred.

#### 6. Minimal Deterministic Experiment
Not applicable to the combat simulation harness. Pacing can be evaluated via an offline mathematical model plotting wins vs. levels.

---

### Q-8: Mộc Yêu Regeneration vs. Player Throughput (`B-07`)

#### 1. Question Restatement
> Mộc Yêu regen: reduce, raise threshold, or keep as intended attrition mechanic? (`TASK-191` §6 Q-8; proposal B-07).

#### 2. Relevant Evidence & Citations
- **Authored Rule (`BOSS_RULES.md` §6.2.3)**:
  - Regenerates 5% MaxHP (250 HP on MaxHP 5000) every 5 Player Matches.
- **TASK-194 §20 & §27 Evidence (15 Mộc Yêu runs)**:
  - `average` policy (5 seeds): **5 / 5 Stalemates** at 1000 Turns.
    - Player Damage Total (M-03): 48,560 – 52,751 HP.
    - Final Boss HP: 4,922 – 5,000 HP (net boss HP loss across 1000 turns: **0 – 78 HP**).
    - Measured Regeneration (M-12, empirical lower bound): 28,213 – 29,529 HP.
    - Passive Triggers (M-13): 304 – 317 triggers.
    - Derived Applied Regeneration ($M\text{-}03 - \text{net loss}$): **48,537 – 52,728 HP**.
    - Raw Trigger Ceiling ($triggers \times 250$): 77,500 – 79,250 HP.
  - `skilled` policy (5 seeds): **5 / 5 Defeats** in 34–84 Turns.
    - Player Damage Total: 2,826 – 7,551 HP.
    - Boss HP Min: 3,849 – 4,294 HP (never reached enrage threshold 1500 HP).
    - Derived Applied Regeneration: 2,120 – 6,526 HP.
  - `passive` policy (5 seeds): **5 / 5 Defeats** in 14–25 Turns.
  - Across all 15 runs, player win rate against Mộc Yêu was **0%**.

#### 3. Observation vs. Interpretation
- **Observation**:
  In every run where the player sustained themselves (`average`), Mộc Yêu regenerated almost exactly as much damage as the player dealt ($\approx 50\text{ HP/turn}$), resulting in a flat HP line at near-maximum health over 1,000 turns. In every run where the player prioritized damage over healing (`skilled`), player throughput was insufficient to overcome regen before Pet HP was exhausted by incoming boss attacks.
- **Interpretation**:
  Under baseline parameters, Mộc Yêu's regeneration rate creates an unbreakable stalemate against a sustain build and an unwinnable race against a damage build. The regeneration rate exceeds the damage output of any policy that allocates sufficient actions to survival.

#### 4. Evidence Sufficiency
The evidence conclusively establishes that current baseline tunings make Mộc Yêu unwinnable and prone to infinite stalemate under the tested policies.

#### 5. Recommendation
**`NEEDS CONTROLLED EXPERIMENT` & `DESIGN DECISION`**.
The stalemate behavior is `EVIDENCE-SUPPORTED`. The Product Owner must decide whether Mộc Yêu's role as an attrition boss allows stalemate or requires winnability for standard starter loadouts. If tuning is approved, controlled experiments are required to find the minimal viable adjustment.

#### 6. Minimal Deterministic Experiment
**EXP-01 (Mộc Yêu Regen Controlled Sweep)**:
- Test 4 variants of Mộc Yêu in `ControlledComparison` mode across the 5 canonical seeds with `average` and `skilled` policies (40 runs total):
  - Variant A (Baseline): 5% MaxHP (250 HP) every 5 matches.
  - Variant B (Magnitude reduction): 3% MaxHP (150 HP) every 5 matches.
  - Variant C (Magnitude reduction): 2% MaxHP (100 HP) every 5 matches.
  - Variant D (Threshold increase): 5% MaxHP (250 HP) every 8 matches.
- Measure whether stalemates resolve to victories and track resulting Turn counts.

---

### Q-9: Card Damage Efficiency Normalization Band (`B-08`)

#### 1. Question Restatement
> Card damage normalization: approve a cost-per-expected-damage tolerance band after simulation? (`TASK-191` §6 Q-9; proposal B-08).

#### 2. Relevant Evidence & Citations
- **Baseline Configuration**:
  - Only **one** damage card was equipped in baseline: `card-iron-fang` (PowerCost 40, 120 damage + 10pp Crit next attack).
  - Un-equipped damage cards: `Inferno` (100 Power, 120 Hỏa + Burn 50×2), `Venomous Bloom` (80 Power, 80 Mộc + Burn 25×2), `Earthshaker` (100 Power, 150 Thổ).
- **TASK-194 §25 / §29 (Finding D-194-8 / TASK-195 Issue 7)**:
  - `card-iron-fang` was cast **296 times**, all by `skilled` policy.
  - Card damage in `CardCastExecutor` resolves with the **Pet's Element** (`AttackerElement: state.PetState.Element` = Hỏa), not the card's theme.
  - Iron Fang resolved with Advantage (×1.50) vs Kim Lôi Vương, Disadvantage (×0.75) vs Thủy Ma, and Neutral (×1.00) vs Hỏa Long, Mộc Yêu, and Sơn Thạch Vệ.

#### 3. Observation vs. Interpretation
- **Observation**:
  The baseline matrix tested only one damage card, cast by one policy, using one Pet element. The other 3 Pet Skill cards were never cast. Card damage element inherits from the active Pet.
- **Interpretation**:
  A tolerance band (e.g. $\pm 15\%$ expected damage per Power) cannot be evaluated from a single data point. Furthermore, because card damage inherits the Pet's element, efficiency is not an intrinsic property of the card alone; it varies dynamically depending on the Pet-Boss elemental matchup.

#### 4. Evidence Sufficiency
Insufficient to evaluate all 4 Pet Skill cards or establish an empirical tolerance band.

#### 5. Recommendation
**`NEEDS CONTROLLED EXPERIMENT` & `DESIGN DECISION`**.
1. Product Owner must decide whether mathematical damage-per-Power normalization is a design goal.
2. Controlled comparison runs across all Pet Skill cards are required to measure baseline damage output.

#### 6. Minimal Deterministic Experiment
**EXP-03 (Full Pet Skill Card Damage Sweep)**:
- Equip each Pet Skill card (`Inferno`, `Tidal Barrier`, `Iron Fang`, `Venomous Bloom`, `Earthshaker`) in isolated loadouts with their matching native Pets.
- Run against Bosses 1–5 across the 5 canonical seeds with `skilled` policy.
- Measure average post-DEF damage dealt per cast and damage-per-Power efficiency.

---

### Q-10: Relic Power Engines & Balance (`B-10`)

#### 1. Question Restatement
> Relic engines: cap or reduce Arcane/Cascade Power grants; buff Berserker? (`TASK-191` §6 Q-10; proposal B-10).

#### 2. Relevant Evidence & Citations
- **TASK-194 §5.2 / §25**:
  - Baseline matrix configured `Relics = null` (0 relics equipped).
  - Metric `RelicTriggersTotal` (M-09) = **0 across all 75 runs**.
- **TASK-194 §26 Match-3 Dynamics**:
  - 7,274 total turns resolved.
  - Combo distribution: Combo 1 = 58.0%, Combo 2 = 20.6%, Combo 3 = 9.4%, Combo 4 = 4.2%, Combo $\ge 5$ = 7.8%. Maximum combo = 32.
  - Cascades occur regularly across turns.
- **Authoritative Rules**:
  - `RELIC_RULES.md` §3.1 prevents recursive loops on `Arcane Battery` by ignoring Relic-generated Power.
  - `RELIC_RULES.md` §3.2 specifies that each cascade iteration triggers `Cascade Core` (+5 Power).

#### 3. Observation vs. Interpretation
- **Observation**:
  Zero relics were equipped in the baseline. Baseline evidence provides an unequipped control reference but zero direct measurement of relic impact.
- **Interpretation**:
  Because cascades occur in over 40% of turns (Combo $\ge 2$ is 42.0%), Cascade Core will trigger frequently. However, without ablation runs, its actual contribution to Power inflation cannot be quantified.

#### 4. Evidence Sufficiency
**`NOT MEASURABLE` from baseline alone** (requires controlled ablation experiments).

#### 5. Recommendation
**`NEEDS CONTROLLED EXPERIMENT`**.
Relic performance cannot be judged without dedicated simulation runs comparing relic-equipped vs unequipped loadouts.

#### 6. Minimal Deterministic Experiment
**EXP-04 (Relic Single-Ablation Matrix)**:
- Run each of the 10 MVP relics individually (1 relic equipped per run) and against an unequipped baseline control across Bosses 1–5 and the 5 canonical seeds using `average` and `skilled` policies (10 relics × 5 bosses × 2 policies × 5 seeds = 500 runs).
- Measure delta in `M-07 PowerGeneratedTotal`, `M-09 RelicTriggersTotal`, `M-03 PlayerDamageTotal`, and `M-01 Turns`.

---

### Q-11: Pet Passive Magnitudes for Xích Lang & Sơn Hùng

#### 1. Question Restatement
> Approve pet-passive magnitudes for **Xích Lang** and **Sơn Hùng** (not authored anywhere)? (`TASK-191` §6 Q-11; proposal B-14 item 1).

#### 2. Relevant Evidence & Citations
- **Authoritative Rules (`PET_RULES.md` §8)**:
  - Thanh Xà: Every 7 Matches $\to$ Restore 8% HP (authored).
  - Huyền Quy: Every 6 Matches $\to$ Shield 15% Max HP (authored).
  - Bạch Hổ: Every 4 Matches $\to$ Crit chance up (+10pp example in `COMBAT_RULES.md` §5.4).
  - Xích Lang: Every 5 Matches $\to$ Empower + Burn (**magnitudes unauthored**).
  - Sơn Hùng: Every 5 Matches $\to$ Temp Defense (**magnitudes unauthored**).
- **Implementation State (Finding D-2 / TASK-195 Issue 8)**:
  - Pet Passive effects are **not applied in combat**. `BattleStateService` only calls `PassiveTracker.Charge` and emits events; no gameplay effects are executed for any Pet passive.
  - M-13 counts only Boss passive triggers (`source == PassiveEventSource.Boss`). Pet passive triggers are not recorded in M-01…M-15.

#### 3. Observation vs. Interpretation
- **Observation**:
  No numbers exist in design documents for Xích Lang and Sơn Hùng passive magnitudes, and no code exists in the combat engine to apply Pet passive effects.
- **Interpretation**:
  Pet passives are completely blocked from simulation and tuning until design values are authored and combat code is implemented.

#### 4. Evidence Sufficiency
**`NOT MEASURABLE`**.

#### 5. Recommendation
**`DESIGN DECISION`** (followed by an implementation prerequisite).
The Product Owner must author the missing magnitudes in `PET_RULES.md` §8. An engineering task must implement Pet passive effect application before any balance measurement is possible.

#### 6. Minimal Deterministic Experiment
None possible until authored and implemented.

---

### Q-12: Simulation Harness Prerequisite

#### 1. Question Restatement
> Priority: is building the **simulation harness** (`B-14` item 3) approved as a prerequisite task? (`TASK-191` §6 Q-12).

#### 2. Relevant Evidence & Citations
- **Task History**:
  - `TASK-193`: Approved and built the deterministic balance simulation harness (`tests/backend/GameServer.Application.Tests/Balance/`).
  - `TASK-194`: Executed the 75-run baseline matrix and archived results.
  - `TASK-195`: Audited and corrected harness measurement fidelity, passing 2,903 full-suite tests and 13 dedicated regression tests.

#### 3. Observation vs. Interpretation
- **Observation**:
  The harness is fully implemented, verified, deterministic, and operational.
- **Interpretation**:
  This prerequisite is complete.

#### 4. Evidence Sufficiency
Completely sufficient.

#### 5. Recommendation
**`EVIDENCE-SUPPORTED`**.
Resolved and closed.

#### 6. Minimal Deterministic Experiment
None required.

---

## 4. Evidence-Supported Conclusions

The following statements are supported by the corrected TASK-194 / TASK-195 empirical baseline without speculation:

1. **Anti-Infinite-Chain Effectiveness**:
   Under the post-TASK-192 rule of at most one card cast per committed Match-3 turn (`ADR-021`), no infinite card cast loops exist. In all 75 baseline runs, card casts were strictly bounded by turn count ($\text{CastsTotal} \le \text{Turns}$).

2. **Binding Failure Constraint in Baseline Combat**:
   All 43 defeats across the baseline matrix were caused by Pet HP exhaustion (`PlayerHpMin = 0`). No run terminated due to timeouts, invalid board states, or unhandled errors. Incoming boss damage pressure is the binding constraint on survival.

3. **Pure Match-3 Without Cards is Non-Viable**:
   The `passive` policy (0 card casts) lost 100% of its runs (25/25) across all 5 Bosses within 7 to 25 turns. Baseline Pet HP (1000) cannot survive unmitigated boss damage without card sustain or burst.

4. **Sustain Dominates Burst Under Greedy Scripted Policies**:
   The sustain-first policy (`average`) achieved a 80.0% win rate (20/25 wins, 5 stalemates), whereas the damage-first policy (`skilled`) achieved only a 28.0% win rate (7/25 wins, 18 defeats). Although `skilled` generated 1.5× to 2.5× higher player damage per turn, its lower healing frequency resulted in Pet death in 18 runs.

5. **Mộc Yêu Regeneration Produces Stalemate**:
   Mộc Yêu's authored passive (5% MaxHP = 250 HP every 5 matches) absorbs $\approx 99.8\%$ of player throughput under sustain play. In all 5 `average` runs, the battle ran to the 1,000-turn limit with the Boss retaining $\ge 4,922$ HP (net loss $\le 78$ HP) after absorbing over 48,000 damage. Against `skilled` play, player damage was insufficient to overcome regen before the Pet died (0/5 wins). Overall player win rate was 0/15.

6. **Inverted Boss Progression Profile**:
   Under authored base stats, Bosses 4 and 5 (Sơn Thạch Vệ, Kim Lôi Vương) have 0 DEF and lower HP (3,000 and 2,800), resulting in significantly shorter battles (mean 22.5 and 16.5 turns) and higher player win rates (7/15 and 10/15) than Bosses 1 and 2 (mean 37.9 and 50.6 turns; 5/15 win rate). Bosses 4 and 5 take post-mitigation damage equal to raw damage, negating their higher ATK because battles end before their skill cadences mature.

7. **Enrage Cadence is Inverted**:
   Kim Lôi Vương reached Enrage in 15/15 runs (turns 2–8) and Sơn Thạch Vệ in 10/15 runs (turns 6–24). In contrast, Hỏa Long and Thủy Ma reached Enrage in only 5/15 runs (turns 50–101), and Mộc Yêu never reached Enrage in any run (0/15).

8. **Shield 0-Cast Metric is a Heuristic Artifact**:
   `card-shield` was cast 0 times across all 75 runs solely because both casting policies prioritized other cards in the decision tree. It was never evaluated in isolation.

9. **Element Mechanics Function Asymmetrically**:
   Where elemental counters exist, modifiers apply as authored: Thủy Ma (Thủy vs Hỏa Pet) resolved 754 Boss advantage instances (×1.50) and 798 Pet disadvantage instances (×0.75); Kim Lôi Vương (Kim vs Hỏa Pet) resolved 279 Pet advantage instances (×1.50) and 241 Boss disadvantage instances (×0.75). In the other 3 Boss matchups (Hỏa Long, Mộc Yêu, Sơn Thạch Vệ), damage resolved neutral (1.00×) because the Tương Khắc cycle has no advantage relationship between Hỏa and Mộc or Hỏa and Thổ.

10. **Card Damage Inherits Pet Element**:
    In accordance with `ELEMENT_RULES.md` §5 and `CardCastExecutor`, card damage resolves with the active Pet's Element (`state.PetState.Element`), meaning elemental matchup modifies card damage based on the Pet rather than card theme.

---

## 5. Questions Requiring Controlled Experiments

The following questions cannot be resolved from baseline evidence alone because critical parameters were fixed, unequipped, or masked by policy heuristics:

1. **Q-2 (Power Charge Cost Sensitivity)**:
   - *Gap*: Baseline only tested cost 0.
   - *Requirement*: Measure whether increasing cost to 10 or 20 starves player healing and causes defeat.

2. **Q-5 (Card Non-Domination & Shield Viability)**:
   - *Gap*: Shield was cast 0 times due to loadout ordering and cost ties with Heal; 4 cards were unequipped.
   - *Requirement*: Test Shield in isolation (Shield-only loadout) and test all 8 cards across standardized loadouts.

3. **Q-8 (Mộc Yêu Regeneration Tuning)**:
   - *Gap*: Baseline proved that 5% / 5 matches causes stalemates, but does not identify the optimal tuning.
   - *Requirement*: Test candidate adjustments (2% MaxHP, 3% MaxHP, or 8-match threshold) to determine which restores winnability without trivializing the mechanic.

4. **Q-9 (Card Damage Normalization Band)**:
   - *Gap*: Baseline only equipped Iron Fang.
   - *Requirement*: Test all 4 Pet Skill damage cards across standard elemental matchups to compute empirical expected-damage-per-Power values.

5. **Q-10 (Relic Power Engines & Contribution)**:
   - *Gap*: Baseline equipped 0 relics.
   - *Requirement*: Execute single-relic ablation experiments across all 10 canonical relics to measure their individual impact on Power generation, damage, and turn counts.

---

## 6. Questions Requiring Design / Product Decisions

The following questions require explicit human product decisions because they represent design philosophy, business requirements, or authored content that cannot be deduced from simulation metrics:

1. **Q-1 (Target Fight Duration)**:
   - Product Owner must decide what Turn count range constitutes a desirable battle duration for MVP (e.g. 15–30 turns). Data can only measure compliance against a standard, not establish the standard.

2. **Q-2 (Power Charge Identity)**:
   - Product Owner must decide whether a 0-cost resource generator card is intended in the starter deck, or whether all resource generators must carry a cost.

3. **Q-3 (Boss Difficulty Progression Philosophy)**:
   - Product Owner must choose between:
     - *Candidate A*: Retain flat stats for Bosses 1–3 and glass-cannon stats for Bosses 4–5, accepting mechanics-only difficulty per `GDD.md` §12.
     - *Candidate B*: Re-scale HP and DEF into a monotonic progression curve where each successive boss has equal or greater Effective HP.

4. **Q-5 (Non-Domination Invariant)**:
   - Product Owner must define whether "no strictly dominated Card or Relic" is an absolute architectural rule for MVP launch, or whether situational utility is sufficient.

5. **Q-6 (Pet Progression in Combat)**:
   - Product Owner must decide whether Pet Tier/Star/Level should scale in-battle stats via an authored formula $f(\text{Tier}, \text{Level}, \text{Star})$, or remain cosmetic metadata for MVP.

6. **Q-7 (Player XP Progression Pacing)**:
   - Product Owner must decide whether 50 wins to cap (flat 1 win = 1 level) is the intended account pacing for MVP or if a steeper XP curve should be authored.

7. **Q-8 (Attrition Boss Philosophy)**:
   - Product Owner must decide whether Mộc Yêu is intended to be a hard check requiring specific offensive builds, or whether standard starter loadouts must be able to defeat it.

8. **Q-9 (Tolerance Band Acceptance)**:
   - Product Owner must define the acceptable variance band (e.g. $\pm 15\%$) for card damage efficiency.

9. **Q-11 (Pet Passive Content Authorship)**:
   - Product Owner must author the missing numeric magnitudes for Xích Lang (Empower + Burn) and Sơn Hùng (Temp Defense) in `PET_RULES.md` §8.

---

## 7. Minimal Recommended Experiment Matrix

To unblock the questions requiring empirical validation, the following test-only experiments should be executed using the existing `BalanceSimulator` harness in `ControlledComparison` mode:

| Exp ID | Target Question | Matrix Dimensions | Configurations / Variants | Metrics Evaluated | Purpose |
|---|---|---|---|---|---|
| **EXP-01** | **Q-8** (Mộc Yêu Regen) | 4 variants × 2 policies (`average`, `skilled`) × 5 seeds = **40 runs** | Var A: Baseline (5% / 5 matches)<br>Var B: 3% MaxHP / 5 matches<br>Var C: 2% MaxHP / 5 matches<br>Var D: 5% MaxHP / 8 matches | M-01 Turns<br>M-05 BossHpMin<br>M-12 Regen<br>M-14 Outcome | Identify minimal change that eliminates stalemates while maintaining boss identity. |
| **EXP-02** | **Q-5** (Heal vs. Shield) | 3 loadouts × 5 bosses × 2 policies × 5 seeds = **150 runs** | Loadout A: Shield-only (no Heal)<br>Loadout B: Heal-only (no Shield)<br>Loadout C: Inverted order (Shield before Heal) | M-01 Turns<br>M-06 PlayerHpMin<br>M-08 CastsByCard<br>M-14 Outcome | Measure independent EHP contribution of Shield vs Heal and verify viability. |
| **EXP-03** | **Q-9** (Card Damage Sweep) | 5 Skill cards × 5 bosses × 1 policy (`skilled`) × 5 seeds = **125 runs** | Test each Pet Skill card equipped with its native Pet across all 5 Bosses | M-03 PlayerDamageTotal<br>M-07 PowerSpent<br>M-08 Casts<br>Damage per Power | Establish empirical baseline of expected damage per card to evaluate normalization. |
| **EXP-04** | **Q-10** (Relic Ablation) | 11 loadouts (10 single relics + 1 unequipped control) × 5 bosses × 2 policies × 5 seeds = **550 runs** | Test each of the 10 MVP relics equipped individually (1 relic per run) | M-01 Turns<br>M-07 PowerGen/Spent<br>M-09 RelicTriggers<br>M-03 PlayerDmg | Measure individual impact of each relic on economy, damage, and fight duration. |
| **EXP-05** | **Q-2** (Power Charge Cost) | 3 cost variants × 5 bosses × 2 policies × 5 seeds = **150 runs** | Var A: Cost 0 (Baseline)<br>Var B: Cost 10<br>Var C: Cost 20 | M-01 Turns<br>M-07 PowerSpent<br>M-08 CastsByCard<br>M-14 Outcome | Measure whether increasing Power Charge cost leads to resource starvation. |

*Total execution burden*: 1,015 deterministic test simulation runs (estimated run time: $< 45$ seconds within `GameServer.Application.Tests`). Zero production code changes required.

---

## 8. Conclusions That MUST NOT Be Drawn From Current Evidence

To maintain strict scientific and engineering rigor, the following invalid inferences **must not** be made:

1. **MUST NOT conclude that `card-shield` is "useless", "underpowered", or "unviable"**:
   Shield recorded 0 casts solely because the scripted policies prioritized Heal or Iron Fang. Shield was never evaluated under conditions where Heal was unavailable.

2. **MUST NOT conclude that `average` policy is "better" than `skilled` policy**:
   `average` won more runs because its greedy heuristic defaulted to constant healing, which happened to match the attrition requirements of Bosses 1–2. `skilled` dealt far more damage per turn but died because its heuristic neglected healing. This reflects the specific heuristics of the test instruments, not human player capability.

3. **MUST NOT conclude that Bosses 1–3 are "balanced" or Bosses 4–5 are "unbalanced"**:
   No balance verdicts (`BALANCED` / `UNBALANCED` / `PASS` / `FAIL`) exist in authoritative rules. The data merely shows that Bosses 1–3 require longer attrition fights, while Bosses 4–5 are rapid glass-cannon encounters.

4. **MUST NOT conclude that Pet Tier/Star/Level or Player XP pacing have been tested**:
   These systems were completely unrepresented in the combat simulation. Pet stats were identical defaults, and Player XP is an out-of-battle reward.

5. **MUST NOT conclude that Relic Power engines (Arcane Battery, Cascade Core) are overpowered**:
   Zero relics were equipped in the baseline. Any claims about relic power engine inflation remain purely speculative until ablation runs are executed.

6. **MUST NOT conclude that Pet Passives are balanced or functional**:
   Pet passive effects are currently unimplemented in the combat engine and were not executed during the simulations.

7. **MUST NOT conclude that fight durations "pass" or "fail" acceptance criteria**:
   Duration is an observational metric. No numeric duration target exists in any authoritative design document (`GDD.md`, `GAME_RULES.md`, `BOSS_RULES.md`).

8. **MUST NOT conclude that Mộc Yêu is unwinnable for human players**:
   Human players have tactical agency to save resources, build combos, and burst during key windows, whereas the simulated policies used simple greedy rules. The baseline proves only that greedy baseline heuristics cannot defeat Mộc Yêu.

9. **MUST NOT treat Card damage element behavior as a defect**:
   Card damage inheriting the Pet's Element is compliant with `ELEMENT_RULES.md` §5 (damage instance carries the attacking element of the Pet entity casting the skill).

---

## 9. Traceability Matrix

| Subject / Question | TASK-191 Ref | TASK-194 Evidence Ref | TASK-195 Audit Ref | Authoritative Rule Reference | Status in TASK-196 |
|---|---|---|---|---|---|
| **Fight Duration** | Q-1, BT-6, §1.8 | §22, M-01, M-02 | Issue 2 (series off-by-one fixed) | `GDD.md` §12; `TASK-193` §2 | `DESIGN DECISION` (Turn baseline `EVIDENCE-SUPPORTED`; wall-clock `NOT MEASURABLE`) |
| **Power Charge** | Q-2, B-01, §1.5 | §24, §25, M-07, M-08 | Issue 6 (Drain Power semantics) | `CARD_RULES.md` §2 item 3; `GAME_RULES.md` §12 | `NEEDS CONTROLLED EXPERIMENT` & `DESIGN DECISION` |
| **Boss Stat Curve** | Q-3, B-03, B-04, §1.7 | §23, M-03, M-04, M-05, M-13 | Issue 1 (post-DEF damage verified) | `BOSS_RULES.md` §6.1, §7; `GDD.md` §12 | `DESIGN DECISION` (Empirically `EVIDENCE-SUPPORTED`) |
| **1-Card-per-Turn** | Q-4, B-02, BT-4 | §20, §25, M-08, M-14 | Issue 2 (terminal card cast reconciled) | `CARD_RULES.md` §3 item 6; `ADR-021` | `EVIDENCE-SUPPORTED` (Resolved) |
| **Non-Domination** | Q-5, BT-5, §1.5, §1.6 | §25, M-08, M-09 | Issue 8 (measurement boundaries) | `CARD_RULES.md` §2; `RELIC_RULES.md` §8.5 | `DESIGN DECISION` & `NEEDS CONTROLLED EXPERIMENT` |
| **Pet Stat Curve** | Q-6, B-05, BT-7, §1.4 | §28, Finding D-5 | Issue 8 (Pet passives boundary) | `PET_RULES.md` §5.7, §6; `COMBAT_RULES.md` §1.1 | `NOT MEASURABLE` & `DESIGN DECISION` |
| **Player XP Pacing** | Q-7, B-06, BT-8, §1.4 | §28, Finding D-4 | None (out-of-battle boundary) | `COMBAT_RULES.md` §7; `GDD.md` §14 | `NOT MEASURABLE` & `DESIGN DECISION` |
| **Mộc Yêu Regen** | Q-8, B-07, §1.8 | §27, M-03, M-05, M-12, M-13 | Issue 3 (M-12 lower bound verified) | `BOSS_RULES.md` §6.2.3; `GAME_RULES.md` §17 | `NEEDS CONTROLLED EXPERIMENT` & `DESIGN DECISION` |
| **Card Damage Norm**| Q-9, B-08, §1.5 | §25, M-03, M-08, Finding D-194-8 | Issue 7 (Pet element inheritance verified) | `CARD_RULES.md` §4; `ELEMENT_RULES.md` §5 | `NEEDS CONTROLLED EXPERIMENT` & `DESIGN DECISION` |
| **Relic Engines** | Q-10, B-10, §1.6 | §25, §26, M-09, M-10 | Issue 8 (0 relics verified) | `RELIC_RULES.md` §3.1, §3.2, §5, §8.5 | `NEEDS CONTROLLED EXPERIMENT` |
| **Pet Passives** | Q-11, B-14, BT-10, §1.4| §28, Finding D-2, D-194-6 | Issue 8 (effects unimplemented) | `PET_RULES.md` §8; `PASSIVE_RULES.md` §4 | `NOT MEASURABLE` & `DESIGN DECISION` |
| **Harness Prereq** | Q-12, B-14 item 3 | §18, §19, §31 | Issues 1–5 (audited & operational) | `TASK-193` §1; `TASK-195` §1 | `EVIDENCE-SUPPORTED` (Resolved) |

---

## 10. Scope Verification

```text
Production code changed (src/):       NO  (0 files modified)
Gameplay rules changed (docs/):       NO  (0 files modified)
Balance values changed:               NO  (0 values modified)
Database schema / migrations:         NO  (0 migrations created)
Frontend code changed:                NO  (0 files modified)
ADR created:                          NO  (0 ADRs created)
Balance verdicts emitted:             NO  (0 BALANCED / UNBALANCED / PASS / FAIL strings)
TASK-191 closed or modified:          NO  (TASK-191 remains OPEN in backlog)
TASK-194 artifact modified:           NO  (tasks/artifacts/TASK-194-baseline-simulation-results.json untouched)
Files added by this task:             tasks/backlog/TASK-196-balance-decision-evidence-analysis.md (only)
```

---

## 11. Recommended Next Task

Based on the findings of this audit, the recommended next task is:

```text
TASK-197 — Controlled Balance Experiment Matrix Execution (Analysis / Simulation Only)
```

**Objective**: Implement and execute the minimal controlled experiment matrix defined in Section 7 (EXP-01 through EXP-05) within the test harness (`GameServer.Application.Tests.Balance`) in `ControlledComparison` mode, generating empirical comparative evidence to unblock Product Owner decisions for Q-8 (Mộc Yêu), Q-5 (Shield vs Heal), Q-9 (Card Damage), Q-10 (Relics), and Q-2 (Power Charge).
