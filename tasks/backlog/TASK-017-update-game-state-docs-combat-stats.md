# TASK-017 — Update GAME_STATE.md to Reflect Implemented Combat Stats

---

## Metadata

```text
Task ID:           TASK-017
Type:              DOCUMENTATION
Status:            BACKLOG
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

- [ ] `GAME_STATE.md` §2.2 line 791 accurately reflects that HP/MaxHP, ATK/DEF/Crit, Power, and Crit are now implemented
- [ ] `GAME_STATE.md` §2.2 lines 801-806 are updated to remove "not yet implemented" for the combat stats that are now implemented
- [ ] Documentation clearly distinguishes between implemented combat stats and still-deferred systems (StatusEffects, EquippedRelics, EquippedCards)
- [ ] All relevant tests pass at the depth required by `core/validation.md §2` for the task's Risk level
- [ ] `quality/review.md §1` checklist passes
- [ ] Documentation impact addressed (§ Documentation Impact below)

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
<What was accomplished.>

### Changes
<Files modified/created/deleted.>

### Tests
<Tests added or updated, and their results.>

### Documentation Consulted
<Docs read during execution, with sections.>

### Documentation Changed
<Docs updated as part of this task, or "None".>

### Validation
<What was validated and at what depth (core/validation.md).>

### Risks
<Known risks or unresolved issues remaining.>

### Remaining Issues
<Issues discovered but not in scope of this task, per AGENTS.md §16.>

### Agent
<Which agent(s) executed the task.>

### Workflow Used
<Which workflow was used.>

### Skills Used
<Which skills were invoked.>

### Status
DONE | BLOCKED

---

## Handoff

<What was completed, what remains, known risks, validation status.>
