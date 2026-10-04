# TASK-173 — Apply Approved Product Owner Decisions for Sơn Thạch Vệ Persistent Boss Passive Once-Per-Battle Semantics to Authoritative Documentation

<!--
  GEN-TASK EXECUTION MANIFEST — DOCUMENTATION-AMENDMENT TASK
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ and task files by path and section — it does NOT copy game
  rules, formulas, magnitudes, schemas, or contracts.

  THIS TASK APPLIES AN ALREADY-RECORDED PRODUCT OWNER DECISION.
  It decides nothing, implements no runtime code, provisions nothing, changes no
  database schema, and introduces no new state members.

  PROVENANCE: During the runtime execution implementation of the two remaining
  MVP Boss Passives at Step 18a, an internal contract gap was uncovered in
  BOSS_RULES.md §6.2.4: Sơn Thạch Vệ's Passive ("son-thach-ve-enrage") is specified
  as one-time ("it does not re-trigger once it has activated") with a 3-Turn duration,
  but §6.2.4 simultaneously declared that "The retrigger guard is the authored
  one-time behavior above, not a new state field." When the 3-Turn effect expired at
  Step 19a, the transient StatusEffects[] instance was removed, leaving no durable
  record that the Passive had already fired; consequently, subsequent Turns where
  Boss HP remained <= 50% re-triggered the Passive. The Product Owner was presented
  with the decision options (Option A: durable state field vs. Option B: Boss-scoped
  once-per-battle semantics for the existing Persistent token) and explicitly approved
  Option B. This task formalizes that decision into the canonical documentation.

  BOUNDARY: documentation only. Exactly two primary files under docs/01-game-design/
  (PASSIVE_RULES.md §4 and BOSS_RULES.md §6.2.4) plus narrow cross-reference checks
  in docs/02-technical/ (DATABASE.md §1 and GAME_STATE.md §2.4). Zero files under src/.
  Zero files under tests/. No database migration. No state member added. No change to
  Pet Passive semantics. No new enum or token introduced.
-->

---

## Metadata

```text
Task ID:           TASK-173
Type:              DOCUMENTATION (TASK_TYPES.md §2 — "Change docs/ content — documentation is the primary output, not code"; workflow documentation/documentation-change.md)
Status:            DONE
Risk:              MEDIUM (amends the canonical Reset Behavior contract in PASSIVE_RULES.md §4 to define Boss-scoped once-per-battle firing eligibility for Persistent, updates BOSS_RULES.md §6.2.4 to resolve the retrigger contract gap, and aligns cross-references in DATABASE.md and GAME_STATE.md. No runtime code, schema, migration, or state member changes.)
Priority:          HIGH (the sole unblocking input for completing runtime execution of the remaining MVP Boss Passives under GAME_RULES.md §17 step 18a and closing the Sơn Thạch Vệ retrigger defect without guessing game design.)
Primary Agent:     review (TASK_TYPES.md §2 / AGENT_SELECTION.md — Documentation change: Primary Agent Review; transcribes and aligns canonical documentation from an approved Product Owner decision)
Supporting Agents: gameplay (consulted for PASSIVE_RULES.md §4 and BOSS_RULES.md §6.2.4 domain accuracy)
Workflow:          documentation/documentation-change.md
Skills:            discovery/documentation-discovery,
                   discovery/impact-analysis,
                   gameplay/gameplay-behavior-derivation,
                   quality/documentation-consistency,
                   quality/scope-validation
Dependencies:      TASK-171 (READY — captured initial Boss specifications),
                   TASK-172 (BACKLOG/applied in docs — authored Boss specifications into BOSS_RULES.md §6),
                   PO Decision: Option B (approved — defines once-per-battle Boss-scoped Persistent semantics)
Blocks:            Runtime implementation task for Sơn Thạch Vệ retrigger guard resolution in BattleStateService.cs
Estimate:          Simple (5 skills, 2 primary document edits, 2 narrow cross-reference checks; zero code, zero schema, zero tests)
```

---

## 1. Objective

Formally amend the authoritative game-design documentation (`PASSIVE_RULES.md` §4 and `BOSS_RULES.md` §6.2.4) and dependent technical documentation (`DATABASE.md` §1 note item 3 and `GAME_STATE.md` §2.4) to record the approved Product Owner decision:

For **Boss Passives only**, the existing `Persistent` reset behavior token (Domain enum: `PassiveResetBehavior.NoReset`) defines **once-per-battle firing eligibility**. Once a Persistent Boss Passive activates during a battle, its firing eligibility is permanently consumed for the remainder of that battle; expiry of its 3-Turn effect does not restore eligibility, and remaining at or below the 50% HP threshold does not trigger another activation. A new battle creates fresh eligibility. No new `BossState` member is introduced.

---

## 2. Background / Problem

During the runtime connection of the two remaining MVP Boss Passives (`son-thach-ve-enrage` and `kim-loi-vuong-combo`) to `BattleStateService.cs` at Boss Response Step 18a (`GAME_RULES.md` §17 step 18a), an internal documentation contradiction was uncovered for Sơn Thạch Vệ:

1. `BOSS_RULES.md` §6.2.4 defines Sơn Thạch Vệ's Passive as:
   - Trigger: `Boss HP <= 50%` of MaxHP (post-damage evaluation, inclusive `≤`).
   - Effect: `+20% ATK` for 3 Turns as a Turn-based `BuffDebuff` in `BossState.StatusEffects[]`.
   - `PassiveThreshold = null` (non-match-charged).
   - One-time activation: "Because this Passive is authored as **one-time** — it does not re-trigger once it has activated — no second application source exists within a battle, and no refresh or stacking behavior arises."
   - Retrigger guard clause: "The retrigger guard is the authored one-time behavior above, **not a new state field**."

2. When executed in a live multi-turn combat simulation, using the presence of the `son-thach-ve-enrage` instance in `BossState.StatusEffects[]` as the retrigger guard proved defective:
   - Turn 1: Boss HP drops to `<= 1500` (<= 50%). Passive triggers. `son-thach-ve-enrage` applied with `RemainingTurns = 3`. Step 19a decrements to 2.
   - Turn 2: RemainingTurns decrements to 1.
   - Turn 3: RemainingTurns decrements to 0; effect expires and is removed from `ActiveStatusEffects[]`.
   - Turn 4: Boss HP is still `<= 1500`. Since the effect instance is gone and no state field recorded prior activation, the trigger condition `HP <= 50%` evaluated to true and re-activated the Passive for another 3 Turns.
   - Turn 7: The Passive re-activated yet again.

3. All existing carriers in the state model were audited:
   - `BossState.StatusEffects[]`: Transient; expired instances disappear.
   - `BossState.State`: Closed enum (`Idle`, `Charging`, `Enraged`, `Stunned`). Enrage is a separate rule (`HP < EnrageThreshold` with strict `<`) that §6.2.4 explicitly forbids conflating with the Passive.
   - `PassiveProgress(Threshold, Current)`: Initialized to `(0, 0)` for `PassiveThreshold = null`. Overloading `Current` as a fired flag would violate match-charging semantics.
   - `PassiveResetBehavior = Persistent`: Defined in `PASSIVE_RULES.md` §4 item 2 only as a match-charge progress retention rule ("no reset / persistent"), not as a firing limiter.

The task stopped per `AGENTS.md` §20 and the Product Owner was presented with Decision Options A and B.

---

## 3. Approved Product Owner Decision

The Product Owner explicitly selected and approved **Option B**:

> `Persistent` gains **once-per-battle firing eligibility semantics for Boss Passives only**.

### Specifications for Sơn Thạch Vệ (`son-thach-ve-enrage`):
- **Passive ID:** `son-thach-ve-enrage`
- **Trigger:** `Boss HP <= 50%` of MaxHP (inclusive `≤`, evaluated at Step 18a post-damage).
- **Effect:** `+20% ATK` represented as a Turn-based `BuffDebuff` in `BossState.StatusEffects[]` with `TargetStat = "ATK"`, `Magnitude = 20`.
- **Duration:** `3 Turns`.
- **Threshold:** `PassiveThreshold = null` (non-match-charged).
- **Firing Rule:** The Passive may fire at most once per battle.
- **After Effect Expiry:** When the 3-Turn effect expires at Step 19a, firing eligibility remains consumed. The Passive MUST NOT fire again during the same battle, even if Boss HP remains `<= 50%`.
- **New Battle:** A new battle instantiates fresh battle state, resetting firing eligibility.
- **State Representation:** Do NOT add a new `BossState` member. Do NOT repurpose `PassiveProgress.Current` as a fired marker.
- **Scope:** Boss-scoped only. Pet Passives and generic match-charging reset rules are NOT altered.

---

## 4. Canonical Contract Amendment

This task amends the canonical documentation across the documentation hierarchy (`AGENTS.md` §2):

1. **`docs/01-game-design/PASSIVE_RULES.md` §4 ("Reset Behavior"):**
   - Author the explicit Boss Passive contract for `Persistent` (Domain: `NoReset`).
   - Define that for Boss Passives, `Persistent` governs **firing eligibility**: "A Persistent Boss Passive may fire at most once per battle."
   - Explicitly decouple **firing eligibility** from **effect duration**:
     ```text
     Persistent (Boss Passive)
         ├── firing eligibility: at most once per battle
         └── effect duration: independently defined by the Passive (e.g. 3 Turns)
     ```
   - Clarify that `Persistent` does NOT mean the effect instance lasts indefinitely.
   - Maintain Pet Passive semantics unchanged: Pet Passives continue to use §4 item 1/2 for match-charging progress retention.

2. **`docs/01-game-design/BOSS_RULES.md` §6.2.4 ("Sơn Thạch Vệ — Rage on an HP threshold"):**
   - Reaffirm: Trigger is `Boss HP <= 50%`, Effect is `+20% ATK` for 3 Turns, `PassiveThreshold = null`.
   - Author the once-per-battle firing eligibility rule directly referencing `PASSIVE_RULES.md` §4.
   - State that when the 3-Turn effect expires at step 19a, firing eligibility is NOT restored. Remaining at or below 50% HP produces no further activation.
   - State that a new battle resets firing eligibility.
   - Retire/replace the ambiguous sentence: *"The retrigger guard is the authored one-time behavior above, not a new state field."* Replace it with an unambiguous contract statement: *"Under PASSIVE_RULES.md §4's Boss-scoped Persistent reset behavior, firing eligibility is consumed upon first activation and is not re-armed during that battle; this is enforced by runtime dispatch reading the definition's Persistent behavior without introducing a new BossState field."*

3. **`docs/02-technical/DATABASE.md` §1 note item 3 ("BossDefinition / resetBehavior"):**
   - Confirm that the storage token remains `Persistent` and maps to Domain enum `PassiveResetBehavior.NoReset`.
   - Add a brief note clarifying that for Boss Passives, `Persistent` carries the once-per-battle firing eligibility contract defined in `PASSIVE_RULES.md` §4.
   - Confirm: No schema changes, no migration, no column additions.

4. **`docs/02-technical/GAME_STATE.md` §2.4 ("Boss State"):**
   - Ensure `BossState` documentation is aligned: confirm that no new `BossState` field is added for the fired marker, and that firing eligibility is a dispatch-level evaluation governed by the definition's `Persistent` reset behavior.

---

## 5. Exact Semantics

| Dimension | Specification |
|---|---|
| **Passive Identity** | `son-thach-ve-enrage` (`BOSS_RULES.md` §6.4) |
| **Trigger Condition** | `Boss HP <= 50%` of MaxHP (`MaxHP = 3000` -> `HP <= 1500`) |
| **Evaluation Point** | Boss Response Step 18a (`GAME_RULES.md` §17 step 18a), post-damage |
| **Boundary Operator** | Inclusive `≤` (deliberately distinct from strict `<` for Enrage transition §5 item 4) |
| **Passive Threshold** | `null` (non-match-charged; never charged via `PassiveTracker.Charge`) |
| **Status Effect** | Turn-based `BuffDebuff`, `TargetStat = "ATK"`, `Magnitude = 20` |
| **Effect Duration** | `3 Turns` (decremented at Step 19a boundary) |
| **Reset Behavior** | `Persistent` (storage/JSON token) / `NoReset` (Domain enum member) |
| **Firing Eligibility** | **At most once per battle** |
| **Post-Expiry Behavior** | Effect expires after 3 Turns; firing eligibility remains **consumed** |
| **Sustained HP <= 50%** | Does **NOT** re-trigger; eligibility is consumed |
| **New Battle Lifecycle** | Fresh battle creates fresh `BossState`; firing eligibility resets |
| **State Footprint** | **Zero new members** on `BossState`; `PassiveProgress.Current` is **not** overloaded |

---

## 6. Canonical Owners

Per `AGENTS.md` §2 and `.ai/workflow/documentation/documentation-change.md` §1–§3:

1. **`docs/01-game-design/PASSIVE_RULES.md`** — Canonical owner of Passive trigger categories, charge models, and **Reset Behavior** (§4).
2. **`docs/01-game-design/BOSS_RULES.md`** — Canonical owner of Boss statistics, Skills, and **Boss Passive definitions** (§6, §6.2.4).
3. **`docs/02-technical/DATABASE.md`** — Technical owner of database persistence, serialization tokens, and enum mappings (`resetBehavior` storage token `Persistent` -> `NoReset`).
4. **`docs/02-technical/GAME_STATE.md`** — Technical owner of active battle state shape (`BossState`).

---

## 7. Invariants

The documentation amendment MUST strictly preserve the following invariants:
1. **Trigger:** `Boss HP <= 50%` (inclusive comparison).
2. **Magnitude:** `+20% ATK` (adds 20% to effective ATK per `COMBAT_RULES.md` §5.5.1).
3. **Duration:** Exactly 3 Turns.
4. **Threshold:** `PassiveThreshold = null` (not match-charged).
5. **Firing limit:** At most once per battle.
6. **Persistence distinction:** `Effect duration != Passive firing eligibility`.
7. **Effect representation:** Existing Turn-based `BuffDebuff`, `TargetStat = "ATK"`. No new status type.
8. **Closed vocabulary:** Element `Thổ`, Skill `earthquake`, Passive `son-thach-ve-enrage`.
9. **State invariance:** No new member added to `BossState`.
10. **Pet Passive invariance:** Pet Passives are completely unaffected by this Boss-scoped rule.

---

## 8. Scope

### In Scope
- Amending `docs/01-game-design/PASSIVE_RULES.md` §4 to define Boss-scoped once-per-battle firing eligibility for `Persistent`.
- Amending `docs/01-game-design/BOSS_RULES.md` §6.2.4 to author the exact once-per-battle firing contract and retire the ambiguous retrigger-guard phrasing.
- Reviewing and aligning `docs/02-technical/DATABASE.md` §1 note item 3 regarding `Persistent` Boss-scoped semantics.
- Reviewing and aligning `docs/02-technical/GAME_STATE.md` §2.4 to verify no state-field contradiction exists.
- Updating document version headers and revision notes for modified documents.

### Out of Scope
- Any runtime C# code changes (`BattleStateService.cs`, `BossDefinitions.cs`, etc.).
- Any test code changes (`Task172BossPassiveRuntimeTests.cs`, etc.).
- Modifying `Kim Lôi Vương` (`kim-loi-vuong-combo`), which uses `Default` reset and is already complete.
- Modifying Pet Passives or generic match-charge cascading reset rules in `PassiveTracker.cs`.
- Changing database schema, creating EF Core migrations, or touching provisioning scripts.
- Adding any member to `BossState`, `PetState`, or `BattleState`.
- Any SignalR protocol or REST API contract changes.

---

## 9. Explicit Non-Goals

1. **Do NOT implement the runtime fix.** This task is strictly documentation-only. The runtime implementation belongs to the subsequent execution task.
2. **Do NOT invent a new reset token.** The storage token remains `Persistent`; the Domain enum remains `PassiveResetBehavior.NoReset`. No token such as `OncePerBattle` or `NoRetrigger` may be authored.
3. **Do NOT redefine Pet Passives.** The once-per-battle firing eligibility applies exclusively to Boss Passives.
4. **Do NOT add state fields.** The contract must explicitly forbid adding a boolean `HasActivated` or similar field to `BossState`.
5. **Do NOT touch Kim Lôi Vương.** Kim Lôi Vương's Passive is independent, uses `Default` reset, and is already fully verified.

---

## 10. Required Documentation Changes

### 10.1 `docs/01-game-design/PASSIVE_RULES.md`
- In **§4 Reset Behavior**, add a distinct subsection or paragraph for **Boss Passives**:
  - State that for Boss Passives, the `Persistent` reset behavior (storage token `Persistent`, Domain `NoReset`) governs **firing eligibility**.
  - A Persistent Boss Passive is eligible to fire at most once per battle.
  - Upon triggering, its firing eligibility is consumed for the duration of that battle.
  - Distinguish firing eligibility from effect duration: the effect lasts for its authored turn duration (e.g. 3 Turns), but the Passive does not re-fire when the effect expires, even if the trigger condition remains satisfied.
  - Reiterate that Pet Passives remain governed by the match-charging reset rules (§4 item 1/2).

### 10.2 `docs/01-game-design/BOSS_RULES.md`
- In **§6.2.4 Sơn Thạch Vệ — Rage on an HP threshold**:
  - Update the "Duration and reapplication" bullet to reference `PASSIVE_RULES.md` §4's Boss-scoped `Persistent` once-per-battle firing eligibility rule.
  - Explicitly state:
    1. Triggers at `Boss HP <= 50%` (post-damage evaluation at step 18a).
    2. Applies `+20% ATK` for 3 Turns.
    3. Firing eligibility is consumed upon first activation.
    4. When the 3-Turn effect expires at step 19a, the Passive does NOT re-trigger, even if Boss HP remains `<= 50%`.
    5. A new battle resets firing eligibility.
  - In the "No new state or protocol" bullet, replace:
    *"The retrigger guard is the authored one-time behavior above, not a new state field."*
    with:
    *"Firing eligibility is governed by the definition's Persistent reset behavior under PASSIVE_RULES.md §4 and is consumed upon first activation; no new BossState field is added."*

### 10.3 `docs/02-technical/DATABASE.md`
- In **§1 note item 3**:
  - Add a clarifying note that for Boss Passives, the `Persistent` token carries the once-per-battle firing eligibility defined in `PASSIVE_RULES.md` §4 and `BOSS_RULES.md` §6.2.4.

### 10.4 `docs/02-technical/GAME_STATE.md`
- In **§2.4 Boss State**:
  - Ensure documentation reflects that no additional state member is needed to represent Boss Passive firing eligibility under the `Persistent` definition-level contract.

---

## 11. Contradiction Handling

### 11.1 `Persistent` vs. `NoReset` Vocabulary
- **Verification:** An inspection of the repository confirms:
  - Database JSON and persistence DTOs use the string token `"Persistent"` (`DATABASE.md` §1 note item 3).
  - Domain code uses the enum member `PassiveResetBehavior.NoReset` (`src/backend/GameServer.Domain/Passives/PassiveResetBehavior.cs`).
  - `BossDefinition.cs` projects `"Persistent"` to `PassiveResetBehavior.NoReset`.
  - Content-fidelity tests assert both representations: `Assert.Equal("Persistent", passive.ResetBehavior)` and `Assert.Equal(PassiveResetBehavior.NoReset, boss.PassiveResetBehavior)`.
- **Resolution:** There is NO naming defect; `"Persistent"` is the canonical storage/JSON contract token, and `NoReset` is its C# Domain enum projection. `DATABASE.md` §1 note item 3 already authors this mapping explicitly. The documentation amendment must preserve this dual-layer naming exactly as documented and avoid inventing any new names.

### 11.2 Generic Match-Charging Progress vs. Boss Firing Eligibility
- **Conflict:** `PASSIVE_RULES.md` §4 originally defined `No reset / persistent` in the context of match-charging tracker progress (`PassiveTracker.Reset`): `NoReset => current` (do not reduce progress). In `PassiveTrackerTests.cs`, for match-charged passives, `NoReset` left progress intact, causing it to trigger again on subsequent cascades.
- **Resolution:** The documentation amendment in `PASSIVE_RULES.md` §4 explicitly partitions the rule:
  - For **Pet Passives** (match-charged): `Persistent` / `NoReset` retains charge progress across triggers.
  - For **Boss Passives** (threshold / non-match-charged): `Persistent` / `NoReset` defines **once-per-battle firing eligibility**, preventing any re-trigger during the battle.

---

## 12. Acceptance Criteria

- [x] Product Owner Decision (Option B) is explicitly recorded in `PASSIVE_RULES.md` §4 and `BOSS_RULES.md` §6.2.4.
- [x] `Persistent` once-per-battle firing eligibility semantics are explicitly scoped to **Boss Passives only**.
- [x] `son-thach-ve-enrage` is documented as firing at most once per battle.
- [x] Trigger remains `Boss HP <= 50%` (inclusive `≤`).
- [x] Effect remains `+20% ATK` for `3 Turns` as Turn-based `BuffDebuff` (`TargetStat = "ATK"`).
- [x] `PassiveThreshold = null` is preserved.
- [x] Effect expiry after 3 Turns is documented as NOT restoring firing eligibility.
- [x] Persistent HP `<= 50%` across multiple Turns is documented as NOT re-triggering.
- [x] Starting a new battle is documented as restoring firing eligibility.
- [x] No new `BossState` member is introduced.
- [x] `PassiveProgress.Current` is NOT repurposed as a fired marker.
- [x] Pet Passive semantics in `PASSIVE_RULES.md` remain unchanged.
- [x] The relationship between the `"Persistent"` storage token and `PassiveResetBehavior.NoReset` domain enum is preserved.
- [x] Cross-references in `DATABASE.md` and `GAME_STATE.md` are verified and updated if ambiguous.
- [x] Zero lines of runtime C# code (`src/`) are modified.
- [x] Zero lines of test C# code (`tests/`) are modified.
- [x] The task manifest is self-contained and implementable by a fresh documentation agent.

---

## 13. Validation Requirements

1. **Document Cross-Check:** Verify that all references to `Persistent`, `NoReset`, `son-thach-ve-enrage`, and `PassiveResetBehavior` across `PASSIVE_RULES.md`, `BOSS_RULES.md`, `DATABASE.md`, and `GAME_STATE.md` are mutually consistent.
2. **No Duplication:** Confirm that `PASSIVE_RULES.md` owns the generic reset behavior contract and `BOSS_RULES.md` owns Sơn Thạch Vệ's definition, without duplicating formulas or state schemas (`.ai/workflow/documentation/documentation-change.md` §2).
3. **Tree Purity:** Confirm with `git status` that zero files under `src/` or `tests/` were modified as part of this documentation task.
4. **Implementability Audit:** Verify that a downstream runtime developer can implement the Sơn Thạch Vệ retrigger guard without needing any further Product Owner decisions.

---

## 14. Traceability to PO Decision

| PO Decision Element | Canonical Target Section | Contract Expression |
|---|---|---|
| **Option B Selected** | `PASSIVE_RULES.md` §4 | `Persistent` gains Boss-scoped once-per-battle firing eligibility |
| **Boss-Scoped Only** | `PASSIVE_RULES.md` §4 | Explicitly excludes Pet Passives from firing limitation |
| **One-Time Firing** | `BOSS_RULES.md` §6.2.4 | `son-thach-ve-enrage` fires at most once per battle |
| **Trigger: HP <= 50%** | `BOSS_RULES.md` §6.2.4 | Evaluated post-damage at Step 18a with inclusive `≤` |
| **Effect: +20% for 3 Turns** | `BOSS_RULES.md` §6.2.4 | `BuffDebuff`, `TargetStat = "ATK"`, `Magnitude = 20`, `duration = 3` |
| **No Re-trigger on Expiry** | `BOSS_RULES.md` §6.2.4 | Step 19a expiry does not restore firing eligibility |
| **New Battle Resets** | `BOSS_RULES.md` §6.2.4 | Fresh battle state has fresh eligibility |
| **No New State Field** | `BOSS_RULES.md` §6.2.4 / `GAME_STATE.md` §2.4 | Retires ambiguous wording; no new `BossState` field added |

---

## 15. Stop Conditions

An agent executing this documentation task MUST STOP and report `BLOCKED` if:
1. The repository authority hierarchy contradicts the approved PO decision (`AGENTS.md` §4, §20).
2. Any requirement demands altering Pet Passive semantics or generic combat resolution.
3. Applying the amendment would require creating an EF Core migration or touching database schema.
4. The approved PO decision cannot be expressed without adding a new state member to `BossState`.
5. Any instruction requests modifying runtime code or tests within this documentation task.

---

## 16. Implementation Guidance for Downstream Runtime Task

*(Informational note for the subsequent runtime task that will implement this contract in `BattleStateService.cs`):*

When the runtime task picks up `son-thach-ve-enrage`:
- Do NOT add a new field to `BossState`.
- Do NOT repurpose `PassiveProgress.Current`.
- Because `Persistent` Boss Passives fire at most once per battle and have `PassiveThreshold = null`, the runtime execution pipeline can track battle-lifetime Boss Passive firing eligibility within the battle session or execution context (or evaluate that a `Persistent` Boss Passive whose effect has been applied during the battle cannot fire again). The exact runtime mechanism must comply with `GAME_STATE.md` (no state schema mutation) and satisfy the unit test asserting that on Turn 4 (after Turn 3 effect expiry) with `HP <= 50%`, no second activation occurs.

---

## 17. Completion Evidence

### Decision Source

```text
Product Owner Decision: Option B (approved)
Boss-scoped once-per-battle firing eligibility for Persistent Boss Passives.
Zero new BossState members; no PassiveProgress.Current overloading; no new enum token.
```

### Canonical Content Updated

```text
docs/01-game-design/PASSIVE_RULES.md (v1.3 -> 1.4)
    §4  — Added dedicated Boss Passives clause: Persistent (Domain: NoReset) governs
          firing eligibility (at most once per battle); consumed upon first activation;
          effect expiry does not restore eligibility; fresh battle creates fresh eligibility.
          Pet Passive match-charging progress retention semantics remain unchanged.
    header — version record with TASK-173 provenance.

docs/01-game-design/BOSS_RULES.md (v2.8 -> 2.10)
    §6.2.4 — Sơn Thạch Vệ specification amended: retired ambiguous retrigger-guard
             sentence ("The retrigger guard is the authored one-time behavior above, not a new state field")
             and replaced with explicit once-per-battle firing contract under PASSIVE_RULES.md §4.
             Effect expiry at step 19a does not restore eligibility; re-crossing does not re-fire;
             new battle creates fresh eligibility; no new BossState member added.
    header — version record with TASK-173 provenance.

docs/02-technical/DATABASE.md (v1.30 -> 1.33)
    §1 note item 3 — Clarified that for Boss Passives, the existing Persistent token
                     additionally carries the once-per-battle firing eligibility contract
                     defined in PASSIVE_RULES.md §4 and BOSS_RULES.md §6.2.4.
                     No schema change, no migration, no column additions.
    header — version record with TASK-173 provenance.

docs/02-technical/GAME_STATE.md (v2.19 -> 2.21)
    §2.4.2 — Added Boss Passive subsection: once-per-battle firing eligibility is a
             dispatch-level evaluation governed by the definition's Persistent reset behavior,
             not a state member of the BossState tree. Confirms no HasActivated field added,
             PassiveProgress not overloaded, and StatusEffects[] retains transient applied-effect meaning.
    header — version record with TASK-173 provenance.
```

### Validation Results

```text
Documentation consistency audit   PASS — All 4 authoritative docs mutually aligned on Option B contract.
Closed vocabulary check          PASS — Storage token remains "Persistent", Domain enum remains "NoReset".
State footprint check            PASS — Zero new BossState members; no state schema mutation.
Pet Passive invariance           PASS — Pet Passives retain unchanged match-charging reset semantics.
Tree purity                      PASS — No source code, tests, schemas, or migrations modified by doc task.
```
