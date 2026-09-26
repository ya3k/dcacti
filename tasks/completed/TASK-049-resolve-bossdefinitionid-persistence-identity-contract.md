# TASK-049 — Resolve BossDefinitionId Persistence Identity Contract

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ by path and section; does not copy rules or schemas.
-->

---

## Metadata

```text
Task ID:           TASK-049
Type:              DOCUMENTATION
Status:            DONE
Risk:              MEDIUM
Priority:          HIGH (P1 — clears the sole remaining precondition for a
                   fresh TASK-044 implementation-readiness verification)
Primary Agent:     review
Supporting Agents: persistence (database contract accuracy),
                   backend (ARCHITECTURE.md conformance)
Workflow:          documentation/documentation-change.md
Skills:            documentation-discovery, documentation-consistency,
                   impact-analysis, persistence-analysis
Dependencies:      TASK-045 (DONE), TASK-046 (DONE)
Blocks:            TASK-044 (must not be edited by this task)
```

---

## Objective

Resolve, by explicit human decision and documentation only, the single
unresolved contract gap that blocks TASK-044: **what value the
`BossDefinitionId` column holds, and whether it is content-supplied or
database-generated.** `DATABASE.md` §1 currently establishes that
`BossDefinitionId` is the row's persistence primary key and that it is
distinct from `Identity` and from the display name, but it never defines the
key's value, form, source, or generation model — and the Domain
`BossDefinition` record has no `BossDefinitionId` member at all. This task
obtains that decision from a human and records it in the canonical owner
document (`docs/02-technical/DATABASE.md`). It must not select a value, a
type, a generation strategy, or a Domain representation on the project's
behalf (`AGENTS.md` §7, §20, §23).

---

## Authoritative References

- `docs/02-technical/DATABASE.md` §1 (`BossDefinition` block, L117–136) —
  **canonical owner** of the persistence contract; defines the five entries
  `BossDefinitionId (PK)`, `Identity`, `Element`, `PassiveDefinition`,
  `SkillDefinition`
- `docs/02-technical/DATABASE.md` §1 "Persistence contract for
  `BossDefinition`" item 1 (L162–181) — the three-never-collapsed statement;
  `Identity` is "**not** `BossDefinitionId` (this row's persistence primary
  key)"
- `docs/02-technical/DATABASE.md` §1 "Identity and reward sourcing for
  `BattleResult`" item 2 (L231–256, esp. L244–250) — `BattleResult.BossDefinitionId`
  is "the key of the `BossDefinition` row whose `Identity` equals
  `BattleState.BossState.BossId`" (the FK lookup this PK must support)
- `docs/02-technical/DATABASE.md` §2 (L262–273) — `BattleResult N ── 1
  BossDefinition`; §3 (L290–319) — documented constraints; §4 (L323–334) —
  indexes; §5 item 1 (L340) — "Migration scripts / exact SQL types —
  implementation detail" (scope of that exemption matters, see Decision A-iv)
- `docs/01-game-design/BOSS_RULES.md` §6.4 (L243–298) — the identity contract:
  three never-collapsed concepts; `BossDefinitionId` is "the stable
  persistence/database identity of the `BossDefinition` row (`DATABASE.md`
  §1)" and is **owned there, not here** (L263–264, L287–289); the three
  canonical Identities (L274–276)
- `docs/02-technical/GAME_STATE.md` §2.4 (L1088–1101) — `BossId` is the
  canonical technical Identity, "**not** `BossDefinitionId` (the persistence
  primary key — `DATABASE.md` §1); the three are never collapsed"
- `docs/02-technical/ARCHITECTURE.md` §1 (`Bosses/` module), §2 layer
  direction, §5 item 1 (no plugin/registry system) — bounds any answer that
  would move identity ownership between layers or require a registry
- `docs/02-technical/TDD.md` §4 (persistence strategy), §7 (non-goals)
- `docs/03-decisions/ADR/ADR-006-postgresql-persistence.md` — PostgreSQL for
  durable data; §Consequences states relational constraints enforce integrity
- `tasks/backlog/TASK-044-implement-bossdefinition-persistence.md` —
  downstream consumer; **read-only** (Never modify — `AGENTS.md` §16)
- `tasks/completed/TASK-045-resolve-bossdefinition-persistence-contract.md`
  — §5A/§9/§11: the persistence contract this task extends; Q2 option (b)
  settled the five-column model and the stats split (context; do not modify)
- `tasks/completed/TASK-046-resolve-canonical-boss-identity-contract.md`
  §5A Decision A — `BossDefinitionId` = persistence PK, `Identity` = canonical
  game-level identity, `DisplayName` = human-readable content; three distinct
  (context; **do not reopen**)
- `tasks/completed/TASK-047-*`, `tasks/completed/TASK-048-*` — source/test and
  `BOSS_RULES.md` synchronization of the canonical Identities (context; do
  not modify)
- `AGENTS.md` §2 (owner hierarchy), §4 (conflict resolution), §7 (no invented
  rules), §9 (anti-overengineering), §16 (task discipline), §17
  (documentation change rule), §18 (architecture change rule), §20 (stop
  conditions), §22 (Definition of Done)
- `docs/00-overview/MVP_SCOPE.md` §1 — first check: this records an existing
  persistence contract; it adds no system or content (scope N/A)
- Read-only precedent evidence, **not authority** (`AGENTS.md` §7 — the
  agent must not treat sibling mappings as the decision):
  `src/backend/GameServer.Domain/Pets/PetDefinition.cs`,
  `.../Cards/CardDefinition.cs`, `.../Relics/RelicDefinition.cs`,
  `src/backend/GameServer.Infrastructure/Postgres/Configurations/*.cs`,
  `src/backend/GameServer.Domain/Bosses/BossDefinition.cs`

---

## Scope

### In Scope

1. Identify and record the exact **`BossDefinitionId` semantics**: what the
   value denotes, as distinct from `Identity` and the display name.
2. Record the **value/representation** of `BossDefinitionId` and its
   derivation/source — including, if the human chooses an independent key, the
   exact `BossDefinitionId` value for each of the three content-defined MVP
   Bosses (Hỏa Long, Thủy Ma, Mộc Yêu) **as supplied by the human**.
3. Record the **ownership/generation model**: content-supplied (and, if so,
   which Domain member carries it) or database-generated (and, if so, the
   generation model and how the Domain type represents the identity).
4. Record the **relationship to `Identity`** and to the display name,
   preserving the non-collapse rule unless the human explicitly selects the
   contract-changing Option B (§5 A-ii) and that amendment is written down.
5. Update **only the minimum sections** of `docs/02-technical/DATABASE.md`
   needed to record the decision (expected: §1 `BossDefinition` block
   annotation, the §1 contract note, and — only if the decision requires it —
   a §3 constraint line), with a version bump following the file's existing
   header convention and its prior-version history chain preserved.
6. Verify the five referencing documents for staleness after the edit
   (verification only; an edit is permitted only if a directly stale
   `BossDefinitionId` reference is found, and only to fix that wording).

### Out of Scope

- **Selecting any answer.** The agent must not choose the PK model, the value,
  the type, caller-vs-generated, or a Domain carrier for implementation
  convenience, precedent resemblance, provider default, or apparent tidiness
  (`AGENTS.md` §7, §20). Options in §5 are listed for evaluation only.
- **Reopening TASK-046 / TASK-047 / TASK-048.** The canonical technical Boss
  identities remain `boss-hoa-long` / `boss-thuy-ma` / `boss-moc-yeu`; do not
  derive, rename, or re-case them.
- **Changing `BossState.BossId`, API `bossId`, event `sourceId`, `PassiveId`,
  or `SkillId`** — none of these is the PK question.
- Any change under `src/` or `tests/` — including a `BossDefinitionId` member
  on the Domain record, an EF entity/configuration, a DbSet, a migration, or a
  repository. Code follows docs (`AGENTS.md` §17); this task records the
  contract only.
- **Production Boss row provisioning** — already settled and **not reopened**:
  `DATABASE.md` §1 contract note item 4 (L221–229) and §5 item 4 (L345–349)
  state no provisioning mechanism is documented and none may be invented. No
  `HasData`, seed, startup loader, or migration-inserted Boss rows.
- Database schema/migrations, Redis, SignalR, API contracts, `BattleResult`,
  `BattleState` shape, authentication (TASK-034), localization, gameplay
  rules, or architecture changes.
- Creating or amending an ADR — unless the human's answer requires one, in
  which case **stop** per §8 (`AGENTS.md` §18).
- Editing TASK-044, TASK-045, TASK-046, TASK-047, TASK-048, or any other
  existing task file (`AGENTS.md` §16).
- Broad documentation cleanup or a Boss persistence audit beyond §Scope item 6.

---

## Current State — The Gap (verified at creation)

**Verified by a fresh TASK-044 readiness audit (2026-09-26), then re-verified
at this task's creation. Re-locate by search before editing; line numbers are
creation-time evidence.**

### Evidence 1 — the two concepts are declared distinct, but only one has a value

| Fact | Location |
|---|---|
| `BossDefinitionId (PK)` is annotated "persistence identity of this row — distinct from `Identity` below and from the display name, TASK-046" | `DATABASE.md` §1 L123–125 |
| `Identity` is annotated with a value form: "canonical technical Boss ID — `BOSS_RULES.md` §6.4, e.g. `"boss-hoa-long"`; never a display name" | `DATABASE.md` §1 L126–130 |
| `Identity` "is **not** the display name, and it is **not** `BossDefinitionId` (this row's persistence primary key): the three are never collapsed (TASK-046)" | `DATABASE.md` §1 contract note item 1 L168–170 |
| `BossDefinitionId` is "the stable persistence/database identity of the `BossDefinition` row (`DATABASE.md` §1) — a persistence key, distinct from both the canonical technical Identity above and the display name" | `BOSS_RULES.md` §6.4 L287–289 |
| `BossId` is "**not** `BossDefinitionId` (the persistence primary key — `DATABASE.md` §1); the three are never collapsed (TASK-046)" | `GAME_STATE.md` §2.4 L1096–1097 |
| **No document states what value `BossDefinitionId` holds.** A search of `docs/` for `BossDefinitionId` paired with `=`, `:`, `"`, `holds`, `is the`, or `value` returns **zero** value-defining matches | `docs/` (absence) |

### Evidence 2 — the Domain record has no `BossDefinitionId` member

| Fact | Location |
|---|---|
| The Domain record is `BossId, Element, MaxHP, ATK, DEF, PassiveId, PassiveThreshold, SkillId, SkillBaseDamage, SkillChargeRequirement, SkillCooldownTurns, EnrageThreshold, PassiveResetBehavior?` — **no `BossDefinitionId`** | `src/backend/GameServer.Domain/Bosses/BossDefinition.cs` L137–150 |
| Its XML header even states "No registry, service, or persistence" | same file L39–47 |

### Evidence 3 — the sibling precedent is the opposite shape (evidence, not authority)

| Fact | Location |
|---|---|
| Sibling Domain types carry an explicit, caller-supplied PK: `public required string PetDefinitionId { get; init; }`, `CardDefinitionId`, `RelicDefinitionId` | `PetDefinition.cs` L61; `CardDefinition.cs` L46; `RelicDefinition.cs` |
| Siblings map it as a bounded, required, non-generated string: `.HasMaxLength(64).IsRequired()` with no `ValueGenerated`/`HasDefaultValue` | `PetDefinitionConfiguration.cs` L53–55; `CardDefinitionConfiguration.cs` L53–55 |
| `BossDefinition` is the **only** definition type without such a member | comparison of the four Domain definition types |

### Consequence

TASK-044 cannot determine the PK value, its owner/source, its Domain
representation, or its generation strategy without inventing a persistence
identity — a schema/business decision, not an ordinary EF implementation
detail. The two available readings are mutually exclusive and neither is
documented:

```text
READING 1  BossDefinitionId value = Identity value ("boss-hoa-long")
           → COLLAPSES the two concepts, which the documents above forbid.

READING 2  BossDefinitionId is an independent key
           → its value, form, and source are documented NOWHERE.
```

`DATABASE.md` §5 item 1 exempts only "exact SQL types"; it does not make the
*value form* an implementation detail — the same reasoning TASK-046 §5
Decision A-iv applied when rejecting that exemption.

---

## Decision Points (the deliverable)

Each question must be answered **by a human**. If an answer cannot be obtained,
report `BLOCKED` naming the exact question (`AGENTS.md` §4, §7, §20;
`.ai/README.md` §13). Never select an option — especially a key format — for
implementation convenience.

### Q1 — What is the `BossDefinitionId` value?

**Questions:**

- **Q1.1.** What does the `BossDefinitionId` value denote, stated as precisely
  as `DATABASE.md` §1 states `Identity`'s?
- **Q1.2.** What is its value form (string / bounded string / integer / GUID /
  other)? No form may be assumed from provider defaults or sibling mappings.
- **Q1.3.** If it is a value distinct from `Identity`, what are the **exact**
  values for Hỏa Long, Thủy Ma, and Mộc Yêu? (Or is the value assigned per
  row by another documented rule?)
- **Q1.4.** Does the answer preserve the non-collapse rule
  (`BossDefinitionId ≠ Identity ≠ DisplayName`) — or does it deliberately
  amend it (`AGENTS.md` §4)?

**Options (evaluate; choose only with human approval, or report blocked):**

- **A-i — independent stable technical persistence key.** `BossDefinitionId`
  is distinct from the canonical `Identity`. *Must check:* the human supplies
  the three exact values and the convention; the §1 FK-lookup statement
  (L244–250) already resolves `BattleResult.BossDefinitionId` **via
  `Identity`**, so the relationship between the two lookups must be stated
  without contradiction.
- **A-ii — `Identity` is reused as `BossDefinitionId`.** **This is a contract
  change, not an implementation detail.** It must explicitly amend the
  non-collapse rule in every document that states it (`DATABASE.md` §1
  contract note item 1; `BOSS_RULES.md` §6.4; `GAME_STATE.md` §2.4) — a
  multi-document, `AGENTS.md` §4–relevant decision. *Must check:* whether this
  contradicts TASK-046's recorded decision, which would require reopening an
  approved decision rather than implementing one.
- **A-iii — a surrogate key whose value no document fixes** (e.g. a
  database-generated value with no content meaning). *Must check:* then the
  persistence identity is not reproducible from content, and the §1 note item
  4 provisioning rules and any round-trip expectation must still hold.
- **A-iv — treat the value form as an implementation detail.** *Must check:*
  `DATABASE.md` §5 item 1 covers **exact SQL types only**; `API_CONTRACTS.md`,
  `GAME_EVENTS.md`, and `DATABASE.md` §1's FK lookup consume the *value*, so
  the value is contract-visible. Recorded as a rejected reading unless the
  human explicitly re-scopes §5 item 1.
- **A-v — not derivable** → `BLOCKED`, human decision required (the expected
  outcome unless the human supplies the basis).

### Q2 — Is `BossDefinitionId` caller/content-supplied or database-generated?

**Questions:**

- **Q2.1.** Which model applies, and which document owns that statement?
- **Q2.2.** If **caller/content-supplied**: which Domain member carries the
  value? The record currently has none, so this answer determines whether the
  Domain type gains a member (a Domain change TASK-044 would implement — this
  task only records it) and what its type/semantics are.
- **Q2.3.** If **database-generated**: what is the generation model, and how
  does the Domain entity represent the identity if the architecture requires
  it? *Must check:* `ARCHITECTURE.md` §2/§5 and whether generating the key
  changes any documented read path (a possible `AGENTS.md` §18 trigger).
- **Q2.4.** Does the answer require any `DATABASE.md` §3 constraint
  (uniqueness, NOT NULL, value form) beyond the existing
  `Identity NOT NULL, UNIQUE` (L302–303)?
- **Q2.5.** Does the answer stay inside the five-column model TASK-045 Q2
  option (b) fixed, or does it require a column addition? If a column is
  needed, state it explicitly — §1 note item 1 (L179–181) forbids adding one
  unless an authoritative document explicitly requires it.

**Options:** the two named models (caller/content-supplied;
database-generated), each evaluated against Q2.2–Q2.5. A mixed answer that
leaves the PK's source unstated is the current gap, not a contract.

### Decision F — contract surface to synchronize

Confirm the complete set of places that carry or reference the
`BossDefinitionId` concept before editing, verifying each rather than
assuming: `DATABASE.md` §1 (block annotation + contract note), §1 FK lookup
(L244–250), §2 relationship (L272), §3 constraints; `BOSS_RULES.md` §6.4
(L263–264, L287–289); `GAME_STATE.md` §2.4 (L1096–1097). For each, decide
whether a change is required by Q1/Q2 or whether a cross-reference/staleness
fix suffices, and which single document owns each change.

---

## Existing Pattern (evidence only — NOT authority)

TASK-044 is expected to follow, in order of closeness, once this contract
exists. These are **precedents to read, not the decision** (`AGENTS.md` §7):

```text
Configurations/CardDefinitionConfiguration.cs   static content; required fields; no default
Configurations/PetDefinitionConfiguration.cs    Identity + Element + Passive pair; bounded PK string
Configurations/RelicDefinitionConfiguration.cs  static content; optional column
Postgres/GameDbContext.cs                       DbSet + ApplyConfigurationsFromAssembly;
                                                ForeignKeyIndexConvention removed
Migrations/*_Add*Persistence.cs                 the Add*Persistence migration series
tests/.../CardPersistenceTests.cs               documented-fields + model + round-trip tests
tests/.../TestGameDbContextFactory.cs           the test seam
```

TASK-049 must **not** cite these as the reason for an answer, and must not
implement any of them.

---

## Acceptance Criteria

- [x] Exact `BossDefinitionId` semantics are defined in `DATABASE.md` §1 with
      the same precision as `Identity`'s annotation.
- [x] Its relationship to the canonical `Identity` is defined, and the
      non-collapse rule is preserved — unless the human explicitly selected
      Option A-ii, in which case the amendment is written in **every** document
      that states the rule.
- [x] Its relationship to the display name remains explicit (display name is
      not a column and not the PK).
- [x] Its value/representation is defined.
- [x] Caller/content-supplied vs database-generated is defined.
- [x] The Domain ownership/carrier is defined if the chosen model requires it.
- [x] The three current content-defined Bosses can be represented without
      guessing (values supplied by the human, or the rule that assigns them).
- [x] TASK-046's canonical Boss identities remain unchanged
      (`boss-hoa-long` / `boss-thuy-ma` / `boss-moc-yeu`).
- [x] No source code changes.
- [x] No tests modified.
- [x] No migration generated and no EF configuration/entity/DbSet added.
- [x] No provisioning/seed/`HasData` change (`DATABASE.md` §1 note item 4,
      §5 item 4 remain authoritative and are not reopened).
- [x] No Redis changes.
- [x] No SignalR changes.
- [x] No API contract changes.
- [x] No gameplay changes.
- [x] TASK-044 remains blocked until TASK-049 is resolved (TASK-044 file
      untouched).
- [x] Version header note follows `DATABASE.md`'s existing convention with the
      prior-version history chain preserved.
- [x] Quality review checklist passes (`quality/review.md` §1 documentation
      subset).

---

## Affected Files & Areas

```text
[x] src/backend/                (forbidden)
[x] src/frontend/               (forbidden)
[x] tests/                      (forbidden)
[x] docs/ (documentation only — DATABASE.md canonical-owner edit; dependent
           references only if wording becomes stale)
[x] tasks/ (this new task file only; no existing task modified)
```

---

## Implementation Notes

- Follow `.ai/workflow/documentation/documentation-change.md`: identify the
  canonical owner (already identified: `DATABASE.md` §1) → read referencing
  docs → check conflicts → update the smallest authoritative source → update a
  dependent reference only if its wording became stale → validate consistency
  (no duplicate definition introduced).
- **Ask, do not assume.** Present Q1/Q2 with the evidence in §Current State and
  the options in §Decision Points; stop for the human answer. Record the
  answer verbatim where it defines a value, exactly as TASK-046/TASK-047 did
  when a human supplied identity values.
- Preserve §1's structure and wording wherever it is already correct: the
  entity block's five entries and their ordering, the contract note items
  1–4, and the §3 constraint lines. Change only what the decision makes stale.
- Do not derive, rename, or re-case any identity value; do not confuse
  `BossDefinitionId` with `Identity`, `BossState.BossId`, `PassiveId`, or
  `SkillId` (note `boss-thuy-ma` / `boss-moc-yeu` are substrings of the
  PassiveIds `boss-thuy-ma-heal` / `boss-moc-yeu-regen`).
- Line numbers are creation-time evidence; re-locate by search before editing.
- If the answer implies a Domain member on `BossDefinition`, record the
  requirement in the completion report as TASK-044's implementation input —
  do not add it here.
- Report format for any `BLOCKED` outcome: `.ai/README.md` §13's exact format,
  then set `Status: BLOCKED` and move the file to `tasks/blocked/`
  (`tasks/README.md` §10).

---

## Testing Requirements

### Required Verification

```text
[x] Search docs/ for a value-defining statement for `BossDefinitionId` — the
    decided value/source is now recorded exactly once, in DATABASE.md §1
[x] Re-read DATABASE.md §1 together with BOSS_RULES.md §6.4 and
    GAME_STATE.md §2.4 — the non-collapse rule is consistent (or the
    amendment is applied everywhere it is stated)
[x] Confirm the three canonical Identities are unchanged
    (boss-hoa-long / boss-thuy-ma / boss-moc-yeu)
[x] Confirm no source/test file changed and no migration/EF/DbSet file exists
    (git diff scoped to this task)
[x] Confirm no task file other than this new one was modified
```

### Key Edge Cases

- **`Identity NOT NULL, UNIQUE`** (`DATABASE.md` §3 L302–303) is the unique
  target of the FK lookup in §1 (L244–250) — do not weaken or restate it
  while defining the PK.
- **`BossDefinitionId` is consumed as a FK** by `BattleResult`
  (`DATABASE.md` §1 L146–150, §2 L272), so its value form is contract-visible
  to TASK-041 as well as TASK-044.
- **Code tests are not required:** this is documentation-only, and TASK-044
  established that no test reads `docs/`. Verification is the consistency
  checks above.

---

## Stop Conditions

Universal stops in `AGENTS.md` §20 apply. Task-specific stops — STOP and
report instead of guessing if:

1. Another authoritative document contains a conflicting PK decision for
   `BossDefinition` (at creation: **none** — the only PK statements are
   `DATABASE.md` §1 L123, `BOSS_RULES.md` §6.4 L263/L287, and they agree).
2. TASK-046's non-collapse rule is stated inconsistently somewhere (at
   creation: **consistent** in `DATABASE.md` §1, `BOSS_RULES.md` §6.4,
   `GAME_STATE.md` §2.4).
3. The human decision cannot be obtained — report `BLOCKED` naming Q1/Q2.
4. Resolving Q1/Q2 requires a broader architecture decision (a new state
   member, a new identity service, a changed read path, an owned-type
   redesign) → ADR first (`AGENTS.md` §18); stop this task.
5. The answer requires touching `src/`, `tests/`, TASK-044, or any
   `tasks/completed/` file (`AGENTS.md` §16).
6. The answer would introduce provisioning/seed/`HasData` or reopen
   `DATABASE.md` §1 note item 4 / §5 item 4.
7. The answer contradicts another authoritative document — report per
   `AGENTS.md` §4; do not silently resolve.
8. The skill budget (7) is exceeded — stop and decompose
   (`tasks/README.md` §12).

Unrelated issues discovered while editing are report-only (`AGENTS.md` §16).

---

## Decisions Received — 2026-09-26 (Q1 and Q2 answered; written to docs)

The human answered both decision points. Both answers were verified against the
authoritative documents before writing; **no STOP condition fired** and no rule
conflict arose (`AGENTS.md` §4 not triggered):

```text
Q1 — What is the BossDefinitionId value?

  ANSWER: Option A-i — an independent stable persistence key, distinct from
  the canonical technical Identity and from the display name.

    BossDefinitionId = independent stable persistence key
    BossDefinitionId ≠ Identity
    BossDefinitionId ≠ DisplayName

  A stable string key supplied by game content/Domain (not derived from
  Identity, not derived from the display name, no runtime slugification).

  Exact values (human-supplied, verbatim — no agent-chosen value):

    Hỏa Long → boss-def-hoa-long
    Thủy Ma  → boss-def-thuy-ma
    Mộc Yêu  → boss-def-moc-yeu

  Non-collapse rule PRESERVED (not amended; Option A-ii was not chosen).
  TASK-046 canonical Identities unchanged:

    Hỏa Long → boss-hoa-long
    Thủy Ma  → boss-thuy-ma
    Mộc Yêu  → boss-moc-yeu
```

```text
Q2 — Is BossDefinitionId caller/content supplied or database generated?

  ANSWER: Caller/content supplied — NOT database generated.

  The Domain `BossDefinition` must carry the value explicitly as:

    required string BossDefinitionId

  The persistence layer stores that value as the primary key.

  TASK-045's five persistence concepts remain unchanged and no additional
  persisted concept is introduced:

    BossDefinitionId
    Identity
    Element
    PassiveDefinition
    SkillDefinition

  Therefore §Q2.5 is satisfied: the answer stays inside TASK-045 Q2 option
  (b)'s five-column model (no column added).
```

**Recorded constraints** (all carried verbatim from the human decision): the key
is stable and content-defined; not derived from `Identity` or from the display
name at runtime; no GUID/integer/database-generated PK; no provider-generated
value; no runtime slugification; no `HasData`; no seed; no startup loader; no
migration-inserted production Boss rows.

**Where written:** `docs/02-technical/DATABASE.md` — §1 `BossDefinitionId (PK)`
block annotation (value/source/generation/derivation) and §1 persistence-contract
note item 1 (the Domain-carrier and key-model statement). Version bumped
1.8 → 1.9 following the file's existing header convention.

**No dependent reference became stale:** `BOSS_RULES.md` §6.4, `GAME_STATE.md`
§2.4, `API_CONTRACTS.md`, `SIGNALR_PROTOCOL.md`, and `ARCHITECTURE.md` all
already state the three-way distinction and defer the PK to `DATABASE.md` §1 —
none of them defines or contradicts the PK value, so none required an edit
(`documentation-change.md` §1–§2: canonical owner only; fix a dependent
reference only where stale).

**Not reopened:** TASK-046 canonical identities; TASK-047 source sync; TASK-048
`BOSS_RULES.md` sync; `BossState.BossId`; API `bossId`; event `sourceId`;
`PassiveId`; `SkillId`; and Boss production provisioning (`DATABASE.md` §1 note
item 4, §5 item 4).

---

## Prior STOP CONDITION (resolved — retained as history)

**Reported:** 2026-09-26. Stop Condition 3 fired ("The human decision cannot be
obtained — report `BLOCKED` naming Q1/Q2"). No documentation was edited; no
value was chosen. **Resolved the same day** by the human decision recorded
above; the file returned `blocked/` → `active/` with `Status: IN PROGRESS`
(`tasks/TASK_LIFECYCLE.md` §3).

```text
STOP CONDITION (RESOLVED)

Problem:
TASK-049 cannot proceed. It exists to record a human decision defining what
value `BossDefinitionId` holds and whether it is content-supplied or
database-generated, but no such decision has been supplied, and the
repository contains none. Every authoritative document still defines the two
identity concepts as distinct without stating the PK's value, form, source,
or generation model — so the contract cannot be written without the agent
choosing it, which `AGENTS.md` §7/§20/§23 forbids.

Relevant sources:
- docs/02-technical/DATABASE.md §1 L123-125 — `BossDefinitionId (PK)` is
  "persistence identity of this row — distinct from `Identity` below and from
  the display name, TASK-046" (no value given)
- docs/02-technical/DATABASE.md §1 L126-130 — `Identity` (value given:
  canonical technical Boss ID, e.g. "boss-hoa-long")
- docs/02-technical/DATABASE.md §1 contract note item 1 L168-170 — "it is
  **not** the display name, and it is **not** `BossDefinitionId` … the three
  are never collapsed (TASK-046)"
- docs/02-technical/DATABASE.md §5 item 1 L340 — "Migration scripts / exact
  SQL types — implementation detail" (exempts types only, not value form)
- docs/01-game-design/BOSS_RULES.md §6.4 L263-264, L287-289 —
  `BossDefinitionId` "Owned there, not here" (i.e. DATABASE.md owns it), and
  "distinct from both the canonical technical Identity above and the display
  name"
- docs/02-technical/GAME_STATE.md §2.4 L1096-1097 — "**not**
  `BossDefinitionId` (the persistence primary key — `DATABASE.md` §1); the
  three are never collapsed (TASK-046)"
- src/backend/GameServer.Domain/Bosses/BossDefinition.cs L137-150 — the Domain
  record has no `BossDefinitionId` member (evidence only; not authority)

Conflict / missing information:
Searches performed, all negative:
- `BossDefinitionId` paired with `=`, `:`, `"`, `holds`, `is the`, or `value`
  across docs/ → 0 value-defining matches
- `BossDefinitionId` paired with GUID/uuid/integer/int/generated/surrogate/
  format across docs/ and tasks/ → 0 matches
- The 11 total `BossDefinitionId` mentions (BOSS_RULES.md L9/L263/L287;
  DATABASE.md L3/L5/L123/L146/L168/L244; GAME_STATE.md L6/L1096) are all
  identity-distinction or FK-reference statements — none define a value
- TASK-049's own "Decisions Received" section is an unfilled placeholder
- No decision log, values file, or answer artifact exists in the repository

The two available readings remain mutually exclusive and both undocumented:
  READING 1 — BossDefinitionId value = Identity value ("boss-hoa-long")
              → collapses two concepts the documents above forbid.
  READING 2 — BossDefinitionId is an independent key
              → its value, form, and source are documented nowhere, and the
                Domain record carries no member for it.
Exactly one of these is correct, and only a human can say which.

Proposed resolution:
Obtain the human decision on the two questions TASK-049 already poses. No new
task is needed — TASK-049 is the correct, minimal, already-scoped vehicle.
Resolve and record:
  Q1  Denotation, value form, and — if an independent key is chosen — the exact
      `BossDefinitionId` value for each of Hỏa Long, Thủy Ma, Mộc Yêu
      (supplied by the human, never invented); plus whether the existing
      non-collapse rule is preserved or explicitly amended.
  Q2  Caller/content-supplied vs database-generated; if supplied, which Domain
      member carries it; if generated, the generation model and how the Domain
      type represents the identity; and whether the answer stays inside
      TASK-045 Q2 option (b)'s five-column model.
Then write the decision to `docs/02-technical/DATABASE.md` §1 only, and fix a
dependent reference only if its wording became stale
(documentation/documentation-change.md §1-§2).

Waiting for:
The human's answers to Q1 and Q2, in the terms recorded in this task's
"Decision Points" section. Neither may be inferred from EF/PostgreSQL
conventions, sibling entities, `Identity`, display names, GUID/integer
preferences, existing migrations, the current Domain shape, provider defaults,
or implementation convenience — those are evidence only, not authority.
```

---

## Completion Evidence

**Status: DONE** (executed 2026-09-26). The contract gap is resolved and
recorded. The task was `BLOCKED` briefly (Stop Condition 3 — no human decision
available), then resumed the same day when the human supplied Q1/Q2; no further
STOP condition fired, and no rule conflict arose (`AGENTS.md` §4 not
triggered). `DATABASE.md` v1.8 → v1.9.

### Changed Files

Documentation (1 — canonical owner only):

- `docs/02-technical/DATABASE.md` — v1.8 → **v1.9**:
  1. **§1 `BossDefinitionId (PK)` block annotation (L131–138)** — the
     previously value-less "persistence identity of this row" annotation now
     states the key model: an independent stable persistence key,
     content/Domain supplied, never database-generated, distinct from
     `Identity` and the display name and never derived from either
     (TASK-046/TASK-049), with value form
     `boss-def-<ascii-kebab-case-name>` (e.g. `"boss-def-hoa-long"`).
  2. **§1 persistence-contract note — new item 2 (L195–223)** — the full
     `BossDefinitionId` contract: value form; the three canonical values with
     their `Identity` counterparts shown side by side; source/ownership
     (`required string BossDefinitionId` on the Domain record, stored as the
     PK; no GUID/integer/provider/database generation; no `HasData`/seed/
     startup loader/migration-inserted row); and the relationship to
     `Identity` (separate values, separate purposes — a caller resolves the
     row **by `Identity`** and stores that row's `BossDefinitionId` as the FK,
     never substituting one for the other).
  3. **§1 note items renumbered** 2→3 (`PassiveDefinition`), 3→4
     (`SkillDefinition`), 4→5 (Rows and provisioning), with item 5 gaining one
     clarifying sentence that the `BossDefinitionId` contract introduces no
     provisioning mechanism. All content preserved verbatim; only the numbers
     and the one cross-reference moved. No duplicate definition introduced.
  4. **§3 Constraints (L345–347)** — added
     `BossDefinition.BossDefinitionId NOT NULL, UNIQUE, caller/content-supplied`
     with the "independent persistence key, never database-generated; distinct
     from `Identity` and from the display name" clause. The pre-existing
     `BossDefinition.Identity NOT NULL, UNIQUE` line is untouched.
  5. **Header version note** — bumped to 1.9 with a TASK-049 resolution note
     prepended to the existing chain; the `prior 1.8:` (TASK-046) and earlier
     entries are preserved verbatim.

Task file (1 — this file only):

- `tasks/active/TASK-049-resolve-bossdefinitionid-persistence-identity-contract.md`
  — Status BACKLOG → BLOCKED → IN PROGRESS → DONE; "Decisions Received" now
  records the human's Q1/Q2 answers verbatim; the STOP CONDITION is retained
  under "Prior STOP CONDITION (resolved — retained as history)"; completion
  evidence recorded; moved `backlog/` → `blocked/` → `active/` → `completed/`.

**Zero dependent-document edits.** `BOSS_RULES.md`, `GAME_STATE.md`,
`API_CONTRACTS.md`, `SIGNALR_PROTOCOL.md`, and `ARCHITECTURE.md` were verified
byte-identical (hash comparison) and required no change: each cites
`DATABASE.md` §1 as the owner and none defines or contradicts the PK value.
The §1 section number did not move, so no cross-reference went stale
(`documentation-change.md` §1–§2: canonical owner only).

### Validation Results

- **Exactly one coherent contract** — `boss-def-*` appears only in
  `DATABASE.md` (L7–8, L137–138, L199, L206–208); no other document restates
  the value, so no duplicate definition exists.
- **No `BossDefinitionId = Identity` collapse** — search across `docs/` → 0
  matches. The non-collapse rule still reads "never collapsed" in
  `DATABASE.md` L182, `BOSS_RULES.md` L251, `GAME_STATE.md` L1097.
- **All 9 determinism requirements covered** (verified by targeted search of
  the edited section): meaning; vs `Identity`; vs `DisplayName`; format; exact
  values; owner/supplier; generated?; Domain representation; TASK-045
  five-concept compatibility.
- **Three canonical Identities unchanged** — `boss-hoa-long` /
  `boss-thuy-ma` / `boss-moc-yeu` (`BOSS_RULES.md` §6.4 L274–276, L281);
  TASK-046/047/048 not reopened. `BOSS_RULES.md` hash unchanged.
- **§3 constraint added, not invented** — `NOT NULL, UNIQUE` follows the
  existing `Identity` constraint pattern (L350–351); no undocumented range or
  value constraint was added.
- **Scope check** — `src/` `35E21F91…CBA20EF4` and `tests/`
  `B24645C9…EFBA0CF5` byte-identical to the pre-edit baseline; `TASK-044`
  `447752BE…3DE242` unchanged and still `Status: BACKLOG`; no Boss EF
  entity/configuration/DbSet/migration exists.
- **Code tests** — **N/A: documentation-only.** No source compiled or
  executed; verification is the consistency checks above.

### Decisions Received

- `Q1 (value/representation)` — **A-i, independent stable persistence key**
  (human-supplied). Not `Identity`, not the display name, non-collapse rule
  preserved (A-ii not chosen). Value form
  `boss-def-<ascii-kebab-case-name>`; canonical values
  `boss-def-hoa-long` / `boss-def-thuy-ma` / `boss-def-moc-yeu` (verbatim; no
  agent-chosen value).
- `Q2 (ownership/generation)` — **caller/content supplied, NOT
  database-generated.** The Domain `BossDefinition` carries the value
  explicitly as `required string BossDefinitionId`; persistence stores it as
  the PK.
- Domain carrier implication for TASK-044 — **a new member is required**: the
  Domain record (`src/backend/GameServer.Domain/Bosses/BossDefinition.cs`)
  must gain `required string BossDefinitionId`. This is TASK-044's
  implementation input; **nothing was added here.**
- TASK-045 compatibility — **preserved**: the five persistence concepts
  (`BossDefinitionId`, `Identity`, `Element`, `PassiveDefinition`,
  `SkillDefinition`) are unchanged and no column was added (§Q2.5 satisfied).

### Scope Verification

- [x] Documentation-only: zero source, test, migration, EF, provisioning,
      Redis, SignalR, API, or gameplay changes
- [x] TASK-044 untouched and not implemented (file unmodified,
      `Status: BACKLOG`)
- [x] Provisioning not reopened — no `HasData`, seed, startup loader, or
      migration-inserted production row; `DATABASE.md` §1 note item 5 and §5
      item 4 remain authoritative
