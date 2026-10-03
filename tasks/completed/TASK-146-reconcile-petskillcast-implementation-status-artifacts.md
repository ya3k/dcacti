# TASK-146 — Reconcile the Stale `PetSkillCast` Implementation-Status Artifacts in `BattleHub.cs` and `ApiIntegrationTests.cs`

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ by path and section — it does NOT copy contracts, payloads,
  parameters, schemas, or rules.

  THIS TASK AUTHORS NO CONTRACT AND IMPLEMENTS NO BEHAVIOR. It removes two
  now-false implementation-status artifacts: one source-embedded class comment
  in `BattleHub.cs`, and one obsolete assertion plus its rationale in
  `ApiIntegrationTests.cs`. Every contract it touches stays identical in meaning.

  BOUNDARY: exactly TWO edited non-task files. `src/backend/GameServer.Api/Hubs/
  BattleHub.cs` — XML doc comment text only, not one executable line. `tests/
  backend/GameServer.Api.Tests/ApiIntegrationTests.cs` — one test's absent-method
  list entry and its rationale, keeping its real boundary assertion.

  PROVENANCE: both artifacts were discovered, verified, and explicitly REPORTED
  (not fixed) by the post-TASK-144 / TASK-145 reconciliation — see
  `tasks/completed/TASK-145-synchronize-castcast-petskillcast-reconnect-implementation-status.md`
  §"Known artifacts that contradict the implementation" and §"Items Reported, Not
  Fixed" (AGENTS.md §16).

  THIS TASK IS NOT: the `SIGNALR_PROTOCOL.md` §4 item 7 stale §6 → §7 reference,
  the `BATTLE_NOT_FOUND` persisted-result presentation follow-up, the TASK-107
  unfilled Completion Evidence gap, or a general stale-artifact sweep. Those are
  separate concerns and must not be combined with this one.

  LINE REFERENCES are to the files as of this task's creation and must be
  re-verified at pickup (see Implementation Notes).
-->

---

## Metadata

```text
Task ID:           TASK-146
Type:              BUG (TASK_TYPES.md §2 — its BUG definition explicitly includes
                   "documentation bugs (docs are stale)" and "test bugs (test
                   asserts the wrong thing)"; both categories in
                   development/bug-fix.md §1 apply here. See "Type classification
                   note" below.)
Status:            DONE (executed directly at pickup; the lifecycle gates
                   BACKLOG → READY → IN PROGRESS → IN REVIEW → DONE were
                   passed in one session — see Completion Evidence →
                   Lifecycle)
Risk:              MEDIUM (TASK_TYPES.md §4 — BUG baseline LOW–MEDIUM,
                   "depends on what the bug touches". The corrected test is an
                   integration test across the SignalR boundary and the change
                   re-points a boundary guard, so the MEDIUM class applies
                   ("SignalR change", core/task-intake.md §3) and
                   core/validation.md §2 requires integration depth +
                   documentation validation. It is not HIGH: no behavior, rule,
                   contract, state model, or schema changes.)
Priority:          MEDIUM (the false comment tells every reader — human or agent —
                   that `PetSkillCast` does not exist, which is the duplicate-work
                   failure mode TASK-097 / TASK-100 / TASK-101 / TASK-145 already
                   recorded; the obsolete assertion additionally claims a
                   boundary it does not test. It blocks no implementation task and
                   no runtime defect exists.)
Primary Agent:     realtime (RESPONSIBILITY_MATRIX.md §1 assigns "SignalR hub /
                   protocol" to Realtime — authoritative source
                   `SIGNALR_PROTOCOL.md`, ADR-004 — and realtime.md's modifiable
                   scope includes `src/GameServer.Api/Hubs/*`. The stale claim is
                   about the hub's method surface, which `SIGNALR_PROTOCOL.md` §2
                   owns. Precedent: TASK-143's hub-method task was Realtime-primary.
                   See "Agent selection note" below.)
Supporting Agents: testing (owns `tests/` — RESPONSIBILITY_MATRIX.md §1
                   "Integration tests → Testing" — and owns the replacement
                   boundary assertion),
                   backend (owns the `GameServer.Api` layer file being edited),
                   review (documentation consistency verification)
Workflow:          development/bug-fix.md
Skills:            discovery/documentation-discovery,
                   quality/documentation-consistency,
                   realtime/realtime-protocol-validation,
                   testing/test-scenario-generation
                   (4 skills — Simple budget, tasks/README.md §12; the set is
                   .ai/skills/README.md §5.3's mapping for
                   development/bug-fix.md, restricted to the categories this bug
                   touches: the protocol surface and the test)
Dependencies:      TASK-115 (DONE — implemented the server-authoritative
                     `PetSkillCast` path this comment denies),
                   TASK-120 (DONE — client `CardCast` / `PetSkillCast` action
                     paths),
                   TASK-145 (DONE — verified and REPORTED both artifacts; also
                     the precedent for this reconciliation's boundary discipline),
                   TASK-143 (DONE — the identical precedent: `GetBattleState` was
                     removed from this same absent-method list once implemented),
                   TASK-107 (COMPLETED — Basic `CardCast` server path, context);
                   all five are read-only evidence sources and are NOT modified
Blocks:            Nothing. This task unblocks no implementation; it removes two
                   false statements about already-DONE work.
Estimate:          Simple (two artifacts in two files: one comment clause, one
                   test list entry plus its rationale; no new test, no new
                   abstraction, no executable behavior)
```

**Type classification note.** `BUG` — not `DOCUMENTATION` and not `REFACTOR`.

It is **not `DOCUMENTATION`**: `TASK_TYPES.md` §2 defines that type as *"Change
`docs/` content — documentation is the primary output, not code"* and states that
code changes which also touch docs use the code-change type instead. Zero files
under `docs/` are edited here; the primary output is a `.cs` file and a test.

It is **not `REFACTOR`**: `development/refactor.md` §2 requires *"existing tests
(must still pass, **unchanged in intent**)"* and §1 forbids smuggling a change of
intent into a refactor. This task changes one test's intent by design — it removes
a claim that `PetSkillCast` is not implemented and requires the replacement
boundary to remain a genuine assertion — so the REFACTOR preservation check cannot
be honestly discharged. TASK-101's `REFACTOR` precedent covers **source-comment-only**
changes; it does not cover a test whose assertion and rationale change.

It **is `BUG`**, and the bug categories are determined first per
`development/bug-fix.md` §1:

```text
Artifact A (BattleHub.cs class comment)  →  Documentation bug
                                             (the source-embedded documentation is stale;
                                              the code reflects the intended behavior)
Artifact B (test list entry + rationale) →  Test bug
                                             (the test asserts the wrong thing)
```

`development/bug-fix.md` §3's default assumption — *docs describe intended
behavior; code is the (possibly incorrect) implementation* — resolves in favor of
the code here, and the evidence is explicit rather than assumed:
`docs/02-technical/SIGNALR_PROTOCOL.md` §2 documents `PetSkillCast` as a client →
server hub method, the hub implements it, `docs/02-technical/ARCHITECTURE.md`
§2.2.1 (synchronized by TASK-145) states it is implemented, and the completed
TASK-115/TASK-120 record its implementation and passing suites. The two artifacts
are the wrong side of the contradiction. The workflow's regression artifact is the
corrected boundary assertion itself plus the already-existing
`BattleHubPetSkillCastTests` coverage of the positive contract; `bug-fix.md` §4's
hard rule (never change behavior to satisfy a test) is not engaged, because no
behavior changes.

**Agent selection note.** Two ownership rows are in play:
`RESPONSIBILITY_MATRIX.md` §1 assigns *"SignalR hub / protocol"* to **Realtime**
(authoritative source `SIGNALR_PROTOCOL.md`), and *"Integration tests"* to
**Testing**. The subject of both artifacts is the hub's **method surface**, which
`SIGNALR_PROTOCOL.md` §2 owns, and `realtime.md` lists `src/GameServer.Api/Hubs/*`
in its modifiable scope — so Realtime is primary and Testing owns the test-side
edit as a supporting agent (`RESPONSIBILITY_MATRIX.md` §2.3 cross-boundary
coordination). The precedent for a BUG task editing source comments and tests
together is `tasks/completed/TASK-047-synchronize-boss-technical-identity-in-source-and-tests.md`.

**Readiness note (BACKLOG → READY, `TASK_LIFECYCLE.md` §3).**

```text
[x] Task type confirmed (TASK_TYPES.md §2 — BUG, categories in development/bug-fix.md §1)
[x] Relevant documentation exists in docs/ (SIGNALR_PROTOCOL.md §2–§5, §7, §8)
[x] MVP scope confirmed (docs/00-overview/MVP_SCOPE.md §1 — Signature Skill /
    Pet Skill Cards are IN; this task adds no system, content, or capability)
[x] Not blocked by an unresolved dependency (all dependencies DONE, evidence-only)
[x] Primary agent assigned (realtime); supporting agents assigned
[x] Workflow assigned (development/bug-fix.md)
[x] Acceptance criteria are binary and testable
```

---

## Objective

Remove the two stale `PetSkillCast` implementation-status artifacts so that both
agree with the currently implemented and documented state, while changing no
runtime behavior and no contract:

1. Correct the `BattleHub` class comment that states `PetSkillCast` *"is
   intentionally NOT implemented"*, so the hub's own documentation of its method
   surface matches the method the same file implements, citing
   `docs/02-technical/SIGNALR_PROTOCOL.md` §2 as the owner of that surface.
2. Correct `ApiIntegrationTests.BattleHub_ShouldNotRegisterGameplayMethods` so it
   no longer claims `PetSkillCast` is not implemented / not registered, and so it
   keeps asserting the boundary that is *actually* still true — that a Server →
   Client delivery (`ReceiveEvents`) is not an invokable hub method — following
   the treatment that same test already documents for `GetBattleState`.

Both corrections are confined to implementation-status accuracy. No SignalR
method, parameter, payload, event, ordering, `BattleState`, Redis, authentication,
authorization, or gameplay statement changes.

---

## Authoritative References

- `docs/02-technical/SIGNALR_PROTOCOL.md` **§2** — **the canonical owner of the
  client → server (hub method) surface**, and the authority that makes both
  artifacts false: it lists the client-invokable methods and their parameters.
  Cited by the corrected comment; **not edited**
- `docs/02-technical/SIGNALR_PROTOCOL.md` **§3** — Server → Client (event
  delivery); the section that makes `ReceiveEvents` a delivery and not an
  invokable method. **Not edited**
- `docs/02-technical/SIGNALR_PROTOCOL.md` §3.2.20 / §3.2.21 / §3.2.22 and **§5** —
  the `CardCast` / `PetSkillCast` wire items, their emission order, and the
  acknowledgement envelope the *runtime* honors; cited so the correction does not
  restate them. **Not edited**
- `docs/02-technical/SIGNALR_PROTOCOL.md` §7, §8 — reconnect/resync and the
  "what is not here" exclusions; cited for the `GetBattleState` and `ReceiveEvents`
  precedent language already present in the test. **Not edited**
- `docs/02-technical/ARCHITECTURE.md` §2.2.1 — synchronized by TASK-145 (Version
  1.4) to state that `CardCast`, `PetSkillCast`, and reconnect/resync recovery
  **are implemented**; the document-level cross-check for the corrected prose.
  **Not edited**
- `docs/02-technical/GAME_EVENTS.md` §2 — `CardCast` / `PetSkillCast` event
  definitions; cited for agreement only. **Not edited**
- `docs/01-game-design/CARD_RULES.md` §4 / §6 — Pet Skill Card / Signature Skill
  casting and the `CardCast` → `PetSkillCast` emission rule; the domain owner of
  what the method does. **Not edited**
- `src/backend/GameServer.Api/Hubs/BattleHub.cs` — **the stale artifact A** (class
  XML doc comment) and the implemented `PetSkillCast` runtime entry point
- `tests/backend/GameServer.Api.Tests/ApiIntegrationTests.cs` — **the stale
  artifact B** (`BattleHub_ShouldNotRegisterGameplayMethods`)
- `src/backend/GameServer.Application/Battle/BattleStateService.cs` —
  `ExecutePetSkillCastAsync`, the Application-layer call `BattleHub.PetSkillCast`
  delegates to (read-only evidence; **not edited**)
- `src/backend/GameServer.Domain/Cards/CardCastExecutor.cs` — the Domain execution
  path that emits the `PetSkillCast` event (read-only evidence; **not edited**)
- `src/backend/GameServer.Api/Hubs/BattleEventWireProjection.cs` — the §3.2.21
  wire projection of that event (read-only evidence; **not edited**)
- `tests/backend/GameServer.Api.Tests/Hubs/BattleHubPetSkillCastTests.cs` — the
  covering suite for the positive `PetSkillCast` contract (read-only; **not
  edited**), which is what the corrected test must point at
- `tests/backend/GameServer.Api.Tests/Hubs/BattleHubReconnectRecoveryTests.cs` —
  the sibling absence probe (`GetBattleState`'s contract suite) and the identical
  removal precedent's owner (read-only; **not edited**)
- `tasks/completed/TASK-115-implement-server-authoritative-petskillcast-crit-and-burn.md`,
  `TASK-120-implement-client-card-and-skill-cast-action-paths.md`,
  `TASK-145-synchronize-castcast-petskillcast-reconnect-implementation-status.md`,
  `TASK-143-implement-server-battle-state-reconnect-recovery.md`,
  `TASK-107-implement-basic-cardcast-server-path.md` — the DONE work that makes
  both artifacts false, and the provenance of their discovery (read-only; **not
  modified**)
- `tasks/completed/TASK-101-synchronize-battlestateservice-boss-response-comment.md`
  and `TASK-047-synchronize-boss-technical-identity-in-source-and-tests.md` — the
  classification precedents for source-comment and source-plus-test
  synchronization (read-only; **not modified**)
- `AGENTS.md` §15 (never edit a test to make incorrect behavior pass — not
  engaged, the implementation is correct), §16 (report, do not fix inline), §17
  (documentation change rule), §4 (conflict resolution), §9 (anti-overengineering),
  §10 / ADR-001 (server authority)
- `.ai/workflow/development/bug-fix.md` §1–§4, `.ai/workflow/core/validation.md`
  §2 (MEDIUM depth), `.ai/agents/realtime.md`, `.ai/agents/RESPONSIBILITY_MATRIX.md`
- `tasks/TASK_LIFECYCLE.md` §3 (completed tasks are immutable)

---

## Scope

### In Scope

1. **Re-verify the Current State inventory against the two files and the four
   source links at pickup.** Every row of the evidence table must be reproduced,
   including the "why this test passes" finding. A premise that no longer holds
   changes this task's basis — see Stop Conditions.
2. **Correct the `PetSkillCast` clause of the `BattleHub` class comment only**
   (`BattleHub.cs`, ~L490–491). The corrected text must:
   - no longer state or imply that `PetSkillCast` is unimplemented, out of scope,
     or absent;
   - name `SIGNALR_PROTOCOL.md` §2 as the owner of the client → server method
     surface (cite, do not restate);
   - not restate the method's parameters, the acknowledgement shape, the event
     payload, the rejection codes, or the emission order;
   - not claim anything about any capability other than `PetSkillCast`'s status.
3. **Preserve the rest of that comment byte-for-byte** — the `Swap` / `CardCast`
   sentence, the `GetBattleState` paragraph, the `ReceiveEvents` paragraph, and
   the thin-transport framing are correct and are not this task's subject.
4. **Correct `ApiIntegrationTests.BattleHub_ShouldNotRegisterGameplayMethods`**
   (~L255–291):
   - remove `"PetSkillCast"` from the absent-method list (~L284);
   - correct the stale rationale (~L261–262) so the test's stated boundary matches
     what it asserts, recording that `PetSkillCast` **is** implemented and that its
     contract is covered by `BattleHubPetSkillCastTests` — mirroring the treatment
     the same comment already gives `GetBattleState` (~L277–280);
   - keep the `"ReceiveEvents"` entry, its rationale (~L264–270), and the
     assertion mechanism (~L286–287) unchanged.
5. **Require the corrected test to remain a real boundary assertion.** The list
   must contain only names that are **not** `BattleHub` hub methods, so each
   invocation fails because the method is absent — never because the call supplied
   the wrong number of arguments. Removing `PetSkillCast` must not leave the test
   vacuous, and the test must not be deleted.
6. **Verify — not "improve" — everything else.** The `CardCast` clause in the same
   comment, the other assertion blocks in `ApiIntegrationTests.cs`, and
   `BattleHubReconnectRecoveryTests`'s timeout-bounded absence probe are expected
   to require **no change**. Confirm each.
7. **Report, never fix, anything else** (`AGENTS.md` §16): additional stale
   implementation-status claims (including those enumerated in Current State §
   "Reported, not fixed"), and any comment or test whose wording is loose but not
   false.

### Out of Scope

- **The `CardCast` clause of the same comment**, unless re-verification proves it
  false. It is verified true at creation: `SIGNALR_PROTOCOL.md` §2 carries
  `CardCast(battleId, cardId, clientSequence)` for Basic Cards and
  `PetSkillCast(battleId, clientSequence)` for the active Pet's Signature Skill.
  Report a discovered problem; do not change the protocol wording.
- **Renaming the test method** `BattleHub_ShouldNotRegisterGameplayMethods`. A
  method name is neither a stale assertion nor a stale comment, so it is outside
  this task's stated boundary; its looseness also predates this task (`Swap` and
  `CardCast` were already registered gameplay methods when it was written).
  Report it if the executing agent believes it is independently false.
- **Any other test** in `ApiIntegrationTests.cs` or in any other test file, and any
  new test class, test project, helper, or reflection-based hub inventory
  (`AGENTS.md` §9).
- **All executable behavior.** No hub method signature, body, attribute, DI
  registration, record, parameter, validation, rejection code, broadcast, or
  ordering changes (`AGENTS.md` §10, ADR-001).
- **Any file under `docs/`** — including `SIGNALR_PROTOCOL.md` (its §4 item 7
  stale §6 → §7 reference is a separate, excluded concern), `ARCHITECTURE.md`,
  `GAME_EVENTS.md`, and `CARD_RULES.md`.
- **`BattleStateService.cs`, the Domain (`CardCastExecutor`, `BattleEvent`),
  `BattleEventWireProjection.cs`, the Redis store, and the client** — verified
  correct; not edited.
- **Protocol:** new SignalR methods, new events, payload changes, hub signature
  changes, `BattleState` schema changes, Redis changes, API contract changes.
- **Gameplay:** board, gems, swap, match detection, cascade, damage, Crit, Burn,
  Pet progression, Cards, Relics, Passives, Bosses, rewards — no rule, value,
  formula, or timing changes.
- **Authentication / authorization** semantics — not touched, not reinterpreted.
- **Any completed task file** (`TASK-107`, `TASK-115`, `TASK-120`, `TASK-143`,
  `TASK-144`, `TASK-145`, `TASK-101`, `TASK-047`, and all others) — immutable per
  `TASK_LIFECYCLE.md` §3.
- **The separately-tracked follow-ups:** the `SIGNALR_PROTOCOL.md` §4 item 7 stale
  §6 → §7 reference; the `BATTLE_NOT_FOUND` persisted-result presentation; the
  TASK-107 incomplete historical completion evidence. Not investigated, not
  scoped, not created here.
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2.

---

## Current State

`PetSkillCast` is implemented and documented as implemented. Two artifacts still
claim otherwise.

### Verified implementation evidence (re-verify at pickup)

| Link in the chain | Verified state | Evidence (as of creation) |
|---|---|---|
| Documented as a client → server hub method | **TRUE** | `docs/02-technical/SIGNALR_PROTOCOL.md` §2 lists `Swap`, `CardCast`, `PetSkillCast` with their parameters |
| `BattleHub.PetSkillCast` | **EXISTS** | `BattleHub.cs:895` `public async Task<PetSkillCastResponse> PetSkillCast(string battleId, string? clientSequence = null)`; response record at `:344` |
| → Application layer | **EXISTS** | `BattleHub.cs:903` delegates to `BattleStateService.ExecutePetSkillCastAsync` (`BattleStateService.cs:1080`), which reads the authoritative state, identifies the active Pet's Pet Skill Card, and commits under the repository's compare-and-set |
| → Domain execution + event | **EXISTS** | `CardCastExecutor.cs:109` emits `BattleEvent.CreatePetSkillCast(...)` |
| → authoritative `BattleState` write-back | **EXISTS** | Same Application path (`BattleStateService`, `GAME_STATE.md` §5.1) — cited, not restated |
| → documented SignalR projection | **EXISTS** | `BattleEventWireProjection.cs:534–539` (`PetSkillCast(cardId)` wire item), `:747` (switch arm), `:785–788` (projection) → `SIGNALR_PROTOCOL.md` §3.2.21, ordered by §3.2.22 |
| Client action path | **EXISTS** | `SignalRService.petSkillCast` (~`:648–655`), the `PetSkillCast` runtime action kind (`GameRuntimeEvents.ts`), `BattleScene.submitPetSkillCast` (~`:768`) — TASK-120 DONE |
| Positive contract covered by tests | **TRUE** | `tests/backend/GameServer.Api.Tests/Hubs/BattleHubPetSkillCastTests.cs` — three facts (`:110` accepted + `BattleStateUpdated`/`ReceiveEvents` broadcast + §3.2.20/§3.2.21 wire items in order, `:167` `INSUFFICIENT_POWER`, `:191` `BATTLE_NOT_FOUND`), each invoking `InvokeAsync<PetSkillCastResponse>("PetSkillCast", battleId, "client-seq-N")` |
| Completed tasks | **DONE** | TASK-115 (server path, Crit, Burn), TASK-120 (client paths); TASK-145 recorded the capability as implemented in `ARCHITECTURE.md` §2.2.1 |
| **Hub methods that actually exist** | **Five** | `BattleHub.cs` — `JoinBattle` `:603`, `GetBattleState` `:698`, `Swap` `:801`, `CardCast` `:859`, `PetSkillCast` `:895`. `ReceiveEvents` is **not** among them: it is only ever *sent* (`Clients.Group(...).SendAsync("ReceiveEvents", …)`, e.g. `:884`, `:918`) |

### Stale artifact A — `src/backend/GameServer.Api/Hubs/BattleHub.cs` (class XML doc)

Inside the class `<summary>` (opened `:470`, closed `:503`) the comment reads,
verbatim (`:489–491`):

```text
/// <c>CardCast</c> is implemented for Basic Cards (<c>CARD_RULES.md</c> §2, §3).
/// <c>PetSkillCast</c> (§2) is a client → server method that is intentionally NOT
/// implemented: it requires Pet Skill Card resolution, which is out of scope.
```

The second and third lines are **FALSE**: `PetSkillCast` is a registered hub
method in the same file (`:895`) whose Application and Domain paths exist and whose
event projection is contractually specified. Classification: **stale
implementation-status documentation embedded in source**. The neighbouring
`CardCast` clause (`:489`) is verified true; the `GetBattleState` paragraph
(`:493–498`, synchronized by TASK-143/TASK-145) and the `ReceiveEvents` paragraph
(`:500–502`) are verified true.

### Stale artifact B — `tests/backend/GameServer.Api.Tests/ApiIntegrationTests.cs`

`BattleHub_ShouldNotRegisterGameplayMethods` (`:255–291`) contains a stale
rationale and a stale list entry:

```text
:261–262   // `PetSkillCast` is a §2 client → server method that is intentionally NOT
           // implemented, so a client invocation fails.
:284       foreach (var method in new[] { "PetSkillCast", "ReceiveEvents" })
:286–287   await Assert.ThrowsAnyAsync<Exception>(() =>
               hubConnection.InvokeAsync<object>(method));
```

The stated rationale is **FALSE** (see artifact A). The assertion **PASSES**, but
not for the reason the comment gives — see "Verified Contradiction" §2.

Two things in this test are *not* stale and must survive the correction:

```text
:264–270   the `ReceiveEvents` rationale — it is a §3 Server → Client delivery,
           sent by the server and not an invokable hub method; both halves of that
           distinction matter
:272–280   `JoinBattle` / `Swap` / `CardCast` deliberately absent from the list,
           and `GetBattleState` deliberately absent because §7's snapshot method
           "is now implemented, so it is a real invokable hub method. Its contract
           is covered by BattleHubReconnectRecoveryTests."
```

That `GetBattleState` note is the repository's own, already-documented treatment of
exactly this situation and is the model for the correction.

### Reported, not fixed (outside this task's two-file boundary — `AGENTS.md` §16)

Listed so the executing agent does not mistake them for this task's scope, and does
not treat a stale comment as evidence of absence:

- `src/backend/GameServer.Infrastructure/Postgres/Migrations/20261001112446_StructureCardDefinitionEffectDefinition.cs`
  ~L214–215 and ~L238–239 — present-tense claims that "PetSkillCast remains
  blocked"; superseded by TASK-110 (magnitudes authored) and TASK-115
  (implemented). A migration comment records the authoring task's state; report,
  do not edit.
- `src/backend/GameServer.Domain/Cards/CardDefinition.cs:53–58` and
  `.../Postgres/Configurations/CardDefinitionConfiguration.cs:101` — verified
  **not stale**: both are task-scoped statements about what TASK-028/TASK-109/
  TASK-112 did (they remain true of those types and tasks). Nothing to fix.
- `ApiIntegrationTests.cs:949–954` (`PassiveCharged` / `PassiveTriggered` /
  `PetStateUpdated`) and
  `BattleHubReconnectRecoveryTests.cs:641–675` (`Hub_DefinesNoEventReplayOrAdditionalRecoveryMethod`)
  — verified **not defective**: every name in both probes is absent as a hub
  method, so each fails on absence rather than on argument arity. Do not "unify"
  or restyle them.
- The test method name `BattleHub_ShouldNotRegisterGameplayMethods` — see Out of
  Scope.

---

## Verified Contradiction

1. **The contradiction itself.** `docs/02-technical/SIGNALR_PROTOCOL.md` §2 — the
   owner of the client → server method surface — lists `PetSkillCast`. The
   implementation path exists end to end
   (`BattleHub.PetSkillCast` → `BattleStateService.ExecutePetSkillCastAsync` →
   authoritative `BattleState` → the documented `BattleEventWireProjection`
   §3.2.21 projection), TASK-115/TASK-120 are DONE, and
   `docs/02-technical/ARCHITECTURE.md` §2.2.1 (synchronized by TASK-145) states
   the capability is implemented. Both artifacts nevertheless say `PetSkillCast`
   is not implemented. The artifacts contradict the authoritative protocol **and**
   the code.

2. **What the test actually asserts, and why it passes.** Verified by execution at
   this task's creation:

   ```text
   dotnet test tests/backend/GameServer.Api.Tests/GameServer.Api.Tests.csproj \
     --filter "FullyQualifiedName~BattleHub_ShouldNotRegisterGameplayMethods"
   → Passed!  Failed: 0, Passed: 1, Skipped: 0, Total: 1
   ```

   It passes **because the invocation supplies no arguments**:
   `hubConnection.InvokeAsync<object>("PetSkillCast")` calls a registered method
   that requires `battleId`, so the call fails on the request's shape, not on the
   method's absence. The probe cannot distinguish "no such hub method" from
   "registered hub method invoked with the wrong arguments" — while
   `BattleHubPetSkillCastTests` invokes the *same* method with its two documented
   arguments and receives `accepted: true`, and TASK-115 records that suite
   passing. The entry therefore never tested the registration boundary its
   rationale claims, and removing it removes **no real coverage**.

3. **The `ReceiveEvents` half is different and still true.** No such hub method
   exists — the name is a §3 Server → Client delivery the server sends through
   `SendAsync`. Its zero-argument probe fails because the name is unresolvable,
   which is exactly the direction boundary `SIGNALR_PROTOCOL.md` §2 vs §3 draws
   and `BattleHub.cs:500–502` restates. That assertion, its rationale, and the
   assertion mechanism are correct and must be preserved.

4. **The correct current boundary**, therefore, is:

   ```text
   `PetSkillCast`   §2  client → server hub METHOD      → invokable; NOT a valid
                                                          "not registered" entry
   `ReceiveEvents`  §3  Server → Client DELIVERY         → not a hub method; a valid
                                                          "not registered" entry
   ```

   This is exactly the distinction the same test already applies to
   `GetBattleState` (`:277–280`) and matches `ARCHITECTURE.md` §2.2.1's statement
   that the hub's methods are split by direction. No new architectural decision,
   contract interpretation, or test mechanism is required — the correction is a
   status correction.

---

## Dependencies

All dependencies are **DONE evidence sources**, read-only for this task and
immutable per `tasks/TASK_LIFECYCLE.md` §3:

```text
TASK-115  DONE       server-authoritative PetSkillCast path   → falsifies artifact A/B's claim
TASK-120  DONE       client CardCast / PetSkillCast paths      → corroborates the capability
TASK-145  DONE       recorded both artifacts as REPORTED       → provenance; boundary-discipline precedent
TASK-143  DONE       GetBattleState removal precedent          → the correction pattern already in the test
TASK-107  COMPLETED  Basic CardCast server path                → context for the same comment paragraph
```

`TASK-101` and `TASK-047` are cited as **classification precedents** only. No
dependency is incomplete and this task creates no dependency for another task:
`PetSkillCast`'s runtime contract is already covered by
`BattleHubPetSkillCastTests`, which needs no change.

---

## Acceptance Criteria

- [x] The current `PetSkillCast` implementation is verified against the
      authoritative protocol (`SIGNALR_PROTOCOL.md` §2, §5, §3.2.20–§3.2.22) and
      against the full source path (`BattleHub.PetSkillCast` →
      `BattleStateService.ExecutePetSkillCastAsync` → authoritative
      `BattleState` → the documented wire projection) before any edit.
- [x] The stale "`PetSkillCast` … intentionally NOT implemented" source
      commentary is identified by exact location in `BattleHub.cs`.
- [x] The stale `ApiIntegrationTests` `PetSkillCast` boundary assertion — its list
      entry **and** its rationale — is identified by exact location, and the reason
      it currently passes is reproduced.
- [x] The correct current Hub boundary is established from
      `SIGNALR_PROTOCOL.md` §2 vs §3 (client → server methods vs Server → Client
      deliveries), not from the test's own rationale.
- [x] The `BattleHub.cs` `PetSkillCast` clause no longer states or implies that
      `PetSkillCast` is unimplemented, out of scope, or absent, and names
      `SIGNALR_PROTOCOL.md` §2 as the owner of the method surface.
- [x] Every other sentence of that comment is byte-unchanged: the `Swap` /
      `CardCast` clause, the `GetBattleState` paragraph, the `ReceiveEvents`
      paragraph, and the thin-transport framing.
- [x] `BattleHub.cs`'s executable content is byte-unchanged — no method signature,
      body, attribute, record, using directive, or DI registration differs.
- [x] `BattleHub_ShouldNotRegisterGameplayMethods` no longer lists `"PetSkillCast"`
      and no longer claims it is unimplemented or not registered.
- [x] That test's stated boundary now matches what it asserts, and records that
      `PetSkillCast`'s contract is covered by `BattleHubPetSkillCastTests`
      (mirroring the existing `GetBattleState` note).
- [x] The `"ReceiveEvents"` entry, its rationale, and the assertion mechanism are
      preserved byte-for-byte.
- [x] Every name in the corrected list is verified **not** to be a `BattleHub` hub
      method, so each invocation fails on absence rather than on argument arity.
- [x] The corrected test passes, and it is not vacuous, not deleted, and not
      converted into a duplicate of the positive `PetSkillCast` contract.
- [x] `PetSkillCast` runtime behavior is unchanged: validation, rejection codes,
      `BATTLE_NOT_FOUND` handling, the `BattleStateUpdated` + `ReceiveEvents`
      broadcasts, and the §5 acknowledgement are exactly as before.
- [x] SignalR payload/event contracts are unchanged: no §2 method added, removed,
      or renamed; no §3.2.20 / §3.2.21 / §3.2.22 payload member, value, or order
      changed; the `PetSkillCastResponse` record is byte-unchanged.
- [x] `BattleState` / Redis contracts are unchanged: no edited line in
      `BattleStateService`, no state-shape, key, or TTL change.
- [x] No gameplay behavior changes (no board, gem, swap, match, cascade, damage,
      Crit, Burn, Pet progression, Card, Relic, Passive, Boss, or reward code
      touched).
- [x] Authentication and authorization semantics are unchanged and unreinterpreted.
- [x] Completed task files remain unchanged: `tasks/completed/**` — including
      TASK-107, TASK-115, TASK-120, TASK-143, TASK-144, TASK-145, TASK-101, and
      TASK-047 — are hash-verified identical.
- [x] Relevant backend/API tests pass after reconciliation at `core/validation.md`
      §2's MEDIUM depth: the `GameServer.Api.Tests` suite green, including
      `ApiIntegrationTests`, `BattleHubPetSkillCastTests`, and
      `BattleHubReconnectRecoveryTests`.
- [x] No file under `docs/` is modified (hash-verified).
- [x] No unrelated source or test file is modified (hash-verified over `src/**` and
      `tests/**`), and no new test, test class, helper, or project is added.
- [x] The changed-file set equals exactly
      `src/backend/GameServer.Api/Hubs/BattleHub.cs`,
      `tests/backend/GameServer.Api.Tests/ApiIntegrationTests.cs`, plus this task
      file.
- [x] No authoritative rule or contract is violated (`AGENTS.md` §10 / ADR-001) and
      the quality review checklist passes (`quality/review.md` §1), skipping only
      items a comment-plus-test correction cannot exercise.

### Explicit Constraints

```text
No gameplay.
No new feature or capability.
No protocol change (no new method, event, payload member, or ordering change).
No hub signature change. No BattleState / Redis / API contract change.
No change to server authority, authentication, or authorization.
No docs/ change.
No test deletion and no weakening of boundary coverage.
No new test, test class, helper, or reflection-based inventory.
No BATTLE_NOT_FOUND presentation work.
No SIGNALR_PROTOCOL.md §4 item 7 cross-reference fix.
No TASK-107 completion-evidence work.
TASK-107 / TASK-115 / TASK-120 / TASK-143 / TASK-144 / TASK-145 and all other
  completed tasks are NOT modified.
```

---

## Affected Files & Areas

```text
EDITED (completed)

[x] src/backend/GameServer.Api/Hubs/BattleHub.cs
        the class XML doc comment's `PetSkillCast` clause only (~L490–491);
        zero executable lines

[x] tests/backend/GameServer.Api.Tests/ApiIntegrationTests.cs
        `BattleHub_ShouldNotRegisterGameplayMethods` only: the absent-method list
        entry (~L284) and the stale rationale (~L261–262)

[x] tasks/backlog/TASK-146-reconcile-petskillcast-implementation-status-artifacts.md
        Status and Completion Evidence only (moved to tasks/completed/ at DONE)

VERIFIED UNCHANGED (hash-verified at completion)

[x] src/backend/**            (all other files, including BattleStateService.cs,
                               CardCastExecutor.cs, BattleEventWireProjection.cs,
                               the CardDefinition types, and the migrations)
[x] src/frontend/**           (the client action path is already correct)
[x] tests/**                  (except the one named test file)
[x] docs/**                   (all of docs/00-overview, 01-game-design,
                               02-technical, 03-decisions)
[x] tasks/completed/**        (all completed tasks immutable)
[x] tasks/blocked/**, tasks/active/**
```

---

## Implementation Notes

- **Verified reality is the authority, not this task's prose.** Re-verify every
  row of Current State and both "Verified Contradiction" findings at pickup. If
  the source differs from what this file documents, **the source wins** and the
  difference is reported (`AGENTS.md` §16, `§4`).
- **The comment correction is surgical.** Replace the `PetSkillCast` clause; do
  not delete the paragraph and do not rewrite its neighbours. The paragraph
  carries the `CardCast`, `GetBattleState`, and `ReceiveEvents` boundary
  statements, and the missing-method cross-reference the same class relies on.
- **Cite, do not restate.** Name `SIGNALR_PROTOCOL.md` §2 as the owner of the
  method surface. Do not restate parameters, the acknowledgement envelope, wire
  members, rejection codes, or emission order — those live in §2/§3.2/§5.
- **Status wording, not mechanism wording.** The corrected clause must state
  *that* `PetSkillCast` is implemented and *where its surface is owned*. It must
  not describe how the cast resolves, what it emits, or what it costs.
- **Do not over-correct into "everything is implemented".** Only the
  `PetSkillCast` status statement was false. Do not claim anything about other
  capabilities, the MVP, the reconnect path, or result presentation.
- **Keep the XML doc well-formed.** The edit is inside a `<summary>` block; keep
  tag structure and escaping (`<c>`, `§`, `→`) intact and build the project.
- **The test correction mirrors a note already in the same comment.** Follow the
  `GetBattleState` treatment at ~L277–280: implemented → not in this list → its
  contract is covered by its own suite. For `PetSkillCast` that suite is
  `BattleHubPetSkillCastTests`.
- **The list entry and the rationale must agree.** A rationale that still says
  `PetSkillCast` is unimplemented while the list omits it — or a removed list entry
  with the stale rationale left in place — is a half-fix and fails this task.
- **Do not delete the test and do not replace it with a positive assertion.**
  The positive contract already has its own suite; duplicating it adds no boundary
  and increases coupling.
- **Do not add a reflection-based hub inventory or any helper.** `AGENTS.md` §9.
  Verifying the list by reading `BattleHub`'s public methods at pickup is
  sufficient; the guard is a test of the boundary, not of the type system.
- **The zero-argument probe is only valid for names that are genuinely absent.**
  That is precisely why `PetSkillCast` must leave the list and `ReceiveEvents` must
  stay on it.
- **Encoding safety.** Write both files with a UTF-8-preserving writer, matching
  each file's existing line endings. Verify no BOM, zero U+FFFD, zero mojibake,
  and that em-dashes, en-dashes, arrows (`→`), and `§` survive outside the edited
  lines. TASK-098's process note records typographic damage from a
  `Get-Content`/`Set-Content` round-trip; do not repeat it.
- **The working tree is not clean.** TASK-145 recorded ~149 pre-existing
  modified/untracked entries before it ran, and the `bin/`/`obj/` trees are
  populated. Record guard-file hashes at pickup rather than relying on
  `git status` alone to prove what this task changed.
- **Reported items stay reported.** The migration comments, the test method name,
  and the three separately-tracked follow-ups listed in Scope/Out of Scope are not
  to be fixed here, however tempting.

---

## Testing Requirements

Per `core/validation.md` §2, a MEDIUM `BUG` task takes build validation,
integration tests across the boundary it touches (SignalR), and documentation
validation. Gameplay-scenario depth is not required: no rule is derived, changed,
or exercised. (The suite commands below are the repository's; verify the project
path at pickup.)

### Required Verification

```text
[ ] Build                 — the backend solution / Api test project builds with no
                            new error or warning.
[ ] Integration tests     — dotnet test
                            tests/backend/GameServer.Api.Tests/GameServer.Api.Tests.csproj
                            (full suite green).
[ ] Focused re-run        — BattleHub_ShouldNotRegisterGameplayMethods PASS, and
                            it still passes *for the intended reason*: every name
                            it lists is absent as a hub method.
[ ] Positive contract     — BattleHubPetSkillCastTests PASS, unmodified (the
                            boundary the corrected test now points at).
[ ] Sibling guard         — BattleHubReconnectRecoveryTests PASS, unmodified.
[ ] Absence enumeration   — every name in the corrected list is confirmed NOT to
                            be among BattleHub's hub methods (JoinBattle,
                            GetBattleState, Swap, CardCast, PetSkillCast at
                            creation).
[ ] Diff-shape check      — the BattleHub.cs diff contains comment lines only:
                            zero declaration, statement, attribute, or using-line
                            changes.
[ ] Half-fix check        — the list entry and the rationale agree; no stale
                            "not implemented" claim about PetSkillCast remains in
                            the test.
[ ] No-vacuity check      — the corrected test asserts at least one currently-true
                            boundary (`ReceiveEvents`), and was not deleted.
[ ] Cross-document agree  — SIGNALR_PROTOCOL.md §2/§3, ARCHITECTURE.md §2.2.1,
                            GAME_EVENTS.md §2, CARD_RULES.md §4/§6, the corrected
                            comment, and the corrected test no longer disagree
                            about PetSkillCast's implementation status.
[ ] Unmodified-guard      — hash comparison, pickup vs completion, over:
                            src/** (except the one named file), tests/** (except
                            the one named file), docs/**, tasks/completed/**.
[ ] Changed-file scope    — exactly the two named files plus this task file.
[ ] Docs validation       — zero docs/ files modified; the corrected prose cites
                            documents that own what they are cited for
                            (documentation-change.md §2's no-duplication rule,
                            applied to the comment).
[ ] Gameplay scenarios    — N/A: no gameplay rule is derived, changed, or
                            exercised (AGENTS.md §6).
```

### Key Edge Cases

- **Half-fix.** Removing the list entry but leaving the rationale (or the reverse)
  is the likeliest failure mode; it leaves a false statement behind and fails the
  task.
- **Deleting the whole comment paragraph.** It carries the `CardCast`,
  `GetBattleState`, and `ReceiveEvents` boundary statements, and it is the only
  place in the class that records the split by direction.
- **"Correcting" the `CardCast` clause into a capability it does not have** — for
  example claiming `CardCast` now serves Pet Skill Cards — would assert a protocol
  change that `SIGNALR_PROTOCOL.md` §2 does not authorize. STOP.
- **Replacing the test with a positive `PetSkillCast` assertion.** It duplicates
  `BattleHubPetSkillCastTests`, adds no new boundary, and changes the test's
  purpose.
- **Deleting the test to "remove the stale claim".** It would drop the real §3
  direction guard. Not acceptable.
- **Adding an invented name to the list.** A replacement name must be a documented
  Server → Client delivery (§3/§4) or otherwise verified absent; inventing one
  creates a new false claim.
- **Substituting a registered method into the list to "make the probe fail
  properly."** That is exactly the defect being removed; a registered method must
  never be listed.
- **Treating the stale comment as evidence that the code does not exist.** The
  comment is false; the code, the tests, the protocol, and TASK-115 are the
  evidence.
- **Widening the fix.** Silently correcting the migration comments, the test method
  name, `SIGNALR_PROTOCOL.md` §4 item 7's §6 → §7 reference, or any other stale
  artifact discovered along the way violates `AGENTS.md` §16. Report instead.
- **A "correction" that requires the protocol to be reinterpreted** — e.g.
  deciding `PetSkillCast` should be merged into `CardCast` — is an architecture /
  protocol change, not a reconciliation. STOP per `AGENTS.md` §4/§18.

---

## Stop Conditions

Universal `AGENTS.md` §20 and `.ai/README.md` §13 stops always apply.
Task-specific:

- **If the current `PetSkillCast` implementation conflicts with the authoritative
  protocol at pickup** — method absent, signature unlike `SIGNALR_PROTOCOL.md` §2,
  payload unlike §3.2.21, ordering unlike §3.2.22, acknowledgement unlike §5, or
  behavior contradicting §2 item 3 — **STOP.** That is an implementation or
  contract bug, not a status reconciliation, and it must not be fixed here.
- **If the correct replacement boundary assertion cannot be determined from
  `SIGNALR_PROTOCOL.md` §2–§5/§7/§8: STOP and report the ambiguity.** Do not invent
  one, and do not fall back on deleting coverage.
- **If removing `PetSkillCast` from the list leaves the test with no genuine
  boundary: STOP and report** — do not leave a vacuous test, and do not delete the
  test to make the suite "correct".
- **If fixing the test would require a new architectural decision, a new test
  mechanism, a new test, or a new abstraction: STOP and report** instead of
  expanding the task.
- **If the artifacts can only be reconciled by changing the SignalR contract**
  (a method, parameter, payload, event, or ordering) — **STOP** per `AGENTS.md`
  §4/§18.
- **If reconciling would require gameplay changes, authentication or authorization
  changes, `BattleState`/Redis changes, or a `BattleStateService`/Domain/client
  edit: STOP.**
- **If authorization or authentication semantics turn out to be ambiguous or
  relevant at all: STOP and report** — this task does not reinterpret them.
- **If the stale artifacts turn out to be intentional** — for example a documented
  decision that `PetSkillCast` is out of scope, or a deliberate guard the comment's
  wording is meant to preserve — **STOP** per `AGENTS.md` §4 and report both
  sources (file + line, both sides) rather than picking the easier side.
- **If a premise of the Verified Contradiction no longer holds** (the test no
  longer passes, the hub method was removed, the test entry was already corrected,
  or a capability was reverted): **STOP and report** — the task's basis must be
  re-derived before editing.
- **If the correction cannot be confined to the two named files, the enumerated
  spans, and this task file: STOP and report** — decompose per `tasks/README.md`
  §13 instead of expanding this task.
- **If satisfying any criterion would require editing a completed task, any file
  under `docs/`, `BattleStateService.cs`, the Domain, the client, or another test:
  STOP and report.**
- **If satisfying any criterion would require touching the `BATTLE_NOT_FOUND`
  result presentation, the `SIGNALR_PROTOCOL.md` §4 item 7 cross-reference, or
  TASK-107's completion evidence: STOP** — those are separate follow-up concerns
  and must not be combined with this reconciliation.
- **If the repository's task-generation workflow does not permit this class of
  reconciliation under the assigned type/workflow: STOP and report** the conflict
  (`tasks/README.md` §8) rather than executing it under a type that does not fit.
- **If the task exceeds 7 skills or crosses multiple uncoupled architectural
  boundaries: STOP and decompose.**

---

## Completion Evidence

<!--
  TO BE COMPLETED BY THE EXECUTING AGENT after reaching DONE.
  Keep concise and factual. Record before/after text for both corrections.
  Do not alter this task's Objective, Scope, Acceptance Criteria, Stop
  Conditions, or Authoritative References while recording completion.
-->

### Changed Files

- `src/backend/GameServer.Api/Hubs/BattleHub.cs` — **the `PetSkillCast` clause of
  the class XML doc comment only** (pickup L489–491): three `///` lines replaced by
  three `///` lines. Zero executable lines, declarations, attributes, `using`
  directives, or signatures differ. `PetSkillCastResponse` and
  `PetSkillCast(battleId, clientSequence)` are byte-unchanged.
- `tests/backend/GameServer.Api.Tests/ApiIntegrationTests.cs` —
  `BattleHub_ShouldNotRegisterGameplayMethods` only: the stale `PetSkillCast`
  rationale (pickup L261–262) replaced in place by a not-in-this-list note
  (L261–263), and `"PetSkillCast"` removed from the absent-method array (L285).
  The `ReceiveEvents` rationale, the `JoinBattle` / `Swap` / `CardCast` /
  `GetBattleState` notes, and the assertion mechanism are byte-unchanged.
- `tasks/backlog/TASK-146-reconcile-petskillcast-implementation-status-artifacts.md`
  — `Status` and Completion Evidence only; no objective, scope, acceptance
  criterion, stop condition, or authoritative-reference text changed. Moved to
  `tasks/completed/` at DONE (`TASK_LIFECYCLE.md` §3, §4).

### Correction Record

```text
Artifact A — BattleHub.cs class comment (pickup L489–491)

  Before (verbatim):
    /// <c>CardCast</c> is implemented for Basic Cards (<c>CARD_RULES.md</c> §2, §3).
    /// <c>PetSkillCast</c> (§2) is a client → server method that is intentionally NOT
    /// implemented: it requires Pet Skill Card resolution, which is out of scope.

  After (verbatim):
    /// <c>CardCast</c> is implemented for Basic Cards (<c>CARD_RULES.md</c> §2, §3).
    /// <c>PetSkillCast</c> is implemented too, and its client → server method surface
    /// is owned by <c>SIGNALR_PROTOCOL.md</c> §2.

  Rationale: the removed clause contradicted the file's own
  `BattleHub.cs:895 public async Task<PetSkillCastResponse> PetSkillCast(...)`,
  `SIGNALR_PROTOCOL.md` §2 (which lists `PetSkillCast(battleId, clientSequence)`
  as a client → server method), and `ARCHITECTURE.md` §2.2.1 (synchronized by
  TASK-145 to state the capability is implemented). The replacement states the
  implementation status and cites §2 as the owner of the method surface. It
  restates no parameter, acknowledgement envelope, wire member, rejection code,
  or emission order, and it claims nothing about any other capability. The
  `Swap`/`CardCast` line, the `GetBattleState` paragraph (L493–498), the
  `ReceiveEvents` paragraph (L500–502), and the thin-transport framing are
  byte-unchanged.

Artifact B — ApiIntegrationTests.BattleHub_ShouldNotRegisterGameplayMethods

  Before (rationale, pickup L261–262, verbatim):
    // `PetSkillCast` is a §2 client → server method that is intentionally NOT
    // implemented, so a client invocation fails.

  After (rationale, L261–263, verbatim):
    // `PetSkillCast` is deliberately NOT in this list: §2's Pet Skill cast method
    // is implemented, so it is a real invokable hub method. Its contract is
    // covered by BattleHubPetSkillCastTests.

  Before (list, pickup L284, verbatim):
    foreach (var method in new[] { "PetSkillCast", "ReceiveEvents" })

  After (list, L285, verbatim):
    foreach (var method in new[] { "ReceiveEvents" })

  Coverage note: the corrected test still asserts one currently-true boundary —
  that `ReceiveEvents` (§3, a Server → Client delivery) is not an invokable hub
  method — through the unchanged zero-argument probe and the unchanged
  `ReceiveEvents` rationale. Verified empirically at pickup: for `ReceiveEvents`
  the server answers "HubException: Method does not exist." (genuine absence),
  while for `PetSkillCast` the same zero-argument probe produced a binding
  failure without that message, and the same name invoked with its two
  documented arguments bound successfully and returned the §5 acknowledgement
  (`accepted=False, reason=BATTLE_NOT_FOUND` for an unknown battle). The removed
  entry therefore never tested registration, and removing it removes no real
  coverage. The positive `PetSkillCast` contract remains covered, unmodified, by
  `BattleHubPetSkillCastTests` (3 facts: accepted + `BattleStateUpdated` /
  `ReceiveEvents` broadcast + §3.2.20/§3.2.21 wire order; `INSUFFICIENT_POWER`;
  `BATTLE_NOT_FOUND`).
```

### Validation Results

```text
Build                          — PASS. `dotnet build src/backend/GameServer.sln`
                                 → Build succeeded, 0 Error(s), 2 Warning(s). Both
                                 warnings are the pre-existing MSB3277
                                 EntityFrameworkCore.Relational version conflicts in
                                 GameServer.Api.Tests / GameServer.Infrastructure.Tests,
                                 present in the baseline run before this task's edits.
                                 No CS1570/CS1571/CS1573/CS1591 XML-doc warning: the
                                 edited `<summary>` is well-formed.
Integration tests              — PASS. `dotnet test src/backend/GameServer.sln`:
                                 2,606 tests, 0 failed, 0 skipped —
                                 GameServer.Domain.Tests 1463, Application.Tests 489,
                                 Infrastructure.Tests 363, Api.Tests 291.
Focused re-run                 — PASS. `BattleHub_ShouldNotRegisterGameplayMethods`
                                 1/1, passing for the intended reason: the only name
                                 it lists is `ReceiveEvents`, confirmed empirically to
                                 fail on absence ("Method does not exist"), not on arity.
Positive contract suite        — PASS. `BattleHubPetSkillCastTests` 3/3, unmodified.
Sibling absence probe          — PASS. `BattleHubReconnectRecoveryTests` 15/15,
                                 unmodified.
Related integration class      — PASS. `ApiIntegrationTests` 60/60.
                                 (60 + 3 + 15 = 78 — the union run of the four filters
                                 above was 78/78, consistent.)
Absence enumeration            — `ReceiveEvents` verified NOT a `BattleHub` method: it
                                 appears only as `Clients.Group(...).SendAsync(
                                 "ReceiveEvents", ...)` (L843, L884, L918) and as the
                                 `ReceiveEventsPayload` record; no method declaration
                                 carries that name. Every other registered hub method
                                 (`JoinBattle`, `GetBattleState`, `Swap`, `CardCast`,
                                 `PetSkillCast`, `Ping`) is therefore correctly absent
                                 from the list.
Diff-shape check               — PASS. Hash-revert proof: replacing only the two intended
                                 spans in each file with their pickup text reproduces the
                                 pickup SHA-256 exactly, so the current file equals pickup
                                 plus exactly those spans and nothing else.
                                 BattleHub.cs        D35A24975CB2789D546C68ABBC57C20E77D4DF6ABA290B8593A4A3EC3808B59C
                                                  → F485D8FC1862FE5724FADA6CD3472B1E6FFA39626D21065FBE2848E3E07733BF
                                 ApiIntegrationTests CAB67A3364F179222BD6B918D0E20F2D47D4B07C19195031508CB2631654505B
                                                  → BA2C492CF78CE71C957F85AC341CB5D2848014FC17935C8ED21E7269716D55C1
                                 Both files remain LF-only (0 CRLF), no BOM, zero U+FFFD;
                                 `§`, `→`, and the en-dash in `§3.2.20–§3.2.22` survive.
Half-fix / no-vacuity checks   — PASS. List entry and rationale agree: `PetSkillCast`
                                 is out of the list and the rationale says so, with no
                                 remaining "not implemented" claim about it in the test
                                 (zero matches for "intentionally NOT" in the method).
                                 The test is not deleted, not vacuous (one real
                                 boundary asserted), and not converted into a duplicate
                                 of the positive suite.
Cross-document agreement       — PASS. `SIGNALR_PROTOCOL.md` §2 (method surface), §3
                                 (Server → Client delivery), §3.2.20–§3.2.22 (wire
                                 members and emission order), §5 (acknowledgement),
                                 `ARCHITECTURE.md` §2.2.1, `GAME_EVENTS.md` §2,
                                 `CARD_RULES.md` §4/§6, the corrected comment, and the
                                 corrected test now agree about `PetSkillCast`'s status.
                                 None of those documents was edited.
Unmodified-guard check         — PASS (SHA-256 per-file manifest, pickup vs completion,
                                 excluding bin/obj):
                                   src/backend/**   211 files — only BattleHub.cs differs
                                   src/frontend/**   68 files — identical
                                   tests/**         146 files — only ApiIntegrationTests.cs differs
                                   docs/**           40 files — identical
                                   tasks/**         160 files — identical (all completed
                                                    tasks immutable); no file added or removed
Changed-file scope             — PASS. Exactly `BattleHub.cs` + `ApiIntegrationTests.cs`
                                 + this task file.
Docs validation                — PASS. Zero `docs/` files modified. The corrected prose
                                 cites `SIGNALR_PROTOCOL.md` §2 for the method surface —
                                 the document that owns it — and restates none of its
                                 content.
Gameplay scenarios             — N/A (core/validation.md §2; no rule derived, changed,
                                 or exercised).
Review checklist (quality/review.md §1)
  Correctness                  — PASS: the corrected artifacts now match
                                 `SIGNALR_PROTOCOL.md` §2/§3.
  Architecture                 — PASS: no layer, boundary, or dependency changed.
  Scope                        — PASS: two artifacts in two files; nothing refactored,
                                 added, or "improved"; no new test, helper, or project.
  Tests                        — PASS: MEDIUM depth satisfied (build + integration +
                                 documentation validation); the boundary guard is intact.
  Documentation                — PASS: no `docs/` change is required (TASK-145 had
                                 already synchronized the owning documents).
  Security                     — PASS: no authentication/authorization line touched or
                                 reinterpreted.
  Performance                  — PASS: no code path changed.
  Maintainability              — PASS: comment text and one array literal only; no
                                 abstraction, reflection inventory, or helper added
                                 (AGENTS.md §9).
  Determinism                  — N/A: no gameplay or battle logic touched; server
                                 authority unchanged.
```

### Server Authority & Scope Verification

- [x] Confirmed zero client-authoritative gameplay logic introduced
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1) — no new system or content

### Items Reported, Not Fixed (`AGENTS.md` §16)

- `src/backend/GameServer.Api/Hubs/BattleHub.cs:1078` — **premise delta.**
  `public Task<PingResponse> Ping(string? clientSequence = null)` is a sixth
  registered, client-invokable hub method (used by
  `ApiIntegrationTests.cs:142` and `ApplicationSessionSignalRTests.cs:271`), while
  this task's Current State table enumerated the hub's methods as five and
  `SIGNALR_PROTOCOL.md` §2 does not mention `Ping` anywhere in `docs/` (zero
  matches for "Ping" under `docs/`). Impact: the task file's hub-method count is
  one short, and an undocumented hub method exists on the protocol surface.
  Neither falsifies this task's premises nor affects the correction — `Ping` is a
  real hub method, so its absence from the absent-method list is correct, and
  `ReceiveEvents` is still genuinely not a hub method. Reported only; no
  documentation or runtime change made. Suggested follow-up: a task reconciling
  `Ping` against `SIGNALR_PROTOCOL.md` §2 / §8 (it is described in source as a
  "bootstrap-era connectivity probe").
- `src/backend/GameServer.Infrastructure/Postgres/Migrations/20261001112446_StructureCardDefinitionEffectDefinition.cs`
  ~L214–215 and ~L238–239 — present-tense claims that `PetSkillCast` "remains
  blocked", superseded by TASK-110/TASK-115. Historical migration comments;
  out of scope by this task's Out of Scope list; still present, not edited.
- `tests/backend/GameServer.Api.Tests/ApiIntegrationTests.cs:285` — the test
  method name `BattleHub_ShouldNotRegisterGameplayMethods` is now looser still
  (it asserts one non-method delivery), but a method name is not a false
  assertion, and renaming it is explicitly out of scope for this task.
  Not changed.
- `docs/02-technical/SIGNALR_PROTOCOL.md` §4 item 7 — refers to `GetBattleState`
  as "§6" while the method is documented under §7. Separately tracked, explicitly
  out of scope, not fixed, no task created.
- `tasks/completed/TASK-107-implement-basic-cardcast-server-path.md` — declares
  `Status: COMPLETED` with an unfilled `## Completion Evidence` block. Completed
  tasks are immutable (`TASK_LIFECYCLE.md` §3); reported, not edited.
- `ApiIntegrationTests.cs:951–954` (`PassiveCharged` / `PassiveTriggered` /
  `PetStateUpdated`) and
  `BattleHubReconnectRecoveryTests.cs:641–675` — re-verified **not defective**:
  every name in both probes is absent as a hub method (none is declared in
  `BattleHub.cs`), so each fails on absence rather than on argument arity.
  Unchanged, as the task requires.

### Lifecycle

```text
Execution:  direct pickup from tasks/backlog/. No separate active/ file was
            materialized because pickup, implementation, validation, and
            completion happened in one session. Gates passed in order:
            BACKLOG → READY (docs + scope confirmed) → IN PROGRESS →
            IN REVIEW → DONE.
File move:  tasks/backlog/ → tasks/completed/ (TASK_LIFECYCLE.md §3 DONE
            "File location: tasks/completed/", §4 terminal move).
Scope text: unchanged — no objective, scope, acceptance-criterion,
            stop-condition, or authoritative-reference text was altered while
            recording completion; only `Status` and this Completion Evidence
            block were written, plus the pre-existing acceptance/affected-file
            checkboxes.
Working tree: carried 152 pre-existing modified/untracked entries at pickup.
            Nothing was reset, stashed, reverted, or cleaned, and no
            pre-existing change was touched.
```
