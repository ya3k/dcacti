# TASK-022 — Implement Boss Response (Steps 18a–18c) and Victory/Defeat (Step 19)

**Status:** DONE
**Priority:** P1 — Blocks Phase 1 boss loop completion
**Depends On:** TASK-021 (Damage Pipeline), TASK-020A (Boss Configuration), TASK-009 (Passive Tracker)
**Scope:** Backend only (Domain + Application layers)

---

## 1. Objective

Implement the Boss Response phase (`GAME_RULES.md` §17 steps 18a–18c) and Victory/Defeat determination so a committed Swap resolves the full documented loop. Battle-end rule authority is `GAME_RULES.md` §1 item 4 (`§1.4`) and `BOSS_RULES.md` §7 — not §17 step 19 itself (step 19 is End Turn; “Step 19” in this task title is the codebase convention for the post-response outcome check).

```
Validation → SwapExecutor (board resolution + Turn++ in its write-back,
               MATCH3_RULES §8.1/§8.3 — BattleStateService does NOT increment Turn)
  → SkillCooldown-- (once per committed Swap, after SwapExecutor returns)
  → Player Passive → SkillCharge += matches
  → Player→Boss Damage (write Boss.HP in same computation)
  → Enrage check (state transition; applies even if Boss.HP just reached 0)
  → Boss HP == 0 → BattleWon → END (no Boss Response)
  → Boss Passive (18a) → Boss Skill OR Boss Basic Attack (18b–18c, Boss→Player Damage)
  → Victory/Defeat (post-response outcome check) → single write-back
```

The task delivers:

1. **Boss Passive** (step 18a): match-counting passives charged on Player Matches via `PassiveTracker.Charge`. Uses shared `PassiveCharged`/`PassiveTriggered` events with `source = "boss"` (`GAME_EVENTS.md` §2, `BOSS_RULES.md` §3). Thủy Ma’s Always-Active trigger (`BOSS_RULES.md` §6.2) is **not** match-charged — skip `PassiveTracker.Charge` for it (see §3.3).
2. **Boss Skill** (step 18b): charges over Player Matches, fires when charged and off cooldown. Applies Boss→Player damage through the Damage Pipeline (`BOSS_RULES.md` §4, §6).
3. **Boss Basic Attack** (step 18c): unconditional Boss→Player damage every turn the Boss does not cast its Skill, using Combo = 1 (`GAME_RULES.md` §17, `COMBAT_RULES.md` §3.4).
4. **Victory/Defeat** (post-response outcome check, codebase “step 19”): outcome determination after Boss Response (or at Boss HP terminal check), emitting `BattleWon` or `BattleLost` (`GAME_EVENTS.md` §2, `GAME_RULES.md` §1 item 4).

---

## 2. Corrections to Previous TASK-022

| Previous Claim | Correct Reference | Issue |
|---|---|---|
| `BossPassiveCharged` event type | `PassiveCharged` with `source = "boss"` (`GAME_EVENTS.md` §2, `SIGNALR_PROTOCOL.md` §3.2.16) | Boss Passive reuses shared events |
| `BossPassiveTriggered` event type | `PassiveTriggered` with `source = "boss"` (`GAME_EVENTS.md` §2, `SIGNALR_PROTOCOL.md` §3.2.17) | Same — shared event |
| `BossBasicAttack` event type | `DamageCalculated`/`DamageDealt`/`DamageTaken` (`GAME_EVENTS.md` §2) | No `BossBasicAttack` exists |
| `BossEnraged` event type | State change only — no event (`BOSS_RULES.md` §5, `GAME_EVENTS.md` §2) | No `BossEnraged` exists |
| `BossPassiveTracker` wrapper | Reuse `PassiveTracker.Charge` directly (`PASSIVE_RULES.md` §2) | No wrapper needed |
| `BossResponseResolver` separate class | Inline logic in `BattleStateService.ExecuteSwap` | Part of §17 resolution, not a separate class |
| `BattleOutcomeResolver` separate class | Inline logic in `BattleStateService.ExecuteSwap` | 2-line conditional, not a separate class |
| `SkillCharge` increments per Swap | `SkillCharge` increments per Player Match (`BOSS_RULES.md` §6.3) | Both Passive and Skill charge per match |
| `SkillCooldown` decrements after Skill fires | `SkillCooldown` decrements once per committed Swap after SwapExecutor returns (post-Turn++) (`BOSS_RULES.md` §6.3, `MATCH3_RULES.md` §8.1) | Cooldown decrements after Turn++ inside the same action, not at end-of-turn only |
| `BattleWonEvent(BossId)` payload | `BattleWon(outcome, finalBossHp, finalPlayerHp)` (`SIGNALR_PROTOCOL.md` §3.2.19) | Missing final HP values |
| `BossDefinition.SkillChargeMatches` | `BossDefinition.SkillChargeRequirement` (`GAME_STATE.md` §2.4.3) | Field name mismatch |
| `BattleEvent` not extended | Must extend for `BossSkillCast`, `BattleWon`, `BattleLost` (`GAME_EVENTS.md` §2) | New event types needed |
| `SkillChargeRequirement=3, SkillCooldownTurns=1` for all bosses | Per-boss values from `BOSS_RULES.md` §6.3 table | Wrong config values |
| Boss Skill `Attack = Boss.ATK` | `Attack = Boss.ATK + BossDefinition.SkillBaseDamage` (`COMBAT_RULES.md` §3 step 1) | Missing Skill base damage term |
| Boss damage uses “Player Element” | Defender Element = active Pet’s Element (`COMBAT_RULES.md` §3.4, `ELEMENT_RULES.md` §5) | Player has no Element; Pet defends |
| `PassiveProgress` and `SkillCharge` cross-reset | Independent — no cross-reset documented | No doc describes cross-reset |

---

## 3. Design Decisions

### 3.1 Boss Skill Attack Composition

`COMBAT_RULES.md` §3 step 1: "ATK stat, Skill/Card base value, and any ATK-Gem-generated damage pool for this action." The `SkillBaseDamage` is the **Skill/Card base value** term — a separate additive component, not a replacement for ATK and not part of `BaseDamagePool`.

**Boss Basic Attack:**
```
Attack = Boss.ATK
BaseDamagePool = 0
Combo = 1
```

**Boss Skill:**
```
Attack = Boss.ATK + BossDefinition.SkillBaseDamage
BaseDamagePool = 0
Combo = 1
```

Both use the same `DamagePipeline.Calculate` with `DamageParty source` and `DamageParty target` parameters.

### 3.2 PassiveId / SkillId Canonical Values

`BOSS_RULES.md` §6.4 is the identity contract for BossId, PassiveId, and
SkillId. Use these exact strings — do not invent others:

```
Boss        BossId              PassiveId                    SkillId
---------   ------------------  ---------------------------  ------------
Hỏa Long    "Hỏa Long"          "boss-hoa-long-rage"         "flame-burst"
Thủy Ma     "Thủy Ma"           "boss-thuy-ma-heal"          "drain-power"
Mộc Yêu     "Mộc Yêu"           "boss-moc-yeu-regen"         "root"
```

- **BossId** is the display name already in `BossDefinitions.cs` (`BossId("Hỏa Long")`, etc.).
- **PassiveId** is used in `PassiveCharged`/`PassiveTriggered` (`source = "boss"`, `SIGNALR_PROTOCOL.md` §3.2.16–§3.2.17).
- **SkillId** is used in `BossSkillCast.skillId` (`SIGNALR_PROTOCOL.md` §3.2.18).
- `sourceId` on Boss events is the display-name BossId (e.g. `"Hỏa Long"`), not a slug.

### 3.3 Phase 1 Boss Configuration

Per `BOSS_RULES.md` §6.2–§6.4:

| Boss | PassiveId | PassiveThreshold | SkillId | SkillBaseDamage | SkillChargeRequirement | SkillCooldownTurns | EnrageThreshold |
|------|-----------|------------------|---------|-----------------|------------------------|--------------------|-----------------|
| Hỏa Long | `"boss-hoa-long-rage"` | 5 | `"flame-burst"` | 150 | 5 | 2 | 0.30 |
| Thủy Ma | `"boss-thuy-ma-heal"` | 0 (Always-Active — see note) | `"drain-power"` | 120 | 4 | 3 | 0.30 |
| Mộc Yêu | `"boss-moc-yeu-regen"` | 5 | `"root"` | 100 | 6 | 2 | 0.30 |

**PassiveThreshold notes:**
- Hỏa Long and Mộc Yêu: match-charged — `PassiveTracker.Charge` called with
  Threshold 5 after each Cascade batch (`BOSS_RULES.md` §6.2, `PASSIVE_RULES.md` §2).
- Thủy Ma: trigger is “Passive (always active)” (`BOSS_RULES.md` §6.2) — an
  alternate trigger (`PASSIVE_RULES.md` §3), **not** match-based. Store
  `PassiveThreshold = 0` as the Always-Active marker and **never call**
  `PassiveTracker.Charge` for this boss (`PassiveTracker` rejects
  Threshold < 1). No match-driven `PassiveCharged`/`PassiveTriggered` for
  Thủy Ma. (TASK-022 does not apply passive *effects* — see §3.8.)
- SkillChargeRequirement is independent of PassiveThreshold (both increment
  from Player Matches where applicable; SkillCharge uses a plain int).

### 3.4 SkillCooldown Lifecycle

`BOSS_RULES.md` §6.3: Cooldown “Decrements by 1 at each Turn increment”
(citation corrected — was wrongly “GAME_RULES.md §17 step 17”; Turn begins
during board resolution per `MATCH3_RULES.md` §8.1/§8.3, and the stored
Turn advances once in SwapExecutor’s write-back, `GAME_STATE.md` §5.1).

`SwapExecution.cs` already performs `Turn = state.Turn + 1` in SwapExecutor’s
write-back. `BattleStateService.ExecuteSwap` must **not** increment Turn again.
`SkillCooldown--` runs **once per committed Swap, after SwapExecutor returns**
(so after that Turn++), before Player Passive / SkillCharge / damage / Boss Response:

```
Validation
    ↓
SwapExecutor — board resolution + Turn++ (existing; MATCH3_RULES §8.1)
    ↓
if SkillCooldown > 0 → SkillCooldown--
    ↓
Player Passive (PassiveTracker.Charge, source="pet")
    ↓
SkillCharge += totalMatches
    ↓
Player → Boss Damage (write Boss.HP)
    ↓
Enrage → terminal check → 18a → 18b/18c → outcome → single write-back
```

When a Boss Skill fires:
```
SkillCharge = 0
SkillCooldown = BossDefinition.SkillCooldownTurns
```

The next committed Swap’s post-SwapExecutor `SkillCooldown--` performs the
first cooldown decrement.

### 3.5 Enrage — Permanent State Transition

`BOSS_RULES.md` §5 item 4: "Enrage is a permanent state transition triggered when `BossHP < EnrageThreshold`."

- Evaluated after Player→Boss damage (steps 15–17) and before Boss Response (step 18)
- Uses strict `<` per authoritative wording
- No `BossEnraged` event exists — state change is inferred from event sequence
- If `BossState.State` is already `Enraged`, do not transition again

### 3.6 Boss Death Preempts Boss Response

Order is: Player→Boss Damage → **Enrage evaluation** → terminal Boss HP check → Boss Response.

If `Boss HP == 0` after Player→Boss damage:
- Enrage is evaluated first (`BOSS_RULES.md` §5 item 4, §6): the state transition
  applies whenever `BossHP < MaxHP × EnrageThreshold`, including when HP just
  reached 0 (state set, then battle ends — harmless, consistent with §6).
- Emit `BattleWon`
- **END** — no Boss Passive, no Boss Skill, no Boss Basic Attack
- No Boss Passive events after Boss death

Enrage is **not** skipped on death; only Boss Response (18a–18c) is skipped.

### 3.7 PassiveProgress and SkillCharge Independence

These are completely independent state variables:
- `PassiveProgress` controls Boss Passive charging
- `SkillCharge` controls Boss Skill charging
- Player Matches contribute to both
- `PassiveTriggered` does NOT reset `SkillCharge`
- Boss Skill firing does NOT reset `PassiveProgress`
- Unless `PassiveTracker`'s own reset contract explicitly changes `PassiveProgress` as part of its trigger/reset behavior

### 3.8 Boss Passive Effect Boundary

TASK-022 implements only:
```
PassiveProgress charging → PassiveCharged → PassiveTriggered → PassiveProgress reset per PassiveResetBehavior
```

Does NOT implement: Mộc Yêu healing, Thủy Ma healing reduction, Root, Stun, Damage-over-Time, Buffs, Debuffs.

### 3.9 Event Contract

**Event types used:**
```
PassiveCharged       (shared — source="pet" or source="boss")
PassiveTriggered     (shared — source="pet" or source="boss")
BossSkillCast        (NEW)
BattleWon            (NEW)
BattleLost           (NEW)
DamageCalculated     (existing)
DamageDealt          (existing)
DamageTaken          (existing)
```

**Event types NOT created:**
```
BossPassiveCharged
BossPassiveTriggered
BossBasicAttack
BossEnraged
```

Boss Passive events contain `source = "boss"`, `sourceId = BossId`. Pet Passive events remain `source = "pet"`, `sourceId = PetId`.

### 3.10 Final Terminal Resolution Order

**If Boss dies (or would be at 0 HP):**
```
Player → Boss Damage
    ↓
Enrage evaluation (BossHP < MaxHP × EnrageThreshold) — state set even if HP == 0
    ↓
Boss HP == 0 → BattleWon → END
```
No Boss Passive, Skill, or Basic Attack (Enrage does fire — see §3.6).

**If Boss survives:**
```
Player → Boss Damage
    ↓
Enrage evaluation (BossHP < MaxHP × EnrageThreshold)
    ↓
Boss Passive 18a
    ↓
Boss Skill 18b OR Basic Attack 18c
    ↓
Boss → Player Damage (write Player.HP)
    ↓
Player HP == 0 → BattleLost
```

Both paths end with one state write-back (`GAME_STATE.md` §5.1).

---

## 4. Gap Analysis — Authoritative Docs vs. Current Source

### 4.1 `BossState` — `GAME_STATE.md` §2.4

| Field | Doc §2.4 | Source | Action |
|-------|----------|--------|--------|
| `BossId` | §2.4 | ✅ Implemented (`BossState.cs`) | — |
| `Element` | §2.4 | ✅ Implemented | — |
| `HP` / `MaxHP` | §2.4 | ✅ Implemented | — |
| `ATK` / `DEF` | §2.4 | ✅ Implemented | — |
| `State` (Idle/Charging/Enraged/Stunned) | §2.4 | ✅ Implemented (`BossStateKind.cs`) | — |
| `PassiveId` | §2.4, §2.4.1 | ❌ Missing | **NEEDS NEW** |
| `PassiveProgress` | §2.4, §2.4.2 | ❌ Missing | **NEEDS NEW** |
| `SkillCharge` | §2.4, §2.4.3 | ❌ Missing | **NEEDS NEW** |
| `SkillCooldown` | §2.4, §2.4.3 | ❌ Missing | **NEEDS NEW** |
| `StatusEffects[]` | §2.4 | ❌ Missing | DEFERRED |

### 4.2 `BossDefinition` — `BOSS_RULES.md` §6

| Field | Doc | Source | Action |
|-------|-----|--------|--------|
| `BossId`, `Element`, `MaxHP`, `ATK`, `DEF` | §6.1 | ✅ Implemented (`BossDefinition.cs`) | — |
| `PassiveId` | §6.4, §3 | ❌ Missing | **NEEDS NEW** |
| `PassiveThreshold` | §6.2, §6.4 | ❌ Missing | **NEEDS NEW** (0 = Always-Active for Thủy Ma) |
| `PassiveResetBehavior` | §3 | ❌ Missing | **NEEDS NEW** |
| `SkillId` | §6.4, §4 | ❌ Missing | **NEEDS NEW** |
| `SkillBaseDamage` | §4 item 3, §6.3 | ❌ Missing | **NEEDS NEW** |
| `SkillChargeRequirement` | §4, §6.3 | ❌ Missing | **NEEDS NEW** |
| `SkillCooldownTurns` | §4, §6.3 | ❌ Missing | **NEEDS NEW** |
| `EnrageThreshold` | §5 item 1, §6.1 | ❌ Missing | **NEEDS NEW** |

### 4.3 `BattleEvent` / `BattleEventType` — `GAME_EVENTS.md` §2

| Event | Doc | Source | Action |
|-------|-----|--------|--------|
| 9 existing types | §2 | ✅ Implemented (`BattleEvent.cs`) | — |
| `PassiveCharged` (shared) | §2 | ⚠️ Partial | **ADD Source/SourceId** |
| `PassiveTriggered` (shared) | §2 | ⚠️ Partial | **ADD Source/SourceId** |
| `BossSkillCast` | §2 | ❌ Missing | **NEEDS NEW** |
| `BattleWon` | §2 | ❌ Missing | **NEEDS NEW** |
| `BattleLost` | §2 | ❌ Missing | **NEEDS NEW** |

### 4.4 `DamagePipeline` — `COMBAT_RULES.md` §3

| Requirement | Doc | Source | Action |
|-------------|-----|--------|--------|
| Player→Boss direction | §3 | ✅ Implemented | — |
| Boss→Player direction | §3.4 | ❌ Missing | **Generalize pipeline** — add `source`/`target` params; write `Player.HP` in same write-back; no second damage formula |
| Combo = 1 for Boss | §4 item 3 | ❌ Missing | Pass `Combo = 1` at call site |
| SkillBaseDamage in Attack | §3 step 1 | ❌ Missing | `Attack = Boss.ATK + SkillBaseDamage` |
| Defender Element for Boss→Player | §3.4 Step 3, ELEMENT_RULES §5 | ❌ Missing | Defender is the active Pet (`playerPet.Element`), not “Player Element” |
| Defender DEF for Boss→Player | §3.2 | ❌ Missing | Active Pet DEF per §3.2 |

### 4.5 `BattleEventWireProjection` — `SIGNALR_PROTOCOL.md` §3.3

| Requirement | Doc | Source | Action |
|-------------|-----|--------|--------|
| `source`/`sourceId` on PassiveCharged/PassiveTriggered | §3.3, §3.2.16–§3.2.17 | ❌ Missing | **ADD fields to DTO + projection** |
| `BossSkillCast` wire projection | §3.2.18 | ❌ Missing | **NEEDS NEW** |
| `BattleWon` wire projection | §3.2.19 | ❌ Missing | **NEEDS NEW** |
| `BattleLost` wire projection | §3.2.19 | ❌ Missing | **NEEDS NEW** |

### 4.6 `BattleStateService.ExecuteSwap` — Orchestration

| Step | Doc | Source | Action |
|------|-----|--------|--------|
| Validation → SwapExecutor board resolution (includes Turn++ in its write-back) | §17, MATCH3 §8.1/§8.3 | ✅ Implemented (`SwapExecution.cs` `Turn = state.Turn + 1`) | **Do NOT add a second Turn++** in `BattleStateService` |
| SkillCooldown-- after SwapExecutor returns | MATCH3 §8.1, BOSS §6.3 | ❌ Missing | **NEEDS NEW** — once per committed Swap, post-Turn++ |
| Player Passive → SkillCharge increment | §17, BOSS §6.3 | ⚠️ Partial / ❌ SkillCharge | Player Passive exists; add SkillCharge += matches |
| Player→Boss Damage | §17 steps 15–17 | ✅ Implemented | — |
| Enrage check after Player→Boss damage (before terminal) | BOSS §5.4, §6 | ❌ Missing | **NEEDS NEW** |
| Boss HP terminal check → BattleWon | GAME_RULES §1.4, BOSS §7 | ❌ Missing | **NEEDS NEW** — skip 18a–18c if HP == 0 |
| Boss Passive charge (step 18a) — skip if PassiveThreshold == 0 | §17, BOSS §6.2 | ❌ Missing | **NEEDS NEW** |
| Boss Skill or Basic Attack (steps 18b–18c) | §17, COMBAT §3.4 | ❌ Missing | **NEEDS NEW** |
| Boss→Player damage via DamagePipeline + Player.HP write-back | COMBAT §3.4, GAME_STATE §5.1 | ❌ Missing | **NEEDS NEW** — same write-back, no duplicate pipeline |
| Outcome check → BattleLost / both alive → no outcome event | GAME_RULES §1.4 | ❌ Missing | **NEEDS NEW** |

---

## 5. Implementation Steps

### Phase A: Domain Layer — State Expansion

**Step A1: Expand `BossState`** (`src/backend/GameServer.Domain/Battle/BossState.cs`)

Add four fields per `GAME_STATE.md` §2.4.1:

```
BossState
├── BossId, Element, HP, MaxHP, ATK, DEF, State  (existing)
├── PassiveId          (PassiveId — which Boss Passive, NEW)
├── PassiveProgress    (PassiveProgress — charging state, NEW)
├── SkillCharge        (int — matches charged toward Skill, NEW)
└── SkillCooldown      (int — turns remaining before Skill can fire, NEW)
```

Update `BossState.Initial(...)` to initialize:
- `PassiveId = passiveId` (from `BossDefinition`)
- `PassiveProgress = PassiveProgress.AtStart(passiveThreshold)`
- `SkillCharge = 0`
- `SkillCooldown = 0`

**Step A2: Expand `BossDefinition`** (`src/backend/GameServer.Domain/Bosses/BossDefinition.cs`)

Add mechanic configuration fields:

```
BossDefinition
├── BossId, Element, MaxHP, ATK, DEF  (existing)
├── PassiveId              (PassiveId — which Boss Passive, NEW)
├── PassiveThreshold       (int — "every N Matches", NEW)
├── PassiveResetBehavior   (PassiveResetBehavior? — null = Default, NEW)
├── SkillId                (string — which Boss Skill, NEW)
├── SkillBaseDamage        (int — additive to Boss.ATK in Skill Attack, NEW)
├── SkillChargeRequirement (int — matches needed to charge Skill, NEW)
├── SkillCooldownTurns     (int — cooldown after Skill fires, NEW)
└── EnrageThreshold        (double — fraction of MaxHP, default 0.30, NEW)
```

Update `ToInitialState()` to pass new fields to `BossState.Initial`.

**Step A3: Update `BossDefinitions`** (`src/backend/GameServer.Domain/Bosses/BossDefinitions.cs`)

Per-boss values from `BOSS_RULES.md` §6.2–§6.4 (identical to §3.3):

| Boss | BossId | PassiveId | PassiveThreshold | SkillId | SkillBaseDamage | SkillChargeRequirement | SkillCooldownTurns | EnrageThreshold |
|------|--------|-----------|------------------|---------|-----------------|------------------------|--------------------|-----------------|
| Hỏa Long | `"Hỏa Long"` | `"boss-hoa-long-rage"` | 5 | `"flame-burst"` | 150 | 5 | 2 | 0.30 |
| Thủy Ma | `"Thủy Ma"` | `"boss-thuy-ma-heal"` | 0 (Always-Active) | `"drain-power"` | 120 | 4 | 3 | 0.30 |
| Mộc Yêu | `"Mộc Yêu"` | `"boss-moc-yeu-regen"` | 5 | `"root"` | 100 | 6 | 2 | 0.30 |

### Phase B: Domain Layer — Passive Event Source Fields

**Step B1: Extend `PassiveChargedEvent`** (`src/backend/GameServer.Domain/Passives/PassiveEvents.cs`)

Add `Source` and `SourceId` parameters with defaults:

```csharp
public readonly record struct PassiveChargedEvent(
    PassiveId PassiveId,
    int Progress,
    int Threshold,
    string Source = "pet",     // "pet" or "boss" (GAME_EVENTS.md §2)
    string? SourceId = null)   // PetState.PetId or BossState.BossId
```

**Step B2: Extend `PassiveTriggeredEvent`** — same pattern:

```csharp
public readonly record struct PassiveTriggeredEvent(
    PassiveId PassiveId,
    int Progress,
    int Threshold,
    string Source = "pet",
    string? SourceId = null)
```

**Step B3: Update existing Pet Passive call sites** — pass `Source = "pet"`, `SourceId = petId` explicitly for clarity (the defaults handle backward compatibility, but explicit is better than implicit).

### Phase C: Domain Layer — DamagePipeline Direction

**Step C1: Add direction parameters to `DamagePipeline.Calculate`** (`src/backend/GameServer.Domain/Combat/DamagePipeline.cs`)

Add `DamageParty source` and `DamageParty target` parameters:

```csharp
public static DamageResult Calculate(
    BossState boss,           // defender stats
    DamageInputs inputs,      // attacker stats
    ComboModifiers comboModifiers,
    ElementModifiers elementModifiers,
    DamageParty source,       // NEW — "player" or "boss"
    DamageParty target)       // NEW — "boss" or "player"
```

Replace hardcoded `DamageParty.Player`/`DamageParty.Boss` in event construction (lines 327–335):

```csharp
var dealt = new DamageDealtEvent(source, target, finalDamage);
var taken = new DamageTakenEvent(source, target, finalDamage);
```

For Player→Boss: `source = DamageParty.Player, target = DamageParty.Boss` (existing call sites unchanged).
For Boss→Player: `source = DamageParty.Boss, target = DamageParty.Player`.

**Step C2: Update existing call site** — Pass `DamageParty.Player, DamageParty.Boss` to preserve current behavior.

### Phase D: Domain Layer — BattleEvent Expansion

**Step D1: Add new `BattleEventType` members** (`src/backend/GameServer.Domain/Match3/BattleEvent.cs`)

```
BattleEventType (existing 9 + 3 new = 12 total)
├── ... existing 0–8 ...
├── BossSkillCast = 9
├── BattleWon = 10
└── BattleLost = 11
```

No `BossPassiveCharged`, `BossPassiveTriggered`, `BossBasicAttack`, or `BossEnraged`.

**Step D2: Add new event payload types**

- `BossSkillCastEvent(string SkillId, string SourceId)` — skill was cast (`SIGNALR_PROTOCOL.md` §3.2.18)
- `BattleWonEvent(int FinalBossHp, int FinalPlayerHp)` — victory (`SIGNALR_PROTOCOL.md` §3.2.19)
- `BattleLostEvent(int FinalBossHp, int FinalPlayerHp)` — defeat (`SIGNALR_PROTOCOL.md` §3.2.19)

**Step D3: Extend `BattleEvent` record struct**

Add private fields and public accessors for the three new event types:

```csharp
private readonly BossSkillCastEvent? _bossSkillCast;
private readonly BattleWonEvent? _battleWon;
private readonly BattleLostEvent? _battleLost;

public BossSkillCastEvent BossSkillCast => _bossSkillCast ?? throw ...;
public BattleWonEvent BattleWon => _battleWon ?? throw ...;
public BattleLostEvent BattleLost => _battleLost ?? throw ...;
```

Update the private constructor to accept the three new nullable fields.

**Step D4: Add factory methods to `BattleEvent`**

```csharp
public static BattleEvent ForBossSkillCast(string skillId, string sourceId)
public static BattleEvent ForBattleWon(int finalBossHp, int finalPlayerHp)
public static BattleEvent ForBattleLost(int finalBossHp, int finalPlayerHp)
```

Update `ToString()` for the three new types.

### Phase E: Application Layer — Orchestration Update

**Step E1: Update `BattleStateService.ExecuteSwap`** (`src/backend/GameServer.Application/Battle/BattleStateService.cs`)

Constraints:
- **No second Turn++.** `SwapExecution.cs` already does `Turn = state.Turn + 1` in
  SwapExecutor’s write-back. Do not increment Turn in `BattleStateService`.
- **One write-back** at the end for all state mutated after SwapExecutor returns
  (cooldown, passive progress, SkillCharge, Boss HP, Player HP, Enrage, outcome) —
  `GAME_STATE.md` §5.1.
- **Generalize `DamagePipeline`** (currently Player→Boss / Boss-HP-only) to accept
  direction and write the correct defender HP path — no second damage formula,
  no ad-hoc arithmetic in the Application layer.
- Thủy Ma (`PassiveThreshold == 0`): skip `PassiveTracker.Charge`.

The complete resolution order becomes:

```csharp
// === Existing: Validation + SwapExecutor (board resolution + Turn++) ===
// (MATCH3_RULES §8.1/§8.3 — Turn++ already inside SwapExecutor; do not repeat)

// === NEW: SkillCooldown-- after SwapExecutor returns (post-Turn++) ===
// 1. if (boss.SkillCooldown > 0) boss.SkillCooldown--

// === Existing ===
// 2. Player Passive charging (PassiveTracker.Charge, source="pet")

// === NEW: SkillCharge increment ===
// 3. boss.SkillCharge += totalMatches   // every committed Swap's match count

// === NEW: Player→Boss Damage ===
// 4. DamagePipeline.Calculate(..., source=Player, target=Boss)
//    boss.HP = result.NewHP   // write Boss.HP in the same computation

// === NEW: Enrage check (after Player→Boss damage, before terminal) ===
// 5. if (boss.HP < boss.MaxHP * bossDef.EnrageThreshold
//        && boss.State != BossStateKind.Enraged)
//        boss = boss with { State = BossStateKind.Enraged }
//    // Applies even when boss.HP == 0 (BOSS_RULES §5.4) — set state, then check terminal

// === NEW: Boss HP terminal check ===
// 6. if (boss.HP == 0)
//        → emit BattleWon(finalBossHp=0, finalPlayerHp=player.HP)
//        → skip to write-back, RETURN (no 18a–18c, no outcome re-check)

// === NEW: Boss Passive (step 18a) — only if not Always-Active ===
// 7. if (bossDef.PassiveThreshold > 0)
//        var bossPassiveResult = PassiveTracker.Charge(
//            boss.PassiveProgress, totalMatches,
//            bossDef.PassiveId, bossDef.PassiveResetBehavior)
//        Update boss.PassiveProgress
//        Append PassiveCharged/PassiveTriggered with source="boss",
//              sourceId=boss.BossId  // display name, BOSS_RULES §6.4
//    else: Thủy Ma Always-Active — no Charge, no match-driven passive events

// === NEW: Boss Response (steps 18b–18c) ===
// 8. if (boss.SkillCharge >= bossDef.SkillChargeRequirement
//        && boss.SkillCooldown == 0)
//        → Boss Skill Cast:
//          - emit BossSkillCast(skillId, sourceId: boss.BossId)
//          - bossInputs = DamageInputs(
//                Attack: boss.ATK + bossDef.SkillBaseDamage,
//                BaseDamagePool: 0,
//                Combo: 1,
//                AttackerElement: boss.Element,
//                DefenderElement: playerPet.Element,   // active Pet, ELEMENT_RULES §5
//                DefenderDefense: playerPet.DEF)
//          - DamagePipeline.Calculate(..., source=Boss, target=Player)
//            → write player.HP (and shield pool if any) in same computation
//          - boss.SkillCharge = 0
//          - boss.SkillCooldown = bossDef.SkillCooldownTurns
//     else
//        → Boss Basic Attack:
//          - same pipeline, Attack = boss.ATK, Combo = 1
//          - write player.HP

// === NEW: Outcome check (codebase "step 19") ===
// 9. if (player.HP <= 0)
//        → emit BattleLost(finalBossHp, finalPlayerHp=0)
//    // both HP > 0 → no BattleWon/BattleLost this action (battle continues)

// === Single state write-back ===
// 10. Persist all mutated state once (GAME_STATE.md §5.1)
```

### Phase F: Application Layer — Wire Projection

**Step F1: Update `BattleEventWireDto`** (`src/backend/GameServer.Api/Hubs/BattleEventWireProjection.cs`)

Add `Source` and `SourceId` fields to the DTO for PassiveCharged/PassiveTriggered:

```csharp
[property: JsonPropertyName("source")]
[property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Source = null,

[property: JsonPropertyName("sourceId")]
[property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? SourceId = null
```

Update `PassiveCharged` and `PassiveTriggered` factory methods to accept `source` and `sourceId`:

```csharp
public static BattleEventWireDto PassiveCharged(
    string passiveId, int progress, int threshold,
    string source, string? sourceId)
```

**Step F2: Add new wire projections**

Add `BossSkillCast`, `BattleWon`, `BattleLost` to `BattleEventWireProjection.Project` switch.

**Step F3: Update `ProjectPassiveCharged`/`ProjectPassiveTriggered`**

Read `Source`/`SourceId` from the event payload and project `source`/`sourceId` in JSON.

### Phase G: Tests

**Step G1: Update `BossStateTests.cs`**
- Add `PassiveId`, `PassiveProgress`, `SkillCharge`, `SkillCooldown` to field audit
- Update `BossState.Initial` tests for new field initialization

**Step G2: Update `DamagePipelineTests.cs`**
- Add `source`/`target` parameters to existing tests (pass `Player`/`Boss`)
- Add Boss→Player direction tests
- Verify `Attack = Boss.ATK + SkillBaseDamage` produces correct base damage
- Verify Combo = 1 produces documented boss damage
- Verify Element matchup applies for Boss→Player

**Step G3: Update `PassiveEventsTests.cs`**
- Verify `PassiveChargedEvent` with `Source = "boss"` serializes correctly
- Verify `PassiveTriggeredEvent` with `Source = "boss"` serializes correctly
- Verify existing Pet Passive events default to `Source = "pet"`

**Step G4: Create `BossResponseTests.cs`**
- Boss Passive charges on Player Matches, triggers at Threshold (Hỏa Long/Mộc Yêu PT=5)
- Thủy Ma (PT=0): no `PassiveTracker.Charge`, no match-driven passive events
- Boss Passive uses shared events with `source="boss"`, `sourceId=display BossId`
- Boss Skill casts when `SkillCharge >= Requirement && SkillCooldown == 0`
- Boss Skill resets `SkillCharge = 0`, sets `SkillCooldown = CooldownTurns`
- Boss Skill uses `Attack = Boss.ATK + SkillBaseDamage`
- Boss Basic Attack when Skill not charged; Element defender = active Pet
- Boss Basic Attack uses `Attack = Boss.ATK`
- Boss Skill and Basic Attack are mutually exclusive
- Enrage triggers at `BossHP < MaxHP × EnrageThreshold`, does not re-trigger
- Enrage fires even when Player damage sets Boss HP == 0 (state before terminal)
- Boss does not attack if HP = 0 (Player killed Boss)
- `SkillCharge` increments per Player Match
- `SkillCooldown` decrements once after SwapExecutor returns (after Turn++), not at end
- Exactly one Turn++ per committed Swap (no double increment)
- `PassiveProgress` and `SkillCharge` do NOT cross-reset

**Step G5: Create `VictoryDefeatTests.cs`**
- `BattleWon` when Boss HP = 0 after Player damage (Boss dies before attacking)
- `BattleLost` when Player HP = 0 after Boss damage
- `BattleWon` carries `finalBossHp = 0` and correct `finalPlayerHp`
- `BattleLost` carries correct `finalBossHp` and `finalPlayerHp = 0`
- No outcome event when both HP > 0
- Boss HP checked before Player HP (Boss death preempts Boss Response)

**Step G6: Update `BattleEventEmissionTests.cs`**
- Verify event ordering matches §6 event ordering
- Verify `BossSkillCast` appears in correct position
- Verify `BattleWon`/`BattleLost` appear as last events
- Verify no `BossBasicAttack`, `BossEnraged`, `BossPassiveCharged`, `BossPassiveTriggered` events exist

**Step G7: Update `BattleStateServiceTests.cs`**
- Integration test: full Swap → Passive → Damage → Enrage → Boss Passive → Boss Response → Outcome
- Verify single write-back includes all state changes
- Verify Boss→Player damage events in `ReceiveEvents` batch

**Step G8: Update `BattleEventWireProjectionTests.cs`**
- Verify PassiveCharged/PassiveTriggered wire projection includes `source`/`sourceId`
- Verify BossSkillCast wire projection
- Verify BattleWon/BattleLost wire projection

---

## 6. Event Ordering (Authoritative)

`GAME_EVENTS.md` §1 + §17 steps 15–19 + `BOSS_RULES.md` §5.4/§6:

```
1.  MatchCreated / CascadeCreated / ComboChanged / GemMatched     (board resolution)
2.  PassiveCharged (per Match) / PassiveTriggered (at most once)   (Player Passive, source="pet")
3.  DamageCalculated / DamageDealt / DamageTaken                   (Player→Boss)
4.  [Enrage state transition — no event]                           (BOSS_RULES §5.4, §6)
5.  [Boss HP terminal check]                                       (GAME_RULES §1.4, BOSS_RULES §7)
    IF Boss HP == 0:
        BattleWon → END
        SKIP steps 6–8
6.  PassiveCharged (per Match) / PassiveTriggered (at most once)   (Boss Passive, source="boss";
       only if PassiveThreshold > 0 — never for Thủy Ma Always-Active)
7.  BossSkillCast                                                  (only if Skill fires, only if Boss survived)
8.  DamageCalculated / DamageDealt / DamageTaken                   (Boss→Player, if Boss attacked)
9.  BattleLost                                                     (only if Player HP <= 0 after Boss Response;
       if both HP > 0 → no outcome event this action)
```

Key rules:
- Step 4 Enrage fires BEFORE Boss HP check — state is set even if Boss HP == 0 (`BOSS_RULES.md` §5.4/§6)
- Step 5 Boss HP check preempts Boss Response: if Player damage killed the Boss, no Boss Passive, no Boss Skill, no Boss Basic Attack
- `BossBasicAttack` is NOT an event — boss basic attack produces `DamageCalculated`/`DamageDealt`/`DamageTaken`
- `BossEnraged` is NOT an event — state change is inferred from `BossState.State`
- `BossPassiveCharged`/`BossPassiveTriggered` are NOT events — Boss Passive uses shared `PassiveCharged`/`PassiveTriggered` with `source="boss"`
- No second Turn++ after step 1 — Turn already advanced inside SwapExecutor (`MATCH3_RULES.md` §8.1)

---

## 7. Out of Scope

Per `MVP_SCOPE.md` §2 and this task's boundaries:

- **No new API endpoints** — `POST /api/battle/start` already handles Boss selection
- **No new database tables** — all state is Active Battle State in Redis (ADR-005)
- **No new SignalR messages** — events travel on existing `ReceiveEvents` (`SIGNALR_PROTOCOL.md` §3)
- **No Match-3 changes** — board resolution is unchanged
- **No Pet/Relic/Card changes** — Player Passive is unchanged
- **No Rewards/Progression** — `GAME_RULES.md` §19 reward system is a separate task
- **No StatusEffects** — `COMBAT_RULES.md` §5 is deferred
- **No multi-phase Boss State machine** — `BOSS_RULES.md` §5 item 3 defers to Future
- **No Stun implementation** — deferred
- **No Boss healing/regeneration** — Mộc Yêu's Passive effect is noted but not applied
- **No Thủy Ma healing reduction** — Passive effect is noted but not applied
- **No Boss Passive effect application** — Passive triggers emit events only

---

## 8. BLOCKED Items

| Item | Reason | Resolution |
|------|--------|------------|
| Boss Passive effect application | `GAME_EVENTS.md` §2 item 3 defers effect summary to Combat stage | Wait for Passive effect system — still OUT of scope |
| Per-Boss Passive/Skill/Boss identity strings | Previously undefined in docs | **RESOLVED** — canonical values in `BOSS_RULES.md` §6.4 (`boss-hoa-long-rage`, `boss-thuy-ma-heal`, `boss-moc-yeu-regen`; `flame-burst`, `drain-power`, `root`; BossId = display names). Applied in §3.2/§3.3/A3. |
| Thủy Ma PassiveThreshold | Trigger is Always-Active, not match-count | **RESOLVED** — `BOSS_RULES.md` §6.2: no match charging; store `PassiveThreshold = 0`, skip `PassiveTracker.Charge` (§3.3, Phase E step 7). |

---

## 9. Documentation Updates

Documentation updates performed for the TASK-022 final audit (this task’s
BLOCKED resolution — not a change made during implementation):

| File | Change |
|------|--------|
| `BOSS_RULES.md` → v2.1 | §3.3 sub-steps 18b/18c; §5.4 Enrage ordering sentence; §6.2 Thủy Ma Always-Active + PassiveThreshold; §6.3 Turn citation → MATCH3 §8.1; new §6.4 identity table |
| `COMBAT_RULES.md` → v1.2 | §3.4 Boss Damage — defender Element = active Pet; duplicate §3.2 resolved (Boss Damage moved to §3.4 after Crit) |
| `SIGNALR_PROTOCOL.md` → v2.1 | `sourceId` examples → display-name BossId; identity note citing BOSS §6.4 |
| `GAME_STATE.md` → v2.1 | §2.4.3 Turn citation → MATCH3 §8.1 / §5.1 |

During implementation, if a genuine contract inconsistency is found:
```
STOP → report the mismatch → do not silently modify documentation
```

---

## 10. Definition of Done

- [x] `BossState` has `PassiveId`, `PassiveProgress`, `SkillCharge`, `SkillCooldown` fields
- [x] `BossDefinition` has all mechanic configuration fields
- [x] `BossDefinitions` uses `BOSS_RULES.md` §6.4 identities (PassiveId/SkillId/BossId) and §6.2–§6.3 values (Thủy Ma PassiveThreshold = 0)
- [x] `PassiveChargedEvent`/`PassiveTriggeredEvent` have `Source`/`SourceId` fields
- [x] `DamagePipeline.Calculate` accepts `source`/`target` direction parameters (generalized — no second pipeline / no duplicate formula)
- [x] Boss→Player damage writes `Player.HP` in the same write-back as other state
- [x] `BattleEvent` supports `BossSkillCast`, `BattleWon`, `BattleLost` types
- [x] `BattleStateService.ExecuteSwap` orchestrates full §17 loop including Boss Response
- [x] **No second Turn++** — only SwapExecutor increments Turn; `SkillCooldown--` runs once after SwapExecutor returns
- [x] Boss Passive uses shared `PassiveCharged`/`PassiveTriggered` with `source="boss"`; Thủy Ma skips `PassiveTracker.Charge`
- [x] Boss Skill uses `Attack = Boss.ATK + BossDefinition.SkillBaseDamage`
- [x] Boss Basic Attack uses `Attack = Boss.ATK` (no SkillBaseDamage); Element defender = active Pet
- [x] Boss Skill and Basic Attack are mutually exclusive
- [x] Boss does not attack if HP = 0 (Player killed Boss) — Enrage still evaluated first
- [x] Enrage triggers at `BossHP < MaxHP × EnrageThreshold`, does not re-trigger, fires before terminal check
- [x] `SkillCooldown` decrements once per committed Swap after SwapExecutor returns (post-Turn++), not at end
- [x] `PassiveProgress` and `SkillCharge` do NOT cross-reset
- [x] `BattleWon`/`BattleLost` carry `finalBossHp`/`finalPlayerHp`
- [x] `BattleEventWireProjection` projects `source`/`sourceId` on PassiveCharged/PassiveTriggered
- [x] All existing tests pass (with updated signatures)
- [x] New tests cover Boss Passive, Boss Skill, Boss Basic Attack, Enrage, Victory/Defeat
- [x] Event ordering matches `GAME_EVENTS.md` §1 + §17 + §6 above
- [x] No unrelated behavior changed
- [x] Documentation not silently modified during implementation

**Test evidence:** Backend 1010 passed / 0 failed (Domain 830, Application 100, Api 79,
Infrastructure 1). Debug and Release builds succeed with 0 errors.

**Implementation note (defender DEF).** `COMBAT_RULES.md` §3.2/§3.4 give the
Boss→Player defending DEF as "the active Pet's DEF", but `PetState`
(`GAME_STATE.md` §2.3) defines no DEF field and no document supplies a Pet DEF
value — the Pet stat stage is unimplemented. Rather than invent a value
(`AGENTS.md` §7), the Boss→Player instance uses `PlayerState.DEF` (documented MVP
25, `COMBAT_RULES.md` §1.1), recorded in a code comment at the call site. When the
Pet stat stage lands, that single input changes. No documentation was modified.

---

## 11. Final Readiness Audit

After applying all decisions and verifying against authoritative docs + current source:

**Authoritative docs checked:**
- `GAME_RULES.md` §1 item 4 (battle end), §17 (resolution order, steps 18a–18c, 19)
- `BOSS_RULES.md` §3 (Boss Passive), §4 (Boss Skill), §5.4/§6 (Enrage ordering), §6.2–§6.4 (config + identity), §7 (Events)
- `COMBAT_RULES.md` §3.2 (Defense), §3.4 (Boss Damage — Pet as Element defender)
- `MATCH3_RULES.md` §8.1/§8.3 (Turn begins / single write-back — Turn++ lives in SwapExecutor)
- `PASSIVE_RULES.md` §2 (charging), §3 (alternate triggers), §4 (reset), §7 (events)
- `GAME_STATE.md` §2.4 (BossState fields), §5.1 (single write-back)
- `GAME_EVENTS.md` §2 (event definitions)
- `SIGNALR_PROTOCOL.md` §3.2.16–§3.2.19 (wire schemas; sourceId = display BossId)

**Current source checked:**
- `SwapExecution.cs` — `Turn = state.Turn + 1` already in write-back (do not duplicate)
- `BattleStateService.ExecuteSwap` — no Turn++, no SkillCooldown, no Boss Response yet
- `BossState.cs` — 7 fields, missing 4
- `BossDefinition.cs` / `BossDefinitions.cs` — base stats only; display-name BossIds; missing PassiveId/SkillId/config
- `DamagePipeline.cs` — Player→Boss only; needs generalization + Player.HP write path
- `PassiveEvents.cs` — 3-param events, missing Source/SourceId
- `BattleEvent.cs` — 9 event types, missing 3
- `BattleEventWireProjection.cs` — no source/sourceId, no BossSkillCast/BattleWon/BattleLost
- `BossStateKind.cs` — 4 states (Idle/Charging/Enraged/Stunned); Enraged = 2 unused

**Prior blockers — status:**
1. PassiveThreshold (Thủy Ma always-active vs match-count table) — RESOLVED (§3.3, BOSS §6.2)
2. Identity contract (BossId/PassiveId/SkillId/sourceId) — RESOLVED (§3.2, BOSS §6.4, SIGNALR sourceId)
3. DamagePipeline Boss→Player + Pet as Element defender — RESOLVED (§4.4, Phase C/E, COMBAT §3.4)
4. Enrage vs Death ordering — RESOLVED (§3.5/§3.6/§3.10, BOSS §5.4: Enrage first, then terminal)
5. Turn++ / SkillCooldown / citation step-17 — RESOLVED (§3.4, Phase E1: no second Turn++; cooldown after SwapExecutor; citations → MATCH3 §8.1)

**Conflicts found:** None remaining.

```
READY
```
