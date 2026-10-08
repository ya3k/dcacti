# TASK-219A — Signature Skill Identification and Protocol Correction

**Type:** COMPLETION RECORD (retrospective reconstruction) — record creation only. This task
creates this record and nothing else. No production code, test, contract document, smoke script,
migration, or historical task record is modified by it.
**Subject:** the client's Signature Skill **identification source** and the two authoritative
statements that own it.
**Decision authority for the corrected contract:**
`tasks/completed/TASK-213-content-reachability-decision.md` §5 (Product Owner decision — "Signature
Skill is IN; the defect is the identification source").
**Retrospective authority:** `tasks/active/TASK-224-commit-step-and-task-209-record.md` §Revision 2
"Product Owner Decisions" — **Decision 1 = A** (agent-owned commit workflow; TASK-223 §6.2 P-1…P-6
now policy) and **Decision 2 = A** (evidence-only reconstruction of the six executed tasks with no
record; TASK-219A is the fifth).
**Primary evidence:** `tasks/completed/TASK-219A-post-implementation-audit.md` — the record of
**TASK-219A-A**, the *audit of this task*; cited throughout, never duplicated.
**Status:** DONE (reconstructed).

> Retrospective reconstruction authored under TASK-224, Product Owner Decision 2 = A (evidence-only).
> It records only what code, documents, git state and citing records **prove**; every unproven item
> is labelled. **This record closes the lifecycle gap that
> `tasks/completed/TASK-219A-post-implementation-audit.md` §P-2/§11 reports.**
>
> **Naming trap:** that file is TASK-219A-**A**'s record (the audit). Its §8.2/§P-2 state that
> **TASK-219A's** record is required and absent — that absence is what this file fills. This record
> does not edit, extend, or supersede the audit record.

## Metadata

```text
Task ID:           TASK-219A
Type:              FEATURE (authoritative-document correction + implementation)
Status:            DONE
Risk:              MEDIUM — reconstructed; no task file ever assessed it
Priority:          P1 as a contract conflict / P2 player-visible (TASK-213 §9 row 4)
Primary Agent:     backend / client — never assigned; no task file existed
Supporting Agents: N/A (never assigned)
Workflow:          development/feature.md — inferred from TASK-213 §9 row 4's kind
                   ("I — the decision is now made"); none was recorded
Skills:            N/A (never selected)
Dependencies:      TASK-213 (the decision this task applies; §9 row 4 names it as the only
                   dependency, and §9 row 5 makes TASK-219B depend on this task). §9 row 1
                   orders the commit/ownership task (TASK-223) before it.
Evidence status:   RECONSTRUCTED, EVIDENCE-ONLY. Direct evidence = current tree,
                   `git show --name-only 9b5c5fe`, `git status --porcelain`, and the two
                   documents whose version headers name TASK-219A. Assertion-level evidence =
                   the TASK-219A-A audit's traced chain, labelled in §5 and §7 below.
Lifecycle note:    reconstructed retrospectively; the original task file was never authored
                   (finding P-2 of the TASK-219A-A audit record).
```

## 1. Objective

Make the client identify the active Pet's **derived Signature Skill** from a delivered server datum
— `GET /api/pets` / `GET /api/pets/{petId}`'s `signatureSkill`, projected from
`PetDefinition.SignatureSkillCardId` through the `CardDefinition` it references — instead of from a
`category === "PetSkill"` test over owned Cards, which can never contain a `PetSkill` row; state
that rule on the two owning documents (the §5 collection read; `SIGNALR_PROTOCOL.md` §4.3 item 13);
dispatch **`PetSkillCast`** (no identifier) as the canonical client request while Basic Cards keep
`CardCast(cardId)`; and keep the Skill **non-owned** — never a `PlayerUnlockedCard` row, never a
§5.3 member. Reconstructed from `TASK-213:383-391`'s required-definition table. Nothing is
reconstructed from a task file, because none exists.

## 2. Authoritative References

Cited by path and section; contracts are not restated.

```text
docs/00-overview/MVP_SCOPE.md §1                             Signature Skill is IN
docs/01-game-design/CARD_RULES.md §1 items 1–5, §4.1         Card model, fixed 3+1 loadout, five Skills
docs/01-game-design/PET_RULES.md §8                          five Pets, one Signature Skill each
docs/02-technical/API_CONTRACTS.md §5.1/§5.2/§5.3/§5.6       the member, its nested shape, the membership
                                                             boundary, non-equip (amended here — v1.19)
docs/02-technical/SIGNALR_PROTOCOL.md §2, §3.2.20–§3.2.22,  hub methods, cast confirmations, the
    §4.3 item 13, §7                                         identification rule (rewritten here — v2.18)
docs/02-technical/DATABASE.md §1/§2                          required FK; the grant's Excluded: Skill clause
docs/02-technical/GAME_STATE.md §2.3                         equippedCards = 3 Basic + 1 Pet Skill
docs/03-decisions/ADR/ADR-012 items 9/10 · ADR-021           ownership model + loadout · one cast per turn
tasks/completed/TASK-213-content-reachability-decision.md §5 the decision this task applies
tasks/completed/TASK-219A-post-implementation-audit.md       the audit of this task
tasks/TASK_LIFECYCLE.md §3, §6.2                             DONE/SUPERSEDED · P-1…P-6 (P-3)
```

## 3. Scope

**In scope** (reconstructed from `TASK-213:383-391` + the audit's traced chain): the Pet read's
Skill identity (`cardId`, `name`, `category`) projected single-source from the required FK and the
referenced definition row; the client identification rewrite (delivered-Pet-read lookup keyed by
`cardId`, failing closed when the datum is absent); the dispatch split (`PetSkillCast` canonical;
`CardCast(<signature cardId>)` still conformant and still implemented); the two document amendments
(§5.1/§5.2/§5.3/§5.6; §4.3 item 13) with their version preambles; and making the client harness
model production (no synthetic `PetSkill` unlock row on the identification path) with the E2E
asserting the delivered reference.

**Out of scope:** widening §5.3's membership or making the Skill ownable (`TASK-213:390`;
`CARD_RULES.md` §1 item 4; `ADR-012` item 9); cost, affordability, cast legality, or
`effectDefinition` on `signatureSkill` (`API_CONTRACTS.md` §5.1 item 4); any new endpoint, request
member, parameter, header, pagination member, ownership row, schema, index, migration, Domain type,
SignalR member, event, or Redis contract (audit §2.5); the pre-existing reconnect group re-join gap
(audit P-1) and the phase-5c floater duplication (audit P-4), which the audit classified apart; any
item listed as OUT in `MVP_SCOPE.md` §2.

## 4. Evidence Base

```text
Git (read-only)   `git show --name-only 9b5c5fe`   the batch commit's 73 paths
                  `git status --porcelain`         the §5 markers
                  `git log --oneline --all`        no subject names TASK-219A
                  `git log -1 --format=%B 9b5c5fe` subject only — no body
                  `git rev-parse HEAD`             9b5c5fe (master)
                  No writing git command was run.
Tree snapshot     HEAD 9b5c5fe · 30 modified tracked · 1 deleted (the TASK-224 backlog/ →
                  active/ move) · 15 untracked
Caveat            The workspace is changing concurrently: TASK-224 D2 orders six evidence-only
                  reconstructions authored in parallel (TASK-227's record and TASK-224's
                  Revision 2 both landed during authoring). Every §5 marker is a snapshot.
Assertion source  tasks/completed/TASK-219A-post-implementation-audit.md (971 lines); its
                  audit HEAD was afe5b14 — one commit behind the current HEAD.
```

## 5. Declared File Set

This is the record's P-3 declaration. Markers per §4: `COMMITTED-IN-9b5c5fe` = in
`git show --name-only 9b5c5fe`, absent from status · `COMMITTED-PRE-9b5c5fe` = in neither (an
earlier commit's content, cited by the chain but not changed by this task) ·
`WORKING-TREE-MODIFIED` = ` M` in `git status --porcelain`.

| # | Path | Evidence | Marker | Other tasks sharing the file |
|---|---|---|---|---|
| 1 | `docs/02-technical/API_CONTRACTS.md` | §5.1/§5.2/§5.3/§5.6; the **1.19 header names TASK-219A** (`:3-5`) — direct attribution; audit §1.1/§1.2/§8.1 | `COMMITTED-IN-9b5c5fe` | TASK-212A-1 (§5.3/§5.4, v1.18 — TASK-223 §2.1 row 5) |
| 2 | `docs/02-technical/SIGNALR_PROTOCOL.md` | §4.3 item 13 rewritten; the **2.19 header records TASK-219A as "Prior 2.18"** (`:16-17`) — direct attribution; audit §1.1/§1.3 | `WORKING-TREE-MODIFIED` | TASK-208A (v2.17 — row 4); **TASK-226** (§7 item 4 = v2.19, the current hunk) |
| 3 | `src/backend/GameServer.Application/Collection/PetSignatureSkillItem.cs` | New Application read model `:44`; audit §2.1/§2.2 | `COMMITTED-IN-9b5c5fe` | None found |
| 4 | `src/backend/GameServer.Application/Collection/PetCollectionItem.cs` | `SignatureSkill` member `:61-68`; audit §2.1 | `COMMITTED-IN-9b5c5fe` | None found |
| 5 | `src/backend/GameServer.Application/Collection/CollectionQueryService.cs` | `ListPetsAsync` `:98-133`, `GetOwnedPetAsync` `:164-194`, `LoadSignatureSkillDefinitionsAsync` `:402-420`, `Project` `:444-501`; audit §2.1–§2.3 | `COMMITTED-IN-9b5c5fe` | TASK-212A-1 §3/§7 (row 7) |
| 6 | `src/backend/GameServer.Application/Cards/ICardRepository.cs` | New member `ListDefinitionsAsync` `:127-129`; audit §2.4 | `COMMITTED-IN-9b5c5fe` | None found |
| 7 | `src/backend/GameServer.Infrastructure/Postgres/Repositories/CardRepository.cs` | `ListDefinitionsAsync` `:98-120` (bulk, no `playerId`); audit §2.4 | `COMMITTED-IN-9b5c5fe` | None found |
| 8 | `src/backend/GameServer.Api/Controllers/CollectionResponses.cs` | `PetSignatureSkillResponse` `:141-153`, `PetResponse.From` `:90-102`, member `:82-83`; audit §2.1/§2.5 | `COMMITTED-IN-9b5c5fe` | TASK-212A-1 §2.1/§2.2/§7 (row 9) |
| 9 | `src/backend/GameServer.Api/Controllers/CollectionController.cs` | Skill named in the route docs `:13-16`, `:72`; routes `:95`, `:141`; audit §2.5 | `COMMITTED-IN-9b5c5fe` | TASK-212A-1 §7 (doc comments — row 10) |
| 10 | `src/frontend/client/src/services/api/CollectionModels.ts` | `PetResponse.signatureSkill` `:117`; `PetSignatureSkillResponse` `:137`; audit §4.1 | `COMMITTED-IN-9b5c5fe` | TASK-212A-1 §7 (row 22) |
| 11 | `src/frontend/client/src/game/scenes/BattleScene.ts` | `loadPetCatalog` `:1256-1276` → `signatureSkills` `:427`/`:586` → `renderCastControls` `:1348-1394` (`:1367` lookup, `:1392-1394` dispatch); `:1741`; `:2176`; audit §4.1/§4.2 | `WORKING-TREE-MODIFIED` | TASK-209 + TASK-210 (row 16); **TASK-218B** (the current worktree hunk, per the TASK-218C record) |
| 12 | `src/frontend/client/tests/SceneLifecycle.test.ts` | Production-faithful Card fixture `:312-341`; identification test `:3250-3287`; audit §6b/§6c | `WORKING-TREE-MODIFIED` | TASK-209/210/211 (row 32); **TASK-218B** (the current worktree hunk, per TASK-218C) |
| 13 | `src/frontend/client/scripts/standalone-web-smoke.mjs` | Phase 6d identification/presentation/dispatch `:2997-3136`; phase 6c resync `:3228-3239`; audit §6/§7.1 | `WORKING-TREE-MODIFIED` | TASK-209/210/211/212A-1 (row 23); TASK-218B; 226; 227; 228 |
| 14 | `tests/backend/GameServer.Application.Tests/CollectionQueryServiceTests.cs` | `ListPets_ShouldResolveEveryProvisionedPetsSignatureSkill` `:174-227`; `…ShouldNeverReachTheSignatureSkillThroughTheUnlockedCardCollection` `:229-262`; audit §6a/§6b — **assertion-level authorship** | `COMMITTED-IN-9b5c5fe` | TASK-212A-1 §5.1 (row 38) |
| 15 | `tests/backend/GameServer.Api.Tests/CollectionEndpointTests.cs` | `Pets_SignatureSkillReference_ShouldBeDeliveredWithoutAnyUnlockRow` `:211-260`; audit §6b — **assertion-level authorship** | `COMMITTED-IN-9b5c5fe` | TASK-212A-1 §5.1 (row 35) |

**Declaration caveat (binding on the table).** The audit states TASK-219A **is not separable by
diff** — all `TASK-207 → TASK-223` work was uncommitted on one worktree at audit time (audit §0.3
item 1) — and commit `9b5c5fe` then staged **73 files in one batch with no task ids in its body**
(`TASK_LIFECYCLE.md` §6.4). Rows 1–13 are anchored by in-file or in-tree artefacts (rows 1–2 by
TASK-219A's own version headers). **Rows 14–15 are assertion-level**: the audit credits those
methods (§6a/§6b) but no diff proves authorship; under P-2 they are shared with TASK-212A-1 and must
not be committed as a TASK-219A-only slice. Cited but **not** declared (pre-existing, unchanged):
`Domain/Pets/PetDefinition.cs:115`, `…/PetDefinitionConfiguration.cs` (FK, no navigation — audit
§2.4.1), `Domain/Cards/CardDefinition.cs`, `services/api/ApiService.ts:265`,
`game/runtime/GameRuntime.ts:834` (worktree hunk is TASK-226's), `Hubs/BattleHub.cs`,
`Infrastructure.Tests/PetCardRelicDefinitionProvisioningTests.cs`.

## 6. The Implemented Chain, as the Audit Traced It

Re-confirmed in the current tree. Lines are **current-tree**; where they differ from the audit's
citation the file changed after the audit (TASK-218B/226/227/228 touched the client files) — the
shift is noted, not hidden.

```text
docs (the two owning surfaces)  API_CONTRACTS.md §5.1/§5.2/§5.3/§5.6 (v1.19)
                                SIGNALR_PROTOCOL.md §4.3 item 13     (v2.18)
        ↓ resolved through CardDefinition (Domain/Cards/CardDefinition.cs)
PetDefinition.SignatureSkillCardId           Domain/Pets/PetDefinition.cs:115 (required FK)
        ↓
PetSignatureSkillItem                        Application/Collection/PetSignatureSkillItem.cs:44
        ↓
PetCollectionItem.SignatureSkill             Application/Collection/PetCollectionItem.cs:61-68
        ↓
CollectionQueryService                       ListPetsAsync :98-133 · GetOwnedPetAsync :164-194
                                             LoadSignatureSkillDefinitionsAsync :402-420
                                             Project :444-501 (copy at :497-500)
        ↓
ICardRepository.ListDefinitionsAsync         Application/Cards/ICardRepository.cs:127-129
CardRepository.ListDefinitionsAsync          Infrastructure/Postgres/Repositories/CardRepository.cs:98-120
        ↓
PetSignatureSkillResponse / PetResponse.From Api/Controllers/CollectionResponses.cs:141-153 / :90-102
        ↓
GET /api/pets · GET /api/pets/{petId}        Api/Controllers/CollectionController.cs:95-112 / :141-175
        ↓
BattleScene.loadPetCatalog                   BattleScene.ts:1256-1276 → signatureSkills :427/:586
  → renderCastControls :1348-1394 (lookup :1367; dispatch :1392-1394) → submitPetSkillCast :1741
        ↓
GameRuntime.requestAction → SignalRService → BattleHub.PetSkillCast / CardCast
```

Verified at each site and re-confirmed as text here: **one source of truth** (the projection copies
the referenced row's own `CardDefinitionId`/`Name`/`Category`, composes nothing, and never reads
`PowerCost`, `LoadoutCopyLimit`, or `EffectDefinition` — audit §2.2); **one bulk read, no N+1**
(audit §2.3); **no ownership writer** (audit §3.1/§3.3); **always present, never `null`** by
construction (audit §2.5); **identification is a value comparison against delivered data keyed by
`cardId`** — no local registry, no per-Pet literal, no `category` read (audit §4.1/§4.2); **dispatch
split** — delivered match → `PetSkillCast`, no match → `CardCast(cardId)` as the decided fail-closed
path (audit §4.2/§4.3); **durable, not transient** — a REST read of persistent content, surviving
the §7 snapshot, reconnect, and PLAY AGAIN (audit §5.1/§5.2).

## 7. Acceptance Criteria and Verification Status

The original task file was never authored, so **no acceptance criteria were ever written for
TASK-219A** (finding P-2). These are reconstructed from `TASK-213 §5`'s required-definition table
and the audit's §11 PASS conditions.

| # | Criterion (reconstructed) | Status | Evidence |
|---|---|---|---|
| AC-1 | Identified from the delivered Pet read, never from `category === 'PetSkill'` over `/api/cards`. | **VERIFIED** | audit §1.2/§4.2; `API_CONTRACTS.md:3-18`; `BattleScene.ts:1256-1276`, `:1367`; `SceneLifecycle.test.ts:3250-3287` |
| AC-2 | `signatureSkill` always present, never `null`, exactly `cardId`/`name`/`category`; no cost/affordability/legality/`effectDefinition`; no new endpoint, schema, index, migration, Domain type, SignalR member, event, or Redis contract. | **VERIFIED** | audit §1.2/§2.5; `CollectionResponses.cs:141-153`; `PetCollectionItem.cs:61-68`; routes unchanged at `CollectionController.cs:95/141/197/234` |
| AC-3 | Stays non-owned: no `PlayerUnlockedCard` row; §5.3 membership unchanged, not widened. | **VERIFIED** | audit §3.1–§3.5/§11; `CollectionQueryServiceTests.cs:229-262`; `CollectionEndpointTests.cs:211-260` |
| AC-4 | Canonical dispatch `PetSkillCast` (no identifier); Basics keep `CardCast(cardId)`; `CardCast(<signature cardId>)` conformant and implemented. | **VERIFIED** | audit §4.3; `BattleScene.ts:1392-1394` |
| AC-5 | Client holds no authoritative Card registry and no hard-coded mapping; fails closed to the raw `cardId`. | **VERIFIED** | audit §4.2 (comment-stripped source guard, `SceneLifecycle.test.ts:3412-3448` at audit time) |
| AC-6 | The new repository member is necessary and correctly scoped (bulk, no `playerId`, no index/cache). | **VERIFIED** | audit §2.4; `ICardRepository.cs:127-129`; `CardRepository.cs:98-120` |
| AC-7 | All five provisioned Pets covered. | **PARTIAL** | 5/5 at projection (`CollectionQueryServiceTests.cs:174-227`), provisioned-content and client layers; **1/5** at API and E2E — audit C-1, not a defect (audit §6a/§7.3) |
| AC-8 | Identification survives initial state, reconnect, `GetBattleState`, PLAY AGAIN; no transient-only state. | **VERIFIED**, with caveat | audit §5.1/§5.2; caveat C-4 (scene-level test uses the harness setter); the group re-join gap is audit P-1, classified apart |
| AC-9 | The two document amendments are sufficient, correctly owned, and leave no contradictory sentence. | **VERIFIED** | audit §1.1/§1.3/§8.1; `API_CONTRACTS.md:3-18`; `SIGNALR_PROTOCOL.md:16-26` |
| AC-10 | Relevant tests pass at the required validation depth. | **NOT PROVEN** | The audit executed no suite (audit §0.3 item 3: "No test suite was executed… test *content* was audited instead"); this record executed none. Content verified; run status is not. |
| AC-11 | `quality/review.md` §1 checklist passes. | **NOT FOUND** | No review artefact for TASK-219A. Searched `tasks/**`: the only `*219*` file is the audit record; `tasks/active/` holds `.gitkeep` + TASK-224. |
| AC-12 | No authoritative rule/contract violated; no client-authoritative computation (`AGENTS.md` §10 / `ADR-001`); no implementation defect remains. | **VERIFIED** | audit §4.2/§10/§11 — "IMPLEMENTATION DEFECTS FOUND — NONE"; residuals are E2E artefacts (V-1/V-2) and coverage gaps (C-1…C-8) |

**No criterion is asserted as passing on execution evidence.** Where the audit passed the
post-condition, that is recorded as the audit's decision, not as this record's measurement.

## 8. Tests and Validation

**Verdict: PASS, post-condition holding (audit §11)** — contract consistent · Pet-derived source
authoritative · Skill non-owned · `/api/cards` ownership-only · `PetSkillCast` canonical ·
`CardCast` for Basics · 5/5 covered (projection/content/client) · reconnect correct · no unintended
architecture/schema/API change · repository boundary justified · no implementation defect. Each
property was verified by **reading the assertion site**, not by running the suite (audit §6
preamble) — so the evidence is **first-hand for content, second-hand for run status**:

- Test *content* was audited at the assertion sites (audit §6a–§6h).
- The E2E check count was derived **statically**: 152 `record(` sites − 4 never-taken `else` branch
  − 1 `catch` branch = **147** (audit §7.2). **Point-in-time figure.** The current script contains
  **164** `record(` sites (measured for this record) because TASK-218B/226/227/228 added phases
  afterwards; the current successful-run total was **not** computed here.
- **No live E2E ran at audit time** (no PostgreSQL on its port, Docker absent — audit §0.3 item 2).
- **The audit itself found evidence second-hand**: the 147-check number is a static count from the
  script, not an observed run, and the one 5/5-against-a-live-database test skips silently without
  one (`PetCardRelicDefinitionPostgresProvisioningTests.cs:396-397`, audit §0.3 item 2).

**This record's own verification:** read-only — every §5/§6 `file:line` exists and contains the
cited artefact; both version-header attributions hold; every marker was measured; no TASK-219A task
file and no TASK-219A commit exist. **Not verified here:** any test run, build, E2E, typecheck, or
live behaviour; no live PostgreSQL, Redis, or browser was run. **Not verifiable by diff:**
TASK-219A's change set as such (audit §0.3 item 1; `TASK_LIFECYCLE.md` §6.4).

## 9. Remaining Issues

The audit's `P-n` findings are **not** TASK-223/TASK_LIFECYCLE's P-1…P-6 commit policy; the two
series are unrelated and are disambiguated per line.

- **Closed by this record — audit P-2 (no TASK-219A lifecycle record exists).** `TASK_LIFECYCLE.md`
  §3 requires a record for an executed task (DONE or SUPERSEDED); the audit could not author it
  (audit §8.2) and TASK-224 D2 commissioned it. Re-verified before authoring: no `*219*` file
  exists under `tasks/` other than the audit record, and no commit names TASK-219A.
- **P-1 — reconnect group re-join unstated/unimplemented** (pre-existing; not attributed to
  TASK-219A). Current state: TASK-226's record claims it implemented the re-join and
  `SIGNALR_PROTOCOL.md`'s 2.19 header documents §7 item 4 (`:3-8`). **Not verified beyond that
  record and header text** — no code path was executed.
- **P-3 (register) — starter grant resolves 18 definitions with 18 sequential `GetDefinitionAsync`
  calls** (pre-existing, unrelated). No change found; not re-examined.
- **P-4 — phase-5c floater duplication** (pre-existing probe timing). No change found; not
  attributed to TASK-219A by the audit or here.
- **P-5 — TASK-217A's stale comments and TASK-217C's superseded §3.2.20 sentence.** At measurement
  the two stale comment strings were **not found** in `src/backend/**` (searched `content-defined
  MVP Bosses` and `out of this task`; `BattleStartService.cs:251` now reads "five content-defined
  MVP Bosses"). The TASK-217C sentence **is still present** (`SIGNALR_PROTOCOL.md:1132`). Both
  tasks are in TASK-224 D2's reconstruction set — theirs, not TASK-219A's.
- **DOC-1 — the ownership-exclusion proposition is cited one step from its owning text.**
  Non-blocking, no contradiction; the audit assigns it to TASK-217B's area.
- **V-1 — phase-6d in-flight-caption race** (audit-assigned to TASK-219B). Current state:
  **remediated in the working tree** by TASK-227 — the wait requires `isSettledCastAcknowledgement`
  and retries up to four times (`standalone-web-smoke.mjs:3055-3074`; helper `:267-269`).
  Attribution per TASK-228's record §4/§7, not provable by diff.
- **V-2 — Basic-Card check satisfied by the in-flight caption.** Current state: **remediated in the
  working tree** by TASK-228 (`:2871-2884`, TASK-228 record §4/§6); same caveat.
- **Coverage gaps (C-series), audit-classified, none blocking:** `C-1` (5/5 not asserted at
  API/E2E), `C-2` (no `/api/cards` E2E assertion), `C-3` (no failing-Pet-read test), `C-4`
  (scene-level reconnect uses the harness setter), `C-5` (single-file source guard), `C-6` (residual
  synthetic `PetSkill` unlock rows), `C-7` (five Pet→Skill pairs transcribed in ≥5 fixtures). **Not
  re-measured here.** `C-8` — the stale comment "the three Pet Skills are 1" — is **still present**
  (`PetCardRelicDefinitionProvisioningTests.cs:351-352`, verified).
- **TASK-219B re-scope (audit §9 row 1): READY — RE-SCOPED (reduced); do not execute as written.**
  Both named deliverables landed inside the TASK-219A work (production-faithful client harness;
  phase-6d assertions of the delivered name). Residual: V-1, V-2, C-3, C-5, C-6. Re-verified: **no
  TASK-219B file exists anywhere under `tasks/`** (searched the whole tree for `219B`; only
  citations in TASK-213, TASK-218A and the TASK-219A-A audit match). It must be authored and
  re-scoped before pickup, or closed by a reconciliation record.
- **New — commit attribution was impossible at commit time; this record now supplies it.** `HEAD` is
  `9b5c5fe`, whose body carries no task ids (`git log -1 --format=%B` returns the subject only), so
  history holds no per-task attribution for TASK-219A's files. `TASK_LIFECYCLE.md` §6.4 records that
  commit as a **pre-adoption batch commit** that "complied with none of P-2…P-4"; P-6 forbids
  remedying it by rewriting history. **Consequence under P-3:** the §5 declared file set is the only
  per-task attribution instrument for TASK-219A. TASK-224 Revision 2's Stop-Condition-6 list of
  self-identifying work (`tasks/active/TASK-224-…md:423-442`) does **not** include TASK-219A,
  precisely because its record was absent; this record supplies what that list could not.
- **New — rows 14–15 of §5 are assertion-level.** If a commit plan cannot substantiate them, P-3
  requires them to be disclosed as shared/unowned, never committed as a TASK-219A-only slice.
- **New — version-state drift, and the audit record stays untouched.** `API_CONTRACTS.md` remains
  1.19 (this task's revision); `SIGNALR_PROTOCOL.md` moved to 2.19 (TASK-226), carrying TASK-219A as
  "Prior 2.18" — neither drift contradicts this declaration. The audit record is the P-2 source and
  `TASK_LIFECYCLE.md` §3 makes completed records immutable, so it is not edited.

## 10. Revision History

**Revision 1 — retrospective completion record authored.** Authority:
`tasks/active/TASK-224-…md` §Revision 2 "Product Owner Decisions" (Decision 2 = A, evidence-only;
TASK-219A is the fifth of six commissioned reconstructions); Decision 1 = A adopted TASK-223 §6.2
P-1…P-6 as the commit policy this record's declared file set serves. Closes the TASK-219A-A audit's
§8.2/§P-2. Method: read-only only — `git show --name-only 9b5c5fe`, `git status --porcelain`,
`git log`, and file reads; no writing git command was run and no existing file was edited. This
record creates exactly one new file. Honesty: no commit hash, date, test result, acceptance result,
or scope was invented; unproven items carry NOT PROVEN / NOT FOUND plus the location searched;
contracts are cited, not restated; the audit's evidence is summarised and cited, not copied.

TASK-219A COMPLETION RECORD — RECONSTRUCTED

```text
Subject:                Signature Skill identification / protocol correction
Status:                 DONE (reconstructed under TASK-224 D2 = A, evidence-only)
Correction:             identified from the delivered Pet read (API_CONTRACTS.md §5.1/§5.2
                        `signatureSkill`), never from a `category` read over owned Cards
                        (SIGNALR_PROTOCOL.md §4.3 item 13, v2.18)
Ownership / dispatch:   no `PlayerUnlockedCard` row, §5.3 not widened; `PetSkillCast`
                        canonical, `CardCast(cardId)` for Basics and as the fail-closed path
Defects / tests:        NONE found (audit §10); no test suite executed by the audit
                        (audit §0.3 item 3) or by this record; live E2E not run (item 2)
Declared file set:      15 paths (§5), every path marked COMMITTED-IN-9b5c5fe /
                        WORKING-TREE-MODIFIED / COMMITTED-PRE-9b5c5fe; rows 14–15
                        assertion-level and labelled
Lifecycle gap:          CLOSED — audit §P-2 satisfied · git operations: read-only only
```
