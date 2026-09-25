# TASK-039 — Resolve Card Loadout Duplicate and Validation Contract

## Status

DONE

(BLOCKED → IN PROGRESS → IN REVIEW → DONE per `TASK_LIFECYCLE.md`
§2–§3. Stop condition §6 fired during execution round 1; the human
gameplay decision was then received — per-CardDefinition copy limits,
explicit-value model (Model A) — and the §7–§10 documentation updates
were executed. §13 consistency validation and the `quality/review.md` §1
documentation checks passed. The round-1 stop report is retained below
as Appendix A (superseded by the decision recorded in the Final Report).)

## Type

DOCUMENTATION

## Priority

P1 — Blocks TASK-028 Card Ownership & Loadout Snapshot

## Dependencies

None.

This task blocks:

```text
TASK-028 — Card Ownership & Loadout Snapshot
```

---

# 1. OBJECTIVE

Resolve the remaining authoritative documentation ambiguity in the
**MVP Card Loadout contract** so that TASK-028 can be implemented
deterministically.

TASK-028 readiness audit found one root gameplay-contract ambiguity:

> What happens when the `cardLoadout` submitted to `POST /api/battle/start`
> contains the same Basic Card more than once?

This ambiguity also leaves the card-side API validation contract
incomplete.

This task is **documentation-only**.

Do NOT implement source code.

Do NOT modify TASK-028 implementation behavior.

Do NOT modify TASK-027.

---

# 2. READ FIRST

Before making any documentation change, read:

```text
AGENTS.md

tasks/README.md
tasks/TASK_LIFECYCLE.md
tasks/TASK_TYPES.md
tasks/TASK_TEMPLATE.md

.ai/README.md
.ai/agents/orchestrator.md
.ai/agents/gameplay.md
.ai/agents/backend.md
.ai/agents/testing.md
.ai/agents/review.md

docs/01-game-design/CARD_RULES.md
docs/01-game-design/GAME_RULES.md

docs/02-technical/GAME_STATE.md
docs/02-technical/API_CONTRACTS.md
docs/02-technical/DATABASE.md
docs/02-technical/SIGNALR_PROTOCOL.md
docs/02-technical/GAME_EVENTS.md

docs/03-decisions/ADR-011*.md
docs/03-decisions/ADR-012*.md
```

Also inspect:

```text
tasks/completed/TASK-038*.md
tasks/backlog/TASK-028*.md
```

and the actual current implementation only where necessary to verify
that the documentation task does not conflict with the repository.

Do not perform a broad repository audit.

---

# 3. AUTHORITATIVE CONTRACT ALREADY SETTLED

Do NOT reopen decisions that are already resolved.

The current documented Card model is:

```text
Player
  │
  └── PlayerUnlockedCard
          │
          └── CardDefinition
```

Cards are unlock flags, not per-instance inventory.

There is:

```text
CardDefinitionId
```

but no:

```text
CardInstanceId
CardInstance
CardTier
CardStar
CardLevel
Pet.CardInventory
```

The MVP battle loadout is:

```text
3 submitted Basic Cards
+
1 derived Signature Skill Card
=
4 equipped cards
```

The Signature Skill Card is derived from the active Pet's:

```text
PetDefinition.SignatureSkillCardId
```

and is not submitted in `cardLoadout`.

The submitted Basic Cards are selected at:

```text
POST /api/battle/start
```

and the resulting card set is snapshotted into:

```text
PetState.EquippedCards[]
```

for that battle.

Do not change these contracts.

---

# 4. ROOT OPEN DECISION — DUPLICATE BASIC CARDS

Resolve this question:

> Must the three submitted Basic Cards be distinct?

Example:

```json
{
  "cardLoadout": [
    "heal",
    "heal",
    "shield"
  ]
}
```

There are two materially different possible semantics.

### Option A — Distinct Basic Cards Required

The three submitted Basic Cards must be pairwise distinct.

Example:

```text
["heal", "shield", "power"]
```

is valid.

```text
["heal", "heal", "shield"]
```

is rejected.

The rejection must use the documented invalid-loadout mechanism.

### Option B — Duplicate Basic Cards Permitted

Repeated Basic Card definitions are permitted as long as:

* exactly 3 Basic Card entries are submitted;
* every submitted CardDefinitionId is owned/unlocked by the Player;
* every submitted card is a valid Basic Card.

Example:

```text
["heal", "heal", "shield"]
```

is valid.

Do NOT select an option based on:

* implementation convenience;
* similarity to Relics;
* TASK-027 behavior;
* assumed deck-building conventions;
* what is easiest to code;
* what appears more common in other games.

The decision must come from the authoritative gameplay contract or an
explicit human gameplay decision.

---

# 5. EVIDENCE REVIEW

Inspect all relevant authoritative sources for evidence.

At minimum evaluate:

```text
CARD_RULES.md §1
CARD_RULES.md §3
GAME_RULES.md §11
GAME_STATE.md §2.3
API_CONTRACTS.md §3
DATABASE.md §1–§2
ADR-011
ADR-012
```

Pay particular attention to whether the wording:

```text
3 Basic Cards
4 total Cards
Cards available to cast
```

means:

* three distinct selectable cards, or
* three loadout entries that may reference the same definition.

Also inspect whether card casting semantics consume/remove a card from
the battle loadout.

Do not infer a distinctness rule merely because Relics have one.
Relics are governed by their own contract.

---

# 6. STOP CONDITION — HUMAN GAMEPLAY DECISION

If the authoritative documentation does NOT conclusively determine
whether duplicates are permitted:

**STOP.**

Do not choose Option A or Option B.

Record the task as requiring a human gameplay decision.

The task report must state:

```text
Human decision required:
Are the 3 submitted Basic Cards required to be distinct?
```

and present:

```text
Option A — distinct required
Option B — duplicates permitted
```

with the relevant evidence and gameplay-contract consequences.

Do not implement either behavior.

Do not update API validation as though one option had already been
selected.

---

# 7. CANONICAL DOCUMENT UPDATE

Once the duplicate policy is explicitly determined:

## 7.1 CARD_RULES.md

Update the canonical gameplay owner first.

In:

```text
CARD_RULES.md §1
```

state the selected duplicate policy explicitly.

The wording must be deterministic.

It must make clear whether:

```text
cardLoadout = [A, A, B]
```

is valid or invalid.

Do not rely on implications such as:

```text
3 Basic Cards
```

to communicate the rule.

---

# 8. API_CONTRACTS.md

After the gameplay rule is explicit, update:

```text
docs/02-technical/API_CONTRACTS.md §3
```

so `cardLoadout` has a deterministic validation contract.

The card validation section must explicitly define:

1. Required count:

   ```text
   exactly 3 submitted Basic Cards
   ```

2. Ownership:

   ```text
   every submitted CardDefinitionId must be unlocked by the requesting
   Player
   ```

3. Category:

   ```text
   every submitted card must be Category = Basic
   ```

4. Duplicate handling:

   * explicitly state the selected rule.

5. Invalid-loadout mapping:

   * define the exact documented error mapping for invalid card loadout
     input.

6. Snapshot behavior:

   * explicitly state how the submitted cards become the battle-scoped
     `PetState.EquippedCards[]`.

7. Signature Skill:

   * explicitly state that the Signature Skill Card is derived from the
     active Pet and is not part of the submitted `cardLoadout`.

Do not accidentally change the already-resolved Relic validation
contract.

Keep the Relic validation block unchanged unless a genuine
cross-contract consistency issue is discovered.

---

# 9. GAME_STATE.md HYGIENE

Update:

```text
docs/02-technical/GAME_STATE.md §2.3
```

to make the Card snapshot representation explicit.

State that:

```text
PetState.EquippedCards[]
```

contains:

```text
CardDefinitionId
```

for the four battle-scoped equipped cards.

Also explicitly state:

```text
Array order has no gameplay significance.
```

unless the authoritative gameplay decision establishes that order has
meaning.

Do not invent slot semantics.

Do not introduce:

```text
CardSlot
CardSlotIndex
BasicCardSlot
```

unless required by the resolved gameplay contract.

Also correct the stale wording:

```text
ownership of the underlying instances
```

for Cards.

Cards are not instances.

Use wording consistent with:

```text
PlayerUnlockedCard
CardDefinitionId
Player-owned unlock
battle-scoped snapshot
```

Do not change the already-resolved Relic wording unless necessary.

---

# 10. TASK-028 CLEANUP

After the documentation contract is resolved, make only the necessary
task-definition corrections to:

```text
tasks/backlog/TASK-028*.md
```

Correct the stale statement:

```text
GameDbContext has no sets
```

because the repository now contains:

```text
Players
Pets
PetDefinitions
Relics
RelicDefinitions
```

Add:

```text
TASK-039
```

to TASK-028's dependency/contract-resolution references as appropriate.

Update any statement that currently claims:

```text
Invalid-loadout errors follow API_CONTRACTS §6
```

if the new API contract establishes the exact card error mapping
elsewhere.

Do not rewrite TASK-028 into a new implementation design.

Keep it focused on the now-resolved contract.

---

# 11. DO NOT CHANGE

This task MUST NOT change:

```text
source code
database schema
EF migrations
GameDbContext
PetState implementation
Card implementation
Relic implementation
Battle-start implementation
SignalR implementation
Redis implementation
frontend implementation
tests
TASK-027
```

Do not create:

```text
CardDefinition
PlayerUnlockedCard
CardRepository
CardLoadoutService
CardLoadoutValidator
```

Those belong to TASK-028.

---

# 12. NO CONTENT SEEDING

Do not solve the separate content-seeding concern.

Do not add:

```text
CardDefinition seed data
PetDefinition seed data
RelicDefinition seed data
```

This task only resolves the Card Loadout contract.

The undefined content for individual Signature Skills remains a separate
concern.

---

# 13. VALIDATION

After documentation changes:

Verify consistency across:

```text
CARD_RULES.md
GAME_RULES.md
GAME_STATE.md
API_CONTRACTS.md
DATABASE.md
ADR-011
ADR-012
TASK-028
```

Specifically verify:

```text
3 submitted Basic Cards
        ↓
duplicate policy
        ↓
ownership validation
        ↓
Basic category validation
        ↓
battle snapshot
        ↓
3 Basic + 1 derived Signature Skill
        ↓
PetState.EquippedCards[]
```

There must be no contradiction between the gameplay rule and API
validation contract.

---

# 14. ACCEPTANCE CRITERIA

The task is complete only when:

* [ ] The duplicate Basic Card question has been explicitly resolved OR
  explicitly recorded as requiring human gameplay decision.
* [ ] CARD_RULES.md is the canonical owner of the gameplay rule.
* [ ] API_CONTRACTS.md defines deterministic cardLoadout validation.
* [ ] Exact invalid-loadout behavior is documented.
* [ ] Card ownership validation is explicit.
* [ ] Basic-card category validation is explicit.
* [ ] Signature Skill derivation remains explicit.
* [ ] `PetState.EquippedCards[]` identity is explicitly `CardDefinitionId`.
* [ ] Card array ordering is explicitly documented as non-semantic unless
  a human decision establishes otherwise.
* [ ] Card snapshot timing remains battle-start only.
* [ ] Card ownership remains PlayerUnlockedCard-based.
* [ ] No card-instance model is introduced.
* [ ] No Card Level/Tier/Star semantics are introduced.
* [ ] No source code is modified.
* [ ] No database/migration changes are made.
* [ ] TASK-027 is untouched.
* [ ] TASK-028 is updated only where necessary for the resolved contract.
* [ ] No unrelated documentation is rewritten.
* [ ] Documentation consistency check passes.

---

# 15. FINAL REPORT

Provide:

## 1. Status

```text
DONE
```

or:

```text
BLOCKED — HUMAN GAMEPLAY DECISION REQUIRED
```

## 2. Decision

State either:

```text
Option A — Basic Cards must be distinct
```

or:

```text
Option B — Duplicate Basic Cards are permitted
```

or explicitly state that no decision could be derived.

## 3. Evidence

List the authoritative documents and exact sections supporting the
result.

## 4. Documentation Changes

List every modified documentation/task file.

## 5. Final Card Loadout Contract

Show the resulting deterministic contract:

```text
Submitted:
3 Basic Cards

Derived:
1 Signature Skill Card

Total:
4 Equipped Cards
```

and the duplicate policy.

## 6. Implementation Impact

Explain exactly what TASK-028 must now implement.

Do not implement it.

## 7. Scope Verification

Explicitly confirm:

```text
No source code changed.
No database changed.
No migration created.
No Card entity implemented.
No CardLoadoutService implemented.
No battle-start implementation.
No SignalR implementation.
No Redis implementation.
TASK-027 unchanged.
```

---

# FINAL RULE

This task exists only to close the **Card Loadout contract** before
implementation.

Do not infer Card behavior from Relics.

Do not invent duplicate semantics.

Do not invent error codes.

Do not implement code.

If the authoritative documents cannot determine whether duplicate Basic
Cards are allowed, **STOP and require the human gameplay decision**.

Only after this task is DONE should TASK-028 receive another readiness
audit and potentially move:

```text
BACKLOG → READY
```

---
---

# FINAL REPORT (round 2 — current and authoritative)

## 1. Status

```text
DONE
```

## 2. Decisions (human gameplay decision, round 2)

```text
D1 — Duplicates permitted, governed per CardDefinition:
     each CardDefinition carries a loadout copy limit
     (LoadoutCopyLimit) bounding its occurrence count in the
     submitted 3-card Basic loadout. A limit of 1 makes a Basic
     Card loadout-unique. [A, A, B] is valid iff
     LoadoutCopyLimit(A) ≥ 2.
     Supersedes the round-1 global A/B framing.

D2 — Model A: every CardDefinition must define its limit
     explicitly. A missing value is invalid definition data.
     No default value was invented.
```

Concrete limit values are content/balance configuration, deferred to a
future balance/content task — none were invented.

## 3. Evidence

* Round-1 §6 evidence review (Appendix A): no distinctness rule, no
  copy-limit concept, and no consumption rule exists anywhere in
  `docs/` — the global A/B question was not derivable → AGENTS.md §7
  stop → human decision obtained this round.
* Owner assignments after the decision: gameplay rule → `CARD_RULES.md`
  §1 (per AGENTS.md §2 hierarchy); validation sequence + error mapping →
  `API_CONTRACTS.md` §3; snapshot representation → `GAME_STATE.md`
  §2.3; definition field → `DATABASE.md` §1; implementation task →
  `TASK-028`.

## 4. Documentation Changes

| File | Change |
|---|---|
| `docs/01-game-design/CARD_RULES.md` | Version 1.2 → 1.3. §1: new **Loadout copy limit (per CardDefinition)** rule block — formula, 5 numbered rules (duplicates up to limit; explicit value, no default; per-submitted-loadout scope, not inventory; Basic-only, Signature Skill derived/not counted; values = content/balance). |
| `docs/02-technical/API_CONTRACTS.md` | Version 1.3 → 1.4. §3: new **`cardLoadout` validation is determined** block — 4-step sequence (count → ownership → category → copy limit), rejection with `INVALID_LOADOUT` (same documented code as `relicLoadout`), rejected request equips nothing, accepted snapshot = 4 `CardDefinitionId` entries, Signature Skill derived not submitted. Relic validation block left unchanged. |
| `docs/02-technical/GAME_STATE.md` | Version 2.4 → 2.5. §2.3: stale "ownership of the underlying instances" wording corrected for the card side (Cards are unlock rows, not instances); new **`EquippedCards[]`'s element is a definition identity, not an instance** paragraph — `CardDefinitionId` elements, repeats = same definition per copy limit, exactly 4 entries snapshotted at battle start, element order carries no gameplay significance (contrast: relic order = slot order). |
| `docs/02-technical/DATABASE.md` | Version 1.4 → 1.5. §1: `CardDefinition.LoadoutCopyLimit` added (required, explicit, no default, values deferred to content/balance). |
| `tasks/backlog/TASK-028-card-ownership-and-loadout-snapshot.md` | §16 cleanup only: + `TASK-039` dependency; Objective/Scope/Acceptance aligned to the 4-step contract + derived Signature Skill; stale "GameDbContext has no sets" replaced with the actual set list; §6 note replaced by §3 mapping + §6 envelope split; unit-test and edge-case references updated. |

No other file changed. No ADR changed (gameplay/content data only — no
architectural decision involved; ADR-011 item 4 and ADR-012 items 9–10
remain accurate).

## 5. Final Card Loadout Contract

```text
Submitted (POST /api/battle/start):
  cardLoadout = exactly 3 Basic CardDefinitionId entries

Validation order (server, API_CONTRACTS.md §3):
  1. count       exactly 3 elements
  2. ownership   every entry has a PlayerUnlockedCard row for
                 the requesting Player
  3. category    every entry is Category = Basic
  4. copy limit  each entry's occurrence count in the array
                 ≤ that CardDefinition's LoadoutCopyLimit
                 (explicit value required, no default)

Reject: INVALID_LOADOUT (400, same documented code as relicLoadout).
        A rejected request equips nothing, writes no battle state.

Derived: 1 Signature Skill Card (PetDefinition.SignatureSkillCardId)
         — never submitted, never part of cardLoadout, never counted
         against the copy limit; PetSkill entries can never satisfy
         the Basic composition rule.

Snapshot: PetState.EquippedCards[] = 4 CardDefinitionId entries
          (3 submitted Basics + 1 derived Signature Skill),
          battle-scoped, fixed at battle start, unchanged for the battle.

Duplicates: permitted up to LoadoutCopyLimit; [A, A, B] is valid iff
            LoadoutCopyLimit(A) ≥ 2 (and all entries pass 1–3).
Order:      card array order carries no gameplay significance
            (no slots; contrast EquippedRelics[] = equip slot order).
Ownership:  one unlock row per (Player, CardDefinition) regardless of
            allowed copies; no Card instances exist at any time.
Values:     concrete limits are content/balance configuration,
            owned by a future balance/content task.
```

## 6. Validation

```text
§13 consistency check — PASS across: CARD_RULES.md, GAME_RULES.md,
  GAME_STATE.md, API_CONTRACTS.md, DATABASE.md, ADR-011, ADR-012,
  TASK-028. Verified chain: 3 submitted Basic → copy limit → ownership
  → category → battle snapshot → 3 Basic + 1 derived Signature Skill →
  PetState.EquippedCards[]. No gameplay/API contradiction.

Cross-doc greps — GAME_EVENTS.md, SIGNALR_PROTOCOL.md, REDIS_STATE.md,
  GDD.md, MVP_SCOPE.md, ROADMAP.md contain no loadout representation or
  duplicate policy requiring change; stale phrases ("copied from the
  Player's owned collection" card reading, "GameDbContext has no sets",
  "§6 error convention for invalid loadout") cleared from live docs/tasks.

quality/review.md §1 (documentation subset) — Correctness: all cited
  sections exist and say what the new text claims. Architecture: no
  layering/boundary change. Scope: only the five files listed above.
  Documentation: updates complete and mutually consistent. Determinism:
  server-side request validation only; no client authority introduced
  (ADR-001 unchanged).
```

## 7. Implementation Impact on TASK-028

TASK-028 can now implement the contract deterministically:

```text
- CardDefinition + PlayerUnlockedCard entities (DATABASE.md §1–§2)
- PetDefinition.SignatureSkillCardId FK (TASK-024 deferral)
- 4-step cardLoadout validation at battle start, reject INVALID_LOADOUT
- Copy-limit check against CardDefinition.LoadoutCopyLimit (no default
  fallback — missing value = invalid definition data)
- Snapshot into PetState.EquippedCards[]: 4 CardDefinitionId entries,
  definition-based, order-insensitive
- Unit tests: count/category/ownership/copy-limit boundaries
  (e.g. [A,A,B] at limit 1 → reject; at limit 2 → accept), duplicates
  repeat the same CardDefinitionId, snapshot immutability
```

TASK-028 must be re-audited (post-DONE check) before moving
BACKLOG → READY.

## 8. Scope Verification

```text
No source code changed.
No database changed.
No migration created.
No Card entity implemented.
No CardLoadoutService implemented.
No battle-start implementation.
No SignalR implementation.
No Redis implementation.
TASK-027 unchanged.
No ADR changed.
No concrete LoadoutCopyLimit values invented.
No error codes invented (INVALID_LOADOUT reused).
No Card instance/quantity model introduced.
No unrelated documentation rewritten.
```

## Acceptance against §14

```text
[x] Duplicate question explicitly resolved (human decision D1/D2)
[x] CARD_RULES.md is the canonical owner of the gameplay rule (§1)
[x] API_CONTRACTS.md defines deterministic cardLoadout validation
[x] Exact invalid-loadout behavior documented (INVALID_LOADOUT)
[x] Card ownership validation explicit (step 2)
[x] Basic-category validation explicit (step 3)
[x] Signature Skill derivation explicit (API §3 + CARD_RULES §1 item 4)
[x] PetState.EquippedCards[] identity explicitly CardDefinitionId
[x] Card array ordering explicitly documented as non-semantic
[x] Card snapshot timing remains battle-start only
[x] Card ownership remains PlayerUnlockedCard-based
[x] No card-instance model introduced
[x] No Card Level/Tier/Star semantics introduced
[x] No source code modified
[x] No database/migration changes made
[x] TASK-027 untouched
[x] TASK-028 updated only where necessary for the resolved contract
[x] No unrelated documentation rewritten
[x] Documentation consistency check passes
```

---
---

# APPENDIX A — ROUND-1 STOP CONDITION REPORT (superseded; written during execution round 1)

## 1. Status

```text
BLOCKED — HUMAN GAMEPLAY DECISION REQUIRED
```

## 2. Decision

```text
No decision could be derived from the authoritative documents.
Neither Option A nor Option B was selected.
```

## Human decision required

```text
Human decision required:
Are the 3 submitted Basic Cards required to be distinct?
```

### Option A — distinct required

`["heal", "heal", "shield"]` is rejected; only triples of pairwise
distinct Basic Card definitions are accepted.

### Option B — duplicates permitted

`["heal", "heal", "shield"]` is accepted; the rule is exactly 3 entries,
each an unlocked Basic Card, with no distinctness requirement.

## 3. Evidence

Evidence review completed per §5 across every mandated source:

| Source | What it says | Bearing on duplicates |
|---|---|---|
| `CARD_RULES.md` §1 (:22-23) | "A battle loadout always contains exactly 3 Basic Cards + 1 Pet Skill Card (GDD §3), for **4 total Cards available to cast**." | The only textual hook. "4 total Cards available to cast" is ambiguous: it can mean 4 entries (compatible with B) or 4 distinct cast options (compatible with A). Not decisive. |
| `CARD_RULES.md` §2 (:56) | "Basic Cards are identical for every Pet" | About cross-Pet identity, not loadout composition. |
| `CARD_RULES.md` §3 (:66-85) | Cast validation is **membership** ("The Card is in the active Pet's battle loadout"); success path deducts Power, applies effect, emits `CardCast`. :85: "Casting a Card does NOT consume a Turn" (Turn only). | **No rule removes/consumes a card from the loadout on cast.** Duplicates would be mechanically inert (cast is by `CardId`, membership check, no copy index). Supports consistency with either option. |
| `GAME_RULES.md` §1 (:26-36) | Core Battle Rules: Player/Pet/Boss/board/authority. | No card composition rule at all. |
| `GAME_RULES.md` §11 (:193-197) | Card categories; "MVP Basic Cards: Heal, Shield, Power Charge"; Cards consume/generate Power. | Categories and names only; no composition/distinctness rule. |
| `GDD.md` §3 (:72-74), flow (:57) | Battle consists of "...3 Basic Cards, 1 Pet Skill Card..."; "Equip Cards" step. | Composition list only; no distinctness. |
| `GAME_STATE.md` §2.3 (:886, :935-939) | `EquippedCards[]` = "3 Basic Cards + 1 Pet Skill Card"; battle-scoped loadout; :937 uses stale "underlying instances" wording (hygiene item, §9). | Count only. |
| `GAME_STATE.md` §2.3 (:951-958) | "The member's order is the submitted loadout order, and it is fixed" — paragraph is entirely relic-scoped (`relicLoadout[0]`, slot 1, `RELIC_RULES.md` §2.1–§2.5). | No card order/slot rule exists; nothing bearing on duplicates. |
| `API_CONTRACTS.md` §3 (:310-311) | `cardLoadout` rules: count (:310) + ownership (:311) only. Example (:300) shows three distinct cards. | Count + ownership only. Example is illustrative, not normative. |
| `API_CONTRACTS.md` §3 (:312-313, :335, :340) | Explicit distinctness rule exists **for `relicLoadout` only** (v1.3, owner-scoped, instance-based). | Shows the contract formulates distinctness explicitly when intended; cards have no such clause. Absence is evidence for B but not conclusive (v1.3 predates the card contract). |
| `DATABASE.md` §1-§2 (:65-67, :121-133, :162) | `PlayerUnlockedCard` unlock-flag join; no instance rows. | No composition rule; no unlock-acquisition rule exists anywhere in `docs/` (only the join itself). |
| `ADR-011` item 4 | Player owns collection; equipped set on active Pet. | Ownership split only. |
| `ADR-012` items 9-10 | Unlock-flag join; "exactly 3 Basic + 1 Pet Skill Card selected at battle start". | Count only. |
| `GAME_EVENTS.md` `CardCast` (:385-391) | Payload: `CardId`, Power cost paid, effect summary. | No copy index / slot / remaining-copies field → casting cannot distinguish copies; duplicates add no cast option. |
| `GAME_EVENTS.md` `BattleStarted` (:212-216) | Payload: BattleId, PetId, BossId, initial summary. Loadout not delivered. | Nothing bearing on duplicates. |
| Repo-wide grep: `distinct\|duplicate\|same card\|repeated card\|3 different` etc. | Zero card-side hits. All loadout-distinctness hits are `RELIC_RULES.md` §2.4 / relic API lines. | **No distinctness rule for cards exists in any authoritative document.** |
| Repo-wide grep: card consumption/removal | Only "does NOT consume a Turn" (CARD_RULES:85) and "Cards consume or generate Power" (GAME_RULES:197). | **No rule removes a card from the loadout when cast.** |

**Why neither option is derivable:**

* Option A is supported only by a plausible reading of "4 total Cards
  available to cast" (`CARD_RULES.md`:23) as "4 distinct options" — but
  the sentence equally parses as "4 entries," and no rule states
  distinctness. Adopting A would require writing a new rule into
  `CARD_RULES.md`, which per docs/AGENTS.md §7 is a human design
  decision, not an inference.
* Option B is supported by the absence of any distinctness rule and by
  the count-only API line — but B forces "4 total Cards available to
  cast" to mean "4 entries / fewer than 4 distinct castables," and no
  doc sanctions that reading either.
* Both options are fully consistent with every authoritative document.
  Per docs/AGENTS.md §20 (Ambiguous requirement / Missing rule) and
  this task §6, this is a STOP.

### Gameplay-contract consequences of each option

```text
Option A — distinct required
  + "4 total Cards available to cast" reads as 4 distinct cast options;
    loadout is unambiguous for fully-unlocked players.
  + With MVP's exactly-3 Basic Cards (MVP_SCOPE.md §1), the only valid
    submission is the player's 3 distinct unlocked basics.
  - Requires authoring a brand-new rule (docs currently say nothing).
  - Creates an undocumented edge: a Player who owns fewer than 3
    distinct Basic Cards could not submit a valid loadout and therefore
    could not start a battle. No rule in docs/ guarantees unlock
    coverage or defines unlock acquisition, so Option A introduces a
    must-start-or-fail case that no document currently contemplates.

Option B — duplicates permitted
  + Literal reading of the existing count rule ("exactly 3 Basic Cards"
    = 3 entries, each Basic, each unlocked); adds no new rule.
  + No unlock-coverage edge case (duplicates can fill the third entry).
  + Duplicates are mechanically inert — cast is membership-by-CardId
    with no consumption and no copy index in any payload — so no
    battle-state ambiguity results either way.
  - Leaves "4 total Cards available to cast" (CARD_RULES.md:23)
    satisfied only at entry level: a duplicate triple yields fewer than
    4 distinct castable definitions, a reading no document explicitly
    authorizes.
  - Permits a strictly-worse loadout (no mechanical upside), which the
    docs neither endorse nor forbid.
```

## 4. Documentation Changes

```text
None. No file under docs/ was modified.
No file under src/ or tests/ was modified.
No existing task file was modified (TASK-028, TASK-027 untouched).

Task file created: tasks/blocked/TASK-039-resolve-card-loadout-
duplicate-and-validation-contract.md (this task; Status set to BLOCKED
with this report appended — TASK_LIFECYCLE.md §3 BLOCKED).
```

Per §6: "Do not update API validation as though one option had already
been selected" — sections 7, 8, 9, and 10 are all gated on an explicit
decision and were therefore not executed.

## 5. Final Card Loadout Contract

```text
Submitted:
3 Basic Cards                    (count: settled — API_CONTRACTS §3:310)
  each: Category = Basic         (category rule: to be made explicit in §8 work)
  each: unlocked by the Player   (ownership: settled — API_CONTRACTS §3:311)

Derived:
1 Signature Skill Card           (PetDefinition.SignatureSkillCardId — settled)

Total:
4 Equipped Cards                 (CARD_RULES §1 — settled)

Snapshot:
POST /api/battle/start → PetState.EquippedCards[]
battle-scoped, fixed at battle start (GAME_STATE §2.3 — settled)

Duplicate policy:
UNRESOLVED — pending the human decision above.
  [ ] Option A — 3 pairwise distinct Basic Card definitions required
  [ ] Option B — duplicates permitted (3 entries, each unlocked Basic)
```

Everything above the duplicate policy line is settled and unchanged.

## 6. Implementation Impact

TASK-028 must implement everything listed in the contract above EXCEPT
the duplicate rule and its validation path. Specifically, until the
human decision lands, TASK-028 **cannot** finalize:

* the `cardLoadout` distinctness check (accept vs reject `[A, A, B]`);
* the §8 error mapping for invalid card loadout input (an error code
  must not be invented — docs define `INVALID_LOADOUT` for relics;
  whether cards reuse it is part of the §8 work after the decision);
* the §9 `GAME_STATE.md` hygiene edits (identity sentence, order
  sentence, stale "instances" wording) and §10 TASK-028 corrections —
  all gated on resolution.

All other work (ownership join validation, Basic category validation,
Signature Skill derivation, battle-start snapshot, persistence shape)
is unblocked by existing documentation.

## 7. Scope Verification

```text
No source code changed.
No database changed.
No migration created.
No Card entity implemented.
No CardLoadoutService implemented.
No battle-start implementation.
No SignalR implementation.
No Redis implementation.
TASK-027 unchanged.
No docs/ file changed.
TASK-028 file unchanged (its §10 cleanup is gated on resolution).
```

## Acceptance status against §14

```text
[x] The duplicate Basic Card question has been explicitly recorded as
    requiring human gameplay decision (this report).
[ ] Remaining criteria depend on the human decision and the §7-§10
    doc updates that follow it.
```

## Resolution path

```text
1. Human answers: distinct required (A) or duplicates permitted (B).
2. BLOCKED → IN PROGRESS (TASK_LIFECYCLE.md §2): execute §7 (owner:
   CARD_RULES.md §1), §8 (API_CONTRACTS.md §3), §9 (GAME_STATE.md
   §2.3 hygiene), §10 (TASK-028 corrections), §13 validation.
3. Task → DONE (§14 all criteria).
4. TASK-028 receives another readiness audit; may then move
   BACKLOG → READY.
```
