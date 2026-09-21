# TASK-014 — Resolve PetState Wire Delivery Contract

## Metadata

```text
Task ID:           TASK-014
Type:              DOCUMENTATION
Status:            DONE
Risk:              LOW
Priority:          HIGH
Primary Agent:     realtime
Supporting Agents: gameplay
Workflow:          documentation/documentation-change.md
Skills:            N/A
Dependencies:      TASK-013 (READY — blocked by this task)
```

---

## Objective

Resolve the documentation contradiction about whether and how `PetState` is delivered through `BattleStateUpdated`. This is a contract decision — no code is implemented.

---

## Context

A contradiction exists between four authoritative sources:

1. **`GAME_STATE.md` §2.3**: `PetState` is a field of `BattleState`, present from battle creation. It carries `PassiveId`, `PassiveProgress`, and `PassiveResetOverride`.

2. **`SIGNALR_PROTOCOL.md` §4 item 4**: `BattleStateUpdated` delivers exactly the fields of the currently implemented `GAME_STATE.md` §2.0 stage. No other field may be added to this record — additional state is introduced by extending `GAME_STATE.md` §2.0, not by the wire shape.

3. **`PASSIVE_RULES.md` §6 item 1**: "Passive progress must be exposed via a UI-facing value (e.g. `7 / 10 Matches`) and/or a `PassiveCharged` event."

4. **TASK-013**: Claims "Client-facing PetState delivery — deferred (`SIGNALR_PROTOCOL.md`)" but **no explicit exclusion exists** in `SIGNALR_PROTOCOL.md` for `PetState`. The existing tests (`BattleStateTests.cs` line 133, `ApiIntegrationTests.cs` line 449) assert `PetState` is absent from `BattleStateUpdated`, but those tests were written when `PetState` did not exist in `BattleState` — they guard against later-stage fields leaking into the current stage, not against a deliberate protocol exclusion.

The core question: `PetState` is a `BattleState` field per §2.3. `BattleStateUpdated` delivers `BattleState` fields per §4 item 4. There is no documented exception for `PetState` the way `LastCommittedSwapPair` has one (`SIGNALR_PROTOCOL.md` §4 item 12). This means `PetState` should be delivered — but the task, the tests, and possibly the implementation assume otherwise.

---

## Authoritative Sources

- `docs/02-technical/GAME_STATE.md` §2.3 — PetState definition, present from battle creation
- `docs/02-technical/SIGNALR_PROTOCOL.md` §4 items 4, 11 — BattleStateUpdated delivers stage fields, no new methods
- `docs/02-technical/SIGNALR_PROTOCOL.md` §4.2 — PlayerState delivery precedent (nested object, implemented fields only)
- `docs/02-technical/SIGNALR_PROTOCOL.md` §4 item 12 — LastCommittedSwapPair exclusion precedent
- `docs/01-game-design/PASSIVE_RULES.md` §6 — UI-facing visibility requirement
- `docs/02-technical/GAME_EVENTS.md` §2 — PassiveCharged/PassiveTriggered event payloads
- `tests/backend/GameServer.Domain.Tests/BattleStateTests.cs` lines 107–139 — existing PetState absent assertion
- `tests/backend/GameServer.Api.Tests/ApiIntegrationTests.cs` lines 424–456 — existing petState absent assertion

---

## Scope

### In Scope

- Decide whether `PetState` is delivered through `BattleStateUpdated`
- If delivered: define the exact wire payload shape (`petState` object members)
- Define field naming/serialization conventions for `PetState` members
- Decide how `PassiveResetOverride = null` is serialized (omitted vs. explicit null)
- Decide how the existing "PetState absent" tests should change
- Decide whether `TASK-013`'s "client-facing delivery deferred" statement is accurate and should be removed, or whether a new explicit exclusion (like §4 item 12) should be added

### Out of Scope

- Implementing `PetState` in `BattleState` (TASK-013)
- Implementing the wire projection (a future integration task)
- Redis persistence of `PetState` (`REDIS_STATE.md` §7)
- Any changes to `GAME_STATE.md` §2.3's field definitions
- Pet selection, PetId, Element, Tier — not yet implemented

---

## Current State

`PetState` does not yet exist in `BattleState` (`src/backend/GameServer.Domain/Battle/BattleState.cs` line 34 documents it as staged-absent). The existing tests assert it is absent from `BattleStateUpdated`:

- `BattleStateTests.cs:133` — asserts `"PetState"` is not in `BattleState`'s declared members
- `ApiIntegrationTests.cs:449` — asserts `"petState"` is not in the wire payload

These tests were authored to guard the staged-absent contract. They need updating when `PetState` becomes a `BattleState` field, but the question is what they should assert afterward.

---

## Acceptance Criteria

- [ ] A decision is documented for each of the six questions below
- [ ] The decision is consistent across all four authoritative sources (no new contradictions introduced)
- [ ] If `PetState` is delivered: `SIGNALR_PROTOCOL.md` §4 is updated with a `PetState` delivery subsection (following the §4.1 / §4.2 precedent)
- [ ] If `PetState` is NOT delivered: an explicit exclusion is added to `SIGNALR_PROTOCOL.md` §4 (following the §4 item 12 precedent for `LastCommittedSwapPair`)
- [ ] The "PetState absent" test assertions in `BattleStateTests.cs` and `ApiIntegrationTests.cs` are updated to reflect the decision
- [ ] `TASK-013`'s "client-facing PetState delivery — deferred" statement is either removed (if delivery is now in scope) or replaced with a reference to the new exclusion (if delivery remains excluded)
- [ ] No gameplay rules, event semantics, or authoritative game-design documents are changed

---

## Decision Questions

The task must explicitly decide and document the following:

### Q1: Is PetState delivered through BattleStateUpdated?

`PetState` is a `BattleState` field per §2.3. `BattleStateUpdated` delivers `BattleState` fields per §4 item 4. There is no documented exclusion for `PetState`. The precedent for stage delivery is `PlayerState` (§4.2): it is delivered as a nested object containing only the implemented stage's fields.

**Decision needed**: Deliver or exclude? If exclude, add an explicit §4 item-style exclusion like `LastCommittedSwapPair` (§4 item 12).

### Q2: Wire payload shape (if delivered)

Following the §4.2 `playerState` precedent, `petState` would be a nested object. The members must match §2.3's current implemented fields:

| Wire member | Type | Source |
| --- | --- | --- |
| `passiveId` | string | §2.3 PassiveId |
| `passiveProgress` | object | §2.3 PassiveProgress (§2.5) |
| `passiveResetOverride` | string or omitted | §2.3 PassiveResetOverride |

**Decision needed**: Confirm this shape, or adjust.

### Q3: PassiveProgress wire shape

`PassiveProgress` is defined in `GAME_STATE.md` §2.5 as `(Threshold int, Current int)`. Following §3.2's flat-object convention and §3.2.5's omission rule:

| Wire member | Type | Presence |
| --- | --- | --- |
| `threshold` | int | always |
| `current` | int | always |

**Decision needed**: Confirm this shape. Both fields are always present (no "not yet charged" state — §2.3 item 3).

### Q4: PassiveResetOverride serialization

`PassiveResetOverride` is `PassiveResetBehavior?` — null means Default reset (§4 item 3). Per §3.2.5's optionality rule, non-applicable members are omitted, never sent as `null`.

**Decision needed**: When `PassiveResetOverride` is null (Default reset), is the member omitted from the wire? This follows the §3.2.5 convention exactly.

### Q5: How should existing tests change?

Two test files assert PetState is absent:

1. `BattleStateTests.cs:107–139` — reflection-based guard that `BattleState` has no later-stage fields
2. `ApiIntegrationTests.cs:424–456` — wire-payload guard that `petState` is absent

**Decision needed**:
- If PetState IS delivered: remove `"PetState"` / `"petState"` from the absent-field lists. The tests continue to guard other later-stage fields (BossState, HP, ATK, etc.).
- If PetState is NOT delivered: keep the assertions but update comments to reference the new explicit exclusion.

### Q6: TASK-013 scope adjustment

TASK-013 currently lists "Client-facing PetState delivery — deferred (`SIGNALR_PROTOCOL.md`)" in Out of Scope.

**Decision needed**:
- If PetState IS delivered through BattleStateUpdated: remove this line from TASK-013's Out of Scope. Delivery is part of §4's existing push — no separate task is needed beyond TASK-013 including PetState in BattleState and the wire projection updating.
- If PetState is NOT delivered: replace the line with a reference to the new SIGNALR_PROTOCOL.md exclusion.

---

## Affected Areas

```text
[ ] Domain (GameServer.Domain/)
[ ] Application (GameServer.Application/)
[ ] API (GameServer.Api/)
[ ] Client (client/)
[ ] SignalR / Redis
[ ] PostgreSQL
[ ] Tests (tests/)
[x] Documentation (docs/) — SIGNALR_PROTOCOL.md §4
[ ] ADR
```

---

## Implementation Notes

### Precedent: PlayerState Delivery (§4.2)

`PlayerState` (§2.2) is delivered as a nested `playerState` object in `BattleStateUpdated`. It carries exactly two members: `combo` and `matchCount`. Other §2.2 fields (HP, ATK, Power, etc.) are NOT delivered because they belong to later stages (§4.2 item 2). This is the model for `PetState` delivery.

### Precedent: LastCommittedSwapPair Exclusion (§4 item 12)

`LastCommittedSwapPair` IS a `BattleState` field but is NOT delivered. The exclusion is documented explicitly in §4 item 12 with reasoning: the client has no use for the value, staleness is server-side, and no new message is introduced. If `PetState` is excluded, a similar explicit justification is required.

### Naming Convention

Following §3.2.3 (all wire properties are camelCase) and the `playerState` precedent, the nested object would be `petState` with members `passiveId`, `passiveProgress`, `passiveResetOverride`.

---

## Testing Requirements

### Test Types Required

```text
[ ] Unit tests         — N/A (documentation task)
[ ] Integration tests  — N/A
[ ] Gameplay scenarios — N/A
[ ] API tests          — N/A
[ ] Realtime tests     — N/A
[ ] Persistence tests  — N/A
```

This is a documentation/contract task. Testing is limited to verifying the updated docs are internally consistent and the test-file changes match the decision.

---

## Documentation Impact

**Option B — Update existing doc:**
> `docs/02-technical/SIGNALR_PROTOCOL.md` §4 must be updated to either
> add a PetState delivery subsection (following §4.1/§4.2 precedent) or
> add an explicit exclusion (following §4 item 12 precedent).
> Use `documentation/documentation-change.md`.

---

## Stop Conditions

- If the decision creates a contradiction with `PASSIVE_RULES.md` §6 (UI-facing visibility): STOP per `AGENTS.md §4`
- If the wire shape contradicts `GAME_STATE.md` §2.3 or §2.5: STOP per `AGENTS.md §4`
- If the decision requires a new ADR (architectural change to realtime delivery): STOP per `AGENTS.md §18`

---

## Dependencies

- TASK-013 (READY — blocked by this task)

---

## Completion Evidence

### Decisions

```text
Q1  DELIVER. PetState IS delivered through BattleStateUpdated as a nested
    `petState` object. No exclusion is added; §4 item 4 applies in the ordinary
    way. §4 item 13 records why item 12 (LastCommittedSwapPair) is NOT the
    precedent: that field is server-side bookkeeping with no client use, while
    PetState is client-facing because PASSIVE_RULES.md §6 item 1 requires
    Passive progress be exposed as a UI-facing value.

Q2  CONFIRMED. `petState` carries exactly three members, following the §4.2
    `playerState` nested-object precedent:
        passiveId              string    §2.3 PassiveId
        passiveProgress        object    §2.3 PassiveProgress (§2.5)
        passiveResetOverride   string    §2.3 PassiveResetOverride (conditional)
    PetId/Identity, Element, and Tier/Star/Level are NOT delivered — they
    belong to the Pet identity/progression stage (§4 item 4).

Q3  CONFIRMED. `passiveProgress` is a nested flat object with exactly two
    always-present int members: `threshold` and `current`, projected from
    §2.5's (Threshold, Current) pair. Both are non-nullable and never omitted:
    `current = 0` is a real publishable value, so absence is never used for it
    (§4.3 item 4).

Q4  OMIT THE MEMBER. `passiveResetOverride` is present iff the reset behavior
    is non-default. When present it is the contract-name string `"Partial"` or
    `"NoReset"` — never a numeric ordinal (§3.2.4). A Default reset OMITS the
    member and never sends explicit JSON null, following §3.2.5 and §2.3's
    "only present if ... non-default" wording. `"Default"` is deliberately NOT
    a third permitted value: the member's absence already means it.

Q5  TESTS UPDATED. `"PetState"` removed from the absent-field list in
    BattleStateTests.cs; `"petState"` removed from the absent-field list in
    ApiIntegrationTests.cs. Both files' exact-field-set assertions now include
    `petState`, and the API test additionally asserts the §4.3 member shape.
    Both continue to guard the other later-stage fields (BossState, HP, ATK,
    Power, EquippedRelics, etc.).

Q6  TASK-013 SCOPE ADJUSTED. The Out of Scope line "Client-facing PetState
    delivery — deferred (SIGNALR_PROTOCOL.md)" was replaced with
    "Pet identity/progression delivery (PetId, Element, Tier/Star/Level) — not
    yet implemented". Delivery is now in scope: TASK-013 adding PetState to
    BattleState plus the §4 wire projection is all that is required, since §4's
    existing push carries it. TASK-013's Blocker section is marked RESOLVED.
```

### Changes

```text
docs/02-technical/SIGNALR_PROTOCOL.md
  - Version 1.5 → 1.6.
  - §0: PetState noted as delivered by the same push (see §4.3).
  - §4 header: payload signature and the staged field list gain `petState`;
    new "Pet / Passive state" stage block.
  - §4: new item 13 — PetState is delivered; item 12 is not its precedent.
  - §4.3: new subsection — the full PetState delivery contract (12 items),
    following the §4.1/§4.2 precedent.
  - §8 item 1: stale "§4.4" reference corrected to "§4 item 4".

tests/backend/GameServer.Domain.Tests/BattleStateTests.cs
  - Removed "PetState" from the later-stage absent-field list; comment updated.

tests/backend/GameServer.Api.Tests/ApiIntegrationTests.cs
  - Removed "petState" from the absent-field list.
  - Both exact-field-set assertions now expect petState.
  - New assertions for the §4.3 shape (passiveId, passiveProgress
    {threshold, current}) and for the Pet-identity fields' continued absence.

tasks/blocked/TASK-013-integrate-passive-tracker-resolution.md
  - Out of Scope line replaced; Blocker section marked RESOLVED by TASK-014.
```

### Source consistency check

```text
GAME_STATE.md §2.3 / §2.5   UNCHANGED and consistent — the wire members project
                            the documented fields one-to-one; no §2.3 field
                            definition was altered (task Out of Scope).
PASSIVE_RULES.md §6 item 1  SATISFIED — PassiveProgress is now delivered as the
                            UI-facing value §6 item 1 requires. No gameplay
                            rule changed.
GAME_EVENTS.md §2           UNCHANGED and consistent — `petState` reports the
                            settled position; PassiveCharged/PassiveTriggered
                            keep their per-Match §3 delivery. §4.3 items 9–11
                            state the two are not interchangeable and neither
                            is re-derived from the other.
SIGNALR_PROTOCOL.md §4      UPDATED — the single owner of the BattleStateUpdated
                            record's member set, per §8 item 1.
```

### Tests

```text
GameServer.Domain.Tests   624 passed, 0 failed.
GameServer.Api.Tests      44 passed, 2 failed — the two failures are exactly the
                          two assertions this task updated to expect `petState`.
                          They fail only because PetState does not yet exist in
                          BattleState (TASK-013's deliverable); verified the
                          sole diff is the missing `petState` member. They pass
                          once TASK-013 adds the field and its projection.
```

### Documentation impact / ADR

```text
SIGNALR_PROTOCOL.md §4.3 is the canonical owner of this contract. No ADR was
created: docs/03-decisions/README.md §2 excludes an ADR that duplicates content
a technical document already fully owns, and §1 requires an ADR to record a
decision already established. This decision is a staged-delivery application of
the existing §4 item 4 rule — it changes no architecture, transport, or
authoritative model (AGENTS.md §18 does not fire).
```

### Remaining issues (reported, not fixed — AGENTS.md §16)

```text
1. src/frontend/client/src/services/realtime/SignalRService.ts:15 carries the
   same stale "§4.4" reference corrected in SIGNALR_PROTOCOL.md §8 item 1, and
   its BattleStateUpdatedPayload will need a `petState` member. Client-side
   work — belongs to TASK-013's client follow-up.
2. src/frontend/client/tests/GameRuntime.test.ts:648 lists the expected payload
   keys; it will need `petState` added for the same reason.
3. GameServer.Application/Battle/BattleStateService.cs:65 and
   BattleHub.cs's payload record document PetState as absent — both become
   stale when TASK-013 lands and should be updated in that task.
```

---
