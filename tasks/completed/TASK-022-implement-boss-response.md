# TASK-022 — Implement Boss Response (Steps 18a–18c) and Victory/Defeat (Step 19)

**Status:** BACKLOG
**Priority:** P1 — Blocks Phase 1 boss loop completion
**Depends On:** TASK-021 (Damage Pipeline), TASK-020A (Boss Configuration), TASK-009 (Passive Tracker)
**Scope:** Backend only (Domain + Application layers)

---

## 1. Objective

Implement the Boss Response phase (`GAME_RULES.md` §17 steps 18a–18c) and Victory/Defeat determination (step 19) so a committed Swap resolves the full documented loop:

```
Turn++ → SkillCooldown decrement → Board → Player Passive → SkillCharge increment
  → Player→Boss Damage → Enrage check
  → Boss HP == 0 → BattleWon → END (no Boss Response)
  → Boss Passive (18a) → Boss Skill OR Boss Basic Attack (18b–18c, Boss→Player Damage)
  → Victory/Defeat (19)
```

The task delivers:

1. **Boss Passive** (step 18a): per-Boss match-counting passive charged on Player Matches. Uses shared `PassiveCharged`/`PassiveTriggered` events with `source = "boss"` (`GAME_EVENTS.md` §2, `BOSS_RULES.md` §3).
2. **Boss Skill** (step 18b): charges over Player Matches, fires when charged and off cooldown. Applies Boss→Player damage through the Damage Pipeline (`BOSS_RULES.md` §4, §6).
3. **Boss Basic Attack** (step 18c): unconditional Boss→Player damage every turn the Boss does not cast its Skill, using Combo = 1.0 (`GAME_RULES.md` §17, `COMBAT_RULES.md` §3.2).
4. **Victory/Defeat** (step 19): outcome determination after Boss Response, emitting `BattleWon` or `BattleLost` (`GAME_EVENTS.md` §2).

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
| `SkillCooldown` decrements after Skill fires | `SkillCooldown` decrements at each Turn increment (`BOSS_RULES.md` §6.3) | Cooldown decrements at turn start |
| `BattleWonEvent(BossId)` payload | `BattleWon(outcome, finalBossHp, finalPlayerHp)` (`SIGNALR_PROTOCOL.md` §3.2.19) | Missing final HP values |
| `BossDefinition.SkillChargeMatches` | `BossDefinition.SkillChargeRequirement` (`GAME_STATE.md` §2.4.3) | Field name mismatch |
| `BattleEvent` not extended | Must extend for `BossSkillCast`, `BattleWon`, `BattleLost` (`GAME_EVENTS.md` §2) | New event types needed |
| `SkillChargeRequirement=3, SkillCooldownTurns=1` for all bosses | Per-boss values from `BOSS_RULES.md` §6.3 table | Wrong config values |
| Boss Skill `Attack = Boss.ATK` | `Attack = Boss.ATK + BossDefinition.SkillBaseDamage` (`COMBAT_RULES.md` §3 step 1) | Missing Skill base damage term |
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

### 3.2 PassiveId Canonical Values

`BOSS_RULES.md` §6 describes each Boss's Passive conceptually but does not define string identifiers. These are required for `PassiveCharged`/`PassiveTriggered` events and `PassiveTracker.Charge`.

**Canonical Phase 1 identifiers:**
```
Hỏa Long → new PassiveId("boss-hoa-long")
Thủy Ma  → new PassiveId("boss-thuy-ma")
Mộc Yêu  → new PassiveId("boss-moc-yeu")
```

These are **documentation-contract additions** — they must be recorded explicitly rather than silently invented.

### 3.3 Phase 1 Boss Configuration

Per `BOSS_RULES.md` §6.3:

| Boss | PassiveThreshold | SkillBaseDamage | SkillChargeRequirement | SkillCooldownTurns | EnrageThreshold |
|------|-----------------|-----------------|----------------------|-------------------|----------------|
| Hỏa Long | 5 | 150 | 5 | 2 | 0.30 |
| Thủy Ma | 4 | 120 | 4 | 3 | 0.30 |
| Mộc Yêu | 6 | 100 | 6 | 2 | 0.30 |

### 3.4 SkillCooldown Lifecycle

`BOSS_RULES.md` §6.3: "Decrements by 1 at each Turn increment." The authoritative sequence is:

```
ExecuteSwap
    ↓
Turn++
    ↓
if SkillCooldown > 0
    SkillCooldown--
    ↓
Board resolution
    ↓
Player Passive
    ↓
SkillCharge += totalMatches
    ↓
Player → Boss Damage
    ↓
Boss Response
```

When a Boss Skill fires:
```
SkillCharge = 0
SkillCooldown = BossDefinition.SkillCooldownTurns
```

The next Turn increment performs the first cooldown decrement. `BOSS_RULES.md` §6.3 references "GAME_RULES.md §17 step 17" for the decrement — this is a documentation typo; the actual Turn increment is step 1.

### 3.5 Enrage — Permanent State Transition

`BOSS_RULES.md` §5 item 4: "Enrage is a permanent state transition triggered when `BossHP < EnrageThreshold`."

- Evaluated after Player→Boss damage (steps 15–17) and before Boss Response (step 18)
- Uses strict `<` per authoritative wording
- No `BossEnraged` event exists — state change is inferred from event sequence
- If `BossState.State` is already `Enraged`, do not transition again

### 3.6 Boss Death Preempts Boss Response

If `Boss HP == 0` after Player→Boss damage:
- Emit `BattleWon`
- **END** — no Boss Passive, no Enrage, no Boss Skill, no Boss Basic Attack
- No Boss Passive events after Boss death

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

**If Boss dies:**
```
Player → Boss Damage → Boss HP == 0 → BattleWon → END
```
No Boss Passive, Enrage, Skill, or Basic Attack.

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
Boss → Player Damage
    ↓
Player HP == 0 → BattleLost
```

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
| `PassiveId` | §3 | ❌ Missing | **NEEDS NEW** |
| `PassiveThreshold` | §3 | ❌ Missing | **NEEDS NEW** |
| `PassiveResetBehavior` | §3 | ❌ Missing | **NEEDS NEW** |
| `SkillId` | §4 | ❌ Missing | **NEEDS NEW** |
| `SkillBaseDamage` | §4 item 3 | ❌ Missing | **NEEDS NEW** |
| `SkillChargeRequirement` | §4 | ❌ Missing | **NEEDS NEW** |
| `SkillCooldownTurns` | §4 | ❌ Missing | **NEEDS NEW** |
| `EnrageThreshold` | §5 item 1 | ❌ Missing | **NEEDS NEW** |

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
| Boss→Player direction | §3.2 | ❌ Missing | **ADD source/target params** |
| Combo = 1 for Boss | §4 item 3 | ❌ Missing | Pass `Combo = 1` at call site |
| SkillBaseDamage in Attack | §3 step 1 | ❌ Missing | `Attack = Boss.ATK + SkillBaseDamage` |

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
| Validation → Board → Player Passive → Player→Boss Damage | §17 | ✅ Implemented | — |
| Turn++ and SkillCooldown decrement | §17 step 1, §6.3 | ❌ Missing | **NEEDS NEW** |
| SkillCharge increment per Player Match | §6.3 | ❌ Missing | **NEEDS NEW** |
| Enrage check after Player→Boss damage | §5 item 4 | ❌ Missing | **NEEDS NEW** |
| Boss HP terminal check (Victory) | §17 step 18 | ❌ Missing | **NEEDS NEW** |
| Boss Passive charge (step 18a) | §17 | ❌ Missing | **NEEDS NEW** |
| Boss Skill or Basic Attack (steps 18b–18c) | §17 | ❌ Missing | **NEEDS NEW** |
| Boss→Player damage via Damage Pipeline | §17 | ❌ Missing | **NEEDS NEW** |
| Victory/Defeat check (step 19) | §17 | ❌ Missing | **NEEDS NEW** |

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

Per-boss values from `BOSS_RULES.md` §6.3:

| Boss | PassiveId | PassiveThreshold | SkillBaseDamage | SkillChargeRequirement | SkillCooldownTurns | EnrageThreshold |
|------|-----------|-----------------|-----------------|----------------------|-------------------|----------------|
| Hỏa Long | `"boss-hoa-long"` | 5 | 150 | 5 | 2 | 0.30 |
| Thủy Ma | `"boss-thuy-ma"` | 4 | 120 | 4 | 3 | 0.30 |
| Mộc Yêu | `"boss-moc-yeu"` | 6 | 100 | 6 | 2 | 0.30 |

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

The complete resolution order becomes:

```csharp
// === Turn housekeeping (step 1) ===
// 1. Increment Turn
// 2. if (boss.SkillCooldown > 0) boss.SkillCooldown--

// === Existing (unchanged) ===
// 3. Execute swap + board resolution (SwapExecutor)
// 4. Player Passive charging (PassiveTracker.Charge)

// === NEW: SkillCharge increment ===
// 5. boss.SkillCharge += totalMatches

// === NEW: Player→Boss Damage ===
// 6. DamagePipeline.Calculate(boss, inputs, ..., source=Player, target=Boss)
//    where inputs.Attack = player.ATK, inputs.Combo = player.Combo

// === NEW: Enrage check (after Player→Boss damage, before Boss Response) ===
// 7. if (boss.HP < boss.MaxHP * bossDef.EnrageThreshold && boss.State != Enraged)
//        boss = boss with { State = BossStateKind.Enraged }

// === NEW: Boss HP terminal check (Victory) ===
// 8. if (boss.HP == 0)
//        → emit BattleWon(boss.HP, player.HP)
//        → write back, RETURN (skip Boss Response entirely)

// === NEW: Boss Passive (step 18a) ===
// 9. var bossPassiveResult = PassiveTracker.Charge(
//        boss.PassiveProgress, totalMatches,
//        bossDef.PassiveId, bossDef.PassiveResetBehavior)
//    Update boss.PassiveProgress
//    Append PassiveCharged/PassiveTriggered events with source="boss"

// === NEW: Boss Response (steps 18b–18c) ===
// 10. if (boss.SkillCharge >= bossDef.SkillChargeRequirement
//        && boss.SkillCooldown == 0)
//        → Boss Skill Cast:
//          - emit BossSkillCast(skillId, bossId)
//          - var bossInputs = new DamageInputs(
//                Attack: boss.ATK + bossDef.SkillBaseDamage,
//                BaseDamagePool: 0,
//                Combo: 1,
//                AttackerElement: boss.Element,
//                DefenderElement: playerPet.Element,
//                DefenderDefense: player.DEF)
//          - DamagePipeline.Calculate(boss, bossInputs, ..., source=Boss, target=Player)
//          - boss.SkillCharge = 0
//          - boss.SkillCooldown = bossDef.SkillCooldownTurns
//     else
//        → Boss Basic Attack:
//          - var bossInputs = new DamageInputs(
//                Attack: boss.ATK,
//                BaseDamagePool: 0,
//                Combo: 1,
//                AttackerElement: boss.Element,
//                DefenderElement: playerPet.Element,
//                DefenderDefense: player.DEF)
//          - DamagePipeline.Calculate(boss, bossInputs, ..., source=Boss, target=Player)

// === NEW: Victory/Defeat (step 19) ===
// 11. if (player.HP == 0)
//         → emit BattleLost(boss.HP, player.HP)

// === State write-back ===
// 12. Write back all state (single write-back per GAME_STATE.md §5.1)
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
- Boss Passive charges on Player Matches, triggers at Threshold
- Boss Passive uses shared events with `source="boss"`
- Boss Skill casts when `SkillCharge >= Requirement && SkillCooldown == 0`
- Boss Skill resets `SkillCharge = 0`, sets `SkillCooldown = CooldownTurns`
- Boss Skill uses `Attack = Boss.ATK + SkillBaseDamage`
- Boss Basic Attack when Skill not charged
- Boss Basic Attack uses `Attack = Boss.ATK`
- Boss Skill and Basic Attack are mutually exclusive
- Enrage triggers at `BossHP < MaxHP × EnrageThreshold`, does not re-trigger
- Boss does not attack if HP = 0 (Player killed Boss)
- `SkillCharge` increments per Player Match
- `SkillCooldown` decrements at turn start (not end)
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

`GAME_EVENTS.md` §1 + §17 steps 15–19:

```
1.  MatchCreated / CascadeCreated / ComboChanged / GemMatched     (board resolution)
2.  PassiveCharged (per Match) / PassiveTriggered (at most once)   (Player Passive, source="pet")
3.  DamageCalculated / DamageDealt / DamageTaken                   (Player→Boss)
4.  [Enrage state transition — no event]                           (BOSS_RULES.md §5)
5.  [Boss HP terminal check]                                       (BOSS_RULES.md §3.6)
    IF Boss HP == 0:
        BattleWon → END
        SKIP steps 6–8
6.  PassiveCharged (per Match) / PassiveTriggered (at most once)   (Boss Passive, source="boss")
7.  BossSkillCast                                                  (only if Skill fires, only if Boss survived)
8.  DamageCalculated / DamageDealt / DamageTaken                   (Boss→Player, if Boss attacked)
9.  BattleWon or BattleLost                                        (if outcome determined after Boss Response)
```

Key rules:
- Step 4 Enrage fires BEFORE Boss HP check — state is set even if Boss HP == 0 (irrelevant but consistent)
- Step 5 Boss HP check preempts Boss Response: if Player damage killed the Boss, no Boss Passive, no Boss Skill, no Boss Basic Attack
- `BossBasicAttack` is NOT an event — boss basic attack produces `DamageCalculated`/`DamageDealt`/`DamageTaken`
- `BossEnraged` is NOT an event — state change is inferred from `BossState.State`
- `BossPassiveCharged`/`BossPassiveTriggered` are NOT events — Boss Passive uses shared `PassiveCharged`/`PassiveTriggered` with `source="boss"`

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
| Boss Passive effect application | `GAME_EVENTS.md` §2 item 3 defers effect summary to Combat stage | Wait for Passive effect system |
| Per-Boss Passive identity values | `BOSS_RULES.md` §6 describes passives conceptually but does not define `PassiveId` string values | Canonical values defined in §3.2 above: `"boss-hoa-long"`, `"boss-thuy-ma"`, `"boss-moc-yeu"` — this is a documentation-contract addition |

---

## 9. Documentation Updates

Documentation changes: none expected.

If implementation reveals a genuine contract inconsistency:
```
STOP → report the mismatch → do not silently modify documentation
```

---

## 10. Definition of Done

- [ ] `BossState` has `PassiveId`, `PassiveProgress`, `SkillCharge`, `SkillCooldown` fields
- [ ] `BossDefinition` has all mechanic configuration fields
- [ ] `BossDefinitions` has per-boss values from `BOSS_RULES.md` §6.3 with canonical PassiveId strings
- [ ] `PassiveChargedEvent`/`PassiveTriggeredEvent` have `Source`/`SourceId` fields
- [ ] `DamagePipeline.Calculate` accepts `source`/`target` direction parameters
- [ ] `BattleEvent` supports `BossSkillCast`, `BattleWon`, `BattleLost` types
- [ ] `BattleStateService.ExecuteSwap` orchestrates full §17 loop including Boss Response
- [ ] Boss Passive uses shared `PassiveCharged`/`PassiveTriggered` with `source="boss"`
- [ ] Boss Skill uses `Attack = Boss.ATK + BossDefinition.SkillBaseDamage`
- [ ] Boss Basic Attack uses `Attack = Boss.ATK` (no SkillBaseDamage)
- [ ] Boss Skill and Basic Attack are mutually exclusive
- [ ] Boss does not attack if HP = 0 (Player killed Boss)
- [ ] Enrage triggers at `BossHP < MaxHP × EnrageThreshold`, does not re-trigger
- [ ] `SkillCooldown` decrements at turn start (after Turn++), not at end
- [ ] `PassiveProgress` and `SkillCharge` do NOT cross-reset
- [ ] `BattleWon`/`BattleLost` carry `finalBossHp`/`finalPlayerHp`
- [ ] `BattleEventWireProjection` projects `source`/`sourceId` on PassiveCharged/PassiveTriggered
- [ ] All existing tests pass (with updated signatures)
- [ ] New tests cover Boss Passive, Boss Skill, Boss Basic Attack, Enrage, Victory/Defeat
- [ ] Event ordering matches `GAME_EVENTS.md` §1 + §17
- [ ] No unrelated behavior changed
- [ ] Documentation not silently modified

---

## 11. Final Readiness Audit

After applying all decisions and verifying against authoritative docs + current source:

**Authoritative docs checked:**
- `GAME_RULES.md` §17 (resolution order, steps 18a–18c, 19)
- `BOSS_RULES.md` §3 (Boss Passive), §4 (Boss Skill), §5 (Boss State/Enrage), §6 (MVP config), §7 (Events)
- `COMBAT_RULES.md` §3 (Damage Pipeline, step 1 composition)
- `PASSIVE_RULES.md` §2 (charging), §4 (reset), §7 (events)
- `GAME_STATE.md` §2.4 (BossState fields)
- `GAME_EVENTS.md` §2 (event definitions)
- `SIGNALR_PROTOCOL.md` §3.2.16–§3.2.19 (wire schemas)

**Current source checked:**
- `BossState.cs` — 7 fields, missing 4
- `BossDefinition.cs` — 5 fields, missing 9
- `BossDefinitions.cs` — 3 MVP bosses, missing config
- `DamagePipeline.cs` — hardcoded Player→Boss direction
- `PassiveEvents.cs` — 3-param events, missing Source/SourceId
- `BattleEvent.cs` — 9 event types, missing 3
- `BattleEventWireProjection.cs` — no source/sourceId, no BossSkillCast/BattleWon/BattleLost
- `BossStateKind.cs` — 4 states (Idle/Charging/Enraged/Stunned)

**Conflicts found:** None.

```
READY
```
