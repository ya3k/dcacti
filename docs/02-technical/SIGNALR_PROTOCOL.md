# SignalR Protocol

**Version:** 2.2 (Player/Pet role model per ADR-011 — state-path references
`PlayerState.*` corrected to `BattleState`/`PetState`; wire member names
`playerState`, `finalPlayerHp`, and `target="player"` retained as fixed
protocol labels; prior 2.1: §3.2.16–§3.2.18 `sourceId` examples corrected
to display-name BossId per `BOSS_RULES.md` §6.4)
**Status:** Draft — depends on TDD.md §0 assumption (ASP.NET Core backend)

> This document answers: **"How does realtime communication work?"** It does
> not redefine game mechanics — every validation rule referenced here is
> owned by `GAME_RULES.md` or a domain rule document.

---

# 0. State Delivery Stages

The server → client states delivered over SignalR come from the staged
contract in `GAME_STATE.md` §0:

```text
Battle State Foundation (GAME_STATE.md §2.0)
        ↓  delivered by the initial-state subscription (§4)
Board Foundation State (GAME_STATE.md §2.0.5)
        ↓  delivered by the initial-state subscription (§4)
        ↓
Match / Combo accounting (GAME_STATE.md §2.2 — BattleState root; wire label `playerState`)
        ↓  delivered by the initial-state subscription (§4.2)
        ↓
Battle Events (GAME_EVENTS.md)  — once resolution exists
        ↓  delivered by ReceiveEvents (§3)
        ↓
Full BattleState (GAME_STATE.md §2)  — full snapshots, on resync (§7)
```

Two delivery shapes exist and are not interchangeable:

```text
Subscription push   §4  — server pushes state on join; initial sync
ReceiveEvents       §3  — server pushes one batch per resolved action
GetBattleState      §7  — client requests a snapshot; reconnect resync
```

§4 is the staged state's mechanism. It delivers the fields of whichever
`GAME_STATE.md` §2.0 stage is currently implemented, and nothing else. It does
not change §3 or §7, and no stage adds a second state-push method (§4 item 11),
so the Match-3 resolution stage delivers its board through this same push and
its resolution events through §3 (§3.1). Special Gem state is a property of the
board rather than a stage field of its own (`GAME_STATE.md` §2.1), so it is
delivered by that same push as well — see §4.1 items 5–6. Match/Combo
accounting (`GAME_STATE.md` §2.2) is a stage of its own and is delivered by
that same push as a payload member under the fixed wire label `playerState` —
see §4.2. `PetState` is likewise a stage field of
its own (`GAME_STATE.md` §2.3), so it is delivered by that same push as a
payload member too — see §4.3. There is no `PlayerState` state path; the
`playerState` wire name is a protocol label for the Combo/MatchCount
projection only (ADR-011).

---

# 1. Connection

1. Client obtains `battleId` and hub URL from `POST /api/battle/start`
   (`API_CONTRACTS.md` §3).
2. Client connects to the `BattleHub` and joins a group scoped to
   `battleId`.
3. Connection is authenticated using the application session established
   during initial authentication (`POST /api/auth/discord`, `API_CONTRACTS.md` §2,
   ADR-007).
4. `BattleHub` and realtime game handlers deal exclusively with the
   authenticated application player session, with zero direct dependency on
   Discord Embedded App SDK internals.

---

# 2. Client → Server (Hub Methods)

```text
Swap(battleId, fromCell, toCell, clientSequence)
CardCast(battleId, cardId, clientSequence)
PetSkillCast(battleId, clientSequence)     // shorthand; the active Pet's
                                             // Signature Skill is implied
```

1. `clientSequence` is an opaque client-generated request id, echoed back so
   the client can correlate a request with its resulting events — it is
   NOT the authoritative `BattleState.Sequence` (`GAME_STATE.md` §5). It is
   **not** an action version and is not used to reject stale actions: staleness
   is defined against the action's identity within its Turn, not against this
   value (`MATCH3_RULES.md` §2.1.4 item 1). The client is never required to
   track a server number in order to have its Swap accepted.
2. Every method is validated server-side per its owning domain document
   before any state changes (`MATCH3_RULES.md` §2 for Swap,
   `CARD_RULES.md` §3 for CardCast/PetSkillCast).
3. An invalid request (e.g. non-adjacent Swap, insufficient Power) does not
   throw a hard error to the connection — it returns a rejection result to
   the caller only (see §5) and emits no Battle Events.

### 2.1 `Swap` — Parameters and Validation

| Parameter | Meaning | Owner |
| --- | --- | --- |
| `battleId` | the battle the action applies to; also scopes the group | §1.2 |
| `fromCell` | §1.0 cell index `0..63` the player is moving | `MATCH3_RULES.md` §2.1.1 |
| `toCell` | §1.0 cell index `0..63` it is exchanged with | `MATCH3_RULES.md` §2.1.1 |
| `clientSequence` | opaque correlation id (item 1) | this document |

1. **Both cells are §1.0 indices and nothing else.** No row/column pair, no
   screen or pixel coordinate, and no direction is carried
   (`MATCH3_RULES.md` §2.1.1, §1.0). A client that computes adjacency for local
   feedback computes it for feedback only — the server's validation is
   authoritative (`TDD.md` §2.1).
2. **The pair is unordered.** `Swap(b, 12, 13, …)` and `Swap(b, 13, 12, …)` name
   the same swap (`MATCH3_RULES.md` §2.1.1 item 2).
3. **What makes the Swap valid, and what a rejection means, are not defined
   here.** `MATCH3_RULES.md` §2.1.2 owns the validation order, §2.1.4 owns
   staleness/idempotency, and §2.1.5 owns rejection semantics (board unchanged,
   `Turn` unchanged, `Sequence` unchanged, `RngState` unchanged, no Battle
   Event). This document owns only the transport of the request and of its
   result.
4. **No gameplay field is accepted.** The method carries no Gem type, match
   result, Combo, Turn, or `Sequence` value (`GAME_RULES.md` §18). Everything
   the client sends is a request.

All three require a battle that already exists. **Creating a battle is not a
Hub method** — it is `POST /api/battle/start` (`API_CONTRACTS.md` §3), which
returns the `battleId` this hub is then scoped to (`§1.1`). Battle State
Foundation adds no *gameplay* method here and does not change this contract; see
§4 for how the joining client receives state.

The group join required by §1.2 is the one non-gameplay client → server call in
this protocol:

```text
JoinBattle(battleId)
```

1. It adds the caller's connection to the group scoped to `battleId` (§1.2). It
   is not a gameplay action: it submits no game input, is not validated against
   a domain rule, and returns no result beyond the group join itself.
2. Its only observable effect is the server-initiated state push defined in §4.
   It changes no battle state, and it is the trigger for that push — not a
   request for state (§4.1).
3. It is not a battle-creation mechanism: §8.4 remains unchanged, and
   `POST /api/battle/start` remains the only documented way to create a battle.
4. `JoinBattle` is a transport step, not a state contract. It carries no battle
   field and introduces no `Status`/lifecycle value (§8.3).

---

# 3. Server → Client (Event Delivery)

```text
ReceiveEvents(battleId, serverSequence, events[])
```

1. All events produced by resolving one action (`GAME_EVENTS.md` §1) are
   delivered together, in one `ReceiveEvents` call, in the fixed order
   defined by `GAME_RULES.md` §17.
2. `serverSequence` is `BattleState.Sequence` after this resolution
   (`GAME_STATE.md` §5) — strictly increasing, no gaps, per battle. One
   resolved action produces exactly one such value, however many Matches,
   Cascades, or Special Gems it contained (`MATCH3_RULES.md` §8.2).
3. Every client in the `battleId` group receives the same
   `ReceiveEvents` call (MVP is single-player-vs-Boss, so in practice this
   is the one connected client, but the group mechanism is what allows a
   reconnect to simply rejoin).

## 3.1 Delivery on a Board Resolution

A committed Swap is resolved as one action, so it produces one `ReceiveEvents`
batch, and that batch is the **only** gameplay message the client receives for
it. The batch is atomic at the message level: a client never receives the first
half of a Swap's events without the second half on the same call.

```text
server                                        client
  ├── resolve the Swap  (MATCH3_RULES.md §2–§8)
  ├── write BattleState back under the new Sequence   (GAME_STATE.md §5.1)
  ├── ReceiveEvents(battleId, newSequence, events[])  →  §3
  └── write-back result to the caller                 →  §5
```

1. **Only after the write-back.** Events are produced by the resolution, but
   they describe a state that is already committed: the resolution completes
   and the new `BattleState` is written first, then the batch is sent
   (`GAME_STATE.md` §5.1 item 3). A client that reacts to an event by
   requesting state (`§7`) therefore receives that resolution's result, never
   the pre-resolution state.
2. **No per-step delivery.** The intermediate states of a resolution
   (`MATCH3_RULES.md` §4.1) are Transient Resolution State
   (`GAME_STATE.md` §3) and are never sent. There is no "gravity" message, no
   per-pass message, and no partial batch.
3. **No gameplay message type is introduced.** The board and the resolution's
   resulting values travel in the state push of §4 (and §6 on resync); the
   resolution's events travel in §3. Special Gem state is part of the board
   itself (`GAME_STATE.md` §2.1) and therefore travels in that same §4 push
   with no additional member — no special-gem-specific message, method, or
   subscription exists (§4.1 item 5, §8 items 5–6).
4. **Rejected actions deliver nothing.** A rejected Swap produces no batch and
   no `serverSequence` change; its result is the direct return of §5 only
   (`MATCH3_RULES.md` §2.1.5 item 6, `GAME_EVENTS.md` §1.2).
5. **The client stays non-authoritative.** It applies the batch in order, and
   renders the board from the state it holds; it does not compute matches,
   Cascades, Combo, Special Gems, gravity results, or spawned Gems, and never
   draws from `rngSeed`/`rngState` (`§4.1 item 2`, `GAME_RULES.md` §18,
   `TDD.md` §2.1).

## 3.2 The `events[]` Wire Schema

**This section owns the exact wire schema of `ReceiveEvents.events[]`.** It is
the single owner: `GAME_EVENTS.md` owns what each event *means* and what each
payload *contains* (§2) and defers its wire shape here (`GAME_EVENTS.md` §3
item 1); this section fixes that shape and does not restate §2's semantics.

The envelope of §3 is unchanged by this section: three members, `battleId`,
`serverSequence`, `events[]`. What follows defines the array's items.

### 3.2.1 The Serialization Boundary

```text
BattleEvent                    Domain (GameServer.Domain.Match3)
        ↓  transport projection       Application / API — a pure field mapping
ReceiveEvents payload          { battleId, serverSequence, events[] }
        ↓  SignalR JSON
Frontend                       opaque (ARCHITECTURE.md §2.2.1 rule 4)
```

1. **`BattleEvent` is not a wire DTO, and must never become one.** It is a
   Domain value whose non-applicable payload accessors (`Match`, `CascadeDepth`,
   `Combo`, `Gem`) throw rather than return a default, precisely so that a
   wrong-kind read is a defect (`BattleEvent.cs`). Direct
   `System.Text.Json` serialization of it is therefore **invalid by
   construction**, not merely discouraged: a serializer that reflects over its
   public properties reads all four accessors and throws on the three that do
   not apply, aborting the send. The transport must not depend on, remove, or
   weaken those accessors, and Domain is not made JSON-friendly to accommodate
   the serializer.
2. **The transport layer owns serialization.** The projection reads `Type`
   first and then only that event's own payload, producing a plain transport
   record. It adds no gameplay value, re-derives none, and consults no second
   detection pass. This keeps the documented direction
   `Domain → Application/API transport projection → SignalR → Frontend`
   (`ARCHITECTURE.md` §2.1 item 4) intact.
3. **The projection is a pure field mapping.** It sorts nothing and filters
   nothing: the array is the executor's own event list, in the order
   `GAME_RULES.md` §17 and `GAME_EVENTS.md` §1.1 place it (§3 item 1, §3.1).
4. **The frontend stays opaque.** It receives this documented envelope and does
   not interpret it; `ARCHITECTURE.md` §2.2.1 rule 4 forbids the runtime to
   reorder, filter, or interpret a batch. Defining this schema does not require
   the client to model it, and no client-side event union is introduced by it.

### 3.2.2 Discriminator

Every item in `events[]` is a **flat JSON object** carrying exactly one
discriminator member and that event's own payload members at the same level.

| Member | Type | Value |
| --- | --- | --- |
| `type` | string | one of `MatchCreated`, `CascadeCreated`, `ComboChanged`, `GemMatched`, `DamageCalculated`, `DamageDealt`, `DamageTaken`, `PassiveCharged`, `PassiveTriggered`, `BossSkillCast`, `BattleWon`, `BattleLost` |

1. **The property name is `type`.** The string spelling matches the event name
   `GAME_RULES.md` §16 lists and `GAME_EVENTS.md` §2 defines.
2. **The encoding is the string**, never the numeric enum value and never a
   camelCase variant. The allowed values are exactly the names in the
   discriminator table above — the set is closed. No `MatchResolved`, no
   `SpecialGemActivated`, no `TurnChanged`, no `BoardChanged`, and no other
   name is a valid `type`: those are either state (`SIGNALR_PROTOCOL.md`
   §3.1 item 3, §8 items 5–7) or undefined (`GAME_EVENTS.md` §2 item 3).
3. **The enum is projected to its name, not to its ordinal.** The ordinal
   (`BattleEventType`) is a Domain identity and is not part of the wire
   contract; renumbering the enum must not change the wire.
4. **No numeric alias and no second discriminator exist.** `type` is the only
   member that identifies the event, and it is always present. An item without
   a recognized `type` is malformed, not a fifth event.

### 3.2.3 Property Casing

1. **All wire property names are `camelCase`**, spelled exactly as the tables
   below give them.
2. **`camelCase` is fixed for this contract, not delegated to a serializer
   default.** `System.Text.Json`'s default is PascalCase and must not be relied
   on; the projection names its members explicitly. A serializer-wide naming
   policy that changed these names would break this contract.
3. **Member names are case-sensitive** on the wire. `type` is not `Type`, and
   `cellIndex` is not `cell_index` or `cellIndex`-with-different-casing.

### 3.2.4 Enum Representation

Every enum-valued wire member is a **string** carrying the documented contract
name, never a number.

| Domain enum | Wire member | Allowed string values |
| --- | --- | --- |
| `GemType` | `gemType` | `ATK`, `DEF`, `HP`, `POWER` |
| `SpecialGemType` | `specialGem.type` | `LineClear`, `Burst`, `Area` |
| `SpecialGemOrientation` | `specialGem.orientation` | `Horizontal`, `Vertical` |

1. **Gem types use the documented uppercase contract names.** `GemType` is the
   one enum whose documented wire/display spelling is fixed by the game design
   document (`MATCH3_RULES.md` §1.1: ATK, DEF, HP, POWER), and the existing
   board projection already carries exactly those
   (`CellPayload.gemType`). The four are the complete set.
2. **Special Gem type and orientation use their Domain member names**, which
   are already the documented names (`GAME_STATE.md` §2.1.4 items 1–2), and are
   the same spellings the board projection uses for a cell's Special Gem.
3. **No numeric enum value is ever sent for an enum member**, in any of the
   four events or in the nested match/special-gem objects.
4. **No `GemType` is ever absent or null.** A cleared cell always has one of
   the four types, including a cell that held a Special Gem
   (`GAME_EVENTS.md` §2 item 1, `GAME_STATE.md` §2.1.3 items 1–2).

### 3.2.5 Optionality — Omitted, Never Explicit `null`

**Rule.** A member that is not applicable to an event is **omitted entirely**.
No member of an `events[]` item is ever sent as JSON `null`.

1. **Choice.** This contract selects **omitted** — not explicit `null` — for
   every "not applicable" case: a non-`MatchCreated` has no `match`, a
   non-`CascadeCreated` has no `cascadeDepth`, a non-`ComboChanged` has no
   `combo`, a non-`GemMatched` has no `gem`/`cellIndex`/`gemType`, an ordinary
   cleared Gem has no `specialGem`, and a `Burst`/`Area` Special Gem has no
   `orientation`.
2. **Why omission is the choice, derived from existing convention.**
   `GAME_STATE.md` §2.1.7 item 3 already fixes this convention for the board's
   optional `SpecialGem` member: the representation is "**absent**, not a null
   element, not a sentinel type, and not a default value", and it states that
   omitting the member and writing an explicit `null` are the same statement
   but that no third spelling exists. This section reuses that convention
   rather than inventing a second one, so an optional member means one thing
   everywhere in the protocol. The board projection's
   `specialGem?: SpecialGemPayload | null` is therefore read as
   *absent-on-the-wire*: a producer omits it, and a consumer tolerates absence.
   **A producer must not emit `null` for any optional member of this contract.**
3. **Absence is a statement, not a gap.** For `specialGem`, absence means "this
   cell's occupant was an ordinary Gem and no Special Gem was consumed there"
   (`GAME_EVENTS.md` §2 item 2). It is never an invitation to predict one.
4. **This is not inferred from the throwing accessors.** `BattleEvent`'s
   accessors throw to make a wrong-kind *read* a defect; they are not a wire
   rule. The wire rule above is chosen from the documented board convention
   (item 2) and is what the projection must produce.
5. **The discriminator is never omitted**, and no event carries a member of an
   event it is not.

### 3.2.6 `MatchCreated`

```json
{
    "type": "MatchCreated",
    "shape": "Straight",
    "cells": [8, 9, 10],
    "gemType": "ATK",
    "cascadeDepth": 1
}
```

| Member | Type | Presence | Meaning |
| --- | --- | --- | --- |
| `type` | string | always | `"MatchCreated"` |
| `shape` | string | always | `"Straight"` or `"Lt"` |
| `cells` | array of int | always | the Match's cells, ascending §1.0 index, each once |
| `gemType` | string | always | the Match's Gem type (one of the four) |
| `cascadeDepth` | int | always | the detection pass's depth (`1` = not a Cascade) |
| `createdSpecialGems` | array of object | when non-empty | the Special Gems this Match created |

1. **`shape` is the shape *identity*, not a serialization of `MatchShape`.**
   The two values are exactly `Straight` and `Lt`, which is the documented
   distinction the game rules own: a single straight primitive
   (`MATCH3_RULES.md` §3 item 1), or two perpendicular primitives sharing
   exactly one cell (§3 item 3, §5.4 item 1). `Lt` covers both the L and the T;
   `MATCH3_RULES.md` §5.4 items 1–3 define no separate L and T tiers, so the
   wire does not invent one.
2. **`cells` is the shape's cells** — the union of its arms' cells, each cell
   once, ascending §1.0 index (`MATCH3_RULES.md` §3.3 items 1–3). A shared cell
   appears once. The array position does not carry meaning beyond order: these
   are §1.0 indices, so they are stated explicitly rather than implied by
   position.
3. **The Domain's horizontal/vertical arms, arm lengths, `StartIndex`,
   `IntersectionIndex`, and `Step` are NOT wire members.** They are
   `MatchShape`/`MatchPrimitive` implementation geometry. Every fact §2
   requires is already carried by `shape` + `cells` + `gemType` +
   `createdSpecialGems`, and the client must not re-derive a Match from
   geometry: the authoritative statement is the event itself
   (`GAME_RULES.md` §18, `ARCHITECTURE.md` §2.2.1 rule 3). Exposing the arms
   would leak Domain implementation detail and invite client-side geometry.
4. **`cascadeDepth` is the pass depth, not the `CascadeCreated` index.** It is
   the detection pass's own depth (`MATCH3_RULES.md` §4.2 items 1–2): `1` for
   the pass run on the board the committed Swap produced (which is **not** a
   Cascade), `≥ 2` for a pass produced by Gravity+Spawn. This is the same depth
   the Domain `MatchResolution` carries. It is deliberately **not** the
   `CascadeCreated` payload's `d − 1`, which is a different documented value
   (§3.2.7).
5. **`gemType` is the Match's Gem type** — the type shared by every cell of the
   shape (`MATCH3_RULES.md` §3.3 item 2), spelled per §3.2.4.
6. **`createdSpecialGems` carries what the resolution committed**, in the
   creation order `MATCH3_RULES.md` §5.5.1 gives. A claim discarded by
   collision resolution is absent, because it is "not created, not stored, not
   activated, and not reported as created" (§5.5.4 item 3). The member is
   omitted when the array would be empty — a Match-3 creates no Special Gem
   (§5.1 item 1) — and a present member is never an empty array.
7. **Each `createdSpecialGems` entry** is the transport representation defined
   in §3.2.10.

### 3.2.7 `CascadeCreated`

```json
{
    "type": "CascadeCreated",
    "cascadeDepth": 1
}
```

| Member | Type | Presence | Meaning |
| --- | --- | --- | --- |
| `type` | string | always | `"CascadeCreated"` |
| `cascadeDepth` | int | always | the Cascade's depth index within this Swap |

1. **`cascadeDepth` is the depth index within the Swap**, where `1` is the
   Swap's **second** pass (`GAME_EVENTS.md` §2, `MATCH3_RULES.md` §4.2 item 2).
   It is the Domain `CascadeCreated` payload, which is the pass's own depth
   minus one.
2. **The member name is the same `cascadeDepth` used by `MatchCreated`**, and
   the two carry **different documented values** on the same pass (item 4 of
   §3.2.6 above). They are not required to be equal, and a consumer must not
   treat one as the other. The name is shared because both answer "which
   detection pass is this"; the *value* each reports is owned by its own event
   definition (`GAME_EVENTS.md` §2).
3. **It is always present and is an integer**, never a string and never
   omitted. A `CascadeCreated` with no depth would be meaningless — it reports
   the pass itself.
4. **This event carries no other member.** No Match, no cells, no Gem type, no
   Special Gems: those belong to the `MatchCreated` events that follow it
   (`GAME_EVENTS.md` §1.1 item 1).

### 3.2.8 `ComboChanged`

```json
{
    "type": "ComboChanged",
    "combo": 3
}
```

| Member | Type | Presence | Meaning |
| --- | --- | --- | --- |
| `type` | string | always | `"ComboChanged"` |
| `combo` | int | always | the **new** Combo value |

1. **`combo` is the new value**, per `GAME_EVENTS.md` §2 ("Payload: New Combo
   value") — not a delta, not the previous value, and not the cumulative
   `BattleState.Combo` of an earlier Swap (`GAME_STATE.md` §2.2).
2. **It is an integer, always present, and never omitted or null.** `0` is a
   real value where it occurs and is sent as `0`
   (`GAME_STATE.md` §2.2 item 1: absence is never used for these two members).
3. **It is not a `serverSequence`.** The batch's `serverSequence` is the
   post-resolution `BattleState.Sequence` (§3.2); `combo` is a gameplay value
   inside the resolution and the two are unrelated (`§4.2` item 7).

### 3.2.9 `GemMatched`

```json
{
    "type": "GemMatched",
    "cellIndex": 27,
    "gemType": "HP"
}
```

with a consumed Special Gem:

```json
{
    "type": "GemMatched",
    "cellIndex": 27,
    "gemType": "HP",
    "specialGem": {
        "type": "LineClear",
        "orientation": "Horizontal"
    }
}
```

| Member | Type | Presence | Meaning |
| --- | --- | --- | --- |
| `type` | string | always | `"GemMatched"` |
| `cellIndex` | int | always | the cleared cell's §1.0 index `0..63` |
| `gemType` | string | always | the cleared cell's Gem type |
| `specialGem` | object | only when one was consumed | the Special Gem consumed at that cell |

1. **`cellIndex` is the §1.0 cell index**, an integer in `0..63`
   (`MATCH3_RULES.md` §1.0). It is carried explicitly because this event names
   one cell rather than an array of them. The event is emitted once per cleared
   cell, in ascending index within its sub-step (`GAME_EVENTS.md` §1.3), but
   the index is stated rather than implied by position.
2. **`gemType` is always the cell's Gem type**, one of the four §3.2.4 values,
   and is never absent — including for a cell that held a Special Gem, whose
   occupant is still one of the four types (`GAME_EVENTS.md` §2 item 1).
3. **`specialGem` is present exactly when the cell that was cleared held one**,
   and it identifies that Special Gem's type and, for a Line Clear Gem, its
   orientation (`GAME_EVENTS.md` §2 item 2). It is read from the
   **pre-removal** board: the cell is cleared and the Special Gem consumed by
   the same act (`GAME_STATE.md` §2.1.8 item 2).
4. **For an ordinary Gem the member is omitted** (§3.2.5). Its absence is the
   statement that no Special Gem was consumed there, and it is the only
   representation of that fact: no `null`, no `"None"` type, no empty
   orientation.
5. **`specialGem` is the transport representation of the consumed Special Gem,
   not `SpecialGemClaim`.** See §3.2.10 item 1.

### 3.2.10 Special Gem Representation

```text
specialGem
├── type            "LineClear" | "Burst" | "Area"     always present
└── orientation     "Horizontal" | "Vertical"          present iff type is LineClear
```

1. **`SpecialGemClaim` is NEVER serialized, in any event.** It is documented as
   **Transient Resolution State** (`GAME_STATE.md` §3, `SpecialGemClaim.cs`):
   pass-local creation bookkeeping that "is never a `BattleState` field, is
   never serialized or delivered, and does not survive the pass that built it".
   Its members — `CellIndex`, `ShapeIndex`, `IntraShapeOrder`, `Source`,
   `GemType` — are collision-ordering internals, and `ShapeIndex` /
   `IntraShapeOrder` / `Source` in particular are the §5.5.1/§5.5.4 collision
   keys, which `GAME_STATE.md` §2.1.6 item 4 states are **never serialized**.
   Projecting a claim directly would expose transient resolution bookkeeping as
   protocol, which its own contract forbids.
2. **The transport representation is derived from the Special Gem, not from
   the claim.** Only two facts are needed and only two are sent: the Special
   Gem's **type**, and its **orientation** for a Line Clear Gem — exactly the
   `SpecialGem` metadata shape `GAME_STATE.md` §2.1.4 defines and the same
   shape the board carries for a cell (`CellPayload.specialGem`).
3. **It carries no cell index.** For `GemMatched` the cell is the sibling
   `cellIndex` member; for `createdSpecialGems` it is the entry's own
   `cellIndex` (§3.2.11). A nested cell index would be a second spelling of one
   fact, which `GAME_STATE.md` §2.1.3 item 5 avoids for the board.
4. **It carries no `GemType`.** A created Special Gem carries the Gem type of
   the Gem removed from that cell (`MATCH3_RULES.md` §5.5.4 item 4,
   `GAME_STATE.md` §2.1.3 item 2). For `MatchCreated` that type is already the
   event's own `gemType` — both arms of a shape share one Gem type
   (`MATCH3_RULES.md` §3.3 item 2), so a per-creation copy would be a
   redundant member that can disagree with the event's. For `GemMatched` the
   consumed Special Gem's cell is the event's own `gemType`.
5. **It carries no identity, id, creation order, depth, or age.** A Special
   Gem's behaviour is a pure function of its type, its orientation, and the
   cell it occupies (`GAME_STATE.md` §2.1.3 item 4, §2.1.4 item 4); the model
   holds nothing else and the wire adds nothing.
6. **Orientation is present if and only if `type` is `LineClear`**
   (`GAME_STATE.md` §2.1.4 item 2). A `Burst` or `Area` entry omits it — it is
   a value no rule reads. This member is therefore optional per §3.2.5, and
   `LineClear` always carries one of the two values.
7. **Only created or consumed Special Gems are reported this way.** No
   `SpecialGemActivated` and no `SpecialGemCreated` event exists: activation is
   reported through `GemMatched` and the state push, and creation inside the
   `MatchCreated` payload (`GAME_EVENTS.md` §2 item 3, `SIGNALR_PROTOCOL.md`
   §8 item 7, §3.1 item 3).

### 3.2.11 `createdSpecialGems` Entry Shape

```json
{
    "cellIndex": 9,
    "specialGem": {
        "type": "Burst"
    }
}
```

| Member | Type | Presence | Meaning |
| --- | --- | --- | --- |
| `cellIndex` | int | always | the cell the Special Gem was created at |
| `specialGem` | object | always | the created Special Gem (§3.2.10) |

1. **`cellIndex` is always present here** — unlike in `GemMatched`, where it is
   a sibling of the event, a creation entry is not itself an event and must
   state its cell.
2. **`specialGem` is always present**: a creation entry exists only because a
   Special Gem was created.
3. **Entries are in the §5.5.1 creation order** the resolution committed, and
   a collision-discarded claim is absent (`MATCH3_RULES.md` §5.5.4 item 3) —
   §3.2.6 item 6.

### 3.2.12 No Member Outside This Schema

1. **The seven events carry exactly the members tabulated above.** No event
   carries a `battleId`, a `serverSequence`, a `turn`, a board, a `combo`, a
   `matchCount`, a timestamp, a GUID, or a generated id: the first two belong
   to the envelope (§3), and the rest are state (§3.1 item 3) or nonexistent
   (`AGENTS.md` §11).
2. **No event carries a Match count.** The authoritative cumulative total is
   `BattleState.MatchCount`, delivered by the §4 push; `GAME_EVENTS.md` §3
   item 7 has the accounting stage emit no event.
3. **No event carries any gameplay field §2 does not define.** This section
   fixed a representation; it invented no payload.
4. **Event ordering is unchanged.** This section defines item *shapes*, never
   their order: the array remains the resolution's own order
   (`GAME_RULES.md` §17, `GAME_EVENTS.md` §1.1, §1.3).
5. **The envelope is unchanged.** `ReceiveEvents(battleId, serverSequence,
   events[])` keeps exactly its three members (§3, §3.3).

### 3.2.13 `DamageCalculated`

```json
{
    "type": "DamageCalculated",
    "base": 100,
    "comboModifier": 1.5,
    "elementModifier": 1.5,
    "otherModifiers": 1.0,
    "defense": 68.57142857142857,
    "finalDamage": 68
}
```

| Member | Type | Presence | Meaning |
| --- | --- | --- | --- |
| `type` | string | always | `"DamageCalculated"` |
| `base` | int | always | Step 1 — Base Damage: `PetState.ATK + ResourceGeneration.BaseDamagePool` — the active Pet's ATK (`COMBAT_RULES.md` §3 step 1, `GAME_STATE.md` §2.3) |
| `comboModifier` | double | always | Step 2 — the factor the Swap's Combo selects (`COMBAT_RULES.md` §3 step 2) |
| `elementModifier` | double | always | Step 3 — the factor the resolved element matchup assigns (`ELEMENT_RULES.md` §2.2, `COMBAT_RULES.md` §3 step 3) |
| `otherModifiers` | double | always | Step 4 — the combined Relic / Passive / Buff / Debuff / Crit factor (pass-through `1.00` in MVP) |
| `defense` | double | always | Step 5 — the damage after Defense Mitigation, before truncation: `Pre-Defense × (K / (K + DEF))` (`COMBAT_RULES.md` §3.2) |
| `finalDamage` | int | always | Step 6 — the Final Damage: `defense` truncated toward zero, never negative (`COMBAT_RULES.md` §3 step 6) |

1. **All six pipeline members are always present.** The pipeline always runs
   steps 1–6 (`COMBAT_RULES.md` §3.1: "Steps 1–6 must execute in this order
   for every damage instance"), so every member is populated regardless of
   whether a later stage (Relic, Passive, Crit) has been implemented.
2. **Modifiers are reported as the factors the pipeline applied, not as
   deltas.** A `comboModifier` of `1.5` means "multiplied by 1.5", not
   "+50%". This is the form the configuration types carry
   (`ComboModifiers`, `ElementModifiers`) and what lets the client show the
   pipeline the way `GDD`'s Design Philosophy asks — a number and the
   multipliers that produced it.
3. **`defense` is a `double` and may be fractional.** The mitigation formula
   divides by `(K + DEF)` and the result is genuinely fractional
   (`96 × 100/140 ≈ 68.57`). The report therefore presents the
   intermediate value at the precision the pipeline computes it in.
4. **`finalDamage` is an `int`: truncated, never negative.** §3 step 6
   truncates toward zero and enforces a minimum of 0
   (`COMBAT_RULES.md` §3 step 6). The wire carries the already-truncated
   integer.
5. **`otherModifiers` is `1.00` in MVP and the member is required even so.**
   `GAME_EVENTS.md` §2 lists it in the payload, and `COMBAT_RULES.md` §3.1
   makes step 4 a stage the pipeline always has. Omitting the member would
   understate the breakdown the contract asks for, and would make a later
   step-4 implementation a payload change rather than a value change.
6. **This event precedes `DamageDealt` and `DamageTaken` for the same
   instance** (`GAME_EVENTS.md` §1).

### 3.2.14 `DamageDealt`

```json
{
    "type": "DamageDealt",
    "source": "player",
    "target": "boss",
    "amount": 68
}
```

| Member | Type | Presence | Meaning |
| --- | --- | --- | --- |
| `type` | string | always | `"DamageDealt"` |
| `source` | string | always | the party that dealt the damage (`"player"` or `"boss"`) |
| `target` | string | always | the party that received the damage (`"player"` or `"boss"`) |
| `amount` | int | always | the Final Damage applied (`COMBAT_RULES.md` §3 step 6) |

1. **`source` and `target` are the `DamageParty` enum projected to its
   documented name.** The two allowed values are `"player"` and `"boss"`
   (`DamageEvents.cs`). The ordinal is a Domain identity and is not part of
   the wire contract (§3.2.2 item 3, §3.2.4 item 3).
2. **`amount` is the same `FinalDamage` value `DamageCalculated` reports.**
   It is the truncated integer damage applied to the target's HP. Boss HP is
   reduced once, by the pipeline; these reports describe that reduction
   (`GAME_EVENTS.md` §3 item 6).
3. **This event always follows `DamageCalculated` for the same instance**
   (`GAME_EVENTS.md` §1).

### 3.2.15 `DamageTaken`

```json
{
    "type": "DamageTaken",
    "source": "player",
    "target": "boss",
    "amount": 68
}
```

| Member | Type | Presence | Meaning |
| --- | --- | --- | --- |
| `type` | string | always | `"DamageTaken"` |
| `source` | string | always | the party that dealt the damage (`"player"` or `"boss"`) |
| `target` | string | always | the party that received the damage (`"player"` or `"boss"`) |
| `amount` | int | always | the Final Damage taken (`COMBAT_RULES.md` §3 step 6) |

1. **Same members as `DamageDealt`, same instance.** The two events carry
   identical payloads for the same damage instance — `DamageDealt` reports
   from the dealer's side, `DamageTaken` from the receiver's side
   (`GAME_EVENTS.md` §2). It is **not** a second application of damage:
   Boss HP is reduced once, by the pipeline, and these two reports describe
   that one reduction.
2. **This event always follows `DamageDealt` for the same instance**
   (`GAME_EVENTS.md` §1).
3. **In MVP, `source` is always `"player"` and `target` is always `"boss"`**
   for both events (`GAME_RULES.md` §17 steps 15–17). The `DamageParty`
   enum is widen-able: when Boss-to-Player damage is implemented
   (`BOSS_RULES.md` §4), the same two roles describe it with
   `source = "boss"` and `target = "player"`.

### 3.2.16 `PassiveCharged`

```json
{
    "type": "PassiveCharged",
    "passiveId": "boss-hoa-long-rage",
    "source": "boss",
    "sourceId": "Hỏa Long",
    "progress": 3,
    "threshold": 5
}
```

| Member | Type | Presence | Meaning |
| --- | --- | --- | --- |
| `type` | string | always | `"PassiveCharged"` |
| `passiveId` | string | always | the Passive's identity (`GAME_STATE.md` §2.3 `PetState.PassiveId` or §2.4 `BossState.PassiveId`) |
| `source` | string | always | `"pet"` or `"boss"` — which entity's Passive charged (`GAME_EVENTS.md` §2) |
| `sourceId` | string | always | the identity of the owning entity (`PetState.PetId` or `BossState.BossId`) |
| `progress` | int | always | the new progress value after the increment (`PASSIVE_RULES.md` §2) |
| `threshold` | int | always | the Passive's threshold (`PASSIVE_RULES.md` §1) |

1. **`source` discriminates Pet Passive from Boss Passive.** This event is
   shared by both systems (`PASSIVE_RULES.md` §7, `BOSS_RULES.md` §3).
   The string is `"pet"` or `"boss"`, matching the `DamageDealt`/`DamageTaken`
   convention for party identifiers (§3.2.14 item 1).
2. **`sourceId` identifies the specific entity.** For a Pet Passive, it is
   `PetState.PetId`; for a Boss Passive, it is `BossState.BossId` — the
   Boss's display-name BossId (e.g. `"Hỏa Long"`), per `BOSS_RULES.md` §6.4.
   The client uses both `source` and `sourceId` to attribute the event.
3. **`passiveId` is the same value the owning entity's state holds.** It is
   never re-derived or invented by the emitting stage (`GAME_EVENTS.md` §2
   item 1).

### 3.2.17 `PassiveTriggered`

```json
{
    "type": "PassiveTriggered",
    "passiveId": "boss-hoa-long-rage",
    "source": "boss",
    "sourceId": "Hỏa Long",
    "progress": 5,
    "threshold": 5
}
```

| Member | Type | Presence | Meaning |
| --- | --- | --- | --- |
| `type` | string | always | `"PassiveTriggered"` |
| `passiveId` | string | always | the Passive's identity |
| `source` | string | always | `"pet"` or `"boss"` |
| `sourceId` | string | always | the identity of the owning entity |
| `progress` | int | always | progress at the moment the threshold was crossed — before reset (`PASSIVE_RULES.md` §2 item 4, §4) |
| `threshold` | int | always | the Passive's threshold |

1. **Same members as `PassiveCharged`, same semantics.** The two events
   carry identical payload shapes — `PassiveTriggered` is emitted when the
   threshold is crossed, `PassiveCharged` when it is not
   (`GAME_EVENTS.md` §2).
2. **`progress` is the value before this trigger's own reset.** On a
   `PassiveTriggered`, the progress reported is the value at the moment the
   threshold was crossed — **before** that trigger's own reset
   (`PASSIVE_RULES.md` §2 item 4, §4).
3. **`effect summary` is deferred** per `GAME_EVENTS.md` §2 item 3 and is
   not a wire member yet.

### 3.2.18 `BossSkillCast`

```json
{
    "type": "BossSkillCast",
    "skillId": "flame-burst",
    "sourceId": "Hỏa Long"
}
```

| Member | Type | Presence | Meaning |
| --- | --- | --- | --- |
| `type` | string | always | `"BossSkillCast"` |
| `skillId` | string | always | the Boss Skill's identity (`BOSS_RULES.md` §4) |
| `sourceId` | string | always | the Boss's identity (`BossState.BossId`) |

1. **`skillId` identifies which Boss Skill was used.** It is the same
   identity the boss definition carries (`BOSS_RULES.md` §6.4 — e.g.
   `"flame-burst"`) — the client uses it to look up the
   skill's visual and effect description (`BOSS_RULES.md` §4, §6).
2. **`sourceId` identifies the Boss.** In MVP there is exactly one Boss per
   battle, but the field is present for future-proofing and consistency with
   `PassiveCharged`/`PassiveTriggered` (§3.2.16). The value is the Boss's
   display-name BossId (`BossState.BossId`, e.g. `"Hỏa Long"`) per
   `BOSS_RULES.md` §6.4.
3. **Effect details are carried by subsequent damage events.** The skill's
   damage (if any) is reported by `DamageCalculated`/`DamageDealt`/
   `DamageTaken` in the same `ReceiveEvents` batch, with `source = "boss"`
   and `target = "player"` (a fixed wire label meaning the player's side /
active Pet — `GAME_EVENTS.md` §1, ADR-011).

### 3.2.19 `BattleWon` / `BattleLost`

```json
{
    "type": "BattleWon",
    "outcome": "victory",
    "finalBossHp": 0,
    "finalPlayerHp": 85
}
```

```json
{
    "type": "BattleLost",
    "outcome": "defeat",
    "finalBossHp": 120,
    "finalPlayerHp": 0
}
```

| Member | Type | Presence | Meaning |
| --- | --- | --- | --- |
| `type` | string | always | `"BattleWon"` or `"BattleLost"` |
| `outcome` | string | always | `"victory"` or `"defeat"` |
| `finalBossHp` | int | always | Boss HP at battle end (`GAME_STATE.md` §2.4) |
| `finalPlayerHp` | int | always | Active Pet HP at battle end (`GAME_STATE.md` §2.3 `PetState.HP`) — the wire name `finalPlayerHp` is a fixed protocol label; there is no Player HP pool (ADR-011) |

1. **`outcome` is a string, not a boolean.** It carries `"victory"` or
   `"defeat"` — the same names `GAME_EVENTS.md` §2 item 9 uses. The string
   form is consistent with other discriminator-style wire members
   (`source`, `gemType`).
2. **`finalBossHp` and `finalPlayerHp` are the terminal HP values.** They
   are the state values at the moment the battle ended, after all damage
   from the final action has been applied. `finalPlayerHp` carries the
   **active Pet's** HP (`PetState.HP`); the wire member name is a fixed
   protocol label and does not imply a Player HP pool (ADR-011). The client
   uses them for end-of-battle display.
3. **`reward summary` is deferred** per `GAME_EVENTS.md` §2 item 9 — it is
   not a wire member yet. The data shape is owned by `DATABASE.md`.

---

# 4. Initial State Delivery (`BattleStateUpdated`)

Defines how the client first receives the authoritative battle state, so the
runtime foundation (`BattleState → SignalR → GameRuntime → BattleScene`) can
be built before gameplay exists (`GAME_STATE.md` §0, §2.0).

```text
BattleStateUpdated(battleId, turn, sequence, board, rngSeed, rngState,
                   playerState, petState)
```

Exactly the fields of the currently implemented `GAME_STATE.md` §0 stage,
and no others. Which fields those are depends on the stage (§0):

```text
Battle State Foundation (GAME_STATE.md §2.0)
    battleId, turn, sequence

Board Foundation State (GAME_STATE.md §2.0.5)
    battleId, turn, sequence, board, rngSeed, rngState

Match / Combo accounting (GAME_STATE.md §2.2 — BattleState root; wire label `playerState`)
    battleId, turn, sequence, board, rngSeed, rngState, playerState

Pet / Passive state (GAME_STATE.md §2.3 — PetState)
    battleId, turn, sequence, board, rngSeed, rngState, playerState, petState
```

No gameplay field beyond the stage's own is carried, and no `Status`/lifecycle
value is carried anywhere in the protocol (§8.3).

`turn` and `sequence` are the battle's current values
(`GAME_STATE.md` §2.0.1, §5): once board resolution exists, a client that joins
an already-played battle receives the counters as the resolution left them —
non-zero, and consistent with each other, because the resolution writes both
in one write-back (`GAME_STATE.md` §5.1 item 4). This method does not report a
resolution, a Match, or a Cascade, and it is not an acknowledgement of an
action: it reports state (§4 item 6).

1. **Trigger.** The battle group is joined (§1.2). The server delivers the
   current battle state to the joining client as its first battle-scoped
   message, before any `ReceiveEvents` for that battle. There is no separate
   client request: joining the group is what triggers delivery.
2. **Direction.** Server → client, server-initiated. It is a **push**, not a
   request/response call — there is no `GetBattleState`-style invocation on
   this path.
3. **Scope.** Sent to the joining caller only. Unlike `ReceiveEvents` (§3.3),
   it is not a group broadcast; the joining client is the only recipient that
   needs it.
4. **Payload.** The fields of the implemented `GAME_STATE.md` §2.0 stage (§0
   above), projected one-to-one from that state. Casing is an implementation
   detail (§8.1). No other field may be added to this record: additional state
   is introduced by extending `GAME_STATE.md` §2.0, not by the wire shape.
5. **Initial values.** For a battle with no resolved action, `turn = 0` and
   `sequence = 0` (`GAME_STATE.md` §2.0.2, §2.0.5.2). Board generation is not
   an action resolution and changes neither, so these remain the values
   delivered at the Board Foundation stage; the rule that `sequence`
   otherwise increments by exactly 1 per resolved action is unchanged
   (`GAME_STATE.md` §5).
6. **Relationship to `ReceiveEvents` (§3).** Different purpose and different
   content. `ReceiveEvents` carries the ordered **events** produced by
   resolving one action, keyed by the post-resolution `serverSequence`. It
   carries no battle state. `BattleStateUpdated` carries **state**, and
   carries no events. Neither replaces the other; once resolution exists,
   both are used, and §3 remains the only event-delivery path.
7. **Relationship to `GetBattleState` (§6).** Different mechanism, and §6 is
   unchanged. `GetBattleState` is a request/response snapshot used for
   reconnect/resync, where the client has lost synchronization and must
   re-render from a full `GAME_STATE.md` §2 snapshot (`ADR-008`).
   `BattleStateUpdated` is an unsolicited push on join. §6 is **not**
   redefined as an initial-creation mechanism, and the initial path does not
   require a client invocation.
8. **Not a substitute for §6.** If the joining client is already
   resynchronizing a battle that progressed, the snapshot semantics of §6
   apply; §4 alone is not the documented recovery path.
9. **Ownership.** The payload is authoritative server state
   (`GAME_RULES.md` §18, `ADR-001`). The client stores it as a synchronized
   presentation copy and must never author, adjust, or recompute it.
10. **The board is delivered, never generated by the client.** When the
    payload includes `board` (`GAME_STATE.md` §2.0.5), it carries the
    server-generated `Cells[64]` (`GAME_STATE.md` §2.1.1,
    `MATCH3_RULES.md` §1.2). The client renders the received board and must
    not generate, fill, repair, validate, or re-derive it — no client-side
    RNG participates in any part of it (`GAME_RULES.md` §18,
    `GAME_STATE.md` §2.0.5.4 item 1).
11. **Board staging does not add a method.** `BattleStateUpdated` remains the
    only state-push method in this protocol at every stage (§4 item 11).
    Board-specific messages (`BoardCreated`, `BoardGenerated`, `BoardUpdated`,
    `BoardReady`, or similar) are **not** part of this contract and must not
    be introduced: the board is a field of the battle state, so extending that
    state is what delivers it.
12. **`LastCommittedSwapPair` is not delivered, and that is not a missing
    field.** `GAME_STATE.md` §2.1.10 adds `BattleState.LastCommittedSwapPair`
    to authoritative state — the record `MATCH3_RULES.md` §2.1.4's
    already-applied check reads. It is **not** a member of this payload, and
    this section adds none for it. Item 4 above governs: the record carries
    exactly the implemented `GAME_STATE.md` §2.0 stage's fields, and a field
    that is server-side bookkeeping is not one of them. No exception is needed,
    because the client has no use for the value: `MATCH3_RULES.md` §2.1.4
    makes staleness a server-side decision the client cannot participate in,
    and §2.1.1 item 3 forbids the request from carrying it. The client
    therefore never sends it, never receives it, and must never author,
    adjust, or recompute it (`GAME_RULES.md` §18, `ADR-001`). No new message,
    method, subscription, or payload member is introduced by it
    (`GAME_STATE.md` §2.1.10 item 9).
13. **`PetState` *is* delivered, and item 12 is not the precedent for it.**
    `GAME_STATE.md` §2.3 adds `BattleState.PetState` to authoritative state,
    present from battle creation. Unlike `LastCommittedSwapPair` (item 12), it
    is a **client-facing** field: `PASSIVE_RULES.md` §6 item 1 requires the
    active Pet's Passive progress to be exposed to the player as a UI-facing
    value (e.g. `7 / 10 Matches`), and `PetState.PassiveProgress` is the state
    that value is read from (`GAME_STATE.md` §2.3, §2.5). Item 4 therefore
    applies to it in the ordinary way — it is a field of the implemented stage,
    so it is delivered — and no exclusion is added for it. Its delivery
    contract is §4.3; it adds no message, method, or subscription (§4 item 11),
    and its arrival replaces nothing (item 12's record is unaffected).

This method name is `BattleStateUpdated` for the `BattleState` it delivers,
and is the only state-push method in this protocol. No second or parallel
state-sync method exists — `GameStateSync`, `SyncEverything`, or similar are
not part of this contract.

## 4.1 Delivering the Board Stage

The Board Foundation stage (`GAME_STATE.md` §2.0.5) extends this same push —
it does not add a delivery path, a subscription, or an event:

1. The joining client receives `board`, `rngSeed`, and `rngState` together
   with `battleId`, `turn`, and `sequence` in the single
   `BattleStateUpdated` push.
2. `rngSeed`/`rngState` are delivered as part of the authoritative state
   (`GAME_STATE.md` §2.6). They are included because they are `BattleState`
   fields, not because the client uses them: the client never advances,
   re-seeds, or draws from the RNG, and never uses it to produce a Gem value.
   A client that needs a board reads `board`.
3. `board` is exactly 64 cells (`GAME_STATE.md` §2.1.1). The client does not
   receive a partial board, and does not request cells individually.
4. Board generation happens server-side before the push; `BattleStateUpdated`
   reports the result and never triggers generation. If generation failed
   (`MATCH3_RULES.md` §1.5 item 3), the battle does not exist to be joined.
5. **The board carries its Special Gem state, and needs nothing else.** When
   the Special Gem stage is implemented, `board` carries it with no additional
   payload member and no change to this record's field set: `GAME_STATE.md`
   §2.1 defines `BoardState` as `Cells[64]` alone, and each cell entry holds
   its Gem type and, optionally, the Special Gem at that cell (type, plus
   orientation for a Line Clear Gem). There is no `PendingSpecialGems[]` and
   no second collection to deliver, so §4 item 4 ("no other field may be added
   to this record") is satisfied without an exception: the board is delivered
   as the state holds it, entry for entry, in ascending cell-index order
   (`GAME_STATE.md` §2.1.1 item 6, §2.1.7 items 1–4). `BattleStateUpdated` is
   the only delivery path for it, and this stage adds no method, no
   subscription, and no message (§4 item 11, §3.1 item 3, §8 item 7).
6. **What the client may and may not derive from it.** The client renders the
   board it receives, including its Special Gems, and never computes one: it
   does not create, place, move, match, activate, chain, or clear a Special
   Gem, and it never infers a Special Gem's type or orientation from anything
   other than the state it was sent (`GAME_STATE.md` §2.1.9 item 4,
   `GAME_RULES.md` §18, `TDD.md` §2.1). An omitted Special Gem member on a
   cell means that cell holds an ordinary Gem — it is not an invitation to
   predict one (`GAME_STATE.md` §2.1.7 item 3).

## 4.2 Delivering the Match / Combo Accounting Stage

The Match / Combo accounting stage (`GAME_STATE.md` §2.2 —
`BattleState.Combo`/`MatchCount` at the root; no `PlayerState` path)
extends this same push — it does not add a delivery path, a subscription, or
an event:

1. The client receives `playerState` together with `battleId`, `turn`,
   `sequence`, `board`, `rngSeed`, and `rngState` in the single
   `BattleStateUpdated` push. It is delivered on join (§4.1 trigger) and on
   every committed Swap's resolved-state push (§2.1, §5).
2. `playerState` is the wire projection of `GAME_STATE.md` §2.2's implemented
   fields, and carries **exactly two members**: `combo` and `matchCount`. The
   `playerState` name is a **fixed protocol label** for this Combo/MatchCount
   projection; it is not a state path — this contract has no `PlayerState`
   node (ADR-011). The rest of the battle-time combat and loadout state —
   HP/MaxHP, ATK/DEF/Crit, Power, StatusEffects, EquippedRelics,
   EquippedCards — lives under `PetState` (`GAME_STATE.md` §2.3), belongs to
   the Combat, Passive, Relic, and Card stages, and is **not** delivered here,
   because §4 item 4 admits only the implemented stage's own fields. Referring
   to `playerState` as a whole does not widen that rule.
3. **Both members are always present, and zero is delivered as zero.** Neither
   is nullable and neither is omitted, because both are defined from battle
   creation (`GAME_STATE.md` §2.2): `matchCount` starts at `0` and `combo`
   reads `0` only before the battle's first committed Swap
   (`MATCH3_RULES.md` §6.5 item 4). This is the opposite of the
   absent-Special-Gem convention of `GAME_STATE.md` §2.1.7 item 3 and the
   omitted-commit-record rule of §2.1.10: there is no "not yet occurred" state
   to represent, so absence is never used and a client must not read an
   absent member as zero.
4. **No Combo or Match shape is carried anywhere else.** No `combo` or
   `matchCount` member exists at the top level of the payload, on the board,
   or on any cell: they are delivered only through the fixed `playerState`
   wire label for `BattleState.Combo`/`MatchCount` (§4.2 item 2,
   `GAME_STATE.md` §2.2), and a second spelling
   would be the parallel representation `GAME_STATE.md` §0 item 5 forbids.
5. **The client neither computes nor derives either value.** It renders what it
   was sent (`MATCH3_RULES.md` §6.6 item 3). It does not count a Match, advance
   a Combo, reset one, or re-derive either from `board`, `turn`, or `sequence` —
   `GAME_STATE.md` §5.2 item 2 states that `Sequence` is not a Match, Cascade,
   Combo, or Match-count value and that none of those is derived from it. The
   Swap request still carries no such value (§2.1 item 3), so the client remains
   non-authoritative in both directions (`GAME_RULES.md` §18, ADR-001).
6. **No new message, method, or subscription is introduced.** There is no
   `ComboChanged`, `MatchCountChanged`, `ProgressionUpdated`, or similar
   delivery: this is a state push, not an event (§4 item 6), and the values
   travel exactly as the board and the counters do. `BattleStateUpdated`
   remains the only state-push method (§4 item 11), and the Match/Combo Event
   system — `MatchCreated`, `MatchResolved`, `CascadeCreated`, `ComboChanged`
   (`GAME_EVENTS.md` §2) — is **not** implemented by this stage and is not
   delivered by this section.
7. **Combo is not a `serverSequence` and not an event.** It is a value inside
   the state that the payload's `sequence` versions: the progression values
   become visible together with the board under the new `Sequence`, never
   per-Match or per-step (`MATCH3_RULES.md` §8.3 item 2, `GAME_STATE.md`
   §5.1).

## 4.3 Delivering the Pet / Passive Stage

The Pet / Passive stage (`GAME_STATE.md` §2.3, `PetState`) extends this same
push — it does not add a delivery path, a subscription, or an event:

```text
petState
├── passiveId                 the active Pet's Passive identity   always present
├── passiveProgress            { threshold, current }             always present
└── passiveResetOverride       "Partial" | "NoReset"              present only when
                                                                  non-default
```

1. The client receives `petState` together with `battleId`, `turn`, `sequence`,
   `board`, `rngSeed`, `rngState`, and `playerState` in the single
   `BattleStateUpdated` push. It is delivered on join (§4.1 trigger) and on
   every committed Swap's resolved-state push (§2.1, §5).
2. `petState` is the wire projection of `GAME_STATE.md` §2.3's implemented
   fields, and carries **exactly three members**: `passiveId`,
   `passiveProgress`, and the conditional `passiveResetOverride`. The rest of
   §2.3 — `PetId`/Identity, `Element`, `Tier`/`Star`/`Level` — belongs to the
   Pet identity and progression stage and is **not** delivered, because §4
   item 4 admits only the implemented stage's own fields. Referring to
   `petState` as a whole does not widen that rule.
3. **`passiveId` is always present and is the Passive's identity, not its
   definition.** It carries the same value `PetState.PassiveId` holds
   (`GAME_STATE.md` §2.3) — the identity `GAME_EVENTS.md` §2's
   `PassiveCharged`/`PassiveTriggered` already report. The Passive's
   `Threshold`, `Trigger Type`, `Effect`, and `Reset Behavior`
   (`PASSIVE_RULES.md` §1) are its definition and are **not** members here: no
   second copy of the definition is introduced on the wire, exactly as §2.3
   item 1 forbids one in the state. It is set at battle creation and never
   changes (§2.3 item 2), and there is no absent or null form of it: a battle
   always has its one active Pet and therefore its one Passive (§2.3 item 3).
4. **`passiveProgress` is a nested object with exactly two members** —
   `threshold` and `current`, both integers, both always present — following
   the §3.2 flat-object convention. It is the wire projection of
   `GAME_STATE.md` §2.5's `PassiveProgress` `(Threshold, Current)` pair, and
   the two travel together as one logical field for the same reason
   `RngState`'s two components do (§4.1 item 2): a reader renders the
   documented `Current / Threshold` pair without supplying either from
   elsewhere (`PASSIVE_RULES.md` §6 item 1's `7 / 10 Matches`). Neither member
   is nullable and neither is omitted — `current = 0` is a real publishable
   value (it is what a battle begins with and what `Default` reset writes), so
   absence is never used for it and a client must not read an absent member as
   zero. This is the absent-member convention's opposite, exactly as §4.2
   item 3 states for `combo`/`matchCount`.
5. **`threshold` is the Passive's own Threshold and `current` is the progress
   reached** — the same two values `GAME_EVENTS.md` §2 reports on
   `PassiveCharged`. The state is the authoritative source and the events
   report it; neither is re-derived from the other, and the client recomputes
   neither.
6. **`passiveResetOverride` is present if and only if the Passive's reset
   behavior is non-default** (`GAME_STATE.md` §2.3: "only present if this
   Pet's Passive uses non-default reset behavior"; `PASSIVE_RULES.md` §4
   item 1). When it is present it carries the behavior's contract name as a
   **string** — `"Partial"` or `"NoReset"` (`PASSIVE_RULES.md` §4 item 2) —
   and never a numeric enum ordinal, per §3.2.4's rule for every enum-valued
   wire member.
7. **A default reset omits the member; it is never sent as JSON `null`.** This
   is §3.2.5's convention applied here, and it is the same statement
   `GAME_STATE.md` §2.1.7 item 3 makes for an absent cell `SpecialGem`: the
   absence *is* the statement "this Passive uses the default reset", and no
   `null`, `"Default"` string, or empty value stands in for it. `"Default"` is
   deliberately **not** a third permitted value of this member: it is what the
   member's absence already means (`GAME_STATE.md` §2.3, `PASSIVE_RULES.md`
   §4 item 1), so writing it would be a second spelling of one fact —
   the parallel representation `GAME_STATE.md` §0 item 5 forbids. A consumer
   tolerates absence and reads it as `Default`.
8. **No Passive value is carried anywhere else.** No `passiveId`,
   `passiveProgress`, `threshold`, `current`, or `passiveResetOverride` member
   exists at the top level of the payload, on the board, or on any cell: they
   are fields of `PetState`, and a second spelling would be the parallel
   representation `GAME_STATE.md` §0 item 5 forbids. In particular
   `playerState` gains nothing (§4.2 item 2) — Passive state is not Match /
   Combo accounting.
9. **The client neither computes nor derives any of it.** It renders the
   `current / threshold` pair and the Passive identity it was sent. It does
   not charge a Passive, evaluate a Threshold, reset progress, apply an
   overflow, or re-derive any of those from `board`, `turn`, `sequence`,
   `combo`, or `matchCount` — `PASSIVE_RULES.md` §2–§5 own all of them and the
   server evaluates them (`GAME_RULES.md` §18, `ADR-001`). `PetState` is a
   deliverable, not a client-authorable field.
10. **No new message, method, or subscription is introduced.** There is no
    `PassiveProgressUpdated`, `PetStateChanged`, `PassiveCharged`-style
    *method*, or similar delivery: this is a state push, not an event (§4
    item 6), and the values travel exactly as the board and the Combo/Match
    counters do. `BattleStateUpdated` remains the only state-push method (§4
    item 11), and the Passive Event system — `PassiveCharged`,
    `PassiveTriggered` (`GAME_EVENTS.md` §2) — travels on §3's existing event
    path and is not re-delivered by this section.
11. **The state push and the events are not interchangeable.** `petState`
    reports the Passive's **settled** position under the payload's `sequence`,
    once per resolved action, exactly as the board and the counters do. It is
    not a per-Match progress feed: `PassiveCharged` is emitted once per Match
    within a Cascade (`GAME_EVENTS.md` §1.1, `PASSIVE_RULES.md` §2 item 3),
    and that per-Match detail belongs to §3, not to this push. A client that
    wants the intermediate steps reads the batch; a client that wants the
    current position reads the state. Neither replaces the other (§4 item 6).
12. **Persistence is not extended by this section.** `petState` is delivered
    from whatever `BattleState` the server holds. Whether `PetState` is
    written to Redis is owned by `REDIS_STATE.md` §7 and is unchanged here:
    this section defines the payload member, not the storage contract.

---

# 5. Request/Response Acknowledgement

Hub methods return a direct invocation result to the caller (independent of
the broadcast `ReceiveEvents`):

```json
{ "accepted": true }
```
```json
{ "accepted": false, "reason": "INVALID_SWAP" | "INSUFFICIENT_POWER" | "..." }
```

This lets the client immediately know whether its input was accepted,
without waiting for/parsing the event broadcast.

1. The result is **transport-level feedback about the request**, not
   authoritative state and not a Battle Event: it is not broadcast, it is not
   sequenced, and it changes nothing by itself
   (`GAME_EVENTS.md` §1.2, `GAME_STATE.md` §5.1).
2. `accepted: true` means the action was resolved, and its events arrive in the
   §3 batch. `accepted: false` means the action was rejected under its owning
   domain rule and **nothing happened**: no state changed and no events are
   coming (`MATCH3_RULES.md` §2.1.5). The client must not optimistically
   mutate the board on a rejection, and must not retry a rejection blindly —
   the rejection is deterministic for that board and that action
   (`MATCH3_RULES.md` §2.1.5 item 7).
3. `reason` is a machine-readable rejection code. The codes are owned per
   action by its domain document: for `Swap`, the rejection reasons are the
   failing checks of `MATCH3_RULES.md` §2.1.2 together with the staleness
   rejection of §2.1.4. This document does not maintain a parallel list, so the
   protocol cannot drift from the rule.
4. The result is delivered to the **caller only**, never to the group: a
   rejection is not battle state and no other client has any use for it
   (§2 item 3).

---

# 6. Sequencing & Ordering Guarantees

1. The server processes one action at a time per `battleId` — no two
   resolutions for the same battle run concurrently
   (`REDIS_STATE.md` §4, optimistic lock).
2. If a client sends a new action before receiving `ReceiveEvents` for a
   prior one, the server still resolves them strictly in arrival order.
3. The client must apply `ReceiveEvents` payloads in `serverSequence` order;
   if it receives a `serverSequence` that skips a number, it must treat this
   as state desync (§7).
4. `BattleStateUpdated` (§4) is delivered before any `ReceiveEvents` for that
   battle. It is not sequenced against resolutions and carries no event
   ordering meaning.
5. **In-flight actions are not re-ordered by the client.** Because resolution
   follows arrival order (item 2) and staleness is defined against the board
   rather than against a client version number (`MATCH3_RULES.md` §2.1.4), a
   client that sends two Swaps without waiting will have them resolved in the
   order sent. It must not attempt to predict or roll back the first while the
   second is in flight; it waits for the batch and the result, and renders what
   it receives (§3.1 item 5).

---

# 7. Reconnect & Resync

1. On reconnect, the client calls `GetBattleState(battleId)` (a request/
   response Hub method, not a broadcast) to fetch the current authoritative
   `BattleState` snapshot (`GAME_STATE.md` §2) plus its `Sequence`.
2. The client discards any local prediction and re-renders from this
   snapshot — it does not attempt to replay missed individual events.
3. If `GetBattleState` returns `BATTLE_NOT_FOUND` (state expired/cleared,
   see `REDIS_STATE.md` §3 TTL), the client treats the battle as ended and
   falls back to `GET /api/battle/{battleId}/result` (`API_CONTRACTS.md` §4).

This section is the **reconnect/resync** mechanism only. It is not the
initial-synchronization path — that is §4. The two are not interchangeable:
§4 is an unsolicited push on join carrying the `GAME_STATE.md` §2.0 subset,
while §7 is a client-requested full-snapshot recovery per `ADR-008`.

---

# 8. What Is Not Here

1. **Nothing left undefined about the `ReceiveEvents` event schema.** The exact
   wire schema of `events[]` — discriminator, property casing, enum
   representation, optionality, and each event's members — is owned by §3.2 and
   is **not** an implementation detail. This document owns that schema;
   `GAME_EVENTS.md` §2 owns what each event means and what its payload contains,
   and `GAME_EVENTS.md` §3 item 1 defers the wire shape here. Neither document
   defers to the other: the split is *semantics vs. wire schema*, and §3.2 is
   the single owner of the latter. This is a change from earlier revisions of
   this item, which left the schema to the delivery stage; §3.2 now fixes it.
   What remains an implementation detail is only the *mechanics* of producing
   it (which serializer, which DTO type names) — never the resulting JSON.
   The same applies to the `BattleStateUpdated` record (§4 item 4).
2. Any PvP or multi-client-divergent-state scenario — out of MVP scope
   (`MVP_SCOPE.md` §2).
3. Any battle lifecycle / status message. `BattleStateUpdated` carries no
   `Status` field (`GAME_STATE.md` §2.0.3); battle outcome remains the
   `BattleWon` / `BattleLost` events (`GAME_EVENTS.md` §2).
4. A gameplay-free battle-creation method. The staged state contract (§0)
   adds no way to create a battle over SignalR, and does not weaken
   `POST /api/battle/start` (`API_CONTRACTS.md` §3) — see §2.
5. Any board-specific message (`BoardCreated`, `BoardGenerated`,
   `BoardUpdated`, `BoardReady`, or similar). The board is a field of the
   battle state and is delivered by the existing `BattleStateUpdated` push
   (§4 item 11, §4.1); it has no separate transport contract.
6. Any client → server board method. The client does not ask for a board, a
   cell, or a re-roll: it joins the battle group and receives the board with
   the rest of the state (§4.1). No gameplay method is added by this stage
   (§2).
7. **Any board-resolution-specific message.** The gameplay methods are exactly
   the three of §2 and the delivery paths are exactly the three of §0/§3/§4/§6.
   A committed Swap's board outcome and its resolution events use those
   existing paths (§3.1); no `MatchCreated`-style *method*, no
   `BoardResolved`, no `CascadeUpdated`, no `SpecialGemActivated` message, and
   no additional subscription exists or may be introduced. Extending the state
   (`GAME_STATE.md` §2.0) is how new board data is delivered; extending this
   protocol's method list is not. Special Gem state is no exception: it is part
   of `BoardState` (`GAME_STATE.md` §2.1), so it is delivered by the existing
   §4 push and reported, when it clears cells, by the existing `GemMatched`
   event (`GAME_EVENTS.md` §2) — not by a new message.
8. **An event log or replay channel.** Events are delivered once per resolution
   and are not re-delivered; a desynchronized client resynchronizes from a
   snapshot (§7, ADR-008), never by requesting missed events.
