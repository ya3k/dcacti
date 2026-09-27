# TASK-072 — Define Canonical Element Wire Values in API_CONTRACTS §5

---

## Metadata

```text
Task ID:           TASK-072
Type:              DOCUMENTATION
Status:            DONE
Risk:              MEDIUM
Priority:          HIGH
Primary Agent:     review
Supporting Agents: backend
Workflow:          documentation/documentation-change.md
Skills:            discovery/documentation-discovery, discovery/impact-analysis,
                   quality/documentation-consistency, backend/api-contract-validation
Dependencies:      None (resolves the element wire-format ambiguity blocking the
                   collection-read-endpoint implementation task; TASK-071 remains
                   reserved for that task and must not be created until this
                   contract lands)
```

---

## Objective

Define, in `docs/02-technical/API_CONTRACTS.md` §5.1, exactly one canonical wire
value set for the `element` member — `"Fire" | "Water" | "Earth" | "Wood" |
"Metal"` (the Element names from `ELEMENT_RULES.md` §1) — correct the §5.1 and
§5.2 examples from `"Hỏa"` to `"Fire"`, and record the Vietnamese design names
(Hỏa, Thủy, Thổ, Mộc, Kim) as display/localization values only, never API wire
values. Bump the document version to 1.14 with a changelog entry citing
TASK-072. No code, no game-rule docs, and no battle-start behavior change.

---

## Authoritative References

- `docs/02-technical/API_CONTRACTS.md` §5.1, §5.2 — canonical owner of the REST
  wire representation (`.ai/README.md` §6: REST API → `API_CONTRACTS.md`)
- `docs/01-game-design/ELEMENT_RULES.md` §1 — source of the five Element names
  (English glosses beside the Vietnamese design names)
- `docs/01-game-design/BOSS_RULES.md` §6.4 — established precedent: technical
  identity is ASCII, never display/localization text; display names are
  presentation only, never identifiers in state, events, persistence, or the API
- `docs/AGENTS.md` §4 (conflict resolution), §17 (documentation change rule)
- `docs/00-overview/MVP_SCOPE.md` §1 — Pets/Cards/Relics collection content is
  IN (verified by completed TASK-070); this task adds no scope
- `.ai/workflow/documentation/documentation-change.md` — the assigned workflow
- `tasks/TASK_LIFECYCLE.md` §3 — completed tasks immutable; corrections ride on
  a new task, never on TASK-070

---

## Scope

### In Scope

- `API_CONTRACTS.md` §5.1: bind the `element` value set in the member table,
  add the display-vs-wire statement (Vietnamese names = presentation only),
  change example `"element": "Hỏa"` → `"element": "Fire"`
- `API_CONTRACTS.md` §5.2: change example `"element": "Hỏa"` → `"element":
  "Fire"` (§5.2 inherits §5.1's definition by reference — no restatement)
- `API_CONTRACTS.md` version header: 1.13 → 1.14, new changelog entry citing
  TASK-072, prior 1.13 entry preserved
- Record the existing battle-start `"Hoa"` divergence as a separate follow-up
  (report only — no fix in this task)

### Out of Scope

- Any change to `src/` or `tests/` (notably `BattleStartStateSummary.cs:92,113`
  and `GameRuntime.test.ts:751` — separate follow-up task)
- Any change to `docs/01-game-design/*` (game docs own design names; no wire
  statement exists there and none is added)
- SignalR (`SIGNALR_PROTOCOL.md` §4.3: `petState` carries no `Element`)
- TASK-069, TASK-070 (immutable), and creation of TASK-071
- Architecture changes / new ADRs (ADR index has no serialization or
  localization ADR; same-class decisions TASK-046/TASK-070 were recorded in
  owning docs, not ADRs)

---

## Current State

`API_CONTRACTS.md` is at version 1.13. §5.1 and §5.2 examples show
`"element": "Hỏa"`; the binding member table (§5.1) states only the member name
and type, never a value set — the example content was never normative (the
binding sentence binds the member list; TASK-070 D3 bound members, not values).
No document anywhere in `docs/` defines an Element wire representation, and none
mandates Vietnamese wire values. Meanwhile the existing battle-start
`initialState` serializes `Element.ToString()` → `"Hoa"`
(`BattleStartStateSummary.cs:92,113`; fixture `GameRuntime.test.ts:751`) — a
third spelling, recorded here as follow-up only.

---

## Acceptance Criteria

- [x] §5.1's member table binds `element` to exactly one value set:
      `"Fire" | "Water" | "Earth" | "Wood" | "Metal"`, cited to
      `ELEMENT_RULES.md` §1; no other `API_CONTRACTS.md` section defines
      element wire values (single owner, no duplication)
- [x] §5.1 states the Vietnamese design names (Hỏa, Thủy, Thổ, Mộc, Kim) are
      display/localization values only — presentation text, never API wire
      values
- [x] `grep` of `API_CONTRACTS.md` finds zero remaining `"element": "Hỏa"`;
      §5.1 and §5.2 examples both show `"element": "Fire"`
- [x] Version header reads 1.14 with a changelog entry citing TASK-072; the
      prior 1.13 entry is preserved in the same chain
- [x] Zero changes outside `docs/02-technical/API_CONTRACTS.md`; no
      `docs/01-game-design/*` file modified
- [x] Existing battle-start `"Hoa"` divergence (code + frontend fixture)
      recorded as a separate follow-up in Completion Evidence / Remaining
      Issues — not fixed here
- [x] Quality review checklist passes (`quality/review.md` §1, code-only items
      skipped per `documentation/documentation-change.md` §4)
- [x] All relevant tests pass at the required validation depth
      (`core/validation.md` §2) — documentation-validation layer for a pure
      doc change
- [x] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001)

---

## Affected Files & Areas

```text
[ ] src/backend/ (Domain / Application / Infrastructure / Api)
[ ] src/frontend/client/ (scenes / runtime / services / state / ui)
[ ] tests/ (unit / integration / gameplay scenarios)
[x] docs/ (documentation updates if applicable) — API_CONTRACTS.md only
```

---

## Implementation Notes

- Canonical owner: `API_CONTRACTS.md` per `.ai/README.md` §6 (REST API); edit
  only §5.1/§5.2 + version header (`documentation-change.md` §1, §2 — no
  duplication into `GAME_STATE.md`, `ELEMENT_RULES.md`, or §5.2's own text).
- Member-row style precedent: §5.3's `category` row binds its value set inline
  (`"Basic" | "PetSkill"`); mirror that for `element`.
- §5.2 inherits via its existing "same object as one `/api/pets` array element
  (§5.1)" sentence — do not restate the value set there.
- No dependent reference becomes stale (no section numbers move); verified:
  only two `"element"` occurrences exist in all of `docs/`.
- If any newly noticed doc contradicts the English wire set → STOP per
  `AGENTS.md` §4 (pre-verified: none found).

---

## Testing Requirements

### Required Verification

```text
[x] Unit tests         — N/A (no code change)
[x] Integration tests  — N/A (no code change; documentation-change.md §4)
[x] Documentation validation — re-read §5.1/§5.2 + ELEMENT_RULES.md §1
                          together; confirm single definition, no duplication
[x] Final review       — quality/review.md §1 checklist (code items N/A)
```

### Key Edge Cases

- Value-set casing must match `ELEMENT_RULES.md` §1 English glosses
  (`Fire`, `Water`, `Earth`, `Wood`, `Metal`) exactly — no slugs, no diacritics.
- Examples must remain internally consistent (`identity: "Xích Lang"` keeps its
  Vietnamese form — only `element` is a wire-bound enum value; display content
  strings are unchanged).

---

## Stop Conditions

- If required behavior cannot be fully derived from Authoritative References:
  STOP per `AGENTS.md` §7
- If the documentation hierarchy is found to mandate Vietnamese wire values:
  STOP per `AGENTS.md` §4 (pre-verified during intake: no such mandate exists)
- If the fix appears to require touching battle-start code or frontend fixtures:
  STOP per `AGENTS.md` §16 — record as follow-up, do not fix inline
- If task exceeds 7 skills or crosses multiple uncoupled architectural
  boundaries: STOP & decompose

---

## Completion Evidence

<!-- TO BE COMPLETED BY THE EXECUTING AGENT after reaching DONE. -->

### Changed Files
- `docs/02-technical/API_CONTRACTS.md` — version 1.13 → 1.14 (changelog entry
  citing TASK-072); §5.1 member table binds `element` to
  `"Fire" | "Water" | "Earth" | "Wood" | "Metal"` (ELEMENT_RULES.md §1); new
  §5.1 paragraph records the Vietnamese names as display values only; §5.1 and
  §5.2 examples changed `"Hỏa"` → `"Fire"`

### Validation Results
- Documentation validation (`documentation-change.md` §1 final step) — PASS:
  §5.1/§5.2 re-read together with `ELEMENT_RULES.md` §1 (values match), single
  definition confirmed (`grep "element"` in API_CONTRACTS: only §5.1 binds the
  set), zero remaining `"element": "Hỏa"` in `docs/`, no section numbers moved
  so no dependent reference went stale, no duplication introduced (§5.2
  inherits via its existing §5.1 reference)
- `quality/review.md` §1 checklist — PASS on all applicable items
  (Correctness, Architecture, Scope, Tests, Documentation, Security,
  Performance, Maintainability, Determinism; code-only depth skipped per
  `documentation-change.md` §4)
- Test-dependency check — PASS: `grep` of `src/` and `tests/` shows no
  source/test hardcodes a §5 example element value (endpoints unimplemented
  until TASK-071; `Element.Hoa` matches are C# enum members, unrelated)

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic (no code change)
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
