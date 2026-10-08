# Redis State

**Version:** 1.13 (§3's lifecycle entry is **synchronized** with the verified
battle-end persistence contract: `battle:{battleId}:state` is deleted only after
the battle-end **database unit of work has committed** — the `BattleResult` row
together with both progression tracks, `DATABASE.md` §1, "Battle-end atomicity
for `BattleResult` and the two progression tracks" — and the delete remains
outside that transaction. §3 states the ordering rule and the not-committed
consequence in its current form, and §7 item 11's TASK-040 note gains a concise
**superseding reference** (its historical record of the TASK-041 sequencing
boundary is preserved rather than rewritten). **This changes no key, no
structure, no lifecycle, no TTL, and no concurrency rule**: the delete happens
at the same point in the same order, and only its stated precondition is now
exact. §1–§6 and §7 items 1–15 are unchanged. Decision source: TASK-217C.
Prior 1.12 (§7 item 17 added — `PetState.BurnDamageModifiers[]`
(`GAME_STATE.md` §2.3.9/§2.3.10/§5.1.5) adds no Redis key, no Redis-only field,
and no persistence work: it is a `PetState` member of the `BattleState` shape §2
item 1 already covers, it round-trips under the existing obligation with an empty
array as the no-modifier form and its written order preserved rather than sorted,
it is written in the same single post-resolution write-back under the unchanged
`Sequence` compare-and-set and the unchanged §3 sliding TTL, and it has no wire
consequence because adding state is not adding a wire member
(`SIGNALR_PROTOCOL.md` §4 item 4). A modified Burn tick's damage itself is
derived, not stored. **This changes no key, no structure, no lifecycle, no TTL,
and no concurrency rule**; §1–§6 and §7 items 1–16 are unchanged. Prior 1.11 (§7 item 16 reconciled per **TASK-178** with the
Product-Owner-approved runtime decision **Q-1 = A**: `PetState.ATKModifiers[]`
elements now carry their declared `lifetime` (`Battle` | `NextAttack`,
`GAME_STATE.md` §2.3.7 item 11), and the round-trip obligation therefore covers
that member too — a record that drops or alters an element's `lifetime` does not
round-trip. **This changes no key, no structure, no lifecycle, no TTL, and no
concurrency rule**: the collection remains a `PetState` member of the §2 shape
item 1 already covers, written in the same single post-resolution write-back
under the unchanged `Sequence` compare-and-set, with no new Redis key and no
Redis-only field. §1–§6 and §7 items 1–15 are unchanged. Prior 1.10: (§7 item 16 added — `PetState.ATKModifiers[]`
(`GAME_STATE.md` §2.3.7, TASK-136 D1/D2) adds no Redis key, no Redis-only field,
and no persistence work: it is a `PetState` member of the `BattleState` shape §2
item 1 already covers, it round-trips under the existing obligation with an
empty array as the no-modifier form and a deterministic `SourceIdentity`
ordering, it is written in the same single post-resolution write-back under the
unchanged `Sequence` compare-and-set and the unchanged §3 sliding TTL, and it
has no wire consequence because adding state is not adding a wire member
(`SIGNALR_PROTOCOL.md` §4 item 4). The effective Pet ATK itself is derived, not
stored. §1–§6 and §7 items 1–15 are unchanged; no key, lifecycle, TTL, or
concurrency rule changed. Prior 1.9: (§7 item 15 added — `PetState.CardCostModifiers[]`
(`GAME_STATE.md` §2.3.5, ADR-018, TASK-134 D1/D2) adds no Redis key, no
Redis-only field, and no persistence work: it is a `PetState` member of the
`BattleState` shape §2 item 1 already covers, it round-trips under the existing
obligation with an empty array as the no-modifier form, it is written in the
same single post-resolution write-back under the unchanged `Sequence`
compare-and-set and the unchanged §3 sliding TTL, and it has no wire
consequence because adding state is not adding a wire member
(`SIGNALR_PROTOCOL.md` §4 item 4). Card cost itself is derived, not stored. §1–§6
and §7 items 1–14 are unchanged; no key, lifecycle, TTL, or concurrency rule
changed. Prior 1.8: §7 items 13–14 added — `PetState.NextAttackCritModifiers[]`
(`GAME_STATE.md` §2.3.4, ADR-017) adds no Redis key, no Redis-only field, and no
persistence work: it is a `PetState` member of the `BattleState` shape §2 item 1
already covers, it round-trips under the existing obligation with an empty array
as the no-modifier form, it is written in the same single post-resolution
write-back under the unchanged `Sequence` compare-and-set, and it has no wire
consequence because adding state is not adding a wire member
(`SIGNALR_PROTOCOL.md` §4 item 4). §1–§6 and §7 items 1–12 are unchanged; no key,
lifecycle, TTL, or concurrency rule changed. Prior 1.7: §2 states the active-state record's `Element` encoding
contract per TASK-073 — the record's `PetState.Element` /
`BossState.Element` spelling is **unbound** and deliberately not the REST wire
value set: `GAME_STATE.md` §2 declares `Element` as a member name only and its
stated purpose disclaims serialization, §2 item 1's shape obligation therefore
binds members and structure rather than a spelling, `GAME_STATE.md` §2.1.7
item 4 already classes exact JSON names/casing as the serializer's
implementation detail, and §7 item 9's round-trip obligation is internal to
the round trip. A future binding of this encoding is a change to this document.
Prior 1.6: §3's battle-end delete is implemented by TASK-041 — the §7
status note's sequencing boundary is closed: the `BattleResult` write happens
first and the record is deleted only after it; prior 1.5: §3 battle-end delete-failure behaviour: no retry, no
worker/queue — the sliding TTL remains the cleanup path and the documented
maximum lifetime of the active-state record; at-most-one-result-row
guarantee via `BattleResultId` = `BattleId` (`DATABASE.md` §1); prior 1.4:
Player/Pet role model per ADR-011 — staged PlayerState
narrative corrected to BattleState-root Combo/MatchCount + PetState combat
members; prior 1.3: `LastCommittedSwapPair` persistence boundary stated in
§7 item 11)
**Status:** Draft

> This document answers: **"How is active battle state represented in
> Redis?"** It does not redefine what any field means — see `GAME_STATE.md`
> for the `BattleState` shape and `GAME_RULES.md`/domain rules for meaning.

---

# 1. Key Structure

```text
battle:{battleId}:state       →  serialized BattleState (GAME_STATE.md §2)
battle:{battleId}:lock         →  short-lived lock key, used during
                                   resolution (see §4)
```

No other Redis keys are needed for MVP (no leaderboard, no pub/sub channel
beyond what SignalR's own backplane may require if the server is scaled to
multiple instances — out of scope for MVP, single-instance assumed per
`TDD.md` §7).

There is no separate key, and no partial key, for Battle State Foundation
(`GAME_STATE.md` §2.0) — see §7.

---

# 2. Serialization

1. `BattleState` is serialized as JSON (matches the shape in
   `GAME_STATE.md` §2 exactly — no additional Redis-only fields beyond
   `Sequence`, which is already part of `BattleState`).
2. The serialized value is the single source of truth for a battle's live
   state; the server process holds no long-lived in-memory copy across
   requests (`ARCHITECTURE.md` §4 — `BattleResolutionService` loads, uses,
   and saves it within one resolution).

"A battle's live state" means a **real, playable battle** whose full
`GAME_STATE.md` §2 shape exists. The staged subset in `GAME_STATE.md` §2.0 is
not that record and is never written here (§7).

**The record's `Element` encoding is free, and is deliberately not the REST
wire value set.** This is the storage-side half of the Element contract
(TASK-073); the client-facing half is `API_CONTRACTS.md` §5.1. The five points
below concern that encoding only — items 1–2 above are this section's general
serialization rules and are unchanged by them.

```text
BattleState.PetState.Element   (GAME_STATE.md §2.3)
BattleState.BossState.Element  (GAME_STATE.md §2.4)
        ↓  written into battle:{battleId}:state
encoding = the serializer's; this document binds no Element value set
```

1. **The shape obligation does not decide an encoding.** Item 1 above requires
   the JSON to match `GAME_STATE.md` §2's shape exactly. `GAME_STATE.md` §2.3
   and §2.4 declare `Element` as a **member name only** — no value set, no
   representation, no serialization rule appears for it anywhere in that
   document, whose stated purpose explicitly disclaims serialization and
   points here. "Matches the shape exactly" therefore constrains the record's
   members and structure, which is what item 1's "no additional Redis-only
   fields" states; it binds no Element spelling, because §2 states none to
   match.
2. **`GAME_STATE.md` §2.1.7 item 4 already draws this line, and this record
   follows it.** There, a value's *existence and meaning* are owned by the
   state document while its *exact JSON member names and casing* are "an
   implementation detail of the serializer". `Element`'s existence and meaning
   — one Element per Pet, one per Boss (`ELEMENT_RULES.md` §1.1, §1.2) — are
   owned by `GAME_STATE.md` §2.3/§2.4 and the Element rules; its spelling in
   this record is the serializer's, exactly as §6 item 1 states when it
   declines to re-specify a field-by-field JSON schema.
3. **Round-trip losslessness does not force a shared spelling.** §7 item 9
   (`GAME_STATE.md` §2.1.7 item 5) obliges the serializer to return an equal
   `BattleState` — the same value at every member, and those members are the
   ones `GAME_STATE.md` §2 declares. The obligation is *internal to the round
   trip*: it requires this record's own reader to recover the Element the
   writer wrote. It does not require this record's spelling to equal the REST
   wire spelling or any other surface's, because no document makes the two
   representations the same value.
4. **The value set is not restated here, and none is bound.** Which encoding
   the serializer uses — the `API_CONTRACTS.md` §5.1 English set, the `Element`
   enum member names, or a numeric ordinal — is left to the serializer, and
   this document leaves it there. Two things are nonetheless not permitted. An
   accented design name (`Hỏa`, `Thủy`, `Thổ`, `Mộc`, `Kim` — `ELEMENT_RULES.md`
   §1) is display text and is never a technical value on any surface
   (`BOSS_RULES.md` §6.4's ASCII technical-identity rule). And no encoding may
   be *presented* as the Element contract for a client-facing payload: a reader
   that needs the client-visible value reads `API_CONTRACTS.md` §5.1, while a
   reader of this record needs only that it round trips.
5. **Changing this later is a contract change, not a refactor.** If a future
   task binds this record's Element encoding — for the `DATABASE.md`-style
   reason that a stored representation has been standardized, or to unify it
   with the wire set — that decision belongs here and is recorded here, not
   inferred from a serializer's current output.

Item 1's "matches the shape in `GAME_STATE.md` §2 exactly" therefore remains
verbatim and complete: this section adds no `BattleState` member, removes
none, and renames none. It states only that the record's Element *spelling*
is unbound — a statement about encoding, not about shape.

---

# 3. TTL / Lifecycle

```text
Created:   on POST /api/battle/start (API_CONTRACTS.md §2)
Refreshed: TTL reset on every successful resolution (sliding expiry),
           default 30 minutes of inactivity — Configurable
Deleted:   explicitly, when BattleWon/BattleLost is resolved and the battle-end
           database unit of work has been **committed** to PostgreSQL — the
           `BattleResult` row and both progression tracks together
           (DATABASE.md §1)
```

The delete happens **only after that commit completed**, and it stays outside the
database transaction (`DATABASE.md` §1, "Battle-end atomicity for `BattleResult`
and the two progression tracks" item 3; `ARCHITECTURE.md` §4 item 4). A cleared
key for a battle end that never became durable would destroy the battle's only
authoritative copy (§2 item 2 — the serialized value is the single source of
truth for a battle's live state and the server holds no long-lived in-memory
copy), so the ordering is part of the contract rather than an implementation
detail.

If a battle's key expires from inactivity before completion, the battle is
considered abandoned; the client's `GetBattleState` call
(`SIGNALR_PROTOCOL.md` §7) will receive `BATTLE_NOT_FOUND`, and no partial
result is written to PostgreSQL.

If the explicit **battle-end delete fails after the committed result write**, no
automatic retry is performed and no worker or queue exists for it: the
sliding TTL above remains the cleanup path, so the TTL is the documented
maximum lifetime of the active-state record in every case. The battle-end
order itself is unchanged — the unit of work commits first, then the key is
deleted (`ARCHITECTURE.md` §4 item 4, `DATABASE.md` §1 item 3) — and a failed
delete cannot produce a second result: `BattleResultId` is the battle's own
`BattleId`, so **at most one `BattleResult` row can ever exist per battle**
(`DATABASE.md` §1).

The converse failure is the one the ordering exists to prevent: if the unit of
work does **not** commit, the delete does not happen either, the record above
remains the battle's recoverable authoritative state, and the battle can be
retried in full (`DATABASE.md` §1 items 4–5; §7 item 5 below).

---

# 4. Concurrency

1. Before resolving an action, `BattleResolutionService` reads
   `battle:{battleId}:state` along with its `Sequence`.
2. On write, it uses an optimistic check: write succeeds only if
   `Sequence` in Redis still matches what was read (compare-and-set,
   e.g. via a Lua script or `WATCH`/`MULTI`/`EXEC`). If it does not match,
   the resolution is aborted and retried against the fresh state.
3. This guarantees the single-writer-at-a-time property required by
   `SIGNALR_PROTOCOL.md` §6 item 1 even if two requests for the same battle
   somehow arrive concurrently.
4. `battle:{battleId}:lock` is an optional short-TTL (a few seconds) mutex
   used only to avoid wasted retries under contention; it is not itself the
   source of correctness — the `Sequence` compare-and-set is.
5. **The write is one write-back per resolution.** A board resolution
   (`MATCH3_RULES.md` §2.1.6) mutates the board many times internally and
   writes the record once, after the board is stable and `Sequence` has been
   incremented (`GAME_STATE.md` §5.1). No intermediate board state — a
   half-removed board, a board before Spawn refills it, a partially resolved
   cascade — is ever written here, and no per-step write exists.
6. `Sequence` is the only concurrency token (`GAME_STATE.md` §5 item 3,
   §5.1 item 4). `Turn` is a game value written inside the record and is never
   compared for the compare-and-set. The `Sequence` a retry reads is the
   pre-resolution value, so a retried resolution re-runs the same deterministic
   computation and produces the same result.
7. A rejected action writes nothing at all (`MATCH3_RULES.md` §2.1.5): it does
   not touch this key, does not reset the TTL, and does not change `Sequence`.

---

# 5. Recovery

1. Redis is the only store for active state — there is no write-behind to
   PostgreSQL during an active battle (`TDD.md` §4.3).
2. If the Redis instance itself is lost before a battle completes, that
   battle's progress is lost; this is accepted for MVP (single Redis
   instance, no replication requirement specified). Flag as a
   **Potential Future Idea** only if reliability requirements change —
   not implemented now.

---

# 6. What Is Not Here

1. Field-by-field JSON schema — derived directly from `GAME_STATE.md` §2;
   not re-specified to avoid duplication. This applies to the board's cell
   entries and their Special Gem metadata for the same reason: the shape is
   owned by `GAME_STATE.md` §2.1.7, and it is not restated here as a Redis
   schema.
2. Redis Cluster / sharding — out of scope, no such requirement exists in
   `MVP_SCOPE.md`.

---

# 7. Foundation Runtime State Is Not Stored Here

`GAME_STATE.md` §0/§2.0 defines **Battle State Foundation** — the minimal
technical state (`BattleId`, `Turn`, `Sequence`) used while the runtime
foundation is built before gameplay systems exist — and §2.0.5 defines the
**Board Foundation State**, the next stage, which adds `BoardState` and the
RNG fields (`RngSeed`/`RngState`).

```text
Foundation runtime state        Battle State Foundation (GAME_STATE.md §2.0)
                                  → NOT stored in Redis
Board Foundation state          Board Foundation State (GAME_STATE.md §2.0.5)
                                  → NOT stored in Redis
Match-3 Resolution stage          Board Foundation State + board resolution
                                  (GAME_STATE.md §5.1, MATCH3_RULES.md §2–§8)
                                  → NOT stored in Redis (still a §2 subset —
                                    §7 item 4 applies unchanged: Combo/
                                    MatchCount at the BattleState root and
                                    PetState combat members exist as fields,
                                    but BossState does not)
Full active battle state          Full BattleState (GAME_STATE.md §2)
                                  → Redis, battle:{battleId}:state (§1)
```

1. **Foundation State is not persisted to Redis.** It is not written to
   `battle:{battleId}:state`, and §3's lifecycle (`Created` on
   `POST /api/battle/start`, cleared on battle end) does not apply to it.
2. **No partial schema is created.** A record containing only the §2.0
   subset must not be written to `battle:{battleId}:state`, because §2
   requires that key to hold the full §2 shape exactly. Writing a subset
   there would make a foundation artifact indistinguishable from a real
   battle record.
3. **Why the foundation stages wrote no record.** §3 ties creation to
   `POST /api/battle/start` (`API_CONTRACTS.md` §3), and while the
   foundation stages were being built that endpoint required Pet, Boss,
   Card, and Relic loadout data — systems that did not exist yet
   (`ROADMAP.md` §1 Phase 2). Until a battle could actually be
   started, there was nothing for this key to hold, so Redis persistence
   was deliberately deferred. This is a **sequencing** boundary, not a
   weakening of the requirement.

   **TASK-030 implemented `POST /api/battle/start`, and TASK-040 has since
   discharged the deferral.** §7 item 7's condition — a real battle can be
   created and resolved — was met by TASK-030, and TASK-040 implemented the
   storage this section defers: a created battle now **does** have its
   `battle:{battleId}:state` record. See the status note at the end of this
   section. The history above is retained because it is what §7 items 1–4 still
   say about *staged subsets* — no foundation or board-foundation state is ever
   written here, and adding a stage never authorized persistence on its own.
4. **The Board Foundation stage does not change this boundary.** §7 items 1–3
   apply to it unchanged:
   - `BoardState` and `RngSeed`/`RngState` are §2 fields
     (`GAME_STATE.md` §2.0.5), so the Board Foundation stage is still a
     staged **subset** of §2 and still must not be written to
     `battle:{battleId}:state` (§7 items 1–2).
   - It is still not a "real, playable battle" in §2's sense: `BossState`
     (and later `PetState` loadout/status members) still do not
     complete §2's full shape, which still cannot be produced
     (`GAME_STATE.md` §2.0.5.3).
   - The deferral reason in §7 item 3 is unchanged: `POST /api/battle/start`
     still requires loadout data that does not exist, so no battle can be
     created for this key to hold.
   - Adding the board therefore **neither requires nor authorizes** Redis
     persistence, and introduces no board- or RNG-specific key.
5. **The requirement is unchanged.** For any real, playable battle, active
   state remains authoritative server state stored **only** in Redis, keyed
   per battle (`battle:{battleId}:state`), with sliding TTL and
   `Sequence`-gated optimistic writes (`§1`–`§5`, `ADR-005`, `TDD.md` §4).
   Nothing here permits active battle state to live in process memory:
   `ADR-005` rejected that option for exactly the recoverability reason
   given in `§5`.
6. **Staged state is still server-authoritative.** It is not persisted,
   but it is server-owned and server-produced (`GAME_RULES.md` §18,
   `ADR-001`); it is delivered to the client per `SIGNALR_PROTOCOL.md` §4
   and is never authored by the client.
7. **When Redis becomes required.** Redis persistence becomes required when
   a real battle can be created and resolved — i.e. when §2's full shape
   exists and `POST /api/battle/start` is implemented. That is the point at
   which §3's lifecycle takes effect. The Board Foundation stage (§7 item 4) is
   not that point.
8. **The Match-3 resolution stage does not change this boundary either.**
   The board-resolution contract (`MATCH3_RULES.md` §2–§8) mutates
   `BoardState.Cells[64]`, advances `RngState`, and increments `Turn` and
   `Sequence` (`GAME_STATE.md` §5.1) — all §2 fields that already exist — and
   adds exactly one further §2 field, `LastCommittedSwapPair`
   (`GAME_STATE.md` §2.1.10, §7 item 11 below). So §7 items 1–4 apply to it
    unchanged: it is still a staged subset, it is still not a "real, playable
    battle" without `BattleState.Combo`/`MatchCount` (§2.2),
    `PetState`, and `BossState`, and
    `POST /api/battle/start` still cannot create one. Resolving a board
   therefore **neither requires nor authorizes** Redis persistence, and adds
   no new key, no per-resolution key, and no board-specific key.
9. **One serialization consequence, and it is not a gap.** Because the board
   resolution writes `Sequence` once per resolution
   (`GAME_STATE.md` §5.1), a serializer that round-trips the record must
   preserve `Turn` and `Sequence` exactly (see also §2 item 1: the JSON
   matches `GAME_STATE.md` §2 with no additional Redis-only fields) — the
   counter values are state, not derived from the board or the RNG.
10. **Special Gem state is inside `BoardState.Cells[64]` — it adds no key, no
    field, and no record.** `GAME_STATE.md` §2.1 defines `BoardState` as
    `Cells[64]` alone: each cell entry holds its Gem type and, optionally, the
    Special Gem that occupies that cell (type, and orientation for a Line
    Clear Gem). There is no `PendingSpecialGems[]`, no `SpecialGems[]`, and no
    parallel collection of any kind, so this document defines **no**
    Special-Gem-specific Redis structure — no key, no hash field, no set, no
    index, and no second record — exactly as §1 states.

    The consequences for this document are these, and nothing more:

    - **The serialized shape is unchanged in structure.** The board is written
      as a 64-element array in ascending cell-index order, and a cell's
      Special Gem state is part of that cell's entry (`GAME_STATE.md` §2.1.7
      items 1–4). No additional Redis-only field is introduced by Special
      Gems, so §2 item 1's "matches the shape in `GAME_STATE.md` §2 exactly"
      still holds verbatim.
    - **Round-trip losslessness now covers Special Gems.** §7 item 9's
      round-trip obligation extends to them: a record that loses a cell's
      Special Gem, or its orientation, or that reorders cells, does not
      round-trip (`GAME_STATE.md` §2.1.7 item 5). This is the same
      serialization obligation the counters already have, applied to the
      board's own contents.
    - **The lifecycle and concurrency rules are untouched.** Special Gem state
      is written in the same single post-resolution write-back as the rest of
      the board (§4 item 5), under the same `Sequence` compare-and-set (§4
      item 2), and with no per-activation, per-cascade, or per-creation write.
      An intermediate board — one holding a pass's reservation, a creation not
      yet committed, or a cell momentarily emptied — is Transient Resolution
      State and is never written here (`MATCH3_RULES.md` §4.1,
      `GAME_STATE.md` §2.1.5 item 4).
    - **No Second Board Representation.** A Redis value that stored the board
      twice — once as `Cells[64]` and once as a separate Special Gem
      collection — would be a representation this document does not define,
      and it is the case `GAME_STATE.md` §2.1.2 item 2 declined.

    This is a **content** change to the record, not a **structure** change to
    the store, which is why §1–§4 and §7 items 1–8 required no edit for it.
11. **`LastCommittedSwapPair` adds no key, no Redis-only field, and no
    persistence work at this stage.** `GAME_STATE.md` §2.1.10 defines one
    authoritative `BattleState` field, `LastCommittedSwapPair` — the canonical
    `(min, max)` record of the most recently committed Swap, absent before the
    first commit — which `MATCH3_RULES.md` §2.1.4's already-applied check
    reads. Its consequences here are these, and nothing more:

    - **It is part of the §2 shape, so §2 item 1 covers it unchanged.**
      `BattleState` is serialized as JSON matching `GAME_STATE.md` §2 exactly,
      and this member is part of §2. It introduces no Redis-only field, and
      §1's key structure is untouched: no `battle:{battleId}:swap` key, no
      hash field, no index, and no second record exists for it.
    - **Round-trip losslessness covers it.** A record that drops the member
      when it is present, or that round-trips it with its two indices
      reordered, does not round-trip (`GAME_STATE.md` §2.1.10 item 10,
      §2.1.7 item 5). Its canonical `MinCellIndex < MaxCellIndex` ordering is
      part of the value, so it must survive serialization as written —
      §7 item 9's counter obligation, applied to this field.
    - **Absence must round-trip as absence.** Before the first committed Swap
      the member is omitted, not written as a `null` pair or a zero pair
      (`GAME_STATE.md` §2.1.10 items 3 and 10). A store that materializes a
      sentinel pair for it would invent a commit the battle never made.
    - **The lifecycle and concurrency rules are untouched.** It is written in
      the same single post-resolution write-back as the rest of the state
      (§4 item 5), under the same `Sequence` compare-and-set (§4 item 2), and
      only when a Swap is committed. A rejected action writes nothing and
      therefore does not touch this key, reset the TTL, or change this member
      (§4 item 7, `MATCH3_RULES.md` §2.1.5, `GAME_STATE.md` §2.1.10 item 6).
      It is **not** a concurrency token: `Sequence` remains the only one
      (§4 item 6).
    - **Nothing is stored here yet.** §7 items 1–4 and item 8 above apply to
      this member exactly as they do to the board and the RNG: it is a staged
      §2 field, not a "real, playable battle", so §3's lifecycle does not
      apply and no record is written. When Redis persistence becomes required
      (§7 item 7), this field is carried by the existing
      `battle:{battleId}:state` record with no schema change and no new key.

    Like the Special Gem content change, this is a **content** change to the
    record rather than a **structure** change to the store.
12. **Match/Combo accounting and `PetState` combat members add no key, no
    Redis-only field, and no persistence work at
    this stage — and they do not end the deferral.** `GAME_STATE.md` §2.2
    defines `Combo` and `MatchCount` at the **`BattleState` root** (there is
    no nested `PlayerState` node — ADR-011), and `GAME_STATE.md` §2.3's
    `PetState` carries the active Pet's combat stats. The Match /
    Combo accounting stage implements the two root members: `MatchCount` and
    `Combo`. Their consequences here are these, and nothing more:

    - **They are part of the §2 shape, so §2 item 1 covers them unchanged.**
      `BattleState` is serialized as JSON matching `GAME_STATE.md` §2 exactly,
      and `Combo`/`MatchCount` are root members of that tree. They introduce no
      Redis-only
      field, and §1's key structure is untouched: no
      `battle:{battleId}:progression` key, no per-player key, no hash field, and
      no second record exists for them. A per-player key would in any case be a
      second representation of state §2 already carries inside the battle
      record. The wire label `playerState` (`SIGNALR_PROTOCOL.md` §4.2) is not
      a stored path.
    - **Round-trip losslessness covers both members.** A record that drops,
      defaults, or recomputes either value does not round-trip
      (`GAME_STATE.md` §2.2, §2.1.7 item 5). Both are **state, not derived**:
      neither can be re-derived from the board after the fact, and neither can
      be re-derived from `Turn` or `Sequence` (§5.2 item 2 — "`Sequence` is not a
      Match, Cascade, Combo, or Match-count value, and none of those is derived
      from it"). `Combo` in particular is unrecoverable once lost, because the
      cascade history it counted is not stored anywhere
      (`GAME_STATE.md` §5.3 item 4).
    - **Zero is a value and must round-trip as one.** Both members are
      non-nullable and are written from battle creation
      (`GAME_STATE.md` §2.2). `Combo = 0` is the published value before the
      battle's first committed Swap (`MATCH3_RULES.md` §6.5 item 4), so a store
      that omitted a zero — or read an omitted member as "no value" — would lose
      a real state, unlike `LastCommittedSwapPair` (item 11), whose absence is
      itself the documented statement "no Swap has been committed".
    - **The lifecycle and concurrency rules are untouched.** Both members are
      written in the same single post-resolution write-back as the rest of the
      state (§4 item 5) and under the same `Sequence` compare-and-set (§4 item
      2), and only for a **committed** Swap. Their intermediate values during a
      resolution — the transient `Combo = 0` between the reset and the first
      Match, and the running totals — are Transient Resolution State
      (`GAME_STATE.md` §3) and are never written here. A rejected action writes
      nothing and therefore does not touch this key, reset the TTL, or change
      either member (§4 item 7, `MATCH3_RULES.md` §2.1.5 item 5).
    - **Neither is a concurrency token.** `Sequence` remains the only one
      (§4 item 6, `GAME_STATE.md` §5).
    - **Nothing is stored here yet, and this stage does not change that.** §7
      items 1–4 and item 8 apply unchanged: the accounting members and
      `PetState` combat stats now exist as contract fields, but
      `BossState` (`GAME_STATE.md` §2.4) still does not, so
      §2's full shape still cannot be produced and `POST /api/battle/start`
      still cannot create a real battle (§7 items 3, 7). Redis persistence
      therefore remains deferred, and this stage introduces no key and no record
      — the same position item 8 recorded for the board-resolution stage.
      When Redis persistence becomes required (§7 item 7), both members are
      carried by the existing `battle:{battleId}:state` record with no schema
      change and no new key.

    Like the Special Gem and commit-record changes, this is a **content** change
    to the record rather than a **structure** change to the store.

    > **Status note (TASK-040).** The deferral recorded above is **discharged**.
    > TASK-030 implemented `POST /api/battle/start` and the Boss stage, so a real,
    > playable battle carrying §2's shape can be created and resolved — §7 item 7's
    > precondition — and TASK-040 has implemented the storage §1–§4 specify:
    >
    > - the record is written to `battle:{battleId}:state` on successful battle
    >   creation (`GameServer.Application.Battle.BattleStateService.CreateBattleAsync`
    >   via `IBattleStateRepository`;
    >   implementation `GameServer.Infrastructure.Redis.BattleStateRepository`),
    > - each accepted action loads the record and performs exactly one write-back
    >   guarded by the §4 `Sequence` compare-and-set, and
    > - the §3 sliding 30-minute expiry is refreshed only on a successful
    >   resolution; a rejected action writes nothing (§4 item 7).
    >
    > The value written is the authoritative `BattleState` JSON of §2 item 1 — the
    > TASK-029 runtime mapping — and the server process holds no long-lived
    > in-memory copy of it (§2 item 2). §7 items 1–2 and 4 remain in force for what
    > they say about *staged subsets*: no foundation or board-foundation record is
    > ever written here, and §1's key set is unchanged.
    >
    > `§3`'s explicit delete on battle end **is now implemented** (TASK-041). The
    > sequencing boundary this note recorded is closed:
    >
    > - a resolution that emits `BattleWon`/`BattleLost` writes the durable
    >   `BattleResult` row to PostgreSQL **first** (`DATABASE.md` §1,
    >   `ARCHITECTURE.md` §4 item 4), and
    > - `battle:{battleId}:state` is deleted only after that write succeeded
    >   (`IBattleStateRepository.DeleteAsync`, implementation
    >   `GameServer.Infrastructure.Redis.BattleStateRepository`).
    >
    > When the result write does **not** happen — no `BossDefinition` row resolves
    > for the battle's Identity, or PostgreSQL fails — the delete does not happen
    > either, and the record above remains the battle's recoverable authoritative
    > state (`DATABASE.md` §1 "Identity and reward sourcing" item 3). When the
    > write succeeds and the delete fails, §3's paragraph above applies unchanged:
    > no retry, no worker, the TTL is the cleanup path, and `BattleResultId` =
    > `BattleId` makes a second result impossible. The TTL therefore remains the
    > documented maximum lifetime of the active-state record in every case, and
    > the deferral recorded at the top of this section is fully discharged.
    >
    > **Superseding note (TASK-217A, current contract).** The two bullets above
    > record the TASK-041 sequencing boundary in the terms that were then in
    > force. The durable write is now **one database unit of work** — the
    > `BattleResult` row together with both progression tracks
    > (`DATABASE.md` §1, "Battle-end atomicity for `BattleResult` and the two
    > progression tracks") — so the delete follows the **commit** of that unit of
    > work, not merely the result insert. The ordering rule itself is unchanged
    > and §3 above states it in its current form; nothing else in this note,
    > including the TTL cleanup path and the at-most-one-row guarantee, changes.
13. **`PetState.NextAttackCritModifiers[]` adds no key, no Redis-only field,
    and no persistence work.** `GAME_STATE.md` §2.3.4 defines one new
    `PetState` collection (`ADR-017`) — temporary source-specific Crit
    modifiers awaiting consumption by a qualifying owner attack, each element
    carrying a source identity and a Crit contribution in percentage points.
    Its consequences here are these, and nothing more:

    - **It is part of the §2 shape, so §2 item 1 covers it unchanged.**
      `BattleState` is serialized as JSON matching `GAME_STATE.md` §2 exactly,
      and this collection is a `PetState` member. It introduces no Redis-only
      field, and §1's key structure is untouched: no
      `battle:{battleId}:crit` key, no hash field, no set, no index, and no
      second record exists for it. It is **not** a concurrency token;
      `Sequence` remains the only one (§4 item 6).
    - **Round-trip losslessness covers it, and an empty collection round-trips
      as empty.** A record that drops an element, reorders elements, or
      collapses two distinct source identities into one does not round-trip
      (`GAME_STATE.md` §2.3.4 items 2 and 6, §2.1.7 item 5). The collection is
      always present (`GAME_STATE.md` §2.3.4 item 5), so an entity with no
      active modifier serializes an **empty array** — it is never omitted and
      never `null`, unlike `LastCommittedSwapPair` (item 11), whose absence is
      itself a documented statement.
    - **The lifecycle and concurrency rules are untouched.** Creation and
      consumption both occur inside one resolution and are written in the same
      single post-resolution write-back as the rest of the state (§4 item 5),
      under the same `Sequence` compare-and-set (§4 item 2). A reader never
      observes a modifier mid-consumption (`GAME_STATE.md` §5.1.2 item 6). A
      rejected action writes nothing and therefore does not touch this key,
      reset the TTL, or change this collection (§4 item 7).
    - **It is not a staged subset.** §7 item 7's precondition is met — a real,
      playable battle carrying §2's shape can be created and resolved — so this
      member is carried by the existing `battle:{battleId}:state` record
      exactly as every other `PetState` member is (see the status note above).
      It changes no storage decision.
    - **No expiry of its own is introduced by storage.** A modifier's lifetime
      is owned by `COMBAT_RULES.md` §3.3 item 8 and mutated by
      `GAME_STATE.md` §5.1.2; Redis adds no TTL, sweep, or expiry for this
      collection beyond the record's own §3 sliding TTL.

    Like the Special Gem, commit-record, and accounting changes above, this is
    a **content** change to the record rather than a **structure** change to
    the store.

14. **No SignalR or wire consequence reaches this document.**
    `SIGNALR_PROTOCOL.md` §4 item 4 governs: the state push carries exactly the
    implemented stage's own fields, and adding state is not adding a wire
    member. `NextAttackCritModifiers[]` is therefore **not** part of
    `BattleStateUpdated`'s payload (`SIGNALR_PROTOCOL.md` §4.2/§4.3 fix
    `playerState` and `petState` to their existing member sets) and adds no
    message, method, or subscription. Delivering it would be a protocol change
    owned by its own task, and this document defines no storage contract for
    such a delivery.
15. **`PetState.CardCostModifiers[]` adds no key, no Redis-only field, and no
    persistence work.** `GAME_STATE.md` §2.3.5 defines one new `PetState`
    collection (`ADR-018`, TASK-134 D1/D2) — applied, Battle-scoped Card-cost
    modifiers, each element carrying a source identity and a cost reduction in
    percentage points. Its consequences here are these, and nothing more:

    - **It is part of the §2 shape, so §2 item 1 covers it unchanged.**
      `BattleState` is serialized as JSON matching `GAME_STATE.md` §2 exactly,
      and this collection is a `PetState` member. It introduces no Redis-only
      field, and §1's key structure is untouched: no
      `battle:{battleId}:cardcost` key, no hash field, no set, no index, and no
      second record exists for it. It is **not** a concurrency token;
      `Sequence` remains the only one (§4 item 6).
    - **Round-trip losslessness covers it, and an empty collection round-trips
      as empty.** A record that drops an element, alters a
      `costReductionPercentage`, reorders elements, or collapses two distinct
      source identities into one does not round-trip (`GAME_STATE.md` §2.3.6
      items 3 and 5, §2.1.7 item 5). The collection is always present
      (`GAME_STATE.md` §2.3.5 item 6), so a Pet with no active modifier
      serializes an **empty array** — it is never omitted and never `null`,
      unlike `LastCommittedSwapPair` (item 11), whose absence is itself a
      documented statement.
    - **The lifecycle and concurrency rules are untouched.** Creation,
      replace/refresh, and removal all occur inside one resolution and are
      written in the same single post-resolution write-back as the rest of the
      state (§4 item 5), under the same `Sequence` compare-and-set (§4 item 2),
      and under the unchanged §3 sliding TTL. A reader never observes a
      modifier mid-refresh (`GAME_STATE.md` §5.1.3 item 6). A rejected action
      writes nothing and therefore does not touch this key, reset the TTL, or
      change this collection (§4 item 7).
    - **It is not a staged subset.** §7 item 7's precondition is met — a real,
      playable battle carrying §2's shape can be created and resolved — so this
      member is carried by the existing `battle:{battleId}:state` record
      exactly as every other `PetState` member is (see the status note above).
      It changes no storage decision.
    - **No Card cost is stored here.** The collection stores the *modifiers*;
      `EffectiveCardCost` is a value derived at cast resolution and is not a
      stored field, a Redis-only field, or a second representation of a Card's
      cost (`CARD_RULES.md` §3.6 item 2, `GAME_STATE.md` §2.3.5 item 10).
    - **No expiry of its own is introduced by storage.** The modifier's
      `Battle` lifetime is owned by `RELIC_RULES.md` §8.3/§8.4 and mutated by
      `GAME_STATE.md` §5.1.3; Redis adds no TTL, sweep, or expiry for this
      collection beyond the record's own §3 sliding TTL.

    Like the Special Gem, commit-record, accounting, and Crit-modifier changes
    above, this is a **content** change to the record rather than a
    **structure** change to the store.
16. **`PetState.ATKModifiers[]` adds no key, no Redis-only field, and no
    persistence work.** `GAME_STATE.md` §2.3.7 defines one new `PetState`
    collection (TASK-136 D1/D2) — applied ATK modifiers, each element carrying a
    source identity, a signed percentage-point contribution, and its declared
    `lifetime` (`Battle` | `NextAttack`, `GAME_STATE.md` §2.3.7 item 11,
    TASK-178). Its consequences here are these, and nothing more:

    - **It is part of the §2 shape, so §2 item 1 covers it unchanged.**
      `BattleState` is serialized as JSON matching `GAME_STATE.md` §2 exactly,
      and this collection is a `PetState` member. It introduces no Redis-only
      field, and §1's key structure is untouched: no
      `battle:{battleId}:atk` key, no hash field, no set, no index, and no
      second record exists for it. It is **not** a concurrency token;
      `Sequence` remains the only one (§4 item 6).
    - **Round-trip losslessness covers it, and an empty collection round-trips
      as empty.** A record that drops an element, alters an
      `atkModifierPercentage`, **drops or alters the element's `lifetime`**,
      reorders elements, or collapses two distinct source identities into one
      does not round-trip (`GAME_STATE.md` §2.3.8
      items 3 and 5, §2.1.7 item 5). The collection is always present
      (`GAME_STATE.md` §2.3.7 item 6), so a Pet with no active modifier
      serializes an **empty array** — it is never omitted and never `null`,
      unlike `LastCommittedSwapPair` (item 11), whose absence is itself a
      documented statement.
    - **The ordering obligation is deterministic, not merely preserved.** Unlike
      the sibling collections, whose order records application or equip order,
      this collection's order is the **`SourceIdentity` sort**
      (`GAME_STATE.md` §2.3.7 item 7). A store must therefore not be relied on
      to preserve insertion order for this member: the serialized order is
      reproducible from the element set alone, so two serializations of the same
      state are byte-identical (`TDD.md` §6, TASK-136 D9).
    - **The lifecycle and concurrency rules are untouched.** Creation,
      replace/refresh, and removal all occur inside one resolution and are
      written in the same single post-resolution write-back as the rest of the
      state (§4 item 5), under the same `Sequence` compare-and-set (§4 item 2),
      and under the unchanged §3 sliding TTL. A reader never observes a
      modifier mid-refresh (`GAME_STATE.md` §5.1.4 item 7). A rejected action
      writes nothing and therefore does not touch this key, reset the TTL, or
      change this collection (§4 item 7).
    - **It is not a staged subset.** §7 item 7's precondition is met — a real,
      playable battle carrying §2's shape can be created and resolved — so this
      member is carried by the existing `battle:{battleId}:state` record
      exactly as every other `PetState` member is (see the status note above).
      It changes no storage decision.
    - **No ATK value is stored here.** The collection stores the *modifiers*;
      `EffectivePetATK` is a value derived at attack resolution
      (`COMBAT_RULES.md` §5.6) and is not a stored field, a Redis-only field, or
      a second representation of the ATK stat (`GAME_STATE.md` §2.3.7 item 9).
    - **No expiry of its own is introduced by storage.** The modifier's `Battle`
      lifetime is owned by `RELIC_RULES.md` §8.3/§8.4 and mutated by
      `GAME_STATE.md` §5.1.4; Redis adds no TTL, sweep, or expiry for this
      collection beyond the record's own §3 sliding TTL.

    Like the Special Gem, commit-record, accounting, Card-cost, and
    Crit-modifier changes above, this is a **content** change to the record
    rather than a **structure** change to the store.
17. **`PetState.BurnDamageModifiers[]` adds no key, no Redis-only field, and no
    persistence work.** `GAME_STATE.md` §2.3.9 defines one new `PetState`
    collection (TASK-179, documenting the carrier TASK-177 implemented) —
    applied, `Battle`-scoped Burn-damage modifiers on the active Pet, each
    element carrying a source identity and a Burn-damage modification in
    percentage points, and deliberately **no `lifetime` member** because
    `RELIC_RULES.md` §8.3's `BurnDamage` row fixes the single `Battle` lifetime
    (`GAME_STATE.md` §2.3.9 item 6). Its consequences here are these, and nothing
    more:

    - **It is part of the §2 shape, so §2 item 1 covers it unchanged.**
      `BattleState` is serialized as JSON matching `GAME_STATE.md` §2 exactly,
      and this collection is a `PetState` member. It introduces no Redis-only
      field, and §1's key structure is untouched: no
      `battle:{battleId}:burndamage` key, no hash field, no set, no index, and
      no second record exists for it. It is **not** a concurrency token;
      `Sequence` remains the only one (§4 item 6), and it is not a staged
      subset.
    - **Round-trip losslessness covers it, and an empty collection round-trips
      as empty.** A record that drops an element, alters a
      `burnDamagePercentage`, reorders elements, or collapses two distinct source
      identities into one does not round-trip (`GAME_STATE.md` §2.3.10 items 3
      and 5, §2.1.7 item 5). The collection is always present
      (`GAME_STATE.md` §2.3.9 item 7), so a Pet with no active modifier
      serializes an **empty array** — it is never omitted and never `null`,
      unlike `LastCommittedSwapPair` (item 11), whose absence is itself a
      documented statement. A stored element carrying a blank `sourceIdentity` is
      a contract violation and is rejected at the read rather than repaired
      (`GAME_STATE.md` §2.3.10 item 7).
    - **The ordering obligation is preservation, not a sort.** The collection's
      order is its written order and is preserved for round-trip fidelity
      (`GAME_STATE.md` §2.3.9 item 9, §2.3.10 item 6) — the same obligation
      items 13 and 15 state for the sibling collections, and the deliberate
      contrast with item 16's deterministic `SourceIdentity` sort for
      `ATKModifiers[]`. No sorting rule is introduced for this member.
    - **The lifecycle and concurrency rules are untouched.** Creation,
      replace/refresh, and source-specific removal all occur inside one
      resolution and are written in the same single post-resolution write-back as
      the rest of the state (§4 item 5), under the same `Sequence`
      compare-and-set (§4 item 2), and under the unchanged §3 sliding TTL. A
      reader never observes a modifier mid-refresh (`GAME_STATE.md` §5.1.5
      item 7). A rejected action writes nothing and therefore does not touch this
      key, reset the TTL, or change this collection (§4 item 7).
    - **No Burn damage is stored here.** The collection stores the *modifiers*;
      a modified Burn tick's damage is a value the Damage Pipeline derives at
      tick resolution (`COMBAT_RULES.md` §3 step 4, §5.2 item 4) and is not a
      stored field, a Redis-only field, or a second representation of a Burn
      instance's damage (`GAME_STATE.md` §2.3.9 item 14).
    - **No expiry of its own is introduced by storage.** The modifier's `Battle`
      lifetime is owned by `RELIC_RULES.md` §8.3/§8.4 and mutated by
      `GAME_STATE.md` §5.1.5; Redis adds no TTL, sweep, or expiry for this
      collection beyond the record's own §3 sliding TTL. In particular, storing
      it creates no step-19a participation and no cross-battle carry
      (`GAME_STATE.md` §5.1.5 items 4 and 6).
    - **No Burn instance is affected.** A value stored here creates, refreshes,
      extends, or consumes no Burn event and no Burn instance
      (`COMBAT_RULES.md` §5.2 item 4); the instance and its countdown remain
      `StatusEffects[]`' and its own §7 item 13 sibling lifecycle.

    Like the Special Gem, commit-record, accounting, Card-cost, ATK-modifier,
    and Crit-modifier changes above, this is a **content** change to the record
    rather than a **structure** change to the store.
