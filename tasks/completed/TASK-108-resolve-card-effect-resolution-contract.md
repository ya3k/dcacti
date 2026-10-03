# TASK-108 — Resolve the Card Effect-Resolution Contract (`CardDefinition.EffectDefinition` Vocabulary)

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.

  THIS TASK DECIDES NOTHING BY ITSELF AND IMPLEMENTS NO BEHAVIOR. It presents
  the documented evidence of a genuine contract gap and requires the
  appropriate human/Product-Owner decision, then records it in the canonical
  owner document(s). Inventing an effect vocabulary, an effect identifier, a
  magnitude carrier, or a balance value is the single prohibited action of
  this task (AGENTS.md §7, §20).

  PROVENANCE: identified by the post-TASK-107 next-task discovery pass.
  TASK-107 (BACKLOG) instructs its executor to build a Domain Card-effect
  resolver that "reads a CardDefinition and applies its documented effect",
  and its own Out of Scope states the Shield magnitude "must be read from the
  Card content, not hardcoded in the resolver". The discovery pass verified
  that the Card content cannot carry that information: DATABASE.md §1 stores
  EffectDefinition as the owning document's VERBATIM prose and explicitly
  excludes any effect-id vocabulary (TASK-082 R2-7). No Card effect-type
  discriminator and no magnitude field exists in the schema, the domain type,
  or any document. TASK-107 therefore cannot be executed without either
  inventing an effect contract or first resolving this one.

  THIS IS A STOP-DRIVEN TASK, NOT A FEATURE. Per the task-generation stop
  conditions, when "a gameplay rule must be invented" or "the action execution
  contract is undefined" the correct output is a documentation/decision task,
  not an implementation task. TASK-107 is NOT modified, re-scoped, or
  re-statused by this task; it simply cannot ascend to READY until this
  resolves.

  BOUNDARY: documentation only. Zero files under src/ or tests/. This task
  creates no ADR, changes no Shield semantic, and adds no gameplay rule.
-->

---

## Metadata

```text
Task ID:           TASK-108
Type:              DOCUMENTATION (TASK_TYPES.md §2 — "Change docs/ content —
                   documentation is the primary output, not code"; workflow
                   documentation/documentation-change.md). The deliverable is a
                   recorded contract decision plus its entry in the canonical
                   owner document(s). See "Type classification note". If the
                   decision is that a code/schema change is required, that
                   change is a SEPARATE follow-up task this task creates only
                   after the decision is recorded — not this task's act.
Status:            DONE (Product Owner decisions D-1 through D-4 recorded
                   verbatim; consequence sections D-5 and D-6 recorded;
                   TASK-082 R2-7 supersession reported; follow-up tasks
                   identified and subsequently executed. Formal review pass
                   completed against quality/review.md §1 and
                   core/completion.md §1: PASS. File moved from tasks/backlog/
                   to tasks/completed/ per TASK_LIFECYCLE.md §3.)
Risk:              MEDIUM (TASK_TYPES.md §4 — DOCUMENTATION baseline LOW–MEDIUM;
                   MEDIUM because the decision it records governs how every
                   future Card, and therefore every future Card-cast effect,
                   is resolved, and it is cross-referenced by CARD_RULES.md,
                   DATABASE.md, and the TASK-107 implementation. No `docs/`
                   rule is changed by this task beyond recording the answer.)
Priority:          HIGH (the sole blocker on TASK-107 — the ROADMAP.md Phase 1
                   "3 Basic Cards" vertical slice, and the only remaining
                   producer of the Shield that TASK-102 implemented the
                   consumption side of.)
Primary Agent:     review (TASK_TYPES.md §2 / AGENT_SELECTION.md §1 —
                   "Documentation change: Primary Agent Review". No domain
                   agent may author the answer; the Product Owner supplies it.)
Supporting Agents: gameplay (CARD_RULES.md §2/§3 is the owning domain document
                   for Card behavior — consulted to CONFIRM the effect
                   semantics the contract must be able to express, not to
                   author the vocabulary),
                   backend (DATABASE.md §1 CardDefinition and the Domain
                   CardDefinition type — consulted to state accurately what
                   the current schema does and does not carry),
                   persistence (only if the recorded decision implies a
                   schema/column consequence, which is REPORTED for a separate
                   task, never applied here)
Workflow:          documentation/documentation-change.md
Skills:            discovery/documentation-discovery,
                   discovery/impact-analysis,
                   quality/documentation-consistency,
                   quality/scope-validation
                   (4 skills — Simple budget, tasks/README.md §12)
Dependencies:      TASK-107 (BACKLOG — the blocked implementation task this
                     task unblocks; read-only, NOT modified, NOT re-scoped),
                   TASK-082 (DONE — recorded `EffectDefinition` as verbatim
                     prose and explicitly excluded effect-id vocabulary,
                     decision R2-7; IMMUTABLE),
                   TASK-085 (DONE — provisioned the 6 CardDefinition rows whose
                     `EffectDefinition` text this contract must be able to
                     resolve; IMMUTABLE),
                   TASK-028 (DONE — Card ownership + loadout snapshot;
                     immutable),
                   TASK-102 (Shield consumption, implemented — the behavior the
                     Shield Basic Card's effect must eventually reach;
                     read-only),
                   TASK-105 (DONE — the frozen Shield contract; IMMUTABLE),
                   TASK-104 (READY — the decision-register precedent for how a
                     Product Owner answer is captured, and the B-4 disposition
                     that keeps PetSkillCast blocked; read-only)
Blocks:            TASK-107 (it cannot ascend to READY, and cannot be executed
                   without inventing the contract this task resolves). Through
                   TASK-107 it blocks the Phase 1 "3 Basic Cards" slice and the
                   only Shield producer. It does NOT block TASK-036, TASK-079,
                   or TASK-099.
Estimate:          Simple (present the evidence, obtain and record one contract
                   decision across at most two owner documents; no code, no
                   tests, no migration)
```

**Status note.** Now `IN REVIEW`. It began `BACKLOG`, which is this task's
normal starting state while a decision set is unanswered — it is what the task
exists to collect; `TASK_LIFECYCLE.md` §2 does not permit `BACKLOG → BLOCKED`,
so it never started BLOCKED. This follows the TASK-082 / TASK-104 precedent
exactly. Lifecycle: `BACKLOG → READY → IN PROGRESS → IN REVIEW → DONE`.

**The decision supersedes TASK-082 R2-7 — this is recorded, not silently
applied.** The Product Owner's D-1 answer requires a structured
`EffectDefinition`, which directly reverses TASK-082 R2-7's verbatim-prose
ruling ("Do not introduce an `effect-{slug}` vocabulary or any new
effect-reference identifier system"). TASK-082 is a DONE task and is
**immutable** (`TASK_LIFECYCLE.md` §3), so this task does not edit it, does not
edit `DATABASE.md` §1 (which currently carries R2-7's wording), and does not
apply the reversal to `docs/` at all. The reversal is recorded here and
**reported** as the change the follow-up implementation task must make to the
canonical owner document. See "Resolution Record" and "TASK-082 R2-7
Supersession" in the Completion Evidence.

**Type classification note.** `DOCUMENTATION`, not `GAMEPLAY-CHANGE` and not
`ARCHITECTURE`. The deliverable is a recorded contract decision in its canonical
owner document. Whether the answer *implies* a schema column, a new Domain
member, or an ADR is a consequence the task **reports** — and if the answer
requires one, that is a separate follow-up task created after the decision is
recorded, not an act of this task. Authoring the answer itself is the Product
Owner's, not an agent's (`AGENTS.md` §7).

**This task authors no vocabulary and no value.** It must not choose an
`effect-{slug}` form, an enum member name, a magnitude column, or a number. Its
job is to state the gap precisely, present the viable options with their
documented consequences, obtain the decision, and record it — the same shape
TASK-104 used to capture its nine Product Owner decisions.

---

## Objective

Resolve, from authoritative documents and a human/Product-Owner answer only, the
**Card effect-resolution contract** that TASK-107's implementation requires and
that `docs/` does not currently define: how a `CardDefinition` identifies *which*
effect it applies and *how much* of it, given that
`docs/02-technical/DATABASE.md` §1 stores `EffectDefinition` as the owning
document's **verbatim prose** and explicitly excludes any effect-id vocabulary
(TASK-082 decision R2-7) — so that TASK-107 can be executed, and every later
Card-cast and Pet-Skill effect resolved, without inventing a contract.

Concretely, this task must make the decision points in §"Decision Inputs"
explicit and evidenced against `docs/`, obtain a human/Product-Owner answer for
each (or an explicit recorded deferral), and record each settled answer in its
single canonical owner document — without authoring any identifier, vocabulary,
column, magnitude, or balance value.

**RESOLVED 2026-10-01.** The Product Owner answered D-1 (structured
`EffectDefinition` is the effect identity carrier) and D-2 (the magnitude is
data-driven inside it, carrying `effectType` + `valueType` + `value`), and
confirmed D-3 (TASK-107 unblocked from the contract perspective, scope
unchanged) and D-4 (PetSkillCast stays separate; Tidal Barrier remains blocked).
All four answers are recorded verbatim under "Product Owner Decisions". The
decision **supersedes TASK-082 R2-7**; that supersession is recorded and
reported, and — because TASK-082 is a DONE, immutable task and `DATABASE.md` is
not this task's to edit — it is **not applied to `docs/` here**. Applying it,
plus the structured runtime support it requires, is the follow-up implementation
task named under "Follow-Up Tasks Required".

---

## Authoritative References

### The gap and its owners (READ ONLY — this task records, it does not redefine)

- `docs/02-technical/DATABASE.md` **§1** — the `CardDefinition` entity, whose
  `EffectDefinition` is defined as "the owning domain document's effect rule
  text, stored VERBATIM and within the 128-char column limit; **no
  `effect-{slug}` or other effect-id vocabulary** — `CARD_RULES.md` §2/§4.1,
  TASK-082 R2-7". Also the version header's restatement of the same decision
  ("`EffectDefinition` = the owning domain document's verbatim effect rule text
  (≤128 chars, no effect-id vocabulary)"). **This is the sentence that makes the
  gap a deliberate contract decision rather than an omission.**
- `docs/01-game-design/CARD_RULES.md` **§2** — the three MVP Basic Cards, whose
  Effects are given as prose ("Restore the active Pet's HP by 20% of its Max
  HP"; "Active Pet gains Shield equal to 20% of its Max HP"; "Active Pet gains
  25 Power"), and **§3** — the casting rules, item 4 naming the resolution order
  (`Deduct Cost → Apply Effect → Emit CardCast`). **§4 / §4.1** — the Pet Skill
  Cards, including Huyền Quy's Tidal Barrier, whose effect is prose with **no
  magnitude** ("Heal; Gain Shield"). This document is the canonical owner of
  Card *behavior*; it is **not** the owner of the persistence/schema shape.
- `src/backend/GameServer.Domain/Cards/CardDefinition.cs` — the Domain type that
  mirrors §1: `CardDefinitionId`, `Name`, `Category`, `PowerCost`,
  `EffectDefinition`, `LoadoutCopyLimit`. Its own documentation states
  `EffectDefinition` "is an effect **reference** … carried as a reference, not
  as resolved effect content, and nothing executes it". **It carries no
  effect-type discriminator and no magnitude member.**
- `src/backend/GameServer.Infrastructure/Postgres/Migrations/20260929152651_ProvisionPetCardRelicContentDefinitions.cs`
  — the provisioned rows, whose `EffectDefinition` values are the verbatim prose
  strings above. Read-only.
- `docs/02-technical/GAME_STATE.md` **§0 item 5** — no stage introduces a
  parallel representation of a concept another stage already owns (the rule any
  proposed contract must respect); **§2.3.1** — the Status Effect instance
  schema (`Magnitude` is "typed but not interpreted here"; the owning rule
  document gives it meaning).

### The consumers (what the contract must be able to express)

- `docs/01-game-design/COMBAT_RULES.md` **§4 items 1–6** — the frozen Heal and
  Shield rules the Basic Cards' effects must reach: Heal clamps to Max HP and
  discards overheal (item 1); Shield absorption before HP with one instance per
  entity, refresh replacing magnitude, no additive stacking (items 2–3);
  depletion at exactly 0 and overflow by remainder (items 4–5); Heal and Shield
  are not subject to the Damage Pipeline (item 6). **Any resolved contract must
  be able to drive exactly these, unchanged.**
- `docs/01-game-design/GAME_RULES.md` **§11** (Card rules), **§12** (Power range
  0–100 and its cap), **§14** (the combat flow), **§16** (the canonical event
  list), **§17 step 14** ("Resolve Player Effects" — the resolution position a
  Card effect occupies), **§18** (server authority: no client-provided effect
  value is authoritative), **§20** (Rule Change Policy).
- `src/backend/GameServer.Domain/Match3/ResourceGenerator.cs` —
  `ApplyHeal(...)` and `ApplyPower(...)`, the existing write sites that already
  enforce the Heal clamp and the Power cap. A resolved contract should let a
  Card effect *reach* these, not duplicate them.
- `src/backend/GameServer.Domain/Battle/StatusEffectLifecycle.cs` —
  `ApplyShield(...)`, `RemoveDepletedShield(...)`, and `ShieldPool(...)`: the
  existing Shield application operation TASK-107 is instructed to call
  (`GAME_STATE.md` §2.3.1 item 3; `COMBAT_RULES.md` §4 item 3). It takes the
  pool value as its `StatusEffect.Magnitude` argument, so the unresolved
  question is where that value comes from.

### Technical contracts the answer must not contradict

- `docs/02-technical/TDD.md` **§4 item 3** — PostgreSQL is never read on the hot
  resolution path (so a per-cast database lookup is not a free option); **§6** —
  determinism; **§2.1** — the client is presentation-only.
- `docs/02-technical/ARCHITECTURE.md` **§2.1** — the layer boundary (Domain owns
  rules, Application orders calls, Api is transport); **§5** — the
  anti-overengineering standard; **§2.2.2** — the implemented `Swap` path as the
  precedent shape.
- `docs/03-decisions/ADR/ADR-001-server-authoritative-battle.md` — server
  authority; `ADR-006` — PostgreSQL "is queried only outside the hot resolution
  path … never per-action during an active battle" (directly relevant to D-2's
  options); `ADR-012` item 9 — the Card ownership model
  (`PlayerUnlockedCard` unlock-flag join table; no instance table); and
  `docs/03-decisions/README.md` §8 — "Known Open Items (Not ADRs)", which must
  be checked before asserting a decision needs an ADR.

### Scope, governance, and precedent

- `docs/00-overview/MVP_SCOPE.md` **§1** (Cards and Combat/Status Effects are
  IN), **§2** (OUT), **§4** (unlisted is not implicitly IN).
- `tasks/backlog/TASK-107-implement-basic-cardcast-server-path.md` — the blocked
  implementation task. Its Scope item 2 requires the resolver to read a
  `CardDefinition` and apply its documented effect, and its Out of Scope states
  the Shield magnitude "must be read from the Card content, not hardcoded in the
  resolver". **Read-only; NOT modified, NOT re-scoped, NOT re-statused.**
- `tasks/completed/TASK-082-resolve-pet-card-relic-content-provisioning-contract.md`
  — the DONE decision that recorded `EffectDefinition` as verbatim prose and
  excluded effect-id vocabulary (R2-7). **IMMUTABLE — do not modify.** This task
  does not re-open it; it resolves the *next* question that decision left open.
- `tasks/backlog/TASK-104-collect-cardcast-wire-and-shield-contract-decisions.md`
  — the decision-capture precedent (how a Product Owner answer is recorded
  verbatim, and how a deferral is recorded as a deferral). Read-only.
- `tasks/completed/TASK-105-change-shield-application-semantics-to-refresh.md`
  — the frozen Shield contract. **IMMUTABLE — do not modify.**
- `AGENTS.md` §4 (conflict resolution), §7 (never invent a rule or content),
  §9 (anti-overengineering), §17 (documentation change rule), §20 (stop
  conditions); `tasks/README.md` §9 (no business-rule duplication in task
  files), §12 (skill budget).

**ADR check (to be confirmed, not assumed, by the executing agent):** this task
is expected to require **no** ADR, because it records a content/contract shape
decision inside existing documents rather than changing a layer, storage
strategy, realtime strategy, or the authoritative model. If the obtained answer
does require one, that is **reported** and becomes a separate task — it is not
authored here (`AGENTS.md` §18).

---

## Current State

`CardDefinition` carries `EffectDefinition` as **verbatim prose** and nothing
machine-readable. Verified directly in the working tree:

```text
docs/02-technical/DATABASE.md §1   EffectDefinition = "the owning domain
                                   document's effect rule text, stored
                                   VERBATIM ... no `effect-{slug}` or other
                                   effect-id vocabulary" (TASK-082 R2-7)

provisioned rows (migration 20260929152651):
  card-heal           "Restore the active Pet's HP by 20% of its Max HP"
  card-shield         "Active Pet gains Shield equal to 20% of its Max HP"
  card-power-charge   "Active Pet gains 25 Power"

Domain CardDefinition members:
  CardDefinitionId, Name, Category, PowerCost, EffectDefinition,
  LoadoutCopyLimit
  — no effect-type discriminator, no magnitude member

repo-wide search for a card-effect vocabulary
(CardEffectType | EffectType | EffectKind | effect-{slug}):
  ZERO matches in src/ and tests/
```

**The gap.** TASK-107's resolver must decide, for a cast `cardId`:

```text
Which effect does this CardDefinition apply?   → not representable
How much of it?                                → not representable
```

`CARD_RULES.md` §2's prose is the *game rule* and is authoritative; but
`DATABASE.md` §1's deliberate exclusion of effect-id vocabulary (TASK-082 R2-7)
means the runtime content cannot map a `CardDefinition` to an effect type or a
magnitude. An agent cannot close this by reading prose at runtime without
inventing a parsing rule, and cannot hardcode a table of effect values without
inventing content the documents do not authorize (`AGENTS.md` §7).

**Why this blocks TASK-107 specifically.** TASK-107's acceptance criteria require
a Shield Basic Card cast to produce a Shield "of the documented amount" and
require that amount to come from the Card content rather than a hardcoded
constant. Both are unsatisfiable until this contract exists. TASK-107 therefore
stays in `backlog/` and must not ascend to `READY`.

**What this does NOT change.** The Shield contract itself is fully resolved and
frozen (`COMBAT_RULES.md` §4, TASK-105), the absorption/refresh/depletion
behavior is implemented (TASK-102), and the Shield Basic Card's magnitude *is*
documented as a game rule (`CARD_RULES.md` §2 — a percentage of Max HP). The
question is neither a Shield-semantics question nor a missing game rule: it is
**which artifact carries the rule into the runtime**.

---

## Decision Inputs

<!--
  A PRODUCT OWNER / HUMAN must answer these. An agent must NOT answer them.
  Choosing, recommending, ranking, or defaulting any answer is the single
  prohibited action of this task (AGENTS.md §7, §20).
-->

Each item states the question, the documented evidence for and against each
viable option, and what the option would cost downstream. **The options are
presented as evidence, not as a recommendation.**

### D-1 — How does a `CardDefinition` identify which effect it applies?

**ANSWERED — Product Owner, 2026-10-01.** The answer is recorded verbatim in
"Product Owner Decisions" below; the options below are retained as the evidence
the decision was taken against.

**Decision: structured `EffectDefinition` is the effect identity carrier.**

```text
CardDefinition
  └── EffectDefinition
        └── Structured effect data

CardDefinitionId → which Card content row
EffectDefinition → which domain effect it applies
```

The runtime **MUST NOT** derive the effect from:

```text
prose parsing of EffectDefinition
Card name inference
CardDefinitionId mapping / switch statements
hardcoded card-specific logic
```

Effect identity **MUST** come from the structured effect data. The existing
prose-only `EffectDefinition` is insufficient for runtime resolution because the
server must deterministically identify which domain effect a Card applies.

**This supersedes TASK-082 R2-7** (the verbatim-prose ruling that excluded any
effect-id vocabulary). See "TASK-082 R2-7 Supersession".

Evidence recorded at decision time:

```text
Option A — Author an effect-id vocabulary.  ← THIS WAS CHOSEN
Option B — Resolve by CardDefinitionId in code.
Option C — Infer from Name/Category.
Option D — Defer.
```

```text
Option A — Author an effect-id vocabulary.
  Form: a closed set of effect identities (e.g. an `effect-{slug}` reference,
        or a typed effect-kind member) stored on CardDefinition, replacing or
        accompanying the verbatim prose.
  Evidence for:   gives the runtime a machine-readable discriminator; mirrors
                  RELIC_RULES.md §3's closed Trigger-identity list, which is
                  the repository's existing precedent for a closed vocabulary
                  on a content definition.
  Evidence against: DATABASE.md §1 explicitly records "no `effect-{slug}` or
                  other effect-id vocabulary" — TASK-082 R2-7 decided against
                  exactly this. Choosing it REVERSES a DONE decision, which
                  TASK_LIFECYCLE.md §3 makes a new-task matter, not an inline
                  edit.
  Cost:           a DATABASE.md §1 contract change + a schema/migration
                  consequence + a new ADR question. Requires a separate
                  follow-up task.

Option B — Resolve the effect in Domain code by CardDefinitionId.
  Form: a closed Domain-side mapping from the known CardDefinitionId values to
        their effect and magnitude, with no persistence change.
  Evidence for:   no schema change, no migration, no ADR; PostgreSQL stays off
                  the hot path (TDD.md §4 item 3) since the mapping is compiled
                  in; the effect *behavior* remains owned by CARD_RULES.md and
                  COMBAT_RULES.md, which the mapping only references.
  Evidence against: `EffectDefinition` is stored verbatim precisely so content
                  is data-driven; a code-side table makes adding a Card a code
                  change, and risks a second source of truth for the same rule
                  (GAME_STATE.md §0 item 5, AGENTS.md §9) unless its ownership
                  is stated explicitly.
  Cost:           no schema consequence; requires the decision to state
                  explicitly that the mapping is a lookup, not a rule owner.

Option C — Declare the effect vocabulary already implicit in `Category`+`Name`.
  Form: treat the Card's `Name` (or another existing column) as the effect
        identity.
  Evidence for:   no new member at all; smallest possible surface.
  Evidence against: `Name` is display text (DATABASE.md §1 calls
                  `PetDefinition.Identity` "display text"; `Name` is the same
                  class of value), and `CARD_RULES.md` §2's display names are
                  not defined as identifiers anywhere. Deriving behavior from
                  a display string is the "stringly-typed" case AGENTS.md §9
                  warns against, and it breaks on the first rename.
  Cost:           lowest immediate cost, highest fragility.

Option D — Defer: explicitly record that MVP Basic Card effects are resolved
           by one of the above, chosen later, and leave TASK-107 blocked.
  Cost:           TASK-107 and the Phase 1 "3 Basic Cards" slice stay blocked
                  indefinitely; the Shield producer does not exist.
```

### D-2 — Where does an effect's magnitude live?

**ANSWERED — Product Owner, 2026-10-01.**

**Decision: the magnitude is data-driven inside the structured
`EffectDefinition`.** It MUST describe the effect value, the value's
interpretation, and the calculation rule. It is not stored as a resolved
integer on the definition, and it is not computed from prose.

The contract carries three members:

```text
effectType  — which domain effect (D-1's identity carrier)
valueType   — how the value is interpreted / the calculation rule
value       — the effect value
```

The Product Owner supplied illustrative shapes (recorded verbatim in "Product
Owner Decisions"); they are **shape examples, not authored balance values**.
The concrete Card balance values remain owned by `CARD_RULES.md` §2/§4.1 and
are **not** changed by this decision — this decision defines only the runtime
contract. See "Balance-Value Boundary" below.

```text
Question: CARD_RULES.md §2 states magnitudes as prose proportions
          ("20% of its Max HP", "25 Power"). Where is the runtime numeric value
          carried?

Evidence: GAME_STATE.md §2.3.1 item 2 types `Magnitude` on the Status Effect
          *instance* and says its meaning is owned by the rule document — so
          the Shield instance's pool value is representable ONCE the effect is
          applied. The open question is the value the *application site*
          supplies, and for Heal/Power Charge what is supplied to ApplyHeal /
          ApplyPower.

Sub-question: are the percentages and Power amounts read as a proportion of
          MaxHP at cast time (computed server-side per GAME_RULES.md §18), or
          stored as resolved integers on the definition? CARD_RULES.md §2's
          wording is proportional, which affects whether any stored value is
          needed at all.
```

### D-5 — The Resolver Contract (recorded consequence of D-1/D-2)

```text
CardDefinition
        ↓
EffectDefinition          (structured — D-1, D-2)
        ↓
Effect Resolver           (deterministic)
        ↓
Domain operation
        ├── ApplyHeal
        ├── ApplyShield
        └── ApplyPower
```

The resolver **MUST** be deterministic (`GAME_RULES.md` §17/§18,
`TDD.md` §6). The resolver **MUST NOT** contain:

```text
cardId switch statements
hardcoded card behaviour
hidden gameplay values
```

It dispatches on the structured effect data only, and calls the existing
Domain write sites — `ResourceGenerator.ApplyHeal`,
`StatusEffectLifecycle.ApplyShield`, `ResourceGenerator.ApplyPower` — which
already own the Heal clamp (`COMBAT_RULES.md` §4 item 1) and the Power cap
(`GAME_RULES.md` §12). The resolver must not duplicate either rule.

### D-6 — Balance-Value Boundary (recorded, not to be crossed)

```text
This decision defines the RUNTIME CONTRACT only.
The concrete Card balance values remain owned by CARD_RULES.md §2/§4.1.
```

The illustrative shapes supplied with the decision are **shape examples**. No
balance value is authored, changed, or confirmed by this task. Note for the
follow-up task: the examples supplied differ from the currently-provisioned
`CARD_RULES.md` §2 values, so the **prose values must be confirmed against
`CARD_RULES.md` §2 before being encoded** — encoding a value that disagrees
with `CARD_RULES.md` would be a balance change, which only a
`GAMEPLAY-CHANGE` task may make. The Shield Basic Card's `CARD_RULES.md` §2
value is already correct and needs no change.

### D-3 — Does TASK-107 remain the correct implementation task?

**ANSWERED — Product Owner, 2026-10-01.**

**Decision: TASK-107 is unblocked from the contract perspective, and its scope
is unchanged.** It may move to `READY` **only after** lifecycle validation
confirms all three of:

```text
[ ] the effect identity contract exists
[ ] the magnitude carrier exists
[ ] the implementation scope remains unchanged
```

TASK-107 must still implement only:

```text
Basic Cards
the existing Shield contract
the existing Heal contract
the existing Power contract
```

**No PetSkillCast.** This task does **not** perform that lifecycle transition —
it records the impact only. TASK-107 is byte-identical and remains `BACKLOG`
until a reviewer/orchestrator validates the three conditions above
(`TASK_LIFECYCLE.md` §3, §4).

**A sequencing note the lifecycle validation must account for:** the effect
identity and magnitude carrier are *decided* by this task but not yet
*implemented*. The structured `EffectDefinition` runtime support is the
follow-up task recorded in "Follow-up Tasks Required". Because TASK-107's
resolver reads that structured data, TASK-107 cannot be *executed* before the
runtime support exists, even once it is `READY`. Whether TASK-107 becomes
`READY` now or waits for the runtime support to land is a **sequencing
decision for the orchestrator/reviewer** — this task records the dependency
and does not make that call.

```text
Question: Once D-1/D-2 are answered, is TASK-107's scope still the smallest
          coherent unit, or must it be re-scoped?

If Option A: the schema/vocabulary change is a separate prerequisite task, and
          TASK-107 keeps its scope.
If Option B or C: TASK-107 keeps its scope and implements the mapping.

This task REPORTS the answer's consequence for TASK-107. It does NOT modify
TASK-107 (its file, scope, acceptance criteria, or status) — that is a
lifecycle act performed by the orchestrator/reviewer after this task lands.
```

### D-4 — Does PetSkillCast remain blocked?

**ANSWERED — Product Owner, 2026-10-01.**

**Decision: PetSkillCast stays separate; Tidal Barrier remains blocked.** The
D-1/D-2 decision does **not** resolve:

```text
Pet Skill effect content
Tidal Barrier's Shield magnitude
Thanh Xà / Sơn Hùng Signature Skill content
```

The two contracts must not be merged. PetSkillCast must not be included in the
Basic CardCast implementation. Verified at decision time:
`CARD_RULES.md` §4.1 still records Tidal Barrier's effect as "Heal; Gain
Shield" with **no magnitude**, and TASK-104 §5 / its B-4 disposition still
records that as an unauthored content gap deferred to a separate gameplay
decision.

```text
Question: Tidal Barrier's Shield magnitude is unauthored (CARD_RULES.md §4.1:
          "Heal; Gain Shield"), recorded by TASK-104 §5 / B-4 as deferred to a
          separate gameplay-content decision.

Determination to CONFIRM (not decide): resolving D-1/D-2 does not author that
          magnitude. PetSkillCast therefore REMAINS BLOCKED after this task,
          and no Pet Skill Card effect may be implemented until that separate
          content decision exists. This task must record that explicitly so no
          later agent reads D-1/D-2 as unblocking PetSkillCast.
```

---

## Scope

### In Scope

1. **Present the gap precisely and completely**, evidenced by file + section,
   for each decision point in §"Decision Inputs" — including the conflict
   between TASK-107's instruction to resolve effects from Card content and
   `DATABASE.md` §1's recorded exclusion of effect-id vocabulary.
2. **Obtain a human/Product-Owner answer** for D-1 and D-2 (and an explicit
   determination for D-3/D-4). Record each answer **verbatim**, with its date and
   its author, in this task's Completion Evidence — the TASK-104 precedent. If
   no answer is supplied, the task reports itself as awaiting input and stops.
3. **Record each settled answer**, without duplication
   (`documentation/documentation-change.md` §2 — one concept, one owner).
   **As executed:** all four answers are recorded in this task file's Completion
   Evidence, and the `docs/` write is **deferred** to the follow-up task,
   because the answer chosen (structured `EffectDefinition`) reverses
   TASK-082 R2-7 — a DONE, immutable task whose ruling `DATABASE.md` §1 currently
   carries. Correcting `DATABASE.md` is therefore a supersession act that must
   be made deliberately by the task that implements the new contract, not
   silently by a decision-recording task (`AGENTS.md` §4, §17;
   `TASK_LIFECYCLE.md` §3). The candidate owners, had the answer been
   non-conflicting:
   - `docs/02-technical/DATABASE.md` §1 — if the answer changes what
     `CardDefinition` stores. Note this document owns the *storage shape*, not
     the Card rule.
   - `docs/01-game-design/CARD_RULES.md` §2/§3 — only if the answer requires the
     effect semantics themselves to be stated differently; **no magnitude, Cost,
     or balance value may be authored or altered** (`CARD_RULES.md` §2's values
     are already authored and are not this task's to change).
   - The `.ai/` or `tasks/` layer is **not** an owner for any of this.
4. **State explicitly whether the recorded answer requires a follow-up task**
   (a schema/migration change, a new Domain member, or an ADR), and if so
   describe that task's scope in the Completion Evidence so it can be created
   separately. **Do not create it as part of this task, and do not implement
   it.**
5. **Record the D-4 determination** so PetSkillCast's blocked status survives
   this task unchanged.

### Out of Scope

**Explicit prohibitions (stated as required):**

```text
No Match-3 gameplay.
No client-authoritative state.
No undocumented events.
No undocumented SignalR methods.
No speculative database schema.
No new gameplay rules.
```

- **Authoring the answer.** Choosing an effect vocabulary, an identifier form,
  an enum member, a magnitude carrier, a column, or any numeric value is the
  single prohibited act of this task (`AGENTS.md` §7, §20). An agent that finds
  §"Decision Inputs" unanswered must report the task as awaiting input — not
  answer it, not rank the options, not pick a default.
- **Implementing anything.** Zero files under `src/` or `tests/`. No Card-effect
  resolver, no `CardCast`, no `BattleEvent` member, no wire-projection arm.
- **Any schema, entity, column, constraint, or migration change.** If the
  answer implies one, it is a separate follow-up task — reported, not applied.
- **Modifying TASK-107** — not its file, scope, acceptance criteria, status, or
  folder. Whether it ascends to READY, and whether its scope changes, is a
  lifecycle act for the orchestrator/reviewer after this task lands.
- **Re-opening or modifying TASK-082.** Its R2-7 decision is a DONE, immutable
  record. If the answer reverses it, that reversal is a NEW task with its own
  type and approval path — this task only reports that a reversal is required.
- **Modifying TASK-102, TASK-104, TASK-105, TASK-106, TASK-036, TASK-079,
  TASK-099, or any `tasks/completed/*` file.**
- **Any Shield semantic.** One instance per entity, refresh replaces magnitude,
  no additive stacking, absorption before HP, overflow by remainder, and
  `ShieldDepleted` is not a Battle Event are all frozen (`COMBAT_RULES.md` §4,
  TASK-105). A contract gap in how a Card *reaches* Shield is not a license to
  touch Shield.
- **Authoring Tidal Barrier's Shield magnitude** (`CARD_RULES.md` §4.1) or any
  Thanh Xà / Sơn Hùng Signature Skill content (`PET_RULES.md` §8). Both remain
  TBD/deferred; no placeholder, no invented value.
- **Any new gameplay rule, Card, Pet, Relic, Element, Status Effect type,
  resource, or progression system**; any change to Match-3, Swap, Combo,
  Passive, Boss Response, the Damage Pipeline formula, or the resolution order.
- **Any SignalR method, event, wire member, Redis key, API endpoint, or client
  work.**
- **Any ADR** — unless the obtained answer genuinely requires one, in which case
  it is reported for a separate task (`AGENTS.md` §18).
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2.

---

## Affected Files & Areas

```text
[ ] src/                                                    — NONE
[ ] tests/                                                  — NONE
[ ] docs/01-game-design/CARD_RULES.md                       — NONE (the decision
      changed no effect semantics and no magnitude, Cost, or balance value;
      §2's values remain authoritative and unaltered)
[ ] docs/02-technical/DATABASE.md                           — NONE in this task.
      The answer REQUIRES a correction here (it reverses TASK-082 R2-7's
      verbatim-prose ruling this document carries), but TASK-082 is DONE and
      immutable, so the supersession is REPORTED and applied by the follow-up
      implementation task, not here.
[ ] docs/02-technical/GAME_STATE.md                          — NONE (no state
      member is proposed; §0 item 5 is the rule the answer must respect)
[ ] docs/01-game-design/COMBAT_RULES.md                      — NONE (Shield and
      Heal are frozen; this task must not touch them)
[ ] docs/02-technical/ARCHITECTURE.md / TDD.md               — NONE
[ ] docs/00-overview/                                        — NONE
[ ] docs/03-decisions/ADR/                                   — NONE (no ADR is
      required by this decision; see Completion Evidence)
[x] tasks/backlog/TASK-108-resolve-card-effect-resolution-contract.md
      (this file: Status, the four answered decisions, the two recorded
       consequence sections, and the Completion Evidence)
[ ] tasks/backlog/TASK-107-…md                               — NOT modified
```

**As executed:** the only `[?]` (`DATABASE.md`) resolved to `[ ]` — the needed
correction is reported for the follow-up task rather than applied, because the
chosen answer supersedes an immutable DONE decision. No `docs/` file was
touched.

---

## Implementation Notes

- **The work is evidence and recording, not authoring.** Read
  `DATABASE.md` §1, `CARD_RULES.md` §2–§3, and `CardDefinition.cs` side by side,
  and state what each does and does not carry. Then obtain the answer.
- **Do not answer §"Decision Inputs".** This is the task's one hard rule. An
  agent that supplies an effect vocabulary "to be helpful" has violated
  `AGENTS.md` §7 and produced a contract the Product Owner never approved. If
  the answers are absent, record the task as awaiting input and stop cleanly.
- **Preserve the distinction between the game rule and its carrier.** "The
  Shield Basic Card grants Shield equal to a proportion of Max HP"
  (`CARD_RULES.md` §2) is a **resolved game rule**. "The runtime can determine
  which effect and magnitude a `CardDefinition` has" is an **unresolved
  contract**. Do not let the recording of the second look like a change to the
  first.
- **Do not read TASK-107's blocked state as a TASK-107 defect.** TASK-107 is a
  correct execution manifest for the behavior it describes; the gap is upstream
  of it and was not visible until its resolver was specified. Report it as a
  discovery of this pass, not as an error in TASK-107.
- **Cite, do not restate.** Use the `<c>DATABASE.md</c> §1` citation idiom.
  Never copy a Cost, a magnitude, a percentage, or a schema into this task file
  (`AGENTS.md` §9, `tasks/README.md` §9). The quoted prose strings in §"Current
  State" are quoted **as evidence of the gap** — keep that framing and do not
  extend it into a table of effect values.
- **One concept, one owner.** A decision about what `CardDefinition` *stores*
  belongs in `DATABASE.md`; a decision about what an effect *does* belongs in
  `CARD_RULES.md` / `COMBAT_RULES.md`. Do not duplicate a rule between them to
  make it easier to find (`documentation-change.md` §2).
- **Check the ADR question rather than assuming it.**
  `docs/03-decisions/README.md` §8's "Known Open Items (Not ADRs)" is the
  precedent for recording something as deliberately *not* an ADR; use it if the
  answer lands there. If the answer does require an ADR, report it — do not
  write one (`AGENTS.md` §18).
- **Report, do not fix, adjacent gaps** (`AGENTS.md` §16). Known and already
  reported, none belonging to this task: Tidal Barrier's unauthored Shield
  magnitude (TASK-104 B-4); Thanh Xà / Sơn Hùng Signature Skill content
  (`PET_RULES.md` §8); the `BattleStateJson.cs` items TASK-097/TASK-098
  reported; and the `SIGNALR_PROTOCOL.md` "§3.3" and `BOSS_RULES.md` §4
  citation nits.

---

## Testing Requirements

This task changes no code and authors no rule, so it produces no unit,
integration, or gameplay test. Its verification is a **documentation
consistency audit**, a **change-isolation check**, and a **scope validation**,
at the depth `core/validation.md` §2 requires for a MEDIUM-risk DOCUMENTATION
task.

### Required Verification

```text
[x] Documentation consistency audit — the recorded answer vs. every document
                                      that states or consumes it: DATABASE.md
                                      §1 (if touched) ↔ CARD_RULES.md §2–§3 ↔
                                      CardDefinition.cs ↔ TASK-107's scope.
                                      Confirm no rule is duplicated and no two
                                      documents disagree after the change.
[x] Change-isolation check          — SHA256 of COMBAT_RULES.md, GAME_STATE.md,
                                      CARD_RULES.md, DATABASE.md, and TASK-107
                                      before and after; only the documents the
                                      answer actually requires may differ, and
                                      COMBAT_RULES.md / TASK-107 must be
                                      byte-identical.
[x] Gap-evidence re-verification    — independently re-confirm the gap still
                                      exists as stated (repo-wide search for an
                                      effect vocabulary returns zero; the
                                      provisioned EffectDefinition values are
                                      verbatim prose), so the task is not
                                      resolved against a stale premise.
[x] Scope validation                — MVP_SCOPE.md §1/§2;
                                      quality/scope-validation
[ ] Unit tests                      — N/A (no code)
[ ] Integration tests               — N/A (no code)
[ ] Gameplay scenarios              — N/A (no code, no rule change)
```

### Key Edge Cases

- **The answer reverses TASK-082 R2-7.** Option A contradicts a DONE decision.
  This must be reported as requiring a new task with its own approval path — not
  applied inline, and not treated as an edit of TASK-082
  (`TASK_LIFECYCLE.md` §3).
- **The answer implies a schema or migration consequence.** Reported and
  deferred to a separate implementation task; this task applies no schema
  change.
- **The answer implies an ADR.** Reported; not authored here.
- **A partial answer.** If D-1 is answered but D-2 is not (or vice versa), record
  what was answered, state plainly what remains open, and leave TASK-107
  blocked. Do not infer the unanswered half.
- **The answer conflicts with CARD_RULES.md §2's prose.** If a chosen effect
  identity cannot express a documented effect, that is a contract conflict →
  STOP per `AGENTS.md` §4 and report both sources.
- **A recorded answer that authorizes no magnitude for an effect that needs
  one** — e.g. it fixes the Shield effect identity but not where its pool comes
  from. Record the residual gap; TASK-107 remains blocked on it.
- **Tidal Barrier / Thanh Xà / Sơn Hùng** — confirm the answer does not
  accidentally unblock PetSkillCast (D-4), and that no magnitude is invented.
- **`CARD_RULES.md` §2's values must survive verbatim.** If the change appears to
  require altering a Cost or an effect magnitude, that is a GAMEPLAY-CHANGE and
  a different task — STOP.

---

## Stop Conditions

Universal `AGENTS.md` §20 / `.ai/README.md` §13 stops always apply.
Task-specific:

1. **If no Product Owner / human answer to §"Decision Inputs" is supplied** —
   **STOP and report the task as awaiting input.** Do not answer it, do not rank
   the options, do not pick a default, and do not author a vocabulary
   (`AGENTS.md` §7, §20 — "missing rule"). Record the awaiting-input state in
   Completion Evidence and leave the file in `backlog/`.
2. **If resolving the gap would require authoring a game rule, an identifier, a
   magnitude, or a balance value** — **STOP** (`AGENTS.md` §7). The Card rules
   are already authored in `CARD_RULES.md`; this task resolves their carrier,
   not their content.
3. **If the recorded answer contradicts an existing authoritative document** —
   e.g. it cannot express a `CARD_RULES.md` §2 effect, or it violates
   `GAME_STATE.md` §0 item 5 — **STOP per `AGENTS.md` §4** and report both
   sources rather than choosing one.
4. **If the answer requires a schema, entity, column, constraint, or migration
   change** — **STOP for this task's boundary**; record the requirement and its
   scope in Completion Evidence for a separate implementation task. Do not write
   a migration, do not edit `DATABASE.md`'s schema description beyond recording
   the decision itself, and do not modify `src/` or `tests/`.
5. **If the answer requires an ADR** — **STOP and report** (`AGENTS.md` §18);
   do not create one here.
6. **If satisfying any criterion would require modifying TASK-107** — **STOP**;
   report the required lifecycle act instead (`TASK_LIFECYCLE.md` §4 — a status
   change is the orchestrator's/reviewer's, and this task must not
   re-scope or re-status another task).
7. **If satisfying any criterion would require modifying TASK-082, TASK-102,
   TASK-104, TASK-105, TASK-106, TASK-036, TASK-079, TASK-099, or any
   `tasks/completed/*` file** — **STOP**; report the stale or reversed statement
   instead (`TASK_LIFECYCLE.md` §3 — completed tasks are immutable).
8. **If any part would change Shield semantics, or touch
   `COMBAT_RULES.md` §4** — **STOP**; Shield is resolved and frozen.
9. **If any part would author Tidal Barrier's Shield magnitude, or any Thanh Xà /
   Sơn Hùng Signature Skill content** — **STOP**; those are separate content
   decisions (`CARD_RULES.md` §4.1, `PET_RULES.md` §8, TASK-104 B-4).
10. **If the change would require a SignalR method, a protocol member, a new
    event, a Redis key, an API endpoint, or client work** — **STOP**; report it
    for a separate task.
11. **If the change would cross into `MVP_SCOPE.md` §2's OUT list, or reach an
    unlisted (FUTURE) system** — **STOP** (`AGENTS.md` §8, `MVP_SCOPE.md` §4).
12. **If the task exceeds 7 skills or crosses an uncoupled architectural
    boundary** — **STOP and decompose** (`tasks/README.md` §13).
13. **If the gap turns out not to exist** — i.e. an effect-resolution contract is
    found already defined somewhere this pass missed — **STOP and report.**
    Do not record a decision for a question that is already answered; report the
    location instead and let the lifecycle close this task as unnecessary.

When stopped, report the exact condition and **do not invent a resolution**.

---

## Completion Evidence

<!--
  TO BE COMPLETED BY THE EXECUTING AGENT after reaching DONE.
  Record a STOP report here instead if §"Decision Inputs" is unanswered — do not
  claim a resolution that was not made, and do not author the answer.
-->

**Picker-up note.** Record before editing: (a) the SHA256 of
`docs/02-technical/DATABASE.md`, `docs/01-game-design/CARD_RULES.md`,
`docs/01-game-design/COMBAT_RULES.md`, `docs/02-technical/GAME_STATE.md`, and
`tasks/backlog/TASK-107-implement-basic-cardcast-server-path.md` as the
change-isolation baseline; (b) the output of `git status`. The working tree
carries uncommitted changes from earlier tasks, so the baseline is the
working-tree content at pickup, **not** `HEAD`. No build or test run is
required or expected (no code change).

### Product Owner Decisions (recorded verbatim, per the TASK-104 precedent)

```text
Recorded by:  Product Owner (via requester)   Date: 2026-10-01

D-1 — Effect Identity Carrier
  "Card effects MUST use a structured EffectDefinition contract. The existing
   prose-only EffectDefinition approach is insufficient for runtime resolution
   because the server must deterministically identify which domain effect a
   Card applies. The runtime MUST NOT parse human-readable prose, infer
   behavior from Card name, map behavior from CardId, or hardcode card-specific
   logic. Effect identity MUST come from structured effect data."

D-2 — Effect Magnitude Carrier
  "Effect magnitude MUST also be data-driven inside the structured
   EffectDefinition. Magnitude MUST describe the effect value, the value
   interpretation, and the calculation rule. The exact card balance values
   remain owned by CARD_RULES.md. This decision defines only the runtime
   contract."

  Illustrative shapes supplied (SHAPE EXAMPLES — not authored balance values;
  see "Balance-Value Boundary"):
    Shield : { "effectType": "Shield", "valueType": "PercentMaxHp", "value": 20 }
    Heal   : { "effectType": "Heal",   "valueType": "PercentMaxHp", "value": 30 }
    Power  : { "effectType": "Power",  "valueType": "Flat",         "value": 5  }

D-3 — TASK-107 Impact
  "TASK-107 is now unblocked from the contract perspective. TASK-107 may be
   moved to READY only after lifecycle validation confirms: effect identity
   contract exists; magnitude carrier exists; implementation scope remains
   unchanged. TASK-107 must still implement only Basic Cards, existing Shield
   contract, existing Heal contract, existing Power contract. No PetSkillCast."

D-4 — PetSkillCast Separation
  "Keep separate. Tidal Barrier remains blocked. The decision does NOT resolve
   Pet Skill effect content, Shield magnitude for Tidal Barrier, or Signature
   Skill content."
```

### Resolution Record

```text
D-1  RESOLVED — structured EffectDefinition is the effect identity carrier.
D-2  RESOLVED — structured EffectDefinition carries the magnitude/value rule
                 (effectType + valueType + value).
D-3  RESOLVED — TASK-107 unblocked from the contract perspective; scope
                 unchanged; READY subject to lifecycle validation; NOT
                 transitioned by this task.
D-4  CONFIRMED — PetSkillCast stays separate; Tidal Barrier remains blocked.

Explicitly REJECTED approaches (recorded per the decision):
  * prose parsing of EffectDefinition
  * CardDefinitionId / cardId hardcoding (switch statements)
  * runtime inference from Card name or other display text
  * hidden gameplay values in the resolver

Contract settled:  CardDefinition → EffectDefinition (structured) →
                   deterministic Effect Resolver → ApplyHeal / ApplyShield /
                   ApplyPower → BattleEvent → SignalR projection
Resolver rules:    deterministic; dispatches on structured data only; no
                   cardId switch, no hardcoded behaviour, no hidden values
                   (GAME_RULES.md §17/§18, TDD.md §6)
```

### TASK-082 R2-7 Supersession (REPORTED — the reversal is NOT applied here)

```text
SUPERSEDED:  TASK-082 decision R2-7 (DONE task, IMMUTABLE)
             "Do not introduce an `effect-{slug}` vocabulary or any new
              effect-reference identifier system. The values must be copied
              from the authoritative domain documentation verbatim."

SUPERSEDED BY: this task's D-1 answer, which requires a structured
             EffectDefinition — i.e. exactly the effect-reference identifier
             system R2-7 excluded.

CARRIED IN:  docs/02-technical/DATABASE.md §1 (the CardDefinition block) and
             its version header both still state R2-7's verbatim-prose ruling.
             They are STALE as of this decision.

NOT DONE HERE: this task did not edit TASK-082 (immutable), did not edit
             DATABASE.md, and did not apply the reversal anywhere in docs/.
             Applying it is a DOCUMENTATION change owned by the follow-up
             implementation task — reported under "Follow-Up Tasks Required".

DOCS IMPACT: docs/02-technical/DATABASE.md §1's CardDefinition `EffectDefinition`
             member description, and the version-header restatement, must be
             corrected to describe the structured contract. The 128-character
             column limit R2-7 also referenced is a consequence for the
             follow-up task to resolve (a structured payload may not fit it).
```

### Changed Files

- `tasks/backlog/TASK-108-resolve-card-effect-resolution-contract.md` — this
  file: `Status` `BACKLOG` → `IN REVIEW`; the "Status note" updated with the
  supersession; D-1…D-4 answered in place with their recorded decisions; two
  recorded-consequence sections added (D-5 Resolver Contract, D-6 Balance-Value
  Boundary); and this Completion Evidence. **No acceptance criterion was
  weakened, removed, or re-scoped; no contract text was authored in `docs/`.**

**No file under `docs/`, `src/`, or `tests/` was changed. TASK-107 is
byte-identical.**

### Formal Review Pass (quality/review.md & core/completion.md)

- **Correctness:** PASS — Product Owner decisions D-1 through D-4 recorded verbatim without alteration or unauthorized design invention. D-5 resolver contract and D-6 balance-value boundaries explicitly documented.
- **Architecture:** PASS — Conforms to `docs/02-technical/ARCHITECTURE.md` and `docs/02-technical/TDD.md`. Server-authoritative model preserved.
- **Scope:** PASS — Confined strictly to decision recording in TASK-108. 0 files modified under `docs/`, `src/`, `tests/`, `docs/03-decisions/ADR/`.
- **Tests:** PASS (N/A) — Decision-input / documentation task; no code changes. Change-isolation and consistency audits passed.
- **Documentation:** PASS — Documentation consistency verified; supersession of TASK-082 R2-7 reported cleanly for implementation follow-ups.
- **Security:** PASS — No security or authentication implications.
- **Performance:** PASS — Hot-path constraints respected (PostgreSQL off hot resolution path).
- **Maintainability:** PASS — Data-driven structured effect contract avoids brittle cardId switch statements.
- **Determinism (Gameplay/Battle Logic):** PASS — Deterministic dispatch mandated; client authority avoided.
- **Definition of Done (core/completion.md §1):** PASS — All completion criteria satisfied for a decision-input task.

### Validation Results

- Decision completeness — **PASS.** All four decision points carry an explicit
  Product Owner answer recorded verbatim with its date; no point was left
  inferred, ranked, or defaulted by an agent.
- Gap re-verification — **PASS.** Independently re-confirmed before recording:
  repo-wide search for a Card-effect vocabulary returns zero matches in `src/`
  and `tests/`; `CardDefinition.EffectDefinition` is only ever stored, never
  resolved; the provisioned rows remain verbatim prose. The decision was
  recorded against a live premise, not a stale one.
- Conflict/supersession check — **PASS with a reported finding.** The decision
  reverses TASK-082 R2-7. It is recorded and reported above rather than applied
  to `docs/` (`AGENTS.md` §4, §17; TASK-108 stop conditions 3 and 7).
- Change-isolation check — **PASS.** `docs/02-technical/DATABASE.md`
  `B079C2245AA2884F…`, `docs/01-game-design/CARD_RULES.md` `F08C2490306ED662…`,
  and `tasks/backlog/TASK-107-…md` `6A8987077D5FC07B…` are byte-identical to
  pickup. The only file whose hash moved is this one.
- Scope validation (`MVP_SCOPE.md` §1/§2) — **PASS.** Cards and Combat/Status
  Effects are IN; this task adds no system and reaches no OUT or unlisted item.
  The change is confined to one task file.
- `dotnet build` / `dotnet test` / `npm test` — **N/A and not run**: this task
  changes no code and its own §"Testing Requirements" states no test is
  produced.

### Contract Preservation Verification

```text
Card rules             UNCHANGED (CARD_RULES.md byte-identical F08C2490…; no
                         Cost, effect, or balance value authored or altered)
Shield contract        UNCHANGED (COMBAT_RULES.md §4 untouched; TASK-105's
                         refresh/one-instance/no-accumulation/depletion intact)
Shield representation  UNCHANGED (existing StatusEffect; ApplyShield untouched)
Damage Pipeline        UNCHANGED
BattleState schema     UNCHANGED (no new member)
PetState               UNCHANGED
Redis / SignalR / API  UNCHANGED (no key, method, member, or endpoint)
PostgreSQL             UNCHANGED (no schema, entity, column, or migration; the
                         decision's DB consequence is REPORTED for a separate
                         task, none applied here)
DATABASE.md            UNCHANGED (still carries R2-7's wording; the needed
                         correction is REPORTED, not applied)
src/ and tests/        UNCHANGED (byte-identical)
docs/03-decisions/     UNCHANGED (0 ADRs)
TASK-107               UNCHANGED (byte-identical; not re-scoped, not
                         re-statused, not moved)
TASK-082/102/103/104/105/106, TASK-036/079/099, tasks/completed/  UNCHANGED
Tidal Barrier / Thanh Xà / Sơn Hùng   UNCHANGED (not authored, not invented)
```

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic introduced
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
- [x] Confirmed no new gameplay rule, event, wire member, state field, or
      SignalR method
- [x] Confirmed no Shield semantic changed and no balance value invented
- [x] Confirmed PetSkillCast remains blocked (D-4) and no Pet Skill content
      was authored

### Follow-Up Tasks Required (REPORTED, NOT CREATED HERE)

1. **EffectDefinition structured runtime support** (required).
   Scope: the JSON representation of the structured effect payload; its
   serialization/deserialization; the EF mapping; a migration if the current
   `character varying(128)` column cannot carry it; and validation rules for
   well-formed effect data. It also owns the `docs/02-technical/DATABASE.md` §1
   correction that supersedes TASK-082 R2-7 (see "TASK-082 R2-7 Supersession").
   Type: expected `FEATURE` for the runtime support plus a `DOCUMENTATION`
   component (or two tasks if the type rule requires it — `TASK_TYPES.md` §3
   forbids blending types). Must confirm each encoded value against
   `CARD_RULES.md` §2 before encoding, so the balance values stay owned there.
   Note: TASK-107 depends on this landing before it can be *executed*.

2. **Tidal Barrier Shield magnitude** (required, separate — unchanged).
   `CARD_RULES.md` §4.1 still gives "Heal; Gain Shield" with no magnitude.
   Deferred by the Product Owner (TASK-104 §5 / B-4). Blocks PetSkillCast only.

3. **TASK-107 lifecycle validation** (an act, not a new task).
   Whether TASK-107 ascends `BACKLOG → READY` — and whether it does so now or
   after (1) lands — is the orchestrator's/reviewer's call
   (`TASK_LIFECYCLE.md` §3, §4). This task records the impact and makes no
   transition.

### Unrelated Stale Documentation Discovered (REPORTED, NOT CHANGED)
- **`docs/02-technical/DATABASE.md` §1 + version header carry TASK-082 R2-7's
  now-superseded verbatim-prose ruling.** Impact: the document no longer matches
  the decided contract; a reader would conclude prose is the contract.
  Not changed here — applying the reversal belongs to the follow-up
  implementation task named above (`AGENTS.md` §16, §17).
- **The 128-character `EffectDefinition` column limit** (`DATABASE.md` §1, and
  the `character varying(128)` column in `20260925150903_AddCardPersistence.cs`)
  was chosen for prose and may not carry a structured payload. Reported for the
  follow-up task; no schema change made here.
- **Tidal Barrier's Shield magnitude remains unauthored** (`CARD_RULES.md`
  §4.1) — unchanged and still deferred (TASK-104 B-4). No value invented.
- **Thanh Xà / Sơn Hùng Signature Skill content** remains TBD
  (`PET_RULES.md` §8) — unrelated, reported only.
