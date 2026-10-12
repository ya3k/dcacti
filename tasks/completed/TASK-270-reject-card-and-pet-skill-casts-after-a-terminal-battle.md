# TASK-270 — Reject Card and Pet Skill Casts Against a Terminal Battle

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  Authorization: the Product Owner decision recorded by the requester —
  "Option A: reject terminal Card Cast and Pet Skill Cast using the established
  STALE_ACTION rejection contract" — resolving the question
  tasks/completed/TASK-268-…md §Remaining Issues item 1 left open, and which
  MATCH3_RULES.md §1.6.4 item 4 explicitly declines to own.
  Committed jointly with TASK-269 (shared implementation file) per
  tasks/TASK_LIFECYCLE.md §6.1 Phase A item 5.
-->

---

## Metadata

```text
Task ID:           TASK-270
Type:              GAMEPLAY-CHANGE
Status:            DONE
Risk:              HIGH
Priority:          HIGH
Primary Agent:     gameplay
Supporting Agents: backend, testing, review
Workflow:          development/gameplay-change.md
Skills:            gameplay/gameplay-behavior-derivation,
                   gameplay/authority-determinism-audit,
                   testing/test-scenario-generation,
                   quality/documentation-consistency,
                   quality/implementation-review,
                   quality/scope-validation
Dependencies:      TASK-269 (joint group commit peer), TASK-268 (DONE), TASK-267 (DONE)
Declared Files:    docs/01-game-design/CARD_RULES.md,
                   docs/01-game-design/MATCH3_RULES.md,
                   src/backend/GameServer.Domain/Cards/CardCastRejectionReason.cs,
                   src/backend/GameServer.Application/Battle/BattleStateService.cs,
                   tests/backend/GameServer.Application.Tests/BattleStateServiceTerminalActionGuardTests.cs
```

---

## Objective

Record, in the owning rule document, and then implement the Product Owner's approved
rule: a **Card cast or Pet Skill cast that reaches the server against a battle whose
authoritative state is already terminal is rejected under the established
`STALE_ACTION` rejection contract** — before any cast validation runs, so it consumes no
Power, changes no board/HP/buff/debuff/status/sequence/RNG value, emits no Battle Event,
resolves no round, produces no Boss response, and writes nothing. No new rejection code,
wire discriminator, hub method, event, or response shape is introduced, and
`MATCH3_RULES.md` §2.1.4 item 7's Swap guard is left untouched.

---

## Authoritative References

- `docs/01-game-design/GAME_RULES.md` §1.4 — the terminal condition of a battle (the
  Boss's HP or the active Pet's HP has reached 0), and §11.1/§11.4 — a Card cast is an
  explicit player action, distinct from a Swap.
- `docs/01-game-design/MATCH3_RULES.md` §2.1.4 items 2 and 7 — the established
  action-boundary terminal guard for a Swap, its position after idempotency recognition,
  and the state-precondition rejection code it reuses; §1.6.4 item 4 — the exemption's
  explicit refusal to decide the cast question.
- `docs/01-game-design/CARD_RULES.md` §3 (items 2, 3, 5, 6) and §4 — cast validation,
  rejection semantics, and the Pet Skill Card.
- `docs/02-technical/SIGNALR_PROTOCOL.md` §2 item 4 (validation is performed server-side
  per the action's owning domain document: `CARD_RULES.md` §3 for `CardCast` and
  `PetSkillCast`) and §5 items 2–4 (a rejection changes nothing and is delivered to the
  caller only).
- `docs/02-technical/REDIS_STATE.md` §3, §4 item 7 — the surviving terminal record and
  "a rejected action writes nothing".
- `docs/01-game-design/CARD_RULES.md` §3.6 — `EffectiveCardCost`, the cost a cast would
  otherwise spend.
- `docs/00-overview/MVP_SCOPE.md` §1

---

## Scope

### In Scope
- The rule in `CARD_RULES.md` §3: the terminal condition, the `STALE_ACTION` rejection,
  the absence of state mutation / Power consumption / additional gameplay events, the
  relationship to `MATCH3_RULES.md` §2.1.4 item 7's Swap guard, and compatibility with the
  existing acknowledgement contract.
- One cross-reference correction in `MATCH3_RULES.md` §1.6.4 item 4, which currently
  states that the cast question is one "this section does not own" without naming an
  owner; the question is now decided, so the note names it.
- The Domain carrier of the already-documented `STALE_ACTION` code for a cast rejection
  (`CardCastRejectionReason`), and its existing wire mapping (`CardCastRejectionCodes`).
- The terminal guard in `ExecuteCardCastAsync` and `ExecutePetSkillCastAsync`, on the
  record the action first read and on the fresh record of the compare-and-set retry path.
- Focused regression tests.

### Out of Scope
- Any new rejection code (e.g. `BATTLE_ENDED`), enum-Discriminator, hub method, SignalR
  message, Battle Event, Redis key, or response shape. `CardCastResponse` /
  `PetSkillCastResponse` are `(bool Accepted, string? Reason)` and are unchanged.
- `SIGNALR_PROTOCOL.md`: §5 item 3 deliberately keeps no parallel code list and delegates
  each action's rejection codes to its domain document, which is `CARD_RULES.md` §3 for
  both cast methods, so no protocol text is stale.
- The frontend: `formatCardCastRejection` already has a safe fallback for an unrecognized
  code, so the new code is presented without any client change.
- The Swap guard, the deadlock recovery, the cast cost/effect magnitudes, the
  once-per-cast-Turn allowance, and the compare-and-set retry bound.
- The deferred test-quality findings F-03 … F-07 (`AGENTS.md` §16 — reported, not fixed).

---

## Current State

`ExecuteCardCastAsync` and `ExecutePetSkillCastAsync` read the battle record, resolve the
Card definition, and run `CardCastExecutor.Execute` in a bounded compare-and-set retry
loop. Neither has a terminal-state guard. A battle whose record survives its own end (the
documented fail-closed case of `DATABASE.md` §1 sourcing item 3) can therefore still
accept a cast: the cast spends Power, advances `Sequence`, changes HP/buffs/status/RNG,
emits events, and — through the Pet Skill damage path — can even append a second
`BattleWon`. A concurrent winner that ends the battle has the same consequence on the
loop's fresh-state re-read. `MATCH3_RULES.md` §2.1.4 item 7 fixes the equivalent position
for the Swap only.

---

## Declared File Set (P-2 / P-5)

Exact declared implementation files for TASK-270:
```text
docs/01-game-design/CARD_RULES.md
docs/01-game-design/MATCH3_RULES.md
src/backend/GameServer.Domain/Cards/CardCastRejectionReason.cs
src/backend/GameServer.Application/Battle/BattleStateService.cs
tests/backend/GameServer.Application.Tests/BattleStateServiceTerminalActionGuardTests.cs
```

*(Note: the task manifest file itself is excluded from the implementation declared file
set. `src/backend/GameServer.Application/Battle/BattleStateService.cs` is **shared** with
TASK-269, which is active at the same time; per `TASK_LIFECYCLE.md` §6.1 Phase A item 5
the two tasks are committed together in one joint group commit and neither is committed
separately.)*

---

## Acceptance Criteria

- [x] `CARD_RULES.md` §3 owns the rule: the terminal condition, the `STALE_ACTION`
      rejection, no state mutation / no Power consumption / no additional Battle Event, the
      relationship to the terminal Swap guard, and compatibility with the existing
      acknowledgement contract.
- [x] `MATCH3_RULES.md` §1.6.4 item 4 names the owner of the cast question instead of
      leaving it unattributed, and changes no Swap rule.
- [x] A Card cast against an already-terminal **Boss** state and against an already-terminal
      **Player** state are both rejected with `STALE_ACTION`.
- [x] A Pet Skill cast against an already-terminal **Boss** state and against an
      already-terminal **Player** state are both rejected with `STALE_ACTION`.
- [x] A rejected cast consumes no Power, advances no `Sequence`, moves no `RngState`,
      changes no board/HP/buff/debuff/status value, and emits no Battle Event.
- [x] A rejected cast resolves no round and produces no Boss response.
- [x] The guard also holds on the fresh record of the compare-and-set retry path, so a
      concurrent winner that ended the battle cannot be acted on.
- [x] Missing-battle behaviour is unchanged: the service still returns `null`, reported as
      `BATTLE_NOT_FOUND`.
- [x] Idempotency/replay semantics are unchanged: the guard reuses the established reason
      representation and adds no second reason, and a repeated cast on a live battle keeps
      its existing `CARD_CAST_ALREADY_USED_THIS_TURN` reason.
- [x] A normal Card cast and a normal Pet Skill cast against a non-terminal battle still
      commit exactly as before.
- [x] No new rejection code, wire discriminator, hub method, event, or response shape is
      introduced; the only new Domain member maps to the already-documented `STALE_ACTION`
      string.
- [x] All relevant tests pass at the required validation depth (`core/validation.md` §2)
- [x] Quality review checklist passes (`quality/review.md` §1)
- [x] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001)

---

## Affected Files & Areas

```text
[x] src/backend/ (Domain / Application)
[ ] src/frontend/client/ — not touched (the reason string already has a safe fallback)
[x] tests/ (Application)
[x] docs/ (domain rule and core-rules cross-reference)
```

---

## Implementation Notes

- Rule first (`development/gameplay-change.md` §3): the `CARD_RULES.md` text is authored
  before the code that implements it.
- The terminal predicate is the service's existing `IsTerminalBattle` — the same
  authoritative reading (`BossState.HP <= 0 || PetState.HP <= 0`) the Swap guard uses
  (`GAME_RULES.md` §1.4). No second terminal definition is introduced.
- Guard position: **after** the record read (so absence is still the documented `null` /
  `BATTLE_NOT_FOUND`) and **before** the Card definition lookup and any validation or
  resolution, which is the action boundary `MATCH3_RULES.md` §2.1.4 item 7 fixes for the
  Swap, and which makes the guard pre-empt `CARD_RULES.md` §3 item 2's checks.
- Card casts carry no action-identity contract (`CardCast`/`PetSkillCast` take no
  `clientSequence`; `SIGNALR_PROTOCOL.md` §2 item 1 makes it an unconsulted opaque
  correlation value), so unlike the Swap path there is no replay recognition to place the
  guard after; a repeated cast on a battle that has ended is a rejection either way, and
  no replay semantics are redefined.
- The rejection reason is `CardCastRejectionReason.StaleAction`, whose
  `CardCastRejectionCodes.ToContractCode` mapping is the already-documented `STALE_ACTION`
  string (`SIGNALR_PROTOCOL.md` §5 item 3 delegates the code to the owning domain doc), so
  the Domain member is the carrier of an existing code rather than a new one.
- No seam is added: the retry-path guard is the same three lines on the record the loop
  already re-read.

---

## Testing Requirements

### Required Verification
```text
[x] Unit tests         — not the selected depth for the guard (a boundary ordering)
[x] Integration tests  — Application: both cast methods over the store double, both
                         terminal sides, the retry path, and live-battle compatibility
[x] Gameplay scenarios — Given/When/Then state-transition level, including structural
                         state comparison rather than reference equality
```

### Key Edge Cases
- `MATCH3_RULES.md` §2.1.4 item 7 — the equivalent Swap guard, whose contract this rule
  mirrors: rejection before any resolution, no RNG, no event, no write.
- `REDIS_STATE.md` §4 item 7 — a rejection writes nothing at all.
- `CARD_RULES.md` §3 item 6 — a live battle's second cast keeps
  `CARD_CAST_ALREADY_USED_THIS_TURN`; the new guard must not change it.
- `SIGNALR_PROTOCOL.md` §5 item 2 — `accepted: false` means nothing happened.

---

## Stop Conditions

- If required behavior cannot be fully derived from Authoritative References: STOP per `AGENTS.md` §7
- If the guard cannot be implemented without a new wire code, discriminator, hub method, or
  response shape: STOP and report per `AGENTS.md` §4 instead of changing the contract
- If task requires client-authoritative game logic calculation: STOP per `AGENTS.md` §10

---

## Completion Evidence

### Commit
- Implementation slice (Phase A): `80b9defb68eb88b51bd31b0a31d941e1cc46d88e`
- Subject: `fix(battle): persist the terminal result on the CAS contention fallback`
- Owns: `TASK-269`, `TASK-270` (joint group commit — `BattleStateService.cs` is shared)
- Files: `docs/01-game-design/CARD_RULES.md`, `docs/01-game-design/MATCH3_RULES.md`,
  `src/backend/GameServer.Domain/Cards/CardCastRejectionReason.cs`,
  `src/backend/GameServer.Application/Battle/BattleStateService.cs`,
  `tests/backend/GameServer.Application.Tests/BattleStateServiceTerminalActionGuardTests.cs`
- Shared with: `TASK-269` (`src/backend/GameServer.Application/Battle/BattleStateService.cs`)
- Unowned / pre-existing: `none` — `git status --porcelain --untracked-files=all` was empty at
  preflight; the 13 paths TASK-267/TASK-268 disclose as unowned had already been committed by
  `af64ff6` before this task began, so no unowned working-tree change existed to disclose or
  preserve, and none was staged.

### Changed Files
- `docs/01-game-design/CARD_RULES.md` — new §3 item 8, authored **before** the code
  (`development/gameplay-change.md` §3): the terminal condition, the `STALE_ACTION` rejection at
  the action boundary and its precedence over §3 item 2's checks, the absence of state mutation /
  Power consumption / additional Battle Events / Boss response / persisted result, the
  relationship to `MATCH3_RULES.md` §2.1.4 item 7's Swap guard, and compatibility with the
  existing acknowledgement contract. Version 1.9 → 1.10 with the prior chain retained.
- `docs/01-game-design/MATCH3_RULES.md` — §1.6.4 item 4 no longer leaves the cast question
  unattributed: it now names `CARD_RULES.md` §3 item 8 as its owner. No Match-3 rule, guard,
  step, code, event, or RNG behaviour changes. Version 1.8 → 1.9.
- `src/backend/GameServer.Domain/Cards/CardCastRejectionReason.cs` — `StaleAction = 5` added as
  the cast-side carrier of the **already-documented** `STALE_ACTION` string, and
  `CardCastRejectionCodes.ToContractCode` maps it to that existing code. This is the single
  deviation from the approved instruction's literal wording: the instruction said "do not add a
  new enum member", but `CardCastRejectionReason` had no carrier for `STALE_ACTION` and adding
  one was the only coherent way to satisfy the Product Owner's Option A. No new wire code,
  discriminator, hub method, event, or response shape results — the code and the
  `(bool Accepted, string? Reason)` shape are unchanged — and the recorded decision to take this
  route was confirmed with the requester before editing.
- `src/backend/GameServer.Application/Battle/BattleStateService.cs` — the terminal guard in
  `ExecuteCardCastAsync` and `ExecutePetSkillCastAsync`, on the record the action first read and
  on the fresh record of the compare-and-set retry path. Position: after the record read (absence
  keeps its `null` / `BATTLE_NOT_FOUND` answer) and before the Card-definition lookup and any
  validation, which is the action boundary `MATCH3_RULES.md` §2.1.4 item 7 fixes for the Swap.
  (Shared with TASK-269, which owns this file's other change.)
- `tests/backend/GameServer.Application.Tests/BattleStateServiceTerminalActionGuardTests.cs` —
  12 new tests in a new §4, plus the cast fixtures they need (a Card/Pet-Skill loadout
  configuration, an `ICardDefinitionLookup` double, and a recording `IBattleResultPersistence`
  double so a rejection can prove it reached no battle end). No production seam and no new
  repository double: the retry-path tests use the existing `ContendedBattleStateRepository` for
  exactly the purpose it documents.

### Validation Results

```text
dotnet test src/backend/GameServer.sln
```

The baseline column is an **isolated `git archive HEAD` tree** (the committed tree, without this
session's working-tree changes), so the comparison is like-for-like rather than against a
historical record.

| Suite | Post-change | Isolated `HEAD` baseline |
| :--- | :--- | :--- |
| `GameServer.Domain.Tests` | **PASS** 1623 passed / 0 failed (1623) | 1623 / 0 — identical |
| `GameServer.Api.Tests` | **PASS** 365 passed / 0 failed (365) | 365 / 0 — identical |
| `GameServer.Infrastructure.Tests` | 472 passed / **4 failed** (476) | 472 / **4 failed** — identical set |
| `GameServer.Application.Tests` (full) | 724 passed / **10 failed** (734) | 707 passed / **10 failed** (717) |
| `…~BattleStateServiceTerminalActionGuardTests\|…~BattleResultTerminalFlowTests` | **PASS** 37 passed / 0 failed | n/a |
| `…~BattleHubCardCastTests\|…~BattleHubPetSkillCastTests` (Api) | **PASS** 8 passed / 0 failed | n/a |

- **The +17 Application tests are exactly the two tasks' new tests** (12 here, 5 in TASK-269), all
  passing. The `Balance` set is numerically unchanged (52 passed / 10 failed), and a programmatic
  `Compare-Object` of failing test names before and after reports **IDENTICAL failing sets** — the
  10 `Balance` and 4 `Infrastructure` failures recorded as the TASK-267/268 baseline, all
  assertion failures about boss-definition JSON/balance data rather than reaches of the changed
  code. None was touched or reclassified.
- **The guard tests are discriminating, and that was verified rather than asserted.** The same
  filter was run in a second isolated `git archive HEAD` tree overlaid with only the new test
  files and the Domain carrier — i.e. with `BattleStateService.cs` at `HEAD`, without the guards —
  and reported **12 failed / 25 passed of the same 37**. Nine of those 12 failures are this task's
  guard assertions (both terminal sides × both cast methods, both retry-path cases, the
  no-state-change case, the no-round/no-event/no-battle-end case, and the missing-vs-terminal
  distinction); the other three are TASK-269's fallback assertions. The facts that pass in both
  trees do so **by design**: the code-set fact exercises only the Domain mapping, and the two
  live-battle compatibility facts assert behaviour the guard must not change.
- **Live Redis integration actually ran:** `Infrastructure.Tests/RedisBattleStateRepositoryTests`
  — **PASS 19 passed / 0 failed / 0 skipped** on an explicit filtered run.
- **Not run — no live PostgreSQL.** `127.0.0.1:5432` is closed and `docker` is not installed, so
  no live-database PostgreSQL integration was verified. **Not run:** browser E2E / local runtime
  verification harnesses (prerequisites absent). **Not run: frontend suites** — no client file is
  changed and the presenter's existing `formatCardCastRejection` already renders an unrecognized
  code through its documented safe fallback (`Cast rejected (STALE_ACTION).`), so `vitest` would
  not exercise the changed code path; adding a friendly message for the new code would be
  frontend polish and is reported rather than done (`AGENTS.md` §16). None of these is reported as
  passing.
- `tasks/artifacts/TASK-194-baseline-simulation-results.json` is regenerated by the `Balance`
  suites; it was returned to `HEAD` with `git checkout -- tasks/artifacts/` after the runs, and it
  is not part of the declared set.

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic: the guard is server-side
      Application/Domain work; the client only receives the rejection reason it already receives.
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1): no new system, content, currency,
      mechanic, event, key, or wire member.
- [x] Confirmed P-1…P-6 commit policy compliance: Phase A staged exactly the union of the two
      declared file sets (5 modified files + 1 shared, no task record, no artifact, no unowned
      path) in one joint group commit naming both tasks; conventional subject carrying no task ID;
      body carrying `owns:`/`files:`/`shared with:`/`unowned/pre-existing:`.
- [x] Out of scope and untouched: `SIGNALR_PROTOCOL.md` (§5 item 3 deliberately keeps no parallel
      code list), `CARD_RULES.md` §4's Pet Skill content and magnitudes, the once-per-cast-Turn
      allowance, the compare-and-set retry bound, the Swap guard, the deadlock recovery, and the
      frontend.

### Review Pass (`quality/review.md` §1)

- **Correctness — PASS.** The rule was authored first and the code matches it sentence for
  sentence: the terminal condition is `GAME_RULES.md` §1.4 read through the service's existing
  `IsTerminalBattle`, the code is the established `STALE_ACTION`, the position is the action
  boundary, and each documented absence (Power, state, events, Boss response, round, result,
  reward) has a test that asserts it. The rule's own precedence claim (guard before §3 item 2) is
  what the code does by construction.
- **Architecture — PASS.** The guard sits in the Application boundary where the Swap's terminal
  guard already sits; Domain gains one carrier member and no logic; dependency directions,
  layering, and module ownership are unchanged (`ARCHITECTURE.md` §2.1, §4).
- **Scope — PASS.** Six declared files. No new wire member, discriminator, hub method, event, Redis
  key, response shape, retry bound, cost, or magnitude; the frontend and `SIGNALR_PROTOCOL.md`
  were deliberately not touched.
- **Tests — PASS** at HIGH depth (`core/validation.md` §2): both terminal sides × both cast
  methods, structural whole-record comparison through the serializer (not reference equality) plus
  per-value assertions, the retry path over the documented contended-store double, missing-battle
  and live-battle compatibility, the rejection-code set, and a discriminating pre-fix run.
- **Documentation — PASS.** `CARD_RULES.md` §3 is the owner and states the rule; `MATCH3_RULES.md`
  §1.6.4 item 4 now points at that owner instead of leaving the question open; both Version
  headers are bumped with the prior chain retained, and neither document promises anything the
  code does not do.
- **Security — PASS.** Nothing touched: no authentication, authorization, session, credential, or
  data-exposure surface. The guard only refuses an action on an ended battle.
- **Performance — PASS.** The guard is a two-field comparison before any resolution, so it *removes*
  work on a terminal battle; no new store read or query is added to the live path (`TDD.md` §4.3).
- **Maintainability — PASS.** Four three-line guards reusing the existing predicate and the
  established rejection; no new abstraction, interface, factory, or repository
  (`AGENTS.md` §9). The duplicated rationale comments match the file's existing pattern of
  duplicating the definition lookup and the compare-and-set loop across the two cast methods.
- **Determinism — PASS.** Server authority is preserved (the server decides terminality and the
  rejection); no RNG is drawn on any rejected path; nothing depends on client input, a clock, or
  an allocation order.

### Remaining Issues
1. **One documented wording deviation, reported rather than hidden.** The approved instruction said
   to reuse `STALE_ACTION` and *not* add a new enum member; step 3 above records that a Domain
   carrier member had to be added because none existed, that it introduces no new wire code,
   discriminator, hub method, event, or response shape, and that the route was confirmed with the
   requester before editing.
2. **`CARD_RULES.md` §3 still does not enumerate its other rejection codes.**
   `SIGNALR_PROTOCOL.md` §5 item 3 delegates a cast rejection's codes to §3, whose text names only
   the new one; the other four live in `CardCastRejectionCodes`. That is a pre-existing
   documentation gap, is not a source-of-truth conflict, and was left for its own task rather than
   fixed inline (`AGENTS.md` §16).
3. **The frontend presents the new code through a fallback.** `formatCardCastRejection` maps no
   message for `STALE_ACTION`, so a rejected cast renders `Cast rejected (STALE_ACTION).`. Adding
   a player-facing message is a client presentation change outside this task's declared scope and
   is reported for a follow-up.
4. **The five test-quality findings recorded in TASK-267's completion record (F-03…F-07) remain
   open** and were not touched; they are reserved for the separate test-hardening task.

