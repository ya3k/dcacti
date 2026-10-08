# TASK-215A — Pet Tier, Star, and Passive Scope Decision Record

```text
Task ID:            TASK-215A
Type:               DECISION / SCOPE SPECIFICATION (Contract & scope closure record;
                    no production code, test, migration, schema, or API contract change)
Status:             DONE
Risk:               NONE (documentary decision record only; no executable changes)
Priority:           HIGH (authoritative boundary closure for Pet Tier, Star, and Passive)
Primary Agent:      review / gameplay-architecture-decision
Evidence base:      current working tree (docs/, src/, tests/, tasks/)
Authority base:     docs/01-game-design/ (PET_RULES.md, PASSIVE_RULES.md, COMBAT_RULES.md),
                    docs/00-overview/ (MVP_SCOPE.md, GDD.md),
                    docs/02-technical/ (DATABASE.md, GAME_STATE.md, SIGNALR_PROTOCOL.md, API_CONTRACTS.md),
                    docs/03-decisions/ADR/ (ADR-012, ADR-016),
                    prior decisions (TASK-191, TASK-200, TASK-213)
```

---

## 1. Executive Decision Summary

```text
========================================================================================
FEATURE AXIS                 MVP STATUS      EXACT SEMANTICS / BOUNDARY
========================================================================================
Pet Tier                     MVP-IN          FIXED METADATA per owned Pet instance.
                                             Value = Common (enum 0). Display/read-only.
                                             Tier progression / tier-up is EXCLUDED.
----------------------------------------------------------------------------------------
Pet Star                     MVP-IN (Value)  FIXED METADATA per owned Pet instance.
                             DEFERRED (Prog) Value = 1 (Pet.MinStar). Display/read-only.
                                             Star progression / star-up is DEFERRED.
----------------------------------------------------------------------------------------
Passive Definition           MVP-IN          Static definition on PetDefinition:
                                             PassiveId, PassiveThreshold, default reset.
----------------------------------------------------------------------------------------
Passive Presentation/Events  MVP-IN          Match-based charging, threshold tracking,
                                             reset handling, SignalR events (PassiveCharged,
                                             PassiveTriggered), HUD gauge & counter display.
----------------------------------------------------------------------------------------
Passive Mechanical Effects   DEFERRED        All 5 Pet passive mechanical effects are
                                             DEFERRED to FUTURE. No in-battle stat change,
                                             damage, shield, heal, or status effect is applied.
----------------------------------------------------------------------------------------
Passive Progression/Scaling  OUT OF SCOPE    EXCLUDED / FUTURE. No threshold or magnitude
                                             scaling with Level, Star, or Tier.
========================================================================================
```

**Verdict:** `PASS` — The codebase and documentation are reconciled. The boundaries established by TASK-191 Q-11, TASK-200 Q-11, and TASK-213 §6 are confirmed, finalized, and formally closed to prevent future implementation tasks from inventing conflicting mechanics.

---

## 2. Authoritative Source Precedence

Per `AGENTS.md` §2 and repository conflict resolution rules, contradictions across documentation and code are reconciled using the strict hierarchy:

```text
Specific Domain Rules (docs/01-game-design/PET_RULES.md, PASSIVE_RULES.md, COMBAT_RULES.md)
       ↓
Canonical Scope & Overview (docs/00-overview/MVP_SCOPE.md, GDD.md, GAME_RULES.md)
       ↓
Technical Design & Contracts (docs/02-technical/DATABASE.md, GAME_STATE.md, SIGNALR_PROTOCOL.md, API_CONTRACTS.md)
       ↓
Architectural Decisions (docs/03-decisions/ADR/)
       ↓
Tasks (tasks/completed/)
       ↓
Code & Tests (src/, tests/)
```

Where broader documents (`GAME_RULES.md`, `GDD.md`) speak generically of "progression", the more specific domain document (`PET_RULES.md`) and canonical scope authority (`MVP_SCOPE.md`) govern. Code cannot invent mechanics unauthored in domain rules.

---

## 3. Detailed Audit & Decisions

### 3.1 Pet Tier

#### A. Definitions & Storage Locations
* **Domain Rules:** `PET_RULES.md` §3 defines the five tiers (`Common → Rare → Epic → Legendary → Mythic`). Crucially, §3.4 specifies: *"MVP ships one Tier instance per the 5 MVP Pets (see §6); the multi-Tier system described here is the rule framework for future Pet additions."*
* **Database & Domain Entities:** `DATABASE.md` §1 & §3 define `Pet.Tier` on the owned `Pet` instance row (enum `PetTier`, values `{Common, Rare, Epic, Legendary, Mythic}`). `PetDefinition` carries NO Tier column (`DATABASE.md` §1, `PetDefinition.cs:54-115`).
* **Implementation:** `PetTier.cs` defines the enum and explicitly documents (lines 20–22): *"There is no Tier-up, Evolution, or Tier derivation rule in MVP; the multi-Tier framework in §3 is the rule scaffold for future Pet content, while MVP ships one Tier instance per the five MVP Pets (§3 item 4)."*
* **Immutability:** In `Pet.cs:172`, `Tier` is declared as `public PetTier Tier { get; init; }`. It has no setter and cannot be mutated after creation.
* **Creation:** `PlayerStarterGrantFactory.cs:240` grants all starter Pets with `Tier = PetTier.Common`.
* **Delivery & Presentation:** `PetCollectionItem.cs:49-65` and `CollectionResponses.cs:79` project `pet.Tier.ToString()` to the client via `GET /api/collection` (`API_CONTRACTS.md` §5.1). `CollectionViewerScene.ts:664, 775` renders `Tier: ${pet.tier}`.
* **Battle State:** In-battle `PetState` does NOT hold `Tier` (`GAME_STATE.md` §2.3; `SIGNALR_PROTOCOL.md` §4.3 item 2; `PetState.cs:71-78`). Battle combat stats are fixed MVP defaults (`COMBAT_RULES.md` §1.1).

#### B. Exact MVP Semantics
* **Status:** `MVP-IN` as **fixed content metadata** on the owned Pet instance.
* **Semantics:** Every MVP Pet instance is created with `Tier = Common`. Tier is read-only and displayed in out-of-battle collection inspection.
* **Progression:** Tier progression, tier-up, tier promotion, evolution, and tier-based stat/skill scaling are **OUT OF SCOPE / FUTURE**.
* **Contradiction Resolution:** Phrasing in `GAME_RULES.md` §9.3 ("Pets have Level, Star, and Tier progression") and `GDD.md` §6 ("Tier (Common → Mythic)") is general overview language. It is superseded by `PET_RULES.md` §3.4 and `MVP_SCOPE.md` §1 ("Tier is a fixed per-Pet value (PET_RULES.md §3.4 — MVP ships one Tier instance per Pet; no Tier-up rule exists and none is introduced)").

---

### 3.2 Pet Star & Star Progression

#### A. Definitions & Storage Locations
* **Domain Rules:** `PET_RULES.md` §4 defines `Star` in the range 1–5. §4 item 3 explicitly states: *"Exact star-up costs, materials, and stat curves are Meta Progression concerns (GDD §14) and are not defined in this Battle-facing rules document."*
* **Database & Domain Entities:** `DATABASE.md` §1 & §3 define `Pet.Star` as an integer on the owned `Pet` row constrained by `CK_Pet_Star_Range` (`Star >= 1 AND Star <= 5`).
* **Implementation:** `Pet.cs:180` defines `public int Star { get; init; } = MinStar;` with `MinStar = 1` and `MaxStar = 5`. It is `init`-only with no setter and no modification method.
* **Creation:** `PlayerStarterGrantFactory.cs:241` grants starter Pets with `Star = Pet.MinStar` (1).
* **Delivery & Presentation:** `PetCollectionItem.cs:66` and `CollectionResponses.cs:80` project `pet.Star` to `GET /api/collection`. `CollectionViewerScene.ts:664, 776` renders `★${pet.star}` and `Star: ${pet.star}`.
* **Battle State:** `PetState` does not carry `Star` (`GAME_STATE.md` §2.3; `SIGNALR_PROTOCOL.md` §4.3). Battle combat stats ignore Star.

#### B. Exact MVP Semantics
* **Status:**
  * Star attribute value (`Star = 1`): `MVP-IN` (read-only metadata on owned instance).
  * Star progression (star-up): `DEFERRED to FUTURE`.
* **Semantics:** Every MVP Pet has `Star = 1`. No mechanism exists to increase Star.
* **Contradiction & Drift Identification:**
  1. `GAME_RULES.md` §9.3 groups Star under "Level, Star, and Tier progression". Only Pet Level progression is implemented (`PET_RULES.md` §5). Star progression is deferred per `MVP_SCOPE.md` §1 & §3.
  2. `PET_RULES.md` §6 formula `Final Stat = f(Base Stat[Tier], Level Curve[Level], Star Bonus[Star])` is unauthored (function `f` does not exist). In MVP battle, stats are fixed defaults (`COMBAT_RULES.md` §1.1).
  3. Tests in `CollectionEndpointTests.cs` and `CollectionQueryServiceTests.cs` seed fixture rows with `star: 2..5` to test DTO projection across valid ranges. These are projection assertions, not evidence of progression mechanics.
  4. Test double `AuthorityRegressionSuiteTests.cs:339` invokes `harness.SetPetStar(...)` on a fake harness. The test explicitly asserts that changing database Star does NOT affect battle state (`BattleState`).

---

### 3.3 Pet Passive System (Four Discretionary Layers)

The audit distinguishes four distinct layers of the Pet Passive system:

```text
┌────────────────────────────────────────────────────────────────────────┐
│ 1. Passive Definition & Content Metadata (MVP-IN)                      │
│    - PassiveId, PassiveThreshold, TriggerType (Match-based)            │
├────────────────────────────────────────────────────────────────────────┤
│ 2. Passive Trigger & Event Presentation (MVP-IN)                       │
│    - PassiveTracker.Charge, threshold check, full reset                │
│    - SignalR events: PassiveCharged, PassiveTriggered                  │
│    - Client HUD gauge, match counter text, event presenter log        │
├────────────────────────────────────────────────────────────────────────┤
│ 3. Passive Mechanical Effects (DEFERRED TO FUTURE)                     │
│    - Battle state application: Burn/Empower, Shield, Crit, Heal, DEF   │
│    - Zero effect applied in BattleStateService; unauthored magnitudes  │
├────────────────────────────────────────────────────────────────────────┤
│ 4. Passive Progression & Scaling (OUT OF SCOPE / FUTURE)               │
│    - Scaling thresholds or magnitudes by Level, Star, or Tier          │
│    - No rules, no formulas, no implementation                          │
└────────────────────────────────────────────────────────────────────────┘
```

#### Layer 1: Passive Definition & Content Metadata
* **Status:** `MVP-IN` (fully implemented).
* **Content:** All 5 MVP Pets have authored entries in `PASSIVE_RULES.md` §8 and database rows in `PetDefinition`:
  * Xích Lang: `passive-xich-lang` (Threshold: 5)
  * Huyền Quy: `passive-huyen-quy` (Threshold: 6)
  * Bạch Hổ: `passive-bach-ho` (Threshold: 4)
  * Thanh Xà: `passive-thanh-xa` (Threshold: 7)
  * Sơn Hùng: `passive-son-hung` (Threshold: 5)

#### Layer 2: Passive Trigger & Event Presentation
* **Status:** `MVP-IN` (fully implemented).
* **Mechanics:** `PassiveTracker.Charge` calculates match progress during cascades (`PASSIVE_RULES.md` §2). On crossing threshold, triggers are emitted and progress resets.
* **Events & Protocol:** `SIGNALR_PROTOCOL.md` §3.2.16 (`PassiveCharged`), §3.2.17 (`PassiveTriggered`), and §4.3 (`petState.passiveProgress`) deliver state to client.
* **Client Presentation:** `BattleScene.ts:1126-1135` updates the HUD gauge and counter text (`Passive: ${state.petState.passiveId}`, `current / threshold Matches`). `BattleEventPresenter.ts:527-530` logs `PassiveCharged` and `PassiveTriggered`.
* **Presentation Independence:** `SIGNALR_PROTOCOL.md` §3.2.25 items 3–5 intentionally omits effect summaries from wire events. The presentation layer is fully decoupled from mechanical effects and operates cleanly without effect resolution.

#### Layer 3: Passive Mechanical Effects
* **Status:** `DEFERRED to FUTURE` (`MVP_SCOPE.md` §1 & §3).
* **Audit of Backend:** In `BattleStateService.cs:1204-1240`, `charged.Triggers` emits `BattleEvent.ForPassiveTriggered`, but applies NO effect to `PetState` or `BossState`.
* **Contrast with Boss Passives:** Boss passives (Hỏa Long rage, Mộc Yêu regen, Sơn Thạch Vệ, Kim Lôi Vương) ARE executed in `BattleStateService.cs:1608-1637, 1705-1775`. Pet passives are intentionally unexecuted.
* **Why Deferred:**
  1. Product Owner decision TASK-191 Q-11: *"Approve pet-passive magnitudes for Xích Lang and Sơn Hùng (not authored anywhere)? -> APPROVED — REMAIN UNIMPLEMENTED FOR MVP."*
  2. Product Owner decision TASK-200 Q-11: *"Pet Passives — REMAIN UNIMPLEMENTED FOR MVP."*
  3. TASK-213 §6 decision: ratifies deferral of effect layer to FUTURE due to unauthored numeric magnitudes in `PASSIVE_RULES.md` §8 (Burn damage, Shield amount, Crit chance %, Heal %, Defense amount).
  4. Implementing effects without authored numbers would violate `AGENTS.md` §7 (inventing balance values).

#### Layer 4: Passive Progression & Scaling
* **Status:** `OUT OF SCOPE / FUTURE`.
* **Semantics:** Neither threshold nor magnitude scales with Pet Level, Star, or Tier. No scaling formulas exist in repository documentation.

---

## 4. Existing Implementation Audit & Classification

| Component / Artifact | Observed Behavior / Code | Classification | Action / Notes |
|---|---|---|---|
| `src/backend/GameServer.Application/Battle/BattleStateService.cs:1204-1240` | Charges Pet passive, writes back progress, emits `PassiveCharged` & `PassiveTriggered`, applies 0 effects. | **Conformant Existing Behavior** | Conforms exactly to the MVP-IN presentation / DEFERRED effect boundary. |
| `src/backend/GameServer.Application/Battle/BattleStateService.cs:1582-1584` | Stale comment: *"Passive EFFECT application is out of this task's scope for every Boss..."* | **Documentation Drift** | Drift recorded in TASK-213 §11 (site A3); code is correct (Boss effects apply); housekeeping deferred to TASK-217A. |
| `src/backend/GameServer.Domain/Pets/Pet.cs:172, 180` | `public PetTier Tier { get; init; }`<br>`public int Star { get; init; } = MinStar;` | **Conformant Existing Behavior** | Immutable instance properties conform to fixed-metadata decision. |
| `src/backend/GameServer.Application/Players/PlayerStarterGrantFactory.cs:240-241` | Starter grant sets `Tier = PetTier.Common`, `Star = Pet.MinStar`. | **Conformant Existing Behavior** | Matches `DATABASE.md` §3 creation contract. |
| `src/backend/GameServer.Application/Collection/CollectionQueryService.cs:483-487` | Projects `Tier` and `Star` to `GET /api/collection` DTOs. | **Conformant Existing Behavior** | Conforms to read-only collection inspection contract (`API_CONTRACTS.md` §5.1). |
| `src/frontend/client/src/game/scenes/CollectionViewerScene.ts:664, 775-776` | Renders `Tier: Common` and `Star: 1` in collection viewer UI. | **Conformant Existing Behavior** | Conforms to UI display requirement. |
| `src/frontend/client/src/game/scenes/BattleScene.ts:1126-1135` | Renders HUD gauge and counter for active Pet passive. | **Conformant Existing Behavior** | Conforms to in-battle presentation requirement. |
| `docs/01-game-design/COMBAT_RULES.md:526` & `CARD_RULES.md:429` | Text references worked example "+10 percentage points" for Bạch Hổ's passive. | **Documentation Drift / Illustrative Note** | Not an authored configuration value; clarified as illustrative example only. |
| `tests/backend/GameServer.Application.Tests/AuthorityRegressionSuiteTests.cs:339` | Harness helper `harness.SetPetStar(...)` and `harness.SetPetTier(...)`. | **Conformant Test Double** | Test fake asserting that `BattleState` ignores database Pet attributes. |

---

## 5. Explicitly Out of Scope for MVP

To prevent scope creep in future tasks, the following are **strictly OUT OF SCOPE** for MVP:

1. **Tier Progression / Changes:**
   * Any mechanism to upgrade, evolve, promote, or alter a Pet's Tier.
   * Any gameplay formula deriving ATK, DEF, HP, Power, Crit, or skill potency from Tier.
   * Any UI or endpoint allowing Tier modification.
2. **Star Progression / Changes:**
   * Any star-up mechanism, star upgrade currency, duplicate fusing, or ascension.
   * Any gameplay formula deriving combat stats from Star.
   * Any UI or endpoint allowing Star modification.
3. **Pet Passive Mechanical Effects:**
   * Executing mechanical effects on `PassiveTriggered` in `BattleStateService.cs` (no Burn, no Shield, no Crit boost, no Heal, no DEF buff).
   * Modifying `PetState.HP`, `PetState.NextAttackCritModifiers[]`, `PetState.StatusEffects[]`, or `BossState.HP` from a Pet passive trigger.
   * Adding passive effect summaries to SignalR payloads.
4. **Pet Passive Scaling:**
   * Altering passive thresholds or magnitudes via Level, Star, or Tier.
5. **Mid-battle Pet Swapping:**
   * Swapping Pets during combat (`PET_RULES.md` §2 item 3).

---

## 6. Preconditions for Future Deferred Implementation

Before any deferred mechanic can be authorized or implemented in a future task:

### 6.1 Pet Passive Mechanical Effects
* **Task Identifier:** Proposed future task `TASK-215B` (FUTURE, NOT AUTHORIZED).
* **Preconditions:**
  1. Product Owner must author exact numerical magnitudes in `PASSIVE_RULES.md` §8 (e.g., Burn ticks/damage, Shield %, Crit % bonus, Heal %, DEF bonus/duration).
  2. Storage/configuration schema must be defined if values are configuration-driven rather than hard-coded.
  3. Formal amendment to `MVP_SCOPE.md` moving "Per-Pet Passive effect application" from §3 (FUTURE) to §1 (IN) per `GAME_RULES.md` §20 Rule Change Policy.

### 6.2 Pet Star & Tier Progression
* **Preconditions:**
  1. Design specifications for Meta Progression currencies, costs, and materials (`GDD` §14).
  2. Mathematical formulas for stat scaling curves (`PET_RULES.md` §6 function `f`).
  3. Formal amendment to `MVP_SCOPE.md` moving Star / Tier progression from §3 (FUTURE) to §1 (IN).

---

## 7. Verification & Conformance Statement

* **Source Authority Verification:** Reconciled against `PET_RULES.md` §3–§4, `PASSIVE_RULES.md` §8, `COMBAT_RULES.md` §1.1, `MVP_SCOPE.md` §1 & §3, and `DATABASE.md` §1 & §3.
* **Executable Consistency:** Verified that production code currently treats `Tier` and `Star` as immutable `init`-only properties, runs battle combat stats from baseline defaults, charges and triggers passives for presentation without applying unauthored effects, and passes all E2E smoke tests.
* **TASK-213 Alignment:** Completely internally consistent with TASK-213 §6 and `MVP_SCOPE.md` amendments.
* **Scope Discipline:** No production code, tests, migrations, schemas, contracts, or historical task records were modified. No git operations were executed.
```
