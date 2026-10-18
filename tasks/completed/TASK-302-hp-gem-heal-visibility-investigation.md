# TASK-302 — Investigate and Close the HP Gem Healing Visibility Report

<!--
  PRODUCT OWNER DECISION CLOSURE RECORD.

  This task formalizes the Product Owner's decision that RESOLVED its own stop
  condition (Option C — the HP-Gem heal clamp, overheal discard, and
  same-Round-offset behaviour are intended). It invents no rule, implements
  nothing, and authorizes nothing.

  Decision provenance: the Product Owner's response to the Option A / B / C
  request recorded in this task's `## Proposed Resolutions` while it was
  BLOCKED. The decision is recorded verbatim in
  `## Product Owner Decision (recorded verbatim)` below.
-->

---

## Metadata

```text
Task ID:           TASK-302
Type:              BUG (closed as confirmed intended behavior — no defect)
Status:            DONE
Risk:              LOW (documentation/decision record; no executable change)
Priority:          HIGH
Primary Agent:     gameplay
Supporting Agents: backend, client, realtime, testing, review
Workflow:          development/bug-fix.md
Skills:            gameplay/gameplay-behavior-derivation, gameplay/authority-determinism-audit,
                   testing/test-scenario-generation, quality/implementation-review,
                   quality/scope-validation
Dependencies:      TASK-260, TASK-261, TASK-299, TASK-301
Declared Files:    None (no implementation slice; this record is the only authored file)
```

---

## Objective

Investigate the reported defect "matching HP gems does not appear to increase the
active Pet's HP", trace the complete authoritative state flow (cascade → resource
generation → heal resolution → committed `BattleState` → `BattleStateUpdated` →
client runtime → `BattleScene`), establish the actual root cause with code and
executed-test evidence, and fix only a confirmed implementation deviation — without
changing the documented HP-Gem rate, the combat rules, the turn order, the
server-authoritative model, or the SignalR protocol.

**Outcome:** the investigation found no implementation deviation and raised the
behaviour as a Product Owner decision (`AGENTS.md` §7/§17/§18/§20). The Product
Owner has now ruled that the behaviour is **intended**. This task is closed as a
confirmed intended behaviour (former Option C), with no production, test, or
frontend change.

---

## Authoritative References

- `docs/01-game-design/GAME_RULES.md` §1.4, §17 (steps 12, 13, 14, 15–17, 18a–18d, 19a) — fixed resolution order
- `docs/01-game-design/COMBAT_RULES.md` §1.1, §2 items 5–6, §3, §4 items 1/5/7 — per-Gem rates, actor-relative ownership, heal resolution, overheal clamp, dead-target guard
- `docs/01-game-design/MATCH3_RULES.md` §3.3 item 3, §4.2, §5.7 — cleared-cell union, cascade accumulation
- `docs/01-game-design/BOSS_RULES.md` §5 item 4, §6.2 — Boss terminal check before the Boss response; Thủy Ma's heal modifier
- `docs/01-game-design/PET_RULES.md` §5 — Pet progression (no HP-gem interaction)
- `docs/02-technical/GAME_STATE.md` §2.3, §3, §5.1 — HP as authoritative state, transient pools, one write-back per action
- `docs/02-technical/GAME_EVENTS.md` §1.1, §2 — no heal event exists
- `docs/02-technical/SIGNALR_PROTOCOL.md` §3.1, §4.3 item 15 — push precedes batch; `petState.hp`/`maxHp` are settled state; no heal event is expected
- `docs/02-technical/REDIS_STATE.md` §2 item 2, §4 — active-state record is the source of truth; one write-back per action
- `docs/03-decisions/ADR/ADR-001-server-authoritative-battle.md`, `ADR-005`, `ADR-008`, `ADR-009`, `ADR-025`, `ADR-026` (items 4, D1, D4, §8)
- `AGENTS.md` §2, §4, §7, §16, §17, §22, §23
- `tasks/TASK_LIFECYCLE.md` §1, §3, §4, §5, §6; `tasks/README.md` §7, §10; `.ai/workflow/core/completion.md` §1–§3; `.ai/workflow/quality/review.md` §1

---

## Scope

### In Scope
- End-to-end audit of the HP-Gem heal path, code and executed evidence
- A verdict that distinguishes the outcomes A–E of the task brief
- A STOP CONDITION report if the confirmed behaviour is rule-correct rather than a defect
- Recording the Product Owner decision that resolves it, and closing the task per lifecycle

### Out of Scope
- Any change to the HP-Gem base rate (`ResourceGenerator.HpGemBaseHeal`), to combat/formula rules, or to the §17 order
- Any new Battle Event, SignalR method/member, or wire-schema change
- Any client-side HP derivation, prediction, or optimistic healing
- Temporary HP / overheal retention (**explicitly excluded by the decision**)
- Balance re-tuning (TASK-298/TASK-300 territory) and protected balance artifacts
- Modifying completed task records (TASK-299, TASK-301 are immutable)

---

## Current State

The HP-Gem heal is implemented across `ResourceGenerator` (generation, Pet/Boss
heal, actor-relative dispatch), `HealResolution` (canonical heal step, overheal
clamp, dead-target guard, Thủy Ma modifier read), `SwapExecutor` (Player step
12/14 write inside the single write-back), `BattleStateService` (Boss step 18a
effects, later PetState assignments), `BattleHub` (the §4 projection),
`GameRuntime` (verbatim ingestion) and `BattleScene` (`presentPetHp`). TASK-301
audited this path and reported no production deviation; this task re-audited it
against the newly reported symptom, including the wire and client boundaries
that TASK-301 verified by inspection only. The Product Owner has now confirmed
the current behaviour is intended, so no code, test, or protocol change is made.

---

## Declared File Set (P-2 / P-5)

Exact declared implementation files for TASK-302:

```text
None
```

*(Note: The task manifest file itself is excluded from the implementation
declared file set. `None` above strictly matches the `Declared Files:` field in
`## Metadata`: this task declares no implementation, test, or documentation
file — only this record.)*

---

## Product Owner Decision (recorded verbatim)

Per the `TASK-242` / `TASK-108` decision-register precedent, the decision is
recorded here verbatim and is the authoritative statement of product intent for
this question.

> **TASK-302 — Product Owner Decision: Preserve HP Healing Cap**
>
> The intended gameplay rule is:
>
> **HP Gems restore HP only when the Pet's current HP is below MaxHP. Healing must never increase HP above MaxHP. Any overheal is discarded.**
>
> Examples:
> - `500/1000` + `60` healing → `560/1000`.
> - `980/1000` + `60` healing → `1000/1000`; excess healing is discarded.
> - `1000/1000` + `60` healing → `1000/1000`; no HP increase occurs.
> - If a Boss damages the Pet later in the same round, the final HP may equal the starting HP. This is valid under the current turn-resolution rules.
>
> Instructions:
> 1. Record this Product Owner decision in TASK-302's blocked task record and the applicable decision documentation required by repository policy.
> 2. Verify that the existing implementation and authoritative specifications already conform to this rule.
> 3. Do not implement temporary HP, change combat ordering, change healing values, or add a heal-specific protocol event.
> 4. If the existing implementation conforms, close TASK-302 as a confirmed intended behavior according to `AGENTS.md`, `tasks/TASK_LIFECYCLE.md`, and the completion workflow. Update task records and documentation only as required by those rules.
> 5. If the implementation contradicts the decision, stop and report the exact evidence and required scope before making gameplay changes.

**Decision identity:**

```text
Decision ID:      TASK-302 (former Option C — "close as rule-correct, no change")
Decision:         Preserve the HP healing cap (overheal discarded)
Made by:          Product Owner
Recorded by:      TASK-302 (this record)
Source question:  TASK-302 `## Stop Conditions` and `## Proposed Resolutions`
                  (while BLOCKED)
Supersedes:       Option A (temporary HP) and Option B (protocol change) —
                  neither is authorized; neither is created by this record
```

**Binding statements of this decision:**

```text
T302-1  An HP-Gem heal applies only while the target's current HP is below its
        MaxHP. At HP == MaxHP the heal changes nothing; no HP increase occurs.

T302-2  Healing never raises HP above MaxHP. The ceiling is the target's own
        MaxHP (COMBAT_RULES.md §4 item 1), not a constant.

T302-3  Overheal is discarded and stored nowhere — not carried into a later
        Round, not written to another state member, not delivered to the client.

T302-4  No temporary HP, overheal-retention pool, or equivalent state member is
        introduced. COMBAT_RULES.md §4 item 1's Relic carve-out ("unless a Relic
        explicitly grants overheal/temp-HP") stays exactly as written; no MVP
        Relic exercises it, and this decision neither exercises nor expands it.

T302-5  The §17 resolution order is unchanged: the step-14 heal precedes the
        step-18d Boss attack in the same atomic Round, so a Round whose heal is
        wholly or partly offset by the Boss's own attack may legitimately settle
        at HP equal to the Round's starting HP. That outcome is intended, not a
        defect.

T302-6  Healing remains a state-only change: no heal Battle Event and no heal
        SignalR member is added (GAME_EVENTS.md §2, SIGNALR_PROTOCOL.md §4.3
        item 15). The client continues to render the settled `petState.hp`.

T302-7  The HP-Gem base output, the heal values, the combat formulas, and the
        server-authoritative model are unchanged by this decision.
```

---

## Decision-Record Location and Why No `docs/` Change Was Required

`AGENTS.md` §17 (and `.ai/workflow/documentation/documentation-change.md` §3)
make the **canonical owner** document the required record location for any rule
statement, and `.ai/workflow/documentation/documentation-change.md` §2 forbids
duplicating a rule into a second document.

```text
Rule / contract                                   Canonical owner
────────────────────────────────────────────────  ──────────────────────────────────────────
Heal cap = MaxHP, overheal discarded              COMBAT_RULES.md §4 item 1
HP-Gem pool routes through that clamp             COMBAT_RULES.md §2 item 5
Clamp runs after heal modifiers                   COMBAT_RULES.md §4 item 7
Heal (step 14) precedes Boss attack (step 18d)    GAME_RULES.md §17
HP is authoritative, one write-back per action    GAME_STATE.md §2.3, §5.1
No heal event; settled `petState.hp` only         GAME_EVENTS.md §2,
                                                  SIGNALR_PROTOCOL.md §4.3 item 15
```

Every one of those statements already exists and already says what this decision
says (verified line-by-line in `## Conformance Verification A` below). The
decision therefore **confirms** documented intent; it changes no design. Per
`AGENTS.md` §17, the documentation-first sequence ("update the design → update
the source-of-truth document → …") applies only to an intentional design change,
which this is not. Writing the decision into `COMBAT_RULES.md`/`GAME_RULES.md`
would restate rules already owned there and would duplicate `SIGNALR_PROTOCOL.md`
§4.3 item 15's delivery rule into a gameplay document — the exact duplication
`.ai/workflow/documentation/documentation-change.md` §2 prohibits.

**Therefore:** the Product Owner decision is recorded in this task record (the
repository's decision-record location for decisions that confirm existing
documents — `tasks/completed/TASK-242-*.md` precedent, which likewise changed
zero `docs/` files), and **no `docs/` file is edited**. `AGENTS.md` §16 (work
only on the requested task) and the task's own completion criteria ("no
unnecessary production or frontend changes") both point the same way.

---

## Conformance Verification A — Authoritative Specification

The decision was compared against the specification **first**; the
implementation was checked afterwards against that specification.

| # | Decision statement | Authoritative statement (expected) | Verdict |
|---|---|---|---|
| 1 | Heal applies only below MaxHP; ceiling is MaxHP | `COMBAT_RULES.md:718` — "Heal effects restore HP up to Max HP"; `:279`, `:287` — HP Gem "+20 Heal pool … (clamped to MaxHP, §4)" | CONFORMS |
| 2 | Overheal is discarded, stored nowhere | `COMBAT_RULES.md:718-719` — "overheal is discarded unless a Relic explicitly grants overheal/temp-HP"; no MVP Relic grants it (`:752-754`, `ROADMAP.md` Phase 1 "No Relics yet") | CONFORMS |
| 3 | HP-Gem heals are subject to that clamp | `COMBAT_RULES.md:290-294` — "The HP Gem heal pool is a healing source … receives … the item-1 overheal clamp"; `:800` — clamp is the last Heal Resolution step | CONFORMS |
| 4 | No temp-HP state member exists for HP-Gem heals | `COMBAT_RULES.md:718-719` leaves temp-HP to Relics only; `GAME_STATE.md` §2.3 defines no overheal/temp-HP member; `COMBAT_RULES.md:865-866` — the heal step adds no state member | CONFORMS |
| 5 | Heal precedes same-Round Boss damage; final HP may equal the start | `GAME_RULES.md:392-412` fixes step 14 (Resolve Player Effects) before step 18 (Boss Response) and `:470-480` puts the damage at 18d | CONFORMS |
| 6 | Healing is not separately reported; settled HP only | `SIGNALR_PROTOCOL.md:2358-2363, 2371-2377` — the client computes no heal, "does not expect a heal event (none exists)", and `hp` is the settled value; `GAME_EVENTS.md` §2 defines no heal event | CONFORMS |
| 7 | Server authority and values unchanged | `GAME_RULES.md` §18 + `ADR-001`; `COMBAT_RULES.md` §2 HP row (`+20`), §4 item 1 | CONFORMS |

**Specification verdict: fully conformant with the decision. No spec change is
required or made.**

---

## Conformance Verification B — Implementation

| # | Contract | Implementation evidence | Verdict |
|---|---|---|---|
| 1 | Clamp to MaxHP, no stored overheal | `src/backend/GameServer.Domain/Combat/HealResolution.cs:82` — `var updatedHp = Math.Min(petState.HP + finalHeal, petState.MaxHP);` then `petState with { HP = updatedHp }`; no other field written | CONFORMS |
| 2 | At/above MaxHP the heal changes nothing | same expression returns `MaxHP` when `HP == MaxHP`; `Assert.Equal(state, updated)` is pinned by `ApplyHeal_ShouldLeaveAnAlreadyFullStateUnchanged` | CONFORMS |
| 3 | HP-Gem pool routed through the same clamp | `ResourceGenerator.ApplyHeal` (`ResourceGenerator.cs:474-484`) delegates to `HealResolution.ApplyPetHeal`; doc comment at `:418-448` states `new HP = min(previous HP + generation.HealPool, MaxHP)` and "the discarded excess is stored nowhere" | CONFORMS |
| 4 | Applied once, at step 14, inside the single write-back | `SwapExecution.cs:477` — `ResourceGenerator.ApplyHeal(state.PetState, generation, state.BossState.ActiveStatusEffects)`; committed `PetState` assigned in the one write-back (`SwapExecution.cs:523-560`) | CONFORMS |
| 5 | No temp-HP / overheal state member anywhere | repository-wide search for `Overheal|overheal|TempHp|TemporaryHp|OverhealPool` under `src/` returns only explanatory comments in `ResourceGenerator.cs:422-424` — no member, no field, no pool | CONFORMS |
| 6 | No heal event, no protocol member | no heal member in `BattleEventType`; the wire carries settled `petState.hp`/`maxHp` only (`BattleHub.cs:243-244, 1486-1503`) | CONFORMS |
| 7 | Heal rate and formulas untouched | `ResourceGenerator.HpGemBaseHeal = 20` unchanged by this task (no file changed) | CONFORMS |

**Implementation verdict: fully conformant with the decision. No production or
frontend change is required or made.**

---

## Conformance Verification C — Executed Evidence

All commands below were executed on this working tree at the HEAD recorded in
`## Validation Results`. Focused suites first, full suites after.

| Decision example | Pinned by | Result |
|---|---|---|
| `500/1000` + `60` → `560/1000` (whole pool below the cap) | `HpGemMechanicsAuditTests.HpGems_ShouldHealOnlyTheActingActor_AcrossOneAtomicRound` — Pet pinned `500/1000`, pool = `playerHpCells × 20`, expectation `min(500 + pool, 1000)` | PASS |
| same branch, unit level | `PlayerEffectHealingTests.ApplyHeal_ShouldIncreaseHpByTheWholePoolWhenItFitsUnderMaxHp` (600 + 150 → 750), `…ShouldHealFromOneHpBelowTheCeiling` (999 + 1 → 1000) | PASS |
| `980/1000` + `60` → `1000/1000`, excess discarded | the clamped branch of the same rule: `PlayerEffectHealingTests.ApplyHeal_ShouldClampToMaxHpAndDiscardOverheal` (400 + 10 000 → 1000, and a second identical pool heals nothing), `…ShouldClampToMaxHpWhenTheOverhealIsASinglePoint` (999 + 2 → 1000), `HpGemMechanicsAuditTests.HpGems_AtFullHp_ShouldStopExactlyAtMaxHp_AndDiscardTheOverheal` (995 + pool → 1000), `CommittedSwap_ShouldNotExceedMaxHpHoweverLargeThePoolIs` (real HP-Gem Match, 995 + ≥60 → 1000) | PASS |
| `1000/1000` + `60` → `1000/1000`, no increase | `PlayerEffectHealingTests.ApplyHeal_ShouldLeaveHpAtMaxHpWhenAlreadyFull` (pool 0 / 20 / 1 000 000 → 1000), `ApplyHeal_ShouldLeaveAnAlreadyFullStateUnchanged` (whole state unchanged), `CommittedSwap_ShouldLeaveHpAtMaxHpWhenAlreadyFull` (real HP-Gem Match from the battle-start state → `PetState.DefaultHP` = `DefaultMaxHP` = 1000), `ActorRelativeMatchEffectsTests.ApplyBossHeal_WhenAlreadyAtFullHp_ShouldRemainAtMaxHp` | PASS |
| final HP may equal the start when the Boss damages the Pet later in the Round | `HpGemMechanicsAuditTests.HpGems_ShouldHealOnlyTheActingActor_AcrossOneAtomicRound` derives the committed expectation as `min(PetPinnedHp + pool, MaxHP) − damageDealtBy(Boss)` — the §17 step-14→step-18d composition — and `BossPassiveEffectsTests` pins the clamp zone (`:490`) | PASS |

**Coverage note (reported, not changed):** the decision's exact `980/1000 + 60`
pair is not pinned by an exact-value test; it exercises the same clamped branch
as the pinned `995 + 60 → 1000` and `999 + 2 → 1000` cases, and the clamp
expression itself is pinned directly. The rule is therefore verified; an
additional exact-value case would add no new rule coverage and, per the
decision's instruction 4 ("update task records and documentation only as
required"), no test file was added by this task.

---

## Documentation Consistency Audit

Every statement about the heal cap / overheal across `docs/` was enumerated and
compared (repository-wide search for `overheal`):

| Document | Statement | Consistent? |
|---|---|---|
| `COMBAT_RULES.md:718-727` §4 item 1 | canonical owner: restore up to Max HP; overheal discarded unless a Relic grants it; dead target not healed | — (owner) |
| `COMBAT_RULES.md:279, 287, 290-294` §2 | HP Gem `+20` Heal pool "clamped to MaxHP (§4)"; the pool is a healing source receiving item 1's clamp | YES |
| `COMBAT_RULES.md:800, 841-845` §4 item 7 | clamp is the last Heal Resolution step, unchanged | YES |
| `COMBAT_RULES.md:849-851` §4 item 7 Scope | Boss-side HP-Gem heal applies item 1's clamp (overheal discarded) | YES |
| `COMBAT_RULES.md:335` | Heal clamp / overheal discard unchanged | YES |
| `BOSS_RULES.md:403, 448-450` §6.2 | Mộc Yêu regen lands "up to Max HP; overheal is discarded", referenced not restated; clamp applied after the modifier step | YES |
| `GAME_RULES.md` §17 (§392-412, §470-480) | step-14 heal precedes step-18d Boss attack | YES |
| `GAME_EVENTS.md` §2, `SIGNALR_PROTOCOL.md` §4.3 item 15 | no heal event; settled HP only | YES |

**Result: the HP cap and the overheal-discard behaviour are documented
consistently, with `COMBAT_RULES.md` §4 item 1 as the single owner and every
other mention a reference to it. No contradiction was found and no documentation
edit is required.**

**Pre-existing wording observation carried forward (reported, not changed; not a
behaviour question):** `COMBAT_RULES.md` §2's actor-relative HP bullet describes
the Boss's HP-Gem heal as passing "through Heal Resolution (§4 item 7)", while
§4 item 7's Scope clause states the Boss-side HP change applies only item 1's
clamp and the dead-target guard. The implementation follows §4 item 7, the more
specific and canonical statement (already recorded by TASK-301 §"Non-defect
observations"). It changes no observable value in MVP — no MVP heal modifier
applies to Boss HP — so it is not a conflict that blocks anything and is out of
this task's scope (`AGENTS.md` §16).

---

## Investigation Performed (code + executed evidence)

The full path was traced by call site, then exercised end-to-end. Two temporary
in-repository probes were written, executed, and deleted again (they are not
part of the deliverable): one drove the **real Api DI container + real SignalR hub**
(`JoinBattle` → `Swap`) on a pinned board, one drove the **frontend scene
harness** with the batch/state payload the server actually produced. The
frontend suite, the Domain suite, the Api suite and the Application suite were
executed (results in §Validation).

### 1. `CascadeResolver.Resolve` — HP Gems in the right pass, cascades preserved

| Check | Evidence | Verdict |
|---|---|---|
| HP cells are carried on the pass that cleared them, each once | `BoardResolver.ResolvePass` builds `ClearedGems` from `MatchDetector.ClearedCellUnion` over the pre-removal board (`BoardResolver.cs:168-240`) | MATCH |
| Overlapping shapes count once | union is a `SortedSet<int>`; audit suite row 2 (TASK-301) | MATCH |
| Every cascade pass accumulates | probe with a 2-pass swap: `passes=2`, `hpCellsPerPass=3,0`, `ClearedGemCounts.Hp=3`, `HealPool=60` | MATCH |

### 2. `ResourceGenerator.Generate` — HP Gems → `HealPool`

`BaseOutputFor(Hp) = 20` (`ResourceGenerator.cs:201-208, 255`); `Generate` sums
`BaseOutput × unique cleared cells` over every pass (`ResourceGenerator.cs:318-361`).
Probe: 3 HP cells → `HealPool=60`. No multiplier, no division, no other
collection. Pool is transient (`GAME_STATE.md` §3) and is not discarded.

### 3. `ApplyHeal` / `HealResolution.ApplyPetHeal` — pool reaches the write

`ApplyHeal` → `ApplyPetHeal` → `ResolveHealAmount` (Thủy Ma −50% read
cross-entity) → `Math.Min(HP + finalHeal, MaxHP)` (`HealResolution.cs:23-84`),
dead Pet returned unchanged. `SwapExecutor.Execute` supplies
`state.PetState` and the Boss's active effects (`SwapExecution.cs:477`). Probe:
Pet pinned 500/1000 + 3 HP cells → committed HP **560**.

### 4. `SwapExecutor.Execute` — the healed `PetState` is the committed one

`petState` (healed, then DEF buff) is assigned to `resolved.PetState` in the
single write-back (`SwapExecution.cs:523-560`). No later assignment inside the
executor. `WithEvents`/`WithState` carry the state and resources unchanged
(`SwapExecution.cs:211-265`).

### 5. `BattleStateService` — nothing later overwrites the healed HP

Every subsequent `PetState` write was enumerated and checked: Passive progress
(`BattleStateService.cs:1446-1449`), Relic firing points (`1506-1560`, `2832-2891` — writes Power + modifier collections only; the pre-heal
`observationState` is a read-only condition input and never becomes the state),
step 13 Power (`1564-1574`), Player→Boss attack (`1655-1659`), Boss step-18a
effects (`1841-1856` — the Boss branch returns the input Pet unchanged), Boss
Skill PowerDrain/Root (`2212-2263`), Boss→Pet attack (`2323-2339`), Pet DoT
(`2523-2527`), step 19a (`2578-2582`, `StatusEffectLifecycle.cs:647-671`),
commit-boundary board recovery (`2608`, `3099-3131`). A probe with the **full
live relic loadout attached** gave the same correct result (500 + 60 = 560).

### 6. Server → client synchronization

| Boundary | Evidence | Verdict |
|---|---|---|
| `BattleHub.ToPetStatePayload` | `petState.HP`/`MaxHP` mapped one-to-one (`BattleHub.cs:1486-1503`); `hp`/`maxHp` JSON names (`BattleHub.cs:243-244`) | MATCH |
| Existing wire-level regression guard | `ApiIntegrationTests.BattleStateUpdated_PetState_ShouldCarryTheAuthoritativeCombatValuesUnchanged` (`ApiIntegrationTests.cs:1274+`) already asserts the delivered `petState.hp`/`maxHp`/`power` equal the authoritative repository state — PASS | MATCH |
| Real `BattleStateUpdated` payload | probe printed `petState={… "hp":560,"maxHp":1000,"power":0}` for the committed state `HP=560` | MATCH |
| `ReceiveEvents` batch | probe batch: `SwapStarted(player)`, `MatchCreated HP[0,1,2]`, `GemMatched HP ×3`, `CascadeStepResolved`, `MatchedGemSummary HP ×3`, `PassiveCharged pet`, then the Boss cycle — no heal event, as documented | MATCH |
| `SignalRService` | verbatim transport, no transformation (`SignalRService.ts:632-694, 741-753`) | MATCH |
| `GameRuntime` | `readPetState` requires and stores `hp`/`maxHp` verbatim (`GameRuntime.ts:2533-2569`); sequence guard refuses only lower sequences (`2304-2341`) | MATCH |
| `BattleScene.presentPetHp` | writes delivered `petState.hp / petState.maxHp` verbatim (`BattleScene.ts:2868-2871`); presented by the `retaliation` phase (`5574-5578`) and by convergence (`6420-6446`) | MATCH |
| Client replay of the real batch | scene harness with the verbatim server batch + push `hp=560`: phases `swap / match / gravity / refill / gem summary / banner BOSS TURN / swap / match / gem summary / settle`, `petHpText = "560 / 1000"`, `heldSequence=null` | MATCH |
| Same batch + a legitimate 40-damage Boss instance, push `hp=520` | `… / settle / retaliation`, `petHpText = "520 / 1000"` | MATCH |
| Persistence | `BattleStateSerializer.ToPetStateJson`/`FromPetStateJson` round-trip `HP`/`MaxHP` (`BattleStateSerializer.cs:234-248, 517-547`); `BattleStateSerializationTests` pins 723/1042 | MATCH |

### 7. Distinguishing the task brief's outcomes A–E

| Outcome | Verdict | Evidence |
|---|---|---|
| A. `HealPool` not generated | **NO** | `Generate` + probes: 3 HP cells → 60 |
| B. `HealPool` generated but not applied | **NO** | committed `PetState.HP` = pinned + pool (560 / 500-probe) |
| C. Applied but offset by later legitimate combat effects | **YES, in rounds where the Boss's own resolution confirms an ATK match** | §17 step 18d damage is applied after step 14; audit suite and probe subtract exactly the delivered `DamageDealt(boss→player)` |
| D. Server correct, payload/client stale or wrong | **NO** | real-hub payload and verbatim client replay both carry/show the healed value |
| E. Healing succeeds but final HP legitimately equals start HP | **YES, while the Pet is at/near `MaxHP`** | `PetState.AtBattleCreation` starts the Pet at `HP = MaxHP = 1000`; `ApplyPetHeal` clamps to `MaxHP` and overheal is discarded per `COMBAT_RULES.md` §4 item 1 |

**Root cause of the reported symptom (confirmed):** the heal is applied and
delivered correctly, but two documented behaviours compose so that the
*settled* value the client is allowed to render frequently does not move:

1. the active Pet begins every battle at full HP and every heal is clamped to
   `MaxHP` with the excess discarded (`COMBAT_RULES.md` §4 item 1), and
2. the same atomic Round's Boss attack (§17 step 18d) is applied after the heal
   (§17 step 14), while the protocol delivers only the Round's settled
   `petState.hp` and defines no heal event (`SIGNALR_PROTOCOL.md` §4.3 item 15,
   `GAME_EVENTS.md` §2).

In steady state at `960/1000`: a 3-HP-Gem match (+60) is clamped to
`1000` (20 discarded), the Boss's ATK-gated instance returns it to `960`, and
the delivered `petState.hp` is `960` again — the readout is unchanged even though
the heal was applied. A representative real-engine series
(Hỏa Long / skilled / seed 20261001) shows the mechanism is not inert: 15
inter-turn HP increases, 24 decreases, 12 flat, HP reaching `1000` repeatedly.

**Confirmed implementation defects: none.** The heal plumbing, the committed
state, the wire payload and the client rendering all match the authoritative
documents. No production file was changed.

---

## Stop Condition Resolution

The stop condition raised while this task was BLOCKED is now **resolved by the
Product Owner decision recorded above** (former Option C). It is retained
verbatim below as history, with its disposition.

```text
DISPOSITION (recorded after the Product Owner decision)

Status:  RESOLVED — closed as confirmed intended behavior.
Basis:   Product Owner Decision "Preserve HP Healing Cap" (T302-1 … T302-7).
Effect:  Outcomes C and E of the investigation are ruled INTENDED. No gameplay
         change, no state-model change, no protocol change, and no documentation
         change is required. Options A (temporary HP) and B (protocol change) are
         NOT authorized and are NOT implemented.
```

```text
STOP CONDITION (historical — raised while BLOCKED, now resolved)

Problem:
The reported symptom ("matching HP gems does not appear to increase the active
Pet's HP") is reproduced only in the rule-correct cases: overheal discard at
MaxHP (outcome E) and same-Round Boss damage applied after the heal (outcome C).
No implementation deviation from the authoritative documents was found anywhere
in the cascade → generation → heal → commit → push → client-render path, so no
bug fix exists within the authorized scope.

Relevant sources:
  docs/01-game-design/COMBAT_RULES.md §4 item 1 (heal restores HP up to Max HP;
    overheal is discarded unless a Relic grants overheal/temp-HP)
  docs/01-game-design/GAME_RULES.md §17 (step 14 Resolve Player Effects precedes
    step 18d Boss Attack)
  docs/02-technical/SIGNALR_PROTOCOL.md §4.3 item 15 (the wire carries the
    settled petState.hp and "does not expect a heal event (none exists)")
  docs/02-technical/GAME_EVENTS.md §2 (no heal event is defined)
  docs/02-technical/GAME_STATE.md §2.3 (HP is authoritative state; §5.1 one
    write-back per action)
  docs/03-decisions/ADR/ADR-026 (item 4, D1, §8)
  Implementation evidence: ResourceGenerator.cs:201-208/318-361/474-484,
    SwapExecution.cs:477/523-560, HealResolution.cs:23-84,
    BattleStateService.cs:1446-1449/1564-1574/1841-1856/2323-2339/2578-2582,
    BattleHub.cs:243-244/1486-1503, GameRuntime.ts:2533-2569,
    BattleScene.ts:2868-2871/5574-5578/6420-6446, PetState.cs:896-899

Conflict / missing information:
There is no code-vs-documentation conflict: code and documents agree. What was
missing was a documented decision that a matched HP Gem must read as a visible HP
increase even when the heal is clamped by MaxHP and/or offset by the same
Round's Boss damage.

Waiting for:
Operator / Product Owner decision: (A) authorize the overheal-retention
GAMEPLAY-CHANGE, (B) authorize a protocol change to deliver the step-14 HP, or
(C) confirm the behaviour is intended and this report is closed as not-a-defect.
```

### Bug category determination (`development/bug-fix.md` §1)

```text
Implementation bug       NO — the code matches the documented rule
                         (Conformance Verification B).
Documentation bug        NO — the documents state the rule the Product Owner
                         confirms (Conformance Verification A).
Test bug                 NO — the tests assert the documented rule; none was changed.
Data bug                 NO — no persistent/active state was implicated.
Architecture bug         NO — no structural issue; the delivery model is documented
                         (SIGNALR_PROTOCOL.md §4.3 item 15).
Configuration/environment NO — the symptom reproduces from the documented rule alone.
──────────────────────────────────────────────────────────────────────────────
Conclusion               NOT A DEFECT. The report is closed as confirmed intended
                         behavior, so the workflow's "apply minimal fix" and
                         "add/update a regression test" steps have nothing to act
                         on (no behaviour changes; existing tests already pin it).
```

### Proposed Resolutions — disposition

```text
Option A (temporary HP)          NOT SELECTED — not authorized, not implemented.
Option B (protocol change)       NOT SELECTED — not authorized, not implemented.
Option C (close as rule-correct) SELECTED by the Product Owner. The behaviour is
                                 confirmed intended under T302-1 … T302-7, and
                                 this task is closed as a confirmed intended
                                 behavior with zero code, test, or docs change.
```

---

## Acceptance Criteria

- [x] The six required investigation areas are traced with code-level evidence.
- [x] The generated `HealPool`, the healed committed `PetState`, the
      `BattleStateUpdated` payload and the client rendering are each verified by
      executed tests/probes.
- [x] Outcomes A–E are distinguished with evidence.
- [x] The Product Owner decision resolving the stop condition is recorded
      verbatim in this record (`## Product Owner Decision (recorded verbatim)`).
- [x] The decision's record location is determined per repository policy and the
      absence of a required `docs/` change is justified
      (`## Decision-Record Location and Why No docs/ Change Was Required`).
- [x] The existing implementation and authoritative specifications are verified
      as conformant to the decision (`## Conformance Verification A/B/C`). No
      contradicting implementation was found, so the "stop and report" branch of
      the decision's instruction 5 is not triggered.
- [x] No temporary-HP mechanism, combat-ordering change, healing-value change, or
      heal-specific protocol event was introduced (no file changed).
- [x] The HP cap and overheal-discard behaviour are documented consistently
      (`## Documentation Consistency Audit`).
- [x] Required verification gates executed; pre-existing failures distinguished
      from task-related ones (`## Validation Results`).
- [x] No unnecessary production or frontend change was introduced (`git status`
      shows only this record).
- [x] TASK-302 reaches the lifecycle state required for a verified, intended
      behavior (`## Lifecycle Transition Records`).

---

## Affected Files & Areas

```text
[ ] src/backend/ (Domain / Application / Infrastructure / Api)   — verified, unchanged
[ ] src/frontend/client/ (scenes / runtime / services / state / ui) — verified, unchanged
[ ] tests/  — verified, unchanged (temporary investigation probes were removed before filing)
[ ] docs/   — verified consistent, unchanged (see Decision-Record Location)
[x] tasks/  — this record only (moved from tasks/blocked/ to tasks/completed/)
```

---

## Implementation Notes

None. No production, test, or documentation file was modified by this task.
Temporary investigation probes (`TempHpWireProbeTests`, `TempRelicHealProbeTests`,
`TempCascadeHealProbeTests`, `TempHpSeriesProbeTests`, and an in-file
`SceneLifecycle` diagnostic) were removed after execution during the original
investigation; the working tree contains only this record.

---

## Testing Requirements

### Required Verification (depth: LOW/MEDIUM per `core/validation.md` §2 — documentation/decision record, no executable change)
```text
[x] Build/compile validation — backend solution builds with 0 errors
[x] Unit tests             — Domain heal/resource suites (focused), Application HP-Gem audit suite
[x] Integration tests      — full Application (atomic-Round) and Api (real DI + hub) suites
[x] Gameplay scenarios     — the decision's four examples and the §4 item 1 boundary set,
                             as pinned by existing tests (mapped in Conformance Verification C)
[x] Documentation validation — heal-cap/overheal statements audited for consistency across docs/
[x] Client presentation    — frontend suite (regression guard; no client file changed)
```

### Key Edge Cases (all verified)
- `COMBAT_RULES.md` §4 item 1 — overheal discarded at `MaxHP` (decision examples 2 and 3)
- `COMBAT_RULES.md` §4 item 1 — heal lands in full below `MaxHP` (decision example 1)
- §17 steps 14 vs 18d — same-Round Boss damage legitimately offsets the heal (decision example 4)
- `COMBAT_RULES.md` §2 item 6 — per-pass accumulation, overlap counted once
- `ADR-026` item 4 — the acting side owns its pool (Pet heals Pet, Boss heals Boss)
- `SIGNALR_PROTOCOL.md` §4.3 item 15 — no heal event; settled state only

---

## Validation Results

Executed on this working tree, HEAD `c7e8a33`, Windows, .NET SDK 10.0.400.

| Command | Result |
|---|---|
| `dotnet build src/backend/GameServer.sln` | PASS — 0 errors, 21 pre-existing warnings (xUnit analyzer + EF `MSB3277` version-conflict warnings; none introduced by this task) |
| `dotnet test tests/backend/GameServer.Domain.Tests/…csproj --filter "…PlayerEffectHealingTests\|…ActorRelativeMatchEffectsTests\|…ResourceGenerationTests"` | PASS — 86/86 |
| `dotnet test tests/backend/GameServer.Application.Tests/…csproj --filter "FullyQualifiedName~HpGemMechanicsAuditTests"` | PASS — 5/5 |
| `dotnet test src/backend/GameServer.sln` (full) — Domain | PASS — 1524/1524 |
| … — Api | PASS — 382/382 (42 s) |
| … — Application | 795 passed / 2 failed / 797 total — **both pre-existing**, re-confirmed by name: `BalanceEvidenceMeasurementCorrectionTests.TASK195_PerTurnSeries_ReconcilesTerminalCardCastWithoutPhantomTurn`, `BalanceEvidenceMeasurementCorrectionTests.TASK296_ReviewD_Canonical75RunCandidate_Verification` (recorded by TASK-300/TASK-301 as pinned balance-evidence distributions changed by TASK-299's DEF buff — unrelated to HP Gems) |
| … — Infrastructure | 472 passed / 4 failed / 476 total — **all four pre-existing**, re-confirmed by name: `BossPersistenceTests.Task172ProvisioningMigration_ShouldWriteTheDocumentedPassiveAndSkillDocuments`, `BossDefinitionBalanceLookupTests.GetDefinitionByIdentityAsync_ShouldPreserveTheNonPersistedSecondaryEffects`, `BossPersistenceTests.ProvisioningMigration_ShouldWriteTheDocumentedPassiveAndSkillDocuments`, `BossDefinitionPostgresProvisioningTests.Postgres_ShouldRoundTripBothJsonDocumentsToTheDomainDefinitions` (recorded by TASK-300/TASK-301; Postgres provisioning/JSON-document tests) |
| `npm run test:run` (frontend, `src/frontend/client`) | PASS — 24 files / 1264 tests |
| `git status --short` | Only this record is new/untracked; no production, test, docs, or frontend file modified; no unrelated working-tree change touched |

**Task-related failures: 0.** The six failures above are the identical
pre-existing set recorded by TASK-300 and TASK-301; none is in the HP-Gem heal
path and none changed with this task (this task changed no file). Total backend
tests executed: 1524 + 382 + 797 + 476 = 3179.

---

## Completion Evidence

### Commit
- Implementation slice (Phase A): `none — no implementation, test, documentation, or frontend file changed by this task`
- Phase B completion-record commit: **deferred — no commit created**
- Owns: `TASK-302`
- Files: `tasks/completed/TASK-302-hp-gem-heal-visibility-investigation.md` (this record, moved from `tasks/blocked/`)
- Shared with: `none`
- Unowned / pre-existing: `none` (the working tree contained no other change; the blocked record was the only untracked file and it is the same file being filed)

*(This task's sole deliverable is the decision/closure record. It declares no
implementation file, so no Phase A slice exists — the record is the deliverable
and is filed under the Phase B completion-record step per `TASK_LIFECYCLE.md` §6,
Option 1A, exactly as `TASK-242` did. Phase B was performed as a working-tree
filing only: the operator instruction governing this execution forbids any
commit or push without explicit authorization, so the record is moved and
written but not committed. When authorization is given, the Phase B filing
commit is:*

```text
git add tasks/completed/TASK-302-hp-gem-heal-visibility-investigation.md
git status --short            # expect only that one record staged
git commit -m "docs: close the hp gem healing cap investigation as intended behavior" \
  -m "TASK-302 — records the Product Owner decision preserving the HP healing cap and closes the report as confirmed intended behavior; no implementation slice." \
  -m "owns: TASK-302" \
  -m "files: tasks/completed/TASK-302-hp-gem-heal-visibility-investigation.md" \
  -m "shared with: none" \
  -m "unowned/pre-existing: none"
```

*The commit subject carries no task id (P-4). No implementation commit is
created, so nothing can be misattributed.)*

### Changed Files
- `tasks/completed/TASK-302-hp-gem-heal-visibility-investigation.md` — this
  decision/closure record (moved from `tasks/blocked/`; the only authored file).
- No other file in `src/`, `tests/`, `docs/`, or `src/frontend/client/` was
  created, modified, or deleted.

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic (no code changed; the
      audited client paths render delivered `hp`/`maxHp` only)
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1 — HP Gems and the heal
      clamp are existing MVP behaviour; this decision adds no system or content)
- [x] Confirmed no balance parameter, protected artifact, or TASK-298/299/300/301
      artifact was modified
- [x] Confirmed no temporary-HP mechanism, combat-ordering change, healing-value
      change, or heal-specific protocol event was added (decision instruction 3)

### Final Status
`DONE` — closed as a confirmed intended behavior under the Product Owner
decision. No implementation slice exists; the Phase B completion record is filed
in `tasks/completed/` in the working tree, with its commit deferred pending
operator authorization.

---

## Lifecycle Transition Records

<!--
  LIFECYCLE TRANSITION RECORD
  Governed by tasks/TASK_LIFECYCLE.md §1, §3, §4, §5, §6; tasks/README.md §7, §10.
-->

### 1. `IN PROGRESS` → `BLOCKED` (historical)

* **Triggering condition:** `AGENTS.md` §7 (Game Rule Protection), §18
  (architecture/protocol change requires an ADR and approval), §20 ("missing
  rule" / "behaviour is ambiguous").
* **Repository policy:** `tasks/README.md` §10 steps 1–4.
* **Destination path:** `tasks/blocked/TASK-302-hp-gem-heal-visibility-investigation.md`
* **Decision required:** Product Owner selection of Option A, B or C.

### 2. `BLOCKED` → `IN PROGRESS` (blocking condition resolved)

* **Transition:** `BLOCKED` → `IN PROGRESS`
* **Authorizing basis:** Product Owner decision recorded verbatim in
  `## Product Owner Decision (recorded verbatim)` — Option C selected, so the
  blocking condition ("waiting for the Product Owner decision") no longer holds.
* **Repository policy:** `tasks/TASK_LIFECYCLE.md` §2 ("BLOCKED → IN PROGRESS:
  blocking condition resolved by human decision") and §3 ("Resolution: a human
  provides the required decision … the agent updates the task file with the
  resolution; the agent moves the file back to `active/`").
* **Scope of the resumed work:** record the decision, verify specification and
  implementation conformance, audit documentation consistency, run the
  verification gates, and close the task. No gameplay change was authorized and
  none was attempted.
* **File movement:** `tasks/blocked/` → `tasks/completed/` (the intermediate
  `tasks/active/` placement `TASK_LIFECYCLE.md` §4's table shows for
  `BLOCKED → IN PROGRESS` → `IN REVIEW → DONE` was **not separately
  materialized**: the whole transition sequence completed inside one execution
  turn, no other agent read the record from `tasks/active/`, and the end state is
  exactly the one §4 requires. `tasks/active/` contains only `.gitkeep` before
  and after.)

### 3. `IN PROGRESS` → `IN REVIEW` (verification complete)

* **Transition:** `IN PROGRESS` → `IN REVIEW`
* **Basis:** `core/implementation.md` complete (record authored), `quality/testing.md`
  complete at the depth `core/validation.md` §2 selects for a documentation/
  decision record (build + focused unit + integration + documentation
  validation), and `## Validation Results` recorded.
* **Result:** no conflicting evidence; `quality/review.md` §1 passes (see below).

### 4. `IN REVIEW` → `DONE` (Phase B filing)

* **Transition:** `IN REVIEW` → `DONE`
* **Basis:** `core/completion.md` §1 —
  * Implementation completed: N/A (no implementation was authorized or needed);
    the deliverable is this record.
  * Relevant validation passed: `## Validation Results`.
  * No unresolved documentation conflict: `## Documentation Consistency Audit`
    (one pre-existing wording observation, reported and unchanged, is not a
    conflict).
  * No unintended scope expansion: no file outside this record was touched.
  * Documentation impact checked: no `docs/` change required
    (`## Decision-Record Location …`).
  * Architecture impact checked: none — no state member, protocol member, or
    module boundary changes.
  * Phase A implementation slice: none exists (no file to commit).
  * Phase B completion record: filed in `tasks/completed/` in the working tree;
    its commit is **deferred** under the operator instruction that forbids
    commit/push without explicit authorization.
* **Repository policy:** `tasks/TASK_LIFECYCLE.md` §4 (file move `active/`/`blocked/`
  → `completed/`), §6.1 Phase B, `tasks/README.md` §7.
* **Destination path:** `tasks/completed/TASK-302-hp-gem-heal-visibility-investigation.md`

### `quality/review.md` §1 — pass/fail

| Item | Verdict |
|---|---|
| Correctness | PASS — the decision matches `COMBAT_RULES.md` §4 item 1 + `GAME_RULES.md` §17 + `SIGNALR_PROTOCOL.md` §4.3 item 15 |
| Architecture | PASS — no architecture, state-model, or protocol change |
| Scope | PASS — record only; Options A and B explicitly not implemented |
| Tests | PASS — decision examples mapped to existing pinned tests; focused suites green |
| Documentation | PASS — consistency audited; no `docs/` edit required, with the reason recorded |
| Security | N/A — no auth, data, or exposure surface touched |
| Performance | N/A — no resolution-path code touched |
| Maintainability | PASS — no abstraction, state member, or mechanism added |
| Determinism (gameplay) | PASS — server authority, seeded RNG, and the fixed §17 order unchanged; the client still computes no HP |

---

## Remaining Issues

```text
1. PENDING COMMIT — the Phase B filing commit for this record has not been
   created, because this execution is under the explicit operator instruction
   "do not commit or push without explicit authorization". The exact command is
   recorded in ## Completion Evidence. No push was performed.

2. PRE-EXISTING TEST FAILURES (unchanged, not caused by this task):
   Application 2/797 — TASK195_PerTurnSeries_ReconcilesTerminalCardCastWithoutPhantomTurn,
     TASK296_ReviewD_Canonical75RunCandidate_Verification
   Infrastructure 4/476 — BossPersistenceTests.Task172ProvisioningMigration_…,
     BossPersistenceTests.ProvisioningMigration_…,
     BossDefinitionBalanceLookupTests.GetDefinitionByIdentityAsync_…,
     BossDefinitionPostgresProvisioningTests.Postgres_ShouldRoundTripBothJsonDocumentsToTheDomainDefinitions
   All six were recorded by TASK-300/TASK-301 as expected mechanical
   consequences of TASK-299 or as Postgres-container provisioning conditions.
   Follow-up (if desired) belongs to those tasks' recorded recommendations, not
   to TASK-302.

3. REPORTED, NOT CHANGED — COMBAT_RULES.md §2's actor-relative HP bullet reads
   slightly broadly about the Boss-side HP-Gem heal "passing through Heal
   Resolution (§4 item 7)" while §4 item 7's Scope clause applies only item 1's
   clamp and the dead-target guard. Pre-existing observation from TASK-301; no
   observable MVP value differs. A future documentation-consistency pass could
   tighten the §2 cross-reference; it needs its own (documentation) task.

4. REPORTED, NOT CHANGED — .ai/PROJECT_STATE.md is a stale navigation cache
   (audited HEAD 1d62313, completed-task count through TASK-262) and does not
   reflect the current tree. It is explicitly "not a source of truth", so this
   task did not refresh it; refreshing it is unrelated scope (AGENTS.md §16).

5. OPTIONAL FUTURE PRECISION — the decision's exact 980/1000 + 60 example is
   covered by the same clamped branch as the pinned 995 + 60 and 999 + 2 cases,
   not by an exact-value test. Adding one would add no rule coverage; it is
   recorded as an option, not as work this task should have done.
```
