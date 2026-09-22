# TASK-020A — Resolve Boss State & MVP Boss Configuration Contract

---

## Metadata

```text
Task ID:           TASK-020A
Type:              DOCUMENTATION / CONTRACT
Status:            DONE
Risk:              MEDIUM
Priority:          HIGH
Primary Agent:     review
Supporting Agents: gameplay, backend
Workflow:          documentation/documentation-change.md
Skills:            documentation-discovery, documentation-consistency
Dependencies:      None
Blockers:          None
```

---

## Objective

Resolve all documentation-level contract gaps identified in the TASK-020 audit before TASK-020 implementation begins. This task modifies **documentation only** — no source-code implementation is allowed.

---

## Authoritative Sources

| Document | Section | Relevance |
|----------|---------|-----------|
| `docs/01-game-design/BOSS_RULES.md` | §1, §5, §6 | Boss structure, Boss State enum, MVP boss reference |
| `docs/01-game-design/COMBAT_RULES.md` | §1.2 | Boss stat categories, stat defaults |
| `docs/01-game-design/GAME_RULES.md` | §1.1, §15 | One Boss per battle, Boss rules |
| `docs/01-game-design/ELEMENT_RULES.md` | §2.2, §6 | Element Modifier, Pet/Element assignments |
| `docs/02-technical/GAME_STATE.md` | §2.3, §2.4 | PetState, BossState contract |
| `docs/02-technical/API_CONTRACTS.md` | §3 | `POST /api/battle/start` — already has `bossId` |
| `docs/02-technical/GAME_EVENTS.md` | §2 | `BattleStarted` event includes `BossId` |
| `docs/02-technical/ARCHITECTURE.md` | §3 | Domain layer modules, Boss domain boundary |
| `docs/02-technical/REDIS_STATE.md` | §3, §7 | Redis persistence staging |
| `docs/02-technical/SIGNALR_PROTOCOL.md` | §4 | Wire delivery of BattleState |
| `docs/00-overview/MVP_SCOPE.md` | §1 | 5 Bosses in scope, Element/Passive/Skill per Boss |

---

## Current Conflicts

### Conflict 1: BossState Enum Terminology

Three documents define Boss State enum members with different names:

| Document | Section | States Listed |
|----------|---------|---------------|
| `BOSS_RULES.md` | §1 | Idle, Charging, Enraged, Stunned |
| `COMBAT_RULES.md` | §1.2 | Idle, ChargingSkill, Enraged |
| `GAME_STATE.md` | §2.4 | Idle, Charging, Enraged (references BOSS_RULES §5) |

The differences:
- `COMBAT_RULES.md` uses **ChargingSkill** where `BOSS_RULES.md` uses **Charging**.
- `BOSS_RULES.md` includes **Stunned**; `COMBAT_RULES.md` and `GAME_STATE.md` do not list it.
- `GAME_STATE.md` references `BOSS_RULES.md §5` as the source of truth.

**Resolution:** Per the source-of-truth hierarchy (`AGENTS.md §2`), `BOSS_RULES.md` is the specific domain rule that owns Boss State. `COMBAT_RULES.md` is a dependent reference. The canonical vocabulary is:

```
Idle
Charging
Enraged
Stunned
```

`COMBAT_RULES.md §1.2` has a documentation bug: "ChargingSkill" must be corrected to "Charging". `GAME_STATE.md §2.4` must be updated to list all four states explicitly rather than deferring to an "e.g." example.

### Conflict 2: Boss Base Stats Not Defined

`COMBAT_RULES.md §1.2` lists stat categories (HP, Max HP, ATK, DEF) but provides **no default numeric values** for Bosses. `TASK-020` proposes `HP ~5× player, ATK ~2× player, DEF ~2× player`, but these are AI-generated guesses, not documented values.

**Resolution:** No authoritative document defines MVP Boss base stats. This is a genuine **gameplay balance decision required from the project owner**. The contract task must mark these values as unresolved and leave an explicit decision placeholder.

### Conflict 3: Boss Initialization Path Incomplete

`API_CONTRACTS.md §3` already defines `bossId` as a required field in `POST /api/battle/start`. However:
- No validation rule is documented for `bossId`.
- No documentation explains how `bossId` maps to a Boss definition (stats, Element, Passive, Skill).
- No documentation explains where Boss configuration is resolved or owned.
- `BattleState.Create` factory does not accept `BossState` (currently only accepts `PetState`).

**Resolution:** `API_CONTRACTS.md` needs a validation rule for `bossId` (must be a valid MVP Boss). The boss configuration ownership question must be resolved (see §9).

### Conflict 4: PetState.Element Not Implemented but Documented

`GAME_STATE.md §2.3` documents `Element` as a `PetState` field. The current `PetState` code (`PetState.cs`) does not implement it. `PET_RULES.md §1` also defines Element as part of the Pet data model.

**Resolution:** This is a code-vs-docs gap, not a docs-vs-docs conflict. The documentation is correct and consistent. The contract task should confirm consistency and note that TASK-020 will implement it.

### Conflict 5: Staged BossState Fields

`TASK-020` proposes `PassiveProgress` and `StatusEffects[]` on `BossState` as "OUT OF SCOPE (owned by later systems)." This follows the same staging convention used for `PlayerState` (§2.2) where `StatusEffects[]`, `EquippedRelics[]`, `EquippedCards[]` are documented but not implemented.

**Resolution:** The staging convention is valid and supported by the existing `PlayerState` precedent. `PassiveProgress` and `StatusEffects[]` on `BossState` should be documented as "not yet implemented, not not required" — same pattern as `PlayerState`.

---

## Required Decisions

### Decision 1: BossState Enum — APPROVED

```text
Canonical states: Idle, Charging, Enraged, Stunned
Source of truth: BOSS_RULES.md §1
Dependent correction: COMBAT_RULES.md §1.2 (ChargingSkill → Charging)
GAME_STATE.md §2.4: must list all four states explicitly
```

### Decision 2: MVP Boss Base Stats — BLOCKED

```text
MVP Boss base stats require project-owner approval.
TASK-020 implementation remains blocked until these values are resolved.

No authoritative document defines HP, ATK, or DEF values for any Boss.
The following fields are UNRESOLVED for every MVP Boss:
  - HP / MaxHP
  - ATK
  - DEF

The project owner must provide these values before TASK-020
can supply MVP boss configuration defaults.
```

### Decision 3: Boss Initialization — APPROVED (no new API needed)

```text
POST /api/battle/start already accepts bossId.
No new endpoint, SignalR method, or Redis behavior is required.

The initialization path is:
1. Client sends bossId in POST /api/battle/start
2. Application layer resolves bossId → Boss definition (stats, Element)
3. BossState is created from the definition
4. BattleState.Create accepts BossState (like it accepts PetState)
```

### Decision 4: PetState.Element — APPROVED

```text
PetState.Element is already documented in GAME_STATE.md §2.3 and PET_RULES.md §1.
No documentation change needed — it is consistent and correct.
TASK-020 will implement it.
```

### Decision 5: Staged BossState Fields — APPROVED

```text
BossState implements NOW:
  BossId, Element, HP, MaxHP, ATK, DEF, State

BossState implements LATER (same staging as PlayerState):
  PassiveProgress — owned by Boss Passive system
  StatusEffects[] — owned by Status Effects system

Convention: "not yet implemented, not not required"
```

---

## Documentation Changes

### 1. `docs/01-game-design/COMBAT_RULES.md`

**Section 1.2, line 38:** Fix terminology conflict.

Change:
```
Boss "State" is an internal enum (e.g. Idle / ChargingSkill / Enraged) used by
```
To:
```
Boss "State" is an internal enum (Idle / Charging / Enraged / Stunned — see BOSS_RULES.md §1) used by
```

### 2. `docs/01-game-design/BOSS_RULES.md`

**Section 1, line 22:** Already correct. No change needed.

**Section 6:** Already documents three MVP bosses. Confirm no change needed.

### 3. `docs/02-technical/GAME_STATE.md`

**Section 2.4, line 923:** Update BossState contract to list all four states explicitly and clarify staging.

Change the State field from:
```
│  State                  (Idle/Charging/Enraged — BOSS_RULES.md §5)
```
To:
```
│  State                  (Idle/Charging/Enraged/Stunned — BOSS_RULES.md §1)
```

Add staging note for PassiveProgress and StatusEffects[]:
```
├── PassiveProgress        (not yet implemented — owned by Boss Passive system)
└── StatusEffects[]        (not yet implemented — owned by Status Effects system)
```

### 4. `docs/02-technical/API_CONTRACTS.md`

**Section 3, validation rules:** Add `bossId` validation.

After line 103 (`relicLoadout must be 3–5 Relics owned by the player`), add:
```
bossId must be a valid MVP Boss                         — BOSS_RULES.md §6
```

### 5. `docs/02-technical/REDIS_STATE.md`

**Section 7 (Foundation State NOT Stored):** No change required. BossState follows the same staging as PlayerState — written to Redis when the full BattleState is stored.

### 6. `docs/02-technical/SIGNALR_PROTOCOL.md`

**Section 4 (BattleStateUpdated):** No change required for MVP. BossState wire delivery is explicitly OUT OF SCOPE for TASK-020.

---

## Completion Criteria

```text
[x] BossState enum vocabulary is consistent across all docs (BOSS_RULES, COMBAT_RULES, GAME_STATE)
[x] COMBAT_RULES.md §1.2 "ChargingSkill" corrected to "Charging"
[x] GAME_STATE.md §2.4 lists all four states: Idle, Charging, Enraged, Stunned
[x] GAME_STATE.md §2.4 clarifies PassiveProgress/StatusEffects[] staging
[x] API_CONTRACTS.md §3 adds bossId validation rule
[x] No stale "ChargingSkill" references remain anywhere in docs/
[x] No undocumented balance assumptions exist in any doc
[x] TASK-020 updated with resolved contracts
[x] TASK-020A created with this content
[x] Source code modified: NO
```

---

## Stop Conditions

- If `BOSS_RULES.md §1` is found to contradict itself: STOP per `AGENTS.md §4`
- If `MVP_SCOPE.md` lists Boss content that conflicts with `BOSS_RULES.md §6`: STOP per `AGENTS.md §8`
- If the project owner provides Boss base stats after this task completes: reopen only the stat-related section, not the full contract

---

## Validation

After documentation changes:

1. Search `docs/` for "ChargingSkill" — returns 0 results ✓
2. Search `docs/` for "Boss" + "State" + enum references — all use Idle/Charging/Enraged/Stunned ✓
3. `BOSS_RULES.md §1`, `COMBAT_RULES.md §1.2`, `GAME_STATE.md §2.4` all agree ✓
4. `API_CONTRACTS.md §3` documents `bossId` validation ✓
5. No undocumented Boss balance assumptions — stats documented in `BOSS_RULES.md §6.1` ✓
6. Source code modified: **NO** ✓

---

## Completion Evidence

### Summary

Resolved all documentation-level contract gaps for TASK-020 (BossState implementation). Six contract gaps identified in the TASK-020 audit were analyzed and resolved: BossState enum terminology, MVP boss base stats, MVP boss definitions, boss initialization path, PetState.Element, and staged BossState fields. The Boss base stats (HP=5000, ATK=100, DEF=50) were approved by the project owner and documented in BOSS_RULES.md §6.1.

### Changes

- `COMBAT_RULES.md §1.2`: Corrected "ChargingSkill" → "Charging", added "Stunned"
- `GAME_STATE.md §2.4`: Listed all four states explicitly (Idle/Charging/Enraged/Stunned), added staging notes for PassiveProgress/StatusEffects[]
- `API_CONTRACTS.md §3`: Added `bossId` validation rule
- `BOSS_RULES.md §6.1`: Added approved MVP Boss base stats table
- `TASK-020`: Updated Status to BACKLOG, removed blockers, replaced unresolved stats with approved values

### Files Changed

- `docs/01-game-design/COMBAT_RULES.md`
- `docs/01-game-design/BOSS_RULES.md`
- `docs/02-technical/GAME_STATE.md`
- `docs/02-technical/API_CONTRACTS.md`
- `tasks/backlog/TASK-020-implement-boss-state-contract.md`

### Docs Consulted

- `docs/01-game-design/BOSS_RULES.md` (authoritative for Boss structure, §1, §5, §6)
- `docs/01-game-design/COMBAT_RULES.md` (§1.2 for Boss stats)
- `docs/01-game-design/GAME_RULES.md` (§1.1, §15 for Boss rules)
- `docs/01-game-design/ELEMENT_RULES.md` (§2.2, §6 for Element Modifier, Pet assignments)
- `docs/02-technical/GAME_STATE.md` (§2.3, §2.4 for PetState, BossState contracts)
- `docs/02-technical/API_CONTRACTS.md` (§3 for battle start endpoint)
- `docs/02-technical/GAME_EVENTS.md` (§2 for BattleStarted event)
- `docs/02-technical/ARCHITECTURE.md` (§3 for Domain layer structure)
- `docs/02-technical/REDIS_STATE.md` (§3, §7 for Redis staging)
- `docs/02-technical/SIGNALR_PROTOCOL.md` (§4 for wire delivery)
- `docs/00-overview/MVP_SCOPE.md` (§1 for Boss scope)
- Source code: `BattleState.cs`, `PlayerState.cs`, `PetState.cs`, `Element.cs`

### Validation

- `ChargingSkill` references in `docs/`: 0
- Boss state vocabulary consistent: Yes (Idle/Charging/Enraged/Stunned in all 3 docs)
- Approved Boss stats documented: Yes (BOSS_RULES.md §6.1)
- No undocumented Boss balance assumptions: Yes
- Source code modified: NO

### Remaining Issues

None. All documentation contracts are resolved.
