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
    public const string PlayerId = "playerId";
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
    public const string PetId = "petId";
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

    // StatusEffects[] (GAME_STATE.md §2.3.1, §2.3.2 item 1) — the collection is
    // written on BOTH PetState and BossState under this one member name, and one
    // element shape serves both (§2.3.1 preamble, §2.4.1).
    public const string StatusEffects = "statusEffects";

    // NextAttackCritModifiers[] (GAME_STATE.md §2.3.4, §2.3.4 item 8;
    // REDIS_STATE.md §7 item 13) — a PetState-only collection. It is a DIFFERENT
    // collection from StatusEffects[] and deliberately carries a different member
    // name: §2.3.4 item 1 makes the two separate representations of two different
    // concepts, so sharing a name would be the collision §0 item 5 forbids.
    public const string NextAttackCritModifiers = "nextAttackCritModifiers";

    // One NextAttackCritModifier element (GAME_STATE.md §2.3.4 items 2–4 — exactly
    // two members, both always present, and no third member of any kind).
    public const string NextAttackCritSourceIdentity = "sourceIdentity";
    public const string NextAttackCritContribution = "critContribution";

    // ATKModifiers[] (GAME_STATE.md §2.3.7, §2.3.8 item 1; REDIS_STATE.md §7
    // item 16) — a PetState-only collection. It is a DIFFERENT collection from the
    // two above and deliberately carries a different member name: §2.3.7 makes it a
    // separate representation of a separate concept, so sharing a name would be the
    // collision §0 item 5 forbids.
    public const string ATKModifiers = "atkModifiers";

    // One ATKModifier element (GAME_STATE.md §2.3.7 item 11, §2.3.8 item 3 — exactly
    // three members, all always present, and no fourth member of any kind). The
    // `lifetime` member is TASK-178's addition (Product Owner decision Q-1 = A): the
    // one collection carries both the `Battle` and the `NextAttack` lifetime, so the
    // element states which one it declares and a reader never infers it from absence.
    public const string ATKModifierSourceIdentity = "sourceIdentity";
    public const string ATKModifierPercentage = "atkModifierPercentage";
    public const string ATKModifierLifetime = "lifetime";

    // CardCostModifiers[] (GAME_STATE.md §2.3.5, §2.3.6 item 1; REDIS_STATE.md §7
    // item 15) — the sibling PetState-only collection, distinct by the same reason.
    public const string CardCostModifiers = "cardCostModifiers";

    // One CardCostModifier element (GAME_STATE.md §2.3.5 items 5 and 7, §2.3.6
    // item 3 — exactly these two members, and no third).
    public const string CardCostModifierSourceIdentity = "sourceIdentity";
    public const string CardCostReductionPercentage = "costReductionPercentage";

    // BurnDamageModifiers[] — the Pet's applied Burn-damage modifiers, the runtime
    // form of the Relic `BurnDamage` effect (RELIC_RULES.md §8.2 item 1, §8.5
    // item 5; COMBAT_RULES.md §5.2 item 4). A PetState-only collection, distinct
    // from the three above and from StatusEffects[]: it is a modification applied to
    // Burn damage, not a Burn instance, so sharing a name with any of them would be
    // the collision GAME_STATE.md §0 item 5 forbids.
    public const string BurnDamageModifiers = "burnDamageModifiers";

    // One BurnDamageModifier element (exactly the same two-member shape as the
    // sibling CardCost element: a source-scoped key and one percentage, with the
    // lifetime fixed as `Battle` by RELIC_RULES.md §8.3's BurnDamage row and
    // therefore not carried).
    public const string BurnDamageModifierSourceIdentity = "sourceIdentity";
    public const string BurnDamagePercentage = "burnDamagePercentage";

    // One StatusEffect element (GAME_STATE.md §2.3.2 item 3 — the exact member
    // set, in the documented order). The four required members are always
    // written; the three optional ones carry an ignore-when-absent condition.
    public const string StatusEffectId = "id";
    public const string StatusEffectType = "type";
    public const string StatusEffectSource = "source";
    public const string StatusEffectMagnitude = "magnitude";
    public const string StatusEffectTargetStat = "targetStat";
    public const string StatusEffectRemainingTurns = "remainingTurns";
    public const string StatusEffectExpiryCondition = "expiryCondition";

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
/// §4 item 12, and the <c>bossState</c> projection carries only <c>hp</c> and
/// <c>maxHp</c> — §4.4 item 3). This mapping carries the whole authoritative
/// state for persistence, which is a different question from what the client is
/// sent.
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

    /// <summary>
    /// The identity of the Player who created this battle
    /// (<c>GAME_STATE.md</c> §2.8) — the account/owner identity, carried in the
    /// record so the battle-end persistence path can source
    /// <c>BattleResult.PlayerId</c> from authoritative state (<c>DATABASE.md</c>
    /// §1, <c>ADR-014</c> decision 1). It is always present — a battle always has
    /// its owner — so it has no absent form and no ignore condition.
    ///
    /// It is <b>not</b> a wire member: this mapping is the runtime persistence
    /// representation, and the SignalR projection is a different, protocol-fixed
    /// subset (<c>SIGNALR_PROTOCOL.md</c> §4 item 4, §7.1).
    /// </summary>
    [JsonPropertyName(BattleStateJsonNames.PlayerId)]
    public required string PlayerId { get; init; }

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
/// <c>PetState</c> (<c>GAME_STATE.md</c> §2.3) — the owned Pet instance identity,
/// the active Pet's combat stats, Element, Passive, and the battle-scoped loadout
/// snapshots.
///
/// There is no <c>PlayerState</c> node here or anywhere in this mapping: the Pet
/// is the combat character and the Player is the account/owner with no
/// authoritative battle-time combat pool (§2, <c>ADR-011</c>).
/// </summary>
internal sealed record PetStateJson
{
    /// <summary>
    /// The owned Pet instance this battle's active Pet is
    /// (<c>GAME_STATE.md</c> §2.3) — the same value as
    /// <c>Pet.PetInstanceId</c> (<c>DATABASE.md</c> §1) and the
    /// <c>BattleResult.PetInstanceId</c> the battle-end persistence path writes.
    /// It is the instance, never a definition id — <c>ADR-014</c> decision 4
    /// records that this member already is the instance, so no second
    /// <c>PetInstanceId</c> member is written.
    ///
    /// It is present from battle creation and has no absent form — a battle
    /// always has its one active Pet, selected from the Player's owned collection
    /// (§2.3 item 3) — so it carries no ignore condition.
    /// </summary>
    [JsonPropertyName(BattleStateJsonNames.PetId)]
    public required string PetId { get; init; }

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

    /// <summary>
    /// The active Status Effect instances on this entity
    /// (<c>GAME_STATE.md</c> §2.3.1, §2.3.2 item 1) — the same element shape and
    /// the same lifecycle as <c>BossState.StatusEffects[]</c> (§2.4.1).
    ///
    /// <b>It is always written, and it is never <c>null</c>.</b> §2.3.2 item 1
    /// makes an entity with no active effect serialize an <b>empty array</b>:
    /// "the collection always exists (§0 item 4), so it is never omitted and
    /// never <c>null</c>". It therefore carries <b>no ignore condition</b> —
    /// deliberately unlike <see cref="EquippedRelics"/> and
    /// <see cref="EquippedCards"/> above, whose stages have a documented "not yet
    /// supplied" state to spell. This collection has no such state, so no
    /// absent-member spelling is introduced for it (§0 item 5).
    ///
    /// <b>It is nullable in the DTO so a violation is rejectable, not admissible.</b>
    /// The mapping always writes an array, so the <c>null</c> branch is reachable
    /// only from a stored document that broke the contract. The type admits it so
    /// that <c>FromStatusEffectsJson</c> can reject it with a message naming
    /// §2.3.2 item 1, instead of the failure surfacing incidentally from a LINQ
    /// call. A <c>null</c> here is never a state the Domain accepts.
    /// </summary>
    [JsonPropertyName(BattleStateJsonNames.StatusEffects)]
    public required IReadOnlyList<StatusEffectJson>? StatusEffects { get; init; }

    /// <summary>
    /// The active temporary Crit modifiers awaiting consumption by a qualifying
    /// owner attack (<c>GAME_STATE.md</c> §2.3.4, <c>ADR-017</c>).
    ///
    /// <b>It is always written, and it is never <c>null</c>.</b> §2.3.4 item 5 makes
    /// the collection always-present — "Absence of the <i>collection</i> is not a
    /// representable state" — so an entity with no active modifier serializes an
    /// <b>empty array</b>. It therefore carries <b>no ignore condition</b>, exactly
    /// like <see cref="StatusEffects"/> above and deliberately unlike
    /// <see cref="EquippedRelics"/> and <see cref="EquippedCards"/>, whose stages
    /// have a documented "not yet supplied" state to spell. This collection has no
    /// such state.
    ///
    /// <b>It is nullable in the DTO so a violation is rejectable, not admissible.</b>
    /// As with <see cref="StatusEffects"/>, the mapping always writes an array, so
    /// the <c>null</c> branch is reachable only from a stored document that broke
    /// the contract; the type admits it so the reader can reject it with a message
    /// naming §2.3.4 item 5 rather than failing incidentally.
    ///
    /// <b>Order participates in the round trip.</b> <c>REDIS_STATE.md</c> §7
    /// item 13 requires the order to survive serialization, even though §2.3.4
    /// item 6 makes it non-semantic.
    /// </summary>
    [JsonPropertyName(BattleStateJsonNames.NextAttackCritModifiers)]
    public required IReadOnlyList<NextAttackCritModifierJson>? NextAttackCritModifiers { get; init; }

    /// <summary>
    /// The active Pet's applied, Battle-scoped ATK modifiers, one entry per active
    /// source (<c>GAME_STATE.md</c> §2.3.7; <c>TASK-136</c> D1/D2/D9).
    ///
    /// <b>It is always written, and it is never <c>null</c>.</b> §2.3.8 item 1: "A Pet
    /// with no active modifier serializes an <b>empty array</b> — <c>[]</c> — because
    /// the collection always exists (§2.3.7 item 6). It is never omitted and never
    /// <c>null</c>." It therefore carries <b>no ignore condition</b>, exactly like
    /// <see cref="StatusEffects"/> and <see cref="NextAttackCritModifiers"/> above.
    ///
    /// <b>It is nullable in the DTO so a violation is rejectable, not admissible.</b>
    /// The mapping always writes an array, so the <c>null</c> branch is reachable only
    /// from a stored document that broke the contract; the type admits it so the reader
    /// can reject it with a message naming §2.3.7 item 6 rather than failing
    /// incidentally. A <c>null</c> here is never a state the Domain accepts.
    ///
    /// <b>Order participates in the round trip, and it is deterministic.</b> §2.3.8
    /// item 5 requires the same element order back, and §2.3.7 item 7 fixes that order
    /// as the <c>SourceIdentity</c> sort — so the writer emits the collection as the
    /// Domain holds it and neither re-sorts nor relies on insertion order
    /// (<c>REDIS_STATE.md</c> §7 item 16: two serializations of the same state are
    /// byte-identical).
    /// </summary>
    [JsonPropertyName(BattleStateJsonNames.ATKModifiers)]
    public required IReadOnlyList<ATKModifierJson>? ATKModifiers { get; init; }

    /// <summary>
    /// The active Pet's applied, Battle-scoped Card-cost modifiers, one entry per
    /// active source (<c>GAME_STATE.md</c> §2.3.5; <c>TASK-134</c> D1/D2).
    ///
    /// <b>It is always written, and it is never <c>null</c>.</b> §2.3.6 item 1: "A Pet
    /// with no active modifier serializes an <b>empty array</b> — the collection always
    /// exists (§2.3.5 item 6), so it is never omitted and never <c>null</c>." It
    /// therefore carries <b>no ignore condition</b>.
    ///
    /// <b>It is nullable in the DTO so a violation is rejectable, not admissible.</b>
    /// As with <see cref="ATKModifiers"/>, the mapping always writes an array.
    ///
    /// <b>Order participates in the round trip, and it is preserved rather than
    /// sorted.</b> §2.3.6 item 5 requires "the same element order" back and item 6
    /// states that "Order is preserved for round-trip fidelity, not for semantics" —
    /// so the writer emits the order the state holds and imposes none of its own. This
    /// is the deliberate contrast with <see cref="ATKModifiers"/> above, whose order is
    /// the §2.3.7 item 7 identity sort.
    /// </summary>
    [JsonPropertyName(BattleStateJsonNames.CardCostModifiers)]
    public required IReadOnlyList<CardCostModifierJson>? CardCostModifiers { get; init; }

    /// <summary>
    /// The active Pet's applied Burn-damage modifiers, one entry per active source
    /// — the runtime form of the Relic <c>BurnDamage</c> effect
    /// (<c>RELIC_RULES.md</c> §8.2 item 1, §8.5 item 5;
    /// <c>COMBAT_RULES.md</c> §5.2 item 4).
    ///
    /// <b>It is always written, and it is never <c>null</c>.</b> "No Burn-damage
    /// modifier active" is an <b>empty array</b>, on the same always-present
    /// convention the three sibling collections above follow, so it carries
    /// <b>no ignore condition</b>.
    ///
    /// <b>It is nullable in the DTO so a violation is rejectable, not
    /// admissible.</b> As with <see cref="ATKModifiers"/>, the mapping always
    /// writes an array, so the <c>null</c> branch is reachable only from a stored
    /// document that broke the contract.
    ///
    /// <b>Order is preserved rather than sorted</b>, exactly as
    /// <see cref="CardCostModifiers"/> states for its own collection: this
    /// collection's single lifetime leaves no identity-sort contract, so the
    /// writer emits the order the state holds.
    /// </summary>
    [JsonPropertyName(BattleStateJsonNames.BurnDamageModifiers)]
    public required IReadOnlyList<BurnDamageModifierJson>? BurnDamageModifiers { get; init; }
}

/// <summary>
/// One <c>ATKModifier</c> element (<c>GAME_STATE.md</c> §2.3.7;
/// <c>TASK-136</c> D2; <c>TASK-178</c> Q-1 = A).
///
/// <b>Exactly three members, and there is no fourth.</b> §2.3.7 items 4 and 8 forbid a
/// duration, a Turn counter, an <c>ExpiresAt</c>, an <c>ExpiryCondition</c>, a
/// "consumed" flag, a priority, a stack count, an ordering index, a target reference,
/// a remaining-use counter, and a timestamp — no rule reads any of them, so none is
/// written and none may be added without a recorded owner decision.
///
/// <b>All three members are always present.</b> §2.3.8 item 1 states that absence of a
/// <i>member within</i> an element does not arise, §2.3.7 item 3 makes a blank
/// identity unrepresentable, and §2.3.8 item 3 makes <c>lifetime</c> required on
/// <b>every</b> element — "there is no omitted-member and no defaulted-lifetime form,
/// so a reader never infers a lifetime from absence" — so none of them carries an
/// ignore condition.
/// </summary>
internal sealed record ATKModifierJson
{
    /// <summary>
    /// The stable source-scoped identity of the modifier's source
    /// (<c>GAME_STATE.md</c> §2.3.7 item 3) — the replace/refresh and removal key
    /// (§5.1.4). It is copied verbatim: the mapping applies no meaning to it and
    /// derives no identity of its own.
    /// </summary>
    [JsonPropertyName(BattleStateJsonNames.ATKModifierSourceIdentity)]
    public required string SourceIdentity { get; init; }

    /// <summary>
    /// The modifier's ATK change in signed percentage points (§2.3.7 item 5, §2.3.8
    /// item 3), copied verbatim and uninterpreted — no sign normalization, no scaling,
    /// and no rounding. §2.3.7 item 5 states that what the number means is owned by
    /// <c>COMBAT_RULES.md</c> §5.6.6's composition rule and is not interpreted here.
    /// </summary>
    [JsonPropertyName(BattleStateJsonNames.ATKModifierPercentage)]
    public required int ATKModifierPercentage { get; init; }

    /// <summary>
    /// The element's declared lifetime — <c>Battle</c> or <c>NextAttack</c>
    /// (<c>GAME_STATE.md</c> §2.3.7 item 11, §2.3.8 item 3) — written as the member
    /// <b>name</b>, like every other persisted vocabulary token in this mapping
    /// (<c>DATABASE.md</c> §1 item 2), and copied verbatim.
    ///
    /// <b>The reader must not default it.</b> §2.3.8 item 5 makes a round trip that
    /// "alters or drops a <c>lifetime</c> (which would silently turn a
    /// <c>NextAttack</c> modifier into a <c>Battle</c> one, or the reverse)" a defect,
    /// and §2.3.7 item 11 requires a consumer to read the element's own member rather
    /// than infer the lifetime from the collection or the source. The reader therefore
    /// rejects an absent or non-carrier value instead of substituting one.
    /// </summary>
    [JsonPropertyName(BattleStateJsonNames.ATKModifierLifetime)]
    public required string Lifetime { get; init; }
}

/// <summary>
/// One <c>BurnDamageModifier</c> element — the applied form of the Relic
/// <c>BurnDamage</c> effect (<c>RELIC_RULES.md</c> §8.2 item 1, §8.5 item 5;
/// <c>COMBAT_RULES.md</c> §5.2 item 4).
///
/// <b>Exactly two members, and there is no third.</b> The element mirrors the
/// sibling <see cref="CardCostModifierJson"/>: a source-scoped key and one
/// percentage, with the lifetime fixed as <c>Battle</c> by
/// <c>RELIC_RULES.md</c> §8.3's <c>BurnDamage</c> row and therefore not carried —
/// there is nothing to distinguish, exactly as the Card-cost element records. No
/// duration, Turn counter, expiry label, "consumed" flag, stack count, ordering
/// index, or target reference exists: none is read by any rule, and the
/// modifier creates no Burn event and no Burn instance
/// (<c>COMBAT_RULES.md</c> §5.2 item 4).
///
/// <b>Both members are always present.</b> A blank identity is unrepresentable, so
/// neither member carries an ignore condition.
/// </summary>
internal sealed record BurnDamageModifierJson
{
    /// <summary>
    /// The stable source-scoped identity of the modifier's source — the
    /// replace/refresh and removal key. For the provisioned <c>BurnDamage</c>
    /// source it is the equipped Relic instance identity
    /// (<c>RELIC_RULES.md</c> §2.2 item 3), so Burning Curse holds exactly one
    /// element. It is copied verbatim.
    /// </summary>
    [JsonPropertyName(BattleStateJsonNames.BurnDamageModifierSourceIdentity)]
    public required string SourceIdentity { get; init; }

    /// <summary>
    /// The modifier's Burn-damage change in percentage points — Burning Curse's
    /// <c>30</c> (<c>RELIC_RULES.md</c> §8.5 item 5) — copied verbatim and
    /// uninterpreted. Which Burn instances it reaches is
    /// <c>COMBAT_RULES.md</c> §5.2 item 4's ownership rule and is not applied
    /// here; this mapping stores the value and reads it back unchanged.
    /// </summary>
    [JsonPropertyName(BattleStateJsonNames.BurnDamagePercentage)]
    public required int BurnDamagePercentage { get; init; }
}

/// <summary>
/// One <c>CardCostModifier</c> element (<c>GAME_STATE.md</c> §2.3.5;
/// <c>TASK-134</c> D2).
///
/// <b>Exactly two members, and there is no third.</b> §2.3.5 item 5 forbids a
/// <c>Duration</c>, a <c>RemainingTurns</c>, an <c>ExpiresAt</c>, an
/// <c>ExpiryCondition</c>, a <c>StackCount</c>, a Turn counter, an expiry label, a
/// "consumed" flag, a priority, an ordering index, a target reference, a
/// remaining-use counter, and a timestamp — no rule reads any of them. In particular
/// there is <b>no stack count</b>: a repeated application from one source is a
/// replace/refresh (§5.1.3 item 1), never an increment of a counter.
///
/// <b>Both members are always present.</b> §2.3.5 item 6 states that absence of a
/// <i>member within</i> an element does not arise, and item 7 makes both members
/// required and non-nullable with a non-empty <c>SourceIdentity</c> — so neither
/// member carries an ignore condition.
/// </summary>
internal sealed record CardCostModifierJson
{
    /// <summary>
    /// The stable source-scoped identity of the modifier's source
    /// (<c>GAME_STATE.md</c> §2.3.5 item 3) — the replace/refresh and removal key
    /// (§5.1.3). For the provisioned <c>CardCost</c> source it is the Relic's identity
    /// (<c>RELIC_RULES.md</c> §2.2 item 3). It is copied verbatim.
    /// </summary>
    [JsonPropertyName(BattleStateJsonNames.CardCostModifierSourceIdentity)]
    public required string SourceIdentity { get; init; }

    /// <summary>
    /// The modifier's Card-cost reduction in percentage points (§2.3.5 item 4, §2.3.6
    /// item 3), copied verbatim and uninterpreted. §2.3.5 item 4 states that how
    /// several of them compose, and the cap that composition is subject to, are owned
    /// by <c>CARD_RULES.md</c> §3.6 and are not restated or applied here.
    /// </summary>
    [JsonPropertyName(BattleStateJsonNames.CardCostReductionPercentage)]
    public required int CostReductionPercentage { get; init; }
}

/// <summary>
/// One <c>NextAttackCritModifier</c> element (<c>GAME_STATE.md</c> §2.3.4,
/// <c>ADR-017</c>).
///
/// <b>Exactly two members, and there is no third.</b> §2.3.4 item 4 forbids a
/// duration, a Turn counter, an expiry label, a "consumed" flag, a priority, an
/// ordering index, a target reference, a remaining-use counter, and a timestamp —
/// no rule reads any of them, so none is written and none may be added without a
/// recorded owner decision.
///
/// <b>Both members are always present.</b> §2.3.4 item 5 states absence of a
/// <i>member within</i> an element does not arise, and item 2 makes a blank
/// identity unrepresentable — so neither member carries an ignore condition.
/// </summary>
internal sealed record NextAttackCritModifierJson
{
    /// <summary>
    /// The stable source-scoped identity of the modifier's source
    /// (<c>GAME_STATE.md</c> §2.3.4 item 2) — the removal key consumption matches
    /// on (<c>COMBAT_RULES.md</c> §3.3 item 9). It is copied verbatim: the mapping
    /// applies no meaning to it and derives no identity of its own.
    /// </summary>
    [JsonPropertyName(BattleStateJsonNames.NextAttackCritSourceIdentity)]
    public required string SourceIdentity { get; init; }

    /// <summary>
    /// The modifier's Crit increase in percentage points (§2.3.4 item 3), copied
    /// verbatim and uninterpreted.
    /// </summary>
    [JsonPropertyName(BattleStateJsonNames.NextAttackCritContribution)]
    public required int CritContribution { get; init; }
}

/// <summary>
/// <c>PassiveProgress</c> — "current count vs. threshold" (<c>GAME_STATE.md</c>
/// §2.3). Both members are always present; <c>Current = 0</c> is a real
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

    /// <inheritdoc cref="PetStateJson.StatusEffects"/>
    [JsonPropertyName(BattleStateJsonNames.StatusEffects)]
    public required IReadOnlyList<StatusEffectJson>? StatusEffects { get; init; }
}

/// <summary>
/// One StatusEffect instance (<c>GAME_STATE.md</c> §2.3.1) — the element shape
/// <c>PetState.StatusEffects[]</c> and <c>BossState.StatusEffects[]</c> share
/// (§2.3.1's preamble: "identical element shape and identical lifecycle";
/// §2.4.1). There is therefore <b>one</b> DTO, not a pet variant and a boss
/// variant.
///
/// <b>The member set is §2.3.2 item 3's, exactly.</b>
///
/// <code>
/// id              string    required
/// type            string    required    ("DoT" | "BuffDebuff" | "Shield" | "State")
/// source          string    required    ("player" | "boss")
/// magnitude       number    required
/// targetStat      string    optional    (present iff type = "BuffDebuff")
/// remainingTurns  integer   optional    (present iff the instance uses the Turn countdown)
/// expiryCondition string    optional    (present iff it does not)
/// </code>
///
/// <b>Absence is spelled by omission, never by <c>null</c> or a sentinel.</b>
/// §2.3.1 item 7 requires an inapplicable member to be <b>absent</b> — "never
/// <c>null</c>, never a sentinel string" — following the absent-member convention
/// of §2.1.7 item 3 that <see cref="CellJson.SpecialGem"/> and
/// <see cref="PetStateJson.PassiveResetOverride"/> already follow. The three
/// optional members are therefore nullable and are omitted when they do not
/// apply. §2.3.2 item 5 makes materializing an absent optional member as
/// <c>null</c> an explicit <b>defect</b>, which is what these conditions prevent.
///
/// <b>No second counter exists.</b> §2.3.2 item 4 forbids an <c>elapsedTurns</c>,
/// <c>appliedTurn</c>, <c>duration</c>, or <c>refreshedAt</c> member: a second
/// representation of the quantity <c>remainingTurns</c> already carries would be
/// the parallel representation §0 item 5 forbids. Refresh is expressed by
/// assignment on <c>remainingTurns</c> (§5.1.1 item 1), so this DTO has one
/// duration member per model and nothing else.
///
/// <b>Nothing here interprets a value.</b> §2.3.1 items 1–2 make <c>id</c> an
/// identity and <c>magnitude</c> a typed-but-uninterpreted number; the mapping
/// copies both and applies no meaning, unit, sign, or range to either.
/// </summary>
internal sealed record StatusEffectJson
{
    /// <summary>
    /// The Status Effect identity (§2.3.1 items 1 and 6), e.g. <c>"Burn"</c>,
    /// <c>"Root"</c>, <c>"Shield"</c>, <c>"Stun"</c>. It is an identity, not a
    /// definition: the effect's rules stay with <c>COMBAT_RULES.md</c> §5 and
    /// <c>BOSS_RULES.md</c> §6.3.1 and are not written here.
    /// </summary>
    [JsonPropertyName(BattleStateJsonNames.StatusEffectId)]
    public required string Id { get; init; }

    /// <summary>
    /// The effect category (§2.3.1 item 3), written by name rather than ordinal —
    /// <c>DoT</c>, <c>BuffDebuff</c>, <c>Shield</c>, or <c>State</c>. It selects
    /// which of the two duration models the instance uses, which is why the
    /// mapping keys that decision off the members present rather than off a
    /// re-derived reading.
    /// </summary>
    [JsonPropertyName(BattleStateJsonNames.StatusEffectType)]
    public required string Type { get; init; }

    /// <summary>
    /// Which side applied the instance (§2.3.1 item 7) — <c>Player</c> or
    /// <c>Boss</c>, written by name. It is written from the instance's own
    /// <c>Source</c> and is never inferred from the owning entity: a Boss-applied
    /// effect can sit on the Pet and vice versa.
    /// </summary>
    [JsonPropertyName(BattleStateJsonNames.StatusEffectSource)]
    public required string Source { get; init; }

    /// <summary>
    /// The effect's applied magnitude (§2.3.1 item 2), copied verbatim. What the
    /// number means is owned by the effect's rule document and is not interpreted,
    /// rounded, scaled, or converted here — Burn's is flat damage per tick and
    /// Root's is a negative percentage, and both must round-trip unchanged.
    /// </summary>
    [JsonPropertyName(BattleStateJsonNames.StatusEffectMagnitude)]
    public required double Magnitude { get; init; }

    /// <summary>
    /// The modified stat for a <c>BuffDebuff</c> instance, e.g. <c>"ATK"</c>
    /// (§2.3.1 item 7). Present <b>iff</b> <see cref="Type"/> is
    /// <c>BuffDebuff</c>; omitted otherwise, because a stat on a type that
    /// modifies none would be a value no rule reads.
    /// </summary>
    [JsonPropertyName(BattleStateJsonNames.StatusEffectTargetStat)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TargetStat { get; init; }

    /// <summary>
    /// The per-instance duration counter for a Turn-based instance (§2.3.1
    /// items 3–4; <c>COMBAT_RULES.md</c> §5.3 DR1), omitted for a trigger-based
    /// one.
    ///
    /// It is a plain integer and is never fractional or a duration-and-elapsed
    /// pair (§2.3.2 item 4). <b>A stored <c>0</c> is a contract violation, not a
    /// value to normalize:</b> §2.3.1 item 8 makes an instance at zero
    /// unobservable in a committed state, and the Domain factory rejects a
    /// duration below <c>1</c>, so deserialization rejects it too rather than
    /// repairing it into a state the battle never held.
    /// </summary>
    [JsonPropertyName(BattleStateJsonNames.StatusEffectRemainingTurns)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? RemainingTurns { get; init; }

    /// <summary>
    /// The trigger-based expiry label for an instance that does not use the Turn
    /// countdown, e.g. <c>"ShieldDepleted"</c> (§2.3.1 items 3 and 5). Present
    /// iff <see cref="RemainingTurns"/> is absent — the two are mutually
    /// exclusive by item 3, so exactly one is written.
    ///
    /// §2.3.1 item 5 makes it a condition label and not a rule: the mapping copies
    /// which trigger ends the instance and evaluates nothing.
    /// </summary>
    [JsonPropertyName(BattleStateJsonNames.StatusEffectExpiryCondition)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ExpiryCondition { get; init; }
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
