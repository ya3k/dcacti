# TASK-100 — Synchronize Stale Scene-Staging and Client-Implementation-Status Wording

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ by path and section — it does NOT copy game rules,
  formulas, schemas, or contracts.

  THIS TASK AUTHORS NO CONTRACT AND IMPLEMENTS NO BEHAVIOR. It corrects two
  enumerated documentation statements that completed implementation work has
  made false. Every contract it touches stays identical in meaning.

  BOUNDARY: docs/ only. Zero files under src/ or tests/.

  PROVENANCE: both defects were discovered during the post-TASK-098
  repository reconciliation (a read-only planning pass) and were verified
  against source before this task was written. Neither was fixed inline
  (AGENTS.md §16).

  THIS TASK IS NOT THE D1 / LOADOUT-SELECTION-SURFACE DECISION. TASK-080
  already resolved that (D1 = Phaser/LobbyScene) and already recorded it in
  TDD.md §2.1; TASK-099 attempted to re-record it and is INVALID for that
  reason. TASK-099 is NOT modified, NOT resolved, and NOT referenced as an
  authority here. This task corrects implementation-status wording ONLY.
-->

---

## Metadata

```text
Task ID:           TASK-100
Type:              DOCUMENTATION (TASK_TYPES.md §2 — "Change docs/ content —
                   documentation is the primary output, not code"; workflow
                   documentation/documentation-change.md). The output is
                   docs/02-technical/TDD.md and docs/02-technical/
                   ARCHITECTURE.md. See "Type classification note" below.
Status:            DONE
Risk:              LOW—MEDIUM (TASK_TYPES.md §4 — DOCUMENTATION baseline
                   LOW—MEDIUM; MEDIUM because §2.2.1's sentence is
                   cross-referenced by the client/real-time boundary sections.
                   LOW in that it changes no code, no API contract, no
                   protocol, no state, and no gameplay rule.)
Priority:          MEDIUM (each stale statement would mislead an agent into
                   believing implemented scenes or an implemented action do
                   not exist — the duplicate-work failure mode TASK-097/
                   TASK-098 addressed. Neither blocks an implementation task.)
Primary Agent:     review (documentation consistency — TASK_TYPES.md §2
                   DOCUMENTATION "Primary Agent: Review Agent")
Supporting Agents: client (the client implementation being described is
                   client-layer)
Workflow:          documentation/documentation-change.md
Skills:            discovery/documentation-discovery,
                   quality/documentation-consistency,
                   quality/scope-validation
                   (3 skills — Simple budget, tasks/README.md §12)
Dependencies:      TASK-087 (DONE — implemented ResultScene; the work that
                     made defect 1's clause false),
                   TASK-090 (DONE — implemented MainMenuScene and changed the
                     implemented transition order; same),
                   TASK-069 (DONE — implemented the client Swap action path;
                     the work that made defect 2's clause false),
                   TASK-078 (DONE — recorded the original staging decision;
                     context only, NOT modified)
Blocks:            Nothing. This task corrects documentation-only staleness;
                   it gates no implementation task.
Estimate:          Simple (two single-clause corrections in two documents;
                   no new prose section, no code, no test)
```

**Type classification note.** `DOCUMENTATION`, not `BUG` and not `REFACTOR`.
`TASK_TYPES.md` §2 defines `DOCUMENTATION` as *"A document in `docs/` needs to be
created, updated, or corrected, and no code change is required"* — which is the
whole of this task's output. Both edited artifacts are files under `docs/`.

It is **not** `BUG`: `TASK_TYPES.md` §2's BUG definition does include
"documentation bugs (docs are stale)", but its workflow
(`development/bug-fix.md` §2) requires a regression test that fails before the
fix. There is no executable behavior to fix and no test can fail on a
documentation sentence, so the BUG workflow cannot be honestly discharged here.
This mirrors the reasoning TASK-097 (a `DOCUMENTATION` task correcting stale
`docs/` status wording) already established.

It is **not** `REFACTOR`: no source file is edited, so
`development/refactor.md`'s code-preservation checks have no subject.

**No ADR is created by this task, and none is required.**
`architecture/adr-change.md` §1 and `docs/03-decisions/README.md` §1 state that
an ADR records a decision that has **already been made** and is reflected in the
existing documentation; it is a historical record, not a design proposal. This
task records **no decision** — it corrects two sentences that misdescribe
already-DONE implementation work. The architectural decisions involved are
already owned: `ADR-003` (Phaser scene lifecycle) is unchanged and is not
reopened, and `ADR-008` (reconnect/resync) is unchanged. Creating `ADR-017`
here would restate existing technical documentation, which
`architecture/adr-change.md` §1 ("Decision vs. Implementation") forbids.

---

## Objective

Correct the two enumerated stale statements in `docs/02-technical/` so they
describe the client architecture the repository has already implemented and
DONE tasks already recorded — while changing no code, no contract, no API,
no protocol, no state, and no gameplay rule.

```text
Defect 1  docs/02-technical/TDD.md          §2.1 Phaser Scene Lifecycle
          the MVP staging note still says `MainMenuScene` and `ResultScene`
          "are deferred to their own tasks" — both are implemented.

Defect 2  docs/02-technical/ARCHITECTURE.md §2.2.1 Game Runtime Coordination
          the implementation-status sentence still says `Swap` is
          "not implemented yet" — it is implemented end-to-end.
```

Both are documentation bugs, not implementation bugs: the code matches the
intent recorded as DONE by TASK-069/TASK-087/TASK-090, so the documents are the
stale side (`AGENTS.md` §17).

---

## Authoritative References

- `docs/02-technical/TDD.md` §2.1 — the **canonical owner** of the Phaser scene
  lifecycle and the Phaser/React split; contains defect 1's note (L103–111) and
  the lifecycle diagram (L89–101) the note refers to
- `docs/02-technical/ARCHITECTURE.md` §2.2.1 — the runtime coordination
  boundary and its rules 1–6; contains defect 2 (L231–236)
- `docs/02-technical/ARCHITECTURE.md` §1 (L74–79) — the client scene tree the
  corrected wording must stay consistent with
- `docs/02-technical/ARCHITECTURE.md` §2.2 rule 3, §2.2.3 — the transport
  isolation and pre-battle selection boundaries that §2.2.1 supports; cited for
  consistency, **not edited**
- `docs/02-technical/SIGNALR_PROTOCOL.md` §2 — the client → server gameplay
  method surface (`Swap`, `CardCast`, `PetSkillCast`); the document defect 2's
  sentence already cites, and the owner of which methods exist
- `docs/02-technical/SIGNALR_PROTOCOL.md` §7, `ADR-008` — reconnect/resync
  snapshot recovery; still unimplemented and must stay described as such
- `docs/03-decisions/ADR/ADR-003-phaser-game-runtime.md` — Phaser scene
  lifecycle rationale; unchanged, not reopened
- `tasks/completed/TASK-090-implement-main-menu-scene.md` — the DONE work that
  falsified defect 1's "deferred" claim (read-only; **not modified**)
- `tasks/completed/TASK-087-client-battle-outcome-result-scene.md` — the DONE
  work that falsified defect 1's other half (read-only; **not modified**)
- `tasks/completed/TASK-069-implement-client-swap-action-path.md` — the DONE
  work that falsified defect 2 (read-only; **not modified**)
- `tasks/completed/TASK-097-synchronize-statuseffect-and-provisioning-implementation-status.md`
  — the precedent for a `DOCUMENTATION` task correcting stale
  implementation-status wording (read-only; **not modified**)
- `AGENTS.md` §16 (report, do not fix inline), §17 (documentation change rule),
  §4 (conflict resolution), §9 (anti-overengineering)
- `.ai/workflow/documentation/documentation-change.md` §1–§3 — the governing
  workflow and the "one concept, one owner" / no-duplication rule

---

## Exact Stale Claims and Verified Current Reality

### Defect 1 — `TDD.md` §2.1, the MVP staging note (L103–111)

**Stale claim (verbatim, L103–108):**

```text
> **MVP staging note (recorded per TASK-078's Product Owner decision).** The
> MVP implementation stages this lifecycle as `BootScene → PreloaderScene →
> LobbyScene → BattleScene`: `MainMenuScene` and `ResultScene` are deferred
> to their own tasks, so the implemented transition order passes from
> `PreloaderScene` directly into `LobbyScene` for now, and `LobbyScene`
> transitions to `BattleScene` after a successful battle start.
```

**The false part:** *"`MainMenuScene` and `ResultScene` are deferred to their own
tasks, so the implemented transition order passes from `PreloaderScene` directly
into `LobbyScene` for now"*.

**Verified current reality:**

| Claim in the note | Verified state | Evidence |
|---|---|---|
| `MainMenuScene` deferred | **Implemented** | `src/frontend/client/src/game/scenes/MainMenuScene.ts` exists (`export class MainMenuScene extends Phaser.Scene`, L11) |
| `ResultScene` deferred | **Implemented** | `src/frontend/client/src/game/scenes/ResultScene.ts` exists (`export class ResultScene extends Phaser.Scene`, L32) |
| Implemented order skips `MainMenuScene` | **False — it is included** | `GameConfig.ts:64` — `scene: [BootScene, PreloaderScene, MainMenuScene, LobbyScene, BattleScene, ResultScene]` |
| `PreloaderScene` goes directly to `LobbyScene` | **False** | `PreloaderScene.ts:83` — `this.scene.start('MainMenuScene')`; `MainMenuScene.ts:80` — `this.scene.start('LobbyScene')` |
| `LobbyScene` → `BattleScene` | **True** | `LobbyScene.ts:397` — `this.scene.start('BattleScene')` |

**Provenance:** TASK-090 (DONE, FEATURE) implemented `MainMenuScene`, changed
`PreloaderScene`'s transition target, and registered it in `GameConfig.ts`.
TASK-087 (DONE, FEATURE) implemented `ResultScene`. **Neither task's Changed
Files list includes any `docs/` file** — the note was left behind when the
deferred scenes landed.

**Still accurate in the same note (must be preserved):** the note's remaining
content — that the lifecycle diagram above it remains the design this document
specifies, and that the staging was an implementation-order decision recorded so
it is not left implicit. It must read as a record of staging **that has since
completed**, not as a standing statement that the scenes are absent.

### Defect 2 — `ARCHITECTURE.md` §2.2.1 (L231–236)

**Stale claim (verbatim, L231–233):**

```text
Battle resolution, the client → server gameplay methods (`Swap`, `CardCast`,
`PetSkillCast` — `SIGNALR_PROTOCOL.md` §2), and reconnect/resync snapshot
recovery (`SIGNALR_PROTOCOL.md` §7, ADR-008) are not implemented yet.
```

**The false part:** grouping `Swap` with `CardCast`/`PetSkillCast` as
"not implemented yet".

**Verified current reality — the three claims must be separated:**

| Claim | Verified state | Evidence |
|---|---|---|
| `Swap` not implemented | **False — implemented end-to-end** | Server: `BattleHub.cs:567` `public async Task<SwapResponse> Swap(` delegating to `BattleStateService.ExecuteSwapAsync` (`BattleStateService.cs:712`) → Domain `SwapExecution.cs`. Client: `GameRuntime.ts:358` `requestAction`, `RUNTIME_ACTION_SWAP` (`GameRuntimeEvents.ts:342`). Delivered by TASK-069. |
| `CardCast` not implemented | **True** | No hub method; `BattleHub.cs:389` states `CardCast`/`PetSkillCast`/`GetBattleState` are not implemented |
| `PetSkillCast` not implemented | **True** | Same |
| Reconnect/resync snapshot recovery not implemented | **True** | `GetBattleState` has **no** hub method (`BattleHub.cs` exposes only `JoinBattle` L496, `Swap` L567, `Ping` L752) |
| Battle resolution not implemented | **True** | The full `GAME_RULES.md` §17 pipeline beyond Swap is not implemented |

**Provenance:** TASK-069 (DONE, FEATURE) implemented the client Swap action path
against the already-implemented server path. Its Changed Files list includes no
`docs/` file.

**Correction boundary:** only the `Swap` part is false. The sentence must
continue to state correctly that `CardCast`, `PetSkillCast`, and
reconnect/resync snapshot recovery are **not** implemented.

---

## Scope

### In Scope

1. **Correct `TDD.md` §2.1's staging note (L103–111)** so it no longer states
   that `MainMenuScene` and `ResultScene` are deferred, and no longer states
   that the implemented order passes from `PreloaderScene` directly into
   `LobbyScene`. It must agree with the registered order in `GameConfig.ts:64`.
   Keep the note's accurate remainder (the diagram above remains the specified
   design; the staging was recorded so it is not left implicit). **Single-clause
   correction within the note, not a §2.1 rewrite.**
2. **Correct `ARCHITECTURE.md` §2.2.1's implementation-status sentence
   (L231–233)** so it no longer claims `Swap` is unimplemented, **while
   preserving** the correct statement that `CardCast`, `PetSkillCast`, and
   reconnect/resync snapshot recovery (`SIGNALR_PROTOCOL.md` §7, ADR-008) are
   not implemented yet. Keep the existing citations to `SIGNALR_PROTOCOL.md`
   §2 and §7.
3. **Verify the surrounding statements remain consistent** — in particular
   `TDD.md` §2.1's lifecycle diagram (L89–101) and scene bullets (L113–132), and
   `ARCHITECTURE.md` §1's client tree (L74–79). **These are expected to require
   no change**; edit one only if it is *factually wrong* against the verified
   reality above, and then only minimally and with the reason recorded.
4. **Update each edited document's `**Version:**` line** to record this
   synchronization, following the convention the two files already use (both
   currently record their prior change: `TDD.md` L3 "Version 1.1 (§2.1
   scene-lifecycle MVP staging note added per TASK-078…)", `ARCHITECTURE.md` L3
   "Version 1.1 (§2.2.3 added per TASK-081…)").

### Out of Scope

- **Any source code** — nothing under `src/`. No `GameConfig.ts`,
  `PreloaderScene.ts`, `MainMenuScene.ts`, `ResultScene.ts`, `BattleHub.cs`,
  `GameRuntime.ts`, or any other file.
- **Any test change** — nothing under `tests/`.
- **Any TypeScript or C#** — no interface, signature, member, or payload.
- **Implementing `CardCast`, `PetSkillCast`, or `GetBattleState`.** This task
  documents that they are unimplemented; it does not implement them.
- **Implementing `MainMenuScene` post-battle navigation or `ResultScene`'s
  `RewardSummary`** — both remain genuinely deferred and must stay described as
  such.
- **Re-deciding the D1 / loadout selection surface question.** Resolved by
  TASK-080 and already recorded in `TDD.md` §2.1 L116–129. **Not reopened, not
  re-recorded, not restated.**
- **Resolving or modifying TASK-079** (`tasks/blocked/`) — read-only guard file.
- **Resolving, modifying, or referencing TASK-099** as an authority. TASK-099 is
  INVALID (its D1 objective was already discharged by TASK-080) and is **left
  byte-unchanged by this task**.
- **Modifying TASK-080, TASK-081, TASK-087, TASK-090, TASK-069, TASK-097,
  TASK-098, or any other completed task** (`TASK_LIFECYCLE.md` §3).
- **Creating any ADR** — see the Metadata classification note.
- **Editing `ARCHITECTURE.md` §2.2.3, §2.2.1 rules 1–6, or §5** — their
  boundaries are correct; only the L231–233 status sentence is in scope.
- **Editing `API_CONTRACTS.md`, `SIGNALR_PROTOCOL.md`, `REDIS_STATE.md`,
  `DATABASE.md`, `GAME_STATE.md`, `GAME_EVENTS.md`, or any `docs/01-game-design/`
  rule document.**
- **Editing `docs/00-overview/`** (`GDD.md`, `MVP_SCOPE.md`, `ROADMAP.md`).
- **Sweeping for other stale statements.** Anything discovered beyond the two
  enumerated defects is **reported, not fixed** (`AGENTS.md` §16).
- **Changing scene registration, scene order, or scene behavior** in any way —
  this task corrects the *description* of an already-implemented order.
- **Any gameplay, Match-3, Combat, Boss, Pet, Card, Relic, XP, or reward
  change.**
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2.

---

## Acceptance Criteria

- [x] `docs/02-technical/TDD.md` §2.1 no longer states that `MainMenuScene` is
      deferred.
- [x] `docs/02-technical/TDD.md` §2.1 no longer states that `ResultScene` is
      deferred.
- [x] `docs/02-technical/TDD.md` §2.1 no longer states that the implemented
      transition order passes from `PreloaderScene` directly into `LobbyScene`.
- [x] `docs/02-technical/TDD.md` §2.1's description of the implemented
      transition order matches `GameConfig.ts:64`'s registered order exactly.
- [x] `docs/02-technical/TDD.md` §2.1's lifecycle diagram (L89–101: Boot →
      Preloader → MainMenu → Lobby → Battle → Result) is **byte-unchanged**.
- [x] `docs/02-technical/TDD.md` §2.1's staging note retains its accurate
      content: that the lifecycle diagram remains the design the document
      specifies, and that the staging was an implementation-order decision
      recorded so it is not left implicit.
- [x] `docs/02-technical/TDD.md` §2.1's LobbyScene bullet (L116–129) is
      **byte-unchanged** — it already correctly records the TASK-080 D1
      decision and is not this task's subject.
- [x] `docs/02-technical/ARCHITECTURE.md` §2.2.1 no longer states that `Swap`
      is not implemented.
- [x] `docs/02-technical/ARCHITECTURE.md` §2.2.1 **still** states that
      `CardCast` is not implemented.
- [x] `docs/02-technical/ARCHITECTURE.md` §2.2.1 **still** states that
      `PetSkillCast` is not implemented.
- [x] `docs/02-technical/ARCHITECTURE.md` §2.2.1 **still** states that
      reconnect/resync snapshot recovery (`SIGNALR_PROTOCOL.md` §7, ADR-008) is
      not implemented.
- [x] `docs/02-technical/ARCHITECTURE.md` §2.2.1 still cites
      `SIGNALR_PROTOCOL.md` §2 and §7 for the method surface and recovery
      mechanism respectively.
- [x] `docs/02-technical/ARCHITECTURE.md` §2.2.1 rules 1–6 are byte-unchanged.
- [x] `docs/02-technical/ARCHITECTURE.md` §2.2.3 is byte-unchanged.
- [x] Each edited document's `**Version:**` line records this synchronization,
      in the convention that document already uses.
- [x] No statement in either edited document claims an unimplemented capability
      exists, or that an implemented one does not.
- [x] No new duplicated definition is introduced: no rule, schema, payload, or
      contract is restated in either document
      (`documentation-change.md` §2).
- [x] `docs/02-technical/TDD.md` requires changes in **exactly one** location
      (the §2.1 staging note clause) plus its Version line.
- [x] `docs/02-technical/ARCHITECTURE.md` requires changes in **exactly one**
      location (§2.2.1's L231–233 sentence) plus its Version line.
- [x] No ADR is created; `docs/03-decisions/` is unmodified.
- [x] `docs/00-overview/` is unmodified (all three files, verified by hash).
- [x] `docs/01-game-design/` is unmodified.
- [x] `docs/02-technical/API_CONTRACTS.md`, `SIGNALR_PROTOCOL.md`,
      `REDIS_STATE.md`, `DATABASE.md`, `GAME_STATE.md`, `GAME_EVENTS.md` are
      unmodified (verified by hash).
- [x] Zero files under `src/` are modified.
- [x] Zero files under `tests/` are modified.
- [x] `tasks/backlog/TASK-099-*.md` is **byte-identical** before and after
      (SHA-256 re-verified).
- [x] `tasks/blocked/TASK-079-*.md` is **byte-identical** before and after
      (SHA-256 re-verified).
- [x] All `tasks/completed/` files are unmodified.
- [x] The changed-file set equals exactly: the two `docs/02-technical/` files
      plus this task file.
- [x] No API endpoint, request, response, or contract is changed or added.
- [x] No SignalR method, payload, or event is changed or added.
- [x] No Redis key, field, record, or behavior is changed or added.
- [x] No PostgreSQL schema, entity, migration, or seed is changed or added.
- [x] No gameplay rule, formula, balance value, or timing is changed.
- [x] Documentation consistency checks pass (per Testing/Documentation
      Consistency Requirements below).
- [x] Required review checks pass (`quality/review.md` §1), skipping
      code-behavior items a documentation-only change cannot exercise.
- [x] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001).

### Explicit Constraints

```text
No gameplay.
No source code.
No test changes.
No architecture redesign.
No new ADR.
No speculative architecture.
No client-authoritative state.
No undocumented API.
No undocumented SignalR method.
No undocumented Redis behavior.
No speculative PostgreSQL BattleState persistence.
TASK-099 and all completed tasks are NOT modified.
```

---

## Affected Files

```text
[x] docs/02-technical/TDD.md          (defect 1 — §2.1 staging note clause
                                       only, plus the Version line)
[x] docs/02-technical/ARCHITECTURE.md (defect 2 — §2.2.1 L231–233 sentence
                                       only, plus the Version line)
[ ] docs/02-technical/ARCHITECTURE.md §1 tree, §2.2.1 rules 1–6, §2.2.3, §5
                                      (verify; expected NO CHANGE)
[ ] docs/02-technical/API_CONTRACTS.md, SIGNALR_PROTOCOL.md, REDIS_STATE.md,
    DATABASE.md, GAME_STATE.md, GAME_EVENTS.md   (NO CHANGE)
[ ] docs/00-overview/  (NO CHANGE)
[ ] docs/01-game-design/  (NO CHANGE)
[ ] docs/03-decisions/  (NO CHANGE — no ADR)
[ ] src/  (none)
[ ] tests/  (none)
[x] tasks/backlog/TASK-100-*.md       (this file — Status and Completion
                                       Evidence only)
[ ] tasks/backlog/TASK-099-*.md       (NO CHANGE)
[ ] tasks/blocked/TASK-079-*.md       (NO CHANGE)
[ ] tasks/completed/  (NO CHANGE)
```

---

## Implementation Notes

- **Verified reality is the authority, not this task's prose.** Re-verify each
  claim against the source at pickup. If the code differs from what this task
  documents, **the code wins** and the difference is reported.
- **Correction is surgical, not a rewrite.** In `TDD.md` the false part is one
  clause inside a paragraph; in `ARCHITECTURE.md` it is one sentence. Rewriting
  either section wholesale risks dropping correct statements and violates
  `AGENTS.md` §16.
- **Do not split `Swap` from its neighbours by deletion.** `ARCHITECTURE.md`
  L231–233 groups four things; exactly one is now false. The corrected sentence
  must still name `CardCast`, `PetSkillCast`, and reconnect/resync recovery as
  unimplemented — deleting the whole sentence would make the document claim
  those exist.
- **"Battle resolution … not implemented" is TRUE and must survive.** `Swap`
  resolves one action end-to-end, but the full `GAME_RULES.md` §17 pipeline and
  the reconnect path are not built. Do not over-correct into claiming the battle
  system is complete.
- **Cite, do not restate.** Both documents already cite their owners
  (`SIGNALR_PROTOCOL.md` §2/§7; `ADR-008`; `ARCHITECTURE.md` §2.2.3). Keep the
  citation idiom; do not copy a contract.
- **`TDD.md` §2.1's LobbyScene bullet is not yours to touch.** L116–129 already
  correctly records the TASK-080 surface decision. Leave it byte-identical.
  The D1 question is closed and this task does not reopen it.
- **The staging note's accurate half is a trap for over-editing.** Its purpose
  was to record that the design (the diagram) was being staged for
  implementation-order reasons. Once the scenes exist, that record is
  *historical*. Prefer wording that reads as completed staging over wording that
  presents the bypass as current.
- **Do not let the correction imply other deferrals have landed.**
  `MainMenuScene` has no documented post-battle return navigation, and
  `ResultScene`'s `RewardSummary` member list is still deferred
  (`API_CONTRACTS.md` §4 note 1, `DATABASE.md` §1). Neither is in scope; do not
  state or imply otherwise.
- **`ARCHITECTURE.md`'s own tree needs a check, not an edit.** §1 (L74–79)
  already lists `MainMenuScene.ts`, `LobbyScene.ts`, and `ResultScene.ts`, so it
  is consistent with reality. Confirm; do not "improve" it.
- **Version lines follow their own established convention.** Extend the existing
  parenthetical; do not renumber the document or restructure the header.
- **Do not sweep.** `TASK_LIFECYCLE.md`, `tasks/README.md`, `AGENTS.md`, and
  other documents contain unrelated stale cross-references. Those are out of
  scope — report them in Completion Evidence (`AGENTS.md` §16).
- **Encoding safety.** Write these UTF-8 markdown files with a
  UTF-8-preserving writer. TASK-098's Process Note records typographic damage
  from a `Get-Content`/`Set-Content` round-trip; do not repeat it. Verify no
  BOM, no U+FFFD, and that em-dashes, en-dashes, arrows, and the §/→ characters
  survive.
- **Guard files.** Record SHA-256 for `tasks/backlog/TASK-099-*.md` and
  `tasks/blocked/TASK-079-*.md` at pickup and re-verify at completion. Do not
  edit or move either.

---

## Testing / Documentation Consistency Requirements

This is a documentation-only change. Validation exists to prove **the absence of
a code or contract change** and **the consistency of the corrected prose with
the implemented client** — not to exercise behavior. Per `core/validation.md` §2
a LOW—MEDIUM `DOCUMENTATION` task takes documentation validation plus review;
build/unit/integration/gameplay layers have no subject here and are explicitly
N/A.

### Required Verification

```text
[ ] Code-agreement check — every corrected statement verified against the
                    source it describes, at pickup:
                      defect 1  GameConfig.ts:64 (registered scene order),
                                PreloaderScene.ts:83 (→ MainMenuScene),
                                MainMenuScene.ts:80 (→ LobbyScene),
                                LobbyScene.ts:397 (→ BattleScene),
                                MainMenuScene.ts + ResultScene.ts exist
                      defect 2  BattleHub.cs:567 (Swap) and :496/:752
                                (JoinBattle/Ping only),
                                BattleStateService.cs:712 (ExecuteSwapAsync),
                                GameRuntime.ts:358 + GameRuntimeEvents.ts:342
                                (client Swap),
                                BattleHub.cs:389 (CardCast/PetSkillCast/
                                GetBattleState not implemented),
                                no GetBattleState hub method exists
[ ] Documentation consistency — re-read each edited passage together with the
                    documents and sections it cites (SIGNALR_PROTOCOL.md §2,
                    §7; ADR-008; ADR-003; ARCHITECTURE.md §1, §2.2, §2.2.3;
                    TDD.md §2.1) and confirm no contradiction survives and no
                    duplicated definition was introduced
                    (documentation-change.md §2/§3).
[ ] Stale-claim sweep — search both edited documents for the corrected claims
                    ("deferred", "not implemented yet") and confirm zero
                    residual false statement about the same fact. Confirm
                    `Swap` no longer appears in any "not implemented" claim.
[ ] Mixed-sentence check — confirm ARCHITECTURE.md §2.2.1 still lists
                    `CardCast`, `PetSkillCast`, and reconnect/resync recovery
                    as unimplemented, and that no sentence claims the full
                    GAME_RULES.md §17 pipeline exists.
[ ] Cross-document agreement — confirm TDD.md §2.1's stated order and
                    ARCHITECTURE.md §1's scene tree agree with each other and
                    with GameConfig.ts:64.
[ ] Unmodified-guard verification — hash comparison, pickup vs. completion:
                    docs/00-overview/GDD.md, MVP_SCOPE.md, ROADMAP.md
                    docs/02-technical/API_CONTRACTS.md, SIGNALR_PROTOCOL.md,
                      REDIS_STATE.md, DATABASE.md, GAME_STATE.md, GAME_EVENTS.md
                    docs/01-game-design/** (all)
                    docs/03-decisions/** (all — proves no ADR was created)
                    tasks/backlog/TASK-099-*.md
                    tasks/blocked/TASK-079-*.md
                    tasks/completed/** (all)
[ ] Changed-file scope — `git status` shows exactly the two
                    docs/02-technical/ files plus this task file; zero `src/`
                    and zero `tests/` changes.
[ ] Docs-render check — edited files re-read in full for intact code fences,
                    intact blockquote rendering, unbroken section numbering,
                    and unbroken cross-references.
[ ] Integration tests    — N/A: no boundary is implemented or altered.
[ ] Gameplay scenarios   — N/A: no gameplay rule is derived, changed, or
                    exercised (AGENTS.md §6 maps gameplay scenarios to rule
                    docs; none is touched).
```

### Key Edge Cases

- **One sentence, four claims, one false.** A whole-sentence deletion is the
  most likely failure and would falsely declare `CardCast`, `PetSkillCast`, and
  reconnect recovery implemented.
- **`Swap` looks unimplemented if read as "the battle system is unfinished".**
  Judge the sentence as written, against `BattleHub.cs:567`.
- **The staging note has a true half and a false half.** Deleting the note
  wholesale drops a correct record of why the design was staged; leaving it
  untouched keeps a false claim about the implemented order.
- **`ResultScene` exists but its `RewardSummary` is still deferred.** A careless
  edit could imply the deferred reward work landed with the scene.
- **`MainMenuScene` exists but post-battle navigation does not.** Same trap.
- **Over-correction toward "everything is implemented".** The full resolution
  pipeline, `CardCast`, `PetSkillCast`, `GetBattleState`, and reconnect/resync
  are genuinely unimplemented.
- **Duplication pull.** Because `ARCHITECTURE.md` §2.2.3 and `TDD.md` §2.1
  already describe the client boundary, there is a pull to restate it. Do not —
  cite it.
- **Scope pull toward the D1 decision / TASK-099.** That question is closed; it
  is not this task's subject, and TASK-099 is not an authority.
- **Scope pull toward a typo/consistency sweep of the whole document.** Report;
  do not fix (`AGENTS.md` §16).
- **A "correction" that turns out to need a contract reinterpreted rather than
  a status restated** — STOP per `AGENTS.md` §4.

---

## Stop Conditions

Universal `AGENTS.md` §20 stops always apply. Task-specific:

- **If either enumerated defect is no longer present at pickup: STOP and
  report** for that defect. Do not manufacture a correction, and do not convert
  this task into a different cleanup task.
- **If the verified reality has changed** (e.g. `GetBattleState` or `CardCast`
  has since been implemented, or a scene has been removed): **STOP and report** —
  the task's premises must be re-derived before editing.
- **If `Swap` turns out not to be implemented at pickup: STOP** — defect 2 would
  be invalid, and the sentence would be correct as written.
- **If correcting either statement would require reinterpreting a contract
  rather than restating an implementation-status fact: STOP per `AGENTS.md` §4.**
- **If a correction would require changing source code, a test, an API contract,
  the SignalR protocol, Redis behavior, or a database schema: STOP** — this task
  is documentation-only.
- **If satisfying any criterion requires modifying TASK-099, TASK-079, TASK-080,
  TASK-081, or any completed task: STOP** — report instead (`AGENTS.md` §16,
  `TASK_LIFECYCLE.md` §3).
- **If satisfying any criterion requires re-deciding the D1 loadout-selection
  surface: STOP** — it was resolved by TASK-080 and is out of scope.
- **If an ADR appears to be required: STOP and report** rather than creating one
  — per the Metadata classification note, this task records no decision.
- **If the two corrections cannot be kept to the enumerated clauses without a
  broader §2.1 or §2.2.1 rewrite: STOP and report** — decompose per
  `tasks/README.md` §13 instead of expanding this task.
- **If a third stale statement is discovered that cannot be reported without
  fixing: STOP** — report it and leave it (`AGENTS.md` §16).
- **If a cross-document conflict is found that no ownership rule resolves: STOP**
  per `AGENTS.md` §4.
- **If the task exceeds 7 skills or crosses multiple uncoupled boundaries: STOP
  & decompose.**

---

## Completion Evidence

<!--
  TO BE COMPLETED BY THE EXECUTING AGENT after reaching DONE.
  Keep concise and factual.
-->

### Changed Files

- `docs/02-technical/TDD.md` — §2.1 MVP staging note corrected: the
  "`MainMenuScene` and `ResultScene` are deferred … passes from `PreloaderScene`
  directly into `LobbyScene` for now" clause is gone; the note now records the
  staging as completed by TASK-087/TASK-090 and states the implemented order as
  `BootScene → PreloaderScene → MainMenuScene → LobbyScene → BattleScene →
  ResultScene`, matching `GameConfig.ts:64`. Accurate remainder preserved (the
  diagram remains the specified design; the staging was recorded so it is not
  left implicit). `**Version:**` moved to 1.2 with 1.1 folded into the
  parenthetical.
- `docs/02-technical/ARCHITECTURE.md` — §2.2.1 implementation-status sentence
  corrected: `Swap` is now stated as implemented end-to-end, while `CardCast`,
  `PetSkillCast`, and reconnect/resync snapshot recovery (`SIGNALR_PROTOCOL.md`
  §7, ADR-008) remain stated as not implemented yet. Both §2 and §7 citations
  retained. `**Version:**` moved to 1.2 with 1.1 folded into the parenthetical.

### Validation Results

```text
Code-agreement check        — PASS. Defect 1: MainMenuScene.ts:11
                              (`export class MainMenuScene extends Phaser.Scene`)
                              and ResultScene.ts:32 exist; GameConfig.ts:64 =
                              `scene: [BootScene, PreloaderScene, MainMenuScene,
                              LobbyScene, BattleScene, ResultScene]`;
                              PreloaderScene.ts:83 → 'MainMenuScene';
                              MainMenuScene.ts:80 → 'LobbyScene';
                              LobbyScene.ts:397 → 'BattleScene'. All five rows
                              of the task's evidence table reproduced exactly.
                              Defect 2: BattleHub.cs:567
                              `public async Task<SwapResponse> Swap(` →
                              BattleStateService.cs:712 `ExecuteSwapAsync`;
                              GameRuntime.ts:358 `requestAction` +
                              GameRuntimeEvents.ts:342 `RUNTIME_ACTION_SWAP`;
                              BattleHub.cs:389 confirms CardCast/PetSkillCast/
                              GetBattleState are intentionally NOT implemented;
                              hub exposes only JoinBattle:496, Swap:567,
                              Ping:752 — no GetBattleState method. Every premise
                              held; no Stop Condition fired.
Documentation consistency   — PASS. Corrected passages re-read against
                              SIGNALR_PROTOCOL.md §2/§7, ADR-008, ADR-003,
                              ARCHITECTURE.md §1/§2.2/§2.2.3 and TDD.md §2.1.
                              No contradiction survives; no duplicated
                              definition introduced — both edits cite their
                              owners rather than restating a contract.
Stale-claim sweep           — PASS. Sweep of both edited files for
                              deferred|not implemented|not yet|unimplemented|
                              "for now" returns 5 hits: TDD.md:4 and :111 are
                              explicitly historical ("was falsified", "passed
                              … for a time"); ARCHITECTURE.md:5, :6, :240 refer
                              only to CardCast/PetSkillCast/reconnect, which are
                              genuinely unimplemented. Zero residual false
                              statements. `Swap` no longer appears in any
                              "not implemented" claim.
Mixed-sentence check        — PASS. ARCHITECTURE.md §2.2.1 still names
                              `CardCast`, `PetSkillCast`, and reconnect/resync
                              recovery as not implemented yet, and still cites
                              SIGNALR_PROTOCOL.md §2 and §7. The sentence
                              scopes the implemented part to "one swap" and
                              keeps "Battle resolution beyond that single
                              action … not implemented yet", so the full
                              GAME_RULES.md §17 pipeline is not claimed to
                              exist.
Cross-document agreement    — PASS. TDD.md §2.1's stated order, ARCHITECTURE.md
                              §1's scene tree (BootScene, PreloaderScene,
                              MainMenuScene, LobbyScene, BattleScene,
                              ResultScene), and GameConfig.ts:64 all agree on
                              the same six scenes in the same order.
Unmodified-guard hashes     — PASS. 152-file docs/tasks guard set hashed at
                              pickup and at completion; the only differing
                              entries are the two intended documents. All
                              docs/00-overview/, docs/01-game-design/,
                              docs/03-decisions/ (no ADR created),
                              API_CONTRACTS.md, SIGNALR_PROTOCOL.md,
                              REDIS_STATE.md, DATABASE.md, GAME_STATE.md,
                              GAME_EVENTS.md and tasks/completed/** are
                              byte-identical. src/ and tests/: 344 files hashed
                              at pickup and completion — IDENTICAL (this
                              excludes node_modules/bin/obj build output).
Changed-file scope          — PASS. `git diff --numstat` reports exactly
                              `docs/02-technical/TDD.md` (19+/10-) and
                              `docs/02-technical/ARCHITECTURE.md` (17+/10-),
                              i.e. two hunks each — one content location plus
                              the Version line — plus this task file. Zero
                              src/ and zero tests/ changes.
Docs-render check           — PASS. Both files re-read in full: code fences
                              balanced (TDD 12, ARCHITECTURE 24 — even counts),
                              blockquote rendering intact (the staging note
                              still renders as one blockquote), section
                              numbering unbroken, cross-references intact.
                              §2.2.1 rules 1–6 byte-identical; §2.2.3 through
                              EOF byte-identical (0 differing lines,
                              EOL-normalized); TDD.md lifecycle diagram and the
                              TASK-080 LobbyScene bullet byte-identical.
```

```text
TASK-099 SHA-256 at pickup:      6DB86E4EACA298DC29293EC57A28A0B8D87978A94E51216B57B190DE8CD82322
TASK-099 SHA-256 at completion:  6DB86E4EACA298DC29293EC57A28A0B8D87978A94E51216B57B190DE8CD82322
TASK-079 SHA-256 at pickup:      B60C0A986F9E0EE8AC5B4611980AD73C06293018641D52A6D4D6F43C114185AC
TASK-079 SHA-256 at completion:  B60C0A986F9E0EE8AC5B4611980AD73C06293018641D52A6D4D6F43C114185AC
Result: IDENTICAL
```

Encoding verification (TASK-098's Process Note trap avoided): both edited files
were written with UTF-8-preserving edits, not a `Get-Content`/`Set-Content`
round-trip. Verified: no BOM, zero U+FFFD, zero mojibake (`Ã`/`â€`/`Â`), and
em-dashes (TDD 15 / ARCH 31), en-dashes (3 / 3), arrows (14 / 12) and `§`
(38 / 76) all intact.

### Contract Preservation Verification

```text
API contracts        UNCHANGED (API_CONTRACTS.md not modified)
SignalR protocol     UNCHANGED (SIGNALR_PROTOCOL.md not modified)
Redis behavior       UNCHANGED (REDIS_STATE.md not modified)
Database schema      UNCHANGED (DATABASE.md not modified)
Game state           UNCHANGED (GAME_STATE.md not modified)
Game events          UNCHANGED (GAME_EVENTS.md not modified)
Gameplay rules       UNCHANGED (docs/01-game-design/ not modified)
ADRs                 UNCHANGED (docs/03-decisions/ not modified; no ADR created)
```

### Items Reported, Not Fixed (`AGENTS.md` §16)

- **Pre-existing unrelated working-tree modifications.** `git status` at pickup
  already showed modified files unrelated to this task, with mtimes predating
  this session and hashes unchanged across it: `docs/01-game-design/BOSS_RULES.md`,
  `COMBAT_RULES.md`, `GAME_RULES.md`, `docs/02-technical/API_CONTRACTS.md`,
  `DATABASE.md`, `GAME_STATE.md`, plus ~25 files under `src/`/`tests/` and
  several untracked task files. This task did not touch them; they are reported
  so a later reader does not attribute them to TASK-100. They appear to belong
  to the status-effect work (TASK-091–TASK-098).
- **Untracked task files.** `tasks/completed/TASK-086`–`TASK-098` and
  `tasks/backlog/TASK-099` are untracked in git. Not this task's subject;
  reported because the task's "changed-file set" criterion is stated in terms of
  `git status`, which cannot distinguish them from TASK-100's own scope.
- **Pre-existing CRLF condition.** `core.autocrlf=true` and no `.gitattributes`,
  so the working tree is CRLF while HEAD blobs are LF; `git diff` therefore
  shows content-only hunks. No line-ending change was introduced by this task.
- No further stale statement was discovered in either edited document beyond the
  two enumerated defects.

### Server Authority & Scope Verification

- [x] Confirmed zero client-authoritative gameplay logic introduced — no code
      was written at all
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
- [x] Confirmed no source, test, migration, or seed file changed
- [x] Confirmed no API endpoint, SignalR method, Redis behavior, or PostgreSQL
      change
- [x] Confirmed TASK-099, TASK-079, and all completed tasks are unmodified
- [x] Confirmed no ADR was created and no architectural decision was recorded

### Status

**DONE.** Both enumerated defects corrected, each within its single documented
location plus its Version line. No Stop Condition fired: both defects were
present at pickup, every verified-reality premise held against current source,
`Swap` is implemented, no contract reinterpretation was required, and no
criterion needed TASK-099, TASK-079, or a completed task modified.

### Post-Completion Correction (added during the follow-up task-state reconciliation)

**This task's defect-2 premise was incomplete, and the wording it introduced
into `ARCHITECTURE.md` §2.2.1 was itself corrected afterwards.**

This task's manifest asserted *"'Battle resolution … not implemented' is TRUE
and must survive."* That assertion was **wrong at pickup** and was not
independently re-verified here — the task's evidence table (hub methods, scene
files, `ExecuteSwapAsync`) was checked, but the negative claim about battle
resolution was taken on trust. The full `GAME_RULES.md` §17 pipeline is in fact
implemented in the Swap path: `BattleStateService.cs` Steps 7–13 (Enrage,
Boss HP terminal check, Boss Passive §17 step 18a, Boss Skill / Basic Attack
steps 18b–18c, End Turn step 19a Status Effect tick, terminal
Victory/Defeat, post-resolution write-back), projected to the wire by
`BattleEventWireProjection.cs`.

Consequently the sentence this task wrote — *"Battle resolution beyond that
single action … are not implemented yet"* — was false and has since been
corrected in `ARCHITECTURE.md` §2.2.1 (Version 1.3). The `Swap` and
`CardCast`/`PetSkillCast`/reconnect halves of this task's correction remain
accurate and were preserved.

A further stale claim of the same family was found and corrected in
`GAME_STATE.md` §2.4 (Version 2.10), which had stated the Damage Pipeline and
Resource Generation "are not implemented" while both are implemented
(`DamagePipeline.Calculate`; `ResourceGenerator.Generate`/`ApplyPower`/
`ApplyHeal`).

**Lesson for the record:** a documentation task that certifies a *negative*
implementation claim must verify that absence against source with the same
rigour as a positive one; a stale in-source comment
(`BattleStateService.cs:672` still says "step 18, not implemented") is not
evidence of absence. The stale source comment itself is **reported, not fixed**
here (`AGENTS.md` §16) — it is `src/` scope and needs its own task.
