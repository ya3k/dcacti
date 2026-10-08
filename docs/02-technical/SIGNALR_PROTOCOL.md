# SignalR Protocol

**Version:** 2.19 (§7 **item 4 added** — the reconnect group re-join rule: on a
successful reconnect the client re-adds its new connection to the battle's group
by invoking `JoinBattle(battleId)`. Group membership is connection-scoped and is
never restored by the server, and the re-join completes before §7 item 1's
`GetBattleState` snapshot request is issued, so the new connection is a member of
the battle's group before normal post-reconnect battle event delivery is
expected. The existing `JoinBattle` push (§4.1) and the existing
`GetBattleState` snapshot remain the authoritative existing delivery paths.
**No wire member, member set, hub method, event shape, delivery path,
subscription, or gameplay rule is added or removed by this revision**: §2's
method list is unchanged, `JoinBattle` is still the one group join (§2.2) and
`BattleStateUpdated` is still the only state-push method (§4 item 11), no
server-side automatic group restoration exists, and §7 item 1's snapshot
contract is unchanged. Prior 2.18: (§4.3 item 13's **Signature Skill identification rule
corrected** per TASK-213 §5's decision, implemented by TASK-219A. The client now
identifies the active Pet's derived Signature Skill from the **delivered Pet
read** — `API_CONTRACTS.md` §5.1's `signatureSkill` member, which carries that
Pet's existing `PetDefinition.SignatureSkillCardId` reference — instead of from
`category === "PetSkill"` read out of the owned Card collection, which can never
contain a `PetSkill` row (`CARD_RULES.md` §1 item 4, ADR-012 item 9). Item 13
now states that the Signature Skill is **derived from the active Pet** and is
**not an independently owned Card**, that `API_CONTRACTS.md` §5.3's membership is
**unchanged and must not be widened**, that `PetSkillCast` is the **canonical
client request** for that entry, and that `CardCast(<signature cardId>)` remains
protocol-conformant and remains implemented. Item 13's claim that the client
"maintains no local registry and performs no card validation" is corrected to
what the client actually does — it holds no authoritative Card registry, renders
only the values the delivered reads carry, and validates no Card. **No wire
member, member set, hub method, event shape, delivery path, subscription, or
gameplay rule is added or removed by this revision**: §2's method list is still
the three of §2, `equippedCards` still carries exactly the 4 `CardDefinitionId`
entries item 13 already fixed, and §5.3 remains the §5 ownership-based Card
surface it was. The §5.1 member this rule consumes is owned by
`API_CONTRACTS.md`. Prior 2.17: (§4.3's `petState` projection widened by three members and
§4.4's `bossState` projection by one, applying the TASK-208 Product Owner
decisions D-208-01, D-208-02, D-208-03 and D-208-04. `petState` gains `hp`,
`maxHp` and `power` — the active Pet's current HP, Max HP and Power, read
one-to-one from `PetState.HP` / `PetState.MaxHP` / `PetState.Power`
(`GAME_STATE.md` §2.3) — and `bossState` gains `bossId`, the Boss's canonical
technical Identity read one-to-one from `BossState.BossId` (`GAME_STATE.md`
§2.4, `BOSS_RULES.md` §6.4). All four members are always present, non-nullable,
never omitted and never `null`, and zero is sent as `0`; the projection computes,
clamps, scales, and rounds nothing, and `maxHp` is never derived from `hp`. §4
gains item 18, §4.3 gains item 15, and §4.4 gains item 10, authoring the widened
member sets; §4.3 item 2 and §4.4 items 2, 3, 4 and 5 restate them; §7's parity
paragraph names them; and §4 item 15's claim that the state push was already the
authoritative record of the resulting `Power` is corrected — the push carries it
from this revision onward. The **state projection is the reconstruction-safe
carrier** for these values: `DamageDealt`/`DamageTaken` carry only `amount`,
healing emits no event at all, and §7 item 2 discards events on resync without
replaying them. `GAME_STATE.md` §2.3/§2.4 and `BOSS_RULES.md` §6.2.6 record the
same delivery split and are updated by this revision. **Nothing else changes:**
§2's method list is unchanged (three gameplay methods, no fourth),
`BattleStateUpdated` remains the only state-push method (§4 item 11), the two
projections ride its existing trigger and its existing join and resolution
pushes, the delivery triggers, the storage contract, and the event schema are
unchanged, and no `BossState` member beyond the delivered
`hp`/`maxHp`/`bossId` becomes client-visible. Implementing the projection and
consuming it on the client are separate downstream tasks. Prior 2.16: §1 authentication aligned with ADR-020 — standalone web account JWT ApplicationSession replaces residual Discord access-token references; prior 2.15: §4.3's `petState` projection widened by one member and §4's
push widened by one nested object, applying the TASK-160 Product Owner
decisions D-1A and D-2A. `petState` gains `statusEffects[]` — the **active
Pet's currently active** Status Effect instances, rendered per instance by
reference to `GAME_STATE.md` §2.3.2 item 1's `statusEffects` element shape —
and the push gains a new `bossState` member carrying **exactly** `hp` and
`maxHp`. §4 item 2's closing sentence, which stated that "delivering those
instances remains a protocol change owned by its own task", now records that
TASK-160's decision was that task. §4 item 17 and §4.4 are new and own the two
projections' optionality, absence, ordering and client-boundary rules; §7.1
states that the same projection serves the reconnect snapshot. **Nothing else
changes:** §2's method list is unchanged (three gameplay methods, no fourth),
`BattleStateUpdated` remains the only state-push method (§4 item 11), the two
projections ride its existing trigger and its existing join/resolution pushes,
`bossState` is a narrowed projection rather than `BossState`, and no `BossState`
member beyond `hp`/`maxHp` becomes client-visible. Implementing the projection
and consuming it on the client are separate downstream tasks. Prior 2.14: (§3.2.24 `PowerChanged` reconciled with the TASK-150 Product
Owner decisions D-1–D-8, applying `GAME_EVENTS.md` §2's semantics to the wire
owner: the `source` member's closed value set gains a fourth value, `"boss"`,
so it is now `"match"`, `"card"`, `"relic"`, or `"boss"`; item 1 no longer ties
`delta`'s sign to a source kind; item 3 projects the four-value set; item 5
states the owning-stage emission rule and defines `"boss"` as a Boss-owned Power
mutation; item 6 states the one-mutation-one-event rule and that the events
preserve authoritative mutation order; and §3.2.2 item 5's emission
cross-reference is corrected from "the Power stage" to the owning-stage rule.
**No wire member, member set, event shape, delivery path, method, discriminator,
or gameplay rule is added or removed by this revision** — the payload is still
`type`, `delta`, `power`, `source`, and this revision adds a `source` *value*
rather than a member. Implementing the emission is a separate downstream task.
Prior 2.13: (§2.2 added to document `Ping` as an authenticated technical connectivity probe / non-gameplay transport utility alongside `JoinBattle`, resolving the contract ownership gap identified in TASK-146; zero wire member, event shape, delivery path, or gameplay rule is changed. Prior 2.12: §3.2.2 item 5, §3.2.23 item 4, and §3.2.24 item 5 synchronized
with the landed Relic stage: `GAME_RULES.md` §17 step 11 now emits
`RelicTriggered` for each Relic whose effect applies and `PowerChanged` with
`source: "relic"` where a Relic changes Power (TASK-133). **No wire member,
member set, event shape, delivery path, method, or gameplay rule is changed by
this revision** — the two shapes were already final and both emissions conform to
them; only the statements that recorded the emission as *not implemented* are
corrected. That entry also recorded that the Match and Card `PowerChanged`
sources remained their own stages' — **superseded by 2.14**, which states the
owning-stage emission rule for all four sources.
Prior 2.11: (§4 item 16 added and §4.3 item 2's exclusion list extended —
`PetState.ATKModifiers[]` (`GAME_STATE.md` §2.3.7, TASK-136 D10) is **not** a
wire member and adds no payload member, message, method, or subscription. TASK-136
D10 made the treatment conditional on whether the existing projection already
exposes the relevant `PetState` fields; §4.3 item 2 answers that it does **not**
(`petState` carries exactly four members, and `ATKModifiers[]` is now named
among the §2.3 members explicitly not delivered), so item 4 governs and no new
ATK-specific network contract is introduced. The revision records explicitly that
no `ATKModifierApplied`, `ATKModifierExpired`, or `ATKChanged` event exists or
may be introduced, that the ATK value already reaches the client as
`DamageCalculated.base` (§3.2.13) — a value change, not a member-set change —
and that `RelicTriggered` (§3.2.23) keeps its `{ type, relicId }` shape. No
§2/§3 member set, delivery path, event shape, or gameplay rule is changed by this
revision. Prior 2.10: (§4 item 15 added and §4.3 item 2's exclusion list extended —
`PetState.CardCostModifiers[]` (`GAME_STATE.md` §2.3.5, `ADR-018`, TASK-134
D9) is **not** a wire member and adds no payload member, message, method, or
subscription: item 4 governs, item 13's `PetState` exception was made for
`PassiveProgress` because `PASSIVE_RULES.md` §6 item 1 requires that value to be
visible to the player, and no document requires a Card-cost modifier to be
exposed — the authoritative record of the resulting `Power` is the existing
state push and the existing `PowerChanged` (§3.2.24), whose `"card"` source
value already covers a Card-cost spend (a case that remains in the set — 2.14
widens `"card"` beyond cost spends to any Card-owned Power mutation). The
revision records explicitly that no
`CardCostModifierApplied`, `CardCostModifierExpired`, or `CardCostChanged`
event exists or may be introduced, and that `RelicTriggered` (§3.2.23) keeps its
`{ type, relicId }` shape. No §2/§3 member set, delivery path, event shape, or
gameplay rule is changed by this revision. Prior 2.9: §4.3 `petState` wire projection extended per TASK-121 to include
`equippedCards` (readonly string[], 4 `CardDefinitionId` entries: 3 Basic Cards +
1 derived Signature Skill Card); resolves the client loadout visibility contract
for `CardCast` and `PetSkillCast` action paths in `BattleScene` without client
authority or secondary REST state retention. Prior 2.8: §4 item 14 added — `PetState.NextAttackCritModifiers[]`
(`GAME_STATE.md` §2.3.4, `ADR-017`) is **not** a wire member and adds no
payload member, message, method, or subscription: item 4 governs, item 13's
`PetState` exception was made for `PassiveProgress` because
`PASSIVE_RULES.md` §6 item 1 requires that value to be visible to the player,
and `COMBAT_RULES.md` §3.3 item 6 already fixes the Crit outcome's sole
representation to step 4's combined `otherModifiers` multiplier. No §2/§3/§4
member set, delivery path, or gameplay rule is changed by this revision.
Prior 2.7: §3.2.2's discriminator set extended and the four documented
events projected — `CardCast`, `PetSkillCast`, `RelicTriggered`, and
`PowerChanged` added to the table and given member tables at §3.2.20–§3.2.24,
with `CardCast`/`PetSkillCast` emission order fixed at §3.2.22 and the
`effect summary` convention reconciled at §3.2.25; the set is now closed
against events `GAME_EVENTS.md` §2 does not define rather than against events
it does. Applied per the **TASK-104 A-1A / A-2C / A-3 / A-4B / A-5A** Product
Owner rulings. Two stale count statements are corrected in the same revision:
§3.2.12 item 1's "The seven events carry exactly the members tabulated above"
was already wrong (the table it referred to listed 12 names) and is replaced by
an enumeration of all 16, and §3.2.4 item 3's "in any of the four events" — a
phrase about enum-member scope, not the discriminator set — is widened to "in
any event". No SignalR method, no §4/§6
delivery path, and no gameplay rule is changed by this revision. Prior 2.6:
§1 connection authentication made explicit per `ADR-015` —
the application session is the self-contained signed JWT of
`API_CONTRACTS.md` §2.8 supplied through SignalR's standard access-token
mechanism, a Discord access token is never an accepted `BattleHub`
credential, and a missing/invalid/tampered/expired session is rejected by
the authentication/authorization boundary with no second mechanism inside
the hub; prior 2.5: §3.2.19 notes 1 and 3 corrected — the false
`GAME_EVENTS.md` §2 cross-citations replaced with the actual
BattleWon/BattleLost block, and `outcome` confirmed as the single
`victory`/`defeat` vocabulary that block owns per TASK-050 human
Decision C; prior 2.4: §3.2.16–§3.2.18 `sourceId` boss values corrected to the
canonical technical Boss Identity `"boss-hoa-long"` per `BOSS_RULES.md` §6.4 /
TASK-046 — superseding the prior 2.1 display-name correction; prior 2.3:
§7.1 snapshot projection excludes the non-wire
`BattleState.PlayerId` — GAME_STATE.md §2.8, ADR-014; prior 2.2:
Player/Pet role model per ADR-011 — state-path references
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
   during web authentication (`POST /api/auth/register` or `POST /api/auth/login`,
   `API_CONTRACTS.md` §2, `ADR-020`): that session is the self-contained signed JWT
   `ApplicationSession` defined by `API_CONTRACTS.md` §2.3 (`ADR-015`), supplied
   through SignalR's standard access-token mechanism.
4. Only valid JWT `ApplicationSession` tokens are accepted as `BattleHub`
   authentication credentials; raw external identity tokens are never accepted.
5. A connection presenting a missing, invalid/tampered, or expired session
   is rejected by the authentication/authorization boundary before the hub
   is usable. `BattleHub` defines no second authentication mechanism of its
   own — it remains transport-focused (`ADR-015` D6).
6. `BattleHub` and realtime game handlers deal exclusively with the
   authenticated application player session (`ApplicationSession`), with zero direct
   dependency on external client SDKs or embedded platform runtimes.

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

### 2.2 Non-Gameplay Transport Calls

Two non-gameplay client → server calls exist on `BattleHub`:

#### `JoinBattle`

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

#### `Ping`

```text
Ping(clientSequence?)
```

1. **Purpose:** A technical connectivity probe and keepalive/latency check
   retained for connection verification. It carries no gameplay meaning and is
   not a gameplay action: it submits no game input, does not require an active
   `battleId`, reads zero `BattleState`, mutates zero `BattleState`, touches
   zero Redis state, and emits zero Battle Events.
2. **Parameters:**
   - `clientSequence` (optional `string?`): opaque correlation identifier
     supplied by the caller, echoed back in the response.
3. **Return payload (`PingResponse`):** Delivered directly to the caller only
   (never broadcast):
   - `accepted` (`boolean`): `true` upon receipt on the authenticated
     connection.
   - `clientSequence` (`string?`): the echoed correlation identifier provided
     by the client, or `null`/absent if none was passed.
   - `serverTime` (`string`): ISO 8601 UTC timestamp (`DateTimeOffset`)
     recorded by the server when handling the probe.
4. **Authentication:** Inherits connection authentication (§1 item 3,
   `ADR-015`). The hub connection itself requires a valid JWT Bearer application
   session; unauthenticated callers cannot establish a connection to invoke
   `Ping`.
5. **Protocol Isolation:** `Ping` is not sequenced by `BattleState.Sequence`
   (§6, `GAME_STATE.md` §5). It does not advance or affect turns, combos,
   matches, or actions, and has no relationship to battle recovery or reconnect
   resynchronization (§7).

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
| `type` | string | one of `MatchCreated`, `CascadeCreated`, `ComboChanged`, `GemMatched`, `DamageCalculated`, `DamageDealt`, `DamageTaken`, `PassiveCharged`, `PassiveTriggered`, `BossSkillCast`, `BattleWon`, `BattleLost`, `CardCast`, `PetSkillCast`, `RelicTriggered`, `PowerChanged` |

1. **The property name is `type`.** The string spelling matches the event name
   `GAME_RULES.md` §16 lists and `GAME_EVENTS.md` §2 defines.
2. **The encoding is the string**, never the numeric enum value and never a
   camelCase variant. The allowed values are exactly the names in the
   discriminator table above. **The set is closed against events that
   `GAME_EVENTS.md` §2 does not define** — not against events it does define:
   a name `GAME_RULES.md` §16 lists and `GAME_EVENTS.md` §2 gives a payload for
   is a valid `type` once this section tabulates its members (§3.2.20, §3.2.21,
   §3.2.23, §3.2.24).
   No `MatchResolved`, no `SpecialGemActivated`, no `TurnChanged`, no
   `BoardChanged`, and no other name is a valid `type`: those are either state
   (`SIGNALR_PROTOCOL.md` §3.1 item 3, §8 items 5–7) or undefined
   (`GAME_EVENTS.md` §2 item 3). This is the TASK-104 A-1A ruling; the four
   events it admits are projected by their own subsections below, and
   `GAME_EVENTS.md` §3 item 5 already required that every event §2 defines be
   delivered.
3. **The enum is projected to its name, not to its ordinal.** The ordinal
   (`BattleEventType`) is a Domain identity and is not part of the wire
   contract; renumbering the enum must not change the wire.
4. **No numeric alias and no second discriminator exist.** `type` is the only
   member that identifies the event, and it is always present. An item without
   a recognized `type` is malformed, not a fifth event.
5. **Admitting a §2 event adds no method and no state-push member.** The
   gameplay methods remain exactly the three of §2 (§8 item 7), and the §4/§6
   delivery paths are unchanged. `RelicTriggered` and `PowerChanged` are
   projected by schema here, and their emission is owned elsewhere and is not
   restated here: `RelicTriggered` by `RELIC_RULES.md` §7 (`GAME_RULES.md` §17
   step 11), and `PowerChanged` by whichever stage owns the `PetState.Power`
   mutation — the Power, Card, Relic, or Boss Response stage, per
   `GAME_EVENTS.md` §2 and §3.2.24 item 5. The Relic stage emits both for its
   own Relic — `RelicTriggered` per Relic whose effect applies, and
   `PowerChanged` with `source: "relic"` where a Relic changes Power
   (§3.2.23, §3.2.24) — so this contract fixes their shape for whichever stage
   emits them (`GAME_EVENTS.md` §3 item 7's sequencing position), and no member
   is added to either by that stage.

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
3. **No numeric enum value is ever sent for an enum member**, in any event or
   in the nested match/special-gem objects.
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

1. **The events tabulated above carry exactly the members this section gives
   them** — the twelve of §3.2.6–§3.2.19 plus the four of §3.2.20, §3.2.21,
   §3.2.23, and §3.2.24 (§3.2.22 owns their order and §3.2.25 owns the
   `effect summary` convention; neither is an event). No
   event carries a `battleId`, a `serverSequence`, a `turn`, a board, a `combo`,
   a `matchCount`, a timestamp, a GUID, or a generated id: the first two belong
   to the envelope (§3), and the rest are state (§3.1 item 3) or nonexistent
   (`AGENTS.md` §11). (The prior revision's first sentence read "The seven events
   carry exactly the members tabulated above". That count was already wrong
   before this revision — the discriminator table it referred to listed 12
   names — and it is now doubly wrong, because this revision adds four more.
   It is replaced by the enumeration above, which accounts for all 16. §3.2.4
   item 3's unrelated "**in any of the four events**" phrase, which described
   the scope of the enum-representation rule rather than the discriminator set,
   is widened to "in any event" in the same revision.)
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
    "sourceId": "boss-hoa-long",
    "progress": 3,
    "threshold": 5
}
```

| Member | Type | Presence | Meaning |
| --- | --- | --- | --- |
| `type` | string | always | `"PassiveCharged"` |
| `passiveId` | string | always | the Passive's identity (`GAME_STATE.md` §2.3 `PetState.PassiveId` or §2.4 `BossState.PassiveId`) |
| `source` | string | always | `"pet"` or `"boss"` — which entity's Passive charged (`GAME_EVENTS.md` §2) |
| `sourceId` | string | always | the identity of the owning entity — `PetState.PetId` (Pet instance identity) or `BossState.BossId` (canonical technical Boss Identity, `BOSS_RULES.md` §6.4; never a display name) |
| `progress` | int | always | the new progress value after the increment (`PASSIVE_RULES.md` §2) |
| `threshold` | int | always | the Passive's threshold (`PASSIVE_RULES.md` §1) |

1. **`source` discriminates Pet Passive from Boss Passive.** This event is
   shared by both systems (`PASSIVE_RULES.md` §7, `BOSS_RULES.md` §3).
   The string is `"pet"` or `"boss"`, matching the `DamageDealt`/`DamageTaken`
   convention for party identifiers (§3.2.14 item 1).
2. **`sourceId` identifies the specific entity.** For a Pet Passive, it is
   `PetState.PetId` (the Pet instance identity); for a Boss Passive, it is
   `BossState.BossId` — the canonical technical Boss Identity (e.g.
   `"boss-hoa-long"`), per `BOSS_RULES.md` §6.4, never the Boss's display
   name. The client uses both `source` and `sourceId` to attribute the event.
3. **`passiveId` is the same value the owning entity's state holds.** It is
   never re-derived or invented by the emitting stage (`GAME_EVENTS.md` §2
   item 1).

### 3.2.17 `PassiveTriggered`

```json
{
    "type": "PassiveTriggered",
    "passiveId": "boss-hoa-long-rage",
    "source": "boss",
    "sourceId": "boss-hoa-long",
    "progress": 5,
    "threshold": 5
}
```

| Member | Type | Presence | Meaning |
| --- | --- | --- | --- |
| `type` | string | always | `"PassiveTriggered"` |
| `passiveId` | string | always | the Passive's identity |
| `source` | string | always | `"pet"` or `"boss"` |
| `sourceId` | string | always | the identity of the owning entity (same semantics as §3.2.16) |
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
3. **`effect summary` is omitted**, under the §3.2.25 convention. It is not
   tabulated here, and — because `GAME_EVENTS.md` §2 item 3 records that the
   member is not yet populated — its absence must not be read as "no effect
   occurred": it means the effect is not yet reported (§3.2.25 item 5).

### 3.2.18 `BossSkillCast`

```json
{
    "type": "BossSkillCast",
    "skillId": "flame-burst",
    "sourceId": "boss-hoa-long"
}
```

| Member | Type | Presence | Meaning |
| --- | --- | --- | --- |
| `type` | string | always | `"BossSkillCast"` |
| `skillId` | string | always | the Boss Skill's identity (`BOSS_RULES.md` §4) |
| `sourceId` | string | always | the Boss's canonical technical Identity (`BossState.BossId` — `BOSS_RULES.md` §6.4; never a display name) |

1. **`skillId` identifies which Boss Skill was used.** It is the same
   identity the boss definition carries (`BOSS_RULES.md` §6.4 — e.g.
   `"flame-burst"`) — the client uses it to look up the
   skill's visual and effect description (`BOSS_RULES.md` §4, §6).
2. **`sourceId` identifies the Boss.** In MVP there is exactly one Boss per
   battle, but the field is present for future-proofing and consistency with
   `PassiveCharged`/`PassiveTriggered` (§3.2.16). The value is the Boss's
   canonical technical Identity (`BossState.BossId`, e.g. `"boss-hoa-long"`)
   per `BOSS_RULES.md` §6.4 — never the display name.
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
   `"defeat"` — the two `Outcome` values defined by `GAME_EVENTS.md` §2
   (BattleWon / BattleLost), which own the value set for this event and
   for the same battle's persisted (`DATABASE.md` §1) and REST
   (`API_CONTRACTS.md` §4) forms. The string form is consistent with
   other discriminator-style wire members (`source`, `gemType`). Note that
   `source` is a **shared member name whose value set is per-event** — §3.2.14
   and §3.2.16 carry one set each, and §3.2.24 carries a third — so it is
   "discriminator-style" in form, not in having a single global vocabulary
   (§3.2.24 item 3).
2. **`finalBossHp` and `finalPlayerHp` are the terminal HP values.** They
   are the state values at the moment the battle ended, after all damage
   from the final action has been applied. `finalPlayerHp` carries the
   **active Pet's** HP (`PetState.HP`); the wire member name is a fixed
   protocol label and does not imply a Player HP pool (ADR-011). The client
   uses them for end-of-battle display.
3. **`reward summary` is omitted**, under the §3.2.25 convention. It is
   defined for the `BattleWon` event payload only (`GAME_EVENTS.md` §2
   BattleWon / BattleLost); the data shape is owned by `DATABASE.md`, and this
   schema carries no member for it and no deferral note.

### 3.2.20 `CardCast`

```json
{
    "type": "CardCast",
    "cardId": "card-shield"
}
```

| Member | Type | Presence | Meaning |
| --- | --- | --- | --- |
| `type` | string | always | `"CardCast"` |
| `cardId` | string | always | the cast Card's `CardDefinitionId` (`GAME_STATE.md` §2.3 `EquippedCards[]`, `DATABASE.md` §1) |

1. **`cardId` is the definition identity the cast named.** It is the same
   value `PetState.EquippedCards[]` holds (`GAME_STATE.md` §2.3 — a
   `CardDefinitionId`, `DATABASE.md` §1), the same value the
   `CardCast(battleId, cardId, clientSequence)` request carries (§2), and the
   same identity `API_CONTRACTS.md` §5.3 spells `cardId`. There is no second
   Card identifier at any layer (`GAME_STATE.md` §2.3: "There are no Card
   instances"), so this member is a reported identity, never a re-derived one
   (`GAME_EVENTS.md` §2 item 1's "read and reported" convention, §3.2.16 item 3).
   It is camelCase, which is the spelling §3.2.3 item 1 requires and the
   spelling this section — not `GAME_EVENTS.md` §2's PascalCase prose — fixes
   for the wire.
2. **The event carries no Power cost member, and no other member.** The
   TASK-104 **A-2C** ruling records that MVP `CardCast` reports **no** Power
   cost: `GAME_EVENTS.md` §2 lists a cost element, but §3.2.12 item 3 forbids a
   member `GAME_EVENTS.md` §2 does not define, and admitting an undefined cost
   member would require inventing its name, type, and value set. The Card's
   Cost is a definition value owned by `CARD_RULES.md` §2 — not a wire member —
   and the authoritative record of what a cast did to `PetState.Power` is the
   state value itself, delivered by the §4 push (`GAME_STATE.md` §2.3). A client
   that must show the spent Cost reads the Card's definition; a client must not
   recompute it from the event (`GAME_RULES.md` §18, ADR-001).
3. **`effect summary` is omitted, under the §3.2.25 ruling.** It is not
   tabulated here and no deferral note is written for it — see §3.2.25.
4. **Emitted for every successful cast, after the Cost is deducted and the
   Effect applied.** `CARD_RULES.md` §3 item 4 fixes that order, and §6 fixes
   that `CardCast` fires "for every successful Basic Card or Pet Skill Card
   cast". A rejected cast emits nothing (§3.1 item 4, `CARD_RULES.md` §3
   item 3).
5. **It precedes `PetSkillCast` when both are emitted** — §3.2.22.

### 3.2.21 `PetSkillCast`

```json
{
    "type": "PetSkillCast",
    "cardId": "card-inferno"
}
```

| Member | Type | Presence | Meaning |
| --- | --- | --- | --- |
| `type` | string | always | `"PetSkillCast"` |
| `cardId` | string | always | the cast Signature Skill Card's `CardDefinitionId` |

1. **Same `cardId` member as §3.2.20, same identity, same spelling.** The
   Signature Skill is expressed as one Pet Skill Card (`CARD_RULES.md` §4
   item 1), so its `CardDefinitionId` is the identity this event reports, and it
   is already present in the loadout snapshot (`GAME_STATE.md` §2.3
   `EquippedCards[]`, which holds the 3 submitted Basic Cards **plus** the
   derived Signature Skill Card).
2. **The `cardId` value is the Signature Skill confirmation — no dedicated
   member is added.** This is the TASK-104 **A-4B** ruling. `GAME_EVENTS.md` §2
   says `PetSkillCast` "additionally confirms it was the active Pet's Signature
   Skill" but names no member; that confirmation is carried by the `cardId`
   identity, because each Pet has exactly one Signature Skill expressed as one
   Pet Skill Card (`CARD_RULES.md` §4 item 1) and it is derivable server-side
   from `PetDefinition.SignatureSkillCardId` (`CARD_RULES.md` §1 item 4). No
   `skillId`, no boolean flag, and no other member is introduced
   (`GAME_EVENTS.md` §2 defines none, and §3.2.12 item 3 forbids inventing one).
3. **The client resolves the confirmation from the value it already holds.**
   `cardId` alone suffices for a client that knows the active Pet's loadout from
   the §4 push; the event needs no second member to be decodable, and this is
   the same "identity member" shape §3.2.16/§3.2.18 establish for
   `passiveId`/`skillId`/`sourceId`.
4. **Omitted `effect summary`, as in §3.2.20 item 3 (§3.2.25).**
5. **It is emitted only when the cast Card is the Signature Skill, and always
   in addition to `CardCast`** — never instead of it (`CARD_RULES.md` §6: "in
   addition to `CardCast`). A Basic Card cast therefore produces no
   `PetSkillCast`.

### 3.2.22 `CardCast` / `PetSkillCast` Emission Order

The two events of a Pet Skill Card cast are ordered, and the order is part of
the contract. This subsection owns that order; §3.2.20 and §3.2.21 own each
event's shape.

1. **`CardCast` is emitted first, then `PetSkillCast`.** The TASK-104 **A-5A**
   ruling fixes this order. It is the order `GAME_EVENTS.md` §1's list already
   displays, and it matches the resolution semantics: every successful cast
   emits `CardCast` (`CARD_RULES.md` §6), and `PetSkillCast` is the additional
   event that refines it, not a prerequisite of it.
2. **The two are emitted together, at the same point in the resolution, in one
   batch.** `CARD_RULES.md` §3 item 4 places both after "Apply Effect" and
   before the downstream Relic step; `CARD_RULES.md` §6 places both at
   `GAME_RULES.md` §17 step 14 ("Resolve Player Effects"). This subsection fixes
   their **relative** order only — it adds no resolution step, and
   `GAME_RULES.md` §17's step list is unchanged (step 14 remains one step; no
   18a/19a-style expansion is created).
3. **Both travel in the same `ReceiveEvents` batch** as every other event of
   that cast's resolution (§3 item 1), under the same post-resolution
   `serverSequence` (§3 item 2). The batch is atomic at the message level
   (§3.1), so a client never sees one without the other.
4. **A Basic Card cast emits `CardCast` alone** — no `PetSkillCast`, and
   therefore no ordering question arises. This is the §3.2.21 item 5 rule seen
   from the other side (`CARD_RULES.md` §6).
5. **The order is a property of the contract, not of an implementation's
   iteration.** The projection is a pure field mapping that sorts nothing
   (§3.2.1 item 3), so the executor emits the pair in this order and the
   transport preserves it.

### 3.2.23 `RelicTriggered`

```json
{
    "type": "RelicTriggered",
    "relicId": "relic-berserker-core"
}
```

| Member | Type | Presence | Meaning |
| --- | --- | --- | --- |
| `type` | string | always | `"RelicTriggered"` |
| `relicId` | string | always | the triggered Relic instance identity |

1. **`relicId` is the identity `RELIC_RULES.md` §2.2 item 3 fixes** — the same
   value `PetState.EquippedRelics[]` holds, and the same "identity, not a
   definition" member shape §2.3 records for `PassiveId` and §2.4 for `BossId`.
   `RELIC_RULES.md` §2.2 item 3 already states the array element and this
   member are one identity, so this subsection reports it rather than defining
   a second one.
2. **`effect summary` is omitted**, by the same §3.2.25 ruling.
3. **`GAME_EVENTS.md` §2 lists a "deterministic order index for this event" and
   it is NOT a wire member here.** `RELIC_RULES.md` §4 owns the deterministic
   trigger order, and that order is a property of the **emission sequence** —
   the array is already in it (§3.2.1 item 3, §3.2.12 item 4). Carrying an index
   as well would be a second spelling of the array's own order, which
   `GAME_STATE.md` §0 item 5 forbids; the order is read from position, exactly
   as it is for the resolution's other ordered events. The member is therefore
   declined rather than deferred, and `GAME_EVENTS.md` §2's payload list is left
   to its own owner for any wording correction.
4. **Emission is implemented by the Relic stage, and this contract fixes only the
   shape.** The emission **point** is owned by `RELIC_RULES.md` §7 — fired at
   `GAME_RULES.md` §17 step 11 ("Trigger Relics") — and the trigger, condition,
   and deterministic order by `RELIC_RULES.md` §3/§4, with the
   Trigger/Condition/Effect representation owned by `RELIC_RULES.md` §8
   (TASK-131 D1–D11, `ADR-018`); those are the specific governing rules, and this
   subsection does not restate them. The stage that owns that step emits the
   event — one `RelicTriggered` per Relic whose effect actually applies
   (`RELIC_RULES.md` §7), in the equip-slot order §4.2 fixes
   (`GAME_EVENTS.md` §3 item 7's sequencing position, applied to this event).
   This subsection fixes the wire shape for that emitter, adds no Relic engine,
   and fires no trigger (§3.2.2 item 5).
5. **The wire shape is confirmed final and carries no effect summary.**
   (TASK-131 **D10**) This member set — `type` and `relicId` — is the decided
   shape, not a placeholder awaiting a follow-up decision. A Relic's resulting
   state reaches the client through the existing `BattleState` projection
   (§4) and not through this event, which is why item 2's §3.2.25 omission
   ruling stands unchanged. No member is added to `RelicTriggered` by
   `RELIC_RULES.md` §8's contract.

### 3.2.24 `PowerChanged`

```json
{
    "type": "PowerChanged",
    "delta": 25,
    "power": 45,
    "source": "card"
}
```

| Member | Type | Presence | Meaning |
| --- | --- | --- | --- |
| `type` | string | always | `"PowerChanged"` |
| `delta` | int | always | the signed change applied to `PetState.Power` |
| `power` | int | always | `PetState.Power` **after** the change |
| `source` | string | always | `"match"`, `"card"`, `"relic"`, or `"boss"` |

1. **`delta` is signed, and its sign is the mutation's own.** The value is the
   change the mutation actually applied to `PetState.Power` — positive for a
   gain, negative for a spend — and `delta = 0` is a real value where it occurs
   and is sent as `0`, following §3.2.8 item 2's rule for a zero-valued member.
   The sign is a property of the **mutation**, not of the source: a `"card"`
   delta is negative when the cast pays its cost and positive when the cast's
   effect grants Power (item 5), and a `"boss"` delta is negative for a
   Power-draining effect. A reader must not infer the direction from `source`.
2. **`power` is the resulting value, not a delta and not the previous value.**
   It is `PetState.Power` (`GAME_STATE.md` §2.3) after this change, and
   `GAME_RULES.md` §12's 0–100 range is an invariant of that state, not
   something this event re-derives.
3. **`source` shares its name with §3.2.14 and §3.2.16, and carries a
   different value set.** All three answer "what is the origin of this report",
   which is why the name is shared (§3.2.7 item 2's convention for a shared
   member name); the *value set* each reports is owned by its own event
   definition. For this event it is the set `GAME_EVENTS.md` §2 names —
   "source (Gem match / Card / Relic / Boss)" — projected to its documented
   lowercase contract name: `"match"`, `"card"`, `"relic"`, or `"boss"`. This
   is the string-enum convention of §3.2.4 and the same value-projection rule
   §3.2.14 item 1 applies to `DamageParty`; the ordinal, if any, is a Domain
   identity and never reaches the wire.
   It is **not** a `DamageParty` (§3.2.14's `"player"`/`"boss"`) and it is
   **not** §3.2.16's entity-owner `"pet"`/`"boss"`: those answer "which side"
   and "which entity", while this answers "which stage owns the Power
   mutation". The shared spelling `"boss"` across these events is exactly the
   shared-name convention above: the value sets are per-event, so `"boss"` here
   denotes a Boss-owned **Power mutation** and not the damage or Passive
   entity-owner meaning it carries at §3.2.14 and §3.2.16. A consumer must
   read `source` together with `type` — a member name alone does not fix a value
   set anywhere in this schema (§3.2.19 item 1, §3.2.7 item 2).
4. **This is not the Card cost member.** `CardCast` deliberately carries no cost
   (§3.2.20 item 2, TASK-104 A-2C). Where a Power change must be reported, this
   event reports it authoritatively; the two are not reconciled by adding a cost
   member back to `CardCast`.
5. **Emission is owned by the stage that owns the mutation.** Every
   authoritative gameplay mutation of `PetState.Power` emits this event, and the
   stage that performs the mutation is the stage that emits it:
   `"match"` for the Power stage's resource generation, `"card"` for the Card
   stage's cost and Power effects, `"relic"` for the Relic stage's `Power`
   effect, and `"boss"` for the Boss Response stage's Power-draining secondary
   effect — `BOSS_RULES.md` §6.3.1 item 2's Drain Power is the provisioned
   case. What each source means, and which stage owns it, is
   `GAME_EVENTS.md` §2's; this subsection fixes only the wire shape for
   whichever stage emits it, and §3.2.2 item 5 applies. No separate or
   centralized emission step is introduced. A `"card"` mutation is **not** a
   cost-only case: `"card"` covers any Power mutation a Card cast owns.
6. **One mutation, one event; the events preserve authoritative order.** When
   one action performs several Power mutations — a Card cast that both pays a
   cost and applies a Power effect is the documented case — each mutation emits
   its own `PowerChanged` carrying that mutation's `delta` and the resulting
   `power`, in the authoritative mutation order `GAME_RULES.md` §17 fixes.
   Several mutations are never combined into one net event, and a consumer
   applying the events in delivered order therefore reaches the same `power`
   the state holds. This adds no ordering rule of its own: the order is the
   resolution's existing order, carried by the batch like every other event's
   (§3.2.1 item 3, §3.2.12 item 4). A mutation that does not occur emits
   nothing.

### 3.2.25 The `effect summary` Convention — Omission

**This subsection owns how this schema disposes of a payload element
`GAME_EVENTS.md` §2 lists but the wire cannot yet represent.** It resolves the
contradiction between §3.2.17 item 3 and §3.2.18, which previously gave two
different answers to the same question.

1. **The convention is OMISSION.** A §2 payload element that no rule yet
   populates is **not a wire member**: it is not tabulated in the event's
   member table, and no deferral note is written for it. This is the TASK-104
   **A-3** ruling ("explicit omission").
2. **What "explicit omission" means here, and why it is not silence.** The
   element is omitted **deliberately and is recorded as such in this
   subsection** — one place, once — rather than being left as an unexplained
   gap in each event table. The event's member table therefore carries exactly
   what is sent, and a reader who notices that `GAME_EVENTS.md` §2 lists an
   unrepresented element finds the governing rule here rather than having to
   infer it from three inconsistent subsections. The §2 payload list itself is
   owned by `GAME_EVENTS.md` and is not restated, rewritten, or contradicted by
   this section (`GAME_EVENTS.md` §3 item 1).
3. **This supersedes §3.2.17 item 3's deferral wording.** The prior revision
   declared `effect summary` "deferred … and is not a wire member yet" on
   `PassiveTriggered`, while §3.2.18 disposed of the same element on
   `BossSkillCast` by silent omission and §3.2.19 item 3 used a third wording.
   Under this ruling **one** convention governs: where such an element is
   omitted, it is omitted, and this subsection is the single statement of that
   fact. §3.2.17 and §3.2.18 are not otherwise changed — their member tables
   already carry exactly what is sent, which is what the ruling requires.
4. **It applies to every event with such an element**, including
   `CardCast`/`PetSkillCast` (§3.2.20 item 3, §3.2.21 item 4),
   `RelicTriggered` (§3.2.23 item 2), `PassiveTriggered` (§3.2.17), and
   `BossSkillCast` (§3.2.18).
5. **Absence means "not reported yet", never "no effect occurred."** This
   preserves `GAME_EVENTS.md` §2 item 3's substantive point for
   `PassiveTriggered` — a reader must not read a missing `effect summary` as
   "no effect happened" — while dropping the deferral *wording* in favour of
   the omission convention. The element is added to the emitted value by the
   stage that implements the effect (`GAME_EVENTS.md` §2 item 3, §3 item 7),
   and adding it then is a member addition owned by that stage's task.
6. **`reward summary` follows the same convention** (see §3.2.19 item 3): it is
   defined for the `BattleWon` payload by `GAME_EVENTS.md` §2, its data shape is
   owned by `DATABASE.md`, and it is not a wire member. This subsection fixes
   no shape for it and adds none.
7. **No new convention is invented, and no member is added.** The ruling selects
   between the two conventions the document already contained rather than
   authoring a third, and it adds no wire member to any event.

---

# 4. Initial State Delivery (`BattleStateUpdated`)

Defines how the client first receives the authoritative battle state, so the
runtime foundation (`BattleState → SignalR → GameRuntime → BattleScene`) can
be built before gameplay exists (`GAME_STATE.md` §0, §2.0).

```text
BattleStateUpdated(battleId, turn, sequence, board, rngSeed, rngState,
                   playerState, petState, bossState)
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

Pet Status Effects / Boss HP (GAME_STATE.md §2.3.1, §2.4)
    … , petState, bossState        (§4.3 items 2 and 14, §4.4)
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
   Several members are **narrowed projections** of the state field they report
   rather than the whole field: `playerState` (§4.2 item 2), `petState` (§4.3
   item 2) and `bossState` (§4.4 item 2) each carry an enumerated member set,
   and each section states that referring to the state field as a whole does
   not widen it. Adding a member to one of those sets is a protocol change
   owned by its own task, exactly as adding a top-level member is.
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
    that value is read from (`GAME_STATE.md` §2.3). Item 4 therefore
    applies to it in the ordinary way — it is a field of the implemented stage,
    so it is delivered — and no exclusion is added for it. Its delivery
    contract is §4.3; it adds no message, method, or subscription (§4 item 11),
    and its arrival replaces nothing (item 12's record is unaffected).

14. **`NextAttackCritModifiers[]` is not delivered, and item 13 is not the
    precedent for delivering it.** `GAME_STATE.md` §2.3.4 adds
    `PetState.NextAttackCritModifiers[]` to authoritative state (`ADR-017`) —
    temporary source-specific Crit modifiers awaiting consumption by a
    qualifying owner attack. It is **not** a member of this payload, and this
    section adds none for it. Item 4 governs: the record carries exactly the
    implemented stage's fields, and item 13's exception was made for
    `PassiveProgress` specifically because `PASSIVE_RULES.md` §6 item 1
    requires that value to be exposed to the player. No document requires a
    Crit modifier to be exposed: `COMBAT_RULES.md` §3.3 item 6 fixes the Crit
    outcome's representation to step 4's combined `otherModifiers` multiplier
    and states that "no separate Crit event, state property, or wire member is
    emitted", and §3.3 item 7 makes Effective Crit a value computed for the
    current pipeline execution rather than a stored member. The client
    therefore neither receives nor needs the collection. It is also not a
    field the client may infer: it must never compute, predict, or reconstruct
    a Crit modifier from events, from `otherModifiers`, or from the Crit value
    (`GAME_RULES.md` §18, `ADR-001`). No new message, method, subscription, or
    payload member is introduced by it (`GAME_STATE.md` §2.3.4 item 8), and it
    replaces nothing in item 12's or item 13's record.

15. **`CardCostModifiers[]` is not delivered, and no new event carries it.**
    `GAME_STATE.md` §2.3.5 adds `PetState.CardCostModifiers[]` to authoritative
    state (`ADR-018`, TASK-134 D1/D2) — applied, Battle-scoped Card-cost
    modifiers. It is **not** a member of this payload, and this section adds
    none for it. Item 4 governs: the record carries exactly the implemented
    stage's fields, and item 13's exception was made for `PassiveProgress`
    specifically because `PASSIVE_RULES.md` §6 item 1 requires that value to be
    exposed to the player. No document requires a Card-cost modifier to be
    exposed: it changes the cost of a Card the server validates and charges
    (`CARD_RULES.md` §3.6), and the authoritative record of the resulting
    `Power` is the state push's `petState.power` (§4.3 item 15) together with
    `PowerChanged`
    (`SIGNALR_PROTOCOL.md` §3.2.24) — which already carries the `"card"` source
    value for a Card-cost spend. (The state push did **not** carry `Power`
    before the `TASK-208` **D-208-02** amendment, so the sentence this revision
    corrects asserted something that was false at the time; it is accurate now.
    `PowerChanged` remains the per-change report and is not the
    reconstruction-safe carrier — §7 item 2 — and neither statement authorizes
    a Card-cost member: `CardCostModifiers[]` stays undelivered, and `power`
    authorizes no client-side cost, affordability, or legality computation.)
    The client therefore neither receives nor
    needs the collection. It is also not a field the client may infer: it must
    never compute, predict, or reconstruct a Card-cost modifier from events,
    from a `PowerChanged` delta, or from a Card's definition
    (`GAME_RULES.md` §18, `ADR-001`). Delivering it would be a protocol change
    owned by its own task (`GAME_STATE.md` §2.3.5 item 10).

    **No new event, method, or subscription is introduced by it** — in
    particular there is no `CardCostModifierApplied`,
    `CardCostModifierExpired`, or `CardCostChanged` event. A modifier's apply,
    refresh, and removal are state mutations (`GAME_STATE.md` §5.1.3) reported
    by the existing state push and, where Power actually changes, by the
    existing `PowerChanged` (`§3.2.24`); `RelicTriggered` (`§3.2.23`) remains
    the only Relic-side event and keeps its `{ type, relicId }` shape. It
    replaces nothing in item 12's, item 13's, or item 14's record.
16. **`ATKModifiers[]` is not delivered, and no new event carries it.**
    `GAME_STATE.md` §2.3.7 adds `PetState.ATKModifiers[]` to authoritative
    state (TASK-136 D1/D2) — applied, Battle-scoped ATK modifiers. It is **not**
    a member of this payload, and this section adds none for it. TASK-136 **D10**
    decided exactly this, conditionally on whether the existing projection
    already exposes the relevant `PetState` fields; item 4 and §4.3 item 2
    answer that condition:

    - **The existing projection does not expose it.** §4.3 item 2 fixes
      `petState` to its enumerated member set and names
      `ATKModifiers[]` among the §2.3 members that are explicitly **not**
      delivered. `ATKModifiers[]` is therefore **not a wire member**, per
      item 4's rule that a payload carries only the implemented stage's own
      fields. Item 13's exception was made for `PassiveProgress` specifically
      because `PASSIVE_RULES.md` §6 item 1 requires that value to be exposed to
      the player; no document requires an ATK modifier to be exposed.
    - **No new contract is introduced.** TASK-136 D10 forbids a new ATK-specific
      network contract, and none is required: the ATK value the Relic modifier
      produces already reaches the client as `DamageCalculated.base`
      (`§3.2.13`) — a **value** change, not a member-set change, exactly as
      `COMBAT_RULES.md` §5.4 and §5.6 state for their own rules.
    - **The client neither receives nor needs the collection, and may not infer
      it.** It must never compute, predict, or reconstruct an ATK modifier from
      events, from a `DamageCalculated` value, or from a Relic's definition
      (`GAME_RULES.md` §18, `ADR-001`).
    - **No new event, method, or subscription is introduced by it** — in
      particular there is no `ATKModifierApplied`, `ATKModifierExpired`, or
      `ATKChanged` event. A modifier's apply, refresh, and removal are state
      mutations (`GAME_STATE.md` §5.1.4) reported by the existing state push;
      `RelicTriggered` (`§3.2.23`) remains the only Relic-side event and keeps
      its `{ type, relicId }` shape. Delivering the collection would be a
      protocol change owned by its own task (`GAME_STATE.md` §2.3.7 item 10).
17. **`PetState.StatusEffects[]` *is* delivered, for the active Pet and under
    `petState`, and item 13 is not the precedent for it.** `GAME_STATE.md`
    §2.3.1 defines the collection of active Status Effect instances on the
    active Pet, and item 4 governs it in the ordinary way — it is a field of
    the implemented stage, so it is delivered. The projection is **narrowed**,
    not whole: only the active Pet's instances travel, only under the existing
    `petState` member, and only through the element shape and rules §4.3
    item 14 states. This is a **Product Owner decision**, not a derivable fact:
    `TASK-160`'s **D-1A** ruling authorized it, and the exclusion sentence
    item 2 previously carried closed with "delivering these instances remains a
    protocol change owned by its own task" — this *is* that task, so item 2's
    surviving exclusion list is re-worded there rather than contradicted here.
    Nothing else about the collection changes: it adds no top-level member
    (`petState` already exists), no method, no event, and no subscription, and
    it delivers no `PetState` member beyond `statusEffects[]` itself — the
    sibling collections item 14 (`NextAttackCritModifiers[]`), item 15
    (`CardCostModifiers[]`) and item 16 (`ATKModifiers[]`) decline remain
    **not delivered** and are unaffected by this item. `BossState.StatusEffects[]`
    is likewise **not** delivered (§4.4 item 3). The combat-stat subset (item 18)
    is a later, separate member-set widening; it does not change this item's
    scope or reopen any member this item leaves undelivered.
18. **The active Pet's combat-stat subset *is* delivered — `hp`, `maxHp` and
    `power` — and item 13 is not the precedent for it.** `GAME_STATE.md` §2.3
    defines the active Pet's current HP, Max HP, and Power, and item 4 governs
    them in the ordinary way — they are fields of the implemented stage, so they
    are delivered. The projection is **narrowed**, not whole: only those three
    members travel, only under the existing `petState` member, and only through
    the member rules §4.3 item 15 states. This is a **Product Owner decision**,
    not a derivable fact: `TASK-208`'s **D-208-01** and **D-208-02** rulings
    authorized them, and §4.3 item 2's exclusion sentence previously named them
    among the §2.3 members not delivered — this *is* that task, so item 2's
    surviving exclusion list is re-worded there rather than contradicted here.
    The same carrier delivers the Boss's canonical Identity under `bossState`
    (§4.4 item 10); the two widenings are one amendment and neither adds a
    top-level member.

    The state projection is the **reconstruction-safe carrier** for these
    values. They are authoritative `BattleState`, carried member for member by
    the join push, by every committed Swap's resolved-state push, and by the §7
    reconnect/resync snapshot alike, and they cannot be reconstructed from the
    event stream: `DamageDealt`/`DamageTaken` carry only `amount` (§3.2.14,
    §3.2.15), healing emits no event at all, and §7 item 2 discards events on
    resync without replaying them. `power` in particular is already
    authoritative, persistent state whose only prior client carrier was the
    transient `PowerChanged` (§3.2.24) — one event per mutation, lost on resync.
    Nothing else about the stage changes: this item adds no top-level member
    (`petState` already exists), no method, no event, and no subscription, and
    it delivers no `PetState` member beyond those three — the sibling declines
    item 14 (`NextAttackCritModifiers[]`), item 15 (`CardCostModifiers[]`) and
    item 16 (`ATKModifiers[]`) remain **not delivered**, as do `PetId`/Identity,
    `Element`, the Tier/Star/Level axes, `ATK`/`DEF`/`Crit`, `EquippedRelics[]`,
    and every remaining collection of §2.3.

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
├── passiveId                 the active Pet's Passive identity          always present
├── passiveProgress            { threshold, current }                    always present
├── passiveResetOverride       "Partial" | "NoReset"                     present only when
│                                                                        non-default
├── equippedCards              string[] (4 CardDefinitionId entries)     always present
├── statusEffects[]            Status Effect instances of the ACTIVE Pet  always present
│                               — an array, `[]` when none is active      (§4.3 item 14)
├── hp                         the ACTIVE Pet's current HP                always present
│                                                                        (§4.3 item 15)
├── maxHp                      the ACTIVE Pet's Max HP                    always present
│                                                                        (§4.3 item 15)
└── power                      the ACTIVE Pet's Power (0–100 invariant)  always present
                                                                         (§4.3 item 15)
```

1. The client receives `petState` together with `battleId`, `turn`, `sequence`,
   `board`, `rngSeed`, `rngState`, and `playerState` in the single
   `BattleStateUpdated` push. It is delivered on join (§4.1 trigger) and on
   every committed Swap's resolved-state push (§2.1, §5).
2. `petState` is the wire projection of `GAME_STATE.md` §2.3's implemented
   fields, and carries **eight members**: `passiveId`, `passiveProgress`, the
   conditional `passiveResetOverride`, `equippedCards`, `statusEffects[]`,
   `hp`, `maxHp`, and `power`.
   The rest of
   §2.3 — `PetId`/Identity, `Element`, `Tier`/`Star`/`Level`, the remaining
   combat stats (`ATK`, `DEF`, `Crit`),
   `NextAttackCritModifiers[]`, `CardCostModifiers[]`, `ATKModifiers[]`, and
   `EquippedRelics[]` —
   belongs to other subsystems or server-only calculation and is **not**
   delivered, per §4 item 4's rule that a payload carries only the implemented
   stage's own fields. Referring to `petState` as a whole does not widen that
   rule. `StatusEffects[]` was on that exclusion list until `TASK-160`'s
   **D-1A** Product Owner decision delivered it (item 14), and the combat-stat
   triple (`HP`, `MaxHP`, `Power`) was on it until `TASK-208`'s **D-208-01** and
   **D-208-02** decisions delivered them (item 15); those four members are
   therefore the members this sentence's list no longer names, and every other
   named member remains excluded.
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
   `GAME_STATE.md` §2.3's `PassiveProgress` `(Threshold, Current)` pair, and
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
13. **`equippedCards` is the active Pet's battle-scoped loadout.** It carries
    the 4 `CardDefinitionId` strings (`GAME_STATE.md` §2.3, `CARD_RULES.md`
    §1) — the 3 submitted Basic Cards plus the active Pet's derived Signature
    Skill Card. It is always present, non-empty, and non-nullable.
    - **Bootstrap and synchronization:** Snapshotted once at battle creation
      (`POST /api/battle/start`, `API_CONTRACTS.md` §3), it does not mutate
      during the battle. It is delivered to the client on group join
      (`JoinBattle`, §4.1) and on every subsequent `BattleStateUpdated` push,
      as well as in the reconnect snapshot (`GetBattleState`, §7, `ADR-008`).
    - **Client usage boundary:** The presentation layer (`BattleScene` via
      `GameRuntimePort`) reads `runtime.getBattleState().petState.equippedCards`
      to render interactive casting controls and dispatch action requests
      (`CardCast`, `PetSkillCast`, §2). Full card definitions and effect
      mechanics are resolved server-side (`CARD_RULES.md` §3): the client holds
      no authoritative Card registry, renders only the values the delivered
      reads carry (`API_CONTRACTS.md` §5.1's Pet Signature Skill reference and
      §5.3's unlocked Cards), and performs no card validation, cost,
      affordability, or cast-legality computation.
    - **Signature Skill identification — from the Pet, never from the owned
      Card collection.** Exactly one entry in `equippedCards` is the active
      Pet's derived Signature Skill: `CARD_RULES.md` §4 item 1 gives each Pet
      exactly one Signature Skill, and `API_CONTRACTS.md` §3 composes the
      snapshot as the 3 submitted Basics plus that derived entry. That entry is
      the `CardDefinitionId` `API_CONTRACTS.md` §5.1's `signatureSkill.cardId`
      names — the Pet's own `PetDefinition.SignatureSkillCardId`, which is the
      reference the derived entry was resolved from — so the client identifies
      it by matching the entry against that **delivered Pet reference**. It must
      **not** be identified by reading a `Category` out of the owned Card
      collection: a `PetSkill` `CardDefinition` is never a `PlayerUnlockedCard`
      row (`CARD_RULES.md` §1 item 4, ADR-012 item 9), so `GET /api/cards`
      (`API_CONTRACTS.md` §5.3) can never contain the Signature Skill and a
      `PetSkill` category test over that collection can never succeed.
      - **It is derived, and it is not an independently owned Card.** The
        entry follows the active Pet (`CARD_RULES.md` §4 item 2) and is never
        owned, acquired, or persisted as an ownership row (`CARD_RULES.md` §1
        item 4, ADR-012 item 9). `API_CONTRACTS.md` §5.3's membership is
        therefore **unchanged and must not be widened** to deliver it, and the
        Pet read is the one source for it — no second Signature Skill source
        exists.
      - **`PetSkillCast` is the canonical client request.** Client-side
        invocation of `PetSkillCast(battleId, clientSequence)` (§2) does not
        require a card or skill identifier; the server resolves it from this
        same loadout. **`CardCast(<signature cardId>)` remains
        protocol-conformant and remains implemented** (§2, §3.2.20): both
        methods reach the same Card cast path and emit the same event order, and
        this item fixes only which path the **client** uses for the Signature
        Skill. A Basic Card is unaffected and keeps `CardCast` as its client
        path (§2, §3.2.21).
14. **`statusEffects[]` is the active Pet's own active instances, and it is
    always present as an array.** It carries `PetState.StatusEffects[]`
    (`GAME_STATE.md` §2.3, §2.3.1) for the **active Pet** — the subject
    `petState` already describes — and for that entity: no other entity's
    collection travels under it, and no second collection is introduced. This
    is the `TASK-160` **D-1A** Product Owner decision, and §4 item 17 records
    that it is a decision rather than a derived fact.

    - **Source.** The value is `GAME_STATE.md` §2.3.1's collection on the
      active Pet, which `GAME_STATE.md` §5.1.1 mutates and which the single
      post-resolution write-back (`GAME_STATE.md` §5.1) commits. It is read
      from that state and reported; the push derives nothing and filters
      nothing beyond projecting the entity it already names.
    - **Member name and casing.** `statusEffects` — the same name
      `GAME_STATE.md` §2.3.2 item 1 fixes for the serialized collection, and
      the same name the `petState` object's sibling members already use
      (`equippedCards`). §3.2.3 item 1's camelCase rule governs it, and the
      name is stated explicitly rather than left to a naming policy
      (§3.2.3 item 2). It is a **new wire name** for this record: the member
      did not exist on `petState` before this revision.
    - **Type.** An **array** of Status Effect instance objects. Each element is
      the element shape `GAME_STATE.md` §2.3.2 item 3 fixes for the serialized
      collection — `id` (string, required), `type` (string, required; one of
      `"DoT"`, `"BuffDebuff"`, `"Shield"`, `"State"`), `source` (string,
      required; `"player"` or `"boss"`), `magnitude` (number, required),
      `targetStat` (string, optional), `remainingTurns` (integer, optional),
      `expiryCondition` (string, optional) — in the same `camelCase` spelling
      and with the same optional-member presence rules. This subsection
      **references** that shape and states no second one: `GAME_STATE.md`
      §2.3.2 owns the element's existence, type and meaning, as it does for the
      serialized record.
    - **Always present, and the empty case is `[]`.** The member is never
      omitted and never `null`, and an active Pet with no active effect is sent
      an **empty array**. This is `GAME_STATE.md` §2.3.2 item 1's
      always-present-collection rule — "it is never omitted and never `null`" —
      carried onto the wire, and it is the **opposite** of §3.2.5's
      omitted-when-not-applicable convention: §3.2.5 governs a member that does
      not apply to an event, while this member always applies and its empty
      value is a real published value, exactly as §4.2 item 3 states for
      `combo`/`matchCount` and §4.3 item 4 for `current`. A client must
      therefore **not** read an absent `statusEffects` as "no effect is active",
      and a producer must never spell "no effect is active" by omitting it.
    - **Per-element optionality stays omission.** Within an element, the three
      optional members follow `GAME_STATE.md` §2.3.1 item 7 — absent when they
      do not apply, never `null`, never a sentinel string. That is §3.2.5's
      convention in its own domain, and it is unchanged here.
    - **Ordering is not semantic, and it is preserved.** No rule reads element
      positions (`GAME_STATE.md` §2.3.1 item 10), so this projection imposes no
      ordering and a client must not infer one; the array is delivered in the
      order the state holds it, which is what the round trip preserves
      (`GAME_STATE.md` §2.3.2 item 6).
    - **The client renders it and computes none of it.** It displays the
      instances the active Pet actually holds. It does not apply, refresh,
      decrement, expire, or remove one, does not evaluate a duration or an
      expiry condition, and does not re-derive the collection from `board`,
      `turn`, `sequence`, `playerState`, or any event. `GAME_STATE.md` §5.1.1
      and `COMBAT_RULES.md` §5 own that lifecycle and the server performs it
      (`GAME_RULES.md` §18, `ADR-001`).
    - **The state push and the events are not interchangeable, and no event
      carries this list.** §4 item 6 applies unchanged: `statusEffects[]`
      reports the **settled** collection under the payload's `sequence`, once
      per resolved action. No Battle Event carries a Status Effect instance
      collection, and this item introduces none: `GAME_EVENTS.md` §2's events
      report the *changes* a resolution made, this member reports the resulting
      state, and neither replaces the other.
    - **No new method, event, subscription, or persistence behavior.** The
      member rides the existing `BattleStateUpdated` push on its existing
      trigger — join (§4.1 item 1) and every committed Swap's resolved-state
      push (§2.1, §5). It adds no method to §2's three, no event to §3, and no
      subscription, and it changes no storage contract: the collection is
      already part of `BattleState` and already serializes with it
      (`REDIS_STATE.md` §2 item 1, §7 item 9), so this revision defines a wire
      member and nothing about persistence.
15. **`hp`, `maxHp` and `power` are the active Pet's own live combat values,
    and they are always present.** They carry `PetState.HP`, `PetState.MaxHP`,
    and `PetState.Power` (`GAME_STATE.md` §2.3) for the **active Pet** — the
    subject `petState` already describes — and for that entity: no other
    entity's values travel under these names, and no second representation of
    any of them is introduced. This is the `TASK-208` **D-208-01**/**D-208-02**
    Product Owner decision, and §4 item 18 records that it is a decision rather
    than a derived fact.

    - **Source.** The values are `GAME_STATE.md` §2.3's `HP`, `MaxHP` and
      `Power` on the active Pet, mutated by the landed resolution pipeline in
      the single post-resolution write-back (`GAME_STATE.md` §5.1). They are
      read from that state and reported; the push derives nothing, filters
      nothing beyond projecting the entity it already names, and computes,
      clamps, scales, and rounds nothing.
    - **Member name and casing.** `hp`, `maxHp` and `power` — the same names
      the serialized Redis battle record already uses for the same state members
      (`REDIS_STATE.md` §2 item 1, §7), and the same `maxHp` spelling the sibling
      `bossState` member and the REST battle-start summary already use
      (`§4.4` item 5, `API_CONTRACTS.md` §3). §3.2.3 item 1's `camelCase` rule
      governs them, and the names are stated explicitly rather than left to a
      naming policy (§3.2.3 item 2). All three are **new wire names** for this
      record: none of them existed on `petState` before this revision.
    - **Type.** All three are integers. `hp` and `maxHp` are the Pet's current
      and maximum health; `power` is the Card/Skill resource, whose `0–100`
      range is the documented invariant of `GAME_RULES.md` §12 and
      `COMBAT_RULES.md` §1.1 — an invariant of that state, not a value this
      projection re-derives, and **not** a member: no `maxPower` member is
      added, because no such state member exists.
    - **Always present, and zero is sent as zero.** All three are defined from
      battle creation (`GAME_STATE.md` §2.3), so there is no absent or "not yet
      available" case: none is nullable, none is omitted, and `power = 0` — the
      value a battle begins with — as well as a terminal `hp = 0` are real
      published values that are sent as `0`. This is the always-present
      convention §4.2 item 3 and §4.3 item 4 state for their own members, not
      §3.2.5's omitted-when-not-applicable one, and a client must not read an
      absent member as zero.
    - **`maxHp` is delivered alongside `hp`, and it is never derived from it.**
      The projection applies no unit, scale, clamp, or rounding, and it never
      re-derives one of the three from another — the same rule §4.4 item 5
      states for the Boss's pair.
    - **The client renders them and computes none of them.** It displays the
      three values it was sent. It does not damage, heal, clamp, or infer one
      from another, does not reconstruct either HP value from
      `DamageCalculated`/`DamageDealt`/`DamageTaken` (which carry only `amount`,
      §3.2.13–§3.2.15) or expect a heal event (none exists), and does not
      re-derive `power` from `PowerChanged` (§3.2.24) or from any event: §7
      item 2 discards events on resync and never replays them, so the state
      projection is the reconstruction-safe carrier. `power` also does **not**
      authorize a client-side cost, affordability, or cast-legality
      computation: the effective cost is composed server-side
      (`CARD_RULES.md` §3.6), `CardCostModifiers[]` remains undelivered (§4
      item 15), and the server remains the only validator of a cast
      (`GAME_RULES.md` §18, `ADR-001`).
    - **The state push and the events are not interchangeable.** `hp`, `maxHp`
      and `power` report the **settled** values under the payload's `sequence`,
      once per resolved action, exactly as the board and the counters do. The
      events report the changes a resolution made, and `PowerChanged` remains
      the per-mutation feed for `power` (§3.2.24); neither replaces the other
      (§4 item 6). A client that wants the change reads the batch; a client that
      wants the current position reads the state.
    - **No new method, event, subscription, or persistence behavior.** The three
      members ride the existing `BattleStateUpdated` push on its existing
      trigger — group join (§4.1 item 1) and every committed Swap's
      resolved-state push (§2.1, §5) — and, because the reconnect snapshot is
      the same projection (§7), they are carried there too. They add no method
      to §2's three, no event to §3, and no subscription, and they change no
      storage contract: the values are already part of `PetState` and already
      serialize with it (`REDIS_STATE.md` §2 item 1, §7 item 9).

## 4.4 Delivering the Boss Projection

The Boss projection (`GAME_STATE.md` §2.4, `BossState`) extends this same
push — it does not add a delivery path, a subscription, or an event:

```text
bossState
├── bossId                      the Boss's canonical technical Identity  always present
│                               (BOSS_RULES.md §6.4, e.g. `"boss-hoa-long"`) (§4.4 item 10)
│                               — never a display name
├── hp                          the Boss's current HP      always present
└── maxHp                       the Boss's MaxHP            always present
```

1. The client receives `bossState` together with `battleId`, `turn`, `sequence`,
   `board`, `rngSeed`, `rngState`, `playerState`, and `petState` in the single
   `BattleStateUpdated` push. It is delivered on join (§4.1 trigger) and on
   every committed Swap's resolved-state push (§2.1, §5).
2. **`bossState` carries exactly three members, and it is a projection, not
   `BossState`.** The record is `bossId`, `hp` and `maxHp` and nothing else.
   The three are a **narrowed projection** of `GAME_STATE.md` §2.4's
   authoritative
   `BossState`, in the same sense §4.2 item 2 and §4.3 item 2 use for their own
   objects — and, as those sections state, referring to the state field as a
   whole does not widen the enumerated member set. A client must not read the
   object as the Boss's state.
3. **Which `BossState` fields are NOT delivered — explicitly.** Every other
   §2.4 member stays server-side: `Element`, `ATK`, `DEF`,
   `State`, `PassiveId`, `PassiveProgress`, `SkillCharge`, `SkillCooldown`, and
   `StatusEffects[]`. This list is exhaustive for the current tree, and no
   member of it may be added to `bossState` without a protocol change owned by
   its own task. In particular **Boss `StatusEffects[]` is not delivered**: the
   `TASK-160` **D-2A** ruling authorized Boss live health only, the canonical
   Identity is a separate authorization (item 10, `TASK-208` **D-208-03**), and
   §4 item 17 records the same boundary from the Pet side. `BossState` is
   therefore still not a wire member — `bossState` is a three-member projection
   *of* it, which is the opposite of exposing it.
4. **All three members are always present, and none is optional.** `BossId`,
   `HP` and `MaxHP` are defined from battle creation (`GAME_STATE.md` §2.4: a
   battle always has its one Boss, at full health in its Initial State), so
   there is
   no absent or "Boss not yet available" case for any member: none is
   nullable, none is omitted, and `hp = 0` is a real published value — the
   terminal value of a won battle — that is sent as `0`. This is the
   always-present convention §4.2 item 3 and §4.3 item 4 state for their own
   members, not §3.2.5's omitted-when-not-applicable one. A client must not
   read an absent `hp` as zero.
5. **Types.** `hp` and `maxHp` are integers, read from `BossState.HP` and
   `BossState.MaxHP` (`GAME_STATE.md` §2.4) and reported unchanged: the
   projection applies no
   unit, scale, clamp, or rounding, and it never re-derives one member from the
   other. `bossId` is a **string**, read from `BossState.BossId`
   (`GAME_STATE.md` §2.4, `BOSS_RULES.md` §6.4) and reported unchanged. The wire
   spelling is `camelCase` per §3.2.3 item 1, `maxHp` matching
   the casing the existing REST battle-start summary already uses for the same
   state member (`API_CONTRACTS.md` §3), and `bossId` matching the spelling that
   summary and the battle-start request already use for the Boss identity.
6. **There is no "Boss unavailable" or terminal-state variant.** A battle is
   created against exactly one Boss, and the projection reports the Boss's
   current HP for as long as the battle exists. When the battle ends, the
   result is expressed by the existing events — `BattleWon`/`BattleLost` and
   their terminal `finalBossHp` (`§3.2.19`, `GAME_EVENTS.md` §2) — and, if the
   battle is then removed, by the documented state-read outcomes
   (`§7 item 3`, `REDIS_STATE.md` §3), which are unchanged. This item adds no
   third spelling of "the Boss's HP at the end".
7. **The client neither computes nor derives any of them.** It renders the
   values it was sent. It does not damage the Boss, clamp `hp` to `maxHp`,
   infer `MaxHP` from a damage report, predict a remaining-HP percentage, or
   re-derive `hp`/`maxHp` from
   `DamageCalculated`/`DamageDealt`/`DamageTaken`
   or from `finalBossHp` (`GAME_RULES.md` §18, `ADR-001`). `bossId` is the Boss
   identity and nothing more (item 10): the client does not author it, does not
   reconstruct it from an event `sourceId` or from its own record of the battle
   it asked for, and reads it as sent. This is
   authoritative Boss state made renderable, not client-side Boss logic.
8. **No new message, method, or subscription is introduced.** There is no
   `BossStateUpdated`, `BossHpChanged`, `BossHpUpdated`, `BossIdentityChanged`,
   or similar delivery: this is a state push, not an event (§4 item 6), and the
   values travel
   exactly as the board and the counters do. `BattleStateUpdated` remains the
   only state-push method (§4 item 11). Any requirement for client-visible Boss
   state was a separate future protocol decision; that requirement was made, and
   it is answered for the Boss's health and canonical Identity by this section
   (items 4–5 and item 10) and declined for Boss Status Effects by item 3 above —
   the decisions authorized `hp`/`maxHp` (`TASK-160` **D-2A**) and `bossId`
   (`TASK-208` **D-208-03**), and the rest of `BossState` remains undelivered.
9. **Persistence is not extended by this section.** `bossState` is delivered
   from whatever `BattleState` the server holds. `BossState` is already part of
   that state and already serializes with it under the existing round-trip
   obligation (`REDIS_STATE.md` §2 item 1, §7 item 9); this section defines a
   payload member and no storage contract, adds no Redis key and no Redis-only
   field, and changes no lifecycle, TTL, or compare-and-set rule.
10. **`bossId` is the Boss's canonical technical Identity — an identity, never
    presentation content.** The value is `BossState.BossId` (`GAME_STATE.md`
    §2.4), which `BOSS_RULES.md` §6.4 fixes as the stable machine-readable
    game-level Boss ID (`"boss-hoa-long"`, `"boss-thuy-ma"`, `"boss-moc-yeu"`,
    `"boss-son-thach-ve"`, `"boss-kim-loi-vuong"`). This is the `TASK-208`
    **D-208-03** Product Owner decision, and §4 item 18 records the amendment it
    belongs to.

    - **Source and form.** The value is read from `BossState.BossId` and mapped
      one-to-one. It is a **string**, always present and non-nullable (item 4),
      and it is neither re-derived nor normalized by the projection.
    - **Identity only — never a second copy of the Boss's content and never
      `BossDefinitionId`.** No display name, localization text, `Element`,
      portrait, asset key, boss type, `ATK`/`DEF`, `State`, Passive or Skill
      metadata, `SkillCharge`/`SkillCooldown`, or Boss `StatusEffects[]` travels
      with it (item 3), and the three identity concepts `BOSS_RULES.md` §6.4
      keeps distinct — the canonical technical Identity, the display name, and
      the persistence `BossDefinitionId` — are not collapsed here.
    - **The client resolves presentation from the catalog it already holds.**
      The identity is what lets the client look up the Boss's display name and
      Element label in its own existing single transcription of
      `BOSS_RULES.md` §6.4/§6.1; no display string is put on the wire for it.
      The client must not create a second Boss catalog and must not fetch one.
      A resolved Element label is **display metadata only**: it takes no part in
      Element matchup, damage, or any other gameplay result, which remain
      server-resolved (`GAME_RULES.md` §18, `ELEMENT_RULES.md`, `ADR-001`), and
      an unrecognized identity is rendered as the identity or a neutral
      placeholder — never as a guessed name.
    - **It is the same identity the events already report.** `BossSkillCast`'s
      `sourceId` and `PassiveCharged`/`PassiveTriggered`'s `sourceId` when
      `source = "boss"` already carry it (`§3.2.16`–`§3.2.18`). This member
      therefore discloses nothing new: it moves an already-delivered fact onto
      the path that survives a resync, exactly as §4.3 item 3 delivers
      `petState.passiveId` as the identity the events already report. The state
      push and the events are not interchangeable (§4 item 6): the events report
      an occurrence, this member reports the identity that occurrence belonged
      to, and neither replaces the other.
    - **No new method, event, subscription, or persistence behavior.** The
      member rides the existing `BattleStateUpdated` push on its existing
      trigger — group join (§4.1 item 1) and every committed Swap's
      resolved-state push (§2.1, §5) — and, because the reconnect snapshot is
      the same projection (§7), it is carried there too. It adds no method to
      §2's three, no event to §3, and no subscription, and it changes no storage
      contract: the identity is already part of `BossState` and already
      serializes with it (`REDIS_STATE.md` §2 item 1, §7 item 9).

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
   `BattleState` snapshot (`GAME_STATE.md` §2, projected to the wire:
   `BattleState.PlayerId` is server-only and excluded — `GAME_STATE.md`
   §2.8) plus its `Sequence`.
2. The client discards any local prediction and re-renders from this
   snapshot — it does not attempt to replay missed individual events.
3. If `GetBattleState` returns `BATTLE_NOT_FOUND` (state expired/cleared,
   see `REDIS_STATE.md` §3 TTL), the client treats the battle as ended and
   falls back to `GET /api/battle/{battleId}/result` (`API_CONTRACTS.md` §4).
4. On a successful reconnect, the client re-adds its new connection to the
   battle's group by invoking `JoinBattle(battleId)`. Group membership is
   connection-scoped and is never restored by the server. The existing
   `JoinBattle` push and existing `GetBattleState` snapshot remain the
   authoritative existing delivery paths; no new method, event, wire member,
   or state model is introduced. The re-join **completes before** item 1's
   snapshot request is issued, so the new connection is a member of the
   battle's group before normal post-reconnect battle event delivery is
   expected — a reconnect whose runtime holds no current battle knows no
   group to re-join and issues no `JoinBattle`.

The snapshot is **the same projection** the §4 push carries, member for member:
the record's own members (§4 item 4), `playerState` (§4.2 item 2), `petState`
(§4.3 item 2 — `passiveId`, `passiveProgress` and the conditional
`passiveResetOverride`, `equippedCards`, `statusEffects[]` (§4.3 item 14), and
the combat-stat triple `hp`/`maxHp`/`power` (§4.3 item 15)), and `bossState`
(§4.4 item 2 — `bossId` (§4.4 item 10), `hp` and `maxHp`). §7 therefore defines
no member set of its own.

For those members, the three delivery moments carry the **same** values from the
**same** authoritative `BattleState`:

```text
join snapshot        §4.1 — the unsolicited `BattleStateUpdated` push triggered
                     by `JoinBattle` (§2.2)

resolution snapshot   §4.1/§4.3 item 1 — the same `BattleStateUpdated` push
                     after every committed Swap's resolution (§2.1, §5)

resync snapshot       §7 — the client-requested `GetBattleState` response
```

That is the invariant: **join snapshot = resolution snapshot = resync
snapshot**, member for member. A recovery cannot report a different member set
from the push that preceded it, and a member cannot appear on one path and be
missing on another — which is what lets a client re-render from either path with
one model (`ADR-008`). In particular the members this revision added
(`petState.hp`/`maxHp`/`power` and `bossState.bossId`) are present consistently
on all three: they are reconstruction-safe by construction, not by client-side
rebuilding. This adds no method, event, or member: it states that the three
existing delivery moments agree.

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
9. **Any Status-Effect-, Boss-HP-, or combat-stat-specific message.** The Pet's
   active Status Effect instances (`§4.3` item 14), the Pet's
   `hp`/`maxHp`/`power` (`§4.3`
   item 15), and the Boss's live HP and canonical Identity (`§4.4`) are
   delivered by the existing `BattleStateUpdated` push, so none introduces a
   message of its own: there is no `StatusEffectsUpdated`, no
   `StatusEffectApplied`/`StatusEffectExpired`, no `BossStateUpdated`, no
   `BossHpChanged`, no `PetHpChanged`, no `PowerUpdated`, and no additional
   subscription, and none may be introduced.
   The gameplay methods remain exactly the three of §2 and the delivery paths
   remain exactly the three of §0/§3/§4/§6 — extending the state is how new data
   is delivered, and extending this protocol's method list is not
   (`§4` item 11). Nor is a Boss member the exception that reopens the narrow
   projections: `bossState` is the three-member projection §4.4 item 2 fixes,
   not `BossState`, and the Pet-side projection gains the enumerated members
   §4.3 items 14 and 15 author rather than becoming `PetState` (`§4.3` item 2).
