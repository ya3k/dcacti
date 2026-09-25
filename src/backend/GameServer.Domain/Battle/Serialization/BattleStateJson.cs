using System.Text.Json.Serialization;

namespace GameServer.Domain.Battle.Serialization;

/// <summary>
/// The JSON member names of the runtime <c>BattleState</c> serialization mapping
/// (<c>GAME_STATE.md</c> §2, §2.1.7 item 4; <c>REDIS_STATE.md</c> §2 item 1).
///
/// <b>Every name is declared here, once.</b> <c>GAME_STATE.md</c> §2.1.7 item 4
/// makes the exact JSON member names and casing "an implementation detail of the
/// serializer", and <c>SIGNALR_PROTOCOL.md</c> §3.2.3 warns against relying on a
/// serializer default. The mapping therefore names every member explicitly rather
/// than depending on a naming policy, so changing a global option cannot silently
/// rename a persisted member.
///
/// This is the <b>camelCase</b> convention the existing model-level serialization
/// evidence already uses (<c>BoardStateSerializationTests</c>). It is a runtime
/// persistence representation only: it is not the SignalR wire contract
/// (<c>SIGNALR_PROTOCOL.md</c> owns that) and it is not a second state path.
///
/// These names are the <c>BattleState</c> tree's own member names, projected
/// one-to-one from <c>GAME_STATE.md</c> §2 — the same members, never a renamed or
/// additional one (<c>REDIS_STATE.md</c> §2 item 1: "no additional Redis-only
/// fields").
/// </summary>
internal static class BattleStateJsonNames
{
    // BattleState root (GAME_STATE.md §2).
    public const string BattleId = "battleId";
    public const string Turn = "turn";
    public const string Sequence = "sequence";
    public const string RngSeed = "rngSeed";
    public const string RngState = "rngState";
    public const string BoardState = "boardState";
    public const string Combo = "combo";
    public const string MatchCount = "matchCount";
    public const string PetState = "petState";
    public const string BossState = "bossState";
    public const string LastCommittedSwapPair = "lastCommittedSwapPair";

    // RngState pair (GAME_STATE.md §2.6.2 item 1 — the two components stay together).
    public const string RngStateValue = "state";
    public const string RngIncrement = "increment";

    // BoardState (GAME_STATE.md §2.1) — Cells[64] is the whole of it.
    public const string BoardCells = "cells";

    // A cell entry (GAME_STATE.md §2.1.1).
    public const string CellGemType = "gemType";
    public const string CellSpecialGem = "specialGem";

    // SpecialGem metadata (GAME_STATE.md §2.1.4).
    public const string SpecialGemType = "type";
    public const string SpecialGemOrientation = "orientation";

    // PetState (GAME_STATE.md §2.3).
    public const string PetHP = "hp";
    public const string PetMaxHP = "maxHp";
    public const string PetATK = "atk";
    public const string PetDEF = "def";
    public const string PetCrit = "crit";
    public const string PetPower = "power";
    public const string PetElement = "element";
    public const string PetPassiveId = "passiveId";
    public const string PetPassiveProgress = "passiveProgress";
    public const string PetPassiveResetOverride = "passiveResetOverride";
    public const string PetEquippedRelics = "equippedRelics";
    public const string PetEquippedCards = "equippedCards";

    // PassiveProgress — the documented "current count vs. threshold" pair (§2.3).
    public const string PassiveThreshold = "threshold";
    public const string PassiveCurrent = "current";

    // BossState (GAME_STATE.md §2.4).
    public const string BossId = "bossId";
    public const string BossElement = "element";
    public const string BossHP = "hp";
    public const string BossMaxHP = "maxHp";
    public const string BossATK = "atk";
    public const string BossDEF = "def";
    public const string BossStateKind = "state";
    public const string BossPassiveId = "passiveId";
    public const string BossPassiveProgress = "passiveProgress";
    public const string BossSkillCharge = "skillCharge";
    public const string BossSkillCooldown = "skillCooldown";

    // LastCommittedSwapPair (GAME_STATE.md §2.1.10) — the canonical (min, max) pair.
    public const string SwapMinCellIndex = "minCellIndex";
    public const string SwapMaxCellIndex = "maxCellIndex";
}

/// <summary>
/// The runtime JSON projection of <c>BattleState</c> (<c>GAME_STATE.md</c> §2;
/// <c>REDIS_STATE.md</c> §2).
///
/// <b>This is a mapping, not a second contract.</b> Every member is one member of
/// <c>GAME_STATE.md</c> §2's tree, in the same order, with the same meaning. It
/// introduces no field the contract does not have, and it omits none the contract
/// does have: <c>REDIS_STATE.md</c> §2 item 1 requires the record to match §2
/// "exactly — no additional Redis-only fields".
///
/// <b>It is not a wire DTO.</b> The SignalR projection is
/// <c>SIGNALR_PROTOCOL.md</c>'s and lives at the Api boundary
/// (<c>BattleHub.ToPayload</c>); it delivers a different, protocol-fixed subset
/// (for example, <c>LastCommittedSwapPair</c> is deliberately never delivered —
/// §4 item 12). This mapping carries the whole authoritative state for
/// persistence, which is a different question from what the client is sent.
///
/// <b>Optional members are nullable and omitted when absent.</b>
/// <c>LastCommittedSwapPair</c> is <c>null</c> until the first Swap is committed,
/// and <c>GAME_STATE.md</c> §2.1.10 item 3 makes that absence the documented
/// representation of "no Swap has been committed". The serializer writes no
/// sentinel pair in its place (§2.1.10 item 10).
/// </summary>
internal sealed record BattleStateJson
{
    [JsonPropertyName(BattleStateJsonNames.BattleId)]
    public required string BattleId { get; init; }

    [JsonPropertyName(BattleStateJsonNames.Turn)]
    public required int Turn { get; init; }

    [JsonPropertyName(BattleStateJsonNames.Sequence)]
    public required int Sequence { get; init; }

    /// <summary>
    /// The battle's seed as an unsigned 64-bit value (<c>GAME_STATE.md</c>
    /// §2.6.1). It is written as a JSON number; <c>RngSeed</c> is never rewritten
    /// after creation and this mapping never re-derives it.
    /// </summary>
    [JsonPropertyName(BattleStateJsonNames.RngSeed)]
    public required ulong RngSeed { get; init; }

    [JsonPropertyName(BattleStateJsonNames.RngState)]
    public required RngStateJson RngState { get; init; }

    [JsonPropertyName(BattleStateJsonNames.BoardState)]
    public required BoardStateJson BoardState { get; init; }

    /// <summary>
    /// Root Match/Combo accounting (<c>GAME_STATE.md</c> §2.2). It is written even
    /// when it is <c>0</c>: <c>Combo = 0</c> is a real publishable value, not an
    /// absence convention (§2.2.1 item 1).
    /// </summary>
    [JsonPropertyName(BattleStateJsonNames.Combo)]
    public required int Combo { get; init; }

    /// <inheritdoc cref="Combo"/>
    [JsonPropertyName(BattleStateJsonNames.MatchCount)]
    public required int MatchCount { get; init; }

    [JsonPropertyName(BattleStateJsonNames.PetState)]
    public required PetStateJson PetState { get; init; }

    [JsonPropertyName(BattleStateJsonNames.BossState)]
    public required BossStateJson BossState { get; init; }

    /// <summary>
    /// The canonical committed-Swap record, or <c>null</c> when no Swap has been
    /// committed (<c>GAME_STATE.md</c> §2.1.10). The null condition omits the
    /// member entirely rather than writing <c>null</c>, because absence is what the
    /// contract's representation means (§2.1.10 items 3 and 10).
    /// </summary>
    [JsonPropertyName(BattleStateJsonNames.LastCommittedSwapPair)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CommittedSwapPairJson? LastCommittedSwapPair { get; init; }
}

/// <summary>
/// The <c>RngState</c> pair (<c>GAME_STATE.md</c> §2.6.2).
///
/// Its two components travel as one nested object, not as two flat members: §2.6.2
/// item 1 makes them one logical field whose components "are never split across
/// separate <c>BattleState</c> fields", and nesting is what keeps the serialized
/// shape matching that rule.
/// </summary>
internal sealed record RngStateJson
{
    [JsonPropertyName(BattleStateJsonNames.RngStateValue)]
    public required ulong State { get; init; }

    [JsonPropertyName(BattleStateJsonNames.RngIncrement)]
    public required ulong Increment { get; init; }
}

/// <summary>
/// <c>BoardState</c> (<c>GAME_STATE.md</c> §2.1) — <c>Cells[64]</c> and nothing
/// else. There is no second field and no parallel Special Gem collection
/// (§2.1.2 item 1).
/// </summary>
internal sealed record BoardStateJson
{
    /// <summary>
    /// Exactly 64 entries in ascending cell-index order (§2.1.1 item 6,
    /// §2.1.7 item 2). The array position <b>is</b> the cell index, so no element
    /// carries an index of its own.
    /// </summary>
    [JsonPropertyName(BattleStateJsonNames.BoardCells)]
    public required IReadOnlyList<CellJson> Cells { get; init; }
}

/// <summary>
/// One <c>Cells[64]</c> entry (<c>GAME_STATE.md</c> §2.1.1) — the cell's Gem type
/// plus its optional Special Gem.
/// </summary>
internal sealed record CellJson
{
    /// <summary>
    /// The cell's Gem type by its documented contract name — <c>ATK</c>,
    /// <c>DEF</c>, <c>HP</c>, <c>POWER</c> (<c>MATCH3_RULES.md</c> §1.1). It is
    /// always present, including for a cell holding a Special Gem, because a
    /// Special Gem is metadata on the occupant and not a substitute for its Gem
    /// type (§2.1.3 items 1–2).
    ///
    /// The contract name is written rather than the enum ordinal: the ordinal is
    /// an implementation detail no document fixes, and a stored record must not
    /// change meaning if a member is added to the enum.
    /// </summary>
    [JsonPropertyName(BattleStateJsonNames.CellGemType)]
    public required string GemType { get; init; }

    /// <summary>
    /// The cell's Special Gem, or <c>null</c> for an ordinary Gem. The null
    /// condition omits the member, which is the documented "absent" representation
    /// — "not a null element, not a sentinel type, and not a default value"
    /// (§2.1.7 item 3).
    /// </summary>
    [JsonPropertyName(BattleStateJsonNames.CellSpecialGem)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SpecialGemJson? SpecialGem { get; init; }
}

/// <summary>
/// <c>SpecialGem</c> metadata (<c>GAME_STATE.md</c> §2.1.4) — the type, and the
/// orientation only when the type requires it.
/// </summary>
internal sealed record SpecialGemJson
{
    /// <summary>
    /// <c>LineClear</c>, <c>Burst</c>, or <c>Area</c> — the three MVP types
    /// (§2.1.4 item 1). There is no fourth type and no <c>None</c> value; an
    /// ordinary Gem is an absent member, never a type (§2.1.7 item 3).
    /// </summary>
    [JsonPropertyName(BattleStateJsonNames.SpecialGemType)]
    public required string Type { get; init; }

    /// <summary>
    /// <c>Horizontal</c> or <c>Vertical</c>, present <b>if and only if</b> the type
    /// is <c>LineClear</c> (§2.1.4 item 2). A <c>Burst</c> or <c>Area</c> entry
    /// carries no orientation member, because an orientation on those types would
    /// be a value no rule reads.
    /// </summary>
    [JsonPropertyName(BattleStateJsonNames.SpecialGemOrientation)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Orientation { get; init; }
}

/// <summary>
/// <c>PetState</c> (<c>GAME_STATE.md</c> §2.3) — the active Pet's combat stats,
/// Element, Passive, and the battle-scoped loadout snapshots.
///
/// There is no <c>PlayerState</c> node here or anywhere in this mapping: the Pet
/// is the combat character and the Player is the account/owner with no
/// authoritative battle-time combat pool (§2, <c>ADR-011</c>).
///
/// Only the members the current contract implements exist
/// (<c>StatusEffects[]</c> is not yet implemented and is therefore not a member —
/// §2.3, §0 item 4).
/// </summary>
internal sealed record PetStateJson
{
    // The combat stats are never omitted and never null: they are defined from
    // battle creation and zero is a real value (§2.3). No ignore condition is
    // declared for any of them.

    [JsonPropertyName(BattleStateJsonNames.PetHP)]
    public required int HP { get; init; }

    [JsonPropertyName(BattleStateJsonNames.PetMaxHP)]
    public required int MaxHP { get; init; }

    [JsonPropertyName(BattleStateJsonNames.PetATK)]
    public required int ATK { get; init; }

    [JsonPropertyName(BattleStateJsonNames.PetDEF)]
    public required int DEF { get; init; }

    /// <summary>
    /// Critical hit chance as a percentage (§2.3) — the value is <c>5</c>, not
    /// <c>0.05</c>; the unit is <c>COMBAT_RULES.md</c> §1.1's.
    /// </summary>
    [JsonPropertyName(BattleStateJsonNames.PetCrit)]
    public required int Crit { get; init; }

    [JsonPropertyName(BattleStateJsonNames.PetPower)]
    public required int Power { get; init; }

    /// <summary>
    /// The Pet's one Element (<c>ELEMENT_RULES.md</c> §1.2). It is written by its
    /// enum name — <c>Moc</c>, <c>Tho</c>, <c>Thuy</c>, <c>Hoa</c>, <c>Kim</c> —
    /// rather than by ordinal, so a stored record does not depend on member
    /// ordering. It is a non-nullable value: §1.1 gives every Pet an Element, so
    /// there is no elementless-Pet case to spell.
    /// </summary>
    [JsonPropertyName(BattleStateJsonNames.PetElement)]
    public required string Element { get; init; }

    /// <summary>
    /// The Pet's one Passive identity (§2.3). It is present from battle creation
    /// and has no absent form — a battle always has its one active Pet and
    /// therefore its one Passive (§2.3 item 3).
    /// </summary>
    [JsonPropertyName(BattleStateJsonNames.PetPassiveId)]
    public required string PassiveId { get; init; }

    [JsonPropertyName(BattleStateJsonNames.PetPassiveProgress)]
    public required PassiveProgressJson PassiveProgress { get; init; }

    /// <summary>
    /// The Passive's non-default Reset Behavior, or <c>null</c> for the default
    /// (§2.3, <c>PASSIVE_RULES.md</c> §4). The null condition omits the member,
    /// because §2.3 makes its <i>absence</i> the representation of
    /// <c>Default</c> — a <c>"Default"</c> string or an explicit <c>null</c> would
    /// be a second spelling of one fact.
    /// </summary>
    [JsonPropertyName(BattleStateJsonNames.PetPassiveResetOverride)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PassiveResetOverride { get; init; }

    /// <summary>
    /// The battle-scoped Relic loadout snapshot — 3–5 owned Relic instance
    /// identities in equip-slot order, so element <c>i</c> is slot <c>i + 1</c>
    /// (<c>RELIC_RULES.md</c> §2.2, §2.3, §2.5; §2.3). Order is
    /// gameplay-significant and is preserved exactly as submitted, never sorted.
    ///
    /// It is <c>null</c> only while the Relic loadout stage has not supplied it —
    /// the documented staging position, which is not an empty loadout
    /// (<c>RELIC_RULES.md</c> §2.1 item 1 defines no zero-Relic battle). The member
    /// is then omitted; an empty array is never substituted for it.
    /// </summary>
    [JsonPropertyName(BattleStateJsonNames.PetEquippedRelics)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? EquippedRelics { get; init; }

    /// <summary>
    /// The battle-scoped Card loadout snapshot — exactly 4
    /// <c>CardDefinitionId</c> values (the 3 submitted Basic Cards plus the active
    /// Pet's derived Signature Skill Card) (§2.3, <c>CARD_RULES.md</c> §1).
    ///
    /// Element order carries no gameplay significance (§2.3), but the array is
    /// still carried through unchanged rather than sorted, because no rule
    /// authorizes reordering it. Repeated values are the same definition repeated,
    /// not separate instances (there are no Card instances — <c>ADR-012</c>
    /// item 9), so a duplicate must round-trip as a duplicate.
    /// </summary>
    [JsonPropertyName(BattleStateJsonNames.PetEquippedCards)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? EquippedCards { get; init; }
}

/// <summary>
/// <c>PassiveProgress</c> — "current count vs. threshold" (<c>GAME_STATE.md</c>
/// §2.3, §2.5). Both members are always present; <c>Current = 0</c> is a real
/// publishable value, so absence is never used for it.
/// </summary>
internal sealed record PassiveProgressJson
{
    [JsonPropertyName(BattleStateJsonNames.PassiveThreshold)]
    public required int Threshold { get; init; }

    [JsonPropertyName(BattleStateJsonNames.PassiveCurrent)]
    public required int Current { get; init; }
}

/// <summary>
/// <c>BossState</c> (<c>GAME_STATE.md</c> §2.4) — the battle's one Boss.
///
/// <c>StatusEffects[]</c> is not yet implemented and is therefore not a member
/// (§2.4.1).
/// </summary>
internal sealed record BossStateJson
{
    [JsonPropertyName(BattleStateJsonNames.BossId)]
    public required string BossId { get; init; }

    /// <inheritdoc cref="PetStateJson.Element"/>
    [JsonPropertyName(BattleStateJsonNames.BossElement)]
    public required string Element { get; init; }

    [JsonPropertyName(BattleStateJsonNames.BossHP)]
    public required int HP { get; init; }

    [JsonPropertyName(BattleStateJsonNames.BossMaxHP)]
    public required int MaxHP { get; init; }

    [JsonPropertyName(BattleStateJsonNames.BossATK)]
    public required int ATK { get; init; }

    [JsonPropertyName(BattleStateJsonNames.BossDEF)]
    public required int DEF { get; init; }

    /// <summary>
    /// The Boss's own internal State — <c>Idle</c>, <c>Charging</c>,
    /// <c>Enraged</c>, or <c>Stunned</c> (<c>BOSS_RULES.md</c> §1, §2.4). It is
    /// written by name, not ordinal.
    ///
    /// It is <b>not</b> a battle lifecycle value: a battle has no lifecycle state
    /// machine and no <c>Status</c> field (§2.0.3), and this member is the Boss's
    /// own documented enum, which the mapping carries as state without
    /// transitioning or evaluating it.
    /// </summary>
    [JsonPropertyName(BattleStateJsonNames.BossStateKind)]
    public required string State { get; init; }

    [JsonPropertyName(BattleStateJsonNames.BossPassiveId)]
    public required string PassiveId { get; init; }

    [JsonPropertyName(BattleStateJsonNames.BossPassiveProgress)]
    public required PassiveProgressJson PassiveProgress { get; init; }

    /// <summary>
    /// Matches charged toward the Skill's requirement (§2.4.3).
    /// </summary>
    [JsonPropertyName(BattleStateJsonNames.BossSkillCharge)]
    public required int SkillCharge { get; init; }

    /// <summary>
    /// Turns remaining before the Skill can fire (§2.4.3).
    /// </summary>
    [JsonPropertyName(BattleStateJsonNames.BossSkillCooldown)]
    public required int SkillCooldown { get; init; }
}

/// <summary>
/// <c>LastCommittedSwapPair</c> (<c>GAME_STATE.md</c> §2.1.10) — the canonical
/// <c>(min, max)</c> record of the most recently committed Swap.
///
/// The two indices are written in ascending order, so the serialized order is
/// itself canonical and a round trip cannot reorder or re-spell the pair
/// (§2.1.10 items 2 and 10).
/// </summary>
internal sealed record CommittedSwapPairJson
{
    [JsonPropertyName(BattleStateJsonNames.SwapMinCellIndex)]
    public required int MinCellIndex { get; init; }

    [JsonPropertyName(BattleStateJsonNames.SwapMaxCellIndex)]
    public required int MaxCellIndex { get; init; }
}
