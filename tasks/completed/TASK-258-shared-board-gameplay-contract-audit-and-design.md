# TASK-258 — Shared-Board Gameplay Contract Audit & Design Review

<!--
  GEN-TASK EXECUTION MANIFEST / DESIGN REVIEW
  Principle: TASK = EXECUTION MANIFEST & DESIGN CONTRACT AUDIT.
  This is strictly a review and design-specification artifact. No gameplay code has been implemented.
-->

---

## Metadata

```text
Task ID:           TASK-258
Type:              GAMEPLAY-CHANGE
Status:            DONE
Risk:              HIGH
Priority:          HIGH
Primary Agent:     gameplay
Supporting Agents: backend, client, testing, review
Workflow:          development/gameplay-change.md
Skills:            discovery/documentation-discovery, discovery/impact-analysis,
                   quality/documentation-consistency, gameplay/authority-determinism-audit,
                   testing/test-scenario-generation
Dependencies:      None
Declared Files:    tasks/active/TASK-258-shared-board-gameplay-contract-audit-and-design.md
```

---

## Objective

Establish an authoritative, repository-grounded gameplay and technical contract for the shared-board battle system in `ya3k/dcacti` across Domain, Application, Realtime Protocol, and Client Presentation layers. Incorporate Product Owner-approved structural decisions D1–D7 (including the final selection of Option 5B: Boss ATK buff for Hỏa Long), reconcile contract conflicts and semantic ambiguities concerning turns, sequences, `SkillCharge`, and persistence/event boundaries, and define a dependency-ordered downstream task roadmap without modifying gameplay code.

---

## 1. Repository Baseline & Environment Verification

Verified directly against the active checkout:
- **Repository Branch:** `master`
- **HEAD Commit:** `3f29612` (`feat: add CLAUDE.md orientation file for AI agent project guidance`)
- **Working Tree State:** Clean prior to this review artifact update (verified via `git status`).
- **Task Registry Baseline:**
  - Highest completed task: `TASK-257` (`tasks/completed/TASK-257-fix-swap-action-identity-preconditions-and-concurrency-coverage.md`).
  - Active task: `TASK-258` (this review artifact).
  - Downstream task candidates: `TASK-259` through `TASK-265` are verified absent from `tasks/backlog/`, `tasks/active/`, `tasks/blocked/`, and `tasks/completed/`.
- **ADR Baseline:**
  - Highest accepted ADR: `ADR-025` (`docs/03-decisions/ADR/ADR-025-swap-action-preconditions-and-idempotency.md`).
  - Next sequential ADR: **`ADR-026`** (verified absent from `docs/03-decisions/ADR/` and `docs/03-decisions/README.md`).

---

## 2. Authoritative Approved PO Decisions (D1–D7)

The following decisions are approved by the Product Owner and form the normative baseline for this contract review:

### D1 — ATK Damage Gating
- Damage may be calculated and applied **only when the acting side produces a valid ATK-gem match** (`ClearedGemCounts.Atk > 0`).
- The acting side's ATK stat (`PetState.ATK` or `BossState.ATK`) may contribute to damage **only after** an ATK match is confirmed.
- No default basic attack or independent damage may be generated solely from ATK stats when zero ATK gems are matched.
- Applicable attack modifiers (such as `NextAttack` modifiers) are preserved when no match occurs; they must not be consumed without a qualifying ATK match.

### D2 — Boss Resource Accounting
- Retain `BossState.SkillCharge` as the single Boss ability resource.
- Do **not** introduce `BossState.Power`.
- Boss `SkillCharge` increases **only from POWER gems matched by the Boss** during the Boss's turn.
- Player POWER gem matches must not charge the Boss.
- Specific charge units and threshold values remain balance-tuning decisions.

### D3 — Boss Skill Damage (No Default Direct Damage)
- Direct damage from Boss skills is **prohibited by default**.
- Boss skills must modify the board, resources, or combat modifiers/multipliers.
- Direct damage base values (`BaseDamage = 100..180`) and un-gated damage-over-time (such as Burn DoT) are eliminated.

### D4 — Atomic Round Transaction
- One atomic round encompasses the Player action and the Boss response.
- The state transition must commit completely or not at all.
- Redis Compare-And-Set (`TryUpdateAsync`) provides atomic persistence for the relevant state record; it does **not** provide distributed two-phase commit with SignalR delivery or guarantee exactly-once event broadcast. Reconnection and recovery remain strictly governed by `ADR-008` (state snapshot resynchronization via `GetBattleState`).

### D5 — Hỏa Long: `flame-burst` (Final PO Choice: Option 5B — Boss ATK Buff)
- Remove `flame-burst` direct damage (`BaseDamage = 0`).
- Remove Burn DoT (`StatusEffectType.DoT`) and any fixed damage tick that bypasses a valid ATK match.
- Replace those effects with a temporary **Boss ATK buff** on Hỏa Long.
- The buff modifies Boss ATK but does not independently deal damage; Boss damage is applied only if Hỏa Long produces a valid ATK-gem match.
- Target/stat choice is settled: Option 5A (Pet DEF debuff) is retired. Numeric magnitude and duration remain balance-tuning parameters.

### D6 — Sơn Thạch Vệ: `earthquake` (Option 2A — Temporary Boss Shield)
- Remove direct damage (`BaseDamage = 0`).
- Grant a temporary Shield to the Boss using the existing `StatusEffectType.Shield` mechanism on `BossState.ActiveStatusEffects`.
- Boss deals attack damage only if the Boss matches ATK gems on its turn.

### D7 — Kim Lôi Vương: `thunder-strike` (Option 3A — Buff Next Boss ATK Match)
- Remove direct damage (`BaseDamage = 0`).
- Apply a modifier that buffs the Boss's next valid ATK match.
- The modifier never deals damage independently; it is consumed only upon a qualifying Boss ATK match.

---

## 3. Evidence-Backed Contract-Conflict Matrix

| Conflict ID | Existing Behavior | Evidence (Path & Symbols) | Approved Decision | Classification | Minimal Change Required | Implementation Owner | Status |
|---|---|---|---|---|---|---|---|
| **C-ATK-01** | Boss deals fixed basic attack (`Base = Boss.ATK`) when Skill does not fire. | `GAME_RULES.md:421` (§17 step 18c); `COMBAT_RULES.md:562` (§3.4); `BattleStateService.cs:1943–1946` | **D1** | Contract conflict | Formally retire step 18c basic attack in docs. In `BattleStateService.cs`, skip Boss attack if Boss matched 0 ATK gems. | TASK-260 (Domain) / TASK-262 (App) | Needs docs & code update |
| **C-ATK-02** | Pet ATK stat added unconditionally to Base Damage during player swap resolution. | `COMBAT_RULES.md:334` (§3 step 1); `DamagePipeline.cs:13,374`; `BattleStateService.cs:1486–1506` | **D1** | Contract conflict | Gate attack calculation on confirmed ATK match (`ClearedGemCounts.Atk > 0`). If 0 ATK gems cleared, skip Damage Pipeline and preserve `NextAttack` modifiers. | TASK-260 (Domain) / TASK-262 (App) | Needs docs & code update |
| **C-ATK-03** | Order of operations calculates damage before verifying match type. | `GAME_RULES.md:360` (§17 steps 15–17); `DamagePipeline.cs:354–375` | **D1** | Documentation gap | Amend `GAME_RULES.md` §17 and `COMBAT_RULES.md` §3 to explicitly specify the order of operations: match validation $\rightarrow$ derive match inputs $\rightarrow$ apply ATK stat $\rightarrow$ calculate damage. | TASK-259 (Docs) | Needs documentation |
| **C-RES-01** | Boss `SkillCharge` increments by player total match count during player resolution. | `GAME_STATE.md:2445` (§2.4.3); `BOSS_RULES.md:597` (§6.3); `BattleStateService.cs:1891` | **D2** | Contract conflict | Remove player match increment. In `BattleStateService.cs`, increment `SkillCharge` solely during Boss resolution from Boss POWER gem matches. | TASK-259 (Docs) / TASK-262 (App) | Needs docs & code update |
| **C-RES-02** | `SkillCharge` unit and `ChargeRequirement` thresholds (4–6) were designed for match counts. | `BOSS_RULES.md:611` (§6.3 table); `BossDefinitions.cs:126,173,214,276,316` | **D2** | Balance proposal | Propose clarifying charge unit (+1 per match vs gem energy) during downstream implementation planning. Label as non-blocking tuning proposal. | TASK-259 (Docs) / TASK-260 (Domain) | Non-blocking balance proposal |
| **C-RES-03** | `BossState.SkillCharge` lacks ceiling or cap during cooldown in current code. | `GAME_STATE.md:2444` (§2.4.3); `BossState.cs:22`; `BattleStateService.cs:1891` | **D2** | Balance proposal | Propose explicit clamp: `SkillCharge = min(SkillCharge + delta, ChargeRequirement)` to prevent multi-turn charge banking. Label as balance proposal. | TASK-259 (Docs) / TASK-260 (Domain) | Non-blocking balance proposal |
| **C-SKL-01** | All 5 MVP Boss skills deal direct base damage (100–180) through `DamagePipeline`. | `BOSS_RULES.md:611–618` (§6.3 table); `BossDefinitions.cs:125,172,213,275,315`; `BattleStateService.cs:1943–1946` | **D3** | Contract conflict | Set `BaseDamage = 0` on Boss skills in `BossDefinitions.cs` and remove direct damage calculation from `BattleStateService.cs`. | TASK-259 (Docs) / TASK-262 (App) | Needs docs & code update |
| **C-SKL-02** | Sơn Thạch Vệ (`earthquake`) and Kim Lôi Vương (`thunder-strike`) have no secondary effects; removing direct damage leaves no-ops. | `BOSS_RULES.md:642–651` (§6.3.1 items 4–5); `BossDefinitions.cs:281–284,320–323` | **D3, D6, D7** | Contract conflict | Incorporate D6 (Boss Shield for Earthquake) and D7 (Next-ATK buff for Thunder Strike) into skill definitions. | TASK-259 (Docs) / TASK-260 (Domain) | Resolved by D6, D7 (Needs docs & code update) |
| **C-SKL-03** | Hỏa Long (`flame-burst`) applies Burn DoT (50 fixed dmg/tick at step 19a) outside ATK matches. | `BOSS_RULES.md:628` (§6.3.1 item 1); `BossDefinitions.cs:134`; `BattleStateService.cs:2077–2093,2326–2343` | **D3, D5** | Contract conflict | Remove Burn DoT. Replace with temporary Boss ATK buff per D5 (Option 5B). Flag compounding interaction with Rage passive for review. | TASK-259 (Docs) / TASK-260 (Domain) | Resolved by D5 (Needs docs & code update) |
| **C-DEF-01** | DEF gem matches generate a `DefensePool` that is discarded without affecting combat. | `COMBAT_RULES.md:278` (§2); `ResourceGenerator.cs:309`; `BattleStateService.cs:1456–1510` | None | Implementation gap | Connect `DefensePool` to temporary Shield pool generation on the acting actor (`PetState` or `BossState`). | TASK-260 (Domain) | Needs implementation |
| **C-HP-01** | HP gem healing is hardcoded to Pet; Boss cannot heal from HP gem matches. | `COMBAT_RULES.md:320` (§2 item 5, §4 item 7); `ResourceGenerator.cs:378–418` | None | Implementation gap | Generalize `ResourceGenerator.ApplyHeal` to restore HP to the acting actor (Pet HP or Boss HP, clamped to MaxHP). | TASK-260 (Domain) | Needs implementation |
| **C-TRX-01** | Turn and round semantics conflate individual player action with the complete round. | `GAME_RULES.md:75` (§2 item 1); `GAME_STATE.md:372`; `SwapExecution.cs:508` | **D4** | Documentation gap | Propose intended contract: `Turn` represents the Round counter (advances once per atomic round write-back). | TASK-259 (Docs) / `ADR-026` | Downstream design proposal |
| **C-TRX-02** | Atomicity of Redis CAS does not extend to SignalR event delivery. | `BattleHub.cs:1120–1121` (`TryUpdateAsync` commit followed by `SendAsync`) | **D4** | Documentation gap | Document realistic boundaries: Redis CAS guarantees single-record state atomicity; recovery from mid-delivery failure is governed by `ADR-008` snapshot resync, not 2PC. | `ADR-026` / TASK-263 (Realtime) | Needs documentation |
| **C-TRX-03** | Board deadlock (zero valid swaps) unhandled during in-combat cascade resolution. | `MATCH3_RULES.md:333` (§1.4); `CascadeResolver.cs:83` | **D4** | Implementation gap | Implement deterministic board reshuffle when `HasValidSwap == false` after a cascade, consuming server PRNG and emitting `BoardReshuffled`. | TASK-260 (Domain) / TASK-261 (Boss AI) | Needs implementation |
| **C-WIRE-01** | SignalR protocol closed event discriminator set omits swap coordinates. | `SIGNALR_PROTOCOL.md:505` (§3.2.2); `BattleHub.cs:1120`; `BattleScene.ts:3565` | None | Protocol proposal | Propose admitting `SwapStarted` (`type: "SwapStarted"`, `actor: "player" \| "boss"`, `fromCell`, `toCell`) to enable client animation of Boss moves. | TASK-263 (Realtime) | Protocol proposal |

---

## 4. Reconciled Contract for All Five MVP Boss Skills

Verified against `src/backend/GameServer.Domain/Bosses/BossDefinitions.cs` and `docs/01-game-design/BOSS_RULES.md` §6.3:

| Boss | Skill Name | Current Implementation | Approved Structural Contract | Verification & Implementation Notes |
|---|---|---|---|---|
| **Hỏa Long** | `flame-burst` | 150 Base Dmg + Burn DoT (50 dmg/tick at step 19a) | **Temporary Boss ATK buff (Option 5B)**; direct damage and Burn DoT removed. | Reuses verified `StatusEffectLifecycle.EffectiveBossAttack` (`StatusEffectLifecycle.cs:640`). In `StatusEffectLifecycle.cs:752`, active ATK buffs compound multiplicatively; interaction with `boss-hoa-long-rage` (+20% ATK passive) is flagged for downstream implementation review. |
| **Thủy Ma** | `drain-power` | 120 Base Dmg + instant -20 Pet Power drain | **Remove direct damage; retain verified Pet Power drain.** | Direct damage removed (`BaseDamage = 0`). Instant -20 Power drain operates directly on `PetState.Power` (`GAME_STATE.md` §2.3.1 item 9) and emits `PowerChanged` (`SIGNALR_PROTOCOL.md` §3.2.24). Fully supported by existing domain models. |
| **Mộc Yêu** | `root` | 100 Base Dmg + 2-turn Root debuff (-30% Pet ATK) | **Remove direct damage; retain verified Pet ATK debuff.** | Direct damage removed (`BaseDamage = 0`). Root debuff is stored in `PetState.ActiveStatusEffects`, decrements at step 19a, and reduces Pet ATK at Step 1 (`COMBAT_RULES.md` §5.4.1). Fully supported by existing domain models. |
| **Sơn Thạch Vệ** | `earthquake` | 150 Base Dmg; no secondary effect | **Temporary Boss Shield (Option 2A)**; direct damage removed. | Direct damage removed (`BaseDamage = 0`). `StatusEffectLifecycle.ApplyShield` and `ShieldPool` already operate on `BossState.ActiveStatusEffects` (`BattleStateService.cs:1527,2294`) and absorb damage before Boss HP (`DamagePipeline.cs:500`). Shield amount is deferred to balance tuning. |
| **Kim Lôi Vương** | `thunder-strike` | 180 Base Dmg; no secondary effect | **Buff Boss's next valid ATK match (Option 3A)**; direct damage removed. | Direct damage removed (`BaseDamage = 0`). Represented on `BossState.ActiveStatusEffects` as a `TriggerBased` `BuffDebuff` (`ExpiryCondition = "NextQualifyingAttack"`). Consumed during Boss attack calculation only when an ATK match occurs (`ClearedGemCounts.Atk > 0`). Buff magnitude is deferred to balance tuning. |

### Detailed Skill Audit and Implementation Analysis

#### 1. Hỏa Long — `flame-burst` (Approved Decision D5 — Option 5B)
- **Approved Contract:** Remove direct damage (`BaseDamage = 0`) and Burn DoT. Apply a temporary **Boss ATK buff** to Hỏa Long. The buff modifies Boss ATK but causes no damage independently; Boss damage is applied only when Hỏa Long produces a valid ATK-gem match.
- **Domain Verification:** `StatusEffectLifecycle.EffectiveBossAttack` (`StatusEffectLifecycle.cs:640`) already calculates `BossState.ATK` modified by `BuffDebuff` instances with `TargetStat = "ATK"` on `BossState.ActiveStatusEffects`.
- **Interaction with Passive (Implementation Review Flag):** Hỏa Long's passive (`boss-hoa-long-rage`, `BOSS_RULES.md` §6.2.1) is also an ATK buff (+20% for 3 Turns). In `StatusEffectLifecycle.cs:752`, multiple ATK modifiers fold multiplicatively: `effective = effective * (100 + magnitude) / 100`. Downstream implementation must determine whether `flame-burst` and `rage` compound multiplicatively, additively, or refresh/override each other.
- **Tuning Parameters:** Buff magnitude and duration remain non-blocking balance parameters.

#### 2. Thủy Ma — `drain-power` (Approved Decision D3)
- **Approved Contract:** Remove direct damage (`BaseDamage = 0`). Preserve the existing -20 flat Pet Power drain (`BOSS_RULES.md` §6.3.1 item 2).
- **Domain Verification:** Drains `PetState.Power` directly (`GAME_STATE.md` §2.3.1 item 9), creates no status instance, and emits `PowerChanged(source: "boss", delta: -20)` (`SIGNALR_PROTOCOL.md` §3.2.24). Fully supported by existing code.

#### 3. Mộc Yêu — `root` (Approved Decision D3)
- **Approved Contract:** Remove direct damage (`BaseDamage = 0`). Preserve the existing -30% Pet ATK debuff for 2 Turns (`BOSS_RULES.md` §6.3.1 item 3).
- **Domain Verification:** Stored in `PetState.ActiveStatusEffects` (`GAME_STATE.md` §2.3.1), decremented at step 19a via `StatusEffectLifecycle.ConsumeAtStep19a` (`COMBAT_RULES.md` §5.3), and consumed at Damage Pipeline Step 1 via `StatusEffectLifecycle.EffectiveAttack` (`COMBAT_RULES.md` §5.4.1). Fully supported by existing code.

#### 4. Sơn Thạch Vệ — `earthquake` (Approved Decision D6 — Option 2A)
- **Approved Contract:** Remove direct damage (`BaseDamage = 0`). Grant a temporary Shield to the Boss (`BossState.ActiveStatusEffects`).
- **Domain Verification:** `StatusEffectLifecycle.ApplyShield` and `StatusEffectLifecycle.ShieldPool` already take `IReadOnlyList<StatusEffect>` and operate on `BossState.ActiveStatusEffects` (`BattleStateService.cs:1527,2294`). In `DamagePipeline.cs:500`, `DefenderShieldPool` absorbs incoming player damage before Boss HP. Fully supported by existing code.
- **Tuning Parameters:** Shield magnitude is a non-blocking balance parameter.

#### 5. Kim Lôi Vương — `thunder-strike` (Approved Decision D7 — Option 3A)
- **Approved Contract:** Remove direct damage (`BaseDamage = 0`). Apply a modifier buffing the Boss's next valid ATK match.
- **Domain Verification:** Represented on `BossState.ActiveStatusEffects` as a `TriggerBased` `BuffDebuff` instance (`StatusEffect.cs:142`) with `TargetStat = "ATK"` and `ExpiryCondition = "NextQualifyingAttack"`. Consumed during Boss attack calculation only when an ATK match occurs (`ClearedGemCounts.Atk > 0`). If Boss matches 0 ATK gems, the buff is preserved unconsumed. Step 19a leaves trigger-based instances untouched (`GAME_STATE.md` §5.1.1 item 7).
- **Tuning Parameters:** Buff magnitude is a non-blocking balance parameter.

---

## 5. `SkillCharge` Contract and Clamp Classification (Decision D2)

### Mandatory Normative Contract
1. **Charging Source:** Sever player match coupling. `SkillCharge` increments **solely from POWER gems matched by the Boss during the Boss's turn**.
2. **Player Independence:** Player POWER gem matches must never increase Boss `SkillCharge`.

### Proposals Pending Implementation Clarification (Non-Blocking)
1. **Charging Unit Metric:**
   - *Proposal A (Match-Unit):* `SkillCharge` counts *POWER matches produced by the Boss* (+1 per match). Preserves existing thresholds (4 to 6) in `BossDefinitions.cs` without rebalancing.
   - *Proposal B (Gem-Energy Unit):* `SkillCharge` accumulates `ResourceGeneration.Power` (+10 per gem $\times$ Tier). Requires re-scaling `ChargeRequirement` configuration to 30–60.
   - *Classification:* Engineering/balance proposal. Existing thresholds cannot be assumed balanced under Proposal B without explicit review.
2. **Charge Overflow / Clamp Behavior:**
   - In current code (`BattleStateService.cs:1891`), accumulation is uncapped while `SkillCooldown > 0`.
   - *Proposal:* `SkillCharge = min(SkillCharge + delta, ChargeRequirement)` to prevent multi-turn charge banking.
   - *Classification:* Balance proposal pending explicit confirmation. It is not an approved normative rule and does not block documentation synchronization.

---

## 6. `Sequence` and Round Compatibility Analysis (Decision D4)

### Current Implementation Baseline vs. Intended Contract
- **Current Verified Implementation:** `BattleStateService.cs:921` commits via a single Redis CAS update (`_repository.TryUpdateAsync`). `Sequence` is incremented once per committed write-back (`SwapExecution.cs:517`). `expectedSequence` is verified prior to execution (`BattleStateService.cs:885`). Idempotent retries check `LastCommittedSwapPair.ActionId` (`BattleStateService.cs:873`).
- **Intended Round Contract:** One sequence increment for each successfully committed atomic round is proposed as an intended design contract:
  $$\text{Sequence}_{\text{post-round}} = \text{Sequence}_{\text{pre-round}} + 1$$
  *Status:* Pending comprehensive verification against all protocol and client consumers during downstream implementation.
- **Committed-State Versioning vs. Event Delivery & Client Observation:**
  - `Sequence` is an internal state version number assigned to committed state records in Redis.
  - Incrementing `Sequence` monotonically does **not** guarantee gap-free event delivery across a network connection.
  - A client observing a sequence gap due to network drop recovers via `ADR-008` snapshot resynchronization (`GetBattleState`), not through event stream guarantees.
- **Idempotency Recognition vs. Replay RPC Payload:**
  - In `BattleStateService.cs:870–880`, the server recognizes a matching `actionId` and avoids re-execution, returning `SwapExecutionResult.IdempotentReplay`.
  - In `BattleHub.cs:1115–1118`, the replay RPC returns *only* an acceptance response (`SwapResponse(Accepted: true, Reason: null)`), and **does not** return the complete state snapshot or re-broadcast `ReceiveEvents`.
  - If the client missed the original broadcast or RPC return due to an interrupted connection, recovery requires client-initiated state snapshot resynchronization (`GetBattleState`). Downstream implementation must explicitly test this recovery path.

---

## 7. Redis CAS Persistence vs. SignalR Delivery Boundaries

In strict adherence to D4, the architectural boundaries and failure modes are explicitly decoupled:

```text
┌─────────────────────────────────────────────────────────────┐
│ 1. In-Memory Round Execution                                │
│    Player Phase -> Boss AI & Phase -> Status 19a -> Counters│
└──────────────────────────────┬──────────────────────────────┘
                               │
                [TryUpdateAsync CAS Check]
                               │
             ┌─────────────────┴─────────────────┐
             │ Failure                           │ Success
             ▼                                   ▼
┌──────────────────────────┐       ┌──────────────────────────┐
│ Round Aborted            │       │ State Committed to Redis │
│ Zero State Persisted     │       │ Complete Atomic Round    │
└──────────────────────────┘       └─────────────┬────────────┘
                                                 │
                                     [SignalR WebSocket Send]
                                                 │
                                 ┌───────────────┴───────────────┐
                                 │ Network Drop                  │ Success
                                 ▼                               ▼
                   ┌───────────────────────────┐   ┌───────────────────────────┐
                   │ Events Lost in Transit    │   │ Events Delivered to Client│
                   │ Recovery: ADR-008 Snapshot│   │ Presentation Plays Back   │
                   └───────────────────────────┘   └───────────────────────────┘
```

1. **State Persistence Atomicity:** Redis CAS (`TryUpdateAsync`) guarantees that the candidate round (Player mutations, Boss mutations, HP, charges, and counters) is committed atomically into a single Redis record. If CAS fails, 0% of the round is persisted.
2. **Failure Before Commit:** An exception during Player resolution, Boss AI selection, or Boss resolution aborts execution prior to `TryUpdateAsync`. No partial round is ever committed to Redis.
3. **Failure After Commit:** SignalR broadcasts (`BattleStateUpdated` and `ReceiveEvents`) occur *after* the Redis CAS write succeeds (`BattleHub.cs:1120–1121`). If the server terminates or the network drops during broadcast, events will be lost.
4. **Recovery via `ADR-008`:** Reconnection is strictly governed by `ADR-008` (Snapshot Recovery): the client calls `GetBattleState(battleId)` to obtain the authoritative state snapshot. There is **no transactional outbox or event replay log**.
5. **Idempotency vs. Delivery:** Idempotent retries (`actionId`) protect state consistency against duplicated submissions; they do not provide distributed exactly-once delivery guarantees.

---

## 8. Shared-Board Contract Classification

| Gameplay / Technical Item | Classification | Verification Status & Code Evidence |
|---|---|---|
| **Boss Shield Support via `StatusEffects[]`** | **Verified Existing Behavior** | Implemented. `StatusEffectLifecycle.ApplyShield` and `ShieldPool` already operate on `BossState.ActiveStatusEffects` (`BattleStateService.cs:1527,2294`). |
| **Boss ATK Modifier Support** | **Verified Existing Behavior** | Implemented. `StatusEffectLifecycle.EffectiveBossAttack` (`StatusEffectLifecycle.cs:640`) already calculates `BossState.ATK` modified by `BuffDebuff` instances with `TargetStat = "ATK"`. |
| **Snapshot Recovery via `GetBattleState`** | **Verified Existing Behavior** | Implemented. Governed by `ADR-008` and implemented in `BattleHub.cs:958`. Recovers full state snapshot; zero event replay. |
| **Idempotent Retries via `actionId`** | **Verified Existing Behavior** | Implemented. Implemented in `BattleStateService.cs:870–880` per `ADR-025`. Server recognizes `actionId` and avoids re-execution; RPC returns `SwapResponse(Accepted: true)`. |
| **Single-Record Persistence Atomicity** | **Verified Existing Behavior** | Implemented. In `BattleStateService.cs:921`, state mutations are persisted to Redis in a single CAS write (`_repository.TryUpdateAsync`). |
| **ATK Match Gating on Attack Damage** | **Approved Normative Contract (D1)** | Implementation gap. `DamagePipeline.Calculate` currently executes unconditionally. Must be gated on `ClearedGemCounts.Atk > 0`. |
| **Boss Resource from POWER Matches** | **Approved Normative Contract (D2)** | Implementation gap. `SkillCharge` currently increments from player match counts (`BattleStateService.cs:1891`). Must route from Boss POWER gem matches. |
| **Elimination of Direct Damage on Boss Skills** | **Approved Normative Contract (D3)** | Implementation gap. `BossDefinitions.cs` currently configures direct damage on all 5 skills. Must be set to `BaseDamage = 0`. |
| **DEF Gem Matches Granting Shield** | **Approved Normative Contract** | Implementation gap. `ResourceGenerator.cs:309` calculates `DefensePool`, but it is currently discarded in `BattleStateService.cs`. Must connect to `ApplyShield`. |
| **HP Gem Matches Healing Acting Actor** | **Approved Normative Contract** | Implementation gap. `ResourceGenerator.ApplyHeal` currently hardcoded to `PetState.HP`. Must generalize to heal `BossState.HP` when Boss acts. |
| **In-Combat Deadlock Reshuffling** | **Engineering Proposal** | Implementation gap. `BoardGenerationValidator.HasValidSwap` exists for creation, but runtime deadlock detection and reshuffling after cascades do not exist. |
| **`SwapStarted` Wire Event** | **Engineering Proposal** | Protocol proposal. Currently absent from `SIGNALR_PROTOCOL.md` §3.2.2. Proposed as protocol extension to transmit `{ actor, fromCell, toCell }` for Boss move animation. |
| **`SkillCharge` Threshold Unit Metric** | **Engineering Proposal** | Balance proposal. Propose keeping existing thresholds (4–6) and counting *POWER matches*, or scaling thresholds to *POWER gems*. |
| **`SkillCharge` Cap / Overflow Clamp** | **Engineering Proposal** | Balance proposal. Propose `min(SkillCharge + delta, ChargeRequirement)` to prevent multi-turn banking. |

---

## 9. Downstream Task Dependency Proposal

The downstream work is mapped to candidate tasks `TASK-259` through `TASK-265`.
*(All IDs verified unassigned on disk; tasks remain in proposed candidate status and are NOT marked READY).*

```text
TASK-259 (Docs Synchronization & ADR-026 Authoring)
   ↓
TASK-260 (Domain: ATK Gating, DEF Shield & HP Symmetry) ──┐
   ↓                                                      │
TASK-261 (Domain: Deterministic Boss AI Move Selector)  ──┼─→ TASK-262 (App: Atomic Round Orchestration)
                                                          │      ↓
                                                          │   TASK-263 (Realtime: SwapStarted Wire Event)
                                                          │      ↓
                                                          │   TASK-264 (Client: Phaser Boss Swap Presentation)
                                                          │      ↓
                                                          └── TASK-265 (Testing: Headless Simulation & Tuning)
```

1. **TASK-259 — Shared-Board Game Rules and Documentation Synchronization**
   - Type: `DOCUMENTATION` | Risk: `MEDIUM` | Status: `PROPOSED (BACKLOG CANDIDATE)`
   - Scope: Synchronize `GAME_RULES.md`, `BOSS_RULES.md`, `COMBAT_RULES.md`, `GAME_STATE.md`, `SIGNALR_PROTOCOL.md`, and author `ADR-026`.
2. **TASK-260 — Actor-Relative Cell Effects, ATK Gating, and Combat Pipeline**
   - Type: `GAMEPLAY-CHANGE` | Risk: `HIGH` | Status: `PROPOSED (BACKLOG CANDIDATE)`
   - Scope: Update `ResourceGenerator.cs` and `DamagePipeline.cs`. Enforce zero damage when ATK gems = 0. Connect DEF gems to Shield pool. Enable Boss HP healing.
3. **TASK-261 — Deterministic Boss AI Move Selection Engine**
   - Type: `FEATURE` | Risk: `MEDIUM` | Status: `PROPOSED (BACKLOG CANDIDATE)`
   - Scope: Enumerate 112 candidate swaps. Implement zero-RNG first-pass scoring function and tie-breaking in `BossAiEngine.cs`.
4. **TASK-262 — Shared-Board Atomic Round Orchestration and Service Integration**
   - Type: `FEATURE` | Risk: `HIGH` | Status: `PROPOSED (BACKLOG CANDIDATE)`
   - Scope: Refactor `BattleStateService.ResolveSwapAsync` to execute atomic Round: Player phase $\rightarrow$ Boss phase $\rightarrow$ Step 19a. Single Redis CAS commit.
5. **TASK-263 — Realtime Protocol Extension and Client Event Projection**
   - Type: `FEATURE` | Risk: `MEDIUM` | Status: `PROPOSED (BACKLOG CANDIDATE)`
   - Scope: Admit `SwapStarted` to SignalR protocol. Update `BattleHub.cs` and `BattleEventPresenter.ts` to attribute events to `'player'` or `'boss'`.
6. **TASK-264 — Phaser Battle Presentation for Shared Board and Boss Swaps**
   - Type: `FEATURE` | Risk: `MEDIUM` | Status: `PROPOSED (BACKLOG CANDIDATE)`
   - Scope: Animate Boss swap movement and cascades in `BattleScene.ts`, locking input across round playback.
7. **TASK-265 — Headless Gameplay Balance Simulation and Integration Verification**
   - Type: `FEATURE` | Risk: `MEDIUM` | Status: `PROPOSED (BACKLOG CANDIDATE)`
   - Scope: Run balance simulation harness (`BalanceSimulator.cs`) and integration test suite across all 5 MVP bosses on shared board.

---

## 10. Validation Performed & Artifact Summary

- **File Modified:** `tasks/active/TASK-258-shared-board-gameplay-contract-audit-and-design.md` (only this review artifact was edited).
- **Validation Commands Executed:**
  - Direct regex trailing whitespace check on the complete target file: Zero trailing whitespace lines found.
  - `git diff --check`: Clean on tracked files.
- **Scope Compliance:**
  - Zero gameplay code files modified.
  - Zero ADR files created.
  - Zero downstream task files created.
  - No tasks promoted to `READY`.

---

## Completion Evidence

### Commit
- Implementation slice (Phase A): none (design review and contract audit artifact; declares no implementation files outside its own record)
- Completion record filing (Phase B): this completion record alone (`tasks/completed/TASK-258-shared-board-gameplay-contract-audit-and-design.md`)
- Owns: `TASK-258`
- Files: `tasks/completed/TASK-258-shared-board-gameplay-contract-audit-and-design.md`
- Shared with: `none`
- Unowned / pre-existing: `none`

### Final Status

**`DONE — AUDIT COMPLETE & DOWNSTREAM ROADMAP AUTHORIZED`**
