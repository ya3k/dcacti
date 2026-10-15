# TASK-277 — Cascade Protocol Decision & Compatibility Gate

---

## Metadata

```text
Task ID:           TASK-277
Type:              ARCHITECTURE / PROTOCOL DECISION + CONTRACT VALIDATION
Status:            SUPERSEDED
Superseded by:     TASK-278 (Decision items A-1…A-7 formally approved by Operator;
                   ADR-027 adopted; implementation completed under commits 86fcdd0 / eaaf70d)
Risk:              MEDIUM
Priority:          HIGH
Mode:              Controlled Repository Workflow — investigation / decision documentation only
Production code:   NONE CHANGED (no source, test, doc, ADR, or task file edited except this record)
Dependencies:      TASK-254 (DONE — DR-1 option B), TASK-273 (DONE), TASK-274 (DONE),
                   TASK-275 (IN REVIEW), TASK-276 (SUPERSEDED)
Declared Files:    tasks/completed/TASK-277-cascade-protocol-decision-and-compatibility-gate.md
Verification:      git diff --check (see §12); Domain test suite run (1623 passed)
```

## 1. Outcome in one paragraph

TASK-276's proposal is **not decision-ready and is not doc-compatible as written**. Its central claim — that a
per-pass board-carrying event "requires zero changes to the `ReceiveEvents` envelope, zero mid-resolution
network pushes, zero changes to Redis CAS persistence, and zero changes to snapshot recovery"
(`TASK-276:52`) — is true only about the *envelope member count* and about Redis. The proposal introduces a
**per-pass message** and a **board inside an event**, and both are prohibited in terms by more than one
authoritative section (`SIGNALR_PROTOCOL.md` §3.1 item 2; §3.1 item 3; §3.2.12 item 1; §8 items 5 and 7;
`GAME_EVENTS.md` §1.1; `GAME_STATE.md` §3, §5.1 item 2, §5.3 item 4). It also misstates the current
implementation in three places that change the size of the work (§3.4), and it omits the two documents whose
ownership the repository's own precedent requires the decision to change **first** (`GAME_RULES.md` §16 and
`GAME_EVENTS.md` §2 — see §9.1). This task therefore confirms the facts that *are* already determined by
approved requirements, recommends the smallest safe option where one is technically forced, and leaves the
genuinely operator-owned decisions as explicit questions (§4).

---

## 2. Work performed

| # | Required work | Where |
| :-- | :--- | :--- |
| 1 | Read the named documents and the serialization/deserialization code | §3, §5, §6 — all cited inline |
| 2 | Verify pass and cascade-depth semantics against implementation and tests | §3.1, §3.2 |
| 3 | Trace all sources of cleared cells | §3.3 |
| 4 | Verify the complete compatibility path, older and newer clients | §5 |
| 5 | Verify the board payload schema and its invariants | §6 |
| 6 | Evaluate deltas + full board, estimate payload size | §7 |
| 7 | Define failure behaviour | §8 |
| 8 | Resolve only what approved requirements authorize; document the rest | §4 |

Read directly: `AGENTS.md`, `docs/AGENTS.md`, `CLAUDE.md`; `docs/03-decisions/README.md`, `ADR-008`,
`ADR-026`; `docs/02-technical/SIGNALR_PROTOCOL.md` (§3, §3.1, §3.2.1–§3.2.12, §3.2.26, §3.2.27, §4, §6, §7,
§8), `GAME_EVENTS.md` (§1, §1.1, §1.3, §2, §3), `GAME_STATE.md` (§2.1.1–§2.1.9, §3, §5.0, §5.1, §5.3),
`ARCHITECTURE.md` §2.2.1; `docs/01-game-design/MATCH3_RULES.md` (§1.0, §1.1, §4, §4.1–§4.6, §5.5.5, §6.3),
`GAME_RULES.md` (§16, §17); `TASK-254`, `TASK-275`, `TASK-276`; backend
`GravityAndSpawn.cs`, `BoardResolver.cs`, `CascadeResolver.cs`, `BattleEventBuilder.cs`, `BattleEvent.cs`,
`BattleEventWireProjection.cs`, `BattleHub.cs`, `Program.cs`; backend tests
`BattleEventEmissionTests.cs`, `GravityAndSpawnTests.cs`, `CascadeAndDeterminismTests.cs`,
`ApiIntegrationTests.cs`, `SignalRWireJson.cs`; frontend `GameRuntime.ts`, `BattleEventPresenter.ts`,
`BattleScene.ts`, `SignalRService.ts`; frontend tests `BattleEventPresentation.test.ts`,
`SceneLifecycle.test.ts`, `GameRuntime.test.ts`.

---

## 3. Confirmed existing semantics (evidence-backed)

### 3.1 Pass index — one unambiguous meaning exists, and it is 1-based

`MATCH3_RULES.md:1024-1040` (§4.2) fixes it:

```text
Detection pass 1   (board after the committed Swap)     → depth 1
Detection pass 2   (board after pass 1's Gravity+Spawn)  → depth 2, a Cascade
```

> "**Depth 1 is the first pass** – the one run on the board produced by the committed Swap (`§2.1.6
> step 8`). It is **not** a Cascade." — `MATCH3_RULES.md:1034`
> "**Depth ≥ 2 is a Cascade.** A pass at depth `d ≥ 2` is a Cascade, and the Cascade's depth index within
> the Swap is `d − 1`" — `MATCH3_RULES.md:1036`

The implementation agrees exactly, and the two existing wire members already carry **the two different
documented values**:

| Fact | Value | Evidence |
| :--- | :--- | :--- |
| `ResolvePass(..., int cascadeDepth)` parameter | 1-based pass depth | `BoardResolver.cs:164-169`; doc `:160-163` "1 for the first pass of a Swap, ≥ 2 for a Cascade" |
| `MatchResolution.CascadeDepth` | = that same 1-based pass depth | `BoardResolver.cs:297` — `new MatchResolution(matchSet[shapeIndex], cascadeDepth, shapeCreations)` |
| `MatchCreated.cascadeDepth` on the wire | = 1-based pass depth (`1` for pass 1) | `BattleEventWireProjection.cs:1003` — `cascadeDepth: match.CascadeDepth`; `SIGNALR_PROTOCOL.md:669-675`; asserted at `ApiIntegrationTests.cs:3692` |
| `CascadeCreated.cascadeDepth` on the wire | = pass depth − 1 (`1` for pass 2) | `BattleEventBuilder.cs:111` — `BattleEvent.ForCascade(pass.Matches[0].CascadeDepth - 1)`; `SIGNALR_PROTOCOL.md:701-704`; asserted at `ApiIntegrationTests.cs:3789-3813` |
| `CascadeResult.CascadeDepth` | count of cascade passes (0-based; `0` when none) | `CascadeResolver.cs:133` — `Math.Max(0, passes.Count - 1)` |
| Loop counter | `depth` incremented before the empty check, so the terminating pass consumes a number but is never recorded | `CascadeResolver.cs:90,99-107` |

Tests that pin the numbering (quoted): `CascadeAndDeterminismTests.cs:72` —
`Assert.Equal(1, result.Passes[0].Matches[0].CascadeDepth);`; `:77-80` —
`for (var i = 0; i < result.Passes.Count; i++) { Assert.Equal(i + 1, result.Passes[i].Matches[0].CascadeDepth); }`;
`BattleEventEmissionTests.cs:378-381` — `Assert.Equal(Enumerable.Range(1, result.Resolution.Passes.Count - 1), cascades.Select(e => e.CascadeDepth));`;
`ApiIntegrationTests.cs:3805-3813` — `var passDepth = cascadeDepth + 1;` … `Assert.Contains(matches, m => m.GetProperty("cascadeDepth").GetInt32() == passDepth);`.

**Conclusion.** *"Pass index" already has exactly one meaning in this repository: the 1-based detection-pass
depth that `MatchCreated.cascadeDepth` reports. It is the only value the new event may state, and it must be
stated under a member name that is not `cascadeDepth`* — see R-2 in §4.

### 3.2 Existing `CascadeCreated` fields must not be reinterpreted — and need not be

`SIGNALR_PROTOCOL.md:705-710` (§3.2.7 item 2):

> "**The member name is the same `cascadeDepth` used by `MatchCreated`**, and the two carry **different
> documented values** on the same pass (item 4 of §3.2.6 above). They are not required to be equal, and a
> consumer must not treat one as the other."

Nothing in this task's recommended design touches `CascadeCreated`, `MatchCreated`, `ComboChanged`,
`GemMatched`, `SwapStarted`, or `SwapResolved`. The new event is **additive**; no existing field is
reinterpreted, renamed, re-based, or given a second meaning. That directly satisfies the task's constraint
"Do not silently reinterpret existing `CascadeCreated` fields".

### 3.3 Cleared cells — the one authoritative source, and it already exists

`PassResult.ClearedCellUnion` is the pass's cleared union — matched cells **plus every activation and chain
cell**, each cell once, ascending `§1.0` index:

- Built: `BoardResolver.cs:303-305` — `var union = new SortedSet<int>(matchedUnion); union.UnionWith(activation.ClearedCells); var clearedUnion = union.ToArray();`
- Declared and returned: `BoardResolver.cs:75` and `:358`.
- Documented: `BoardResolver.cs:43-48` — "The union of every cell the pass cleared — matched cells plus every activation and chain — each cell once, ascending §1.0 index."
- Rule owners: `MATCH3_RULES.md` §5.8.2 (union semantics) and `GAME_EVENTS.md:206-230` (§1.3).

All sources of a cleared cell, traced:

```text
matchedUnion            MatchDetector.ClearedCellUnion(matchSet)      BoardResolver.cs:179
                        (union of the pass's matched shape cells)
consumed Special Gems   scanned from the match set's own cells,       BoardResolver.cs:211-222
                        so a consumed gem's cell is always a matched cell
activation + chains     ResolveActivations(...) breadth-first,        BoardResolver.cs:227, 413-521
                        affected set = full row/column | 3x3 | plus,
                        always including the gem's own cell            SpecialGemEffects.cs:20-22
                        reduced by reserved creation cells (never
                        the activating gem's own cell)                  BoardResolver.cs:470-483
created Special Gems    removals from the empty set, and a creation    BoardResolver.cs:242-257
                        cell is itself a matched cell already in the union
terminating pass        not present in Passes at all                   CascadeResolver.cs:99-107
```

Test evidence that the union is the union: `SpecialGemScenarioTests.cs:291-295` —
`Assert.Equal(pass.ClearedCellUnion.OrderBy(i => i), pass.MatchedCellGemMatched.Concat(pass.ActivationCellGemMatched).Select(e => e.CellIndex).OrderBy(i => i));`;
`SpecialGemEdgeCaseTests.cs:81-82` — deduped and ascending.

**Conclusion.** If a declared cleared-cell list is wanted, its only correct derivation is
`PassResult.ClearedCellUnion`, and it is already computed. See R-5 (§4) for why the recommendation is
nevertheless *not* to put it on the wire.

### 3.4 Corrections required to TASK-276's description of the implementation

Three statements in TASK-276 §2.2/§5.1 are false against HEAD. They matter because each changes the work.

| TASK-276 claim | Reality | Evidence |
| :--- | :--- | :--- |
| "`Gravity.Apply` … **calculates the exact source-to-destination moves (`readIndex -> writeIndex`)**, but discards the move pairings from `GravityResult`" (`TASK-276:91`) | **No pairing is computed at all.** Survivors are collected as bare `Cell` values (`var survivors = new List<Cell>(BoardState.Rows);` … `survivors.Add(entries[readIndex]);`) and written positionally (`entries[writeIndex] = survivors[i];`). `readIndex` is used only for the emptiness test. `GravityResult` has no move member. Producing drop moves requires **capturing the source index alongside the survivor** — a new data path, not an un-discarded one. | `GravityAndSpawn.cs:77, 83-90, 98-107, 175-177` |
| "`BattleEventBuilder.Build` accepts an `actor` parameter (`DamageParty.Player` or `DamageParty.Boss`)" (`TASK-276:266`) | **There is exactly one overload and it takes two parameters: `(CascadeResult resolution, int combo)`.** No actor/party parameter exists anywhere on the build path; the side is carried only by the separately-added `SwapStarted` event. | `BattleEventBuilder.cs:84-86`; asserted single-method at `BattleEventEmissionTests.cs:1559-1566` |
| Emitted code uses `pass.CascadeDepth` (`TASK-276:259-264`) | **`PassResult` has no depth field.** A grep for `PassIndex` across `src/backend` returns nothing; the pass depth reaches callers only through `MatchResolution.CascadeDepth`. The builder already reads it as `pass.Matches[0].CascadeDepth`. | `BoardResolver.cs:67-76`; `BattleEventBuilder.cs:109-111` |

Confirmed as stated: `SpawnResult.CreatedCells` exists and is discarded by `PassResult`
(`GravityAndSpawn.cs:231,266,284-287`; `BoardResolver.cs:274,351`).

### 3.5 What the backend can and cannot supply today

| Needed for a per-pass transition report | Available in the resolution result at HEAD? |
| :--- | :--- |
| pass index (1-based) | **yes** — `pass.Matches[0].CascadeDepth` |
| cleared cells | **yes** — `pass.ClearedCellUnion` |
| drop moves (source → destination) | **no** — must be captured in `Gravity.Apply` |
| spawned cells + gem types | **derivable** — `Spawn.Fill` returns `CreatedCells`, currently discarded |
| spawned special gem | **never exists** — `Cell.Ordinary(gemType)` only; `MATCH3_RULES.md` §4.5 item 7 |
| the post-pass board | **yes** — `pass.Board` (= `spawn.Board`, full again) |
| the per-pass pre-gravity board | **no** — `Gravity`/`Spawn` results are locals; never published |

---

## 4. Decision table

Three statuses only: **CONFIRMED** (already determined by an approved requirement — no new approval needed
to *state* it); **RECOMMENDED** (technically forced or strongly indicated, but it is a protocol/contract
change and therefore cannot take effect without the approval in §4.3); **REQUIRES APPROVAL** (an operator /
Product-Owner decision no existing document authorizes this task to make).

### 4.1 Confirmed (no approval required to state)

| ID | Decision | Basis |
| :--- | :--- | :--- |
| **C-1** | The only unambiguous pass index is the **1-based detection-pass depth**: `1` = the pass on the committed Swap's board (not a Cascade), `d ≥ 2` = a Cascade. It is identical to `MatchCreated.cascadeDepth` on the same pass and equal to `CascadeCreated.cascadeDepth + 1`. | `MATCH3_RULES.md` §4.2 items 1–2; `SIGNALR_PROTOCOL.md` §3.2.6 item 4, §3.2.7 items 1–2; implementation + wire tests (§3.1) |
| **C-2** | `MatchCreated.cascadeDepth` and `CascadeCreated.cascadeDepth` are **not to be changed, renamed, or unified**. Both keep their current values and meanings. | §3.2 above; `SIGNALR_PROTOCOL.md` §3.2.7 item 2 |
| **C-3** | Any declared cleared-cell list must be `PassResult.ClearedCellUnion` (matched ∪ activation/chain, deduped, ascending `§1.0` index), and nothing else. | §3.3; `MATCH3_RULES.md` §5.8.2; `GAME_EVENTS.md` §1.3 |
| **C-4** | The board wire schema is `{ "cells": [ … exactly 64 … ] }`, row-major, element *i* = cell *i*, no per-element index, `gemType` one of `ATK`/`DEF`/`HP`/`POWER`, `specialGem` absent for an ordinary cell or `{ type ∈ {LineClear,Burst,Area}, orientation iff LineClear }`. | §6; `GAME_STATE.md` §2.1.1, §2.1.4, §2.1.7; `BattleHub.cs:518,537,553` |
| **C-5** | A spawned Gem **never** carries a Special Gem, on any path. | `MATCH3_RULES.md` §4.5 item 7; `GravityAndSpawn.cs:260-263`; `GravityAndSpawnTests.cs:411-413` |
| **C-6** | `Gravity.Apply` today computes **no** source→destination pairing, and `BattleEventBuilder.Build` has **no** actor parameter and `PassResult` has **no** depth field. | §3.4 |
| **C-7** | There is **no protocol version, capability negotiation, or client-version channel anywhere** — server, client, or docs. The only version-like value is `BattleState.Sequence`. | greps over `src/`, `docs/`; `SIGNALR_PROTOCOL.md:2456-2457` |
| **C-8** | Server-side, an unknown event kind **throws** (`BattleEventWireProjection.cs:803-806`), and the API test suite **fails** on an undocumented wire discriminator (`ApiIntegrationTests.cs:4107`). The "closed set" is enforced in code and in tests, not merely documented. | §5.1 |
| **C-9** | Client-side, an unrecognised `type` **is** skipped today (`BattleEventPresenter.ts:252-255` → `BattleScene.ts:4243-4245`), and there are tests for it. | §5.2 |

### 4.2 Recommended (protocol change — effective only with §4.3 approval)

| ID | Recommendation | Why |
| :--- | :--- | :--- |
| **R-1** | **Packaging: exactly one new discriminator in `ReceiveEvents.events[]`.** Not a new hub method, not a subscription, not a member of the `BattleStateUpdated` record. | The state-push route is *larger*, not smaller: `SIGNALR_PROTOCOL.md` §4 item 4 forbids adding a field to that record, and §7 fixes **join snapshot = resolution snapshot = resync snapshot, member for member** — so a per-pass history member would have to be reconciled with `GAME_STATE.md` §2.0, §3, §5.1 item 2, §5.3 items 3–4 **and** with the resync contract. The event route touches the event owners only and leaves the envelope's three members, the state push, Redis, and recovery untouched. |
| **R-2** | Name it **`CascadeStepResolved`**; state the pass index under the **new member name `passDepth`**, never under `cascadeDepth`. 1-based, per C-1. | §3.2.7 item 2 forbids treating the two existing `cascadeDepth` values as one another; reusing the *name* for a third value would create exactly the ambiguity that item was written to close. The discriminator name is free: `CascadeStepResolved` appears nowhere in `docs/` or `src/` (only in TASK-276), and is not in any existing forbidden list. |
| **R-3** | **Deltas only. Do not put a board, and do not put `actor`, in the event.** | See §7 — the board is (a) prohibited by `SIGNALR_PROTOCOL.md` §3.2.12 item 1 ("No event carries … a board") and §8 item 5, (b) a second spelling of a fact the state push already carries (§3.1 item 3, `GAME_EVENTS.md` §3 item 6), and (c) redundant: `clearedCells + dropMoves + spawnedGems + createdSpecialGems` reconstructs the post-pass board from the pre-pass board. `actor` is redundant for the same reason `SwapResolved` omits it — the sibling `SwapStarted` is the actor's only spelling (`ApiIntegrationTests.cs:4076`: "The actor is the sibling `SwapStarted`'s member, not repeated here"), and the client already carries it forward (`BattleScene.ts:4362-4368`, `adaptPresentedEvent(event, boardActionActor)`). |
| **R-4** | **Omit `spawnedGems[].specialGem` entirely.** | C-5: it can never be present. An always-absent optional member is dead schema, and its presence would invite a consumer to predict one — which `SIGNALR_PROTOCOL.md` §3.2.10 item 5 and §3.2.5 item 3 forbid. |
| **R-5** | **Omit a declared `clearedCells` member** — *conditional on* fixing the multi-match `GemMatched` defect (D-1 in §11). If D-1 is **not** fixed under this work, then `clearedCells` must be included, because it would be the only reliable per-pass clear statement. | The pass's cleared union is already delivered by that pass's `GemMatched` run (`GAME_EVENTS.md` §1.3; `SpecialGemScenarioTests.cs:291-295`). A second member would be "a second spelling of one fact", which this codebase repeatedly refuses (`SIGNALR_PROTOCOL.md` §3.2.10 item 3; `GAME_STATE.md` §2.1.3 item 5; `BattleEventWireProjection.cs:39-41`). |
| **R-6** | **Ordering: the event is the last item of its own pass** — after that pass's `MatchCreated`/`GemMatched`/`ComboChanged` and before the next pass's `CascadeCreated` (or before the following stage's events). Exactly one per entry in `CascadeResult.Passes`; none for the terminating pass. | `MATCH3_RULES.md` §4.1 fixes remove → activate/create → gravity → spawn, and `GAME_EVENTS.md` §1.1 places events on those steps. A report of the transition cannot precede the transition it reports. `CascadeCreated` is "once for the pass, **first**" (`GAME_EVENTS.md:150`), so the new event closing the pass is the only position that does not disturb any existing ordering. **This position is not doc-determined today and must be written into `GAME_EVENTS.md` §1.1.** |
| **R-7** | **Empty arrays are omitted**, consistent with `createdSpecialGems`; a present array is never empty. | `SIGNALR_PROTOCOL.md` §3.2.5 item 1 and §3.2.6 item 6 fix one omission convention for the whole protocol, and §3.2.5 item 2 refuses to invent a second. |
| **R-8** | **Compatibility strategy: additive discriminator + one explicitly documented tolerant-reader rule. No version negotiation.** | §5.4 — this is the smallest change that is *safe*, and it converts a behaviour that currently has no owner into a rule. |
| **R-9** | **Failure behaviour** per §8: presentation degrades to the existing `settle` convergence; no client inference; no permanent input lock; no weakening of the §6/§7 sequence and recovery checks. | §8 |

#### Recommended wire schema (the exact proposal)

```json
{
  "type": "CascadeStepResolved",
  "passDepth": 1,
  "dropMoves": [
    { "fromCell": 2, "toCell": 18 },
    { "fromCell": 10, "toCell": 19 }
  ],
  "spawnedGems": [
    { "cellIndex": 2, "gemType": "ATK" },
    { "cellIndex": 10, "gemType": "DEF" }
  ]
}
```

| Member | Type | Presence | Meaning |
| :--- | :--- | :--- | :--- |
| `type` | string | always | `"CascadeStepResolved"` — discriminator (`SIGNALR_PROTOCOL.md` §3.2.2) |
| `passDepth` | int | always | the detection pass's own 1-based depth: `1` for the committed Swap's pass, `≥ 2` for a Cascade. **Identical in value to `MatchCreated.cascadeDepth` on the same pass**, and to `CascadeCreated.cascadeDepth + 1` where that event exists. |
| `dropMoves` | array of object | when non-empty | surviving Gems that changed cell during this pass's Gravity. Ordered column `0 → 7`, and within a column by **descending** source index (bottom-to-top), the read order of `MATCH3_RULES.md` §4.4 item 1. |
| `dropMoves[].fromCell` | int | always | origin `§1.0` index `0..63` before Gravity |
| `dropMoves[].toCell` | int | always | destination `§1.0` index `0..63` after Gravity; `fromCell != toCell`; the two share a column |
| `spawnedGems` | array of object | when non-empty | the Gems this pass's Spawn placed. Ordered column `0 → 7`, and within a column by **ascending** destination index (top-to-bottom), the fill order of `MATCH3_RULES.md` §4.5 item 2. |
| `spawnedGems[].cellIndex` | int | always | destination `§1.0` index `0..63` |
| `spawnedGems[].gemType` | string | always | `ATK` \| `DEF` \| `HP` \| `POWER` (`MATCH3_RULES.md` §1.1, `SIGNALR_PROTOCOL.md` §3.2.4) |

No `actor` member (R-3). No `board` member (R-3). No `clearedCells` member when D-1 is fixed (R-5). No
`specialGem` inside `spawnedGems` (R-4). No `cascadeDepth` member (R-2).

#### Validation invariants for the recommended schema

Structural (per item):

```text
I-1   passDepth is an integer ≥ 1.
I-2   Exactly one CascadeStepResolved per entry in CascadeResult.Passes; none for the terminating pass.
I-3   passDepth values in one batch are strictly increasing with no gaps and the first is 1.
I-4   fromCell, toCell, cellIndex are all integers in 0..63.
I-5   fromCell != toCell for every drop move.
I-6   toCell is unique across dropMoves, and fromCell is unique across dropMoves.
I-7   cellIndex is unique across spawnedGems.
I-8   gemType ∈ {ATK, DEF, HP, POWER}.
I-9   dropMoves is ordered by (column asc, fromCell desc); spawnedGems by (column asc, cellIndex asc).
I-10  A present array is non-empty; omit rather than send [].
I-11  No member outside this table appears on the event; no member here appears as JSON null.
```

Cross-member and cross-event (per pass):

```text
I-12  floor(fromCell / 8) == floor(toCell / 8)          — Gravity never moves a Gem between columns
                                                        (MATCH3_RULES.md §4.4 item 2)
I-13  Within one column: ordering the moves by toCell ascending
      reproduces ordering by fromCell ascending          — Gravity is order-preserving, never a sort
                                                        (MATCH3_RULES.md §4.4 item 3)
I-14  dropMoves[].toCell ⊆ the pass's cleared cells     — a fall lands in a cell that was emptied
I-15  dropMoves[].fromCell ∩ the pass's cleared cells = ∅ — an emptied cell has no survivor in it
I-16  spawnedGems[].cellIndex ∩ dropMoves[].toCell = ∅  — Spawn fills only what Gravity left empty
                                                        (MATCH3_RULES.md §4.5 item 1)
I-17  spawnedGems[].cellIndex ∩ { created Special Gem cells of this pass } = ∅
                                                        — a creation cell is reserved and never vacated
                                                        (MATCH3_RULES.md §5.5.4 item 2)
I-18  Every spawned cell is among the topmost k cells of its column — Spawn never fills a mid-column cell
                                                        (MATCH3_RULES.md §4.5 item 1)
I-19  passDepth == MatchCreated.cascadeDepth for every MatchCreated of the same pass
I-20  passDepth == CascadeCreated.cascadeDepth + 1 on any pass preceded by a CascadeCreated
I-21  The pass's cleared cells == the cellIndex set of that pass's GemMatched run
      (matched sub-step then activation sub-step, ascending, each once)
      — REQUIRES D-1 fixed; §11
I-22  Applying clearedCells → dropMoves → spawnedGems (then re-placing this pass's created
      Special Gems at their cells) to the pre-pass board yields exactly the pass's post-pass board.
      This is the single reconciliation invariant the state push's final board also satisfies at `settle`.
```

Failure semantics for each invariant are in §8; **no invariant is a gameplay gate** — violating one may only
reduce presentation fidelity, never produce a value the client did not receive.

### 4.3 Requires approval (operator / Product-Owner)

Nothing below is decided by this task, and no ADR may decide it (`docs/03-decisions/README.md` §1, §5;
`GAME_EVENTS.md` §3 item 8; `ADR-026:104-113`).

| ID | Question | Options | TASK-277's note |
| :--- | :--- | :--- | :--- |
| **A-1** | **Authorize the protocol change at all?** | **(i)** authorize one additive discriminator and the four owning-document amendments in §9.1; **(ii)** retain TASK-254 DR-1 **option A** (one board transition per round, one phase per delivered cascade depth) and close DR-1 as accepted-limited. | This is the gate. Option (ii) is a legitimate outcome: TASK-254 option A is already "accepted as the supported subset" (`TASK-275:108-109`). |
| **A-2** | **Packaging.** | **(a)** one new discriminator in `events[]` (R-1, recommended); **(b)** a member on the `BattleStateUpdated` record; **(c)** a new hub method or subscription. | (c) is prohibited by `SIGNALR_PROTOCOL.md` §2 and §8 items 4–7. (b) is larger than (a) — see R-1 and §7.4. |
| **A-3** | **Payload content.** | **(a)** deltas only (R-3, recommended); **(b)** deltas + full 64-cell board ("hybrid", TASK-276's recommendation); **(c)** board only. | (a) is the only option that does not contradict `SIGNALR_PROTOCOL.md` §3.2.12 item 1 / §8 item 5, and §7 shows (b) buys no accuracy the client needs. TASK-276 recommended (b); that recommendation is **not adopted here**. |
| **A-4** | **Reconcile the prohibiting rules.** `SIGNALR_PROTOCOL.md` §3.1 item 2 ("no per-pass message"), §3.1 item 3, §3.2.12 item 1 ("No event carries … a board"), §8 item 5 and §8 item 7; `GAME_EVENTS.md` §1.1 line 148 (`gravity + spawn (no events…)`); `MATCH3_RULES.md` §4.4 item 7; `GAME_STATE.md` §3, §5.1 item 2, §5.3 item 4; and **TASK-245 PD-4** (no per-pass snapshots). | **(i)** amend each to admit an event *report* of a pass transition while keeping the board and the snapshot rules intact; **(ii)** do not proceed. | TASK-275 already requires this: "`SIGNALR_PROTOCOL.md` §3.1 item 2 (no per-step delivery) and `TASK-245` PD-4 must be reconciled by that decision first" (`TASK-275:314-316`). |
| **A-5** | **ADR form, and order.** | **(i)** amend `ADR-026` in place (the TASK-265 precedent) **and** treat ADR-027 as unnecessary; **(ii)** new `ADR-027` (free — highest existing is ADR-026); **(iii)** no ADR. | Either way the ADR is written **last**, as a *record*: "ADRs are historical records, not design proposals. Writing an ADR does not create or change a decision" (`docs/03-decisions/README.md:116-118`); "`Accepted` is only used when the decision is genuinely established" (ibid. §5); "never by an ADR asserting an event that this document does not define" (`GAME_EVENTS.md` §3 item 8). |
| **A-6** | **Scope of D-1** (the multi-match `GemMatched` duplication defect, §11). | **(i)** carry the fix in this work as a prerequisite (needed if R-5 is taken); **(ii)** separate corrective task, and then include `clearedCells` per R-5. | TASK-277 must not change code; the defect is reported, not fixed. |
| **A-7** | **Confirm R-5** (omit `clearedCells`) against A-6. | **(i)** omit and fix D-1; **(ii)** include. | The two decisions are coupled; they must be answered together. |

---

## 5. Event compatibility path — evidence

### 5.1 Server path

```text
Domain BattleEvent                     GameServer.Domain.Match3        BattleEvent.cs:53-294 (18 members)
   ↓  BattleEventWireProjection.Project  pure field mapping, one-to-one, order-preserving
   ↓                                      BattleEventWireProjection.cs:744-758, :774-807
BattleEventWireDto                     flat camelCase record, 32 optional slots + Type
   ↓                                      BattleEventWireProjection.cs:200-266
ReceiveEventsPayload                   EXACTLY 3 members, each [JsonPropertyName]
   ↓                                      BattleHub.cs:726-729
SignalR JSON                           no AddJsonProtocol / JsonHubProtocol configured
                                          Program.cs:25
```

- **Envelope is exactly 3 members**, and a backend test pins it: `ApiIntegrationTests.cs:3018`
  (`Swap_ShouldSendExactlyOneReceiveEventsBatch_WithTheDocumentedThreeMembers`), `:3058-3060` —
  `Assert.Equal(new[] { "battleId", "events", "serverSequence" }, payload.EnumerateObject().Select(p => p.Name)…)`.
- **Omission is per member**, not a serializer-wide policy (`BattleEventWireProjection.cs:53-60`), and a test
  asserts no item contains a JSON `null` (`ApiIntegrationTests.cs:4049`).
- **Casing is fixed explicitly** by `[JsonPropertyName]` on every event member, as `SIGNALR_PROTOCOL.md`
  §3.2.3 item 2 requires. SignalR's default naming policy (camelCase) applies only to members that declare
  no attribute — such as the whole `BoardPayload`/`CellPayload`/`SpecialGemPayload` family
  (`BattleHub.cs:518,537,553`), whose names therefore rest on the protocol default rather than on the
  contract (`tests/backend/GameServer.Api.Tests/SignalRWireJson.cs:38-57`). **This is a reason not to embed
  a board payload inside an event** (see §7.3).
- **An unknown Domain event kind throws**: `BattleEventWireProjection.cs:803-806` —
  `_ => throw new ArgumentOutOfRangeException(… "Not one of the documented Battle Event types …")`, with the
  doc at `:770-772`: "an unknown kind is a defect rather than a further event — it is never silently
  dropped, because dropping one would break the one-to-one projection the batch's atomicity depends on".
- **An undocumented wire discriminator fails the API test suite**: `ApiIntegrationTests.cs:4107` —
  `_ => throw new InvalidOperationException($"Undocumented event type on the wire: {type}.")`. The same file
  asserts membership in the closed 18-name list (`:4006-4007`) and, at `:4058-4060`, states the prohibition
  the proposal collides with: "No item carries a `battleId`, a `serverSequence`, a `turn`, **a board**, a
  `combo` on a non-`ComboChanged` event, a `matchCount`, a timestamp, or a GUID."
- **No version/capability handshake exists** anywhere (C-7), so the server cannot branch on client ability.

### 5.2 Client path

```text
SignalR 'ReceiveEvents'  →  GameRuntime.forwardBattleEvents        GameRuntime.ts:1612-1614, :1691-1741
   envelope check: battleId string, serverSequence number, events array   GameRuntime.ts:1798-1820
   (events[] is returned BY REFERENCE and is not validated)
   admission:  seq > held   → withhold WHOLE, request §7.1 snapshot       GameRuntime.ts:1730-1733, :1763-1791
               seq <= held  → forward WHOLE, unmodified                  GameRuntime.ts:1735-1740
   ("It is deliberately not an event filter.")                            GameRuntime.ts:1685-1689
        ↓  BattleScene.handleBattleEvents / playEventBatch                BattleScene.ts:4226-4347
   per-event parse: parseInBattleEvent → default: return null             BattleEventPresenter.ts:252-255
   unknown event DROPPED before the move log and before phase building    BattleScene.ts:4243-4245
        ↓  adaptPresentedEvent (compile-time exhaustive over a closed union) BattleEventPresenter.ts:1021-1165
```

- **An unknown discriminator survives deserialization.** There is no JSON schema library anywhere
  (no zod/valibot/ajv/io-ts/superstruct/yup in any `package.json`) and no `JsonHubProtocol` customization;
  the runtime validates only the three envelope members and returns `events` unmodified
  (`GameRuntime.ts:1803,1818`). `BattleEventPresenter.ts:214` is likewise a blind cast plus an explicit
  switch.
- **It is then skipped, with tests.** `BattleEventPresenter.ts:252-255` —
  `default: // Unknown event type or outcome event — ignored safely per SIGNALR_PROTOCOL.md §3.2.2 → return null;`
  Tested at `BattleEventPresentation.test.ts:2014` ("safely ignores unknown event types without throwing or
  fabricating presentation", including `{ type: 'UnknownFabricatedEvent', randomData: 123 }` and a
  non-event `{ notAnEvent: true }`) and `SceneLifecycle.test.ts:4597` ("ignores unknown events inside a batch
  without fabricating a phase"), which also asserts `playedPhases → []`, `inputLocked → false`,
  `pendingTweenCount → 0`.
- **`adaptPresentedEvent` never sees an unknown type.** It is an exhaustive switch with no `default` over the
  closed union `InBattleServerEvent` (`BattleEventPresenter.ts:187-203`); with `"strict": true` a missing arm
  is a compile error, not a runtime fallback. **Adding a member to that union without an arm for it would
  produce an implicit `undefined` return, not `null`** — a typing hazard the implementation must avoid.
- **No observability for a dropped event.** Unknown events are skipped before `presentedEventsLog` and
  produce no Move Log row and no `console.*` call (no `console.*` exists in `src/game` at all).
- **Batch-loss hole (C-9 magnitude).** The broadcast is
  `try { SendAsync("BattleStateUpdated", …); SendAsync("ReceiveEvents", …); } catch (Exception ex) { LogError }`
  (`BattleHub.cs:1143-1156`), and the caller still receives `SwapResponse(Accepted: true)` (`:1158`). Because
  the state push is sent **first** and the argument `ToPayload(result.Value)` — which runs
  `BattleEventWireProjection.Project` — is evaluated inside the same `try`, a projection failure **after** a
  successful state push loses the whole batch while the client's held sequence has already advanced. The
  client then sees **no sequence gap**, requests **no** snapshot, and silently never presents that resolution.
  `SIGNALR_PROTOCOL.md` §3.1 item 6 already accepts that "events may be lost in transit"; what it does not
  say is that the loss is undetectable. This is a **pre-existing** property, not one the proposal creates —
  but a larger event payload widens it, and TASK-276's failure table does not cover it.

### 5.3 Does the compatibility claim in TASK-276 hold?

| TASK-276 §7.2 claim | Verdict |
| :--- | :--- |
| "An old client ignores unknown event types in `adaptPresentedEvent` or skips them." | **Substantively true, but the mechanism named is wrong.** The skip is in `parseInBattleEvent` (`:252-255`) plus `BattleScene.ts:4243-4245`; `adaptPresentedEvent` is exhaustive over a closed union and never receives an unknown type. Also `requestBattleStateRecovery()` and `heldResolutionState` (`TASK-276:117,119`) do not exist; the identifiers are `recoverBattleState()` and `heldSequence`. |
| "No crash, no desync." | **True for an old client receiving a new event**, per the tests above. |
| "New client with old server … gracefully falls back." | **True, trivially** — an old server cannot emit the name, because its projection throw (`:803`) and its test throw (`:4107`) both prevent it. |
| Implied: "the protocol therefore permits this" | **False.** `SIGNALR_PROTOCOL.md` §3.2.2 item 4 — "An item without a recognized `type` is **malformed**, not a fifth event"; §3.2.2 item 2 closes the set against events `GAME_EVENTS.md` §2 does not define; and `ARCHITECTURE.md` §2.2.1 rule 4's exception "does **not** permit reading an event payload, **dropping an event from an admitted batch**, counting events, deriving a board or combat value, or deciding what an event means". `GAME_EVENTS.md` §3 item 3 declines ownership ("Client-side handling of each event: client implementation detail"). No document states what a client does with an unrecognised `type`. |

### 5.4 Recommended compatibility / versioning strategy (R-8) — smallest safe

1. **Additive discriminator only.** Never reuse or repurpose an existing name; never send a numeric enum
   ordinal (`SIGNALR_PROTOCOL.md` §3.2.2 item 3).
2. **Declare forward tolerance at its owner.** Add to `SIGNALR_PROTOCOL.md` §3.2.2 the one rule that is
   currently missing and that the implementation already assumes: *an item whose `type` is not in the
   discriminator table is ignored by a consumer and never presented; it is not a desync, not a second event,
   and not evidence about the resolution.* This resolves the current tension between "unrecognized ⇒
   malformed" (§3.2.2 item 4) and the client's tested behaviour, and it gives `ARCHITECTURE.md` §2.2.1
   rule 4's "dropping" clause the reconciliation it lacks.
3. **Do not add version negotiation.** There is no version channel anywhere (C-7); introducing one is a larger
   change than the feature it guards, and the additive-plus-tolerance rule is sufficient for a two-sided
   deployment where the server is authoritative. Record the absence as a known limitation rather than
   inventing a handshake.
4. **Keep the server strict.** The projection's throw and the API test's throw are correct and must stay: they
   make "an event exists but is not documented" a defect on the producing side, which is the opposite and
   compatible guarantee.
5. **Update the enforcement tests in the same change**, not after: `ApiIntegrationTests.cs:4006-4007` (closed
   18-name list), `:4069-4108` (per-type member table + `_ => throw`), and the frontend closed-set test
   `BattleEventPresentation.test.ts:938-953`, which currently asserts that board-resolution-style names
   "must stay ignored" (`CascadeStepResolved` is not among its seven names, so it will need a positive case
   rather than an exception).

---

## 6. Board payload schema (needed only if A-3 selects a board — otherwise this section fixes the schema
that any board-carrying option must reuse)

```text
board            { "cells": Cell[64] }
Cell             { "gemType": "ATK" | "DEF" | "HP" | "POWER",
                   "specialGem"? }               absent  ⇒ an ordinary Gem
specialGem       { "type": "LineClear" | "Burst" | "Area",
                   "orientation"? }              present iff type == "LineClear",
                                                 value "Horizontal" | "Vertical"
```

Invariants (all owned by existing documents — no new contract needed):

```text
B-1   cells has exactly 64 entries, row-major, index = row * 8 + column, (0,0) = top-left = index 0
      GAME_STATE.md §2.1.1 item 1; MATCH3_RULES.md §1.0
B-2   element i is the entry for cell i; no per-element cellIndex member is written
      GAME_STATE.md §2.1.7 item 2
B-3   Entries are in ascending index order and are never re-enumerated
      GAME_STATE.md §2.1.1 item 6
B-4   gemType is always present, always one of the four, including for a cell holding a Special Gem
      GAME_STATE.md §2.1.3 items 1-2
B-5   specialGem is absent (or null) for an ordinary cell — never "None", never an empty-string
      orientation, never a sentinel type                              GAME_STATE.md §2.1.7 item 3
B-6   At most one Special Gem per cell                              GAME_STATE.md §2.1.3 item 3
B-7   orientation is present iff type is LineClear                  GAME_STATE.md §2.1.4 item 2
B-8   No other per-cell member exists: no identity, id, sequence, creation pass, cascade depth, age,
      armed flag, remaining charge, or creation order                GAME_STATE.md §2.1.3 item 4
B-9   The board is always full: every cell holds one of the four Gem types. There is no empty-cell,
      null-cell, negative-index, or fifth-type representation        GAME_STATE.md §2.1.5 item 4
B-10  Round-trip is lossless                                        GAME_STATE.md §2.1.7 item 5
B-11  A board produced for delivery must be a *post-Spawn* board, never Gravity's output. Gravity writes a
      placeholder Gem type into every vacated cell and its result is explicitly "not yet a publishable
      board"; Spawn overwrites exactly those cells in the same pass.
      GravityAndSpawn.cs:120-132, :158-165, :271-274
```

`B-11` is the invariant that matters operationally: `GravityResult.Board` and `SpawnResult.Board` are both
`BoardState` values of the right *shape*, and only the second is legal to publish. A per-pass board taken from
`GravityResult.Board` would leak placeholders and would still pass B-1…B-10.

Observed divergence worth recording (not a defect to fix here): on the SignalR push an ordinary cell's
`specialGem` is present as JSON `null` (`ApiIntegrationTests.cs:1993`), whereas in the Redis/persisted record
the member is omitted (`BattleStateSerializationTests.cs:377`). `GAME_STATE.md` §2.1.7 item 3 says both
spellings decode to the same statement, so this is conformant — but a new board-carrying event would create a
**third** place where the choice is made, which is another argument for not carrying a board in an event.

---

## 7. Is an event carrying both deltas and a full board justified?

**No.** Five independent reasons, in order of weight.

**7.1 It is redundant for correctness.** Given the client's pre-pass board, the post-pass board is fully
determined by this pass's cleared cells, drop moves, spawned gems, and created Special Gems — all of which the
documented data already provides (or would, under R-5's coupled decision). Invariant I-22 states the identity;
the existing `settle` phase already reconciles against the authoritative final board. A per-pass board adds
no fact, only a second spelling of one — which this codebase refuses on principle
(`SIGNALR_PROTOCOL.md` §3.2.10 item 3; `GAME_STATE.md` §2.1.3 item 5).

**7.2 It is the most contract-conflicting variant.** A board inside an event is prohibited in terms by
`SIGNALR_PROTOCOL.md` §3.2.12 item 1 and §8 item 5, is precisely the "state on a side channel" that
`GAME_EVENTS.md` §3 items 5–6 and §3 item 8 refuse, and is what §3.1 item 3 routes to the state push. The
delta-only variant still requires the per-pass-delivery reconciliation (A-4), but it does not additionally
require rewriting the board rule.

**7.3 Its names would not even be contract-fixed.** `BoardPayload`/`CellPayload`/`SpecialGemPayload` declare
no `[JsonPropertyName]`; their camelCase names come from SignalR's default naming policy
(`SignalRWireJson.cs:38-57`). `SIGNALR_PROTOCOL.md` §3.2.3 item 2 requires the event schema's names to be
fixed explicitly and *not* delegated to a serializer default. Embedding this payload inside an event would
import a payload whose names rest on exactly the default that section forbids relying on.

**7.4 It is the largest option.** §7.5.

**7.5 Serialized size, computed from the documented schema** (byte counts of the exact JSON spellings the
schema fixes; **computed, not measured at runtime** — labelled as such).

| Element | Bytes |
| :--- | :--- |
| `{"gemType":"HP"}` × 64 + commas + `{"cells":[…]}` | **1 099** |
| `{"gemType":"ATK"}` board (all ATK/DEF) | **1 163** |
| `{"gemType":"POWER"}` board (worst case) | **1 291** |
| `,"specialGem":{"type":"LineClear","orientation":"Horizontal"}` per cell | +61 |
| `,"specialGem":{"type":"Burst"}` per cell | +30 |
| `dropMoves`, n = 6 / 8 / 16 / 24 / 32 / 64 | 163 / 217 / 433 / 649 / 865 / **1 729** |
| `spawnedGems`, n = 9 / 16 / 30 / 32 / 64 | 289 / 513 / 961 / 1 025 / **2 049** |
| Event frame (`{"type":"CascadeStepResolved","passDepth":1` … `}`) | 45 |

Per pass, delta-only:

| Case | Estimate |
| :--- | :--- |
| Typical (≈9 spawns, ≈6 drops) | ≈ 0.5 KB |
| Heavy (≈30 spawns, ≈24 drops) | ≈ 1.7 KB |
| Pathological (64/64) | ≈ 4.0 KB |

Per pass, hybrid (deltas + board): add **1.10–1.29 KB** (more with Special Gems).

A whole round, against the board the state push already carries every round:

| Scenario | Delta-only added | Hybrid added |
| :--- | :--- | :--- |
| 2-pass round, typical | ≈ 1.0 KB | ≈ 3.5 KB (two extra boards) |
| 4-pass round, typical | ≈ 2.0 KB | ≈ 4.5 KB |
| 4-pass round, heavy | ≈ 6.8 KB | ≈ 10.7 KB |

TASK-276 §7.1 estimated "`board` … ≈ 2.2 KB" and "≈ 2.5–2.8 KB" per pass; the board is in fact
**≈ 1.1–1.3 KB**, i.e. TASK-276 over-estimated it by roughly a factor of two. Its qualitative conclusion
(the payload is small relative to a WebSocket frame and compresses well) survives, but the numbers should be
restated. **No document states any payload-size budget or message-size limit** (`SIGNALR_PROTOCOL.md` §7:
"NOT ADDRESSED"; no `MaximumReceiveMessageSize` anywhere in `src/backend`), so no size option can be
*rejected* on documented grounds — which is why §7 is an argument from redundancy and contract, not from bytes.

**7.6 Recommendation.** A-3 option (a): deltas only. If the operator nonetheless requires a verification
anchor per pass, the smallest one that does not duplicate the whole board is to rely on the existing `settle`
convergence (§8, F-4) rather than to add a board.

---

## 8. Failure behaviour for per-pass data (required behaviour, not current behaviour)

Governing limits, already approved and not to be weakened: `SIGNALR_PROTOCOL.md` §6 item 3 and §7 (sequence
admission, whole-batch withholding, one in-flight snapshot request, bounded retry), `ARCHITECTURE.md` §2.2.1
rule 4 + its exception, `ADR-008`, and `GAME_STATE.md` §5.0. The per-pass event is **presentation data**; no
failure of it may (i) cause client-side gameplay inference, (ii) hold input permanently, or (iii) weaken a
sequence or recovery check.

| # | Condition | Required behaviour | Convergence |
| :--- | :--- | :--- | :--- |
| **F-1** | Event absent (old server, dropped member, or a batch built without it) | Build no drop/spawn phases; keep today's behaviour (`BattleScene.ts:4315-4326`, `:4595-4630`). | Single `settle` phase converges the board and releases the lock. |
| **F-2** | Event malformed (missing/`null` `passDepth`; non-integer or out-of-range indices) | Reject **that event only** (`parseInBattleEvent` returns null); never fabricate a cell, a gem type, or a move. | Remaining events present normally; `settle` converges. |
| **F-3** | Event internally inconsistent (I-5…I-9, I-12, I-13, I-16, I-17, I-18 violated) | Skip the offending element (or the whole event if the inconsistency is structural); never "repair" it by inference; never let it change gameplay state. | `settle` converges to the authoritative board. |
| **F-4** | Event disagrees with the delivered final board (I-22) | Present nothing from the disagreement; converge on the delivered authoritative board. The state push is the authority, never the event (`GAME_EVENTS.md` §3 item 6; `SIGNALR_PROTOCOL.md` §3.1 item 3). | `settleHeldPresentation()` → `renderBoard(state.board, false)` (`BattleScene.ts:5347-5373`). |
| **F-5** | Duplicated event inside one batch (same `passDepth` twice) | Apply the first occurrence; ignore later duplicates. Never apply a move or a spawn twice. | Unchanged; `acceptedResolutionSequence` already suppresses whole repeats (`BattleScene.ts:4270-4282`). |
| **F-6** | Stale batch (`serverSequence < held.sequence`) | Unchanged from today: forwarded, then suppressed by the scene's `acceptedResolutionSequence` guard. **Not** a desync. | Unchanged. |
| **F-7** | Sequence gap (`serverSequence > held.sequence`) | Unchanged from today: the **whole** batch is withheld and one `GetBattleState` snapshot is requested; the per-pass event is never presented from a withheld batch. | §7.1 snapshot; no replay (`SIGNALR_PROTOCOL.md` §8 item 8). |
| **F-8** | Interrupted presentation (disconnect, scene shutdown, superseding state) | Abort the timeline, cancel tweens, converge, release the lock exactly once (existing `abortPresentation` / `settleHeldPresentation`; TASK-275 AC-8/AC-9/AC-11). | Snapshot or the delivered state. |
| **F-9** | Whole batch lost after a successful state push (the C-9 hole) | **No new mechanism.** The client converges from the state push it did receive; the missing batch must not leave a lock engaged. Document that per-pass presentation is best-effort and never a correctness dependency. | State push already rendered; the timeline either never starts or is aborted by the newer state (`renderBattleState`, `BattleScene.ts:2245-2373`). |
| **F-10** | Recovery snapshot arrives mid-animation | Unchanged: abort, cancel, converge instantly; do **not** replay missed passes (`ADR-008`; `SIGNALR_PROTOCOL.md` §7). | Snapshot board. |

**Explicit prohibitions** (each maps to an existing rule): no client-side gravity, refill, spawn-identity,
match, Combo, or board derivation (`AGENTS.md` §10; `SIGNALR_PROTOCOL.md` §3.1 item 5); no input lock that
outlives a resolution or a recovery (TASK-275 AC-9); no relaxing of the sequence comparison, the
whole-batch-withholding rule, or the one-snapshot-per-gap bound (TASK-273/TASK-274 — and TASK-277 changes
nothing there).

---

## 9. Revised implementation task breakdown and acceptance criteria

### 9.1 Documentation first — the order is mandated, and TASK-276 inverts it

The repository already rules on both the order and the instrument:

- `GAME_EVENTS.md` §3 item 5: "a value the client needs but no event reports is a gap to be resolved by
  adding an event to **`GAME_RULES.md` §16**, not by delivering state on a side channel."
- `GAME_EVENTS.md` §3 item 8: "Whether such an operation needs an event is decided the ordinary way, by
  adding it to `GAME_RULES.md` §16 first (§3 item 5) — never by delivering state on a side channel, and
  **never by an ADR asserting an event that this document does not define**."
- `ADR-026:104-113` (the TASK-265 amendment) records the identical lesson from the `BoardReshuffled`
  incident: the name had been "carried into item 8 … without ever being added to the event list's owner. **An
  ADR may not create a contract**."
- `docs/03-decisions/README.md` §1 and §5: an ADR "documents one that the rest of the documentation already
  establishes"; `Accepted` is never used for something "still under discussion".
- `TASK-275:312-316` gives the exact chain: "`GAME_RULES.md` §16 → `GAME_EVENTS.md` §2 →
  `SIGNALR_PROTOCOL.md` §3.2.x, an ADR-026 amendment, and Product-Owner approval."

TASK-276's Phase 1 is "Author ADR-027 … Update `SIGNALR_PROTOCOL.md`, `GAME_EVENTS.md`, `GAME_STATE.md`"
(`TASK-276:389-393`) — ADR first, and `GAME_RULES.md` §16 **absent from the declared file set entirely**
(`TASK-276:444-450`). Both must be corrected.

Required order:

```text
0. Operator/Product-Owner approval of A-1 … A-7              (no document is edited before this)
1. GAME_RULES.md §16          add CascadeStepResolved to the canonical event-name list
2. GAME_EVENTS.md §1.1, §2, §3 define trigger, payload, and its position in the cycle
3. SIGNALR_PROTOCOL.md §3.2.2 add the discriminator; §3.2.28 (new) fix the member table;
                          §3.1 item 2 / §3.1 item 3 / §3.2.12 item 1 / §8 items 5,7 reconciled per A-4;
                          §3.2.2 the tolerant-reader rule per R-8
4. GAME_STATE.md §3, §5.1 item 2, §5.3 item 4   state explicitly that a *report* of a pass transition
                          is delivered while the intermediate board remains transient and unpublished
5. ADR-027 or an ADR-026 amendment                     record the decision, last, as a record
6. tests updated in the same change                    see §9.4
7. implementation                                      §9.2, §9.3
```

### 9.2 Backend subtasks

```text
BT-1  GravityAndSpawn.cs — capture drop moves.
      Add the source index to the survivor collection (a new local carrying (index, Cell)), and a
      DropMove(FromCell, ToCell) list on GravityResult, appended when the source index differs from the
      write index, in the column-ascending / bottom-to-top order of MATCH3_RULES.md §4.4 item 1.
      NOTE: this is a new data path, not an un-discarded one (§3.4).
BT-2  BoardResolver.cs — extend PassResult with the pass's drop moves and spawned gems.
      Map SpawnResult.CreatedCells -> (cellIndex, gemType) in CreatedCells' own §4.5 item 2 order.
      Do NOT add a depth field: the pass depth is already carried by MatchResolution.CascadeDepth.
      Do NOT add a board member: pass.Board already is the post-Spawn board (B-11).
BT-3  BattleEventBuilder.cs — emit the event as the LAST item of each pass.
      Source the depth from pass.Matches[0].CascadeDepth (already the pattern at :109-111).
      Add a BattleEvent.ForCascadeStepResolved(passDepth, dropMoves, spawnedGems) factory and the
      corresponding payload field + throwing accessor on BattleEvent.
      The pass loop already reads pass.Matches[0].CascadeDepth, so no actor parameter is needed — do NOT
      add one (the actor is carried by SwapStarted; adding one would be new API for no consumer).
BT-4  BattleEventType — add one member. The enum is documented as closed and contract-owned
      (BattleEvent.cs:12-19); it may gain a member only after step 1 of §9.1 has landed.
BT-5  BattleEventWireProjection.cs — add the discriminator case, the optional DTO slots
      (passDepth, dropMoves, spawnedGems, plus nested DropMoveWireDto / SpawnedGemWireDto), and the
      factories. Every new member carries [JsonPropertyName] (camelCase) and
      [JsonIgnore(WhenWritingNull)]; nested DTO members likewise (§3.2.3 item 2, §7.3).
```

Not touched: `BattleStateService.cs` (no actor plumbing needed — see BT-3), Redis serialization,
`BattleState`, `CascadeResolver`, `SwapExecution`, and the whole recovery path.

### 9.3 Frontend subtasks

```text
FT-1  BattleEventPresenter.ts — add the arm to the closed union InBattleServerEvent and a
      parseCascadeStepResolved; add the arm to adaptPresentedEvent (a missing arm is a compile error,
      and an unmatched arm would return undefined rather than null).
FT-2  Add one compound presentation step (or two sequential phases) for the pass transition.
      Both the drop and the spawn must be driven ONLY by delivered members.
FT-3  BattleScene.ts — buildPresentationPhases: place the new step at its delivered position (the
      pass's last item), so it follows the pass's match/cascade phase and precedes the next.
FT-4  Apply the transition by re-indexing gem views strictly from dropMoves/spawnedGems, and treat
      the delivered pass board as unavailable (there is none under A-3(a) / R-3).
FT-5  Validate every invariant in §4.2 before applying; skip the offending element on violation (F-3).
      Never infer a destination, a gem type, or a cleared cell.
FT-6  Guarantee lock release on every failure path (F-1…F-10); do not touch the sequence admission
      logic, the withholding rule, or the recovery request (§8 prohibitions).
```

### 9.4 Test subtasks (existing tests that MUST change, and new ones)

| Action | Test | Why |
| :--- | :--- | :--- |
| **Update** | `ApiIntegrationTests.cs:4006-4007` (closed 18-name list) | the new discriminator joins the set |
| **Update** | `ApiIntegrationTests.cs:4069-4108` (per-type member table + `_ => throw`) | the new type needs an allowed-member row; the throw must stay for everything else |
| **Update** | `BattleEventPresentation.test.ts:938-953` | currently asserts board-resolution-style names "must stay ignored"; add the positive parse case and keep the seven negatives |
| **Update** | Any whole-batch shape assertion that enumerates the emitted types (e.g. `BattleEventEmissionTests.cs:505-536`, `:1372-1381`) | the new event is now in the stream |
| **New** | Gravity computes exact drop moves; non-moving survivors excluded; order is §4.4 item 1 | BT-1 |
| **New** | Spawn-created gems are recorded in §4.5 item 2 order with types and indices; never a Special Gem | BT-2, C-5 |
| **New** | One event per pass, last in the pass, ascending `passDepth`, none for the terminating pass | R-6 |
| **New** | `passDepth` equals `MatchCreated.cascadeDepth` on the same pass and `CascadeCreated.cascadeDepth + 1` where present | I-19, I-20 |
| **New** | **A pass with two or more Matches**: per-pass event emitted once, and (if R-5/D-1) the `GemMatched` run is not duplicated | I-21; §11 D-1 |
| **New** | Wire: exact member set, camelCase, no nulls, nested DTO names | BT-5, §3.2.3 |
| **New** | Boss cycles carry the pass transition too (the event needs no actor, so the same path serves both) | R-3; evidenced by `BattleStateService.cs:1788-1799` |
| **New (frontend)** | Presenter adapts the event; scene animates only delivered moves; multi-pass destruction confirms at depth ≥ 2 | FT-1…FT-4 |
| **New (frontend)** | Missing / malformed / inconsistent / duplicated / stale / interrupted per-pass data each degrade per F-1…F-10, with input released | §8 |

### 9.5 Acceptance criteria (superseding TASK-276 §11.3)

```text
AC-1  Zero client simulation. Every presented movement, spawn, and cell change is a delivered member.
      No gravity, refill, spawn-identity, match, or board derivation on the client.   (AGENTS.md §10)
AC-2  Per-pass visual fidelity: each pass presents its own confirmed destruction and its own delivered
      transition, in the delivered order.
AC-3  Multi-pass confirmation: destruction gates confirm at depth >= 2 because the prior pass's
      transition was applied from delivered data.
AC-4  Wire contract integrity: the ReceiveEvents envelope stays exactly 3 members; BattleStateUpdated
      is unchanged and still first; one Redis CAS per round; the board stays out of events; no existing
      event's members or position change.
AC-5  Recovery and desync invariants are unaltered and unweakened (TASK-273/TASK-274):
      whole-batch withholding, one in-flight snapshot per gap, bounded retry, no event replay.
AC-6  Both actors: player and boss cycles present identically, with no actor member added to the event.
AC-7  Failure behaviour per F-1..F-10, with input released on every path and no permanent lock.
AC-8  Compatibility: an old client ignores the new discriminator without error; a new client without it
      falls back to today's single settle; the tolerant-reader rule is documented at its owner.
AC-9  Documentation: GAME_RULES.md §16, GAME_EVENTS.md §2/§1.1, SIGNALR_PROTOCOL.md §3.2.x, and the
      ADR agree with the implementation, in the §9.1 order.
AC-10 Verification: dotnet test on the solution, the frontend suite, npx tsc --noEmit, npm run build,
      and git diff --check all pass; pre-existing working-tree changes are preserved; no commit or push.
```

---

## 10. Explicit list of changes required to the existing TASK-276 plan

| # | Location in TASK-276 | Required change |
| :--- | :--- | :--- |
| 1 | `:52` ("requires zero changes to …") | Restate. True only for the envelope's member count, the state push, Redis, and recovery. It **does** change `SIGNALR_PROTOCOL.md` §3.1 item 2, §3.1 item 3, §3.2.12 item 1, §8 items 5 and 7, `GAME_EVENTS.md` §1.1, `GAME_STATE.md` §3/§5.1 item 2/§5.3 item 4, and TASK-245 PD-4. |
| 2 | `:389-393` (Phase 1) and `:444-450` (declared file set) | Invert to the §9.1 order; **add `docs/01-game-design/GAME_RULES.md`** (§16) to the file set; move the ADR to the **last** documentation step and state it records rather than creates. |
| 3 | `:91` | Correct: `Gravity.Apply` computes **no** source→destination pairing. BT-1 is new code, not an un-discarded value. |
| 4 | `:259-264` (`pass.CascadeDepth`) | Correct: `PassResult` has no depth member. Source the depth from `pass.Matches[0].CascadeDepth` (the existing pattern at `BattleEventBuilder.cs:109-111`). |
| 5 | `:266`, `:401` (actor plumbing) | Remove. `Build(resolution, combo)` has no actor parameter and needs none (R-3); `BattleStateService.cs` is not in the declared set. |
| 6 | `:92` | Confirm `SpawnResult.CreatedCells` is discarded by `PassResult` — accurate — and note that it already carries exactly the needed `(index, Cell)` pair. |
| 7 | `:159-202` (schema) | Apply R-2 (`passDepth`, new name), R-3 (drop `board` and `actor`), R-4 (drop `spawnedGems[].specialGem`), R-5/R-7 (drop `clearedCells`, omit empty arrays). |
| 8 | `:146-152` (B1/B2/Hybrid) | Record that the Hybrid was **evaluated and is not recommended** (§7), with the size table for both variants. |
| 9 | `:377-381` (failure table) | Replace with §8's F-1…F-10. The existing table omits malformed-but-parseable data, duplicated elements within one event, invariant inconsistency, and the whole-batch-loss case. |
| 10 | `:336-355` (payload size) | Restate with the computed table in §7.5. The board is ≈1.1–1.3 KB, not ≈2.2 KB. |
| 11 | `:347-355` (backward compatibility) | Correct: the skip is `parseInBattleEvent` + `BattleScene.ts:4243-4245`, not `adaptPresentedEvent`; and the protocol currently calls an unrecognised type *malformed* (§3.2.2 item 4), so the tolerance must be documented, not assumed. |
| 12 | `:117`, `:119`, `:349` | Correct non-existent identifiers: `recoverBattleState()` (not `requestBattleStateRecovery`), `heldSequence` (not `heldResolutionState`). |
| 13 | `:387-414` (WBS) | Replace with §9.2/§9.3/§9.4, and add the two omissions: the `GAME_RULES.md` §16 step and the enforcement-test updates. |
| 14 | `:431-441`, `:456-469` (test matrix) | Add the multi-match-per-pass case (D-1 / I-21), the `passDepth` equality assertions (I-19, I-20), the invariant-violation cases, and the four existing tests that must be updated (§9.4). |
| 15 | `:472-477` (ACs) | Replace with §9.5 (AC-7, AC-8, AC-9 are new). |
| 16 | `:141-154` (§3.2) | Reframe. "Packaging location" and "payload content" are operator questions (A-2, A-3), and "Pass Depth Naming & Base" is **already resolved** by `MATCH3_RULES.md` §4.2 — only the member *name* remains a choice, and R-2 fixes it. |
| 17 | `:36-42` (§1.2) | Correct the "consequently" list: match detection, cleared cells, cascade sequencing are delivered as stated; the third bullet should cite `MATCH3_RULES.md` §4.4 item 7 **and** `GAME_EVENTS.md:148`; and a fourth fact is missing — a **created Special Gem** stays in its own cell through Gravity (it is a reserved creation cell, never vacated), so a cleared cell can be re-occupied by a third kind of content that is neither a falling survivor nor a spawned gem. |

---

## 11. Defects and contradictions reported, not fixed (`AGENTS.md` §16)

| ID | Issue | Location | Impact | Suggested follow-up |
| :--- | :--- | :--- | :--- | :--- |
| **D-1** | **A pass with two or more Matches emits that pass's entire `MatchedCellGemMatched` list once per Match**, because the `foreach (var gem in pass.MatchedCellGemMatched)` sits *inside* `foreach (var match in pass.Matches)`. That duplicates `GemMatched` events for any such pass. The test that would catch it (`BattleEventEmissionTests.cs:1149-1163`) compares the emitted count against `Σ (MatchedCellGemMatched.Count + ActivationCellGemMatched.Count)`, and only runs on the `CascadeBattle()` fixture, whose passes are never asserted to be single-Match; the whole Domain suite passes (1623/1623). **Code-level finding; not verified at runtime** — no fixture in the suite is asserted to contain a multi-match pass. | `BattleEventBuilder.cs:114-136`; test intent at `BattleEventEmissionTests.cs:1150-1162`, `:1207-1236` | Violates `GAME_EVENTS.md` §1.3 ("once per cell of the union") and the test's own stated intent. It makes the `GemMatched` stream unreliable as a per-pass cleared-cell statement, which is the reason R-5 is coupled to this fix. | Corrective task (or A-6(i)); add a pass with ≥2 disjoint Matches to the emission fixtures. |
| **D-2** | Docs-vs-docs: `MATCH3_RULES.md` §6.7 item 4 calls the terminating pass a Cascade ("three Cascades (passes 2, 3, and the terminating pass 4)"), while §4.2 item 2 and §4.3 item 4 deny exactly that. | `MATCH3_RULES.md:2864-2866` vs `:1036-1040`, `:1060-1061` | Directly relevant to pass numbering; a reader could implement a per-pass event for the terminating pass. | Report per `AGENTS.md` §4; smallest correction is to §6.7 item 4. |
| **D-3** | `GAME_EVENTS.md` §3 item 5 asserts "every event defined in §2 belongs to the ordered list of §1 — none is defined as presentation-only", while `SIGNALR_PROTOCOL.md` §3.2.2 item 2 excludes `MatchResolved`, `TurnStarted`, `TurnEnded`, and `BattleStarted` from the delivered set. | `GAME_EVENTS.md:682-688` vs `SIGNALR_PROTOCOL.md:535-539` | Pre-existing; the already-open TASK-246 C-1. It means "add an event to §16" and "it will be delivered" are not yet equivalent. | TASK-246 C-1. |
| **D-4** | TASK-254's acceptance criteria still say "the closed **16-name** discriminator set"; the set is 18. | `TASK-254:237` | Stale task record; a completed task is immutable, so this is recorded only. | Record; no edit. |
| **D-5** | `GAME_EVENTS.md` §2 lists "tier (3/4/5/L-T)" in `MatchCreated`'s payload; `SIGNALR_PROTOCOL.md` §3.2.6 defines no `tier` member and says the wire does not invent one. | `GAME_EVENTS.md:339` vs `SIGNALR_PROTOCOL.md:640-655` | Semantic-vs-wire drift; harmless today because the wire owner is explicit. | Report. |
| **D-6** | `SIGNALR_PROTOCOL.md` cites "§3.3" three times but has no §3.3 heading. | `:4`, `:893`, `:1603` | Dangling cross-reference for an implementer. | Report. |
| **D-7** | No document states what a client does with an unrecognised `type`; `ARCHITECTURE.md` §2.2.1 rule 4's exception forbids "dropping an event from an admitted batch", while the shipped client drops it and TASK-254:307 requires exactly that. An ordinary cell's `specialGem` is JSON `null` on the push but omitted in Redis. | `ARCHITECTURE.md:333-339`; `BattleEventPresenter.ts:252-255`; `TASK-254:307`; `ApiIntegrationTests.cs:1993` vs `BattleStateSerializationTests.cs:377` | Bearing on R-8; the tolerant-reader rule has no owner. | R-8 step 2. |
| **D-8** | Whole-batch loss with no detectable sequence gap when the projection throws after a successful state push. | `BattleHub.cs:1143-1156`; `BattleEventWireProjection.cs:803` | Pre-existing; per-pass presentation is best-effort and the client must never depend on it for correctness (F-9). | Report; covered by F-9/AC-7. |
| **D-9** | `GravityResult.VacatedCells` is appended in column-ascending but **row-descending** order (`7,6,…,0` for an emptied column), while the class comment describes them only as "the topmost cells". No test asserts the raw order — every order-sensitive assertion sorts first. | `GravityAndSpawn.cs:127-132`; `GravityAndSpawnTests.cs:82,317,336` | No current consumer depends on it (BT-2 uses `SpawnResult.CreatedCells`, not `VacatedCells`), but an implementation that derived `spawnedGems` from `VacatedCells` would produce the wrong order. | Report; if a future step publishes this list, document its order at its owner. |
| **D-10** | `SwapExecution.cs:463` passes `swapOriginIndex: null` while the adjacent comment (`:456-462`) describes deriving the origin from both exchanged cells. | `SwapExecution.cs:456-463` | Does not affect this task's decisions; reported as an observation only (the sub-investigation could not determine intent from docs or tests). | Report. |

---

## 12. Verification performed, and its limitations

### 12.1 Run

```text
git status --short         unchanged before and after this task: 1 pre-existing untracked file
                           (tasks/backlog/TASK-276-…md) plus this task's new untracked record
git diff --check           clean (exit 0) — no tracked file modified, nothing staged
git diff --check           clean (exit 0) again with this record staged via `--intent-to-add`,
                           i.e. with the record itself actually inspected (see §12.3)
trailing-whitespace lines  0 ; tab lines 0 ; line endings LF
dotnet test tests/backend/GameServer.Domain.Tests/…  Passed! Failed: 0, Passed: 1623, Skipped: 0
                           (2 pre-existing xUnit2000 analyzer warnings, unrelated)
```

No production code, test, `docs/`, ADR, or task file was edited. `TASK-276` was read but not modified, and it
is still untracked and unmodified. No commit, push, or destructive Git command was issued. The one index
mutation performed — `git add --intent-to-add` on this record, to make the whitespace check see it — was
undone with `git reset -- <this file>`; `git status --short` afterwards is the same two untracked entries as
before this task, and `git diff --cached --name-only` is empty.

### 12.2 Evidence standard applied

Every factual claim in §3, §5, §6, §7.5, and §11 cites a `path:line`; verbatim quotes are used where the
wording carries the rule. Where an implementation statement in TASK-276 could not be reproduced, it is
recorded as a correction with the code that contradicts it (§3.4) rather than silently restated.

### 12.3 `git diff --check`

Run twice. First over the working tree as found: exit 0, which proves no tracked file moved but
**cannot** inspect this record, because `git diff --check` does not see untracked files. The record was then
staged with `git add --intent-to-add` and `git diff --check` re-run, so that the check actually read the new
file: exit 0, with `git diff --stat` reporting `812 insertions(+)` and no whitespace error. Independently,
the file was measured to contain **0** trailing-whitespace lines, **0** tab-indented lines, and LF line
endings. The index mutation was then reverted with `git reset -- <this file>`; `git status --short` is
afterwards the same two untracked entries as before this task, and `git diff --cached --name-only` is empty.

**Remaining limitation:** `git status --short` at the start of this task showed a clean tracked tree and
exactly one untracked file (`TASK-276`), which is not the 19-entry working tree `TASK-275` recorded — those
changes were committed in `425bebb` before this task began. The claim "pre-existing working-tree state
preserved" therefore means: no tracked file changed, and the one pre-existing untracked file is byte-identical
and still untracked. It is not a claim about the 19 entries TASK-275 described.

### 12.4 Limitations of this investigation

1. **`D-1` is a code-level finding, not a runtime reproduction.** It rests on the lexical nesting of
   `BattleEventBuilder.cs:114-136`. No fixture in the suite is asserted to contain a multi-match pass, and
   creating one would require adding a test file, which this task's "investigation only" scope forbids.
2. **§7.5's sizes are computed from the documented JSON spellings, not measured from serialized DTOs.** They
   are byte counts of schemas that are themselves pinned by `docs/` and by the DTO attributes; they are
   labelled computed wherever used, and TASK-276's corresponding figures are labelled unverified-by-source.
3. **No end-to-end browser run.** No frontend suite, `tsc`, or build was run: no frontend file changed, and
   the claims about the frontend are read from source and from the existing test assertions, which are quoted
   rather than re-executed.
4. **No Redis-backed test was run** (the repository round-trip test is gated behind `RequireRedis()`), so the
   persisted-cell-shape claim in §6 rests on source plus the Domain serialization tests, not on a live store.
5. **No deployment configuration was inspected.** No message-size or compression limit was found in this
   repository (no `MaximumReceiveMessageSize` anywhere), so the transport-limit question in §7.5 is recorded
   as NOT FOUND rather than answered.
6. **Decisions A-1…A-7 are deliberately unresolved.** They are operator/Product-Owner calls; this task
   documents the exact question and the options, and does not select one (`AGENTS.md` §7, §18, §20).

### 12.5 Decisions still pending

`A-1` (authorize the change at all) · `A-2` (packaging) · `A-3` (payload content) · `A-4` (reconcile the
prohibiting rules, including TASK-245 PD-4) · `A-5` (ADR form and order) · `A-6` (scope of D-1) ·
`A-7` (confirm R-5, coupled to A-6). Until `A-1` and `A-4` are answered, **no implementation of authoritative
per-pass cascade presentation is authorized**, and TASK-276's declared file set must not be acted on.
