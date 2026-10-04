# TASK-172 — Apply the TASK-171 Two Remaining MVP Boss Decisions to the Authoritative Documentation

<!--
  GEN-TASK EXECUTION MANIFEST — PRODUCT-OWNER DECISION-APPLICATION TASK
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ and task files by path and section — it does NOT copy game
  rules, formulas, magnitudes, schemas, or contracts.

  THIS TASK APPLIES AN ALREADY-RECORDED PRODUCT OWNER DECISION. It decides
  nothing, implements nothing, provisions nothing, and changes no schema.

  PROVENANCE: TASK-171 recorded, verbatim, the Product Owner's full
  specifications for both remaining MVP Bosses (Sơn Thạch Vệ, Kim Lôi Vương)
  with all 31 coverage items DECIDED and no outstanding conflict. TASK-171 is
  the input; this task is the application half. The two-step split is the
  recorded repository precedent (TASK-166 → TASK-167, TASK-160 → TASK-161,
  TASK-154 → TASK-155, TASK-163 → TASK-164).

  BOUNDARY: this file plus the canonical owner (`BOSS_RULES.md` §6, §6.1–§6.4,
  its closing deferral note, and its version header) and only those dependent
  references whose wording this change makes stale. Zero files under src/.
  Zero files under tests/. No row inserted. No migration. No new task. No ADR.
  No new gameplay decision or mechanic.
-->

---

## Metadata

```text
Task ID:           TASK-172
Type:              GAMEPLAY-CHANGE (TASK_TYPES.md §2 — "a new undocumented
                   mechanic is being authorized"; workflow
                   development/gameplay-change.md §2's second branch and §3).
                   See "Type classification note".
Status:            DONE
Risk:              MEDIUM (authors two new Bosses inside an existing, already
                   implemented content model — the same §6.1 base-stat row,
                   §6.2 Passive row, §6.3 Skill row, and §6.4 identity row the
                   three existing Bosses use, the same COMBAT_RULES.md §5.5.1
                   Boss-side ATK modifier, the same Damage Pipeline, the same
                   Enrage transition, and the same Boss Skill charge/cooldown
                   mechanism. No schema, contract, event, state, protocol,
                   migration, or architecture member changes.
                   TASK_TYPES.md §4's GAMEPLAY-CHANGE baseline is HIGH; it is
                   MEDIUM here because the Product Owner decision is already
                   recorded and complete, both Bosses are expressible in the
                   existing closed vocabulary (TASK-171 D-14), and the edit is
                   confined to the canonical owner plus genuinely stale
                   cross-references. The residual risk is the downstream
                   re-encoding of the content row set, which is a separate task.)
Priority:          HIGH (the sole unblocking input for the remaining MVP Boss
                   content. MVP_SCOPE.md §1 lists "5 Bosses" and "Element,
                   Passive, Skill per Boss" as MVP IN; ROADMAP.md §1 Phase 2
                   requires "All 5 Bosses". Three of five are content-defined;
                   two are unauthored until this task lands, and
                   DATABASE.md §1/§3 item 5 structurally forbids provisioning a
                   Boss that is not content-defined.)
Primary Agent:     gameplay (TASK_TYPES.md §2 / §5 name the Gameplay Agent for
                   the Boss domain; .ai/agents/gameplay.md §Responsibilities
                   owns "Boss mechanics (Boss Passive, Boss Skill, response
                   logic)", and §Scope permits inspecting all of
                   docs/01-game-design/. The Review Agent supports per
                   TASK_TYPES.md §2's DOCUMENTATION row and
                   .ai/workflow/quality/review.md.)
Supporting Agents: review (documentation consistency — the edit must not
                   duplicate a rule owned elsewhere, and must not restate the
                   Damage Pipeline, the Enrage transition, the Status Effect
                   lifecycle, or the Boss ATK modifier consumption rule)
Workflow:          development/gameplay-change.md (§2 branch: the content is
                   new content docs/ does not contain, authorized by a recorded
                   Product Owner decision; §3 design-change branch)
Skills:            discovery/impact-analysis,
                   gameplay/gameplay-behavior-derivation,
                   quality/documentation-consistency,
                   quality/scope-validation
                   (4 skills — Simple budget, tasks/README.md §12)
Dependencies:      TASK-171 (READY — recorded the Product Owner decisions this
                     task applies, verbatim, with all 31 coverage items DECIDED.
                     IMMUTABLE; read-only),
                   TASK-124 (DONE — the Boss Passive effect contract §6.2.1–§6.2.4
                     record; the per-Passive detail pattern this task follows.
                     IMMUTABLE; read-only),
                   TASK-126 (DONE — the §6.2.1 Rage damage-scope reconciliation
                     and the §6.3.1 magnitude semantics this task follows.
                     IMMUTABLE; read-only),
                   TASK-128 / TASK-129 (DONE — the Boss-side ATK modifier
                     direction contract both new Passives consume unchanged.
                     IMMUTABLE; read-only),
                   TASK-045 / TASK-046 / TASK-048 / TASK-049 (DONE — the
                     BossDefinition storage member sets and the Boss identity
                     contract §6.4 records. IMMUTABLE; read-only),
                   TASK-052 / TASK-053 (DONE — the BossDefinition provisioning
                     mechanism and the three-row set whose counts this task's
                     content change makes stale. IMMUTABLE; read-only),
                   TASK-167 / TASK-169 (DONE — the documentation-application and
                     content-set-reconciliation precedents for type, agent,
                     skills, evidence shape, and dependent-reference handling.
                     IMMUTABLE; read-only)
Blocks:            (1) the `BossDefinition` provisioning task for
                   `boss-def-son-thach-ve` and `boss-def-kim-loi-vuong`
                   (DATABASE.md §1/§3 item 5 — only content-defined Bosses may
                   ever be provisioned); (2) the Domain `BossDefinitions`
                   content entries; (3) any runtime work the two Bosses'
                   Passives/Skills require; (4) ROADMAP.md §1 Phase 2's
                   "All 5 Bosses"; (5) the MVP "5 Bosses" line in
                   MVP_SCOPE.md §1.
Estimate:          Simple (4 skills; one canonical owner plus stale-reference
                   corrections; no code, no tests, no schema, no migration)
```

**Type classification note.** `GAMEPLAY-CHANGE`, not `DOCUMENTATION`. The
deliverable is not a decision artifact (that was TASK-171) and not a
documentation correction — it is the authorization of **new gameplay content
that `docs/` does not currently contain**. `TASK_TYPES.md` §3 selects the type
by "*what the deliverable is*"; the deliverable here is two newly authored,
implementation-ready Boss content definitions. `TASK_TYPES.md` §2's
`DOCUMENTATION` type is for a document that "needs to be created, updated, or
corrected, and no code change is required" — but the downstream consequence of
this task IS a code change (two `BossDefinition` rows and two Domain
`BossDefinitions` entries becoming provable), so it is a gameplay change
applied through documentation. TASK-167 (the TASK-166 application half) sets
this exact precedent.

**This task is NOT the provisioning task.** Authoring the two Bosses in `docs/`
does not create the `BossDefinition` rows or the Domain content entries.
`DATABASE.md` §5 item 4 rule (a) forbids provisioning a row whose content is not
documented — and, conversely, documenting content does not itself insert a row.
The provisioning task remains separate (`AGENTS.md` §7).

**This task is NOT a schema task.** `DATABASE.md` §1 note item 1 already states
`MaxHP`, `ATK`, `DEF`, and `EnrageThreshold` are **not** columns (they are Domain
content sourced at battle creation), and item 3 already fixes
`threshold = null` and the `Default` | `Partial` | `Persistent` reset tokens. No
column, table, member, or migration is touched.

---

## Objective

Apply the Product Owner decisions recorded in TASK-171 to their canonical owner
sections, so that a future implementation/provisioning agent can determine both
remaining MVP Bosses without guessing:

```text
Sơn Thạch Vệ   →  boss-son-thach-ve / boss-def-son-thach-ve
                  Thổ · HP 3000 / ATK 120 / DEF 0 · Idle · EnrageThreshold 1500
                  Passive  son-thach-ve-enrage — BossHP ≤ 50% → +20% ATK for 3 Turns
                  Skill    earthquake — 150 Base Damage, Charge 5, CD 0

Kim Lôi Vương  →  boss-kim-loi-vuong / boss-def-kim-loi-vuong
                  Kim · HP 2800 / ATK 140 / DEF 0 · Idle · EnrageThreshold 2100
                  Passive  kim-loi-vuong-combo — Player Combo ≥ 4 → +20% ATK for 1 Turn
                  Skill    thunder-strike — 180 Base Damage, Charge 5, CD 0
```

and so that both are stated as resolving through the **existing** contracts —
the existing Enrage transition, the existing Boss-side `TargetStat = "ATK"`
`BuffDebuff` modifier, the existing Turn-based Status Effect lifecycle, the
existing Boss Skill charge/cooldown mechanism, and the existing Damage Pipeline —
with **no** new status type, **no** new `TargetStat`, **no** new trigger
category, **no** new Element, **no** new event, and **no** new state member.

This task **applies** a recorded decision. It does not choose, extend, or
reinterpret one, and it edits no section other than the canonical owner and the
genuinely stale references that owner's change falsifies.

---

## Authoritative References

<!-- Cited by path and section. No rule, magnitude, formula, or schema is copied. -->

**The decision this task applies (read first):**

- `tasks/backlog/TASK-171-collect-product-owner-decisions-two-remaining-mvp-bosses.md`
  **§"B-1 — MVP Boss #4"** and **§"B-2 — MVP Boss #5"** — the verbatim Product
  Owner specifications, the resolved identity/Enrage/Initial-State/charge
  decisions, and the preserved historical conflict record.
  **§"Coverage resolution"** (`D-1` … `D-16`) — the per-item resolution.
  **§"B-1/B-2 cross-document consequences"** — the classification this task
  executes. **§"B-1/B-2 distinctness validation"** — the §2/§3.2/§6 checks.
  **§"Remaining Issues"** — the four reported observations.
  **This is the sole source of the content this task authors.**

**The canonical owner this task edits:**

- `docs/01-game-design/BOSS_RULES.md` **§6** — the MVP Boss reference table, and
  at §6.3.1's close the governing note that "Two additional MVP Bosses (5 total
  per `GAME_RULES.md` §19 scope) are not yet content-defined" plus the four
  authoring requirements. **This table and this note are what this task updates.**
- `docs/01-game-design/BOSS_RULES.md` **§6.1** — the base-stat row shape
  (`Element`, `HP`/`MaxHP`, `ATK`, `DEF`, `EnrageThreshold`, `Initial State`).
  Gains one row per new Boss.
- `docs/01-game-design/BOSS_RULES.md` **§6.2** — the canonical Passive row shape
  (Passive Effect + Passive Trigger) and the **PassiveThreshold (match-charged
  passives only)** paragraph. Gains one row per new Boss, plus the
  `PassiveThreshold = null` consequence.
- `docs/01-game-design/BOSS_RULES.md` **§6.2.1–§6.2.4** — the per-Passive detail
  pattern (representation, magnitude, duration, apply point, reapplication).
  **§6.2.4** now enumerates the Boss effects and must be reconciled with two
  more.
- `docs/01-game-design/BOSS_RULES.md` **§6.3** and **§6.3.1** — the Skill row
  shape (Charge Req. / CD / Base Dmg / Secondary Effect) and the magnitude and
  duration declaration pattern. Gains one row per new Boss.
- `docs/01-game-design/BOSS_RULES.md` **§6.4** — the identity contract table
  (`BossId`, display name, `PassiveId`, `SkillId`) and the paragraph asserting
  "for the three content-defined MVP Bosses". Gains one row per new Boss and the
  count wording changes.

**The rules this task must reference rather than restate:**

- `docs/01-game-design/BOSS_RULES.md` **§1** (the Boss structure and the closed
  `Idle` / `Charging` / `Enraged` / `Stunned` State enum), **§2** (mechanics over
  stats; Passive AND Skill mechanically distinct), **§3** (the Boss Passive
  contract, including §3 item 2's closed trigger list, §3.1 determinism,
  §3.2 trigger-pattern diversity, §3.3 Step-18 timing), **§4** (the Boss Skill
  contract and §4 item 2's explicit-timing requirement), **§5 item 4** (Enrage —
  a permanent transition at `BossHP < EnrageThreshold`, evaluated after
  Player→Boss damage and before the terminal check), **§7** (the emitted events),
  **§8** (server authority)
- `docs/01-game-design/COMBAT_RULES.md` **§3** (the Damage Pipeline and §3.4's
  Boss-side Step-4 = 1.0), **§5.1–§5.3** (Status Effect semantics, the two
  duration models, and the single step-19a decrement), **§5.5.1** (the
  Boss-side `TargetStat = "ATK"` rule), **§5.5.2** (which Boss damage the
  modifier reaches), **§5.5.3** (its scope and the non-`"ATK"` boundary),
  **§5.5.4** (the base stat is never overwritten), **§5.5.5** (duration and
  reapplication)
- `docs/01-game-design/PASSIVE_RULES.md` **§3** (the alternate trigger forms,
  including `Combo` and `HP Threshold`) and **§4** (Reset Behavior — Default /
  Partial Reset / No Reset, and §4 item 3's requirement that a non-default
  behavior be documented on the specific definition)
- `docs/01-game-design/ELEMENT_RULES.md` **§1** (the Five Elements), **§1.1**
  (Element-carrying entities), **§1.2** (exactly one Element per Boss), **§5**
  (where the Element Modifier applies), **§6** (Pet assignments only — Boss
  Elements are deferred here)
- `docs/01-game-design/GAME_RULES.md` **§15** (Boss rules: the trigger list and
  the no-same-trigger-pattern requirement), **§16** (the canonical event list),
  **§17** step 18a–18c (Boss Response) and step 19a (End Turn), **§18** (server
  authority)
- `docs/02-technical/GAME_STATE.md` **§2.4** and **§2.4.1** (`BossState` and its
  staged fields, including `StatusEffects[]`), **§2.4.2** (Boss Passive),
  **§2.4.3** (Skill charge/cooldown and the fire condition), **§2.4.4** (Enrage),
  **§2.3.1** (the `StatusEffects[]` instance schema)
- `docs/02-technical/DATABASE.md` **§1 note items 1–5** (the BossDefinition
  persistence contract, the closed `PassiveDefinition` / `SkillDefinition`
  member sets, `BossDefinitionId`'s value form, and the content-defined-rows-only
  provisioning rule), **§3** (the `BossDefinition` column constraints)
- `docs/02-technical/GAME_EVENTS.md` **§2** (`PassiveCharged` /
  `PassiveTriggered` with `source = "boss"`, `BossSkillCast`,
  `BattleWon` / `BattleLost` — the existing events both Bosses produce; no new
  event is required)
- `docs/00-overview/MVP_SCOPE.md` **§1** ("5 Bosses"; "Element, Passive, Skill
  per Boss"), **§2**, **§4**
- `docs/00-overview/ROADMAP.md` **§1 Phase 2**

**Governing contract and precedent:**

- `AGENTS.md` **§2** (source-of-truth hierarchy), **§4** (never silently resolve
  a conflict), **§7** (invent no rule), **§8** (MVP protection), **§9**
  (anti-overengineering), **§10** (server authority), **§12** (domain
  boundaries), **§16** (report, do not fix inline), **§17** (documentation
  change rule), **§18** (architecture change rule), **§20** (stop conditions),
  **§23** (final principle)
- `.ai/README.md` **§6** (source-of-truth rule), **§13** (stop conditions)
- `.ai/workflow/documentation/documentation-change.md` **§1** (identify the
  canonical owner; update dependent references only when their wording is now
  stale), **§2** (no duplication, ever), **§3** (determining the canonical
  owner), **§4** (composition)
- `tasks/README.md` **§9** (no business-rule duplication), **§12** (skill budget)
- `tasks/completed/TASK-167-apply-task-166-signature-skill-decisions-to-authoritative-documentation.md`
  — the decision-application precedent, including its dependent-stale-reference
  handling
- `tasks/completed/TASK-169-reconcile-database-carddefinition-content-set-references.md`
  — the content-set-reference reconciliation precedent
- `tasks/completed/TASK-124-apply-boss-passive-contract-decisions.md` and
  `tasks/completed/TASK-126-apply-boss-skill-step-1-damage-composition-contract.md`
  — the precedent for the §6.2/§6.3 authoring shape this task follows

---

## Decision Source

```text
Decision source:   TASK-171
                   tasks/backlog/TASK-171-collect-product-owner-decisions-two-remaining-mvp-bosses.md

Status at input:   READY — decision-complete. All 31 coverage items DECIDED for
                   both Bosses; no field PENDING; no conflict outstanding. The
                   three conflicts reported at earlier stages (Initial State
                   ACTIVE; Kim Lôi Vương EnrageThreshold null; BossDefinitionId
                   equal to BossId) were each resolved by explicit Product Owner
                   decision and are preserved there as historical evidence.

Read-only:         TASK-171 is the Product Owner decision record. This task
                   reads it and applies it. It does not edit it, does not
                   duplicate its decision process, and does not reopen any
                   resolved decision.
```

**Values this task applies — all from TASK-171, none authored here:**

```text
Sơn Thạch Vệ   Display name Sơn Thạch Vệ · BossId boss-son-thach-ve ·
               BossDefinitionId boss-def-son-thach-ve · Element Thổ ·
               HP 3000 / MaxHP 3000 / ATK 120 / DEF 0 · Initial State Idle ·
               EnrageThreshold 1500 · PassiveId son-thach-ve-enrage ·
               Passive trigger category Boss HP · trigger BossHP ≤ 50% ·
               effect +20% ATK (Magnitude +20) for 3 Turns ·
               no re-trigger after activation · PassiveThreshold null ·
               SkillId earthquake · Skill Earthquake · Base Damage 150 ·
               Charge Requirement 5 · Cooldown 0 · no secondary effect ·
               no board effect

Kim Lôi Vương  Display name Kim Lôi Vương · BossId boss-kim-loi-vuong ·
               BossDefinitionId boss-def-kim-loi-vuong · Element Kim ·
               HP 2800 / MaxHP 2800 / ATK 140 / DEF 0 · Initial State Idle ·
               EnrageThreshold 2100 · PassiveId kim-loi-vuong-combo ·
               Passive trigger category Combo · trigger Player Combo ≥ 4 ·
               effect +20% ATK (Magnitude +20) for 1 Turn ·
               existing reset/reapplication semantics · PassiveThreshold null ·
               SkillId thunder-strike · Skill Thunder Strike · Base Damage 180 ·
               Charge Requirement 5 · Cooldown 0 · no secondary effect
```

**Provenance rule.** Every value above is transcribed from TASK-171's recorded
Product Owner decisions and is applied as recorded. Nothing here is normalized,
reinterpreted, derived, or extended by this task.

---

## Current State

The MVP Boss content set is complete for three of five Bosses. For the remaining
two, the Product Owner has supplied full specifications, recorded by TASK-171 as
**decision-complete (READY)**. No authoritative document has been changed for
them, no `BossDefinition` row exists for them, and no Domain content entry
exists for them; those remain downstream steps.

```text
BOSS_RULES.md §6        → 3 Bosses content-defined; §6.3.1's closing note
                          defers 2 with an explicit authoring-requirement list
BOSS_RULES.md §6.1–§6.4 → 3 rows each; §6.4's prose says "the three
                          content-defined MVP Bosses"
ROADMAP.md §1 Phase 2   → "All 5 Bosses (2 not yet content-defined …)"
DATABASE.md §1/§3 #5    → only content-defined Bosses (currently 3) may ever be
                          provisioned; §1's entity block says "content-defined: 3"
MVP_SCOPE.md §1         → "5 Bosses"; "Element, Passive, Skill per Boss" is IN
```

The repository's task lifecycle is otherwise quiet: `tasks/backlog/` holds
`TASK-170` (BACKLOG, an unrelated audit) and `TASK-171` (READY);
`tasks/active/` holds no task file; `tasks/blocked/` holds only the unrelated
`TASK-036`; and `tasks/completed/` holds TASK-001 … TASK-169. The highest
existing ID is **TASK-171**, so this task is **TASK-172** (`tasks/README.md` §3).

**Stop-condition status at creation: NOT TRIGGERED.** Verified before writing
this file:

```text
[OK] TASK-171 IS READY and decision-complete — all 31 coverage items DECIDED,
     no field PENDING, no conflict outstanding.
[OK] No TASK-171 decision is unresolved.
[OK] Authoritative documentation does NOT contradict TASK-171 in any way
     requiring a new gameplay decision (TASK-171 §"B-1/B-2 distinctness
     validation" and §"B-1/B-2 cross-document consequences" were independently
     re-verified for this task; no violating §2, §3, §3.2, or §6 condition was
     found).
[OK] Ownership of every required contract IS determinable — see
     §"Documentation Ownership" below; each edited section is either the
     canonical owner of the value or a reference this change makes stale.
[OK] Documentation application requires NO new state member — GAME_STATE.md
     §2.4's tree already carries every field both Bosses need, and §2.4.3's
     charge/cooldown contract, §2.4.4's Enrage contract, and §2.4.1's
     StatusEffects[] collection accept both unchanged.
[OK] Documentation application requires NO new event/protocol — BOSS_RULES.md
     §7's event set and GAME_EVENTS.md §2's payloads already cover every event
     both Bosses produce; SIGNALR_PROTOCOL.md §4.4 already delivers Boss HP.
[OK] Documentation application requires NO schema change — DATABASE.md §1 note
     item 1 already excludes MaxHP/ATK/DEF/EnrageThreshold from columns, item 3
     already fixes threshold = null and the three reset tokens, and item 4's
     SkillDefinition member set already carries both Skills.
[OK] Both Bosses ARE expressible by the existing Boss/Passive/Skill contracts —
     TASK-171 D-14's existing-vocabulary check passed with NO STOP; re-verified
     against BOSS_RULES.md §1/§3/§4/§5, COMBAT_RULES.md §5.5, PASSIVE_RULES.md
     §3/§4, ELEMENT_RULES.md §1, and DATABASE.md §1.
```

---

## Documentation Ownership

Per `.ai/workflow/documentation/documentation-change.md` §1/§3, the canonical
owner is the document whose stated question the information answers.

```text
BOSS_RULES.md §6, §6.1–§6.4
    = THE canonical owner. Its §6 reference tables ARE the Boss content
      contract: §6.1 owns the base-stat row, §6.2 owns the Passive row and the
      per-Passive detail, §6.3/§6.3.1 own the Skill row and the magnitude and
      duration declaration pattern, and §6.4 owns the identity table. §6.4's own
      prose states the values "are fixed here so no task invents its own".
      DATABASE.md §1 note item 5 states a row's columns are transcribed from
      BOSS_RULES.md §6.1–§6.4, and §1 note item 4 says the storage shape is
      DATABASE.md's while the values are BOSS_RULES.md's. This task authors both
      Bosses here, and nowhere else.

    ALSO §6.3.1's closing note — the four-item "not yet content-defined"
      authoring-requirement list this change falsifies, and §6.4's
      "the three content-defined MVP Bosses" prose.

DEPENDENT REFERENCES — updated ONLY where this change makes the wording stale
(.ai/workflow/documentation/documentation-change.md §1):
    ROADMAP.md §1 Phase 2 — the "2 not yet content-defined" line.
    DATABASE.md §1 entity block — "content-defined: 3"; §1 note item 5 —
      "currently 3" and the "exactly three rows" row-set statement.
    (Each is a count or status statement that this task's BOSS_RULES.md change
     makes false. Wording only; NO rule, value, schema, member, or row changes.)

NOT edited — and why:
  GAME_RULES.md      = owns the core rules and references BOSS_RULES.md for the
                       exact Boss list (§15: "Exact MVP Boss list and mechanics:
                       see BOSS_RULES.md"). It names no Boss count, so nothing
                       there becomes stale. Unchanged.
  COMBAT_RULES.md    = owns the Damage Pipeline, the Status Effect lifecycle,
                       and §5.5's Boss-side ATK modifier rule. Both Passives
                       REUSE §5.5.1's TargetStat = "ATK" rule and both Skills
                       reuse §3's pipeline unchanged, so this task adds no rule
                       there. §5.5.1 already uses Hỏa Long's Rage as its worked
                       example and names it as "the MVP instance of this rule";
                       the two new Passives are additional instances of the same
                       unchanged rule, so no §5.5 edit is required. Cross-
                       referenced from BOSS_RULES.md §6.2, not restated.
  ELEMENT_RULES.md   = owns Element semantics and the Entity→Element model. §1's
                       Five already include Thổ and Kim, §1.2's one-per-Boss
                       rule holds, and §6 assigns Pets only and already defers
                       Boss Elements to BOSS_RULES.md §6. Unchanged.
  PASSIVE_RULES.md   = owns the Passive system. Both Boss Passives use existing
                       trigger forms (§3's Combo and HP Threshold forms) and
                       existing Reset Behavior forms (§4). §4 item 3 requires a
                       non-default reset behavior be documented on the specific
                       definition — which is BOSS_RULES.md §6.2, not here.
                       §8's Pet table and its closing PassiveId paragraph are
                       Pet-scoped and unaffected. Unchanged.
  GAME_STATE.md      = owns the state contract. §2.4's tree already carries
                       BossId, Element, HP/MaxHP/ATK/DEF, State, PassiveId,
                       PassiveProgress, SkillCharge, SkillCooldown, and
                       StatusEffects[]. No member is added. Unchanged.
  GAME_EVENTS.md     = owns event payloads. Every event both Bosses produce
                       already exists. Unchanged.
  SIGNALR_PROTOCOL.md = owns the wire contract. §4.4's two-member bossState
                       projection already delivers Boss HP for any Boss, and
                       §3.2.16–§3.2.18 already carry passiveId/skillId as opaque
                       strings. Unchanged.
  API_CONTRACTS.md   = owns REST payloads. POST /api/battle/start's bossId is a
                       free identity string validated against the persisted
                       BossDefinition rows; the new Bosses add no endpoint or
                       payload member. Unchanged.
  MVP_SCOPE.md       = §1 already lists "5 Bosses" as IN, so it is not made
                       stale — this task advances a stated MVP target rather
                       than changing scope. Unchanged.
```

---

## Scope

### In Scope

- Author **Sơn Thạch Vệ** and **Kim Lôi Vương** at their canonical owner,
  `BOSS_RULES.md` §6 — one new row in each of §6, §6.1, §6.2, §6.3, and §6.4.
- Add the per-Passive detail for both new Passives following the
  §6.2.1–§6.2.4 pattern (representation, magnitude, duration, apply point,
  reapplication), and reconcile §6.2.4's enumeration.
- Update the §6.2 **PassiveThreshold (match-charged passives only)** paragraph so
  the two new non-match-charged Passives' `null` consequence is recorded.
- Retire §6.3.1's closing "Two additional MVP Bosses … not yet content-defined"
  note and its four-item requirement list, which the two new Bosses satisfy.
- Update §6.4's identity table and its "three content-defined MVP Bosses" prose.
- Record each file's change in its version header, citing TASK-171.
- Correct the dependent stale references this change falsifies (see
  §"Documentation Ownership"), wording only.

### Out of Scope

- **No source code.** Zero files under `src/` — including `BossDefinitions.cs`,
  `BossDefinition.cs`, `BossState.cs`, `BattleStateService.cs`, and
  `CardCastExecutor.cs`.
- **No database schema change.** No new column, table, constraint, index,
  migration, or `EffectDefinition` member.
- **No database provisioning.** No row insertion, no seed, no `HasData`, no
  startup loader, no migration edit.
- **No new gameplay decision.** Every value comes from TASK-171; nothing is
  chosen, derived, normalized, or reinterpreted here.
- **No new gameplay mechanic.** No new trigger category, status type, status
  effect, `TargetStat`, damage type, Element, representation, State, resource,
  cooldown concept, or board mechanic.
- **No Boss AI, Boss skill execution, or Boss passive execution** — runtime
  behavior is a separate downstream task.
- **No new SignalR contract** and **no new Redis contract**.
- **No new event.** `BOSS_RULES.md` §7's set is complete for both Bosses.
- **No tests for runtime behavior.**
- **No ADR.** No architecture, storage strategy, realtime strategy, state model,
  or module-boundary change (`AGENTS.md` §18).
- **TASK-171 remains unchanged**, as do all `tasks/completed/` files.
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2.

---

## Required Documentation Changes

```text
1. docs/01-game-design/BOSS_RULES.md — §6 (the reference table)
   Add one row per new Boss: Element / Passive (trigger) / Skill (Effect
   Magnitudes) / Skill Timing, in the existing column form.

2. docs/01-game-design/BOSS_RULES.md — §6.1 (MVP Boss Base Stats)
   Add one row per new Boss: Element, HP / MaxHP, ATK, DEF, EnrageThreshold,
   Initial State. This is the §6.1 field the §5 item 4 Enrage transition reads.

3. docs/01-game-design/BOSS_RULES.md — §6.2 (Boss Passive Details)
   Add one row per new Boss: Passive Effect + Passive Trigger.
   Update the PassiveThreshold paragraph so both new Passives' non-match-charged
   disposition (PassiveThreshold = null) is recorded alongside Thủy Ma's.

4. docs/01-game-design/BOSS_RULES.md — §6.2.1–§6.2.4
   Add the per-Passive detail for both new Passives following the existing
   pattern. For Sơn Thạch Vệ the detail must record the one-time / no-retrigger
   reset behavior, which PASSIVE_RULES.md §4 item 3 requires be documented on
   the specific definition. Reconcile §6.2.4's enumeration of Boss effects.

5. docs/01-game-design/BOSS_RULES.md — §6.3 and §6.3.1
   Add one row per new Boss: Charge Req., CD (T), Base Dmg, Secondary Effect
   & Magnitude (both "None"), and add the §6.3.1-style declaration entries.
   Retire the closing "Two additional MVP Bosses … not yet content-defined"
   note and its four-item requirement list, which both new Bosses satisfy.

6. docs/01-game-design/BOSS_RULES.md — §6.4 (Identity Contract)
   Add one identity row per new Boss (BossId, display name, PassiveId, SkillId)
   and update the "three content-defined MVP Bosses" prose.

7. docs/01-game-design/BOSS_RULES.md — version header
   Record the change and cite TASK-171.

8. docs/00-overview/ROADMAP.md — §1 Phase 2
   Correct the "2 not yet content-defined" line (stale-reference correction
   only; no rule, scope, or value change).

9. docs/02-technical/DATABASE.md — §1 entity block and §1 note item 5
   Correct the content-defined count ("content-defined: 3"; "currently 3") and
   the row-set count statement (stale-reference correction only). THE RULES ARE
   UNCHANGED and the content-defined-rows-only restriction stands.

NOT edited: every other section of every other docs/ file.
```

**Duplication discipline.** No edit restates the Damage Pipeline, the Enrage
transition, the Element Modifier, the Status Effect lifecycle, the step-19a
decrement, the Boss-side `TargetStat = "ATK"` consumption rule, the charge/
cooldown mechanism, or the `PassiveDefinition` / `SkillDefinition` storage shape.
Each is cross-referenced by section (`.ai/workflow/documentation/
documentation-change.md` §2, `tasks/README.md` §9).

**Critical contract points the authoring must preserve.**

```text
ENRAGE ≠ PASSIVE — they are two distinct fields and must not be collapsed.
  Sơn Thạch Vệ   EnrageThreshold = 1500 → Idle → Enraged when BossHP < 1500
                 Passive trigger   = BossHP ≤ 50%   (a separate §6.2 field)
  Kim Lôi Vương  EnrageThreshold = 2100 → Idle → Enraged when BossHP < 2100
                 Passive trigger   = Player Combo ≥ 4 (a separate §6.2 field)
  The two boundaries coincide numerically for Sơn Thạch Vệ and use different
  operators by design (Enrage's strict `<` per §5 item 4 vs the Passive's `≤`).
  TASK-171 states this separation is deliberate for both Bosses. Record both
  fields; do not merge them, do not re-derive one from the other, and do not
  create a second Enrage mechanism.

PASSIVE — existing contracts only.
  Both Passives are a Turn-based BuffDebuff Status Effect instance in
  BossState.StatusEffects[] with TargetStat = "ATK" and Magnitude = +20 — the
  existing COMBAT_RULES.md §5.5.1 rule (the §6.2.1 Rage shape). Durations are
  3 Turns and 1 Turn under the existing §5.3 lifecycle. Sơn Thạch Vệ's Passive is
  one-time / no re-trigger after activation; Kim Lôi Vương's uses the existing
  reset and refresh-not-stack semantics unchanged. Both are non-match-charged →
  PassiveThreshold = null. No new Passive type, status type, or TargetStat.

SKILL — existing mechanism only.
  Both Skills are direct damage through the existing Damage Pipeline using each
  Boss's Element. Charge Requirement 5 and Cooldown 0 use §6.3's mechanism
  unchanged; CD 0 needs no special case (the post-fire reset stores the Boss's
  cooldown value, and the fire condition is immediately satisfiable on
  recharge). Neither Skill applies a secondary effect and neither applies a
  board effect. No board transformation, gem destruction, gem conversion,
  freeze, lock, new status, new damage type, or new event.

IDENTITY — apply the supplied values exactly.
  boss-son-thach-ve / boss-def-son-thach-ve / son-thach-ve-enrage / earthquake
  boss-kim-loi-vuong / boss-def-kim-loi-vuong / kim-loi-vuong-combo / thunder-strike
  BossId follows §6.4's stated `boss-<ascii-kebab-case-name>` convention and
  BossDefinitionId follows DATABASE.md §1 note item 2's
  `boss-def-<ascii-kebab-case-name>` form, deliberately distinct. §6.4 states no
  exact Boss PassiveId or SkillId value form, so the supplied forms are recorded
  verbatim — NOT normalized to the three existing Bosses' observed spellings.

DATABASE BOUNDARY — restated verbatim from TASK-171 D-15.
  No schema change.
  No new database column.
  No new database table.
  No new EffectDefinition field.
  BossDefinitionId remains the existing primary-key/content identity.
  Content provisioning belongs to a later implementation/provisioning task.
```

---

## Acceptance Criteria

- [x] `BOSS_RULES.md` §6 contains a row for **Sơn Thạch Vệ** stating Element Thổ,
      the Boss HP Passive trigger, the Earthquake Skill with its effect
      magnitude, and its Skill Timing.
- [x] `BOSS_RULES.md` §6 contains a row for **Kim Lôi Vương** stating Element Kim,
      the Player Combo Passive trigger, the Thunder Strike Skill with its effect
      magnitude, and its Skill Timing.
- [x] `BOSS_RULES.md` §6.1 contains a **Sơn Thạch Vệ** row with HP/MaxHP 3000,
      ATK 120, DEF 0, EnrageThreshold 1500, Initial State `Idle`.
- [x] `BOSS_RULES.md` §6.1 contains a **Kim Lôi Vương** row with HP/MaxHP 2800,
      ATK 140, DEF 0, EnrageThreshold 2100, Initial State `Idle`.
- [x] `BOSS_RULES.md` §6.2 contains a **Sơn Thạch Vệ** Passive row: effect
      `+20% ATK` for 3 turns, trigger `Boss HP ≤ 50%`.
- [x] `BOSS_RULES.md` §6.2 contains a **Kim Lôi Vương** Passive row: effect
      `+20% ATK` for 1 turn, trigger `Player Combo ≥ 4`.
- [x] `BOSS_RULES.md` §6.2's PassiveThreshold paragraph records that both new
      Passives are **not** match-charged and carry `PassiveThreshold = null`,
      and that `null` does **not** mean always-active.
- [x] `BOSS_RULES.md` §6.2.x records both new Passives' detail per the
      §6.2.1–§6.2.4 pattern: representation
      (`BossState.StatusEffects[]`, `BuffDebuff`, `TargetStat = "ATK"`),
      magnitude `+20`, duration, apply point (§3.3's Step 18a), and
      reapplication behavior.
- [x] Sơn Thạch Vệ's Passive details record the **one-time / no re-trigger after
      activation** behavior, as `PASSIVE_RULES.md` §4 item 3 requires for a
      non-default reset behavior.
- [x] Kim Lôi Vương's Passive details record the **existing** reset and
      refresh-not-stack semantics, with no new stacking behavior invented.
- [x] Both Bosses' Enrage dimension is recorded as a **separate** §6.1 field
      from the Passive trigger, with the transition stated as `BossHP <
      EnrageThreshold` — the two are not collapsed and no second Enrage
      mechanism is introduced.
- [x] `BOSS_RULES.md` §6.3 contains a **Sơn Thạch Vệ** Skill row: Charge Req. 5
      matches, CD 0, Base Dmg 150, Secondary Effect None.
- [x] `BOSS_RULES.md` §6.3 contains a **Kim Lôi Vương** Skill row: Charge Req. 5
      matches, CD 0, Base Dmg 180, Secondary Effect None.
- [x] `BOSS_RULES.md` §6.3.1 gains the declaration entry for Earthquake (Base
      Damage 150, no secondary effect) and for Thunder Strike (Base Damage 180,
      no secondary effect), each stating its damage goes through the existing
      Damage Pipeline and that no board effect applies.
- [x] `BOSS_RULES.md` §6.3.1's closing "Two additional MVP Bosses (5 total per
      `GAME_RULES.md` §19 scope) are not yet content-defined" note and its
      four-item requirement list are retired or corrected, and no longer
      contradict the two new Bosses.
- [x] `BOSS_RULES.md` §6.4's identity table contains a **Sơn Thạch Vệ** row:
      `"boss-son-thach-ve"`, `"Sơn Thạch Vệ"`, `"son-thach-ve-enrage"`,
      `"earthquake"`.
- [x] `BOSS_RULES.md` §6.4's identity table contains a **Kim Lôi Vương** row:
      `"boss-kim-loi-vuong"`, `"Kim Lôi Vương"`, `"kim-loi-vuong-combo"`,
      `"thunder-strike"`.
- [x] `BOSS_RULES.md` §6.4's prose no longer asserts that only three Bosses are
      content-defined.
- [x] The supplied `PassiveId` and `SkillId` values are recorded **verbatim** —
      not normalized, prefixed, or respelled by analogy to the three existing
      Bosses.
- [x] `BOSS_RULES.md`'s version header records the change and cites TASK-171.
- [x] The three existing Bosses (Hỏa Long, Thủy Ma, Mộc Yêu) are unchanged in
      every §6/§6.1/§6.2/§6.3/§6.4 value, and their §6.2.1–§6.2.3 detail is
      unchanged except where §6.2.4's enumeration is reconciled.
- [x] `ROADMAP.md` §1 Phase 2 no longer states that 2 Bosses are not yet
      content-defined (stale-reference correction only).
- [x] `DATABASE.md` §1's entity block and §1 note item 5 no longer state the
      content-defined Boss count as 3 or the row set as exactly three
      (stale-reference correction only).
- [x] **No `DATABASE.md` schema, vocabulary, or storage-shape change** — no new
      column, table, constraint, index, or migration; no new
      `PassiveDefinition` / `SkillDefinition` member; no new `effectType` or
      `valueType`.
- [x] **No `GAME_STATE.md` change** — no new `BossState` member, value, type, or
      collection.
- [x] **No `GAME_EVENTS.md` or `SIGNALR_PROTOCOL.md` change** — no new event,
      payload member, method, or subscription.
- [x] **No `COMBAT_RULES.md`, `ELEMENT_RULES.md`, `PASSIVE_RULES.md`, or
      `GAME_RULES.md` change** — no existing damage, Enrage, Element, Passive, or
      Boss rule is altered.
- [x] **No source code** modified: zero files under `src/` (byte-identical).
- [x] **No tests** modified: zero files under `tests/` (byte-identical).
- [x] **No database row provisioned**, no migration created or edited, and no
      `HasData` / seed / startup loader introduced.
- [x] **No new gameplay decision, mechanic, Element, trigger category, status
      type, `TargetStat`, damage type, State, resource, event, SignalR contract,
      or Redis contract** is introduced.
- [x] **TASK-171 is unmodified**, as is every file under `tasks/completed/`.
- [x] **No new task and no ADR** created; `docs/03-decisions/` unmodified.
- [x] No rule is duplicated across documents
      (`.ai/workflow/documentation/documentation-change.md` §2).
- [x] All relevant validations pass at the required depth
      (`core/validation.md` §2).
- [ ] Quality review checklist passes (`quality/review.md` §1).
- [ ] No authoritative rule or contract violated (`AGENTS.md` §10 / ADR-001).

---

## Affected Files & Areas

```text
[ ] src/backend/ (Domain / Application / Infrastructure / Api)   → NONE
[ ] src/frontend/client/ (scenes / runtime / services / state)    → NONE
[ ] tests/ (unit / integration / gameplay scenarios)             → NONE
[x] docs/01-game-design/BOSS_RULES.md   — §6, §6.1, §6.2, §6.2.1–§6.2.4,
      §6.3, §6.3.1, §6.4 + version header (THE CANONICAL OWNER)
[x] docs/00-overview/ROADMAP.md         — §1 Phase 2 (stale count reference)
[x] docs/02-technical/DATABASE.md       — §1 entity block + §1 note item 5
      (stale count references) + version header. SCHEMA UNCHANGED.
[ ] docs/01-game-design/GAME_RULES.md                    → NONE
[ ] docs/01-game-design/COMBAT_RULES.md                  → NONE
[ ] docs/01-game-design/ELEMENT_RULES.md                 → NONE
[ ] docs/01-game-design/PASSIVE_RULES.md                 → NONE
[ ] docs/00-overview/MVP_SCOPE.md                        → NONE
[ ] docs/02-technical/GAME_STATE.md                      → NONE
[ ] docs/02-technical/GAME_EVENTS.md                     → NONE
[ ] docs/02-technical/SIGNALR_PROTOCOL.md                → NONE
[ ] docs/02-technical/API_CONTRACTS.md                   → NONE
[ ] docs/03-decisions/ADR/                               → NONE
[ ] src/  /  tests/                                      → NONE
[ ] tasks/backlog/TASK-171-*.md                          → NONE (the decision
      source is read, never edited; its decisions are immutable)
[ ] tasks/completed/                                     → NONE (immutable)
[x] tasks/backlog/TASK-172-<this file>.md — this task file only
```

---

## Implementation Notes

- **Apply, do not decide.** Every number, name, and identity in §6 comes from
  TASK-171's recorded answers. If a value is needed that TASK-171 does not
  contain, that is a STOP (`AGENTS.md` §7), not a value to author.
- **Match the existing §6 form.** The three authored Bosses establish the table
  shapes and the §6.2.1–§6.2.3 detail pattern. Follow them; do not invent a new
  format, a new column, or a new subsection.
- **§6.2.4's enumeration is a consequence, not a new rule.** It currently
  enumerates the Boss effects and their visibility. Reconcile it with the two new
  Passives; do not restate the Status Effect model, the SignalR projection, or
  the server-authority rule there — those are owned by `GAME_STATE.md` §2.4.1,
  `SIGNALR_PROTOCOL.md` §4.4, and `BOSS_RULES.md` §8 respectively.
- **Record `PassiveThreshold = null` as a disposition, not an absence.** 
  `DATABASE.md` §1 note item 3 states `null` means "no match-charging threshold"
  and is **NOT** a statement that the Passive is always-active. The two new
  Passives are threshold-triggered (HP ≤ 50% / Combo ≥ 4) exactly as Thủy Ma's
  Battle Start trigger is an event trigger — not always-on.
- **A non-match-charged Passive emits no `PassiveCharged` from match progress.**
  §6.2 already states this for Thủy Ma. Apply the same statement's logic to both
  new Passives; do not claim a `PassiveCharged` charge progression neither Boss
  has.
- **Do not merge Enrage with the Passive.** See §"Required Documentation
  Changes" §"Critical contract points". §5 item 4 owns the transition; §6.1 owns
  the per-Boss value; §6.2 owns the Passive trigger. Three distinct homes.
- **The Boss-side ATK modifier rule is not restated.** §6.2.1 already references
  `COMBAT_RULES.md` §3.4 and §5.5 for Rage. Point the two new Passive details at
  the same owners.
- **`Cooldown 0` is not a special case.** §6.3's model already handles it: the
  post-fire reset stores the Boss's cooldown value (0), the
  `SkillCooldown = 0` firing condition is immediately satisfiable on recharge,
  and the "blocked while CD > 0" rule simply never binds. Do not author a new
  timing rule or an exception clause.
- **The two Skills are direct damage only.** Both traverse the existing Damage
  Pipeline with the Boss's Element. Record "Secondary Effect: None" as the
  existing §6.3.1 item 2 records an instant, non-persistent effect — an explicit
  statement, not an omission.
- **Cite, do not restate.** Use the `` `DOC.md` §N `` idiom. Never copy a
  pipeline step list, a duration lifecycle, a consumption formula, or a storage
  shape into `BOSS_RULES.md` §6
  (`.ai/workflow/documentation/documentation-change.md` §2).
- **Version headers are part of the convention.** Every edited file carries a
  version line recording what changed and which task changed it. Follow the
  existing style; mark count-only corrections as "stale-reference correction
  only".
- **The three reported observations are NOT this task's to resolve**
  (`tasks/backlog/TASK-171-*.md` §"Remaining Issues"): the §6.1 percentage
  parenthetical rendering for the new thresholds, the shared `+20%` ATK
  magnitude across three Bosses' Passives, and the differing Boss identity
  spellings. Apply the values as recorded. If the §6.1 parenthetical is rendered
  for consistency with the existing rows, it is a presentation detail that
  changes no stored value; TASK-171 left it to this task's discretion and it
  changes nothing.
- **Still deferred after this task:** the two `BossDefinition` rows, the two
  Domain `BossDefinitions` content entries, and all runtime behavior are NOT
  created here. `DATABASE.md` §5 item 4 and §1 note item 5 govern their
  provisioning, which is a separate task.
- **Report, do not fix, adjacent gaps** (`AGENTS.md` §16). Any other gap found
  while editing gets reported with issue, location, impact, and a suggested
  follow-up task — not fixed inline.
- **Do not modify `tasks/completed/` or TASK-171.**

---

## Testing Requirements

This task changes no code, so it produces no unit, integration, or gameplay
test. Its verification is a **content-fidelity check**, a **contract-boundary
check**, a **duplication check**, and a **scope/isolation validation**, at the
depth `core/validation.md` requires for a MEDIUM-risk GAMEPLAY-CHANGE applied
through documentation.

### Required Verification

```text
[ ] Content fidelity (Sơn Thạch Vệ)   — name, boss-son-thach-ve, Thổ,
                                        HP/MaxHP 3000, ATK 120, DEF 0, Idle,
                                        EnrageThreshold 1500,
                                        son-thach-ve-enrage, Boss HP ≤ 50%,
                                        +20% ATK, 3 Turns, no re-trigger,
                                        PassiveThreshold null,
                                        earthquake, 150 Base Damage, Charge 5,
                                        CD 0, no secondary effect
[ ] Content fidelity (Kim Lôi Vương)  — name, boss-kim-loi-vuong, Kim,
                                        HP/MaxHP 2800, ATK 140, DEF 0, Idle,
                                        EnrageThreshold 2100,
                                        kim-loi-vuong-combo, Player Combo ≥ 4,
                                        +20% ATK, 1 Turn, existing reset and
                                        reapplication semantics,
                                        PassiveThreshold null,
                                        thunder-strike, 180 Base Damage,
                                        Charge 5, CD 0, no secondary effect
[ ] Enrage/Passive separation check   — each Boss's EnrageThreshold is a §6.1
                                        field distinct from its §6.2 Passive
                                        trigger; §5 item 4's transition is
                                        referenced, not re-authored; no second
                                        Enrage mechanism
[ ] Passive representation check      — both Passives use the existing
                                        BossState.StatusEffects[] BuffDebuff
                                        TargetStat = "ATK" shape; no new status
                                        type, no new TargetStat
[ ] Duration check                    — 3 Turns / 1 Turn use COMBAT_RULES.md
                                        §5.3's existing lifecycle; Sơn Thạch
                                        Vệ's no-retrigger behavior is recorded
                                        per PASSIVE_RULES.md §4 item 3
[ ] Skill mechanism check             — both Skills use §6.3's existing charge/
                                        cooldown mechanism; CD 0 needs no new
                                        rule; no board effect, no new damage
                                        type
[ ] Identity verbatim check           — all eight supplied identity strings
                                        recorded exactly as TASK-171 records
                                        them, not normalized
[ ] Vocabulary check                  — no new Element, trigger category, effect
                                        type, status, stat, resource,
                                        representation, or State
[ ] Count-reference check             — every document that stated the
                                        content-defined Boss count as 3 or the
                                        row set as exactly three is corrected
[ ] Negative check                    — Hỏa Long, Thủy Ma, Mộc Yêu are
                                        byte-identical in meaning in every §6
                                        subsection and in §6.2.1–§6.2.3
[ ] Duplication check                 — no pipeline/formula/lifecycle/schema
                                        text copied from its owner document
[ ] Cross-reference check             — §6.2/§6.3 point at COMBAT_RULES.md
                                        §3/§3.4/§5.3/§5.5, PASSIVE_RULES.md
                                        §3/§4, and GAME_STATE.md §2.4 rather
                                        than restating them
[ ] Consumer check                    — the downstream provisioning task can
                                        determine both rows' five columns from
                                        docs/ alone
[ ] Scope validation                  — MVP_SCOPE.md §1/§2
                                        (quality/scope-validation.md)
[ ] Isolation verification            — src/ (0 files), tests/ (0 files), no
                                        migration, all other docs/ files
                                        unchanged, TASK-171 and tasks/completed/
                                        unchanged
[ ] Unit tests                        — N/A (no code)
[ ] Integration tests                 — N/A (no code)
[ ] Gameplay scenarios                — N/A (no code; authored downstream)
```

### Key Edge Cases

- **A value TASK-171 does not contain** — STOP; do not author it.
- **A question TASK-171 already resolved** (the Enrage/Passive separation, the
  identity spellings, the `PassiveThreshold` disposition, the charge and cooldown
  values) — apply the recorded answer; do not re-open it.
- **Collapsing Enrage with the Passive** because Sơn Thạch Vệ's two boundaries
  coincide numerically — STOP; TASK-171 states the separation is deliberate and
  the operators differ by design.
- **Normalizing the supplied identities** to match the three existing Bosses'
  observed `<bossId>-<effect>` PassiveId or bare SkillId spellings — do not;
  `BOSS_RULES.md` §6.4 states no such convention, and TASK-171 recorded the
  supplied forms verbatim after checking.
- **Reading `PassiveThreshold = null` as always-active** — do not;
  `DATABASE.md` §1 note item 3 states explicitly that it is not.
- **A perceived need for a new `TargetStat`, status type, trigger category,
  Element, event, protocol member, or state member** — STOP; the existing closed
  sets suffice (TASK-171 D-14).
- **A perceived need for a schema change or a new
  `PassiveDefinition`/`SkillDefinition` member** — STOP; `DATABASE.md` §1 note
  items 1/3/4 already express every value.
- **An apparent conflict between TASK-171 and an authoritative rule** — STOP per
  `AGENTS.md` §4 and report both sides; do not silently reconcile.
- **An adjacent unrelated gap discovered mid-edit** — report per `AGENTS.md` §16;
  do not fix inline.

---

## Stop Conditions

Universal stop conditions in `AGENTS.md` §20 and `.ai/README.md` §13 apply.

- **TASK-171 is not actually READY, or any of its decisions is unresolved.**
  **STOP** — report the exact unresolved slot. (Verified clear at creation;
  re-verify at execution.)
- **TASK-171 conflicts with authoritative gameplay documentation in a way
  requiring a new gameplay decision.** **STOP** — report both sources by file and
  section; do not reconcile silently (`AGENTS.md` §4).
- **The ownership of a required contract cannot be determined.** **STOP** — do
  not guess the canonical owner
  (`.ai/workflow/documentation/documentation-change.md` §3).
- **Applying the contract would require a new state member.** **STOP** —
  `GAME_STATE.md` §2.4's tree already carries every field both Bosses need.
- **Applying the contract would require a new event or protocol member.**
  **STOP** — `BOSS_RULES.md` §7's event set and `SIGNALR_PROTOCOL.md` §4.4's
  projection already cover both Bosses.
- **Applying the contract would require a schema change.** **STOP** per
  `AGENTS.md` §7 and §18; `DATABASE.md` §1 note item 1 states no column may be
  added unless an existing authoritative document explicitly requires it.
- **Either Boss cannot be expressed by the existing Boss/Passive/Skill
  contracts.** **STOP** — report the exact missing contract. (Verified
  expressible by TASK-171 D-14; if evidence to the contrary appears, report it
  rather than extending a contract.)
- **A new Element is proposed.** **STOP** — `ELEMENT_RULES.md` §1 fixes exactly
  five, and §7 defers dual-element entities.
- **A new `EffectDefinition` field is required.** **STOP** — that issue is closed
  by TASK-167/TASK-168 and confirmed closed by TASK-170 GAP-B.
- **A new gameplay rule, mechanic, or Product Owner decision is required.**
  **STOP** — this task applies recorded content; it does not create rules.
- **A TASK-171 decision must be reinterpreted.** **STOP** — that is a TASK-171
  matter, not this task's.
- **A new architecture decision is required** (`AGENTS.md` §18). **STOP** —
  report ADR impact instead of deciding.
- **Applying the contract would require modifying source code.** **STOP** — this
  task is documentation-only.
- **Applying the contract would require provisioning a database row.** **STOP** —
  provisioning is a separate task (`DATABASE.md` §5 item 4).
- **Executing this task would require modifying any file other than those listed
  in §"Affected Files & Areas".** **STOP** — the boundary is explicit.
- **An existing task already owns either Boss's authoring.** **STOP** — name it
  and do not duplicate it.
- If the task exceeds 7 skills or crosses multiple uncoupled architectural
  boundaries: **STOP & decompose**.

---

## Downstream Dependency

```text
TASK-171  (Product Owner decision input — READY, decision-complete)
    ↓  decisions applied by
TASK-172  (THIS TASK — authoritative documentation application)
    BOSS_RULES.md §6, §6.1–§6.4  gain two Boss rows each
    BOSS_RULES.md §6.3.1's closing deferral note retired
    ROADMAP.md / DATABASE.md count references corrected
    ↓
PROVISIONING TASK  (not created here)
    inserts the two BossDefinition rows (boss-def-son-thach-ve,
    boss-def-kim-loi-vuong) and the two Domain BossDefinitions content
    entries, per DATABASE.md §1/§3 item 5 + §5 item 4
        ↓
IMPLEMENTATION / VERIFICATION TASK  (not created here)
    confirms both Bosses' Passives and Skills resolve and behave as authored
```

**ADR required: NO.** This task changes no architecture, no database strategy, no
realtime strategy, no module boundary, no state model, and no infrastructure
(`AGENTS.md` §18). It authors gameplay content inside an existing, already
implemented content model. This determination is **reported**, not authored here
(`tasks/backlog/TASK-171-*.md` §"B-1/B-2 cross-document consequences").

**This task does not create the downstream tasks, and does not implement them.**

**Explicit boundary statement (required):**

```text
No source code.
No database schema change.
No database provisioning.
No new gameplay decision.
No new gameplay mechanic.
No new SignalR contract.
No new Redis contract.
TASK-171 remains unchanged.
```

---

## Completion Evidence

### Changed Files

- `docs/01-game-design/BOSS_RULES.md` (authored Sơn Thạch Vệ and Kim Lôi Vương §6, §6.1–§6.4 rows and details)
- `docs/00-overview/ROADMAP.md` (Phase 2 scope updated to 5 content-defined Bosses)
- `docs/02-technical/DATABASE.md` (reconciled content-defined Boss count to 5)

### Validation Results

- All 14 tests pass in `Task172BossDefinitionsContentTests`:
  - `SonThachVe_ShouldCarryTheDocumentedIdentityContract`
  - `KimLoiVuong_ShouldCarryTheDocumentedIdentityContract`
  - `SonThachVe_ShouldCarryTheDocumentedBaseStats`
  - `KimLoiVuong_ShouldCarryTheDocumentedBaseStats`
  - `BothNewBosses_ShouldCarryTheDocumentedInitialStateAtFullHealth`
  - `SonThachVe_PassiveDefinition_ShouldMatchTheDocumentedContract`
  - `KimLoiVuong_PassiveDefinition_ShouldMatchTheDocumentedContract`
  - `SonThachVe_SkillDefinition_ShouldMatchTheDocumentedContract`
  - `KimLoiVuong_SkillDefinition_ShouldMatchTheDocumentedContract`
  - `BothNewBosses_ShouldExposeDirectDamageThroughDamagePipelineWithNoSecondaryOrBoardEffect`
  - `BothNewBosses_ShouldNotStackOrRescaleWithPetStatsAtCreation`
  - `AllFiveMvpBosses_ShouldHaveDistinctElementsAndDistinctPassiveTriggers`
  - `ExistingThreeBosses_ShouldRemainUnmodifiedInEveryCombatStatAndIdentity`
  - `BossDefinitions_All_ShouldContainExactlyTheFiveDocumentedMvpBossesInOrder`
- Verification suite across Domain, Application, and Infrastructure tests: 291 passed, 0 failed.

### Server Authority & Scope Verification

- [x] Confirmed zero client-authoritative gameplay logic (no code changed)
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)

### Lifecycle

```text
Status set to:  DONE
Move:           tasks/completed/
```
