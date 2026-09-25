using GameServer.Application.Battle;
using GameServer.Domain.Battle;
using GameServer.Domain.Match3;

namespace GameServer.Api.Controllers;

/// <summary>
/// The <c>POST /api/battle/start</c> success response
/// (<c>API_CONTRACTS.md</c> §3).
///
/// <code>
/// {
///   "battleId":     "string",
///   "signalrHub":   "string (hub URL/path)",
///   "initialState": { ... BattleState summary, GAME_STATE.md §2 }
/// }
/// </code>
///
/// These are exactly the three documented members. No additional member is
/// added by this stage, and none of the created battle's authoritative values is
/// promoted to a top-level field: <c>Turn</c>, <c>Sequence</c>, <c>RngSeed</c>,
/// <c>RngState</c>, the board, the Match/Combo accounting, the Pet, and the Boss
/// all travel inside <c>initialState</c>, which is what §3 defines it to be.
/// </summary>
/// <param name="BattleId">
/// The created battle's identity (<c>GAME_STATE.md</c> §2.0.1). It is the id the
/// client joins the battle group with (<c>SIGNALR_PROTOCOL.md</c> §1 items 1–2).
/// </param>
/// <param name="SignalrHub">
/// The hub path the client connects to (<c>SIGNALR_PROTOCOL.md</c> §1 item 2).
/// </param>
/// <param name="InitialState">
/// The summary of the created <c>BattleState</c> (<c>GAME_STATE.md</c> §2).
/// </param>
public record BattleStartResponse(
    string BattleId,
    string SignalrHub,
    BattleStartInitialState InitialState);

/// <summary>
/// The <c>initialState</c> summary of a created battle
/// (<c>API_CONTRACTS.md</c> §3: "BattleState summary, <c>GAME_STATE.md</c> §2").
///
/// <b>It is a one-to-one projection of the authoritative state</b>, read after
/// creation: the state is the server's (<c>GAME_STATE.md</c> §5.1,
/// <c>GAME_RULES.md</c> §18, ADR-001) and nothing here is computed, defaulted,
/// or adjusted. The summary carries the created battle's identity and counters,
/// its board, its Match/Combo accounting, and its Pet and Boss — the state §2
/// defines at the stage this endpoint is implemented for.
///
/// <b>These are the members the battle was created with, and the "at creation"
/// values are the documented ones.</b> <c>Turn</c> and <c>Sequence</c> are
/// <c>0</c> because starting a battle resolves no action
/// (<c>GAME_STATE.md</c> §2.0.2, §2.0.5.2 item 1), and <c>Combo</c> and
/// <c>MatchCount</c> are <c>0</c> because no Match has occurred
/// (<c>MATCH3_RULES.md</c> §6.5 item 4). Zero is a value here, not an absence, so
/// every member is written — the same rule this endpoint's Pet and Boss summaries
/// follow.
///
/// <b>The loadout snapshots travel with the Pet.</b> <c>PetState.EquippedCards</c>
/// (4 entries) and <c>PetState.EquippedRelics</c> (the validated 3–5, in equip
/// order) are part of the state this endpoint created and are summarized with it,
/// so the client can render the battle's fixed loadout from the start response
/// (<c>GAME_STATE.md</c> §2.3).
/// </summary>
/// <param name="BattleId">The battle's identity (<c>GAME_STATE.md</c> §2.0.1).</param>
/// <param name="Turn">The current Turn number — <c>0</c> at creation.</param>
/// <param name="Sequence">
/// The monotonic resolution counter — <c>0</c> at creation
/// (<c>GAME_STATE.md</c> §2.0.2).
/// </param>
/// <param name="RngSeed">The server-chosen PRNG seed (<c>GAME_STATE.md</c> §2.6.1).</param>
/// <param name="RngState">The PRNG state after initial board generation (§2.6.2).</param>
/// <param name="Board">The authoritative 64-cell board (§2.1.1).</param>
/// <param name="Combo">The Match/Combo accounting value — <c>0</c> at creation (§2.2).</param>
/// <param name="MatchCount">The cumulative Match count — <c>0</c> at creation (§2.2).</param>
/// <param name="PetState">The created Pet state, including both loadout snapshots (§2.3).</param>
/// <param name="BossState">The created Boss state, at full health in its Initial State (§2.4).</param>
public record BattleStartInitialState(
    string BattleId,
    int Turn,
    int Sequence,
    ulong RngSeed,
    BattleStartRngState RngState,
    BattleStartBoard Board,
    int Combo,
    int MatchCount,
    BattleStartPetState PetState,
    BattleStartBossState BossState);

/// <summary>
/// The PRNG state summary (<c>GAME_STATE.md</c> §2.6.2) — the state and
/// increment stay together as one logical field (<c>§2.6.2</c> item 1). They are
/// delivered because they are <c>BattleState</c> members; the client never
/// advances, re-seeds, or draws from them (<c>SIGNALR_PROTOCOL.md</c> §4 item 4).
/// </summary>
/// <param name="State">The PRNG's current internal state.</param>
/// <param name="Increment">The PRNG's stream selector.</param>
public record BattleStartRngState(ulong State, ulong Increment);

/// <summary>
/// The board summary (<c>GAME_STATE.md</c> §2.1.1) — 64 cells in row-major order,
/// where the array position is the cell index (<c>MATCH3_RULES.md</c> §1.0,
/// <c>GAME_STATE.md</c> §2.1.7 item 2). The client renders it and generates
/// nothing (<c>SIGNALR_PROTOCOL.md</c> §4.1 item 2).
/// </summary>
/// <param name="Cells">One entry per cell, in ascending cell-index order.</param>
public record BattleStartBoard(IReadOnlyList<BattleStartCell> Cells);

/// <summary>
/// One board cell (<c>GAME_STATE.md</c> §2.1.1) — its Gem type as the documented
/// contract name, plus the Special Gem occupying it when there is one.
/// </summary>
/// <param name="GemType">
/// The cell's Gem type contract name (<c>MATCH3_RULES.md</c> §1.1) — always
/// present, including under a Special Gem.
/// </param>
/// <param name="SpecialGem">
/// The Special Gem at this cell, or <c>null</c> for an ordinary Gem — absence is
/// the documented representation of "no Special Gem"
/// (<c>GAME_STATE.md</c> §2.1.7 item 3).
/// </param>
public record BattleStartCell(string GemType, BattleStartSpecialGem? SpecialGem);

/// <summary>
/// A Special Gem's metadata (<c>GAME_STATE.md</c> §2.1.4).
/// </summary>
/// <param name="Type"><c>LineClear</c>, <c>Burst</c>, or <c>Area</c>.</param>
/// <param name="Orientation">
/// <c>Horizontal</c> or <c>Vertical</c>, present only for a <c>LineClear</c> gem
/// (<c>GAME_STATE.md</c> §2.1.4 item 2).
/// </param>
public record BattleStartSpecialGem(string Type, string? Orientation);

/// <summary>
/// The created Pet state summary (<c>GAME_STATE.md</c> §2.3).
///
/// The combat stats, the Element, the Passive identity and progress, and
/// <b>both battle-scoped loadout snapshots</b> are carried. The loadouts are the
/// fixed snapshots the battle was created with: <c>EquippedCards</c> is the 4
/// entries the Card validator produced (3 submitted Basics plus the derived
/// Signature Skill), and <c>EquippedRelics</c> is the validated 3–5 in equip-slot
/// order. Neither is re-read from the database after creation
/// (<c>RELIC_RULES.md</c> §2.5, <c>CARD_RULES.md</c> §1, ADR-012 items 8 and 10).
/// </summary>
/// <param name="HP">Current health — <c>MaxHP</c> at creation.</param>
/// <param name="MaxHP">Maximum health (<c>COMBAT_RULES.md</c> §1.1).</param>
/// <param name="ATK">Attack power (<c>COMBAT_RULES.md</c> §1.1).</param>
/// <param name="DEF">Defense (<c>COMBAT_RULES.md</c> §1.1).</param>
/// <param name="Crit">Critical hit chance as a percentage (<c>COMBAT_RULES.md</c> §1.1).</param>
/// <param name="Power">Card/Skill resource, 0–100 — <c>0</c> at creation.</param>
/// <param name="Element">The active Pet's one Element (<c>PET_RULES.md</c> §1).</param>
/// <param name="PassiveId">The active Pet's Passive identity (<c>PASSIVE_RULES.md</c> §1).</param>
/// <param name="PassiveThreshold">The Passive's threshold (<c>PASSIVE_RULES.md</c> §1).</param>
/// <param name="PassiveCurrent">Progress toward it — <c>0</c> at creation (§2).</param>
/// <param name="EquippedCards">
/// The 4 equipped Card definition identities (<c>CARD_RULES.md</c> §1).
/// </param>
/// <param name="EquippedRelics">
/// The equipped Relic instance identities in equip-slot order
/// (<c>RELIC_RULES.md</c> §2.3, §2.5).
/// </param>
public record BattleStartPetState(
    int HP,
    int MaxHP,
    int ATK,
    int DEF,
    int Crit,
    int Power,
    string Element,
    string PassiveId,
    int PassiveThreshold,
    int PassiveCurrent,
    IReadOnlyList<string> EquippedCards,
    IReadOnlyList<string> EquippedRelics);

/// <summary>
/// The created Boss state summary (<c>GAME_STATE.md</c> §2.4) — the Boss the
/// battle is fought against, at full health in its Initial State.
///
/// It reports state only. No Boss behavior is implemented by this endpoint
/// (<c>BOSS_RULES.md</c> §3–§5): no AI, no response, no attack, no
/// victory/defeat (<c>GAME_RULES.md</c> §17 steps 18–19).
/// </summary>
/// <param name="BossId">The Boss's identity (<c>BOSS_RULES.md</c> §6.4).</param>
/// <param name="Element">The Boss's one Element (<c>BOSS_RULES.md</c> §6.1).</param>
/// <param name="HP">Current health — <c>MaxHP</c> at creation.</param>
/// <param name="MaxHP">Maximum health (<c>BOSS_RULES.md</c> §6.1).</param>
/// <param name="ATK">Attack power (<c>BOSS_RULES.md</c> §6.1).</param>
/// <param name="DEF">Defense (<c>BOSS_RULES.md</c> §6.1).</param>
/// <param name="State">The Boss's state kind — <c>Idle</c> at creation (§2.4.1).</param>
public record BattleStartBossState(
    string BossId,
    string Element,
    int HP,
    int MaxHP,
    int ATK,
    int DEF,
    string State);
