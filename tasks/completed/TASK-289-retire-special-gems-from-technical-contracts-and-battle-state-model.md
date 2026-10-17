# TASK-289 — Retire Special Gems and Match-Tier Multipliers from Technical Contracts and Battle-State Model

---

## Metadata

```text
Task ID:           TASK-289
Type:              DOCUMENTATION
Status:            DONE
Risk:              HIGH
Priority:          HIGH
Primary Agent:     architecture
Supporting Agents: backend, realtime, persistence, review
Workflow:          documentation/documentation-change.md
Skills:            quality/documentation-consistency, architecture/data-contract-evolution,
                   discovery/documentation-discovery, quality/implementation-review
Dependencies:      ADR-028 (Accepted); TASK-286 (completed); TASK-287 (completed);
                   OD-1…OD-7 + B-1…B-3 (operator-approved)
Declared Files:    docs/02-technical/GAME_STATE.md,
                   docs/02-technical/SIGNALR_PROTOCOL.md,
                   docs/02-technical/REDIS_STATE.md,
                   docs/02-technical/GAME_EVENTS.md,
                   docs/02-technical/TDD.md,
                   docs/02-technical/ARCHITECTURE.md
```

---

## Objective

Reconcile the authoritative technical documentation in `docs/02-technical/` with architectural decision ADR-028 and approved game-design decisions OD-1 through OD-7, formally retiring Special Gem metadata, wire payloads, event members, and persistence definitions from the battle-state model.

This is a **technical documentation and specification** task. Runtime code is **not** modified in this task.

---

## Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1 — MVP scope boundaries
- `docs/01-game-design/GAME_RULES.md` §3, §7, §17 — reconciled match and combat rules
- `docs/01-game-design/MATCH3_RULES.md` §1.A, §4.1, §5, §5.7 — retired Special Gem mechanics and unified match rates
- `docs/01-game-design/COMBAT_RULES.md` §2, §3 — base output values and confirmed ATK match gating
- `docs/02-technical/GAME_STATE.md` §2.1, §3 — board state and transient resolution state
- `docs/02-technical/SIGNALR_PROTOCOL.md` §3.2 — event payloads and wire schemas
- `docs/02-technical/REDIS_STATE.md` §3.10 — active battle persistence and tolerant deserialization
- `docs/03-decisions/ADR/ADR-028-retire-special-gems-from-battle-state-model.md` — governing architectural decision

---

## Scope

### In Scope

- `GAME_STATE.md`:
  - §2.1.1, §2.1.2, §2.1.3, §2.1.4: Retire `SpecialGem` metadata and orientation from `BoardState.Cells[64]`. Each cell contains only `GemType`.
  - §2.1.7: Update cell occupancy and immutability invariants to pure `GemType` entries.
  - §3: Remove `CreatedSpecialGems` and `ConsumedSpecialGems` from Transient Resolution State.
  - §5: Update state transition documentation to reflect pure match resolution.
- `SIGNALR_PROTOCOL.md`:
  - §3.2.6 (`MatchCreated`): Remove `createdSpecialGems` array from the payload table and example.
  - §3.2.11: Retire section `createdSpecialGems Entry Shape`.
  - §3.2.13 (`CellPayload`): Retire optional `specialGem?: SpecialGemPayload | null` field.
  - §8: Ensure protocol compatibility rules document no wire version negotiation.
- `REDIS_STATE.md`:
  - §3.10: Reconcile Special Gem section to record retirement from `Cells[64]`.
  - Document the Decision B-2 tolerance basis (`BattleStateSerializer` default System.Text.Json configuration safely ignores legacy `cell.specialGem` fields without runtime error).
  - Document the Decision B-2 deployment policy: drain active battles before joint server/client deployment; do not mutate, discard, or migrate Redis state.
- `GAME_EVENTS.md`:
  - Remove all references to Special Gem creation or activation from event specifications.
- `TDD.md` and `ARCHITECTURE.md`:
  - Reconcile Match-3 module descriptions and pipeline stages to remove Special Gem planners and effect resolvers.

### Out of Scope

- Any `src/` file.
- Any `tests/` file.
- Runtime C# code removal in `GameServer.Domain` or `GameServer.Api` (owned by downstream implementation tasks).
- Frontend client presentation removal (owned by downstream frontend implementation task).

---

## Current State

`ADR-028` is Accepted. Game-design documents (`GAME_RULES.md`, `MATCH3_RULES.md`, `COMBAT_RULES.md`, `PASSIVE_RULES.md`) are unified under OD-1–OD-7 (TASK-286 and TASK-287 completed).
`docs/02-technical/` files still describe `SpecialGem` metadata on `Cells[64]`, `createdSpecialGems` on `MatchCreated`, and `CellPayload.specialGem`.

---

## Declared File Set (P-2 / P-5)

```text
docs/02-technical/GAME_STATE.md
docs/02-technical/SIGNALR_PROTOCOL.md
docs/02-technical/REDIS_STATE.md
docs/02-technical/GAME_EVENTS.md
docs/02-technical/TDD.md
docs/02-technical/ARCHITECTURE.md
```

---

## Acceptance Criteria

- [x] AC-1 `GAME_STATE.md` defines `BoardState.Cells[64]` carrying only `GemType`, with `SpecialGem` metadata and transient special gem structures retired in full.
- [x] AC-2 `SIGNALR_PROTOCOL.md` §3.2.6 (`MatchCreated`) and §3.2.13 (`CellPayload`) contain no `createdSpecialGems` or `specialGem` payload fields.
- [x] AC-3 `REDIS_STATE.md` §3.10 explicitly documents Special Gem retirement from Redis persistence, Decision B-2 tolerance basis, and battle-drain deployment policy.
- [x] AC-4 `GAME_EVENTS.md` event catalog contains zero Special Gem references.
- [x] AC-5 `TDD.md` and `ARCHITECTURE.md` module boundaries reflect match-only pipeline resolution without Special Gem stages.
- [x] AC-6 Version headers of all amended documents describe the content actually present.
- [x] AC-7 LF-only line endings and clean `git diff --check`.
- [x] AC-8 Quality review checklist passes (`.ai/workflow/quality/review.md` §1).
- [x] AC-9 Zero `src/` or `tests/` files modified; production runtime code remains untouched.

---

## Affected Files & Areas

```text
[ ] src/backend/            NOT TOUCHED
[ ] src/frontend/client/    NOT TOUCHED
[ ] tests/                  NOT TOUCHED
[x] docs/02-technical/      GAME_STATE.md, SIGNALR_PROTOCOL.md, REDIS_STATE.md,
                            GAME_EVENTS.md, TDD.md, ARCHITECTURE.md
```

---

## Testing Requirements

### Required Verification

```text
[x] git diff --check                  clean on docs/
[x] Reference sweep                   zero active Special Gem definitions in docs/02-technical/
[x] Line endings                      LF only, zero CRLF
```

---

## Stop Conditions

- If runtime code modification in `src/` is attempted before this technical documentation task passes review: STOP (`AGENTS.md` §17).
- If any data contract conflict arises between `GAME_STATE.md`, `SIGNALR_PROTOCOL.md`, and `REDIS_STATE.md`: STOP (`AGENTS.md` §20).
- If a change outside the declared file set is required: STOP (`AGENTS.md` §16).

---

## Completion Evidence

### Phase A Commit
- **Commit SHA:** `03a6412d8c4af63435c3326e9c59d865133c21a4`
- **Subject:** `docs: reconcile technical contracts and battle-state model for special gem retirement`
- **Files Committed:**
  - `docs/02-technical/GAME_STATE.md`
  - `docs/02-technical/SIGNALR_PROTOCOL.md`
  - `docs/02-technical/REDIS_STATE.md`
  - `docs/02-technical/GAME_EVENTS.md`
  - `docs/02-technical/TDD.md`
  - `docs/02-technical/ARCHITECTURE.md`

### Verification Results
- **Acceptance Criteria:** AC-1 through AC-9 all satisfied.
- **Reference Sweep:** 0 active Special Gem or match-tier multiplier definitions remain across `docs/02-technical/`. Remaining references are explicitly classified historical changelog records, retirement notices, or downstream implementation gates.
- **Line Endings:** LF only (`\n`) across all declared files; verified `hasCR=False`.
- **Git Diff Check:** `git diff --check` clean.
- **Quality Review:** `.ai/workflow/quality/review.md` §1 checklist fully satisfied.
- **Scope Discipline:** Zero `src/` or `tests/` files modified; production runtime code remains untouched.
