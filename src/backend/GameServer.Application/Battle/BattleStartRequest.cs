namespace GameServer.Application.Battle;

/// <summary>
/// The submitted <c>POST /api/battle/start</c> request
/// (<c>API_CONTRACTS.md</c> §3).
///
/// <code>
/// {
///   "petId":        "string",
///   "bossId":       "string",
///   "cardLoadout":  ["heal", "shield", "power_charge"],
///   "relicLoadout": ["relic_id_1", "relic_id_2", "relic_id_3"]
/// }
/// </code>
///
/// <b>These are the documented members and no others.</b> §3 defines exactly
/// four selection/input fields, so this type adds none: in particular there is
/// no <c>playerId</c> (the requesting Player is resolved from the authenticated
/// session — §1's "All endpoints … require an authenticated session", §3's
/// "the requesting Player"), no <c>battleId</c>, and no combat value.
///
/// <b>No authoritative value is accepted.</b> <c>API_CONTRACTS.md</c> §1 states
/// that no endpoint accepts Damage, HP, Power, Match, or Combo values, and
/// <c>GAME_RULES.md</c> §18 / ADR-001 make the server the sole author of
/// <c>BattleId</c>, <c>Turn</c>, <c>Sequence</c>, <c>RngState</c>,
/// <c>BoardState</c>, and every combat stat. None of those appears here, because
/// the client cannot supply one.
///
/// <code>
/// client supplies   →  petId, bossId, cardLoadout, relicLoadout
/// server authors    →  BattleId, Turn, Sequence, RngSeed, RngState,
///                      BoardState, Combo, MatchCount, PetState, BossState
/// </code>
/// </summary>
/// <param name="PetId">
/// The selected Pet's owned instance identity (<c>DATABASE.md</c> §1
/// <c>PetInstanceId</c>). It must be owned by the requesting Player
/// (<c>API_CONTRACTS.md</c> §3, <c>PET_RULES.md</c> §2).
/// </param>
/// <param name="BossId">
/// The selected Boss identity (<c>BOSS_RULES.md</c> §6.4 — the display-name
/// identity, e.g. <c>"Hỏa Long"</c>), which must resolve to a content-defined
/// MVP Boss (<c>API_CONTRACTS.md</c> §3, <c>BOSS_RULES.md</c> §6).
/// </param>
/// <param name="CardLoadout">
/// Exactly 3 submitted Basic Card <c>CardDefinitionId</c> values. The active
/// Pet's Signature Skill is <b>derived</b>, never submitted, and is not part of
/// this list (<c>CARD_RULES.md</c> §1, §4; <c>API_CONTRACTS.md</c> §3).
/// </param>
/// <param name="RelicLoadout">
/// The 3–5 selected owned Relic instance identities, <b>in equip-slot order</b>
/// — element <c>i</c> is slot <c>i + 1</c>. The array order is authoritative and
/// is never re-sorted (<c>RELIC_RULES.md</c> §2.3;
/// <c>API_CONTRACTS.md</c> §3).
/// </param>
public sealed record BattleStartRequest(
    string PetId,
    string BossId,
    IReadOnlyList<string> CardLoadout,
    IReadOnlyList<string> RelicLoadout);
