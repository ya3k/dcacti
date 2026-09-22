# TASK-017 — Update GAME_STATE.md to Reflect Implemented Combat Stats

---

## Metadata

```text
Task ID:           TASK-017
Type:              DOCUMENTATION
Status:            DONE
Risk:              LOW
Priority:          HIGH
Primary Agent:     gameplay
Supporting Agents: N/A
Workflow:          documentation/documentation-change.md
Skills:            documentation-consistency
Dependencies:      TASK-016
```

---

## Objective

Update `docs/02-technical/GAME_STATE.md` §2.2 to accurately reflect that combat stats (HP, MaxHP, ATK, DEF, Power, Crit) are now implemented in `PlayerState`, correcting the stale documentation that describes them as "not yet implemented".

---

## Context

TASK-016 (Add Combat Stats to PlayerState) implemented combat stats in the domain model and tests. However, `GAME_STATE.md` §2.2 still contains outdated text stating that only `Combo` and `MatchCount` are implemented. This creates a documentation drift that could confuse future agents and violate `AGENTS.md` §17 (documentation must remain consistent with implementation).

---

## Authoritative Sources

- `docs/02-technical/GAME_STATE.md` §2.2 (lines 773-822) — current documentation with stale text
- `src/backend/GameServer.Domain/Battle/PlayerState.cs` — authoritative domain model showing implemented combat stats
- `tests/backend/GameServer.Domain.Tests/MatchComboAccountingTests.cs` — test coverage verifying combat stats initialization

---

## Scope

### In Scope

- Update `GAME_STATE.md` §2.2 line 791 to reflect that combat stats are now implemented
- Update `GAME_STATE.md` §2.2 lines 801-806 to remove "not yet implemented" language for HP/MaxHP, ATK/DEF/Crit, Power

### Out of Scope

- Any code changes
- Wire contract changes (intentionally deferred per staged implementation)
- New game rules or mechanics
- Any system listed in `docs/00-overview/MVP_SCOPE.md` §2 (OUT)

---

## Current State

`GAME_STATE.md` §2.2 contains:
- Line 791: "**Implemented so far: `Combo` and `MatchCount`.**"
- Lines 801-806: "The remaining members above are **not yet implemented** — not **not required** (§0 item 4): `HP`/`MaxHP`, `ATK`/`DEF`/`Crit`, `Power`, `StatusEffects[]`, `EquippedRelics[]`, and `EquippedCards[]`..."

However, `PlayerState.cs` now includes:
- `HP = 1000`, `MaxHP = 1000`, `ATK = 50`, `DEF = 25`, `Power = 0`, `Crit = 5`
- Tests verify these defaults exist

---

## Acceptance Criteria

- [x] `GAME_STATE.md` §2.2 line 791 accurately reflects that HP/MaxHP, ATK/DEF/Crit, Power, and Crit are now implemented
- [x] `GAME_STATE.md` §2.2 lines 801-806 are updated to remove "not yet implemented" for the combat stats that are now implemented
- [x] Documentation clearly distinguishes between implemented combat stats and still-deferred systems (StatusEffects, EquippedRelics, EquippedCards)
- [x] All relevant tests pass at the depth required by `core/validation.md §2` for the task's Risk level
- [x] `quality/review.md §1` checklist passes
- [x] Documentation impact addressed (§ Documentation Impact below)

---

## Affected Areas

```text
[ ] Domain (GameServer.Domain/) — which module(s)?
[ ] Application (GameServer.Application/)
[ ] API (GameServer.Api/)
[ ] Client (client/)
[ ] SignalR / Redis (GameServer.Infrastructure/SignalR/, /Redis/)
[ ] PostgreSQL (GameServer.Infrastructure/Postgres/)
[ ] Tests (tests/)
[x] Documentation (docs/)
[ ] ADR (docs/03-decisions/ADR/)
```

---

## Implementation Notes

This is a documentation-only task. The agent must:

1. Read `GAME_STATE.md` §2.2 to understand the current stale text
2. Read `PlayerState.cs` to confirm the implemented combat stats and their default values
3. Update line 791 to reflect that combat stats are now implemented
4. Update lines 801-806 to remove "not yet implemented" for combat stats while preserving the distinction for still-deferred systems (StatusEffects, EquippedRelics, EquippedCards)
5. Ensure the documentation accurately reflects the staged implementation plan

---

## Testing Requirements

### Test Types Required

```text
[ ] Unit tests         — <which components/functions>
[ ] Integration tests  — <which boundaries>
[ ] Gameplay scenarios — <which state-transition chain, per
                          GAME_RULES.md §17 order where applicable>
[ ] API tests          — <which endpoints, per API_CONTRACTS.md>
[ ] Realtime tests     — <which Hub methods/events, per
                          SIGNALR_PROTOCOL.md>
[ ] Persistence tests  — <which schema constraints, per DATABASE.md>
```

### Key Edge Cases

- Ensure documentation accurately reflects the staged implementation plan
- Preserve the distinction between implemented combat stats and still-deferred systems
- Maintain consistency with `SIGNALR_PROTOCOL.md` §4.2 (wire contract intentionally defers combat stats)

---

## Documentation Impact

**Option B — Update existing doc:**
> `docs/02-technical/GAME_STATE.md` §2.2 must be updated because the current text describes combat stats as "not yet implemented" when they are now implemented in TASK-016.
> Use `documentation/documentation-change.md`.

---

## Stop Conditions

- If the required behavior cannot be fully derived from the
  Authoritative Sources listed above: STOP per `AGENTS.md §7`
- If the documentation update creates conflicts with other authoritative documents: STOP per `AGENTS.md §4`

---

## Dependencies

- TASK-016 (Add Combat Stats to PlayerState) — must be DONE before this documentation update

---

## Completion Evidence

### Summary

Corrected the documentation drift TASK-016 introduced in
`docs/02-technical/GAME_STATE.md`. §2.2 now states accurately that the combat
stats `HP`, `MaxHP`, `ATK`, `DEF`, `Power`, and `Crit` are implemented in the
Domain `PlayerState` alongside `Combo` and `MatchCount`, and that
`StatusEffects[]`, `EquippedRelics[]`, and `EquippedCards[]` remain not yet
implemented.

The task file's "Current State" section described §2.2 as still carrying
"Implemented so far: `Combo` and `MatchCount`" at line 791. That specific text
had in fact **already been updated** (presumably by TASK-016), so the edit was
narrowed to the two places that were still genuinely stale, plus explicit
implemented/deferred and Domain-vs-wire markers:

1. **§2.0.5.3 (the drift the task's stated scope did not name).** It still
   listed `HP/MaxHP, ATK/DEF/Crit, Power` under "*Owned by later stages*" as
   part of "the remainder of `PlayerState`" — i.e. as not yet implemented.
   After TASK-016 that list is wrong, and §2.0.5.3 is the section the rest of
   the doc set reads to answer "what exists at this stage" (it is cited by
   `REDIS_STATE.md` §7 item 4). Corrected to defer only the three collection
   members and to record that the combat stats are now implemented.
2. **§2.2 field tree — `Combo` comment.** Described `Combo` as "current Combo
   for the **in-progress** Swap", which contradicts the implemented type and
   the owning rule: `PlayerState.Combo` holds the value of the most recently
   **committed** Swap (`PlayerState.cs`), and `GAME_RULES.md` §5 item 3 /
   `MATCH3_RULES.md` §6.1 item 2 put the reset at the start of a committed
   Swap, with the value written back only after resolution (§5.1). Corrected to
   "the Swap that just committed and resolved".

No value, rule, formula, or wire contract was invented or changed.

### Changes

Modified:
- `docs/02-technical/GAME_STATE.md` §2.0.5.3 — removed `HP/MaxHP`,
  `ATK/DEF/Crit`, `Power` from the "Owned by later stages" remainder of
  `PlayerState`; recorded them as implemented.
- `docs/02-technical/GAME_STATE.md` §2.2 — field tree now marks each member
  `implemented in the Domain model` / `not yet implemented`; `Combo` comment
  corrected from "in-progress" to "just committed and resolved"; the
  implemented/deferred summary sentence rewritten; a new sentence states
  explicitly that **Domain state implemented ≠ client wire delivery**; the
  wire paragraph now names that only `Combo` and `MatchCount` of the eight
  implemented members reach the client.

No files created or deleted. No code, test, SignalR, API-payload, Redis, or
database file was touched.

### Tests

None added, none modified, none required — this is a documentation-only task
(`TASK_TYPES.md`, DOCUMENTATION) and `core/validation.md` §2 selects no test
creation at LOW risk. Existing coverage was re-run as regression evidence:

- `dotnet build src/backend/GameServer.sln` → **Build succeeded, 0 Warning(s),
  0 Error(s)**
- `dotnet test tests/backend/GameServer.Domain.Tests` (full suite) →
  **645 passed, 0 failed, 0 skipped**
- `dotnet test ... --filter MatchComboAccountingTests` →
  **32 passed, 0 failed** (the suite that pins the combat-stat fields, their
  `COMBAT_RULES.md` §1.1 defaults, and the `PlayerState` field set)

### Documentation Consulted

- `AGENTS.md` — §2 (precedence), §4 (conflict resolution), §6 (required
  reading), §16 (task discipline), §17 (documentation change rule), §20 (stop
  conditions), §22 (Definition of Done)
- `.ai/README.md` — §6 (source of truth), §13 (stop conditions), §18
  (code-vs-docs classification), §21 (output contract)
- `.ai/agents/orchestrator.md`, `.ai/agents/review.md`,
  `.ai/agents/testing.md`
- `.ai/workflow/documentation/documentation-change.md` — §1 flow, §2 no
  duplication, §3 canonical owner, §4 composition
- `.ai/workflow/core/validation.md` §2 (LOW depth), `.ai/workflow/core/completion.md` §1
- `.ai/workflow/quality/review.md` §1, `.ai/workflow/quality/testing.md`
- `.ai/skills/quality/documentation-consistency.md` — Modes A/B/C
- `docs/02-technical/GAME_STATE.md` — §0 (staged implementation), §1, §2,
  §2.0.5.3, §2.1.10 item 9, §2.2, §2.3, §2.4, §5.1
- `docs/02-technical/SIGNALR_PROTOCOL.md` — §4 item 4, §4.1, §4.2, §4.3
- `docs/02-technical/ARCHITECTURE.md` — §1 (project structure / Domain layer)
- `docs/02-technical/REDIS_STATE.md` — §7 items 4 and 8
- `docs/01-game-design/COMBAT_RULES.md` — §1.1, §2, §3 (ownership of values)
- `docs/01-game-design/MATCH3_RULES.md` — §6.1, §6.2, §6.3, §6.5
- `docs/01-game-design/GAME_RULES.md` — §3, §5, §12, §18
- `src/backend/GameServer.Domain/Battle/PlayerState.cs`
- `tests/backend/GameServer.Domain.Tests/MatchComboAccountingTests.cs`
- `tasks/completed/TASK-016-add-combat-stats-to-playerstate.md` (dependency check)
- `tasks/TASK_LIFECYCLE.md`

### Documentation Changed

- `docs/02-technical/GAME_STATE.md` §2.0.5.3 and §2.2 (only file changed).

Not changed, as required by the task's wire-contract boundary:
`SIGNALR_PROTOCOL.md`, `BattleStateUpdated`, `PlayerStatePayload`,
`RuntimePlayerState`, `SignalRService`, `GameRuntime`, `BattleHub`.

### Validation

Depth: **LOW** (`core/validation.md` §2) — build/compile validation, focused
tests, and review.

Checked per the task's §6 list:

1. Re-read the modified `GAME_STATE.md` §2.2 — done.
2. Every documented combat field matches `PlayerState.cs` — verified. The type
   declares exactly eight instance members: `HP`, `MaxHP`, `ATK`, `DEF`,
   `Power`, `Crit`, `Combo`, `MatchCount` — the same eight §2.2 now lists as
   implemented. `MatchComboAccountingTests` asserts this exact field set.
3. Default values match `COMBAT_RULES.md` §1.1 — verified:
   `HP`=1000, `MaxHP`=1000, `ATK`=50, `DEF`=25, `Power`=0, `Crit`=5.
   §1.1 gives no Power default beyond the 0–100 range; `PlayerState.cs`
   documents 0 as the range floor because no Match has generated Power at
   creation, and §2.2 reproduces that value without inventing one.
4. The documentation does not claim these fields are delivered over SignalR —
   verified. §2.2 now states the opposite explicitly, and
   `SIGNALR_PROTOCOL.md` §4.2 (unmodified) still fixes `playerState` to
   exactly `combo` and `matchCount`.
5. Deferred fields remain correctly described — verified:
   `StatusEffects[]`, `EquippedRelics[]`, `EquippedCards[]` are marked not yet
   implemented in both §2.2 and §2.0.5.3, and remain stubbed nowhere.
6. Minimum validation for the documentation workflow — run: build succeeded,
   645/645 Domain tests pass, review checklist applied.

`quality/review.md` §1: Correctness PASS (matches `PlayerState.cs` and
`COMBAT_RULES.md` §1.1) · Architecture PASS (no layer touched, no dependency
direction changed) · Scope PASS (one document; no code, tests, SignalR, API,
Redis, or database) · Tests PASS (existing suite green; none required) ·
Documentation PASS (drift corrected; no duplicate definition introduced — §2.2
cites `COMBAT_RULES.md` §1.1 as owner rather than restating a new rule) ·
Security N/A · Performance N/A · Maintainability PASS (no new abstraction) ·
Determinism PASS (server authority unchanged; no value or RNG rule altered).

No stop condition fired: TASK-016 is DONE (`tasks/completed/`, `Status: DONE`);
`PlayerState.cs` contains every documented field; defaults match
`COMBAT_RULES.md` §1.1; no authoritative-document conflict was introduced; no
signalR semantics were changed; no code change was needed; no new gameplay rule
was discovered; no architectural decision was required.

### Risks

- **Documentation-only change.** No runtime or wire behavior is affected; no
  risk to gameplay.
- The version header of `GAME_STATE.md` remains `1.6` with its
  `PetState.PassiveId` note. That line describes the last *contract* version,
  and this task adds no field to §2 — it corrects descriptive prose only — so
  it was deliberately left unchanged rather than inventing a version bump the
  document does not define a policy for.

### Remaining Issues

Discovered but **out of scope**, reported per `AGENTS.md` §16 rather than fixed:

1. **`docs/02-technical/REDIS_STATE.md` §7 item 4 (line ~161)** still states
   "`PlayerState`, `PetState`, and `BossState` do not exist, so §2's full shape
   still cannot be produced (`GAME_STATE.md` §2.0.5.3)." `PlayerState` and
   `PetState` **do** now exist; only `BossState` does not. The same stale claim
   appears in the §7 stage table at **line ~133-134** ("PlayerState, PetState,
   and BossState still do not exist").
   - Location: `docs/02-technical/REDIS_STATE.md` §7 item 4 and its stage table.
   - Impact: the *conclusion* (Redis persistence stays deferred) is still
     correct, because `BossState` is still absent and no battle can be created;
     only the stated reason is stale. `GAME_STATE.md` §2.2 already records the
     corrected position.
   - Suggested follow-up task: a small DOCUMENTATION task to refresh the
     `REDIS_STATE.md` §7 existence claim to name `BossState` alone.

### Agent

`gameplay` (primary, per the task's Metadata). Orchestration, context
discovery, and completion verification per `.ai/agents/orchestrator.md`;
documentation-consistency review per `.ai/agents/review.md`.

### Workflow Used

`.ai/workflow/documentation/documentation-change.md` (primary — the task's
assigned workflow), composed with `core/validation.md` for depth selection,
`quality/testing.md` for the regression run, `quality/review.md` for the
checklist, and `core/completion.md` §1 for the Definition of Done.

### Skills Used

- `quality/documentation-consistency.md` (Modes A, B, C — conflict check,
  docs-vs-code classification, and post-change consistency validation)
- `discovery/impact-analysis.md` (width of the §2.2 change across referencing
  documents)
- `quality/scope-validation.md` (confirming the change stayed inside the task
  boundary)
- `quality/implementation-review.md` (review checklist application)

### Status

DONE

---

## Handoff

**Completed.** `docs/02-technical/GAME_STATE.md` no longer describes the combat
stats as unimplemented. §2.2 lists all eight implemented `PlayerState` members
(`HP`, `MaxHP`, `ATK`, `DEF`, `Power`, `Crit`, `Combo`, `MatchCount`) at their
`COMBAT_RULES.md` §1.1 MVP defaults (`HP`=`MaxHP`=1000, `ATK`=50, `DEF`=25,
`Power`=0, `Crit`=5), keeps `StatusEffects[]`, `EquippedRelics[]`, and
`EquippedCards[]` marked as not yet implemented, and states explicitly that
these fields are Domain state that is **not** delivered on the `playerState`
wire payload. §2.0.5.3 no longer lists the combat stats as owned by a later
stage.

**Validation status.** Build succeeded (0 warnings, 0 errors); 645/645 Domain
tests pass (32/32 in `MatchComboAccountingTests`); `quality/review.md` §1
checklist PASS on every applicable item.

**What remains.** Nothing in this task. The wire delivery of the combat stats
is intentionally **not** implemented and belongs to a separate, future
protocol task (`SIGNALR_PROTOCOL.md` §4.2 is deliberately unchanged).

**Known risks.** None material — documentation-only. The
`REDIS_STATE.md` §7 existence claim noted under Remaining Issues is a
pre-existing, separate drift; its deferral conclusion is still correct.

**No follow-up task was created**, per the task lifecycle instruction.

