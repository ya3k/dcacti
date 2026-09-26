# TASK-046 — Resolve Canonical Boss Identity Contract

---

## Metadata

```text
Task ID:           TASK-046
Type:              DOCUMENTATION
Status:            DONE
Risk:              MEDIUM
Priority:          HIGH
Primary Agent:     review
Supporting Agents: gameplay, realtime, persistence
Workflow:          documentation/documentation-change.md
Skills:            discovery/documentation-discovery, discovery/impact-analysis,
                   quality/documentation-consistency,
                   realtime/realtime-protocol-validation,
                   backend/api-contract-validation
Dependencies:      TASK-045 (DONE — supplies the `DATABASE.md` §1 BossDefinition
                            persistence contract this task must read, not redo)
Blocks:            TASK-044 (must not be edited by this task — see Scope; its
                   BossDefinition PK/Identity columns cannot be chosen until
                   this contract exists)
Estimate:          Complex (documentation-only; six explicit human decision
                   points)
```

**Status note.** Created `BACKLOG` 2026-09-26 after a read-first audit of
Boss identity across `docs/`. No documentation has been edited. If a
decision below cannot be derived from authoritative documents, the
executing agent reports `BLOCKED` naming the unanswered question
(`tasks/README.md` §10, `.ai/README.md` §13) — it must not choose a value,
a naming convention, or a canonical id for implementation convenience.

**Status note (final).** Executed 2026-09-26: human decisions A–F received
and applied to six documents (§5A); no STOP condition fired; no source,
test, or migration change; TASK-041/043/044/045 untouched. Status → `DONE`;
file moved to `tasks/completed/` per `tasks/TASK_LIFECYCLE.md`.

---

## 1. Objective

Establish, in the authoritative documents only, the **deterministic
distinction between a Boss's stable technical identity and its
human-readable display name** before TASK-044 implements the
`BossDefinition` table. Concretely, this task must make the following six
decisions explicit, evidenced against `docs/`, and answered by a human
(or reported `BLOCKED`):

- **Decision A — `BossDefinition` identity columns:** what
  `DATABASE.md` §1's `BossDefinitionId (PK)` and `Identity` entries hold,
  their values, and their relationship to `BOSS_RULES.md` §6.4's `BossId`.
- **Decision B — event `sourceId` semantics:** what
  `GAME_EVENTS.md` §2 / `SIGNALR_PROTOCOL.md` §3.2.16–§3.2.18 `sourceId`
  means when `source = "boss"`, and how that differs from its `pet` branch.
- **Decision C — canonical technical ids:** the stable technical
  identifier(s) for the three content-defined Bosses, if one exists or is
  to be introduced.
- **Decision D — display-name ownership:** which document owns
  human-readable Boss display names, and what remains a game-design value
  vs a technical value.
- **Decision E — `BossState` identity representation:** what
  `GAME_STATE.md` §2.4's `BossId / Identity` entry denotes, given that
  §2.3 spells the sibling Pet entry out explicitly and disclaims the
  display name there.
- **Decision F — contract surface to synchronize:** the complete set of
  event/API/state/persistence contracts that expose a Boss identity
  (`sourceId`, `bossId`, `BossId`, `Identity`, `BossDefinitionId`) and
  which of them must change when A–E are answered.

Documentation-only task. **No source, test, configuration, migration, or
wire-serialization changes.** No identifier, naming convention
(`boss-hoa-long` / `hoa-long` / `hoa_long` / display name / anything else),
format, or display-name field may be chosen by this task — the answers are
**human decision points**; this task makes them explicit, evidenced, and
answerable (`AGENTS.md` §7, §20). It must not design on the project's
behalf (`AGENTS.md` §23).

---

## 2. Authoritative References

**Read first (required):**

| # | Document | Why |
|---|---|---|
| 1 | `docs/01-game-design/BOSS_RULES.md` §6.4 (Identity Contract: BossId, PassiveId, SkillId), §6 (MVP Boss reference), §7 (Events) | Currently fixes `BossId` as the **display name** (`"Hỏa Long"`, …) "already used by `BossDefinitions.cs` … the boss's identity, not a slug", and states these are "the values emitted on events (`PassiveCharged`/`PassiveTriggered.sourceId`) … fixed here so no task invents its own" — the game-design side of the identity contract |
| 2 | `docs/02-technical/GAME_STATE.md` §2.4 (`BossState` tree: `BossId / Identity`), §2.4.1–§2.4.4, and **§2.3** (the sibling paragraph "`PetId` denotes the owned Pet instance … not a Pet definition id and not the display `Identity` name") | State-side owner of the field; §2.4 has **no** equivalent explanatory paragraph for `BossId`, which is the ambiguity |
| 3 | `docs/02-technical/GAME_EVENTS.md` §2 — `BattleStarted` payload (`BossId`), `PassiveCharged`/`PassiveTriggered` (`Source`, `SourceId (PetId \| BossId)`), `BossSkillCast` (`SourceId (BossId)`) | Event-payload owner of the identity members |
| 4 | `docs/02-technical/SIGNALR_PROTOCOL.md` §3.2.16–§3.2.18 (example JSON + `sourceId` field tables + explanatory notes), §4 (state push members), header revision note | Wire owner of `sourceId`/`bossId` serialization and its stated meaning |
| 5 | `docs/02-technical/DATABASE.md` §1 (`BossDefinition` sketch: `BossDefinitionId (PK)`, `Identity`, …; the TASK-045 "Persistence contract for `BossDefinition`"; the "Identity and reward sourcing for `BattleResult`" note), §2 (`BattleResult N─1 BossDefinition`), §3, §5 | Persistence owner of the two identity entries and of `BattleResult.BossDefinitionId` |
| 6 | `docs/02-technical/API_CONTRACTS.md` §3 (`POST /api/battle/start` `bossId` request member + validation line "bossId must be a valid MVP Boss — BOSS_RULES.md §6") | REST contract the client submits a Boss identity through |
| 7 | `docs/01-game-design/PET_RULES.md` §2 (`Identity (unique name/id, e.g. "Thanh Xà")`) and `docs/00-overview/MVP_SCOPE.md` §1 (Bosses), §4 | Precedent for how a display `Identity` is documented for Pets; scope authority |
| 8 | `docs/03-decisions/ADR/ADR-014-battle-state-player-identity.md` (+ `docs/03-decisions/README.md` index row) | The existing precedent for deciding *what an identity member denotes* — must be checked before proposing any new state member or reusing its reasoning |
| 9 | `docs/02-technical/ARCHITECTURE.md` §1 (`Bosses/` module), §2 (layer direction), §5 item 1 (no plugin/registry system) | Bounds any answer that would move identity ownership between layers; candidate ADR trigger |
| 10 | `docs/00-overview/GDD.md` §12 (Boss System), `docs/01-game-design/GAME_RULES.md` §19 | Only if a display-name/presentation question is raised; both defer to the domain doc |
| 11 | `AGENTS.md` §2 (precedence), §4 (conflict resolution), §7 (no invented rules/content), §10 (server authority), §17 (documentation change rule), §18 (architecture change rule), §20–§22 (stop conditions, output discipline, DoD) | Process contract |
| 12 | `tasks/backlog/TASK-044-implement-bossdefinition-persistence.md`, `tasks/backlog/TASK-041-*`, `tasks/completed/TASK-043-*`, `tasks/completed/TASK-045-*` | Downstream consumers and the immediately preceding contract task; **read-only** for this task |
| 13 | `tasks/README.md` §3, §6, §9–§12; `tasks/TASK_TEMPLATE.md`; `tasks/TASK_TYPES.md` (DOCUMENTATION); `tasks/TASK_LIFECYCLE.md` | Task-creation, skill-budget, and lifecycle rules |
| 14 | `.ai/workflow/documentation/documentation-change.md` §1–§3 (canonical owner, no duplication), `.ai/skills/SKILL_REGISTRY.md` (skill identifiers), `.ai/README.md` §13 (stop-condition report format) | Workflow, skill, and report format |

**Read for evidence only (non-authoritative — code sits below tasks in the
precedence order, `AGENTS.md` §2):**

- `src/backend/GameServer.Domain/Bosses/BossId.cs` (records "There is no id
  format, scheme, or validation rule in any document")
- `src/backend/GameServer.Domain/Bosses/BossDefinitions.cs` (`new BossId("Hỏa Long")`
  / `"Thủy Ma"` / `"Mộc Yêu"`; header prose "the `BossId` is the display name …
  not a slug")
- `src/backend/GameServer.Domain/Bosses/BossDefinition.cs`, `src/backend/GameServer.Domain/Battle/BossState.cs`
- `src/backend/GameServer.Domain/Pets/PetId.cs`, `PetDefinition.cs`,
  `src/backend/GameServer.Infrastructure/Postgres/Configurations/PetDefinitionConfiguration.cs`
  ("Identity — the Pet's display name") — sibling precedent only
- Tests asserting the current value (`PassiveEventSourceTests.cs`,
  `PassiveTrackerTests.cs`, `BattleEventEmissionTests.cs`,
  `RedisBattleStateSmokeTest.cs`), `tasks/completed/TASK-022-implement-boss-response.md`
  ("`sourceId` on Boss events is the display-name BossId … not a slug")

---

## 3. Scope

**In scope**

1. Documentation discovery: establish, with file+section evidence, exactly
   what `docs/` does and does not define about each Boss identity question
   in §4.
2. For each Decisions A–F: state the question precisely, identify the
   document that **should own** the answer (`AGENTS.md` §2, §4;
   `documentation-change.md` §3), lay out clearly separated options where
   the docs underdetermine the answer, and obtain a human answer — or
   produce a `BLOCKED` report naming the unanswered decision.
3. Update the **canonical owning document only** for each approved answer
   (candidate owners in §10), and make dependent references consistent only
   where they become stale (`AGENTS.md` §17, `documentation-change.md` §2).
4. Record precisely what TASK-044 (and, through it, TASK-041) needs
   afterwards as a report item — **without editing those task files**.
5. Report discovered adjacent issues (§6) without fixing them.

**Out of scope**

- Any `src/`, `tests/`, migration, serializer, or wire-format change —
  including "fixing" `BossId.cs`, `BossDefinitions.cs`, the `BossId("Hỏa
  Long")` literals, the example JSON in `SIGNALR_PROTOCOL.md`, or the tests
  that assert them. Code changes follow docs, never precede them.
- Editing `tasks/backlog/TASK-044-*`, `tasks/backlog/TASK-041-*`, or any
  file under `tasks/completed/` (incl. TASK-043, TASK-045).
- **Choosing a naming convention or canonical id.** Candidates such as
  `boss-hoa-long`, `hoa-long`, `hoa_long`, `BossDefinitionId` = display
  name, or "keep the display name as the technical id" may be *listed* as
  unapproved options in §5; this task selects none of them. Selecting one
  requires a human answer.
- Introducing a localization, display-name, i18n, or presentation
  subsystem — **none exists in any document** (grep of `docs/` for
  locali*/i18n/translat* returns only this task's subject matter). Do not
  design one; if an answer seems to require one, stop (§8).
- Inventing Boss content, a fourth Boss, or any identity value
  (`AGENTS.md` §7); changing PassiveId/SkillId values (`BOSS_RULES.md`
  §6.4 stands).
- Boss gameplay/effects, provisioning/seeding (TASK-045 Decision D),
  rewards/XP (TASK-033), `BattleResult` behavior (TASK-041), auth, Redis
  storage strategy (ADR-005).
- Creating or amending an ADR — unless an approved answer requires one, in
  which case **stop** (§8; `AGENTS.md` §18).

---

## 4. Current State — Evidence (do not re-derive; verify then cite)

### 4.1 Gap 1 — `BossDefinition` has two identifier entries and no documented values

| Fact | Location |
|---|---|
| The table sketch lists exactly `BossDefinitionId (PK)`, `Identity`, `Element`, `PassiveDefinition`, `SkillDefinition` | `DATABASE.md` §1 (`BossDefinition`) |
| No value, format, type annotation, or example is given for **either** `BossDefinitionId` or `Identity` | `DATABASE.md` §1 (absence) |
| The sibling Pet sketch annotates its name entry explicitly: `Identity ("Thanh Xà", "Xích Lang", ...)` — a *display name* — and separates it from `PetDefinitionId (PK)` | `DATABASE.md` §1 (`PetDefinition`) |
| The sibling Card/Relic sketches use `Name`, not `Identity` | `DATABASE.md` §1 (`CardDefinition`, `RelicDefinition`) |
| TASK-045's contract note defines the row as carrying "`Identity`, `Element`, and the two JSON objects" and fixes the JSON member lists — it defines **no value** for `Identity` and **nothing** for `BossDefinitionId` | `DATABASE.md` §1 "Persistence contract for `BossDefinition`" items 1–4 |
| `BOSS_RULES.md` §6.4 fixes `BossId` = `"Hỏa Long"` / `"Thủy Ma"` / `"Mộc Yêu"` (display names) and states these are the emitted event values | `BOSS_RULES.md` §6.4 |
| No document states whether `BossDefinitionId` **is** §6.4's `BossId`, whether `Identity` is, whether the two differ, or what value a row's PK takes | `docs/` (absence) |

**Consequence:** TASK-044 must create a primary key and an `Identity`
column with no documented value for either, and TASK-041 must write
`BattleResult.BossDefinitionId` with no documented derivation.

### 4.2 Gap 2 — `GAME_STATE.md` §2.4 does not say what `BossId / Identity` denotes

| Fact | Location |
|---|---|
| The tree entry is `BossId / Identity` — two names, no explanation | `GAME_STATE.md` §2.4 |
| The sibling §2.3 entry `PetId / Identity` **is** explained in a dedicated paragraph: "`PetId` denotes the owned Pet instance … the same value as `Pet.PetInstanceId` … It is **not** a Pet definition id and **not** the display `Identity` name" | `GAME_STATE.md` §2.3 |
| No corresponding paragraph exists anywhere under §2.4 (§2.4.1–§2.4.5 cover staged fields, Passive, Skill, Enrage, Stunned only) | `GAME_STATE.md` §2.4–§2.4.5 (absence) |
| `BossState` round-trips as part of `BattleState` (so the value is serialized into the active-state store) | `GAME_STATE.md` §2, `REDIS_STATE.md` §2, ADR-005 |
| `BattleStarted` payload carries `BossId` | `GAME_EVENTS.md` §2 |

**Consequence:** a reader cannot tell whether `BossState.BossId` holds
§6.4's display name, `BossDefinitionId`, `Identity`, or something else —
and the two documented sibling states (`PetState`, `BossState`) therefore
have *unequal* documented precision.

### 4.3 Gap 3 — `sourceId` means two different kinds of identity depending on `source`

| Fact | Location |
|---|---|
| Payload: `SourceId (PetId \| BossId)` for both `PassiveCharged` and `PassiveTriggered` | `GAME_EVENTS.md` §2 |
| "`Source` field is `"pet"` or `"boss"`, and `SourceId` carries the corresponding identity (`PetState.PetId` or `BossState.BossId`)" | `GAME_EVENTS.md` §2 item 2 |
| Field table: `sourceId` = "the identity of the owning entity (`PetState.PetId` or `BossState.BossId`)" | `SIGNALR_PROTOCOL.md` §3.2.16 |
| "For a Pet Passive, it is `PetState.PetId`; for a Boss Passive, it is `BossState.BossId` — the Boss's display-name BossId (e.g. `"Hỏa Long"`), per `BOSS_RULES.md` §6.4." | `SIGNALR_PROTOCOL.md` §3.2.16 note 2 |
| `PetState.PetId` is an **instance** identity (`Pet.PetInstanceId`, `BattleResult.PetInstanceId`), explicitly not a definition id and not a display name | `GAME_STATE.md` §2.3; ADR-014; `DATABASE.md` §1 §2 |
| `BossState.BossId` is by construction a **definition** identity — a battle has exactly one Boss and there is no boss instance record anywhere in `DATABASE.md` §1/§2 | `BOSS_RULES.md` §1; `DATABASE.md` §1/§2 |
| Example values: `"sourceId": "Hỏa Long"` in §3.2.16, §3.2.17, §3.2.18 examples | `SIGNALR_PROTOCOL.md` §3.2.16–§3.2.18 |
| No document states whether `sourceId` is stable across battles, usable as a key, safe to compare against `BossDefinitionId`, or display-presentation data | `docs/` (absence) |

**Consequence:** `sourceId` is documented as one field with one meaning
("identity of the owning entity") whose two branches are different
identity *kinds* (instance id vs definition identity), and the Boss branch
is additionally a human-readable name. Whether that is intended, and what
a client may rely on, is undefined.

### 4.4 Gap 4 — no technical Boss id format exists anywhere (and code says so)

| Fact | Location |
|---|---|
| Validation line is value-semantic only: "bossId must be a valid MVP Boss — BOSS_RULES.md §6"; the request member is `"bossId": "string"` | `API_CONTRACTS.md` §3 |
| `PassiveId`/`SkillId` **do** have documented kebab-case/slug values; `BossId` is explicitly "the display name … not a slug" | `BOSS_RULES.md` §6.4 |
| Domain wrapper documents the absence outright: "There is no id format, scheme, or validation rule in any document … this type imposes no format and holds the identifier verbatim" | `BossId.cs` (code — non-authoritative, but states the gap) |
| In-code values are the three display names; header prose repeats "the display name … not a slug" | `BossDefinitions.cs` (code — non-authoritative) |
| Tests and a completed task assert the display-name value today | `PassiveEventSourceTests.cs`, `PassiveTrackerTests.cs`, `BattleEventEmissionTests.cs`, `RedisBattleStateSmokeTest.cs`; `tasks/completed/TASK-022-*` (code/tasks — non-authoritative) |

**Consequence:** if a stable technical id is required, it does not yet
exist in any document; authoring it is a human content/contract decision
(`AGENTS.md` §7), not an implementation detail. Candidates may be listed
but not chosen (§5 Decision C).

### 4.5 Gap 5 — display-name ownership is split across layers

| Fact | Location |
|---|---|
| A **game-design** document fixes the wire values of `BossId` and states they are emitted on events | `BOSS_RULES.md` §6.4 |
| Technical documents **reference** those values (`sourceId`, examples, field tables) but define no display-name field of their own | `GAME_EVENTS.md` §2; `SIGNALR_PROTOCOL.md` §3.2.16–§3.2.18 |
| Persistence has an `Identity` column with no stated content; the Pet analogue's `Identity` is documented as the display name | `DATABASE.md` §1 (`BossDefinition`, `PetDefinition`) |
| Pet rules document `Identity` as "unique name/id, e.g. `"Thanh Xà"`" — name/id conflated by phrasing, on the definition side only | `PET_RULES.md` §2 |
| For Pets, the display name is definition-side and explicitly excluded from battle state; for Bosses, the display name *is* the battle-state identity | `GAME_STATE.md` §2.3 vs §2.4 |
| No localization/display-name system exists in any document | `docs/` (absence) |

**Consequence:** it is undecided whether Boss display names are
game-design content (`BOSS_RULES.md`), technical state
(`GAME_STATE.md`/`GAME_EVENTS.md`), persistence data (`DATABASE.md`), or
some documented combination — and therefore which single document any
correction must be written to (`documentation-change.md` §2–§3).

### 4.6 Gap 6 — `BattleResult.BossDefinitionId` has no documented derivation

| Fact | Location |
|---|---|
| `BattleResult` carries `BossDefinitionId (FK → BossDefinition)`; relationship recorded as `BattleResult N─1 BossDefinition` | `DATABASE.md` §1, §2 |
| The identity-sourcing note lists `PlayerId` ← `BattleState.PlayerId` and `PetInstanceId` ← `BattleState.PetState.PetId`, and is silent on `BossDefinitionId` | `DATABASE.md` §1 "Identity and reward sourcing for `BattleResult`" item 2 |
| `GAME_STATE.md` §2.8 owns battle-end identity; it does not name a Boss-derived persisted identity (verify during execution) | `GAME_STATE.md` §2.8 |
| `BattleResult` writing is TASK-041, which depends on TASK-044 | `tasks/backlog/TASK-041-*`, TASK-044 Objective |

**Consequence:** at battle end the server must map a battle-state Boss
identity onto a persistence key; no document says which value that is.
This is the concrete join between Decisions A/B/E and TASK-041.

---

## 5. Decision Points (the deliverable)

Each decision: questions to answer explicitly, then options to evaluate
against the cited criteria. **Do not decide unilaterally.** If an answer
cannot be derived from authoritative documents, report `BLOCKED` naming
the exact question for the human. Never select an option — especially a
naming convention — for implementation convenience.

### Decision A — `BossDefinition.BossDefinitionId` vs `Identity`

**Questions:**

- **A1.** Does `BossDefinitionId (PK)` hold a stable technical id, the
  display name, or is it a surrogate key whose value no document defines?
- **A2.** What does `Identity` hold, and is it the same value as
  `BossDefinitionId`, or a distinct display/label field (as `PetDefinition`
  documents: PK + `Identity ("Thanh Xà", …)`)?
- **A3.** What is the relationship between these entries and
  `BOSS_RULES.md` §6.4's `BossId` — is `BossId` = `BossDefinitionId`, =
  `Identity`, or neither?
- **A4.** Does the answer require any `DATABASE.md` §3 constraint or §5
  statement (uniqueness, NOT NULL, value form)? Note §5 item 1 makes SQL
  *types* an implementation detail — it does not make *value form* one.

**Options (evaluate; choose one only with human approval, or report blocked):**

- **A-i — PK and `Identity` are the same documented value** (both = §6.4
  `BossId`). *Must check:* why two entries then exist; the
  `PetDefinition` precedent that separates them; anti-duplication
  (`AGENTS.md` §9).
- **A-ii — PK is a stable technical id, `Identity` is the display name**
  (mirrors `PetDefinition`). *Must check:* the technical id's value does
  not exist yet → Decision C; `BattleResult.BossDefinitionId` then stores
  the technical id (Decision F).
- **A-iii — PK is a stable technical id and `Identity` is redundant /
  should be removed.** *Must check:* §1 is owned by `DATABASE.md` — a
  column removal is a documented-schema change (`AGENTS.md` §4); name it,
  do not perform it silently.
- **A-iv — value form is an implementation detail.** *Must check:* §5 item
  1 covers SQL types only; `API_CONTRACTS.md` §3 and `GAME_EVENTS.md` §2
  consume the *value*, so the form is contract-visible.
- **A-v — not derivable from `docs/`** → `BLOCKED`, human contract
  decision required (the expected outcome unless the human supplies the
  basis).

### Decision B — `GAME_EVENTS.md` / `SIGNALR_PROTOCOL.md` `sourceId` semantics (`source = "boss"`)

**Questions:**

- **B1.** Is `sourceId` "the identity of the owning entity" in one uniform
  sense, or does it legitimately carry an instance id for `pet` and a
  definition identity for `boss`? If the latter, is that asymmetry
  documented anywhere?
- **B2.** When `source = "boss"`, what exact string is serialized, and is
  it presentation data (a name a client may render) or an opaque key (a
  value a client must not render/derive from)?
- **B3.** May a client compare `sourceId` to `BossDefinitionId`
  (`DATABASE.md` §1), to `POST /api/battle/start`'s `bossId`
  (`API_CONTRACTS.md` §3), or to `BattleStarted.BossId` (`GAME_EVENTS.md`
  §2)? State which of these are the *same* value.
- **B4.** Is `sourceId` required to be stable across battles/restarts, and
  is that requirement stated where a client can rely on it?

**Options:**

- **B-i — `sourceId` is a display/rendered value by design** (current
  §6.4 wording + §3.2.16 note). *Must check:* then it is presentation data
  on an authoritative event stream; `GAME_EVENTS.md` should say so
  explicitly, and the client-side rendering owner must be named
  (`GAME_EVENTS.md` §2, `SIGNALR_PROTOCOL.md` §3.2.16).
- **B-ii — `sourceId` is a stable technical key** (then §6.4's
  display-name statement and every example become stale and must be
  corrected by their owners). *Must check:* this **presupposes Decision C**
  — the key must exist first; otherwise `BLOCKED`.
- **B-iii — split the members** (e.g. technical id + separate display
  field). *Must check:* adding a wire member is a protocol change
  (`SIGNALR_PROTOCOL.md` §3, ADR-004/ADR-008), and `GAME_EVENTS.md` §2 /
  `SIGNALR_PROTOCOL.md` field tables are the owners; do not treat this as
  a formatting edit — if it needs an architectural decision, stop (§8).
- **B-iv — document the asymmetry as-is** (instance id for pet, definition
  identity for boss) with an explicit statement of what a client may rely
  on. *Must check:* whether that satisfies the "deterministic distinction"
  objective or merely restates the gap.
- **B-v — not derivable** → `BLOCKED`, human decision required.

### Decision C — canonical technical ids for the three content-defined Bosses

**Questions:**

- **C1.** Does a stable technical Boss id **exist** in authoritative
  documentation? (§4.4: none found; `BossId.cs` states the absence.)
- **C2.** If none exists, does one get introduced, and if so what are the
  exact values for Hỏa Long, Thủy Ma, and Mộc Yêu? **This is a human
  content/contract decision** (`AGENTS.md` §7) — the agent lists candidates
  and stops.
- **C3.** If none is introduced, is the display name *affirmed* as the
  stable identity, with the consequences (rename invalidates stored FKs /
  serialized state; a name is not a key) explicitly accepted and recorded?
- **C4.** Does the answer apply to `BossState`, events, REST, persistence,
  or all four? (Consistency across the Decision F surface is mandatory —
  a mixed answer is not contract, it is the current gap.)

**Options (candidates only — none approved; do not select):**

- **C-i — kebab-case slug patterned on PassiveId** (e.g. `boss-hoa-long`,
  `hoa-long`). *Must check:* `BOSS_RULES.md` §6.4 currently forbids this
  reading ("not a slug") — §6.4 would need its owner to amend it.
- **C-ii — snake_case or another scheme.** *Must check:* no scheme is
  documented anywhere; adopting one invents a convention (`AGENTS.md` §7).
- **C-iii — reuse `BossDefinitionId` as a natural key equal to the display
  name** (no new value). *Must check:* Decisions A1/A3; renames/Unicode
  normalization of `"Hỏa Long"` become data migrations.
- **C-iv — `BLOCKED`: human supplies the three values** (expected outcome
  if C1 is "none exists").

**Never** resolve this by choosing a spelling for convenience.

### Decision D — display-name ownership

**Questions:**

- **D1.** Which document owns human-readable Boss display names —
  `BOSS_RULES.md` §6.4 (where they live today, alongside the technical
  ids), a technical doc, or `DATABASE.md` §1's `Identity` column?
- **D2.** What is the boundary: which documents may *reference* a display
  name, and which may *define* one (`documentation-change.md` §2–§3)?
- **D3.** Is a display name ever carried on an authoritative event or
  state member, and if so is that member's ownership stated in the same
  document that defines the name?
- **D4.** Does `PET_RULES.md` §2 / `DATABASE.md` §1 `PetDefinition.Identity`
  need a corresponding clarification so Pet and Boss display-name ownership
  reads consistently — or is the Pet wording already sufficient?

**Options:**

- **D-i — `BOSS_RULES.md` §6.4 owns both the technical ids and the display
  names** (current de-facto arrangement). *Must check:* §6.4 is
  game-design; technical docs would continue to *reference* it, which is
  allowed, but the cross-reference must be explicit and non-duplicative.
- **D-ii — split: game design owns names, a technical doc owns ids.**
  *Must check:* two documents then each own half of one identity contract;
  the boundary sentence must be written in both, without restating values
  (§2 of the workflow).
- **D-iii — not derivable** → `BLOCKED`, human decision required.

### Decision E — `GAME_STATE.md` §2.4 `BossState` identity representation

**Questions:**

- **E1.** What does `BossId / Identity` denote: the §6.4 `BossId`, the
  `BossDefinitionId`, the `Identity` column, or one value that is all
  three? State it in the same explicit way §2.3 states `PetId`.
- **E2.** Is `BossState.BossId` definition-side (never changes within a
  battle, no boss instance exists) — and is that stated?
- **E3.** Does the answer require a **new state member** (e.g. a separate
  display name alongside a technical id)? If yes, that is a state-model
  change: check ADR-014's reasoning first, then `ARCHITECTURE.md` §2 and
  `AGENTS.md` §18 — propose an ADR, do not add a member here.
- **E4.** Does the value stored in serialized `BattleState`
  (`REDIS_STATE.md` §2) change meaning? If so, state the impact for any
  stored/persisted state and for `BattleResult` identity sourcing
  (`DATABASE.md` §1) — as an impact statement, not a migration.

**Options:**

- **E-i — one member, value = the decided identity** (whatever Decision A/C
  resolve it to), with a §2.4 paragraph mirroring §2.3's precision.
- **E-ii — one member, value = display name, affirmed and documented as
  such.** *Must check:* then §2.4 must say plainly that it is a display
  name, and the consequences of C3 apply to persisted/serialized state.
- **E-iii — two members (technical id + display name).** *Must check:*
  state-model change → ADR path (E3); stop if architectural.
- **E-iv — not derivable** → `BLOCKED`, human decision required.

### Decision F — contract surface to synchronize

**Questions:**

- **F1.** Confirm the complete inventory of members/consumers that carry a
  Boss identity, verifying each during execution rather than assuming:
  `GAME_EVENTS.md` §2 (`BattleStarted.BossId`,
  `PassiveCharged`/`PassiveTriggered.SourceId`, `BossSkillCast.SourceId`);
  `SIGNALR_PROTOCOL.md` §3.2.16–§3.2.18 (examples + field tables + notes),
  §3.2.19 and §4 (verify whether battle-end / state-push payloads carry a
  Boss identity); `API_CONTRACTS.md` §3 (`bossId`) and §4 (verify);
  `GAME_STATE.md` §2.4; `DATABASE.md` §1/§2 (`BossDefinitionId`,
  `Identity`, `BattleResult.BossDefinitionId`).
- **F2.** For each inventoried member, is a change required by A–E, or is
  a cross-reference/staleness fix enough?
- **F3.** Which single document owns each change (per §10), and did any
  dependent reference become stale (section numbers moved, wording now
  wrong) (`AGENTS.md` §17)?

**Options:** F is satisfied by producing the verified inventory with a
per-member verdict (change / reference-only / unaffected). If the
inventory reveals a member whose ownership cannot be attributed to any
document → `BLOCKED` (structural ambiguity, `documentation-change.md` §3).

---

## 5A. Decisions Received — 2026-09-26 (all six answered; written to docs)

The human answered all six decision points (A–F). Each answer was checked
against authoritative documents before writing; no STOP condition fired
(`AGENTS.md` §4 not triggered; no ADR mentions Boss identity, so Decision E
needed no ADR). The contract is now written to six documents (§10 owner
map, versions bumped per document).

| Decision | Answer received | Where written |
|---|---|---|
| A | `BossDefinitionId` = persistence PK (stability/technical identity of the persisted row); `Identity` = canonical game-level Boss technical ID; `DisplayName` = human-readable content — all three distinct; no new DB column unless an existing contract requires it | `DATABASE.md` v1.8 §1 (block annotations on `BossDefinitionId (PK)`/`Identity`, contract note item 1) |
| B | `sourceId`: `source = "pet"` → `PetState.PetId` (Pet instance identity, unchanged); `source = "boss"` → canonical Boss technical Identity; machine-readable only, never a display name | `GAME_EVENTS.md` v2.3 §2 (Passive note 2, `BossSkillCast`, `BattleStarted`); `SIGNALR_PROTOCOL.md` v2.4 §3.2.16–§3.2.18 (examples, tables, notes) |
| C | Hỏa Long → `"boss-hoa-long"`; convention `boss-<ascii-kebab-case-name>` (ASCII, lowercase, kebab, stable, no diacritics, no runtime slugification); **Thủy Ma / Mộc Yêu NOT assigned** — no document defines their technical IDs, so they are recorded `UNRESOLVED`, not invented (display names not used as fallback) | `BOSS_RULES.md` v2.3 §6.4 (convention + three-way table + UNRESOLVED note) |
| D | Display names are presentation/content (reference tables only); technical Identity used in `sourceId`, `BossState`, `BossDefinition.Identity`, machine refs; no `DisplayName` member in BattleState; no localization infrastructure; misnamed `BossId` fields corrected by semantics, not aliased | `BOSS_RULES.md` §6.4 (display-name bullet); `GAME_STATE.md` v2.7 §2.4; `API_CONTRACTS.md` v1.6 §3 (`bossId` value form); `DATABASE.md` §1 ("no display-name column") |
| E | The existing `BossState.BossId / Identity` field = the canonical Identity — clarify its denotation (§2.3 `PetId` paragraph as wording model); no second identity field, no `displayName` member; minimal ADR only if an existing ADR conflicts — **verified: no ADR mentions BossId/Boss identity/display name ⇒ no ADR required** | `GAME_STATE.md` v2.7 §2.4 (new denotation paragraph after the tree) |
| F | Full inventory verified during execution; every occurrence classified exactly one of persistence identity / canonical technical Boss Identity / Pet instance identity / display name / other documented identifier; no ambiguous `BossId`/`Identity`/`sourceId` remains | per-member verdicts in §11 `Contract Surface`; owner rows per §10 |

**Additional Rules noted:** (1) only documentation and this task's
completion evidence may change — verified (six `docs/` files + this file
only). (2) TASK-044 not implemented — verified (no source/test/migration
changes). (3) TASK-041/043/045 unmodified — verified (`git status`, file
contents untouched). (4) `sourceId` must be machine-readable everywhere —
verified (examples now `boss-hoa-long`). (5) `BossState` must not gain a
second identity/display field — verified (single tree entry, no new
member).

---

## 6. Discovered Adjacent Issues — report only, do not fix

1. **`sourceId` asymmetry is repo-wide, not Boss-only.** The pet branch
   carries an instance id, the boss branch a definition identity; any
   future `source = "relic"`/`"card"` emission inherits the question.
   Suggested follow-up: one event-identity contract task if the human's B
   answer generalizes beyond Boss.
2. **Display-name-as-key risk for stored data.** If C-iii is chosen,
   `BattleResult.BossDefinitionId` FKs and serialized `BossState` values
   change whenever a Boss name changes. Report the risk; do not design a
   migration or a rename mechanism here.
3. **Sibling inconsistency between Pets and Bosses.** `PetState.PetId` is
   an instance id and explicitly *not* the display name; `BossState.BossId`
   may be the display name. If the human's answer keeps them asymmetric,
   say so explicitly in both §2.3 and §2.4 so the asymmetry reads as
   intentional, not as a gap.
4. **`PET_RULES.md` §2 "unique name/id" phrasing** conflates name and id
   for Pets (definition side). Out of scope here unless Decision D
   requires it; suggested follow-up: a pet-identity wording clarification
   task if the human's D answer touches Pets.
5. **Stale-by-design references if C-ii/C-iii land.** `BOSS_RULES.md`
   §6.4, `SIGNALR_PROTOCOL.md` examples, `BossDefinitions.cs` literals,
   `BossId.cs`/`BossDefinition.cs` XML prose, and four test files all
   encode the display-name reading. List them in the completion report as
   the change set a *later* task would execute — this task edits
   documentation only, and must not start that code work.

---

## 7. Preserved Decisions (must remain true after this task)

1. **No identifier, convention, or display-name field is invented here.**
   Until a human answers §5, Boss identity remains *documented as-is*, not
   "improved" (`AGENTS.md` §7, §20, §23).
2. **`BOSS_RULES.md` §6.4's PassiveId/SkillId values are unchanged**; no
   Boss content is authored or renamed beyond what the human explicitly
   approves (`AGENTS.md` §7).
3. **No gameplay, Boss behavior, effects, rewards, provisioning, or
   seeding** is specified, changed, or implied (TASK-045 Decision D
   remains authoritative for provisioning).
4. **TASK-044, TASK-041, TASK-043, TASK-045 and all `tasks/completed/`
   files are untouched**; what TASK-044 needs is reported, not edited
   (`AGENTS.md` §16).
5. **Server authority unaffected** — identity carriage never becomes
   client-authoritative; the client still only *requests* (`AGENTS.md`
   §10, `GAME_RULES.md` §18, ADR-001).
6. **No speculative abstraction** — no registry, resolver, id service, or
   presentation layer is endorsed (`ARCHITECTURE.md` §5 item 1,
   `AGENTS.md` §9).
7. **Scope stays IN** — Bosses and persistent storage are `MVP_SCOPE.md`
   §1 items; nothing OUT/FUTURE (incl. localization) is introduced
   (`MVP_SCOPE.md` §2–§4).
8. **Code follows docs.** Any code/test/example change implied by an
   approved answer is a separate, later task (`AGENTS.md` §17); this task
   records the requirement only.

---

## 8. Stop Conditions (report `BLOCKED`, do not guess)

- The technical-id question (Decision C) cannot be answered from
  authoritative documentation and the human has not supplied values —
  **then no convention may be selected** (`AGENTS.md` §7).
- `BossDefinition.Identity` semantics (A) or `BossState.BossId` semantics
  (E) remain unresolved at review time.
- The `sourceId` contract (B) remains unresolved, or resolving it requires
  adding/changing a wire member without an architectural check
  (`SIGNALR_PROTOCOL.md`, ADR-004/ADR-008, `AGENTS.md` §18) → propose an
  ADR first; stop this task.
- Display-name ownership (D) cannot be attributed to a single canonical
  owner, or two authoritative documents disagree about it
  (`AGENTS.md` §4) — report both sources; do not pick the convenient one.
- The Decision F inventory cannot be completed, or a member's owning
  document is unknown (`documentation-change.md` §3 structural ambiguity).
- Resolving any decision would introduce a **new architecture/state-model
  decision** (new state member, new identity service, changed
  authoritative model) → ADR first (`AGENTS.md` §18); stop this task.
- An answer requires inventing localization/presentation infrastructure or
  any system `MVP_SCOPE.md` §2 excludes → stop and report (`AGENTS.md` §8).
- Execution would require editing `TASK-044`, `TASK-041`, any
  `tasks/completed/` file, or any `src/`/`tests/` file (`AGENTS.md` §16).
- A decision contradicts another authoritative document (`AGENTS.md` §4) —
  report per §4's procedure; do not silently resolve.
- Skill budget (7) exceeded → stop and decompose (`tasks/TASK_TEMPLATE.md`,
  `tasks/README.md` §12).
- Universal stop conditions in `AGENTS.md` §20 and `.ai/README.md` §13
  always apply; write the report in `.ai/README.md` §13's exact format and
  set `Status: BLOCKED`, moving the file to `tasks/blocked/`
  (`tasks/README.md` §10).

---

## 9. Acceptance Criteria

- [x] **A resolved:** the semantics and values of `BossDefinitionId (PK)`
      and `Identity`, and their relationship to `BOSS_RULES.md` §6.4
      `BossId`, are documented in the owning document with file/section
      citations — **or** the task is `BLOCKED` naming A1–A4.
- [x] **B resolved:** what `sourceId` means when `source = "boss"`, what a
      client may rely on, and which identity-bearing members are the same
      value — documented — **or** `BLOCKED` naming B1–B4.
- [x] **C resolved:** canonical technical id(s) for the three
      content-defined Bosses are stated by the human, **or** an explicit
      human affirmation that the display name is the identity with its
      consequences recorded — **or** `BLOCKED` naming C1–C4. No convention
      selected by the agent.
- [x] **D resolved:** display-name ownership is attributed to one
      canonical document, with the reference/define boundary stated —
      **or** `BLOCKED` naming D1–D4.
- [x] **E resolved:** `GAME_STATE.md` §2.4 states precisely what
      `BossId / Identity` denotes, in the same explicitness as §2.3's
      `PetId` paragraph — **or** `BLOCKED` naming E1–E4 (a new state member
      ⇒ ADR path, stop).
- [x] **F resolved:** a verified inventory of every contract member
      carrying a Boss identity exists, each marked change /
      reference-only / unaffected — **or** `BLOCKED` naming the
      unattributable member.
- [x] Every approved answer is written to the **canonical owner only**
      (`AGENTS.md` §2; `documentation-change.md` §2–§3); dependent
      references are touched only where stale; no identity value or rule is
      duplicated across documents (`tasks/README.md` §9).
- [x] No identifier, naming convention, display-name field, or example
      value appears in the recorded output unless a human chose it in
      answer to §5.
- [x] **No source, test, migration, serializer, or task-file change**;
      all edits are documentation edits (plus the report for TASK-044).
- [x] **TASK-044, TASK-041, TASK-043, TASK-045 are not modified**; the
      downstream requirements are delivered in the completion report.
- [x] §6 issues are reported, not fixed; §7 preserved decisions still hold.
- [x] Completion report delivered in the §11 format; any unanswered
      decision ⇒ status `BLOCKED` naming it.
- [x] All relevant tests pass at the required validation depth
      (`core/validation.md` §2) — **N/A: documentation-only** (verification
      is the consistency checks below).
- [x] Quality review checklist passes (`quality/review.md` §1
      documentation subset).
- [x] No authoritative rules or contracts violated (`AGENTS.md` §10 /
      ADR-001); no client-authoritative identity introduced.

---

## Affected Files & Areas

```text
[ ] src/backend/ (Domain / Application / Infrastructure / Api)   — NOT EDITABLE
[ ] src/frontend/client/ (scenes / runtime / services / state / ui) — NOT EDITABLE
[ ] tests/ (unit / integration / gameplay scenarios)             — NOT EDITABLE
[x] docs/ — the only area this task may edit, and only the §10 owner
    documents + stale cross-references
[x] tasks/ — this manifest only; TASK-044 / TASK-041 / TASK-043 /
    TASK-045 are read-only inputs
```

---

## Implementation Notes

- `§4` evidence tables were pre-verified during task authoring (2026-09-26);
  the executing agent must **re-verify each citation before relying on it**
  (section numbers may have moved) rather than re-deriving from scratch.
- `§10` is the owner map — write an approved answer only to its canonical
  owner; fix a dependent reference only where it became stale
  (`documentation-change.md` §2–§3).
- Precedent for precision: `GAME_STATE.md` §2.3's `PetId` paragraph is the
  wording model Decision E must match in §2.4.
- TASK-045's contract note (`DATABASE.md` §1) defines the BossDefinition
  row **shape** (Identity/Element/JSON objects), not identity **values** —
  do not restate or extend it; Decision A owns values.
- Downstream handoff: §11's `Dependency Graph` entry is the only channel for
  telling TASK-044 what it may now implement — task files themselves are
  immutable here (`AGENTS.md` §16).
- Report format for any `BLOCKED` outcome: `.ai/README.md` §13 exact
  format, then move this file to `tasks/blocked/` (`tasks/README.md` §10).
- Skill budget: 5 of 7 (Complex allows 5–7); decompose if a sixth decision
  point appears (`tasks/README.md` §12).

---

## Testing Requirements

### Required Verification
```text
[ ] Unit tests         — N/A (documentation-only task)
[ ] Integration tests  — N/A (documentation-only task)
[ ] Gameplay scenarios — N/A (no rule behavior changes; §7 preserved decisions)
[ ] Documentation consistency verification — quality/review.md §1
    documentation subset: every edited section cross-checked against its
    dependents; no value/rule duplicated between documents (AGENTS.md §17,
    documentation-change.md §2)
[ ] Stale-reference sweep — Decision F inventory re-checked after edits
    (GAME_EVENTS §2, SIGNALR §3.2.16–§3.2.18 + §4, API_CONTRACTS §3,
    DATABASE §1/§2, GAME_STATE §2.4)
```

### Key Edge Cases
- See `docs/01-game-design/BOSS_RULES.md` §6.4 — the "display name, not a
  slug" statement must survive every decision unless **its owner** amends it.
- See `docs/02-technical/DATABASE.md` §1 `BattleResult` identity-sourcing
  note — `BossDefinitionId` derivation must be stated if Decision A lands
  (Gap 6, §4.6).
- See `docs/03-decisions/ADR/ADR-014` — any proposed new state member
  (E-iii) must be checked against its identity reasoning first.

---

## 10. Expected Documentation Changes (only after a decision is approved)

| Decision | Canonical owner (edit here) | Dependent references (fix only if stale) |
|---|---|---|
| A | `docs/02-technical/DATABASE.md` §1 (`BossDefinition` — the two identity entries + contract note) | `GAME_STATE.md` §2.4 only if it must name which column it reads; `API_CONTRACTS.md` §3 only if the request member's value form is stated there |
| B | `docs/02-technical/GAME_EVENTS.md` §2 (payload semantics) and/or `docs/02-technical/SIGNALR_PROTOCOL.md` §3.2.16–§3.2.18 (wire meaning) — **owner chosen by the human per each doc's stated purpose**; if both must carry wording, they must cross-reference, not restate (`documentation-change.md` §2) | `BOSS_RULES.md` §6.4 only if its "emitted on events" sentence becomes wrong |
| C | `docs/01-game-design/BOSS_RULES.md` §6.4 (the identity contract fixes values — its owner today) | Every §F member that serializes the value: `GAME_EVENTS.md` §2, `SIGNALR_PROTOCOL.md` §3.2.16–§3.2.18, `API_CONTRACTS.md` §3, `DATABASE.md` §1 — as *references*, updated only where the recorded value makes them stale |
| D | Determined by the human (candidates: `BOSS_RULES.md` §6.4 for names; `GAME_STATE.md`/`GAME_EVENTS.md` for state/event members; `DATABASE.md` §1 for the `Identity` column) | `PET_RULES.md` §2 / `DATABASE.md` §1 `PetDefinition` only if D4 requires it |
| E | `docs/02-technical/GAME_STATE.md` §2.4 (state owner) — a §2.4 paragraph mirroring §2.3 | `REDIS_STATE.md` §2 only if serialized-value meaning must be stated there; a **new member** ⇒ ADR first (§8) |
| F | none — F produces the inventory; each real edit lands in the owner row above | all inventoried members |

If a decision's answer requires editing a document **not** listed as its
owner, stop and re-check §8 (`AGENTS.md` §4).

---

## 11. Completion Evidence — required final report format

```text
Task Created          TASK-046 tasks/backlog/TASK-046-resolve-canonical-boss-
                      identity-contract.md — created in backlog/, executed the
                      same day, Status DONE, moved to tasks/completed/
Exact Ambiguity       A (BossDefinitionId/Identity/DisplayName never defined or
                      distinguished), B (sourceId = PetId instance but
                      display-name BossId), C (no technical Boss id format
                      exists anywhere), D (display-name ownership split across
                      layers), E (GAME_STATE §2.4 silent on BossId, unlike §2.3),
                      F (BattleResult.BossDefinitionId has no documented
                      derivation) — file+section evidence each (§4)
Authoritative Evidence   BOSS_RULES.md §6/§6.4; DATABASE.md §1/§2/§3/§5;
                      GAME_EVENTS.md §2 (BattleStarted, Passive*, BossSkillCast,
                      BattleWon/Lost); GAME_STATE.md §2.3/§2.4/§2.8;
                      SIGNALR_PROTOCOL.md §3.2.16–§3.2.19, §4; API_CONTRACTS.md
                      §3/§4; ADR-014 + docs/03-decisions grep (no ADR mentions
                      Boss identity ⇒ E needs no ADR); AGENTS.md §2/§4/§7/§9/
                      §17/§18/§20; MVP_SCOPE.md §1
Decisions Required    A–F all answered by the human, zero agent-chosen values
                      (answers + verification: §5A); no STOP fired
Documentation Ownership   C → BOSS_RULES.md v2.3 §6.4 (values + convention +
                      three-way distinction — canonical owner); A → DATABASE.md
                      v1.8 §1 (block annotations, contract note item 1) + §3
                      (Identity NOT NULL/UNIQUE) + §1 sourcing item 2 (FK
                      derivation); B → GAME_EVENTS.md v2.3 §2 (semantics) and
                      SIGNALR_PROTOCOL.md v2.4 §3.2.16–§3.2.18 (wire examples/
                      tables/notes) cross-referenced per documentation-change
                      §2; D → display-name rule in BOSS_RULES §6.4, referenced
                      by GAME_STATE/API_CONTRACTS/DATABASE; E → GAME_STATE.md
                      v2.7 §2.4 denotation paragraph; stale references fixed:
                      SIGNALR prior-2.1 display-name correction superseded by
                      version note (kept as history), GAME_EVENTS "emitted on
                      events" sentence kept correct, GAME_STATE §2.4 "records
                      for BossId" cross-references now backed by the paragraph
Contract Surface      (F inventory, each classified exactly one way) —
                      CHANGE: BOSS_RULES §6.4; DATABASE §1 BossDefinition block,
                      §1 contract note item 1, §1 BattleResult FK + sourcing
                      item 2, §3 constraints; GAME_EVENTS §2 BattleStarted /
                      Passive note 2 / BossSkillCast; GAME_STATE §2.4 paragraph
                      + version header; SIGNALR §3.2.16 example+table+note 2,
                      §3.2.17 example+table, §3.2.18 example+table+note 2,
                      version header; API_CONTRACTS §3 example + validation line
                      + version header. REFERENCE-ONLY (verified, no stale): §2.4
                      tree entry `BossId / Identity` (single field kept — E),
                      staged list; GAME_STATE §2.3/§2.7 cross-refs 977/996/1039;
                      RELIC_RULES §2.2 note (identity-not-definition holds);
                      GAME_EVENTS §1 wire-schema split. UNAFFECTED (verified
                      absent/consistent): SIGNALR §3.2.19 + §4 (no Boss identity
                      member); API_CONTRACTS §4 (no boss member); REDIS_STATE;
                      ADRs; BOSS_RULES §6 tables (display-name row labels are
                      content); ROADMAP §content list; BattleWon/BattleLost
                      payload (no Boss identity member by design)
Dependency Graph       TASK-046 → TASK-044 → TASK-041; TASK-043/045 DONE and
                      unmodified. TASK-044 may now implement the BossDefinition
                      table (PK/Identity columns per DATABASE.md §1, mapping,
                      migration) — it may NOT provision rows (TASK-045 D3),
                      may NOT invent Thủy Ma/Mộc Yêu technical IDs (BOSS_RULES
                      §6.4 UNRESOLVED), and the PK's SQL value form remains an
                      implementation detail per DATABASE.md §5 item 1.
                      TASK-041 may derive BattleResult.BossDefinitionId from
                      BattleState.BossState.BossId via Identity (DATABASE.md §1)
Scope Verification     MVP_SCOPE.md §1 checked — a documentation contract, no
                      new system/content; src/, tests/, migrations, and other
                      task files untouched (git-verified); §6 adjacent issues
                      reported not fixed; nothing OUT/FUTURE introduced
Final Rule            "Three distinct Boss identity concepts exist and are never
                      collapsed (TASK-046): BossId (canonical technical
                      Identity) … Display name … presentation only; never a
                      technical identifier in state, events, persistence, or the
                      API … BossDefinitionId — the persistence primary key"
                      (BOSS_RULES.md §6.4), with `Identity` holding "the Boss's
                      canonical technical ID … not the display name, and … not
                      `BossDefinitionId`" (DATABASE.md §1 contract note item 1)
```

---

## 12. Definition of Done (`AGENTS.md` §22)

- [x] Requirement understood; all six gaps evidenced with file/section
      citations (§4)
- [x] Relevant docs read (§2 table); scope checked against `MVP_SCOPE.md`
      §1/§4
- [x] Existing implementation checked read-only (Domain Bosses, `BossId`,
      `BossState`, Pet identity precedent, event examples, tests)
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
| Correctness | PASS — contract matches human decisions A–F verbatim (§5A); `boss-hoa-long` used exactly where a technical identity is required; display names only in content tables/version history; Thủy Ma/Mộc Yêu left UNRESOLVED per Decision C (no invented ids); no rule conflict with any ADR (grep: no ADR mentions Boss identity) |
| Architecture | PASS — no layering/state-model change; `BossState` gained no member (E); server-authoritative derivation only (FK resolved from BattleState server-side — ADR-001/GAME_RULES §18); no ADR required |
| Scope | PASS — this session edited exactly six `docs/` files + this task file; TASK-041/043/044/045 untouched; no src/tests/migrations/serializer changes; MVP_SCOPE §1 = documentation contract, no new system |
| Tests | N/A (documentation-only) — consistency checks recorded instead: repo-wide grep of display names vs `boss-hoa-long` (checks 1–3 below), Decision F inventory re-verified after edits (check 4), `git status` shows no source changes attributable to this session |
| Documentation | PASS — each answer written to its §10 canonical owner; dependents cross-referenced not duplicated (`documentation-change.md` §2); version headers bumped in all six docs (BOSS_RULES 2.2→2.3, DATABASE 1.7→1.8, GAME_EVENTS 2.2→2.3, GAME_STATE 2.6→2.7, SIGNALR 2.3→2.4, API_CONTRACTS 1.5→1.6) with prior history preserved |
| Security | N/A (no auth/data-exposure surface; `bossId` remains a validated request member — API_CONTRACTS §3) |
| Performance | N/A |
| Maintainability | PASS — no new columns/fields/abstractions; one added DB constraint (`Identity` NOT NULL/UNIQUE) is the documented prerequisite of the §1 FK lookup, not speculative |
| Determinism | N/A for logic; identity resolution recorded as server-side/battle-end derivation from BattleState (client `bossId` is a request, never an authority) |

**Validation checks executed:** (1) display-name grep across `docs/` —
remaining occurrences are content tables, version history, or the
explicitly-superseded code-literal note only; (2) `sourceId` display-name
grep — zero matches (examples now `boss-hoa-long` in §3.2.16–§3.2.18);
(3) `boss-hoa-long` present in all six edited documents; (4) `BossId`/
`bossId`/`BossDefinitionId` full-repo inventory — every occurrence now
falls under a defined owner (§11 Contract Surface), none ambiguous.

---

## Revision History

| Revision | Date | Change |
|---|---|---|
| 1 | (created) | Created after a read-first Boss identity audit: `BOSS_RULES.md` §6.4 fixes `BossId` as the display name while `DATABASE.md` §1 carries an undefined `BossDefinitionId (PK)` + `Identity` pair, `GAME_STATE.md` §2.4 explains no identity semantics (unlike §2.3's explicit `PetId` paragraph), `sourceId` mixes an instance id with a definition display name, `API_CONTRACTS.md` §3 states no value form, and `DATABASE.md` §1's `BattleResult` identity-sourcing note omits `BossDefinitionId` — TASK-044 cannot choose its PK/`Identity` values and TASK-041 cannot derive its FK until a human settles the contract. Assigned sequential ID TASK-046 (next free after TASK-045). |
| 2 | 2026-09-26 | Human decisions A–F received (all six answered, no STOP fired) — answers + verification in §5A. Six documents edited per §10 owner map: `BOSS_RULES.md` v2.3 §6.4 (three-way distinction, convention, `boss-hoa-long`, Thủy Ma/Mộc Yêu UNRESOLVED, superseded-code note); `DATABASE.md` v1.8 (§1 PK/Identity/FK annotations + contract note item 1 + sourcing item 2, §3 `Identity` NOT NULL/UNIQUE); `GAME_EVENTS.md` v2.3 §2 (BattleStarted, Passive note 2, BossSkillCast); `GAME_STATE.md` v2.7 §2.4 denotation paragraph; `SIGNALR_PROTOCOL.md` v2.4 §3.2.16–§3.2.18 examples/tables/notes + version note superseding prior 2.1; `API_CONTRACTS.md` v1.6 §3 `bossId` value form. §9/§12 ticked, §11 evidence filled, Quality Review Record added. No source/test/migration change; TASK-041/043/044/045 untouched. File → `tasks/completed/`, Status `DONE`. |
