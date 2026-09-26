# TASK-045 — Resolve BossDefinition Persistence Contract

---

## Metadata

```text
Task ID:           TASK-045
Type:              DOCUMENTATION
Status:            DONE
Risk:              MEDIUM
Priority:          HIGH
Primary Agent:     review
Supporting Agents: backend, persistence, gameplay
Workflow:          documentation/documentation-change.md
Skills:            discovery/documentation-discovery, discovery/impact-analysis,
                   quality/documentation-consistency, backend/persistence-analysis,
                   quality/architecture-conformance
Dependencies:      None
Blocks:            TASK-044 (must not be edited by this task — see Scope)
Estimate:          Complex (documentation-only; five explicit human decision points)
```

**Status note.** Created `BACKLOG` 2026-09-26. Set to `BLOCKED` the same
day (decisions A–E verified; Q1–Q3 fired — see §5A and §8). Human answered
Q1–Q3 the same day; answers verified against authoritative documents with no
rule conflicts (AGENTS.md §4 not triggered). Contract written to
`DATABASE.md` (v1.7: §1 `BossDefinition` persistence contract, §3
constraints, §5 item 4); task file updated with resolutions, acceptance,
and evidence (§5A, §8, §9, §11, §12). Quality review PASS (Quality Review
Record). File moved to `tasks/completed/`, `Status: DONE`.

---

## 1. Objective

Resolve the authoritative persistence contract for the `BossDefinition`
entity so that **TASK-044 (Implement BossDefinition Persistence)** can be
implemented without inventing a schema. Concretely, this task must make
the following five decisions explicit, evidenced against `docs/`, and
answered by a human (or reported `BLOCKED`):

- **Decision A — `PassiveDefinition` representation:** what the
  `DATABASE.md` §1 `BossDefinition.PassiveDefinition` entry is in
  PostgreSQL — shape, column count, identity carriage, nullability.
- **Decision B — `SkillDefinition` representation:** the same for the
  `SkillDefinition` entry.
- **Decision C — Domain→database mapping:** where each documented field of
  the Domain `BossDefinition` record maps in persistence, and whether
  `DATABASE.md` §1's five columns are sufficient or additional columns are
  required to preserve documented Boss configuration data.
- **Decision D — row provisioning:** which mechanism creates
  `BossDefinition` rows, and which document (or ADR) owns that decision.
- **Decision E — "5 MVP Bosses" vs 3 content-defined Bosses:** what the
  count means for persistence, where the reconciliation is documented, and
  what row expectation TASK-044 implements once this contract exists.

Documentation-only task. **No source, test, configuration, or migration
changes.** No schema, column type, seed mechanism, or Boss count may be
chosen by this task for implementation convenience (`AGENTS.md` §7, §9,
§20). The answers are **human decision points** — this task makes them
explicit, evidenced, and answerable; it must not choose for the project.

---

## 2. Authoritative References

**Read first (required):**

| # | Document | Why |
|---|---|---|
| 1 | `docs/02-technical/DATABASE.md` §1 | Owns the `BossDefinition` entity sketch (5 entries incl. `PassiveDefinition`, `SkillDefinition`) and the "(static content: 5 MVP Bosses)" annotation; §2 relationships; §3 constraints (lists none for BossDefinition); §4 indexes; §5 item 1 (exact SQL types are implementation detail) |
| 2 | `docs/01-game-design/BOSS_RULES.md` §1 (Boss structure), §3–§4 (Passive/Skill), §6–§6.3 (MVP configuration values, "not yet content-defined" note), §6.4 (BossId/PassiveId/SkillId identity contract) | Owns Boss content semantics and canonical identities; owns the 3-defined / 5-scoped statement |
| 3 | `docs/01-game-design/PASSIVE_RULES.md` §1–§4 | Owns Passive threshold/effect/reset semantics referenced by `DATABASE.md` §1's PetDefinition annotation |
| 4 | `docs/00-overview/MVP_SCOPE.md` §1 (Bosses: "5 Bosses"), §4 (classification authority) | Canonical scope count |
| 5 | `docs/01-game-design/GAME_RULES.md` §19 | Defers scope to `MVP_SCOPE.md` (the chain `BOSS_RULES.md` §6.3 cites) |
| 6 | `docs/02-technical/ARCHITECTURE.md` §1 (`Bosses/` module), §2 (layer direction), §5 item 1 (no plugin system; concrete Domain type with data-driven configuration) | Bounds any provisioning/read-path answer; candidate ADR trigger |
| 7 | `docs/02-technical/TDD.md` §2/§4 (persistence layer, hot-path boundary) | Bounds where a provisioning/read mechanism may live |
| 8 | `docs/02-technical/GAME_STATE.md` §2.4–§2.4.4 | What a battle's `BossState` requires at battle creation (the data a persisted definition must be able to supply, if persistence is to supply it at all) |
| 9 | `docs/03-decisions/ADR/ADR-006-postgresql-persistence.md` | PostgreSQL for durable data; references "static Definition tables" — contains no provisioning decision |
| 10 | `AGENTS.md` §2 (precedence), §4 (conflict resolution), §7 (no invented rules), §9 (no speculative abstractions), §17 (documentation change rule), §18 (architecture change rule), §20–§21 (stop conditions, output discipline) | Process contract |
| 11 | `tasks/backlog/TASK-044-implement-bossdefinition-persistence.md` | The blocked consumer; **read-only** for this task |
| 12 | `tasks/README.md` §6, `tasks/TASK_TEMPLATE.md`, `tasks/TASK_TYPES.md` (DOCUMENTATION type), `tasks/TASK_LIFECYCLE.md` | Task-creation and lifecycle rules |

**Read for evidence only (non-authoritative — code sits below tasks in the
precedence order, `AGENTS.md` §2):**

- `src/backend/GameServer.Domain/Bosses/BossDefinition.cs` (13-field record; XML header prose)
- `src/backend/GameServer.Domain/Bosses/BossDefinitions.cs` (3 in-code definitions)
- `src/backend/GameServer.Infrastructure/Postgres/Configurations/PetDefinitionConfiguration.cs`,
  `CardDefinitionConfiguration.cs`, `RelicDefinitionConfiguration.cs` (implementation precedent only)
- `src/backend/GameServer.Application/Battle/BattleStartService.cs` (current definition source)

---

## 3. Scope

**In scope**

1. Documentation discovery: establish, with file+section evidence, exactly
   what `docs/` does and does not define about each `BossDefinition`
   persistence question (§4).
2. For each of Decisions A–E: state the question precisely, identify the
   document that **should own** the answer (`AGENTS.md` §2, §4), lay out
   clearly separated options where the docs underdetermine the answer, and
   obtain a human answer — or produce a `BLOCKED` report naming the
   unanswered decision.
3. Update the **canonical owning document only** for each approved answer
   (candidate owners in §10), and make dependent references consistent
   where they become stale (`AGENTS.md` §17).
4. Record precisely what TASK-044 needs afterwards as a report item —
   **without editing TASK-044**.
5. Report discovered adjacent issues (§6) without fixing them.

**Out of scope**

- Any `src/`, `tests/`, EF configuration, or migration file change.
- Editing `tasks/backlog/TASK-044-*`, `tasks/backlog/TASK-041-*`, or any
  file under `tasks/completed/`.
- Choosing a column type/shape (string / Guid / int / JSON / FK / nullable
  FK / embedded object), a provisioning mechanism, or a Boss count —
  unless the human makes that choice in answer to §5; this task records
  the decision, it does not make it.
- Inventing the two not-yet-content-defined Bosses (`BOSS_RULES.md` §6.3)
  or any Boss value, Passive, Skill, or identity (`AGENTS.md` §7).
- Boss gameplay, Boss AI, Boss effects, Rewards/XP (TASK-033),
  `BattleResult` (TASK-041), authentication, Redis, SignalR.
- Creating or amending an ADR — unless an approved answer requires one, in
  which case **stop** (§8; `AGENTS.md` §18).

---

## 4. Current State — Evidence (do not re-derive; verify then cite)

### 4.1 Gap 1 — the two column entries are bare labels

| Fact | Location |
|---|---|
| `BossDefinition` is sketched with exactly five entries: `BossDefinitionId (PK)`, `Identity`, `Element`, `PassiveDefinition`, `SkillDefinition`; the two latter are bare names, annotated only "(BOSS_RULES.md)" | `DATABASE.md` §1 (`BossDefinition`) |
| No type, column count, nullability, FK, or representation is stated for either entry | `DATABASE.md` §1 (absence), §3 (no `BossDefinition` constraint listed), §4 (no `BossDefinition` index) |
| §5 item 1 makes exact **SQL types** an implementation detail — it does not resolve which **columns/structures exist** or what they hold | `DATABASE.md` §5 item 1 |
| `DATABASE.md` §2 records only `BattleResult N─1 BossDefinition`; there is no `BossDefinition → Passive`/`→ Skill` relationship anywhere | `DATABASE.md` §2 |
| Sibling label `PetDefinition.PassiveDefinition` is annotated "(threshold/effect reference — PASSIVE_RULES.md)" — a **different, richer annotation** than BossDefinition's | `DATABASE.md` §1 (`PetDefinition`) |
| The Pet label was implemented as **two columns** (`PassiveId` + `PassiveThreshold`), with a code comment asserting BossDefinition "carries the same pair" | `PetDefinitionConfiguration.cs` (implementation; `AGENTS.md` §2 places code below tasks — this is precedent, not contract) |
| No task records a decision about the Pet `PassiveDefinition` mapping either (grep of `tasks/` found none) | `tasks/` (absence) |

**Consequence:** the repository cannot safely infer whether
`PassiveDefinition` / `SkillDefinition` are `string`, `Guid`, `int`, JSON,
FK, nullable FK, embedded objects, one column, or several. `BOSS_RULES.md`
§6.4 fixes the identity **values**; it says nothing about PostgreSQL
storage.

### 4.2 Gap 2 — Domain record carries 13 fields; persistence documents 5 entries

| Fact | Location |
|---|---|
| Domain `BossDefinition` is a 13-parameter record: BossId, Element, MaxHP, ATK, DEF, PassiveId, PassiveThreshold, SkillId, SkillBaseDamage, SkillChargeRequirement, SkillCooldownTurns, EnrageThreshold, PassiveResetBehavior | `BossDefinition.cs` |
| `DATABASE.md` §1 lists five entries for the table — no entry for MaxHP/ATK/DEF/PassiveThreshold/PassiveResetBehavior/SkillBaseDamage/SkillChargeRequirement/SkillCooldownTurns/EnrageThreshold | `DATABASE.md` §1 |
| Those values are documented game configuration owned by the game rules | `BOSS_RULES.md` §6.1–§6.3 |
| A battle's `BossState` needs HP/MaxHP/ATK/DEF, PassiveId+threshold, Skill charge/cooldown, EnrageThreshold at creation | `GAME_STATE.md` §2.4–§2.4.4 |
| Currently the only definition source is in-code static content (`BattleStartService` iterates `BossDefinitions.All`); no Boss read path, repository, or DB source exists | `BossDefinitions.cs`, `BattleStartService.cs`; `ARCHITECTURE.md` §5 item 1; `AGENTS.md` §9 |
| Domain XML header claims "No registry, service, or persistence" — non-authoritative prose (`AGENTS.md` §2) that TASK-044 already flags for alignment | `BossDefinition.cs` header |

**Consequence:** no document states whether persistence must preserve the
full documented configuration, only identities, or some subset — and if
the five documented entries are insufficient, no document says what
additional columns exist or where that is decided.

### 4.3 Gap 3 — no provisioning mechanism is documented or implemented anywhere

| Fact | Location |
|---|---|
| `DATABASE.md` labels `BossDefinition` "(static content: 5 MVP Bosses)" and similarly labels PetDefinition/CardDefinition/RelicDefinition as static content | `DATABASE.md` §1 |
| No document describes how any Definition row is created: grep of `docs/` for provisioning terms (HasData / seed data / content loader / startup seed / migration seed / provision) returns nothing | `docs/` (absence) |
| No seeding exists in code either: grep of `src/backend` for `HasData`/`InsertData`/`EnsureCreated`/`Seed` returns nothing | `src/backend` (absence) |
| ADR-006 mentions "foreign keys to static Definition tables" but decides no provisioning mechanism | `ADR-006` |
| `TDD.md` "seed" references concern only the RNG | `TDD.md` §6 |

**Consequence:** EF seed, migration seed, application startup, explicit
content loader, manual deployment data, and any other mechanism are all
equally consistent with the docs. The gap is not Boss-specific (Pet/Card/
Relic rows have the same gap — §6), but TASK-044 is where it blocks.

### 4.4 Gap 4 — "5 Bosses" vs 3 content-defined Bosses

| Fact | Location |
|---|---|
| MVP scope: "5 Bosses / Element, Passive, Skill per Boss" — `MVP_SCOPE.md` is "the single source of truth for IN/OUT/FUTURE" | `MVP_SCOPE.md` §1, header |
| `GAME_RULES.md` §19 defers scope entirely to `MVP_SCOPE.md` | `GAME_RULES.md` §19 |
| `BOSS_RULES.md` §6 defines three Bosses; §6.3 states "Two additional MVP Bosses (5 total per GAME_RULES.md §19 scope) are not yet content-defined" plus authoring requirements; §6.4 fixes identities "for the three content-defined MVP Bosses" | `BOSS_RULES.md` §6, §6.3, §6.4 |
| `DATABASE.md` §1 header annotates the table "(static content: 5 MVP Bosses)" | `DATABASE.md` §1 |
| In-code content holds exactly the three, deliberately ("Only content-defined Bosses appear") | `BossDefinitions.cs` |

**Consequence:** count-of-scope (5, canonical in `MVP_SCOPE.md`),
count-of-content (3, owned by `BOSS_RULES.md`), and count-of-annotated-rows
(5, in `DATABASE.md` §1) are three statements at three layers. What
persistence must support *now*, and where that reconciliation is
documented, is not stated anywhere. Two rows cannot be created — their
content does not exist (`AGENTS.md` §7 forbids inventing them).

---

## 5. Decision Points (the deliverable)

Each decision: questions to answer explicitly, then options to evaluate
against the cited criteria. **Do not decide unilaterally.** If an answer
cannot be derived from authoritative documents, report `BLOCKED` naming
the exact question for the human. Never select an option for implementation
convenience.

### Decision A — `BossDefinition.PassiveDefinition` representation

**Questions:**

- **A1.** Does `PassiveDefinition` name one column or a column set? (The
  sibling Pet label was implemented as two columns — precedent, not
  authority.)
- **A2.** What is stored: the `BOSS_RULES.md` §6.4 `PassiveId` identity
  alone; identity + threshold; identity + threshold + reset behavior; a
  structured/embedded value; or a reference to another documented
  structure? State the exact resulting column set.
- **A3.** Is the §6.4 `PassiveId` persisted directly as part of this
  contract, or carried through some other documented structure? (`§6.4`
  fixes event identity only; `DATABASE.md` §2 defines no Passive
  relationship.)
- **A4.** Nullability/requiredness of each resulting column — is a Passive
  required for every Boss? (`BOSS_RULES.md` §1 shows a Passive in the
  Boss structure, but `DATABASE.md` §1/§3 state no nullability or
  constraint.)

**Options (evaluate; choose one only with human approval, or report blocked):**

- **A-i — identity-only column:** store the §6.4 `PassiveId`.
  *Must check:* where threshold/reset values then live (Decision C);
  `PASSIVE_RULES.md` §1–§4 semantics that a persisted threshold would
  need; §6.2's Always-Active marker.
- **A-ii — identity + configuration columns** (the shape
  `PetDefinitionConfiguration` implemented for the Pet label).
  *Must check:* `AGENTS.md` §7 — a code comment claiming "BossDefinition
  carries the same pair" is implementation precedent, not contract; a
  documented basis in `DATABASE.md`/`BOSS_RULES.md` must be named or the
  label annotation must be extended by its owner.
- **A-iii — structured/embedded value (e.g. JSON):** matches the
  `EffectDefinition` precedent only loosely.
  *Must check:* `DATABASE.md` §1 shows JSON only for `RewardSummary`/
  `EffectDefinition`-style prose; whether an embedded object satisfies §3
  constraintability; anti-overengineering (`AGENTS.md` §9).
- **A-iv — FK to a Passive definition table:** **STOP** — no such table is
  documented in `DATABASE.md` §1; adding an entity is a schema/architecture
  expansion (`AGENTS.md` §18, §4) → propose it separately, do not adopt it
  here.
- **A-v — not derivable from `docs/`** → `BLOCKED`, human contract
  decision required (the expected outcome unless the human supplies the
  basis).

### Decision B — `BossDefinition.SkillDefinition` representation

**Questions:**

- **B1.** Same shape question as A1/A2: which columns/structure does
  `SkillDefinition` resolve to?
- **B2.** Is the §6.4 `SkillId` persisted directly, or through another
  documented structure?
- **B3.** Nullability/requiredness (`BOSS_RULES.md` §1 gives every Boss a
  Skill; `DATABASE.md` §1/§3 state nothing).

**Options:**

- **B-i — identity-only column** storing the §6.4 `SkillId`.
  *Must check:* where Skill configuration values live (Decision C).
- **B-ii — identity + skill configuration columns** (base damage, charge
  requirement, cooldown). *Must check:* same basis requirement as A-ii;
  those values are owned by `BOSS_RULES.md` §6.3.
- **B-iii — structured/embedded value.** *Must check:* as A-iii.
- **B-iv — FK to a Skill definition table:** **STOP** — none documented
  (`AGENTS.md` §18).
- **B-v — not derivable** → `BLOCKED`, human decision required.

*Note for the human:* `CardDefinitionConfiguration` stores
`EffectDefinition` as a bounded reference string — again precedent, not a
documented rule for `BossDefinition`.

### Decision C — Domain `BossDefinition` → persistence mapping

**Questions:**

- **C1.** Which documented Domain fields must persistence preserve?
  Enumerate them against `BOSS_RULES.md` §6.1–§6.3 (values) and
  `GAME_STATE.md` §2.4 (what battle creation consumes).
- **C2.** Are `DATABASE.md` §1's five entries sufficient? If not, **what
  additional columns/structures does §1 gain**, and is that a `DATABASE.md`
  §1 edit (contract) or something larger?
- **C3.** If persistence deliberately stores a subset, what is then
  authoritative for battle creation — the persisted row or the in-code
  `BossDefinitions` — and **where is that documented**? State the read-path
  boundary explicitly.
- **C4.** Does the answer change any nullability/constraint statement
  needed in `DATABASE.md` §3?

**Options:**

- **C-1 — persist the full documented configuration:** extend `DATABASE.md`
  §1 with the additional columns (named by the human), then TASK-044 maps
  them.
  *Must check:* §5 item 1 (types remain implementation detail, column
  existence does not); §3 (any new constraint must be documented, not
  invented); whether a read path must then exist (→ C-3).
- **C-2 — persist only the five documented entries (identities + Element +
  Identity):** no schema change; the record's numeric configuration stays
  in-code.
  *Must check:* then document where the in-code values remain
  authoritative and that `BossDefinition` persistence is explicitly not
  the battle-creation source — otherwise the table's purpose is ambiguous
  and TASK-044's round-trip test proves nothing about contract compliance.
- **C-3 — any answer that makes the database the battle-creation source
  for Boss configuration:** **STOP** — that changes the documented read
  path/architecture (`ARCHITECTURE.md` §2/§5, `TDD.md` §2/§4,
  `AGENTS.md` §18) → propose an ADR first; do not resolve it inside this
  documentation task.
- **C-4 — not derivable** → `BLOCKED`, human decision required.

### Decision D — BossDefinition row provisioning

**Questions:**

- **D1.** Which mechanism creates rows: EF seed, migration seed,
  application startup, explicit content loader, manual deployment data, or
  another mechanism? (`DATABASE.md` calls the content "static" and decides
  nothing; §4.3 shows no doc or code precedent for any Definition table.)
- **D2.** Which document owns the decision — `DATABASE.md` (technical
  storage contract) — or does the mechanism require an ADR
  (`AGENTS.md` §18: never introduce an architectural decision inside a
  code change)?
- **D3.** Does the decided mechanism also cover PetDefinition /
  CardDefinition / RelicDefinition rows (same undocumented gap)? If yes,
  name that as a follow-up — do not silently generalize (§6).

**Options:** the six mechanisms listed in D1 plus "report blocked".
Evaluate each against `ARCHITECTURE.md` §2 (layer that may own it),
`TDD.md` §2/§4 (not on the hot path), and `AGENTS.md` §9 (no speculative
infrastructure). **Do not pick one for convenience.** If the mechanism is
architectural → ADR path, stop (§8).

**Explicitly not chosen by this task:** `HasData`, any seed, any startup
hook — until the human approves D1/D2.

### Decision E — "5 MVP Bosses" vs 3 content-defined Bosses

**Questions:**

- **E1.** What does "(static content: 5 MVP Bosses)" require of
  persistence **now**: a table able to hold 5 rows with 3 rows once
  provisioning exists; some explicit row-count expectation; or a corrected
  annotation?
- **E2.** Where is the reconciliation documented: `MVP_SCOPE.md` §1
  (canonical count), `BOSS_RULES.md` §6.3 (already states 2 are not yet
  content-defined), and/or `DATABASE.md` §1 (the annotation)? Which
  document is wrong if any is?
- **E3.** What row expectation does TASK-044 implement once this contract
  exists: table only (no rows — its current Out of Scope), the 3
  content-defined rows (requires Decision D answered), or rows deferred
  until all 5 Bosses are content-defined? Record the answer as a report
  item for TASK-044 — **do not edit TASK-044**.

**Options:**

- **E-1 — scope 5 / content 3 is already consistent; annotation stands:**
  persistence must be *capable* of 5 rows; no document changes beyond
  stating that in `DATABASE.md` §1.
- **E-2 — annotation is misleading and should be corrected** (e.g. to
  reflect content-defined count with a pointer to `MVP_SCOPE.md` §1):
  `DATABASE.md` §1 edit per `AGENTS.md` §4 (identify owner, propose
  smallest correction, human approves).
- **E-3 — persistence must support all 5 immediately:** **STOP** — the 2
  missing Bosses are not content-defined (`BOSS_RULES.md` §6.3);
  authoring them is a `GAMEPLAY-CHANGE`/design task (`AGENTS.md` §7), not
  this task.
- **E-4 — not derivable / human picks** → record the decision.

**Never** resolve this by choosing "3" or "5" for implementation
convenience.

---

## 5A. Decisions Received — 2026-09-26 (verification applied; Q1–Q3 resolved, written to docs)

The human answered all five decision points (A–E), then answered the three
stop-condition questions Q1–Q3. Each answer was checked against
authoritative documents; the final answers introduced **no conflict** with
any document (AGENTS.md §4 not triggered). The contract is now written to
`DATABASE.md` v1.7 (§1 contract note items 1–4, §3 constraints, §5 item 4).

| Decision | Answer received | Verification outcome |
|---|---|---|
| A1/A2 | `PassiveDefinition` = structured JSON object `{passiveId, threshold, resetBehavior}` with canonical names/values from PASSIVE_RULES/BOSS_RULES | **RESOLVED (Q1)** — names confirmed verbatim; `resetBehavior` tokens `Default`\|`Partial`\|`Persistent` (§8 Q1); written to `DATABASE.md` §1 contract note item 2 |
| A3 | Non-nullable for every persisted Boss | Verified — matches BOSS_RULES.md §1 (every Boss has one Passive) and §6.4; written to `DATABASE.md` §1/§3 (object NOT NULL) |
| A4 | Part of the static model; **the database definition is the source for creating the BattleState snapshot** | **RESOLVED (Q2 → option b)** — amended: the row supplies identity/configuration only; combat stats combine at battle creation (§8 Q2); written to `DATABASE.md` §1 contract note item 1 |
| B1/B2 | `SkillDefinition` = structured JSON `{skillId, baseDamage, chargeRequirement, cooldownTurns}` | **RESOLVED (Q1)** — names confirmed verbatim (§8 Q1); written to `DATABASE.md` §1 contract note item 3 |
| B3 | Non-nullable for every persisted Boss | Verified — BOSS_RULES.md §1, §6.4; written to `DATABASE.md` §1/§3 (object NOT NULL) |
| C1 | Row = static definition required to **reconstruct the authoritative battle-start configuration** | **RESOLVED (Q2 → option b)** — amended per Q2 answer; written to `DATABASE.md` §1 contract note item 1 |
| C2 | Persist exactly the five documented concepts; do not flatten the 13-field Domain record into columns | Recorded — matches DATABASE.md §1; held pending Q2 (see below) |
| C3 | JSON objects are the persistence representation for passive/skill configuration otherwise spread over Domain fields | Recorded — fits DATABASE.md precedent: `RewardSummary` is "(JSON — member list owned by TASK-033)" and §5 item 3 establishes that **member lists are owned by the defining decision** (here: this contract, once Q1 answers) |
| C4 | At battle creation the row is resolved into Domain `BossDefinition`/`BossState`; thereafter the BattleState/Redis contract governs | Recorded — consistent with ARCHITECTURE.md §2 and the ADR-005 Redis boundary |
| D1/D2 | Rows are static content, provisioned deterministically from canonical Boss definitions; idempotent; never dependent on a player's runtime battle | Recorded — to be written into DATABASE.md with the rest of the contract |
| D3 | Use an existing documented static-definition provisioning pattern; **if none is documented, TASK-044 must STOP rather than invent one**; TASK-045 does not authorize a new seed architecture | Verified — **no such pattern exists** (no provisioning provision in any document; no `HasData`/`InsertData`/`EnsureCreated`/seed anywhere in `src/backend`). TASK-044 already lists row provisioning as Out of Scope with its own STOP (TASK-044 §Out of Scope, §Stop Conditions), so nothing new is blocked and nothing is authorized. Any rows require a future provisioning-contract task (§6 issue 1) |
| E1/E2 | MVP target = **5** (`MVP_SCOPE.md` §1); content-defined set = **3** (`BOSS_RULES.md` §6) | Verified — no conflict; `DATABASE.md` §1 "(static content: 5 MVP Bosses)" stands as the scope statement |
| E3 | Persist only content-defined Bosses; never invent the missing 2 or create placeholder rows | Recorded — constrains whichever future task provisions rows; TASK-044 creates no rows at all (D3 + its own Out of Scope) |

**Additional Rules noted:** (1) `TASK-041` untouched — verified.
(2) TASK-044 not implemented by this task — no code written.
(5) Any architectural change beyond the decided persistence model
requires an ADR first — recorded as a standing stop condition (§8).

---

## 6. Discovered Adjacent Issues — report only, do not fix

1. **Provisioning gap is repository-wide.** PetDefinition, CardDefinition,
   and RelicDefinition rows have exactly the same undocumented,
   unimplemented provisioning story (§4.3). Impact: definition tables are
   empty in any real deployment. Suggested follow-up: one provisioning
   contract task covering all Definition tables once Decision D names the
   owner.
2. **No documented read path from `BossDefinition` (or any Definition
   table) to battle creation.** `BattleStartService` reads the in-code
   `BossDefinitions`; `PetLevelService`/`PetRepository` show a repository
   read exists for Pets. Whether persistence becomes a source is Decision
   C-3 territory and may require an ADR. Suggested follow-up: a separate
   task if the human's C answer opens it.
3. **`PetDefinitionConfiguration.cs` comment** asserting BossDefinition
   "carries the same pair" will read as authority to future agents; it is
   code-below-tasks precedent (`AGENTS.md` §2). If the human's Decision A
   differs, TASK-044's documentation-impact step should correct the
   comment — not this task (code out of scope).
4. **`BossDefinition.cs` XML header** ("No registry, service, or
   persistence") becomes stale once TASK-044 lands — TASK-044 already owns
   aligning it; noted so this task does not duplicate that work.
5. **Docs-vs-code discrepancy — Thủy Ma PassiveThreshold.**
   `BOSS_RULES.md` §6.2 documents Thủy Ma as always-active with "no
   PassiveThreshold"; `BossDefinitions.cs` carries `PassiveThreshold: 0`.
   Impact: any persisted threshold value choice must not be read off the
   code as if it were the rule (Q3). Suggested follow-up: human confirms
   which side is correct when answering Q3, then the stale side is fixed
   by its owner (rule doc or code) — not by this task.

---

## 7. Preserved Decisions (must remain true after this task)

1. **No schema is invented here.** Until a human answers §5, the
   representation of `PassiveDefinition`/`SkillDefinition` and the
   Domain→DB mapping remain *unresolved*, not defaulted (`AGENTS.md` §20).
2. **`BOSS_RULES.md` §6.4 identities are unchanged** — no new BossId,
   PassiveId, or SkillId; no Boss content authored (`AGENTS.md` §7).
3. **No Boss gameplay, AI, effects, or rewards** are specified, changed, or
   implied.
4. **TASK-044, TASK-041, TASK-043, and all `tasks/completed/` files are
   untouched**; TASK-044's blocking dependency is reported, not edited
   (`AGENTS.md` §16).
5. **Server authority unaffected** — `BOSS_RULES.md` §8 / ADR-001 remain
   the authority for battle resolution regardless of where definitions are
   stored.
6. **No speculative abstraction** — no repository/registry/service for
   Boss definitions is endorsed (`ARCHITECTURE.md` §5 item 1,
   `AGENTS.md` §9`).
7. **Scope stays IN** — persistence and Bosses are `MVP_SCOPE.md` §1 items;
   nothing OUT/FUTURE is introduced (`MVP_SCOPE.md` §2–§4).

---

## 8. Stop Conditions (report `BLOCKED`, do not guess)

- The schema representation for `PassiveDefinition` **or**
  `SkillDefinition` cannot be derived from authoritative documentation
  (§7/§20 of `AGENTS.md`).
- `PassiveDefinition` representation is unresolved at review time.
- `SkillDefinition` representation is unresolved at review time.
- The Domain→database mapping (which fields persist, where) is unresolved.
- The provisioning mechanism is unresolved, or answering it requires
  choosing a mechanism for convenience rather than by evidence.
- The 3-vs-5 Boss scope cannot be determined, or resolving it would require
  inventing the two undefined Bosses (`AGENTS.md` §7).
- Resolving any decision requires a **new architectural decision** (new
  store, changed persistence/read strategy, module-boundary change) →
  propose an ADR first (`AGENTS.md` §18); stop this task.
- Any option contradicts another authoritative document (`AGENTS.md` §4) —
  report both sources; do not pick the convenient one.
- Execution would require editing `TASK-044`, `TASK-041`, any
  `tasks/completed/` file, or any `src/`/`tests/` file (`AGENTS.md` §16).
- MVP_SCOPE §1 read as not covering the change → stop per `MVP_SCOPE.md` §4.
- Skill budget (7) exceeded → stop and decompose (`tasks/TASK_TEMPLATE.md`).

### Stop Condition Report — fired 2026-09-26 (human Additional Rules 3 and 4); RESOLVED same day

**Trigger:** Decisions A–E were received (§5A); verification found one
under-specified contract (Q1) and one internal conflict (Q2), plus one
edge needing a ruling (Q3). **No documentation was edited** until the
human answered. `tasks/backlog/TASK-044-*` and `TASK-041-*` untouched. No
source code touched.

**Resolutions (2026-09-26, human second round — verified, no conflicts):**

| # | Human's answer | Recorded in |
|---|---|---|
| Q1 | Exact member names confirmed verbatim: `PassiveDefinition` = `{passiveId, threshold, resetBehavior}`; `SkillDefinition` = `{skillId, baseDamage, chargeRequirement, cooldownTurns}`. Reset tokens exactly `Default`\|`Partial`\|`Persistent` mapping to PASSIVE_RULES.md §4's Default Reset / Partial Reset / No Reset — Persistent; no additional behaviors. If internal type naming differs, JSON names are the contract and the mapping is documented (`Persistent` ↔ `NoReset`). | `DATABASE.md` §1 contract note items 2–3 (incl. enum mapping) |
| Q2 | **Option (b)** — no schema expansion; the five columns stand; `MaxHP`/`ATK`/`DEF`/`EnrageThreshold` stay sourced from authoritative Domain content at battle creation; the row supplies persistent identity/configuration only; server combines both at battle creation to construct `BossState`; that split is documented. No new columns unless an existing authoritative document explicitly requires them. | `DATABASE.md` §1 contract note item 1 |
| Q3 | Always-active passives (no documented threshold, per BOSS_RULES.md §6.2): `"threshold": null` — never `0` as sentinel; `PassiveDefinition` object stays NOT NULL; `null` semantics documented. | `DATABASE.md` §1 contract note item 2, §3 constraint |

---

**Q1 — Exact JSON member names/tokens are not defined by authoritative
documentation (Additional Rule 4).** *(Blocking A1/A2, B1/B2)*

Verified by case-insensitive **and** case-sensitive search across all of
`docs/`:

- **Documented serialized members (usable as-is):** `passiveId` and
  `threshold` (SIGNALR_PROTOCOL.md §3.2.16 payload + field table —
  `threshold`, int, always; GAME_EVENTS.md §2 references `threshold`);
  `skillId` (SIGNALR_PROTOCOL.md §3.2.18; BOSS_RULES.md §6.4 names
  `BossSkillCast.skillId`).
- **Absent from every document:** `resetBehavior`, `baseDamage`,
  `chargeRequirement`, `cooldownTurns`.
- **Reset-behavior values:** PASSIVE_RULES.md §4 defines only prose
  variants — "Default", "Partial reset", "No reset / persistent". No
  serialized token strings exist anywhere; enum tokens would be invented.
- **Documented candidate names for the four missing members** (for the
  human to confirm or replace — the agent will not choose; Rule 4):
  | Human's name | Documented candidate | Source |
  |---|---|---|
  | `resetBehavior` | "Reset Behavior" / Domain `PassiveResetBehavior` | PASSIVE_RULES.md §1, §4 (label); Domain field (code) |
  | `baseDamage` | "Skill Base Dmg" / Domain `SkillBaseDamage` | BOSS_RULES.md §6.3 table |
  | `chargeRequirement` | `SkillChargeRequirement` | GAME_STATE.md §2.4.3 |
  | `cooldownTurns` | `SkillCooldown` / "Cooldown (CD)" | GAME_STATE.md §2.4.1; BOSS_RULES.md §6.3 |

**Needed from human:** the exact member names for those four fields and
the exact string tokens for reset behavior — or an instruction that a
different document owns them and must be amended first.

---

**Q2 — The decided persistence model cannot satisfy its own stated
purpose (Additional Rule 3; conflict inside A4/C1 vs C2/A2/B2).**
*(Blocking A4, C1)*

- A4: "the database definition is the source for creating that snapshot."
  C1: the row must "reconstruct the authoritative battle-start
  configuration."
- Neither the five persisted concepts (C2) nor either JSON object (A2/B2)
  contains **`MaxHP`, `ATK`, `DEF`, `EnrageThreshold`** — all documented
  Boss definition values (BOSS_RULES.md §6.1) that GAME_STATE.md §2.4
  requires in `BossState` at battle creation.
- Therefore the row, as decided, cannot by itself reconstruct the
  battle-start configuration: four documented definition values have no
  home in the model.

**Options for the human (choose one or supply another):**

- **(a)** Persist the four values as well — then name where (new columns
  under C2's "unless explicitly required by the authoritative
  documentation" clause, or an additional JSON member) and the exact
  column/member names (Q1 applies again).
- **(b)** Amend A4/C1: the row supplies `Identity`/`Element`/
  `PassiveDefinition`/`SkillDefinition`, while `MaxHP`/`ATK`/`DEF`/
  `EnrageThreshold` continue to be sourced from the in-code
  `BossDefinitions` at battle creation — with that split documented in
  `DATABASE.md` §1.
- **(c)** Other.

---

**Q3 — `threshold` for Thủy Ma (Additional Rules 3/4; BOSS_RULES.md
§6.2).** *(Blocking A2 edge case)*

- BOSS_RULES.md §6.2: Thủy Ma's trigger is always-active and "it has **no
  PassiveThreshold** for match counting … is never charged via
  `PassiveTracker.Charge`."
- A2's fixed shape carries `"threshold": <int>`; the Domain record
  carries `PassiveThreshold: 0` (`BossDefinitions.cs` — code, non-authoritative).
  This is also a **pre-existing docs-vs-code discrepancy** (§6 issue 5).

**Needed from human:** the persisted representation for always-active
passives — member present with which value, or member omitted/null —
noting A3 keeps the whole `PassiveDefinition` object non-null either way.

---

---

## 9. Acceptance Criteria

- [x] **A resolved:** the representation of `BossDefinition.PassiveDefinition`
      — exact column set, identity carriage (relation to `BOSS_RULES.md`
      §6.4), and nullability — is documented in its owning document with
      file/section citations, **or** the task is `BLOCKED` naming
      unanswered question A1–A4. → `DATABASE.md` §1 contract note item 2
      (+ §3), human answers Q1/Q3.
- [x] **B resolved:** same for `SkillDefinition` (B1–B3), or `BLOCKED`
      naming B1–B3. → `DATABASE.md` §1 contract note item 3 (+ §3), Q1.
- [x] **C resolved:** the Domain `BossDefinition` → persistence mapping is
      enumerated field-by-field; it is explicit whether `DATABASE.md` §1
      gains columns, and what is authoritative at battle creation; or
      `BLOCKED` naming C1–C4. → no columns gained (Q2 option b);
      `DATABASE.md` §1 contract note item 1 states the
      persistent-record vs combat-stat split and battle-creation
      combination.
- [x] **D resolved:** the provisioning mechanism and its owning document
      (or the ADR requirement) are named, or `BLOCKED` naming D1–D3. →
      human decision: no documented pattern exists; none may be invented;
      a future provisioning-contract decision is required before any row
      exists — recorded in `DATABASE.md` §1 contract note item 4 and §5
      item 4.
- [x] **E resolved:** the meaning of "5 MVP Bosses" for persistence, the
      document that reconciles it, and the row expectation reported for
      TASK-044 are recorded, or `BLOCKED` naming E1–E3. → annotation
      clarified in `DATABASE.md` §1 (`BossDefinition` header): MVP scope
      target 5 (`MVP_SCOPE.md` §1) / content-defined 3 (`BOSS_RULES.md`
      §6), only content-defined rows may ever be provisioned.
- [x] Every resolved decision is written to the **canonical owner only**
      (`AGENTS.md` §2, `documentation-change.md` canonical-owner rule);
      dependent references fixed only where stale; no rule/value duplicated
      across documents (`tasks/README.md` §9). → only `DATABASE.md`
      edited; grep of `docs/` for `BossDefinition` shows no other document
      referencing the annotation/contract; `BOSS_RULES.md`,
      `PASSIVE_RULES.md`, `GAME_STATE.md` unchanged (no stale references —
      no section numbers moved).
- [x] `PetDefinitionConfiguration`/`CardDefinitionConfiguration`/
      `RelicDefinitionConfiguration` precedent was **not** cited as
      authority anywhere in the recorded decisions. → contract cites
      `DATABASE.md`/`BOSS_RULES.md`/`PASSIVE_RULES.md`/`GAME_STATE.md`
      only; configuration code appears only as non-authoritative evidence
      (§4).
- [x] No schema, type, seed, count, Boss content, or mechanism appears in
      the recorded output unless a human chose it in answer to §5. →
      member names/tokens/null semantics/split/no-provisioning/scope all
      human-chosen (Q1–Q3, D, E); no agent-chosen value present.
- [x] **No source, test, EF configuration, or migration file is changed**;
      all edits are documentation edits (plus the report for TASK-044).
- [x] **TASK-044 and TASK-041 are not modified**; TASK-044's needed
      re-audit notes are delivered in the completion report as a separate,
      human-approved follow-up.
- [x] §6 issues are reported, not fixed; §7 preserved decisions still hold.
- [x] Completion report delivered in the §11 format; any unanswered
      decision ⇒ status `BLOCKED` naming it.
- [x] Quality review checklist passes (`quality/review.md` §1 documentation
      subset). — PASS; results recorded in the Quality Review Record below.

---

## 10. Expected Documentation Changes (only after a decision is approved)

| Decision | Canonical owner (edit here) | Dependent references (fix only if stale) |
|---|---|---|
| A | `docs/02-technical/DATABASE.md` §1 (`BossDefinition` — representation, annotation) | `BOSS_RULES.md` §6.4 only if a persistence-boundary cross-ref becomes stale; `PASSIVE_RULES.md` untouched (semantics unchanged) |
| B | `docs/02-technical/DATABASE.md` §1 (`BossDefinition`) | — |
| C | `docs/02-technical/DATABASE.md` §1 (± §3 if a constraint is documented) | `GAME_STATE.md` §2.4 only if sourcing is stated there; `ARCHITECTURE.md`/`TDD.md` only if the read path changes → **ADR first** (`AGENTS.md` §18) |
| D | `docs/02-technical/DATABASE.md` §1/§5 — **or** a new ADR if the mechanism is architectural | none |
| E | `docs/02-technical/DATABASE.md` §1 header annotation (owner of the annotation) | `MVP_SCOPE.md` §1 / `BOSS_RULES.md` §6.3 only if the human finds one actually wrong — note `BOSS_RULES.md` §6.3 already reconciles 3-vs-5 |

If a decision's answer requires editing a document **not** listed as its
owner, stop and re-check §8 (`AGENTS.md` §4).

---

## 11. Completion Evidence — required final report format

```text
Task Created          TASK-045 tasks/active/TASK-045-resolve-bossdefinition-
                      persistence-contract.md (created in backlog/, blocked/
                      round-trip, now active/)
Exact Ambiguities     A (PassiveDefinition), B (SkillDefinition), C (Domain→DB
                      mapping / additional columns), D (provisioning),
                      E (5-vs-3 scope) — with file+section evidence each (§4)
Decisions Required    A1–A4 / B1–B3 / C1–C4 / D1–D3 / E1–E3 — all answered:
                      A = JSON {passiveId, threshold, resetBehavior}, NOT NULL
                      (Q1/Q3); B = JSON {skillId, baseDamage, chargeRequirement,
                      cooldownTurns}, NOT NULL (Q1); C = five columns stand,
                      stats split documented, combine at battle creation
                      (Q2 option b); D = no provisioning pattern exists, none
                      may be invented, future contract required; E = scope 5 /
                      content 3, only content-defined rows ever provisioned
Authoritative Sources Inspected   DATABASE.md §1/§3/§5; BOSS_RULES.md §1, §6–
                      §6.4; PASSIVE_RULES.md §1–§4; MVP_SCOPE.md §1/§4;
                      GAME_RULES.md §19; ARCHITECTURE.md §1/§2/§5; TDD.md §2/
                      §4; GAME_STATE.md §2.4; ADR-006; AGENTS.md §2/§4/§7/§9/
                      §17/§18/§20; SIGNALR_PROTOCOL.md §3.2.16/§3.2.18;
                      GAME_EVENTS.md §2 (+ code as non-authoritative evidence:
                      BossDefinition.cs, BossDefinitions.cs, three Definition
                      configurations, BattleStartService.cs, PassiveResetBehavior.cs)
Documents Expected to Change   docs/02-technical/DATABASE.md — EDITED (v1.7:
                      §1 BossDefinition contract note items 1–4 + block
                      annotation, §3 four constraint lines, §5 item 4);
                      no dependent reference became stale (verified by grep)
Dependency Graph       TASK-045 blocks TASK-044; TASK-041 depends on TASK-043
                      (DONE) + TASK-044 — TASK-041/TASK-044 unmodified
Files Created         the TASK-045 file
Files Modified        docs/02-technical/DATABASE.md; tasks/active/TASK-045-*.md
Source Code Modified  None  (documentation-only)
```

---

## 12. Definition of Done (`AGENTS.md` §22)

- [x] Requirement understood; all five gaps evidenced with file/section
      citations (§4)
- [x] Relevant docs read (§2 table); scope checked against `MVP_SCOPE.md`
      §1/§4
- [x] Existing implementation checked read-only (Domain Bosses, three
      Definition configurations, `BattleStartService`)
- [x] Plan created; human decision points made explicit, not auto-answered
- [x] Code implemented — **N/A (documentation-only)**
- [x] Relevant tests added/updated — **N/A** (verification is documentation
      consistency per `quality/review.md` §1 documentation subset)
- [x] Tests pass — **N/A**; consistency checks recorded instead
- [x] No unrelated behavior or documentation changed (`AGENTS.md` §16)
- [x] Documentation updated only where a decision was approved
      (`AGENTS.md` §17)
- [x] No source-of-truth conflict introduced (`AGENTS.md` §4); §6 issues
      reported, not fixed

---

## Quality Review Record (`quality/review.md` §1, documentation subset) — 2026-09-26

| Item | Result |
|---|---|
| Correctness | PASS — contract matches authoritative sources: member names/tokens per human Q1; `threshold: null` per BOSS_RULES.md §6.2; token set ↔ PASSIVE_RULES.md §4's three variants; `Persistent` ↔ `NoReset` mapping verified against `PassiveResetBehavior.cs` enum; stats-not-stored statement consistent with BOSS_RULES.md §6.1 and GAME_STATE.md §2.4; no rule conflict (§4 not triggered) |
| Architecture | PASS — no persistence/read-path/layer change; battle-creation combination documents current behavior; ADR-005 boundary preserved; ADR-018 not required |
| Scope | PASS — this session edited only `docs/02-technical/DATABASE.md` and this task file; TASK-044/TASK-041 untouched; no src/tests/EF/migration changes |
| Tests | N/A (documentation task) — consistency checks recorded instead: grep of `docs/` for the new member names shows definitions only in DATABASE.md (no duplicate definition, `documentation-change.md` §2); `git status` confirms no source changes from this session |
| Documentation | PASS — canonical owner only (`DATABASE.md`); no dependent reference became stale (no sections moved; `GAME_STATE.md` §2.4.3 citation verified to exist); version bumped 1.6 → 1.7 with prior history preserved |
| Security | N/A |
| Performance | N/A |
| Maintainability | PASS — no abstractions/columns/infrastructure introduced; contract explicitly forbids columns without authoritative requirement |
| Determinism | N/A for logic; provisioning determinism/idempotence requirements recorded from human decision D |

---

## Revision History

| Revision | Date | Change |
|---|---|---|
| 1 | (created) | Created after TASK-044 readiness audit: `DATABASE.md` §1's `PassiveDefinition`/`SkillDefinition` are bare labels, the 13-field Domain record has no documented column mapping, no provisioning mechanism exists anywhere, and 5-vs-3 Boss counts are unreconciled for persistence — TASK-044 cannot proceed without a human contract decision. Assigned sequential ID TASK-045 (next free after TASK-044). |
| 2 | 2026-09-26 | Human decisions A–E received; verification against authoritative docs fired Additional Rules 3/4 → Status `BLOCKED`, file moved to `tasks/blocked/`. Decisions logged in §5A (A3, B3, C2–C4, D1–D3, E1–E3 verified and held); STOP CONDITION REPORT added to §8 with open questions Q1 (four JSON member names + reset tokens undefined), Q2 (`MaxHP`/`ATK`/`DEF`/`EnrageThreshold` missing from the decided model), Q3 (Thủy Ma `threshold` edge). No documentation edited. |
| 3 | 2026-09-26 | Human answered Q1/Q2/Q3 (exact member names + tokens; option b — five columns, stats split documented; `threshold: null` for always-active, no 0-sentinel). Verified — no rule conflict (§4 not triggered; `Persistent` ↔ `NoReset` enum mapping documented as instructed). File → `tasks/active/`, Status `IN PROGRESS`. `DATABASE.md` v1.7: §1 `BossDefinition` block annotation + persistence contract note (items 1–4), §3 four constraints, §5 item 4. §9/§12 ticked, §11 evidence filled. |
