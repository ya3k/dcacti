# Redis State

**Version:** 1.4 (Player/Pet role model per ADR-011 — staged PlayerState
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

---

# 3. TTL / Lifecycle

```text
Created:   on POST /api/battle/start (API_CONTRACTS.md §2)
Refreshed: TTL reset on every successful resolution (sliding expiry),
           default 30 minutes of inactivity — Configurable
Deleted:   explicitly, when BattleWon/BattleLost is resolved and the result
           has been written to PostgreSQL (DATABASE.md)
```

If a battle's key expires from inactivity before completion, the battle is
considered abandoned; the client's `GetBattleState` call
(`SIGNALR_PROTOCOL.md` §7) will receive `BATTLE_NOT_FOUND`, and no partial
result is written to PostgreSQL.

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
3. **Why: no real battle can exist yet.** §3 ties creation to
   `POST /api/battle/start` (`API_CONTRACTS.md` §3), and that endpoint
   requires Pet, Boss, Card, and Relic loadout data — systems not yet
   implemented (`ROADMAP.md` §1 Phase 2). Until a battle can actually be
   started, there is nothing for this key to hold, so Redis persistence is
   deliberately deferred. This is a **sequencing** boundary, not a weakening
   of the requirement.
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
