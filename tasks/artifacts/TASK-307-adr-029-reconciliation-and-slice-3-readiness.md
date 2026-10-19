# TASK-307 — ADR-029 Reconciliation and Slice 3 Readiness

**Deliverable type:** consolidated reconciliation report (artifact). Not an
authoritative source: every rule statement cites the owning `docs/` section or the
source location it was verified against. No rule, value, schema or wire contract
is authored here.
**Date:** 2026-10-19
**Repository baseline:** branch `master`, HEAD `94d40d0a0a15fa3da195ac3cf851d527f855acd3`,
working tree clean at audit start, upstream `origin/master` (`ahead 2` — the two
TASK-306 commits; not pushed).
**Verdict:** `READY FOR OPERATOR APPROVAL` (see §10). Runtime implementation and
`docs/` application remain blocked at the three operator decision points in §4.2.
**Task-count impact:** +1 lifecycle task now (TASK-307); +1 proposed after
approval (the single Slice 3 task); no other task registered.

---

## 0. Decision at a glance

| # | Contract area | State after this reconciliation |
|---|---|---|
| A | Boss Card definition and provisioning | **Contract settled, authorization pending (OD-1):** `CardDefinition` is the canonical contract, category `BossCard`, identity = the ability `SkillId` (requires the `DATABASE.md` exception), one data-only EF-migration row, no new table, no `PlayerUnlockedCard`, no loadout/collection/API visibility |
| B | U-3 cast-priority score | **Contract settled, carrier + value pending (OD-3):** per-Boss authored score on the Boss definition, nullable column (`NULL` = not authored), compared to the selected board candidate's own score, board wins ties, zero unseeded RNG, fail-closed when a migrated Boss lacks it. Only `earthquake`'s value is needed for Slice 3 |
| C | Initial Boss Card | **Shape settled, two values pending (OD-2):** `earthquake` → self, trigger-based `Shield`, no Turn duration, cooldown 0, magnitude `150 Flat` implemented-but-unrecorded (confirm or replace), `PowerCost` unauthored |
| D | Active form and cooldown | **Settled by authority + OD-1's form-signal authorization:** one form per ability; form = provisioned Boss Card presence, resolved once per battle; cooldown ordering is already documented/implemented/tested (decrement → check → set; authored `N` blocks `N−1` rounds); U-12 is **not** an operator decision |
| E | One-action policy and atomicity | **Settled by authority:** full policy is already normative in `BOSS_RULES.md` §4.3 and `GAME_RULES.md` §17 step 18; this report fixes the remaining implementation-level ambiguities (18c re-check, event batch order, `PowerChanged` non-applicability, `SkillCharge` inertia) |
| F | Event/wire surface | **Settled by authority, absent in code:** `BossCardCast` is already canonical and fully specified; Slice 3 adds the enum member, factory, projection arm and client presentation. No new wire member, hub method, version or state field |
| G | Slice graph | **Consolidated:** TASK-306's separate "Slice P" is folded into the single end-to-end Slice 3 task (one approval boundary, one acceptance boundary); Slice 4/5 unchanged |

**One-sentence outcome:** the Boss Card contract is now coherent and verifiable
end to end, TASK-306's decision set shrinks from ten items to **three operator
decisions**, and no additional lifecycle task is needed until those three are
answered.

---

## 1. Preflight evidence

### 1.1 Baseline (verified in-session)

```text
git rev-parse --abbrev-ref HEAD        master
git rev-parse HEAD                     94d40d0a0a15fa3da195ac3cf851d527f855acd3
git status --porcelain                 empty at audit start (clean)
upstream                               origin/master, branch ahead 2 (the TASK-306 commits)
HEAD subject                           docs: file the adr-029 slice boundary reconciliation completion record
recent history                         94d40d0 (TASK-306 Phase B), ccecba1 (TASK-306 Phase A),
                                       0551386 (TASK-305 Phase B), 3e2518d (Slice 2)
task folders                           backlog/ 0 md, active/ 0 md, blocked/ 0 md,
                                       completed/ 308 md, artifacts/ 5 files
highest existing task ID               TASK-306  ->  this task is TASK-307
ADR set                                29 ADR files; ADR-029 Accepted; no ADR-030
```

`git diff --name-only 0551386 HEAD` (the delta since the cache's recorded SHA) is
exactly the TASK-306 artifact, its completion record and `.ai/PROJECT_STATE.md`.

### 1.2 Duplicate-record search

`TASK-307` was searched for across the repository (excluding `.git`, `bin`,
`obj`, `node_modules`) — **0 matches**. No TASK-307 record, artifact or partial
work exists. `TASK-307` is therefore the next sequential ID
(`tasks/README.md` §3: highest existing ID + 1), created as
`tasks/active/TASK-307-…md`; no existing record is overwritten.

### 1.3 `.ai/PROJECT_STATE.md` freshness decision

The cache records `Audited HEAD SHA 0551386` and `STALE`; HEAD is `94d40d0`, so
`core/task-selection.md` §2 selects **INCREMENTAL** (recorded SHA ≠ HEAD), not
FAST. The delta is bookkeeping only (TASK-306 artifact + record + cache), and the
recorded lifecycle facts were re-derived from Git rather than trusted. Per Step 5
the cache is refreshed by this task (declared file `.ai/PROJECT_STATE.md`).

### 1.4 Lifecycle classification

The primary output is a report artifact plus the navigation-cache refresh; no
`docs/` file is authorized because the consolidated contract is not approved
(`AGENTS.md` §4 step 6, §7, §17). Type `DOCUMENTATION`
(`tasks/TASK_TYPES.md` §2), risk HIGH, workflow
`documentation/documentation-change.md` with `architecture/adr-change.md` as a
**proposal only** — the same single-type structure TASK-303 and TASK-306 used.

### 1.5 Method and limits

Direct read-only inspection of the owning `docs/` sections and the source they
govern, plus the tests that pin the legacy behaviour. **No test suite was run**
(no code changed, so no test result could differ); no network; no Git writes
before the lifecycle commits. Where this report says "verified", it means
file-and-line inspection, not execution. Citation shorthand: `path:NN` is a
repository-relative path and 1-based line.

---

## 2. Verification of TASK-306 — what still holds, what was corrected

TASK-306 is the primary investigation artifact, not authority. Every finding this
reconciliation relies on was re-checked. Its central conclusions hold; four
attributions/scope claims do not.

### 2.1 Confirmed (re-verified)

| TASK-306 claim | Verification |
|---|---|
| One-action policy is documented but not implemented | `BOSS_RULES.md:355-423` and `GAME_RULES.md:506-545` are normative; runtime runs the board action unconditionally (`BattleStateService.cs:1746-1800`) and may additionally fire the legacy Skill (`:2087-2095`) |
| Board score is `BossMoveSelectionResult.Score` over first-pass legal swaps, weights 10/5/20/10, no tier multiplier | `BossAiEngine.cs:13-33`, `:148-173`; selection highest score then lowest `From`/`To` at `:219-247` |
| Deadlock recovery precedes scoring and throws on failed recovery | `BossAiEngine.cs:197-217` (`BoardGenerationFailedException` at `:214-215`) |
| Cooldown order is decrement → check → set | `BattleStateService.cs:1399-1418`, `:2087-2088`, `:2287-2291`; the `N−1` reading is asserted at `BossResponseTests.cs:1398-1509` |
| Shared cast contract resolves a Boss self-Shield | `CardCastExecutor.cs:582-774`; Shield at `:669-695`; representability gate before deduction at `:621-623`, `:814-849` |
| `BossCardCast` exists on paper only | canonical in `GAME_RULES.md:398,424-433`, `GAME_EVENTS.md:772-804`, `SIGNALR_PROTOCOL.md:1723-1778`, `BOSS_RULES.md:1009-1011`; absent from code (enum `BattleEvent.cs:52-344`, highest member `MatchedGemSummary = 19`; no factory; no projection arm in `BattleEventWireProjection.cs`) — the wire DTO already has `cardId`/`sourceId` slots (`:229-240`) |
| No Boss-side card lookup path | `CardRepository.GetDefinitionAsync` is id-only (`CardRepository.cs:123-131`); `ICardDefinitionLookup` mirrors it; `CardCastActor.Boss` appears only in the dispatch arm `CardCastExecutor.cs:119` |
| `CardDefinition` category is an int with no DB CHECK; `BossCard = 2` Domain-only | `CardDefinitionConfiguration.cs:65-73`; migration `20260925150903_AddCardPersistence.cs:34` (`Category` `integer`); `CardCategory.cs` (`BossCard = 2`) |
| Earthquake's Shield magnitude is implemented but unrecorded | `BossDefinitions.cs:272-296` (`Magnitude = 150` at `:286`); `BOSS_RULES.md:897-901` records it as "Non-blocking balance tuning parameter"; ADR-029 §3 U-4 |
| Provisioning mechanism is an insert-only EF migration; 8 `CardDefinition` rows | `DATABASE.md:1673-1753` §5 item 4; `20260929152651_ProvisionPetCardRelicContentDefinitions.cs`, `20261004055006_ProvisionThanhXaAndSonHungSignatureSkills.cs` |
| Effect vocabulary is closed; only Shield is expressible for the five abilities | `DATABASE.md:1534-1545`; `CardEffectType.cs`; `CardCastExecutor.cs:814-849` |
| Client cannot present `BossCardCast` | 0 occurrences in `src/frontend`; presenter parses 18 in-battle types (`BattleEventPresenter.ts:4-30, 288-329`), maps `BossSkillCast` at `:311-312, 787-793, 1416-1421, 1854-1859`; scene hold sites `BattleScene.ts:5415-5421, 5633-5636, 5754` |
| Legacy behaviour is pinned by many tests | e.g. `BossResponseTests.cs:1117-1224`, `AtomicRoundOrchestrationTests.cs:1026-1064`, `BossSkillSecondaryEffectTests.cs:451`, `BossResponseWireTests.cs:330-391` |
| Boss content input is cached per battle; no rehydration | `BattleStateService.cs:326, 557, 585-588, 1692` |
| CAS retries recompute the whole phase from fresh state | `BattleStateService.cs:924-1035` (`MaxAttempts = 8`) |
| `BossDefinitionLookup` overlays four balance columns from PostgreSQL, fails closed | `BossDefinitionLookup.cs:112-148`; ADR-024 §2.2 |

### 2.2 Corrections to TASK-306 (each is a reported finding, not a fix)

**C-1 — F13 mis-attributed the `BaseDamage` drift.** TASK-306 said "the
provisioned `SkillDefinition` JSON and `BossDefinitions` carry 150/120/100/150/180
(`BossDefinitions.cs:124,171,212,274,322`)". Current source is unambiguous: the
`BossDefinitions` static baseline declares `BaseDamage: 0` for all five abilities
(`BossDefinitions.cs:125, 172, 213, 275, 323`), matching `BOSS_RULES.md:858-865`
and `ADR-026` D3; the legacy values survive only in the two provisioning
migrations' JSON (`20260926151112_ProvisionBossDefinitions.cs:71,82,91`;
`20261004094100_ProvisionTwoRemainingMvpBosses.cs:118,131`). The Domain baseline
is what the runtime overlays (`BossDefinitionLookup.cs:112-148`), and
`SkillBaseDamage` is exposed but never read by resolution
(`BossDefinition.cs:273`; no consumer in Application), so the drift is inert at
runtime but breaks the JSON round-trip assertions. This is exactly the
pre-existing failure family TASK-305 §8 recorded
(`BossDefinitionPostgresProvisioningTests.Postgres_ShouldRoundTripBothJsonDocumentsToTheDomainDefinitions`
and `BossDefinitionBalanceLookupTests.GetDefinitionByIdentityAsync_ShouldPreserveTheNonPersistedSecondaryEffects`,
`TASK-305` record lines 464-476). TASK-306's *conclusion* (a pre-existing,
non-blocking content/provisioning drift) stands; its *attribution* does not.

**C-2 — The "14 documentation changes" are not 14 changes.** TASK-306's §11 table
row 14 already said "no change required" for four documents, and the table
conflated candidate edits with one row of non-edits. Revalidated below (§5) the
post-approval set is **9 owned edits across 8 files**, plus one candidate
(`API_CONTRACTS.md` §5.3) that must be recorded as an explicit no-change. None is
applied now.

**C-3 — U-12 is not an operator decision.** TASK-306 framed "`N` blocks `N−1`
rounds" as needing ratification. The ordering is already fixed by authority —
"decrements by exactly 1 when the committed Round's write-back advances `Turn` by
1" (`GAME_STATE.md:2380-2383`), "set to the ability's authored cooldown value in
the same single write-back as a successful cast" (`:2386-2388`), "while `> 0` the
cast is unavailable" (`:2384-2385`), and `BOSS_RULES.md:840-849` defines the
cooldown as "turns remaining … blocked while `CD > 0`" — and by the implementation
and its regression test (`BattleStateService.cs:1399-1418`,
`BossResponseTests.cs:1398-1509`). The stale inline comment at
`BattleStateService.cs:2278-2282` contradicts both; it is a **code-comment defect**
to correct inside Slice 3 (the file is touched anyway), not a design decision.
Retuning the authored `2`/`3` values is a *different* question (U-5) and is not
needed for `earthquake` (`CD = 0`).

**C-4 — U-3 did not need all five values now, and U-9's non-nullable column would
have forced them.** TASK-306 recommended a non-nullable `CastPriorityScore` and
listed "the five per-Boss scores" as one decision. A `NOT NULL` column would
require authoring balance values for four Bosses whose abilities are not being
migrated until Slice 4 — premature content, and a fabricated `0` default would
violate ADR-024's fail-closed/no-fallback discipline (`ADR-024` §2.2 item 3).
The reconciled carrier is **nullable** (`NULL` = "no cast-priority score
authored"), validated fail-closed only when a Boss actually has a Boss Card; the
Domain already has the identical documented null-marker precedent for
`BossPassiveDefinition.Threshold` (`BossDefinitions.cs:82-95`;
`DATABASE.md` §1 note item 3). Slice 3 therefore needs exactly **one** authored
score (`earthquake`), not five.

**C-5 — Do not insert a separate "Slice P" lifecycle task.** TASK-306 proposed a
definition/content/provisioning slice between Slice 2 and Slice 3. That work has
no independent acceptance boundary: `ADR-029` §4's Slice 3 exit condition ("both
action kinds are presented from their own events", `ADR-029:352-358`) cannot be
met without a provisioned Boss Card, and the brief's task-count discipline
forbids splitting dependent steps of one deliverable. The prerequisite is folded
into the single Slice 3 task as its first ordered work items (§6–§7), and
`ADR-029` §4 is amended on approval to say so.

**C-6 — `PowerChanged` is not the Boss Power event.** TASK-306's cast-phase
write/event tables listed `BossCardCast` and "no Power-related event" only
implicitly. The owner is explicit: `PowerChanged`'s `power` member is
`PetState.Power` (`GAME_EVENTS.md:542-546`; `SIGNALR_PROTOCOL.md:1295-1310`), and
its `"boss"` source means a **Boss-owned mutation of the Pet's Power** (Drain
Power), not a mutation of `BossState.Power`. A Boss Card's Power **spend** must
therefore emit **no** `PowerChanged`; the resulting Boss Power is reported by the
`bossState` projection (`ADR-029:293-304`; `SIGNALR_PROTOCOL.md:1753-1759`). A
future Boss Card that mutates the **Pet's** Power raises a real `source`-value
question (`"card"` vs `"boss"`) that Slice 4 must settle; it does not affect
`earthquake`.

**C-7 — `BossCharge` accrual must be stated as inert for a migrated Boss.**
`ResourceGenerator` will keep charging `SkillCharge` on the migrated Boss's board
phases unless it is changed. Slice 3 must not change it (the member is retirement-
gated), so the contract states that a migrated Boss's `SkillCharge` is written
only by the existing transitional generator, is never read as a gate, and is
retired in Slice 5. Without that statement a reviewer could mistake the dead
counter for a second active resource.

---

## 3. Consolidated Boss Card contract

This section is the single reconciled contract. Sections marked **(OD-n)** depend
on an operator decision in §4.2; everything else is settled by the cited
authority and is what Slice 3 must implement.

### 3.1 A — Definition, identity, provisioning

```text
Canonical contract   CardDefinition (Domain entity + PostgreSQL row)
Category             CardCategory.BossCard (= 2)
CardDefinitionId     the ability's SkillId from BOSS_RULES.md §6.4 (e.g. "earthquake")
Name                 the ability's documented display name (e.g. "Earthquake")
PowerCost            authored integer (OD-2); EffectiveCardCost == PowerCost
LoadoutCopyLimit     1 — persisted only because the column is required and
                     non-nullable; never read for a BossCard row
EffectDefinition     structured array, one element per effect (Shield for earthquake)
Persistence          one data-only EF-migration INSERT, insert-only, mirrored Down;
                     no HasData, no seed runner, no runtime provisioning
Ownership/visibility no PlayerUnlockedCard row, no loadout slot, no collection
                     projection, no API exposure, no Player cast
Boss-side resolution resolved at battle creation by CardDefinitionId ==
                     bossDefinition.SkillId through the existing id-only lookup,
                     cached with the Boss content input for the battle
```

1. **One identity, no second identifier.** `ADR-029:194-197` and
   `CARD_RULES.md:163-168` fix the Boss Card's `CardDefinitionId` to the ability
   `SkillId`. The wire contracts already assume that equality:
   `BossCardCast.cardId` and `BossSkillCast.skillId` are "the same string for the
   same Boss ability" (`SIGNALR_PROTOCOL.md:1744-1748`; `GAME_EVENTS.md:785-788`;
   `BOSS_RULES.md:976-981`).
2. **The identity-form conflict is real and must be approved, not silently
   resolved.** `DATABASE.md:425-429` fixes every `CardDefinitionId` as
   `card-<ascii-kebab-case-name>` (TASK-082 decision B/R2-9), while the specific
   domain rule requires `earthquake`/`flame-burst`/`root` (root has no prefix at
   all). Under `AGENTS.md` §2 a specific domain rule outranks a technical
   document, so the smallest correction is a documented exception in
   `DATABASE.md` §1/§3 — **but `AGENTS.md` §4 requires operator approval for the
   correction.** OD-1b authorizes it. The alternative (renaming the domain
   identity to `card-earthquake`) would create two identifiers for one ability
   and break the event identity equality above; it is rejected.
3. **Category set is a documented constraint, not a DB constraint.** The stored
   `Category` is an integer ordinal with no database CHECK
   (`CardDefinitionConfiguration.cs:65-73`; migration
   `20260925150903_AddCardPersistence.cs:34`), and `DATABASE.md:431,1527` still
   documents `{Basic, PetSkill}`. OD-1a authorizes provisioning and the
   corresponding documentation amendment. No schema change is needed for the
   category value itself; the only schema change in the whole slice is the score
   column (3.2).
4. **Effect expressibility is already sufficient.** The closed `effectType` set
   is `Heal | Shield | Power | Damage | Burn | Crit` with `valueType`
   `Flat | PercentMaxHp | PercentagePoints | Undetermined`
   (`DATABASE.md:1534-1545`). `earthquake` is `Shield`; the other four abilities
   need a vocabulary extension and are Slice 4 (`BOSS_RULES.md:882-906`).
5. **Provisioning is a data-only migration, not a new mechanism.** `DATABASE.md`
   §5 item 4 (`:1673-1753`) already sanctions EF-migration `InsertData` rows whose
   values are copied from the owning documents, insert-only, tracked by migration
   history. The content-defined set becomes 9 `CardDefinition` rows (8 + the Boss
   Card). The provisioning tests that assert the row set/count are updated in
   Slice 3; the row is never exposed through `/api/cards` because membership
   there is the Player's unlock rows (`API_CONTRACTS.md:934-971`), and no
   `PlayerUnlockedCard` row is created.
6. **Boss-side resolution point.** The existing `ICardDefinitionLookup.
   GetDefinitionAsync` is id-only and unrestricted for content reads
   (`CardRepository.cs:123-131`), so no repository change is needed. The Boss Card
   is resolved **once**, at battle creation, into the existing battle-scoped
   content-input cache beside the Boss definition (`BattleStateService.cs:326,
   557`), never per round and never per CAS attempt. A missing/invalid Boss Card
   for a migrated Boss fails closed at creation; there is no per-battle fallback
   to the legacy Skill (§3.4).
7. **Capability limits stay.** Exactly one Boss Card per Boss; no collection, no
   loadout, no equip, no acquisition, no ownership row (`BOSS_RULES.md:320-353`;
   `ADR-029:185-197`).

### 3.2 B — Cast-priority score (U-3)

```text
Name        CastPriorityScore
Carrier     nullable int column on the BossDefinition row, loaded by the same
            IBossDefinitionLookup Domain-Overlay path that supplies MaxHP/ATK/
            DEF/EnrageThreshold; cached per battle at creation; never re-read
Type        32-bit signed integer
Valid val.  NULL = "no cast-priority score authored" (legal only while the Boss
            has no Boss Card); otherwise an integer >= 0
Selection   cast selected iff CastPriorityScore > boardScore
boardScore  BossMoveSelectionResult.Score of the selected best board candidate
            on the post-recovery board (weights ATK 10 / DEF 5 / HP 20 / POWER 10;
            no tier multiplier; score = sum(weight x matched cells))
Threshold   smallest possible boardScore is 15 (3 cells x weight 5), and ties go
            to the board, so authored values 0..15 always yield the board action;
            >= 16 can win
Timing      recovery -> enumeration -> score -> comparison, all at 18a; the cast
            (if selected) executes at 18c; 18b still runs; 18d deals no damage
Tie-break   board action wins (cast must strictly out-score); board ties keep the
            lowest From then lowest To
RNG         zero unseeded PRNG; no clock; no client input; the only RNG is the
            existing seeded deadlock reshuffle built from the retained RngState
Retry       selection is recomputed from the fresh committed state on every CAS
            attempt; content inputs are frozen at battle creation
Validation  a migrated Boss (active form = BossCard) whose score is NULL or
            negative is invalid definition data: battle creation fails closed
            (ADR-024 §2.2 item 3). No default, no fallback weight, no board-only
            degradation
```

1. **Ownership is already decided.** `BOSS_RULES.md:378-384` fixes the cast score
   as "the Boss definition's authored deterministic cast-priority score for its
   Boss Card" — per Boss, owned by the Boss definition, not global and not
   per-card-instance. The carrier is therefore the `BossDefinition` row, in the
   same overlay contract as its four balance values. Putting it on the Boss Card
   row would contradict that ownership sentence and would not exist for an
   unmigrated Boss; storing it in the transitional `SkillDefinition` JSON would
   conflate the two forms (that JSON's four-member contract is
   `DATABASE.md:989-1002`, TASK-045).
2. **Nullable is the fail-closed form, not a default.** `NULL` is an explicit
   "not authored" marker with a documented Domain precedent
   (`BossPassiveDefinition.Threshold`; `BossDefinitions.cs:82-95`;
   `DATABASE.md` §1 note item 3), not a fabricated `0`. It keeps Slice 3 to one
   authored value and keeps the other four Bosses' balance open until Slice 4.
3. **ADR-024 must be amended, so this is an operator decision.** ADR-024 §2.2
   decides "four balance columns" (`ADR-024:49-56`) and is `Accepted`; adding a
   fifth overlay value changes that decision
   (`AGENTS.md` §18; `.ai/workflow/architecture/adr-change.md`). OD-3a authorizes
   the amendment and the schema migration.
4. **Score semantics under a cast phase are a balance question, not a design
   gap.** A cast phase forgoes the Boss board action and therefore its Power
   generation and its ATK-gated attack (`BOSS_RULES.md:411-423`); how often that is
   worth it is exactly what the authored score controls (U-3, ADR-029 §3).

### 3.3 C — Initial Boss Card: `Earthquake → Shield` (OD-2 values)

| Field | Reconciled value | Evidence |
|---|---|---|
| Ability identity | `earthquake` | `BOSS_RULES.md:952, 979-980` |
| `CardDefinitionId` | `earthquake` | `CARD_RULES.md:163-168` (pending OD-1b) |
| `Category` | `BossCard` | `CARD_RULES.md:146-175`; `BOSS_RULES.md:320-325` |
| `Name` | `Earthquake` (the documented ability name; transcription, not invention) | `BOSS_RULES.md:858-865`, `:952` |
| `PowerCost` | **unresolved — OD-2a** (integer 0..100) | `ADR-029` §3 U-2; `BOSS_RULES.md:825-827` |
| `LoadoutCopyLimit` | `1`, persisted-never-read | `CARD_RULES.md:133-144`; `:169-172` |
| Effect | `Shield`, `Flat`, magnitude **`150` implemented-but-unrecorded** — **OD-2b** confirms or replaces | `BossDefinitions.cs:272-296` (`:286`); `BOSS_RULES.md:897-901` |
| Target | self (actor-relative → `BossState.ActiveStatusEffects`) | `BOSS_RULES.md:336-339`; `CARD_RULES.md:298-336`; `COMBAT_RULES.md:761-765` |
| Duration/expiry | none; trigger-based; expires at `ShieldDepleted` (pool exactly 0); refresh-replaces; at most one per entity | `COMBAT_RULES.md:726-800`; `GAME_STATE.md` §2.3.1 |
| Cooldown | `0` (carried over; authoritative) | `BOSS_RULES.md:857-867` |
| Cost deduction | exactly once, `EffectiveCardCost == PowerCost` | `CARD_RULES.md:298-336`; `CardCastExecutor.cs:609-633` |

1. **The cast is executable by the existing shared contract.** The Boss branch
   validates category, composes `EffectiveCardCost` with an empty modifier set,
   checks `BossState.Power`, gates representability before deduction, deducts
   exactly once, resolves `Shield` via
   `StatusEffect.TriggerBased("Shield", …, StatusEffectSource.Boss, amount,
   ShieldDepletedCondition)` + `ApplyShield`, and returns the committed state with
   no events (`CardCastExecutor.cs:582-774`, `:669-695`). Slice 3 adds the
   selection, the cooldown write, and the event at the phase owner; it does not
   extend the effect vocabulary or migrate any other ability.
2. **`150` is not approved by having been implemented.** `BOSS_RULES.md:897-901`
   records the magnitude as "Non-blocking balance tuning parameter"; ADR-026's
   unresolved choices and ADR-029 §3 U-4 list Shield points for `earthquake` as
   unresolved. OD-2b is the operator's confirmation. If `150 Flat` is confirmed,
   the owning document records it (and the implemented value becomes the authored
   value with parity evidence). If another value is chosen, it is recorded and
   provisioned instead. `PercentMaxHp` is a viable but unit-changing alternative
   (150 = 5 % of the Boss's `MaxHP` 3000); it is not recommended because it
   changes the effect's unit and would need its own parity comparison.
3. **Legacy parity is exact.** The legacy handler applies the same
   `ApplyShield` with the declared instance (`BattleStateService.cs:2182-2197`),
   so a confirmed `150` yields old/new parity for Slice 4's comparison too.
4. **Cooldown `0` makes the Power/score gates the only cadence limiters**; a cast
   phase generates no Power (`BOSS_RULES.md:296-303, 411-418`), so casting and
   Power accumulation trade off — the interaction OD-2a/OD-3b control.

### 3.4 D — Active form and cooldown

```text
One form per ability, always.
Active form = provisioned content:
    BossCard  iff a provisioned CardDefinition row exists with
              Category = BossCard and CardDefinitionId = that Boss's SkillId
    BossSkill otherwise (the transitional path)
Resolved ONCE per battle at creation, from the same content-input path that
supplies BossDefinition; cached with it; never re-derived per round; never stored
in BattleState/Redis; never inferred from SkillCharge or SkillCooldown.
No fallback: if the migrated content is missing or invalid, battle creation fails
closed. An ability never executes both forms in one response phase.
```

1. **This is the only genuinely new design rule of the slice** (OD-1c): the
   authorities fix *that* the migration is staged and that one form exists per
   ability (`BOSS_RULES.md:257-269`, `:425-457`; `ADR-029:261-282`), but not *how*
   the runtime knows which form is active. Content presence is the single-source
   signal: it cannot disagree with itself, needs no state/wire member, and is
   stable because provisioning is insert-only (`DATABASE.md:1673-1753`).
   Rejected alternatives: an explicit `abilityForm` flag (duplicates the fact and
   can disagree with it); a code allowlist (deploy-time, contradicts ADR-024);
   inferring from `SkillCooldown`/`SkillCharge` (forbidden — cooldown is a timing
   counter, charge is a transitional counter).
2. **Enablement for Slice 3 is exactly one Boss.** Only Sơn Thạch Vệ
   (`boss-son-thach-ve`, `earthquake`) is migrated; the other four keep the legacy
   path and their existing tests unmodified (`BOSS_RULES.md:447-452`; ADR-029
   §4 migration order).
3. **Cooldown is one carrier for both forms.** `BossState.SkillCooldown` is
   retained (`GAME_STATE.md:2375-2390`; `BOSS_RULES.md:340-343`) and:
   decrements by exactly 1 when the committed Round's write-back advances `Turn`
   by 1; is a hard gate while `> 0`; is set to the authored value on a successful
   cast in the same write-back; keeps running during board-action phases; is
   never an ability-identity or migration-state signal. With the documented
   order, an authored `N` blocks `N−1` subsequent rounds and the Nth round is
   eligible again; `N = 0` is eligible every round. The stale inline comment at
   `BattleStateService.cs:2278-2282` is corrected as part of Slice 3's touched
   file (C-3), not treated as a rule.
4. **`SkillCharge` is inert for a migrated Boss.** It continues to be written by
   the transitional generator on the Boss's board phases, is not read as a gate
   for a `BossCard` ability, and is not reset by a Card cast; it is retired with
   `BossSkillCast` in Slice 5. No new work may build on it (`BOSS_RULES.md:434-437`).
5. **Duplicate execution is structurally impossible.** The legacy Skill gate runs
   only when the active form is `BossSkill`; the Card path runs only when the form
   is `BossCard` **and** the cast was selected. One form per battle + one selected
   action per phase ⇒ at most one of the two gates can execute.

### 3.5 E — One-action policy, cooldown transition and atomicity

The policy itself is already normative (`BOSS_RULES.md:355-423`;
`GAME_RULES.md:506-545`; `GAME_RULES.md:361-366`). This reconciliation fixes the
implementation-level readings Slice 3 must follow.

**3.5.1 Selection transition table** (`form` = §3.4; `CD'` = cooldown after the
round's decrement; `B` = selected board candidate's score; `S` = authored score;
`E` = `EffectiveCardCost`):

| # | form | CD' | Power vs E | B vs S | Selected action | 18a board work | 18c cast | 18d attack |
|---|---|---|---|---|---|---|---|---|
| 1 | Skill (unmigrated) | any | — | — | Board action (legacy) | yes | legacy gate fires iff `SkillCharge ≥ req AND CD' == 0` | iff ATK match |
| 2 | Card | > 0 | — | — | Board action | yes | none | iff ATK match |
| 3 | Card | 0 | < E | — | Board action | yes | none | iff ATK match |
| 4 | Card | 0 | ≥ E | S > B | **Cast** | **none** | Boss Card cast | **none** |
| 5 | Card | 0 | ≥ E | S ≤ B | Board action | yes | none | iff ATK match |
| 6 | neither available | — | — | — | **no action** | none | none | none |

Rows 2, 3 and 5 share one runtime path; row 6 is the documented but currently
unreachable branch (§3.5.4); row 1 is the transitional path and must stay
behaviour-identical.

**3.5.2 Eligibility and ordering.** Board action is available iff the post-
recovery legal-move enumeration finds at least one match-producing adjacent swap
(`BossAiEngine.cs:117-141`); recovery runs first and is unchanged
(`BOSS_RULES.md:364-369`). Cast is available iff the active form is `BossCard`
(content presence), `BossState.Power >= EffectiveCardCost`, and the decremented
cooldown is 0. If exactly one action is available it is selected whatever its
score; if both are, the higher score wins; a tie goes to the board. Selection is
18a; the board action, if selected, executes at 18a; Passive at 18b; the cast, if
selected, resolves at 18c; the attack check is 18d. The one-action choice changes
*which* of 18a/18c does work; it adds, removes or reorders no step.

**3.5.3 A cast-selected phase suppresses the board action and the attack.**
No Boss swap, cascade, generation, board-cycle event, `MatchedGemSummary`, Boss
Power gain, or Boss damage event. `BossCardCast` is emitted at 18c; 18b's Passive
still evaluates. `GAME_RULES.md:517-518, 519-523, 532-541`;
`BOSS_RULES.md:411-418`.

**3.5.4 The neither-action branch is defined, not reachable today.** It means
"no board resolution, no cast, no attack; 18b–18d still run; the Round proceeds"
(`BOSS_RULES.md:374-375`). In the current engine a successful recovery guarantees
a legal swap and a failed recovery throws `BoardGenerationFailedException`
(`BossAiEngine.cs:197-217`), so the branch is only reachable if recovery
semantics change. Slice 3 must implement the branch as specified and must **not**
conflate it with `BoardGenerationFailedException` (an error path).

**3.5.5 The 18c re-check is a fail-closed invariant, not a second selection.**
`GAME_RULES.md:524-531` says the cast eligibility is "evaluated" at 18c "if and
only if 18a selected" a cast. Because only the Passive stage runs between
selection and execution and no Passive in MVP can change the Boss's Power,
cooldown, or Boss Card, the re-check is redundant on valid data. Contract: it is
re-evaluated against the same committed state; if it fails, the round **fails
closed** (throws) rather than silently performing a board action or dropping the
cast — either alternative would violate "exactly one action", the event/state
contract and the determinism guarantee.

**3.5.6 Cooldown transition.** Step 3 of the round decrements once per committed
round, after `Turn` advances and before selection (`BattleStateService.cs:1399-1418`).
A cast sets the authored value in the same write-back (the Card form must mirror
`:2287-2291`). A board phase only decrements. Nothing decrements twice; the
executor stays policy-free and writes no cooldown (`CardCastExecutor.cs:569-580`),
so Slice 3 applies the set at the Boss response phase owner (Application) after
the executor returns, preserving TASK-305's Domain/Application boundary.

**3.5.7 One atomic write and one ordered event batch.** The cast executor returns
a committed state (`CardCastExecutor.cs:759-774`); the phase owner applies the
cooldown and emits `BossCardCast`; the round writes the whole `BattleState` once
(`GAME_STATE.md` §5.1 item 9) and delivers one ordered `ReceiveEvents` batch
(`GAME_EVENTS.md` §1; `SIGNALR_PROTOCOL.md` §3.1). Event order in a cast-selected
phase: 18b Passive events (if any) → `BossCardCast` at 18c → no Boss board/damage
events → no `PowerChanged` for the Boss Power spend (C-6; §3.6). `Turn` and
`Sequence` advance once for the round, not by the cast.

**3.5.8 CAS retry determinism.** A refused compare-and-set writes nothing; the
next attempt re-reads the committed state and re-runs the whole phase
(`BattleStateService.cs:924-1035`). Selection must be a pure function of
(committed state, fixed content inputs, fixed enumeration order); the Boss Card
definition, form and score are resolved once at creation, so a retry cannot
observe different content. The only RNG is the seeded recovery stream built from
the retained `RngState` on each attempt (`:1770-1784`); its consumption on a
refused attempt is discarded with the attempt. Acceptance: a retried round
produces one Power deduction, one cooldown write, one `BossCardCast`.

### 3.6 F — Event and wire contract

1. **`BossCardCast` is already canonical and fully specified** — no new event,
   hub method, discriminator, version or state field is needed:
   `GAME_RULES.md:398, 424-433` (canonical name),
   `GAME_EVENTS.md:772-804` (meaning/payload),
   `SIGNALR_PROTOCOL.md:1723-1778` (wire schema), `BOSS_RULES.md:1009-1011`
   (Boss events), `SIGNALR_PROTOCOL.md:551-618` (discriminator/tolerant reader).
2. **Payload:** `type: "BossCardCast"`, `cardId` = the Boss Card definition
   identity (= the ability `SkillId`), `sourceId` = the Boss's canonical technical
   Identity (`BossState.BossId`). No cost, no Power value, no cooldown, no
   effect summary. The resulting Boss Power/cooldown are authoritative in the
   `bossState` projection (`SIGNALR_PROTOCOL.md:1753-1759`; ADR-029 §2.11).
3. **Code gap is exactly four additive points:** `BattleEventType.BossCardCast`
   (next ordinal 20; current highest `MatchedGemSummary = 19`,
   `BattleEvent.cs:52-344`), a `BossCardCastEvent` payload + `ForBossCardCast`
   factory (pattern: `BossSkillCastEvent`/`ForBossSkillCast`, `:569, 1178-1182`),
   a projection arm (pattern: `BattleEventWireProjection.cs:830, 954-965`; the DTO
   already has `cardId`/`sourceId` at `:229-240`), and the client pipeline (§7).
4. **Emission rules:** after cost deduction and effect application, at 18c, inside
   the round's single batch; never for a board match action; never a `CardCast`;
   never `BossSkillCast` for the same ability resolution.
5. **Client presentation** must reuse the boss-skill display-name resolver
   (`BOSS_SKILL_NAMES` / `resolveBossSkillDisplayName`,
   `BattleEventPresenter.ts:962-976`) because the identity is the `SkillId`;
   adding a parser, a phase kind, a callout and the scene's hold handling is the
   Slice 3 client work (§7). Unknown-event tolerance is preserved
   (`BattleEventPresenter.ts:288-329`); today an unhandled `BossCardCast` is
   dropped by the presenter and invisible in the scene, not a desync.

---

## 4. Decision table

### 4.1 Settled by existing authority — no new approval needed

| # | Decision | Authority |
|---|---|---|
| D-1 | Exactly one action per response phase (board **or** cast, never both) | `ADR-029:212-214`; `BOSS_RULES.md:357-361`; `GAME_RULES.md:361-366` |
| D-2 | Selection at 18a; board at 18a; Passive 18b; cast 18c; attack check 18d | `BOSS_RULES.md:400-404`; `GAME_RULES.md:506-545` |
| D-3 | Board wins ties; board ties keep lowest `From` then `To` | `BOSS_RULES.md:387-392`; `BossAiEngine.cs:219-247` |
| D-4 | Determinism: zero unseeded PRNG, no clock, no client input, fixed enumeration | `BOSS_RULES.md:394-399`; `ADR-026` item 8; `TDD.md` §6 |
| D-5 | Cast selected ⇒ no Boss board resolution, no generation, no attack; Passive still runs | `BOSS_RULES.md:411-418`; `GAME_RULES.md:517-523, 532-541` |
| D-6 | Cooldown: decrement once per committed round when `Turn` advances, hard gate while `> 0`, set on successful cast, retained carrier, never a form signal | `GAME_STATE.md:2375-2390`; `BOSS_RULES.md:405-410`; §6.3 |
| D-7 | One Boss Card per Boss; identity = ability `SkillId`; no loadout/ownership/collection | `CARD_RULES.md:153-168`; `ADR-029:187-197` |
| D-8 | Shared cast contract; empty modifier set; cost from/against `BossState.Power` | `CARD_RULES.md:298-336`; `BOSS_RULES.md:326-339` |
| D-9 | Dedicated `BossCardCast` (`cardId` + `sourceId`); Player `CardCast` unchanged | `GAME_RULES.md:398, 424-433`; `GAME_EVENTS.md:772-804`; `SIGNALR_PROTOCOL.md:1723-1778` |
| D-10 | Earthquake's effect is a self, trigger-based Shield with no Turn duration | `BOSS_RULES.md:897-901`; `COMBAT_RULES.md:726-800` |
| D-11 | `SkillCharge`/`BossSkillCast`/legacy handler are not retired here; no new work builds on them | `ADR-029:261-282`; `BOSS_RULES.md:425-457` |
| D-12 | No new event, hub method, state member or wire member beyond `BossCardCast`, which is already defined | `BOSS_RULES.md:994-1032`; `SIGNALR_PROTOCOL.md:1723-1778` |
| D-13 | Unmigrated Bosses keep the pre-migration response order; their tests stay unmodified | `BOSS_RULES.md:425-432, 447-452` |
| D-14 | A cast phase's Boss Power spend emits no `PowerChanged`; the resulting value is the `bossState` projection's | `GAME_EVENTS.md:542-593`; `SIGNALR_PROTOCOL.md:1295-1356, 1753-1759` |
| D-15 | Cooldown ordering (`N` blocks `N−1`) is authoritative, not an open decision; the stale inline comment is a code defect | `GAME_STATE.md:2380-2388`; `BOSS_RULES.md:840-849`; `BossResponseTests.cs:1398-1509` (this report C-3) |
| D-16 | Boss Card content never appears in the Player collection/loadout/API | `API_CONTRACTS.md:934-971`; `BOSS_RULES.md:352-353`; `CARD_RULES.md:169-172` |

### 4.2 Unresolved operator decisions — the complete remaining set

Every row names the exact decision, the evidence, the genuinely viable options,
the recommended default, dependencies, whether approval is required, and what it
blocks. **No value below is assumed approved; no numeric value is proposed.**

#### OD-1 — Approve the consolidated Boss Card contract (definition, provisioning, identity, active form, ADR-029 amendment)

```text
Decision required (one authorization, four inseparable clauses):
  1a. Reuse CardDefinition as the canonical Boss Card contract (Option A):
      Category = BossCard; one data-only EF-migration row; no new table,
      no schema change for Category, no Player loadout/ownership/API surface.
  1b. Resolve the authoritative identity-form conflict in the direction the
      precedence already indicates: amend DATABASE.md §1/§3 so a BossCard row's
      CardDefinitionId is the ability SkillId (not card-<kebab>), keeping
      CARD_RULES.md §1.1 item 3 as owner. (Alternative — rename the domain
      identity to card-<kebab> — is viable only by also breaking the
      BossCardCast.cardId == BossSkillCast.skillId equality; rejected.)
  1c. Adopt the active-form rule: form = provisioned-content presence, resolved
      once per battle at creation; enable only Son Thach Ve (earthquake) in
      Slice 3; no fallback to the legacy Skill.
  1d. Amend ADR-029: supersede §2.5 item 15's "persistence is out of scope" with
      the recorded U-7 decision, add the reconciled exit criteria, and record
      that the definition/provisioning prerequisite is part of Slice 3 (no
      separate Slice P task).

Evidence: CARD_RULES.md §1.1 items 1/3/5; DATABASE.md §1 CardDefinition block,
§3 constraint, §5 item 4; ADR-029 §2.5 items 12-15, §2.9, §4; BOSS_RULES.md
§4.2, §4.4; AGENTS.md §2/§4/§17/§18.
Viable options: Option A (recommended) vs a new Boss-side definition carrier
(rejected: duplicate identity/cost/effect and a second provisioning mechanism)
vs a code-side catalogue as the production source (rejected: reintroduces
code-authored content and contradicts DATABASE.md §5 item 4).
Recommended default: Option A with clauses 1a-1d as one approval, because it is
the only option with one identity, one cost contract, one effect vocabulary,
one lookup and one provisioning mechanism, and it is what CARD_RULES.md §1.1
and TASK-305 already prepared.
Dependencies: none (this is the root authorization).
Approval required: YES — product/architecture authorization and an
authoritative-document conflict resolution (AGENTS.md §4/§18).
Blocks: Boss Card provisioning (data-only migration); the Slice 3 runtime; the
post-approval documentation amendments.
Does NOT block: nothing else.
```

#### OD-2 — Author the initial Boss Card's content values for `earthquake`

```text
Decision required:
  2a. PowerCost: an integer. Mechanics bound the valid range to 0..100
      (BossState.Power is clamped 0..100 and EffectiveCardCost == PowerCost, so
      0 is always affordable and >100 is never affordable); which value is a
      balance decision and is not derivable from any document.
  2b. Shield magnitude: confirm the implemented 150 Flat, or author a different
      positive integer Flat, or switch to PercentMaxHp (unit change; 150 = 5% of
      the Boss's MaxHP 3000 today). No magnitude is currently recorded as
      authored.
  2c. Display Name: transcribe "Earthquake" (the ability's documented name);
      no separate decision unless the operator wants a different display name.

Evidence: ADR-029 §3 U-2/U-4; BOSS_RULES.md §4.2 item 3, §6.3 (each migrated
ability's Power cost is UNRESOLVED), §6.3.1 item 4 ("Non-blocking balance tuning
parameter"); BossDefinitions.cs:272-296 (Magnitude = 150; BaseDamage = 0);
COMBAT_RULES.md §4; ADR-026 Unresolved Balance Choices #4.
Viable options: any integer 0..100 for PowerCost; confirm 150 Flat / another
positive integer Flat / PercentMaxHp for the Shield.
Recommended default: NONE for 2a/2b — these are balance values and may not be
fabricated (AGENTS.md §7). If the operator wants parity with today's behaviour,
2b defaults to confirming 150 Flat (it is the implemented, tested legacy value).
Dependencies: OD-1 (authorization to provision).
Approval required: YES — balance/content values.
Blocks: the provisioning row and therefore Slice 3.
Does NOT block: documentation-only U-3/score work (OD-3).
```

#### OD-3 — Approve the cast-priority score carrier and author `earthquake`'s score

```text
Decision required:
  3a. Approve the carrier: a nullable CastPriorityScore (int) column on the
      BossDefinition row, loaded through the existing Domain-Overlay lookup,
      with CHECK (CastPriorityScore IS NULL OR CastPriorityScore >= 0);
      ADR-024 amended from four to five overlay values (fifth nullable);
      DATABASE.md §1/§3 updated; one schema migration in Slice 3.
  3b. Author earthquake's score: an integer >= 0. Values 0..15 always yield the
      board action (minimum board score 15, ties to the board); >= 16 can win.
      Which value is a balance decision and is not derivable.

Evidence: BOSS_RULES.md §4.3 steps 4-7 and item 5 (score is the Boss
definition's authored deterministic value; form and values UNRESOLVED);
ADR-029 §3 U-3 and §2.7 item 20; BossAiEngine.cs:13-33/148-173 (board score
scale, min 15); ADR-024 §2.2 (four-column overlay, fail-closed, no fallback);
BossDefinitions.cs:82-95 (documented null-marker precedent).
Viable options: (a) nullable column on BossDefinition (recommended);
(b) non-nullable column (rejected: forces authoring four premature Slice-4
balance values and a NOT NULL default would fabricate data, violating
ADR-024's no-fallback rule); (c) store on the Boss Card row (rejected:
contradicts BOSS_RULES.md's ownership sentence and does not exist for an
unmigrated Boss); (d) store in the SkillDefinition JSON (rejected: conflates
the two ability forms).
Recommended default: (a) + the smallest possible authored value only for
earthquake; the other four Bosses stay NULL until their own migration (Slice 4).
Dependencies: OD-1 (documentation authority); independent of OD-2 at the
doc/schema level, but the Slice 3 runtime needs both.
Approval required: YES — a new persisted balance value and an ADR amendment
(AGENTS.md §18), plus the authored value (AGENTS.md §7).
Blocks: the Slice 3 selection policy and its acceptance tests.
Does NOT block: the CardDefinition provisioning row itself (the score lives on
BossDefinition), though Slice 3 cannot be declared complete without it.
```

#### Explicitly closed / reclassified items (no operator decision required)

| Item | Status | Why |
|---|---|---|
| U-1 | Resolved (TASK-304, Option A) | `ADR-029:317`; `BOSS_RULES.md:313-318` |
| U-2 | **narrowed to OD-2a** | Only `earthquake`'s cost is needed for Slice 3; the other four are Slice 4 |
| U-3 | **contract settled; narrowed to OD-3b** | Structure decided (`BOSS_RULES.md` §4.3); only `earthquake`'s value is needed for Slice 3 |
| U-4 | **narrowed to OD-2b** | Only `earthquake`'s Shield magnitude is needed; the other three remain Slice 4 |
| U-5 | Not blocking; deferred | Cooldown values unchanged and `earthquake` is `0`; any retune is Slice 4 balance work |
| U-6 | Not blocking; retired in Slice 5 | Closed by retirement under §2.9's parity gate, not answered |
| U-7 | **OD-1a** | Provisioning authorization |
| U-8 | **OD-1b** | Identity value-form conflict (reported, unapplied) |
| U-9 | **OD-3a** | Score carrier/ADR-024 amendment |
| U-10 | **OD-2a/2b (+ Name transcription)** | Initial content values |
| U-11 | **OD-1c** | Active-form signal + first-Boss enablement |
| U-12 | **Closed — not an operator decision** | Authority already fixes decrement/check/set and `N−1`; the stale inline comment is a code defect (C-3/D-15) |

---

## 5. Documentation revalidation — exact patches, none applied now

All candidate changes were re-derived from the owning sections (§2 precedence:
the concept's owner makes the change). **None is applied by this task**, because
every one of them records an approval that has not been granted (`AGENTS.md` §4
step 6, §7, §17; `documentation/documentation-change.md` §2). The exact patch set
for the post-approval Slice 3 task is:

| # | File | Section | Change | Requires |
|---|---|---|---|---|
| 1 | `DATABASE.md` | §1 `CardDefinition` block; §3 constraints | Add `BossCard` to the stored category set; record the identity value-form exception; record the 9-row content set; record that `LoadoutCopyLimit` is persisted but never read for a `BossCard` | OD-1a/1b |
| 2 | `DATABASE.md` | §5 item 4 | Record the Boss Card provisioning row/mechanism and update the provisioned-row set/count | OD-1a |
| 3 | `DATABASE.md` | §1 `BossDefinition` block; §3 constraints | Add nullable `CastPriorityScore`, its `NULL` semantics and `>= 0` check; reconcile the block's "proposed under ADR-024" wording with the now-five-column overlay | OD-3a |
| 4 | `ADR-024-database-driven-boss-balance-configuration.md` | §2.2 (and status/amendment note) | Extend the overlay from four to five values, the fifth nullable, keeping fail-closed/no-fallback semantics | OD-3a |
| 5 | `CARD_RULES.md` | §1.1 items 3 and 5 | Item 3: point at the `DATABASE.md` identity exception. Item 5: replace "no persistence contract is added here" with the authorized provisioning contract and the persisted-never-read `LoadoutCopyLimit` rule | OD-1a/1b |
| 6 | `BOSS_RULES.md` | §4.3 step 4 and item 5; §4.4 (new item); §6.3.1 item 4; §6.3/closing note | Record the score's carrier/type/range/validation and that the values are authored content; add the one-active-form/content-presence/once-per-battle/no-fallback rule; record `earthquake`'s authored magnitude; resolve the "no Boss-specific value remains to be authored" sentence against the still-unresolved magnitudes/costs/scores | OD-1c, OD-2b, OD-3 |
| 7 | `BOSS_RULES.md` | §4.3 item 4 (clarification) | State that a cast-selected phase may still carry the pre-selection deadlock reshuffle and its seeded-RNG consumption, and that the reshuffle emits no board-cycle event | settled; added by the approved Slice 3 (no new decision) |
| 8 | `GAME_STATE.md` | §2.4 / §2.4.3 (note) | State that the Boss Card definition, active form and score are battle-start **content inputs**, not state members (no Redis/wire change); optionally state the `N` blocks `N−1` reading explicitly | OD-1c/OD-3; clarification |
| 9 | `ADR-029-…md` | §2.5 item 15; §3; §4 | Supersede the persistence-out-of-scope clause; record U-7…U-10 as decided, U-12 as closed, U-2/U-3/U-4 as narrowed; fold the definition/provisioning prerequisite into Slice 3 and add the executable exit criteria | OD-1d |
| 10 | `ARCHITECTURE.md` | §3 | Add `CardRepository`/`ScopedCardDefinitionLookup` to the persistence-boundary row and name the Boss Card resolution point; `BossActionPolicy` is already named (`:804-808`) | OD-1 |
| 11 | `docs/03-decisions/README.md` | ADR index / supersession block | Record the ADR-029 and ADR-024 amendments (in-place amendment precedent) | OD-1d/OD-3a |
| 12 | `API_CONTRACTS.md` | §5.3 | **No change required** — revalidated: membership is the Player's unlock rows, and a `BossCard` row has no `PlayerUnlockedCard`; the documented category enum describes the reachable set, not the stored set. Record this as an explicit no-change in review | — |
| 13 | `GAME_EVENTS.md`, `SIGNALR_PROTOCOL.md`, `MVP_SCOPE.md`, `REDIS_STATE.md` | — | **No change required** — `BossCardCast`, `bossState.power` and the Boss Card capability are already defined; no new state/wire member is proposed | — |

**Not in this list (implementation comments, not contracts):**
`BattleHub.cs:366-372` (stale "three-member" projection text),
`BattleStateService.cs:2278-2282` (stale cooldown comment — corrected in Slice 3
because the file is touched), and the decision-time snapshots in ADR-029
§1.2/§1.3. They are reported in §9 as separate debt (`AGENTS.md` §16).

**Internal inconsistency found and revalidated (not fixed):**
`BOSS_RULES.md:914` states "No Boss-specific value remains to be authored", while
§6.3.1 records each ability's magnitude as an unresolved tuning parameter and
`BOSS_RULES.md:825-827` says each migrated ability's Power cost is unresolved. The
sentence is true for §6's identity/stats/timing content but not for the
magnitudes/costs/scores; the post-approval BOSS_RULES edit in row 6 must qualify
it. Until then it is a reported contradiction, not silently resolved.

---

## 6. Reconciled ADR-029 dependency graph and Slice 3 exit criteria

### 6.1 Graph (reconciled)

```text
[Slice 1] Boss Power foundation                          DONE (TASK-304)
    |
[Slice 2] Shared actor-aware cast contract               DONE (TASK-305)
    |
[TASK-307] Contract reconciliation + operator approval    THIS TASK
    |   verdict READY FOR OPERATOR APPROVAL
    |   (OD-1, OD-2, OD-3 answered by the operator)
    v
[Slice 3] ONE end-to-end task (GAMEPLAY-CHANGE, type rationale in §7.1)
    W1 authoritative docs + ADR amendments (docs-first, approved contract)
    W2 Boss Card definition + provisioning + score carrier
       (data-only row migration + score schema migration + fail-closed lookups)
    W3 deterministic BossActionPolicy (Domain component already named)
    W4 round integration: selection, cast/board branches, cooldown, atomicity
    W5 BossCardCast domain event + wire projection (payload already specified)
    W6 client parse/present/callout/hold
    W7 regression + balance-metric re-derivation + gates
    |
[Slice 4] Four remaining abilities, each only after an approved
          effect-vocabulary extension (flame-burst -> root -> drain-power ->
          thunder-strike last)
    |
[Slice 5] Retire SkillCharge / BossSkillCast / legacy handler after the
          four-part parity gate
```

**Edges:** Slice 3 needs TASK-307's approval plus Slices 1–2. Slice 4 needs
Slice 3 plus a separately approved vocabulary extension (an independent design
risk; not registered now). Slice 5 needs Slice 4 plus every part of
`ADR-029` §2.9's parity gate. No *separate* definition/provisioning slice exists:
it is Slice 3's first work items, because its acceptance is inseparable from
Slice 3's ("both action kinds are presented from their own events" cannot be met
without a provisioned card).

### 6.2 Executable exit criteria

| Slice | Exit criterion | What it does **not** prove |
|---|---|---|
| 1 (done) | Boss `Power` generated, clamped, serialized, projected, rendered, reconnect-restored; `SkillCharge` untouched | no cast, no selection |
| 2 (done) | One cast contract serves both actors; Player path byte-identical; Boss cast validates/resolves against `BossState.Power` — **Domain tests with synthetic fixtures only** | no production Boss Card; no selection; no event |
| **3** | With the provisioned `earthquake` Boss Card and authored score: at battle start the card/form/score resolve once and fail closed on bad content; the Boss selects exactly one action per phase deterministically (board-only / cast-only / both / tie / neither); a cast phase produces no Boss swap, cascade, generation, board event, Power gain or attack, deducts Power exactly once, applies the Shield, sets cooldown, emits one `BossCardCast` on the real wire inside the round's single ordered batch; the client renders the cast; CAS retry produces one commit/deduction/event; unmigrated Bosses' suites pass unmodified; Player cast contracts are byte-identical | **not** end-to-end for the other four abilities; `SkillCharge`/`BossSkillCast` still present; no vocabulary extension; the deadlock-reshuffle observation is deterministic but not client-identifiable |
| 4 | All five abilities migrated with the same authored effect/target/duration as their legacy form, each after an approved vocabulary extension, with old/new parity evidence — including Thunder Strike's exact qualifying attack and modifier-consumption timing | `SkillCharge`/`BossSkillCast` still present; no retirement |
| 5 | `ADR-029` §2.9's four-part gate met: all abilities migrated/tested, old/new compared, every consumer reconciled, ADR/index amendments recorded; `SkillCharge`, `BossSkillCast` and the legacy handler removed; `SkillCooldown` retained | — |

**Explicit statement:** Slice 3 must never be reported as end-to-end complete on
Domain fixtures alone (Slice 2 already did that); it requires an actually
provisioned Boss Card row and a real event/client presentation.

---

## 7. Proposed single Slice 3 task (NOT registered)

### 7.1 Identity and type rationale

```text
Working title   Slice 3 — End-to-end Boss Card action policy for Son Thach Ve
Type            GAMEPLAY-CHANGE (development/gameplay-change.md)
Risk            HIGH
Primary agent   gameplay; supporting backend, persistence, realtime, client,
                testing, review
Depends on      TASK-307 (approval), TASK-303/304/305 (done), OD-1/OD-2/OD-3
Register when   the three decisions are recorded
```

**Why one GAMEPLAY-CHANGE task rather than several tasks.** The work introduces
approved balance values/content and new documented rules, then propagates them
through docs, schema, runtime and tests — precisely `GAMEPLAY-CHANGE`'s shape
(`.ai/workflow/development/gameplay-change.md` §3), and that workflow explicitly
includes updating technical docs and creating/updating an ADR when required. This
avoids the `TASK_TYPES.md` §3 type-blend problem without splitting a single
acceptance boundary. `TASK_TYPES.md` §2 also states that code changes which
update docs as a side effect use the code-change type and include the doc update
in the same task. If the reviewer nevertheless enforces a strict split, the only
permissible contingency is two tasks (documentation/ADR application first, then
implementation); that split would add a lifecycle task and therefore needs
explicit approval — it is not recommended.

### 7.2 Ordered work items (one task, one declared file set, one review gate)

```text
W1  Authoritative documentation + ADR amendments (docs-first, AGENTS.md §17)
    exactly the approved patches in §5 rows 1-11, plus the reported
    contradictions this contract depends on (§5 note).
W2  Definition, provisioning and score carrier
    - data-only EF migration: the one BossCard row (identity, name, cost,
      LoadoutCopyLimit 1, structured Shield effect), insert-only + mirrored Down
    - schema migration: nullable CastPriorityScore + CHECK, and the
      ADR-024 overlay lookup/validation extension (fail closed)
    - battle-start resolution of the Boss Card, active form and score, cached
      with the Boss content input; invalid/missing => fail closed
    - provisioning and lookup tests (9-row set, category, identity, cost/effect
      round-trip, range validation, no Player visibility)
W3  Domain BossActionPolicy (name already reserved by ARCHITECTURE.md §3)
    pure deterministic selection over (board candidates, cast eligibility,
    cast score); zero unseeded RNG; unit tests for board-only/cast-only/both/
    tie/neither and the 15/16 boundary.
W4  Round integration (Application)
    selection at 18a; board or cast branch; suppressions; Passive 18b; 18c
    fail-closed re-check; cooldown set/decrement; Power deduction via the shared
    executor; single write-back; CAS-retry determinism; stale cooldown comment
    correction in the touched file.
W5  Event + wire
    BattleEventType.BossCardCast (ordinal 20), payload/factory, projection arm,
    application tests for payload/order/batch position; no Player CardCast change.
W6  Client presentation
    parser, presented type, phase kind, callout, hold, display-name resolver
    reuse (SkillId identity), unknown-type tolerance; vitest coverage.
W7  Regression and evidence
    unmigrated Bosses' suites unmodified; legacy Skill path; Player cast path;
    CAS retries; event ordering; atomic write; balance cadence metrics re-derived
    (never hand-edited); protected artifacts untouched; pre-existing failure set
    documented and unchanged.
```

### 7.3 Acceptance checklist (binary)

- [ ] OD-1, OD-2, OD-3 recorded verbatim in the task record before any file changes; no value or authorization inferred.
- [ ] §5 patches applied; no unresolved documentation conflict remains; ADR-024/ADR-029 amendment notes recorded.
- [ ] One `CardDefinition` row `earthquake` (`BossCard`, `Earthquake`, approved `PowerCost`, `LoadoutCopyLimit = 1`, approved `Shield` effect) provisioned by an insert-only migration with mirrored `Down`; the provisioned set is 9; no `PlayerUnlockedCard`, loadout or API surface changes.
- [ ] `BossDefinition.CastPriorityScore` (nullable, `CHECK NULL OR >= 0`) provisioned; `earthquake`'s approved score stored; the overlay loads/validates it and fails closed for a migrated Boss with `NULL`/negative.
- [ ] At battle start the Boss Card, active form and score resolve exactly once and are cached; a missing/invalid card or score fails the battle creation (no legacy fallback).
- [ ] Selection is deterministic and exhaustive over the five cases; board wins ties; score `0..15` always loses to a board action.
- [ ] A cast-selected round: no Boss swap/cascade/generation/board event, no Boss attack/damage event, Passive 18b still runs, exactly one `BossCardCast`, Power deducted exactly once, Shield applied under `COMBAT_RULES.md` §4 (refresh-replaces, trigger-based, at most one), cooldown set to the authored value.
- [ ] A board-selected round for the migrated Boss: board action exactly as today; no `BossSkillCast`; cooldown ticks.
- [ ] Unmigrated Bosses: legacy board-then-Skill order unchanged; their existing suites pass **unmodified**.
- [ ] `BossCardCast` wire item: `type`/`cardId`/`sourceId` only, inside the round's single ordered batch, after 18b events, never for a board phase, never a `CardCast`, never concurrently with `BossSkillCast` for the same ability; the client parses and presents it with the boss-skill display-name resolver.
- [ ] CAS retry: one state write, one ordered batch, one deduction, one cooldown write, one event; the same committed state selects the same action.
- [ ] Boss Power spend emits no `PowerChanged`; the resulting Boss Power/cooldown reach the client only through `bossState`.
- [ ] Player `CardCast`/`PetSkillCast` contracts byte-identical; the one-cast-per-Turn allowance is neither read nor spent.
- [ ] Balance cadence metrics re-derived with a new metric (not hand-edited); protected historical artifacts untouched.
- [ ] Full gates: `dotnet build`, backend tests, frontend tests/build, `git diff --check`; pre-existing failure set documented as unchanged; quality review; two-stage commit with exact declared set.

### 7.4 Out of scope for Slice 3

The other four abilities and any effect-vocabulary extension; retiring
`SkillCharge`/`BossSkillCast`/the legacy handler; API/collection surface changes;
the fifth Gem/Power-drain Gem; fixing the pre-existing `BaseDamage` provisioning
drift or the unrelated stale `BattleHub` comment; any client-authoritative logic;
unrelated test debt or balance retuning (U-5).

---

## 8. Prerequisite vs. same-implementation dependencies

| Work | Classification | Why |
|---|---|---|
| Operator answers OD-1/OD-2/OD-3 | **Prerequisite** (external, before intake) | No documentation, migration, or code may record them before approval (`AGENTS.md` §4, §7, §17) |
| §5 documentation + ADR amendments | **Same implementation, first work item** | Documentation-first ordering; records the approved contract; not a separate lifecycle task |
| Boss Card row + score column + lookups/migrations | **Same implementation, prerequisite to runtime within the task** | Slice 3's acceptance cannot be met on fixtures; but no independent task boundary |
| Domain policy, round integration, event/wire | **Same implementation** | One policy/acceptance boundary; splitting would create an intermediate task with no observable outcome |
| Client presentation | **Same implementation** | The exit criterion is presentation of the real event; a separate client task would leave Slice 3 falsely incomplete |
| Regression + balance re-derivation | **Same implementation** | Proves the slice; the protected-artifact rule forbids in-place regeneration |
| Slice 4 vocabulary design | Independent future work (deferred) | A genuine independent design risk (ATK buff/debuff, Power drain, next-qualifying-attack semantics); must be approved before Slice 4, not now |
| Slice 4 migrations / Slice 5 retirement | Future slices | Gated by Slice 4's parity work |
| P-8 Signature-Skill category guard, P-9 `BaseDamage` drift/comments | Independent defects (reported, no task now) | Neither blocks Slice 3; `AGENTS.md` §16 forbids bundling. The Provisioning test drift is a pre-existing failure set to keep documented, not to fix here |

**TASK-306 proposals P-1…P-9 disposition:** P-1 (docs/ADR) → §5 + Slice 3 W1;
P-2 (provision card/resolution) → W2; P-3 (policy/cooldown/event) → W3–W5;
P-4 (client) → W6; P-5 (vocabulary design) → deferred to Slice 4's prerequisite,
not registered; P-6a–d → Slice 4, not registered; P-7 → Slice 5, not registered;
P-8 and P-9 → reported, no task. **No TASK-306 proposal becomes its own task.**

---

## 9. Risks and reported defects (not fixed here)

```text
R-1  The three operator decisions are the critical path. The contract is ready;
     the authorization/values are not. No Slice 3 task may be registered before
     they are recorded.
R-2  A cast phase forgoes the Boss board action and attack (documented, not a
     defect). Its pacing effect is OD-3b's balance question and cannot be
     assessed until the value exists.
R-3  OD-3a expands ADR-024's overlay (four -> five values). If the operator
     rejects the schema change, no alternative carrier satisfies the current
     ownership sentence; BOSS_RULES.md §4.3 step 4 would then need an explicit
     amendment and the Slice 3 task must not start.
R-4  The battle-scoped content cache is in-process (F14): a Redis-recovered battle
     handled by a different process already faults today. The Boss Card/score
     resolution must reuse the same single creation-time path and must not add a
     second recovery assumption; fixing the pre-existing recovery gap is out of
     scope and must stay documented.
R-5  The balance harness pins charge-based cadence in five per-seed rows
     (BalanceEvidenceMeasurementCorrectionTests.cs:219-237). Slice 3 will change
     those numbers for a migrated Boss; they must be re-derived, and
     ProtectedArtifactGuard must not be bypassed.
R-6  The Signature-Skill derivation has no category guard
     (CardLoadoutService.cs:198-232 checks only null): a mis-provisioned
     BossCard row referenced as a Pet's Signature Skill would be snapshotted.
     Provisioning discipline is not a guard; report P-8, do not fix here.
R-7  Pre-existing content/provisioning drift: the two Boss migrations' JSON
     carries legacy baseDamage 150/120/100/150/180 while the Domain baseline and
     Boss rules carry 0 (C-1; TASK-305 §8's known failures). Slice 3 must not
     "fix" it in passing; its failure set must remain unchanged.
R-8  A cast-selected phase may still reshuffle a deadlocked board before
     selection (deterministic; seeded; emits no event — ADR-026 item 8). The
     approved docs must state it so it is not later mistaken for a defect.
R-9  The stale cooldown comment and any other contradictory comment inside a
     file Slice 3 touches are corrected there (comment-only); comments in files
     the slice does not touch are reported, not edited (AGENTS.md §16).

Reported, not fixed (AGENTS.md §16):
- DATABASE.md vs CARD_RULES.md Boss Card identity form (authoritative conflict;
  OD-1b is the resolution).
- BOSS_RULES.md:914 vs §6.3.1's unresolved magnitudes (internal inconsistency;
  post-approval edit).
- DATABASE.md's BossDefinition block still calls the ADR-024 columns "proposed"
  although ADR-024 is Accepted and implemented (reconciled by §5 row 3).
- BattleHub.cs:366-372 "three-member" projection comment vs the four-member
  record; ADR-029 §1.2/§1.3 decision-time snapshots that no longer match source;
  the two pre-existing Infrastructure test families recorded in TASK-305 §8.
```

---

## 10. Final verdict

**`READY FOR OPERATOR APPROVAL`**

The consolidated contract is coherent and verified: one canonical Boss Card
definition and provisioning path, one deterministic cast-priority score contract,
one fully specified initial card shape, one active-form rule, an unambiguous
cooldown reading that required no new decision, and a complete one-action /
atomicity / event contract whose only code gap is additive. TASK-306's decision
set is reduced from ten items to **three operator decisions** (OD-1, OD-2, OD-3),
and no lifecycle task is registered beyond this one.

The verdict is **not** `READY FOR SLICE 3 IMPLEMENTATION` because the
authorization (OD-1), the initial content values (OD-2) and the score carrier/value
(OD-3) are product/balance decisions that no evidence can settle and that
`AGENTS.md` §7 forbids inventing. It is **not** `BLOCKED` as a lifecycle state
because the assigned investigation deliverable is complete; the operator gate is
the documented next action, exactly the pairing TASK-306 recorded.

### Smallest decision package that unblocks Slice 3

```text
1. OD-1  Approve the consolidated Boss Card contract: Option A (reuse
         CardDefinition), the identity exception (BossCard id = ability SkillId),
         provisioned-content presence as the once-per-battle active form with only
         Son Thach Ve enabled, and the ADR-029 amendment folding the
         definition/provisioning prerequisite into Slice 3.
2. OD-2  Author earthquake's PowerCost (integer 0..100) and confirm/replace the
         Shield magnitude (implemented 150 Flat; or another positive integer Flat;
         or PercentMaxHp as a unit change). Display Name transcribes "Earthquake".
3. OD-3  Approve the nullable CastPriorityScore overlay column (ADR-024 four -> five
         values) and author earthquake's score (integer >= 0; 0..15 always yields
         the board action and >= 16 can win).

After those three answers, exactly one Slice 3 task (§7) is registered and can be
declared complete only against the §7.3 checklist with a provisioned Boss Card
executing on the real runtime, wire and client.
```
