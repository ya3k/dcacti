# TASK-311 Engineering Artifact — ADR-029 Slice 5 Removal-Readiness Reconciliation

**Version:** 1.0  
**Status:** Binding Evidence  
**Task:** TASK-311 (ADR-029 Slice 5 Removal-Readiness Reconciliation)  
**Author:** Orchestrator / Team Lead  
**Governing ADR:** [ADR-029](../../docs/03-decisions/ADR/ADR-029-boss-as-player-opponent-boss-power-and-action-policy.md) (§2.9, §4 Slice 5, §7)

---

## 1. Executive Summary & Purpose

Under `ADR-029` §2.9 item 25, `SkillCharge` and the legacy `BossSkill` runtime handlers may only be removed after a four-part parity gate is satisfied:
1. **Gate (a):** All five MVP abilities migrated to Boss Cards and covered by tests. *(Satisfied: `earthquake` migrated in Slice 3 / TASK-308; `flame-burst`, `drain-power`, `root`, and `thunder-strike` migrated in Slice 4 / TASK-309; verified by TASK-310).*
2. **Gate (b):** Old and new behavior compared across all five Bosses, including the exact qualifying attack and the modifier-consumption timing of the trigger-based ability. *(Satisfied by Section 2 of this artifact).*
3. **Gate (c):** Every consumer reconciled — event presenters, wire tests, state readers, database definitions, Redis state, and documentation. *(Satisfied by Section 3 of this artifact and Scope B/C documentation amendments).*
4. **Gate (d):** The removal's ADR and ADR index amendments recorded. *(Satisfied by Section 4 of this artifact and Scope C amendments).*

This artifact establishes the complete evidence base required for ADR-029 §2.9(b) and §2.9(c), explicitly audits all runtime contracts and consumers, reconciles documentation contradictions, and verifies that the eventual Slice 5 removal task is reviewable and registrable. **This task implements no runtime removal, alters no gameplay values, and does not authorize Slice 5 execution.**

---

## 2. Scope A: Detailed Legacy-vs-Card Behavior Comparison (All 5 Bosses)

Every substantive comparison below is traced directly to source code, named tests, and authoritative documentation.

### 2.1 Sơn Thạch Vệ — `earthquake`

| Dimension | Legacy `BossSkill` Path | Canonical `BossCard` Path | Evidence & Classification |
| :--- | :--- | :--- | :--- |
| **Trigger / Selection Point** | Evaluated at Step 18c: fires if `SkillCharge >= 5` (`ChargeRequirement`) AND `SkillCooldown == 0`. Evaluated **after** the Boss's Step 18a board action has already executed. | Evaluated at Step 18a by `BossActionPolicy.Select`: available if `Power >= 30` AND `SkillCooldown == 0`. Selected if `CastPriorityScore` (16) strictly out-scores `boardMove.Score`. | `BattleStateService.cs` lines 1925–1950, 2297–2300; `BOSS_RULES.md` §4.3, §6.3. **Tested:** `TASK308AtomicRoundCardCastIntegrationTests.cs`. |
| **Action Replacement** | Does NOT replace board action. Step 18a executes a full board swap, match, and cascade cycle; Skill fires at Step 18c in the same round. | **Replaces board action.** If cast is selected, the Boss performs no swap, resolves no board cycle, and generates no board resources at Step 18a. | `BattleStateService.cs` lines 1950–1974; `BOSS_RULES.md` §4.3 item 4. **Tested:** `AssertCastPhaseSuppressesTheBossBoardAction`. |
| **Same-Round Boss Attack** | **Eligible.** If the Step 18a board action cleared ATK gems, Step 18d resolves a Boss attack against the Player in the same round. | **Suppressed.** Because no board action occurred, no confirmed ATK match exists; Step 18d attack is completely suppressed. | `BattleStateService.cs` lines 2623–2629; `GAME_RULES.md` §17 step 18d. **Tested:** `AssertDoesNotContain(events, DamageDealt/Taken)`. |
| **Cost & Resource** | Spends 5 `SkillCharge` matches (accumulated +1 per POWER match group from Boss board matches; clamped to 5). Resets `SkillCharge = 0`. | Spends 30 `BossState.Power` (accumulated +10 per uniquely matched POWER cell; 0–100). Validated against `EffectiveCardCost`. | `ResourceGenerator.cs` lines 270–285; `CardCastExecutor.cs` lines 70–95. **Tested:** `AtomicRoundOrchestrationTests.cs`, `TASK308BossCardWireTests.cs`. |
| **Cooldown** | Cooldown: 0 Turns. Sets `SkillCooldown = 0` on cast. Post-decrement value at step 3 is always 0, eligible every round. | Cooldown: 0 Turns. Sets `SkillCooldown = 0` on cast. Eligible every round provided Power is sufficient and cast out-scores board. | `BossDefinitions.cs` (`SonThachVe`); `BOSS_RULES.md` §6.3. **Tested:** `TASK308AtomicRoundCardCastIntegrationTests.cs`. |
| **Effect & Magnitude** | Grants temporary Shield to Boss: 150 Flat (`BossSkillSecondaryEffectKind.BossShield`). Trigger-based: `ExpiryCondition = "ShieldDepleted"`. | Grants temporary Shield to Boss: 150 Flat (`CardEffectType.Shield`, self, flat). Trigger-based: `ExpiryCondition = "ShieldDepleted"`. | `StatusEffectLifecycle.ApplyShield`; `COMBAT_RULES.md` §4. **Tested:** identical shield absorption pipeline. |
| **Application & Expiration** | Applied at Step 18c via `StatusEffectLifecycle.ApplyShield`. Absorbs incoming Player damage via `DamagePipeline`; removed when depleted. | Applied at Step 18c via `CardCastExecutor.Execute` -> `StatusEffectLifecycle.ApplyShield`. Same absorption pipeline; removed when depleted. | Identical lifecycle in `StatusEffectLifecycle.cs`. |
| **Event Ordering & Wire** | Emits `BossSkillCast(skillId: "earthquake", sourceId: "boss-son-thach-ve")` at Step 18c. | Emits `BossCardCast(cardId: "earthquake", sourceId: "boss-son-thach-ve")` at Step 18c. No `PowerChanged` emitted for Boss power spend. | `SIGNALR_PROTOCOL.md` §3.2.18 vs §3.2.30. **Tested:** `TASK308BossCardWireTests.cs`. |
| **Behavioral Divergence** | Intentional action policy divergence: legacy fires alongside a board action and can attack same round; Boss Card occupies the response phase and forgoes board action/attack. | |

---

### 2.2 Hỏa Long — `flame-burst`

| Dimension | Legacy `BossSkill` Path | Canonical `BossCard` Path | Evidence & Classification |
| :--- | :--- | :--- | :--- |
| **Trigger / Selection Point** | Evaluated at Step 18c: fires if `SkillCharge >= 5` AND `SkillCooldown == 0`, following Step 18a board action. | Evaluated at Step 18a by `BossActionPolicy.Select`: available if `Power >= 40` AND `SkillCooldown == 0`. Selected if `CastPriorityScore` (85) strictly out-scores `boardMove.Score`. | `BattleStateService.cs` lines 1944–1951, 2297–2300; `BOSS_RULES.md` §4.3, §6.3. **Tested:** `TASK309BossCardEffectRuntimeTests.Selection_*`. |
| **Action Replacement** | Does NOT replace board action. Step 18a executes board swap/matches; Skill fires at Step 18c. | **Replaces board action.** Board action and resource generation are suppressed in the cast round. | `BattleStateService.cs` lines 1950–1974; `BOSS_RULES.md` §4.3 item 4. **Tested:** `AssertCastPhaseSuppressesTheBossBoardAction`. |
| **Same-Round Boss Attack** | **Eligible.** If Step 18a board action had an ATK match, Step 18d executes an attack that **immediately benefits** from the +50% ATK buff applied at Step 18c. | **Suppressed.** Cast suppresses board action and attack. No attack occurs in the cast round. | `BattleStateService.cs` line 2628. **Tested:** `TASK309StatusEffectLifecycleTests.cs`. |
| **Cost & Cooldown** | Charge: 5 matches. Cooldown: 2 Turns. Resets `SkillCharge = 0`, sets `SkillCooldown = 2`. | Power Cost: 40. Cooldown: 2 Turns. Deducts 40 from `BossState.Power`, sets `SkillCooldown = 2`. | `BOSS_RULES.md` §6.3; `ADR-029` §3 U-2/U-5. **Tested:** `FlameBurst_ShouldSpend40...`. |
| **Effect Magnitude & Duration** | **Magnitude:** +50% Boss ATK (`AtkBuff`).<br>**Duration:** **2 Turns** (historical persisted value). | **Magnitude:** +50% Boss ATK (`CardEffectType.AtkBuff`).<br>**Duration:** **3 Turns** (approved re-authored value, TASK-309 B-2). | `BossDefinitions.cs`; `CardEffectDefinitions.cs`; `BOSS_RULES.md` §6.3.1 item 1. **Tested:** `FlameBurst_ShouldAffectAtMostTwoQualifyingBossAttacks...`. |
| **Duration Lifecycle & Attack Windows** | **2 Turns duration in legacy:**<br>- Turn 1 (cast): Applied at 18c. Step 18d attacks with +50% buff (**Attack #1**). Step 19a decrements duration 2 -> 1.<br>- Turn 2: Board action. Step 18d attacks with +50% buff (**Attack #2**). Step 19a decrements 1 -> 0 (expires).<br>*(Result: up to 2 attack opportunities).* | **3 Turns duration on Boss Card:**<br>- Turn 1 (cast): Applied at 18c. Step 18d suppressed (no attack). Step 19a decrements duration 3 -> 2.<br>- Turn 2: Board action. Step 18d attacks with +50% buff (**Attack #1**). Step 19a decrements 2 -> 1.<br>- Turn 3: Board action. Step 18d attacks with +50% buff (**Attack #2**). Step 19a decrements 1 -> 0 (expires).<br>*(Result: up to 2 attack opportunities).* | Explicit intentional divergence. If Boss Card had kept duration 2, Turn 1 suppresses attack and decrements 2 -> 1, leaving only Turn 2 (at most 1 attack opportunity). Duration 3 preserves the effective 2-attack window without guaranteeing two attacks. |
| **Stacking Interaction** | Compounds multiplicatively with Rage (+20%): `1.20 × 1.50 = 1.80` (ATK 100 becomes 180). | Compounds multiplicatively with Rage (+20%): `1.20 × 1.50 = 1.80` (TASK-309 B-4). Additive stacking is NOT authorized. | `COMBAT_RULES.md` §5.5.1; `TASK309StatusEffectLifecycleTests.cs` lines 79–84. **Tested.** |
| **Event Ordering & Wire** | Emits `BossSkillCast(skillId: "flame-burst", sourceId: "boss-hoa-long")` at Step 18c. | Emits `BossCardCast(cardId: "flame-burst", sourceId: "boss-hoa-long")` at Step 18c. | `SIGNALR_PROTOCOL.md` §3.2.18 vs §3.2.30. **Tested:** `TASK309BossCardWireTests.cs`. |

---

### 2.3 Thủy Ma — `drain-power`

| Dimension | Legacy `BossSkill` Path | Canonical `BossCard` Path | Evidence & Classification |
| :--- | :--- | :--- | :--- |
| **Trigger / Selection Point** | Evaluated at Step 18c: fires if `SkillCharge >= 4` AND `SkillCooldown == 0`, following Step 18a board action. | Evaluated at Step 18a by `BossActionPolicy.Select`: available if `Power >= 25` AND `SkillCooldown == 0`. Selected if `CastPriorityScore` (85) strictly out-scores `boardMove.Score`. | `BattleStateService.cs` lines 1944–1951, 2297–2300; `BOSS_RULES.md` §4.3, §6.3. |
| **Action Replacement** | Does NOT replace board action. Step 18a executes board action; Skill fires at Step 18c. | **Replaces board action.** Board action and resource generation are suppressed in the cast round. | `BattleStateService.cs` lines 1950–1974. **Tested:** `AssertCastPhaseSuppressesTheBossBoardAction`. |
| **Same-Round Boss Attack** | **Eligible.** If Step 18a board action cleared ATK gems, Step 18d resolves a Boss attack against the Player. | **Suppressed.** Cast suppresses board action and attack. | `BattleStateService.cs` line 2628. **Tested.** |
| **Cost & Cooldown** | Charge: 4 matches. Cooldown: 3 Turns. Resets `SkillCharge = 0`, sets `SkillCooldown = 3`. | Power Cost: 25. Cooldown: 3 Turns. Deducts 25 from `BossState.Power`, sets `SkillCooldown = 3`. | `BOSS_RULES.md` §6.3; `ADR-029` §3 U-2/U-5. **Tested:** `DrainPower_ShouldSpend25...`. |
| **Effect & Transfer** | Instantly subtracts 20 flat Power from active Pet: `max(0, Pet.Power - 20)`. **No Power transferred to Boss.** | Instantly subtracts 20 flat Power from active Pet: `max(0, Pet.Power - 20)`. **No Power transferred to Boss.** | `GAME_RULES.md` §12; `BOSS_RULES.md` §6.3.1 item 2. **Tested:** `Assert.Equal(created.BossState.Power - 25, committed.BossState.Power)`. |
| **Duration & State Model** | Instant mutation on `PetState.Power`. Creates NO `StatusEffect` instance on Pet or Boss. | Instant mutation on `PetState.Power` (`CardEffectType.PowerDrain`). Creates NO `StatusEffect` instance on Pet or Boss. | `GAME_STATE.md` §2.3.1 item 9. **Tested:** `Assert.DoesNotContain(ActiveStatusEffects)`. |
| **Event Ordering & Wire** | Emits `BossSkillCast(skillId: "drain-power", sourceId: "boss-thuy-ma")` at 18c. Emits `PowerChanged(source: Boss, delta: -clamped, power: newPower)` after `BossSkillCast`. | Emits `PowerChanged(source: Boss, delta: -clamped, power: newPower)` **BEFORE** `BossCardCast(cardId: "drain-power", sourceId: "boss-thuy-ma")` in the round's single batch. | `BattleStateService.cs` lines 2562–2591 (TASK-309 W5 / D-1). **Tested:** `DrainPower_ShouldOrderPowerChangedBeforeBossCardCast`. |
| **Zero-Mutation Behavior** | If Pet Power is 0, clamped delta is 0; emits `PowerChanged` with delta 0 or skips per contract. | If Pet Power is 0, clamped delta is 0; emits **NO** `PowerChanged` event. `BossCardCast` still emits. | `TASK309BossCardEffectRuntimeTests.cs` lines 334–353. **Tested.** |

---

### 2.4 Mộc Yêu — `root`

| Dimension | Legacy `BossSkill` Path | Canonical `BossCard` Path | Evidence & Classification |
| :--- | :--- | :--- | :--- |
| **Trigger / Selection Point** | Evaluated at Step 18c: fires if `SkillCharge >= 6` AND `SkillCooldown == 0`, following Step 18a board action. | Evaluated at Step 18a by `BossActionPolicy.Select`: available if `Power >= 35` AND `SkillCooldown == 0`. Selected if `CastPriorityScore` (85) strictly out-scores `boardMove.Score`. | `BattleStateService.cs` lines 1944–1951, 2297–2300; `BOSS_RULES.md` §4.3, §6.3. |
| **Action Replacement** | Does NOT replace board action. Step 18a executes board action; Skill fires at Step 18c. | **Replaces board action.** Board action and resource generation are suppressed in the cast round. | `BattleStateService.cs` lines 1950–1974. **Tested.** |
| **Same-Round Boss Attack** | **Eligible.** If Step 18a board action cleared ATK gems, Step 18d resolves a Boss attack against the Player. | **Suppressed.** Cast suppresses board action and attack. | `BattleStateService.cs` line 2628. **Tested.** |
| **Cost & Cooldown** | Charge: 6 matches. Cooldown: 2 Turns. Resets `SkillCharge = 0`, sets `SkillCooldown = 2`. | Power Cost: 35. Cooldown: 2 Turns. Deducts 35 from `BossState.Power`, sets `SkillCooldown = 2`. | `BOSS_RULES.md` §6.3; `ADR-029` §3 U-2/U-5. **Tested:** `Root_ShouldSpend35...`. |
| **Effect Magnitude & Target** | Applies −30% active Pet ATK debuff (`AtkDebuff`) to `PetState.ActiveStatusEffects`. TargetStat: `"ATK"`. | Applies −30% active Pet ATK debuff (`CardEffectType.AtkDebuff`) to `PetState.ActiveStatusEffects`. TargetStat: `"ATK"`. | `BOSS_RULES.md` §6.3.1 item 3; `COMBAT_RULES.md` §5.1, §5.4.1. **Tested.** |
| **Duration & Consumption Timing** | **Duration: 2 Turns** (historical persisted value).<br>- Applied at Step 18c (after Player's Turn 1 attack).<br>- Step 19a decrements 2 -> 1.<br>- Turn 2: Player attacks under debuff. Step 19a decrements 1 -> 0 (expires).<br>*(Result: covers at most 1 Player attack).* | **Duration: 3 Turns** (approved re-authored value, TASK-309 B-3).<br>- Applied at Step 18c (after Player's Turn 1 attack).<br>- Step 19a decrements 3 -> 2.<br>- Turn 2: Player attacks under debuff. Step 19a decrements 2 -> 1.<br>- Turn 3: Player attacks under debuff. Step 19a decrements 1 -> 0 (expires).<br>*(Result: covers at most 2 Player attacks).* | Explicit intentional divergence. Because Step 18c executes after the Player's turn attack (Step 15), Step 19a ticks down on the cast turn without affecting an attack. Duration 3 preserves the intended 2-attack defensive window without guaranteeing two attacks. |
| **Event Ordering & Wire** | Emits `BossSkillCast(skillId: "root", sourceId: "boss-moc-yeu")` at Step 18c. | Emits `BossCardCast(cardId: "root", sourceId: "boss-moc-yeu")` at Step 18c. | `SIGNALR_PROTOCOL.md` §3.2.18 vs §3.2.30. **Tested:** `TASK309BossCardWireTests.cs`. |

---

### 2.5 Kim Lôi Vương — `thunder-strike`

| Dimension | Legacy `BossSkill` Path | Canonical `BossCard` Path | Evidence & Classification |
| :--- | :--- | :--- | :--- |
| **Trigger / Selection Point** | Evaluated at Step 18c: fires if `SkillCharge >= 5` AND `SkillCooldown == 0`, following Step 18a board action. | Evaluated at Step 18a by `BossActionPolicy.Select`: available if `Power >= 40` AND `SkillCooldown == 0`. Selected if `CastPriorityScore` (85) strictly out-scores `boardMove.Score`. | `BattleStateService.cs` lines 1944–1951, 2297–2300; `BOSS_RULES.md` §4.3, §6.3. |
| **Action Replacement** | Does NOT replace board action. Step 18a executes board action; Skill fires at Step 18c. | **Replaces board action.** Board action and resource generation are suppressed in the cast round. | `BattleStateService.cs` lines 1950–1974. **Tested.** |
| **Cost & Cooldown** | Charge: 5 matches. Cooldown: 0 Turns. Resets `SkillCharge = 0`, sets `SkillCooldown = 0`. | Power Cost: 40. Cooldown: 0 Turns. Deducts 40 from `BossState.Power`, sets `SkillCooldown = 0`. | `BOSS_RULES.md` §6.3; `ADR-029` §3 U-2/U-5. **Tested:** `ThunderStrike_ShouldSpend40...`. |
| **Effect & Expiry Condition** | Buffs next qualifying Boss ATK match by +50% (`NextAtkBuff`). Trigger-based: `ExpiryCondition = "NextQualifyingAttack"`, no turn countdown (`RemainingTurns = null`). | Buffs next qualifying Boss ATK match by +50% (`CardEffectType.NextAtkBuff`, scope `NextAttack`). Trigger-based: `ExpiryCondition = "NextQualifyingAttack"`, no turn countdown (`RemainingTurns = null`). | `StatusEffect.NextQualifyingAttackCondition`; `BOSS_RULES.md` §6.3.1 item 5; `COMBAT_RULES.md` §3.3 item 8. **Tested.** |
| **Modifier Consumption Timing (Crucial Divergence)** | **CAN be consumed in the SAME round as the cast.**<br>Step 18a resolves board action. If it matched ATK gems (`bossQualified == true`), Step 18c fires and applies `NextAtkBuff`. Then Step 18d resolves `AttackResolution`, computes `EffectiveBossATK = 100 × 1.50 = 150`, and **immediately consumes `NextAtkBuff` in the very same round!** | **CANNOT be consumed in the same round as the cast.**<br>Step 18a selects cast. Step 18c installs `NextAtkBuff`. Step 18d attack is **suppressed**. The modifier **must survive** into a subsequent round where a board action is selected and clears ATK gems! | `AtomicRoundOrchestrationTests.cs` lines 641–646 (`if (bossQualified) Assert.False(hasBuff)`) vs `TASK309BossCardEffectRuntimeTests.cs` lines 423–427 (`Assert.Single(castRound.ActiveStatusEffects)`). **Directly tested.** |
| **Survival Across Non-Qualifying Rounds** | Survives every subsequent round in which the Boss matches zero ATK gems; consumed on the first round that produces a qualifying ATK match. | Survives every subsequent round in which the Boss matches zero ATK gems; consumed on the first round that produces a qualifying ATK match. | `TASK309BossCardEffectRuntimeTests.cs` lines 463–529 (TASK-310 test). **Tested.** |
| **Event Ordering & Wire** | Emits `BossSkillCast(skillId: "thunder-strike", sourceId: "boss-kim-loi-vuong")` at Step 18c. Followed by `DamageCalculated`/`DamageDealt` at Step 18d if ATK matched. | Emits `BossCardCast(cardId: "thunder-strike", sourceId: "boss-kim-loi-vuong")` at Step 18c. No damage events in the cast round. | `SIGNALR_PROTOCOL.md` §3.2.18 vs §3.2.30. **Tested:** `TASK309BossCardWireTests.cs`. |

---

## 3. Scope C: Inventory of Affected Consumers for Future Slice 5 Removal

The eventual Slice 5 task must remove `SkillCharge`, the legacy `BossSkill` execution path, and the legacy `BossSkillCast` event. Below is the authoritative, exhaustive inventory of every repository component and contract affected:

### 3.1 Backend Domain Layer

1. **`BossState.cs` (`GameServer.Domain.Battle`):**
   - Member: `SkillCharge` (int positional record member).
   - Action for Slice 5: Remove `SkillCharge` from positional constructor, `ToString()`, and equality/hashcode.
   - Retain: `SkillCooldown` (int) — retained unchanged per ADR-029 §2.9.
2. **`BossDefinition.cs` / `BossDefinitions.cs` (`GameServer.Domain.Bosses`):**
   - Members: `SkillChargeRequirement` property; `SkillDefinition` structure.
   - Action for Slice 5: Retire `SkillChargeRequirement` from `BossDefinition` and domain baselines.
   - Retain: `SkillId` (reused as `CardDefinitionId` per ADR-029 §2.5 item 14), `Element`, `PassiveDefinition`, `CastPriorityScore`.
3. **`BattleEvent.cs` (`GameServer.Domain.Match3`):**
   - Discriminator: `BattleEventType.BossSkillCast = 9`.
   - Types: `BossSkillCastEvent` struct, `BattleEvent.ForBossSkillCast` factory method, `BossSkillCast` property.
   - Action for Slice 5: Remove discriminator 9, struct, factory, and property.
4. **`ResourceGenerator.cs` (`GameServer.Domain.Match3`):**
   - Method: `ApplyBossSkillCharge` (+1 per match group).
   - Action for Slice 5: Delete `ApplyBossSkillCharge`. The Boss branch of `ApplyActorMatchEffects` will solely call `ApplyPower` (+10 per uniquely matched cell clamped to [0, 100]).
5. **`BossSkillDefinition.cs` (`GameServer.Domain.Bosses`):**
   - Types: `BossSkillSecondaryEffect`, `BossSkillSecondaryEffectKind`, `ChargeRequirement`.
   - Action for Slice 5: Remove secondary effect declarations and legacy charge requirement. All abilities reside in `CardEffectDefinition` / `CardDefinition`.

### 3.2 Backend Application Layer

1. **`BattleStateService.cs` (`GameServer.Application.Battle`):**
   - Board generation: Remove call to `ApplyBossSkillCharge` in Step 18a resolution.
   - Legacy Execution Branch: Remove lines 2280–2510 (`var skillFires = bossCard is null && ...`).
   - Active form resolution: Once `BossSkill` fallback is removed, every Boss requires a valid provisioned `BossCard`; remove `bossCard is null` fallback branch.
   - Event emission: Remove `BattleEvent.ForBossSkillCast`.
   - Active state mutation: Remove `SkillCharge = 0` from write-back.

### 3.3 Backend Infrastructure Layer & Persistence

1. **`BossDefinitionConfiguration.cs` (`GameServer.Infrastructure.Postgres.Configurations`):**
   - Entity configuration for JSONB `SkillDefinition` (`chargeRequirement`, `baseDamage`).
   - Disposition: Whether `chargeRequirement` is physically migrated out of PostgreSQL JSONB or retained as an ignored property is an operator decision for Slice 5.
2. **EF Core Migrations:**
   - Existing historical migrations (`20260926151112_ProvisionBossDefinitions`, `20261004094100_ProvisionTwoRemainingMvpBosses`) remain immutable in history (P-6). No back-editing of migrations.
3. **`InMemoryBattleStateRepository.cs` / Redis Deserialization:**
   - `REDIS_STATE.md` §2 Decision B-2 establishes tolerant-reader deserialization using `System.Text.Json`. When `SkillCharge` is deleted from C# `BossState`, existing in-flight Redis records with `"SkillCharge"` deserialize safely without error.
   - Deployment requirement: In-flight active battles should be drained prior to joint deployment per `REDIS_STATE.md` §2 item 4 to maintain clean state.

### 3.4 Backend Api Layer & Wire Protocol

1. **`BattleEventWireProjection.cs` (`GameServer.Api.Hubs`):**
   - Mapping: `BattleEventType.BossSkillCast => ProjectBossSkillCast(...)`.
   - DTO factory: `BattleEventWireDto.BossSkillCast(...)`.
   - Action for Slice 5: Remove projection and wire factory.
2. **`SIGNALR_PROTOCOL.md`:**
   - Discriminator: Remove `"BossSkillCast"` from §3.2.2 canonical event list.
   - Section: Retire §3.2.18 (`BossSkillCast` schema).
   - Retain: `BossCardCast` (§3.2.30) and `bossState.power` (§4.4 item 12).

### 3.5 Frontend Client Layer

1. **`BattleScene.ts` (`src/frontend/client/src/game/scenes/`):**
   - Callout handling: lines 5416, 5639, 5765, 5781 (`case 'BossSkillCast': presentBossSkillCast`).
   - Action for Slice 5: Remove `'BossSkillCast'` handling; retain `'BossCardCast'`.
2. **`BattleEventPresenter.ts` (`src/frontend/client/src/game/scenes/`):**
   - Interface: `PresentedBossSkillCast` (lines 139–143).
   - Parser: `parseBossSkillCast` (lines 821–830).
   - Formatter: `formatInBattleEvent` case `'BossSkillCast'` (lines 961–962).
   - Action for Slice 5: Remove interface, parser, and format case.
3. **Frontend Vitest Suites:**
   - `GameRuntime.test.ts`, `SceneLifecycle.test.ts`, `RuntimeBoundaries.test.ts`, `BattleEventPresentation.test.ts`.
   - Action for Slice 5: Audit and migrate legacy `BossSkillCast` test assertions to `BossCardCast`.

### 3.6 Balance Analysis Fixtures & Existing Tests

1. **`AtomicRoundOrchestrationTests.cs`:**
   - Lines 508, 527, 609, 635, 718, 756, 1179, 1285 assert `BossSkillCast` and `SkillCharge`.
   - Action for Slice 5: Retire or convert legacy tests to `BossCardCast` tests.
2. **Historical Infrastructure Tests (`BossPersistenceTests.cs`, `BossDefinitionBalanceLookupTests.cs`):**
   - Reconcile test expectations with the retirement of `SecondaryEffect`.

---

## 4. Scope D: Classification of Deferred Issues

| Deferred Issue | Blocker for ADR-029? | Blocker for Slice 5? | Evidence & Resolution Path |
| :--- | :--- | :--- | :--- |
| **4 Infrastructure test failures** (`BossPersistenceTests`, `BossDefinitionPostgresProvisioningTests`, `BossDefinitionBalanceLookupTests`) | **NO.** Does not block ADR-029 Slices 1–4. | **YES (for Slice 5).** Must be resolved during Slice 5 implementation. | Assert historical `BaseDamage` (150/180 vs 0) and legacy `SecondaryEffect` (Burn vs AtkBuff). When Slice 5 retires `SecondaryEffect`, these test assertions must be updated or retired. |
| **2 Application balance-evidence test failures** (`BalanceEvidenceMeasurementCorrectionTests`, `TASK296_ReviewD_Canonical75RunCandidate`) | **NO.** | **NO.** | Historical balance telemetry from TASK-195 and TASK-296. Completely independent of Boss Power and Boss Cards. |
| **P-8 Signature-Skill category guard** | **NO.** | **NO.** | Advisory category guard noted in TASK-309. Domain `CardCategory.BossCard` already enforces server-side separation. |
| **Stale `.ai/PROJECT_STATE.md` navigation cache** | **NO.** | **NO.** | AI navigation cache only. Updated during repository lifecycle state synchronization in TASK-311. |
| **Gate terminology inconsistencies & retired table** | **NO.** | **NO.** | Formally reconciled and closed by TASK-311 (Scope B). |

---

## 5. Unresolved Operator Decisions (Carried to Slice 5)

1. **OD-311-1 (PostgreSQL JSON Storage Disposition):**  
   Does Slice 5 physically remove `"chargeRequirement"` and `"baseDamage"` from the `BossDefinition.SkillDefinition` JSONB column via an EF Core data migration, or retain them as ignored/deprecated JSON fields to avoid table rewrites?  
   *Classification:* Deferred to Slice 5 task registration.
2. **OD-311-2 (Wire Event Deprecation Window):**  
   Does Slice 5 remove `BossSkillCast` from `SIGNALR_PROTOCOL.md` and `BattleEventWireProjection.cs` in a single breaking release (requiring coordinated client deployment with battle drain), or retain a temporary tolerant-reader no-op mapping?  
   *Classification:* Deferred to Slice 5 task registration.

---

## 6. Readiness Verdict

**Verdict:** `READY FOR SLICE 5 REGISTRATION REVIEW`

- Gate (a): Satisfied (Slices 1–4 delivered and verified).
- Gate (b): Satisfied (Detailed 5-Boss comparison recorded in Section 2).
- Gate (c): Satisfied (Consumer inventory and documentation reconciled in Section 3 and Scope B/C).
- Gate (d): Satisfied (ADR-029 and ADR index amendments prepared).

*Notice:* This readiness verdict confirms that all prerequisite documentation, comparisons, and consumer audits required by ADR-029 §2.9 are complete. **Slice 5 remains unauthorized and unimplemented. The Fifth Gem remains FUTURE.**
