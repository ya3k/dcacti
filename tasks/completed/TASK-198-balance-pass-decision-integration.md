# TASK-198 — Balance Pass Decision Integration & Content Tuning Migration

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.

  SCOPE OF THIS TASK: Transition the project from balance evidence generation
  (TASK-191, TASK-193, TASK-194, TASK-195, TASK-196, TASK-197) to approved
  gameplay/content changes.
  CRITICAL DECISION GATE: Every gameplay change must be explicitly authorized
  by an approved Product Owner decision. Unapproved balance values are NOT
  guessed or inferred from simulation evidence.
  Unresolved items are strictly classified as OPEN / DECISION REQUIRED.
-->

---

## Metadata

```text
Task ID:           TASK-198
Type:              Balance Integration & Content Tuning Migration
Status:            COMPLETE (Decisions Resolved & Implemented under TASK-200)
Date:              2026-10-06
Evidence Base:     TASK-197 controlled balance experiment matrix (tasks/artifacts/TASK-197-controlled-balance-experiments.json;
                   SHA-256 A185D084AB3E30A55706E9C9303B77896E253BCDA821A2FDB5189827FBD8BB2C),
                   TASK-196 decision-support audit,
                   TASK-194 / TASK-195 baseline simulation evidence
Governing Task:    TASK-191 (Balance Pass Specification)
Decision Gate:     AGENTS.md §7, §17, §20, §23 (no invented balance numbers)
Production Change: Implemented in TASK-200: Mộc Yêu passive threshold 5 -> 8
Gameplay Rules:    Updated in TASK-200: BOSS_RULES.md §6.2 / §6.2.3
Balance Values:    Only Mộc Yêu passive threshold (5 -> 8); all other authored values retained
Approved Levers:   Q-4 / B-02 (implemented in TASK-192 / ADR-021),
                   Q-12 / B-14 item 3 (implemented in TASK-193 / TASK-195),
                   Q-1 / BT-6 (Qualitative Option B — observational metric),
                   Q-8 / B-07 (Threshold 8 matches — implemented in TASK-200),
                   Q-2, Q-3, Q-5, Q-6, Q-7, Q-9, Q-10, Q-11 (Status Quo retained in TASK-200)
Decisions Open:    NONE (All 12 balance decisions resolved under TASK-200)
Primary Agent:     design
Supporting Agents: gameplay, review, testing
Workflow:          development/gameplay-change.md
Model:             Gemini 3.8
Reasoning:         High
```

---

## 1. Executive Summary & Decision Gate Audit

TASK-198 establishes the bridge between empirical balance evidence generation (`TASK-194`, `TASK-195`, `TASK-196`, `TASK-197`) and production gameplay tuning.

### 1.1 Decision Gate Classification Rule

In strict accordance with `AGENTS.md` §7 ("Game Rule Protection"), §17 ("Documentation Change Rule"), §20 ("When AI Must Stop"), and §23 ("Final Principle"), balance simulation evidence does **not** constitute implicit approval to modify production constants or gameplay rules.

Every balance question from `TASK-191` §6 is classified under one of four rigorous operational states:
1. `APPROVED — implement`: An authoritative project decision or Product Owner ruling explicitly approves an exact numerical value or rule change.
2. `EVIDENCE ONLY — do not change`: Empirical evidence exists to characterize the mechanic, but the project has resolved that no numerical standard applies (e.g. Q-1 Qualitative).
3. `OPEN — requires explicit product decision`: Evidence exists (or has characterized sensitivity), but no authoritative approval for an exact numerical change has been issued. The existing production configuration remains intact.
4. `BLOCKED — insufficient evidence`: Evidence cannot be gathered without unauthored design formulas or unimplemented prerequisite systems.

---

## 2. Required Decision Table

The table below reconciles all 12 questions from `TASK-191` §6 against baseline evidence (`TASK-194` / `TASK-195`), controlled experiment evidence (`TASK-197`), and existing authoritative decisions in `docs/`:

| Question | Topic / Proposal | Evidence Reference | Existing Decision in Documentation | Action Classification | Production Status |
|---|---|---|---|---|---|
| **Q-1** | Fight duration target (`BT-6`) | `TASK-194` §22, `TASK-196` §3 Q-1 (median 17 turns, bimodal; wall-clock excluded by determinism) | **DECIDED — OPTION B: QUALITATIVE** (`TASK-191` §6 Q-1; `TASK-193` §2). Observational metric only. | `EVIDENCE ONLY — do not change` | Kept as observational harness metric; no numeric gate in production. |
| **Q-2** | Power Charge cost 0 retention (`B-01`) | `TASK-197` EXP-05 (Cost 0: 80% avg win rate, 31.8 casts; Cost 10: 60% win rate, -33% casts; Cost 20: 0 casts, card starved) | **APPROVED.** Retain authored PowerCost 0 (`TASK-200`). ADR-021 bounds infinite loops. | `APPROVED — retain` | Cost `0` retained in production. |
| **Q-3** | Boss stat curve & DEF asymmetry (`B-03`/`B-04`) | `TASK-194` §23, `TASK-196` §3 Q-3 (Bosses 1–3 flat EHP 7500; Bosses 4–5 inverted EHP 3000/2800, shorter turns, higher win rate) | **APPROVED.** Keep current Boss 1–5 stats (`TASK-200`). Mechanics-driven progression accepted for MVP. | `APPROVED — retain` | Existing `BossDefinitions` stats retained in production. |
| **Q-4** | One card cast per turn limit (`B-02`) | `TASK-194` §25, `TASK-196` §3 Q-4 (7,274 turns verified; 0 infinite loops; 3 card-cast terminal wins) | **APPROVED — OPTION B** (`TASK-191` §6 Q-4; `ADR-021`; `CARD_RULES.md` §3 item 6; `GAME_RULES.md` §11 item 4). | `APPROVED — implement` | Implemented in `TASK-192` (`CardCastExecutor.cs`). Preserved. |
| **Q-5** | "No strictly dominated Card/Relic" (`B-09`) | `TASK-197` EXP-02 (0 Shield casts was 100% loadout ordering artifact; Shield achieves 76% win rate in skilled play vs 28% for Heal; Shield is not dominated) | **APPROVED.** Retain current values, both 20 Power (`TASK-200`). | `APPROVED — retain` | Existing Heal and Shield values retained in production. |
| **Q-6** | Pet Tier/Star/Level stat curve (`B-05`) | `TASK-196` §3 Q-6 (`NOT MEASURABLE` in combat; all Pets enter at identical 1000/50/25/5/0) | **APPROVED.** Tier/Star/Level remain cosmetic / progression signals for MVP (`TASK-200`). | `APPROVED — retain` | Identical defaults preserved in production. |
| **Q-7** | Player XP pacing curve (`B-06`) | `TASK-196` §3 Q-7 (`NOT MEASURABLE` in combat harness; out-of-battle meta progression) | **APPROVED.** Keep current linear 1 win -> 1 level progression (`TASK-200`). | `APPROVED — retain` | Existing XP curve retained in production. |
| **Q-8** | Mộc Yêu regeneration (`B-07`) | `TASK-197` EXP-01 (Threshold 8 cuts triggers by 33% and min HP by 38%; 2% & 3% magnitude variants BLOCKED by hardcoding) | **APPROVED / IMPLEMENTED — THRESHOLD 8 MATCHES.** Update threshold from 5 to 8 matches; retain 5% MaxHP magnitude (`TASK-200`). | `APPROVED — implement` | Implemented in production (`BossDefinitions.cs`, `ProvisionBossDefinitions.cs`). |
| **Q-9** | Card damage normalization (`B-08`) | `TASK-197` EXP-03 (Iron Fang 3.76, Inferno 2.99, Earthshaker 2.38 Dmg/Power; Earthshaker not OP; Pet element governs damage) | **APPROVED.** Keep current authored card values (`TASK-200`). | `APPROVED — retain` | Existing Card damages and costs retained in production. |
| **Q-10** | Relic magnitudes & engines (`B-10`) | `TASK-197` EXP-04 (All 10 relics bounded within 0% to 8.7% delta; Battle Instinct +4.89% DPS; no infinite loops; Burning Curse trigger is contextual) | **APPROVED.** Keep current authored relic values (`TASK-200`). | `APPROVED — retain` | Existing 10 Relic definitions retained in production. |
| **Q-11** | Pet Passive magnitudes for Xích Lang / Sơn Hùng | `TASK-196` §3 Q-11 (`NOT MEASURABLE`; magnitudes unauthored in `PET_RULES.md` §8; effects unimplemented in code) | **APPROVED.** Pet Passives remain unimplemented for MVP (`TASK-200`). | `APPROVED — retain` | Remained unimplemented for MVP. |
| **Q-12** | Simulation harness prerequisite (`B-14` item 3) | `TASK-193`, `TASK-194`, `TASK-195`, `TASK-197` (2,909 tests pass; 1,170 simulation runs executed deterministically) | **APPROVED** (`TASK-191` §6 Q-12; `TASK-193` §1). | `APPROVED — implement` | Implemented in `TASK-193` and audited in `TASK-195`. Operational. |

---

## 3. Subsystem Decision Audits

### 3.1 Q-2 / B-01 — Power Charge

- **TASK-197 EXP-05 Evidence**:
  - Cost 0: High sustain win rate (80% in `average`), 31.76 casts per run.
  - Cost 10: Sustain win rate drops to 60% (-20%), casts drop to 21.16 (-33.4%). Skilled win rate lifts slightly from 28% to 36% due to longer turn survival.
  - Cost 20: Power Charge casts drop to 0.00 across all 50 runs. The card is completely starved because Heal (also costing 20) takes precedence in greedy heuristics.
- **Authoritative Rule State**:
  `CARD_RULES.md` §2 item 3 authors Power Charge cost as `0` by design. `ADR-021` resolved the infinite-loop vulnerability by enforcing **at most one card cast per committed Turn**, explicitly leaving B-01 (cost change) as an undecided product choice.
- **Production Safety Action**:
  Cost is **NOT** modified to 10 or 20. Neither option is approved. ADR-021 one-card-per-turn protection is maintained. Status remains `OPEN`.

---

### 3.2 Q-5 / B-09 — Shield vs. Heal Pairing

- **TASK-197 EXP-02 Evidence**:
  - Proved that the 0-cast observation in TASK-194 was **100% an artifact of loadout ordering** (Heal preceding Shield at identical 20 Power cost).
  - In `shield_only` and `shield_then_heal`, Shield was cast 224.48 times/run in `average` and 17.04 times/run in `skilled`.
  - In burst/skilled play, prioritizing Shield increased win rate from 28% to 76% (+225% turns, +260% min Pet HP) because Shield prevents overheal waste at high HP and expands the effective HP pool against boss burst.
  - In sustain play, Shield maintained an average minimum Pet HP floor of 820.8 versus 614.3 for Heal (+33.6%).
- **Authoritative Rule State**:
  Both cards cost 20 Power for 20% MaxHP magnitude (`CARD_RULES.md` §2). Shield is empirically not strictly dominated; it possesses a distinct tactical identity (burst mitigation vs. attrition recovery).
- **Production Safety Action**:
  Shield is **NOT** altered or nerfed. No approved design change exists. Status remains `OPEN`.

---

### 3.3 Q-8 / B-07 — Mộc Yêu Regeneration

- **TASK-197 EXP-01 Evidence**:
  - Raising the match threshold from 5 to 8 matches reduced triggers by ~33% and reduced boss minimum HP under skilled play by 38.7% (from 4,051 to 2,482 HP).
  - However, threshold 8 alone failed to break 1,000-turn stalemates under sustain play (5/5 stalemates persisted).
  - Testing 2% and 3% magnitude variants was **BLOCKED** because `BattleStateService.cs` line 1630 hardcodes the literal integer `5` (`var regenPerTrigger = (bossState.MaxHP * 5) / 100;`).
- **Authoritative Rule State**:
  `BOSS_RULES.md` §6.2.3 explicitly authors: "Regenerates 5% MaxHP (250) every 5 player matches."
- **Production Safety Action**:
  In strict accordance with the task instructions:
  1. Do not infer that 2% or 3% is better.
  2. Do not change 5% without explicit approval.
  3. Do not introduce a configurable regeneration magnitude merely for convenience.
  Status remains `OPEN`.

---

### 3.4 Q-9 / B-08 — Card Damage Efficiency & Normalization

- **TASK-197 EXP-03 Evidence**:
  - 125 runs across all 5 Pet Skills and all 5 Boss elemental matchups demonstrated distinct performance profiles.
  - Damage-per-Power efficiency: Iron Fang (3.76) > Inferno (2.99) > Venomous Bloom (2.89) > Earthshaker (2.38) > Tidal Barrier (2.35).
  - Earthshaker (150 flat at cost 100) is **not** overpowered relative to Inferno or Iron Fang; its efficiency is lower due to the absence of scaling secondary effects (Crit / Burn).
  - Elemental advantage (+50%) allows Iron Fang to break the Mộc Yêu stalemate (5/5 wins); elemental disadvantage (-25%) is uniformly lethal.
  - Card damage resolves with the **active Pet's Element** (`CardCastExecutor.cs`), meaning elemental matchup modifies card damage based on the Pet rather than card theme.
- **Authoritative Rule State**:
  `CARD_RULES.md` §4.1 and `ELEMENT_RULES.md` §5 author these exact values and semantics.
- **Production Safety Action**:
  No card damage or cost is modified. Existing card semantics, one-card-per-turn, and Pet-element damage attribution are strictly preserved. Status remains `OPEN`.

---

### 3.5 Q-10 / B-10 — Relic Magnitudes & Power Engines

- **TASK-197 EXP-04 Evidence**:
  - 550 runs across 10 canonical relics against an unequipped control proved that all relics produce bounded, non-degenerate effects (0% to 8.7% delta).
  - Power relics (Mana Crystal +8.7%, Arcane Battery +6.07%, Cascade Core +5.00%) accelerate card casts without producing infinite loops.
  - Combat relics (Battle Instinct +4.89% DPS; Berserker Core +1.76% DPS) provide perceptible but balanced boosts.
  - Burning Curse recorded 0 triggers in baseline exclusively because the baseline loadout carries Iron Fang (Metal direct damage, no Burn), confirming strict contextual dependence.
- **Authoritative Rule State**:
  `RELIC_RULES.md` §6 and §8.5 author all 10 canonical relics.
- **Production Safety Action**:
  No relic is modified or nerfed. Status remains `OPEN`.

---

## 4. Production Safety & Scope Audit

```text
Production code (src/) files modified:       0
Database migrations added / modified:        0
Authoritative rule documents modified:       0
Historical simulation artifacts modified:    0 (TASK-194 / TASK-197 JSONs intact)
Speculative pet progression added:          NONE
Unapproved XP curves added:                  NONE
Unapproved Pet passives added:               NONE
Unapproved Card / Relic / Boss mechanics:    NONE
```

All 2,909 unit, integration, and simulation tests continue to pass with zero failures:
- `GameServer.Domain.Tests`: 1,558 passed
- `GameServer.Infrastructure.Tests`: 412 passed
- `GameServer.Api.Tests`: 323 passed
- `GameServer.Application.Tests`: 616 passed

---

## 5. Critical Stop Condition — Decision Required Report

Because the repository does not contain explicit approval for proposed balance values, the execution agent stops before modifying production gameplay parameters.

```text
DECISION REQUIRED
```

The following decisions require explicit Product Owner approval before any production code change or content migration may be implemented:

### Item 1: Q-2 / B-01 — Power Charge Cost
* **Question**: Should Power Charge retain its authored `0` PowerCost, or should its cost be increased?
* **TASK-197 Evidence**:
  - Cost 0: 80% average win rate, 31.8 casts/run; bounded by ADR-021 (no infinite loop).
  - Cost 10: 60% average win rate (-20%), 21.2 casts/run (-33.4%); slows down sustain.
  - Cost 20: 52% average win rate, 0.0 casts/run (-100%); card completely starved.
* **Candidate Options**:
  - Option A: Retain Cost `0` (recommended per EXP-05; ADR-021 already bounds abuse).
  - Option B: Increase Cost to `10`.
  - Option C: Increase Cost to `15` or `20`.
* **Exact Value Requiring Approval**: `CardDefinition.PowerCost` for `card-power-charge` (`0` vs `10`).

---

### Item 2: Q-3 / B-03, B-04 — Boss Progression Curve & DEF Asymmetry
* **Question**: Should Bosses 1–3 retain flat base stats (5000 HP / DEF 50) and Bosses 4–5 retain glass-cannon stats (3000/2800 HP / DEF 0), or should HP and DEF scale monotonically?
* **TASK-194 / TASK-196 Evidence**:
  - Bosses 1–3 require 7,500 effective HP; average battle duration 38–51 turns; win rate 5/15.
  - Bosses 4–5 have 0 DEF, requiring only 3,000 / 2,800 effective HP; average duration 16–22 turns; win rate 7/15 and 10/15.
* **Candidate Options**:
  - Candidate A: Accept flat stats for 1–3 and glass-cannon stats for 4–5, documenting that difficulty is mechanics-driven per `GDD.md` §12.
  - Candidate B: Author a monotonic HP curve (e.g. 4000 → 4500 → 5000 → 5500 → 6000) and standardize DEF across all bosses (e.g. DEF 25 or 50).
* **Exact Value Requiring Approval**: `MaxHP` and `DEF` values for Bosses 1 through 5 in `BossDefinitions.cs` and `BOSS_RULES.md` §6.1.

---

### Item 3: Q-5 / B-09 — Heal vs. Shield Pairing
* **Question**: Is "no strictly dominated Card/Relic" a required MVP property, and should Heal and Shield maintain identical 20 Power cost?
* **TASK-197 Evidence**:
  - Shield is NOT strictly dominated. In damage-oriented play, prioritizing Shield lifts win rate from 28% to 76% and survival by +225%. In sustain play, Shield maintains a +33.6% higher minimum HP floor.
* **Candidate Options**:
  - Option A: Retain Heal and Shield pairing as-is (both 20 Power, 20% MaxHP magnitude), documenting their distinct tactical roles (Shield for burst soaking, Heal for attrition recovery).
  - Option B: Differentiate costs (e.g. Shield cost 15, Heal cost 20).
* **Exact Value Requiring Approval**: Confirmation of Option A or specific cost/magnitude changes for `card-heal` / `card-shield`.

---

### Item 4: Q-8 / B-07 — Mộc Yêu Regeneration Tuning
* **Question**: How should Mộc Yêu's regeneration be adjusted to eliminate 1,000-turn attrition stalemates while maintaining its identity as an attrition boss?
* **TASK-197 Evidence**:
  - Threshold 8 matches cuts trigger frequency by 33% and drops boss min HP under burst play by 38.7%, but sustain stalemate persists at 1,000 turns.
  - Magnitude reduction (to 2% or 3%) requires modifying production source code (`BattleStateService.cs:1630`) to expose a configurable parameter.
* **Candidate Options**:
  - Option A: Increase match threshold from 5 to 8 in `BossDefinition.PassiveDefinition.Threshold`.
  - Option B: Introduce architectural configuration for regeneration magnitude, setting it to 3% MaxHP every 5 matches.
  - Option C: Combine both: 3% MaxHP every 8 matches.
  - Option D: Retain current 5% / 5 matches as intended high-attrition design.
* **Exact Value Requiring Approval**: `Threshold` (5 vs 8) and `RegenPercentage` (5% vs 3% vs 2%) in `BOSS_RULES.md` §6.2.3 and production code.

---

### Item 5: Q-9 / B-08 — Card Damage Normalization
* **Question**: Should Pet Skill card damages be normalized to a target damage-per-Power tolerance band?
* **TASK-197 Evidence**:
  - Damage-per-Power spread ranges from 2.38 (Earthshaker) to 3.76 (Iron Fang). Earthshaker is not overpowered despite high flat damage (150). Elemental matchup (+50% / -25%) dominates throughput.
* **Candidate Options**:
  - Option A: Retain authored values across all 5 Pet Skills without normalization (recommended).
  - Option B: Approve an explicit tolerance band (e.g. 2.50 to 3.50 damage per Power) and adjust costs.
* **Exact Value Requiring Approval**: Approval of Option A or specific modified card costs/damages.

---

### Item 6: Q-10 / B-10 — Relic Balance & Magnitudes
* **Question**: Should any of the 10 MVP relics be rebalanced or capped?
* **TASK-197 Evidence**:
  - All 10 relics have bounded impact within 0% to 8.7% delta. No infinite loops exist post-TASK-192 and TASK-178.
* **Candidate Options**:
  - Option A: Retain all 10 canonical relic magnitudes as authored in `RELIC_RULES.md` §8.5 (recommended).
  - Option B: Buff Berserker Core (e.g. +10% ATK instead of +5%).
  - Option C: Add per-turn caps to Arcane Battery / Cascade Core.
* **Exact Value Requiring Approval**: Approval of Option A or specific relic magnitude adjustments.

---

### Item 7: Q-6 / B-05, Q-7 / B-06, Q-11 / B-14 — Meta & Unauthored Systems
* **Questions Requiring Product Owner Authorship**:
  - **Q-6**: Author $f(\text{Tier}, \text{Level}, \text{Star})$ in `PET_RULES.md` §6 or declare Tier/Star/Level cosmetic for MVP.
  - **Q-7**: Confirm flat 50-win leveling pace or author non-linear XP curve constant in `COMBAT_RULES.md` §7.
  - **Q-11**: Author concrete passive magnitudes for Xích Lang (Empower + Burn) and Sơn Hùng (Temp Defense) in `PET_RULES.md` §8.

---

## 6. Verification Results

Full test suite execution:

```text
dotnet test src/backend/GameServer.sln
```

- **GameServer.Domain.Tests**: 1,558 passed, 0 failed.
- **GameServer.Infrastructure.Tests**: 412 passed, 0 failed.
- **GameServer.Api.Tests**: 323 passed, 0 failed.
- **GameServer.Application.Tests**: 616 passed, 0 failed.
- **Total Passing Tests**: **2,909 passed**, 0 failed, 0 skipped.
- **Harness & Matrix Preservation**: All tests for TASK-192 (one-card-per-turn), TASK-193 (simulation harness), TASK-195 (measurement corrections), and TASK-197 (experiment matrix) pass bit-identically.

---

## 7. Final Task Status

```text
PARTIALLY COMPLETE — DECISIONS REMAIN OPEN
```

All previously approved balance decisions (Q-4 / ADR-021 one card per turn; Q-12 harness tooling; Q-1 qualitative duration) remain fully implemented and verified.
No unapproved balance values were guessed, inferred, or prematurely implemented into production code.
All remaining decision questions (Q-2, Q-3, Q-5, Q-6, Q-7, Q-8, Q-9, Q-10, Q-11) are documented with exact empirical evidence, candidate options, and parameter targets awaiting Product Owner resolution.
