# TASK-166 — Collect the Product Owner's Decisions for the Thanh Xà and Sơn Hùng Signature Skills

<!--
  GEN-TASK EXECUTION MANIFEST — PRODUCT-OWNER DECISION-INPUT TASK
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ and task files by path and section — it does NOT copy
  game rules, formulas, magnitudes, schemas, or contracts.

  THIS TASK DECIDES NOTHING, DOCUMENTS NOTHING, AND IMPLEMENTS NOTHING. Its
  only purpose is to capture the Product Owner's explicit answers to the two
  Signature Skill content questions in §"Decision Options" below, so that the
  downstream documentation-application task can author the authoritative
  contract at its canonical owners (CARD_RULES.md §4.1 and PET_RULES.md §8),
  and so that the deferred Thanh Xà / Sơn Hùng provisioning work can later be
  reconsidered.

  AN AGENT MUST NOT ANSWER THESE QUESTIONS. If no Product Owner answer is
  present, the agent reports the task as awaiting input and stops. Choosing,
  recommending, ranking, or defaulting either Skill — its name, purpose,
  effect, target, magnitude, duration, trigger, cost, or interactions — is the
  single prohibited action of this task.

  PROVENANCE: PET_RULES.md §8 records Thanh Xà's and Sơn Hùng's Signature
  Skills as "(Signature Skill: TBD content)" and defers both Pet rows until
  their Skills are content-defined. CARD_RULES.md §4.1 states the same and
  directs that when authored they "must follow this same structure (Cost +
  Effect, consistent with §4)". The repository audit confirmed these are
  CONTENT DECISION REQUIRED, not an implementation gap. Every prior task that
  reached this boundary STOPPED rather than guessing: TASK-102, TASK-103,
  TASK-105…TASK-110, TASK-113, TASK-115, TASK-153, TASK-159, and TASK-163 each
  record it as deferred / TBD / out of their scope. This task is the smallest
  artifact that can supply the missing decision input.

  BOUNDARY: this file only. Zero files under docs/. Zero files under src/ or
  tests/. No task file is modified. No ADR is created. No new gameplay system,
  resource, cooldown, RNG, status, damage type, event, SignalR method,
  database column, or BattleState structure is introduced.
-->

---

## Metadata

```text
Task ID:           TASK-166
Type:              DOCUMENTATION (TASK_TYPES.md §2 — "Change docs/ content —
                   documentation is the primary output, not code"; workflow
                   documentation/documentation-change.md). The deliverable is a
                   recorded decision artifact. See "Type classification note".
                   This task itself edits no docs/ file; it collects the input
                   the downstream documentation task consumes.
Status:            DONE (lifecycle reconciliation — both `Decision:` slots were
                   filled by the Product Owner and C-1…C-12 are resolved — see
                   §"Coverage resolution". TASK-166's deliverable, the decision
                   record, is complete, and this file's own §"Lifecycle
                   reconciliation" recorded at the time that the move to
                   `completed/` was the Orchestrator's lifecycle act rather
                   than the recording step's. That act is performed now, after
                   every downstream consumer became terminal: TASK-167 applied
                   both decisions to the canonical owners (DONE),
                   TASK-168 provisioned the rows (DONE), and TASK-169
                   reconciled `DATABASE.md` (DONE). Per
                   tasks/TASK_LIFECYCLE.md §1, DONE is the correct terminal
                   state because the task WAS directly executed — a recording
                   task is executed by recording. It is expressly NOT
                   SUPERSEDED. The file is moved to tasks/completed/ per
                   tasks/TASK_LIFECYCLE.md §4; the original status text is
                   preserved verbatim in §"Lifecycle Reconciliation" below.)
Risk:              LOW (input capture only — no authoritative document is
                   edited, no rule is changed, no magnitude is authored, no
                   schema is touched, and no code exists in scope.
                   TASK_TYPES.md §4's DOCUMENTATION baseline is LOW–MEDIUM; it
                   is LOW here because this task writes to no `docs/` file and
                   touches no cross-referenced contract — the contracts it
                   collects decisions FOR remain untouched by it. Risk rises to
                   HIGH only downstream, when the documentation-application task
                   authors a new Card's `EffectDefinition` and re-encodes a
                   provisioned content row.)
Priority:          HIGH (the sole unblocking input for the Thanh Xà / Sơn Hùng
                   content. ROADMAP.md §1 Phase 2 requires "All 5 Pets
                   (including the 2 Signature Skills not yet content-defined —
                   see PET_RULES.md §8 note)", and MVP_SCOPE.md §1 lists
                   "5 Pets (Thanh Xà, Xích Lang, Sơn Hùng, Bạch Hổ, Huyền Quy)"
                   and "5 Pet Skill Cards (one per Pet)" as MVP IN. Two of the
                   five are unbuildable until this decision exists.)
Primary Agent:     orchestrator (task-lifecycle / requester coordination — this
                   task records requester input; TASK_TYPES.md §2 DOCUMENTATION
                   names the Review Agent, and no domain agent may author these
                   values. The orchestrator's own contract permits "Task
                   classification artifacts only" and forbids it to "make game
                   design decisions" — .ai/agents/orchestrator.md §Scope /
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
Dependencies:      TASK-082 (DONE — recorded the provisioned/deferred row set:
                     only Pets whose Signature Skill is content-defined may be
                     provisioned; Thanh Xà and Sơn Hùng deferred. IMMUTABLE;
                     read-only),
                   TASK-085 (DONE — provisioned the 6 content-defined rows and
                     explicitly did NOT insert the 2 deferred Pet rows or the 2
                     deferred Skill Cards. IMMUTABLE; read-only),
                   TASK-110 (DONE — authored the three existing Pet Skill Card
                     magnitudes; the §4.1 structure this decision must follow.
                     IMMUTABLE; read-only),
                   TASK-111 / TASK-112 (DONE — the structured multi-effect
                     `EffectDefinition` contract and the `effectType`/`valueType`
                     closed sets any new Card element must use. IMMUTABLE;
                     read-only),
                   TASK-084 (DONE — MVP starter ownership; excludes the two
                     deferred Pets. IMMUTABLE; read-only),
                   TASK-163 (DONE — most recent task to record this item as
                     deferred and out of its scope. IMMUTABLE; read-only)
Blocks:            (1) The authoritative-documentation task that applies the
                   recorded decision at its canonical owners (CARD_RULES.md
                   §4.1 Pet Skill examples; PET_RULES.md §8 provisioned/deferred
                   row set) — it cannot be created until this decision exists;
                   (2) the Thanh Xà / Sơn Hùng `CardDefinition` and
                   `PetDefinition` provisioning work (blocked by the FK:
                   `PetDefinition.SignatureSkillCardId` → `CardDefinition`);
                   (3) ROADMAP.md §1 Phase 2's "All 5 Pets"; (4) the MVP
                   "5 Pet Skill Cards (one per Pet)" line in MVP_SCOPE.md §1.
Estimate:          Simple (4 skills; no code, no tests, no document edits; the
                   answers are this task's INPUT, not its output)
```

**Type classification note.** `DOCUMENTATION`, not `GAMEPLAY-CHANGE` and not
`FEATURE`. `tasks/TASK_TYPES.md` §2 types a `GAMEPLAY-CHANGE` as a rule change
that is "propagat[ed] … through implementation and tests"; this task changes no
rule and touches no implementation — it only records requester decisions into a
task file. `TASK_TYPES.md` §3 selects the type by "*what the deliverable is*";
here the deliverable is a recorded decision artifact, and the only file written
is a `tasks/` file. This mirrors TASK-160 §"Type classification note",
TASK-104 §"Type classification note", and TASK-061 §"Type classification note"
exactly.

**Why not `GAMEPLAY-CHANGE`.** The downstream task that writes these decisions
into `CARD_RULES.md` §4.1 and `PET_RULES.md` §8 **is** a `GAMEPLAY-CHANGE`
(the decision authorizes new content that `docs/` does not currently contain,
per `development/gameplay-change.md` §2's second branch and §3). That
classification belongs to the downstream task, not to this input-capture task.
This task is the DECISION-INPUT half; the documentation-application half is a
separate task.

**This task is NOT a substitute for the downstream documentation task.** It
records decisions; it does not write them into `docs/`. Transcribing the
recorded answers into the authoritative documents — and only through their
canonical-owner sections — remains the downstream task's deliverable, per
`documentation/documentation-change.md` §3 (canonical owner).

---

## Objective

Obtain and record the **Product Owner's explicit decisions** on the two
unresolved Signature Skills — **Thanh Xà's** and **Sơn Hùng's** — so that the
downstream documentation-application task can author them at their canonical
owners (`CARD_RULES.md` §4.1 and `PET_RULES.md` §8) as an implementation-ready
content contract, and so that the deferred Thanh Xà / Sơn Hùng provisioning
work can later be reconsidered without an agent guessing a Skill name, effect,
target, magnitude, duration, trigger, or cost.

This task **collects and records only**. It does not choose, recommend, rank,
default, infer, design, or implement any Skill, and it edits no authoritative
document.

---

## Why This Task Is Required

The MVP Pet content is incomplete in exactly one place, and it is a **content
decision**, not an engineering effort. Four of the five Pets are done: Xích
Lang, Bạch Hổ, and Huyền Quy have authored Signature Skills and provisioned
rows, and Thanh Xà / Sơn Hùng have documented Elements and Passives but **no
Skill content at all**.

```text
WHAT EXISTS (not in question):
  PET_RULES.md §8   — Thanh Xà (Mộc) and Sơn Hùng (Thổ) are documented Pets
                      with documented Elements, Passives, and Passive
                      thresholds. Only their Signature Skills are TBD.
  PASSIVE_RULES.md §8 — both Passives are authored (Match-based, documented
                      thresholds and effect summaries). Not in question here.
  CARD_RULES.md §4  — the Pet Skill Card structure these Skills must follow
                      (one per Pet, only while that Pet is active, generally
                      costing more Power than a Basic Card).

WHAT IS MISSING (the decision this task requests):
  CARD_RULES.md §4.1 — the Thanh Xà and Sơn Hùng Skills are not
                       content-defined. §4.1 names them as unauthored and
                       states that when authored they "must follow this same
                       structure (Cost + Effect, consistent with §4)".
  PET_RULES.md §8    — both rows read "(Signature Skill: TBD content)" and are
                       DEFERRED for provisioning until their Skills are
                       content-defined.
```

**Why the deferral is structurally enforced, not merely administrative.**
`PetDefinition.SignatureSkillCardId` is a required foreign key to
`CardDefinition` (`DATABASE.md` §1/§2). Because the two Skill Cards do not
exist, the two `PetDefinition` rows **cannot** be created — `DATABASE.md` §5
item 4 forbids a placeholder row, an invented Skill Card, or an invented value.
So the missing content blocks the Pet rows by construction, not by choice. This
was verified by TASK-085 (which provisioned only the 6 authorable rows and
inserted neither deferred Pet) and recorded as the governing rule by TASK-082
decision A.

**Why this is a decision and not a derivable fact.** A Signature Skill is
content: its name, role, effect, target, magnitude, duration, trigger, cost, and
interactions are creative/balance choices that no authoritative document states.
Every candidate source was checked and each is either silent or affirmative that
the value must not be invented:

```text
Checked — and each is NOT a source for these two Skills:

The Pet's NAME or ELEMENT      No document derives a Skill from the Pet's
                               name, Element, or theme. BOSS_RULES.md §6.4's
                               identity rules and PET_RULES.md §7 require a
                               Skill to be a DISTINCT authored thing; they do
                               not generate one.

The Pet's existing PASSIVE     PET_RULES.md §7 requires Element + Passive +
                               Signature Skill together to form identity — the
                               Skill is a SEPARATE component, not a
                               restatement or scaling of the Passive. Sơn
                               Hùng's Passive grants temporary Defense; that
                               says nothing about its Skill.

The three EXISTING Pet Skills  CARD_RULES.md §4.1 documents three Skills and
                               does not extend them to these two Pets. Copying
                               Inferno / Tidal Barrier / Iron Fang, or a
                               variant of one, would be invention and would
                               also collide with PET_RULES.md §3 item 1's
                               explicit ban on "power creep clones".

Adjacent implementation        No code path defines either Skill. The
convenience                   provisioning code deliberately rejects
                               placeholder values (DATABASE.md §5 item 4).

Design intuition / other games  AGENTS.md §7 and §23 forbid it.
```

**No prior decision exists anywhere.** A repository-wide search of `docs/`,
`tasks/`, and `.ai/` found only *deferrals*: TASK-102, TASK-103, TASK-105,
TASK-106, TASK-107, TASK-108, TASK-109, TASK-110, TASK-113, TASK-115, TASK-153,
TASK-159, and TASK-163 each record "Thanh Xà / Sơn Hùng Signature Skill content"
as TBD, deferred, or explicitly outside their scope. TASK-108 and TASK-110 even
carry explicit STOP conditions against authoring it. **The decision has never
been made.**

Per `AGENTS.md` §7 and §20 ("Missing rule"), `AGENTS.md` §23, and
`.ai/README.md` §8 ("an agent … is not a Product Owner and does not decide game
design"), the only correct action is to STOP and request the decision. This is
that request.

---

## Authoritative References

<!-- Cited by path and section. No rule, magnitude, formula, or schema is copied. -->

**The unresolved content (read first):**

- `docs/01-game-design/PET_RULES.md` **§8** — the MVP Pet table. Thanh Xà
  (Mộc) and Sơn Hùng (Thổ) both read `(Signature Skill: TBD content)`. The
  section's closing "**Provisioned vs. deferred row set**" paragraph names
  exactly which Pets may be provisioned and records that the Thanh Xà and Sơn
  Hùng rows are **deferred** until their Signature Skills are content-defined,
  because their `SignatureSkillCardId` targets do not exist and the FK is
  required. **This is the D-1/D-2 blocking statement.**
- `docs/01-game-design/CARD_RULES.md` **§4.1** — the Pet Skill content home.
  Documents the three authored Skills and then states that the Thanh Xà and Sơn
  Hùng Signature Skills "are not yet content-defined; when authored they must
  follow this same structure (Cost + Effect, consistent with §4)". **This
  sentence is the structure any answer must satisfy, and the canonical owner
  section the downstream task edits.**

**The structure and vocabulary any answer must use:**

- `docs/01-game-design/CARD_RULES.md` **§4 items 1–4** — one Signature Skill per
  Pet, expressed as one Pet Skill Card; available only while that Pet is active
  (`PET_RULES.md` §2 item 3); Pet Skill Cards generally cost more Power than
  Basic Cards (the "spend now vs. save for Skill" tension, `GDD.md` §10); a
  Skill that deals damage goes through the full Damage Pipeline including the
  Element Modifier.
- `docs/01-game-design/CARD_RULES.md` **§1** (Card categories; the loadout is
  3 Basic + 1 Pet Skill; `LoadoutCopyLimit` = 1 for every `CardDefinition` this
  document defines), **§3** (Card resolution: cost validation against
  `EffectiveCardCost`, deduction from `PetState.Power`), **§6** (the `CardCast`
  and `PetSkillCast` events)
- `docs/02-technical/DATABASE.md` **§1 "Card `EffectDefinition` contract"** —
  the structured, ARRAY-valued effect rule every Card must store. Its
  `effectType` closed set is `Heal` | `Shield` | `Power` | `Damage` | `Burn` |
  `Crit`; its `valueType` set is `Flat` | `PercentMaxHp` | `PercentagePoints` |
  `Undetermined`; per-effect extra members (`duration` on `Burn`, `scope` on
  `Crit`) exist. `Undetermined` **records an unauthored magnitude and is why no
  value has to be invented to make such a row representable**. **This is the
  existing vocabulary a selected Skill must be expressible in.**
- `docs/02-technical/DATABASE.md` **§3 item 1** — the stored
  `EffectDefinition` summary row and its column constraints

**The gameplay systems a Skill may interact with (existing vocabulary only):**

- `docs/01-game-design/COMBAT_RULES.md` **§3** — the Damage Pipeline, including
  the Element Modifier step and §3.3's Crit modifier sources and NextAttack
  scope
- `docs/01-game-design/COMBAT_RULES.md` **§4** — Heal Resolution and the Shield
  application semantics (refresh-not-stack)
- `docs/01-game-design/COMBAT_RULES.md` **§5.1–§5.3** — Buff/Debuff and Status
  Effect semantics, the two duration models, and the single step-19a
  decrement
- `docs/01-game-design/ELEMENT_RULES.md` **§5** — the Element Modifier a
  damage-dealing Skill passes through; **§6** — the Pet→Element assignment
  (Thanh Xà = Mộc, Sơn Hùng = Thổ)
- `docs/01-game-design/PASSIVE_RULES.md` **§8** — the two Pets' authored
  Passives and thresholds (context for what the Skill must NOT duplicate)
- `docs/01-game-design/GAME_RULES.md` **§17** — the fixed resolution order,
  including step 14 "Resolve Player Effects" (where a Card/Skill cast resolves)
  and step 19a (End Turn duration consumption); **§16** — the canonical event
  list; **§18** — server authority
- `docs/02-technical/GAME_STATE.md` **§2.3** (`PetState`, including `Power`,
  `HP`/`MaxHP`, `StatusEffects[]`, `NextAttackCritModifiers[]`) — the state a
  Skill reads or writes, and its absence conventions; **§5.1.1–§5.1.3** — the
  mutation lifecycles
- `docs/02-technical/GAME_EVENTS.md` **§2** — `CardCast` / `PetSkillCast` and
  the events a Skill's effect already produces

**Scope, architecture, and decisions:**

- `docs/00-overview/MVP_SCOPE.md` **§1** ("5 Pets (Thanh Xà, Xích Lang, Sơn
  Hùng, Bạch Hổ, Huyền Quy)"; "5 Pet Skill Cards (one per Pet)"; "Combat: HP,
  ATK, DEF, Power, Crit, Status Effects, Damage, Element interaction"), **§2**,
  **§4**
- `docs/00-overview/ROADMAP.md` **§1 Phase 2** ("All 5 Pets (including the 2
  Signature Skills not yet content-defined — see `PET_RULES.md` §8 note)")
- `docs/00-overview/GDD.md` **§10** (the spend-now-vs-save-for-Skill tension),
  **§14** (Pet identity)
- `docs/02-technical/ARCHITECTURE.md` **§5** (anti-overengineering)
- `docs/03-decisions/ADR/ADR-001-server-authoritative-battle.md`
- `docs/03-decisions/README.md` — ADR index; §3 (an ADR never overrides the
  what/how owned by `docs/02-technical/`)

**Governing contract and precedent:**

- `AGENTS.md` **§4** (never silently resolve a conflict), **§7** (invent no
  rule — the governing section here), **§8** (MVP protection), **§9**
  (anti-overengineering), **§10** (server authority), **§12** (domain
  boundaries), **§16** (report, do not fix inline), **§17** (documentation
  change rule), **§18** (architecture change rule), **§20** (stop conditions),
  **§23** (final principle)
- `.ai/README.md` **§6** (source-of-truth rule), **§8** (an agent is not a
  Product Owner), **§13** (stop conditions and the stop-report format)
- `.ai/agents/orchestrator.md` §Scope / §Decision Authority;
  `.ai/agents/gameplay.md` §Decision Authority (Pet/Card domain)
- `.ai/workflow/development/gameplay-change.md` **§2** (the branch that fires
  when a mechanic does not exist in `docs/`) and **§3** (the design-change
  branch the DOWNSTREAM task follows)
- `tasks/README.md` **§9** (no business-rule duplication), **§12** (skill
  budget)
- `tasks/completed/TASK-160-collect-product-owner-decisions-pet-statuseffects-and-boss-live-hp-projection.md`
  — the decision-collection precedent this task follows
- `tasks/completed/TASK-104-collect-cardcast-wire-and-shield-contract-decisions.md`
  — the decision-collection precedent this task follows
- `tasks/completed/TASK-110-author-pet-skill-card-effect-magnitudes.md` — the
  precedent for authoring Pet Skill magnitudes from Product Owner decisions
- `tasks/completed/TASK-061-record-product-owner-pet-xp-decisions.md` — the
  Pet-content decision-collection precedent
- `tasks/completed/TASK-082-resolve-pet-card-relic-content-provisioning-contract.md`
  — decision A: the provisioned/deferred row set this task unblocks
- `tasks/completed/TASK-085-provision-mvp-pet-card-relic-content-definitions.md`
  — the provisioning task that inserted neither deferred row, and said why

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

THE STRUCTURAL CONSEQUENCE — DATABASE.md §1/§2, §5 item 4
  PetDefinition.SignatureSkillCardId is a required FK → CardDefinition.
  The two Pet rows therefore cannot exist before the two Skill Cards do.

THE VOCABULARY A SKILL MUST BE EXPRESSIBLE IN — DATABASE.md §1
  CardDefinition.EffectDefinition is a structured ARRAY of
  effectType/valueType/value triples plus per-effect extra members.
  effectType ∈ {Heal, Shield, Power, Damage, Burn, Crit}   (closed set)
  valueType  ∈ {Flat, PercentMaxHp, PercentagePoints, Undetermined}
  extra members: duration (Burn), scope (Crit)
  Undetermined is valid and records an unauthored magnitude.

  The three authored Skills use only this vocabulary today. No other effect
  identity, value interpretation, damage type, status, resource, or trigger
  form is defined for a Pet Skill Card anywhere in docs/.
```

---

## Exact Ambiguity

Two content items are unresolved. Neither is answered anywhere in `docs/`,
`tasks/`, or `.ai/`.

```text
D-1  THANH XÀ'S SIGNATURE SKILL
     Thanh Xà is a documented MVP Pet (Mộc) with a documented Element, a
     documented Passive, and a documented Passive threshold
     (PET_RULES.md §8, PASSIVE_RULES.md §8). Its Signature Skill field reads
     "TBD content", and CARD_RULES.md §4.1 states it is not content-defined.

     Its PetDefinition row and its Pet Skill CardDefinition row do not exist
     and cannot be created, because SignatureSkillCardId is a required FK.

     UNRESOLVED: what IS Thanh Xà's Signature Skill — its name, its purpose,
     what it does to whom, in what amount, with what lifetime and trigger, at
     what Power cost, and how it interacts with the existing damage / crit /
     status / passive systems?

D-2  SƠN HÙNG'S SIGNATURE SKILL
     The identical gap for Sơn Hùng (Thổ), whose Passive grants temporary
     Defense.

     UNRESOLVED: what IS Sơn Hùng's Signature Skill, on every field above?

WHY THESE ARE NOT DERIVABLE
  A Signature Skill is authored content. No document states either Skill; no
  document derives a Skill from a Pet's name, Element, Passive, or Tier; and
  no document authorizes copying or adapting the three existing Skills.
  PET_RULES.md §7 requires the Skill to be a DISTINCT component of Pet
  identity, and PET_RULES.md §3 item 1 bans "power creep clones" — so
  "existing Skill with different numbers" is expressly excluded, not merely
  unauthored. AGENTS.md §7, §20, and §23 make inventing it a STOP, and
  .ai/README.md §8 states an agent is not a Product Owner.
```

**A note on completeness, not on content.** The two Skills are independent
content decisions. They must be answered **separately** — answering one
establishes nothing about the other, and neither may be treated as a default,
template, or precedent for the other. (`PET_RULES.md` §3 item 2 allows Tier
variants to share a strategic identity; that is a multi-Tier framework rule
about the *same* Pet, and it says nothing about two different MVP Pets.)

---

## Decision Options

> **PRODUCT OWNER INPUT.** The two items below are gameplay content decisions.
> Only the Product Owner may supply them. An agent executing this task must not
> fill in any `Decision:` field.
>
> Answer each item **independently**. Do not treat either item's answer as a
> default for the other. Each item states the coverage its answer must satisfy;
> "unspecified" counts as unanswered.
>
> **No answer is pre-judged anywhere in this task.** The options below are
> candidate shapes drawn from the documents' own structure — not a ranking, and
> not a recommendation. The Product Owner may select one, combine them, define
> another, or supply free-form content, provided every coverage item is
> answered.
>
> **Every option requires the Product Owner to supply the content itself.**
> This task deliberately proposes **no** Skill name, effect, magnitude,
> duration, or cost for either Pet — proposing those would be exactly the
> prohibited act. The option lists exist only to show which *existing* rule
> structures a Skill can be expressed in, so that a supplied answer can be
> recorded in a form the downstream documentation task can apply.
>
> **Do not design the stored row here.** Per this task's boundary, no
> `EffectDefinition` JSON, no `CardDefinitionId` spelling, and no database row
> is authored in this file. The downstream documentation-application task
> derives those from the recorded answer.

### Coverage checklist (applies to BOTH D-1 and D-2)

The supplied answer for **each** Pet must explicitly determine the items below
that its selected Skill actually uses. Items marked **conditional** are
required only if the Skill has that property — a Skill that does not apply a
temporary effect does not need a duration, and a Skill that deals no damage does
not need a damage type. Do not force a mechanic to exist in order to fill a
field, and do not leave a field of a mechanic that IS used implicit.

```text
 1. Skill name
    The Skill's display name, as it will appear to the player. (Each of the
    three authored Skills has a name: Inferno, Tidal Barrier, Iron Fang.)

 2. Skill purpose / role
    The strategic role the Skill plays for that Pet — the "spend now vs. save
    for Skill" decision it is meant to reward (GDD.md §10, CARD_RULES.md §3).

 3. Effect — what the Skill does
    The primary effect. [CONDITIONAL] A secondary effect, if any.
    PET_RULES.md §7 requires the result to be a DISTINCT Pet identity, so the
    answer must be distinguishable from the Pet's existing Passive
    (PASSIVE_RULES.md §8) and from the three authored Skills (CARD_RULES.md
    §4.1).

 4. Target
    Which entity the effect applies to: the active Pet itself, the Boss, both,
    or another explicitly named target. Basic Card effects target the active
    Pet (CARD_RULES.md §2); a damage-dealing Skill targets the Boss. State it
    explicitly rather than implying it.

 5. Damage behaviour                                    [CONDITIONAL]
    If the Skill deals damage:
      - the base damage value and whether it is Flat or a percentage
        (DATABASE.md §1's valueType set)
      - the damage type / Element the Skill carries, and whether it is the
        Pet's own Element (ELEMENT_RULES.md §6) — a damage-dealing Skill goes
        through the full Damage Pipeline including the Element Modifier
        (CARD_RULES.md §4 item 4, COMBAT_RULES.md §3)

 6. Healing behaviour                                   [CONDITIONAL]
    If the Skill heals: the amount and whether it is Flat or PercentMaxHp
    (CARD_RULES.md §2's two interpretations; COMBAT_RULES.md §4's Heal
    Resolution and its clamp).

 7. Shield behaviour                                    [CONDITIONAL]
    If the Skill grants Shield: the amount, flat or %MaxHP, and confirmation
    that the existing refresh-not-stack semantics apply
    (COMBAT_RULES.md §4).

 8. Power / stat modification                           [CONDITIONAL]
    If the Skill modifies a stat: which stat (ATK, DEF, Crit, …), by how much,
    and whether it is Flat or a percentage. Note which stats are documented as
    modifiable at all before selecting this — an undocumented stat is a
    stop condition, not a field to fill.

 9. Crit participation                                  [CONDITIONAL]
    If the Skill's damage can crit, or the Skill grants a Crit modifier: the
    increase in percentage points and which damage instances it applies to.
    COMBAT_RULES.md §3.3 items 7–10 own the NextAttack scope and consumption
    rule; state whether the Skill uses that existing scope or another
    documented one. (Iron Fang is the precedent.)

10. Status / Buff / Debuff                              [CONDITIONAL]
    If the Skill applies a status effect: which existing documented status
    (Burn is the only one a Pet Skill Card uses today — CARD_RULES.md §4.1),
    its magnitude, and its duration in the authoritative Turn unit. Note that
    BOSS_RULES.md §6.2.2 and COMBAT_RULES.md §5.4.5/§5.5.3 each close the
    non-"ATK" stat case with "would require its own recorded decision" — a
    status outside the documented set is a stop condition.

11. Duration / lifetime
    Immediate, Turn-based, trigger-based, or another explicitly defined
    lifetime. State which of the two documented Status Effect duration models
    applies if one is used (COMBAT_RULES.md §5.2/§5.3), and state that the
    single step-19a decrement is unchanged.

12. Trigger / timing
    When the Skill takes effect, when its damage occurs, when any secondary
    effect occurs, and whether any part of it happens immediately or at a later
    phase. A Pet Skill resolves when the player casts it, at GAME_RULES.md §17
    step 14 ("Resolve Player Effects") — confirm or state otherwise. State the
    ordering relative to the Pet's Passive if both can fire in one Swap.

13. Cost / resource behaviour
    The Power cost, expressed in the existing `Power` resource
    (CARD_RULES.md §3; Pet Skill Cards generally cost more than Basic Cards).
    No new resource may be introduced. If the Skill has a cost interaction
    (e.g. a `CardCostModifier`), state it and name the existing documented
    mechanism.

14. Cooldown / usage constraints                        [CONDITIONAL]
    Only if the existing rules support such a concept. `CARD_RULES.md` §4
    defines no cooldown for a Pet Skill Card, and no document defines a
    cooldown anywhere. If the answer requires one, see the Stop Conditions —
    that is a new gameplay system, not a Skill field.

15. Interaction rules                                  [CONDITIONAL]
    Where necessary and only where necessary: Crit participation, Burn/status
    interaction, Passive interaction, Relic interaction, and the Damage
    Pipeline interaction. Do not expand into unrelated combat design.

16. Distinctness confirmation
    An explicit statement that the resulting Skill is distinct from that Pet's
    own Passive and from the three authored Skills, satisfying PET_RULES.md §7
    and §3 item 1 (no "power creep clone").

17. Cross-document consequences
    Which authoritative sections the decision requires the downstream task to
    change — at minimum CARD_RULES.md §4.1 and PET_RULES.md §8. State whether
    any existing contract ceases to hold, and whether the decision requires
    anything beyond those two sections.
```

### D-1 — Thanh Xà's Signature Skill

**Question.** What is Thanh Xà's Signature Skill, on every coverage item above?

**Current evidence.**

- `PET_RULES.md` §8 lists Thanh Xà as Mộc with the Passive "Every 7 Matches →
  Restore 8% HP" and the Signature Skill field "(Signature Skill: TBD content)",
  and defers the row.
- `CARD_RULES.md` §4.1 states the Skill "is not yet content-defined" and that
  when authored it "must follow this same structure (Cost + Effect, consistent
  with §4)".
- `PASSIVE_RULES.md` §8 confirms the Passive and threshold are authored; the
  Skill is a separate, unauthored component.
- `ELEMENT_RULES.md` §6 / `GAME_RULES.md` §9 assign Thanh Xà the **Mộc**
  Element. If the Skill deals damage, that Element participates in the Damage
  Pipeline's Element Modifier — but the assignment does not define the Skill.
- No `CardDefinition` or `PetDefinition` row for Thanh Xà exists
  (`DATABASE.md` §1's FK makes this structural).

**Options.** *(Shapes only. Every one requires the Product Owner to supply the
actual content — name, effect, target, magnitudes, lifetime, cost. None is
ranked.)*

```text
D-1A  A DAMAGE-ORIENTED Skill, expressed in the existing `Damage` (and
      optionally `Burn` or `Crit`) vocabulary, following the Inferno /
      Iron Fang structure in CARD_RULES.md §4.1 and DATABASE.md §1's
      effectType set. The Product Owner supplies the name, base value,
      Element, any Burn magnitude/duration or Crit increase/scope, and cost.

D-1B  A SUPPORT-ORIENTED Skill, expressed in the existing `Heal`, `Shield`, or
      `Power` vocabulary, following the Tidal Barrier structure. The Product
      Owner supplies the name, the healed/shielded target, the amount and its
      valueType (Flat or PercentMaxHp), and cost.

D-1C  A Skill that COMBINES effects (CARD_RULES.md §4.1's Skills and
      DATABASE.md §1's ARRAY shape both permit more than one element per
      Card). The Product Owner supplies every element and the cost.

D-1D  A Skill whose effect is NOT expressible in the current vocabulary
      (DATABASE.md §1's closed `effectType` / `valueType` sets), or which
      requires a documented-but-not-yet-used stat/status. See the Stop
      Conditions — this is a new-mechanic decision, not a Skill content
      answer, and it must be flagged rather than recorded as settled.

D-1E  Another explicitly stated Skill this list does not anticipate. Fully
      acceptable: supply the coverage checklist items directly.
```

**Coverage the answer must provide.** All applicable items 1–17 of the coverage
checklist above, for Thanh Xà specifically. Items 5–10 and 14 are conditional on
the Skill actually using that mechanic.

```text
Decision:
Skill Name:
Venomous Bloom

Purpose / Role:
Sustained damage / Damage-over-Time oriented Signature Skill.

Power Cost:
80

Damage:
80 Flat → Boss

Damage Element:
Mộc

Burn:
25 Flat → Boss
Duration: 2 Turns

Burn Element:
Hỏa (Fire) — the existing Burn Element. The Mộc Element applies only to the
direct Damage effect.

Skill Lifetime:
Immediate resolution on PetSkillCast. The Skill itself does not remain as a
pending/active state after resolution.

Trigger / Timing:
Resolve immediately when the PetSkillCast is successfully executed.

Interaction Rules:
Damage and Burn use the existing server-authoritative Damage Pipeline.
Burn follows the existing Burn duration/tick rules.
No new gameplay mechanic, event, resource, or pending state is introduced.

Distinctness:
Confirmed distinct from existing Skills and from Thanh Xà's Passive.
Its defining role is sustained/DoT-oriented damage.

Rationale (optional):
<NOT SUPPLIED BY PRODUCT OWNER>
```

**Recorded verbatim** (§"Recording Discipline"). This answer completes the two
items previously recorded here as unresolved:

```text
RESOLVED — D-1 damage Element (coverage item 5; C-4/C-9/C-10)
  The Product Owner states "Damage Element: Mộc". CARD_RULES.md §4 item 4's
  requirement that a damage-dealing Pet Skill Card carry an Element is
  therefore satisfied by an EXPLICIT statement (the §4 item 4 "or explicitly
  stated" branch), not by inheritance.

RESOLVED — D-1 coverage items 2, 11, 12, 15, 16
  Purpose/role (item 2: "Sustained damage / Damage-over-Time oriented"),
  lifetime (item 11: immediate, no pending state), trigger/timing (item 12:
  immediate on successful PetSkillCast), interaction rules (item 15: existing
  Damage Pipeline and existing Burn duration/tick rules), and distinctness
  (item 16: confirmed distinct from existing Skills and from Thanh Xà's
  Passive) are all supplied.
```

**RESOLVED — the Burn effect's own Element (ELEMENT_RULES.md §5).**

```text
Product Owner decision (recorded verbatim):
  "Venomous Bloom's Burn tick keeps the existing Hỏa (Fire) Element. The Mộc
   Element applies only to the direct Damage effect."

This resolves the wording point previously recorded here as OPEN. The two
effects carry DIFFERENT Elements:
  Damage effect — Mộc (Thanh Xà's own Element)
  Burn effect   — Hỏa (the existing Burn Element)

This agrees with the existing rule rather than changing it: ELEMENT_RULES.md §5
gives a Status Effect damage-over-time tick its own Element and applies the
Element Modifier "using the Effect's Element", and COMBAT_RULES.md §5.1 states
Burn's Element is Hỏa for Element Modifier purposes. Both effects nevertheless
traverse the same Damage Pipeline (COMBAT_RULES.md §3), each using its own
Element at the Element Modifier step — ELEMENT_RULES.md §5 already enumerates
both "Pet Signature Skill damage" and "Status Effect damage-over-time ticks
(e.g. Burn)" as applying instances.

No rule is changed, no new Element is introduced, and no new mechanic is
required. C-4, C-9, and C-10 are fully resolved for D-1.
```

### D-2 — Sơn Hùng's Signature Skill

**Question.** What is Sơn Hùng's Signature Skill, on every coverage item above?

**Current evidence.**

- `PET_RULES.md` §8 lists Sơn Hùng as Thổ with the Passive "Every 5 Matches →
  Temp Defense" and the Signature Skill field "(Signature Skill: TBD content)",
  and defers the row.
- `CARD_RULES.md` §4.1 states the same as for Thanh Xà.
- `PASSIVE_RULES.md` §8: the Passive ("Gain temporary Defense") and its
  5-Match threshold are authored and say nothing about the Skill. **The
  existing Passive must not be treated as a template for the Skill** —
  `PET_RULES.md` §7 requires the two to be distinct components of identity.
- `ELEMENT_RULES.md` §6 / `GAME_RULES.md` §9 assign Sơn Hùng the **Thổ**
  Element.
- `PASSIVE_RULES.md` §8's closing paragraph notes that Sơn Hùng's `PassiveId`
  will be derived by the documented rule once its row is provisioned — which is
  downstream of this decision, not part of it.
- No `CardDefinition` or `PetDefinition` row for Sơn Hùng exists.

**Options.** *(Identical shape list to D-1; separate answer required. None is
ranked, and D-1's answer is not a default for this one.)*

```text
D-2A  A DAMAGE-ORIENTED Skill — see D-1A's structure.
D-2B  A SUPPORT-ORIENTED Skill — see D-1B's structure.
D-2C  A Skill that COMBINES effects — see D-1C's structure.
D-2D  A Skill not expressible in the current vocabulary — see D-1D, including
      its stop condition.
D-2E  Another explicitly stated Skill this list does not anticipate.
```

**Coverage the answer must provide.** All applicable items 1–17 of the coverage
checklist above, for Sơn Hùng specifically.

```text
Decision:
Skill Name:
Earthshaker

Purpose / Role:
Burst damage Signature Skill.

Power Cost:
100

Damage:
150 Flat → Boss

Damage Element:
Thổ

Skill Lifetime:
Immediate resolution on PetSkillCast. The Skill itself does not remain as a
pending/active state after resolution.

Trigger / Timing:
Resolve immediately when the PetSkillCast is successfully executed.

Interaction Rules:
Direct damage uses the existing server-authoritative Damage Pipeline.
No Burn, new status effect, new event, new resource, or pending state is
introduced.

Distinctness:
Confirmed distinct from existing Skills and from Sơn Hùng's Passive.
Its defining role is burst/direct damage.

Rationale (optional):
<NOT SUPPLIED BY PRODUCT OWNER>
```

**Recorded verbatim** (§"Recording Discipline"). This answer completes both
items previously recorded here as unresolved:

```text
RESOLVED — D-2 damage Element (coverage item 5; C-4/C-9/C-10)
  The Product Owner states "Damage Element: Thổ". CARD_RULES.md §4 item 4's
  requirement that a damage-dealing Pet Skill Card carry an Element is
  therefore satisfied by an EXPLICIT statement (the §4 item 4 "or explicitly
  stated" branch), not by inheritance.

RESOLVED — D-2 coverage items 2, 11, 12, 15, 16
  Purpose/role (item 2: "Burst damage"), lifetime (item 11: immediate, no
  pending state), trigger/timing (item 12: immediate on successful
  PetSkillCast), interaction rules (item 15: existing Damage Pipeline only, no
  Burn and no new status/event/resource/pending state), and distinctness
  (item 16: confirmed distinct from existing Skills and from Sơn Hùng's
  Passive) are all supplied.
```

**No open wording point.** D-2 carries one effect only (Damage), so the D-1
Burn-Element ambiguity has no counterpart here; "Damage Element: Thổ" fully
determines this Skill's Elemental behaviour. Coverage item 14 (cooldown) is
correctly absent: the answer introduces no cooldown, and `CARD_RULES.md` §4
defines none.

---

## Required Decision Coverage

The recorded decision must explicitly resolve **all** items below. Any item left
unspecified is recorded as unspecified — not inferred, and not defaulted.

```text
C-1  Thanh Xà Signature Skill content
     → answered by the D-1 slot above.

C-2  Sơn Hùng Signature Skill content
     → answered by the D-2 slot above.

C-3  Skill identity (name and purpose/role) for each Pet
     → coverage item 1 and item 2 of the checklist, for each Pet separately.

C-4  Effect, target, and any secondary effect for each Pet
     → coverage items 3 and 4, and items 5–10 where the Skill uses them.

C-5  Magnitudes, expressed in the EXISTING vocabulary
     → Whether each magnitude the Skill uses is Flat, PercentMaxHp, or
       PercentagePoints (DATABASE.md §1's valueType set). If a magnitude is
       genuinely not yet decided but the effect identity IS decided, say so
       explicitly — `Undetermined` is the documented way to record that, and
       it is a valid answer, not an incomplete one.

C-6  Duration / lifetime
     → coverage item 11. If the Skill applies no temporary effect, state that
       it is immediate rather than leaving it blank.

C-7  Trigger / timing
     → coverage item 12.

C-8  Cost / resource behaviour
     → coverage item 13. The Power cost, in the existing `Power` resource.
       No new resource may be introduced.

C-9  Interaction rules, where necessary
     → coverage item 15, plus the applicable parts of items 8, 9, and 10.
       Only where necessary; do not expand into unrelated combat design.

C-10 Existing-vocabulary check
     → Confirmation that each Skill is expressible in DATABASE.md §1's
       existing `effectType` / `valueType` sets and per-effect extra members.
       If either Skill needs an effect identity, value interpretation, damage
       type, status, resource, trigger form, or state member that docs/ does
       not define, record that as a SEPARATE unresolved decision per the Stop
       Conditions — do not record it as part of this answer.

C-11 Distinctness
     → coverage item 16. Each Skill must be distinct from its own Pet's Passive
       and from the three authored Skills (PET_RULES.md §7, §3 item 1).

C-12 Downstream consequences
     → coverage item 17. Which authoritative sections must change (at minimum
       CARD_RULES.md §4.1 and PET_RULES.md §8), and whether anything beyond
       those two is required.
```

### Coverage resolution (recorded after the Product Owner's answers)

```text
C-1  RESOLVED — Thanh Xà's Signature Skill is content-defined: "Venomous Bloom",
     80 Power, Damage 80 Flat → Boss (Mộc), Burn 25 Flat → Boss for 2 Turns.
C-2  RESOLVED — Sơn Hùng's Signature Skill is content-defined: "Earthshaker",
     100 Power, Damage 150 Flat → Boss (Thổ).
C-3  RESOLVED — names ("Venomous Bloom" / "Earthshaker") and purposes
     ("Sustained damage / Damage-over-Time oriented" / "Burst damage") supplied.
C-4  RESOLVED — effects and targets supplied for both Pets; Thanh Xà's
     secondary effect (Burn) supplied with its magnitude and duration.
C-5  RESOLVED — every magnitude is Flat (DATABASE.md §1's valueType set).
     No magnitude is Undetermined and none was invented.
C-6  RESOLVED — both Skills are immediate resolution on PetSkillCast with no
     pending/active state after resolution. Thanh Xà's Burn carries its own
     documented duration (2 Turns), governed by the existing Burn rules.
C-7  RESOLVED — both resolve immediately when the PetSkillCast is successfully
     executed (consistent with GAME_RULES.md §17 step 14).
C-8  RESOLVED — 80 Power (Thanh Xà) / 100 Power (Sơn Hùng), in the existing
     `Power` resource. No new resource, no cost modifier.
C-9  RESOLVED — Thanh Xà: Damage and Burn use the existing server-authoritative
     Damage Pipeline, each with its own Element (Damage = Mộc, Burn = Hỏa);
     Burn follows the existing Burn duration/tick rules.
     Sơn Hùng: direct damage uses the existing Damage Pipeline; no Burn and no
     new status effect. Both: no new gameplay mechanic, event, resource, or
     pending state.
C-10 RESOLVED — both Skills use only DATABASE.md §1's existing closed
     `effectType` set (Damage; Burn) and `valueType` Flat, with Burn's required
     `duration` member. No new effect identity, value interpretation, damage
     type, status, resource, trigger form, or state member is required. The
     damage Element is stated in content (CARD_RULES.md §4 item 4 prose), as the
     three authored Skills already do. The Burn Element (Hỏa) is the existing
     documented Burn Element, not a new one.
C-11 RESOLVED — distinctness explicitly confirmed by the Product Owner for both
     Pets, against both their own Passives and the three authored Skills
     (PET_RULES.md §7, §3 item 1).
C-12 RESOLVED — the downstream documentation-application task must author
     CARD_RULES.md §4.1 (the two Skill entries, Cost + Effect, consistent with
     §4) and update PET_RULES.md §8's provisioned/deferred row set. Nothing
     beyond those two sections is authorized by this decision.
```

```text
No item remains PENDING PRODUCT-OWNER DECISION. The Burn effect's own Element
for Thanh Xà was the last open point and is now decided: the Burn tick keeps the
existing Hỏa Element, and Mộc applies only to the direct Damage effect (see
§"D-1 — Thanh Xà's Signature Skill"). C-1…C-12 are fully resolved and no
unresolved wording choice remains for the downstream task.
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

Derived details:    Any mechanically derived detail (e.g. the card_* /
                    PassiveId / PetDefinitionId spellings the provisioning
                    work will need) is NOT part of this decision and is NOT
                    recorded here as if it were. CARD_RULES.md §4.1 names
                    Skills by display name; the identifier conventions are
                    owned elsewhere and are the downstream task's to apply.

Ownership of the
follow-on edit:     Transcribing the recorded answers into docs/ is the
                    DOWNSTREAM documentation-application task's deliverable,
                    per documentation/documentation-change.md §3 (canonical
                    owner). This task does not do it.
```

---

## Out of Scope

- **Any implementation.** No source file, test file, migration, or configuration
  is added or modified by this task.
- **Any change under `docs/`.** Zero files under `docs/` are created, edited,
  renamed, or deleted. Not `CARD_RULES.md`, not `PET_RULES.md`, not
  `GAME_RULES.md`, not `PASSIVE_RULES.md`, not `ELEMENT_RULES.md`, not
  `COMBAT_RULES.md`.
- **Modifying any task file.** `tasks/completed/` is immutable. The backlog and
  blocked tasks referenced as provenance are read-only context and are not
  edited, moved, re-statused, or re-scoped.
- **Creating another task.** The downstream documentation-application task and
  the subsequent provisioning/implementation tasks are recorded here as
  dependency edges only. Creating them is the Orchestrator's act.
- **Creating an ADR.** None is warranted (see §"Downstream Dependency") and none
  is created.
- **Choosing a Skill.** No name, effect, target, magnitude, duration, cost,
  trigger, or interaction is proposed, ranked, recommended, or defaulted for
  either Pet anywhere in this file. The `Decision:` slots are the Product
  Owner's to fill.
- **Designing the stored row.** No `EffectDefinition` JSON, no
  `CardDefinitionId` / `PassiveId` / `PetDefinitionId` value, and no database row
  is authored here.
- **Authoring Thanh Xà's or Sơn Hùng's Passive, Element, threshold, tier, star,
  level, or stats.** All are already documented and are not in question.
- **The 2 remaining Bosses** (`BOSS_RULES.md` §6 note) and **the remaining
  Relics / Burning Curse trigger** — each is a separate missing-content decision
  with its own scope, and each is explicitly out of scope here.
- **Provisioning the Thanh Xà / Sơn Hùng rows** — blocked on this decision and
  on the downstream documentation task, and not performed here.
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2, and any item not
  listed in §1 (treat unlisted as FUTURE per §4).

---

## Stop Conditions

Universal stop conditions in `AGENTS.md` §20 and `.ai/README.md` §13 apply.

**This task STOPS instead of creating, expanding, or resolving itself if any of
the following holds.** In each case, report the exact decision gap and do not
proceed.

- **An agent is asked to fill in a `Decision:` slot.** **STOP** — that is the
  single prohibited action of this task.
- **The existing authoritative docs already determine either Skill.** If a
  document is found that unambiguously defines Thanh Xà's or Sơn Hùng's
  Signature Skill, this task is unnecessary: report that the documentation
  workflow may proceed without a Product Owner decision, and stop. *(Checked:
  they do not — see §"Why This Task Is Required".)*
- **A proposed Skill requires a new gameplay system.** If either Skill needs a
  cooldown, a new resource, a new RNG behaviour, a new status system, a new
  damage type, a new event, a new SignalR method, a new database column, or a
  new `BattleState` structure, **STOP** per `AGENTS.md` §7 and §18: that is a
  separate architectural/gameplay decision requiring its own task and (where
  architectural) its own ADR. Record it as a separate unresolved decision — do
  not fold it into this answer.
  Specific known boundaries: `CARD_RULES.md` §4 defines **no cooldown** for a
  Pet Skill Card; `DATABASE.md` §1 closes the `effectType` and `valueType` sets;
  `COMBAT_RULES.md` §5.4.5 and §5.5.3 close the non-"ATK" `TargetStat` case with
  "would require its own recorded decision"; `GAME_RULES.md` §16 closes the
  event list.
- **A proposed Skill requires a new `BattleState` field.** **STOP** per
  `AGENTS.md` §18 — `GAME_STATE.md` §0 item 5 forbids a second representation of
  one fact, and adding state is an architecture change.
- **A proposed Skill conflicts with an existing Pet rule.** **STOP** per
  `AGENTS.md` §4 and report both sides. In particular: a Skill that is a
  scaled copy of the Pet's Passive or of an existing Skill conflicts with
  `PET_RULES.md` §7 and §3 item 1.
- **A decision would change an already-frozen gameplay contract.** **STOP** per
  `AGENTS.md` §4. For example, changing the Damage Pipeline, the Heal
  Resolution, the Shield refresh semantics, or the step-19a duration
  consumption to accommodate a Skill is a change to an existing frozen
  contract — a separate decision, not a Skill field.
- **The answer requires inventing a magnitude, name, or value.** **STOP** —
  inventing it is the prohibited act.
- **Existing documentation does not provide enough information to formulate
  grounded options.** Record the item as **"Human gameplay decision required"**
  rather than guessing, and stop short of proposing options for it.
- **Only one Pet's answer is supplied.** Record that answer and leave the other
  `Decision:` slot pending. Do not treat one Pet's Skill as a template, default,
  or precedent for the other.
- **The decision requires a `CARD_RULES.md` §4 structural change** (e.g. a
  second Skill per Pet, or a Skill usable when the Pet is not active), which
  `PET_RULES.md` §2 item 3 and `CARD_RULES.md` §4 items 1–2 currently forbid.
  **STOP** — that is a structural rule change with its own decision.
- If the task exceeds 7 skills or crosses multiple uncoupled architectural
  boundaries: **STOP & decompose**.

---

## Downstream Dependency

The recorded decision is the input to a **documentation-application task**, which
owns the authoritative edits. This task does not perform it.

```text
Thanh Xà / Sơn Hùng content  (deferred — PET_RULES.md §8)
    the Pet rows cannot exist without their Skill Cards (required FK)
        ↑
        │  unblocked only by authored content
        │
TASK-166  (this task — Product Owner decision input)
    Product Owner answers D-1 and D-2 with C-1…C-12 coverage.
    Records the CONTENT only:
      name, purpose, effect, target, magnitudes, lifetime, trigger, cost,
      interactions — never a stored row and never an identifier spelling.
        ↓
DOWNSTREAM DOCUMENTATION-APPLICATION TASK  (not created here)
    authors the two Skills in CARD_RULES.md §4.1 (Cost + Effect, consistent
    with §4) and updates PET_RULES.md §8's provisioned/deferred row set,
    producing an implementation-ready content contract
        ↓
PROVISIONING TASK  (not created here)
    inserts the two CardDefinition rows and the two PetDefinition rows
        ↓
IMPLEMENTATION / VERIFICATION TASK  (not created here)
    confirms the Skills resolve and behave as authored
```

**The boundary is deliberate: do not jump from TASK-166 straight to a content
row or to code.** A decision to ship a Skill is not an authored contract; the
Skill must exist in `docs/` before any `CardDefinition` may be provisioned or
any behavior implemented (`AGENTS.md` §7, `DATABASE.md` §5 item 4). This mirrors
the TASK-104 → TASK-103 → TASK-102 and TASK-110 → TASK-085 precedents.

**ADR required: NO.** This decision changes no architecture, no database
strategy, no realtime strategy, no module boundary, and no infrastructure
(`AGENTS.md` §18). It authors gameplay content inside an existing, already
implemented content model: the same `CardDefinition.EffectDefinition` shape the
three authored Skills use, the same Damage Pipeline, the same Heal/Shield/
Status Effect rules. This determination is **reported**, not authored here,
consistent with TASK-154's treatment. If the Product Owner's answer turns out to
require a new system, the corresponding Stop Condition fires and the ADR
question becomes part of that separate decision.

**This task does not create the downstream tasks, and does not implement them.**
Naming them here records the dependency edges only.

---

## Acceptance Criteria

All binary and testable. Criteria 1–7 concern the decision register; 8–15 concern
scope, isolation, and authority.

- [ ] Both decision slots exist and are individually listed, each with its own
      `Decision:` field (D-1 for Thanh Xà, D-2 for Sơn Hùng).
- [ ] Every slot states the **coverage its answer must provide**, by reference to
      the shared coverage checklist.
- [ ] Every slot records its **current authoritative evidence** by file +
      section, without restating or copying a rule, magnitude, formula, or schema
      (`tasks/README.md` §9).
- [ ] Every one of C-1…C-12 in §"Required Decision Coverage" is explicitly
      resolved by the recorded answer, or explicitly recorded as unspecified.
- [ ] Where options are presented, they are drawn from the documents' own
      structure (the `effectType` / `valueType` vocabulary and the three authored
      Skills' shape) and **no option is marked recommended, preferred, or best**
      anywhere in this task.
- [ ] Each slot has a Product Owner input field (`Decision:` + optional
      `Rationale:`) initialized to `<PENDING PRODUCT-OWNER DECISION>`.
- [ ] No Skill content — name, purpose, effect, target, magnitude, damage type,
      healing/shield amount, stat modification, crit value, status, duration,
      trigger, cost, cooldown, or interaction — was proposed, made, guessed,
      ranked, defaulted, or partially answered by an agent for either Pet.
- [ ] Any supplied answer is recorded **verbatim**; no value or wording was
      altered by an agent.
- [ ] **No source code** was modified: zero files under `src/` (byte-identical).
- [ ] **No authoritative documentation changes**: zero files under `docs/` were
      created, modified, renamed, or deleted (byte-identical). In particular
      `CARD_RULES.md` and `PET_RULES.md` are untouched.
- [ ] **No tests** were modified: zero files under `tests/` (byte-identical).
- [ ] **No other task file** was created, modified, moved, or re-statused;
      `tasks/completed/` is byte-identical.
- [ ] **No new task, no ADR** was created; `docs/03-decisions/` is unmodified.
- [ ] **No invented mechanic**: no new resource, cooldown, RNG behaviour, status,
      damage type, event, SignalR method, database column, or `BattleState`
      structure is proposed or authored anywhere in this file.
- [ ] The downstream documentation-application task is recorded as the owner of
      the authoritative edit, and is not created or performed here.
- [ ] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001).

---

## Affected Files & Areas

```text
[ ] src/backend/ (Domain / Application / Infrastructure / Api)   — NONE
[ ] src/frontend/client/ (scenes / runtime / services / state)   — NONE
[ ] tests/ (unit / integration / gameplay scenarios)             — NONE
[ ] docs/ (CARD_RULES.md / PET_RULES.md / GAME_RULES.md /
      PASSIVE_RULES.md / COMBAT_RULES.md / DATABASE.md)          — NONE
[ ] docs/00-overview/                                            — NONE
[ ] docs/03-decisions/ADR/                                        — NONE
[ ] tasks/completed/                                              — NONE (immutable)
[ ] tasks/backlog/TASK-154-*.md, TASK-156-*.md                    — NONE (read-only context)
[ ] tasks/blocked/TASK-036-*.md                                   — NONE (read-only context)
[x] tasks/backlog/TASK-166-<this file>.md — this task file only
```

---

## Implementation Notes

- **This task is a decision register, not a resolution.** Its whole deliverable
  is the two recorded answers. If an answer is absent, the correct outcome is to
  report "awaiting Product Owner input" — **not** to author one.
  *(Superseded in practice: both answers have now been supplied. See
  §"Coverage resolution" and §"Completion Evidence".)*
- **Do not "execute" this task while it has no Product Owner answer.** With both
  `Decision:` slots pending, the only correct agent behavior is to report
  **awaiting input** and stop. There is no code to write, no test to run, and no
  document to edit. `Status: BACKLOG` is the correct resting state.
  *(Superseded in practice: both slots are filled, `Status:` is `READY`, and the
  deliverable is complete. The resting-state guidance no longer applies.)*
- **Follow TASK-160's and TASK-104's shape exactly.** Their §"Decision Options"
  is the model for §"Decision Options" here; their §"Recording Discipline" is the
  model for §"Recording Discipline"; their binary acceptance criteria are the
  model for §"Acceptance Criteria"; their "awaiting input" first stop condition is
  the model for this task's normal starting state.
- **Cite, do not restate.** Use the `` `DOC.md` §N `` citation idiom. Never copy
  a magnitude, a formula, a schema, a member list, or a stored-row shape into this
  file (`AGENTS.md` §9, `documentation-change.md` §2, `tasks/README.md` §9).
- **Neutral presentation is the requirement, not a courtesy.** `AGENTS.md` §4
  forbids "pick[ing] whichever rule is easier to implement". An option list that
  ranks options has already made the decision.
- **The option lists are shapes, not suggestions.** They exist so that a supplied
  answer can be slotted into a known structure. If the Product Owner supplies a
  Skill that fits none of them, `D-1E` / `D-2E` covers it — that is a complete
  answer, not an exception.
- **Distinctness is a hard requirement, not a nicety.** `PET_RULES.md` §7 makes
  the Signature Skill one of the four components that must give a Pet its
  identity, and §3 item 1 explicitly disallows implementing a Pet as "existing
  Pet + higher numbers". Sơn Hùng's Passive already grants temporary Defense —
  a Skill that merely grants more Defense would fail both rules. This is the most
  likely source of a deficient answer; if the supplied content is
  indistinguishable from the Pet's Passive or from an existing Skill, record it as
  given and flag the §7/§3 item 1 tension rather than silently accepting it.
- **`Undetermined` is a legitimate magnitude outcome.** If the Product Owner
  decides the *effect identity* but not the *number*, the answer is complete and
  recordable: `DATABASE.md` §1 states `Undetermined` "remains valid" and is why
  no magnitude has to be invented to make a row representable. Do not treat "no
  number yet" as an incomplete decision, and do not invent a number to fill it.
- **Do not confuse this task with the two adjacent open content items.** The 2
  remaining Bosses and the remaining Relics / Burning Curse trigger are separate
  missing-content decisions (`TASK-163` records all three as distinct). Do not
  broaden this task into them.
- **The `Sơn Hạc` / `Sơn Hùng` naming question is NOT this task's.** It is a
  documented report-only finding (TASK-082, TASK-083, TASK-084 carried it
  forward). Every authoritative document says "Sơn Hùng"; that spelling governs.
  Report it again if it resurfaces; do not resolve it here.
- **Report, do not fix, adjacent gaps** (`AGENTS.md` §16). In particular, the
  zero-magnitude "Restore 8% HP" / "Temp Defense" Passive values are config
  balance values owned elsewhere (`PASSIVE_RULES.md` §8's closing note) and are
  not this task's concern.
- **Do not modify `tasks/completed/`.**

---

## Testing Requirements

This task changes no code, so it produces no unit, integration, or gameplay
test. Its verification is an **input-completeness check**, a **pre-judgement
check**, a **recording-fidelity check**, and a **scope validation**, at the depth
`core/validation.md` requires for a LOW-risk DOCUMENTATION task.

### Required Verification

```text
[ ] Input completeness       — confirm both §"Decision Options" slots (D-1, D-2)
                               exist, each with its coverage reference and a
                               PENDING Product Owner field
[ ] Coverage completeness    — confirm C-1…C-12 are each resolved or explicitly
                               recorded as unspecified
[ ] Pre-judgement check      — confirm no Skill name, effect, target, magnitude,
                               duration, cost, or interaction is proposed,
                               recommended, ranked, or defaulted anywhere in this
                               task, for either Pet
[ ] Recording fidelity check — confirm any supplied answer was transcribed
                               verbatim (wording, names, and values unaltered)
[ ] No-invention check       — confirm no `EffectDefinition` shape, identifier
                               spelling, database row, resource, cooldown, status,
                               damage type, event, or state member is proposed
                               anywhere in this file
[ ] Vocabulary check (C-10)  — confirm each recorded Skill is expressible in
                               DATABASE.md §1's existing effectType / valueType
                               sets, or that the gap is recorded as a separate
                               unresolved decision
[ ] Distinctness check (C-11)— confirm each recorded Skill is stated as distinct
                               from its Pet's Passive and from the three authored
                               Skills (PET_RULES.md §7, §3 item 1)
[ ] Scope validation         — MVP_SCOPE.md §1/§2 (quality/scope-validation.md)
[ ] Isolation verification   — docs/ (0 files), src/ (0 files), tests/ (0 files),
                               tasks/completed/ and all other task files unchanged
[ ] Unit tests               — N/A (no code)
[ ] Integration tests        — N/A (no code)
[ ] Gameplay scenarios       — N/A (no code; the scenarios are authored by the
                               implementation tasks downstream)
```

### Key Edge Cases

- **An answer that leaves one of its coverage items unspecified** — record it as
  unspecified and stop short of resolving it; do not infer the missing part.
- **A Skill whose effect identity is decided but whose magnitude is not** — this
  is complete. Record the effect; record the magnitude as `Undetermined` per
  `DATABASE.md` §1. Do not invent a number.
- **A Skill not expressible in the current vocabulary** — C-10 is unresolved; the
  new-mechanic Stop Condition fires. Do not record it as settled, and do not
  silently extend the `effectType` or `valueType` set.
- **A Skill that requires a cooldown** — `CARD_RULES.md` §4 defines none and no
  document defines one anywhere. Record the requirement as a separate unresolved
  decision and STOP; do not introduce the mechanic.
- **A Skill indistinguishable from the Pet's Passive or an existing Skill** —
  record the answer as given and flag the `PET_RULES.md` §7 / §3 item 1 tension
  per `AGENTS.md` §4 rather than silently accepting or silently "fixing" it.
- **An answer that conflicts with an existing documented contract** (e.g. a
  non-"ATK" `TargetStat`, `COMBAT_RULES.md` §5.4.5/§5.5.3) — record the answer
  and report the conflict per `AGENTS.md` §4; do not silently reconcile it, and
  do not edit the document.
- **The two answers arriving separately** — record each independently; do not
  treat one as a default, template, or precedent for the other.
- **The Product Owner answering "these two Pets are cut from MVP"** — this is a
  scope decision, not a Skill decision. It belongs to `MVP_SCOPE.md` §1 and
  `PET_RULES.md` §8, and it is a materially different outcome: report it, do not
  record it as a Signature Skill answer, and do not edit the scope documents.

---

## Completion Evidence

<!--
  TO BE COMPLETED BY THE EXECUTING AGENT after reaching DONE.
  Do not mark this task DONE until all required decisions are explicitly
  recorded. Keep concise and factual.
-->

### Decision Source

```text
Decision source:   Product Owner
```

### Pet 1 — Thanh Xà

```text
PRODUCT OWNER ANSWER (recorded verbatim — see §"D-1 — Thanh Xà's Signature
Skill"):

Skill Name: Venomous Bloom
Purpose / Role: Sustained damage / Damage-over-Time oriented Signature Skill.
Power Cost: 80
Damage: 80 Flat → Boss
Damage Element: Mộc
Burn: 25 Flat → Boss, Duration: 2 Turns
Burn Element: Hỏa (Fire) — the existing Burn Element. The Mộc Element applies
  only to the direct Damage effect.
Skill Lifetime: Immediate resolution on PetSkillCast. The Skill itself does not
  remain as a pending/active state after resolution.
Trigger / Timing: Resolve immediately when the PetSkillCast is successfully
  executed.
Interaction Rules: Damage and Burn use the existing server-authoritative
  Damage Pipeline. Burn follows the existing Burn duration/tick rules. No new
  gameplay mechanic, event, resource, or pending state is introduced.
Distinctness: Confirmed distinct from existing Skills and from Thanh Xà's
  Passive. Its defining role is sustained/DoT-oriented damage.

STATUS: COMPLETE — all applicable coverage items 1–17 supplied. C-1 resolved.
No open points: the Burn Element question was decided (Burn = Hỏa; Mộc applies
only to the direct Damage effect), closing the last wording point.
```

### Pet 2 — Sơn Hùng

```text
PRODUCT OWNER ANSWER (recorded verbatim — see §"D-2 — Sơn Hùng's Signature
Skill"):

Skill Name: Earthshaker
Purpose / Role: Burst damage Signature Skill.
Power Cost: 100
Damage: 150 Flat → Boss
Damage Element: Thổ
Skill Lifetime: Immediate resolution on PetSkillCast. The Skill itself does not
  remain as a pending/active state after resolution.
Trigger / Timing: Resolve immediately when the PetSkillCast is successfully
  executed.
Interaction Rules: Direct damage uses the existing server-authoritative Damage
  Pipeline. No Burn, new status effect, new event, new resource, or pending
  state is introduced.
Distinctness: Confirmed distinct from existing Skills and from Sơn Hùng's
  Passive. Its defining role is burst/direct damage.

STATUS: COMPLETE — all applicable coverage items 1–17 supplied. C-2 resolved.
No open wording point.
```

### Changed Files

- `tasks/backlog/TASK-166-<this file>.md` — this task file only (both Product
  Owner decisions recorded verbatim with full C-1…C-12 coverage; the previously
  PENDING items resolved, including the final Burn-element wording point). No
  other file created or modified. Zero files under `src/`, `tests/`, or `docs/`.

### Validation Results

```text
Recording fidelity      PASS — both answers transcribed verbatim. No supplied
                        name, value, target, duration, cost, Element, purpose,
                        timing, interaction, or distinctness wording was
                        altered. The Burn-element decision is recorded verbatim
                        ("Venomous Bloom's Burn tick keeps the existing Hỏa
                        (Fire) Element. The Mộc Element applies only to the
                        direct Damage effect."). Emoji headings from pass 1 were
                        removed as formatting only (§"Recording Discipline"
                        permits formatting fixes, never wording changes); the
                        supplied "Skill Name:" field labels introduced in pass 2
                        replace them.

Coverage completeness   PASS — C-1…C-12 are all resolved (see §"Coverage
                        resolution"). Every applicable checklist item 1–17 is
                        supplied for both Pets. Conditional items not used are
                        correctly absent: no Heal, no Shield, no stat
                        modification, no Crit participation, and no cooldown
                        for either Skill. No item remains PENDING.

Pre-judgement check     PASS — no Skill name, effect, target, magnitude,
                        duration, cost, Element, trigger, or interaction was
                        proposed, guessed, ranked, recommended, or defaulted by
                        the agent for either Pet. The Elements arrived from the
                        Product Owner explicitly ("Mộc" / "Thổ", and "Hỏa" for
                        the Burn tick) and were NOT inferred from
                        ELEMENT_RULES.md §5/§6. The Burn-element ambiguity was
                        reported to the Product Owner rather than decided by the
                        agent.

No-invention check      PASS — no EffectDefinition JSON, identifier spelling
                        (CardDefinitionId / PassiveId / PetDefinitionId),
                        database row, resource, cooldown, damage type, event,
                        SignalR method, persistence structure, state member, or
                        EffectDefinition field was authored in this file.

Vocabulary check (C-10) PASS — both Skills use only DATABASE.md §1's closed
                        effectType set (Damage; Burn) and valueType Flat, with
                        Burn's required `duration` member present. No new effect
                        identity, value interpretation, damage type, status,
                        resource, trigger form, or state member is required.
                        The damage Element is content stated in CARD_RULES.md
                        §4.1 prose — the same representation the three authored
                        Skills use — so no schema change is triggered. The Burn
                        Element (Hỏa) is the existing documented Burn Element
                        (COMBAT_RULES.md §5.1), so it introduces no new damage
                        type. The earlier stop condition is RESOLVED: the prior
                        blocker was the ABSENCE of an Element decision, not the
                        storage representation. No vocabulary was extended.

Distinctness check      PASS — the Product Owner explicitly confirmed
  (C-11)                distinctness for both Pets, against both their own
                        Passives and the existing Skills (PET_RULES.md §7,
                        §3 item 1). Sơn Hùng's confirmed distinctness is
                        load-bearing: its Passive grants temporary Defense
                        (PASSIVE_RULES.md §8) while Earthshaker is burst damage.

Isolation verification  PASS — docs/ (0 files), src/ (0 files), tests/ (0
                        files), tasks/completed/ unchanged, and no other task
                        file created, modified, moved, or re-statused.

Scope validation        PASS — both Skills are within MVP_SCOPE.md §1 ("5 Pet
                        Skill Cards (one per Pet)"). No out-of-scope system
                        introduced. ROADMAP.md §1 Phase 2's "All 5 Pets" and
                        the deferred-row blocker in PET_RULES.md §8 are
                        unblocked.
```

### Scope Verification

```text
Documentation:     No authoritative documentation modified.
Source:            No source code modified.
Tests:             No test modified.
Tasks:             No other task created, modified, moved, or re-statused.
Mechanics:         No new gameplay mechanic assumed without explicit approval.
                   No cooldown, resource, state field, EffectDefinition field,
                   SignalR event, gameplay event, damage type, status effect,
                   or persistence structure was introduced.
Handoff:           READY — the decision record is complete and ready for the
                   subsequent authoritative documentation-application task
                   (CARD_RULES.md §4.1 + PET_RULES.md §8).
```

**No item remains OPEN or PENDING.** The Burn-element wording point for Thanh Xà
was the last one and is now decided: the Burn tick keeps the existing Hỏa
Element, and Mộc applies only to the direct Damage effect. The decision record is
fully closed — nothing is left for the Product Owner and no ambiguity remains for
the downstream task.

**No stop condition remains active.** The pass-1 C-10 stop condition is resolved:
the Skills need no new effect identity, value interpretation, damage type,
status, resource, trigger form, state member, event, or architecture.

**Lifecycle reconciliation.** `tasks/TASK_LIFECYCLE.md` §4 permits exactly one
path into `completed/`: `IN REVIEW → DONE` (`active/` → `completed/`), and
requires `READY → IN PROGRESS` (`backlog/` → `active/`) before it
(`tasks/TASK_LIFECYCLE.md` §3, `tasks/README.md` §7). TASK-166 was created
directly in `tasks/backlog/` by the Orchestrator (TASK-165 §"Next Task ID") and
has never been picked up, so it has no `active/` residency and no review pass to
satisfy — the lifecycle therefore does not allow this recording step to move it
to `completed/`. Its `Status:` is set to `READY`: the decision record, which is
this task's entire deliverable, is complete and awaiting the Orchestrator's
lifecycle act. **No file was moved, and no `Status: DONE` was set.**

- [x] Confirmed zero files under `docs/` modified — **PASS**
- [x] Confirmed zero files under `src/` modified — **PASS**
- [x] Confirmed zero files under `tests/` modified — **PASS**
- [x] Confirmed no new task, ADR, event, SignalR method, database column, or
      `BattleState` structure created — **PASS**
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1) — **PASS**

---

## Lifecycle Reconciliation

<!--
  LIFECYCLE METADATA ONLY. Appended by the lifecycle-reconciliation pass.
  No substantive content above this section was changed: "Why This Task Is
  Required", "Authoritative References", "Current Contract", "Exact
  Ambiguity", "Decision Options", "Required Decision Coverage", "Recording
  Discipline", "Out of Scope", "Stop Conditions", "Downstream Dependency",
  "Acceptance Criteria", "Implementation Notes", "Testing Requirements",
  "Completion Evidence", and "Scope Verification" are byte-identical to their
  recorded state.
-->

```text
Reconciled status:   READY → DONE
File move:           tasks/backlog/ → tasks/completed/
Authority:           tasks/TASK_LIFECYCLE.md §1 (DONE), §2 (allowed
                     transitions), §4 (file movement summary)
Recorded by:         lifecycle reconciliation pass
```

**Why this file's own deferral is now discharged.** This file's
§"Lifecycle reconciliation" (recorded at the time the decisions were captured)
stated the move was "the Orchestrator's lifecycle act ... No file was moved, and
no `Status: DONE` was set." That deferral was correct then and is honoured now:
the move is performed by the reconciliation pass, not by re-opening the
recording step.

**Why DONE and not SUPERSEDED** (`tasks/TASK_LIFECYCLE.md` §1/§3):

```text
DONE        "All core/completion.md §1 criteria are satisfied by DIRECT
             EXECUTION of the task."
SUPERSEDED  "All intended deliverables ... 100% satisfied or rendered
             obsolete by downstream/decomposition tasks WITHOUT the task
             itself being directly executed."

This task WAS directly executed. Its Objective is to "obtain and record" the
Product Owner's explicit decisions on the two unresolved Signature Skills; both
`Decision:` slots (D-1 "Venomous Bloom", D-2 "Earthshaker") are filled and all
of C-1…C-12 are resolved in §"Coverage resolution", with §"Completion Evidence"
fully populated and closing "No item remains OPEN or PENDING". A recording task
is executed by recording. SUPERSEDED would misstate that the task was never
directly executed.
```

**Evidence that the deliverable is complete** (read from this file's own body —
not inferred from a downstream reference):

```text
E-166-1  §"D-1 — Thanh Xà's Signature Skill" → `Decision:` slot FILLED with
         the Product Owner's verbatim content (Venomous Bloom, 80 Power,
         Damage 80 Flat Mộc → Boss, Burn 25 Flat Hỏa for 2 Turns), plus the
         §"Recorded verbatim" resolution notes.
E-166-2  §"D-2 — Sơn Hùng's Signature Skill" → `Decision:` slot FILLED
         (Earthshaker, 100 Power, Damage 150 Flat Thổ → Boss), with "No open
         wording point."
E-166-3  §"Coverage resolution" → C-1 … C-12 each read RESOLVED, closing
         "No item remains PENDING PRODUCT-OWNER DECISION."
E-166-4  §"Completion Evidence" populated: "Decision Source", both Pet answer
         blocks with "STATUS: COMPLETE", "Changed Files", and PASS
         "Validation Results"; the Burn-element wording point was the last
         open item and is recorded as decided.
→ DIRECT EXECUTION IS EVIDENCED.
```

**Downstream chain is terminal** (independent repository corroboration — read,
not modified):

```text
TASK-167  Status: DONE, resides in tasks/completed/.
          It applied both decisions to the canonical owners —
          CARD_RULES.md §4.1 (v1.7 → 1.8) and PET_RULES.md §8 (v3.1 → 3.2) —
          plus three dependent stale-reference corrections.
          Corroboration: CARD_RULES.md §4.1 carries both authored entries and
          the closing sentence "all five MVP Pets' Signature Skills are
          authored in this section."

TASK-168  Status: DONE, resides in tasks/completed/.
          It provisioned card-venomous-bloom, card-earthshaker, pet-thanh-xa,
          and pet-son-hung.
          Corroboration: migration
          20261004055006_ProvisionThanhXaAndSonHungSignatureSkills.cs carries
          all four inserts.

TASK-169  Status: DONE, resides in tasks/completed/.
          It reconciled the DATABASE.md CardDefinition content-set references
          to the post-TASK-168 state.

No dependent action remains that is uniquely this task's: the decisions are
recorded (this task), applied (TASK-167), provisioned (TASK-168), and their
documentation reconciled (TASK-169).
```

**Recorded status history (preserved verbatim, not rewritten):**

```text
Original Status field value: "READY (both `Decision:` slots are now filled by
the Product Owner and C-1…C-12 are resolved — see §'Coverage resolution'.
TASK-166's deliverable, the decision record, is complete. Lifecycle note:
`tasks/TASK_LIFECYCLE.md` §4 permits only `IN REVIEW → DONE` as a path into
`completed/`, and requires `READY → IN PROGRESS` (backlog/ → active/) first.
This task has not been picked up, so the file correctly remains in
`tasks/backlog/` with `Status: READY`. Moving it to `completed/` is the
Orchestrator's lifecycle act (`tasks/TASK_LIFECYCLE.md` §4,
`core/completion.md`), not this recording step.)"
```

**Precedent.** This is the third lifecycle reconciliation in the repository,
following `TASK-162` and `TASK-165`. Both established the shape applied here:
per-task evidence rows, a lifecycle-rule citation block, and an
isolation-checked record. The `TASK-165` reconciliation supplies the closest
precedent for this exact case — a decision-recording task whose own
§"Completion Evidence" records completion while its `Status` field and folder
lag behind (its `TASK-163`).

**Boundary honoured by this reconciliation:**

```text
[x] No source file created, modified, renamed, or deleted.
[x] No test file created, modified, renamed, or deleted.
[x] No migration or database change.
[x] No authoritative document under docs/ created, modified, renamed, or
    deleted.
[x] No gameplay rule, Card, Pet, cost, magnitude, Element, duration, or
    interaction changed.
[x] The substantive decision record (D-1/D-2, C-1…C-12), Coverage resolution,
    Recording Discipline, Out of Scope, Stop Conditions, Downstream
    Dependency, Acceptance Criteria, Implementation Notes, Testing
    Requirements, Completion Evidence, and Scope Verification above are
    unchanged.
[x] TASK-167, TASK-168, and TASK-169 were not modified.
[x] No new task was created.
```
