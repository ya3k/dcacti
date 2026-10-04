# TASK-171 — Collect the Product Owner's Decisions for the Two Remaining MVP Bosses

<!--
  GEN-TASK EXECUTION MANIFEST — PRODUCT-OWNER DECISION-INPUT TASK
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ and task files by path and section — it does NOT copy
  game rules, formulas, magnitudes, schemas, or contracts.

  THIS TASK DECIDES NOTHING, DOCUMENTS NOTHING, AND IMPLEMENTS NOTHING. Its
  only purpose is to capture the Product Owner's explicit answers to the two
  Boss content decisions in §"Decision Options" below, so that a downstream
  documentation-application task can author them at their canonical owner
  (BOSS_RULES.md §6, §6.1–§6.4), and so that the deferred BossDefinition
  provisioning work can later be reconsidered.

  AN AGENT MUST NOT ANSWER THESE QUESTIONS. If no Product Owner answer is
  present, the agent reports the task as awaiting input and stops. Choosing,
  recommending, ranking, or defaulting either Boss — its name, Element, base
  stats, Passive, trigger, Skill, timing, charge, cooldown, secondary effect,
  magnitudes, or identities — is the single prohibited action of this task.

  STATUS: COMPLETE. The Product Owner has supplied both Bosses' full
  specifications, and every required decision slot is now recorded as DECIDED.
  Three conflicts were raised and reported rather than resolved by an agent
  (Initial State = ACTIVE; Kim Lôi Vương EnrageThreshold = null;
  BossDefinitionId = BossId); each was resolved by an explicit Product Owner
  decision in the following round. No value was normalized, reinterpreted, or
  inferred by this task at any point.

  PROVENANCE: BOSS_RULES.md §6's closing note records that "Two additional MVP
  Bosses (5 total per GAME_RULES.md §19 scope) are not yet content-defined" and
  lists exactly what each must declare when authored. ROADMAP.md §1 Phase 2
  requires "All 5 Bosses (2 not yet content-defined …; each requires Element,
  Passive, Skill, stats)". DATABASE.md §1/§3 item 5 permits provisioning only
  content-defined Bosses (currently 3) and forbids placeholder rows. TASK-170's
  GAP-A analysis classified this as `E. Not actually a gap after full
  authoritative-document review` and reported the residual fact: the 2 Bosses
  are UNAUTHORED CONTENT requiring Product Owner decisions, not a documentation
  inconsistency. This task is the smallest artifact that can supply that input.
  Every prior task that reached this boundary STOPPED rather than guessing:
  TASK-124, TASK-153, TASK-159, TASK-160, and TASK-170 each record it as
  deferred / not created / outside their scope.

  BOUNDARY: this file only. Zero files under docs/. Zero files under src/ or
  tests/. No task file is modified. No ADR is created. No new Element, gameplay
  system, resource, cooldown, RNG, status, damage type, event, SignalR method,
  database column, or BattleState structure is introduced. The TASK-170 GAP-B
  EffectDefinition/Element conclusion is CLOSED and is not reopened here.
-->

---

## Metadata

```text
Task ID:           TASK-171
Type:              DOCUMENTATION (TASK_TYPES.md §2 — "Change docs/ content —
                   documentation is the primary output, not code"; workflow
                   documentation/documentation-change.md). The deliverable is a
                   recorded decision artifact. See "Type classification note".
                   This task itself edits no docs/ file; it collects the input
                   the downstream documentation task consumes.
Status:            DONE
Risk:              LOW (input capture only — no authoritative document is
                   edited, no rule is changed, no magnitude is authored, no
                   schema is touched, and no code exists in scope.
                   TASK_TYPES.md §4's DOCUMENTATION baseline is LOW–MEDIUM; it
                   is LOW here because this task writes to no `docs/` file and
                   touches no cross-referenced contract — the contracts it
                   collects decisions FOR remain untouched by it. Risk rises to
                   HIGH only downstream, when the documentation-application task
                   authors two new Bosses and re-encodes the provisioned content
                   row set.)
Priority:          HIGH (the sole unblocking input for the remaining MVP Boss
                   content. MVP_SCOPE.md §1 lists "5 Bosses" and "Element,
                   Passive, Skill per Boss" as MVP IN; ROADMAP.md §1 Phase 2
                   requires "All 5 Bosses". Three of the five are content-defined;
                   two are unbuildable until this decision exists. TASK-170
                   classified the gap and reported exactly this decision-input
                   work; nothing else can proceed without it.)
Primary Agent:     orchestrator (task-lifecycle / requester coordination — this
                   task records requester input; TASK_TYPES.md §2 DOCUMENTATION
                   names the Review Agent, and no domain agent may author these
                   values. The orchestrator's own contract permits
                   "Task classification artifacts only" and forbids it to "make
                   game design decisions" — .ai/agents/orchestrator.md §Scope /
                   §Decision Authority — which is exactly this task's posture.)
Supporting Agents: N/A (no domain agent may supply or review the VALUES. Review
                   is limited to confirming that both decision slots exist,
                   that neither is pre-judged, and that any supplied answer is
                   recorded verbatim.)
Workflow:          documentation/documentation-change.md
                   (no docs/ file is edited by this task. The workflow governs
                   the recording discipline — §2 no duplication, §3 canonical
                   owner — and §4 routes the final report through
                   quality/review.md + core/completion.md.)
Skills:            discovery/documentation-discovery,
                   discovery/impact-analysis,
                   gameplay/gameplay-behavior-derivation,
                   quality/scope-validation
                   (4 skills — Simple budget, tasks/README.md §12)
Dependencies:      TASK-170 (BACKLOG, read-only — GAP-A/GAP-B audit. It classified
                     the Boss count as consistent and reported the residual fact
                     that the 2 Bosses are unauthored content requiring Product
                     Owner decisions, and it closed GAP-B.
                     IMMUTABLE; read-only),
                   TASK-046 / TASK-047 / TASK-048 (DONE — the BossId / display
                     name / BossDefinitionId identity contract and its
                     code alignment that BOSS_RULES.md §6.4 records.
                     IMMUTABLE; read-only),
                   TASK-045 / TASK-049 / TASK-051 / TASK-052 / TASK-053
                     (DONE — the BossDefinition persistence contract,
                     provisioning mechanism, three-row set, and the
                     content-defined-rows-only rule this task's decision feeds.
                     IMMUTABLE; read-only),
                   TASK-123 / TASK-124 / TASK-125 / TASK-126 / TASK-154 /
                     TASK-155 / TASK-160 (DONE — the Boss Passive/Skill contract
                     history BOSS_RULES.md §6.2–§6.3 references.
                     IMMUTABLE; read-only)
Blocks:            (1) The authoritative-documentation task that applies the
                   recorded decisions at their canonical owner (BOSS_RULES.md
                   §6, §6.1–§6.4) — it cannot be created until these decisions
                   exist; (2) the two `BossDefinition` provisioning rows
                   (DATABASE.md §1/§3 item 5 — only content-defined Bosses may
                   ever be provisioned); (3) the Domain `BossDefinitions`
                   content entries and any content-side implementation the two
                   Bosses' Passives/Skills require; (4) ROADMAP.md §1 Phase 2's
                   "All 5 Bosses"; (5) the MVP "5 Bosses" line in
                   MVP_SCOPE.md §1.
Estimate:          Normal (4 skills; no code, no tests, no document edits; two
                   Boss decision records with full contract coverage. The
                   answers are this task's INPUT, not its output.)
```

**Type classification note.** `DOCUMENTATION`, not `GAMEPLAY-CHANGE` and not
`FEATURE`. `tasks/TASK_TYPES.md` §2 types a `GAMEPLAY-CHANGE` as a rule change
that is "propagat[ed] … through implementation and tests"; this task changes no
rule and touches no implementation — it only records requester decisions into a
task file. `TASK_TYPES.md` §3 selects the type by "*what the deliverable is*";
here the deliverable is a recorded decision artifact, and the only file written
is a `tasks/` file. This mirrors TASK-166 §"Type classification note",
TASK-160 §"Type classification note", and TASK-104 §"Type classification note"
exactly.

**Why not `GAMEPLAY-CHANGE`.** The downstream task that writes these decisions
into `BOSS_RULES.md` §6 **is** a `GAMEPLAY-CHANGE` (the decision authorizes new
content `docs/` does not currently contain, per `development/gameplay-change.md`
§2's second branch and §3). That classification belongs to the downstream task,
not to this input-capture task. This task is the DECISION-INPUT half; the
documentation-application half is a separate task.

**This task is NOT a substitute for the downstream documentation task.** It
records decisions; it does not write them into `docs/`. Transcribing the
recorded answers into `BOSS_RULES.md` §6 — and only through its canonical-owner
sections — remains the downstream task's deliverable, per
`documentation/documentation-change.md` §3 (canonical owner).

**This task does NOT create TASK-172 or any other task.** Per instruction and
per `TASK_LIFECYCLE.md` §2, a decision-input task is a recording step, not an
authorization to generate downstream work.

**The TASK-170 GAP-B conclusion is CLOSED and is not reopened.** `Element` is
not a stub to be added to `EffectDefinition`; it is Boss *content*
(`ELEMENT_RULES.md` §1.1/§1.2), and this task records only the Boss-side
Element choice. See §"Boundary Confirmations".

---

## Objective

Obtain and record the **Product Owner's explicit decisions** for the **two
remaining MVP Bosses** (`BOSS_RULES.md` §6's "Two additional MVP Bosses … not
yet content-defined"), so that a downstream documentation-application task can
author them at their canonical owner (`BOSS_RULES.md` §6, §6.1–§6.4) as an
implementation-ready content contract, and so that the deferred
`BossDefinition` provisioning work can later be reconsidered without an agent
guessing an Element, a base stat, a Passive, a trigger, a Skill, a timing rule,
a magnitude, or an identity string.

This task **collects and records only**. It does not choose, recommend, rank,
default, infer, design, or implement any Boss, and it edits no authoritative
document.

---

## Why This Task Is Required

The MVP Boss content is incomplete in exactly one place, and it is a **content
decision**, not an engineering effort. Three of the five Bosses are done: Hỏa
Long, Thủy Ma, and Mộc Yêu have authored Elements, Passive triggers, Skills,
timing rules, magnitudes, base stats, and canonical identities, and their
`BossDefinition` rows are provisioned.

```text
WHAT EXISTS (not in question):
  BOSS_RULES.md §6      — the three content-defined MVP Bosses, their Passive
                          triggers, Skills, and Skill timing.
  BOSS_RULES.md §6.1    — their approved base stats (HP/MaxHP, ATK, DEF,
                          EnrageThreshold, Initial State).
  BOSS_RULES.md §6.2    — their canonical Passive rows (effect + trigger),
                          plus §6.2.1–§6.2.4's per-Passive details.
  BOSS_RULES.md §6.3    — Charge Requirement, Cooldown, Base Damage, and
                          secondary effects per Skill, plus §6.3.1's magnitude
                          and duration semantics.
  BOSS_RULES.md §6.4    — the BossId / PassiveId / SkillId identity contract.
  DATABASE.md §1        — the BossDefinition persistence contract, including the
                          PassiveDefinition and SkillDefinition member sets.
  ELEMENT_RULES.md §1   — the Five Elements and the one-Element-per-entity rule.

WHAT IS MISSING (the decision this task requests):
  BOSS_RULES.md §6      — Boss #4 and Boss #5 are not content-defined. §6's
                          closing note states what each must declare when
                          authored (Element; a Passive with an explicit trigger
                          category from §3.2; a Skill with an explicit timing
                          rule and effect; trigger-pattern diversity).
  DATABASE.md §1/§3     — only content-defined Bosses may ever be provisioned
                          (currently 3). The 5-Boss figure is the MVP scope
                          target, not permission to create placeholder rows.
  ROADMAP.md §1 Ph. 2   — "All 5 Bosses (2 not yet content-defined …; each
                          requires Element, Passive, Skill, stats)".
```

**Why the deferral is structurally enforced, not merely administrative.**
`DATABASE.md` §1 note item 5 states only content-defined Bosses may ever be
provisioned, and §3 item 5 forbids "placeholder rows for undefined content". A
`BossDefinition` row's columns are transcribed from `BOSS_RULES.md` §6.1–§6.4,
"never invented" (`DATABASE.md` §1 note item 5, "Row content — transcribed,
never invented"). So a missing content definition blocks the row by
construction, not by choice.

**Why this is a decision and not a derivable fact.** A Boss is content: its
name, Element, base stats, Passive, trigger, Skill, timing, charge, cooldown,
secondary effect, magnitudes, and identities are creative/balance choices that
no authoritative document states. Every candidate source was checked and each
is either silent or affirmative that the value must not be invented:

```text
Checked — and each is NOT a source for these two Bosses:

The three EXISTING Bosses    BOSS_RULES.md §6.4 requires a Boss's identities to
                             be fixed so "no task invents its own", and §2 item
                             2 requires every Boss's Passive and Skill to be
                             mechanically distinct from other Bosses'. Copying
                             Hỏa Long / Thủy Ma / Mộc Yêu, or a variant of one,
                             would be invention AND would collide with §3.2's
                             trigger-pattern diversity requirement.

A Boss's ELEMENT              ELEMENT_RULES.md §1.1/§1.2 constrain Element to
                             exactly one of the Five and make it a matchup
                             input; they do not select an assignment. §6 assigns
                             Pets only and defers Bosses to BOSS_RULES.md. §6's
                             closing note item 1 explicitly says a distinct
                             pairing "is encouraged but not mandated".

The three EXISTING base      BOSS_RULES.md §6.1 gives all three Bosses the same
stat rows                    values and states they are "Project-Owner
                             Approved" and "do not represent formulas or
                             scaling rules". Nothing derives a fourth row.

Player progression /         AGENTS.md §7, §20, §23 and .ai/README.md §8 forbid
expected difficulty /        it; no document defines a Boss-scaling rule.
implementation convenience

A README/lore/name source     docs/ is the only source of truth (AGENTS.md §2).
                             No document derives a Boss from a name or theme.

Design intuition / other games  AGENTS.md §7 and §23 forbid it.
```

**No prior decision exists anywhere.** A repository-wide search of `docs/`,
`tasks/`, and `.ai/` found only *deferrals*: TASK-124, TASK-153, TASK-159,
TASK-160, and TASK-170 each record "the 2 remaining MVP Bosses" as not yet
content-defined, deferred, or outside their scope. TASK-170 recorded the exact
input set a future content task would need and explicitly stated it "neither
collects nor records it". **The decision has never been made.**

Per `AGENTS.md` §7 and §20 ("Missing rule"), `AGENTS.md` §23, and
`.ai/README.md` §8 ("an agent is not a Product Owner and does not decide game
design"), the only correct action is to STOP and request the decision. This is
that request.

---

## Authoritative References

<!-- Cited by path and section. No rule, magnitude, formula, or schema is copied. -->

**The unresolved content (read first):**

- `docs/01-game-design/BOSS_RULES.md` **§6** — the MVP Boss reference tables and,
  at its close, the governing statement that "Two additional MVP Bosses (5 total
  per `GAME_RULES.md` §19 scope) are not yet content-defined", followed by the
  four items each must declare when authored. **This is the B-1/B-2 blocking
  statement and the canonical owner the downstream task edits.**
- `docs/02-technical/DATABASE.md` **§1** ("Persistence contract for
  `BossDefinition`") and **§3 item 5** ("Rows and provisioning") — the
  content-defined-rows-only rule, the "transcribed, never invented" column rule,
  and the three-row set. **This is why the decision structurally blocks
  provisioning.**
- `docs/00-overview/ROADMAP.md` **§1 Phase 2** — "All 5 Bosses (2 not yet
  content-defined — see `BOSS_RULES.md` §6 note; each requires Element, Passive,
  Skill, stats)".

**The contract the two answers must satisfy (every decision slot derives from these):**

- `docs/01-game-design/BOSS_RULES.md` **§1** — the Boss structure
  (`Element`; `HP`/`MaxHP`; `ATK`; `DEF`; `Passive`; `Skill`; `State`), and
  "A battle has exactly one Boss".
- `docs/01-game-design/BOSS_RULES.md` **§2** — the design principle: difficulty
  from mechanics, not HP/ATK inflation; every Boss must have a Passive AND a
  Skill mechanically distinct from each other and from other Bosses'.
- `docs/01-game-design/BOSS_RULES.md` **§3** — the Boss Passive contract: **§3
  item 2's closed list of supported trigger categories** (`Turn`, `Match Count`,
  `Combo`, `Active Pet Power`, `Active Pet HP`, `Boss HP`, `Status`), §3 item 3
  (automatic, no casting input), **§3.1** (determinism; no undeclared random
  branch), **§3.2** (trigger pattern diversity), **§3.3** (firing at Step 18,
  once per player action, after all player damage).
- `docs/01-game-design/BOSS_RULES.md` **§4** — the Boss Skill contract: §4 item 2
  (the timing rule is part of the Boss's design and "must be explicit, not
  implicit"), §4 item 3 (Skill damage traverses the full Damage Pipeline using
  the Boss's Element), §4 item 4 (non-damage effects permitted).
- `docs/01-game-design/BOSS_RULES.md` **§5** — Boss State: §5 item 4
  (**Enrage** — a permanent transition at `BossHP < EnrageThreshold`, a per-Boss
  value in §6.1, with no timer), §5 item 5 (Stun — "For MVP, no content-defined
  Boss applies Stun"), §5 item 3 (a full multi-phase State machine is FUTURE).
- `docs/01-game-design/BOSS_RULES.md` **§6.1** — the base-stat row shape
  (`Element`, `HP`/`MaxHP`, `ATK`, `DEF`, `EnrageThreshold`, `Initial State`).
- `docs/01-game-design/BOSS_RULES.md` **§6.2** — the canonical Passive row shape
  (Passive Effect + Passive Trigger), the `PassiveThreshold` rule for
  match-charged Passives, and §6.2.1–§6.2.4 as the pattern for per-Passive
  detail (representation, magnitude, duration, apply point, reapplication).
- `docs/01-game-design/BOSS_RULES.md` **§6.3** — the Skill row shape: Charge
  Requirement, Cooldown (CD), Base Damage, Secondary Effect & Magnitude; plus the
  post-fire reset rule (`SkillCharge` → 0, `SkillCooldown` → the Boss's cooldown).
- `docs/01-game-design/BOSS_RULES.md` **§6.3.1** — the magnitude/duration
  declaration pattern each Skill's secondary effect follows.
- `docs/01-game-design/BOSS_RULES.md` **§6.4** — the identity contract: the three
  never-collapsed concepts (BossId / display name / `BossDefinitionId`), the
  `boss-<ascii-kebab-case-name>` naming convention, and the PassiveId/SkillId
  roles.
- `docs/01-game-design/BOSS_RULES.md` **§7** — the emitted events
  (`PassiveCharged`/`PassiveTriggered` with `source = "boss"`, `BossSkillCast`,
  `BattleWon`/`BattleLost`) and the fact that no Boss-specific passive event
  exists.
- `docs/01-game-design/BOSS_RULES.md` **§8** — server authority over all Boss HP,
  State, Passive progress, and Skill resolution.

**The vocabulary a Passive/Skill answer must be expressible in:**

- `docs/01-game-design/PASSIVE_RULES.md` **§1–§4** — the Passive model, **§2**
  (charge/threshold evaluation once per Cascade batch), **§3** (the alternate
  trigger list: `Combo`, `HP Threshold`, `Damage Dealt`, `Battle Start`,
  `Card Cast`), **§4** (Reset Behavior: Default / Partial Reset / No Reset).
- `docs/01-game-design/COMBAT_RULES.md` **§3** (Damage Pipeline incl. the Element
  Modifier step), **§4** (Heal Resolution and Shield application semantics),
  **§5.1–§5.3** (Buff/Debuff and Status Effect semantics, the two duration
  models, the single step-19a decrement), **§5.4–§5.5** (the non-`"ATK"`
  `TargetStat` boundary).
- `docs/01-game-design/ELEMENT_RULES.md` **§1** (the Five Elements),
  **§1.1** (Element-carrying entities), **§1.2** (exactly one Element per Boss;
  no dual/multi-element entity), **§2** (Tương Khắc and the Advantage/Neutral/
  Disadvantage modifiers), **§5** (where the Element Modifier applies), **§7**
  (dual-element deferred — not MVP).
- `docs/02-technical/DATABASE.md` **§1 note items 3 and 4** — the closed
  `PassiveDefinition` member set (`passiveId`, `threshold`, `resetBehavior`) and
  the closed `SkillDefinition` member set (`skillId`, `baseDamage`,
  `chargeRequirement`, `cooldownTurns`). **These are the storage forms any answer
  must be expressible in.**
- `docs/02-technical/DATABASE.md` **§3** — the `BossDefinition` column
  constraints, including
  `PassiveDefinition.resetBehavior ∈ {Default, Partial, Persistent}` and
  `threshold = null ⇔ no match-charging threshold (NOT always-active)`.
- `docs/02-technical/GAME_STATE.md` **§2.4** (`BossState`: `BossId`,
  `Element`, `HP`/`MaxHP`/`ATK`/`DEF`, `State`, `PassiveId`, `PassiveProgress`,
  `SkillCharge`, `SkillCooldown`, `StatusEffects[]`), **§2.4.1** (staged fields),
  **§2.4.2** (Boss Passive), **§2.4.3** (Skill charge/cooldown and the fire
  condition), **§2.4.4** (Enrage), **§2.4.5** (Stunned).
- `docs/01-game-design/GAME_RULES.md` **§15** ("Bosses may react to Turn, Match
  Count, Combo, active Pet Power, active Pet HP, Boss HP, or Status"; unique
  mechanics; no same trigger pattern), **§17** step 18a–18c (Boss Response) and
  step 19a (End Turn), **§16** (the canonical event list), **§18** (server
  authority).

**Scope, architecture, and governing contract:**

- `docs/00-overview/MVP_SCOPE.md` **§1** ("5 Bosses"; "Element, Passive, Skill
  per Boss"), **§2**, **§3** ("Boss Phases" is FUTURE), **§4**
- `docs/00-overview/ROADMAP.md` **§1 Phase 1 and Phase 2**
- `docs/03-decisions/README.md` §3 (an ADR never overrides the what/how owned by
  `docs/02-technical/`)
- `AGENTS.md` **§2** (source-of-truth hierarchy), **§4** (never silently resolve
  a conflict), **§7** (invent no rule — the governing section here), **§8** (MVP
  protection), **§9** (anti-overengineering), **§10** (server authority), **§12**
  (domain boundaries), **§16** (report, do not fix inline), **§17** (documentation
  change rule), **§18** (architecture change rule), **§20** (stop conditions),
  **§23** (final principle)
- `.ai/README.md` **§6** (source-of-truth rule), **§8** (an agent is not a
  Product Owner), **§13** (stop conditions and the stop-report format)
- `.ai/agents/orchestrator.md` §Scope / §Decision Authority;
  `.ai/agents/gameplay.md` §Decision Authority (Boss domain)
- `.ai/workflow/development/gameplay-change.md` **§2** (the branch that fires
  when a mechanic does not exist in `docs/`) and **§3** (the design-change branch
  the DOWNSTREAM task follows)
- `tasks/README.md` **§9** (no business-rule duplication), **§12** (skill budget)

**Precedent:**

- `tasks/backlog/TASK-170-audit-classify-boss-count-and-effect-element-gaps.md`
  — the audit that classified GAP-A/GAP-B `E` and reported this exact
  decision-input set without creating it
- `tasks/completed/TASK-166-collect-product-owner-decisions-thanh-xa-and-son-hung-signature-skills.md`
  — the decision-collection precedent this task follows
- `tasks/completed/TASK-160-collect-product-owner-decisions-pet-statuseffects-and-boss-live-hp-projection.md`
  — the Boss-adjacent decision-collection precedent
- `tasks/completed/TASK-104-collect-cardcast-wire-and-shield-contract-decisions.md`
  — the decision-collection precedent

---

## Current Contract

The current contract, stated as citations only (`tasks/README.md` §9 — no
magnitude, formula, or schema is restated here):

```text
CONTENT-DEFINED TODAY — BOSS_RULES.md §6, §6.1–§6.4
  Three Bosses are fully authored: Hỏa Long, Thủy Ma, Mộc Yêu. Each has an
  Element, a base-stat row, a Passive row with an explicit trigger, a Skill row
  with Charge Requirement / CD / Base Damage / secondary effect, detailed
  magnitude-and-duration semantics, and canonical BossId / PassiveId / SkillId.

  §6's closing note: "Two additional MVP Bosses (5 total per GAME_RULES.md §19
  scope) are not yet content-defined. When authored, each must:
    1. Declare an Element (distinct pairing with the other Bosses is encouraged
       but not mandated — multiple Bosses may share an Element; only trigger
       *pattern* diversity is required, per §3.2).
    2. Declare a Passive with an explicit trigger category from §3.2.
    3. Declare a Skill with an explicit timing rule and effect.
    4. Avoid duplicating an existing Boss's trigger category where reasonably
       possible …"

DEFERRED TODAY — DATABASE.md §1 / §3 item 5
  "Only content-defined Bosses (currently 3 — BOSS_RULES.md §6) may ever be
  provisioned; the 5-Boss figure is the MVP scope target (MVP_SCOPE.md §1), not
  permission to create placeholder rows for undefined content."
  Each provisioned row's five columns are "sourced per column, with no value
  computed or invented at provisioning time".

THE STRUCTURAL CONSEQUENCE — DATABASE.md §1 items 3–5
  PassiveDefinition: { "passiveId", "threshold", "resetBehavior" }
  SkillDefinition:   { "skillId", "baseDamage", "chargeRequirement",
                       "cooldownTurns" }
  resetBehavior ∈ {Default, Partial, Persistent}      (closed set)
  threshold = null ⇔ no match-charging threshold (NOT always-active)

  A Boss with no authored Element / Passive / Skill / base stats / identities
  has nothing to transcribe, so no row can exist.

WHAT IS NOT STORED — DATABASE.md §1 note item 1
  MaxHP, ATK, DEF, EnrageThreshold are NOT columns: they "remain sourced from
  the authoritative Domain BossDefinition content at battle creation". The
  display name is not a column either. The persisted row is the
  identity/configuration subset only.

THE TRIGGER VOCABULARY — BOSS_RULES.md §3 item 2 / GAME_RULES.md §15
  Turn | Match Count | Combo | Active Pet Power | Active Pet HP | Boss HP |
  Status   — a closed list.
  PASSIVE_RULES.md §3 adds the alternate trigger forms a Passive declaration may
  use: Combo | HP Threshold | Damage Dealt | Battle Start | Card Cast.

THE ELEMENT VOCABULARY — ELEMENT_RULES.md §1
  Exactly five: Mộc, Hỏa, Thổ, Kim, Thủy. Exactly one per Boss (§1.2).
```

---

## Exact Ambiguity

Two content items are unresolved. Neither is answered anywhere in `docs/`,
`tasks/`, or `.ai/`.

```text
B-1  MVP BOSS #4
     BOSS_RULES.md §6 defines three Bosses and states a fourth and fifth are
     "not yet content-defined". Boss #4 has no display name, no Element, no base
     stats, no Passive, no Passive trigger, no Skill, no Skill timing, no
     magnitudes, and no identities.

     Its BossDefinition row does not exist and cannot be created, because
     DATABASE.md §1/§3 item 5 forbids provisioning a Boss that is not
     content-defined.

     UNRESOLVED: what IS MVP Boss #4 — its Element, its base stats, its Passive
     and its explicit trigger category, its Skill and its explicit timing rule,
     every magnitude and duration, its gameplay role, and its canonical
     BossId / display name / PassiveId / SkillId / BossDefinitionId?

B-2  MVP BOSS #5
     The identical gap for the fifth Boss.

     UNRESOLVED: what IS MVP Boss #5, on every field above?

WHY THESE ARE NOT DERIVABLE
  A Boss is authored content. No document states either Boss; no document
  derives a Boss from the three existing Bosses, from a Pet, from an Element, or
  from a difficulty expectation; and no document authorizes copying or adapting
  an existing Boss. BOSS_RULES.md §2 item 2 and §3.2 require each Boss's
  Passive/Skill to be mechanically distinct and its trigger category to differ
  from the others where possible, so "an existing Boss with different numbers"
  is expressly excluded, not merely unauthored. AGENTS.md §7, §20, and §23 make
  inventing it a STOP, and .ai/README.md §8 states an agent is not a Product
  Owner.
```

**A note on completeness, not on content.** The two Bosses are **independent**
content decisions. They must be answered **separately** — answering one
establishes nothing about the other, and neither may be treated as a default,
template, or precedent for the other.

---

## Boundary Confirmations

Recorded so the executing agent does not drift. Each is a citation, not a
decision.

```text
1. EFFECTDEFINITION / ELEMENT — CLOSED, NOT REOPENED.
   TASK-170 classified GAP-B as `E. Not actually a gap after full
   authoritative-document review`, and recorded that the decision is already
   made by TASK-166 (Product Owner), TASK-167 (the §"Storage Decision"), and
   TASK-168 (the provisioning boundary). The existing decision stands:

       EffectDefinition
       ├── effectType
       ├── valueType
       ├── value
       ├── duration
       └── scope

   `Element` is NOT a member of that contract, and no `Element`,
   `DamageElement`, or `BurnElement` member may be introduced
   (DATABASE.md §1 note item 1's closed set; TASK-168's Completion Evidence).
   ELEMENT_RULES.md §1.1's listing of "Skill" and "Effect" as Element-carrying
   entities is a GAME-DESIGN ownership statement about entities, not a
   storage-schema mandate (TASK-170 GAP-B Findings 1 and 4).

   This task therefore does NOT: add an Element field; modify EffectDefinition
   storage; modify CARD_RULES.md; modify ELEMENT_RULES.md; modify
   CardCastExecutor; modify BattleStateService. It records a Boss's Element
   choice as Boss CONTENT only.

2. THE THREE EXISTING BOSSES ARE PRESERVED.
   BOSS_RULES.md §6.1–§6.4's three rows, the Domain `BossDefinitions` content,
   and the three provisioned `BossDefinition` rows are untouched. This task
   records decisions for Boss #4 and Boss #5 only.

3. NO NEW VOCABULARY IS AUTHORIZED BY THIS TASK.
   The trigger categories (BOSS_RULES.md §3 item 2, PASSIVE_RULES.md §3), the
   Elements (ELEMENT_RULES.md §1), the storage member sets (DATABASE.md §1
   items 3–4), the Status Effects (COMBAT_RULES.md §5.1), and the events
   (BOSS_RULES.md §7) are closed sets. A proposed answer requiring anything
   outside them is a STOP, not a field to fill — see §"Stop Conditions".
```

---

## Decision Options

> **PRODUCT OWNER INPUT.** The two items below are gameplay content decisions.
> Only the Product Owner may supply them. An agent executing this task must not
> fill in any `Decision:` field.
>
> Answer each item **independently**. Do not treat either item's answer as a
> default for the other.
>
> **No answer is pre-judged anywhere in this task.** The options below are
> candidate shapes drawn from the documents' own structure — not a ranking, and
> not a recommendation. The Product Owner may select one, combine them, define
> another, or supply free-form content, provided every coverage item is
> answered.
>
> **Every option requires the Product Owner to supply the content itself.** This
> task deliberately proposes **no** Boss name, Element, stat, Passive, trigger,
> Skill, magnitude, or identity for either Boss — proposing those would be
> exactly the prohibited act. The option lists exist only to show which
> *existing* rule structures a Boss can be expressed in, so that a supplied
> answer can be recorded in a form the downstream documentation task can apply.
>
> **Do not design the stored row here.** Per this task's boundary, no
> `PassiveDefinition` JSON, no `SkillDefinition` JSON, no `BossDefinitionId`
> spelling, and no database row is authored in this file. The downstream
> documentation-application task derives those from the recorded answer.

### Coverage checklist (applies to BOTH B-1 and B-2)

The supplied answer for **each** Boss must explicitly determine the items below.
Items marked **conditional** are required only if the Boss actually uses that
property — a Boss whose Passive is not match-charged does not need a
`PassiveThreshold`, and a Boss whose Skill deals no damage does not need a Base
Damage. Do not force a mechanic to exist in order to fill a field, and do not
leave a field of a mechanic that IS used implicit.

```text
  1. Display name
     The Boss's human-readable content name, as it will appear to the player.
     (Each of the three authored Bosses has one: Hỏa Long, Thủy Ma, Mộc Yêu.)
     BOSS_RULES.md §6.4: presentation only, never a technical identifier.

  2. BossId
     The canonical technical Identity, following §6.4's convention
     `boss-<ascii-kebab-case-name>` — ASCII only, lowercase, kebab-case, stable,
     no Vietnamese diacritics, no spaces, no runtime slugification. §6.4 states
     these are fixed there "so no task invents its own"; this is the slot where
     the Product Owner fixes the two new ones.

  3. Element
     Exactly one of the Five Elements (ELEMENT_RULES.md §1: Mộc, Hỏa, Thổ, Kim,
     Thủy). §1.2 forbids dual/multi-element Bosses, and ELEMENT_RULES.md §7
     defers dual-element entities. BOSS_RULES.md §6's closing note item 1 states
     a distinct pairing with the other Bosses "is encouraged but not mandated —
     multiple Bosses may share an Element". State whether sharing an existing
     Boss's Element is intended.

  4. Base HP / MaxHP
     The §6.1 `HP / MaxHP` value. BOSS_RULES.md §6.1 states these are
     project-owner-approved configuration defaults, "not universal balance
     invariants" and "not … formulas or scaling rules", so no value is derivable.

  5. Base ATK
     The §6.1 `ATK` value. BOSS_RULES.md §6.3.1 item 1 notes a Skill's Burn
     magnitude "does not scale with Boss ATK", and §6.2.1 states `BossState.ATK`
     is the immutable/base value — so state the value directly.

  6. Base DEF
     The §6.1 `DEF` value.

  7. EnrageThreshold
     The §6.1 `EnrageThreshold` (a per-Boss value). BOSS_RULES.md §5 item 4 owns
     the Enrage semantics: a permanent transition triggered when
     `BossHP < EnrageThreshold`, no timer, evaluated after Player→Boss damage and
     before the terminal check. If the Boss defines any Enrage-specific behavior
     change (e.g. a Skill damage increase), §5 item 4 places it in §6 — state it
     or state that there is none.

  8. Initial State
     The §6.1 `Initial State` (e.g. `Idle`). BOSS_RULES.md §5 items 3–5: MVP
     Bosses use State minimally; a full multi-phase State machine is FUTURE
     (GDD §20); and §5 item 5 states "For MVP, no content-defined Boss applies
     Stun".

  9. Gameplay role / core concept                              [required]
     The Boss's strategic identity in one or two sentences — the mechanic the
     player must play around. Required because BOSS_RULES.md §2 item 1 requires
     difficulty to come primarily from mechanics, and §2 item 2 requires the
     Passive and Skill to be mechanically distinct from each other and from
     other Bosses'.

 10. Passive identity / PassiveId
     The Passive's canonical identity (BOSS_RULES.md §6.4: "Values follow the
     kebab-case pattern of Pet PassiveIds"), used in
     `PassiveCharged`/`PassiveTriggered` payloads with `source = "boss"`.

 11. Passive trigger category
     Exactly one from BOSS_RULES.md §3 item 2's closed list — `Turn`,
     `Match Count`, `Combo`, `Active Pet Power`, `Active Pet HP`, `Boss HP`,
     `Status` — as §6's closing note item 2 requires ("an explicit trigger
     category from §3.2"). If the intended concept needs a form outside that
     list, see §"Stop Conditions" — it is a gameplay-contract decision, not a
     field to fill.

 12. Passive trigger threshold / condition
     The exact condition that fires the Passive. BOSS_RULES.md §3.3 item 1: the
     Passive "fires once per player action, after all player damage is
     resolved", and it "sees the post-damage battle state". A threshold-based
     condition must state which value it reads and against which threshold.

 13. PassiveThreshold (match-charged Passives only)           [CONDITIONAL]
     If the trigger is match-charged, the Threshold (BOSS_RULES.md §6.2's
     `PassiveThreshold` rule; PASSIVE_RULES.md §2 — progress increments per
     Player Match, evaluated once per Cascade batch). If the trigger is NOT
     match-charged, say so explicitly: DATABASE.md §1 note item 3 requires
     `threshold = null` for such a Passive, and states null is "NOT a statement
     that the Passive is always-active".

 14. Passive effect
     What the Passive does — which existing mechanic it applies, to which target
     (the Boss itself or the active Pet), and of what kind. BOSS_RULES.md §3
     item 3: it resolves automatically with no player and no Boss casting input.
     A Passive applying a type of effect no document defines is a STOP.

 15. Passive magnitude / value
     The numeric magnitude, and its representation (flat, percentage, or a
     percentage-point rate) as the existing §6.2 rows express. No magnitude is
     derivable from the three existing Bosses.

 16. Passive duration / timing                                [CONDITIONAL]
     If the Passive applies something non-instant: its duration in the
     authoritative Turn unit, and which of the two documented Status Effect
     duration models applies (COMBAT_RULES.md §5.2/§5.3), and at which phase it
     is applied. §6.2.2/§6.2.3 are the worked precedents — one Passive creates a
     Turn-based instance and one is a direct `BossState.HP` update with no
     instance. If the effect is instantaneous, state that explicitly.

 17. Passive representation                                     [CONDITIONAL]
     If the Passive applies a lasting effect: whether it is a
     `BossState.StatusEffects[]` instance, a `PetState.StatusEffects[]` instance,
     or a direct state update — the three shapes the existing content uses
     (BOSS_RULES.md §6.2.1, §6.2.2, §6.2.3; GAME_STATE.md §2.3.1/§2.4.1). This
     determines the storage/state contract the downstream task must record. Do
     not introduce a third representation.

 18. Passive charge / reset behavior                          [CONDITIONAL]
     If the Passive is charged: its Reset Behavior — Default, Partial Reset, or
     No Reset / persistent (PASSIVE_RULES.md §4). DATABASE.md §1 note item 3
     fixes the storage tokens as exactly `Default` | `Partial` | `Persistent`,
     and states the documented default when no override is stated is `Default`.
     A non-default choice must be explicitly declared (PASSIVE_RULES.md §4
     item 3), and "No reset / persistent" "must be explicitly justified".

 19. Passive reapplication behavior                           [CONDITIONAL]
     If the Passive can apply while its own effect is already active: whether a
     re-trigger refreshes the existing instance or stacks. BOSS_RULES.md
     §6.2.1/§6.2.2 state the refresh-not-stack default and cite
     COMBAT_RULES.md §5.2 item 2. State the intended behavior; do not leave it
     implicit.

 20. Skill identity / SkillId
     The Skill's canonical identity (BOSS_RULES.md §6.4), used in
     `BossSkillCast.skillId`.

 21. Skill definition — what the Skill does
     The Skill's effect, and the target of each effect (the active Pet for
     damage and debuffs; BOSS_RULES.md §4 item 2: it is "a distinct, usually
     stronger action the Boss performs"). §2 item 2 requires it to be
     mechanically distinct from the Boss's own Passive.

 22. Skill timing rule (when it fires)
     BOSS_RULES.md §4 item 2: the timing rule "is itself part of the Boss's
     design and must be explicit, not implicit". §6.3 fixes the two parameters —
     Charge Requirement and Cooldown — and §6.3's post-fire reset
     (`SkillCharge` → 0, `SkillCooldown` → the Boss's cooldown value). §3.3
     item 2 fixes the ordering: the Skill is evaluated after the Passive
     resolves, so it sees the Passive's updated state.

 23. Skill Charge Requirement                                 [CONDITIONAL]
     The number of player matches required before the Skill is eligible
     (BOSS_RULES.md §6.3; GAME_STATE.md §2.4.3). Required if the answer uses the
     documented charge mechanism — which §6.3 fixes as the MVP mechanism, and
     which §6's closing note item 3 requires to be explicit.

 24. Skill Cooldown (CD)                                      [CONDITIONAL]
     Turns remaining after each Skill use (BOSS_RULES.md §6.3; GAME_STATE.md
     §2.4.3). Required if the answer uses the documented cooldown mechanism.
     Do not introduce a separate cooldown concept — §6.3's CD is the only one
     defined for a Boss Skill.

 25. Skill Base Damage                                        [CONDITIONAL]
     The Skill's Base Damage (BOSS_RULES.md §6.3; DATABASE.md §1 note item 4's
     `baseDamage`). Required if the Skill deals damage. §6.3.1 item 1 is
     explicit that a Skill's authored Base Damage is the value that does NOT
     itself receive the Rage-style ATK percentage (§6.2.1) — so state the
     authored number directly. Skill damage traverses the full Damage Pipeline
     including the Element Modifier, using the Boss's Element (§4 item 3,
     COMBAT_RULES.md §3, ELEMENT_RULES.md §5).

 26. Skill secondary effects                                  [CONDITIONAL]
     Any non-damage effect the Skill applies (BOSS_RULES.md §4 item 4 — debuffs,
     resource drain, etc.), each with its target. §6.3.1's three items are the
     worked precedents. Every secondary effect must be expressible in an
     existing documented mechanic; a new status or stat is a STOP.

 27. Skill effect magnitudes and durations
     Every magnitude and, for each non-instant effect, its duration in the
     authoritative Turn unit — following BOSS_RULES.md §6.3.1's declaration
     pattern per effect. §6.3.1 item 2 is the precedent for an instant effect
     that states "Duration: None (instant stat reduction, not a persistent
     status effect)"; leave no magnitude or duration implicit.

 28. Skill secondary-effect representation                     [CONDITIONAL]
     If a secondary effect persists: whether it is a `PetState.StatusEffects[]`
     instance (a debuff applied to the active Pet) or a direct stat reduction,
     and the `TargetStat` if a stat-modifying Buff/Debuff — the shape §6.3.1
     item 3 uses for the Pet ATK debuff. COMBAT_RULES.md §5.4.5/§5.5.3 close the
     non-`"ATK"` `TargetStat` case with "would require its own recorded
     decision": a status or `TargetStat` outside the documented set is a STOP.

 29. Distinctness confirmation                                [required]
     An explicit statement that this Boss's Element pairing, Passive trigger
     category, Skill identity, and core gameplay role do not unintentionally
     duplicate an existing Boss, satisfying BOSS_RULES.md §6's closing note
     item 4, §2 items 2–3, and §3.2's trigger-pattern diversity requirement.
     The requirement is §3.2's, not a rule invented here: §3.2 requires each
     MVP Boss's Passive to use a different primary trigger category from the
     other MVP Bosses' "where possible", and §6's note item 4 asks to avoid
     duplicating an existing category "where reasonably possible". Note that
     BOSS_RULES.md §6's closing note item 1 explicitly ALLOWS two Bosses to
     share an Element — only trigger-pattern diversity is required.

 30. PassiveId / SkillId spelling and BossDefinitionId
     The §6.4 identity values above, plus the persistence key of the
     `BossDefinition` row. DATABASE.md §1 note item 2 owns `BossDefinitionId`:
     value form `boss-def-<ascii-kebab-case-name>`, "independent: distinct from
     `Identity` and from the display name, and never derived from either at
     runtime or at content-authoring time", not database-generated. State the
     value, or explicitly defer the spelling to the downstream task — but do
     not leave the requirement unnoticed.

 31. Cross-document consequences
     Which authoritative sections the decision requires the downstream task to
     change — at minimum BOSS_RULES.md §6 (which carries §6.1's base-stat row,
     §6.2's Passive row, §6.3's Skill row, and §6.4's identity row — all four
     gain a new Boss). State whether any existing contract ceases to hold, and
     whether the decision requires anything beyond BOSS_RULES.md §6.
```

### B-1 — MVP Boss #4

**Question.** What is MVP Boss #4, on every coverage item above?

**Current evidence.**

- `BOSS_RULES.md` §6 defines only Hỏa Long, Thủy Ma, and Mộc Yêu, and its
  closing note states a fourth and fifth are not yet content-defined.
- `ROADMAP.md` §1 Phase 2 requires "All 5 Bosses (2 not yet content-defined …
  each requires Element, Passive, Skill, stats)".
- `DATABASE.md` §1/§3 item 5 permits provisioning only content-defined Bosses.
- `MVP_SCOPE.md` §1 lists "5 Bosses" as MVP IN.
- No `BossDefinition` row, no Domain `BossDefinitions` entry, and no content for
  Boss #4 exist anywhere.
- The only Element assignment table, `ELEMENT_RULES.md` §6, covers Pets and
  defers Bosses to `BOSS_RULES.md`.

**Options.** *(Shapes only. Every one requires the Product Owner to supply the
actual content — name, Element, stats, Passive, trigger, Skill, timing,
magnitudes, identities. None is ranked.)*

```text
B-1A  A Boss whose Passive uses a trigger category NOT yet used by the existing
      three, drawn from BOSS_RULES.md §3 item 2's list — e.g. Turn, Combo,
      Active Pet Power, Active Pet HP, Boss HP, or Status (the existing three
      use Match Count twice and Battle Start once). The Product Owner supplies
      the category, condition, effect, magnitude, and every Skill field.

B-1B  A Boss that reuses a trigger category the existing Bosses already use.
      BOSS_RULES.md §3.2 states the different-category requirement as "where
      possible", and §6's note item 4 as "where reasonably possible" — so this
      is permitted, but the distinctness confirmation (coverage item 29) must
      justify it explicitly.

B-1C  A Boss whose Passive is a direct state update (the Mộc Yêu §6.2.3 shape)
      rather than a Status Effect instance.

B-1D  A Boss whose Passive/Skill requires a mechanic no authoritative document
      defines — a new trigger category, a new status effect, a new damage type,
      a new stat, a new representation, or a new event. See the Stop Conditions:
      this is a gameplay-CONTRACT decision, not a Boss content answer, and it
      must be flagged rather than recorded as settled.

B-1E  Another explicitly stated Boss this list does not anticipate. Fully
      acceptable: supply the coverage checklist items directly.
```

**Coverage the answer must provide.** All applicable items 1–31 of the coverage
checklist above, for Boss #4 specifically. Items 13, 16, 17, 18, 19, 23, 24, 25,
26, and 28 are conditional on the Boss actually using that mechanic.

**PRODUCT OWNER INPUT RECEIVED — full specification (supersedes the earlier
concept direction).**

The Product Owner supplied a **concrete specification** for this Boss. It is
recorded verbatim in substance below. Nothing is inferred; every field the
specification does not state is marked `PENDING PRODUCT OWNER DECISION`.

```text
Decision:
DECIDED — PRODUCT OWNER SPECIFICATION (verbatim in substance):

  Boss Name:        Sơn Thạch Vệ
  Element:          Thổ
  Role:             Defensive / Enrage
  HP:               3000
  MaxHP:            3000
  ATK:              120
  DEF:              0

  Passive Role:     Defensive / Enrage
  Trigger:          Boss HP ≤ 50%
  Effect:           +20% ATK
  Duration:         3 Turns
  Reapplication:    "Do not repeatedly trigger the same passive after it
                     has activated."
  Reset:            "No re-trigger for the same battle after activation."

  Skill:            Earthquake
  Role:             Burst Boss attack
  Base Damage:      150
  Timing:           "Use the existing Boss Skill charge/timing contract."
  Secondary Effects: None
  Board Effect:     None

  Product Owner constraint on the Passive's implementation:
  "The intended implementation must use an existing authoritative Boss Passive
   / BuffDebuff mechanic. Do not create a new status type. Do not introduce a
   new TargetStat without an existing contract. If the +20% ATK effect is not
   expressible using the current authoritative Boss Passive/BuffDebuff
   contract: STOP. Do not invent a new representation."

  Product Owner constraint on the Skill's implementation:
  "Do not implement board transformation, gem destruction, gem conversion,
   freeze, lock, or any other new board mechanic."
```

**PRODUCT OWNER INPUT RECEIVED — remaining decision slots resolved.**

The Product Owner resolved the previously pending slots. Recorded verbatim
below; superseded values are preserved as historical evidence per this task's
instruction not to silently rewrite earlier decisions.

```text
RESOLVED SLOTS — PRODUCT OWNER DECISION (verbatim in substance):

  BossId:              boss-son-thach-ve
  BossDefinitionId:    boss-def-son-thach-ve
  Initial State:       Idle
  EnrageThreshold:     1500
  PassiveId:           son-thach-ve-enrage
  SkillId:             earthquake
  Charge Requirement:  5
  Cooldown:            0
```

**Enrage / Passive separation — resolved (SUPERSEDES the earlier ambiguity).**

```text
HISTORICAL — what the earlier stage recorded and returned for confirmation:
  The `Boss HP ≤ 50%` figure was placed by the Product Owner under the
  "### Passive" heading, and the earlier stage recorded it as the PASSIVE
  trigger exactly as placed while REPORTING that whether it also served as the
  Boss's `EnrageThreshold` was ambiguous ("B-1 open item" below preserves that
  report as historical evidence).

NOW RESOLVED — the Product Owner states:
  "Preserve the already resolved decision: Sơn Thạch Vệ EnrageThreshold = 1500.
   The previously recorded Boss HP ≤ 50% is the Passive trigger. Do NOT
   reinterpret it as the authoritative Enrage transition threshold."

  "MaxHP = 3000 / EnrageThreshold = 1500 / Passive trigger = BossHP ≤ 50%"

  "The Passive and Enrage happen at the same HP boundary numerically, but they
   remain separate contract concepts: Enrage transition + Passive trigger.
   Do not collapse their identities."

RECORDED DISPOSITION — two distinct fields, both DECIDED:

  EnrageThreshold = 1500
    → the BOSS_RULES.md §6.1 base-stat field (an absolute HP value).
    → drives the §5 item 4 permanent BossState transition Idle → Enraged
      when `BossHP < EnrageThreshold`, i.e. `BossHP < 1500`.
    → the Product Owner supplied the ABSOLUTE value (1500), not the percentage.
      The percentage form "50%" is numerically equivalent at MaxHP 3000
      (1500 / 3000 = 50%), which is why the two coincide at this Boss — but
      the stored §6.1 field is the absolute value, matching the existing
      Bosses' recorded form ("1500 (30%)" for MaxHP 5000). See "B-1 Enrage
      form note".

  Passive trigger = BossHP ≤ 50%
    → the BOSS_RULES.md §6.2 Passive row's Trigger column.
    → drives the Passive effect (+20% ATK for 3 Turns), which is the Boss's
      §6.1 "Initial State" neighbour concept, NOT the Enrage transition.
    → NOTE the boundary operator differs by design: §5 item 4's Enrage uses
      strict `<` while the Passive trigger uses `≤`. Both are recorded exactly
      as supplied and are NOT unified.

  The two are NOT collapsed: no second Enrage mechanism is created, the
  Passive is not re-expressed as the Enrage transition, and §5 item 4's
  Idle → Enraged transition is not merged with the Passive effect.
```

**B-1 Enrage form note — reporting only, no STOP.** The earlier stage's
ambiguity is now resolved by an explicit absolute value, so no STOP fires. One
detail is reported for the downstream documentation task:

```text
BOSS_RULES.md §6.1's EnrageThreshold column records the three existing Bosses
as "1500 (30%)" — an absolute value with its percentage parenthetical — at
MaxHP 5000. The Product Owner supplied Sơn Thạch Vệ's as "1500" (absolute) at
MaxHP 3000, where 1500 = 50%.

Nothing conflicts: §6.1's column is an absolute per-Boss value, §5 item 4
defines the transition against that absolute value, and the existing rows'
"(30%)" is a derived parenthetical. DATABASE.md §1 note item 1 confirms
EnrageThreshold is "the combat-definition values ... (§6.1)" carried by the
Domain BossDefinition content.

REPORTED, NOT DECIDED HERE: whether the downstream §6.1 row should render the
percentage parenthetical as "(50%)" for consistency with the existing rows.
That is a presentation/transcription detail for the downstream documentation
task, not a gameplay decision, and it changes no value. The stored value is
1500.
```

**B-1 coverage resolution** — per TASK-171 §"Decision Coverage" `D-1`…`D-16`.
`DECIDED` means explicitly supplied by the Product Owner above. Nothing below
was inferred.

```text
Coverage item 1  Display name            DECIDED — "Sơn Thạch Vệ"
Coverage item 2  BossId                  DECIDED — `boss-son-thach-ve`
                 (supplied exactly; follows BOSS_RULES.md §6.4's
                  `boss-<ascii-kebab-case-name>` convention — ASCII, lowercase,
                  kebab-case, no diacritics. Not derived by this task.)
Coverage item 3  Element                 DECIDED — Thổ
                 (ELEMENT_RULES.md §1 — one of the Five; §1.2 one per Boss.
                  Unused by Hỏa Long (Hỏa), Thủy Ma (Thủy), Mộc Yêu (Mộc).)
Coverage item 4  Base HP / MaxHP          DECIDED — HP 3000 / MaxHP 3000
Coverage item 5  Base ATK                 DECIDED — 120
Coverage item 6  Base DEF                 DECIDED — 0
Coverage item 7  EnrageThreshold          DECIDED — 1500
                 (SUPERSEDES the earlier PENDING entry. The Product Owner
                  supplied the absolute value and explicitly separated it from
                  the Passive trigger: "Do NOT reinterpret it as the
                  authoritative Enrage transition threshold." Expresses §5
                  item 4's permanent Idle → Enraged transition at
                  `BossHP < 1500`. See "Enrage / Passive separation".
                  The earlier "B-1 open item" ambiguity is now resolved and is
                  preserved below as historical evidence.)
Coverage item 8  Initial State            DECIDED — `Idle`
                 (SUPERSEDES the earlier "ACTIVE" value, which was reported as
                  CONFLICT 2: `ACTIVE` is not a member of BOSS_RULES.md §1's
                  closed four-member State enum (Idle / Charging / Enraged /
                  Stunned), repeated at GAME_STATE.md §2.4 and COMBAT_RULES.md
                  §1.2. The Product Owner resolved it to `Idle`, the documented
                  initial State every MVP Boss begins in — the same value
                  BOSS_RULES.md §6.1 gives the three existing Bosses.)
Coverage item 9  Gameplay role            DECIDED — "Defensive / Enrage"
Coverage item 10 PassiveId                DECIDED — `son-thach-ve-enrage`
                 (supplied exactly. See "B-1/B-2 identity convention finding"
                  — §6.4 does NOT fix an exact Boss PassiveId value form, so
                  the supplied form is recordable.)
Coverage item 11 Passive trigger category DECIDED — `Boss HP`
                 (BOSS_RULES.md §3 item 2 lists `Boss HP` in its closed set.
                  PASSIVE_RULES.md §3's alternate form is `HP Threshold
                  (e.g. "when HP < 30%")`. Unused by the existing three.)
Coverage item 12 Passive trigger condition DECIDED — Boss HP ≤ 50%
                 (recorded exactly as supplied, including the `≤` operator,
                  and NOT unified with §5 item 4's strict `<` Enrage operator.)
Coverage item 13 PassiveThreshold         NOT APPLICABLE — must remain `null`
                 (the Product Owner's §5 states this explicitly: "Do NOT
                  populate PassiveThreshold for either Boss as a match-count
                  threshold … The existing contract scopes PassiveThreshold
                  away from non-match-charged Boss Passives. Preserve:
                  PassiveThreshold = null". BOSS_RULES.md §6.2 scopes
                  PassiveThreshold to "match-charged passives only";
                  DATABASE.md §1 note item 3 fixes `threshold = null` for a
                  Passive with no match-charging threshold.)
Coverage item 14 Passive effect           DECIDED — +20% ATK
                 (expressible as the existing Boss-side ATK modifier.)
Coverage item 15 Passive magnitude        DECIDED — +20% (Magnitude = +20)
                 (COMBAT_RULES.md §5.5.1 item 4: the sign of `Magnitude` is the
                  direction signal; +20 gives an increase.)
Coverage item 16 Passive duration         DECIDED — 3 Turns
                 (Turn-based; lifecycle owned by COMBAT_RULES.md §5.3.)
Coverage item 17 Passive representation   DECIDED by contract — a Turn-based
                 `BuffDebuff` Status Effect instance in
                 `BossState.StatusEffects[]` with `TargetStat = "ATK"`
                 (COMBAT_RULES.md §5.5.1; GAME_STATE.md §2.4.1. This is the
                  existing authoritative representation the Product Owner's
                  constraint requires; it is the §6.2.1 Hỏa Long Rage shape.
                  No new status type and no new TargetStat is introduced.)
Coverage item 18 Passive charge/reset     DECIDED — no re-trigger after
                 activation (one-time, persistent)
                 See "B-1 reset finding" — the supplied behavior is
                 expressible, and the storage token is reported.
Coverage item 19 Passive reapplication    DECIDED — "Do not repeatedly trigger
                 the same passive after it has activated."
Coverage item 20 SkillId                  DECIDED — `earthquake`
                 (supplied exactly. See "B-1/B-2 identity convention finding".)
Coverage item 21 Skill definition         DECIDED — "Earthquake", a
                 "Burst Boss attack", with Secondary Effects: None and
                 Board Effect: None.
Coverage item 22 Skill timing rule        DECIDED — "Use the existing Boss
                 Skill charge/timing contract."
                 (BOSS_RULES.md §6.3 — Charge Requirement + Cooldown — is that
                  contract; the values are items 23/24.)
Coverage item 23 Charge Requirement       DECIDED — 5
                 (SUPERSEDES the earlier PENDING entry. Recorded as the
                  Product Owner's explicit MVP balance decision after this task
                  verified that §6.3 defines the MECHANISM but does not
                  mechanically fix per-Boss values — §6.3's own table is
                  per-Boss configuration whose values "the project owner
                  approved". See "B-1/B-2 charge and cooldown finding".)
Coverage item 24 Cooldown                 DECIDED — 0
                 (SUPERSEDES the earlier PENDING entry. Consistent with
                  §6.3's model: the Skill fires whenever
                  `SkillCharge ≥ Charge Requirement` AND `SkillCooldown = 0`;
                  a 0 cooldown means the post-fire reset leaves it immediately
                  re-eligible once recharged. No new timing mechanic.)
Coverage item 25 Skill Base Damage        DECIDED — 150
Coverage item 26 Secondary effects        DECIDED — None
Coverage item 27 Secondary-effect         NOT APPLICABLE — no secondary effect
                 magnitudes/durations      is supplied, so none exists.
Coverage item 28 Secondary-effect         NOT APPLICABLE — same as item 27.
                 representation
Coverage item 29 Distinctness             DECIDED — see "B-1/B-2 distinctness
                 validation". Element (Thổ) and trigger category (`Boss HP`)
                 are both unused by the existing three and differ from Boss
                 #5's (`Combo`), satisfying BOSS_RULES.md §3.2 for the
                 five-Boss set.
Coverage item 30 PassiveId / SkillId /    DECIDED — `son-thach-ve-enrage` /
                 BossDefinitionId          `earthquake` / `boss-def-son-thach-ve`
                 (BossDefinitionId SUPERSEDES the earlier PENDING entry. The
                  Product Owner resolved CONFLICT 3 by supplying the
                  DATABASE.md §1 note item 2 form `boss-def-<ascii-kebab-case-
                  name>`, keeping it distinct from BossId — "These MUST remain
                  distinct. Do NOT use BossDefinitionId = BossId." The earlier
                  supplied value equal to the BossId was reported as CONFLICT 3
                  and is preserved below as historical evidence.)
Coverage item 31 Cross-document           DECIDED — see "B-1/B-2 cross-document
                 consequences              consequences" for the classification.
```

**B-1 identity note — HISTORICAL (now resolved).** The earlier stage recorded
`BossId` as PENDING, reasoning that `BOSS_RULES.md` §6.4's convention is
mechanical but §6.4's own governing sentence is "They are fixed here so no task
invents its own", so deriving the spelling would produce a value the identity
contract had not fixed. The Product Owner has now supplied the values directly:
`boss-son-thach-ve`, `son-thach-ve-enrage`, `earthquake`, and
`boss-def-son-thach-ve`. The earlier PENDING disposition is preserved as
historical evidence; it is superseded, not rewritten.

**B-1/B-2 identity convention finding — the supplied forms are RECORDABLE.**

Per the Product Owner's §5 critical rule, this task inspected
`BOSS_RULES.md` §6.4, `PET_RULES.md`, and `DATABASE.md` before recording the
`PassiveId`/`SkillId` forms. **§6.4 does NOT fix an exact Boss PassiveId or
SkillId value form**, so no STOP fires and the supplied values are recorded
exactly as given.

```text
WHAT §6.4 ACTUALLY SAYS (BOSS_RULES.md §6.4)
  - BossId: an explicit CONVENTION is stated —
    `boss-<ascii-kebab-case-name>`, "ASCII only, lowercase, kebab-case, stable,
    no Vietnamese diacritics, no spaces, no runtime-generated or
    runtime-slugified identifiers." §6.4 also records the three existing
    values in a table.
  - PassiveId: "Values follow the kebab-case pattern of Pet PassiveIds
    (e.g. PassiveId("xich-lang"))." — a kebab-case PATTERN reference, with the
    `PassiveId("xich-lang")` example itself UNPREFIXED.
  - SkillId: no value form is stated at all. §6.4 only states its ROLE —
    "identifies the Boss Skill in `BossSkillCast.skillId`".

WHAT §6.4 DOES NOT DO
  §6.4 fixes the three existing Bosses' concrete values in its table, but it
  states no generative rule that would make the existing entries' spellings
  binding on NEW content. The existing Boss PassiveIds happen to be
  `<bossId>-<effect>` (`boss-hoa-long-rage`, `boss-thuy-ma-heal`,
  `boss-moc-yeu-regen`) and the existing SkillIds happen to be bare
  (`flame-burst`, `drain-power`, `root`) — but §6.4 does not state either as a
  convention, and the Pet `passive-<name>` prefix is scoped to PETS ONLY:

    PASSIVE_RULES.md §8, "Pet `PassiveId` values":
      "A Pet passive's `PassiveId` is `passive-<ascii-kebab-case-name>` of the
       owning Pet's documented name — Pets are the only named anchor ..."
      "This format and the values above are scoped to **Pet passives only**
       (R1-3): Boss PassiveIds are unchanged and remain the values fixed by
       `BOSS_RULES.md` §6.4."

    → The `passive-` prefix is expressly NOT a Boss convention. §6.4's Boss
      rule is therefore "kebab-case pattern", which the supplied
      `son-thach-ve-enrage` / `kim-loi-vuong-combo` satisfy.

NO ID-FORMAT CONSTRAINT EXISTS IN THE TECHNICAL CONTRACT EITHER
  PassiveId is carried as an opaque identity, not a formatted key.
  Domain `PassiveId` is a `readonly record struct PassiveId(string Value)` and
  its own contract states: "There is no id format, scheme, or validation rule
  in any document — `GAME_STATE.md` §2.3 defines the field's meaning, not its
  spelling — so this type deliberately imposes none and holds the identifier
  verbatim."
  DATABASE.md §1 note item 3 fixes the `PassiveDefinition.passiveId` member as
  "(string) — the Boss's canonical PassiveId (`BOSS_RULES.md` §6.4)" — a
  reference to §6.4, imposing no additional form.
  SIGNALR_PROTOCOL.md §3.2.16–§3.2.18 carry `passiveId`/`skillId` as opaque
  strings whose values are the state-held identities.

RESULT — RECORD, DO NOT STOP
  §6.4 states no exact Boss PassiveId/SkillId value form, and no other
  authoritative document imposes one. The supplied values are well-formed
  kebab-case identity strings, unambiguous, and consistent with the "no format
  rule" position the technical contract records. They are recorded exactly as
  supplied:
      son-thach-ve-enrage      earthquake
      kim-loi-vuong-combo      thunder-strike
  They are NOT normalized by this task, and §6.4's BossId convention is NOT
  applied to them.

REPORTED FOR THE DOWNSTREAM DOCUMENTATION TASK (not decided here)
  The existing three Bosses' recorded PassiveIds are `<bossId>-<effect>` and
  their SkillIds are bare. The supplied forms do not match either observed
  spelling. Because §6.4 states no convention, this is NOT a conflict and
  blocks nothing — but the downstream documentation task that writes these
  values into §6.4's table may wish to confirm the intended long-term Boss
  identity style, since the two new entries will be the first Boss rows whose
  spellings differ from the established three. Recorded as an observation only.
```

**B-1 open item — `EnrageThreshold` vs the Passive trigger. HISTORICAL
(RESOLVED).** The earlier stage reported this as a placement ambiguity and left
`EnrageThreshold` PENDING rather than assuming. The Product Owner has now
resolved it explicitly: `EnrageThreshold = 1500` as an independent field, with
`Boss HP ≤ 50%` remaining the Passive trigger, and an instruction not to
collapse the two identities. The report is preserved above as historical
evidence; see "Enrage / Passive separation" for the resolved disposition.

The earlier stage also recorded `Initial State` as PENDING; the value `ACTIVE`
supplied in the previous round was reported as CONFLICT 2 (not a member of
`BOSS_RULES.md` §1's closed State enum) and has now been resolved to `Idle`.

**B-1 reset finding — the supplied reset behavior is EXPRESSIBLE.** No STOP.

```text
Supplied:  Reset: "No re-trigger for the same battle after activation."
           Reapplication: "Do not repeatedly trigger the same passive after
           it has activated."

Contract:  PASSIVE_RULES.md §4 item 2 lists "No reset / persistent (rare, must
           be explicitly justified — e.g. a one-time Battle Start Passive)",
           and §4 item 3 requires any non-default reset behavior to be
           documented on the specific Boss's Passive definition, "not assumed".
           DATABASE.md §1 note item 3 fixes the storage tokens as exactly
           `Default` | `Partial` | `Persistent`, and item 5's Row-content rule
           derives `resetBehavior = Default` only "when a rule states no
           override — no Boss Passive documents one".

RESULT:    EXPRESSIBLE. The supplied behavior requires a NON-default reset
           behavior, and the storage token for it is `Persistent` (mapped
           internally to `NoReset`, per DATABASE.md §1 note item 3's stated
           enum mapping). Recording that token is a mechanical mapping of the
           Product Owner's stated reset behavior onto the contract's own closed
           storage set — not a new value and not a gameplay decision.

NOT AUTHORED HERE: the token is reported, not written into any document. The
           downstream documentation task must record the non-default reset
           behavior at BOSS_RULES.md §6.2 (PASSIVE_RULES.md §4 item 3's
           requirement) and the provisioning task must carry `Persistent`.
           No new field and no new token is created.

OPEN and REPORTED (not resolved): the trigger `Boss HP ≤ 50%` is an HP-threshold
           trigger, which is not a charge-tracking trigger. Whether a
           one-shot HP-threshold Passive is more precisely the `Persistent`
           token or a `null`-threshold + no-charge disposition is a
           documentation-application question for the downstream task; it is
           NOT decided here. What IS decided is the Product Owner's stated
           behavior: no repeated activation, no re-trigger in the same battle.
```

**B-1 open item — `EnrageThreshold` vs the Passive trigger.** Reported, not
resolved:

```text
The Role "Defensive / Enrage" and BOSS_RULES.md §5 item 4 both indicate an
Enrage dimension. §5 item 4 defines Enrage as a permanent transition at
`BossHP < EnrageThreshold`, with the threshold a per-Boss §6.1 value, and
states any Enrage-specific behavior change is defined per Boss in §6.

The supplied "Boss HP ≤ 50%" is placed by the Product Owner under
"### Passive" as the PASSIVE Trigger, and no separate EnrageThreshold value
was supplied. Two readings are possible and they are materially different:
  (a) the Passive trigger and the Enrage threshold are the same 50% event;
  (b) they are separate values, and EnrageThreshold is still unsupplied.

Choosing between them would reinterpret a supplied decision. Per this task's
Stop Conditions and the Product Owner's §4, `EnrageThreshold` is recorded
PENDING PRODUCT OWNER DECISION and the ambiguity is REPORTED for explicit
confirmation. This is not a contract conflict — §5 item 4 fully supports a
per-Boss Enrage threshold — it is a missing value plus a placement ambiguity.
[HISTORICAL — the Product Owner has since resolved this: `EnrageThreshold` =
1500 as an independent field, with `Boss HP ≤ 50%` remaining the Passive
trigger and the two identities explicitly not collapsed.]
```

```text
Decision:
FULLY RECORDED — all coverage items DECIDED. No field remains PENDING. The
three conflicts reported at earlier stages were each resolved by explicit
Product Owner decision; see "Enrage / Passive separation" and the per-item
entries above for the dispositions and the historical record.
```

### B-2 — MVP Boss #5

**Question.** What is MVP Boss #5, on every coverage item above?

**Current evidence.** Identical to B-1's, for the fifth Boss. The evidence
carries one additional constraint that matters here and not for B-1:

- `BOSS_RULES.md` §3.2's trigger-pattern diversity is a property of the **set**
  of five Bosses, and §6's closing note item 4 frames it as "avoid a third
  'every N Player Matches' Passive if the other four Bosses already cover
  Match-count, HP-based, and Turn-based patterns". By the time both new Bosses
  are answered, the two answers *together* must satisfy that requirement — so
  B-2's trigger category must be chosen with B-1's already fixed.

**Options.** *(Identical shape list to B-1; separate answer required. None is
ranked, and B-1's answer is not a default for this one.)*

```text
B-2A  A Boss whose Passive uses a trigger category not used by the other four.
      See B-1A's structure and §3.2's set-level requirement above.
B-2B  A Boss that reuses an existing trigger category — see B-1B's structure,
      including its explicit-justification requirement.
B-2C  A Boss whose Passive is a direct state update — see B-1C's structure.
B-2D  A Boss requiring a mechanic no document defines — see B-1D, including its
      stop condition.
B-2E  Another explicitly stated Boss this list does not anticipate.
```

**Coverage the answer must provide.** All applicable items 1–31 of the coverage
checklist above, for Boss #5 specifically, **plus** a statement that the pair of
answers together satisfies §3.2's set-level trigger-pattern diversity.

**PRODUCT OWNER INPUT RECEIVED — full specification (supersedes the earlier
concept direction).**

Recorded verbatim in substance. Nothing is inferred; every field the
specification does not state is `PENDING PRODUCT OWNER DECISION`.

```text
Decision:
DECIDED — PRODUCT OWNER SPECIFICATION (verbatim in substance):

  Boss Name:        Kim Lôi Vương
  Element:          Kim
  Role:             Aggressive / Combo-punishment
  HP:               2800
  MaxHP:            2800
  ATK:              140
  DEF:              0

  Passive Role:     Combo punishment
  Trigger:          Player Combo ≥ 4
  Effect:           +20% ATK
  Duration:         1 Turn
  Reapplication:    "Use the existing authoritative Boss Passive
                     trigger/reapplication semantics. Do not invent stacking
                     behavior."
  Reset:            "Use the existing authoritative Boss Passive reset
                     semantics."

  Stated intent chain:
  "Player reaches Combo ≥ 4
        ↓
   Boss receives +20% ATK
        ↓
   Boss's next relevant attack is stronger"

  Skill:            Thunder Strike
  Role:             Burst Boss attack
  Base Damage:      180
  Timing:           "Use the existing Boss Skill charge/timing contract."
  Secondary Effects: None

  Product Owner constraint on the Passive's implementation:
  "Use an existing authoritative ATK modifier/BuffDebuff mechanism. Do not
   create: BossNextAttackATKModifier, ComboPunishmentModifier, new StatusEffect
   type — unless such a representation already exists in the authoritative
   documentation. If the current contract cannot express this behavior: STOP."
```

**PRODUCT OWNER INPUT RECEIVED — remaining decision slots resolved.**

```text
RESOLVED SLOTS — PRODUCT OWNER DECISION (verbatim in substance):

  BossId:              boss-kim-loi-vuong
  BossDefinitionId:    boss-def-kim-loi-vuong
  Initial State:       Idle
  EnrageThreshold:     2100
  PassiveId:           kim-loi-vuong-combo
  SkillId:             thunder-strike
  Charge Requirement:  5
  Cooldown:            0
```

**Enrage / Passive separation — resolved (SUPERSEDES the earlier CONFLICT 1).**

```text
HISTORICAL — the earlier stage reported CONFLICT 1: the Product Owner had
supplied `EnrageThreshold: null` together with "Kim Lôi Vương does not have a
separate Enrage mechanic." That conflicted with BOSS_RULES.md §5 item 4 and
§6.1, which define EnrageThreshold as a mandatory per-Boss §6.1 value with no
null/absent convention (contrast DATABASE.md §1 note item 3, which defines
`null` for `PassiveDefinition.threshold` ONLY). The conflict is preserved above
as historical evidence.

NOW RESOLVED — the Product Owner states:
  "Kim Lôi Vương EnrageThreshold = 2100."
  "Given MaxHP = 2800, this represents the Boss entering Enraged state when
   BossHP < 2100 according to the existing authoritative Enrage rule."
  "This Enrage transition is separate from Player Combo ≥ 4. The Combo trigger
   remains the Passive trigger. Do NOT merge the two mechanics. Do NOT create a
   second Enrage mechanism."

RECORDED DISPOSITION — two distinct, independent fields, both DECIDED:

  EnrageThreshold = 2100
    → the BOSS_RULES.md §6.1 base-stat field (an absolute HP value).
    → drives the §5 item 4 permanent BossState transition Idle → Enraged when
      `BossHP < 2100`, evaluated after Player→Boss damage and before the
      terminal Boss HP check (GAME_STATE.md §2.4.4).
    → at MaxHP 2800 this is 2100 / 2800 = 75%, so this Boss enters Enrage
      EARLIER in proportional terms than the three existing Bosses (30%).
      That is the supplied value and is NOT questioned or rebalanced here.

  Passive trigger = Player Combo ≥ 4
    → the BOSS_RULES.md §6.2 Passive row's Trigger column; category `Combo`.
    → entirely independent of the Enrage transition — a different trigger
      category driving a different effect through a different mechanism.

  The two are NOT merged: the Combo trigger does not drive Enrage, the Enrage
  transition does not trigger the Passive, and no second Enrage mechanism is
  created.
```

**B-2 coverage resolution** — per TASK-171 §"Decision Coverage" `D-2` and
`D-1`…`D-16`. `DECIDED` means explicitly supplied above. Nothing was inferred.

```text
Coverage item 1  Display name            DECIDED — "Kim Lôi Vương"
Coverage item 2  BossId                  DECIDED — `boss-kim-loi-vuong`
                 (supplied exactly; follows BOSS_RULES.md §6.4's
                  `boss-<ascii-kebab-case-name>` convention. Not derived.)
Coverage item 3  Element                 DECIDED — Kim
                 (ELEMENT_RULES.md §1 — one of the Five; §1.2 one per Boss.
                  Unused by the other four Bosses.)
Coverage item 4  Base HP / MaxHP          DECIDED — HP 2800 / MaxHP 2800
Coverage item 5  Base ATK                 DECIDED — 140
Coverage item 6  Base DEF                 DECIDED — 0
Coverage item 7  EnrageThreshold          DECIDED — 2100
                 (SUPERSEDES the earlier PENDING entry AND resolves the
                  earlier CONFLICT 1's `null` supply. Expresses §5 item 4's
                  permanent Idle → Enraged transition at `BossHP < 2100`.
                  Explicitly separate from the Combo Passive trigger. See
                  "Enrage / Passive separation".)
Coverage item 8  Initial State            DECIDED — `Idle`
                 (SUPERSEDES the earlier "ACTIVE" value, reported as
                  CONFLICT 2 and resolved by the Product Owner to `Idle`, the
                  documented initial State — BOSS_RULES.md §1's closed enum,
                  §6.1's value for the three existing Bosses.)
Coverage item 9  Gameplay role            DECIDED — "Aggressive /
                 Combo-punishment"
Coverage item 10 PassiveId                DECIDED — `kim-loi-vuong-combo`
                 (supplied exactly. See "B-1/B-2 identity convention finding".)
Coverage item 11 Passive trigger category DECIDED — `Combo`
                 (BOSS_RULES.md §3 item 2 lists `Combo (player's)`;
                  PASSIVE_RULES.md §3's alternate form is `Combo (e.g.
                  "on Combo ≥ N")`. Unused by the existing three.)
Coverage item 12 Passive trigger condition DECIDED — Player Combo ≥ 4
Coverage item 13 PassiveThreshold         NOT APPLICABLE — must remain `null`
                 (Product Owner §5 states this explicitly for both Bosses;
                  BOSS_RULES.md §6.2 scopes PassiveThreshold to match-charged
                  Passives; DATABASE.md §1 note item 3 fixes `null`.)
Coverage item 14 Passive effect           DECIDED — +20% ATK
Coverage item 15 Passive magnitude        DECIDED — +20% (Magnitude = +20)
Coverage item 16 Passive duration         DECIDED — 1 Turn
Coverage item 17 Passive representation   DECIDED by contract — a Turn-based
                 `BuffDebuff` Status Effect instance in
                 `BossState.StatusEffects[]` with `TargetStat = "ATK"`
                 (COMBAT_RULES.md §5.5.1; GAME_STATE.md §2.4.1 — the existing
                  authoritative Boss ATK modifier. No
                  `BossNextAttackATKModifier`, no `ComboPunishmentModifier`,
                  and no new StatusEffect type is created.)
Coverage item 18 Passive charge/reset     DECIDED by direction — "Use the
                 existing authoritative Boss Passive reset semantics."
                 The existing semantics are PASSIVE_RULES.md §4. See
                 "B-2 reset finding".
Coverage item 19 Passive reapplication    DECIDED by direction — "Use the
                 existing authoritative Boss Passive trigger/reapplication
                 semantics. Do not invent stacking behavior."
                 The existing semantics are COMBAT_RULES.md §5.2 item 2's
                 refresh-not-stack default, cited by §5.5.5 and
                 BOSS_RULES.md §6.2.1. See "B-2 reset finding".
Coverage item 20 SkillId                  DECIDED — `thunder-strike`
                 (supplied exactly. See "B-1/B-2 identity convention finding".)
Coverage item 21 Skill definition         DECIDED — "Thunder Strike", a
                 "Burst Boss attack", Secondary Effects: None.
Coverage item 22 Skill timing rule        DECIDED — "Use the existing Boss
                 Skill charge/timing contract." (BOSS_RULES.md §6.3.)
Coverage item 23 Charge Requirement       DECIDED — 5
                 (SUPERSEDES the earlier PENDING entry. Recorded as the
                  Product Owner's explicit MVP balance decision after this task
                  verified §6.3 defines the mechanism but not per-Boss values.
                  See "B-1/B-2 charge and cooldown finding".)
Coverage item 24 Cooldown                 DECIDED — 0
                 (SUPERSEDES the earlier PENDING entry. Consistent with
                  §6.3's model; no new timing mechanic.)
Coverage item 25 Skill Base Damage        DECIDED — 180
Coverage item 26 Secondary effects        DECIDED — None
Coverage item 27 Secondary-effect         NOT APPLICABLE — no secondary effect.
                 magnitudes/durations
Coverage item 28 Secondary-effect         NOT APPLICABLE — same.
                 representation
Coverage item 29 Distinctness             DECIDED — see "B-1/B-2 distinctness
                 validation". Element (Kim) and trigger (`Combo`) are unused by
                 the existing three and differ from Boss #4's (`Boss HP`),
                 satisfying BOSS_RULES.md §3.2. The Enrage thresholds also
                 differ from each other by design.
Coverage item 30 PassiveId / SkillId /    DECIDED — `kim-loi-vuong-combo` /
                 BossDefinitionId          `thunder-strike` /
                                           `boss-def-kim-loi-vuong`
                 (BossDefinitionId SUPERSEDES the earlier PENDING entry and
                  resolves CONFLICT 3, using the DATABASE.md §1 note item 2
                  `boss-def-<ascii-kebab-case-name>` form, deliberately
                  distinct from BossId.)
Coverage item 31 Cross-document           DECIDED — see "B-1/B-2 cross-document
                 consequences              consequences" for the classification.
```

**B-2 reset finding — the supplied semantics are EXPRESSIBLE. No STOP.**

```text
Supplied:  Reapplication: "Use the existing authoritative Boss Passive
           trigger/reapplication semantics. Do not invent stacking behavior."
           Reset: "Use the existing authoritative Boss Passive reset
           semantics."

Contract:  Reapplication — COMBAT_RULES.md §5.5.5 defers duration and
           reapplication to §5.3 (Turn-based lifecycle) and §5.2 item 2's
           refresh-not-stack default, which BOSS_RULES.md §6.2.1 cites for
           Rage. A re-trigger while active refreshes the existing instance; it
           does not stack and does not create a second instance.
           Reset — PASSIVE_RULES.md §4's forms; DATABASE.md §1 note item 3's
           `Default` | `Partial` | `Persistent` tokens.

RESULT:    EXPRESSIBLE and, unlike B-1, it is the DEFAULT path. The Product
           Owner directed use of the existing semantics rather than supplying
           a non-default behavior, so PASSIVE_RULES.md §4 item 1's Default
           (progress resets after trigger) and the refresh-not-stack rule
           apply as-is. No new behavior, no new token, and no new field.

NOT AUTHORED HERE: the concrete dispositions above are the existing contract's;
           the downstream documentation task records them at BOSS_RULES.md
           §6.2 following the §6.2.1 pattern. Nothing is written now.
```

**B-2 open item — `EnrageThreshold`. HISTORICAL (RESOLVED).** The earlier stage
reported that no value had been supplied and left it PENDING; the previous round
then supplied `null`, which was reported as CONFLICT 1. The Product Owner has now
resolved it to `2100`, an independent field separate from the Combo Passive
trigger. The earlier reports are preserved above as historical evidence; see
"Enrage / Passive separation".

**B-1/B-2 charge and cooldown finding — the contract defines the mechanism, not
the values; the supplied values are RECORDABLE.**

The Product Owner's §6 states the values "were explicitly resolved after
checking that the existing contract defines the mechanism but does not
mechanically fix per-Boss values." This task independently verified that:

```text
WHAT §6.3 FIXES (the MECHANISM)
  - "Charge Requirement: number of player matches required before the Skill is
     eligible to fire. Matches increment `BossState.SkillCharge` ... When
     `SkillCharge ≥ Charge Requirement` AND `SkillCooldown = 0`, the Skill
     fires."
  - "Cooldown (CD): turns remaining after each Skill use before the Skill can
     fire again. Decrements by 1 at each Turn increment ... The Skill is
     blocked while `CD > 0`."
  - "After the Skill fires: `SkillCharge` resets to 0, `SkillCooldown` resets
     to the Boss's cooldown value."
  Supporting state contract: GAME_STATE.md §2.4.3; Domain members
  `SkillChargeRequirement` / `cooldownTurns` (DATABASE.md §1 note item 4).

WHAT §6.3 DOES NOT FIX (the VALUES)
  The values live in §6.3's per-Boss table (the three existing Bosses:
  5/2, 4/3, 6/2). §6.3 immediately qualifies that table:
    "These are **MVP base configuration** — not universal balance invariants.
     The project owner approved these values. They are configuration defaults
     used at battle creation; they do not represent formulas or scaling rules."
  There is therefore no formula, no fixed value, and no derivation rule binding
  new content. §6.3's reference to "the Boss's cooldown value" defers to the
  per-Boss value, confirming the values are per-Boss authored configuration.

RESULT — RECORD, DO NOT STOP
  The mechanism is fully defined and the values are per-Boss Product Owner
  configuration, exactly as §6.3 states for the existing three. The supplied
  values are recorded:
      Sơn Thạch Vệ   Charge Requirement 5, Cooldown 0
      Kim Lôi Vương  Charge Requirement 5, Cooldown 0

  No new timing mechanic is created. Cooldown 0 is expressible in the existing
  model with no special casing: `SkillCooldown` resets to the Boss's cooldown
  value (0), the `SkillCooldown = 0` firing condition is immediately satisfied
  on recharge, and the Skill is never "blocked while CD > 0". §6.3's decrement
  and block rules simply never bind. No field, no event, and no state member is
  added or changed.
```

**B-1/B-2 cross-document consequences — classification.**

The Product Owner's §7 classification was checked against the authoritative
documents and is **CORRECT as stated**. No document revealed a conflicting
consequence.

```text
BOSS_RULES.md
  → REQUIRES DOWNSTREAM DOCUMENTATION APPLICATION.
    §6.1 gains a base-stat row (HP/MaxHP/ATK/DEF/EnrageThreshold/Initial State);
    §6.2 gains a Passive row plus §6.2.x-style per-Passive detail (including
    Sơn Thạch Vệ's non-default no-retrigger reset behavior, which
    PASSIVE_RULES.md §4 item 3 requires be documented per-Boss);
    §6.3 gains a Skill row (Charge Req. 5 / CD 0 / Base Dmg 150 and 180 / no
    secondary effect); §6.4 gains an identity row; and §6's closing
    "Two additional MVP Bosses ... are not yet content-defined" note requires
    revision. NOT modified by this task.

DATABASE.md
  → REQUIRES DOWNSTREAM CONTENT PROVISIONING ONLY; NO SCHEMA CHANGE.
    §1 note item 5 permits provisioning only content-defined Bosses and states
    rows are "transcribed, never invented"; the two rows become provisionable
    once §6 defines them. §3's column constraints already accept every supplied
    value. §1 note item 1 confirms MaxHP/ATK/DEF/EnrageThreshold are NOT columns
    — they are Domain content — so the new stat values imply no column.
    §1 note item 3 already fixes `threshold = null`; `resetBehavior` uses the
    existing `Default` | `Partial` | `Persistent` token set. No migration, no
    column, no table.

GAME_STATE.md
  → NO NEW STATE MEMBER.
    §2.4's BossState tree already carries BossId, Element, HP/MaxHP/ATK/DEF,
    State, PassiveId, PassiveProgress, SkillCharge, SkillCooldown,
    StatusEffects[]. §2.4.3's charge/cooldown contract accepts Charge Req. 5 and
    CD 0 unchanged. §2.4.4's Enrage contract accepts EnrageThreshold 1500/2100.
    No member, value, or collection is added.

COMBAT_RULES.md
  → NO NEW COMBAT MECHANIC.
    §5.5.1/§5.5.2 already own the Boss-side `TargetStat = "ATK"` `BuffDebuff`
    modifier and its Step-1 `EffectiveBossATK` consumption — which is what both
    Passives use. §5.3/§5.2 item 2 already own Turn-based duration consumption
    and the refresh-not-stack default. Both Skills are direct damage through the
    existing Damage Pipeline (§3, §3.4). §3.4's Step-4 = 1.0 is unchanged.

ELEMENT_RULES.md
  → NO CHANGE REQUIRED.
    §1's Five already include Thổ and Kim; §1.2's one-Element-per-Boss rule is
    satisfied. §6 assigns Pets only and defers Boss Elements to BOSS_RULES.md,
    so no §6 change is needed. Neither Boss requires a dual-element entity (§7
    defers those).

GAME_EVENTS.md
  → NO NEW EVENT REQUIRED.
    The events both Bosses need already exist: `PassiveCharged`/
    `PassiveTriggered` with `source = "boss"`, `BossSkillCast`, and
    `BattleWon`/`BattleLost` (BOSS_RULES.md §7; §7 also states no
    Boss-specific passive event and no dedicated BossStateChanged event exist
    or are needed). §5 item 4's Enrage transition is inferable from the
    existing event sequence, per §7's closing statement.

SIGNALR_PROTOCOL.md
  → NO NEW PROTOCOL REQUIRED.
    §3.2.16–§3.2.18's `passiveId`/`skillId`/`sourceId` members are already
    defined and carry these identities as opaque strings. §4.4's `bossState`
    projection delivers exactly `hp`/`maxHp`; `State`, `EnrageThreshold`,
    `PassiveId`, `SkillCharge`, and `SkillCooldown` stay server-side
    (GAME_STATE.md §2.4.1; BOSS_RULES.md §6.2.4). No member set changes.

NOT CLASSIFIED AS A NEW CONSEQUENCE
  No consequence requires an ADR (no architecture, storage strategy, realtime
  strategy, or authoritative-model change — AGENTS.md §18), and none touches
  MVP_SCOPE.md §1 (both Bosses are already IN scope under "5 Bosses").
```

**B-1/B-2 distinctness validation** (`BOSS_RULES.md` §2, §3, §3.2, §6).

```text
Product Owner's stated intent (verbatim):
  "The Product Owner intentionally selected different Passive trigger
   categories: Sơn Thạch Vệ → Boss HP trigger; Kim Lôi Vương → Player Combo
   trigger. Do NOT change either trigger to another category. This is
   intentional to preserve Boss distinctness."

Element identity — §6 closing note item 1
  Hỏa Long → Hỏa | Thủy Ma → Thủy | Mộc Yêu → Mộc |
  Sơn Thạch Vệ → Thổ | Kim Lôi Vương → Kim
  RESULT: SATISFIED. All five Elements are distinct and complete the roster.
  ELEMENT_RULES.md §1's Five are exactly covered; §1.2's one-per-Boss rule
  holds. The Product Owner's §3 states this is the intended final roster.

Passive trigger category — §3.2 / §6 closing note item 4
  Hỏa Long → Match Count | Mộc Yêu → Match Count | Thủy Ma → Battle Start |
  Sơn Thạch Vệ → Boss HP | Kim Lôi Vương → Combo
  RESULT: SATISFIED. Boss #4 and Boss #5 use different categories from each
  other and from the existing three. `Boss HP` and `Combo` are both in §3
  item 2's closed list. Four of §3.2's categories remain unassigned
  (Turn, Active Pet Power, Active Pet HP, Status), which is permitted — §3.2
  requires difference "where possible", not exhaustive coverage.
  This resolves the observation reported at the first concept-direction stage:
  the earlier shared-`Combo` pairing is superseded by these decisions.

EnrageThreshold — §5 item 4 / §6.1
  Hỏa Long → 1500 (30% of 5000) | Thủy Ma → 1500 | Mộc Yêu → 1500 |
  Sơn Thạch Vệ → 1500 (50% of 3000) | Kim Lôi Vương → 2100 (75% of 2800)
  RESULT: SATISFIED as distinct per-Boss values. §5 item 4 requires a
  per-Boss §6.1 value and states any Enrage-specific behavior change is
  per-Boss; it prescribes no range, no formula, and no uniformity. Both
  supplied values are valid absolute HP thresholds below their MaxHP.
  REPORTED (informational, not a violation): Kim Lôi Vương's 75% threshold is
  proportionally earlier than every other MVP Boss's 30%/50%, and Sơn Thạch
  Vệ's Enrage boundary numerically coincides with its Passive trigger
  boundary. Both were explicitly chosen by the Product Owner (the Enrage/Passive
  separation is stated as deliberate), so neither is questioned or modified.

Passive/Skill mechanical distinctness — §2 item 2
  "Every MVP Boss must have a Passive AND a Skill that are mechanically
   distinct from each other and from other Bosses' Passive/Skill."
  RESULT: SATISFIED at the level the decisions allow.
   - Passive vs Skill, within each Boss: Sơn Thạch Vệ's Passive is a
     Boss-side ATK buff (+20%, 3 Turns, one-shot on an HP threshold) while its
     Skill is pure direct damage (150, no secondary effect). Distinct
     mechanisms.
     Kim Lôi Vương's Passive is a Boss-side ATK buff (+20%, 1 Turn, Combo
     triggered) while its Skill is pure direct damage (180, no secondary
     effect). Distinct mechanisms.
   - Across Bosses: the two new Passives share a +20% ATK effect magnitude
     with Hỏa Long's Rage, but differ in trigger category (Boss HP / Combo vs
     Match Count) and in duration (3 Turns / 1 Turn vs 3 Turns), and Hỏa
     Long's Rage is match-charged while these are not (§6.2's PassiveThreshold
     scoping). §2 item 2 requires the Passive *and* Skill pair to be distinct;
     it does not forbid two Bosses from using the same effect magnitude. The
     Product Owner selected the triggers deliberately to satisfy §3.2.
   - Their Skills are also distinct from each other by Base Damage and SkillId,
     and neither applies a secondary effect — so neither duplicates Hỏa Long's
     Burn, Thủy Ma's Power drain, or Mộc Yêu's ATK debuff.
   - Reported, informational: the effect magnitudes (+20% ATK) coincide across
     three Bosses' Passives. §2's requirement is on the trigger/mechanism
     pairing, which is distinct, so this does not violate §2 — it is noted so
     the Product Owner can confirm the intent if a wider effect-magnitude
     spread was wanted. Not a conflict and not modified.

§2 item 1 (difficulty from mechanics, not HP/ATK inflation)
  All three existing Bosses use HP 5000 / ATK 100 / DEF 50. These two use
  HP 3000 / ATK 120 / DEF 0 and HP 2800 / ATK 140 / DEF 0 — differing stat
  profiles with mechanic-driven Passives. No conflict.

§2 item 3 / §3.1 (determinism, no undeclared randomness)
  RESULT: SATISFIED. No randomness is declared by either specification; no
  undeclared random branch is introduced.

RESULT OF VALIDATION: no §2, §3, §3.2, or §6 violation found. No STOP fires.
The decisions were NOT modified.
```

```text
Decision:
FULLY RECORDED — all coverage items DECIDED. No field remains PENDING. The
CONFLICT 1 `null` EnrageThreshold and the CONFLICT 3 BossDefinitionId supply
were each resolved by explicit Product Owner decision; see "Enrage / Passive
separation" and the per-item entries above for the dispositions and the
historical record.
```

---

## Required Decision Coverage

The recorded decision must explicitly resolve **all** items below. Any item left
unspecified is recorded as unspecified — not inferred, and not defaulted.

```text
D-1  MVP Boss #4 content
     → answered by the B-1 slot above.

D-2  MVP Boss #5 content
     → answered by the B-2 slot above.

D-3  Boss identity — display name and BossId, for each Boss
     → coverage items 1 and 2. BossId must follow BOSS_RULES.md §6.4's
       `boss-<ascii-kebab-case-name>` convention.

D-4  Element, for each Boss
     → coverage item 3. Exactly one of the Five (ELEMENT_RULES.md §1, §1.2).
       State the pairing intent relative to the existing three, per §6's
       closing note item 1.

D-5  Base stats — HP/MaxHP, ATK, DEF, EnrageThreshold, Initial State
     → coverage items 4–8. BOSS_RULES.md §6.1's row shape. Note that
       DATABASE.md §1 note item 1 does NOT store these; they are Domain content.

D-6  Passive identity and trigger
     → coverage items 10–13. PassiveId per §6.4; an explicit trigger category
       from §3 item 2's closed list per §6's closing note item 2; the exact
       condition; and the PassiveThreshold rule where match-charged.

D-7  Passive effect, magnitude, duration, representation
     → coverage items 14–17.

D-8  Passive charge/reset and reapplication behavior
     → coverage items 18–19. Reset Behavior must be one of PASSIVE_RULES.md §4's
       forms, stored as one of DATABASE.md §1 item 3's `Default` | `Partial` |
       `Persistent` tokens.

D-9  Skill identity and definition
     → coverage items 20–21. SkillId per §6.4; the effect and its target(s).

D-10 Skill timing, charge, cooldown
     → coverage items 22–24. BOSS_RULES.md §4 item 2 requires the timing rule to
       be explicit; §6.3's Charge Requirement and CD are the mechanism, including
       the post-fire reset.

D-11 Skill Base Damage and secondary effects, with magnitudes and durations
     → coverage items 25–28. §6.3.1's declaration pattern per effect.

D-12 Distinctness confirmation at the set level
     → coverage item 29. Explicit confirmation that Element pairing, Passive
       trigger category, Skill identity, and core gameplay role do not
       unintentionally duplicate an existing Boss, satisfying BOSS_RULES.md §3.2
       and §6's closing note item 4; and that the two new Bosses satisfy §3.2
       *together*. §6's note item 1 confirms a shared Element is permitted — only
       trigger-pattern diversity is required.

D-13 Persistence key
     → coverage item 30. `BossDefinitionId` per DATABASE.md §1 note item 2, or an
       explicit deferral of its spelling to the downstream task.

D-14 Existing-vocabulary check
     → Confirmation that each Boss is expressible in the EXISTING closed sets:
       the trigger categories (BOSS_RULES.md §3 item 2 / PASSIVE_RULES.md §3), the
       Five Elements (ELEMENT_RULES.md §1), the `PassiveDefinition` and
       `SkillDefinition` member sets (DATABASE.md §1 items 3–4), the Status
       Effects and `TargetStat` cases (COMBAT_RULES.md §5), the Boss States
       (BOSS_RULES.md §5), and the existing events (BOSS_RULES.md §7). If either
       Boss needs a new Element, trigger category, effect identity, status,
       stat, resource, representation, state, or event, record that as a
       SEPARATE unresolved decision per the Stop Conditions — do not record it
       as part of this answer.

D-15 Downstream consequences
     → coverage item 31. Which authoritative sections must change — at minimum
       BOSS_RULES.md §6's four rows — and whether anything beyond BOSS_RULES.md
       is required.

D-16 No-presumption check
     → Explicit confirmation that neither answer was inferred from the three
       existing Bosses, from player progression, from an expected difficulty
       target, or from implementation convenience (the task's §8 prohibition).

### Coverage resolution

```text
D-1  MVP Boss #4 content — Sơn Thạch Vệ
     FULLY RESOLVED: display name, BossId (`boss-son-thach-ve`),
       BossDefinitionId (`boss-def-son-thach-ve`), Element (Thổ),
       role (Defensive / Enrage), HP 3000, MaxHP 3000, ATK 120, DEF 0,
       Initial State (`Idle`), EnrageThreshold (1500 — a separate field from
       the Passive trigger), PassiveId (`son-thach-ve-enrage`), Passive trigger
       category (`Boss HP`), Passive trigger condition (Boss HP ≤ 50%),
       Passive effect (+20% ATK), Passive magnitude (+20), Passive duration
       (3 Turns), Passive representation (Turn-based `BuffDebuff` in
       `BossState.StatusEffects[]`, `TargetStat = "ATK"` — fixed by
       COMBAT_RULES.md §5.5.1), Passive reset/reapplication (no re-trigger
       after activation; no repeated triggering in the same battle),
       SkillId (`earthquake`), Skill name (Earthquake), Skill role (Burst Boss
       attack), Skill Base Damage (150), Charge Requirement (5), Cooldown (0),
       Skill timing (existing §6.3 charge/timing contract),
       Secondary Effects (None), Board Effect (None),
       PassiveThreshold (null — NOT APPLICABLE), cross-document consequences
       (classified).
     PENDING PRODUCT OWNER DECISION: none.
     HISTORICAL: the `Boss HP ≤ 50%` figure was first recorded as the PASSIVE
       trigger exactly as placed, and the question of whether it also served as
       the Boss's `EnrageThreshold` was returned for confirmation rather than
       assumed. The Product Owner has now resolved it: EnrageThreshold = 1500
       as an independent field, with the 50% boundary remaining the Passive
       trigger, and no collapsing of the two identities.

D-2  MVP Boss #5 content — Kim Lôi Vương
     FULLY RESOLVED: display name, BossId (`boss-kim-loi-vuong`),
       BossDefinitionId (`boss-def-kim-loi-vuong`), Element (Kim),
       role (Aggressive / Combo-punishment), HP 2800, MaxHP 2800, ATK 140,
       DEF 0, Initial State (`Idle`), EnrageThreshold (2100 — a separate field
       from the Combo Passive trigger), PassiveId (`kim-loi-vuong-combo`),
       Passive trigger category (`Combo`), Passive trigger condition
       (Player Combo ≥ 4), Passive effect (+20% ATK), Passive magnitude (+20),
       Passive duration (1 Turn), Passive representation (same Turn-based
       `BuffDebuff` in `BossState.StatusEffects[]`, `TargetStat = "ATK"`),
       Passive reset (existing §4 semantics), Passive reapplication (existing
       §5.2 item 2 refresh-not-stack / §5.5.5 semantics — no new stacking
       behavior), SkillId (`thunder-strike`), Skill name (Thunder Strike),
       Skill role (Burst Boss attack), Skill Base Damage (180),
       Charge Requirement (5), Cooldown (0), Skill timing (existing §6.3
       contract), Secondary Effects (None),
       PassiveThreshold (null — NOT APPLICABLE), cross-document consequences
       (classified).
     PENDING PRODUCT OWNER DECISION: none.
       cross-document consequences.

D-3  Boss identity — display name and BossId
     FULLY RESOLVED. Display names (Sơn Thạch Vệ / Kim Lôi Vương) and BossIds
     (`boss-son-thach-ve` / `boss-kim-loi-vuong`) are all explicitly supplied
     by the Product Owner. Both BossIds follow BOSS_RULES.md §6.4's
     `boss-<ascii-kebab-case-name>` convention. HISTORICAL: an earlier stage
     recorded BossId as PENDING on the reasoning that §6.4 states the values
     are "fixed here so no task invents its own"; the Product Owner has now
     supplied them directly.

D-4  Element
     RESOLVED for both — Thổ (Boss #4), Kim (Boss #5). Both within
     ELEMENT_RULES.md §1's Five; both respect §1.2. All five Elements are now
     distinctly assigned across the five MVP Bosses, as the Product Owner's §3
     states is intended.

D-5  Base stats — HP/MaxHP, ATK, DEF, EnrageThreshold, Initial State
     FULLY RESOLVED for both. HP/MaxHP/ATK/DEF: 3000/3000/120/0 (Boss #4) and
     2800/2800/140/0 (Boss #5). EnrageThreshold: 1500 (Boss #4) and 2100
     (Boss #5) — absolute per-Boss §6.1 values, each an independent field
     separate from that Boss's Passive trigger (see "Enrage / Passive
     separation" for both). Initial State: `Idle` for both, per BOSS_RULES.md
     §1's closed State enum. HISTORICAL: EnrageThreshold and Initial State were
     previously PENDING; a later round supplied `null` and `ACTIVE`
     respectively, both reported as conflicts and both resolved by explicit
     Product Owner decision.

D-6  Passive identity and trigger
     FULLY RESOLVED for both. Trigger category: `Boss HP` and `Combo`, both in
     BOSS_RULES.md §3 item 2's closed set, both unused by the existing three,
     and deliberately different from each other per the Product Owner's §3.
     Trigger conditions: Boss HP ≤ 50% and Player Combo ≥ 4. PassiveIds:
     `son-thach-ve-enrage` and `kim-loi-vuong-combo`. PassiveThreshold NOT
     APPLICABLE for both and remains `null`, per the Product Owner's §5 and
     DATABASE.md §1 note item 3.

D-7  Passive effect, magnitude, duration, representation
     RESOLVED for both. Effect +20% ATK; magnitude +20; durations 3 Turns and
     1 Turn; representation is the existing Turn-based `BuffDebuff` ATK
     instance in `BossState.StatusEffects[]` (COMBAT_RULES.md §5.5.1;
     GAME_STATE.md §2.4.1) — the same shape BOSS_RULES.md §6.2.1 records for
     Hỏa Long's Rage. No new status type and no new `TargetStat`; the
     Product Owner's explicit prohibition on `BossNextAttackATKModifier`,
     `ComboPunishmentModifier`, and a new StatusEffect type is satisfied,
     because COMBAT_RULES.md §5.5.2 already establishes that the modifier
     reaches a Boss Skill through the Step-1 `EffectiveBossATK` contribution
     — which is the stated intent ("the Boss's next relevant attack is
     stronger") without any new representation. NO STOP.

D-8  Passive charge/reset and reapplication behavior
     RESOLVED for both. Boss #4: no re-trigger after activation, no repeated
     triggering in the same battle — a non-default behavior expressible as
     PASSIVE_RULES.md §4's "No reset / persistent" form with the
     DATABASE.md §1 item 3 storage token `Persistent` (reported; not authored
     into any document). Boss #5: the existing authoritative semantics — §4
     Default and COMBAT_RULES.md §5.2 item 2's refresh-not-stack — applied
     as-is, with no new stacking behavior. NO STOP.

D-9  Skill identity and definition
     FULLY RESOLVED: names (Earthquake / Thunder Strike), SkillIds
     (`earthquake` / `thunder-strike`), roles (Burst Boss attack), and
     Secondary Effects (None for both). HISTORICAL: SkillId was previously
     recorded PENDING because display names are not §6.4 SkillIds; the Product
     Owner has now supplied the SkillIds directly.

D-10 Skill timing, charge, cooldown
     FULLY RESOLVED for both. The timing CONTRACT is the existing Boss Skill
     charge/timing contract (BOSS_RULES.md §6.3 — the Charge Requirement +
     Cooldown mechanism, §3.3 item 2's ordering, and §6.3's post-fire reset).
     The VALUES are Charge Requirement 5 and Cooldown 0 for both Bosses,
     recorded as the Product Owner's explicit MVP balance decision after this
     task verified that §6.3 defines the mechanism but fixes no per-Boss value
     (its table is configuration §6.3 states the project owner approved). See
     "B-1/B-2 charge and cooldown finding". No new timing mechanic.

D-11 Skill Base Damage and secondary effects, with magnitudes and durations
     RESOLVED for both. Base Damage 150 (Earthquake) and 180 (Thunder Strike);
     no secondary effects; no board effect. No magnitude or duration is
     outstanding because no secondary effect exists.

D-12 Distinctness confirmation at the set level
     RESOLVED. See "B-1/B-2 distinctness validation". Elements: all five
     distinct and complete. Trigger categories: `Boss HP` and `Combo` differ
     from each other and from the existing three (Match Count ×2, Battle
     Start) — this SUPERSEDES and resolves the shared-`Combo` observation
     reported at the earlier concept-direction stage. EnrageThresholds are
     distinct per-Boss values. §2 item 2's mechanical distinctness holds for
     the Passive/Skill pairs. No §2, §3, §3.2, or §6 violation found; no STOP.
     Two informational notes are reported: the +20% ATK magnitude coincides
     across three Bosses' Passives (which §2 does not forbid because the
     mechanisms and triggers differ), and Kim Lôi Vương's 75% Enrage threshold
     is proportionally earlier than every other MVP Boss's.

D-13 Persistence key
     FULLY RESOLVED for both — `boss-def-son-thach-ve` and
     `boss-def-kim-loi-vuong`, following DATABASE.md §1 note item 2's
     `boss-def-<ascii-kebab-case-name>` form and deliberately distinct from
     each Boss's BossId. HISTORICAL: an earlier round supplied values equal to
     the BossIds, which this task reported as CONFLICT 3 (DATABASE.md §1 note
     item 2: "distinct from Identity ... and never derived from either");
     resolved by explicit Product Owner decision.

D-14 Existing-vocabulary check
     RESOLVED. Both Elements are in ELEMENT_RULES.md §1's Five. Both trigger
     categories are in BOSS_RULES.md §3 item 2's closed set. Both Passives use
     the existing Boss-side `BuffDebuff` ATK modifier contract
     (COMBAT_RULES.md §5.5.1) with `TargetStat = "ATK"` — no non-`"ATK"`
     `TargetStat` case is opened, so §5.4.5/§5.5.3's "would require its own
     recorded decision" boundary is not exercised. Both Skills are direct
     damage through the existing Damage Pipeline. `Initial State = Idle` is a
     member of §1's closed State enum. `EnrageThreshold` 1500/2100 are ordinary
     §6.1 per-Boss values. Charge Requirement 5 / Cooldown 0 use §6.3's
     mechanism, and CD 0 requires no special case. The BossIds and
     BossDefinitionIds follow §6.4's and DATABASE.md §1 note item 2's stated
     conventions; the PassiveIds/SkillIds use forms §6.4 does not restrict
     (see "B-1/B-2 identity convention finding"). No new Element, trigger
     category, effect type, status, stat, resource, representation, state,
     board mechanic, event, or storage field is required by anything supplied.
     No `EffectDefinition` field is added; `Element`, `DamageElement`,
     `BurnElement`, `TargetStat`, and `BossNextAttack` are NOT added to it.
     NO STOP.

D-15 Downstream consequences
     RESOLVED — classified, not applied. See "B-1/B-2 cross-document
     consequences": BOSS_RULES.md requires downstream documentation
     application; DATABASE.md requires downstream content provisioning only
     (no schema change); GAME_STATE.md, COMBAT_RULES.md, ELEMENT_RULES.md,
     GAME_EVENTS.md, and SIGNALR_PROTOCOL.md require no change. No document
     was modified by this task.

D-16 No-presumption check
     SATISFIED. No value in this record was inferred from the three existing
     Bosses, from player progression, from an expected difficulty target, or
     from implementation convenience. Every field the Product Owner's
     specification does not state is marked PENDING PRODUCT OWNER DECISION.
     The two Passives' representation is fixed by an authoritative contract
     (COMBAT_RULES.md §5.5.1) rather than chosen here, and both Bosses'
     `Boss HP ≤ 50%` placement was recorded exactly as supplied rather than
     reinterpreted as an Enrage threshold.
```

```text
FULLY RESOLVED — DECISION-COMPLETE.

ALL REQUIRED DECISION SLOTS ARE NOW EXPLICITLY SUPPLIED, for both Bosses:

  Sơn Thạch Vệ — BossId, BossDefinitionId, Element, HP/MaxHP, ATK, DEF,
    Initial State, EnrageThreshold, Passive trigger category + condition,
    Passive effect + magnitude + duration, Passive representation,
    Passive reset/reapplication, PassiveId, PassiveThreshold (null),
    Skill name, SkillId, Base Damage, Charge Requirement, Cooldown,
    Secondary Effects (none), Board Effect (none).

  Kim Lôi Vương — BossId, BossDefinitionId, Element, HP/MaxHP, ATK, DEF,
    Initial State, EnrageThreshold, Passive trigger category + condition,
    Passive effect + magnitude + duration, Passive representation,
    Passive reset/reapplication, PassiveId, PassiveThreshold (null),
    Skill name, SkillId, Base Damage, Charge Requirement, Cooldown,
    Secondary Effects (none).

NO CONFLICT REMAINS. The three conflicts reported at the previous stage were
each resolved by explicit Product Owner decision:
  CONFLICT 1 (Kim Lôi Vương EnrageThreshold null)
    → RESOLVED: EnrageThreshold = 2100, an independent §6.1 field separate
      from the Combo Passive trigger.
  CONFLICT 2 (Initial State = ACTIVE, not a State enum member)
    → RESOLVED: Initial State = Idle, the documented initial State.
  CONFLICT 3 (BossDefinitionId equal to BossId)
    → RESOLVED: the DATABASE.md §1 note item 2 `boss-def-<ascii-kebab-case-
      name>` form, deliberately distinct from BossId.

NO STOP CONDITION FIRED. Every supplied decision is expressible in the
existing authoritative contract:
  - Elements Thổ and Kim are within ELEMENT_RULES.md §1's Five (§1.2 satisfied).
  - Trigger categories `Boss HP` and `Combo` are within BOSS_RULES.md §3
    item 2's closed set.
  - The +20% ATK effect is the existing COMBAT_RULES.md §5.5.1 Boss-side
    `TargetStat = "ATK"` `BuffDebuff` modifier (the §6.2.1 Rage shape). No new
    status type, no new `TargetStat`, no `BossNextAttackATKModifier`, and no
    `ComboPunishmentModifier`.
  - Durations 3 Turns / 1 Turn use COMBAT_RULES.md §5.3's Turn-based lifecycle.
  - Sơn Thạch Vệ's no-retrigger reset is PASSIVE_RULES.md §4 item 2's
    No-Reset form (storage token `Persistent`, DATABASE.md §1 item 3).
    Kim Lôi Vương's reset/reapplication are §4's Default and COMBAT_RULES.md
    §5.2 item 2's refresh-not-stack.
  - EnrageThreshold 1500 / 2100 are §6.1 per-Boss values driving §5 item 4's
    permanent transition. No new Enrage mechanic.
  - Initial State `Idle` is BOSS_RULES.md §1's closed enum member.
  - Charge Requirement 5 / Cooldown 0 use §6.3's mechanism; the values are
    per-Boss configuration §6.3 states are project-owner approved. CD 0 needs
    no special case.
  - Both Skills are direct damage through the existing Damage Pipeline; no
    secondary effect and no board effect, so no board mechanic is required.
  - BossId/BossDefinitionId follow §6.4's and DATABASE.md §1 note item 2's
    stated conventions and remain distinct.
  - PassiveId/SkillId: §6.4 states no exact Boss value form, so the supplied
    forms are recorded verbatim (see "B-1/B-2 identity convention finding").
  - No `EffectDefinition` field is added; `Element`, `DamageElement`,
    `BurnElement`, `TargetStat`, and `BossNextAttack` are NOT added.

REPORTED, NOT RESOLVED (informational only; neither is a conflict):
  1. §6.1's percentage parenthetical rendering for Sơn Thạch Vệ's threshold
     ("1500 (50%)" for consistency with the existing rows) — a downstream
     transcription detail; the stored value is 1500.
  2. The +20% ATK magnitude coincides across three Bosses' Passives. §2 item 2
     governs trigger/mechanism distinctness, which holds.
  3. The supplied PassiveId/SkillId spellings differ from the three existing
     Bosses' observed spellings. §6.4 states no convention, so this blocks
     nothing; the downstream documentation task may wish to confirm the
     intended long-term Boss identity style.

TASK-171's decision-input objective is therefore satisfied. Per the Product
Owner's §12, Status becomes READY. It is NOT marked DONE: a decision-input task
is executed by recording, but per tasks/TASK_LIFECYCLE.md §3, DONE requires the
task to be moved to completed/ after review, which belongs to the repository's
lifecycle rather than to this recording step.
```

---

## Recording Discipline

```text
Decision owner:     The Product Owner (human). Both items are GAMEPLAY CONTENT
                    decisions. No agent, and no domain agent, may supply them
                    (AGENTS.md §7; .ai/README.md §8; .ai/agents/orchestrator.md
                    §Decision Authority; .ai/agents/gameplay.md).

Recording agent:    An agent may ONLY transcribe a supplied answer verbatim.
                    It may fix formatting, never wording, names, or values.

Answer verbatim:    Answers must be recorded exactly as the Product Owner gives
                    them. If an answer is ambiguous or incomplete relative to
                    its required coverage, record it as given and note the gap
                    — do not resolve it.

Derived details:    Any mechanically derived detail (e.g. the `BossDefinitionId`
                    spelling or the storage encoding the provisioning work will
                    need) is NOT part of this decision and is NOT recorded here
                    as if it were. BOSS_RULES.md §6.4 names Bosses by display name
                    and fixes the BossId convention; the persistence key is
                    owned by DATABASE.md §1 and is the downstream task's to
                    apply.

Ownership of the
follow-on edit:     Transcribing the recorded answers into docs/ is the
                    DOWNSTREAM documentation-application task's deliverable,
                    per documentation/documentation-change.md §3 (canonical
                    owner) — at minimum BOSS_RULES.md §6.1's base-stat row,
                    §6.2's Passive row, §6.3's Skill row, §6.4's identity row,
                    and §6's closing deferral note.
```

---

## Scope

### In Scope
- Present the required decision slots for MVP Boss #4 and MVP Boss #5, derived
  from the current `BOSS_RULES.md` §1/§3/§4/§5/§6 contract
- Record the Product Owner's explicit answers for both, verbatim
- State the coverage each answer must satisfy, including the conditional fields
  each Boss's chosen mechanics imply
- Record the distinctness confirmation required by `BOSS_RULES.md` §3.2 / §6's
  closing note item 4, using that document's own rule
- Record which existing closed vocabulary each answer must be expressible in

### Out of Scope
- Applying the decisions to any `docs/` file (`BOSS_RULES.md`, `MVP_SCOPE.md`,
  `ROADMAP.md`, `GAME_RULES.md`, `GAME_STATE.md`, `DATABASE.md`,
  `ELEMENT_RULES.md`, `PASSIVE_RULES.md`, `COMBAT_RULES.md`, `MATCH3_RULES.md`,
  `GAME_EVENTS.md`, `SIGNALR_PROTOCOL.md`, `API_CONTRACTS.md`)
- Any source-code change (`src/**`) — including `BossDefinitions.cs`,
  `BossDefinition.cs`, `BossState.cs`, `BattleStateService.cs`,
  `CardCastExecutor.cs`
- Any test change (`tests/**`)
- Any migration, seed, `HasData`, startup loader, or database row insertion
- Any change to an existing task file, including
  `tasks/backlog/TASK-170-*.md` and every `tasks/completed/**` file
- Creating TASK-172 or any other follow-up/implementation task
- Choosing, recommending, ranking, or defaulting any Boss name, Element, stat,
  Passive trigger, Skill, magnitude, or identity
- Introducing a new Element, a new trigger category, a new status effect, a new
  stat, a new resource, a new event, a new SignalR method, a new Redis key, or a
  new database column
- Reopening the TASK-170 GAP-B conclusion, or adding an `Element` member to
  `EffectDefinition`
- Any architecture decision (no ADR is created by this task)
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2

---

## Current State

The repository's task lifecycle is otherwise quiet: `tasks/backlog/` holds
`TASK-170-audit-classify-boss-count-and-effect-element-gaps.md` (BACKLOG,
unrelated subject — it audits and classifies, and creates nothing);
`tasks/active/` holds no task file; `tasks/blocked/` holds only the unrelated
`TASK-036-discord-credential-secret-hygiene.md`; and `tasks/completed/` holds
TASK-001 … TASK-169. The highest existing ID is **TASK-170**, so this task is
**TASK-171** (`tasks/README.md` §3).

The MVP Boss content set is complete for three of five Bosses. For the
remaining two, the Product Owner has now supplied **full gameplay
specifications** — stats, Elements, roles, Passives, and Skills — which are
recorded in `B-1` and `B-2`. No authoritative document has been changed for
them, no `BossDefinition` row exists for them, and no Domain content entry
exists for them; those remain downstream steps.

```text
BOSS_RULES.md §6        → 3 Bosses content-defined; 2 deferred with an explicit
                          authoring-requirement list
ROADMAP.md §1 Phase 2   → "All 5 Bosses (2 not yet content-defined)"
DATABASE.md §1/§3 #5    → only content-defined Bosses may ever be provisioned
MVP_SCOPE.md §1         → "5 Bosses"; "Element, Passive, Skill per Boss" is IN
```

**Status of the two decisions after this recording — COMPLETE.**

```text
Sơn Thạch Vệ   boss-son-thach-ve / boss-def-son-thach-ve   Thổ
               HP 3000 / MaxHP 3000 / ATK 120 / DEF 0 / Initial State Idle
               EnrageThreshold 1500 (separate field)
               Passive  son-thach-ve-enrage — Boss HP ≤ 50% → +20% ATK for
                        3 Turns, no re-trigger; PassiveThreshold null
               Skill    earthquake — Earthquake, 150 Base Damage,
                        Charge Req. 5, CD 0, no secondary effect

Kim Lôi Vương  boss-kim-loi-vuong / boss-def-kim-loi-vuong  Kim
               HP 2800 / MaxHP 2800 / ATK 140 / DEF 0 / Initial State Idle
               EnrageThreshold 2100 (separate field)
               Passive  kim-loi-vuong-combo — Player Combo ≥ 4 → +20% ATK for
                        1 Turn, existing reset/reapplication semantics;
                        PassiveThreshold null
               Skill    thunder-strike — Thunder Strike, 180 Base Damage,
                        Charge Req. 5, CD 0, no secondary effect
```

```text
DECIDED (all 31 coverage items, both Bosses) — decision-complete.
PENDING PRODUCT OWNER DECISION — none.
CONFLICTS — none outstanding. All three reported conflicts were resolved by
  explicit Product Owner decision (Initial State → Idle; Kim Lôi Vương
  EnrageThreshold → 2100; BossDefinitionId → the `boss-def-*` form distinct
  from BossId).
```

---

## Acceptance Criteria

- [x] Two distinct Boss decision records exist — one for MVP Boss #4 (`B-1`) and
      one for MVP Boss #5 (`B-2`), each answered independently.
- [x] Every required Boss identity field has an explicit Product Owner decision:
      display name, `BossId` (per `BOSS_RULES.md` §6.4's convention),
      `PassiveId`, `SkillId`, and the `BossDefinitionId` disposition
      (`DATABASE.md` §1 note item 2).
- [x] Each Boss's Element has an explicit decision, drawn from
      `ELEMENT_RULES.md` §1's Five and respecting §1.2's one-Element rule.
- [x] Each Boss's base stats have explicit decisions: `HP`/`MaxHP`, `ATK`, `DEF`,
      `EnrageThreshold`, `Initial State` (`BOSS_RULES.md` §6.1).
- [x] Every required Passive field has an explicit decision: trigger category
      from `BOSS_RULES.md` §3 item 2's closed list, the exact condition, the
      effect, the magnitude, and the representation.
- [x] Passive trigger behavior is explicit: the category, the condition, the
      evaluation point, and — where match-charged — the `PassiveThreshold`; and,
      where not match-charged, an explicit statement of that fact with the
      `threshold = null` consequence (`DATABASE.md` §1 note item 3).
- [x] Passive charge/reset behavior is explicit where applicable, using exactly
      one of `PASSIVE_RULES.md` §4's forms and `DATABASE.md` §1 item 3's storage
      tokens.
- [x] Every required Skill field has an explicit decision: `SkillId`, the effect
      and its target(s), the timing rule, the Charge Requirement, the Cooldown,
      the Base Damage, and any secondary effect.
- [x] Skill timing/charge behavior is explicit, per `BOSS_RULES.md` §4 item 2,
      §6.3's post-fire reset, and §3.3 item 2's ordering relative to the Passive.
- [x] Required magnitudes and durations are explicit for every effect used —
      including an explicit "instant, no duration" where an effect does not
      persist (`BOSS_RULES.md` §6.3.1 item 2's precedent). Both Skills apply no
      secondary effect, so none is outstanding.
- [x] Content distinctness requirements are explicitly confirmed: Element
      pairing, Passive trigger category, Skill identity, and core gameplay role
      against the existing three Bosses (`BOSS_RULES.md` §3.2, §2 items 2–3,
      §6's closing note items 1 and 4), including the set-level statement that
      the two new Bosses satisfy §3.2 together.
- [x] Each answer is confirmed expressible in the existing closed vocabulary, or
      the exact missing contract is recorded as a separate unresolved decision
      (`D-14`). Nothing required a new contract.
- [x] No decision is inferred by the task author: both `Decision:` slots contain
      the Product Owner's answers verbatim, and the only contract-derived values
      (the Passive representation and the reset/reapplication semantics) are
      fixed by an authoritative document rather than chosen here.
- [x] The task explicitly records: no authoritative documentation modified, no
      source code modified, no database/schema change, no new task created.
- [x] The `EffectDefinition`/`Element` boundary is explicitly recorded as closed
      and unmodified (`TASK-170` GAP-B; `TASK-167`/`TASK-168` decisions), and no
      `Element` field is introduced anywhere.
- [x] The task file is the only file added; zero files are modified.
- [ ] Quality review checklist passes (`quality/review.md` §1) — **PENDING
      REVIEW.** This is the one criterion not satisfiable by the recording step
      itself; per `tasks/TASK_LIFECYCLE.md` §2, `READY → IN PROGRESS → IN REVIEW`
      is where review runs.
- [x] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001)

---

## Affected Files & Areas

```text
[ ] src/backend/ (Domain / Application / Infrastructure / Api)   → NOT MODIFIED
[ ] src/frontend/client/ (scenes / runtime / services / state / ui)
                                                                 → NOT MODIFIED
[ ] tests/ (unit / integration / gameplay scenarios)             → NOT MODIFIED
[ ] docs/ (authoritative documentation)                          → NOT MODIFIED
[x] tasks/backlog/ — this task file only (TASK-171)
```

---

## Implementation Notes

**This task writes nothing except this task file.** It is a decision-slot
presentation, input-capture, transcription, and coverage-verification task.

The decision slots are derived from exactly four `BOSS_RULES.md` locations, and
no others are needed:

```text
Boss identity / BossId / display name   → §6 (the reference tables) + §6.4
Element                                  → §1 (structure) + §6.1 + §6 note 1
Base HP / ATK / DEF / Enrage / State     → §6.1 + §5 item 4 (Enrage)
Passive identity / trigger / threshold   → §3 item 2 (closed trigger list),
                                            §6.2 + §6.2.1–§6.2.4, §6.4
Passive effect / magnitude / duration    → §6.2.1–§6.2.4
Skill identity / timing / charge / CD    → §4 item 2, §6.3 + §6.3.1, §6.4
Skill damage / secondary effects         → §4 items 3–4, §6.3.1
```

Storage expressibility is checked against exactly two `DATABASE.md` locations:
§1 note items 3–4 (the closed `PassiveDefinition` / `SkillDefinition` member
sets) and §3 (the `BossDefinition` column constraints).

**Do not treat the three existing Bosses as a template.** `BOSS_RULES.md` §2
item 2 and §3.2 require distinctness, and §6's closing note item 4 requires the
new Bosses to avoid duplicating an existing trigger category where reasonably
possible. A "fourth copy of the §6.1 stat row with a new name" is not what §6
asks for, and an agent must not propose it. Equally, §6's note item 1 explicitly
permits sharing an Element — do not treat Element reuse as a violation.

**Do not restate any rule, magnitude, formula, or schema in this file**
(`tasks/README.md` §9). The `BOSS_RULES.md` §6 tables are cited by section, not
copied.

---

## Testing Requirements

### Required Verification
```text
[ ] Unit tests         — N/A (no code changed)
[ ] Integration tests  — N/A (no boundary changed)
[ ] Gameplay scenarios — N/A (no gameplay behavior changed)
```

### Required Non-Code Verification
```text
[ ] Both `Decision:` slots are present and contain the Product Owner's verbatim
    answers, or are explicitly recorded as awaiting input
[ ] Every coverage item 1–31 that the chosen mechanics imply is either answered
    or explicitly recorded as unspecified — none is inferred
[ ] Each Element decision is one of ELEMENT_RULES.md §1's Five
[ ] Each Passive trigger category is one of BOSS_RULES.md §3 item 2's closed list
[ ] Each answer is confirmed expressible in DATABASE.md §1 items 3–4's closed
    member sets, or the missing contract is recorded as a separate decision
[ ] The distinctness confirmation is present for both Bosses and at the set level
[ ] `git status` / file diff shows tasks/backlog/TASK-171-*.md as the ONLY added
    file and ZERO modified files
[ ] No `Element` member was added to `EffectDefinition`, and no
    EffectDefinition/CARD_RULES/ELEMENT_RULES/CardCastExecutor/BattleStateService
    change was made
```

### Key Edge Cases
- `BOSS_RULES.md` §6's closing note item 1 explicitly permits two Bosses to share
  an Element ("distinct pairing … is encouraged but not mandated"). Do not treat
  a shared Element as a distinctness failure; §3.2 requires only trigger-*pattern*
  diversity. See `BOSS_RULES.md` §6 note item 1 and §3.2.
- `DATABASE.md` §1 note item 3: `threshold = null` means "no match-charging
  threshold" and is **NOT** a statement that the Passive is always-active. If an
  answer's Passive is not match-charged, the consequence must be recorded
  correctly. Thủy Ma's record is the worked precedent (`BOSS_RULES.md` §6.2).
- `BOSS_RULES.md` §5 item 5: "For MVP, no content-defined Boss applies Stun." A
  proposed Stun mechanic is not a field to fill — it changes that statement and
  is a STOP.
- `BOSS_RULES.md` §5 item 3: a full multi-phase State machine is FUTURE (GDD
  §20). A proposed phase machine is out of MVP scope, not a Boss field.
- `BOSS_RULES.md` §3.1: no undeclared random branch is allowed. A Skill that
  states "random target" may use server RNG only because the Skill declares it;
  otherwise randomization is a STOP.
- `COMBAT_RULES.md` §5.4.5 / §5.5.3 close the non-`"ATK"` `TargetStat` case with
  "would require its own recorded decision". A proposed debuff on an undocumented
  stat is a STOP, not a magnitude to supply.
- `DATABASE.md` §1 note item 1: `MaxHP`, `ATK`, `DEF`, and `EnrageThreshold` are
  **not** `BossDefinition` columns — they are Domain content. A decision about
  them is required, but it must not be recorded as a schema change.
- The three existing Boss definitions, the three provisioned rows, and
  `TASK-170` are immutable records. They are cited as evidence only and must not
  be edited (`TASK_LIFECYCLE.md` §3).

---

## Stop Conditions

Universal stop conditions (`AGENTS.md` §20, `.ai/README.md` §13) always apply.
Task-specific conditions:

- **STOP** if `BOSS_RULES.md` does not define enough information to construct
  the decision slots — report the exact missing contract rather than inventing a
  slot.
- **STOP** if a required Boss field has conflicting authoritative definitions —
  report both sides by file + section per `AGENTS.md` §4.
- **STOP** if a proposed Product Owner answer requires a new gameplay mechanic:
  a new trigger category outside `BOSS_RULES.md` §3 item 2 / `PASSIVE_RULES.md`
  §3; a new status effect; a new stat or `TargetStat`; a new damage type; a new
  representation; a new Boss State; or a new event. Report the exact missing
  contract and do not solve it inside TASK-171.
- **STOP** if a new Element is proposed — `ELEMENT_RULES.md` §1 fixes exactly
  five, and §7 defers dual-element entities.
- **STOP** if a new `EffectDefinition` field is required — that issue is closed
  by TASK-167/TASK-168 and confirmed closed by TASK-170 GAP-B.
- **STOP** if a new database schema field is required — `DATABASE.md` §1 note
  item 1 states no column may be added unless an existing authoritative document
  explicitly requires it.
- **STOP** if a new SignalR event is required — `BOSS_RULES.md` §7 fixes the
  Boss event set, and no `BossStateChanged` event exists.
- **STOP** if a new architecture decision is required (`AGENTS.md` §18) — report
  ADR impact instead of deciding.
- **STOP** if an existing task is found to already own either decision — name it
  and do not duplicate it.
- **STOP** if executing this task would require modifying any file other than
  `tasks/backlog/TASK-171-*.md`.

**Stop-condition status at creation: NOT TRIGGERED.** The decision slots were
constructible from `BOSS_RULES.md` §1/§3/§4/§5/§6 alone; no authoritative
conflict was found; no existing task owns either decision; and no architecture,
Element, or schema decision is required to *present* the slots. The two answers
themselves remain the Product Owner's to supply, and their absence is a pending
input, not a fired stop condition.

**Stop-condition status at final recording: NOT TRIGGERED.** Three rounds of
supplied decisions each fired a STOP and were reported rather than recorded —
`Initial State = ACTIVE` (not a member of `BOSS_RULES.md` §1's closed State
enum), `Kim Lôi Vương EnrageThreshold = null` (no `null` convention exists for
§6.1's mandatory per-Boss threshold), and `BossDefinitionId` equal to `BossId`
(`DATABASE.md` §1 note item 2 requires the two be distinct). All three were
resolved by explicit Product Owner decision in the following round, and no
supplied value was normalized, reinterpreted, or worked around by this task. The
final record contains no outstanding conflict and no unresolved decision slot.

---

## Completion Evidence

<!--
  TO BE COMPLETED BY THE EXECUTING AGENT after reaching DONE.
  Keep concise and factual.
-->

### Changed Files
- `tasks/backlog/TASK-171-collect-product-owner-decisions-two-remaining-mvp-bosses.md`
  — this decision-input task. The Product Owner's full Boss #4 and Boss #5
  specifications, plus the final round of identity / Enrage / Initial State /
  charge / cooldown decisions, were recorded into the `B-1` and `B-2` slots,
  with per-item coverage resolution, contract-expressibility findings, the
  distinctness validation, and the cross-document classification. All three
  previously reported conflicts were resolved by explicit Product Owner
  decision. No other file was touched.

### Validation Results
- Decision coverage verification (`D-1` … `D-16`) — **COMPLETE.** Every required
  decision slot is explicitly supplied or explicitly marked NOT APPLICABLE under
  the existing contract. No slot is inferred.
- Contract validation — **PASS. NO CONFLICT REMAINS, NO STOP.**
  - Elements: Thổ and Kim are within `ELEMENT_RULES.md` §1's Five; §1.2's
    one-per-Boss rule holds; all five Elements are distinctly assigned.
  - `Initial State = Idle` is a member of `BOSS_RULES.md` §1's closed State enum
    and the value §6.1 gives every existing Boss. The earlier `ACTIVE` supply
    was reported as CONFLICT 2 and resolved to `Idle`.
  - `EnrageThreshold` 1500 / 2100 are per-Boss §6.1 values driving §5 item 4's
    permanent transition. The earlier `null` supply for Boss #5 was reported as
    CONFLICT 1 and resolved to 2100. Both Enrage thresholds remain independent
    fields, not merged with either Passive trigger.
  - `BossDefinitionId` uses `DATABASE.md` §1 note item 2's
    `boss-def-<ascii-kebab-case-name>` form and is distinct from `BossId`. The
    earlier BossId-equal supply was reported as CONFLICT 3 and resolved.
  - `BossId` follows `BOSS_RULES.md` §6.4's stated convention.
  - `PassiveId`/`SkillId`: §6.4 fixes **no** exact Boss value form (its BossId
    convention is explicit, its PassiveId rule is a kebab-case "pattern"
    reference, and its SkillId rule states only the identity's role;
    `PASSIVE_RULES.md` §8 scopes the `passive-` prefix to **Pet passives only**).
    `PassiveId` is an opaque `record struct` whose contract states "there is no
    id format, scheme, or validation rule in any document". The supplied forms
    are therefore recordable and were recorded verbatim, not normalized.
  - Trigger categories `Boss HP` and `Combo` are within `BOSS_RULES.md` §3
    item 2's closed set.
  - Passive effect: the Boss-side +20% ATK modifier is the existing
    `COMBAT_RULES.md` §5.5.1 `TargetStat = "ATK"` `BuffDebuff` shape (§6.2.1
    Rage). No new status type, no non-`"ATK"` `TargetStat`, no
    `BossNextAttackATKModifier`, no `ComboPunishmentModifier`.
  - `Charge Requirement = 5` / `Cooldown = 0`: §6.3 defines the mechanism but
    fixes no per-Boss value (its table is configuration §6.3 states the project
    owner approved). CD 0 needs no special case and adds no timing mechanic.
  - Skills: both are direct damage through the existing Damage Pipeline; no
    secondary and no board effect.
  - No `EffectDefinition` field added; `Element`, `DamageElement`,
    `BurnElement`, `TargetStat`, and `BossNextAttack` were NOT added.
- Distinctness check (`BOSS_RULES.md` §2, §3, §3.2, §6) — **PASS, NO VIOLATION.**
  Five distinct Elements; `Boss HP` and `Combo` differ from each other and from
  the existing three; §2 item 2's Passive/Skill mechanical distinctness holds.
- Cross-document classification (Product Owner §7) — **CORRECT as stated**;
  verified against `BOSS_RULES.md`, `DATABASE.md`, `GAME_STATE.md`,
  `COMBAT_RULES.md`, `ELEMENT_RULES.md`, `GAME_EVENTS.md`, and
  `SIGNALR_PROTOCOL.md`. No document revealed a conflicting consequence.
- File-diff verification that this task file is the only addition and that zero
  other files are modified — **PASS**.

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic (no code changed)
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1) — both Bosses are
      IN-scope content under the "5 Bosses" line; no OUT item touched
- [x] Confirmed no source code changes
- [x] Confirmed no authoritative documentation changes
- [x] Confirmed no database/schema changes and no rows provisioned
- [x] Confirmed no migration changes
- [x] Confirmed no `EffectDefinition` Element field introduced; the TASK-167/
      TASK-168/TASK-170 GAP-B decision was not reopened
- [x] Confirmed no gameplay decision was invented: every value is the Product
      Owner's explicit decision, and the only contract-derived values
      (Passive representation, reset/reapplication semantics) are fixed by an
      authoritative document rather than chosen here
- [x] Confirmed no new Element, trigger category, effect type, status, stat,
      resource, board mechanic, event, Redis structure, or database column
      introduced
- [x] Confirmed no new architecture introduced and no ADR created
- [x] Confirmed no new task created by TASK-171
- [x] Confirmed TASK-170 and all completed tasks unmodified

### Lifecycle Note
- Reconciled per TASK-175: the Product Owner decisions collected by this task
  were fully applied by TASK-172, reconciled in TASK-173, implemented in
  TASK-174, and verified in the final MVP Boss audit.
- `Status: DONE` set and file moved to `tasks/completed/`.

### Remaining Issues (reported, not resolved; none is a conflict)
1. **§6.1 percentage parenthetical.** The downstream `BOSS_RULES.md` §6.1 row
   may render Sơn Thạch Vệ's threshold as "1500 (50%)" for consistency with the
   existing rows' "1500 (30%)". A transcription detail; the stored value is 1500.
2. **Shared +20% ATK magnitude.** Three Bosses' Passives use +20% ATK.
   `BOSS_RULES.md` §2 item 2 governs trigger/mechanism distinctness, which
   holds, so this is informational.
3. **New Boss identity spellings.** The supplied `PassiveId`/`SkillId` forms
   differ from the three existing Bosses' observed spellings. §6.4 states no
   convention, so this blocks nothing; the downstream documentation task may
   wish to confirm the intended long-term Boss identity style before writing
   §6.4's table.
4. **Enrage proportionality.** Kim Lôi Vương's 75% Enrage threshold is
   proportionally earlier than every other MVP Boss (30%/50%). An explicit
   Product Owner choice; reported for awareness only.
