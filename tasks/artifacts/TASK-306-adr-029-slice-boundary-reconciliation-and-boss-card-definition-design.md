# TASK-306 — ADR-029 Slice Boundary Reconciliation and Boss Card Definition Design

**Deliverable type:** design decision report (artifact). Not an authoritative
source: every rule below cites the owning document or the source location it was
verified against.
**Date:** 2026-10-19
**Repository baseline:** branch `master`, HEAD `0551386dcd17438df8daf95ddbef38e84c32a071`,
working tree clean at audit start, in sync with `origin/master`.
**Task verdict:** `READY FOR OPERATOR APPROVAL` (design).
**Runtime status:** `BLOCKED` on the operator decision set in §10 — this is the
expected pairing, not a contradiction (`ADR-029` §4 leaves each slice gated).

---

## 0. Decision at a glance

| # | Decision | Verdict | State |
|---|---|---|---|
| A | Boss Card definition source | **Option A** — reuse `CardDefinition`, authorize Boss Card provisioning | Recommended; needs operator approval (U-7, U-8) |
| B | U-3 cast-priority score contract | Per-Boss authored `int` on the Boss definition, compared to the board action's own score; board wins ties; zero unseeded RNG | Contract decided; **values unresolved** (U-3), **carrier approval** (U-9) |
| C | Initial vertical slice | `Earthquake → Shield`, Boss-self, trigger-based, no Turn duration | Shape decided; **cost + magnitude unresolved** (U-2, U-4, U-10) |
| D | Active-form migration contract | Active form = provisioned content presence, resolved once per battle; one form per ability; no cooldown inference | Contract decided; **authorization** (U-11) |
| E | Action-selection clarifications | All seven reconciled; two documented consequences need text (deadlock reshuffle on a cast phase; cooldown `N−1` semantics) | Decided by existing authority; clarifications proposed |
| F | Slice boundaries | Insert a **definition/content/provisioning prerequisite** between Slice 2 and Slice 3; add executable per-slice exit criteria | Recommended; requires an `ADR-029` §4 amendment |

No runtime code, test, migration, provisioning, API, SignalR, Redis, frontend or
`docs/` change is made by this task.

### Deliverable section index (the twelve required sections)

| # | Required section | Answered in | State |
|---|---|---|---|
| 1 | Repository and task-lifecycle evidence | §1 | Recorded |
| 2 | Evidence base and method | §2 | Recorded |
| 3 | Direct source-inspection findings | §3 (F1–F20) | Recorded |
| 4 | Boss Card definition source, cost/effect ownership, provisioning (Decision A) | §4 | Recommended — needs operator approval |
| 5 | U-3 cast-priority score contract (Decision B) | §5 | Contract decided — values unresolved |
| 6 | Initial Boss Card vertical slice (Decision C) | §6 | Shape decided — balance values unresolved |
| 7 | Legacy Skill versus Boss Card migration and active form (Decision D) | §7 | Contract decided — authorization pending |
| 8 | Action-selection and cooldown ambiguities (Decision E) | §8 | Decided by existing authority; two text clarifications proposed |
| 9 | ADR-029 Slice 3–5 boundaries and exit criteria (Decision F) | §9 | Recommended — needs ADR amendment |
| 10 | Decisions versus unresolved operator decisions | §10 | Recorded |
| 11 | Exact files requiring documentation changes | §11 | Proposed — not applied |
| 12 | Proposed follow-up implementation tasks and dependencies | §12 | Proposed — not created |

*(§0 above is front matter; §13 is the risk/defect register and §14 the single verdict — closing apparatus, not additional required sections.)*

---

## 1. Repository and task-lifecycle evidence

### 1.1 Baseline (verified in-session)

```text
git rev-parse --abbrev-ref HEAD     master
git rev-parse HEAD                  0551386dcd17438df8daf95ddbef38e84c32a071
git status --porcelain              empty at audit start (clean)
upstream                            origin/master, `## master...origin/master` (in sync)
HEAD subject                        docs: file the adr-029 slice 2 shared cast contract completion record
recent history                      a2c1ea2 (Slice 1 filing), de78887 (Slice 1), 3e2518d (Slice 2),
                                    0551386 (Slice 2 filing), dbafdf3 (ADR-029 authorization)
task folders                        backlog/ 0 md, active/ 0 md, blocked/ 0 md, completed/ 307 md
highest existing task ID            TASK-305
ADR set                             29 ADR files, ADR-029 Accepted; no ADR-030
```

### 1.2 Duplicate-record search

`tasks/completed/`, `tasks/backlog/`, `tasks/active/`, `tasks/blocked/`,
`tasks/artifacts/`, `docs/`, `src/`, `tests/` were searched for `TASK-306` — **0
matches**. No readiness assessment for TASK-306 exists in the repository
(`tasks/artifacts/` holds only `MVP-ACCEPTANCE-RELEASE-CLOSURE-AUDIT.md`,
`TASK-194-…json`, `TASK-197-…json`, `TASK-298-…json`), and none was supplied in
the session transcript. **The readiness assessment is absent; it is not treated
as evidence anywhere in this report**, the same treatment `TASK-303` §1.1 applied
to its absent feasibility audit.

`TASK-306` is therefore the next sequential ID (`tasks/README.md` §3: highest
existing ID + 1), created as `tasks/active/TASK-306-…md` with `Status: IN
PROGRESS`; no existing record is overwritten.

### 1.3 `.ai/PROJECT_STATE.md` freshness decision

The snapshot is **materially stale**, not merely behind:

```text
recorded Audited HEAD SHA       1d62313a14edf315029e147ff72a957eb4cab2d4  (!= 0551386)
recorded Freshness Status       STALE ("working tree dirty")
recorded Active task            TASK-263 (tasks/active/ is in fact empty)
recorded Total completed        269 (in fact 307 md records; highest ID 305)
recorded working tree           dirty (in fact clean at audit start)
```

Applied per `.ai/workflow/core/task-selection.md` §2: `recorded SHA != HEAD`
selects INCREMENTAL, but the recorded lifecycle facts are contradicted by Git
evidence (rule 1: "snapshot … materially inconsistent with git evidence") and
the delta crosses Domain + Application + Api + protocol + docs (rule 4:
"changes cross multiple architectural boundaries simultaneously"). **DEEP mode
was selected and bounded to the ADR-029 domains** — Boss response runtime, the
cast contract, card/boss persistence, the event/projection surface, the affected
tests, and the owning documents. The snapshot was not treated as authoritative;
§1.1 was re-derived from Git. `core/task-selection.md` Step 5 requires the cache
to be refreshed, which this task does (declared file `.ai/PROJECT_STATE.md`).

### 1.4 Lifecycle classification

`docs/` is not modified because the decisions this report prepares are not yet
approved (`AGENTS.md` §4 step 6, §7; `AGENTS.md` §17 documentation-first ordering
requires the design to be *approved* before the owning docs change). The primary
output is therefore a report artifact plus the navigation-cache refresh, with the
task record as the lifecycle carrier: type `DOCUMENTATION`
(`tasks/TASK_TYPES.md` §2), risk HIGH, following
`documentation/documentation-change.md` with `architecture/adr-change.md` as a
**proposal only**. This keeps the task single-typed (`tasks/TASK_TYPES.md` §3).

---

## 2. Evidence base and method

1. **Direct source inspection** of every disputed contract: Boss response phase,
   `BossAiEngine`, `BossState`, `ResourceGenerator`, the actor-aware cast
   executor, card/boss persistence and provisioning, the wire projection, the
   tests that pin the legacy behaviour, and the client presentation pipeline.
2. **Direct document inspection** of the owning sections (quoted only where a
   decisive sentence must be pinned).
3. **No network, no fetch, no history rewrite, no code execution** other than
   read-only shell inspection. No test suite was run: no code changed, so no test
   result could differ (the report states which tests a future slice must add or
   re-point, and does not run or edit them).

Citation shorthand: `path:NN` = repository-relative path, 1-based line.

---

## 3. Direct source inspection — findings

**F1 — The one-action policy is documented but not implemented.**
`GAME_RULES.md:509-518` and `BOSS_RULES.md:357-361` require exactly one action per
response phase. The runtime executes the Boss board action *unconditionally*
(`BattleStateService.cs:1772` `BossAiEngine.SelectMove`, then `:1782-1786`
cascade + generation), and may additionally fire the legacy Skill in the same
phase (`:2087-2088` `var skillFires = bossState.SkillCharge >= … && bossState.SkillCooldown == 0;`,
`:2095` `BossSkillCast`). There is no selection branch and no exclusivity gate.
This is the intended target-vs-runtime divergence `ADR-029` §2.9 and
`BOSS_RULES.md` §4.4 label as transitional — Slice 3 must **add** the choice, not
"fix a defect".

**F2 — The board-action score is exactly `BossMoveSelectionResult.Score`.**
`BossAiEngine.EnumerateLegalMoves` enumerates the 112 orthogonal pairs in fixed
row-major order (`BossAiEngine.cs:89-114`), detects first-pass matches, and
scores `Σ weight(gemType) × match.Cells.Count` with no tier multiplier
(`:157-174`). Weights are `ATK 10 / DEF 5 / HP 20 / POWER 10`
(`:13-33`, grounded in `COMBAT_RULES.md:290-296`). Selection takes the highest
score, then lowest `From`/`To` (`BossAiEngine.cs:219-241`), and returns
`Score`, `Reshuffled`, `Board`, `RngState`, `LegalCandidatesCount` (`:52-58`).
Because a match is at least 3 cells (`MATCH3_RULES.md:842`) and the smallest
weight is 5, **the smallest possible board-action score is 15**; a cast must
therefore score **≥ 16** to ever be selected under the documented tie rule.

**F3 — Deadlock recovery is inside `SelectMove`, before the score exists.**
`BossAiEngine.cs:199-217`: enumerate → if zero candidates, `BoardGenerator.Generate`
on the retained `Pcg32` stream (bounded, `BoardGenerator.cs:96`), re-enumerate, and
**throw `BoardGenerationFailedException`** if the reshuffled board still has no
legal swap. The retained state advances only when a reshuffle happened
(`BattleStateService.cs:1779-1780`). Consequence: in the current engine the board
action is always available when the phase runs (or the round throws), so
`BOSS_RULES.md:374-375`'s "neither action available" branch cannot be reached
through a successful recovery.

**F4 — Cooldown ordering is decrement → check → set.**
`BattleStateService.cs:1399-1418` decrements `SkillCooldown` once per committed
round, after `SwapExecutor` advanced `Turn` and before 18a
(`if (bossState.SkillCooldown > 0) { … - 1 }`); the legacy gate then checks
`SkillCooldown == 0` (`:2088`); a successful Skill sets
`SkillCooldown = bossDefinition.SkillCooldownTurns` (`:2290`). Under this order an
authored cooldown of `N` blocks **N−1 subsequent rounds** and the Nth round is
eligible — asserted by
`tests/backend/GameServer.Application.Tests/BossResponseTests.cs:1406-1409`
("a cooldown of N blocks N−1 rounds and the Nth round is the one it is eligible
on again"). The inline comment at `BattleStateService.cs:2280-2282` claims it
blocks N rounds and is the outlier (reported in §13, not fixed).

**F5 — The transitional resources are `SkillCharge` (gate) and `SkillCooldown`
(carrier).**
`SkillCharge` is charged +1 per POWER match group, clamped at
`ChargeRequirement` (`ResourceGenerator.cs:678-696`, called from
`BattleStateService.cs:1841-1847`); it resets on fire (`:2289`). `SkillCooldown`
is serialized to Redis (`BattleStateSerializer.cs:400`, `:802`) but **not**
delivered on the wire (`BattleHub.cs:366-372`, `ApiIntegrationTests.cs:770-781`);
it is not an ability-identity or migration-state signal, and nothing may use it as
one.

**F6 — The shared cast contract already resolves a Boss Shield correctly.**
`CardCastExecutor.Execute(state, cardDefinition, CardCastActor.Boss)`
(`CardCastExecutor.cs:108-127`) routes to `ExecuteBossCast` (`:582-775`):
`BossCard` category gate (`:597-600`), `EffectiveCardCost` with an empty modifier
set (`:609-611`), `BossState.Power` sufficiency (`:616-619`), a representability
gate **before** deduction (`:621-623`), exact deduction (`:630-633`), then
actor-relative effects — `Heal`, `Shield`, `Power` self and `Burn` opponent
(`:646-757`). `Shield` builds
`StatusEffect.TriggerBased("Shield", Shield, Boss, amount, ShieldDepletedCondition)`
and applies it via `StatusEffectLifecycle.ApplyShield` (`:669-695`). `Damage` and
`Crit` throw instead of deriving a value (`:814-849`). The Boss branch writes
neither `SkillCharge` nor `SkillCooldown`, advances no `Sequence`, and returns an
empty event list (`:759-774`) — the Slice-2 scope decision recorded in
`TASK-305` §"Scope Decisions Taken by the Readiness Pass" item 2.

**F7 — `BossCardCast` exists on paper only.**
The wire contract is already authoritative: `SIGNALR_PROTOCOL.md:558` (closed
`type` set) and §3.2.30 (`:1723-1778`: `type` + `cardId` + `sourceId`, no cost /
Power / cooldown member, tolerant-reader compatibility); `GAME_EVENTS.md:772`
defines its meaning; `BOSS_RULES.md:1009-1011` lists it. In code there is **no**
`BattleEventType.BossCardCast` (the enum's highest member is
`MatchedGemSummary = 19`, `BattleEvent.cs:344`), no factory, no projection arm,
and the identifier appears only in a comment (`CardCastExecutor.cs:579`) and a
test comment. The wire DTO already carries `cardId` and `sourceId` slots
(`BattleEventWireProjection.cs:229-240`), so Slice 3 needs no new DTO member.

**F8 — There is no Boss-side card lookup path and no production caller.**
`ICardDefinitionLookup.GetDefinitionAsync` (`ICardDefinitionLookup.cs:8-19`) →
`CardRepository.GetDefinitionAsync` filters **by id only**
(`CardRepository.cs:123-131`); `CardCastActor.Boss` appears in `src/backend` only
in the dispatch arm (`CardCastExecutor.cs:119`) plus doc comments. The Player cast
path resolves definitions per cast (`BattleStateService.cs:1156`); `_cardDefinitions
is null` throws (`:1151-1154`). No `BossDefinition` member names a Boss Card
(`BossDefinition.cs:177-309`).

**F9 — Reusing `CardDefinition` requires three persistence reconciliations.**
`CardDefinition` is an EF entity whose `Category` is stored as the enum ordinal
with **no database CHECK** (`CardDefinitionConfiguration.cs:65-73`), and whose
`PowerCost` (`:80-81`), `LoadoutCopyLimit` (`:109-115`),
`Name` (`:61-63`) and `EffectDefinition` jsonb (`:103-107`) are all required with
no defaults. `CardCategory.BossCard = 2` already exists Domain-only
(`CardCategory.cs:89`, documented at `:65-75`). Three contracts must therefore be
reconciled before any Boss Card row can exist:

1. **Identity value form.** `DATABASE.md:425-429` fixes a `CardDefinitionId` as
   `card-<ascii-kebab-case-name>` (e.g. `card-heal`), while
   `CARD_RULES.md:163-168` requires a Boss Card's `CardDefinitionId` to **be**
   the §6.4 `SkillId` (`flame-burst`, `root`, `earthquake`, …). `root` has no
   prefix at all, so the two cannot both hold. Under `AGENTS.md` §2 precedence a
   specific domain rule (`CARD_RULES.md`) governs a technical document
   (`DATABASE.md`), so the smallest correction is a documented exception in
   `DATABASE.md` — but it is a documentation conflict and must be approved
   (`AGENTS.md` §4), not silently resolved.
2. **Stored category set.** `DATABASE.md:431` (`Category ("Basic" | "PetSkill")`)
   and `DATABASE.md:1527` (`Category ∈ {Basic, PetSkill}`) still describe two
   values; `CARD_RULES.md:173-175` deliberately says the category adds no
   persistence contract *yet*. Authorizing provisioning changes both.
3. **Required columns with no authored value.** `PowerCost` has no default
   (`ADR-029` U-2 unresolved) and `LoadoutCopyLimit` is required although
   `CARD_RULES.md:169-172` says §1's copy limit does not apply to a Boss Card;
   the Pet Skill precedent for the latter is documented persistence-for-schema-only
   (`CARD_RULES.md:141-144`).

**F10 — Provisioning has exactly one sanctioned mechanism.**
`DATABASE.md:1666-1753` §5 item 4: static content rows are inserted by EF Core
migrations, insert-only, tracked by migration history, values copied from the
owning documents, never invented; no `HasData`, seed runner, external content
service or JSON pipeline. The provisioned `CardDefinition` set is 8 rows
(`DATABASE.md:1710-1717`; migration
`20261004055006_ProvisionThanhXaAndSonHungSignatureSkills.cs`), none of them a Boss
Card. A Boss Card row is therefore a **data-only provisioning migration**, not a
schema change (the int column accepts 2 today).

**F11 — The effect vocabulary can express Earthquake and cannot express the other
four.**
The closed set is `Heal | Shield | Power | Damage | Burn | Crit`
(`DATABASE.md:1534-1540`; `CardEffectType.cs`) ×
`Flat | PercentMaxHp | PercentagePoints | Undetermined` (`:1541-1545`). Against
`BOSS_RULES.md` §6.3.1: Earthquake's Boss Shield is expressible as
`{Shield, Flat, <value>}`; Flame Burst (ATK buff), Root (ATK debuff), Drain Power
(flat Power drain) and Thunder Strike (next-qualifying-attack buff) have no
effectType. This is consistent with Slice 4's stated need for an explicitly
approved vocabulary extension, and it is why the initial vertical slice is
Earthquake.

**F12 — Earthquake's Shield is implemented but its magnitude is not recorded as
authored.**
`BossDefinitions.cs:279-288` declares `BossShield`, `StatusEffectId = "Shield"`,
`StatusEffectType = Shield`, `Magnitude = 150`,
`ExpiryCondition = ShieldDepletedCondition`; the legacy handler applies exactly
that (`BattleStateService.cs:2182-2197`). But `BOSS_RULES.md` §6.3.1 item 4
records the Shield magnitude as "Non-blocking balance tuning parameter", and
`ADR-026`'s Unresolved Balance Choices #4 and `ADR-029` §3 U-4 list "Shield points
for earthquake" as unresolved. **150 is therefore an implemented-but-unrecorded
value, not an approved one** — the operator must confirm or replace it (U-10).

**F13 — Pre-existing content/provisioning drift (reported, not fixed).**
`BOSS_RULES.md` §6.3 and §6.3.1 state `BaseDamage = 0` for all five abilities
while the provisioned `SkillDefinition` JSON and `BossDefinitions` carry
150/120/100/150/180 (`20260926151112_ProvisionBossDefinitions.cs:71,82,91`,
`20261004094100_ProvisionTwoRemainingMvpBosses.cs:118,131`; `BossDefinitions.cs:124,171,212,274,322`).
`TASK-305` §8 already records two pre-existing Infrastructure test failures on
exactly this drift. It does not block Slice 3's Shield path — the Shield effect
is carried by `BossSkillSecondaryEffect`, not `BaseDamage` — but it is a Slice-4
parity hazard and is reported in §13.

**F14 — Boss definition content is a per-battle in-memory input, not state.**
`BattleStateService` caches `BossDefinition` per battle in a singleton
`ConcurrentDictionary` at creation (`BattleStateService.cs:326`, `:557`) and reads
it with the indexer in the response phase (`:1692`); `GetBossDefinition` only
`TryGetValue`s (`:585-588`). No rehydration path exists, so a Redis-recovered
battle handled by a different process would fault at `:1692` — an existing
architectural caveat (not introduced here) that any Boss Card/score carrier must
not make worse.

**F15 — Resolution retries recompute the whole phase deterministically.**
`ExecuteSwapAsync` allows `MaxAttempts = 8` (`BattleStateService.cs:924-1035`) and
re-runs `ResolveSwapAsync` against freshly read state on each attempt (`:930`,
`:1034`); a refused CAS writes nothing, so that attempt's RNG consumption is
discarded. The only RNG used in the Boss phase is the seeded deadlock reshuffle
(`:1770-1780`); `Pcg32` is constructed from the retained state each attempt, not
from a shared mutable stream. Selection is therefore a pure function of
(committed state, definitions, enumeration order).

**F16 — The legacy behaviour is pinned by many tests.**
The sharpest coexistence assertions (board action **and** cast **and** attack in
one round): `BossResponseTests.cs:1117-1224` (index ordering,
`:1199`, `:1203`, `:1212`), `:1029-1115` (`:1092`, `:1098`),
`AtomicRoundOrchestrationTests.cs:1026-1064` (`:1054`), `:415-465` (charge
equation), `:307-351`, `:708-719`; `BossSkillSecondaryEffectTests.cs:887-890`;
`BossSkillStep1CompositionTests.cs:499-545`; `BossResponseWireTests.cs:330-391`
(`:381`); `ApiIntegrationTests.cs:3862-3864` (Boss boundary = last `SwapStarted`);
`RoundTestSupport.cs:381-386`, `:441-453` (Boss board-run helpers). Slice 3 must
keep every test that belongs to an **unmigrated** Boss passing and add new tests
for the migrated path — it may not edit them to fit a new policy.

**F17 — The client cannot present a Boss Card cast today.**
`BattleEventPresenter.ts` parses 18 in-battle types (`:4-30`), drops unknown
types by returning `null` (`:288-329`, `:325-327`), adapts each event to
(actor, action, phase kind) at `:1305-1461` (`BossSkillCast` → `boss-skill` at
`:1416-1422`), and describes callouts at `:1815-1883` (`:1854-1859`). `BossCardCast`
occurs **0 times** in `src/frontend`. Consequences for Slice 3b: no parser, no
phase kind, no callout, no hold sizing (`BattleScene.ts:5415-5421` recognises only
`BossSkillCast`), no display-name resolver for a Boss Card (`cardDisplayName`
reads the Player collection; the correct resolver is the existing
`BOSS_SKILL_NAMES` / `resolveBossSkillDisplayName`, `BattleEventPresenter.ts:962-976`,
because the identity is the SkillId). The scene has two safe drop sites
(`BattleScene.ts:4561-4563`, `:5066-5072`), so an unhandled `BossCardCast` is
*invisible*, not a desync.

**F18 — The balance harness inherits the policy and pins cadence numerically.**
The simulator drives production `BattleStateService` and counts events
(`BalanceSimulator.cs:17-20`, `MetricAccumulator.cs:185-186`,
`BalanceSimulationMetrics.cs:256-261`); it has no Boss Card or selection concept.
Five per-seed `BossSkillCastCount` rows are hard-coded
(`BalanceEvidenceMeasurementCorrectionTests.cs:218-241`) together with drained
Power and turn counts (`:437-463`). Protected artifacts must not be regenerated in
place (`ProtectedArtifactGuard.cs:13-19`). Any cadence change must add a new
metric and **re-derive** those rows, not hand-edit them.

**F19 — Stale text inside the implementation (reported, not fixed).**
`BattleHub.cs:366-372` still calls the Boss projection "three-member" while the
record carries four members including `power` (`:417-421`, docs agree with the
record: `SIGNALR_PROTOCOL.md` §4.4 item 12); the cooldown inline comment at
`BattleStateService.cs:2280-2282` contradicts both the implementation and
`BossResponseTests.cs:1406-1409`; `ADR-029` §1.2's "eleven positional members"
describes the decision-time snapshot, before `TASK-304` added `Power`
(`BossState.cs:253-265` has twelve). These are documentation/test debt
(`AGENTS.md` §16), out of scope here.

**F20 — `BossDefinitions` is a production identity/structural baseline, not a
content store.**
`BossDefinitionLookup.cs:102-150` takes identity and structure from the static
catalogue, reads only the four balance columns from PostgreSQL, throws when the
row is absent (`:131-137`) and validates ranges (`:142`) — it is explicitly
prohibited from acting as a silent runtime balance fallback
(`DATABASE.md:886-894`). Cards have no static catalogue at all: production cards
are PostgreSQL-only (`DependencyInjection.cs:90-92`, `ScopedCardDefinitionLookup.cs`,
`CardRepository.cs`). This asymmetry is the crux of Decision A.

---

## 4. Decision A — Boss Card definition source

### 4.1 Options evaluated

| Criterion | **A. Reuse `CardDefinition`** (+ authorize provisioning) | **B. Boss-side definition carrier** (new type/table) | **C. Non-persisted Domain catalogue as permanent production source** |
|---|---|---|---|
| Identity / `CardDefinitionId = SkillId` rule | One identity: the row's PK is the SkillId (`CARD_RULES.md:163-168`); requires the `DATABASE.md` value-form exception (F9.1) | A second identity carrier; would have to restate or reference the SkillId rule; duplicate identity mechanism | Identity trivially expressible in code, but a second content mechanism alongside the DB card contract |
| Cost & effect ownership | Reuses `PowerCost` + `EffectDefinition` and `CardCastExecutor`'s Boss branch unchanged (F6) | Must re-declare cost/effect semantics or convert to `CardDefinition` at the call site | Reuses the Domain types but moves authorship out of the documented persistence contract |
| Provisioning & validation | Existing EF-migration INSERT pattern, one row per ability (F10); fail-closed JSON reader already validates effects | New table/migration/validator; duplicates the provisioning rule or invents a fourth mechanism (`DATABASE.md:1023-1032` forbids) | No DB row at all; contradicts `DATABASE.md` §5 item 4's "the mechanism is also now defined, on the same terms" for content rows; content changes become code deploys |
| DB constraints & doc consistency | No schema change needed for the ordinal; doc changes required (`DATABASE.md` §1/§3/§5, `CARD_RULES.md` §1.1 items 3/5, `ADR-029` §2.5 item 15) | New table + constraints + docs; both carriers must be kept consistent forever | No schema change, but permanently splits "what a card is" across DB and code |
| Runtime lookup & replay determinism | `GetDefinitionAsync` is id-only and already the Player path; the same immutable content row feeds both actors | New lookup path; same determinism shape, more surface | Lookup is code, deterministic, but content and behaviour ship together; no per-battle snapshot |
| Player-card compatibility | Shared contract already proven by `TASK-305` regression suites; Player gates reject `BossCard` both ways (`CardCastExecutor.cs:158-161`, `:597-600`) | Risk of divergence as the Player contract evolves | No runtime risk, but two definitions of the same concept |
| Five-ability migration | One row per ability; effects expressible only after Slice 4's vocabulary extension (F11) | Same vocabulary problem plus a carrier problem | Same vocabulary problem, plus a second authoring location |
| Scope / maintenance / duplicate-source risk | Smallest correct change; one source of truth for "is a Card" | Largest change; two sources of truth for cost/effect/identity | Medium code change, highest long-term duplication risk: it reintroduces a code-side content registry for a concept the project deliberately made database-driven (`DATABASE.md` §5 item 4; `ARCHITECTURE.md` §5 item 1) |

### 4.2 Recommendation

**Option A — reuse `CardDefinition` as the canonical card contract and authorize
Boss Card provisioning.** It is the only option that keeps one identity, one cost
contract, one effect vocabulary, one lookup, and one provisioning mechanism, and
it is the option `CARD_RULES.md` §1.1 and `TASK-305` already prepared. It is
**not approved by this report**; the operator decisions in §10 must be taken
first.

Conditions (all required before any provisioning):

1. **U-7** — authorize Boss Card content provisioning and the persistence
   contract change (no schema change; a data-only migration).
2. **U-8** — resolve the identity value-form conflict by amending `DATABASE.md`
   §1/§3 with a documented exception: a `BossCard` row's `CardDefinitionId` is
   the ability `SkillId` (`CARD_RULES.md` §1.1 item 3 remains the owner). Do not
   change `CARD_RULES` to a `card-` prefix: that would create a second identifier
   for the same ability and break the `BossCardCast.cardId`/`BossSkillCast.skillId`
   identity equality (`SIGNALR_PROTOCOL.md:1744-1748`).
3. **U-2 / U-10** — the row cannot be provisioned without an authored
   `PowerCost`; `ADR-029` U-2 is unresolved.
4. **U-4 / U-10** — the Earthquake effect row needs the Shield magnitude recorded
   as authored (§6).
5. `LoadoutCopyLimit` is persisted as `1` and documented as never read for a
   `BossCard` row, following the Pet Skill precedent (`CARD_RULES.md:141-144`).
6. No `PlayerUnlockedCard` row, no loadout slot, no collection projection, no API
   exposure (unchanged from `ADR-029` §2.5 items 12–13).

### 4.3 Exact contracts affected if A is approved (proposal only — not applied)

- `docs/02-technical/DATABASE.md` — §1 `CardDefinition` block (category set,
  identity value-form exception, content-set description), §3
  `CardDefinition.Category ∈ {Basic, PetSkill}` constraint, §5 item 4's
  provisioned-row set.
- `docs/01-game-design/CARD_RULES.md` — §1.1 item 3 (identity cross-reference to
  the `DATABASE.md` exception), item 5 (the "no persistence contract" statement
  becomes false; replaced by the provisioning contract and the
  `LoadoutCopyLimit`-persisted-never-read rule).
- `docs/03-decisions/ADR/ADR-029-…md` — §2.5 item 15 (persistence out of scope)
  is superseded by a recorded follow-up decision; §3 gains U-7/U-8; §4 gains the
  prerequisite slice (§9).
- `docs/02-technical/ARCHITECTURE.md` §3 — add a `CardDefinition` /
  `CardRepository` boundary row; the persistence row (`:825-828`) currently omits
  the Card boundary even though `CardRepository` exists.
- Provisioning: one data-only EF migration with exactly one `InsertData` and a
  mirrored `DeleteData`, values copied from the owning documents, insert-only.
- Tests: the provisioning row-count/category assertions
  (`PetCardRelicDefinitionPostgresProvisioningTests.cs:362-391`,
  `PetCardRelicDefinitionProvisioningTests.cs:347-383`) gain the Boss Card row;
  the Domain closed-set test already expects three categories (`TASK-305`).
- Lookup: no repository change is required (`GetDefinitionAsync` is id-only), but
  a Boss-side resolution point must exist (§5, §7).

---

## 5. Decision B — U-3 cast-priority score contract

### 5.1 What is already decided (not open)

`BOSS_RULES.md:363-392` fixes the structure: eligibility gates in order, "if
exactly one action is available → selected whatever its score", each available
action gets a deterministic score, the higher score wins, a tie goes to the board
action, and board ties keep lowest cell index. `BOSS_RULES.md:419-423` fixes that
the *authored form and values* are unresolved (U-3), and that Slice 3 may not be
implemented until they are approved. The score is "the Boss definition's authored
deterministic cast-priority score for its Boss Card"
(`BOSS_RULES.md:383-384`) — i.e. **per Boss, owned by the Boss definition**, not
global and not per-card-instance.

### 5.2 Proposed contract (structure only; no value authored)

```text
Name            CastPriorityScore
Type            Integer (32-bit, non-negative)
Scale           The board-action score's own scale: Σ weight(gemType) ×
                matchedCells over the selected best legal swap
                (BossAiEngine.cs:157-174; weights 10/5/20/10)
Ownership       Per Boss, on the Boss definition (BOSS_RULES.md §4.3 step 4)
Range           0 .. int.MaxValue representable; the minimum value that can ever
                be selected is 16, because the smallest board-action score is 15
                (min match 3 cells × min weight 5) and ties go to the board
                (BOSS_RULES.md §4.3 step 6). Values 0–15 are legal authoring
                choices that always yield the board action.
Comparison      castSelected ⇔ castScore > boardScore, where boardScore is the
                Score of the selected best board candidate
Tie-break       Board action wins (unchanged; a cast must strictly out-score)
Deadlock        Comparison happens AFTER the documented recovery, because the
                compared boardScore is the post-recovery selected move's score
RNG             Zero. No roll, no tie-break coin, no unseeded source. The only
                RNG in the phase remains the existing seeded deadlock reshuffle
Validation      Missing/unset ⇒ invalid definition data, fail-closed at battle
                creation (ADR-024 precedent); negative ⇒ invalid; no default and
                no fallback weight is permitted (BOSS_RULES.md §4.3 item 5,
                AGENTS.md §7)
Observation     A cast-selected response phase may still have replaced a
                deadlocked board and advanced the seeded stream, because recovery
                precedes the enumeration. No board-cycle event is emitted for it
                (ADR-026 item 8; GAME_EVENTS.md §3 item 8)
```

### 5.3 Carrier recommendation (needs approval: U-9)

**Recommended:** a new non-nullable integer column `CastPriorityScore` on the
`BossDefinition` row, resolved through the same `IBossDefinitionLookup` overlay
that supplies `MaxHP/ATK/DEF/EnrageThreshold` (`BossDefinitionLookup.cs:102-150`),
cached per battle at creation (`BattleStateService.cs:557`) and never re-read
mid-battle.

- Why the Boss definition: `BOSS_RULES.md:383-384` names it; the value describes
  *what the Boss does*, not what one card is; it exists for a Boss even before its
  ability is migrated, and it survives the eventual retirement of the legacy
  Skill form.
- Why a column and not the `SkillDefinition` JSON: that JSON is the transitional
  Skill carrier with a fixed four-member contract (`DATABASE.md:989-1002`,
  TASK-045), and its `skillId`/`chargeRequirement` members retire; overloading it
  with a Card-form selection value would conflate two forms.
- Consequence to approve: `ADR-024`'s "four balance values" overlay becomes five;
  `ADR-024` needs an amendment, `DATABASE.md` §1/§3 gain the column and a
  `>= 0` CHECK, and a schema migration is required. That migration is **not** in
  this task and is a separate follow-up (§12).
- Alternative (rejected): store the score on the Boss Card row. It keeps
  `CardDefinition` self-contained, but contradicts `BOSS_RULES.md`'s ownership
  sentence, does not exist for an unmigrated Boss, and splits the selection
  policy's input away from the Boss definition. It is viable only if the operator
  prefers it **and** `BOSS_RULES.md` §4.3 step 4 is amended.

### 5.4 What remains unresolved

- **U-3 values** — the five per-Boss scores. Not invented here, not estimated,
  not derived from the legacy charge requirement. A score is a balance value.
- **U-9** — the carrier/persistence authorization above (and the derived
  `ADR-024`/`DATABASE.md`/migration work).
- **U-12** (see §8.3) — the cooldown turn-count semantic, which changes how often
  a cast can be selected and therefore the cadence the score is tuned against.

---

## 6. Decision C — Initial Boss Card vertical slice: `Earthquake → Shield`

### 6.1 Verification against authoritative definitions

| Field | Evidence | Status |
|---|---|---|
| Ability identity | `BOSS_RULES.md:946-954` maps Sơn Thạch Vệ → `boss-son-thach-ve` → SkillId `earthquake`; `:979-980` | Decided |
| `CardDefinitionId` | `earthquake` — `CARD_RULES.md:163-168` (reuse the SkillId; no second identifier) | Decided, blocked by U-8 |
| `CardCategory` | `BossCard` (=2) — `CARD_RULES.md` §1/§1.1; `BOSS_RULES.md` §4.2 item 1 | Decided (Domain value exists) |
| `PowerCost` | No value anywhere (`ADR-029` §3 U-2; `BOSS_RULES.md:455-457`) | **UNRESOLVED (U-2/U-10)** |
| Effect type | `Shield` — `BOSS_RULES.md` §6.3.1 item 4; `COMBAT_RULES.md` §4 | Decided |
| Effect value type | `Flat` (an absorption amount), matching the implemented legacy effect `Magnitude = 150` (`BossDefinitions.cs:286`); `PercentMaxHp` would change the unit against the Boss's `MaxHP` 3000 (candidate alternative, operator choice) | **Magnitude UNRESOLVED (U-4/U-10)** |
| Target | **Self** — actor-relative, the Boss's own `BossState.ActiveStatusEffects` (`BOSS_RULES.md` §4.2 item 4; `CARD_RULES.md` §3 item 9; `COMBAT_RULES.md:761-765`) | Decided |
| Duration / expiration | **No Turn duration.** Shield is trigger-based, outside the Turn countdown; it expires at `ShieldDepleted` (pool reaches exactly 0), is refresh-replaced, and at most one Shield exists per entity (`COMBAT_RULES.md:732-735`, items 2–5; `GAME_STATE.md` §2.3.1). `CardEffectDefinition` has no duration member for Shield (duration is Burn-only, `CardEffectDefinition.cs:171`) | Decided by existing contract |
| Cooldown | `0` Turns — carried over unchanged (`BOSS_RULES.md` §6.3 table; §4.2 item 5) | Decided (value carried over, U-5) |
| Legacy parity of the effect | Legacy handler applies the same `ApplyShield` with `StatusEffectSource.Boss` and the declared instance (`BattleStateService.cs:2182-2197`) | Verified |
| Cast-contract executability | Yes: `CardCastExecutor.cs:669-695` resolves a Boss self Shield through `StatusEffect.TriggerBased` + `StatusEffectLifecycle.ApplyShield`; `Shield` is in the representable Boss set (`:822-826`) | Verified |

### 6.2 Compact proposal for operator approval (every number exposed)

```text
CardDefinitionId   earthquake
Name               Earthquake            (the ability's documented display name)
Category           BossCard
PowerCost          UNRESOLVED — operator must author an integer in 0..100
                    (0 = always affordable; >100 = never affordable because
                     BossState.Power is clamped to 0..100; 0..100 all legal)
LoadoutCopyLimit   1    (persisted only because the column is required and
                         non-nullable; never read — Pet Skill precedent)
EffectDefinition   [ { effectType: "Shield", valueType: "Flat", value: <V> } ]
                    V = 150 is the value the legacy Boss Shield already
                    implements (BossDefinitions.cs:286) and the value the Damage
                    Pipeline already absorbs; BOSS_RULES.md §6.3.1 item 4 records
                    it as "Non-blocking", so 150 is IMPLEMENTED-BUT-UNRECORDED,
                    not approved. The operator must confirm 150 or author
                    another positive integer (or choose PercentMaxHp, which
                    changes the unit).
Cooldown           0 (carried over from BOSS_RULES.md §6.3)
Target             self (Boss)
Duration           none (trigger-based; expires at ShieldDepleted)
```

There is **no vocabulary mismatch** for Earthquake: `Shield` exists, its
resolution target is actor-relative, and the shared cast contract already
executes it. The other four abilities would each require a vocabulary extension
(F11) and are out of scope.

### 6.3 Behavioural consequences the operator is accepting

1. With `PowerCost = C`, the cast is available once `BossState.Power ≥ C` at the
   start of a response phase, and Power is generated only by the Boss's own
   board actions (`BOSS_RULES.md` §4.1 items 2–7; `GAME_STATE.md` §2.4.3 item 3).
   A cast phase generates no Power, so casting and Power accumulation trade off —
   exactly the cadence U-2/U-3 control.
2. With cooldown `0`, the only limiters are Power and the selection score, so the
   cast can recur every phase in which the score wins.
3. Shield refresh-replaces: a second cast **sets** the pool to the new magnitude
   rather than summing it, and at most one Shield exists per entity
   (`COMBAT_RULES.md:752-773`).

### 6.4 Tests the slice will need (stated, not added)

Domain fixtures already exist (`BossCardCastExecutorTests.cs:48-57`); the slice
adds: provisioning-row validation; battle-start resolution of the Boss Card;
selection (board-only / cast-only / both / tie); cast-selected round has no Boss
swap, no Boss generation, no Boss attack, one `BossCardCast`; `Power` deducted
exactly once; cooldown set; wire projection of `BossCardCast`; client phase and
callout; CAS-retry determinism (no double deduction, one event); and the
regression suite for unmigrated Bosses unchanged.

---

## 7. Decision D — Legacy Skill versus Boss Card migration policy

### 7.1 Rule (proposed for approval: U-11)

```text
For each Boss ability there is exactly ONE active form at any time.
The active form is a property of the PROVISIONED CONTENT, not of runtime state:

    active form = BossCard  iff a provisioned CardDefinition row exists with
                              Category = BossCard and
                              CardDefinitionId = that Boss's BOSS_RULES §6.4 SkillId
    active form = BossSkill otherwise (the transitional path)

The form is resolved ONCE per battle, at battle creation, through the same
content-input path that already supplies BossDefinition, and is cached with it
(BattleStateService.cs:508/557). It is never re-derived per round, never stored
in BattleState/Redis, and never inferred from SkillCooldown or SkillCharge.
```

Why content presence: it is a single source (the row either exists or does not),
it needs no new state member or wire member, it cannot disagree with itself (an
explicit flag plus a row could), and provisioning is insert-only and never
updates or deletes rows (`DATABASE.md:1068-1075`), so re-resolution after a
process restart returns the same answer. Why once per battle: it makes the whole
battle deterministic even if content changes mid-deployment, and it keeps
selection a pure function of the committed state plus a fixed content input.

Rejected alternatives: an explicit `abilityForm` flag on `BossDefinition`
(duplicates the row-presence fact, can disagree with it, and needs a second
authorization change); a code allowlist (deploy-time, not content-driven,
contradicts `ADR-024`); inferring from `SkillCooldown`/`SkillCharge` (explicitly
forbidden — cooldown is a timing counter, not identity).

### 7.2 Enablement scope for the first slice

**Exactly one Boss is migrated first: Sơn Thạch Vệ (`boss-son-thach-ve`,
`earthquake`)** — consistent with `ADR-029` §4's migration order ("Earthquake
first") and with the vertical-slice choice in §6. The other four Bosses keep the
legacy path unchanged and their existing test suites keep passing untouched.

### 7.3 Duplicate-execution prevention

```text
18a  selected action = board → no cast at 18c (the Card branch is gated on
                                "selected action = cast")
     selected action = cast  → no swap, no cascade, no generation, no attack
18c  legacy gate runs ONLY when the active form is BossSkill
     Card cast runs ONLY when the active form is BossCard AND the cast was selected
```

Because the form is a per-battle constant, exactly one of the two gates can ever
run for that ability in that battle. There is no fallback from a failed Card path
to the Skill path: if the Card content is invalid (cost, effect, score), battle
creation fails closed rather than silently selecting the other form.

### 7.4 Shared cooldown during the migration window

`BossState.SkillCooldown` is retained as the **single** cooldown carrier for both
forms (`GAME_STATE.md` §2.4.3; `BOSS_RULES.md` §4.2 item 5). It is decremented
once per committed round exactly as today (F4), read as a hard gate by whichever
form is active, and set to the authored value on a successful Skill fire or Card
cast. Because a Boss has only one active form, the counter is never consumed
twice for one ability. It is **not** an ability-identity or migration-state
signal and must never be read as one.

### 7.5 Testing the migration without retiring anything

- **Domain:** a pure active-form resolution function (row present → Card; absent
  → Skill) with no clock/DB/random.
- **Application:** the unmigrated Bosses' existing suites must pass **unmodified**
  (`BossResponseTests`, `AtomicRoundOrchestrationTests`, `BossSkill*Tests`,
  `BossResponseWireTests`, `ApiIntegrationTests`); the migrated Boss gets new
  tests for the Card path and must **not** emit `BossSkillCast` or accrue a
  legacy charge gate for that ability.
- **Mutual exclusion test:** in one battle the ability never produces both
  `BossSkillCast` and `BossCardCast` (`SIGNALR_PROTOCOL.md:1771-1774`).
- **No retirement:** `SkillCharge`, `BossSkillCast` and the legacy secondary-effect
  handler stay in place and stay covered; their removal remains `ADR-029` §2.9's
  four-part gate and Slice 5.
- **Cooldown test:** authored `N` on a Card cast blocks the same number of rounds
  the legacy form would (the ordering test in §8.3), so migration does not
  silently retune cadence through the counter.

---

## 8. Decision E — Action-selection and cooldown reconciliations

### 8.1 The seven required clarifications

1. **Exact board score compared against the cast score.** The board action's
   score is `BossMoveSelectionResult.Score` — the maximum
   `Σ weight × matchedCells` over the legal first-pass candidates on the board
   the selection evaluated (`BossAiEngine.cs:157-174`, `:219-241`). The cast score
   is compared to that value as an integer (§5.2). No other board quantity
   (cleared-cell count, `BaseDamagePool`, candidate count) is the comparator.
2. **Deadlock recovery happens before candidate comparison — and this has a
   consequence.** `BOSS_RULES.md:366-369` places recovery before the
   availability enumeration; `BossAiEngine.SelectMove` performs it before a score
   exists (`:199-217`). Therefore the compared board score is always the
   post-recovery score, and a **cast-selected phase may still have replaced a
   deadlocked board and advanced the seeded `Pcg32` stream**. That is not a board
   action and emits no board-cycle event (`ADR-026` item 8; `GAME_EVENTS.md` §3
   item 8); the reshuffled board becomes authoritative through the round's single
   write-back. Proposed clarification text for `BOSS_RULES.md` §4.3 item 4.
   Rejected alternative: score the pre-recovery board and recover only when the
   board action wins — it contradicts the documented order and moves the reshuffle
   to the commit-boundary invariant, producing the same board change later with a
   less legible cause.
3. **Board wins on a tie.** Unchanged and non-negotiable
   (`BOSS_RULES.md:387-389`): a cast must strictly out-score the board action.
   Board-action ties keep lowest `From` then lowest `To` (`BossAiEngine.cs:232-239`).
4. **The "neither action available" branch.** It is a **defined but unreachable**
   branch in the current engine: the board action is available whenever the phase
   runs, because a successful recovery guarantees ≥1 legal swap and a failed
   recovery throws `BoardGenerationFailedException` (`BossAiEngine.cs:212-216`);
   the only no-action state is a Stunned Boss (`GAME_RULES.md:509`), which is
   outside the policy branch and has no MVP content (`BOSS_RULES.md:486-490`).
   Slice 3 must implement the branch exactly as `BOSS_RULES.md:374-375` states
   (no board resolution, no cast, no attack, 18b–18d still run) rather than
   inventing a new failure mode; it must not treat the existing
   `BoardGenerationFailedException` as that branch.
5. **Cooldown decrement → check → set ordering.** Implemented as F4:
   decrement once per committed round after `Turn` advances and before selection
   (`BattleStateService.cs:1399-1418`); check the decremented value as a hard gate
   at selection; set the authored value only on a successful cast in the same
   write-back (`:2290` for the legacy form; the Card form must do the same).
   Moving the decrement after selection would give `N` blocked rounds instead of
   `N−1` and silently retune every authored cooldown — it is a balance change,
   not an implementation detail (see §8.3 and U-12).
6. **A cast-selected phase resolves no Boss board action and no Boss attack.**
   `BOSS_RULES.md:411-418` and `GAME_RULES.md:518`, `:532-541`: no swap, no
   cascade, no `SwapStarted`/`SwapResolved`/match events/`MatchedGemSummary`, no
   actor-relative generation (so no Boss Power gain), no Boss `DamageCalculated`/
   `DamageDealt`/`DamageTaken`; the 18d gate sees no Boss resolution and produces
   no attack. `BossCardCast` is emitted at 18c only. 18b (Passive) still runs.
7. **Determinism under CAS retry and re-resolution.** Selection is a pure
   function of the committed state, the fixed content inputs (Boss definition,
   including the score, and the Boss Card definition), and the fixed candidate
   enumeration order; it consumes zero unseeded RNG and no wall clock. The only
   RNG is the seeded recovery stream, constructed from the retained state on each
   attempt; a refused CAS writes nothing, so its RNG consumption is discarded and
   the retry recomputes the identical result from identical state
   (`BattleStateService.cs:924-1035`). Requirement for Slice 3: resolve the Boss
   Card definition and the score **once** at battle creation (with
   `_bossConfiguration`), not per attempt against a mutable source, so a retry
   cannot observe different content.

### 8.2 Action-selection transition table (per committed response phase)

`form` = active ability form (§7); `CD'` = cooldown after the round's decrement;
`B` = selected board candidate's score; `S` = authored cast score; `E` =
`EffectiveCardCost` (= `PowerCost` for a Boss Card).

**Scope of the policy.** The one-action policy governs the **target (Card) form**.
For an ability that is still on the transitional path, `BOSS_RULES.md` §4.4
explicitly preserves "the pre-migration response order for the abilities not yet
migrated" — so an unmigrated Boss keeps today's board-action-then-Skill order
(row 1). Slice 3 therefore changes behaviour for **the migrated Boss only**;
it does not retro-apply the policy to the other four Bosses, which is what keeps
their existing test suites valid as regression evidence (§7.5).

| # | form | CD' | Power vs E | B vs S | Selected action | 18a board work | 18c cast work | 18d attack |
|---|---|---|---|---|---|---|---|---|
| 1 | Skill | any | — | — | Board action (legacy, unchanged) | yes | legacy gate: fires iff `SkillCharge ≥ req AND CD' == 0` | iff ATK match |
| 2 | Card | > 0 | — | — | Board action | yes | none | iff ATK match |
| 3 | Card | 0 | < E | — | Board action | yes | none | iff ATK match |
| 4 | Card | 0 | ≥ E | S > B | **Cast** | **none** | Boss Card cast | **none** |
| 5 | Card | 0 | ≥ E | S ≤ B | Board action | yes | none | iff ATK match |
| 6 | none available | — | — | — | **no action** | none | none | none |

Rows 2, 3 and 5 are the same runtime path (board action, no cast). Row 6 is the
defined-but-unreachable branch (§8.1 item 4). Row 1 is the transitional path and
must remain byte-identical for unmigrated Bosses.

### 8.3 Cooldown state-transition table (authored value `N`)

| Round | Step 3 (decrement) | Eligibility check | Event |
|---|---|---|---|
| R (cast) | `CD` is 0 → unchanged | passes | cast resolves; write-back sets `CD := N` |
| R+1 | `N → N−1` | fails while `> 0` | board action only |
| … | … | … | … |
| R+N | `1 → 0` | passes | cast can resolve again |

With the documented ordering, **`N` blocks `N−1` subsequent rounds** and the Nth
round is eligible again; `N = 0` means eligible every round (Earthquake). This is
what the code and `BossResponseTests.cs:1406-1409` assert. The inline comment
claiming `N` blocked rounds (`BattleStateService.cs:2280-2282`) is wrong and is
reported, not fixed. Whether the authored `2`/`3` values are meant to read as
"N blocked rounds" is a **balance/interpretation decision (U-12)**; the
decrement/check/set *ordering* itself is fixed and must not move.

### 8.4 Cast-phase write table (row 4, Earthquake)

| Member | Write |
|---|---|
| `BossState.Power` | `− EffectiveCardCost` (exactly once) |
| `BossState.ActiveStatusEffects` | `ApplyShield(refresh-replace, Shield, Boss source, magnitude, ShieldDepletedCondition)` |
| `BossState.SkillCooldown` | `:= authored value` (overrides the round's decrement, same write-back) |
| `BossState.SkillCharge` | unchanged (no board resolution ⇒ no accrual; no reset) |
| `BossState.HP / MaxHP / ATK / DEF / State / Passive*` | unchanged by the cast (18b may still change Passive/State) |
| `PetState` | unchanged by Earthquake (self effect) |
| `BoardState` | unchanged, **except** a pre-selection deadlock reshuffle if the board was deadlocked |
| `RngState` | unchanged, **except** the seeded reshuffle above |
| `Turn` / `Sequence` | advanced once by the round's normal write-back (not by the cast) |
| Events | `BossCardCast` at 18c; plus 18b Passive events if triggered; **no** Boss board-cycle or Boss damage events |

---

## 9. Decision F — Revised ADR-029 dependency graph and slice exit criteria

### 9.1 Assessment of the current plan

`ADR-029` §4 orders Slices 1→2→3→4→5 with the constraints "Slice 2 before 4,
Slice 3 before 4, Slice 1 before 3, Slice 5 last". That graph is sound but
**incomplete**: Slice 3's own exit condition ("both action kinds are presented
from their own events") cannot be met unless a Boss Card definition exists in the
production runtime — and `ADR-029` §2.5 item 15 explicitly left persistence out of
scope. Without a definition/content/provisioning prerequisite, Slice 3 would have
to either invent content (forbidden) or be declared complete on Domain fixtures
alone (a false completion). The candidate sequence in the brief is correct: an
explicit prerequisite between Slice 2 and Slice 3 is required.

### 9.2 Proposed revised graph

```text
[Slice 1] Boss Power foundation        DONE (TASK-304)
    │
[Slice 2] Shared actor-aware cast      DONE (TASK-305)
    │
[P] Definition / content / provisioning prerequisite          ← NEW
    │   approved carrier (Decision A) + identity resolution (U-8)
    │   + one authored Boss Card (U-2/U-10) + score carrier (U-9)
    │   + data-only provisioning migration + Boss-side lookup
    │
[Slice 3] One-action policy + cooldown + BossCardCast + wire + client + round integration
    │
[Slice 4] Remaining four abilities, each only after an approved vocabulary extension
    │   (flame-burst → root → drain-power → thunder-strike last)
    │
[Slice 5] Retire SkillCharge / BossSkillCast / legacy handler after the four-part parity gate
```

Edges: P needs Decisions A/B/C/D approved; Slice 3 needs P and Slice 1/2; Slice 4
needs Slice 3 and the vocabulary design; Slice 5 needs Slice 4 plus parity.
`P` is a **content/provisioning** slice, not a runtime-policy slice: it enables no
selection, emits no event and changes no gameplay, which is what makes it
independently verifiable.

### 9.3 Executable exit criteria (the distinction the brief requires)

| Slice | Exit criterion | What it does **not** prove |
|---|---|---|
| 1 (done) | Boss `Power` generated, clamped, serialized, projected, rendered, reconnect-restored; `SkillCharge` untouched | no cast, no policy |
| 2 (done) | One cast contract serves both actors; Player path byte-identical; Boss cast validates and resolves against `BossState.Power` — **Domain tests with synthetic fixtures only** | no production Boss Card exists; no selection; no event |
| **P** | A real `BossCard` row is provisioned, loaded by the production lookup at battle start, validated fail-closed, and its identity/cost/effect round-trip is asserted against the owning documents; the Player loadout/collection surfaces still expose nothing | no runtime selection or cast; no client change |
| **3** | With the provisioned Boss Card: the Boss selects exactly one action per phase deterministically; board row paths unchanged for unmigrated Bosses; a cast phase emits `BossCardCast` on the real wire, deducts Power once, sets cooldown, resolves the Shield, and produces no Boss board resolution/attack; the client renders the cast from the real event; CAS retry produces one commit/deduction/event | **not** end-to-end for an unprovisioned boss; `SkillCharge` still exists; four abilities unmigrated |
| **4** | All five abilities migrated to Boss Cards with the same authored effect/target/duration as their legacy form, each after an approved vocabulary extension, with old/new parity evidence — including Thunder Strike's **exact qualifying attack** and **modifier-consumption timing** comparison | `SkillCharge`/`BossSkillCast` still present; no retirement |
| **5** | The four-part parity gate (`ADR-029` §2.9) met: all abilities migrated and tested, old/new compared, every consumer reconciled, ADR/index amendments recorded; `SkillCharge`, `BossSkillCast` and the legacy secondary-effect handler removed; `SkillCooldown` retained | — |

**Explicit statement:** Slice 3 must not be reported as end-to-end complete
unless the production runtime executes an actually provisioned Boss Card. Domain
fixtures prove the contract (Slice 2 already did); they do not prove a runtime
path, a wire event, a client presentation, or a provisioned definition.

### 9.4 ADR-029 amendments the approval would carry (proposal only)

1. §3 gains U-7…U-12 (§10).
2. §4 gains slice `P` and the executable exit criteria above; §4's ordering
   constraints are extended with "P before 3".
3. §2.5 item 15 ("persistence is out of this decision's scope") is superseded by
   the recorded U-7 decision.
4. §2.7 item 19's cast-phase consequence is unchanged; a clarification is added
   that a cast-selected phase may still carry the pre-selection deadlock reshuffle
   (§8.1 item 2).
5. §1.2/§1.3's "verified implementation facts at the decision point" are
   historical snapshots and are annotated as such where they no longer match
   source (they are the decision-time record; they are not restated as current).

---

## 10. Decisions versus unresolved operator decisions

### 10.1 Decided by existing authority (no new approval needed)

| # | Decision | Authority |
|---|---|---|
| D1 | Exactly one action per response phase; never both | `ADR-029` §2.7 item 17; `BOSS_RULES.md` §4.3; `GAME_RULES.md` §17 step 18 |
| D2 | Selection at 18a; board at 18a; Passive 18b; cast 18c; attack check 18d | `BOSS_RULES.md` §4.3 item 2; `GAME_RULES.md:509-545` |
| D3 | Board wins ties; board ties keep lowest cell index | `BOSS_RULES.md` §4.3 items 6–7 |
| D4 | Determinism: zero unseeded PRNG, no clock, no client input, fixed enumeration | `BOSS_RULES.md` §4.3 item 1; `ADR-026` item 8; `TDD.md` §6 |
| D5 | Cast selected ⇒ no Boss board resolution, no generation, no attack | `BOSS_RULES.md` §4.3 item 4; `GAME_RULES.md:518`, `:532-541` |
| D6 | Cooldown is a hard gate, decremented once per round, set on cast, `SkillCooldown` retained | `GAME_STATE.md` §2.4.3; `BOSS_RULES.md` §4.2 item 5, §4.3 item 3 |
| D7 | One Boss Card per Boss; identity = the ability `SkillId`; no loadout/ownership/collection | `CARD_RULES.md` §1.1; `ADR-029` §2.5 items 12–15 |
| D8 | Shared cast contract; empty modifier set; `BossState.Power` cost carrier | `CARD_RULES.md` §3 item 9; `BOSS_RULES.md` §4.2 items 2–4 |
| D9 | Dedicated `BossCardCast` (cardId + sourceId); Player `CardCast` unchanged | `GAME_RULES.md` §16; `GAME_EVENTS.md` §2; `SIGNALR_PROTOCOL.md` §3.2.30 |
| D10 | Earthquake's effect is a self, trigger-based Shield with no Turn duration | `BOSS_RULES.md` §6.3.1 item 4; `COMBAT_RULES.md` §4 |
| D11 | `SkillCooldown` is not an ability-identity or migration-state signal | `GAME_STATE.md` §2.4.3; `BOSS_RULES.md` §4.3 item 3 |
| D12 | No new event, hub method, state member or wire member is needed for the policy beyond `BossCardCast`, which is already defined | `BOSS_RULES.md` §7; `SIGNALR_PROTOCOL.md` §3.2.30 item 4 |

### 10.2 Unresolved operator decisions (the smallest decision set needed to continue)

Existing IDs stay as `ADR-029` §3 defines them; proposed new IDs are marked
"(new)" and take effect only if the operator accepts this report.

```text
U-2   Per-ability PowerCost. Still unresolved. Blocks the provisioning row and
      therefore Slice P/3. Narrow request: author the `earthquake` PowerCost as
      an integer 0..100 (and later the other four).
U-3   Cast-priority score values (and the paced effect of no-attack cast phases).
      Still unresolved. Narrow request: author five per-Boss integer scores on
      the scale defined in §5.2.
U-4   Outstanding magnitudes. For this slice only: confirm `earthquake`'s Shield
      magnitude (the implemented value is 150 flat) or author another. The other
      three magnitudes stay unresolved for Slice 4.
U-5   Whether the carried-over cooldowns are retuned. Still unresolved and not
      needed for the Shield slice (`earthquake` carries 0).
U-6   Retirement timing of SkillCharge's metric/clamp questions. Unchanged;
      closed by retirement, not answered.
U-7   (new) Authorize Boss Card content provisioning, Option A (reuse
      CardDefinition, data-only migration, no schema change).
U-8   (new) Resolve the identity value-form conflict: amend DATABASE.md §1/§3 to
      exempt a BossCard row's CardDefinitionId from the `card-<kebab>` form and
      set it to the ability SkillId (per CARD_RULES.md §1.1 item 3), OR direct a
      different resolution. This is an authoritative-document conflict
      (AGENTS.md §4) and cannot be settled silently.
U-9   (new) Authorize the cast-priority score carrier: a `CastPriorityScore`
      integer column on BossDefinition, including the ADR-024 amendment
      (four balance values → five), the DATABASE.md §1/§3 addition and the
      schema migration.
U-10  (new) Authorize the first Boss Card's authored content: PowerCost (U-2)
      and Shield magnitude (U-4) for `earthquake`, plus its display `Name`.
U-11  (new) Authorize the active-form migration signal: provisioned-content
      presence resolved once per battle (Decision D), and the enablement of the
      first Boss Card for Sơn Thạch Vệ only.
U-12  (new) Confirm the cooldown turn-count semantic: the documented/implemented
      ordering makes an authored N block N−1 subsequent rounds. Either ratify
      that reading (and leave the authored 2/3 values as they are) or direct a
      balance change to the values — the ordering itself does not change.
```

**Nothing above is assumed approved.** No value is recorded in `docs/`, no row is
provisioned, no code is written.

---

## 11. Exact files requiring documentation changes (proposed, not applied)

Ordered by decision; each row names the owning section, because the concept's
owner must make the change (`documentation-change.md` §2).

| # | File | Section | Change | Decision |
|---|---|---|---|---|
| 1 | `docs/02-technical/DATABASE.md` | §1 `CardDefinition` block; §3 constraint list | Add `BossCard` to the stored category set; record the identity value-form exception (U-8); record that a `BossCard` row persists `LoadoutCopyLimit` but it is never read | A |
| 2 | `docs/02-technical/DATABASE.md` | §5 item 4 | Record the Boss Card provisioning mechanism, its insert-only rule, and the provisioned-row set/count | A |
| 3 | `docs/01-game-design/CARD_RULES.md` | §1.1 items 3 and 5 | Item 3: cross-reference the `DATABASE.md` identity exception. Item 5: replace "no persistence contract is added here" with the authorized provisioning contract | A |
| 4 | `docs/02-technical/DATABASE.md` | §1 `BossDefinition` block; §3 constraints | Add `CastPriorityScore` (`int`, NOT NULL, `>= 0`) if U-9 is approved | B |
| 5 | `docs/03-decisions/ADR/ADR-024-…md` | Decision/overlay contract | Extend the "four balance values" overlay to five, or record why the score is not an overlay value | B |
| 6 | `docs/01-game-design/BOSS_RULES.md` | §4.3 item 5; §4.3 step 4 | Record U-3's resolution (carrier, scale, valid range, fail-closed validation) and that the values are authored content | B |
| 7 | `docs/01-game-design/BOSS_RULES.md` | §6.3.1 item 4 | Record the `earthquake` Shield magnitude as an authored value once U-10 is approved | C |
| 8 | `docs/01-game-design/BOSS_RULES.md` | §4.4 (new item) | Add the one-active-form rule, the content-presence signal, the once-per-battle resolution and the no-fallback rule | D |
| 9 | `docs/01-game-design/BOSS_RULES.md` | §4.3 item 4 (clarification) | State that a cast-selected phase may carry the pre-selection deadlock reshuffle and its seeded-RNG consumption, and emits no board-cycle event | E |
| 10 | `docs/02-technical/GAME_STATE.md` | §2.4 / §2.4.3 (note) | State that the Boss Card definition and the active form are battle-start **content inputs**, not state members (no Redis/wire change) | D |
| 11 | `docs/03-decisions/ADR/ADR-029-…md` | §2.5 item 15; §3; §4 | Supersede the "persistence out of scope" clause; add U-7…U-12; add slice P and the executable exit criteria | A–F |
| 12 | `docs/02-technical/ARCHITECTURE.md` | §3 component table | Add the `CardDefinition` / `CardRepository` boundary row; name the Boss Card resolution point beside `BossDefinitionLookup` | A |
| 13 | `docs/03-decisions/README.md` | ADR index / supersession block | Record the ADR-029 amendment if it is filed as an amendment rather than a new ADR | A–F |
| 14 | `docs/02-technical/SIGNALR_PROTOCOL.md`, `GAME_EVENTS.md`, `MVP_SCOPE.md`, `REDIS_STATE.md` | — | **No change required**: `BossCardCast`, `bossState.power` and the Boss Card capability are already defined; no new state/wire member is proposed | — |

Not in this list, because they are implementation comments rather than
authoritative contracts: `BattleHub.cs:366-372` (three-member text),
`BattleStateService.cs:2280-2282` (cooldown comment), `CardCategory.cs` /
`CardDefinition.cs` persistence notes. They are reported in §13 as separate debt.

---

## 12. Proposed follow-up implementation tasks and dependencies

Proposals only. **No task record is created here** (the brief and
`tasks/README.md` §6 require the normal intake workflow, and the WIP limit is 1
until TASK-306 is filed). IDs are the next available numbers after TASK-306 and
must be confirmed at intake.

| Proposal | Working title | Type | Depends on | Delivers |
|---|---|---|---|---|
| P-1 | Record the operator decision set and amend the owning documents | DOCUMENTATION / ARCHITECTURE | TASK-306 (this report), operator approval of U-7…U-12 | The §11 document changes; the ADR-029 amendment; no code |
| P-2 | Provision the first Boss Card and its Boss-side resolution | FEATURE (persistence/gameplay) | P-1 | Data-only migration for `earthquake`; Boss Card resolution at battle creation; fail-closed validation; provisioning tests |
| P-3 | Implement the one-action policy, cooldown integration and `BossCardCast` | FEATURE (gameplay/backend/realtime) | P-2 | Selection function; 18a/18c branches; Power deduction; cooldown set; `BossCardCast` enum+wiring+wire projection; Application/Api/Domain tests; balance-metric impact |
| P-4 | Present `BossCardCast` on the client | FEATURE (client) | P-3 | Parser, phase kind, callout, hold, display-name resolver (reuse `BOSS_SKILL_NAMES`), closed-set tests, scene tests |
| P-5 | Design the effect-vocabulary extension for the remaining four abilities | GAMEPLAY-CHANGE (design only) | P-1 | Approved `effectType`/semantics for ATK buff/debuff, flat Power drain, next-qualifying-attack buff; `DATABASE.md`/`CARD_RULES.md` changes |
| P-6a…P-6d | Migrate `flame-burst` → `root` → `drain-power` → `thunder-strike` (last) | FEATURE/GAMEPLAY-CHANGE per ability | P-5, P-3 | Four rows + parity evidence per ability; `thunder-strike` needs the qualifying-attack/modifier-consumption comparison |
| P-7 | Retire `SkillCharge`, `BossSkillCast` and the legacy handler | REFACTOR / DOCUMENTATION | all P-6, four-part parity gate | Removal + consumer reconciliation + ADR/index amendment |
| P-8 | Harden the Signature-Skill derivation against a non-`PetSkill` category | BUG (separate intake) | none | A category guard in `CardLoadoutService.cs:204-217` (currently only a null check), so a mis-provisioned Boss Card can never be snapshotted as a Pet signature skill |
| P-9 | Reconcile the Boss `BaseDamage` provisioning drift and the stale implementation comments | BUG / DOCUMENTATION (separate intake) | none | The F13 drift, the two known failing Infrastructure tests, the F19 comments |

Dependencies that must not be reordered: P-1 → P-2 → P-3 → P-4; P-5 before any
P-6; P-6 before P-7; `thunder-strike` last among P-6. P-8 and P-9 are independent
and should not be bundled into the migration slices (`AGENTS.md` §16).

---

## 13. Risks and reported defects (not fixed here)

```text
R-1  U-3/U-2/U-4 values are the critical path. Without them Slice P cannot
     provision a valid row and Slice 3 cannot be implemented (BOSS_RULES.md §4.3
     item 5). The design is ready; the values are not.
R-2  A cast phase forgoes the Boss's board action and attack (documented, not a
     defect). Its pacing effect is U-3's unresolved item and cannot be assessed
     until the cadence values exist.
R-3  The cast-score carrier expands ADR-024's overlay contract (four → five
     values). If the operator prefers no schema change, the alternative carrier
     must be chosen explicitly and BOSS_RULES.md §4.3 step 4 amended.
R-4  Fail-closed content resolution interacts with the existing in-memory
     `_bossConfiguration` cache (F14): a Redis-recovered battle handled by a
     different process already faults today; the Boss Card path must not add a
     second recovery assumption. Out of scope to fix, must be documented.
R-5  The balance harness pins charge-based cadence in five per-seed rows (F18).
     Slice 3 will change those numbers; they must be re-derived, and the protected
     artifacts must not be regenerated in place.
R-6  The Signature-Skill derivation has no category guard (F8/P-8): a
     mis-provisioned Boss Card row could be snapshotted as a Pet signature skill.
     Provisioning discipline is not a guard; P-8 is.
R-7  Pre-existing doc/implementation drift on Boss `BaseDamage` (F13) and the two
     known failing Infrastructure tests will surface in Slice 4 parity work.
R-8  A cast-selected phase may still reshuffle a deadlocked board (F3, §8.1 item
     2). Deterministic, but the client cannot identify the reshuffle by design
     (ADR-026 item 8); this must be stated in BOSS_RULES.md §4.3 item 4 so it is
     not later mistaken for a defect.
R-9  Cooldown `N` vs `N−1` ambiguity (F4, U-12) can silently retune all five
     abilities during migration if it is "fixed" in passing.

Reported, not fixed (AGENTS.md §16):
- BattleHub.cs:366-372 "three-member" projection text vs the four-member record.
- BattleStateService.cs:2280-2282 cooldown comment vs code and tests.
- ADR-029 §1.2/§1.3 decision-time snapshots that no longer match source
  (BossState now has 12 positional members; the cast executor is actor-aware).
- Source comments that still state the Card category set is two-valued, and the
  two pre-existing failing test families recorded in TASK-305 §8.
```

---

## 14. Final verdict

**`READY FOR OPERATOR APPROVAL`** — design.

The design blockers the readiness pass identified are resolved *as design
decisions*: the definition source (Option A, conditioned), the U-3 contract
(type/scale/range/comparison/tie/RNG/validation, with values unresolved), the
first vertical slice (Earthquake→Shield, fully shaped, values exposed), the
active-form migration contract, the action-selection and cooldown reconciliations
with state tables, and a reconciled slice graph with executable exit criteria.

**Runtime implementation remains `BLOCKED`** on the operator decisions U-2,
U-3 (values), U-4/U-10 (Earthquake content), U-7 (provisioning authorization),
U-8 (identity value-form conflict), U-9 (score carrier) and U-11 (active-form
signal) — plus U-12 if the cooldown reading needs ratification. These are product
and balance decisions, not design gaps: `AGENTS.md` §7 forbids inventing them, and
this report records them rather than manufacturing approval.

The smallest decision set that unblocks work:

```text
1. Approve Decision A (Option A) and resolve the identity value-form conflict
   (U-7, U-8).
2. Author `earthquake`'s PowerCost (U-2) and confirm/replace its Shield magnitude
   (U-4, U-10).
3. Approve the score carrier (U-9) and author the five per-Boss scores (U-3).
4. Approve the active-form rule and the first-Boss enablement (U-11).
5. Confirm the cooldown turn-count reading (U-12).
```

After those, P-1 through P-4 unblock in order, and Slice 3 can be planned with a
provisioned definition rather than a fixture.
