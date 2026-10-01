# TASK-110 — Author the MVP Pet Skill Card Effect Magnitudes

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ and tasks/ by path and section.

  THIS TASK RESOLVES EXACTLY ONE REMAINING GAMEPLAY CONTENT GAP:
  the unauthored effect magnitudes of the three MVP Pet Skill Cards
  defined in `docs/01-game-design/CARD_RULES.md` §4.1:
    1. Xích Lang — Inferno    (damage magnitude; Burn amount + duration)
    2. Huyền Quy — Tidal Barrier (Heal magnitude; Shield magnitude)
    3. Bạch Hổ   — Iron Fang  (damage magnitude; Crit increase + scope)

  IT IS A PRODUCT-OWNER DECISION + CONTENT-AUTHORING TASK. It does NOT
  implement source code, does NOT encode the values into the structured
  CardDefinition payload, and does NOT invent or recommend a value. See
  §"Decision Questions" and §"Stop Conditions".
-->

---

## Metadata

```text
Task ID:           TASK-110
Type:              GAMEPLAY-CHANGE (TASK_TYPES.md §2 — "a new undocumented
                   mechanic is being authorized": no magnitude for any of the
                   three §4.1 Pet Skill Card effects exists anywhere in docs/
                   today. See the Type classification note below.)
Status:            DONE (all six Product Owner decisions D-1…D-6 supplied and
                   recorded verbatim; the decided values authored into
                   `docs/01-game-design/CARD_RULES.md` §4.1 — the canonical
                   owner — with the §4.1 Cost + Effect structure preserved.
                   §2 Basic Card values, Costs, and all other sections are
                   byte-identical. Zero files under `src/` or `tests/` changed.
                   The structured payload rows deliberately remain
                   `valueType: "Undetermined"` — encoding them is the reported
                   follow-up. `TASK_LIFECYCLE.md` §3: file moves
                   `blocked/` → `completed/`.)
Risk:              HIGH (TASK_TYPES.md §4 — GAMEPLAY-CHANGE baseline is
                   always HIGH; a game rule value is being authored)
Priority:          HIGH (PetSkillCast is blocked on this content decision —
                   TASK-108 D-4 / Follow-Up item 2; TASK-104 §5 B-4; and
                   TASK-102's Pet Skill Card acceptance criteria cannot be
                   asserted without it)
Primary Agent:     gameplay (CARD_RULES.md is a domain rule doc owned by the
                   gameplay domain — TASK_TYPES.md §5 Domain x Type matrix)
Supporting Agents: review (documentation consistency; the decision must be
                   recorded against the canonical owner document),
                   testing (scenario derivation from the authored magnitudes,
                   for whichever implementation task consumes them)
Workflow:          development/gameplay-change.md
Skills:            gameplay/gameplay-behavior-derivation,
                   discovery/documentation-discovery,
                   discovery/impact-analysis,
                   quality/documentation-consistency,
                   quality/scope-validation
                   (5 skills — Normal budget, tasks/README.md §12)
Dependencies:      TASK-109 (DONE — the structured CardEffectDefinition
                      contract the values must eventually feed; its Stop
                      Condition 2 reported this exact content gap),
                   TASK-108 (DONE — D-4 confirmed PetSkillCast stays blocked
                      pending this separate content decision),
                   TASK-104 (BACKLOG — §5 "Outstanding Content Gap" / B-4 is
                      the origin of the Tidal Barrier half; input, not modified)
Blocks:            PetSkillCast implementation (TASK-108 Follow-Up item 2;
                   TASK-102's three Pet Skill Card acceptance criteria),
                   the follow-up task that encodes the authored values into
                   the CardDefinition structured payload (rows currently
                   carry valueType "Undetermined" — TASK-109 row table)
Estimate:          Normal (decision-input + content authoring across 3 Cards
                   and 6 required values; zero code — tasks/README.md §12)
```

**Type classification note.** `GAMEPLAY-CHANGE`, not `DOCUMENTATION`.
`TASK_TYPES.md` §2 types GAMEPLAY-CHANGE as covering "a new undocumented
mechanic is being authorized", and its Critical rule requires updating
authoritative documentation before implementation — which is exactly this
task's shape: the rule values do not exist yet, they must be decided by the
product/gameplay owner, and they are then authored into the owning domain
document. DOCUMENTATION is for correcting or adding documentation of an
already-decided contract; here nothing is decided yet (`AGENTS.md` §7 — a
missing rule is a stop condition, not an edit). This classification is not an
agent's invention: `TASK-104` §5 "Outstanding Content Gap" and `TASK-102`
§"Remaining Issues" both prescribe "a separate gameplay-content decision
task, typed `GAMEPLAY-CHANGE`/content per `TASK_TYPES.md` §2, to author the
magnitude", and `TASK-109`'s completion evidence names the same follow-up.

---

## Objective

Obtain the Product Owner's explicit decisions for every unauthored effect
value of the three MVP Pet Skill Cards (`CARD_RULES.md` §4.1 — Inferno,
Tidal Barrier, Iron Fang) and author those decided values into
`docs/01-game-design/CARD_RULES.md` §4.1, the canonical owner of Card balance
values (`TASK-108` D-2), so that each Pet Skill Card effect becomes
deterministic and the PetSkillCast content block recorded by `TASK-104` §5/B-4
and `TASK-108` D-4 is lifted. No source code, no payload encoding, and no
value chosen or invented by an agent.

> **Final rule for the executing agent:** present each decision question
> neutrally with its citations, obtain the owner's explicit answer, record it
> verbatim with its date and author (the TASK-104 precedent), then write only
> the decided values into `CARD_RULES.md` §4.1. Do not choose a value, do not
> rank candidates, do not derive a value from any other document, and do not
> fill an unanswered item by inference. If any item in §"Required Decision
> Coverage" is left unanswered, record which items remain unanswered and keep
> this task BLOCKED (`AGENTS.md` §7, §20).

---

## Decision Questions

All six questions are **product/balance decisions**. None can be answered
from `docs/` today — that is the gap. Each requires a magnitude (and, where
the prose implies one, a duration or scope) plus the expression form the
owner wants recorded.

```text
D-1  INFERNO — damage magnitude
     §4.1 says: "Deal high Fire (Hỏa) damage; apply Burn" (Cost 100 Power).
     What is the Card's base damage value (the "Card base value" input of
     COMBAT_RULES.md §3 step 1), and in what expression form (flat value,
     or a percentage of a stated stat)? Target is the Boss through the full
     Damage Pipeline (CARD_RULES.md §4 item 4, ELEMENT_RULES.md §5).

D-2  INFERNO — Burn amount and duration
     "apply Burn" states no magnitude and no duration. What is Burn's damage
     per tick, and how many Turns (= End Turn ticks, GAME_RULES.md §17 step
     19a, COMBAT_RULES.md §5.1/§5.2) does it last?

D-3  TIDAL BARRIER — Heal magnitude
     §4.1 says: "Heal; Gain Shield" (Cost 80 Power). What is the Heal amount
     and its expression form (e.g. % of the active Pet's Max HP vs flat)?
     Target is the active Pet (CARD_RULES.md §2 closing note).

D-4  TIDAL BARRIER — Shield magnitude
     The "Gain Shield" half has no magnitude. This is the gap explicitly
     deferred as a "separate gameplay decision" by TASK-104 §5 / B-4 and
     re-confirmed by TASK-108 D-4 and TASK-109 Stop Condition 2. What is the
     Shield amount and its expression form? (Shield semantics themselves are
     frozen: one instance, refresh-not-stack, COMBAT_RULES.md §4,
     TASK-105 — only the number is decided here.)

D-5  IRON FANG — damage magnitude
     §4.1 says: "High damage; increased Crit chance" (Cost 100 Power).
     What is the Card's base damage value and its expression form?

D-6  IRON FANG — Crit chance increase and its scope
     "increased Crit chance" states no magnitude and no scope. What is the
     Crit chance increase (e.g. percentage points over the base Crit chance,
     COMBAT_RULES.md §3.3), and for how long / over which damage instances
     does it apply (e.g. this cast's own damage, the next attack, or N
     Turns)? Note that Bạch Hổ's Pet Passive carries the same unquantified
     wording (PASSIVE_RULES.md §7), whose magnitudes are recorded as
     config-owned (PASSIVE_RULES.md §7 note); the decision must state the
     Card's value explicitly and whether it is independent of, or shared
     with, that config value.
```

**Not decided here (settled — cited, not re-opened):** Card Costs (100 / 80
/ 100, `CARD_RULES.md` §4.1), the ownership of Card balance values by
`CARD_RULES.md` (`TASK-108` D-2), Shield semantics (`COMBAT_RULES.md` §4,
TASK-105), Burn tick timing (`GAME_RULES.md` §17 step 19a, TASK-091), Damage
Pipeline order and Crit multiplier 1.5× (`COMBAT_RULES.md` §3, §3.3), and the
loadout composition rules (`CARD_RULES.md` §1).

---

## Required Decision Coverage

The recorded decision must define deterministic behavior for every item
below. Each item must be answered explicitly; "unspecified" or "high" counts
as unanswered.

```text
1.  Inferno damage — the value, its unit/expression form, and that it enters
    the Damage Pipeline as the Card base value (COMBAT_RULES.md §3 step 1).
2.  Inferno Burn — damage per tick, duration in Turns, and confirmation that
    ticks follow GAME_RULES.md §17 step 19a / COMBAT_RULES.md §5.2.
3.  Tidal Barrier Heal — the value, its unit/expression form, and its target
    (the active Pet).
4.  Tidal Barrier Shield — the value and its unit/expression form, subject to
    the frozen refresh-not-stack semantics (COMBAT_RULES.md §4).
5.  Iron Fang damage — the value and its unit/expression form.
6.  Iron Fang Crit increase — the magnitude (chance in percentage points or
    an absolute chance), its scope/duration, and its relationship (if any) to
    Bạch Hổ's Passive config value (PASSIVE_RULES.md §7).
7.  For each of the six values: the unit/interpretation the owner wants
    recorded (flat vs % Max HP vs % of a stat) so it maps cleanly onto the
    structured CardEffectDefinition valueType vocabulary later (TASK-108 D-2;
    the vocabulary itself is NOT extended by this task — see Out of Scope).
```

---

## Authoritative References

```text
docs/00-overview/MVP_SCOPE.md
  §1 — Cards and Combat/Status Effects are IN scope; this task adds no
       system (check §2/§4 before any edit: nothing here is OUT or unlisted)

docs/01-game-design/CARD_RULES.md
  §4    — Pet Skill Card rules (item 4: damage goes through the full Damage
          Pipeline + Element Modifier)
  §4.1  — THE GAP: the three MVP Pet Skill Examples and their prose effects
          ("Deal high Fire (Hỏa) damage; apply Burn" / "Heal; Gain Shield" /
          "High damage; increased Crit chance") — no magnitude anywhere.
          THIS DOCUMENT IS THE CANONICAL OWNER for the decided values.
  §2    — the authoring pattern (explicit magnitudes already recorded for
          Heal / Shield / Power Charge) and the active-Pet target note
  §6    — CardCast / PetSkillCast emission (what unblocks downstream)

docs/01-game-design/COMBAT_RULES.md
  §3    — Damage Pipeline; step 1 names "Skill/Card base value" as a Base
          Damage input (where D-1/D-5 values live)
  §3.2  — Defense Mitigation (K = 100) — why a decided base value must be a
          number, not a descriptor
  §3.3  — Critical Hits: base Crit 5%, Crit Multiplier 1.5×, and item 3
          (Crit chance modified by Relics and Pet Passives — D-6 context)
  §4    — Shield semantics (one instance, refresh not stack, absorption
          before HP, overflow) — FROZEN; only the magnitude is decided here
  §5.1, §5.2 — Status Effect list, "source + magnitude + duration", stacking
          = refresh duration / do not stack magnitude (D-2 context)

docs/01-game-design/GAME_RULES.md
  §9.5  — one Signature Skill per Pet (what §4.1 expresses as a Card)
  §17   — step 14 (Resolve Player Effects) and step 19a (End Turn Status
          Effect tick) — the timing frame for Burn duration
  §18   — server authority: the client sends a cast request only

docs/01-game-design/PASSIVE_RULES.md
  §7 table — Bạch Hổ "next attack gains increased Crit chance" and Huyền Quy
          "Gain Shield = 15% Max HP" are PET PASSIVE magnitudes, NOT the
          Card's. §7 note: passive magnitudes (Burn amount, Crit increase %)
          are balance values held in config. DO NOT CONFLATE with §4.1.

docs/01-game-design/PET_RULES.md
  §2.3  — Signature Skill availability (active Pet only)
  §7 table (line ~378) — the Huyền Quy row's "Shield 15% Max HP" is the
          PASSIVE column; its Signature Skill column says only "Tidal
          Barrier". Not a source for D-4.

docs/01-game-design/BOSS_RULES.md
  §6.3, §6.3.1 — the precedent for how magnitudes and durations are recorded
          once decided (TASK-092: Burn 50 dmg/tick for 2 Turns, base damages
          150/120/100). Pattern reference only; Boss values are NOT a source
          for Card values.

docs/03-decisions/ADR/ADR-001 — server authority (values are resolved
  server-side; the client only requests a cast)
```

Referenced for context only — **not modified by this task**:

```text
tasks/backlog/TASK-104-collect-cardcast-wire-and-shield-contract-decisions.md
  §5 "Outstanding Content Gap" + B-4 disposition "Separate gameplay decision"
  — origin of D-4 and of this task's prescribed type
tasks/backlog/TASK-108-resolve-card-effect-resolution-contract.md
  D-4 (PetSkillCast stays separate; Tidal Barrier remains blocked) and
  "Follow-Up Tasks Required" item 2 — the required separate content task
tasks/completed/TASK-109-implement-structured-effectdefinition-contract.md
  "Row Content Migration" table + Stop Condition 2 report — rows carry
  valueType "Undetermined"; names this follow-up (Tidal Barrier, and
  Inferno's / Iron Fang's)
tasks/backlog/TASK-102-implement-card-cast-server-path.md
  §"Remaining Issues" — same suggested follow-up; criterion 457-459 depends
  on it
tasks/completed/TASK-092-resolve-boss-skill-effect-magnitudes.md — precedent:
  magnitudes decided by the owner and recorded into the owning domain doc
tasks/completed/TASK-094-resolve-buff-debuff-duration-consumption-timing.md —
  precedent: decision-input GAMEPLAY-CHANGE task structure, neutral options,
  required decision coverage, BLOCKED-when-unanswered discipline
tasks/completed/TASK-105-change-shield-application-semantics-to-refresh.md —
  the frozen Shield semantics D-4 must respect
```

**ADR check:** no ADR governs Card effect magnitudes; none is needed (a
balance-number change is not architectural — `development/gameplay-change.md`
§3).

---

## Current State

`CARD_RULES.md` §4.1 defines all three MVP Pet Skill Cards with an
authoritative Cost but a prose-only Effect: no number, duration, or scope
exists for any of the six values in §"Decision Questions", in that document
or anywhere else in `docs/` or `src/`. `TASK-109` (DONE) migrated the six
CardDefinition rows to the structured `CardEffectDefinition` contract and
represented the three Pet Skill rows as `valueType: "Undetermined"` with no
`value` member — its own Stop Condition 2 fired and was reported as requiring
this separate content decision, with no value invented. `TASK-108` D-4
records that PetSkillCast stays blocked until it exists. The Basic Cards
(`CARD_RULES.md` §2: 20% / 20% / 25 Power) are fully authored and unrelated.

**Known distractors (verified — must not be treated as the answer):**

```text
* PASSIVE_RULES.md §7 / PET_RULES.md table: Huyền Quy's passive gives
  "Shield = 15% Max HP" — that is the PASSIVE, not the Tidal Barrier card.
* Bạch Hổ's passive "increased Crit chance" (PASSIVE_RULES.md §7) is
  likewise unquantified config-owned text — not D-6's value.
* CARD_RULES.md §2's Shield 20% Max HP is the Basic Shield Card.
* TASK-108 D-2's illustrative shapes (Heal 30, Shield 20, Power 5) are
  SHAPE EXAMPLES, explicitly not authored balance values.
* BOSS_RULES.md §6.3/§6.3.1 (Flame Burst 150, Burn 50/tick for 2 Turns) are
  Boss Skill values with their own owner.
* GDD.md intentionally contains no exact numbers.
```

**Consequence of the gap:** any Pet Skill Card effect resolution is
undeterministic today. This is an `AGENTS.md` §7 / §20 "missing rule" stop
condition and the exact blocker recorded by TASK-104 §5/B-4, TASK-108 D-4,
and TASK-109 Stop Condition 2.

---

## Scope

### In Scope

1. **Present each of D-1…D-6 neutrally**, with the citations above and the
   distractor list, to the Product Owner / requester.
2. **Obtain the owner's explicit answer for every item in §"Required
   Decision Coverage"** and record each answer **verbatim**, with its date
   and its author, in this task's Completion Evidence (the TASK-104 /
   TASK-108 precedent). If any item is unanswered, record which and keep the
   task BLOCKED — do not narrow, rank, or default.
3. **Author the decided values into `docs/01-game-design/CARD_RULES.md` §4.1**
   — the canonical owner (`TASK-108` D-2: "the exact card balance values
   remain owned by CARD_RULES.md") — keeping the existing §4.1 structure
   (Cost + Effect per Card, per the closing paragraph of §4.1) and stating
   each value explicitly enough that a resolver could be implemented from it
   without inference. This is the doc-first step required of every
   GAMEPLAY-CHANGE task (`TASK_TYPES.md` §2 Critical;
   `development/gameplay-change.md` §3).
4. **Confirm single ownership** — no value is copied into `GAME_RULES.md`,
   `GAME_STATE.md`, `GAME_EVENTS.md`, `DATABASE.md`, `PET_RULES.md`,
   `PASSIVE_RULES.md`, `.ai/`, or any task file acting as a rules source
   (`documentation/documentation-change.md` §2 — one concept, one owner).
5. **Report the consequences this decision creates**, each as a separate
   follow-up task (reported, not created or implemented here):
   * the CardDefinition row-content update that replaces the three
     `valueType: "Undetermined"` payloads with the decided values
     (a `FEATURE`-typed migration/seed change consuming this content);
   * the effect-vocabulary question below (Out of Scope item 2);
   * the PetSkillCast lifecycle unblock (an orchestrator/reviewer act under
     `TASK_LIFECYCLE.md` §3/§4 — this task makes no status transition on any
     task but its own).
6. **Record the D-4 lift**: after §4.1 carries the Shield magnitude, state
   explicitly that the TASK-104 §5/B-4 and TASK-108 D-4 content block for
   Tidal Barrier is resolved by this task, so no later reader re-opens it.

### Out of Scope

**Explicit prohibitions (stated as required):**

```text
No client-authoritative state.
No undocumented events or SignalR methods.
No speculative database schema.
No new gameplay mechanic beyond the six missing values.
No balance value invented, recommended, or defaulted by an agent.
```

- **Any value not supplied by the Product Owner.** Choosing, recommending,
  estimating, or "sensibly defaulting" a magnitude is the single prohibited
  act of this task (`AGENTS.md` §7, §20). The task reports itself as
  awaiting input instead.
- **Any source code.** Zero files under `src/` or `tests/`. No resolver, no
  `CardCast` / `PetSkillCast` implementation, no `BattleEvent` member, no
  projection arm.
- **Encoding the values into the structured payload.** No CardDefinition row
  UPDATE, no migration, no `CardEffectDefinition` change — reported as
  follow-up. The rows stay `Undetermined` until that separate task runs.
- **Extending the effect vocabulary.** `CardEffectType` is a closed enum
  (Heal | Shield | Power) and a row carries a single `effectType`
  (TASK-109), while Inferno (damage + Burn) and Iron Fang (damage + Crit)
  — and arguably Tidal Barrier (Heal + Shield) — each express more than one
  effect. **No new enum member, no multi-effect structure, and no re-encoding
  may be designed here.** If the decided content cannot be represented
  without such a change, that is a contract question: STOP and report it as
  its own task (`AGENTS.md` §7, §20; the same prohibition TASK-108 recorded).
- **Any change to Costs** (100 / 80 / 100 — already authored, §4.1), to the
  §2 Basic Card values (20% / 20% / 25), or to the loadout rules (§1).
- **Any frozen contract:** Shield semantics (`COMBAT_RULES.md` §4, TASK-105),
  Damage Pipeline order and Crit multiplier (`COMBAT_RULES.md` §3, §3.3),
  Burn tick timing (`GAME_RULES.md` §17 step 19a, TASK-091), Status Effect
  stacking (`COMBAT_RULES.md` §5.2), Element Modifier rules
  (`ELEMENT_RULES.md` §2/§5).
- **Pet Passive magnitudes** (`PASSIVE_RULES.md` §7/§8, `PET_RULES.md`
  table) — not authored, not altered, not used as a value source.
- **Thanh Xà / Sơn Hùng Signature Skill content** (`CARD_RULES.md` §4.1
  closing paragraph, `PET_RULES.md` §7/§8) — remains TBD/deferred; no
  placeholder, no invented value.
- **Any new Card, Pet, Relic, Element, Status Effect type, resource, or
  progression system**; any change to Match-3, Swap, Combo, Passive, Boss
  Response, or the resolution order. Any ADR (`AGENTS.md` §18 — a balance
  number is not architectural).
- **Modifying any other task file** — `TASK-102`, `TASK-104`, `TASK-106`,
  `TASK-107`, `TASK-108`, and every `tasks/completed/*` file are read-only
  inputs (their status transitions are the orchestrator's act).
- **Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2** or
  unlisted per `MVP_SCOPE.md` §4.

---

## Dependencies

```text
Requires (already satisfied):
  TASK-109  DONE  — structured CardEffectDefinition contract exists; its
                    completion evidence names this content task as the
                    follow-up and left the rows deliberately undetermined.
  TASK-108  DONE  — D-1/D-2 settled the identity/magnitude carrier and D-4
                    points the remaining content gap at a separate task.
  TASK-104  BACKLOG (input) — §5 / B-4 recorded the Tidal Barrier gap and
                    prescribed this task's type. Read-only reference; this
                    task does not edit it.

Unblocks (reported, not transitioned here):
  PetSkillCast implementation — TASK-108 "Follow-Up Tasks Required" item 2
                    and TASK-102's Pet Skill Card criteria depend on §4.1
                    carrying magnitudes.
  CardDefinition row-content update — replaces the three "Undetermined"
                    payloads with the decided values (separate FEATURE task).
  TASK-102 / TASK-107 lifecycle validation — a sequencing decision for the
                    orchestrator/reviewer (TASK_LIFECYCLE.md §3, §4); this
                    task records impact only and changes no other task's
                    status.

Not a dependency of, and does not modify:
  TASK-106, TASK-103, TASK-099, TASK-082 (immutable), TASK-091, TASK-092,
  TASK-094, TASK-105.
```

---

## Acceptance Criteria

- [x] The Product Owner has explicitly supplied a value for every item in
      §"Required Decision Coverage" (7 of 7), each recorded **verbatim** with
      its date and its author in this task's Completion Evidence.
- [x] `CARD_RULES.md` §4.1 states an explicit magnitude for each Pet Skill
      Card effect: Inferno damage **and** Burn amount + duration; Tidal
      Barrier Heal **and** Shield; Iron Fang damage **and** Crit increase +
      scope/duration. No prose-only "high" / "increased" / bare "Heal" /
      bare "Gain Shield" magnitude remains where a number is now required.
- [x] Every decided value carries a stated unit/expression form (flat vs
      % Max HP vs % of a stat) so it is implementable without inference.
- [x] Card Costs remain 100 / 80 / 100 Power and the §2 Basic Card values
      remain 20% / 20% / 25 Power (byte-identical outside the §4.1 effect
      lines and any directly necessary note).
- [x] Burn duration is expressed in the authoritative Turn / End Turn tick
      terms (`GAME_RULES.md` §17 step 19a; `COMBAT_RULES.md` §5.2), and the
      Shield magnitude is stated subject to, not instead of, the frozen
      refresh-not-stack semantics (`COMBAT_RULES.md` §4).
- [x] Single ownership confirmed: no decided value is duplicated into any
      other `docs/` file, `.ai/` file, or task file
      (`documentation/documentation-change.md` §2).
- [x] The distractors in §"Current State" were explicitly checked and none
      was used as (or reconciled with) a decided value.
- [x] The structured-payload follow-up, the effect-vocabulary question, and
      the PetSkillCast unblock are each **reported** in Completion Evidence
      with scope + suggested type; none is implemented here.
- [x] Zero files under `src/` and `tests/` changed; no other task file
      changed; no Status transition made on any task but this one.
- [x] Documentation consistency audit passes for the touched document
      (`CARD_RULES.md` internal cross-references, §4 vs §4.1 vs §6).
- [x] Scope validation against `MVP_SCOPE.md` §1 passes (Cards / Combat IN;
      no new system).
- [x] No authoritative rules or contracts violated (`AGENTS.md` §10 /
      ADR-001); quality review checklist passes
      (`.ai/workflow/quality/review.md` §1).

---

## Affected Files & Areas

```text
[ ] src/backend/ (Domain / Application / Infrastructure / Api)   — NONE
[ ] src/frontend/client/ (scenes / runtime / services / state / ui) — NONE
[ ] tests/ (unit / integration / gameplay scenarios)             — NONE
[x] docs/01-game-design/CARD_RULES.md — §4.1 ONLY (author the six decided
      values into the existing Cost + Effect entries; §1, §2, §3, §5, §6
      byte-identical)
[ ] all other docs/ — NONE (no duplication; no technical doc is affected:
      the payload encoding is the follow-up task's act)
[x] tasks/ — this file only (Decision Record + Completion Evidence)
[ ] tasks/backlog|completed|blocked/* — NONE (all read-only inputs)
[ ] docs/03-decisions/ADR/ — NONE
```

---

## Implementation Notes

- **Canonical owner:** `docs/01-game-design/CARD_RULES.md` §4.1. Ownership of
  Card balance values was settled by `TASK-108` D-2; this task applies it,
  it does not re-decide it.
- **Authorization chain (why this task exists):** TASK-104 §5 "Outstanding
  Content Gap" / B-4 → prescribed "a separate gameplay-content decision task,
  typed `GAMEPLAY-CHANGE`/content"; TASK-102 §"Remaining Issues" repeats the
  suggestion; TASK-108 D-4 + Follow-Up item 2 keeps PetSkillCast blocked
  until it exists; TASK-109 Stop Condition 2 reports the same and names
  "Tidal Barrier's Shield magnitude (and Inferno's / Iron Fang's)".
- **Editing pattern precedent:** TASK-092 recorded decided magnitudes into
  the owning domain doc (`BOSS_RULES.md` §6.3.1) in the owner's own terms,
  with durations expressed against the authoritative Turn model. Follow that
  shape; do not restructure §4.1.
- **Value sources that are FORBIDDEN** (repeated here deliberately):
  `CARD_RULES.md` §2's 20% / 25; `PASSIVE_RULES.md` §7's 15% Max HP and
  unquantified Crit text; `BOSS_RULES.md` §6.3/§6.3.1's 150 / 50 / 120 /
  100; `TASK-108` D-2's illustrative 20 / 30 / 5 shapes; Relic rows
  ("Assassin Eye", "Burning Curse", `RELIC_RULES.md` §7); GDD flavor text.
  If the owner volunteers a number, it is recorded as the owner's decision —
  the point is that the agent never supplies one.
- **Determinism check:** after editing, a reader must be able to answer
  "what happens when this Card is cast" from §4.1 alone plus the cited
  frozen contracts (pipeline, tick timing, Shield semantics) — with no
  inference from name, pet, or flavor.
- **Type/workflow discipline:** `development/gameplay-change.md` §2 — the
  mechanic exists but is unquantified; §3's design-change branch is taken
  only for the six values, into the owning document, after the owner decides.
  §4 (MVP boundary) re-checked at every edit.
- **This task makes no lifecycle transition on any file but its own**; the
  PetSkillCast/TASK-102 readiness consequences are recorded for the
  orchestrator/reviewer (`TASK_LIFECYCLE.md` §3, §4).

---

## Testing Requirements

### Required Verification

```text
[ ] Documentation consistency audit — CARD_RULES.md §4.1 against §4, §6,
    COMBAT_RULES.md §3/§4/§5.2, GAME_RULES.md §17, PASSIVE_RULES.md §7
    (no contradiction introduced; passive values untouched).
[ ] Gap-closure audit — for each of the six values, confirm §4.1 now states
    a number (+ duration/scope where applicable) and that no remaining
    §4.1 effect text leaves a magnitude unquantified.
[ ] Distactor audit — confirm no decided value equals or derives from a
    distractor source merely by proximity (§"Current State" list).
[ ] Ownership audit — grep the decided values across docs/ and confirm a
    single occurrence site (CARD_RULES.md §4.1).
[ ] Scope validation against MVP_SCOPE.md §1 / §2.
[ ] Change-isolation check — hash every other task file and docs/ file
    before/after; only this file and CARD_RULES.md §4.1 may move.
[ ] No test suite run is required (zero code change); if run for baseline,
    the existing suites must be unaffected.
```

### Key Edge Cases

- **Multi-effect Cards:** Tidal Barrier = Heal + Shield; Inferno = damage +
  Burn; Iron Fang = damage + Crit. Each half needs its own value — a single
  number per Card does not close the gap (and the single-`effectType` row
  limitation is reported, not solved — Out of Scope item 2).
- **Burn duration boundary:** duration must be expressible as a whole number
  of End Turn ticks and must not collide with `GAME_RULES.md` §17 step 19a's
  DoT scoping or with TASK-091's timing.
- **Crit scope ambiguity:** "increased Crit chance" without a scope (this
  cast vs next attack vs timed) is not a closed decision — D-6 must name it.
- **Refresh, not stack:** the Tidal Barrier Shield magnitude is a refresh
  target value, not an additive pool (`COMBAT_RULES.md` §4, TASK-105).
- **Partial answer:** any unanswered item in §"Required Decision Coverage"
  keeps the task BLOCKED with the outstanding items listed — never partially
  applied to §4.1 (a half-authored §4.1 is a worse state than prose-only,
  because it looks decided).

---

## Stop Conditions

Universal `AGENTS.md` §20 stops always apply. Task-specific:

```text
*** RESOLVED — the stop fired at pickup and is now CLEARED by the Product
    Owner's answers to all six items (recorded verbatim below).
    CARD_RULES.md §4.1 authors the decided values. Not an active block. ***
Decision:    Any item in §"Required Decision Coverage" has no owner answer.
Status:      AWAITING PRODUCT OWNER INPUT (task remains BLOCKED; §4.1 is
             not edited with a partial or guessed value).
Impact:      PetSkillCast remains blocked (TASK-108 D-4); TASK-102's Pet
             Skill criteria remain unassertable; the three CardDefinition
             rows remain "Undetermined".
Blocked tasks: PetSkillCast implementation; CardDefinition row-content
             update (encoding).

Additional stops:

- If an agent is asked to choose, recommend, estimate, or default any of the
  six values: STOP per AGENTS.md §7 — that is a design act reserved to the
  Product Owner.
- If the decided content cannot be represented in the existing structured
  CardEffectDefinition (single effectType; enum Heal | Shield | Power) or
  would otherwise require a new effectType / multi-effect structure: STOP and
  report the contract change as its own task — do not extend the vocabulary,
  re-encode a row, or change src/ here (AGENTS.md §7, §18, §20).
- If a decision would contradict a frozen contract (COMBAT_RULES.md §3/§4/
  §5.2, GAME_RULES.md §17 step 19a, CARD_RULES.md §2): STOP per AGENTS.md §4
  — report the conflict with both sources; do not silently reconcile.
- If resolving the decision would require authoring Thanh Xà / Sơn Hùng
  Signature Skill content or any Pet Passive magnitude: STOP — broader
  content decision, reported not absorbed.
- If the repository already contains an equivalent open task for these
  magnitudes: STOP and report the duplicate.
- If the required reading reveals the magnitudes are already authored
  somewhere authoritative: STOP and report the citation (do not re-author).
- If applying the decision would require editing a technical contract
  (GAME_STATE.md / DATABASE.md / GAME_EVENTS.md / SIGNALR_PROTOCOL.md):
  STOP — that is the follow-up encoding task's act, not this task's.
- If the task exceeds 7 skills or crosses multiple uncoupled architectural
  boundaries: STOP & decompose (tasks/TASK_TEMPLATE.md §Stop Conditions).
```

### STOP CONDITION REPORT (fired at pickup — 2026-10-01)

Format per `.ai/README.md` §13 / `core/context-discovery.md` §3.

```text
Problem:
  TASK-110 cannot begin its authoring step: none of the six required
  Product Owner decisions (D-1 … D-6) was supplied with the execution
  request, and no value is recoverable from any authoritative document.
  Authoring §4.1 requires a balance value. Choosing, recommending,
  estimating, or defaulting one is the single act this task forbids
  (AGENTS.md §7, §20; TASK-110 §"Stop Conditions" item 1). The task
  therefore halts before any edit — a half-authored §4.1 is worse than
  prose-only, because it looks decided.

Relevant sources:
  docs/01-game-design/CARD_RULES.md §4.1        — THE GAP: prose-only
                                                  Effect for all three Cards;
                                                  Cost authored, magnitude not
  docs/01-game-design/COMBAT_RULES.md §3 step 1 — where D-1/D-5 values land
  docs/01-game-design/COMBAT_RULES.md §3.3      — base Crit 5% (D-6 context)
  docs/01-game-design/COMBAT_RULES.md §4        — Shield semantics (frozen)
  docs/01-game-design/COMBAT_RULES.md §5.1/§5.2 — Burn + status magnitudes
  docs/01-game-design/GAME_RULES.md §17 step 19a— Burn tick timing (frozen)
  docs/01-game-design/PASSIVE_RULES.md §7 (§8)  — DISTRACTOR: Huyền Quy
                                                  passive 15% Max HP, Bạch Hổ
                                                  passive Crit text (not D-4/D-6)
  docs/01-game-design/PET_RULES.md §8           — same table, PET_SKILL column
                                                  only names the Skill
  tasks/backlog/TASK-108-…md D-4, Follow-Up 2   — PetSkillCast stays blocked
  tasks/completed/TASK-109-…md Stop Condition 2 — rows left "Undetermined"
  tasks/backlog/TASK-104-…md §5 / B-4           — "Separate gameplay decision"

Conflict / missing information:
  MISSING RULE, not a conflict. Six magnitudes are unauthored:
    D-1 Inferno damage            — no value, no unit
    D-2 Inferno Burn              — no damage/tick, no duration
    D-3 Tidal Barrier Heal        — no value, no unit
    D-4 Tidal Barrier Shield      — no value, no unit
    D-5 Iron Fang damage          — no value, no unit
    D-6 Iron Fang Crit            — no magnitude, no unit, no scope, and no
                                    statement of independence from, or sharing
                                    with, Bạch Hổ's Passive config value
  Verified exhaustive (not assumed):
    - CARD_RULES.md §4.1 is prose-only for all three Cards.
    - No magnitude for any of the six exists in any docs/ file.
    - No magnitude exists in src/ or tests/ (the three provisioned rows carry
      valueType "Undetermined" with no value member — TASK-109).
    - No Product Owner answer for TASK-110 was found anywhere in the repo:
      the "Product Owner Decisions" heading in TASK-110 is still the unfilled
      template placeholder, and no other task file records these six values.
  No distractor may be promoted to an answer (TASK-110 §"Current State"): the
  §2 Basic Card values (20% / 20% / 25), Huyền Quy's Passive (15% Max HP),
  the TASK-108 illustrative shapes (20 / 30 / 5), and the Boss values
  (150 / 120 / 100) are explicitly not sources for Pet Skill Card balance.

Impact:
  Unchanged from the recorded gap: PetSkillCast stays blocked (TASK-108 D-4);
  TASK-102's three Pet Skill Card acceptance criteria stay unassertable; the
  three CardDefinition rows stay "Undetermined" and are not resolvable.
  No authoritative document was modified, so no inconsistency was introduced.

Proposed resolution:
  None proposed by the agent (proposing a balance value is the prohibited
  act). The Product Owner supplies a verbatim answer for each of D-1 … D-6,
  including for each value its expression form (flat / % Max HP / % of a named
  stat) and, for D-2, a duration in Turns, and, for D-6, its scope and its
  relationship to the Passive config value. TASK-110 then resumes from
  BLOCKED → IN PROGRESS and authors §4.1 only.

Waiting for:
  The Product Owner's explicit answers to D-1 … D-6 (all six; §"Required
  Decision Coverage" items 1–7). Any unanswered item keeps this task BLOCKED.
```

### Unanswered Items Register — CLEARED (retained for audit)

At pickup every item below was unanswered and the task was blocked. All six
were subsequently supplied by the Product Owner; the register is kept so the
resolution is traceable. Verbatim answers are in §Completion Evidence.

```text
D-1  Inferno damage              ANSWERED — "100 flat Fire damage. This is the
                                 Card base value entering the Damage Pipeline."
D-2  Inferno Burn                ANSWERED — "Burn deals 50 damage per End Turn
                                 tick for 2 Turns."
D-3  Tidal Barrier Heal          ANSWERED — "Heal the active Pet for 20% of its
                                 Max HP."
D-4  Tidal Barrier Shield        ANSWERED — "Grant Shield equal to 20% of the
                                 active Pet's Max HP. Existing refresh-not-stack
                                 semantics apply."
D-5  Iron Fang damage            ANSWERED — "120 flat damage. This is the Card
                                 base value entering the Damage Pipeline."
D-6  Iron Fang Crit              ANSWERED — "Increase Crit chance by 10
                                 percentage points for the next attack only. The
                                 Card's Crit increase is independent of Bạch Hổ's
                                 Passive configuration value."

Coverage items answered:   7 of 7
CARD_RULES.md §4.1 edited: YES — authored from the verbatim answers above
Task status:               BLOCKED (cleared) → DONE
```

---

## Completion Evidence

### Product Owner Decisions (recorded verbatim, per the TASK-104 / TASK-108 precedent)

```text
Recorded by:  Product Owner (via requester)   Date: 2026-10-01

D-1 Inferno damage:
"100 flat Fire damage. This is the Card base value entering the Damage Pipeline."

D-2 Inferno Burn:
"Burn deals 50 damage per End Turn tick for 2 Turns."

D-3 Tidal Barrier Heal:
"Heal the active Pet for 20% of its Max HP."

D-4 Tidal Barrier Shield:
"Grant Shield equal to 20% of the active Pet's Max HP. Existing
refresh-not-stack semantics apply."

D-5 Iron Fang damage:
"120 flat damage. This is the Card base value entering the Damage Pipeline."

D-6 Iron Fang Crit:
"Increase Crit chance by 10 percentage points for the next attack only. The
Card's Crit increase is independent of Bạch Hổ's Passive configuration value."
```

Answers supplied: **6 of 6** (D-1 … D-6). Coverage items resolved: **7 of 7**
(§"Required Decision Coverage" items 1–7). No item was paraphrased,
interpreted, defaulted, ranked, or inferred — each is recorded exactly as the
Product Owner supplied it, and each carries its own expression form (flat vs
% Max HP), duration (D-2, in Turns), or scope (D-6, next attack only).

### Changed Files

- `docs/01-game-design/CARD_RULES.md` — **§4.1 only**, plus the document's own
  version header (1.4 → 1.5) per its established convention. The three Pet
  Skill Cards now carry their decided magnitudes inside the preserved
  Cost + Effect structure; the D-6 independence note was added immediately
  after the §4.1 block. §1, §2, §3, §5, and §6 are byte-identical.
- `tasks/completed/TASK-110-author-pet-skill-card-effect-magnitudes.md` — this
  file: `Status` → `DONE`; Product Owner decisions recorded verbatim;
  completion evidence, validation results and downstream consequences
  recorded.

No other file was created, modified, moved, or deleted.

### What §4.1 now states (determinism check)

A reader can answer "what happens when this Card is cast" from §4.1 alone plus
the cited frozen contracts, with no inference from name, Pet, or flavor:

```text
Inferno      → 100 flat Hỏa damage as the Card/Skill base value (pipeline
               step 1) + Burn 50/tick for 2 Turns at End Turn (step 19a)
Tidal Barrier→ Heal active Pet 20% Max HP + Shield 20% Max HP
               (refresh-not-stack, one instance)
Iron Fang    → 120 flat damage as the Card/Skill base value (pipeline step 1)
               + Crit +10 percentage points, next attack only, independent of
               Bạch Hổ's Passive config value
```

No prose-only "high" / "increased" / bare "Heal" / bare "Gain Shield" magnitude
remains in §4.1.

### Reported Consequences (REPORTED, NOT DONE HERE)

```text
1. CardDefinition row-content update — encode the six decided values into the
   three structured payloads (replace valueType "Undetermined"). Type:
   expected FEATURE (+ migration/seed). Blocks: Pet Skill effect resolution.
   The three rows were deliberately left "Undetermined" by this task
   (TASK-110 §11 — do not encode the values here).

2. Effect-vocabulary question — REQUIRED, and NOT resolved by this task.
   The authored content cannot be represented by the existing structured
   contract for two of the three Cards:
     Inferno      = damage + Burn        → damage/Burn are not in the enum
     Iron Fang    = damage + Crit        → damage/Crit are not in the enum
     Tidal Barrier= Heal + Shield        → Heal and Shield ARE in the enum, but
                                           a row carries a single effectType
   CardEffectType is the closed set Heal | Shield | Power and a row carries a
   single effectType with a single value (TASK-109; DATABASE.md §1). This is
   the contract gap TASK-110 §8 requires to be reported rather than solved.
   Type: contract decision task. No enum member, multi-effect structure,
   CardEffectDefinition, CardDefinition, migration, or source file was changed
   here.
   NOTE for the owner: the Burn magnitude "50/tick for 2 Turns" and the Crit
   magnitude "+10 percentage points" also have no slot in the current
   valueType vocabulary (Flat | PercentMaxHp | Undetermined) even if the enum
   were extended, so the vocabulary question and the multi-effect question
   must be decided together.

3. PetSkillCast unblock / TASK-102 lifecycle — the gameplay-content blocker is
   lifted by this task, but the implementation lifecycle is the
   orchestrator's/reviewer's act (TASK_LIFECYCLE.md §3, §4). This task made no
   status transition on any task but its own. TASK-108 D-4's "Tidal Barrier
   remains blocked" content condition, and TASK-104 §5/B-4's deferred
   "separate gameplay decision", are both RESOLVED by the D-3/D-4 answers
   recorded above — no later reader should re-open the Tidal Barrier Shield
   magnitude.
```

### Validation Results

- **Gap closure** — PASS. All six values now have an explicit magnitude, unit,
  target and duration/scope in `CARD_RULES.md` §4.1.
- **Documentation consistency** — PASS. `CARD_RULES.md` §4 / §4.1 / §6,
  `COMBAT_RULES.md` §3 / §3.3 / §4 / §5.1 / §5.2, `GAME_RULES.md` §17 step 19a,
  `PASSIVE_RULES.md` §7/§8, `PET_RULES.md` §2.3/§8, `ELEMENT_RULES.md` §5
  verified mutually consistent. No contradiction introduced:
  - Shield semantics **unchanged** — the §4.1 Shield is stated *subject to*
    `COMBAT_RULES.md` §4's refresh-not-stack rule, which is cited, not restated.
  - Burn timing **unchanged** — §4.1 cites `GAME_RULES.md` §17 step 19a /
    `COMBAT_RULES.md` §5.1–§5.2 rather than authoring a new tick rule.
  - Damage Pipeline **unchanged** — D-1/D-5 enter it as the step-1 base value;
    no step, order, or formula was touched.
  - Crit multiplier and base Crit **unchanged** — `COMBAT_RULES.md` §3.3 intact.
  - Pet Passive magnitudes **unchanged** — `PASSIVE_RULES.md` §8 still reads
    "Huyền Quy … Shield = 15% Max HP" and "Bạch Hổ … increased Crit chance";
    D-6 explicitly records the Card value as independent of the Passive config.
- **Ownership** — PASS (single owner). Each decided value occurs at one
  authoritative site, `CARD_RULES.md` §4.1. The coincidental equality with
  values owned elsewhere is not duplication: `BOSS_RULES.md` §6.3.1's
  150 / 50 / 120 / 100 are Boss Skill values for a different entity, and
  `PASSIVE_RULES.md` §8's 15% Max HP is a different effect. No value was
  copied into `GAME_RULES.md`, `GAME_STATE.md`, `GAME_EVENTS.md`,
  `DATABASE.md`, `PET_RULES.md`, `PASSIVE_RULES.md`, `SIGNALR_PROTOCOL.md`,
  `API_CONTRACTS.md`, `.ai/`, or any task file.
- **Distractor audit** — PASS, with two coincidences explicitly permitted by
  TASK-110 §7. `CARD_RULES.md` §2's 20% / 20% / 25, `PASSIVE_RULES.md` §8's
  15% Max HP, TASK-108's illustrative 20 / 30 / 5, and `BOSS_RULES.md`
  §6.3.1's 150 / 50 / 120 / 100 were each located and each was confirmed NOT
  to be the source. Two decided values coincide numerically with a distractor
  (D-2's Burn 50/tick for 2 Turns with Hỏa Long's Boss Burn; D-1's 100 with
  Boss Root's 100; D-3/D-4's 20% with §2's Basic Heal/Shield 20%): TASK-110 §7
  states that an independent Product Owner choice of an equal value is
  acceptable, and each of these was supplied directly by the owner in the
  answers above. No value was derived from, or reconciled with, a distractor.
- **Change isolation** — PASS. SHA-256 baseline recorded at pickup for all 14
  authoritative documents; post-change, **only `CARD_RULES.md` moved**. All 13
  others are byte-identical. `git diff` hunks for `CARD_RULES.md` are confined
  to the version header (line 3) and §4.1 (lines 152–188).
- **Scope validation** — PASS. `MVP_SCOPE.md` §1: Cards and Combat/Status
  Effects are IN. No new system, mechanic, Card, Pet, Relic, Element, Status
  Effect, resource, or progression axis was introduced; no OUT item reached.
- **Server authority** — PASS. Values are server-resolved gameplay content
  (`ADR-001`, `GAME_RULES.md` §18); no client-authoritative logic was added
  (no code was touched at all).
- **ADRs** — PASS, none required and none modified. A balance-value authoring
  is not architectural (`development/gameplay-change.md` §3). No ADR addresses
  Card effect magnitudes.
- **Tests** — N/A, and correctly so: zero files under `src/` or `tests/`
  changed, so no test suite was run and none could regress. The scenario
  coverage these values enable belongs to the follow-up implementation task.

#### Change-isolation baseline (SHA-256, recorded at pickup and re-verified)

```text
F08C2490306ED662A25E569EA5D9F1D25B6AA486BFDA4C568F4D5F02C690D82F  docs/01-game-design/CARD_RULES.md          -> MOVED (authored §4.1 + header)
258B12A3E53962AD3983DFE6E9895FE4AD07762EF1997FFFF5FDA70802EF0EF6  docs/01-game-design/COMBAT_RULES.md        byte-identical
7514786E52AFE7774CA64451275C66A3CA7BEE8AF2B9AC17D4C0CA96CD143C60  docs/01-game-design/GAME_RULES.md          byte-identical
D8F9ACFA698E8769EFFC67A2ECD47FDE7DE97021D2AB89546930F276B059031C  docs/01-game-design/PASSIVE_RULES.md       byte-identical
0FA64FD5CE9CFB6280B1093E4052F3C17795CE0A79D2C33EDA590D1ED1769255  docs/01-game-design/PET_RULES.md           byte-identical
3784E6EAC2D355B26435B3D6906894AF822572B870C1858D73B8CCD6AA74E39A  docs/01-game-design/BOSS_RULES.md          byte-identical
03C43D44FCA163D0C65B63EFFE876D3203AA2039F0E7EB97C33E1C7B6D35E556  docs/01-game-design/ELEMENT_RULES.md       byte-identical
A7155BF78547F5162397023493ABC9B141D1E04FE2F626E77455E039D511ED1A  docs/01-game-design/RELIC_RULES.md         byte-identical
31FDB733BF587235D8BADF07161EAB3E73B1A8B2B7D5FBAFB82231475A9329BA  docs/00-overview/MVP_SCOPE.md              byte-identical
7004B262B7B4CFDDA7FE479038D9C281299040FF983CDFF828AABE6C6481609B  docs/02-technical/GAME_STATE.md            byte-identical
454A6F82611B927F0C4987A9014ECBE75690AC93147225A490B5C43CF27E75CE  docs/02-technical/GAME_EVENTS.md           byte-identical
7365CED86160EFE9FEFF3E819F490A28F911F3FEC18AF20462D8C4BFC333A90A  docs/02-technical/DATABASE.md              byte-identical
AE4FD980C6D5444148093A1BA76831586D45ABA9263A50DDAB447177B4CB2AA1  docs/02-technical/SIGNALR_PROTOCOL.md      byte-identical
AEC7E56D0D0048FCEF940DCA5BF218FC2A83080417A96BCE49A6BBE0FF53AE1F  docs/02-technical/API_CONTRACTS.md         byte-identical
```

### Server Authority & Scope Verification

- [x] Confirmed zero client-authoritative gameplay logic (no code touched)
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
- [x] Confirmed no balance value was invented, recommended, estimated, ranked,
      or defaulted by an agent — every recorded value traces to a verbatim
      Product Owner answer (6 of 6)
- [x] Confirmed the distractor list was explicitly checked and no decided value
      was derived from, or reconciled with, a distractor
- [x] Confirmed no source code or test file changed, and no other task file was
      modified
- [x] Confirmed no enum, `CardEffectDefinition`, `CardDefinition`, EF
      configuration, seed data, migration, or database row was changed — the
      three rows still carry `valueType: "Undetermined"`
- [x] Confirmed no burned/frozen contract was changed (Shield semantics, Burn
      timing, Damage Pipeline, Crit multiplier, status stacking, Element
      Modifiers, Pet Passive magnitudes)
- [x] Confirmed `CARD_RULES.md` §4.1 is the sole owner of the authored values
- [x] Confirmed this task made no status transition on any task but its own
