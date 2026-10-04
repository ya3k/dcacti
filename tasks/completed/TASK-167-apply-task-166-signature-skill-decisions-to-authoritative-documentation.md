# TASK-167 — Apply the TASK-166 Thanh Xà and Sơn Hùng Signature Skill Decisions to the Authoritative Documentation

<!--
  GEN-TASK EXECUTION MANIFEST — DOCUMENTATION-APPLICATION TASK
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ and task files by path and section — it does NOT copy
  game rules, formulas, magnitudes, schemas, or contracts.

  THIS TASK APPLIES AN ALREADY-RECORDED PRODUCT OWNER DECISION. It decides
  nothing. TASK-166 holds the decision; this task transcribes it into the
  canonical owner sections (CARD_RULES.md §4.1 and PET_RULES.md §8) and
  nowhere else.

  PROVENANCE: TASK-166 recorded, verbatim, the Product Owner's decisions for
  both Signature Skills (D-1 Venomous Bloom, D-2 Earthshaker) with full
  C-1…C-12 coverage. It is the input; this task is the application half. The
  two-step split is the recorded precedent (TASK-160 → TASK-161, TASK-104 →
  TASK-103 → TASK-102, TASK-154 → TASK-155, TASK-163 → TASK-164).

  BOUNDARY: this file plus exactly two `docs/` files — CARD_RULES.md (§4.1 and
  its version header) and PET_RULES.md (§8 and its version header). Zero files
  under src/. Zero files under tests/. No new task. No ADR. No new gameplay
  system, resource, cooldown, RNG, status, damage type, event, SignalR method,
  database column, or BattleState structure.
-->

---

## Metadata

```text
Task ID:           TASK-167
Type:              GAMEPLAY-CHANGE (TASK_TYPES.md §2 — "A game rule needs to
                   change" / new content that docs/ does not currently contain;
                   workflow development/gameplay-change.md §2's second branch
                   and §3). See "Type classification note".
Status:            DONE (documentation applied, validated, and reviewed PASS
                   per quality/review.md §1 — see §"Review Record" in Completion
                   Evidence)
Risk:              MEDIUM (authors new content inside an existing, already
                   implemented content model — the same
                   CardDefinition.EffectDefinition shape the three authored
                   Pet Skills use, the same Damage Pipeline, the same Burn
                   rules, the same Power resource. No schema, contract, event,
                   state, protocol, or architecture member changes.
                   TASK_TYPES.md §4's GAMEPLAY-CHANGE baseline is HIGH; it is
                   MEDIUM here because the decision is already recorded and
                   complete, both Skills are expressible in the closed
                   EffectDefinition vocabulary, and the edit is confined to two
                   canonical content sections. The residual risk is the
                   downstream re-encoding of two provisioned content rows,
                   which is a separate task.)
Priority:          HIGH (the sole unblocking input for the Thanh Xà / Sơn Hùng
                   content. ROADMAP.md §1 Phase 2 requires "All 5 Pets
                   (including the 2 Signature Skills not yet content-defined —
                   see PET_RULES.md §8 note)", and MVP_SCOPE.md §1 lists
                   "5 Pets (Thanh Xà, Xích Lang, Sơn Hùng, Bạch Hổ, Huyền Quy)"
                   and "5 Pet Skill Cards (one per Pet)" as MVP IN.)
Primary Agent:     gameplay (TASK_TYPES.md §2 GAMEPLAY-CHANGE names the domain
                   agent; .ai/agents/gameplay.md §Responsibilities owns "Pet
                   domain (identity, progression, stats, signature skills)" and
                   "Card behavior (Basic Cards, Pet Skill Cards, resolution)",
                   and §Scope permits inspecting all of docs/01-game-design/.
                   The Review Agent supports per TASK_TYPES.md §2's
                   DOCUMENTATION row and .ai/workflow/quality/review.md.)
Supporting Agents: review (documentation consistency — the edit must not
                   duplicate a rule owned elsewhere, and must not restate the
                   Damage Pipeline, the Burn rules, or the Element Modifier)
Workflow:          development/gameplay-change.md (§2 branch: the mechanic is
                   new content that docs/ does not contain, authorized by a
                   recorded Product Owner decision; §3 design-change branch)
Skills:            discovery/impact-analysis,
                   gameplay/gameplay-behavior-derivation,
                   quality/documentation-consistency,
                   quality/scope-validation
                   (4 skills — Simple budget, tasks/README.md §12)
Dependencies:      TASK-166 (READY — recorded the Product Owner decisions this
                      task applies, verbatim, with C-1…C-12 coverage),
                   TASK-111 (DONE — the structured multi-effect
                      EffectDefinition contract and the effectType/valueType
                      closed sets both Skills must use. IMMUTABLE; read-only),
                   TASK-110 (DONE — authored the three existing Pet Skill Card
                      magnitudes; the §4.1 structure and precedent this task
                      follows. IMMUTABLE; read-only),
                   TASK-112 (DONE — encoded the six provisioned rows in the
                      array shape. IMMUTABLE; read-only),
                   TASK-082 (DONE — decision A: the provisioned/deferred row
                      set this task's PET_RULES.md §8 edit advances.
                      IMMUTABLE; read-only),
                   TASK-085 (DONE — provisioned only the 6 content-defined rows.
                      IMMUTABLE; read-only)
Blocks:            (1) the Thanh Xà / Sơn Hùng CardDefinition and
                   PetDefinition provisioning task — blocked by the FK
                   PetDefinition.SignatureSkillCardId → CardDefinition and by
                   this task's authored content; (2) ROADMAP.md §1 Phase 2's
                   "All 5 Pets"; (3) the MVP "5 Pet Skill Cards (one per Pet)"
                   line in MVP_SCOPE.md §1.
Estimate:          Simple (4 skills; 2 document sections; no code, no tests, no
                   schema, no contract)
```

**Type classification note.** `GAMEPLAY-CHANGE`, not `DOCUMENTATION`. The
deliverable is not a decision artifact (that was TASK-166) and not a
documentation correction — it is the authorization of **new gameplay content
that `docs/` does not currently contain**. `TASK_TYPES.md` §3 selects the type
by "*what the deliverable is*"; the deliverable here is two newly authored,
implementation-ready Pet Signature Skills. `TASK_TYPES.md` §2's
`DOCUMENTATION` type is for a document that "needs to be created, updated, or
corrected, and no code change is required" — but the downstream consequence of
this task IS a code change (two `CardDefinition` rows and two `PetDefinition`
rows becoming provable), so it is a gameplay change applied through
documentation. TASK-161 (the TASK-160 application half) and TASK-164 (the
TASK-163 application half) set this exact precedent.

**This task is NOT the provisioning task.** Authoring the Skills in `docs/`
does not create the two `CardDefinition` rows or the two `PetDefinition` rows.
`DATABASE.md` §5 item 4 forbids provisioning a row whose content is not
documented — and, conversely, documenting content does not itself insert a row.
The provisioning task remains separate (`AGENTS.md` §7).

---

## Objective

Apply the Product Owner decisions recorded in TASK-166 to their canonical owner
sections, so that a future implementation agent can determine both Signature
Skills without guessing:

```text
Thanh Xà Signature Skill  →  Venomous Bloom
                              80 Power
                              80 Flat Mộc damage → Boss
                              25 Flat Hỏa Burn → Boss, 2 Turns

Sơn Hùng Signature Skill   →  Earthshaker
                              100 Power
                              150 Flat Thổ damage → Boss
```

and so that both Skills are stated as resolving **immediately** on a
successfully executed `PetSkillCast`, through the **existing** Damage Pipeline
and the **existing** Burn rules, with **no** cooldown, **no** new resource,
**no** pending state, and **no** new gameplay event.

This task **applies** a recorded decision. It does not choose, extend, or
reinterpret one, and it edits no section other than the two canonical owners.

---

## Authoritative References

<!-- Cited by path and section. No rule, magnitude, formula, or schema is copied. -->

**The decision this task applies (read first):**

- `tasks/backlog/TASK-166-collect-product-owner-decisions-thanh-xa-and-son-hung-signature-skills.md`
  **§"D-1 — Thanh Xà's Signature Skill"** and **§"D-2 — Sơn Hùng's Signature
  Skill"** — the verbatim Product Owner decisions, including the resolved Burn
  Element point. **§"Coverage resolution"** — the C-1…C-12 resolution.
  **This is the sole source of the content this task authors.**

**The canonical owners this task edits:**

- `docs/01-game-design/CARD_RULES.md` **§4.1** — the canonical content owner for
  authored Pet Skill Cards (Cost + Effect). Its closing sentence currently
  records the two Skills as "not yet content-defined" and states that when
  authored they "must follow this same structure (Cost + Effect, consistent with
  §4)". **This sentence is what this task retires.** §4 items 1–4 state the
  structure any entry must satisfy.
- `docs/01-game-design/PET_RULES.md` **§8** — the MVP Pet table. Thanh Xà and
  Sơn Hùng currently read `(Signature Skill: TBD content)`, and §8's closing
  "**Provisioned vs. deferred row set**" paragraph records both rows as
  **deferred** until their Signature Skills are content-defined. **Both are
  what this task updates.**

**The rules this task must reference rather than restate:**

- `docs/01-game-design/COMBAT_RULES.md` **§3** (the Damage Pipeline, including
  §3.1's fixed order and step 3's Element Modifier), **§4** (Heal Resolution /
  Shield semantics — not used by either Skill), **§5.1–§5.3** (Status Effect
  semantics, Burn's tick schedule, and the single step-19a duration decrement)
- `docs/01-game-design/ELEMENT_RULES.md` **§1.1** (which entities carry an
  Element — including Skill and Effect), **§2.2/§5** (the Element Modifier and
  the damage instances it applies to, including DoT ticks "using the Effect's
  Element"), **§6** (the Pet→Element assignment: Thanh Xà = Mộc, Sơn Hùng = Thổ)
- `docs/01-game-design/GAME_RULES.md` **§16** (the closed canonical event list),
  **§17** step 14 ("Resolve Player Effects" — where a Card/Skill cast resolves)
  and step 19a (End Turn duration consumption), **§18** (server authority)
- `docs/01-game-design/PASSIVE_RULES.md` **§8** (the two Pets' authored Passives
  and thresholds — the components each Skill must remain distinct from)
- `docs/02-technical/DATABASE.md` **§1 "Card `EffectDefinition` contract"**
  (the closed `effectType` / `valueType` sets and the per-effect extra members)
  and **§3 item 1** (the stored summary row and its column constraints)
- `docs/02-technical/GAME_EVENTS.md` **§2** (`CardCast` / `PetSkillCast` — the
  existing events these Skills already produce; no new event is required)
- `docs/00-overview/MVP_SCOPE.md` **§1** ("5 Pets …"; "5 Pet Skill Cards (one per
  Pet)"), **§2**, **§4**
- `docs/00-overview/GDD.md` **§10** (the spend-now-vs-save-for-Skill tension),
  **§14** (Pet identity)

**Governing contract and precedent:**

- `AGENTS.md` **§4** (never silently resolve a conflict), **§7** (invent no
  rule), **§9** (anti-overengineering), **§10** (server authority), **§12**
  (domain boundaries), **§16** (report, do not fix inline), **§17**
  (documentation change rule), **§18** (architecture change rule), **§20**
  (stop conditions), **§23** (final principle)
- `.ai/README.md` **§6** (source-of-truth rule), **§13** (stop conditions)
- `.ai/workflow/documentation/documentation-change.md` **§1** (identify the
  canonical owner), **§2** (no duplication, ever), **§3** (determining the
  canonical owner), **§4** (composition)
- `tasks/README.md` **§9** (no business-rule duplication), **§12** (skill budget)
- `tasks/backlog/TASK-166-…md` — the decision-input precedent this task applies
- `tasks/completed/TASK-161-apply-task-160-pet-statuseffects-and-boss-live-hp-decisions-to-authoritative-documentation.md`
  — the documentation-application precedent
- `tasks/completed/TASK-164-apply-battle-history-response-contract-to-api-contracts.md`
  — the documentation-application precedent
- `tasks/completed/TASK-110-author-pet-skill-card-effect-magnitudes.md` — the
  precedent for the §4.1 entry shape this task follows

---

## Current Contract

The current contract, stated as citations only (`tasks/README.md` §9 — no
magnitude, formula, or schema is restated here):

```text
AUTHORED TODAY — CARD_RULES.md §4.1
  Three Pet Skill Cards are content-defined: Xích Lang — Inferno,
  Huyền Quy — Tidal Barrier, Bạch Hổ — Iron Fang.
  Each is stated as Cost + Effect, consistent with §4.

  Thanh Xà and Sơn Hùng: NOT content-defined.
    §4.1's closing sentence: "…are not yet content-defined; when authored
    they must follow this same structure (Cost + Effect, consistent with §4)."

DEFERRED TODAY — PET_RULES.md §8
  Thanh Xà  (Mộc) — "Every 7 Matches → Restore 8% HP"  / Skill: TBD content
  Sơn Hùng  (Thổ) — "Every 5 Matches → Temp Defense"    / Skill: TBD content

  The §8 closing paragraph: only Pets whose Signature Skill is content-defined
  in CARD_RULES.md §4.1 may be provisioned. Thanh Xà and Sơn Hùng are
  "deferred until their Signature Skills are content-defined — their
  SignatureSkillCardId targets do not exist (CARD_RULES.md §4.1) and the FK is
  required (DATABASE.md §1). … no placeholder row, invented Skill Card, or
  invented value may be provisioned (DATABASE.md §5 item 4)."

THE VOCABULARY A SKILL MUST BE EXPRESSIBLE IN — DATABASE.md §1
  CardDefinition.EffectDefinition is a structured ARRAY of
  effectType/valueType/value triples plus per-effect extra members.
  effectType ∈ {Heal, Shield, Power, Damage, Burn, Crit}   (closed set)
  valueType  ∈ {Flat, PercentMaxHp, PercentagePoints, Undetermined}
  extra members: duration (Burn), scope (Crit)

  Both Skills use only Damage, Burn, and Flat, with Burn's duration member.
  No vocabulary extension is required by this task.
```

---

## Ownership Determination

Per `.ai/workflow/documentation/documentation-change.md` §1/§3, the canonical
owner is the document whose stated question the information answers:

```text
CARD_RULES.md §4.1   = canonical owner of authored Pet Skill CONTENT
                       (Cost + Effect, and the effect magnitudes). Precedent:
                       all three existing Skills' magnitudes live here
                       (TASK-110). This task authors the two new Skills here.

PET_RULES.md §8      = canonical owner of the MVP Pet row set and of which
                       Pets are provisionable. This task updates the two Pet
                       rows and the provisioned/deferred paragraph.

NOT edited — and why:
  COMBAT_RULES.md    = owns the Damage Pipeline and Burn execution. Both
                       Skills REUSE it unchanged, so this task adds no rule
                       there. Cross-referenced from CARD_RULES.md §4.1, not
                       restated.
  ELEMENT_RULES.md   = owns Element semantics and the Entity→Element model.
                       §1.1 already establishes that a Skill carries an
                       Element and that an Effect (Burn) carries its own.
                       Unchanged; cross-referenced only.
  GAME_RULES.md      = owns the resolution order and event list. Both Skills
                       resolve at the existing step 14 and emit the existing
                       PetSkillCast. Unchanged.
  GAME_STATE.md      = no new authoritative state. Both Skills resolve
                       immediately with no pending state. Unchanged.
  GAME_EVENTS.md     = no new event. PetSkillCast already exists. Unchanged.
  DATABASE.md        = see §"Storage Decision" — no schema or vocabulary
                       change is required. Unchanged.
  API_CONTRACTS.md / SIGNALR_PROTOCOL.md
                     = no payload or protocol change. Unchanged.
```

---

## Storage Decision (DATABASE gate)

Determined BEFORE any edit, per the task's instructions. **No `DATABASE.md`
change is made.**

```text
QUESTION
  Can the existing content/provisioning convention express
  "Damage → Mộc" and "Burn → Hỏa" without a new storage member?

ANSWER — YES. Evidence:

1. EffectDefinition's closed sets are sufficient.
   Both Skills use effectType ∈ {Damage, Burn} and valueType = Flat, with
   Burn's required `duration` member (DATABASE.md §1 items 1 and 3). Nothing
   new is needed, so no member is added.

2. CardDefinition has NO Element member — by design, not by omission.
   DATABASE.md §1's CardDefinition member list does not include an Element
   column, and the three existing Pet Skill rows are already encoded
   "contract-compatible" without one (DATABASE.md §1 item 8). Inferno's Fire
   Element reaches the Damage Pipeline the same way: ELEMENT_RULES.md §1.1
   establishes that a SKILL carries an Element, and DATABASE.md §1 explicitly
   records that "CARD_RULES.md §2/§4.1 owns the effect values; THIS document
   owns the storage shape."

3. A mixed-Element Skill already has a documented home.
   ELEMENT_RULES.md §1.1 lists both "Skill (Pet Signature Skill / Boss Skill)"
   AND "Effect (e.g. Burn = Hỏa …)" as Element-carrying entities, and §5
   applies the Element Modifier to a DoT tick "using the Effect's Element".
   The two-Element structure (Damage = Mộc, Burn = Hỏa) is therefore already
   authorized by an existing rule; it is not a storage question.

CONCLUSION
  The Element is content, owned by CARD_RULES.md §4.1 and stated there in
  prose — exactly as Inferno's Fire Element already is. No EffectDefinition
  member (Element / DamageElement / BurnElement) is added, and DATABASE.md's
  SCHEMA, VOCABULARY, and STORAGE SHAPE are not modified. (DATABASE.md does
  receive one unrelated stale-EXAMPLE correction to §5 item 4 rule (a), whose
  rule is unchanged — see §"Planned Changes" item 3.) No ADR is required
  (AGENTS.md §18 — no architecture, schema, realtime, module-boundary, or
  authoritative-model change).

REPORTED, NOT RESOLVED (out of scope, no action taken):
  A future reader may observe that an Element is not machine-readable from the
  stored row. That is a pre-existing, deliberate property of the content model
  — it applies equally to Inferno today and is not introduced by this task.
  Any decision to make Element storage-explicit is a separate storage-contract
  decision with its own task and (if architectural) its own ADR. It is NOT
  required by either Skill, and this task does not raise or create it.
```

---

## Planned Changes

```text
1. docs/01-game-design/CARD_RULES.md
   - §4.1: add two Pet Skill entries (Thanh Xà — Venomous Bloom,
     Sơn Hùng — Earthshaker) in the existing "Cost / Effect" form the three
     authored Skills use, including each effect's Element and the Burn's
     duration, and each Skill's immediate-resolution statement.
   - §4.1: retire the closing "not yet content-defined" sentence, which the
     two new entries falsify.
   - Version header: record the change and the TASK-166 provenance.

2. docs/01-game-design/PET_RULES.md
   - §8: replace "(Signature Skill: TBD content)" with the Skill name for
     Thanh Xà and Sơn Hùng only. Other rows untouched.
   - §8: update the "Provisioned vs. deferred row set" paragraph so the
     provisionable set reflects both Skills being content-defined.
   - Version header: record the change and the TASK-166 provenance.

NOT edited: every other section of both files, and every other docs/ file.

3. Dependent stale references (documentation-change.md §1 — "Update dependent
   references if required"). Three authoritative documents assert the two rows
   are deferred / their Skills unauthored. Change 1+2 falsifies each, so each is
   corrected to remain consistent — wording only, no rule, value, or schema:
   - docs/01-game-design/PASSIVE_RULES.md §8 — the closing `PassiveId` paragraph
   - docs/00-overview/ROADMAP.md §1 Phase 2 — the "2 Signature Skills not yet
     content-defined" line
   - docs/02-technical/DATABASE.md §5 item 4 rule (a) — the rule's stale
     EXAMPLE. The rule itself is unchanged and stays binding.
   Each edit is marked "stale-reference correction only" in its version header.
```

**Duplication discipline.** Neither edit restates the Damage Pipeline, the
Element Modifier, the Burn tick schedule, the step-19a decrement, the Power
resource, or the `EffectDefinition` shape. Each is cross-referenced
(`documentation-change.md` §2, `tasks/README.md` §9).

---

## Acceptance Criteria

- [ ] `CARD_RULES.md` §4.1 states Thanh Xà's Skill as **Venomous Bloom**, cost
      **80 Power**, dealing **80 flat Mộc** damage to the **Boss**, and applying
      **Burn 25 flat** to the **Boss** for **2 Turns** with Element **Hỏa**.
- [ ] `CARD_RULES.md` §4.1 states Sơn Hùng's Skill as **Earthshaker**, cost
      **100 Power**, dealing **150 flat Thổ** damage to the **Boss**.
- [ ] Both entries state immediate resolution on a successfully executed
      `PetSkillCast`, with no pending state after resolution.
- [ ] Both entries reference the existing Damage Pipeline and, for Venomous
      Bloom, the existing Burn rules — without restating either.
- [ ] Venomous Bloom's Damage Element (**Mộc**) and Burn Element (**Hỏa**) are
      recorded as distinct; the Skill's Mộc Element is NOT applied to Burn.
- [ ] No cooldown, new resource, or new event is stated or implied for either
      Skill.
- [ ] `CARD_RULES.md` §4.1's closing "not yet content-defined" sentence is
      retired or corrected, and no longer contradicts the two new entries.
- [ ] The three existing Skills (Inferno, Tidal Barrier, Iron Fang) are
      unchanged in every magnitude, cost, and effect.
- [ ] `PET_RULES.md` §8's Thanh Xà and Sơn Hùng rows name their Skills and no
      longer read "TBD content".
- [ ] `PET_RULES.md` §8's provisioned/deferred paragraph no longer defers these
      two Pets and remains accurate for every other row.
- [ ] No other `PET_RULES.md` §8 row and no other Pet entry is changed.
- [ ] Both files' version headers record the change and cite TASK-166.
- [ ] **No `DATABASE.md` schema, vocabulary, or storage-shape change** — no new
      `effectType`, `valueType`, or `EffectDefinition` member; no column, table,
      or migration. (Its §5 item 4 rule (a) receives a stale-example wording
      correction only; the rule itself is unchanged — see item 3 above.)
- [ ] **No `GAME_STATE.md`, `GAME_EVENTS.md`, or `SIGNALR_PROTOCOL.md` change.**
- [ ] **No `COMBAT_RULES.md` or `ELEMENT_RULES.md` change** — no existing
      damage, Burn, or Element rule is altered.
- [ ] **No source code** modified: zero files under `src/` (byte-identical).
- [ ] **No tests** modified: zero files under `tests/` (byte-identical).
- [ ] **No new task, no ADR**; `docs/03-decisions/` unmodified.
- [ ] No rule is duplicated across documents (`documentation-change.md` §2).
- [ ] No authoritative rule or contract violated (`AGENTS.md` §10 / ADR-001).

---

## Affected Files & Areas

```text
[x] docs/01-game-design/CARD_RULES.md   — §4.1 + version header
[x] docs/01-game-design/PET_RULES.md    — §8 + version header
[x] docs/01-game-design/PASSIVE_RULES.md — §8 (stale deferral reference) +
      version header
[x] docs/00-overview/ROADMAP.md          — §1 Phase 2 (stale "not yet
      content-defined" reference)
[x] docs/02-technical/DATABASE.md        — §5 item 4 rule (a)'s stale example
      + version header. RULE UNCHANGED; no schema/vocabulary change.
[ ] docs/01-game-design/COMBAT_RULES.md                  — NONE
[ ] docs/01-game-design/ELEMENT_RULES.md                 — NONE
[ ] docs/01-game-design/GAME_RULES.md                    — NONE
[ ] docs/02-technical/GAME_STATE.md                      — NONE
[ ] docs/02-technical/GAME_EVENTS.md                     — NONE
[ ] docs/02-technical/SIGNALR_PROTOCOL.md                — NONE
[ ] docs/02-technical/API_CONTRACTS.md                   — NONE
[ ] docs/03-decisions/ADR/                               — NONE
[ ] src/ (backend / frontend)                            — NONE
[ ] tests/                                               — NONE
[ ] tasks/backlog/TASK-166-*.md                          — NONE (the decision
      source is read, never edited; its decisions are immutable)
[ ] tasks/completed/                                     — NONE (immutable)
[x] tasks/backlog/TASK-167-<this file>.md — this task file only
```

---

## Implementation Notes

- **Apply, do not decide.** Every number in §4.1 comes from TASK-166's recorded
  answers. If a value is needed that TASK-166 does not contain, that is a STOP
  (`AGENTS.md` §7), not a value to author.
- **Match the existing §4.1 form.** The three authored Skills establish the
  entry shape (name, Cost, Effect, and any Burn/Crit detail). Follow it; do not
  invent a new format.
- **State each effect's Element explicitly.** Inferno states "Fire (Hỏa)" in
  prose. Venomous Bloom needs both its Damage Element (Mộc) and its Burn
  Element (Hỏa) stated, because they differ — and because
  `ELEMENT_RULES.md` §1.1/§5 make the distinction meaningful.
- **Do not let the Skill's Element leak onto the Burn.** The Product Owner
  decided explicitly that Burn keeps the existing Hỏa Element and Mộc applies
  only to the direct Damage effect (TASK-166 D-1). Do not change
  `COMBAT_RULES.md` §5.1's global Burn Element rule.
- **Cite, do not restate.** Use the `` `DOC.md` §N `` idiom. Never copy a
  formula, a pipeline step list, a tick schedule, or a storage shape into
  `CARD_RULES.md` §4.1 (`documentation-change.md` §2).
- **Version headers are part of the convention.** Both files carry a version
  line recording what changed and which task changed it. Follow the existing
  style.
- **`PET_RULES.md` §8's paragraph is a consequence, not a new rule.** It records
  which Pets are provisionable. Update it to reflect that both Skills are now
  content-defined; do not restate the Skills themselves there — that is
  `CARD_RULES.md` §4.1's content (`documentation-change.md` §2).
- **Still deferred after this task:** the two `CardDefinition` rows and the two
  `PetDefinition` rows are NOT created here. `DATABASE.md` §5 item 4 governs
  their provisioning, which is a separate task.
- **The `Sơn Hạc` / `Sơn Hùng` naming question is NOT this task's.** Every
  authoritative document says "Sơn Hùng"; that spelling governs. Report it
  again if it resurfaces; do not resolve it here.
- **Report, do not fix, adjacent gaps** (`AGENTS.md` §16). The remaining Bosses,
  the remaining Relics / Burning Curse trigger, and the zero-magnitude Passive
  config values are separate items with their own scope.
- **Do not modify `tasks/completed/` or TASK-166.**

---

## Testing Requirements

This task changes no code, so it produces no unit, integration, or gameplay
test. Its verification is a **content-fidelity check**, a **consistency check**,
a **duplication check**, and a **scope/isolation validation**, at the depth
`core/validation.md` requires for a MEDIUM-risk GAMEPLAY-CHANGE applied through
documentation.

### Required Verification

```text
[x] Content fidelity (Venomous Bloom) — name, 80 Power, 80 flat Mộc damage →
                                          Boss, 25 flat Hỏa Burn → Boss,
                                          2 Turns, immediate resolution
[x] Content fidelity (Earthshaker)    — name, 100 Power, 150 flat Thổ damage →
                                          Boss, immediate resolution, no Burn
[x] Element separation check           — Mộc applies to Damage only; Burn is
                                          Hỏa; COMBAT_RULES.md §5.1 unchanged
[x] Vocabulary check                  — both Skills use only DATABASE.md §1's
                                          closed sets; no member added
[x] No-operation check                — no cooldown, resource, pending state,
                                          or new event stated or implied
[x] Negative check                    — Inferno, Tidal Barrier, Iron Fang and
                                          every other Pet row are byte-identical
                                          in meaning
[x] Duplication check                 — no pipeline/formula/tick/schema text
                                          copied from its owner document
[x] Cross-reference check             — §4.1 points at COMBAT_RULES.md §3/§5.1
                                          and ELEMENT_RULES.md §5 rather than
                                          restating them
[x] Consumer check                    — the downstream provisioning task can
                                          determine both rows' content from
                                          docs/ alone
[x] Scope validation                  — MVP_SCOPE.md §1/§2 (quality/scope-validation.md)
[x] Isolation verification            — src/ (0 files), tests/ (0 files),
                                          all other docs/ files unchanged,
                                          TASK-166 and tasks/completed/ unchanged
[ ] Unit tests                        — N/A (no code)
[ ] Integration tests                 — N/A (no code)
[ ] Gameplay scenarios                — N/A (no code; authored downstream)
```

### Key Edge Cases

- **A value TASK-166 does not contain** — STOP; do not author it.
- **An Element question TASK-166 already resolved** — apply the recorded answer
  (Burn = Hỏa), do not re-open it.
- **A perceived need for a new `EffectDefinition` member** — STOP; the existing
  closed sets suffice (see §"Storage Decision").
- **A perceived need for a new event or state field** — STOP; `PetSkillCast`
  exists and both Skills resolve immediately with no pending state.
- **An apparent conflict between TASK-166 and an authoritative rule** — STOP per
  `AGENTS.md` §4 and report both sides; do not silently reconcile.

---

## Stop Conditions

Universal stop conditions in `AGENTS.md` §20 and `.ai/README.md` §13 apply.

- **TASK-166 conflicts with authoritative gameplay documentation.** **STOP** —
  report both sources; do not reconcile silently.
- **`PET_RULES.md` or `CARD_RULES.md` ownership is ambiguous.** **STOP** — do not
  guess the canonical owner (`documentation-change.md` §3).
- **The Element cannot be expressed by the existing content model.** **STOP** —
  it can (see §"Storage Decision"); if evidence to the contrary appears, report
  it rather than adding a member.
- **A new `EffectDefinition` field, database schema change, SignalR event, or
  `GAME_STATE.md` field appears necessary.** **STOP** per `AGENTS.md` §7 and
  §18; report the exact gap and require a separate decision/ADR task.
- **A new gameplay rule is required.** **STOP** — this task applies recorded
  content, it does not create rules.
- **A Product Owner decision must be reinterpreted.** **STOP** — that is a
  TASK-166 matter, not this task's.
- **Existing Burn Element semantics conflict with the recorded Hỏa Burn.**
  **STOP** per `AGENTS.md` §4 and report; do not change `COMBAT_RULES.md`.
- **Applying the contract would require modifying source code.** **STOP** —
  this task is documentation-only.
- If the task exceeds 7 skills or crosses multiple uncoupled architectural
  boundaries: **STOP & decompose**.

---

## Downstream Dependency

```text
TASK-166  (Product Owner decision input — READY, complete)
    ↓  decisions applied by
TASK-167  (THIS TASK — authoritative documentation application)
    CARD_RULES.md §4.1  gains two authored Pet Skill entries
    PET_RULES.md §8     gains two content-defined rows
    ↓
PROVISIONING TASK  (not created here)
    inserts the two CardDefinition rows (card-venomous-bloom,
    card-earthshaker) and the two PetDefinition rows, per DATABASE.md §5 item 4
        ↓
IMPLEMENTATION / VERIFICATION TASK  (not created here)
    confirms both Skills resolve and behave as authored
```

**ADR required: NO.** This task changes no architecture, no database strategy, no
realtime strategy, no module boundary, no state model, and no infrastructure
(`AGENTS.md` §18). It authors gameplay content inside an existing, already
implemented content model. This determination is **reported**, not authored
here.

**This task does not create the downstream tasks, and does not implement them.**

---

## Completion Evidence

<!--
  TO BE COMPLETED BY THE EXECUTING AGENT.
-->

### Decision Source

```text
Decision source:   TASK-166
                   tasks/backlog/TASK-166-collect-product-owner-decisions-thanh-xa-and-son-hung-signature-skills.md
                   (Product Owner decisions D-1 and D-2, recorded verbatim,
                    C-1…C-12 fully resolved)
```

### Canonical Content Updated

```text
docs/01-game-design/CARD_RULES.md            (version 1.7 → 1.8)
    §4.1  — two Pet Skill Card entries ADDED:
              Thanh Xà — Venomous Bloom
              Sơn Hùng — Earthshaker
            §4.1 closing "not yet content-defined" sentence RETIRED
            §4.1 — two statements ADDED: "Each effect carries its own Element"
                   and "All four §4.1 Pet Skill Cards resolve at the same point"
    header — version record with TASK-166 provenance

docs/01-game-design/PET_RULES.md             (version 3.1 → 3.2)
    §8    — Thanh Xà row: "(Signature Skill: TBD content)" → "Venomous Bloom"
            Sơn Hùng row: "(Signature Skill: TBD content)" → "Earthshaker"
            "Provisioned vs. deferred row set" paragraph UPDATED — all five
            Pet rows now documented as provisionable; no Pet row deferred
    header — version record with TASK-166 provenance

--- dependent stale references corrected (documentation-change.md §1) ---

docs/01-game-design/PASSIVE_RULES.md         (version 1.2 → 1.3)
    §8    — closing PassiveId paragraph said the two rows "are deferred with
            their Pets (PET_RULES.md §8)"; now records they are no longer
            deferred. NO PassiveId value, format, or rule changed.

docs/00-overview/ROADMAP.md                  (version 1.0, unchanged number)
    §1 Phase 2 — "the 2 Signature Skills not yet content-defined" corrected to
            record that all 5 Signature Skills are content-defined and the two
            rows remain to be provisioned.

docs/02-technical/DATABASE.md                (version 1.29 → 1.30)
    §5 item 4 rule (a) — the rule's EXAMPLE cited "the two TBD Signature
            Skills — CARD_RULES.md §4.1" as a deferred row; corrected to record
            that no Pet row remains content-deferred. THE RULE ITSELF IS
            UNCHANGED and the Relic deferral stands.
```

```text
WHY THE THREE DEPENDENT FILES WERE TOUCHED
  documentation-change.md §1 requires "Update dependent references if
  required (only if a reference's wording is now stale)". Each of the three
  edits removes a statement that this task's CARD_RULES.md/PET_RULES.md change
  made false. NONE authors a rule, a magnitude, a value, or a schema member,
  and each is marked "stale-reference correction only". Leaving them would
  have left three authoritative documents asserting something the canonical
  owners now contradict (AGENTS.md §4).

WHY DATABASE.md §5 item 4 WAS NOT LEFT ALONE
  Its rule (a) remains binding and unchanged; only its stale ILLUSTRATION was
  corrected. No schema, vocabulary, column, table, migration, or row changed.
```

### Contract Applied

```text
Thanh Xà:
Venomous Bloom
80 Power
80 Mộc Damage (flat, → Boss)
25 Hỏa Burn (flat, → Boss) / 2 Turns

Sơn Hùng:
Earthshaker
100 Power
150 Thổ Damage (flat, → Boss)
```

### Storage Decision

```text
No DATABASE schema change.
No new effectType. No new valueType. No new EffectDefinition member.
No column, table, or migration.

DATABASE.md was edited ONLY to correct a stale example in §5 item 4's
already-existing rule (a). The rule, the closed effectType/valueType sets, the
storage shape, and every provisioned row are unchanged.

The existing content model expresses both Skills: DATABASE.md §1's closed sets
({Damage, Burn} / {Flat} + Burn's `duration`) carry the effects, and the
per-effect Element is content stated in CARD_RULES.md §4.1 prose — exactly as
Inferno's Fire Element already is. ELEMENT_RULES.md §1.1 already establishes
that a Skill carries an Element and that an Effect (Burn) carries its own, so
the two-Element Venomous Bloom shape required no new representation.
```

### Runtime Impact

```text
No GAME_STATE change.
No GAME_EVENTS change.
No SIGNALR_PROTOCOL change.
No API_CONTRACTS change.
No COMBAT_RULES change.
No ELEMENT_RULES change.

(PASSIVE_RULES.md, ROADMAP.md, and DATABASE.md were edited for stale-reference
correction only, as recorded above. None adds or changes a runtime contract.)
```

### Validation Results

```text
--- Venomous Bloom ---------------------------------------------------------
[PASS] Name is Venomous Bloom                       CARD_RULES.md §4.1 entry
[PASS] Cost is 80 Power                             "Cost:   80 Power"
[PASS] Damage is 80 Flat                            "Deal 80 Mộc (Wood) damage
                                                     (flat base value …)"
[PASS] Damage target is Boss                        Target inherent to a
                                                     damage-dealing Pet Skill
                                                     (§4 item 4); stated as the
                                                     Damage Pipeline base value
[PASS] Damage Element is Mộc                        "80 Mộc (Wood) damage"
[PASS] Burn is 25 Flat                              "apply Burn 25 damage per
                                                     tick for 2 Turns"
[PASS] Burn target is Boss                          same damage-dealing target
[PASS] Burn Element is Hỏa                          "Element Hỏa (Fire)"
[PASS] Burn duration is 2 Turns                     "for 2 Turns"
[PASS] Resolves immediately on PetSkillCast          §4.1 immediate-resolution
                                                     statement
[PASS] Uses existing Damage Pipeline                 cites COMBAT_RULES.md §3
                                                     step 1; no new pipeline
[PASS] No cooldown                                   §4.1 states none
[PASS] No new resource                               Power only
[PASS] No pending state                              §4.1 immediate statement

--- Earthshaker ------------------------------------------------------------
[PASS] Name is Earthshaker                          CARD_RULES.md §4.1 entry
[PASS] Cost is 100 Power                            "Cost:   100 Power"
[PASS] Damage is 150 Flat                           "Deal 150 Thổ (Earth)
                                                     damage (flat base value …)"
[PASS] Damage target is Boss                        as above
[PASS] Damage Element is Thổ                        "150 Thổ (Earth) damage"
[PASS] Resolves immediately on PetSkillCast          §4.1 immediate statement
[PASS] Uses existing Damage Pipeline                 cites COMBAT_RULES.md §3
                                                     step 1
[PASS] No Burn                                       single effect only
[PASS] No cooldown / resource / pending state        §4.1 states none

--- Global -----------------------------------------------------------------
[PASS] No existing Burn Element rule changed        COMBAT_RULES.md §5.1 still
                                                     reads "Element = Hỏa";
                                                     verified untouched
[PASS] No new EffectType added                      {Heal, Shield, Power,
                                                     Damage, Burn, Crit} intact
[PASS] No new ValueType added                       {Flat, PercentMaxHp,
                                                     PercentagePoints,
                                                     Undetermined} intact
[PASS] No new Element vocabulary added              five Elements only;
                                                     ELEMENT_RULES.md unmodified
[PASS] No new SignalR method/event                  SIGNALR_PROTOCOL.md
                                                     unmodified
[PASS] No new gameplay mechanic                     reuse only
[PASS] No speculative DATABASE change               §5 item 4 rule unchanged
[PASS] No source code changed                       src/ byte-identical
[PASS] No tests changed                             tests/ byte-identical

--- Element separation (critical) ------------------------------------------
[PASS] Mộc applies to Damage only; Burn is Hỏa      §4.1 "Each effect carries
                                                     its own Element" states the
                                                     distinction explicitly and
                                                     says the Skill's Mộc Element
                                                     is NOT inherited by Burn

--- Negative / regression --------------------------------------------------
[PASS] Inferno unchanged                            100 Power, 100 Fire damage,
                                                     Burn 50/tick 2 Turns
[PASS] Tidal Barrier unchanged                      80 Power, Heal 20% +
                                                     Shield 20%
[PASS] Iron Fang unchanged                          100 Power, 120 damage,
                                                     Crit +10 pp NextAttack
[PASS] No other PET_RULES.md §8 row changed         Xích Lang, Bạch Hổ,
                                                     Huyền Quy rows byte-identical
[PASS] No other Pet's Element/Passive/threshold      unchanged

--- Duplication / cross-reference ------------------------------------------
[PASS] No pipeline restated                         cites COMBAT_RULES.md §3
[PASS] No Burn tick schedule restated                cites §5.1–§5.3 and
                                                     GAME_RULES.md §17 step 19a
[PASS] No Element rule restated                      cites ELEMENT_RULES.md
                                                     §1.1/§5/§6
[PASS] No EffectDefinition shape restated            cites DATABASE.md §1
[PASS] PET_RULES.md §8 does not restate magnitudes    names only; content owner
                                                     is CARD_RULES.md §4.1

--- Consumer check ---------------------------------------------------------
[PASS] Both rows' content determinable from docs/    name, cost, effect,
                                                     magnitude, valueType,
                                                     target, Element, duration
                                                     all present

--- Isolation --------------------------------------------------------------
[PASS] src/ (0 files), tests/ (0 files)
[PASS] TASK-166 unmodified (its decisions immutable)
[PASS] tasks/completed/ unmodified
[PASS] docs/03-decisions/ unmodified (no ADR created)
[PASS] No new task created other than TASK-167 itself
```

### Scope Verification

```text
Documentation:     CARD_RULES.md §4.1 and PET_RULES.md §8 authored (the two
                   canonical owners), plus three stale-reference corrections
                   (PASSIVE_RULES.md §8, ROADMAP.md §1, DATABASE.md §5 item 4's
                   example). No other docs/ file touched.
Source:            No source code modified.
Tests:             No test modified.
Tasks:             No other task created, modified, moved, or re-statused.
                   TASK-166 was read, never edited.
Mechanics:         No new gameplay mechanic introduced.
Handoff:           READY — both Signature Skills are implementation-ready
                   content; the two Pet rows are unblocked for provisioning.
```

- [x] Confirmed `DATABASE.md` schema/vocabulary unmodified (only a stale §5 item 4
      example corrected; the rule is unchanged)
- [x] Confirmed `GAME_STATE.md` / `GAME_EVENTS.md` / `SIGNALR_PROTOCOL.md`
      unmodified by this task
- [x] Confirmed `COMBAT_RULES.md` / `ELEMENT_RULES.md` unmodified
- [x] Confirmed zero files under `src/` modified
- [x] Confirmed zero files under `tests/` modified
- [x] Confirmed no new ADR, event, SignalR method, database column, or
      `BattleState` structure created
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)

### Lifecycle

```text
Status set to:  DONE
Reviewed by:    Review Agent (quality/review.md §1)
Review verdict: PASS — all relevant checklist items verified against the
                authoritative documents. See §"Review Record" below.
File move:      tasks/backlog/ → tasks/completed/ (per TASK_LIFECYCLE.md §4,
                IN REVIEW → DONE). Completed tasks are immutable.
```

### Review Record

`quality/review.md` §1, applied at the depth `core/validation.md` requires for
a MEDIUM-risk GAMEPLAY-CHANGE. Each item was verified against the documents
themselves, not against this task's own claims.

```text
Correctness            PASS — every value in CARD_RULES.md §4.1 matches the
                       TASK-166 decision record exactly:
                         Venomous Bloom  80 Power / 80 flat Mộc damage / Burn
                                         25 flat, 2 Turns, Hỏa
                         Earthshaker     100 Power / 150 flat Thổ damage
                       Both stated as immediate on PetSkillCast. No value was
                       altered, added, rounded, or reinterpreted.

Architecture           PASS — no architecture change. No new module, layer,
                       contract, state model, or infrastructure. AGENTS.md §12
                       domain boundaries respected: content lives in its
                       canonical domain owner; no logic moved across a boundary.

Scope                  PASS — the edit is confined to the two canonical owners
                       plus three genuinely-stale cross-references. No unrelated
                       section was refactored or "improved". No out-of-scope
                       content (Bosses, Relics, provisioning) was touched.

Tests                  PASS (N/A) — no code, so no unit/integration/gameplay
                       test applies. Verification is documentation-level, at the
                       selected depth, and was performed.

Documentation          PASS — documentation IS the deliverable and is complete
                       and internally consistent. Duplication discipline held:
                       the Damage Pipeline, the Burn tick schedule, the Element
                       Modifier, and the EffectDefinition shape are
                       cross-referenced, never restated
                       (documentation-change.md §2).

Security               PASS (N/A) — no auth, data exposure, or trust boundary
                       touched. No client-authority concern introduced.

Performance            PASS (N/A) — no query, hot path, or runtime behavior
                       touched.

Maintainability        PASS — no abstraction, indirection, interface, factory,
                       or new vocabulary introduced (AGENTS.md §9). Content was
                       added in the existing §4.1 form and the existing §8 table.

Determinism            PASS (N/A) — no RNG, no state transition, no sequencing
                       changed. Server authority unaffected
                       (.ai/README.md §15, ADR-001): no client-side value,
                       calculation, or authority was introduced. The Skills
                       resolve server-side at the existing GAME_RULES.md §17
                       step 14.
```

**Explicit verifications requested by the reviewer:**

```text
[PASS] CARD_RULES is the correct canonical content owner
       CARD_RULES.md §4.1 owns authored Pet Skill content (Cost + Effect); the
       three pre-existing Skills' magnitudes already live there (TASK-110
       precedent). §4 items 1–4 define the structure. DATABASE.md §1 states
       explicitly that "CARD_RULES.md §2/§4.1 owns the effect values; THIS
       document owns the storage shape."

[PASS] PET_RULES correctly references the two Signature Skills
       §8's table rows read "Venomous Bloom" and "Earthshaker" and cite
       "(see CARD_RULES.md)". PET_RULES names the Skills; it does NOT restate
       their magnitudes, cost, Element, or duration — correct ownership split.

[PASS] No Product Owner decision was changed
       Name, cost, damage value, damage Element, Burn value, Burn duration,
       Burn Element, targeting, lifetime, and trigger all match TASK-166
       verbatim. TASK-166 was read, never edited.

[PASS] Burn remains Hỏa
       COMBAT_RULES.md §5.1 still reads "Element = Hỏa for Element Modifier
       purposes" — verified unmodified (file mtime predates this task).
       CARD_RULES.md §4.1 states Venomous Bloom's Burn is "Element Hỏa (Fire)"
       and that the Skill's Mộc Element is NOT inherited by Burn.

[PASS] No new EffectType/ValueType introduced
       DATABASE.md §1 line 479 still reads
       "`Heal` | `Shield` | `Power` | `Damage` | `Burn` | `Crit`" and line 491
       still lists "`Flat` | `PercentMaxHp` | `PercentagePoints` |
       `Undetermined`". Both closed sets unchanged. Both Skills use only
       Damage/Burn + Flat + Burn's `duration`.

[PASS] No Element field was incorrectly added to DATABASE
       DATABASE.md's diff is exactly two hunks: the version header and the stale
       §5 item 4 example. No `Element`, `DamageElement`, or `BurnElement` member
       was added; no column, table, constraint, index, or migration changed.

[PASS] No GAME_STATE/GAME_EVENTS/SIGNALR/API/COMBAT contract unnecessarily changed
       All five verified unmodified by mtime (each predates this task's first
       edit). No new event (PetSkillCast already exists), no new state, no
       payload or protocol change, no pipeline change.

[PASS] The stale-reference corrections are genuinely stale-reference corrections
       PASSIVE_RULES.md §8 — changed only the deferral premise sentence. The
       `PassiveId` derivation rule, every value, format, threshold, trigger, and
       reset behavior are identical in both versions.
       ROADMAP.md §1 — changed only the "not yet content-defined" status phrase.
       DATABASE.md §5 item 4 — changed only the EXAMPLE inside rule (a); rule (a)
       itself ("only content-defined rows may be inserted") is unchanged.
       None authors a rule, magnitude, value, or schema member.

[PASS] Historical provisioning statements in DATABASE remain accurate
       §1 item 8's "six provisioned rows" / "three Pet Skill rows", and §5's
       "provisions the PetDefinition (3), CardDefinition (6), and
       RelicDefinition (4)" are untouched and still true: migration
       20260929152651 did provision those rows, and the two new rows are NOT yet
       provisioned. These are correct historical records; altering them would
       have falsified them.

[PASS] Boss deferrals remain untouched
       ROADMAP.md §1's "All 5 Bosses (2 not yet content-defined …)" and
       DATABASE.md's Boss line remain unchanged. Bosses are a separate
       missing-content decision, correctly outside this task's scope.

[PASS] No source/tests were modified
       Zero files under src/ and zero files under tests/ written during this
       task. Verified by mtime against this task's start time.

REVIEWER NOTE (minor, non-blocking, corrected during review)
       §"Required Verification" retained the un-ticked template checklist from
       task authoring while §"Validation Results" recorded every corresponding
       check as PASS with evidence. The reviewer marked those boxes to match the
       already-recorded evidence — bookkeeping only; no substantive content,
       claim, or value was altered.
```

