# TASK-148 — Present `RelicTriggered` and `PowerChanged` In-Battle Events in `BattleScene`

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ and tasks/ by path and section.

  PROVENANCE: Identified during TASK-147 verification (repository task-discovery
  pass following TASK-143/144/145/146/147). The backend Relic stage
  (GAME_RULES.md §17 step 11) landed in TASK-133 and emits RelicTriggered and
  PowerChanged over ReceiveEvents, and their wire shapes are fixed and final by
  SIGNALR_PROTOCOL.md §3.2.23/§3.2.24 (TASK-104 A-1A/A-3/A-5A, TASK-131 D10).
  The client parser, however, still handles only the twelve non-outcome event
  types it handled before those two were admitted to the closed discriminator,
  so both events are silently dropped at the parser's default branch.

  TASK-133's own Out of Scope recorded this as deliberately deferred:
  "Client-side Relic presentation or UI — a separate client task if required
  (ADR-003, ARCHITECTURE.md §2.2)" — and its Completion Evidence confirms the
  client "ignores the two event names it does not parse". TASK-088's Out of
  Scope excluded the two events on the grounds that they "are not delivered",
  which was true when TASK-088 ran and is no longer true.

  THIS IS A CLIENT PRESENTATION TASK ONLY.
  It adds no event, no wire member, no gameplay rule, and no client authority.
  No file under src/backend/ is modified by this task.
-->

---

## Metadata

```text
Task ID:           TASK-148
Type:              FEATURE (TASK_TYPES.md §2 — "Implement a documented mechanic,
                   capability, or system that already has a home in docs/ but has
                   not yet been built"; workflow development/feature.md)
Status:            DONE (direct execution recorded: Changed Files + Validation
                   Results in Completion Evidence; all acceptance criteria
                   verified. Lifecycle: BACKLOG → IN PROGRESS → DONE per
                   TASK_LIFECYCLE.md §3.)
Risk:              LOW (TASK_TYPES.md §4 — isolated presentation-layer change in a
                   single scene-local presenter; no API, schema, state, protocol,
                   or gameplay change)
Priority:          MEDIUM (completes the client-side consumption of two events the
                   server already emits; no other task is blocked by it)
Primary Agent:     client (TASK_TYPES.md §5 — Frontend/Phaser domain → Client)
Supporting Agents: testing, review
Workflow:          development/feature.md
Skills:            client/client-event-projection,
                   client/phaser-battle-presentation,
                   client/client-state-authority,
                   testing/test-scenario-generation,
                   quality/implementation-review
                   (5 skills — Normal budget, tasks/README.md §12)
Dependencies:      TASK-133 (DONE — backend Relic stage emits RelicTriggered and
                   PowerChanged), TASK-104 (DONE — the closed discriminator and the
                   §3.2.23/§3.2.24 wire member tables), TASK-131 (DONE — D10
                   confirms the RelicTriggered shape is final, not a placeholder),
                   TASK-088 (DONE — the existing in-battle presentation path this
                   task extends)
```

---

## Objective

Extend the scene-local in-battle event presenter (`BattleEventPresenter.ts`) so the two already-emitted, already-specified events `RelicTriggered` and `PowerChanged` are parsed, represented, and formatted alongside the twelve event types it handles today, and are surfaced by `BattleScene` through its existing presentation path — while preserving the server-authoritative boundary that the presenter performs zero gameplay computation and presents only delivered members verbatim.

Both events currently reach the client and are dropped silently: `parseInBattleEvent` has no `case` for either name, so they fall to its documented "unknown event type" `default` branch and return `null`. The backend already emits both (`BattleStateService.cs:1334-1335`, projected by `BattleEventWireProjection.cs:748-749`) and `SIGNALR_PROTOCOL.md` §3.2.2's discriminator already admits both names.

---

## Context

The client's event interpretation is centralized in one scene-local module:

```text
src/frontend/client/src/game/scenes/BattleEventPresenter.ts
├── comment (line 4)          "the twelve non-outcome event types of the closed
│                              wire discriminator" — an enumeration of 1..12 at
│                              lines 7–18 that omits both events
├── union InBattleServerEvent (lines 120–132)   twelve members
├── parseInBattleEvent switch (lines 148–176)   twelve cases + default → null
├── formatInBattleEvent switch (lines 417–457)  twelve cases
└── one parseX/one format arm per event type
```

`BattleScene.ts` consumes the presenter's output for its in-battle event
feedback and already presents every event type the presenter returns.

**Why they are dropped today.** `SIGNALR_PROTOCOL.md` §3.2.2's discriminator
table is closed against events `GAME_EVENTS.md` §2 does not define; it admits
`RelicTriggered` and `PowerChanged`, which `GAME_EVENTS.md` §2 does define. The
presenter's `default` branch returns `null` for an unrecognized `type`, so an
admitted-but-unhandled event is discarded without error — the behavior is
silent, not loud.

**Scope of the omission.** `TASK-088`'s Out of Scope listed `PowerChanged` and
`RelicTriggered` among events that "are not delivered and must not be
fabricated" — accurate at that time, because no stage emitted either. TASK-133
subsequently landed the Relic stage and its emission, and explicitly deferred
the client surface: `"Client-side Relic presentation or UI — a separate client
task if required (ADR-003, ARCHITECTURE.md §2.2)"`. That task was never created;
this task is it.

---

## Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1 — Relics ("Trigger system") and Combat ("Power") are IN scope
- `docs/02-technical/SIGNALR_PROTOCOL.md` **§3.2.2** — the closed `type` discriminator, which admits `RelicTriggered` and `PowerChanged` (item 2, item 5)
- `docs/02-technical/SIGNALR_PROTOCOL.md` **§3.2.23** — `RelicTriggered` member table (`type`, `relicId`) and its finality (items 4–5)
- `docs/02-technical/SIGNALR_PROTOCOL.md` **§3.2.24** — `PowerChanged` member table (`type`, `delta`, `power`, `source`)
- `docs/02-technical/SIGNALR_PROTOCOL.md` **§3.2.3** — wire property casing is `camelCase`, case-sensitive
- `docs/02-technical/SIGNALR_PROTOCOL.md` **§3.2.4** — enum-valued members are strings carrying the documented contract name, never numbers
- `docs/02-technical/SIGNALR_PROTOCOL.md` **§3.2.5** — optional members are omitted, never explicit `null`
- `docs/02-technical/SIGNALR_PROTOCOL.md` **§3.2.25** — the `effect summary` omission convention; no effect summary may be added
- `docs/02-technical/SIGNALR_PROTOCOL.md` **§3.2.12 item 1** — the enum member set is exactly the sixteen; no event carries a member outside its own table
- `docs/02-technical/SIGNALR_PROTOCOL.md` **§3.1 item 5** — the client stays non-authoritative; it applies the batch in order and computes no gameplay value
- `docs/02-technical/GAME_EVENTS.md` **§1** — the in-resolution ordering list placing `PowerChanged` (line 94) and `RelicTriggered` (line 97)
- `docs/02-technical/GAME_EVENTS.md` **§2** — the meaning of `RelicTriggered` and `PowerChanged`
- `docs/02-technical/ARCHITECTURE.md` **§2.2.1** — the `GameRuntime` boundary: the runtime forwards the `ReceiveEvents` batch and does not derive gameplay meaning from it
- `docs/02-technical/ARCHITECTURE.md` **§5** — anti-overengineering
- `docs/01-game-design/RELIC_RULES.md` **§7** — `RelicTriggered` reports *that* a Relic applied and *which* one
- `docs/01-game-design/COMBAT_RULES.md` **§2**, **§12** — Power and the 0–100 clamp the event's `power`/`delta` report
- `docs/03-decisions/ADR/ADR-018-structured-relic-trigger-condition-effect-contract.md` — item 10: `RelicTriggered` stays `{ type, relicId }`
- `docs/03-decisions/ADR/ADR-003-phaser-game-runtime.md` — presentation ownership
- `docs/03-decisions/ADR/ADR-001-server-authoritative-battle.md` — server authority

---

## Current State

Verified against the working tree at the time of creation:

```text
src/frontend/client/src/game/scenes/BattleEventPresenter.ts
  - line 4       class comment claims "the twelve non-outcome event types"
  - lines 120–132  union InBattleServerEvent: twelve members
  - lines 148–176  parseInBattleEvent: twelve cases; default returns null
  - lines 417–457  formatInBattleEvent: twelve cases, ending at PetSkillCast
  => 'RelicTriggered' and 'PowerChanged' match no case and are silently dropped.

Backend emission (already present; NOT modified by this task):
  src/backend/GameServer.Application/Battle/BattleStateService.cs:1334-1335
    events.AddRange(relicResolution.Triggered.Select(BattleEvent.ForRelicTriggered));
    events.AddRange(relicResolution.PowerChanges.Select(BattleEvent.ForPowerChanged));
  src/backend/GameServer.Api/Hubs/BattleEventWireProjection.cs:748-749
    BattleEventType.RelicTriggered => ProjectRelicTriggered(...)
    BattleEventType.PowerChanged   => ProjectPowerChanged(...)
```

Client test suites that currently assert the presenter's behavior live in
`src/frontend/client/tests/BattleEventPresentation.test.ts`.

---

## Dependencies

- **TASK-133** (DONE) — implemented the server-authoritative Relic trigger/effect
  resolution and the `RelicTriggered`/`PowerChanged` emission this task presents.
  Its Out of Scope deferred "Client-side Relic presentation or UI" to a separate
  client task (never created).
- **TASK-104** (DONE) — the Product Owner rulings that admitted the four events to
  the closed discriminator and fixed §3.2.20–§3.2.25.
- **TASK-131** (DONE) — D10 confirms `RelicTriggered`'s `{ type, relicId }` shape
  is final, "not a placeholder awaiting a follow-up decision".
- **TASK-088** (DONE) — the existing in-battle event presentation path this task
  extends.

No dependency is unresolved. No other task depends on this one.

---

## Scope

### In Scope

- Add `PresentedRelicTriggered` (`type`, `relicId`) and `PresentedPowerChanged`
  (`type`, `delta`, `power`, `source`) parse results to `BattleEventPresenter.ts`.
- Add both members to the `InBattleServerEvent` union.
- Add a `parseRelicTriggered` and a `parsePowerChanged` that validate the members
  against §3.2.23 / §3.2.24 and return `null` on a malformed payload, matching the
  existing parsers' convention.
- Add both `case` arms to `parseInBattleEvent` so the events are no longer dropped.
- Add both `case` arms to `formatInBattleEvent`, presenting only delivered members
  verbatim.
- Correct the presenter's class comment so its event enumeration and its count
  match what the module actually handles.
- Extend `src/frontend/client/tests/BattleEventPresentation.test.ts` with parse,
  format, malformed-payload, and end-to-end presentation scenarios for both events.
- Surface the two events through the existing `BattleScene` presentation path if,
  and only if, the presenter's returned value is not already rendered generically.

### Out of Scope

- **Any change under `src/backend/`.** The server emission and the wire shapes are
  complete and final (§3.2.23 items 4–5). If a documentation-supported server
  defect is found, STOP per Stop Conditions rather than expanding this task.
- **Any new event, wire member, discriminator value, SignalR method, or
  subscription** (§3.2.12 item 1, §3.2.2 item 5).
- **Any `effect summary` member** on `RelicTriggered` or anywhere else
  (§3.2.23 item 2, §3.2.25).
- **Emitting `PowerChanged` for the `match` or `card` sources.** `GAME_EVENTS.md`
  §2 item 4 records that the `match` and `card` sources "remain their own stages'";
  whether and when those stages emit is a separate backend concern and is not
  settled by this task. This task presents the events it receives and asserts
  nothing about which sources emit.
- **Any Relic definition lookup, Relic rule, trigger, condition, magnitude, or
  effect evaluation on the client** (`GAME_RULES.md` §18, `AGENTS.md` §10,
  `ADR-001`). The client renders; it never resolves.
- **Any Relic UI beyond in-battle event presentation** — no Relic collection
  screen, equip surface, tooltip, icon, or asset pipeline.
- **`ResultScene` reward/XP presentation** — a distinct gap, not owned here.
- **Any change to `GameRuntime` or `SignalRService` port/transport code** — the
  runtime stays opaque to event names (`ARCHITECTURE.md` §2.2.1 rule 4).
- **Any change to `docs/`.** No authoritative contract is amended by this task.
- **A `BoardView` / `PlayerView` / `BossView` / `BattleHUDView` component-hierarchy
  refactor** — scene-local presentation is the smallest correct change
  (`AGENTS.md` §9).
- **Any modification to a completed task file** (`TASK_LIFECYCLE.md` §3).
- **`TurnStarted` / `TurnEnded` / `SwapStarted` / `SwapResolved` / `BattleStarted`
  / `MatchResolved` presentation.** These are not in the closed discriminator
  (§3.2.2 item 2) and are a separate concern. They must not be fabricated.
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2.

---

## Acceptance Criteria

- [x] `'RelicTriggered'` is matched by `parseInBattleEvent` and returns a typed value carrying exactly `type` and `relicId` (`SIGNALR_PROTOCOL.md` §3.2.23)
- [x] `'PowerChanged'` is matched by `parseInBattleEvent` and returns a typed value carrying exactly `type`, `delta`, `power`, and `source` (`SIGNALR_PROTOCOL.md` §3.2.24)
- [x] Neither event reaches the parser's `default` branch, and neither is dropped
- [x] `InBattleServerEvent` includes both new members and remains a discriminated union on `type`
- [x] `formatInBattleEvent` returns a non-empty string for both event types, derived only from delivered members
- [x] `source` is carried as the delivered string and is never converted to a number or an enum ordinal (§3.2.4 item 3)
- [x] `delta` may be negative and is presented as delivered, without recomputation
- [x] A malformed payload (missing `relicId`, or missing any of `delta`/`power`/`source`) returns `null` rather than a partially-populated value
- [x] The presenter still performs zero gameplay calculation: no Power derivation, no accumulation, no Relic condition or effect evaluation (`GAME_RULES.md` §18, `ADR-001`)
- [x] The presenter's class comment enumerates exactly the event types the module handles, and its stated count matches that enumeration
- [x] No event type outside the closed discriminator is added, and no fabricated presentation is produced for one (§3.2.2 item 2)
- [x] `BattleScene` presents both events through its existing path, with no new presentation mechanism introduced
- [x] Zero files under `src/backend/` are modified
- [x] Zero files under `docs/` are modified
- [x] All relevant tests pass at the required validation depth (`core/validation.md` §2)
- [x] Quality review checklist passes (`quality/review.md` §1)
- [x] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001)

---

## Affected Files & Areas

```text
[ ] src/backend/ (Domain / Application / Infrastructure / Api) — NONE (Out of Scope)
[x] src/frontend/client/ (game/scenes/BattleEventPresenter.ts; BattleScene.ts only
                          if the returned value is not already rendered)
[x] tests/ (src/frontend/client/tests/BattleEventPresentation.test.ts)
[ ] docs/ (documentation updates if applicable) — NONE expected (Out of Scope)
```

---

## Implementation Notes

- The module to change is `src/frontend/client/src/game/scenes/BattleEventPresenter.ts`. Follow its existing per-event convention exactly: one `PresentedX` interface, one `parseX` function returning `T | null`, one union member, one `parseInBattleEvent` case, one `formatInBattleEvent` case.
- The existing parsers return `null` for a malformed payload rather than throwing. Keep that convention; do not introduce a second error strategy.
- Read the member set from the §3.2.23 and §3.2.24 tables rather than from this task file — do not add a member the table does not have, and do not omit one it does.
- `PowerChanged.source` is one of `"match"`, `"card"`, or `"relic"` (§3.2.24). It is a delivered string; do not validate it against a client-side enum, and do not reject an unrecognized value — present what was delivered.
- `PowerChanged.delta` is a **signed** integer (§3.2.24, "the signed change applied to `PetState.Power`"). Do not format it as an absolute value and do not recompute it from `power`.
- `RelicTriggered` carries exactly two members and no effect summary (§3.2.23 items 2, 5). Do not attempt to describe the Relic's effect; the resulting state reaches the client through the `BattleState` projection (§4).
- `relicId` is the Relic **instance** identity, not a definition id or a display name (`RELIC_RULES.md` §2.2 item 3). Present it as delivered; do not map it to a human-readable label, which would require a client-side definition registry the architecture does not have.
- The events arrive inside the existing `ReceiveEvents` batch and must be presented in the delivered array order — the array is already in the resolution's order (§3.2.1 item 3, §3.2.12 item 4). Do not sort, group, or reorder them.
- Do not route these through `GameRuntime`. The runtime is deliberately opaque to event names and forwards the batch unchanged (`ARCHITECTURE.md` §2.2.1 rule 4).
- Check `BattleScene.ts` first: if it renders every value the presenter returns through one generic path, no `BattleScene.ts` change is required and none should be made.
- Update the class comment at the top of the file in the same change — its "twelve non-outcome event types" claim and its numbered enumeration become wrong once these two land.

---

## Testing Requirements

### Required Verification

```text
[x] Unit tests         — parseRelicTriggered / parsePowerChanged accept a
                         well-formed payload and return null for a malformed one
[x] Integration tests  — the presenter's output for both events reaches the
                         BattleScene presentation path within a delivered batch
[x] Gameplay scenarios — See "Key Edge Cases" below (Given/When/Then)
```

### Key Edge Cases

- **`RelicTriggered` with the documented payload** (`{ type, relicId }`) parses and formats, per §3.2.23.
- **`RelicTriggered` with no `relicId`** returns `null` — no partial value.
- **`RelicTriggered` carrying an unexpected `effectSummary` member** is still presented from its documented members only; the extra member is not surfaced (§3.2.23 item 2, §3.2.25).
- **`PowerChanged` positive** (`delta: 25, power: 45, source: "card"`) formats with the sign and value as delivered.
- **`PowerChanged` negative** (a Card cost spend) formats the negative `delta` as delivered, without an absolute-value conversion.
- **`PowerChanged` with each of the three documented `source` values** (`match`, `card`, `relic`) is presented without rejection.
- **`PowerChanged` missing any of `delta` / `power` / `source`** returns `null`.
- **A batch containing `RelicTriggered` and `PowerChanged` interleaved with the twelve existing types** presents all events in the delivered order, with neither new type dropped.
- **An event type outside the closed discriminator** (e.g. `TurnStarted`, `MatchResolved`) still returns `null` and produces no fabricated presentation (§3.2.2 item 2, §3.2.12 item 1).
- **No outcome-event regression**: `BattleWon` / `BattleLost` remain handled by their existing outcome path and are unaffected.

---

## Stop Conditions

Universal stop conditions in `AGENTS.md` §20 and `.ai/README.md` §13 apply.

- If the `RelicTriggered` or `PowerChanged` wire member set is found to disagree between `SIGNALR_PROTOCOL.md` §3.2.23/§3.2.24 and the backend projection: STOP per `AGENTS.md` §4 — report the conflict, do not pick a side.
- If presenting an event appears to require a client-side gameplay computation (Power derivation, delta recomputation, Relic effect resolution): STOP per `AGENTS.md` §10.
- If the work appears to require a new event, wire member, discriminator value, or `effect summary`: STOP — §3.2.12 item 1 and §3.2.23 items 2/5 fix those surfaces.
- If `PowerChanged` for the `match` or `card` source is found to be required by a document to be emitted and is not: STOP and report — that is a backend emission question outside this task's scope (`GAME_EVENTS.md` §2 item 4).
- If `BattleScene` requires a presentation-mechanism redesign rather than an extension of the existing path: STOP and decompose (`tasks/README.md` §13).
- If task exceeds 7 skills or crosses multiple uncoupled architectural boundaries: STOP & decompose.

---

## Completion Evidence

<!--
  TO BE COMPLETED BY THE EXECUTING AGENT after reaching DONE.
  Keep concise and factual.
-->

### Changed Files
- `src/frontend/client/src/game/scenes/BattleEventPresenter.ts` — added `PresentedRelicTriggered` (`type`, `relicId`) and `PresentedPowerChanged` (`type`, `delta`, `power`, `source`); added both to the `InBattleServerEvent` union; added `parseRelicTriggered` and `parsePowerChanged` (returning `null` on a malformed payload, matching the existing convention); added both `case` arms to `parseInBattleEvent` and to `formatInBattleEvent`; corrected the class comment's event count and enumeration (twelve → fourteen) and its §3.2.x range.
- `src/frontend/client/tests/BattleEventPresentation.test.ts` — added parser/formatter tests for both events (verbatim members, negative `delta`, all three `source` values, string-typed `source`, `effectSummary` non-surfacing, malformed payloads returning `null`), two end-to-end `ReceiveEvents` presentation cases, a delivered-order interleaving test, and a not-dropped-at-default regression test.
- `tasks/completed/TASK-148-present-relic-triggered-and-power-changed-in-battle-scene.md` — this task file (moved from `backlog/`).

### Validation Results
- `npx vitest run tests/BattleEventPresentation.test.ts` — PASS (48 tests)
- `npm run test:run` — PASS (506 tests, 18 files)
- `npx tsc --noEmit -p tsconfig.json` — PASS (exit 0, zero diagnostics)
- `npm run build` — PASS (exit 0; 138 modules transformed, `dist/assets/index-CDm6zPDm.js` 2,083.89 kB). The stderr `/*#__PURE__*/` annotation notices and the >500 kB chunk-size warning originate from the third-party `@microsoft/signalr` bundle and pre-date this task; this change adds no dependency.

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic — the presenter performs no Power derivation, no `delta` recomputation, no Relic condition/effect evaluation, and no RNG
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1 — Relics and Combat/Power are IN)
- [x] Confirmed zero files under `src/backend/` modified
- [x] Confirmed zero files under `docs/` modified
- [x] Confirmed `GameRuntime.ts` and `SignalRService.ts` unmodified — no routing gap existed; the `ReceiveEvents` batch was already forwarded to the scene intact
- [x] Confirmed no new event, wire member, discriminator value, SignalR method, or `effect summary`
