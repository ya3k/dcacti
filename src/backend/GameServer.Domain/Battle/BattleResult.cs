namespace GameServer.Domain.Battle;

/// <summary>
/// One durable battle outcome — the battle-end record
/// (<c>DATABASE.md</c> §1's <c>BattleResult</c> entity).
///
/// <code>
/// BattleResult
/// ├── BattleResultId        (= the battle's own BattleId; one row per battle)
/// ├── PlayerId              (BattleState.PlayerId — GAME_STATE.md §2.8)
/// ├── PetInstanceId         (BattleState.PetState.PetId — GAME_STATE.md §2.3)
/// ├── BossDefinitionId      (the BossDefinition row's persistence key — §1)
/// ├── Outcome               ("victory" | "defeat" — GAME_EVENTS.md §2)
/// ├── DurationTurns         (BattleState.Turn at terminal resolution)
/// ├── CompletedAt           (server clock at the durable write)
/// └── RewardSummary         (JSON; staging value {} until TASK-033)
/// </code>
///
/// <b>Every value is server-authoritative.</b> The outcome, the duration, and the
/// completion instant all come from the resolution the server itself ran and from
/// the authoritative state it produced; the identities come from that same state
/// and its creation context. Nothing here is derived from a client request, a
/// session, a display name, or a string convention
/// (<c>GAME_RULES.md</c> §18, <c>ADR-001</c>, <c>AGENTS.md</c> §10).
///
/// <b>This type defines no gameplay rule.</b> It is the persistence shape
/// <c>DATABASE.md</c> §1 documents, and it deliberately carries no
/// <c>Status</c>, no battle-state snapshot, no winner/loser id, no
/// <c>Sequence</c>, no RNG state, and no board: §1 defines exactly the eight
/// values above, and a battle's outcome is expressed as the <c>BattleWon</c> /
/// <c>BattleLost</c> events rather than as a lifecycle field
/// (<c>GAME_STATE.md</c> §2.0.3).
///
/// <b>It is deliberately persistence-framework independent.</b> Like
/// <see cref="BattleState"/> it references no EF Core, Redis, HTTP, or SignalR
/// concern: the storage mapping is Infrastructure's
/// (<c>ARCHITECTURE.md</c> §2.1 — Domain has no persistence dependency).
/// </summary>
/// <param name="BattleResultId">
/// The row's primary key — <b>the battle's own <c>BattleId</c></b>
/// (<c>DATABASE.md</c> §1: "<c>BattleResultId</c> <b>is</b> the battle's own
/// <c>BattleId</c> — no second identifier is introduced and no second row can
/// exist"). Because the key is the battle id, the database's primary-key
/// uniqueness <i>is</i> the at-most-one-row guarantee: a repeated terminal
/// persistence attempt for one battle inserts nothing further
/// (<c>REDIS_STATE.md</c> §3), and no separate idempotency mechanism is
/// introduced.
/// </param>
/// <param name="PlayerId">
/// The identity of the Player who fought the battle
/// (<c>DATABASE.md</c> §1, <c>GAME_STATE.md</c> §2.8) — copied from the
/// authoritative <c>BattleState.PlayerId</c> at battle end, never re-derived
/// from a session or from client input at that point (§2.8 item 4).
/// </param>
/// <param name="PetInstanceId">
/// The owned Pet <b>instance</b> that fought the battle
/// (<c>DATABASE.md</c> §1, <c>GAME_STATE.md</c> §2.3) — the
/// <c>Pet.PetInstanceId</c> copied from the authoritative
/// <c>BattleState.PetState.PetId</c>, never a <c>PetDefinitionId</c>.
/// </param>
/// <param name="BossDefinitionId">
/// The persistence key of the <c>BossDefinition</c> row whose <c>Identity</c>
/// equals the battle's <c>BossState.BossId</c>
/// (<c>DATABASE.md</c> §1 "Identity and reward sourcing" item 2, resolved by a
/// PostgreSQL lookup owned by Infrastructure — §1 note item 2). It is the row's
/// own key, never the canonical Identity and never a display name: the three are
/// never collapsed (<c>BOSS_RULES.md</c> §6.4, TASK-046). When no row resolves,
/// no result exists at all — the write fails closed and this member is never
/// fabricated (<c>DATABASE.md</c> §1 sourcing item 3).
/// </param>
/// <param name="Outcome">
/// The battle's outcome (<c>DATABASE.md</c> §1) — exactly <c>"victory"</c> or
/// <c>"defeat"</c>, the value set owned by <c>GAME_EVENTS.md</c> §2 and shared
/// with the REST response and the SignalR wire member
/// (<c>API_CONTRACTS.md</c> §4 note 4). One value per battle, and there is no
/// third value: the documented resolution emits <c>BattleWon</c> or
/// <c>BattleLost</c> and never both (<c>GAME_RULES.md</c> §1.4).
/// </param>
/// <param name="DurationTurns">
/// The battle's Turn count (<c>DATABASE.md</c> §1 "Duration and completion
/// sourcing" item 1) — <c>BattleState.Turn</c> captured at terminal resolution.
/// The terminal Turn <b>is</b> counted, so a battle that ends on its Nth
/// committed Swap records <c>N</c>, and a battle reaching a terminal state
/// before any committed Swap records <c>0</c>. <c>Sequence</c> is a different
/// counter and is never the source.
/// </param>
/// <param name="CompletedAt">
/// The server clock reading captured on the battle-end path when this durable
/// result is written (<c>DATABASE.md</c> §1 "Duration and completion sourcing"
/// item 2). It is server-authoritative and is never a client timestamp, a
/// session/JWT instant, the battle's creation time, or a Redis/SignalR
/// timestamp. No timezone is asserted by the contract; the column type is the
/// storage mapping's (§1 header).
/// </param>
/// <param name="RewardSummary">
/// The result's reward summary as JSON (<c>DATABASE.md</c> §1). Its member list
/// is owned by TASK-033, which is not resolved; until then the documented
/// staging value is the empty JSON object <c>{}</c> "for both <c>Outcome</c>s"
/// — a value that is always present and never absent. No reward field, value,
/// amount, or XP curve may be invented here (<c>AGENTS.md</c> §7).
/// </param>
public sealed record BattleResult(
    string BattleResultId,
    string PlayerId,
    string PetInstanceId,
    string BossDefinitionId,
    BattleOutcome Outcome,
    int DurationTurns,
    DateTimeOffset CompletedAt,
    string RewardSummary);
