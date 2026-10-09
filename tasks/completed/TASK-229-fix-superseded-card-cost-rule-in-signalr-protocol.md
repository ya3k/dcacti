# TASK-229 — Fix Superseded Card-Cost Rule in SIGNALR_PROTOCOL.md

## Metadata

```text
Task ID:           TASK-229
Type:              DOCUMENTATION
Status:            DONE
Risk:              LOW
Priority:          P3
Primary Agent:     review
Supporting Agents: N/A
Workflow:          documentation/documentation-change.md
Skills:            quality/documentation-consistency.md, discovery/documentation-discovery.md
Dependencies:      TASK-213, TASK-224
Declared Files:    docs/02-technical/SIGNALR_PROTOCOL.md
```

---

## Objective

Correct the stale card-cost statement in `docs/02-technical/SIGNALR_PROTOCOL.md` §3.2.20 (`CardCast`) item 2 so it aligns with the authoritative card-cost ruling in the same file (§4 item 15, §4.3 item 15) and `TASK-213` §8.2.

---

## Authoritative References

- `docs/02-technical/SIGNALR_PROTOCOL.md` §3.2.20 item 2 — location of stale sentence
- `docs/02-technical/SIGNALR_PROTOCOL.md` §4 item 15 — governing rule: client must never reconstruct card-cost modifiers from events, `PowerChanged` delta, or Card definition
- `docs/02-technical/SIGNALR_PROTOCOL.md` §4.3 item 15 — governing rule: effective cost is composed server-side; client computes no cost/affordability
- `docs/02-technical/SIGNALR_PROTOCOL.md` §3.2.24 item 4 — surviving cross-reference to §3.2.20 item 2 and TASK-104 A-2C ("CardCast deliberately carries no cost")
- `tasks/completed/TASK-213-content-reachability-decision.md` §8.2 — DECIDED single authoritative rule (TASK-213 R-Cost)
- `docs/01-game-design/CARD_RULES.md` §3.6 — owner of `EffectiveCardCost` (runtime value, server-side composed)
- `docs/01-game-design/GAME_RULES.md` §18, ADR-001 — server authority
- `tasks/TASK_LIFECYCLE.md` §6 — commit step and commit ownership policy (P-1…P-6)

---

## Scope

### In Scope
- Replace the superseded sentence in `docs/02-technical/SIGNALR_PROTOCOL.md` §3.2.20 item 2 claiming the client reads the Card definition to display spent Cost.
- Align wording with §4 item 15, §4.3 item 15, and TASK-213 §8.2: a client must not derive or reconstruct effective or spent cost from Card definitions, `PowerChanged` deltas, or events; if spent cost is displayed, its value must come from an explicitly server-delivered field.
- Preserve the TASK-104 A-2C ruling that `CardCast` carries no Power-cost member.
- Preserve the cross-reference in §3.2.24 item 4.

### Out of Scope
- Backend or frontend source code.
- SignalR event schemas, DTOs, or hub contracts.
- Database or Redis schemas.
- Modifying `TASK-217A`, `TASK-217B`, or `TASK-217C` records.
- Modifying `TASK-225` or any existing task ID reservation.
- Resolving the three open attribution conflicts (TASK-217B dual role, TASK-217C split identity, `REDIS_STATE.md` internal attribution conflict).
- Any protocol redesign or addition of fields to `CardCast` or `BattleStateUpdated`.

---

## Current State

Previously, `SIGNALR_PROTOCOL.md` §3.2.20 item 2 contained the superseded sentence:
> "A client that must show the spent Cost reads the Card's definition; a client must not recompute it from the event (`GAME_RULES.md` §18, ADR-001)."

This contradicted §4 item 15 and §4.3 item 15 in the same document, which state that a client must never compute, predict, or reconstruct Card-cost modifiers or effective cost from a Card's definition or event deltas, and that `EffectiveCardCost` is composed server-side only.

---

## Declared File Set (P-2 / P-5)

Exact declared file set:
```text
docs/02-technical/SIGNALR_PROTOCOL.md
```

---

## Acceptance Criteria

- [x] Stale sentence in `SIGNALR_PROTOCOL.md` §3.2.20 item 2 is replaced with the authoritative rule.
- [x] No client-side card-cost reconstruction from definitions or event deltas is endorsed.
- [x] Any display of spent cost is stated to require an explicitly server-delivered value.
- [x] `CardCast` is not implied to carry a Power cost field (TASK-104 A-2C preserved).
- [x] Cross-reference in §3.2.24 item 4 remains consistent and intact.
- [x] No code, schema, or unrelated documentation files are modified.
- [x] Verification distinguishes executed checks from unexecuted checks.

---

## Stop Conditions

- If authoritative rule differs from audit findings: STOP per `AGENTS.md` §4 (Not triggered; authoritative rule matches).
- If fixing contradiction requires changing another file: STOP per `AGENTS.md` §20 (Not triggered; only `SIGNALR_PROTOCOL.md` required editing).
- If task ID cannot be allocated under repository policy: STOP (Not triggered; allocated sequentially as TASK-229).
- If edit would alter A-2C ruling or expand protocol scope: STOP (Not triggered; A-2C preserved intact).

---

## Implementation Notes

Target section: `docs/02-technical/SIGNALR_PROTOCOL.md` §3.2.20 item 2.
Lines edited: 1131–1136.
Wording replaced:
- Old: "A client that must show the spent Cost reads the Card's definition; a client must not recompute it from the event (`GAME_RULES.md` §18, ADR-001)."
- New: "A client must not derive or reconstruct effective or spent cost from a Card definition, from a `PowerChanged` delta, or from events (`§4 item 15`, `§4.3 item 15`, `GAME_RULES.md` §18, `ADR-001`). If spent cost is displayed, its value must come from an explicitly server-delivered field rather than client derivation; the current `CardCast` event carries no cost member."

---

## Testing & Verification

### Executed Checks
1. Verification against authoritative rules:
   - Confirmed §3.2.20 item 2 no longer contradicts §4 item 15 and §4.3 item 15.
   - Confirmed TASK-104 A-2C ruling remains unchanged.
   - Confirmed cross-reference in §3.2.24 item 4 remains unchanged.
2. Whitespace / formatting hygiene:
   - Command: `git diff --check`
   - Result: PASS (exit code 0, 0 issues detected).
3. File scope and diff inspection:
   - Command: `git diff`
   - Result: PASS — exactly one file modified (`docs/02-technical/SIGNALR_PROTOCOL.md`), exactly one hunk changing lines 1131–1136.

### Unexecuted Checks
- Unit / integration / E2E automated test suites: NOT EXECUTED. This is a documentation-only wording correction. No source code, test code, or wire schemas were altered.

---

## Completion Evidence

### Commit
- Implementation slice: `0f6beb7401d837421cd8d80d9142f66ef2fff0ee`
- Subject: `docs: align CardCast cost statement with authoritative rule`
- Owns: `TASK-229`
- Files: `docs/02-technical/SIGNALR_PROTOCOL.md`
- Shared with: none
- Unowned / pre-existing: none

### Changed Files
- `docs/02-technical/SIGNALR_PROTOCOL.md` — §3.2.20 item 2 superseded cost sentence replaced with authoritative rule.

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic added or endorsed
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
- [x] Confirmed P-1…P-6 commit policy compliance
