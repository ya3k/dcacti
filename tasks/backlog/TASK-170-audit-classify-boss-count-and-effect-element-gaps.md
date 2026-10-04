# TASK-170 — Audit and Classify the Boss-Count and Effect-Element Gaps

<!--
  GEN-TASK EXECUTION MANIFEST — AUDIT / CLASSIFICATION TASK
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ and src/ by path and section — it does NOT copy
  game rules, formulas, magnitudes, schemas, or contracts.

  THIS TASK DECIDES NOTHING AND CHANGES NOTHING.

  It answers exactly one question, for each of two audit-reported gaps:
      Which classification applies, and what is the next required
      workflow step?

  It performs NO implementation, edits NO source, edits NO test, creates
  NO migration, edits NO authoritative document, edits NO existing task
  (including TASK-036), and creates NO follow-up task.

  The two gaps were reported by audit only. They are NOT resolved by this
  task and are NOT resolved anywhere in the repository today. See
  "Classification" for the per-gap outcome and "Dependency / Ownership"
  for the exact next step each requires.
-->

---

## Metadata

```text
Task ID:           TASK-170
Type:              DOCUMENTATION (TASK_TYPES.md §2 — "Change docs/ content" is
                   the closest existing type; this task's output is a
                   classification/consistency finding about docs/ content, and
                   it is executed under the documentation workflow with review
                   posture. See "Type classification note" — no code-change
                   type applies because this task changes nothing at all.)
Status:            BACKLOG
Risk:              LOW (read-only audit. No source file, test file, migration,
                   or authoritative document is written. The task produces a
                   classification and a stop/decision report; it cannot break
                   runtime behavior because it changes nothing.)
Priority:          HIGH (it is the gate that determines whether any
                   implementation task can be generated from the two reported
                   gaps, and it prevents a wrong-scoped implementation task from
                   being generated against either one.)
Primary Agent:     review (quality/review.md posture; AGENT_SELECTION.md §1 —
                   "Documentation change: Primary Agent Review". This task
                   classifies discrepancies between authoritative documents and
                   the implementation and decides no rule, no value, and no
                   contract. Use case: .ai/agents/review.md — "Documentation
                   consistency verification (docs↔docs, docs↔code)" and
                   "Implementation correctness review".)
Supporting Agents: gameplay (ELEMENT_RULES.md / CARD_RULES.md / COMBAT_RULES.md /
                   BOSS_RULES.md domain-rule authority for both gaps — consulted
                   to CONFIRM what the domain owns, never to author a value),
                   backend (Application-layer call sites are inspected as
                   EVIDENCE for classification only — BattleStateService.cs /
                   CardCastExecutor.cs; no Application or Api file is modified),
                   orchestrator (task generation decision — Outcome A–D routing
                   and the blocked-dependency report; read-only)
Workflow:          documentation/documentation-change.md §1 (identify canonical
                   owner → read related docs → check for conflicts → determine
                   smallest authoritative source; this task STOPS at the
                   conflict/owner-determination step and writes no edit)
                   + core/context-discovery.md §3 (STOP reporting)
                   + quality/review.md (finding classification)
Skills:            discovery/documentation-discovery,
                   discovery/impact-analysis,
                   quality/documentation-consistency,
                   quality/scope-validation,
                   gameplay/gameplay-behavior-derivation
                   (5 skills — Normal budget, tasks/README.md §12)
Dependencies:      TASK-166 (DONE — Product Owner decisions for the Thanh Xà /
                     Sơn Hùng Signature Skills, including the Burn-Element
                     decision. IMMUTABLE; read-only),
                   TASK-167 (DONE — applied TASK-166 to authoritative docs and
                     recorded the §"Storage Decision" that GAP-B is resolved
                     against. IMMUTABLE; read-only),
                   TASK-168 (DONE — provisioned both Signature Skill rows with
                     the explicit "Element is NOT a member" boundary.
                     IMMUTABLE; read-only),
                   TASK-169 (DONE — DATABASE.md CardDefinition content-set
                     reconciliation. IMMUTABLE; read-only),
                   TASK-048 / TASK-046 / TASK-047 (DONE — Boss identity and
                     BOSS_RULES.md §6.4 contract history. IMMUTABLE; read-only),
                   TASK-160 / TASK-155 / TASK-126 / TASK-124 (DONE — Boss
                     Passive/Skill contract history referenced by BOSS_RULES.md
                     §6. IMMUTABLE; read-only)
Blocks:            nothing at runtime. It gates the GENERATION of any future
                   task derived from GAP-A or GAP-B (not created here).
Estimate:          Normal (read-only audit across two domain areas; zero files
                   written outside this task file)
```

**Type classification note.** No existing type in `TASK_TYPES.md` §1 describes a
task whose deliverable is *a classification and a next-step report*. The six
types all presuppose a change (FEATURE, BUG, GAMEPLAY-CHANGE, REFACTOR,
ARCHITECTURE, DOCUMENTATION). `DOCUMENTATION` is selected because it is the only
type whose subject matter is `docs/` content consistency and whose workflow
(`documentation/documentation-change.md`) contains the exact step this task
executes — identify the canonical owner and check for conflicts (its §1 and §3).
The completed audit precedent is TASK-063/TASK-066, which likewise returned a
verdict under a read-only posture and created no follow-up task itself.

**This task does NOT create TASK-171 or any other task.** Per instruction and per
`TASK_LIFECYCLE.md` §2, a stopped/blocked item is a report, not an authorization
to generate work. See "Dependency / Ownership" for what the next workflow step
is and who must authorize it.

**This task does NOT modify TASK-036.** `tasks/blocked/TASK-036-discord-credential-secret-hygiene.md`
is unrelated (Discord credential hygiene) and was inspected only to confirm it
owns neither gap.

---

## Objective

Determine, for each of the two audit-reported gaps (`GAP-A`, `GAP-B`), which of
the following classifications applies, using the repository's authority
hierarchy (`AGENTS.md` §2, `docs/01-game-design/GAME_RULES.md` §21) rather than
implementation convenience:

```text
A. Documentation inconsistency correctable without a new
   gameplay/product decision
B. Gameplay/content decision required
C. Technical contract decision required
D. Existing implementation contradicts an already-authoritative contract
E. Not actually a gap after full authoritative-document review
```

Then determine whether an unblocked implementation task exists today (Outcome
A–D of the request), report the exact next required workflow step, and stop.

**Both gaps are classified `E. Not actually a gap after full
authoritative-document review`.** The authoritative documents already determine
the answer for both; neither requires a Product Owner decision, a technical
contract decision, or an implementation correction. See "GAP-A Analysis",
"GAP-B Analysis", and "Classification".

---

## Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1 ("Bosses: 5 Bosses") and §4
  (classification authority / unlisted-is-FUTURE default) — GAP-A scope target
- `docs/00-overview/ROADMAP.md` §1 ("Phase 1 — Core Loop Vertical Slice": "3 MVP
  Bosses"; "Phase 2 — Content Complete (MVP)": "All 5 Bosses (2 not yet
  content-defined …)") — GAP-A deferral record
- `docs/01-game-design/BOSS_RULES.md` §6 (the closing "Two additional MVP Bosses
  … are not yet content-defined" note), §6.2/§6.3/§6.3.1 (the three
  content-defined Bosses), §6.4 (BossId/PassiveId/SkillId identity contract) —
  GAP-A canonical owner
- `docs/01-game-design/GAME_RULES.md` §15 (Boss Rules), §19 (MVP Scope Rules —
  defers the IN/OUT/FUTURE list to `MVP_SCOPE.md`), §20 (Rule Change Policy),
  §21 (Source of Truth Hierarchy)
- `docs/02-technical/DATABASE.md` §1 `BossDefinition` ("MVP scope target: 5
  Bosses … content-defined: 3") and §3 item 5 ("Rows and provisioning" — "Only
  content-defined Bosses (currently 3 …) may ever be provisioned; the 5-Boss
  figure is the MVP scope target … not permission to create placeholder rows")
  — GAP-A technical confirmation
- `docs/01-game-design/ELEMENT_RULES.md` §1.1 (Element-carrying entities:
  Pet, Boss, **Skill**, **Effect**), §3 (Elementless Attacks), §5 (where Element
  Modifier applies, incl. "Status Effect damage-over-time ticks (e.g. Burn),
  using the Effect's Element"), §6 (MVP Pet→Element assignments) — GAP-B canonical
  owner
- `docs/01-game-design/CARD_RULES.md` §4 item 4 (Pet Skill Cards may carry an
  Element) and §4.1 ("Each effect carries its own Element" — the paragraph
  GAP-B cites), plus §4.1's per-Skill Element statements
- `docs/01-game-design/COMBAT_RULES.md` §3 (Damage Pipeline incl. step 3 Element
  Modifier), §5.1 (MVP Status Effects — "Burn … Element = Hỏa for Element
  Modifier purposes"), §5.2 item 3 (DoT ticks traverse the pipeline "using the
  Effect's own Element") — GAP-B mechanics owner
- `docs/02-technical/DATABASE.md` §1 "Card `EffectDefinition` contract" (items 1,
  2, 6 — the **closed** member set `effectType`/`valueType`/`value`/`duration`/
  `scope`, and "none may be added without a recorded owner decision") — GAP-B
  storage owner
- `docs/02-technical/GAME_STATE.md` §2.3 (`PetState.Element`), §2.4
  (`BossState.Element`), §5.1.1 (Status Effect lifecycle / step 19a duration)
- `docs/02-technical/GAME_EVENTS.md` §2 (event types — `DamageCalculated` /
  `DamageDealt` / `DamageTaken` / `PetSkillCast`) — inspected for whether any
  contract carries a per-effect Element member
- `tasks/completed/TASK-167-apply-task-166-signature-skill-decisions-to-authoritative-documentation.md`
  §"Storage Decision (DATABASE gate)" — the recorded decision that GAP-B is
  already resolved by
- `tasks/completed/TASK-168-provision-thanh-xa-and-son-hung-signature-skill-content-rows.md`
  Completion Evidence ("Element is NOT a member of either row and must not become
  one … do not add an `Element`, `DamageElement`, or `BurnElement` member to
  `EffectDefinition`")
- `tasks/completed/TASK-166-collect-product-owner-decisions-thanh-xa-and-son-hung-signature-skills.md`
  (the Product Owner decision that Burn keeps Hỏa and Mộc applies only to the
  direct Damage effect)
- `docs/AGENTS.md` §2 (source-of-truth hierarchy), §4 (conflict resolution), §7
  (game rule protection), §8 (MVP protection), §20 (when AI must stop)
- `.ai/README.md` §6 (source-of-truth rule), §13 (stop conditions), §18
  (documentation update policy); `.ai/agents/review.md`; `.ai/agents/orchestrator.md`

---

## Current State

Lifecycle reconciliation is complete. `tasks/backlog/` and `tasks/active/`
contain no task file (only `.gitkeep`); the only non-completed task file is
`tasks/blocked/TASK-036-discord-credential-secret-hygiene.md` (unrelated).
`tasks/completed/` holds TASK-001 … TASK-169, so the highest existing ID is
**TASK-169** and this task is **TASK-170** (`tasks/README.md` §3).

The current backlog therefore contains **no implementation-ready task**. Two
issues were reported by audit; both are inspected below. Neither has been
resolved: no authoritative document has been changed for either, and no
implementation change has been made for either.

```text
GAP-A  docs/00-overview/MVP_SCOPE.md         → states MVP has 5 Bosses
       docs/01-game-design/BOSS_RULES.md     → defines 3 Bosses; reported as
                                               having "no explicit deferral
                                               note for the remaining 2"

GAP-B  docs/01-game-design/CARD_RULES.md §4.1 → per-effect Element semantics
       EffectDefinition                       → reported as not carrying Element
       CardCastExecutor.cs                     → Damage Element from
                                                 PetState.Element
       BattleStateService.cs                   → Burn tick uses Element.Hoa
```

---

## GAP-A Analysis

**Reported issue.** `MVP_SCOPE.md` §1 states the MVP contains 5 Bosses, while
`BOSS_RULES.md` defines only 3 and — as reported — carries no explicit deferral
note for the remaining 2.

**Finding 1 — Does the MVP actually require 5 Bosses?** Yes, as a *scope
target*, and the documents say so consistently and without conflict.
`MVP_SCOPE.md` §1 is the single source of truth for IN/OUT/FUTURE
(`GAME_RULES.md` §19; `MVP_SCOPE.md` header) and lists "5 Bosses" under
"Includes". Nothing contradicts that.

**Finding 2 — Are the 3 defined Bosses intentionally the complete content set?**
Yes. `BOSS_RULES.md` §6.2/§6.3/§6.3.1 define exactly three Bosses (Hỏa Long,
Thủy Ma, Mộc Yêu) with their Passives, Skills, and magnitudes, and §6.4 records
their canonical identities. `ROADMAP.md` §1 Phase 1 lists "3 MVP Bosses (Hỏa
Long, Thủy Ma, Mộc Yêu)" for the vertical slice. `DATABASE.md` §1 records
`BossDefinition` as "MVP scope target: 5 Bosses … content-defined: 3".

**Finding 3 — Are the remaining 2 explicitly deferred elsewhere?** **Yes — and
the "no explicit deferral note" premise of the report is incorrect.** The
deferral is stated in four independent authoritative places:

```text
docs/01-game-design/BOSS_RULES.md §6 (closing note, verbatim location):
    "Two additional MVP Bosses (5 total per GAME_RULES.md §19 scope) are not
     yet content-defined. When authored, each must: 1. Declare an Element …
     2. Declare a Passive with an explicit trigger category from §3.2.
     3. Declare a Skill with an explicit timing rule and effect. 4. Avoid
     duplicating an existing Boss's trigger category …"

docs/00-overview/ROADMAP.md §1 Phase 2 "Content Complete (MVP)":
    "All 5 Bosses (2 not yet content-defined — see BOSS_RULES.md §6 note;
     each requires Element, Passive, Skill, stats)"

docs/02-technical/DATABASE.md §3 item 5 ("Rows and provisioning"):
    "Only content-defined Bosses (currently 3 — BOSS_RULES.md §6) may ever be
     provisioned; the 5-Boss figure is the MVP scope target (MVP_SCOPE.md §1),
     not permission to create placeholder rows for undefined content."
    …and §1's BossDefinition block: "only content-defined rows may ever be
     provisioned — see contract note below"
    …and the row-set record: "No placeholder rows …, and no rows for the two
     MVP Bosses that are not yet content-defined."

docs/00-overview/MVP_SCOPE.md §4 (Classification Authority):
    unlisted/ambiguous content is FUTURE by default and must be reported,
     not assumed IN.
```

The `BOSS_RULES.md` §6 note also cites `GAME_RULES.md` §19 as the authority for
the 5-Boss scope. `GAME_RULES.md` §19 confirms the count is owned by
`MVP_SCOPE.md`, not by `GAME_RULES.md` itself — so 5 and 3 are two different
statements about two different things (scope target vs. authored content), not
a contradiction.

**Finding 4 — Can the mismatch be corrected by documentation alone?** No
correction is required, because there is no mismatch. The apparent discrepancy
is fully reconciled by the deferral records above; `MVP_SCOPE.md` §1 (5
Bosses = MVP scope) and `BOSS_RULES.md` §6 (3 Bosses = content-defined today, 2
deferred with authoring requirements) describe the same intended state.

**Finding 5 — Do the identities/rules of the missing two Bosses require Product
Owner decisions?** **Yes, but that is not a defect and not a GAP-A blocker.**
Authoring the 2 remaining Bosses (Element, Passive, trigger, Skill, timing
rule, magnitudes, base stats, identity strings) is content authoring that
`BOSS_RULES.md` §6 explicitly defers and that `AGENTS.md` §7 / `.ai/README.md`
§8 forbids AI from inventing. `MVP_SCOPE.md` §3 lists "Boss Phases" and
"More Pets" as FUTURE direction; the 2 remaining Bosses are **IN** scope but
**not yet authored**. Their authoring is a *new content task* requiring Product
Owner decisions — it is not a *reconciliation task*, and GAP-A does not
authorize it.

**GAP-A conclusion.** Per the classification options, GAP-A is
**`E. Not actually a gap after full authoritative-document review`**. The
deferral is explicit and is recorded in `BOSS_RULES.md` §6, `ROADMAP.md` §1
Phase 2, and `DATABASE.md` §1/§3 item 5. The only residual fact is that the 2
deferred Bosses are not yet content-authored — which is the *documented, intended
state* of the MVP at Phase 1/Phase 2 boundary, not an inconsistency.

**Not classified as B/C/D/A.** Not `B` — no Product Owner decision is needed to
*reconcile the documents*, since they are already consistent (a decision *is*
needed to author the 2 Bosses, but that is new content work, tracked separately
and not created by this task). Not `C` — no technical contract is at issue;
`DATABASE.md` §3 item 5 already fixes the provisioning boundary. Not `D` — no
implementation contradicts a contract; `DATABASE.md` §3 item 5 requires exactly
the 3 provisioned rows that exist. Not `A` — there is no wording to correct
without inventing content or deciding a count.

---

## GAP-B Analysis

**Reported issue.** `CARD_RULES.md` §4.1 describes per-effect Element semantics,
but `EffectDefinition` is reported as not carrying Element; `CardCastExecutor.cs`
derives Damage Element from `PetState.Element`; `BattleStateService.cs` uses
`Element.Hoa` for the Burn tick.

**Finding 1 — Is `Element` authoritative per `EffectDefinition`?** **No.**
`CARD_RULES.md` §4.1 states *which Element each authored effect carries* and
explicitly scopes itself: "This states which Element each authored effect
carries; it changes no Element rule, which `ELEMENT_RULES.md` owns." The
canonical owner of Element semantics is `ELEMENT_RULES.md` §1.1, which lists
Element-carrying entities as **Pet, Boss, Skill (Pet Signature Skill / Boss
Skill), and Effect** — a *game-design* ownership statement about entities, not a
storage-schema statement about `EffectDefinition`. `DATABASE.md` §1 owns the
storage shape and fixes the `EffectDefinition` member set as **closed**:
`effectType`, `valueType`, `value`, plus the per-effect extras `duration` (on
`Burn`) and `scope` (on `Crit`) — with the explicit rule "No further extra member
is defined, and none may be added without a recorded owner decision."

**Finding 2 — Does the runtime intentionally derive Damage Element from
`PetState.Element`?** **Yes — and that derivation is consistent with the
authoritative model for the Cards that exist today.** `ELEMENT_RULES.md` §6
assigns each Pet exactly one Element, and `CARD_RULES.md` §4.1 states the damage
Element of each authored damage-dealing Pet Skill. For every currently authored
damage effect, the Skill's damage Element and the active Pet's Element coincide
by content: Inferno (Xích Lang, Hỏa), Venomous Bloom's damage (Thanh Xà, Mộc),
Earthshaker (Sơn Hùng, Thổ). Therefore reading `PetState.Element` at
`CardCastExecutor.cs` produces the documented value for the current Card set; it
is not a contradiction of an authoritative contract. The distinction that
matters: the *content* is authored per effect, and the *code* reads the Pet's
Element because no authored damage effect currently differs from its Pet's
Element. This is a documented-by-content coincidence, not a general rule that
Element lives on `PetState` — which is why the Burn case below is handled
separately.

**Finding 3 — Is Burn being fixed to `Element.Hoa` intentional?** **Yes, and it
is explicitly documented.** `COMBAT_RULES.md` §5.1 defines Burn as
"Element = Hỏa for Element Modifier purposes", and §5.2 item 3 states DoT ticks
traverse the Damage Pipeline "using the Effect's own Element".
`ELEMENT_RULES.md` §5 lists "Status Effect damage-over-time ticks (e.g. Burn),
using the Effect's Element" among the instances the Element Modifier applies to.
`BattleStateService.cs`'s `Element.Hoa` at the Burn tick site is therefore the
direct implementation of `COMBAT_RULES.md` §5.1 — the *effect's* Element, which
for Burn is fixed Hỏa regardless of the applying Pet. This is precisely what
`CARD_RULES.md` §4.1 means by "Venomous Bloom's two effects therefore carry
**different** Elements: its damage is Mộc … while its Burn is Hỏa". The TASK-166
Product Owner decision recorded this explicitly (Burn keeps Hỏa; Mộc applies only
to the direct Damage effect).

**Finding 4 — Does `CARD_RULES.md` §4.1's wording conflict with the actual
authoritative model?** **No.** §4.1's "Each effect carries its own Element" is a
statement about *authored content* (which Element each effect uses at the Element
Modifier step). It is consistent with `ELEMENT_RULES.md` §1.1/§5,
`COMBAT_RULES.md` §5.1/§5.2 item 3, and `DATABASE.md` §1's closed member set.
The report's premise — that per-effect Element semantics *imply* an
`EffectDefinition.Element` member — is the inference `DATABASE.md` §1 item 1 and
TASK-167's §"Storage Decision" were written to foreclose.

**Finding 5 — Is the current `EffectDefinition` schema already complete?**
**Yes, for every currently authored effect.** `DATABASE.md` §1 item 1's closed
set expresses all six authored effects (`Heal`, `Shield`, `Power`, `Damage`,
`Burn`, `Crit`) including `Burn`'s required `duration` and `Crit`'s required
`scope`. TASK-167 §"Storage Decision" answered this exact question ("Can the
existing content/provisioning convention express 'Damage → Mộc' and 'Burn → Hỏa'
without a new storage member?" — "ANSWER — YES") and TASK-168 provisioned both
new Signature Skill rows under that conclusion, recording "Element is NOT a
member of either row and must not become one."

**Finding 6 — Would changing the schema require a new technical/gameplay
decision?** **Yes — which is exactly why no change may be made here.**
`DATABASE.md` §1 item 1 states no further extra member "may be added without a
recorded owner decision", and item 2 fixes the member names as the contract.
Adding an `Element`/`DamageElement`/`BurnElement` member would therefore require
a recorded storage-contract owner decision (and, if it touched the authoritative
model, an ADR per `AGENTS.md` §18). No such decision exists, and none is required
by any authored content — `ELEMENT_RULES.md` §1.1 already authorizes the
two-Element structure as a *rule*, and the Element is carried as content, not as
a storage member.

**Finding 7 — Which category is this?** **Not a real gap.** TASK-167's
§"Storage Decision" recorded the determination and explicitly noted the residual
observation as *reported, not resolved*: "A future reader may observe that an
Element is not machine-readable from the stored row. That is a pre-existing,
deliberate property of the content model … Any decision to make Element
storage-explicit is a separate storage-contract decision with its own task and
(if architectural) its own ADR. It is NOT required by either Skill, and this task
does not raise or create it."

**GAP-B conclusion.** Per the classification options, GAP-B is
**`E. Not actually a gap after full authoritative-document review`**. The
authoritative model is: *Skill* and *Effect* carry an Element
(`ELEMENT_RULES.md` §1.1); the Element Modifier reads the damage instance's
attacking Element, including a DoT tick's own effect Element
(`ELEMENT_RULES.md` §5; `COMBAT_RULES.md` §5.2 item 3); Burn's effect Element is
fixed Hỏa (`COMBAT_RULES.md` §5.1); and `EffectDefinition`'s member set is closed
as content-agnostic storage (`DATABASE.md` §1 item 1). The implementation matches
this model: Burn's tick uses the Burn effect's Hỏa, and Damage reads the attacker
Element that equals each Pet's authored damage Element for all current content.
There is no documentation-only fix to make, no implementation defect to fix, and
no contract mismatch requiring a decision.

**Not classified as A/C/D.** Not `A` — neither `CARD_RULES.md` §4.1 nor any
other document is stale or self-contradictory; its wording is accurate and
correctly scoped. Not `C` — no contract decision is outstanding, because
`DATABASE.md` §1's closed set already expresses all authored content and
TASK-167's §"Storage Decision" already recorded that no member is needed. Not
`D` — the implementation (`CardCastExecutor.cs` Damage via `PetState.Element`;
`BattleStateService.cs` Burn tick via `Element.Hoa`) is consistent with
`ELEMENT_RULES.md` §5/§6, `COMBAT_RULES.md` §5.1/§5.2 item 3, and `CARD_RULES.md`
§4.1's per-effect Element content for every authored Card.

---

## Classification

```text
GAP-A  Boss content count
       E. Not actually a gap after full authoritative-document review
       (the explicit deferral of the 2 remaining Bosses is already recorded in
        BOSS_RULES.md §6, ROADMAP.md §1 Phase 2, and DATABASE.md §1 /
        §3 item 5; MVP_SCOPE.md §1's "5 Bosses" is the scope target those
        documents reference, not a conflicting count)

GAP-B  Effect Element contract
       E. Not actually a gap after full authoritative-document review
       (ELEMENT_RULES.md §1.1/§5 + COMBAT_RULES.md §5.1/§5.2 item 3 +
        DATABASE.md §1 item 1's closed member set + CARD_RULES.md §4.1's
        content-scoped wording already form one consistent model; TASK-167
        §"Storage Decision" recorded the determination, and the current
        implementation matches it)
```

**Implementation readiness: no implementation task is unblocked by either gap.**

- GAP-A is **not implementation work**. Authoring the 2 remaining Bosses would be
  a *new content task* requiring Product Owner decisions on Element, Passive,
  trigger, Skill, timing, magnitudes, base stats, and identity strings — content
  `AGENTS.md` §7 forbids AI from inventing and which `BOSS_RULES.md` §6 defers.
  No reconciliation task is required either, because no inconsistency exists.
- GAP-B is **closed by existing decisions**. No documentation change
  (TASK-167 already made the only relevant ones), no implementation change (the
  code matches the documented model), and no contract decision is outstanding.
  The only *possible* future item — making Element machine-readable in storage —
  is explicitly recorded by TASK-167 as a **separate** storage-contract decision
  that is "NOT required by either Skill," and it is neither raised nor created
  here.

**Outcome: this is Outcome B applied narrowly, plus no Outcome A/C/D work.**
One or both gaps *touch* content that requires explicit Product Owner decisions —
specifically GAP-A's two unauthored Bosses — so implementation derived from that
content is blocked, and this task reports it rather than creating the follow-up
task. GAP-B requires no decision and no change at all; it is a closed,
already-recorded determination. Because the next content task cannot be
determined without Product Owner input (Boss identities and mechanics for the 2
deferred Bosses), this task **reports the required decision-input work and stops**
per `AGENTS.md` §7/§20 and `.ai/README.md` §13.

**Decision-input work required (reported, NOT created here):**

```text
For the 2 remaining MVP Bosses (BOSS_RULES.md §6, ROADMAP.md §1 Phase 2), the
Product Owner must decide, per Boss:
  - Element (ELEMENT_RULES.md §1.1/§1.2 — exactly one)
  - Passive + explicit trigger category (BOSS_RULES.md §3, §3.2)
  - Skill + explicit timing rule, charge, cooldown (BOSS_RULES.md §4, §6.3)
  - Effect magnitudes (BOSS_RULES.md §6.3.1 pattern)
  - Base stats: HP/MaxHP/ATK/DEF/EnrageThreshold/Initial State (§6.1 pattern)
  - Canonical identities: BossId, PassiveId, SkillId (§6.4 convention) and the
    BossDefinitionId persistence key (DATABASE.md §1/§3 item 2)
Trigger-pattern diversity across all 5 Bosses must be respected (§3.2).
This is the exact input set a future content-authoring task would need; this
task neither collects nor records it, and creates no such task.
```

---

## Dependency / Ownership

```text
GAP-A
  Canonical owner:   docs/01-game-design/BOSS_RULES.md §6 (MVP Boss content)
                     Count/scope owner: docs/00-overview/MVP_SCOPE.md §1
  Current status:    consistent across documents — deferral recorded
  Blocking factor:   none for reconciliation; the 2 Bosses are unauthored
                     content requiring Product Owner decisions
  Next step:         a future Product-Owner-decision task (content authoring),
                     NOT created by TASK-170. No implementation dependency is
                     unblocked today.

GAP-B
  Canonical owner:   docs/01-game-design/ELEMENT_RULES.md §1.1/§5 (Element
                     semantics); docs/01-game-design/COMBAT_RULES.md
                     §5.1/§5.2 item 3 (Burn/DoT Element); storage shape owned by
                     docs/02-technical/DATABASE.md §1 item 1
  Current status:    resolved by existing decisions — TASK-166 (Product Owner),
                     TASK-167 (§"Storage Decision"), TASK-168 (provisioning
                     boundary)
  Blocking factor:   none
  Next step:         none. No task is required. If Element-ever-storage-explicit
                     is desired later, TASK-167 records it as a separate
                     storage-contract decision with its own task (and ADR if
                     architectural) — explicitly not raised or created here.
```

**Existing-task ownership check (Outcome C).** No existing task owns either
classification.

```text
tasks/backlog/  → empty (only .gitkeep)
tasks/active/   → empty (only .gitkeep)
tasks/blocked/  → TASK-036 only (Discord credential secret hygiene — inspected,
                  unrelated: it contains no Boss/Element/EffectDefinition
                  subject matter and owns neither gap)
tasks/completed/ → TASK-001…TASK-169. TASK-167 and TASK-168 own the GAP-B
                  DECISION and the provisioning BOUNDARY respectively, and are
                  cited here as the decisions GAP-B is resolved against — but
                  neither owns a *classification* of GAP-B as a gap, and both
                  are DONE/immutable. TASK-162/TASK-165/TASK-169 are the
                  lifecycle-reconciliation precedents, none covering either gap.
```

No duplication of ownership exists, so no Outcome C applies.

---

## Scope

### In Scope
- Inspect the authoritative documents listed above for GAP-A and GAP-B
- Compare `MVP_SCOPE.md` §1 vs `BOSS_RULES.md` §6 vs `ROADMAP.md` §1 vs
  `DATABASE.md` §1/§3 item 5 (GAP-A)
- Compare `CARD_RULES.md` §4.1 vs `ELEMENT_RULES.md` §1.1/§5 vs
  `COMBAT_RULES.md` §5.1/§5.2 item 3 vs `DATABASE.md` §1 item 1 vs the
  `GAME_STATE.md` Element carriers (GAP-B)
- Inspect `CardCastExecutor.cs`, `BattleStateService.cs`, `EffectDefinition` /
  `CardEffectDefinition` **read-only, for classification evidence only**
- Classify each gap per the A–E options using the `AGENTS.md` §2 hierarchy
- Trace ownership, identify the exact next required workflow step / dependency
- Report the Product-Owner decision-input set for the 2 unauthored Bosses

### Out of Scope
- Any source-code change (`src/**`) — including `EffectDefinition`,
  `CardEffectDefinition`, `CardCastExecutor.cs`, `BattleStateService.cs`
- Any test change (`tests/**`)
- Any migration or schema change
- Any authoritative-document change (`docs/**`) — including `MVP_SCOPE.md`,
  `BOSS_RULES.md`, `CARD_RULES.md`, `COMBAT_RULES.md`, `GAME_STATE.md`,
  `DATABASE.md`
- Any change to an existing task file, including
  `tasks/blocked/TASK-036-discord-credential-secret-hygiene.md` and every
  `tasks/completed/**` file
- Creating TASK-171 or any other follow-up/implementation task
- Resolving either gameplay/product decision, inventing the 2 missing Bosses,
  inventing any `EffectDefinition` field, or selecting Boss mechanics
- Any architecture, Redis, SignalR, or database change
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2

---

## Acceptance Criteria

- [ ] `GAP-A` is classified as exactly one of A–E, with the classification
      traceable to named authoritative sources and section numbers.
- [ ] `GAP-B` is classified as exactly one of A–E, with the classification
      traceable to named authoritative sources and section numbers.
- [ ] For `GAP-A`, the analysis states whether 5 Bosses is the MVP scope target,
      whether the 3 defined Bosses are the current content set, whether the
      remaining 2 are explicitly deferred, whether documentation alone can
      correct the mismatch, and whether the missing Bosses require Product Owner
      decisions — each with its source citation.
- [ ] For `GAP-B`, the analysis states whether `Element` is authoritative per
      `EffectDefinition`, whether the `PetState.Element` Damage derivation is
      intentional, whether the Burn `Element.Hoa` tick is intentional, whether
      `CARD_RULES.md` §4.1 conflicts with the authoritative model, whether the
      current `EffectDefinition` schema is complete, and whether a schema change
      would require a new decision — each with its source citation.
- [ ] The classification for each gap is derived from the `AGENTS.md` §2 /
      `GAME_RULES.md` §21 authority hierarchy, not from implementation
      convenience, and does not infer an `EffectDefinition.Element` requirement
      from `CARD_RULES.md` §4.1's wording alone nor remove it from the code's
      shape alone.
- [ ] Exactly one Outcome (A / B / C / D of the request) is identified and
      justified, and the next required workflow step is stated.
- [ ] No existing task is identified as owning either classification, or — if one
      is — it is named and no duplicate work is proposed.
- [ ] The task explicitly records: no source code changes, no documentation
      changes, no gameplay decisions, no new architecture, and no new task
      created by TASK-170.
- [ ] Every acceptance criterion above is binary and verifiable by reading this
      task file plus the cited documents; none requires running code.
- [ ] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001)
- [ ] Quality review checklist passes (`quality/review.md` §1)

---

## Affected Files & Areas

```text
[ ] src/backend/ (Domain / Application / Infrastructure / Api)   → NOT MODIFIED
[ ] src/frontend/client/ (scenes / runtime / services / state / ui)
                                                                 → NOT MODIFIED
[ ] tests/ (unit / integration / gameplay scenarios)             → NOT MODIFIED
[ ] docs/ (authoritative documentation)                          → NOT MODIFIED
[x] tasks/backlog/ — this task file only (TASK-170)
```

---

## Implementation Notes

**This task writes nothing except this task file.** It is an inspection,
comparison, classification, ownership-trace, dependency-identification, and
report task (read-only audit posture; precedent: TASK-063 / TASK-066).

Read-only inspection targets, for classification evidence only:

```text
EffectDefinition / CardEffectDefinition
  src/backend/GameServer.Domain/Cards/CardEffectDefinition.cs
    (the closed member set: EffectType, ValueType, Value, Duration, Scope —
     no Element member; and CardEffectDefinitions as the ARRAY wrapper)
  src/backend/GameServer.Domain/Cards/CardDefinition.cs  (EffectDefinition property)

CardCastExecutor
  src/backend/GameServer.Domain/Cards/CardCastExecutor.cs
    (~line 242–258 — Damage effect: AttackerElement: state.PetState.Element,
     DefenderElement: bossState.Element)
    (~line 200–216 — Burn effect: applies a DoT StatusEffect with its magnitude
     and duration; no Element member is read)

BattleStateService
  src/backend/GameServer.Application/Battle/BattleStateService.cs
    (~line 1843 — Boss-side DoT tick: AttackerElement: Element.Hoa)
    (~line 1906 — Pet-side DoT tick: AttackerElement: Element.Hoa)
    (~line 1233–1258 — Pet→Boss damage: PetState.Element vs BossState.Element)
    (~line 1557–1567 — Boss→Pet damage: bossState.Element vs PetState.Element)

PetSkillCast
  The documented Pet Skill cast path resolves at GAME_RULES.md §17 step 14 and
  emits the existing PetSkillCast event (CARD_RULES.md §4.1 closing paragraph).
  Inspected for whether any Skill/effect contract carries a per-effect Element
  member — none does.

Damage Pipeline
  src/backend/GameServer.Domain/Battle/DamagePipeline.cs (via call sites above)
  docs/01-game-design/COMBAT_RULES.md §3 (step 3 Element Modifier)

Burn tick handling
  docs/01-game-design/COMBAT_RULES.md §5.1, §5.2 item 3
  docs/02-technical/GAME_STATE.md §5.1.1 (step 19a duration/expiry)
```

Do **not** treat the code's shape as the authority for the contract, and do
**not** treat `CARD_RULES.md` §4.1's wording as implying a storage member. The
authority order is `ELEMENT_RULES.md` / `COMBAT_RULES.md` / `CARD_RULES.md`
(domain rules) for semantics, and `DATABASE.md` §1 (technical doc) for the
storage shape — with `docs/` outranking code per `AGENTS.md` §2.

---

## Testing Requirements

### Required Verification
```text
[ ] Unit tests         — N/A (no code changed; read-only audit task)
[ ] Integration tests  — N/A (no boundary changed)
[ ] Gameplay scenarios — N/A (no gameplay behavior changed)
```

### Required Non-Code Verification
```text
[x] Every classification claim cites a file + section actually read
[x] The cited BOSS_RULES.md §6 deferral note is quoted from the current file,
    not inferred
[x] The cited DATABASE.md §1 item 1 closed member set is quoted from the
    current file, not inferred
[x] tasks/backlog/, tasks/active/, tasks/blocked/, tasks/completed/ were listed
    to confirm no existing task owns either classification
[x] `git status` / file diff shows tasks/backlog/TASK-170-*.md as the ONLY
    added file and ZERO modified files
```

### Key Edge Cases
- `MVP_SCOPE.md` §4's "unlisted is FUTURE, not IN" default must not be used to
  reclassify the 2 deferred Bosses as out of scope — `MVP_SCOPE.md` §1 lists
  "5 Bosses" in IN, so they are IN-scope-but-unauthored, not FUTURE. See
  `MVP_SCOPE.md` §1/§3/§4 and `ROADMAP.md` §1 Phase 2.
- `ELEMENT_RULES.md` §1.1 lists both "Skill" and "Effect" as Element-carrying
  entities. That is a game-design ownership statement, not a storage mandate —
  `DATABASE.md` §1 item 1 owns the storage shape and closes the member set.
- The coincidence that each authored damage effect's Element equals its Pet's
  Element (`CARD_RULES.md` §4.1 vs `ELEMENT_RULES.md` §6) means
  `CardCastExecutor.cs` reading `PetState.Element` is *correct for current
  content*, not a general rule that Element belongs to `PetState`.
- Burn is the counter-example that proves the model: its effect Element (Hỏa,
  `COMBAT_RULES.md` §5.1) differs from Thanh Xà's Pet Element (Mộc,
  `ELEMENT_RULES.md` §6), and the code uses Hỏa — matching the documented
  per-effect semantics.
- TASK-167's §"Storage Decision" is a DONE/immutable task record and is cited as
  historical evidence only; it must not be edited (`TASK_LIFECYCLE.md` §3).

---

## Stop Conditions

Universal stop conditions (`AGENTS.md` §20, `.ai/README.md` §13) always apply.
Task-specific conditions:

- **STOP** if the authority hierarchy does not determine how to classify either
  gap — do not guess a classification.
- **STOP** if GAP-A is found to require a gameplay decision that cannot be
  represented as a classification — report it instead of resolving it.
- **STOP** if GAP-B is found to require a new architecture decision
  (`AGENTS.md` §18) — report ADR impact instead of deciding.
- **STOP** if two authoritative documents are found to conflict in a way that
  requires human resolution (`AGENTS.md` §4) — report both sides by file +
  section.
- **STOP** if an existing task is found to already own the same classification
  work — name it and do not duplicate it.
- **STOP** if the next task cannot be determined without guessing — report the
  missing input.
- **STOP** if executing this task would require modifying any file other than
  `tasks/backlog/TASK-170-*.md`.

**Stop-condition status at creation: NOT TRIGGERED.** Both gaps were classifiable
from the authority hierarchy alone (both `E`), no authoritative document
conflict was found, no existing task owns either classification, and no
architecture or gameplay decision is required to *classify* them.

---

## Completion Evidence

<!--
  TO BE COMPLETED BY THE EXECUTING AGENT after reaching DONE.
  Keep concise and factual.
-->

### Changed Files
- `tasks/backlog/TASK-170-audit-classify-boss-count-and-effect-element-gaps.md`
  — this audit/classification task (status field updates only, if the task is
  later moved through the lifecycle)

### Validation Results
- Documentation consistency review (`quality/documentation-consistency.md`) —
  PENDING
- Scope validation (`quality/scope-validation.md` vs `MVP_SCOPE.md` §1/§2/§4) —
  PENDING
- File-diff verification that this task file is the only addition and that zero
  files are modified — PENDING

### Server Authority & Scope Verification
- [ ] Confirmed zero client-authoritative gameplay logic (no code changed)
- [ ] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1) — both gaps concern
      IN-scope content; no OUT item touched
- [ ] Confirmed no source code changes
- [ ] Confirmed no documentation changes
- [ ] Confirmed no gameplay decisions made
- [ ] Confirmed no new architecture introduced
- [ ] Confirmed no new task created by TASK-170
