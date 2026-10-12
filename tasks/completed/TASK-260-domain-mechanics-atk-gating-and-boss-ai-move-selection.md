# TASK-260 — Domain Mechanics, ATK Gating & Boss AI Move Selection

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
-->

---

## Metadata

```text
Task ID:           TASK-260
Type:              GAMEPLAY-CHANGE
Status:            DONE
Risk:              HIGH
Priority:          HIGH
Primary Agent:     gameplay
Supporting Agents: backend, testing, review
Workflow:          development/gameplay-change.md
Skills:            discovery/documentation-discovery, discovery/impact-analysis,
                   quality/documentation-consistency, gameplay/authority-determinism-audit,
                   testing/test-scenario-generation
Dependencies:      TASK-258, TASK-259
Declared Files:    src/backend/GameServer.Domain/Combat/AttackResolution.cs,
                   src/backend/GameServer.Domain/Combat/HealResolution.cs,
                   src/backend/GameServer.Domain/Match3/ResourceGenerator.cs,
                   src/backend/GameServer.Domain/Battle/StatusEffect.cs,
                   src/backend/GameServer.Domain/Battle/StatusEffectLifecycle.cs,
                   src/backend/GameServer.Domain/Bosses/BossDefinition.cs,
                   src/backend/GameServer.Domain/Bosses/BossDefinitions.cs,
                   src/backend/GameServer.Domain/Bosses/BossAiEngine.cs,
                   src/backend/GameServer.Domain/Match3/SwapExecution.cs,
                   tests/backend/GameServer.Domain.Tests/AttackResolutionTests.cs,
                   tests/backend/GameServer.Domain.Tests/ActorRelativeMatchEffectsTests.cs,
                   tests/backend/GameServer.Domain.Tests/BossSkillContractTests.cs,
                   tests/backend/GameServer.Domain.Tests/BossAiEngineTests.cs,
                   tests/backend/GameServer.Domain.Tests/BossStateTests.cs,
                   tests/backend/GameServer.Domain.Tests/Task172BossDefinitionsContentTests.cs,
                   tests/backend/GameServer.Domain.Tests/EffectiveBossAttackTests.cs,
                   tests/backend/GameServer.Domain.Tests/DamagePipelineTests.cs
```

---

## Objective

Implement the Domain layer of the approved shared-board gameplay design for `ya3k/dcacti`.
This task implements actor-relative match effects, ATK-gated damage, non-damaging Boss skill mechanics, deterministic Boss move selection, and deadlock recovery.
The approved design is defined by TASK-258, TASK-259, ADR-026, and Product Owner decisions T1–T3 and D1–D7.

---

## Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1 — Match-3, Bosses, and Combat scope
- `docs/01-game-design/GAME_RULES.md` §1.3, §2, §14, §15, §17 — Core rules and resolution sequence
- `docs/01-game-design/MATCH3_RULES.md` §1.4, §2, §8 — Swap rules and board constraints
- `docs/01-game-design/COMBAT_RULES.md` §2, §3, §4, §5 — Resource generation, damage pipeline, status effects
- `docs/01-game-design/BOSS_RULES.md` §3, §4, §6, §6.3, §6.3.1 — Boss mechanics and skill definitions
- `docs/02-technical/GAME_STATE.md` §2.1, §2.3.1, §2.4, §5 — State definitions and lifecycle
- `docs/03-decisions/ADR/ADR-026-shared-board-gameplay-and-actor-relative-match-resolution.md` — Authoritative ADR
- Binding PO Decisions: T1 (SkillCharge unit), T2 (overflow clamp), T3 (Flame Burst/Rage stacking), D1 (ATK gating), D2 (Boss resource), D3 (Boss skills non-damaging), D5 (Flame Burst ATK buff), D6 (Earthquake Shield), D7 (Thunder Strike Next-ATK buff).

---

## Scope

### In Scope
- **Scope A — ATK-Gated Damage:**
  - Call `DamagePipeline.Calculate` only upon confirmed ATK match (`ClearedGemCounts.Atk > 0`).
  - Deal zero damage and skip attack when `ClearedGemCounts.Atk == 0`; retire fixed fallback basic attack.
  - Preserve `NextAttack` and trigger modifiers when no qualifying ATK match occurs.
  - Consume modifiers upon qualifying ATK match even if mitigation reduces final damage to zero.
  - Apply identical gating rules to Player and Boss attacks.
- **Scope B — Actor-Relative DEF and HP Effects:**
  - DEF match grants temporary Shield to acting actor (`PetState` for Player; `BossState` for Boss) using `StatusEffectType.Shield`.
  - HP match heals acting actor up to `MaxHP`, clamping overheal safely.
- **Scope C — Boss Skill Mechanics:**
  - Set direct `BaseDamage = 0` on all five MVP skills.
  - Hỏa Long `flame-burst`: temporary Boss ATK buff, zero direct damage, Burn DoT removed. Multiplicative stacking with Rage preserved in `EffectiveBossAttack`.
  - Thủy Ma `drain-power`: -20 flat Pet Power, zero direct damage.
  - Mộc Yêu `root`: -30% Pet ATK for 2 Turns, zero direct damage.
  - Sơn Thạch Vệ `earthquake`: temporary Boss Shield, zero direct damage.
  - Kim Lôi Vương `thunder-strike`: next-qualifying-ATK modifier (`ExpiryCondition = "NextQualifyingAttack"`), zero direct damage, preserved on non-ATK turns.
- **Scope D — Deterministic Boss AI:**
  - Enumerate 112 orthogonal candidate swaps on 8×8 board.
  - Evaluate candidates deterministically using explicit scoring weights grounded in combat rules.
  - Deterministic tie-breaker: lowest `From` cell index, then lowest `To` cell index.
  - Deadlock detection: detect absence of legal valid swaps.
  - Deterministic deadlock recovery: reshuffle board using `Pcg32` stream, verify validity, bounded to 64 attempts.
- **Scope E — SkillCharge Integration Boundary:**
  - +1 SkillCharge per valid Boss POWER match group, clamped to `ChargeRequirement`.
  - Player POWER matches never increase Boss SkillCharge.

### Out of Scope
- Atomic Player-plus-Boss round orchestration in Application layer (`BattleStateService.ResolveSwapAsync` refactoring belongs to TASK-261).
- SignalR `SwapStarted` wire event protocol (belongs to TASK-263).
- Phaser presentation animations (belongs to TASK-264).
- Headless balance simulations (belongs to TASK-265).
- Schema migrations or unapproved numeric changes.

---

## Declared File Set (P-2 / P-5)

Exact declared implementation files for TASK-260:
```text
src/backend/GameServer.Domain/Combat/AttackResolution.cs
src/backend/GameServer.Domain/Combat/HealResolution.cs
src/backend/GameServer.Domain/Match3/ResourceGenerator.cs
src/backend/GameServer.Domain/Battle/StatusEffect.cs
src/backend/GameServer.Domain/Battle/StatusEffectLifecycle.cs
src/backend/GameServer.Domain/Bosses/BossDefinition.cs
src/backend/GameServer.Domain/Bosses/BossDefinitions.cs
src/backend/GameServer.Domain/Bosses/BossAiEngine.cs
src/backend/GameServer.Domain/Match3/SwapExecution.cs
tests/backend/GameServer.Domain.Tests/AttackResolutionTests.cs
tests/backend/GameServer.Domain.Tests/ActorRelativeMatchEffectsTests.cs
tests/backend/GameServer.Domain.Tests/BossSkillContractTests.cs
tests/backend/GameServer.Domain.Tests/BossAiEngineTests.cs
tests/backend/GameServer.Domain.Tests/BossStateTests.cs
tests/backend/GameServer.Domain.Tests/Task172BossDefinitionsContentTests.cs
tests/backend/GameServer.Domain.Tests/EffectiveBossAttackTests.cs
tests/backend/GameServer.Domain.Tests/DamagePipelineTests.cs
```

---

## Acceptance Criteria

- [x] Attack damage is strictly gated on `ClearedGemCounts.Atk > 0` for both Player and Boss.
- [x] No damage and no contradictory damage events are emitted when 0 ATK gems are cleared; `NextAttack` and `NextQualifyingAttack` modifiers are preserved.
- [x] Qualifying attacks consume applicable modifiers even if final damage is mitigated to zero.
- [x] DEF matches grant temporary Shield to acting actor (`PetState` or `BossState`).
- [x] HP matches heal acting actor up to `MaxHP`, clamping overheal safely.
- [x] All five Boss skills have `BaseDamage = 0` and secondary effects conform to D3, D5, D6, D7.
- [x] Boss SkillCharge increments by +1 per Boss POWER match group and clamps to `ChargeRequirement`; Player POWER matches never charge the Boss.
- [x] Deterministic Boss AI enumerates 112 candidates, scores deterministically, breaks ties by lowest cell index, detects deadlock, and reshuffles deterministically using `Pcg32` within 64 bounded attempts.
- [x] All Domain regression tests and new focused tests pass.
- [x] Zero changes made outside authorized Domain scope.

---

## Completion Evidence

### Commit
- Implementation slice (Phase A): `7cd07821eb613e26be39d0f19d3ef5be70ab37dc` (Joint Group Commit with TASK-261)
- Subject: `feat(combat): implement domain mechanics, atk gating, and atomic round orchestration`
- Owns: `TASK-260`, `TASK-261`
- Files: the declared file set above
- Shared with: `TASK-261` — `src/backend/GameServer.Domain/Battle/StatusEffectLifecycle.cs`
- Unowned / pre-existing: `none`
- Completion record filing (Phase B): this completion record alone, filed under its own bookkeeping commit (`tasks/completed/TASK-260-domain-mechanics-atk-gating-and-boss-ai-move-selection.md`).

### Changed Files
- `src/backend/GameServer.Domain/Combat/AttackResolution.cs` — ATK-gated Player and Boss attack resolution.
- `src/backend/GameServer.Domain/Combat/HealResolution.cs` — Actor-relative HP healing.
- `src/backend/GameServer.Domain/Match3/ResourceGenerator.cs` — Actor-relative gem match effects for ATK, DEF, HP, POWER.
- `src/backend/GameServer.Domain/Battle/StatusEffect.cs` — Trigger-based NextQualifyingAttack modifier support.
- `src/backend/GameServer.Domain/Battle/StatusEffectLifecycle.cs` — Damage shield absorption and next-qualifying modifier consumption.
- `src/backend/GameServer.Domain/Bosses/BossDefinition.cs` / `BossDefinitions.cs` — Five MVP Boss skills with BaseDamage = 0 and secondary effects.
- `src/backend/GameServer.Domain/Bosses/BossAiEngine.cs` — Deterministic Boss move selection and deadlock reshuffle.
- `src/backend/GameServer.Domain/Match3/SwapExecution.cs` — Board swap execution.
- Domain unit test suites covering the changes (`AttackResolutionTests.cs`, `ActorRelativeMatchEffectsTests.cs`, `BossSkillContractTests.cs`, `BossAiEngineTests.cs`, `BossStateTests.cs`, `Task172BossDefinitionsContentTests.cs`, `EffectiveBossAttackTests.cs`, `DamagePipelineTests.cs`).

### Validation Results
- `dotnet build src/backend/GameServer.sln` — PASS (0 errors)
- `dotnet test tests/backend/GameServer.Domain.Tests/GameServer.Domain.Tests.csproj` — PASS (1605 passed, 0 failed, 0 skipped)

