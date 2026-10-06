# TASK-191 — Balance Pass Specification (Analysis Only)

<!--
  GEN-TASK EXECUTION MANIFEST — ANALYSIS / SPECIFICATION ONLY
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.

  References docs/ and src/ by path and section — it does NOT copy game
  rules, formulas, magnitudes, schemas, or API payload shapes.

  SCOPE OF THIS TASK: Audit every configurable gameplay value (Match-3,
  Elements, Player, Pets, Cards, Relics, Bosses, battle economy) and produce a
  balance specification. ANALYSIS ONLY — no gameplay code, no database schema,
  no API, no SignalR, no auth, no Collection Viewer changes. Every numeric
  recommendation is a candidate requiring Product Owner approval; values with
  no documentary evidence are marked REQUIRES PRODUCT OWNER DECISION.
-->

---

## Metadata

```text
Task ID:           TASK-191
Type:              BALANCE — analysis & specification (no implementation)
Status:            READY (Q-4 = OPTION B — ONE CARD CAST PER TURN — approved and
                    implemented by TASK-192; Q-12 = harness APPROVED and
                    Q-1 = QUALITATIVE — both recorded in §6 and specified by
                    TASK-193. All other Q/B items remain OPEN.)
Risk:              MEDIUM (specification introduces no behavior change; findings may
                    require follow-up rule/content decisions before implementation)
Priority:          HIGH (balance gates playtesting and MVP feel; several findings
                    affect whether battles are winnable/solvable as designed)
Primary Agent:     design (owns game-rule documents and balance intent)
Supporting Agents: review (conformance: no invented numbers presented as authority),
                   testing (validation-method and simulation design),
                   development (follow-up implementation tasks — NOT this task)
Workflow:          analysis (specification-only; no development workflow applies)
Skills:            discovery/documentation-discovery,
                   discovery/impact-analysis,
                   quality/scope-validation,
                   testing/test-scenario-generation
Model:             Gemini 3.8
Reasoning:         High
Dependencies:      TASK-009 / TASK-013 (PassiveTracker — charge/emission only),
                   TASK-105 / TASK-111 / TASK-119 (Card & combat effects),
                   TASK-131 / TASK-176 (Relic content & lifetimes),
                   TASK-153 / Task172 (Boss passive runtime),
                   TASK-169 (Element modifiers as configuration)
```

---

## Objective

Produce a **balance specification** covering all documented configurable gameplay
values across the Match-3 RPG. The specification:

1. Inventories every value that exists **for balance purposes**, with its code
   location and its owning document section.
2. Separates **Gameplay rules** / **Balance values** / **Technical constants** /
   **UI constants** / **Test fixtures** — only balance-relevant values enter the
   audit proper.
3. Classifies each Card and Relic (Underpowered / Balanced / Overpowered /
   Situational / Redundant / Unknown) and the Boss difficulty curve (too flat /
   too steep / reasonable / unknown).
4. States **Balance Targets** derived from documented design intent, marking any
   target the documents do not define as `REQUIRES PRODUCT OWNER DECISION`.
5. Proposes changes as `Current → Proposed` entries with reasoning, expected
   effect, and a validation method — never as silent new numbers.
6. Lists **Decisions Required** for Product Owner resolution.

---

## Authoritative References

- `docs/00-overview/GDD.md` §11 (damage flow), §12 (boss difficulty comes
  primarily from mechanics, not high HP).
- `docs/01-game-design/GAME_RULES.md` §5 (Combo table — canonical and
  configurable), §12 (Power range 0–100), §17 (resolution order), §18 (server
  authority), §20 (rule change policy).
- `docs/01-game-design/MATCH3_RULES.md` §1 (board side length is the **only**
  Match-3 surface explicitly reserved for a future balance pass), §5 (special
  gems), §6 (combo), §7 (deterministic RNG).
- `docs/01-game-design/COMBAT_RULES.md` §1 (pet stat defaults), §2 (per-Gem
  resource rates & tier multipliers — sole source of truth), §3 (damage pipeline),
  §5 (Crit composition), §7 (XP curve — documents the values as balance choices).
- `docs/01-game-design/ELEMENT_RULES.md` §2.1–§2.2 (matchup structure is rule;
  factors are configuration).
- `docs/01-game-design/PET_RULES.md` §5.7 (Level/Star stat curve is **not
  defined here — a balance concern**), §6 (stat composition formula), §8 (passive
  magnitudes).
- `docs/01-game-design/CARD_RULES.md` §2 (Power Charge cost 0 by design), §3
  (cast flow), §3.6 (cost formula), §4 (effects).
- `docs/01-game-design/RELIC_RULES.md` §5 (anti-infinite-chain rule), §6 (cost
  doctrine), §8.5 (all 10 relic triggers/conditions/magnitudes).
- `docs/01-game-design/BOSS_RULES.md` §6.1 (MVP base configuration — explicitly
  "not universal balance invariants"), §6.2–§6.4 (passives/skills), §7 (difficulty
  from mechanics).
- `docs/01-game-design/PASSIVE_RULES.md` (exact passive magnitudes deferred as
  configuration where not authored).
- `docs/00-overview/MVP_SCOPE.md` §1/§2 (scope gate for any follow-up).

---

## 1. Current State

### 1.1 Value classification legend

```text
G = Gameplay rule (changing it is a rule change per GAME_RULES §20 / AGENTS §17)
B = Balance value (docs explicitly call it configurable / configuration)
T = Technical constant (implementation mechanics; not a balance lever)
UI = UI constant (presentation only; excluded from balance audit)
Test = Test fixture (assertion value; excluded from balance audit)
```

### 1.2 Match-3 economy inventory

| Value | Class | Current | Code | Doc owner |
|---|---|---|---|---|
| Board size | B (reserved) | 8×8 | `GameServer.Domain/Match3/BoardState.cs` | `MATCH3_RULES.md` §1 — board side length is the only reserved Match-3 balance surface |
| Minimum match length | G | 3 | `Match3` module | `MATCH3_RULES.md` §3 item 2 |
| Gem resource rates | B | ATK +10, DEF +5, HP +20, POWER +10 per gem | `ResourceGenerator.cs` (`AtkGemBaseDamage` … `PowerGemBasePower`) | `COMBAT_RULES.md` §2 (sole source of truth; "configuration, not hardcoded constants") |
| Match tier multipliers | B | Match3 1.0×, Match4 1.5×, Match5 2.0×, L/T 1.25× (Denominator 4) | `MatchTier.cs` (`MatchTierMultipliers`) | `COMBAT_RULES.md` §2 |
| Run ≥ 6 | G | counts as Match 5 (no 4th special-gem type) | `MatchTier.cs` | `MATCH3_RULES.md` §5.3 item 3 |
| Special gem shapes | G | LineClear (run of 4) clears row/column; Burst (run ≥5) clears 3×3; Area (L/T) clears 5 cells | `SpecialGem.cs` | `MATCH3_RULES.md` §5.5 |
| Cascade multiplier | G | none (pools sum across passes; no cascade bonus) | `ResourceGenerator.cs` | `MATCH3_RULES.md` §5.7 item 7 |
| Power clamp | G | 0–100 | `ResourceGenerator.cs` (`MaxPower`/`MinPower`) | `GAME_RULES.md` §12 |
| Board generation | T | `MaxAttempts = 64` | `BoardGenerator.cs` | technical (attempt bound, not a tuning surface) |

### 1.3 Element & damage pipeline inventory

| Value | Class | Current | Code | Doc owner |
|---|---|---|---|---|
| Matchup cycle | G | Mộc → Thổ → Thủy → Hỏa → Kim → Mộc | `ElementMatchups.cs` | `ELEMENT_RULES.md` §2 (relationship structure is rule, not balance) |
| Element factors | B | Advantage 1.50, Neutral 1.00, Disadvantage 0.75 | `ElementModifiers.cs` (`Default`) | `ELEMENT_RULES.md` §2.2 (configuration) |
| Combo factors | B | 1.00 / 1.10 / 1.20 / 1.35 / 1.50× (Denominator 100) | `ComboModifiers.cs` (`Default`) | `GAME_RULES.md` §5 (canonical table, configurable) |
| DEF mitigation | G | `damage × 100 / (100 + DEF)` | `DamagePipeline.cs` | `COMBAT_RULES.md` §3.2 |
| Crit | B | base 5%; roll `V < EffectiveCrit` in `[0,100)`; Critical factor 1.50×; cap 100pp | `DamagePipeline.cs` (`CritMultiplier`, `CritRollBound`, `MaxCritPercentagePoints`) | `COMBAT_RULES.md` §5 |
| Damage floor / order | G | Base → Combo → Element → Crit → DEF → clamp ≥0 → Shield → HP | `DamagePipeline.cs` | `COMBAT_RULES.md` §3 |
| Pipeline constants | T | Denominators 100/100, truncation points | `DamagePipeline.cs` | technical (rounding mechanics) |

### 1.4 Player & Pet inventory

| Value | Class | Current | Code | Doc owner |
|---|---|---|---|---|
| Pet battle defaults | B | HP/MaxHP 1000, ATK 50, DEF 25, Crit 5, Power 0 | `PetState.cs` (`DefaultMaxHP` … `DefaultPower`) | `COMBAT_RULES.md` §1.1 |
| Pet stat input | G | `BattleStartService` → `PetState.AtBattleCreation` passes **no stats** (Element/Passive/loadouts only) | `BattleStateService.cs` / `BattleStartService.cs` | `PET_RULES.md` §5.7/§6 — stat curve `f(Base Stat, Level, Star)` **not authored** |
| Consequence | B-relevant | All 5 pets are **stat-identical** in battle; Tier/Star/Level contribute 0 | — | derived from above (no doc grants stats elsewhere) |
| Player XP curve | B | +100 XP per win, 0 per loss, flat 100 XP/level, cap Level 50 (1 win = 1 level) | `Player.cs` (`BattleWonXpReward`, `XpPerLevelCurveConstant`, `LevelForXp`) | `COMBAT_RULES.md` §7 (values documented as balance decisions) |
| Player Level combat effect | G | none (Level grants no combat stats) | — | no doc grants player-level stats |
| Pet XP curve | B | same +100/100-per-level (pet cap Level 49 per PET_RULES §5) | `PET_RULES.md` §5 | as above |
| Pet passive thresholds | B (content) | Thanh Xà 7, Sơn Hùng 5, Huyền Quy 6, Xích Lang 5, Bạch Hổ 4 player matches | content migration `20260929152651…` / `20261004055006…` | `PET_RULES.md` §8 |
| Pet passive magnitudes | B | Thanh Xà +8% MaxHP heal, Huyền Quy Shield 15% MaxHP (authored); Bạch Hổ +10pp Crit (example in `COMBAT_RULES.md` §5.4); **Xích Lang & Sơn Hùng not authored** | `PET_RULES.md` §8, `PASSIVE_RULES.md` | `PASSIVE_RULES.md` defers exact magnitudes → Xích Lang / Sơn Hùng = `REQUIRES PRODUCT OWNER DECISION` |
| Starter set | B (content) | Xích Lang, Bạch Hổ, Huyền Quy; 3 Basic cards; 3 relics | `PlayerStarterGrantFactory.cs` | `MVP_SCOPE` / task records |

### 1.5 Card inventory & classification

Costs are the `PowerCost` column of the `CardDefinition` provisioning migration
(`20260929152651_ProvisionPetCardRelicContentDefinitions.cs`, column order
`Id, Name, Category, PowerCost, LoadoutCopyLimit, EffectDefinition`).

| Card | Cat | Cost | Effect (doc owner `CARD_RULES.md` §2/§4) | Classification | Evidence / rationale |
|---|---|---|---|---|---|
| Heal | Basic | 20 | Restore 20% MaxHP (200 HP) | **Balanced (staple)** | Sole repeatable HP recovery; 10 HP per Power; overheal discarded; strongest in attrition fights but useless at full HP |
| Shield | Basic | 20 | Shield 20% MaxHP (200 pool, persists until depleted) | **Situational** | Equal cost to Heal; better at full HP / vs burst, worse in sustained attrition — neither strictly dominates, but its niche is narrow; candidate for cost differentiation |
| Power Charge | Basic | **0** | +25 Power | **Overpowered** | Zero cost + **no cast limit** + cast does not consume a Turn ⇒ spammable free resource engine (see B-01/B-02) |
| Inferno (Xích Lang) | PetSkill | 100 | 120 damage (Hỏa) + Burn 50 × 2 Turns | **Balanced / element-Situational** | Total ~220 expected but front/back-loaded; neutral vs Hỏa Long (same element), disadvantaged vs Hỏa→Kim matchup direction as attacker |
| Tidal Barrier (Huyền Quy) | PetSkill | 80 | Heal 20% + Shield 20% | **Balanced** | Two 20%-MaxHP effects for 80; premium sustain, fair cost |
| Iron Fang (Bạch Hổ) | PetSkill | 100 | 120 damage (Kim) + Crit +10pp NextAttack | **Situational** | Kim attacker is disadvantaged (×0.75) vs Hỏa Long — hardest starter pairing; +10pp Crit amortizes only over follow-up attack |
| Venomous Bloom (Thanh Xà) | PetSkill | 80 | 80 damage (Mộc) + Burn 25 × 2 | **Balanced** | 130 expected for 80 — best damage-per-Power among PetSkills; justify vs its element matchups |
| Earthshaker (Sơn Hùng) | PetSkill | 100 | 150 damage (Thổ) | **Overpowered-candidate / Unknown** | Highest flat damage at equal cost, no secondary drawback; but element matchups and DEF mitigation apply — needs simulation before a verdict |

Supporting facts (verified in `CardCastExecutor.cs`):

- Cast gates are **only**: card in loadout → category Basic/PetSkill → effective
  Power ≥ cost. **No cooldown, no per-Turn cast limit, no cast-count limit exists**
  (`grep CastCount|MaxCasts|CardCooldown` → none).
- Card/PetSkill **Damage** effects enter the real Damage Pipeline (element factor,
  boss DEF, Crit composition apply); `Combo` is fixed at 1 for card damage.
- A card cast does not consume a Turn and does not trigger the boss response
  (`ExecuteCardCastAsync` never resolves boss attack/skill; `GAME_RULES.md` §17
  orders boss response inside swap resolution only).

### 1.6 Relic inventory & classification

All magnitudes doc-verified at `RELIC_RULES.md` §8.5 (table lines 697–706 and
structured payloads §8.8).

| Relic | Trigger / condition | Effect | Lifetime | Classification | Rationale |
|---|---|---|---|---|---|
| Berserker Core | OnMatchCount ≥3 | +5% ATK | Battle | **Underpowered-candidate / Unknown** | +5% of base ATK 50 ≈ +2.5 base damage per firing at threshold cadence; stacking semantics (refresh-not-stack) need simulation to verdict |
| Mana Crystal | OnMatchCount ≥4 | +10 Power | Immediate | **Balanced** | Accelerates card economy at a documented cadence |
| Assassin Eye | OnCombo ≥3 | +10pp Crit | NextAttack | **Balanced** | Moderate, conditional |
| Emergency Core | HP <30% | Heal card cost −50% | Battle | **Situational** | Deficit-only; synergizes with Heal staple |
| Burning Curse | OnBattleStart | +30% Burn damage | Battle | **Situational / Unknown** | Only two cards inflict Burn; value depends on Burn-card inclusion — narrow |
| Combo Fang | OnCombo ≥5 | +20pp Crit | NextAttack | **Situational** | High-roll only (Combo 5+ = 1.5× territory) |
| Arcane Battery | OnPowerGain | +5 Power | Immediate | **Overpowered-candidate** | Multiplies every POWER-gem gain and cascade; stacks with Cascade Core; feeds the Power Charge loop (B-01) |
| Execution Mark | HP <30% | +15pp Crit | NextAttack | **Situational** | Deficit-only execute window |
| Cascade Core | OnCascade | +5 Power | Immediate | **Balanced / Overpowered-candidate** | Resolves per cascade iteration (§8: three cascades = three firings); cascade is near-every-swap → strongest Power engine alongside Arcane Battery |
| Battle Instinct | OnDamageTaken | +10% ATK | NextAttack | **Balanced** | Reactive, consumed by next qualifying attack |

Note: `RELIC_RULES.md` §5's anti-infinite-chain rule already forbids
`OnPowerGain → Arcane Battery → +5 Power → OnPowerGain → …` self-chaining; the
rule documents the *principle* that free recursive gains are invalid.

### 1.7 Boss inventory & difficulty-curve classification

Doc-verified (`BOSS_RULES.md` §6.1–§6.4) and code-confirmed
(`BossDefinitions.cs`):

| Boss | Element | HP | ATK | DEF | Enrage | Skill (base / CD / charge) | Secondary | Passive |
|---|---|---|---|---|---|---|---|---|
| Hỏa Long | Hỏa | 5000 | 100 | 50 | 30% | 150 / 2T / 5 | Burn 50 ×2T | Rage +20% ATK, threshold 5 matches |
| Thủy Ma | Thủy | 5000 | 100 | 50 | 30% | 120 / 3T / 4 | PowerDrain −20 | Heal reduction −50%, always active |
| Mộc Yêu | Mộc | 5000 | 100 | 50 | 30% | 100 / 2T / 6 | ATK debuff −30% ×2T | Regen 5% MaxHP (250) every 5 player matches |
| Sơn Thạch Vệ | Thổ | 3000 | 120 | 0 | 50% | 150 / **0T** / 5 | none | once-per-battle at HP ≤50%, persistent |
| Kim Lôi Vương | Kim | 2800 | 140 | 0 | 75% | 180 / **0T** / 5 | none | triggered on player Combo ≥4 |

Derived observations:

- **Curve classification: TOO FLAT → then HP-inverted.** Bosses 1–3 are
  stat-identical; bosses 4–5 have *lower* HP and zero DEF. Difficulty rise is
  carried by ATK, cooldown-0 skills, and mechanics — which matches
  `GDD.md` §12 / `BOSS_RULES.md` §7 intent ("difficulty must come primarily from
  mechanics, not high HP"), but the **stat curve itself is non-monotonic** and
  the first three fights are indistinguishable by numbers alone.
- DEF asymmetry: ×0.667 mitigation for bosses 1–3, none for 4–5 ⇒ bosses 1–3 are
  disproportionately tanky per HP point.
- Boss Combo = 1 (no boss-side combo scaling); one boss attack per committed swap.
- Enrage: `HP < MaxHP × threshold`, strict, permanent, no event.

### 1.8 Derived throughput analysis (analysis, not authority)

Assumptions stated explicitly; every figure is a candidate for simulation-based
validation, **not** a documented target.

```text
Player per-swap damage (neutral matchup, boss DEF 50):
  base ≈ pet ATK 50 + ATK-gem pool (2–4 gems × 10, tier-scaled) ≈ 70–90
  × combo 1.00 (Combo 1) × element 1.00 × DEF 0.667 ≈ 47–60
  strong swap (cascade, Combo 4 =1.35, advantage 1.5, pool ~150):
  ≈ 150 × 1.35 × 1.50 × 0.667 ≈ 200–270

Swaps to kill Hỏa Long (5000 HP): ≈ 90–110 typical, ≈ 20–25 excellent
  → candidate concern: fight length vs UX target (undocumented → PO)

Boss per-swap pressure (ATK 100, pet DEF 25 → ×100/125 = 0.80):
  neutral ≈ 80, advantage 120, disadvantage 60
  Skill (150, DEF 50 boss-side irrelevant; pet DEF applies) ≈ 120 + Burn 50×2
  Pet HP 1000 ≈ 12–13 neutral hits; Heal card = +200 HP / 20 Power
  Power income ≈ 10 per POWER gem → ≈ 1 heal per 1–2 swaps

Sustain verdict: healing throughput ≈ effective incoming damage ⇒ attrition
  fights are survivable; binding constraint is fight duration, not survival —
  EXCEPT vs Mộc Yêu regen (250 per 5 player matches ≈ every 3–5 swaps,
  comparable to player damage) → stalemate risk.

Element pairing (starter set vs Hỏa Long):
  Huyền Quy (Thủy) advantage ×1.5 (easiest), Xích Lang (Hỏa) neutral,
  Bạch Hổ (Kim) disadvantage ×0.75 (hardest — boss counters player)
```

### 1.9 Documentation & implementation observations (reported, not fixed)

Per `AGENTS.md` §16 these are reported only; no unrelated fixes are made.

| # | Observation | Location | Impact |
|---|---|---|---|
| D-1 | Comments state "no Crit roll exists in MVP" although Crit is implemented (roll, 1.5× factor, composition, consumption) | `DamagePipeline.cs` lines ~16, 36, 97–99; `DamageEvents.cs` ~line 108 | Stale documentation in code — misleading for balance readers; suggest follow-up comment-only task |
| D-2 | **Pet Passive effects are not applied.** Swap resolution step 10 only calls `PassiveTracker.Charge` and emits `PassiveCharged`/`PassiveTriggered`; no pet-passive effect application found (pet passive IDs appear only in content migrations). Boss passives ARE applied (step 9) and tested. | `BattleStateService.cs` (~1191–1241); `Passives/PassiveTracker.cs`; tests: `Task172BossPassiveRuntimeTests.cs` exist, pet passive effect tests absent | **Blocks** any pet-passive balance validation; PET_RULES §8 magnitudes cannot be tuned before implementation exists — follow-up task required |
| D-3 | Card cast free-action loop (see B-02): Power Charge costs 0, casts are unlimited and consume no Turn and provoke no boss response | `CardCastExecutor.cs`, `ExecuteCardCastAsync` | Documented rule behavior (MATCH3_RULES: "a Card cast does not consume a Turn") but produces a risk-free damage cycle → balance defect, not code defect |
| D-4 | `Player.cs` comment: "Both currently equal 100; that equality is a balance decision" (XP reward = per-level constant) | `Player.cs` ~line 1836 region | Confirms flat 1-win-per-level pacing is deliberate-but-unreviewed |
| D-5 | Pet `Tier`/`Star`/`Level` are collection metadata with zero battle effect | `PET_RULES.md` §5.7/§6 (curve not authored) | Meta progression currently has no gameplay payoff |

---

## 2. Balance Targets

Targets **derived from documented intent** (authoritative), then targets the
documents do **not** define (PO).

| ID | Target | Source / status |
|---|---|---|
| BT-1 | Boss difficulty comes **primarily from mechanics, not raw HP**; HP values are MVP configuration, not invariants | `GDD.md` §12, `BOSS_RULES.md` §6.1/§7 — **documented** |
| BT-2 | Element advantage/disadvantage must be perceptible in outcomes (1.50 / 0.75 structure) | `ELEMENT_RULES.md` §2.2 — **documented** (relationship structure) |
| BT-3 | Match resolution is deterministic; all tuning values are configuration reachable without rule changes where the docs say "configurable" | `GAME_RULES.md` §5, `COMBAT_RULES.md` §2, `MATCH3_RULES.md` §1 — **documented** |
| BT-4 | No free recursive resource chain (anti-degenerate-economy principle) | `RELIC_RULES.md` §5 — **documented for relics**; the extension to card casts was a PO decision and is now **DECIDED** (Q-4 = OPTION B, one card cast per committed Match-3 Turn; `CARD_RULES.md` §3 item 6, `ADR-021`) |
| BT-5 | Every Card and Relic is a meaningful build choice — no strictly dominated option | **inferred** design intent (10 relics / 8 cards each have distinct triggers); confirm → Q-5 |
| BT-6 | Target fight duration / number of swaps per boss | **DECIDED — QUALITATIVE (Q-1 = OPTION B).** No document defines a numeric target and none is invented. Duration is an **observational** harness metric; acceptability is a Product Owner judgment, not a computed pass/fail. See `TASK-193` §2. |
| BT-7 | Pet Tier/Star/Level must affect battle power via the stat curve `f(...)` | `PET_RULES.md` §5.7/§6 declares the formula but authors **no curve** → **`REQUIRES PRODUCT OWNER DECISION`** |
| BT-8 | Player Level pacing (currently 1 win = 1 level to cap 50) and whether Level should matter in combat | `COMBAT_RULES.md` §7 documents the values; the **target pace** → **`REQUIRES PRODUCT OWNER DECISION`** |
| BT-9 | Boss stat curve should be monotonic or justify inversion by mechanics | **inferred** from BT-1 (mechanics carry difficulty) + non-monotonic stats observed → confirm → Q-3 |
| BT-10 | Balance of pet passives (magnitudes for Xích Lang / Sơn Hùng; all magnitudes overall) | **blocked** by D-2 until effects are implemented; magnitudes not fully authored → **`REQUIRES PRODUCT OWNER DECISION`** |

---

## 3. Proposed Changes

Each entry is a **candidate specification**. No number below is approved.
"Proposed" values marked `REQUIRES PO DECISION` require Q-answering first.
Validation methods are executable checks, not guarantees.

### B-01 — Power Charge free-spam (Card economy)

- **Classification:** Overpowered (Power Charge); systemic (card economy)
- **Current:** Power Charge cost `0` (`CARD_RULES.md` §2 item 3 — "costs 0 by
  design"); no cast limit; cast consumes no Turn.
- **Proposed:** `REQUIRES PRODUCT OWNER DECISION` — options:
  (a) content-only: raise Power Charge `PowerCost` to 10–25 (new EF migration);
  (b) rule: limit Basic-card casts per Turn (rule change → `CARD_RULES.md` §3 +
  `GAME_RULES.md` §17 update first, per `AGENTS.md` §17);
  (c) leave as-is if B-02 is resolved by a Turn-consumption rule.
  **Note (Q-4 approved as OPTION B):** B-02 is now resolved by a per-Turn
  *cast-count* limit, not by Turn consumption, so option (c) does not apply.
  B-01 remains an open decision: the `0` cost is retained and out of scope for
  B-02, which expressly changes no Card cost.
- **Reasoning:** cost 0 + unlimited + free action = strictly dominant opener;
  accelerates every other card and interacts with Arcane/Cascade Power engines.
- **Expected effect:** restores Power as a real constraint; reduces dominance of
  Power-fueled cards; makes `CARD_RULES.md` §2.3's deliberate cost-0 design
  defensible only under a per-Turn limit.
- **Validation:** scripted-cast integration test asserting Power can't be farmed
  beyond N per Turn; economy simulation (casts/fight histogram) before/after.

### B-02 — Risk-free card-cast cycle (free actions)

- **Classification:** systemic balance defect (documented-rule-consistent)
- **Current:** a card cast neither consumes a Turn nor triggers the boss attack,
  and casts are unlimited ⇒ `Power Charge ×4 → damage card` loops kill the boss
  with **zero incoming risk** (infinite for every boss; even the loop-free
  variant gives ~100+ damage per free action).
- **Proposed:** **APPROVED — OPTION B** (`TASK-191` §6 Q-4, approved by the
  Product Owner; recorded as `ADR-021`):

  ```text
  OPTION B — ONE CARD CAST PER TURN
  ```

  A player may successfully cast **at most one Card during each committed
  Match-3 Turn**. This is a **cast-count constraint, not Turn consumption**:
  a Card cast still consumes no Turn, does not resolve the Match-3 board, and
  does not independently trigger the normal boss response
  (`CARD_RULES.md` §3 item 5, `MATCH3_RULES.md` §8.1 item 5 — both preserved).
  The Match-3 Turn remains the authoritative unit of combat progression; the
  boss response is reached only through a committed Swap. After one successful
  cast, further casts are rejected until the next committed Turn.

  Rejected alternatives: **Option A** (cast consumes a Turn / triggers the boss
  response — would fundamentally couple Cards to the Match-3 Turn system) and
  **Option C** (keep free actions, remove only the zero-cost generators —
  leaves the underlying unlimited zero-risk chain intact).
- **Reasoning:** without a brake, optimal play degenerates to menu management;
  boss mechanics/skills (the documented difficulty source, `GDD.md` §12) never
  fire because the boss only acts on swaps. Option B removes the degenerate loop
  without coupling Cards to the Turn system, which is why it was preferred over
  A; Option C was insufficient because it addresses the fuel (B-01) rather than
  the mechanism.
- **Expected effect:** boss passives/skills become reachable; fights regain
  interaction; element matchups matter again. Card casts keep their existing
  independence — only the *number* of casts per Turn is bounded.
- **Validation:** E2E "loop attempt" test — cast Basic cards repeatedly with no
  swap and assert either a rejection, a Turn advance, or a boss response;
  assert no battle can reach `BattleWon` without at least one committed swap.
- **Status:** ✅ **UNBLOCKED — approved, awaiting implementation.** Implemented
  by a separate task (docs-first, per `AGENTS.md` §17). This task does not
  implement it.

### B-03 — Boss stat curve

- **Classification:** curve too flat (1–3) / HP-inverted (4–5)
- **Current:** 5000/100/50/30% ×3, then 3000/120/0/50%, 2800/140/0/75%.
- **Proposed:** `REQUIRES PRODUCT OWNER DECISION` (see Q-3). Candidate A
  (mechanics-only, doc-aligned): keep HP as-is; differentiation already via
  skills/secondaries/passives — accept flat stats and document it. Candidate B
  (monotonic HP): e.g. 5000 → 5500 → 6000 → 6500 → 7000 while keeping
  mechanics-driven pressure (numbers illustrative only).
- **Reasoning:** `GDD.md` §12 forbids difficulty-by-HP as the primary lever, but
  a non-monotonic curve still reads as no-progression; either accept-and-document
  (A) or make HP gently monotonic while keeping mechanics primary (B).
- **Expected effect:** A = zero change, documented rationale; B = perceptible
  ramp without violating §12.
- **Validation:** simulation harness (below) measuring swaps-to-kill and
  win-rate per boss under 3 scripted skill levels; assert ordering matches the
  chosen candidate.

### B-04 — Boss DEF asymmetry

- **Classification:** unclassified balance artifact
- **Current:** DEF 50 for bosses 1–3 (player damage ×0.667), DEF 0 for 4–5.
- **Proposed:** `REQUIRES PRODUCT OWNER DECISION` — either keep (later bosses are
  "glass cannons"; verify win-rate) or flatten (e.g. 0/25/50 pattern aligned
  with HP curve).
- **Reasoning:** combined with lower HP on 4–5, players experience a
  disproportionate damage jump at boss 4 unrelated to intended difficulty.
- **Validation:** per-boss effective-HP metric (`HP × (100+DEF)/100`): current
  effective HP = 7500, 7500, 7500, 3000, 2800 — verify this ordering against
  intended difficulty (BT-9).

### B-05 — Pet stat curve (Tier/Star/Level)

- **Classification:** missing balance surface (meta progression inert)
- **Current:** `f(Base Stat, Level, Star)` undefined; all pets enter battle with
  identical 1000/50/25/5/0; Tier/Star/Level are display-only.
- **Proposed:** `REQUIRES PRODUCT OWNER DECISION` (Q-6): (a) author the curve in
  `PET_RULES.md` §5.7/§6 (e.g. per-Level percentage growth + per-Star bonus) and
  implement in a follow-up task; (b) declare Level/Star cosmetic for MVP and
  document that explicitly.
- **Reasoning:** players are told pets progress but gain nothing; either payoff
  or honesty.
- **Expected effect:** (a) gives meta progression meaning and re-opens pet
  balance as a lever; (b) removes implied promise.
- **Validation:** (a) unit tests asserting distinct battle stats per Level/Star;
  (b) doc statement present; either way a balance-sim re-run with (a).

### B-06 — Player XP pacing

- **Classification:** flat pacing (deliberate but unreviewed, D-4)
- **Current:** 100 XP/win = 1 level/win → Level 50 after 50 wins; Level grants
  no combat stats.
- **Proposed:** `REQUIRES PRODUCT OWNER DECISION` (Q-7): keep as MVP (document
  target), or curve (`XpPerLevelCurveConstant` increase, or per-level table), or
  make Level matter (needs BT-7-like design).
- **Reasoning:** pacing target undefined; current value is explicitly flagged as
  a balance decision in code.
- **Validation:** simulate N wins → level curve; assert against chosen target.

### B-07 — Mộc Yêu regeneration vs player throughput

- **Classification:** stalemate risk (regen ≈ player damage)
- **Current:** +250 HP every 5 player matches (~every 3–5 swaps), while typical
  player damage is ~50–60/swap vs its DEF 50.
- **Proposed:** `REQUIRES PRODUCT OWNER DECISION` (Q-8) — reduce magnitude
  (e.g. 5% → 2–3% MaxHP) or raise threshold (5 → 8 matches), or keep and accept
  it as intended "attrition boss" mechanic (doc-aligned: difficulty via
  mechanics). Numbers illustrative.
- **Reasoning:** net-positive regeneration can make the fight unwinnable or
  interminable for low-skill play.
- **Validation:** simulation of scripted fights vs Mộc Yêu; assert P(win) and
  duration within BT-6 targets; test the degenerate "weak player" policy.

### B-08 — Card damage efficiency normalization

- **Classification:** Earthshaker Overpowered-candidate; Power Charge
  Overpowered (B-01); others Balanced/Situational (§1.5)
- **Current:** Earthshaker 150 flat for 100 Power vs Inferno 120 + Burn 100 for
  100, Iron Fang 120 +10pp for 100, Venomous Bloom 80 + Burn 50 for 80.
- **Proposed:** `REQUIRES PRODUCT OWNER DECISION` (Q-9): normalize cost-per-
  expected-damage (consider element factor, DEF mitigation, Burn duration) —
  candidates only after simulation produces expected-value numbers per card
  under the five boss matchups.
- **Reasoning:** same-cost cards should have comparable expected output or an
  explicit situational identity.
- **Validation:** expected-damage matrix (card × boss) computed by the
  simulation harness; assert spread within chosen tolerance (e.g. ±15%).

### B-09 — Heal / Shield pairing

- **Classification:** Shield Situational (narrow niche)
- **Current:** both cost 20 for 20% MaxHP; Heal restores HP, Shield adds a
  depleting pool.
- **Proposed:** `REQUIRES PRODUCT OWNER DECISION` — e.g. Shield cost 15 or
  magnitude 25% (content-only migration), or keep (identity: burst-vs-attrition).
- **Reasoning:** in attrition fights Heal strictly outperforms; Shield's niche
  (full-HP burst soak) rarely binds under current boss damage patterns.
- **Validation:** simulated fight variants measuring EHP contribution of each
  card; document resulting identity.

### B-10 — Relic magnitudes & engines

- **Classification:** Berserker Core Underpowered-candidate; Arcane Battery +
  Cascade Core Overpowered-candidate (Power engine stack); others Balanced/
  Situational (§1.6)
- **Current:** Berserker +5% ATK (Battle); Arcane +5 Power per gain; Cascade +5
  Power per cascade iteration.
- **Proposed:** `REQUIRES PRODUCT OWNER DECISION` (Q-10): e.g. Berserker +10%
  or threshold 3→2; cap Arcane/Cascade contributions per Turn (rule change) or
  reduce to +3 (content-only). Illustrative only.
- **Reasoning:** one relic family feeds the B-01/B-02 loop; another is likely
  imperceptible (+2.5 base damage).
- **Validation:** per-relic DPS/Power contribution measured by ablation
  simulation (relic on vs off); assert each relic contributes within a chosen
  band (e.g. 3–12% behavior change).

### B-11 — Crit stack headroom

- **Classification:** unclassified (composition cap 100pp)
- **Current:** base 5pp + Assassin +10pp + Combo Fang +20pp + Execution +15pp
  + Iron Fang +10pp + Bạch Hổ +10pp → up to ~70pp effective before any cap
  concern; Critical factor 1.50×.
- **Proposed:** `REQUIRES PRODUCT OWNER DECISION` — verify intended crit
  frequency; candidates: reduce stacking or lower factor. No change proposed
  without simulation evidence.
- **Validation:** simulated effective-crit distribution per build; expected
  damage inflation vs baseline.

### B-12 — Board size

- **Classification:** reserved balance surface, untouched
- **Current:** 8×8. `MATCH3_RULES.md` §1 reserves board side length as the only
  Match-3 balance-pass lever.
- **Proposed:** no change in this pass unless BT-6 simulation shows match rates
  can't meet targets at 8×8 → then `REQUIRES PRODUCT OWNER DECISION` (7×7 / 9×9).
- **Reasoning:** board size changes match probability, cascade frequency, and
  therefore every economy input in §1.2 — highest-blast-radius lever, last resort.
- **Validation:** only if invoked: match/cascade rate statistics at candidate
  sizes under a fixed seed harness.

### B-13 — Gem rates / tier multipliers / combo table

- **Classification:** B values, currently doc-default
- **Current:** 10/5/20/10; 1.0/1.5/2.0/1.25; 1.00–1.50×.
- **Proposed:** no change unless BT-6 simulation misses targets; these are the
  documented defaults and the documented first-order levers
  (`COMBAT_RULES.md` §2, `GAME_RULES.md` §5).
- **Validation:** if invoked, re-run economy simulation and assert fight-length
  target (BT-6) without breaking B-01/B-02 constraints.

### B-14 — Prerequisites (not balance changes, but blocking)

1. **D-2:** implement pet Passive effects (follow-up implementation task; doc
   magnitudes exist for 3 of 5 pets) — pet-passive balance cannot be validated
   before this.
2. **D-1:** comment-only cleanup task for stale "no Crit roll" comments.
3. Build a **balance simulation harness** (deterministic seed, scripted player
   policies: passive / average / skilled; boss matchup matrix) — this is the
   validation backbone for every entry above and does not ship gameplay logic
   (test-only tooling, `AGENTS.md` §15). ✅ **APPROVED (Q-12)** — specified in
   `tasks/backlog/TASK-193-deterministic-balance-simulation-harness.md`.

---

## 4. Acceptance Criteria

This task is specification-only. Done means:

- [ ] Spec covers **all** balance-class (B) values across Match-3, Elements,
      Player, Pets, Cards, Relics, Bosses, and battle economy (§1.2–§1.7), each
      with code path + owning doc section.
- [ ] Gameplay / Balance / Technical / UI / Test classification present; UI and
      Test values explicitly excluded from the audit.
- [ ] Every Card classified: Underpowered / Balanced / Overpowered / Situational /
      Redundant / Unknown (§1.5).
- [ ] Every Relic classified (§1.6).
- [ ] Boss curve classified: too flat / too steep / reasonable / unknown (§1.7 —
      "too flat → HP-inverted" with reasoning).
- [ ] Balance Targets split into documented (BT-1…BT-4) vs
      `REQUIRES PRODUCT OWNER DECISION` (BT-6…BT-10).
- [ ] Every Proposed Change carries Current → Proposed / Reasoning / Expected
      effect / Validation method; no proposed number presented as approved.
- [ ] Findings reported without modifying code (D-1…D-5; `AGENTS.md` §16).
- [ ] Decisions Required list complete and answerable by a Product Owner (§6).
- [ ] Scope: zero gameplay code, schema, API, SignalR, auth, or Collection
      Viewer changes (§7).
- [ ] Only `tasks/backlog/TASK-191-balance-pass.md` is added/modified
      (`git status` / `git diff` verified).

---

## 5. Test Plan

No production tests are written by this task. The plan defines (a) how the
spec's claims were/are verified, and (b) tests the future implementation tasks
must add.

### 5.1 Specification verification (run at task completion)

```text
[ ] Inventory cross-check: for each §1.2–§1.4 row, the cited code file and doc
    section exist and state the recorded value (spot re-verification performed
    during authoring: ComboModifiers, ElementModifiers, ResourceGenerator,
    MatchTier, CardCastExecutor, BossDefinitions, PetState, Player,
    RELIC_RULES §8.5 table).
[ ] Classification completeness: 8/8 cards and 10/10 relics classified.
[ ] Numbering: every numeric claim in §3 marked candidate or REQUIRES PO.
[ ] Scope: `git status` + `git diff` show exactly one new file.
```

### 5.2 Validation harness (prerequisite for any follow-up implementation)

```text
[ ] Deterministic simulation: fixed `RngState` seeds, scripted policies
    (passive / average / skilled swap selection), per-boss × per-pet matrix.
[ ] Metrics: swaps-to-kill, win-rate, damage taken, casts used, relic
    contribution (ablation), effective-HP ordering of bosses.
[ ] Regression baseline: current values recorded before any change lands.
```

### 5.3 Tests required when implementing approved items

```text
B-01/B-02  Integration: repeated card casts cannot produce a risk-free win;
            per-Turn cast limit or Turn consumption enforced server-side
            (server-authoritative, GAME_RULES.md §18).
B-03/B-04  Content assertions: BossDefinition stat table matches approved
            curve; enrage thresholds remain fractions of MaxHP.
B-05       Domain tests: battle stats differ per Level/Star per approved f(...);
            defaults unchanged for Level 1 / Star 0.
B-06       Domain test: LevelForXp matches approved curve; cap still 50 unless
            decided otherwise.
B-07       Simulation + domain test: regen magnitude/threshold per approval;
            no stalemate at the "weak player" policy.
B-08/B-10  Content tests: card PowerCosts / relic magnitudes match approved
            values (EF migration InsertData rows).
```

### 5.4 Edge cases to preserve

- Determinism: no RNG or clock introduced by any balance change
  (`MATCH3_RULES.md` §7, `AGENTS.md` §11).
- Power range 0–100 clamp; damage floor ≥ 0; overheal discarded; Enrage strict
  `<` and permanent.
- `RELIC_RULES.md` §5 anti-infinite-chain must hold for any new economy value.

---

## 6. Decisions Required

`REQUIRES PRODUCT OWNER DECISION` items, answerable independently.

| ID | Question | Options | Blocks |
|---|---|---|---|
| Q-1 | Approve a **target fight duration** (swaps and/or wall-clock per boss) for MVP? | ✅ **DECIDED — OPTION B: QUALITATIVE.** No numeric fight-duration target is approved, and **none is invented**: an exhaustive search of `docs/` found no authored duration, swap-count, or wall-clock target anywhere (`GDD.md`, `MVP_SCOPE.md`, `GAME_RULES.md`, `COMBAT_RULES.md`, `BOSS_RULES.md`, and every other rule document are silent on it). MVP balance therefore does **not** enforce a numeric duration target: the simulation harness reports duration as an **observational metric**, and later balance decisions use Product Owner judgment on that evidence. Rationale and consequences: `tasks/backlog/TASK-193-deterministic-balance-simulation-harness.md` §2. Still **blocks** B-03/B-04/B-12/B-13 from having *pass/fail* duration evidence, but no longer blocks them from producing observational evidence. | B-03, B-04, B-12, B-13 — unblocked for **observational** evidence; no numeric acceptance test |
| Q-2 | Is **Power Charge cost 0** retained? If not, which option (cost raise / per-Turn cast limit / both)? | ✅ **APPROVED — KEEP POWERCOST 0.** Retain authored PowerCost 0 per `CARD_RULES.md` §2. Per-turn cast limit (Q-4 / ADR-021) already prevents infinite accumulation. | B-01 — ✅ **RESOLVED** |
| Q-3 | Accept **flat boss stats 1–3** with mechanics-only differentiation (doc-aligned), or make HP/DEF curve monotonic? | ✅ **APPROVED — KEEP CURRENT BOSS 1–5 STATS.** Flat Boss 1–3 base stats (5000 HP, 100 ATK, 50 DEF) and mechanics-driven difficulty differentiation accepted for MVP. | B-03, B-04 — ✅ **RESOLVED** |
| Q-4 | Extend `RELIC_RULES.md` §5's anti-free-chain principle to **card casts** (i.e. every card cast consumes a Turn or is limited)? | ✅ **APPROVED — OPTION B: ONE CARD CAST PER TURN.** A player may successfully cast at most one Card per committed Match-3 Turn. A cast-count constraint, **not** Turn consumption — a cast still consumes no Turn, does not resolve the board, and does not independently trigger the boss response. Option A (cast consumes a Turn / triggers boss response) and Option C (keep unlimited free casts, change only Power Charge's cost) are **rejected**. Recorded as `ADR-021`. | B-02 — ✅ **UNBLOCKED** |
| Q-5 | Is **"no strictly dominated Card/Relic"** a required MVP property? | ✅ **APPROVED — KEEP CURRENT VALUES, BOTH 20 POWER.** Heal & Shield pairing retained at 20 Power cost as authored. | B-08, B-09, B-10 — ✅ **RESOLVED** |
| Q-6 | Pet Tier/Star/Level: **author the stat curve** or declare cosmetic for MVP? | ✅ **APPROVED — COSMETIC FOR MVP.** Tier/Star/Level remain cosmetic / progression signals with no combat stat scaling for MVP. | B-05 — ✅ **RESOLVED** |
| Q-7 | Player XP: keep 1-win-per-level to 50, curve it, or give Level combat meaning? | ✅ **APPROVED — KEEP CURRENT 1 WIN → 1 LEVEL.** Retain current linear 1 win = 1 level progression without combat stat scaling. | B-06 — ✅ **RESOLVED** |
| Q-8 | Mộc Yêu regen: reduce, raise threshold, or keep as intended attrition mechanic? | ✅ **APPROVED / IMPLEMENTED — OPTION (b): THRESHOLD 8 MATCHES.** Mộc Yêu regeneration threshold updated from every 5 player matches to every 8 player matches, retaining 5% MaxHP (250 HP) magnitude. Implemented in production by TASK-200. Magnitude variants (2%, 3%) rejected. | B-07 — ✅ **RESOLVED / IMPLEMENTED** |
| Q-9 | Card damage normalization: approve a cost-per-expected-damage tolerance band after simulation? | ✅ **APPROVED — KEEP CURRENT AUTHORED CARD VALUES.** Authored card damage, costs, and effects retained for MVP. | B-08 — ✅ **RESOLVED** |
| Q-10 | Relic engines: cap or reduce Arcane/Cascade Power grants; buff Berserker? | ✅ **APPROVED — KEEP CURRENT AUTHORED RELIC VALUES.** Authored relic values and triggers retained for MVP. | B-10 — ✅ **RESOLVED** |
| Q-11 | Approve pet-passive magnitudes for **Xích Lang** and **Sơn Hùng** (not authored anywhere)? | ✅ **APPROVED — REMAIN UNIMPLEMENTED FOR MVP.** Pet passives remain unimplemented for MVP per existing MVP scope. | B-14 — ✅ **RESOLVED** |
| Q-12 | Priority: is building the **simulation harness** (B-14 item 3) approved as a prerequisite task? | ✅ **APPROVED.** The deterministic balance simulation harness is approved as test-only tooling (no gameplay, production, API, frontend, or database change). Full specification, scope, determinism model, lifecycle, and acceptance criteria (H-01…H-10): `tasks/backlog/TASK-193-deterministic-balance-simulation-harness.md`. It is the validation backbone for B-03, B-04, B-07, B-08, B-10, and B-13 — it **measures**, and decides no balance value. | B-03, B-04, B-07, B-08, B-10, B-13 — ✅ **UNBLOCKED for measurement** (each still requires its own Product Owner decision) |

---

## 7. Scope Verification

```text
Gameplay code changed?              No
Database schema changed?            No
API contracts changed?              No
SignalR protocol changed?           No
Auth / session changed?             No
Collection Viewer changed?          No
Existing task records modified?     No
Files added/modified by this task:  tasks/backlog/TASK-191-balance-pass.md  (only)
```

---

## Stop Conditions

<!-- Universal stop conditions in AGENTS.md §20 always apply. -->

- If a proposed change conflicts with an authoritative document, STOP per
  `AGENTS.md` §4 — record the conflict, do not pick the convenient side.
- If implementing any approved item requires a rule the documents do not define,
  STOP per `AGENTS.md` §7 — the item returns to §6 Decisions Required.
- If an approved item drifts outside `MVP_SCOPE.md` §1, STOP per `AGENTS.md` §8.
- This task itself: if any section begins inventing numbers without labeling
  them candidates, STOP and mark `REQUIRES PRODUCT OWNER DECISION`.

---

## Completion Evidence

<!--
  TO BE COMPLETED BY THE EXECUTING AGENT after reaching DONE.
  Keep concise and factual.
-->

### Changed Files
- `tasks/backlog/TASK-191-balance-pass.md` — NEW: balance-pass specification
  (inventory, classifications, targets, proposed changes, acceptance criteria,
  test plan, decisions required).

### Validation Results
- Inventory rows re-verified against source during authoring (combo/element
  modifiers, resource rates, tier multipliers, card cast gates, boss stats/skills,
  pet defaults, XP curve, relic §8.5 magnitudes).
- Scope: this task created exactly one new file
  (`tasks/backlog/TASK-191-balance-pass.md`, untracked → new). The working tree
  already contained unrelated uncommitted changes from other tasks
  (TASK-187/189/190 and friends) before this task began; none were touched.

### Server Authority & Scope Verification
- [x] Zero gameplay code, schema, API, SignalR, auth, or UI changes
- [x] No invented numbers presented as approved values
- [x] Findings (D-1…D-5) reported, not fixed
- [x] Adherence to MVP Scope (`MVP_SCOPE.md` §1, §2) — no new systems proposed

### Note
TASK-191 is a specification. No balance value changes behavior until a human
approves the corresponding item in §6 and a follow-up implementation task lands
the approved change (docs first, per `AGENTS.md` §17).
