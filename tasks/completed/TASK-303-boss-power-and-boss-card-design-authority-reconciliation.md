# TASK-303 — Boss Power & Boss Card Design Authority Reconciliation

<!--
  GEN-TASK EXECUTION MANIFEST / DESIGN AUTHORITY RECONCILIATION

  Principle: TASK = EXECUTION MANIFEST & DECISION PREPARATION.
  This task is design reconciliation, explicit operator decision capture, and
  authoritative-documentation work ONLY. No runtime implementation.

  STATUS: DONE — the operator granted the required authorizations (recorded in
  §4/§8), so the previously BLOCKED task was resumed, the authoritative
  documentation and ADR were amended, and every lifecycle gate was satisfied.
  No runtime code, database schema, test code or protocol implementation was
  changed.
-->

---

## Metadata

```text
Task ID:           TASK-303
Type:              DOCUMENTATION
Status:            DONE
Risk:              HIGH
Priority:          HIGH
Primary Agent:     orchestrator
Supporting Agents: gameplay, backend, realtime, review
Workflow:          documentation/documentation-change.md
                   (+ architecture/adr-change.md; + development/gameplay-change.md
                    design branch for the rule change itself)
Skills:            discovery/documentation-discovery, discovery/impact-analysis,
                   gameplay/authority-determinism-audit,
                   quality/documentation-consistency,
                   quality/scope-validation,
                   realtime/realtime-protocol-validation
Dependencies:      TASK-258 (COMPLETED — its §2 D2 decision is superseded here),
                   TASK-259, TASK-260, TASK-261 (COMPLETED — the in-force
                   shared-board Boss contract), ADR-021, ADR-024, ADR-026,
                   ADR-028
Declared Files:    docs/00-overview/MVP_SCOPE.md
                   docs/00-overview/GDD.md
                   docs/01-game-design/GAME_RULES.md
                   docs/01-game-design/CARD_RULES.md
                   docs/01-game-design/BOSS_RULES.md
                   docs/01-game-design/COMBAT_RULES.md
                   docs/02-technical/GAME_STATE.md
                   docs/02-technical/GAME_EVENTS.md
                   docs/02-technical/SIGNALR_PROTOCOL.md
                   docs/02-technical/REDIS_STATE.md
                   docs/02-technical/ARCHITECTURE.md
                   docs/03-decisions/ADR/ADR-026-shared-board-gameplay-and-actor-relative-match-resolution.md
                   docs/03-decisions/ADR/ADR-029-boss-as-player-opponent-boss-power-and-action-policy.md
                   docs/03-decisions/README.md
```

**Lifecycle path executed:**

```text
prior session:  BACKLOG -> READY -> IN PROGRESS -> BLOCKED
this session:   BLOCKED -> IN PROGRESS -> IN REVIEW -> DONE
```

The BLOCKED record was filed directly in `tasks/blocked/` by the prior session
and was never committed. On resolution the record was re-instantiated as
IN PROGRESS, its `Declared Files` manifest was **authorized with the work** (it
was `None` precisely because nothing was authorized; it is not edited post-hoc to
legalize a commit — see §7), executed, reviewed, and filed here.

---

## Objective

Resolve the design-authority question left BLOCKED by the prior session: can the
Boss be migrated from the in-force `SkillCharge`-driven model to an
actor-relative **Boss Power** resource with **Boss Cards**, and if so, under what
authorized architecture, scope and staged implementation plan? The operator
resolved every required decision in this session (§4, §8), so this task applies
the authorized documentation and ADR changes and records the remaining
sub-decisions as unresolved.

---

## 1. Preflight Baseline (verified facts at HEAD)

```text
Repository:                E:\dcacti (ya3k/dcacti)
Branch:                    master (ahead of origin/master by 1 pre-existing commit)
HEAD:                      4e28388e32bf0c0a1567df318faecc866642b803
HEAD subject:              docs: close the hp gem healing cap investigation as intended behavior
Working tree at pickup:    1 pre-existing untracked path — the BLOCKED TASK-303
                           record itself (tasks/blocked/); no other entry
Stashes:                   none
Highest existing task ID:  TASK-302  -> this task is TASK-303
Task registry at pickup:   backlog/ 0, active/ 0, blocked/ 1 (this record),
                           completed/ 305 records
Highest accepted ADR:      ADR-028   -> next sequential ADR = ADR-029
Date:                      2026-10-18
```

Lifecycle facts verified:

- `tasks/TASK_LIFECYCLE.md` §3: `BLOCKED` is resolvable by a human decision; the
  agent updates the record, moves it back to `active/` and sets `IN PROGRESS`.
  That resolution is exactly what happened here.
- `tasks/TASK_LIFECYCLE.md` §4: `IN REVIEW -> DONE` files the record in
  `tasks/completed/`.
- `tasks/TASK_LIFECYCLE.md` §6: the two-stage commit protocol applies to the
  `IN REVIEW -> DONE` transition only. It was executed (§13).
- `tasks/TASK_TYPES.md` §2: type is `DOCUMENTATION` (the primary output is
  `docs/` content). The implementation slices this record authorizes are
  decomposed as separate future tasks (§9), so the task does not blend types
  (`TASK_TYPES.md` §3).
- `docs/03-decisions/README.md` §4/§5 and
  `.ai/workflow/architecture/adr-change.md` §2/§3: ADR-029 is the next sequential
  number, is `Accepted` because this task establishes the decision in the
  owning documents, and supersedes the prior decision explicitly.

### 1.1 Evidence base: repository only, no feasibility report

The "Boss Power & Card Architecture Feasibility Audit Report" named by the
requesting brief **does not exist** in this repository, in git history, in a
stash, or in the reachable workspace (re-verified this session; the prior
session's search record is preserved in §1.2 of this document's history and was
re-run: no `*power*`, `*boss-card*`, `*bosscard*`, or `*audit*` artifact other
than the unrelated `tasks/artifacts/MVP-ACCEPTANCE-RELEASE-CLOSURE-AUDIT.md`).

Consequence, applied throughout: **every "audit-derived" value is an unverified
proposal, adopts nothing, and is not cited as evidence.** The operator
explicitly reinforced this in decision item 2 ("Do not assume `+10 Power per gem`
or any other audit-derived proposal is approved"), and `ADR-029` records the
exclusion explicitly so it cannot be mistaken for adopted design.

---

## 2. Existing Task / Duplicate Search

No duplicate task exists for this scope.

```text
Searched tasks/completed/*.md and tasks/{backlog,active,blocked}/*.md for:
  "Boss Card", "BossCard", "Boss Power economy", "BossState.Power",
  "fifth gem", "Power-drain gem", "actor-relative card", "generalized card"
Result:
  - No task proposes a Boss Power resource or a Boss Card category.
  - TASK-258 §2 D2 records the OPPOSITE decision (now superseded here).
  - TASK-091 excludes "Boss Skill magnitudes, Power-drain, or ATK-reduction"
    from its scope — an exclusion, not a duplicate.
Verdict: no duplicate; TASK-303 is the correct record and overwrites nothing.
```

---

## 3. Prior Authoritative Constraints That This Task Reverses (and how)

These were **in force** at pickup and are the reason the prior session could not
proceed on its own initiative. Each is now handled explicitly; none was silently
rewritten.

| # | Prior constraint (in force at pickup) | Disposition in this task |
|---|---|---|
| P-1 | `TASK-258` §2 **D2**: retain `BossState.SkillCharge`; do not introduce `BossState.Power` (transcribed into `ADR-026` item 4, `BOSS_RULES.md` §6.3, `COMBAT_RULES.md` §2, `GAME_RULES.md` §17 step 18a, `GAME_STATE.md` §2.4.3) | **Superseded in full** by the operator's explicit decision (item 1) and recorded by `ADR-029`. The preserved clause — the Boss is charged only by its own POWER matches, never the Player's — is retained. `TASK-258` itself is an immutable completed record and is **not** edited; `ADR-029` §7 names the superseded clause verbatim. |
| P-2 | `COMBAT_RULES.md` §2 / `BOSS_RULES.md` §6.3 / `ADR-026` Unresolved Balance Choices #1–#2: the `SkillCharge` **unit metric** and **clamp** are documented as unresolved, while the runtime had already adopted "+1 per match group clamped at `ChargeRequirement`" (`ResourceGenerator.ApplyBossSkillCharge`, asserted by `tests/backend/GameServer.Application.Tests/AtomicRoundOrchestrationTests.cs`) | Documented **and reconciled**: the divergence is reported (§11 item 1), and the questions are **closed by retirement**, not answered — `BOSS_RULES.md` §4.4/§6.3 now label the whole charge path transitional, gated by `ADR-029` §2.9's parity gate, and `ADR-026`'s unresolved note is annotated instead of rewritten. The runtime is **not** changed by this task. |
| P-3 | `MVP_SCOPE.md` §1 listed no Boss Power and no Boss Card; §3 listed "More Cards / Relics" as FUTURE; §4 classified unlisted content as FUTURE | **FUTURE → IN move executed** through the `GAME_RULES.md` §20 Rule Change process on the operator's approval: `MVP_SCOPE.md` §1 gains Boss Power and the minimum Boss Card capability, §3 records the fifth Gem as explicitly not authorized. |
| P-4 | `GAME_RULES.md` §17 step 18a–18d: the Boss acts on the board **and then** casts if eligible | **Rule change executed**: step 18 now selects exactly one action per response phase (`GAME_RULES.md` §17, `BOSS_RULES.md` §4.3), with determinism, eligibility, ordering, cooldown handling and tie-break defined. |
| P-5 | `CARD_RULES.md` §3 item 6 + `ADR-021`: one Card cast per committed Turn is a **Player** allowance | **Preserved and clarified, not widened**: `CARD_RULES.md` §3 item 9 and `GAME_STATE.md` §2.2.2 item 7 state the Boss cast neither reads nor spends it. |
| P-6 | `SIGNALR_PROTOCOL.md` §4.4 item 3: a closed exclusion list naming `SkillCharge`/`SkillCooldown`; any addition is a protocol change owned by its own task | **Protocol change executed by this task as that owner**: `bossState` gains the always-present `power` member (item 12). `SkillCharge`/`SkillCooldown` remain undelivered. |
| P-7 | `GAME_RULES.md` §16 / `SIGNALR_PROTOCOL.md` §3.2.2: closed event-name and discriminator sets with no Boss Card event | **Additive event authorized and defined**: `BossCardCast` added to both, plus `GAME_EVENTS.md` §2. The Player `CardCast` payload is unchanged. |

---

## 4. Decision Matrix — Resolved by the Operator

Legend — **Approval status**: `APPROVED` (operator decision applied here) /
`PARTIAL` (direction approved, sub-decisions still unresolved) / `OUT OF SCOPE`.

### D1 — Boss Power Economy — **APPROVED (direction) / PARTIAL (numbers)**

| # | Item | Outcome |
|---|---|---|
| D1.1 | Existing authoritative rule | `GAME_RULES.md` §12 (Power is the active Pet's resource, 0–100) became **actor-relative** in this task: Power is a per-actor resource. |
| D1.2 | Operator decision | **`BossState.Power` is introduced**, constrained to integer 0–100, in MVP scope; `TASK-258` §2 D2 reversed explicitly. |
| D1.3 | Applied to | `GAME_RULES.md` §12/§15, `BOSS_RULES.md` §1/§4.1, `COMBAT_RULES.md` §1.2/§2/§6, `GAME_STATE.md` §2.4/§2.4.1/§2.4.3/§5.1, `MVP_SCOPE.md` §1, `SIGNALR_PROTOCOL.md` §4.4, `REDIS_STATE.md` §2.1, `ADR-029` |
| D1.4 | Shared-rule requirement | The POWER-Gem output is generated by the one actor-relative rule `COMBAT_RULES.md` §2 owns; actor-specific effects on the other three Gem types are preserved. |
| D1.5 | **Not approved / still unresolved** | The Boss's grant basis (whether the shared per-Gem output is used unchanged or a Boss-specific rate is authored); any conversion arithmetic from the legacy charge metric; any HP / Pet-Power threshold. **`ADR-029` §3 U-1** blocks Slice 1. |
| D1.6 | `SkillCharge` disposition | **Retained as a documented transitional path**, removed only after `ADR-029` §2.9's four-part parity gate; never converted to/from `Power`. |

### D2 — Boss Card Content and Balance — **APPROVED (category + minimum capability) / PARTIAL (per-ability values)**

| # | Item | Outcome |
|---|---|---|
| D2.1 | Category | **`Boss Card` created** as a third Card category (`CARD_RULES.md` §1, new §1.1); it is a Card, not a Passive and not a Boss Skill. |
| D2.2 | MVP capability | **Minimum capability in MVP**: exactly one authored Boss Card per Boss, carrying that Boss's MVP ability. No loadout, collection, ownership row, acquisition or Player visibility. |
| D2.3 | Identity | The Boss Card's `CardDefinitionId` **reuses** the ability identity already recorded in `BOSS_RULES.md` §6.4's `SkillId` column — no second identifier. |
| D2.4 | Migration order | `earthquake`, then `flame-burst`, then `root`, then `drain-power`, then **`thunder-strike` last** (`ADR-029` §4 Slice 4; `BOSS_RULES.md` §4.4 item 3). |
| D2.5 | Presentation/encounter split | Enrage, HP-threshold healing and combo/threshold passives **remain passive/trigger-based and are never Boss Cards** (`GAME_RULES.md` §15 item 5, `BOSS_RULES.md` §4.2 item 7, §5). |
| D2.6 | **Not approved / still unresolved** | Every migrated ability's `PowerCost` (**U-2**), the outstanding magnitudes (**U-4**: `earthquake` Shield points, `flame-burst` buff, `thunder-strike` modifier, `flame-burst` × `boss-hoa-long-rage` compounding), and whether the carried-over cooldowns need re-tuning (**U-5**). |
| D2.7 | Persistence | **Out of scope**: no database column, table, migration or provisioning; `DATABASE.md` unchanged (§12 item 3). |

### D3 — Boss Cast Policy — **APPROVED (defined in authoritative documentation)**

| # | Item | Outcome |
|---|---|---|
| D3.1 | Action policy | **Exactly one action per response phase**: a board match action **or** a Boss Card cast. Never both; never "cast whenever eligible". |
| D3.2 | Determinism | Pure function of committed state + Boss definition; **zero unseeded PRNG**, no wall clock, no client input; fixed candidate enumeration order; same state → same action. |
| D3.3 | Eligibility | Hard gates: board action iff the legal-move enumeration finds a match-producing swap (deadlock recovery unchanged); cast iff the Boss Card exists, `BossState.Power ≥ EffectiveCardCost`, and cooldown `= 0`. |
| D3.4 | Action ordering | Selection at 18a; board action (if selected) at 18a; Passive at 18b; cast (if selected) at 18c; attack check at 18d. No step added, removed or reordered. |
| D3.5 | Cooldown handling | Hard eligibility gate; `−1` per `Turn` increment; set to the authored value in the successful cast's write-back; keeps running during board-action phases. |
| D3.6 | Tie-break | A tie resolves in favour of the **board match action** (a cast must strictly out-score it); board-action ties keep `ADR-026` item 8's lowest cell-index rule. |
| D3.7 | Independence | The Player's one-cast-per-Turn allowance is neither read nor spent by a Boss cast. |
| D3.8 | **Still unresolved** | The cast-priority score's authored form and values (**U-3**), and the pacing effect of a cast phase producing no Boss board resolution and no Boss attack (documented consequence; **U-3**). |

### D4 — Card Execution Architecture — **APPROVED (boundary recorded; implementation staged)**

| # | Item | Outcome |
|---|---|---|
| D4.1 | Decision | **Share the cast contract, validation and effect services; keep the actor states separate.** No `PetState`/`BossState` merge, no generic multi-actor action framework. |
| D4.2 | Ownership | Card module owns what a Card is and how a cast resolves (`CARD_RULES.md` §3, §3.6). Boss module owns what the Boss does (`BOSS_RULES.md` §4.2–§4.4). |
| D4.3 | Documented at | `CARD_RULES.md` §3 item 9 (actor-relative split), `ARCHITECTURE.md` §3 component table (`CardCastExecutor` = the shared contract; new `BossActionPolicy`; `BossAiEngine`), `ADR-029` §2.3. |
| D4.4 | Implementation | **Not in this task**; Slice 2. Verified at pickup: `CardCastExecutor.Execute(BattleState, CardDefinition)` is Player-anchored, rejects non-`Basic`/`PetSkill` categories, and `MaxCardCastsPerTurn = 1`; no `BossCardExecutor` exists. |

### D5 — Wire Contract and Client Visibility — **APPROVED (defined)**

| # | Item | Outcome |
|---|---|---|
| D5.1 | Cast event | **Dedicated `BossCardCast`** (`GAME_RULES.md` §16, `GAME_EVENTS.md` §2, `SIGNALR_PROTOCOL.md` §3.2.2/§3.2.30), carrying `cardId` + `sourceId`. |
| D5.2 | Player contract | **`CardCast` preserved exactly** (`type` + `cardId`; no actor/source/cost member) and never emitted for a Boss cast. |
| D5.3 | Boss Power visibility | **Delivered**: `bossState.power`, always present (integer 0–100), HUD-visible on both actors (`SIGNALR_PROTOCOL.md` §4.4 item 12). |
| D5.4 | Reconnect | Same `BattleStateUpdated` push and therefore the same §7 snapshot; **no** `BossPowerChanged`, no new method, no subscription, no event replay (`ADR-008`). |
| D5.5 | Non-conflation | `SkillCharge`/`SkillCooldown` remain undelivered; `power` is never `SkillCharge` on the wire. |

### D6 — Fifth Gem — **OUT OF SCOPE (explicitly kept outside MVP)**

| # | Item | Outcome |
|---|---|---|
| D6.1 | Disposition | **Not authorized and kept outside MVP.** No rule, design, content, protocol member or scaffold; recorded in `MVP_SCOPE.md` §3 and `ADR-029` §2.10 item 29, gated until Slices 1–4 are stable and then requiring its own task plus its own Rule Change (`GAME_RULES.md` §20). `MATCH3_RULES.md` is **untouched** by this task. |

---

## 5. D2 — Boss Ability Disposition Matrix

The five MVP abilities keep their authored effect, target and duration
(`BOSS_RULES.md` §6.3.1 is unchanged) and are migrated to Boss Cards in the
order fixed by `ADR-029` §4. **Every numeric balance value below remains
unresolved**; no value was promoted by this task.

| Ability (BossId / ability identity) | Disposition | Migration position | Effect (unchanged, in force) | Cost / cadence | Cooldown | Unresolved value |
|---|---|---|---|---|---|---|
| Earthquake (`boss-son-thach-ve` / `earthquake`) | Migrate to Boss Card | **Slice 4, first** | `BaseDamage = 0`; temporary Boss Shield on `BossState.ActiveStatusEffects` | Legacy: charge 5 matches → target: `PowerCost` (**U-2**) | 0 Turns (carried over, **U-5**) | Shield points (**U-4**) |
| Flame Burst (`boss-hoa-long` / `flame-burst`) | Migrate to Boss Card | Slice 4, second | `BaseDamage = 0`; temporary Boss ATK buff | `PowerCost` (**U-2**) | 2 Turns | Buff magnitude/duration and `boss-hoa-long-rage` compounding (**U-4**) |
| Root (`boss-moc-yeu` / `root`) | Migrate to Boss Card | Slice 4, third | `BaseDamage = 0`; `−30%` Pet ATK debuff for 2 Turns | `PowerCost` (**U-2**) | 2 Turns | none beyond **U-2**/`U-5` |
| Drain Power (`boss-thuy-ma` / `drain-power`) | Migrate to Boss Card | Slice 4, fourth | `BaseDamage = 0`; instant `−20` flat Pet Power | `PowerCost` (**U-2**) | 3 Turns | none beyond **U-2**/`U-5` |
| Thunder Strike (`boss-kim-loi-vuong` / `thunder-strike`) | Migrate to Boss Card | **Slice 4, LAST** | `BaseDamage = 0`; trigger-based modifier (`ExpiryCondition = "NextQualifyingAttack"`) buffing the next qualifying ATK match | `PowerCost` (**U-2**) | 0 Turns | Modifier magnitude (**U-4**), and the **exact qualifying attack / modifier-consumption timing** comparison that is the reason this ability goes last |

Passive/encounter distinction preserved: no row is reclassified as a passive and
no encounter mechanic is reclassified as a Card (`GAME_RULES.md` §15 item 5,
`BOSS_RULES.md` §4.2 item 7, §5). Boss Phases remain deferred
(`BOSS_RULES.md` §5 item 3).

---

## 6. Decision → Implementation Dependency Map (authorized target state)

```text
OPERATOR AUTHORIZATION (this task, recorded in ADR-029)
        |
        +--> MVP_SCOPE.md FUTURE->IN move .......................... DONE (this task)
        +--> ADR-029 + ADR-026 in-place amendment + D2 supersession . DONE (this task)
        +--> authoritative rule/protocol/state docs reconciled ...... DONE (this task)
        |
        v
Slice 1  Boss Power foundation (U-1 approved first)
        |--> Slice 2  Shared cast contracts + Player regression coverage
        |       |--> Slice 3  Deterministic one-action Boss AI + BossCardCast presentation
        |       |       |--> Slice 4  Ability migration (thunder-strike last)
        |       |       |       |--> Slice 5  Remove SkillCharge (parity gate)
        v       v       v       v
   No slice is authorized to start by this task. Each is a separate future task
   with its own declared file set, review gate, and (for Slices 1/3/4) the
   operator's approval of the unresolved value it depends on.
```

Blocking edges, stated explicitly:

```text
U-1 (grant basis)        BLOCKS Slice 1
Slice 1                  BLOCKS Slice 3 (the cast candidate needs Power)
Slice 2                  BLOCKS Slice 4 (an ability cannot migrate without the shared contract)
Slice 3                  BLOCKS Slice 4 (an ability cannot be selected by a policy that does not exist)
U-2/U-3/U-4/U-5          BLOCK the slice named against them in ADR-029 §3
Slice 4 + parity gate    BLOCKS Slice 5 (SkillCharge removal)
Fifth Gem                BLOCKED by all of the above and by its own future Rule Change
```

---

## 7. Documentation Change Plan — Applied (exact targets)

Every target below was authorized by the operator decision set and was changed.
Ownership was respected: each concept was edited only at its canonical owner and
referencing documents point at it (`documentation-change.md` §2).

| # | File | Owning sections amended | Nature |
|---|---|---|---|
| 1 | `docs/00-overview/MVP_SCOPE.md` | version header, §1 Cards, §1 Relics, §1 Bosses, §3 FUTURE | FUTURE→IN move for Boss Power + minimum Boss Card capability; Relics recorded Player-only; fifth Gem recorded as not authorized; Rule Change authorization recorded |
| 2 | `docs/00-overview/GDD.md` | version header, §8, §9, §12 | Overview-level presentation of the Boss Card category and per-actor Power; no value authored. Also removed the stale "Special Matches" reference from the Power sentence being rewritten (disclosed in §11) |
| 3 | `docs/01-game-design/GAME_RULES.md` | version header, §11 item 5, §12, §15 (renumbered to 8 items), §16 (+ note), §17 step 18a–18d, §17 Card-cast-position paragraph | Actor-relative Power; Boss Power generation; one-action policy; `BossCardCast` name; step 18 restructured with no step added/removed/reordered |
| 4 | `docs/01-game-design/CARD_RULES.md` | version header, §1 (categories), new §1.1, new §3 item 9, §6 | Boss Card category + minimum capability; actor-relative cast contract; Player-only allowance/Relics; Boss event |
| 5 | `docs/01-game-design/BOSS_RULES.md` | version header, §1, §2 (stale `§15.x` citations fixed), §3.3, §4 (retitled + new §4.1–§4.4), §5 items 4–5, §6.2.6, §6.3 (+ footnote), §6.3.1 note, §7, §8 | Canonical owner of Boss Power, Boss Card capability, the action policy (determinism/eligibility/ordering/cooldown/tie-break), the staged migration + removal gate, visibility, events |
| 6 | `docs/01-game-design/COMBAT_RULES.md` | version header, §1.2, §2 table row + actor-relative bullet, §2 item 6 modifier list, §6 | POWER Gem → actor-relative Power; Boss Power stat; legacy charge marked transitional; no numeric change |
| 7 | `docs/02-technical/GAME_STATE.md` | version header, §2.2.2 item 7, §2.4 tree, §2.4.1 (staged + 4 client-visible members), §2.4.3 (rewritten), §5.1 diagram + new item 9 | `BossState.Power` member, shape/write points/clamp, transitional `SkillCharge`, cooldown counter, one write-back |
| 8 | `docs/02-technical/GAME_EVENTS.md` | version header, §1 Boss Response block, §2 `CardCast`/`PetSkillCast` (Player-only item 6), §2 `BossSkillCast` (transitional), new §2 `BossCardCast` | Event meaning/payload for the dedicated Boss action event; Player events confirmed unchanged |
| 9 | `docs/02-technical/SIGNALR_PROTOCOL.md` | version header, §3.2.2 table + list, new §3.2.30, §4.4 (projection, items 2–5, 8, old 10→11, new 12), §4 item 18, §7 snapshot list, §8 item 9 | `BossCardCast` wire schema; `bossState.power` always-present; Player `CardCast` untouched; reconnect unchanged |
| 10 | `docs/02-technical/REDIS_STATE.md` | version header, new §2.1 | `BossState.Power` serialization, tolerant-reader (absent → 0 = documented initial value), no migration/rewrite/drain required by the addition, no conversion from `SkillCharge` |
| 11 | `docs/02-technical/ARCHITECTURE.md` | version header, §3 component table, §2.2.2 Boss-response parenthetical | Shared cast contract boundary; new `BossActionPolicy`; `BossAiEngine`; no layer moved |
| 12 | `docs/03-decisions/ADR/ADR-026-…md` | Status line, Unresolved Balance Choices note, new `TASK-303`/`ADR-029` amendment section, Related Documents | Partial supersession recorded in place; historical content preserved; no silent rewrite |
| 13 | `docs/03-decisions/ADR/ADR-029-boss-as-player-opponent-boss-power-and-action-policy.md` | new ADR | The decision, the verified findings, six unresolved balance choices, alternatives, consequences, supersession record, staged plan |
| 14 | `docs/03-decisions/README.md` | version header, §7 index (ADR-026 row + new ADR-029 row), new "Partial supersession (ADR-029)" block | Registry and supersession index |

**`Declared Files` manifest integrity (P-5).** The manifest was `None` in the
BLOCKED record because no change was authorized. It was populated **as part of
authorizing the work** — before any file was edited — with exactly the 14 paths
above, and the Phase A commit staged exactly those paths (§13). It was not edited
after the fact to legalize a commit whose staged set differed.

**No-duplication check (documentation-change.md §2).** Cross-checked: no document
restates another's rule. The Boss Power *rates* stay in `COMBAT_RULES.md` §2; the
state shape stays in `GAME_STATE.md`; the Boss's gameplay rules stay in
`BOSS_RULES.md`; the cast contract stays in `CARD_RULES.md` §3; the wire stays in
`SIGNALR_PROTOCOL.md`; the event semantics stay in `GAME_EVENTS.md`; the ADR
explains *why* and copies no rule text as a second owner.

---

## 8. Operator Decisions — Recorded, and What Remains Unresolved

### 8.0 Operator input recorded in this session (2026-10-18)

```text
OD-0   RESOLVED (carried forward) — proceed on current-repository evidence. The
       absent feasibility audit is NOT supplied and is NOT a blocker. Every
       audit-derived value remains unverified and adopted nowhere.

OD-1.1 RESOLVED — introduce BossState.Power, constrained to integer 0–100, into
       MVP scope; reverse TASK-258 §2 D2 explicitly.
OD-1.2 RESOLVED (direction) — use the same resource-generation and
       cost-validation rules for Player and Boss wherever the rules define them
       as shared; preserve actor-specific effects.
OD-1.3 RESOLVED — the audit-derived "+10 Power per gem" conversion and any
       HP / Pet-Power threshold are NOT approved and must not be assumed.
OD-1.4 RESOLVED — preserve separate PetState and BossState; share appropriate
       cast contracts, validation and effect services; do not merge the states.
OD-1.5 RESOLVED — Relics remain Player-only.
OD-3.1 RESOLVED — each Boss response phase follows an explicit AI action policy:
       a board match action OR a Card cast, never "cast every eligible ability";
       determinism, eligibility, action ordering, cooldown handling and
       tie-break defined in authoritative documentation before runtime work.
OD-2.1 RESOLVED — encounter mechanics (Enrage, HP-threshold healing,
       combo-triggered passives) stay passive/trigger-based; not player-like
       Cards.
OD-5.1 RESOLVED — introduce a dedicated Boss action event; preserve the existing
       Player CardCast wire contract.
OD-6.1 RESOLVED — move Boss Power and the minimum required Boss Card capability
       into MVP; keep the fifth Gem outside MVP until Slices 1–4 are stable.

NOT DECIDED BY THE OPERATOR (recorded as unresolved; must NOT be inferred)
  U-1  the Boss's Power grant basis (shared per-Gem output unchanged, or an
       authored Boss-specific rate)
  U-2  each Boss Card's PowerCost / any legacy-threshold re-tuning
  U-3  the Boss Card cast-priority score's authored form and values (and the
       resulting cast/board cadence)
  U-4  the outstanding ability magnitudes (Earthquake Shield points; Flame Burst
       buff; Thunder Strike modifier; Flame Burst × Rage compounding)
  U-5  whether the carried-over cooldowns are re-tuned
  U-6  the retirement timing of SkillCharge's own metric/clamp questions (gated
       by the parity gate, not answered)
```

These are `ADR-029` §3's `U-1`…`U-6`. Silence is not approval (`AGENTS.md` §7);
no value was transcribed into any authoritative document.

---

## 9. Acceptance Criteria

- [x] Repository state and lifecycle requirements verified at HEAD `4e28388`.
- [x] The BLOCKED record's path/status was re-read and the blocking condition
      resolved by the operator's explicit decisions before work resumed.
- [x] Every required design decision (D1–D6) is resolved (APPROVED) or explicitly
      recorded as PARTIAL/OUT OF SCOPE, with the operator authorization noted.
- [x] A new ADR (`ADR-029`) is the next valid number after the current accepted
      registry (`ADR-028`), and it **explicitly supersedes `TASK-258` §2 D2** and
      **reconciles `ADR-026`** without silently rewriting historical decisions.
- [x] `MVP_SCOPE.md` updated through its documented Rule Change process
      (`GAME_RULES.md` §20), citing the recorded approval.
- [x] Every affected authoritative owner document is updated: resource rules
      (`GAME_RULES.md` §12, `COMBAT_RULES.md` §2), Boss rules (`BOSS_RULES.md`),
      Card rules (`CARD_RULES.md`), combat rules (`COMBAT_RULES.md`), state model
      (`GAME_STATE.md`), SignalR protocol, event catalog (`GAME_EVENTS.md`, plus
      `GAME_RULES.md` §16) and Redis-state documentation (`REDIS_STATE.md`), with
      `ARCHITECTURE.md` and `GDD.md` as dependents.
- [x] Boss Power projection and reconnect contract specified (`bossState.power`,
      always present, same push/snapshot, no new delivery path).
- [x] The dedicated Boss action event specified (`BossCardCast`) and the
      compatibility guarantee for Player `CardCast` stated explicitly.
- [x] Unresolved numerical balance values and decisions recorded as unresolved
      (`ADR-029` §3, §8.0 above) — none promoted to a rule, no audit proposal
      adopted.
- [x] Decision indexes and cross-references updated (`docs/03-decisions/README.md`
      version + §7 rows + partial-supersession block; ADR-026 status/related
      docs; stale `GAME_RULES.md` §15.x citations in `BOSS_RULES.md` corrected
      because this task renumbered that list).
- [x] TASK-303's decision matrix updated with the approved direction and the
      remaining sub-decisions (§4, §5, §8).
- [x] **No runtime code, database schema, test code or protocol implementation
      changed** — verified by the final diff (§11, §12).
- [x] No claim that Boss Power generation is already equivalent to Pet Power;
      the opposite is documented with source-level evidence (`ADR-029` §1.2).
- [x] `ResourceGenerator` inspected before scope was estimated, and the remaining
      questions about it documented (`ADR-029` §1.2 item 1, §4 Slice 1).
- [x] `PetState`/`BossState` separation preserved; Player-only Relics preserved;
      fifth Gem kept outside MVP.
- [x] `git diff --check` clean; documentation consistency checks run (§11).
- [x] Review gate (`quality/review.md` §1) passed (§11.3).
- [x] Two-stage commit protocol executed (Phase A = 14 documentation files;
      Phase B = this record) — §13.

---

## 10. Verification Results

```text
Command / check                                        Result
----------------------------------------------------  ----------------------------
git rev-parse --abbrev-ref HEAD                        master
git rev-parse HEAD (pre-edit)                          4e28388e32bf0c0a1567df318faecc866642b803
git status --porcelain (at pickup)                     1 untracked path (this record)
git stash list                                         empty
Highest accepted ADR pre-change                        ADR-028 -> ADR-029 is next
git diff --check                                       exit 0 (no whitespace/patch errors)
Runtime/source files changed (.cs, .ts, .json, .sql)   0
Database/schema/test files changed                     0
```

Documentation checks performed (no docs-lint project exists in this repository —
`scripts/` holds only `collect-project-state.sh` and `standalone-web-smoke.mjs`,
and `tests/backend/` holds only the four game test projects, so the mechanical
checks available are the ones below):

1. **`git diff --check`** — exit 0.
2. **Changed-file scope scan** — every changed path is under `docs/`; nothing in
   `src/`, `tests/`, `scripts/`, `docker-compose.yml`, or the task templates was
   touched.
3. **Section-reference validation** — every newly written cross-reference was
   checked against the target document's actual headings/items:
   `BOSS_RULES.md` §4.1–§4.4/§4.3 items/§6.3/§7/§8;
   `CARD_RULES.md` §1.1/§3 item 9/§3.6/§6;
   `GAME_STATE.md` §2.2.2 item 7/§2.4.1/§2.4.3/§5.1 item 9;
   `SIGNALR_PROTOCOL.md` §3.2.30/§4.4 items 11–12;
   `GAME_RULES.md` §11/§12/§15 items 1–8/§16/§17 step 18/§20;
   `MVP_SCOPE.md` §1/§3/§4.
4. **ADR numbering and index** — `ADR-029` is unused before this task; the §7
   table, the ADR-026 status cell and the new partial-supersession block all name
   it; ADR-026 keeps its historical content with an appended amendment section
   (`adr-change.md` §3 partially-supersession treatment, matching the
   `ADR-016`/`ADR-011`/`ADR-012` precedent).
5. **Task-link validation** — the record cites `TASK-258` (completed) and cites
   no non-existent task; `ADR-029`'s Related Documents resolve.
6. **Stale-reference sweep** — every remaining `SkillCharge` mention in the
   amended documents now reads as transitional/retirement context; every remaining
   `GAME_RULES.md` §15 citation was renumbered to the item form; no `three-member`
   `bossState` phrase remains.
7. **No-duplication sweep** — see §7's no-duplication paragraph.
8. **Encoding/markdown sanity** — `§`, em-dash and box-drawing characters render
   correctly in all amended files (an early PowerShell-based rewrite of
   `SIGNALR_PROTOCOL.md` corrupted its encoding; the file was discarded and
   `git checkout`ed, and every edit was then re-applied through the
   encoding-safe editor, which is why the committed diff for that file is a
   normal 145/28-line change).

Working-tree effect of this task before the Phase A commit:

```text
M docs/00-overview/GDD.md
M docs/00-overview/MVP_SCOPE.md
M docs/01-game-design/BOSS_RULES.md
M docs/01-game-design/CARD_RULES.md
M docs/01-game-design/COMBAT_RULES.md
M docs/01-game-design/GAME_RULES.md
M docs/02-technical/ARCHITECTURE.md
M docs/02-technical/GAME_EVENTS.md
M docs/02-technical/GAME_STATE.md
M docs/02-technical/REDIS_STATE.md
M docs/02-technical/SIGNALR_PROTOCOL.md
M docs/03-decisions/ADR/ADR-026-shared-board-gameplay-and-actor-relative-match-resolution.md
M docs/03-decisions/README.md
A docs/03-decisions/ADR/ADR-029-boss-as-player-opponent-boss-power-and-action-policy.md
```

### 10.3 Review gate (`quality/review.md` §1)

```text
Correctness        PASS — the amended rules match the operator decision set; the
                   target state and the transitional path are labeled, and no
                   numeric value is invented.
Architecture       PASS — no layer moved, no dependency direction changed, no
                   module merged (ARCHITECTURE.md §3, ADR-029 §2.3).
Scope              PASS — only the 14 declared docs paths changed; no runtime,
                   schema, test or unrelated cleanup (§11 item 3).
Tests              N/A — no code changed; no test was expected or run, and no
                   existing test was modified.
Documentation      PASS — owner-first edits, no duplication, indexes updated,
                   historical ADR content preserved.
Security           PASS — no auth, secret or exposure surface touched; the only
                   new client-visible value is the Boss's own Power, authorized
                   for HUD display.
Performance        N/A — no hot-path code or query touched.
Maintainability    PASS — no abstraction invented; the "shared contract, separate
                   states" boundary is the smallest correct one; a generic
                   multi-actor framework was explicitly rejected (ADR-029 §5).
Determinism        PASS — the one-action policy is specified as pure, seeded-free,
                   enumeration-order-stable, and server-authoritative; the client
                   is explicitly forbidden from deriving Boss Power.
```

---

## 11. Remaining Issues (reported, not fixed — `AGENTS.md` §16)

1. **Documentation/implementation divergence is open until Slices 1–4 land.** The
   documents now describe the authorized target state while the runtime still
   executes the legacy path (`SkillCharge`, `+1 per match group` clamped at
   `ChargeRequirement`, board-action-then-skill order, no `BossState.Power`, no
   `BossCardCast`, no `bossState.power`). This is deliberate
   documentation-first ordering, and the divergence is explicitly labeled in
   `BOSS_RULES.md` §4.4/§6.3, `GAME_STATE.md` §2.4.1/§2.4.3, `COMBAT_RULES.md` §2
   and `ADR-029` §2.9. **Suggested follow-up:** the four slice tasks, in order.
2. **The legacy charge metric remains adopted in runtime but undocumented as
   approved.** `ResourceGenerator.ApplyBossSkillCharge` (+1 per match group,
   clamped at `ChargeRequirement`) and the tests that assert it still exist and
   are unchanged by this task. **Suggested follow-up:** it is retired by Slice 5,
   not patched here; until then the divergence stands as reported.
3. **Boss Card persistence is deliberately undefined.** `DATABASE.md` §1 still
   describes `BossSkillDefinition`/`SkillChargeRequirement` as the Boss ability's
   persistence carrier (`DATABASE.md` around line 999), and no Boss Card
   provisioning exists. **Suggested follow-up:** a separate persistence/
   provisioning task after Slice 4 decides the final carrier; `ADR-029` §2.5
   item 15 authorizes no schema change.
4. **`ADR-024`'s `SecondaryEffect` carrier note will need a reconciliation at
   Slice 4.** The database-driven boss balance decision preserves
   `BossSkillDefinition.SecondaryEffect`; once an ability is a Boss Card, its
   effect carrier changes while the Domain Overlay model does not. `ADR-029` §7
   records this as unaffected-for-now with the reconciliation point named.
5. **A stale GDD reference was removed in passing.** The Power sentence in
   `GDD.md` §9 named "Special Matches", a mechanic retired by OD-1/`ADR-028`; the
   sentence was rewritten for per-actor Power and the stale reference was dropped
   rather than preserved. Disclosed here because it is a dependent correction, not
   a new decision.
6. **Stale cross-references in `TASK-258` remain.** The completed record cites
   `GAME_STATE.md:2445` for the charge rule and is immutable
   (`TASK_LIFECYCLE.md` §3); it is not corrected here.
7. **The prior session's `§1.1`/`§12` history is preserved in summary only.** The
   BLOCKED record's full narrative history was superseded by this record's
   resolution sections; the findings it recorded (missing audit report; the
   documentation/implementation divergence; the stale TASK-258 reference) are
   carried forward above and in §1.1.

---

## 12. Constraints Honored

- No runtime code, database schema, test code or protocol implementation changed
  — the diff is `docs/` only.
- No claim that Boss Power generation is already equivalent to Pet Power; the
  differences are documented with source-level evidence.
- `ResourceGenerator` was inspected and the design's dependence on further
  changes is documented rather than assumed (its Boss branch charges
  `SkillCharge`, not `Power`; Slice 1 must add a path beside it).
- `PetState`/`BossState` separation preserved; Relics Player-only; fifth Gem
  outside MVP (`MATCH3_RULES.md` untouched).
- Lifecycle and review gates followed, including the two-stage commit (§13).
- No review or commit gate bypassed.

---

## 13. Completion Evidence

### Commit

- **Phase A — Implementation slice commit**

```text
Subject:   docs: authorize boss power, boss cards, and the boss action policy
Owns:      TASK-303
Files:     the 14 declared documentation paths recorded in Metadata
Shared with: none
Unowned/pre-existing: none
Phase A SHA: dbafdf3015866873dd6ac8c5b70560aa0c8f0da2
Staged set: M GDD.md, M MVP_SCOPE.md, M BOSS_RULES.md, M CARD_RULES.md,
            M COMBAT_RULES.md, M GAME_RULES.md, M ARCHITECTURE.md,
            M GAME_EVENTS.md, M GAME_STATE.md, M REDIS_STATE.md,
            M SIGNALR_PROTOCOL.md, M ADR-026-…, A ADR-029-…, M
            docs/03-decisions/README.md — 14/14, no extraneous path, record
            excluded from Phase A.
```

**Known cosmetic defect in the Phase A commit message (disclosed, not repaired).**
The commit subject stored in the object carries a leading UTF-8 byte-order mark
(`U+FEFF`) before the word `docs` — a tooling artifact of writing the message file
with a BOM-emitting encoder. It does not affect the subject's content, carries no
task ID, and changes no file. It was **not** repaired because
`tasks/TASK_LIFECYCLE.md` §6.2 P-6 explicitly prohibits amend, rebase, force-push,
history rewrite and duplicate implementation commits; repairing it would require
an operator-authorized one-time exception. Recorded here and in the final report
so the defect is on the record rather than silently rewritten.

- **Phase B — Completion record filing commit** (this file, dedicated
  bookkeeping commit; subject carries no task ID)

```text
Files:     tasks/completed/TASK-303-boss-power-and-boss-card-design-authority-reconciliation.md
Shared with: none
Unowned/pre-existing: none
```

- **Prior blocked record:** `tasks/blocked/TASK-303-…md` was untracked and
  uncommitted; its content is fully carried into this record, and the file was
  removed from the working tree as part of the same resolution. Nothing was
  committed to git for it, so no git deletion exists and no history was rewritten
  (`TASK_LIFECYCLE.md` §6.2 P-6).

### Validation evidence

```text
git diff --check                exit 0 (Phase A staged set)
Staged set == Declared Files    yes (14/14, no extraneous path, record excluded)
Runtime files in staged set     0
Reviews                        quality/review.md §1 PASS (§10.3)
```
