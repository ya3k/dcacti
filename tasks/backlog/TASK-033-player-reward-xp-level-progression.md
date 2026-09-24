# TASK-033 — Player Reward XP / Level Progression

---

## Metadata

```text
Task ID:           TASK-033
Type:              FEATURE
Status:            BLOCKED
Risk:              MEDIUM
Priority:          HIGH
Primary Agent:     persistence
Supporting Agents: backend, gameplay, testing, review
Workflow:          development/feature.md
Skills:            discovery/documentation-discovery, backend/persistence-analysis, testing/test-scenario-generation, quality/scope-validation
Dependencies:      TASK-023
```

---

## BLOCKED — Required Design Inputs Missing

This task is **BLOCKED** and must not be implemented until a design decision
explicitly defines every item below. None of these values exists in any
authoritative document, and **this task must not invent them** (`AGENTS.md` §7).

The authoritative documents state the mechanism and then explicitly refuse to
define its magnitude:

```text
ADR-012 item 2      "Exact XP amounts/curve are balance/config concerns and
                     are not owned by this ADR"
ADR-012 Consequences "Player Level progression mechanism (Reward XP) is stated
                     at the mechanism level only; exact XP numbers remain a
                     future balance task."
PET_RULES.md §5(2)  "the exact XP curve is a balance/config concern and is not
                     defined in this document"
PET_RULES.md §5(6)  "Exact level curve (linear/exponential/tabled) is a balance
                     concern defined in COMBAT_RULES.md / config, not here."
MVP_SCOPE.md §1     "Exact XP curve is a balance concern, not a scope item."
DATABASE.md §1      RewardSummary "may include Player XP granting Player Level"
                     — permissive, not normative
```

### Required decisions before this task may start

```text
[ ] Player XP persistence       — does XP persist on Player? If yes, an XP column
                                  must be added to DATABASE.md §1 BEFORE code
                                  (AGENTS.md §17). DATABASE.md §1 currently
                                  defines Player as exactly PlayerId,
                                  DiscordUserId, Level, CreatedAt.
[ ] Battle Reward → XP grant    — how much XP does a won battle grant? (amount)
[ ] XP level-up curve           — the function from XP to Level
                                  (linear / exponential / tabled)
[ ] Level-up threshold          — how much XP each Level requires
[ ] Level cap = 50              — confirmed by ADR-012 item 4; the curve must
                                  respect it
```

### Why blocking was necessary

The predecessor task (TASK-023) was originally scoped to include this work and
was BLOCKED. Beyond the missing amounts above, the earlier task required
resolving whether XP persists across sessions, and **both branches were
unsatisfiable**:

```text
XP persists      → requires an XP column that DATABASE.md §1 does not define
                   and ADR-012 does not authorize (AGENTS.md §17 forbids
                   adding it before the design does)
XP session-scoped → XP resetting every session could never accumulate enough to
                   raise a [1, 50] Level, contradicting ADR-012 item 2
```

No third representation is documented. The design must therefore settle XP
persistence explicitly.

---

## Objective

Once the design decisions above are recorded in the authoritative documents,
implement Player Level progression driven by battle Rewards: the `BattleWon`
path grants Player XP per the documented amount, Player Level advances per the
documented curve, and the result is clamped to the documented `[1, 50]` range.

---

## Authoritative References

- `docs/03-decisions/ADR/ADR-012-player-level-pet-level-ownership.md` items 1–2, 4 — Player Level definition, Rewards increase mechanism, range clamp
- `docs/01-game-design/PET_RULES.md` §5 items 2, 3, 6 — no Pet XP; Player Level carries no combat stats; curve is a balance concern
- `docs/00-overview/GDD.md` §14 — Meta Progression; Player Level increases through battle Rewards
- `docs/00-overview/MVP_SCOPE.md` §1 — Player Level (1–50) listed IN
- `docs/02-technical/DATABASE.md` §1, §3 — Player fields and `Level ∈ [1, 50]`
- `docs/02-technical/GAME_EVENTS.md` §2 — `BattleWon` / `BattleLost`
- `docs/03-decisions/ADR/ADR-011-player-owner-pet-combat.md` item 5 — no Player combat stats

---

## Scope

### In Scope (after unblocking)
- Player XP persistence, if and only if the design decides XP persists
- Reward → XP grant on the `BattleWon` path
- XP → Player Level advancement per the documented curve
- Clamping the resulting Level to `[1, 50]`
- Unit/integration tests for grant amounts, curve boundaries, and cap behavior

### Out of Scope
- Player combat stats of any kind (ADR-011 item 5 — combat stats live on `PetState`)
- **Pet XP** — no such system exists (`PET_RULES.md` §5 item 2, ADR-012 item 6)
- **Evolution** — out of scope (ADR-012 item 6)
- Pet Level derivation (TASK-024)
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2

---

## Current State

No Reward or XP system exists. `BattleStateService`
(`src/backend/GameServer.Application/Battle/BattleStateService.cs`) records the
`BattleWon` event on the Boss-death path but grants nothing. TASK-023
establishes the Player entity and `Player.Level` persistence; this task builds
the progression on top of it.

---

## Acceptance Criteria

*(Provisional — every value below must be replaced by reference to the
authoritative document section that defines it once the design decision is
recorded. No number may be chosen by the implementing agent.)*

- [ ] Player XP persistence matches the documented decision (column present iff the design says XP persists, added to `DATABASE.md` §1 first per `AGENTS.md` §17)
- [ ] A won battle grants Player XP per the documented amount (`<doc §>`)
- [ ] Player Level advances per the documented curve and threshold (`<doc §>`)
- [ ] Player Level is clamped to `[1, 50]` (`DATABASE.md` §3, ADR-012 item 4)
- [ ] The grant occurs server-side on the `BattleWon` path only (`AGENTS.md` §10, ADR-001)
- [ ] Zero combat-stat columns exist on Player; the grant adds no combat stats (ADR-011 item 5)
- [ ] No Pet XP and no Evolution is introduced (ADR-012 items 6)
- [ ] All relevant tests pass at the required validation depth (`core/validation.md` §2)
- [ ] Quality review checklist passes (`quality/review.md` §1)
- [ ] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001)

---

## Affected Files & Areas

```text
[x] src/backend/ (Domain / Application / Infrastructure / Api)
[ ] src/frontend/client/
[x] tests/ (unit / integration / gameplay scenarios)
[x] docs/ (DATABASE.md §1 — only if the design decides XP persists)
```

---

## Implementation Notes

- Documented entry point: the `BattleWon` path in
  `src/backend/GameServer.Application/Battle/BattleStateService.cs`. Grant
  server-side there — never client-side (`AGENTS.md` §10).
- Any XP amount or curve must arrive as configuration, consistent with how
  `PetLevelMultiplier` is treated (`DATABASE.md` §1: "config — never
  hard-coded").
- Do not add a second progression mechanism alongside the documented one.
- This task depends on TASK-023 for the Player entity and `Player.Level`
  persistence. Do not re-implement either.

---

## Testing Requirements

### Required Verification
```text
[ ] Unit tests         — grant amount, curve/threshold boundaries, Level clamp at
                         1 and 50
[ ] Integration tests  — BattleWon persists the advanced Level; XP column
                         round-trip (if XP persists)
[ ] Gameplay scenarios — Given a won battle, When Rewards resolve, Then Player
                         Level advances per ADR-012 item 2 and the Player gains
                         no combat stats
```

### Key Edge Cases
- Level cap boundary: advancing at Level 50 must not exceed the documented cap (ADR-012 item 4)
- Level floor: Level must never fall below the documented minimum
- Multiple consecutive wins accumulate per the documented curve
- A lost battle does not grant progression (only `BattleWon` is the documented path)

---

## Stop Conditions

- **While BLOCKED: any attempt to implement is itself the violation.** Do not choose an XP amount, curve, threshold, or persistence decision.
- If any required design value is still undefined when work resumes: STOP per `AGENTS.md` §7
- If adding XP persistence requires a `DATABASE.md` §1 change that has not been approved and applied first: STOP per `AGENTS.md` §17
- If implementation requires Player combat stats: STOP per ADR-011 item 5
- If task requires client-authoritative game logic calculation: STOP per `AGENTS.md` §10
- If task exceeds 7 skills or crosses multiple uncoupled architectural boundaries: STOP & decompose

---

## Completion Evidence

### Changed Files
- `<file path>` — <summary of change>

### Validation Results
- `<test command or suite>` — PASS (<N> tests)

### Server Authority & Scope Verification
- [ ] Confirmed zero client-authoritative gameplay logic
- [ ] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
- [ ] Confirmed no Pet XP and no Evolution introduced (ADR-012 item 6)
