# TASK-082 — Resolve Pet/Card/Relic Static Content Provisioning Contract (D3)

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.

  THIS TASK PRESENTS THE DOCUMENTED EVIDENCE AND REQUIRES THE APPROPRIATE
  HUMAN/PRODUCT DECISIONS. It does not author content. Inventing Pet, Card,
  Relic, Passive or Skill identifiers, values, effects, magnitudes, row counts,
  or acquisition rules is the single prohibited action of this task
  (AGENTS.md §7, §20).

  Dependency classification recovered from:
    TASK-079 §"Separate Dependencies Identified"  → D3 origin
    TASK-080 §5 "Follow-up Dependencies"          → D3 status UNRESOLVED
    docs/02-technical/DATABASE.md §5 item 4       → owner of the gap
-->

---

## Metadata

```text
Task ID:           TASK-082
Type:              DOCUMENTATION
Status:            DONE
Risk:              MEDIUM
Priority:          HIGH (P1 — closes D3, a named dependency of TASK-078)
Primary Agent:     review (documentation consistency; this task records the
                   content/provisioning decisions, no domain agent may author
                   the answers)
Supporting Agents: persistence, backend, gameplay
Workflow:          documentation/documentation-change.md
Skills:            discovery/documentation-discovery, discovery/impact-analysis,
                   quality/documentation-consistency,
                   backend/persistence-analysis,
                   quality/scope-validation
                   (5 skills — Normal budget, tasks/README.md §12)
Dependencies:      TASK-045 (DONE — §6 issue 1 recorded this gap as open),
                   TASK-046, TASK-049 (DONE — Boss identity / persistence-key
                   precedent this task may cite but not extend by fiat),
                   TASK-052, TASK-053 (DONE — Boss provisioning mechanism and
                   its implementation; precedent, explicitly scoped to
                   BossDefinition only),
                   TASK-070, TASK-071 (DONE — collection read endpoints read
                   these tables; empty ⇒ `200 []`),
                   TASK-079 (BLOCKED — origin of D3; read-only context,
                   AGENTS.md §16; NOT modified),
                   TASK-080 (DONE — D1 decision; D4 context; NOT modified),
                   TASK-078 (BACKLOG — dependent; report readiness impact
                   only; NOT modified)
                   D2 (selection-state ownership — separate, NOT created,
                   NOT resolved here), D4 (which Boss the MVP hardcodes —
                   separate, NOT created, NOT resolved here)
Estimate:          Normal (5 skills; documentation/decision only — no code,
                   no tests, no endpoints, no migrations authored here)
```

**Status note.** Started as `BACKLOG`, not `BLOCKED`. Per `tasks/TASK_LIFECYCLE.md` §2,
`BACKLOG → BLOCKED` is not a valid transition, and an unanswered decision set
is this task's normal starting state — it is what the task exists to collect
(`tasks/TASK_LIFECYCLE.md` §3). Lifecycle: `BACKLOG → READY →
IN PROGRESS → IN REVIEW → DONE` (review checklist `quality/review.md` §1 passed;
documentation-only validation at `core/validation.md` §2 MEDIUM depth).
File moves to `tasks/completed/`.

**Type classification note.** `DOCUMENTATION`, not `FEATURE`/`ARCHITECTURE`.
The deliverable is a set of content/persistence decisions plus their record in
the canonical owner document(s). `tasks/TASK_TYPES.md` selects DOCUMENTATION
when documentation is the primary output; code follows only in a separate,
later implementation task (not created here).

---

## Objective

Resolve, from authoritative documents and human/product answers only, the
`PetDefinition` / `CardDefinition` / `RelicDefinition` **static-content
provisioning contract** that `docs/02-technical/DATABASE.md` §5 item 4 records
as open (per `tasks/completed/TASK-045-…` §6 issue 1), so that a later
provisioning implementation task can be written without inventing content.
Concretely, this task must make the five decision points in §"Decision Inputs"
explicit, evidenced against `docs/`, and answered by a human (or recorded as
explicitly deferred), and must record each settled answer in its single
canonical owner document — without authoring any identifier, value, effect,
row count, or ownership rule.

---

## Authoritative References

- `docs/02-technical/DATABASE.md` §5 item 4 — the gap itself: no provisioning
  mechanism is defined for `PetDefinition`, `CardDefinition`,
  `RelicDefinition`; none may be assumed.
- `docs/02-technical/DATABASE.md` §1 — entity definitions for `Pet`, `PetDefinition`,
  `CardDefinition`, `PlayerUnlockedCard`, `RelicDefinition`, `Relic` (including
  the open Relic ownership-storage-shape note), §2 ownership model,
  §3 constraints.
- `docs/00-overview/MVP_SCOPE.md` §1 — content counts (5 Pets, 3 Basic Cards +
  5 Pet Skill Cards, "~10 Relics"), §4 — unlisted content is not implicitly IN.
- `docs/01-game-design/PET_RULES.md` §1 (Identity wording), §2 (ownership),
  §6 (stat composition), §8 (5 MVP Pets; 2 Signature Skills "TBD content").
- `docs/01-game-design/CARD_RULES.md` §1 (ownership, `LoadoutCopyLimit`,
  item 5 defers concrete values), §2 (3 Basic Cards), §4/§4.1 (Pet Skill Cards;
  2 not yet content-defined).
- `docs/01-game-design/RELIC_RULES.md` §2 (ownership/equipment), §6 (MVP Relic
  reference — 5 entries listed).
- `docs/01-game-design/PASSIVE_RULES.md` §8 (5 MVP Pet Passives; magnitudes
  "live in config, not in this document").
- `docs/02-technical/API_CONTRACTS.md` §3 (`petId`/`cardLoadout`/`relicLoadout`
  semantics), §5.1–§5.4 (which id each collection response exposes).
- `docs/01-game-design/BOSS_RULES.md` §6.4 — the existing identity-contract
  precedent (three non-collapsed concepts; example values are contracts there,
  unlike the Pet/Card/Relic examples elsewhere).
- `tasks/TASK_TEMPLATE.md`, `tasks/README.md` §12 — manifest form and skill budget.
- `docs/AGENTS.md` §4 (conflict resolution), §7 (no invented rules/content),
  §9 (anti-overengineering), §17 (documentation change rule), §20 (stop
  conditions).

---

## Decision Inputs

> Present each question with its evidence and candidate owner. **Do not answer
> any question by inference.** Record the human/product answer verbatim,
> including an explicit "not yet decided" — recorded as deferred, never as an
> agent-chosen value (`tasks/completed/TASK-080-…` §6).

### Decision A — Provisioned row set (which content rows exist)

**Evidence.** `MVP_SCOPE.md` §1 states the scope counts (5 Pets; 3 Basic +
5 Pet Skill Cards; "~10 Relics"). `PET_RULES.md` §8 lists 5 MVP Pets.
`CARD_RULES.md` §2 defines 3 Basic Cards; §4.1 defines 3 of the 5 Pet Skill
Cards and states Thanh Xà and Sơn Hạc Signature Skills "are not yet
content-defined". `RELIC_RULES.md` §6 lists 5 MVP Relics against
`DATABASE.md` §1's "~10 MVP Relics". Precedent for separating scope target
from provisioned rows: `TASK-045` decision E → `DATABASE.md` §1 note item 5
("the 5-Boss figure is the MVP scope target … not permission to create
placeholder rows for undefined content").

**Question.** Exactly which rows may be provisioned for each of the three
tables — scope target, content-defined rows, or another stated set — and how
is the "~10 Relics vs 5 listed" and "5 Pet Skill Cards vs 3 defined" gap
classified (scope target, deferred content, or something else)?

**Candidate owner.** `docs/02-technical/DATABASE.md` §5 item 4 (with the
counts remaining owned by `MVP_SCOPE.md` §1 — no count is restated or changed
here).

### Decision B — Persistence keys and technical identities

**Evidence.** `DATABASE.md` §1 defines `PetDefinitionId`, `CardDefinitionId`,
`RelicDefinitionId` as primary keys but fixes no value form and no canonical
values (contrast `BossDefinitionId`, fixed by `TASK-049` as
`boss-def-<ascii-kebab-case-name>` with three canonical values, and `BossId`
`boss-<ascii-kebab-case-name>` fixed by `BOSS_RULES.md` §6.4).
`API_CONTRACTS.md` §5.3 binds `cardId` = `CardDefinition.CardDefinitionId`
as the value submitted in `cardLoadout` (§3), and §3's example shows
`"heal" | "shield" | "power_charge"`; `RELIC_RULES.md` §2.5-area examples use
`"relic-a"`; `PET_RULES.md` §1 says `Identity (unique name/id, e.g. "Thanh Xà")`
while `API_CONTRACTS.md` §5.1 binds `identity` = `PetDefinition.Identity` as
the value `"Xích Lang"`. `BOSS_RULES.md` §6.4 cites a Pet PassiveId example
(`PassiveId("xich-lang")`), but no document fixes Pet `passiveId` values.
A grep of `docs/` finds no canonical `PetDefinitionId` / `CardDefinitionId` /
`RelicDefinitionId` / Pet `passiveId` value set.

**Question.** For each of `PetDefinitionId`, `CardDefinitionId`,
`RelicDefinitionId`, and the Pet `passiveId` carried by
`PetDefinition.PassiveDefinition` (and referenced by
`PetDefinition.SignatureSkillCardId`): what value form and what canonical
values are fixed, and which document owns them? Is `PetDefinition.Identity`
display text (as `API_CONTRACTS.md` §5.1 currently projects it) or a technical
identity — i.e. does a Pet technical identity concept exist at all, and if so
how is it kept distinct from display name and persistence key (the
TASK-046 three-way non-collapse rule)?

**Candidate owner.** The domain rule documents for their own identities
(`PET_RULES.md`, `CARD_RULES.md`, `RELIC_RULES.md` — mirroring
`BOSS_RULES.md` §6.4) and `docs/02-technical/DATABASE.md` §1 for persistence
keys; `API_CONTRACTS.md` §5.3/§5.1 may only be corrected if the answer makes a
binding member stale (per `AGENTS.md` §4, stop and report that first).

**Guard.** Example strings in `API_CONTRACTS.md` §3, `RELIC_RULES.md`, and
`PET_RULES.md` §1 are **examples, not contracts** — precedent:
`TASK-080` §6 (an example is not an assignment).

### Decision C — Definition member values that no document defines

**Evidence.** `CardDefinition.LoadoutCopyLimit` is required with an explicit
value and "no default"; "concrete values are content/balance configuration …
this document defines no values" (`CARD_RULES.md` §1 item 5; `DATABASE.md` §1
changelog 1.5). `PetDefinition.SignatureSkillCardId` is a required FK
(`DATABASE.md` §1) whose targets for Thanh Xà and Sơn Hạc do not exist
(`CARD_RULES.md` §4.1, `PET_RULES.md` §8). Pet Passive magnitudes (Burn,
Defense, Crit increase) are "balance values [that] live in config"
(`PASSIVE_RULES.md` §8). Pet stat function `f` is "a balance/config concern"
(`PET_RULES.md` §6).

**Question.** For every column whose value is not sourced by an authoritative
document, what is the sourcing rule: which document owns the value, who
supplies it, and what happens to a row whose required member has no defined
value (may it be provisioned at all)? This includes `LoadoutCopyLimit` values,
the two undefined Signature Skill Cards, and Passive/Effect magnitudes.

**Candidate owner.** `docs/02-technical/DATABASE.md` §1/§5 item 4 for the
provisioning rule; the value owners remain the domain rule documents
(`CARD_RULES.md` §1/§4.1, `PET_RULES.md` §8, `PASSIVE_RULES.md` §8,
`RELIC_RULES.md` §6). If a value does not exist yet, the answer must be a
deferral decision, not an invented number.

### Decision D — Provisioning mechanism for the three tables

**Evidence.** `DATABASE.md` §5 item 4: no mechanism is defined for these
tables; "No `HasData`, seed, or startup loader exists for any of them."
`TASK-052`'s migration-INSERT decision is explicitly scoped: "no other content
table's provisioning (`PetDefinition`, `CardDefinition`, `RelicDefinition`, …)
is decided here, and each remains open (§5 item 4)." `GameDbContext.cs`
carries the same statement as a comment (non-authoritative, read-only).

**Question.** Is the `TASK-052` EF Core migration-INSERT mechanism extended
verbatim to these three tables (including its determinism, idempotency,
content-defined-rows-only, and availability-ordering properties), or is a
different mechanism defined? If extended, is any availability/ordering
guarantee required for these tables (compare the Boss "before first
`BattleResult` write" rule), and what documented failure behaviour results
when rows are absent — without inventing a new gate, health check, or error
code (`AGENTS.md` §9)?

**Candidate owner.** `docs/02-technical/DATABASE.md` §5 item 4 (as it already
owns the mechanism statement for `BossDefinition`).

### Decision E — Scope boundary: Player ownership rows

**Evidence.** `API_CONTRACTS.md` §3 validates ownership for `petId`,
`cardLoadout` (a `PlayerUnlockedCard` row), and `relicLoadout` (owned
instances). `DATABASE.md` §1/§2 define the ownership tables, and §1 marks the
Relic ownership storage shape as still OPEN. A grep of `docs/` finds **no**
rule for how a Player's first owned Pet/Card/Relic rows come to exist
(no grant, starter, acquisition, or unlock-on-creation rule; `MVP_SCOPE.md` §2
excludes Gacha/Trading).

**Question.** Is "how a Player's initial owned rows come to exist" inside this
contract's scope, or is it a separate design task? Record the answer either
way. If separate, report it as a distinct unresolved dependency — do not
design an acquisition flow here.

**Candidate owner.** `docs/02-technical/DATABASE.md` §2 (ownership model) if
in scope; otherwise report-only, with the owning document identified but not
edited.

---

## Product Owner Answers (recorded verbatim — 2026-09-29)

> Reproduced exactly as supplied by the Product Owner. Recording an answer
> does **not** settle it: four answers collide with authoritative documents or
> leave a asked sub-question unanswered (§"Clarifications Required" below), so
> per `AGENTS.md` §4 **no owner document is edited** until those are answered.

### Decision A — Provisioned content

> Pets:
> - Provision the minimum explicitly defined MVP Pet set.
> - Do not invent additional Pets.
> - Use the Pet definitions already present in the authoritative game-design docs.
>
> Cards:
> - Provision the 3 explicitly defined MVP Skill Cards.
> - Do not invent the remaining 2 cards merely to satisfy the "5 cards" target.
>
> Relics:
> - Provision the 5 explicitly defined MVP Relics.
> - Do not invent additional Relics merely to satisfy the "~10 Relics" target.
>
> Rule:
> The MVP provisioned content is limited to content that already has authoritative
> gameplay definitions. Undefined content remains out of scope until separately
> defined.

### Decision B — Technical identities

> Use stable explicit string IDs.
>
> Format:
> - PetDefinitionId: `pet-{slug}`
> - CardDefinitionId: `card-{slug}`
> - RelicDefinitionId: `relic-{slug}`
> - PassiveId: `passive-{slug}`
>
> IDs must be unique, deterministic, human-readable, and stable across
> environments and migrations.
>
> Do not use database-generated IDs as the gameplay/content identity.

### Decision C — Undefined values

> LoadoutCopyLimit:
> - Use the value explicitly defined by CARD_RULES.md.
> - Do not create a new value.
>
> Signature Skills:
> - Keep the two currently undefined Signature Skills as TBD.
> - Do not invent their gameplay/content values in TASK-082.
>
> Passive magnitudes:
> - Keep undefined passive magnitudes as TBD.
> - Do not invent numerical values in TASK-082.

### Decision D — Provisioning mechanism

> Use EF Core migration-based provisioning/seed data.
>
> The provisioned Pet/Card/Relic definition rows must be created deterministically
> through the database migration/provisioning mechanism already used by the
> project.
>
> Do not introduce a startup seed runner, external content service, JSON content
> pipeline, or new persistence mechanism.

### Decision E — Initial Player ownership

> For MVP, provision a deterministic starter loadout for the Player so the
> battle-start flow can be exercised without implementing a separate acquisition
> system first.
>
> The starter ownership must reference only the provisioned, authoritative
> content from Decision A.
>
> Initial ownership:
> - 1 MVP Pet
> - 3 MVP Cards
> - 3–5 MVP Relics, according to the existing loadout rule
>
> No gacha, shop, drop table, reward system, or progression acquisition system
> is introduced by TASK-082.
>
> The exact starter item IDs must be selected from the authoritative provisioned
> content and recorded in the follow-up implementation task; no new gameplay
> content may be invented.

**Settled as recorded (no conflict):** Decision D — matches `TASK-052`'s
migration-INSERT precedent and `DATABASE.md` §5's mechanism framing.
**Answered but scoped:** Decision A Relics (5 content-defined rows vs
`MVP_SCOPE.md` §1 "~10" target — `TASK-045` §5 decision E / `DATABASE.md` §1
note item 5 precedent), Decision E composition (1/3/3–5; "the existing loadout
rule" = `GAME_RULES.md` §13 item 2 / `RELIC_RULES.md` §2 `relicLoadout`
3–5; `CARD_RULES.md` §1 exactly 3 Basic + 1 Pet Skill).

---

## Clarification Rounds (AGENTS.md §4 — raised, then answered)

### Round 1 (2026-09-29) — conflicts detected, asked, answered

### C1 — Decision C cites a value `CARD_RULES.md` does not define

- **Answer:** "Use the value explicitly defined by CARD_RULES.md."
- **Conflict:** `docs/01-game-design/CARD_RULES.md` §1 item 5 states verbatim:
  "Concrete limit values are content/balance configuration, owned by a future
  balance/content task; **this document defines no values**."
  `docs/02-technical/DATABASE.md` §1 marks `LoadoutCopyLimit` "required …
  explicit value required, no default"; `CardDefinitionConfiguration`
  maps it non-nullable with no `HasDefaultValue` (a substituted 1 or 3 would
  be the invented default the contract forbids).
- **Effect:** every `CardDefinition` row — hence Decision A's cards and
  Decision E's starter 3 cards — is unprovisionable until a value exists or
  the row set is explicitly deferred. **Resolution requires the value itself
  (human-supplied, then owned by `CARD_RULES.md`) or an explicit deferral.**
  Agent must not supply a number (`AGENTS.md` §7).

### A1 — Decision A's card set vs. the documented battle-loadout composition

- **Answer:** "Provision the 3 explicitly defined MVP Skill Cards."
- **Gap:** the submitted loadout is **exactly 3 Basic Cards + 1 Pet Skill Card**
  (`CARD_RULES.md` §1; `API_CONTRACTS.md` §3 `cardLoadout` = exactly 3 Basic
  Cards; `GAME_STATE.md` §2.3). `CARD_RULES.md` §2 content-defines exactly 3
  Basic Cards (Heal, Shield, Power Charge). If the answer is read as *only*
  the 3 defined Pet Skill Cards, no valid `cardLoadout` can ever be submitted
  and Decision E's "3 MVP Cards" cannot exist; if read as *including* the 3
  Basic Cards, "the remaining 2 of the 5-card target" excludes only Thanh Xà
  and Sơn Hạc's Skill Cards. Both readings are consistent with the wording —
  **the provisioned CardDefinition row set must be stated explicitly.**

### A2 — Decision A's Pet set vs. required `SignatureSkillCardId`

- **Answer:** "Provision the minimum explicitly defined MVP Pet set … Use the
  Pet definitions already present in the authoritative game-design docs"
  (`PET_RULES.md` §8 lists 5 Pets, 2 with Signature Skill "TBD content").
- **Conflict:** `PetDefinition.SignatureSkillCardId` is **required and
  non-nullable** (`DATABASE.md` §1; `PetDefinitionConfiguration`
  `.IsRequired()` + FK `Restrict`; `CARD_RULES.md` §4.1 — a Pet whose Skill
  Card cannot be resolved is invalid definition data, reported, never
  substituted). The Thanh Xà and Sơn Hạc skill-card target rows do not exist
  (`CARD_RULES.md` §4.1) and may not be invented (Decision A rule). Therefore
  those 2 Pets cannot be provisioned in the same migration as the answer's
  "content-defined rows only" rule — **the Pet row set must be stated
  explicitly (and the 2 Pets deferred until their Skill Cards are defined,
  or a separate decision made).**

### B1 — Decision B's `passive-{slug}` format vs. existing Boss PassiveIds

- **Answer:** "PassiveId: `passive-{slug}` … stable across environments and
  migrations."
- **Conflict (if read as global):** `docs/01-game-design/BOSS_RULES.md` §6.4
  fixes canonical Boss PassiveIds `boss-hoa-long-rage`,
  `boss-thuy-ma-heal`, `boss-moc-yeu-regen` — already implemented and
  emitted by the Boss config (TASK-047). A universal `passive-{slug}` rule
  would contradict an authoritative, shipped contract, and Boss content is
  explicitly Out of Scope for this task. **Confirm the format applies to the
  Pet passives provisioned under Decision A only.**

### B2 — Decision B did not answer its second sub-question

- The Decision B question asked both (i) value form/canonical values —
  answered above — and (ii) whether `PetDefinition.Identity` is display text
  or a technical identity (the `TASK-046` three-way non-collapse rule, with
  `API_CONTRACTS.md` §5.1 projecting `identity` = "Xích Lang"). **Unanswered;
  no identity concept may be inferred.**

### E1 — Decision E's starter-row mechanism is undetermined

- **Answer:** "provision a deterministic starter loadout for the Player."
- **Gap:** no documented rule exists for how a Player's first owned
  Pet/Card/Relic rows come to exist (grep of `docs/` finds no grant, starter,
  or unlock-on-creation rule; `DATABASE.md` §2, `API_CONTRACTS.md` §3 only
  *consume* ownership). A migration cannot target a `Player` row that does
  not exist yet (players are created at Discord auth — `DATABASE.md` §2/§5),
  and Decision D forbids a startup seed runner. **The mechanism is therefore
  undetermined** and, per this task's Stop Conditions, any new starter-grant
  rule is a design change to be proposed for approval, not designed here.

### Answers — Round 1 (2026-09-29): the five conflicts above

**R1-1 — Card rows (resolves A1):**
> 1. 3 Basic + 3 Pet Skill = 6 rows (Recommended)
>
> Provision these CardDefinition rows:
>
> Basic Cards:
> - Heal
> - Shield
> - Power Charge
>
> Pet Skill Cards:
> - Inferno
> - Tidal Barrier
> - Iron Fang
>
> The undefined Thanh Xà and Sơn Hạc skill cards remain unprovisioned.

**R1-2 — Pet rows (resolves A2):**
> 1. 3 fully-defined Pets (Recommended)

**R1-3 — PassiveId scope (resolves B1):**
> 1. Pet passives only (Recommended)

**R1-4 — Starter mechanism (resolves E1):**
> 1. Record composition; decide mechanism in follow-up task (Recommended)

**R1-5 — `LoadoutCopyLimit` value (resolves C1):**
> 1. Supply the value now — LoadoutCopyLimit = 1 for every Basic Card.

### Answers — Round 2 (2026-09-29): required members with no documented value

Five further gaps surfaced while verifying that every required column of the
settled row set is sourceable (`CardDefinition.LoadoutCopyLimit` on Pet Skill
rows, `EffectDefinition` for cards/relics, `RelicDefinition.Trigger` for the
static-modifier Relic, slug derivation, the Pet identity sub-question).
Verbatim answers:

**R2-6 — Pet Skill rows' `LoadoutCopyLimit` (required column, no default):**
> 1. Also 1 for Pet Skill Cards (Recommended)
>
> Record LoadoutCopyLimit = 1 for all six provisioned CardDefinition rows:
>
> Basic Cards:
> - Heal = 1
> - Shield = 1
> - Power Charge = 1
>
> Pet Skill Cards:
> - Inferno = 1
> - Tidal Barrier = 1
> - Iron Fang = 1
>
> For Pet Skill Cards, this value is persisted only because the database column is
> required and non-nullable. It is not used to determine the Basic Card loadout
> composition under CARD_RULES.md §1 item 4.
>
> No additional gameplay behavior is introduced by this value.

**R2-7 — `EffectDefinition` values for Card and Relic rows:**
> 1. Use the documented rule text (Recommended)
>
> For the provisioned CardDefinition and RelicDefinition rows, store the
> authoritative effect rule text from the corresponding domain documentation in
> EffectDefinition.
>
> Card examples:
> - Heal → "Restore the active Pet's HP by 20% of its Max HP"
> - Shield → use the exact authoritative Shield effect rule text
> - Power Charge → use the exact authoritative Power Charge effect rule text
> - Inferno → use the exact authoritative Inferno effect rule text
> - Tidal Barrier → use the exact authoritative Tidal Barrier effect rule text
> - Iron Fang → use the exact authoritative Iron Fang effect rule text
>
> Relic rows:
> - Store the exact effect rule text defined for each provisioned Relic by
> RELIC_RULES.md §6.
>
> Do not introduce an `effect-{slug}` vocabulary or any new effect-reference
> identifier system.
>
> The values must be copied from the authoritative domain documentation
> verbatim and must satisfy the existing 128-character database limit.
>
> No new gameplay semantics are introduced by TASK-082.

**R2-8 — Burning Curse `RelicDefinition.Trigger` (amends A's relic row set):**
> Defer Burning Curse row

**R2-9 — slug derivation (resolves B's `{slug}`):**
> 1. ASCII kebab-case of the name (Recommended)
>
> Slug = ASCII kebab-case of the documented display name, mirroring TASK-049's
> `boss-def-<ascii-kebab-case-name>` precedent: `pet-xich-lang`, `card-heal`,
> `relic-berserker-core`. For PassiveId, the passive is identified by its
> owning Pet: `passive-xich-lang`, `passive-bach-ho`, `passive-huyen-quy`
> (Pets are the only named anchor — pet passives have no separate name in any
> doc).

**R2-10 — Pet identity concept (resolves B2):**
> 1. PetDefinitionId = technical identity (Recommended)
>
> `PetDefinitionId` is the technical identity of the PetDefinition.
>
> `PetDefinition.Identity` remains the documented display identity and continues
> to project the display text defined by API_CONTRACTS.md §5.1, for example:
>
> - Xích Lang
> - Bạch Hổ
> - Huyền Quy
>
> No separate technical-identity column or new Pet identity concept is introduced.
>
> This preserves TASK-046's three-way distinction:
>
> - Display name → `PetDefinition.Identity`
> - Technical identity → `PetDefinitionId`
> - Persistence identity → the database primary key
>
> The existing `PetDefinitionId` is therefore the canonical technical identity
> for Pet content.

**Resolution status.** All five decisions and all ten clarifications are now
answered by the Product Owner; no decision remains deferred by the agent.
Effective settled row sets — **Pets: 3** (Xích Lang, Bạch Hổ, Huyền Quy);
**Cards: 6** (Heal, Shield, Power Charge, Inferno, Tidal Barrier, Iron Fang);
**Relics: 4 provisioned + 1 deferred** (Burning Curse pending a documented
static-modifier Trigger, R2-8 — amending Decision A's "5 Relics"). Starter
ownership composition: 1 Pet / 3 Cards / 3–5 Relics (mechanism + exact IDs →
follow-up implementation task). Next step: record each answer in its single
owner document per `documentation/documentation-change.md`.

**Execution status:** `Status: DONE` — lifecycle `BACKLOG → READY →
IN PROGRESS → IN REVIEW → DONE` completed (`TASK_LIFECYCLE.md` §2, §3;
review checklist passed). Zero `src/`/`tests/` changes; no other task
touched.

---

## Scope

### In Scope

- Read-only discovery across the references above to evidence each decision
  point (re-verify at execution; the evidence in §"Decision Inputs" was
  verified at creation).
- Presenting Decisions A–E and recording human/product answers verbatim,
  including explicit deferrals.
- Recording each settled answer in its single canonical owner document, gated
  by `.ai/workflow/documentation/documentation-change.md` (one concept, one
  owner; no restatement of a value into a second document).
- Reporting the follow-up implementation task this contract unblocks, and the
  readiness impact on TASK-078 and on the D2/D4 dependencies — report-only.

### Out of Scope

- **Authoring content**: no Pet, Card, Relic, Passive, or Skill identifier,
  display name, cost, effect, magnitude, threshold, tier, star, or row-count
  value may be chosen, inferred, or "filled in" by the agent (`AGENTS.md` §7).
- **Decision D4** — which Boss the MVP hardcodes — is not decided, discussed
  as a recommendation, or implied here; it stays a separate unresolved
  dependency (candidate owner `docs/01-game-design/BOSS_RULES.md` §6/§6.4).
- **Decision D2** — in-progress selection-state ownership / `GameRuntimePort`
  responsibility — is not decided or scoped here.
- Any change under `src/` or `tests/` (no entity, configuration, migration,
  seeder, fixture, HasData, or startup loader is written by this task).
- Implementation of the provisioning itself (rows inserted, migration authored,
  endpoints changed) — a separate follow-up task, not created here.
- Any gameplay rule, count, formula, or balance change; any new API endpoint,
  field, or contract change; any SignalR/Redis/`GAME_STATE.md` change.
- Modifying `TASK-078`, `TASK-079`, `TASK-080`, `TASK-081`, or any
  `tasks/completed/` file.
- Creating an ADR (this is a content/persistence contract, not an
  architecture boundary; `docs/03-decisions/README.md` §2 gates ADRs).
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2.

---

## Current State

`GameDbContext` already maps `PetDefinition`, `CardDefinition`,
`RelicDefinition`, `PlayerUnlockedCard`, `Pet`, and `Relic`, but the only
content rows ever provisioned are the three `BossDefinition` rows
(migration `20260926151112_ProvisionBossDefinitions`, TASK-053). There is no
`HasData`, seed, startup loader, or migration for `PetDefinition`,
`CardDefinition`, or `RelicDefinition`, and no documented rule for a Player's
initial owned rows — so every collection read returns `200 []`
(`TASK-071` §5.5 semantics) and no real `POST /api/battle/start` loadout can
pass `API_CONTRACTS.md` §3 ownership validation. `DATABASE.md` §5 item 4 and
`TASK-045` §6 issue 1 record this as open; `TASK-079` and `TASK-080` both
record D3 as UNRESOLVED and report-only.

---

## Acceptance Criteria

- [x] Decisions A–E each carry an evidence block citing file + section for
      every claim (`AGENTS.md` §4), and each carries a human/product answer
      recorded verbatim — or an explicit, labelled deferral — with no
      agent-chosen value anywhere in the task file.
- [x] Every settled answer is recorded in exactly one canonical owner document
      (per §"Decision Inputs" candidate owners), with the document's version
      header and changelog updated to cite TASK-082
      (`documentation/documentation-change.md` §1, §2 — no duplication).
- [x] Zero content values are invented: a grep of this task file and of the
      edited documents shows no newly introduced identifier, cost, effect,
      magnitude, threshold, tier, star, or row count that was not already
      present in an authoritative document or supplied by the human.
- [x] D4 and D2 remain unresolved and untouched; no Boss is named, ranked, or
      recommended anywhere in this task's output (`AGENTS.md` §7).
- [x] No file under `src/` or `tests/` is created or modified.
- [x] `TASK-078`, `TASK-079`, `TASK-080`, `TASK-081`, and every
      `tasks/completed/` file are byte-identical to their pre-task state.
- [x] The follow-up provisioning **implementation** task is reported (not
      created), and TASK-078's readiness impact is reported (not applied).
- [x] Quality review checklist passes (`quality/review.md` §1, code-only items
      skipped per `documentation/documentation-change.md` §4).
- [x] Documentation validation depth is met (`core/validation.md` §2) for a
      documentation-only change.
- [x] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001).

---

## Affected Files & Areas

```text
[ ] src/backend/ (Domain / Application / Infrastructure / Api)   — none
[ ] src/frontend/client/ (scenes / runtime / services / state / ui) — none
[ ] tests/ (unit / integration / gameplay scenarios)             — none
[x] docs/ (documentation updates if applicable)                  — owner doc
                                                                  per decision,
                                                                  decided at
                                                                  execution
[x] tasks/backlog/TASK-082-*.md (this manifest — moves to tasks/completed/)
```

---

## Implementation Notes

- **Single-owner rule.** Identify the canonical owner for each answer before
  editing (`documentation/documentation-change.md` §1): provisioning mechanism
  and persistence keys → `DATABASE.md`; identities → the owning domain rule
  document; counts → never restated (`MVP_SCOPE.md` §1 remains sole owner).
- **Precedent chain to read first:** `TASK-046` (three-way non-collapse),
  `TASK-049` (`BossDefinitionId` value form), `TASK-052` (mechanism),
  `TASK-053` (implementation). Their decisions are scoped to `BossDefinition`;
  extending them to other tables is Decision D, not an assumption.
- **Example ≠ contract.** `"heal"`/`"shield"`/`"power_charge"` (API_CONTRACTS
  §3), `"relic-a"` (RELIC_RULES), `"Thanh Xà"` (PET_RULES §1), and
  `PassiveId("xich-lang")` (BOSS_RULES §6.4) are examples unless Decision B
  makes them canonical — never treat them as already-fixed values.
- If a human answer makes a binding sentence in another document stale
  (e.g. `API_CONTRACTS.md` §5.3), STOP and report it per `AGENTS.md` §4 —
  do not silently edit the second document.
- Do not expand this task to fix TASK-078's Implementation Notes (its
  `"bossId": "boss-hoa-long"` line is a non-authoritative note and the D4
  question; `TASK-080` §6 forbids inferring D4 from it).

---

## Testing Requirements

### Required Verification
```text
[x] Unit tests         — N/A (documentation-only)
[x] Integration tests  — N/A (documentation-only)
[x] Gameplay scenarios — N/A (no rule changed; if a decision changes a rule,
                          STOP — a GAMEPLAY-CHANGE task is required first,
                          tasks/TASK_TYPES.md) — verified: no gameplay rule
                          value altered, only identity/count/provisioning
                          contract recorded
[x] Documentation      — version header + changelog updated; no duplicate
                          definition introduced (documentation-change.md §2);
                          grep confirms no newly authored content values;
                          dependent references still resolve (no stale section
                          numbers) — PASS (see Completion Evidence →
                          Validation Results)
```

### Key Edge Cases
- A decision is answered "not yet decided": the deferral is recorded
  explicitly and the task may still be reported complete, with the unresolved
  item listed under Remaining Issues (`TASK-080` §6).
- Two documents disagree on a value or concept: STOP per `AGENTS.md` §4 and
  report both file+section citations; do not pick the easier one.
- A human answer would require a document outside the listed candidate owners:
  STOP and re-check ownership before editing (`AGENTS.md` §4).

---

## Stop Conditions

- If any decision cannot be answered from authoritative documents + an explicit
  human answer: STOP per `AGENTS.md` §7 / §20 — never supply the missing
  content, identifier, or rule.
- If asked to name, rank, or recommend the MVP Boss (D4): STOP and report it as
  a separate dependency.
- If the answer requires a new gameplay mechanic, count, or acquisition system
  (e.g. a starter-grant or reward flow): STOP and propose the smallest design
  change for approval — do not implement it (`AGENTS.md` §7, §17).
- If the task exceeds 5 planned skills or crosses an uncoupled architectural
  boundary: STOP and decompose (`tasks/README.md` §12, hard limit 7).
- If any change under `src/` or `tests/` appears necessary: STOP — a separate
  implementation task owns that work.

---

## Completion Evidence

<!-- COMPLETED BY THE EXECUTING AGENT — 2026-09-29. -->

### Decisions Recorded
- A — recorded verbatim; row set settled (R1-1: 6 Cards; R1-2: 3 Pets;
  R2-8: Burning Curse deferred) → `CARD_RULES.md` §1/§4.1 pointers via
  `DATABASE.md` §1/§5, `PET_RULES.md` §8, `RELIC_RULES.md` §6
- B — recorded verbatim; settled (R2-9 slug form, R2-10 Pet identity)
  → `DATABASE.md` §1 (key forms), `PET_RULES.md` §1 (identity model),
  `PASSIVE_RULES.md` §8 (Pet PassiveId values)
- C — recorded verbatim; settled (C1 + R1-5 + R2-6) → `CARD_RULES.md` §1
  item 5 (`LoadoutCopyLimit` = 1, all six rows); `EffectDefinition` rule
  (R2-7) → `DATABASE.md` §1 (Card + Relic blocks)
- D — recorded verbatim; settled → `DATABASE.md` §5 item 4 (mechanism
  defined for all three tables on the TASK-052 terms)
- E — recorded verbatim; composition settled (R1-4: mechanism + exact
  IDs → follow-up task) → `DATABASE.md` §2 (starter-ownership note)
- Clarifications C1, A1, A2, B1, B2, E1 (Round 1) and R2-6…R2-10
  (Round 2) all answered by the Product Owner; no owner document was
  edited until all were answered (`AGENTS.md` §4)
- Two conflict classes were reported, not resolved (per `AGENTS.md` §4):
  (1) `RELIC_RULES.md` §3 "exactly one primary Trigger" vs §6 note 1
  static-modifier Burning Curse — recorded in `RELIC_RULES.md` §6 note 3;
  (2) the TASK-082 PO quotes say "Sơn Hạc" while seven authoritative
  documents say "Sơn Hùng" — docs win (`AGENTS.md` header); owner-doc
  edits use "Sơn Hùng" (see Report-Only Findings)

### Changed Files
- `tasks/backlog/TASK-082-resolve-pet-card-relic-content-provisioning-contract.md`
  (this manifest — answers, clarifications, completion evidence)
- `docs/02-technical/DATABASE.md` — v1.17 → **1.18** (§1 Pet/Card/Relic
  key value forms + Identity + EffectDefinition/LoadoutCopyLimit notes;
  §2 MVP starter-ownership composition; §5 item 4 mechanism defined)
- `docs/01-game-design/PET_RULES.md` — v3.0 → **3.1** (§1 identity model;
  §8 provisioned/deferred row set)
- `docs/01-game-design/CARD_RULES.md` — v1.3 → **1.4** (§1 item 5
  `LoadoutCopyLimit` MVP values)
- `docs/01-game-design/PASSIVE_RULES.md` — v1.0 → **1.1** (§8 Pet
  `PassiveId` values)
- `docs/01-game-design/RELIC_RULES.md` — v1.4 → **1.5** (§6 note 3
  provisioned/deferred row set)
- Nothing else: zero `src/`/`tests/`/`migrations/` changes;
  `TASK-078`/`079`/`080`/`081` and `tasks/completed/` untouched
  (`git status` verified: exactly these 5 modified + this new file)

### Validation Results
- `core/validation.md` §2 (documentation-only depth): changed sections
  re-read in context; every cross-reference added resolves
  (`GAME_RULES.md` §13 item 2, `CARD_RULES.md` §1/§2/§4.1,
  `RELIC_RULES.md` §2/§3/§6, `PET_RULES.md` §8, `BOSS_RULES.md` §6.4,
  `GAME_STATE.md` §2.3, `API_CONTRACTS.md` §5.1, `MVP_SCOPE.md` §1/§2,
  TASK-046/052/053, ADR-012); dependent docs re-checked for staleness —
  two stale references found and reported below, not edited
- `quality/review.md` §1: Correctness ✓ (each recorded value traced to
  a verbatim PO answer or a pre-existing table), References ✓, no
  duplicate definition — each concept written once to its single owner
  (`documentation-change.md` §2); code-only checklist items skipped
- No-invented-value grep: every new literal (`pet-xich-lang`,
  `card-heal`, `relic-berserker-core`, `passive-*` × 3, `LoadoutCopyLimit`
  = 1, ≤128, EF Core migration-INSERT, row names, 1/3/3–5) occurs
  verbatim in this file's human answers or in the pre-existing owner
  tables (`PET_RULES.md` §8, `RELIC_RULES.md` §6, `CARD_RULES.md` §2/§4.1);
  no Boss named/ranked (D4 untouched), no `GameRuntimePort`/D2 content
  in any doc edit

### Report-Only Findings
- **"Sơn Hạc" vs "Sơn Hùng" (naming):** the PO quotes in this task say
  "Sơn Hạc" (R1-1, and Decision A context); `GAME_RULES.md`,
  `MVP_SCOPE.md`, `GDD.md`, `ELEMENT_RULES.md`, `CARD_RULES.md` §4.1,
  `PASSIVE_RULES.md` §8, `PET_RULES.md` §8 all say "Sơn Hùng". Docs win;
  owner-doc edits and the recorded row sets use "Sơn Hùng". A human
  should confirm the pet's canonical name (docs change or a corrected
  quote) — not resolved here (`AGENTS.md` §4)
- **Stale dependent reference #1 (NOT edited):** `BOSS_RULES.md` §6.4
  (line 292) says Boss PassiveIds "follow the kebab-case pattern of Pet
  PassiveIds (e.g. `PassiveId("xich-lang")`)" — Decision B/R2-9 now sets
  Pet PassiveIds to `passive-{slug}`, so that example/pattern sentence is
  stale. Boss content is out of scope for this task; reported for a
  follow-up Boss-doc correction (smallest fix: update the example to
  `PassiveId("passive-xich-lang")` or decouple the sentence from the Pet
  pattern)
- **Stale dependent reference #2 (NOT edited, report-only per the
  Implementation Note):** `API_CONTRACTS.md` §3 example payload
  `"cardLoadout": ["heal", "shield", "power_charge"]` (line ~458) no
  longer matches the canonical `CardDefinitionId` form `card-{slug}`
  (`card-heal`, `card-shield`, `card-power-charge`) decided in R2-9;
  `"relicLoadout": ["relic-a", ...]` (line ~459) uses instance-id
  examples — `relicId` is `Relic.RelicInstanceId` (§5.4), so those are
  NOT contradicted by the `relic-{slug}` definition-key form, but the
  card example should be refreshed by a follow-up API-contract task
- `RELIC_RULES.md` §3 vs §6 note 1 (Burning Curse static modifier vs
  "exactly one primary Trigger") — reported inside
  `RELIC_RULES.md` §6 note 3, not resolved (`AGENTS.md` §4)
- D3 → follow-up provisioning **implementation** task (recorded, not
  created): migration-INSERT rows + §2 starter mechanism + exact IDs
- D4 → still unresolved (not created, not decided); no Boss named
- D2 → still unresolved (not created, not decided)
- TASK-078 readiness → D3 remains open as an implementation task;
  TASK-078 stays NOT READY (report-only, no status change applied)

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic (no code touched)
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1) — no new content
      class introduced, no OUT item approached (the §2 note explicitly
      binds starter ownership to existing rules and excludes
      gacha/shop/drop/reward/progression acquisition — `MVP_SCOPE.md` §2)
