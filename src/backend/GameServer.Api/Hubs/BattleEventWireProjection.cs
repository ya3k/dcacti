using System.Text.Json.Serialization;
using GameServer.Domain.Battle;
using GameServer.Domain.Combat;
using GameServer.Domain.Match3;
using GameServer.Domain.Passives;
using GameServer.Domain.Relics;

namespace GameServer.Api.Hubs;

/// <summary>
/// The transport projection of one Battle Event — the wire item of
/// <c>ReceiveEvents.events[]</c> (<c>SIGNALR_PROTOCOL.md</c> §3.2).
///
/// It is a <b>flat camelCase record</b> carrying exactly one discriminator
/// member plus that event's own payload members at the same level
/// (<c>§3.2.2</c>). The discriminator is the documented string
/// <c>SwapStarted</c> / <c>SwapResolved</c> / <c>MatchCreated</c> /
/// <c>CascadeCreated</c> / <c>ComboChanged</c> /
/// <c>GemMatched</c> / <c>PassiveCharged</c> / <c>PassiveTriggered</c> /
/// <c>DamageCalculated</c> / <c>DamageDealt</c> / <c>DamageTaken</c> /
/// <c>BossSkillCast</c> / <c>BattleWon</c> / <c>BattleLost</c> / <c>CardCast</c> /
/// <c>PetSkillCast</c> / <c>RelicTriggered</c> / <c>PowerChanged</c> — never
/// the Domain enum's ordinal, which is a Domain identity and not part of the
/// wire contract (<c>§3.2.2</c> item 3). The first two are the committed board
/// action's own boundary pair (<c>§3.2.26–§3.2.27</c>); the next four are the board
/// resolution's events (<c>§3.2.6–§3.2.9</c>); the next two are the Passive
/// stage's (<c>§3.3</c>, the shared Pet/Boss events; <c>§3.2.16–§3.2.17</c>);
/// the three after them are the Damage Pipeline's (<c>§3.2.13–§3.2.15</c>); the
/// next three are the Boss Response's and the outcome's (<c>§3.2.18–§3.2.19</c>);
/// the two after those are the cast stages' (<c>§3.2.20–§3.2.21</c>); and the
/// last two are the Relic stage's and the Power stage's (<c>§3.2.23–§3.2.24</c>).
/// All travel in this same <c>events[]</c> array on the same path (<c>§3.1</c>,
/// <c>§4.3</c> item 10).
///
/// <b>One type, one shape per event.</b> The schema defines a flat object per event, not a
/// shared envelope: a <c>CascadeCreated</c> carries <c>cascadeDepth</c> and
/// nothing else, and a <c>ComboChanged</c> carries <c>combo</c> and nothing
/// else. A cross-product record would therefore have to emit members that do
/// not belong to the event, which <c>§3.2.12</c> item 1 forbids. The permitted
/// member sets are modelled as the one-of slots below and are enforced at
/// construction, so no combination outside the contract can be built.
///
/// <b>A wire name may be shared by two facts when the contract shares it.</b>
/// <c>source</c> is <c>DamageDealt</c>/<c>DamageTaken</c>'s dealing party
/// (<c>§3.2.14</c>) and also <c>PassiveCharged</c>/<c>PassiveTriggered</c>'s
/// <c>"pet"</c>/<c>"boss"</c> discriminator (<c>§3.2.16</c> item 1) — the same
/// spelling and the same string vocabulary, so one slot carries it and each event
/// populates it from its own payload. <c>sourceId</c> is likewise shared by the
/// two Passive events and <c>BossSkillCast</c> (<c>§3.2.16</c> item 2,
/// <c>§3.2.18</c>), where it always means the same thing: the owning entity's
/// identity.
///
/// <b>Omission, never <c>null</c>.</b> A member that is not applicable to an
/// event is <b>omitted entirely</b>: no member of any item is ever sent as JSON
/// <c>null</c> (<c>§3.2.5</c>). Every inapplicable slot below therefore carries
/// <see cref="JsonIgnoreCondition"/><c>.WhenWritingNull</c>, which is what
/// turns the one-of construction into the omission the contract requires. The
/// condition is declared per member rather than set as a serializer-wide null
/// policy, so the rule travels with the contract instead of depending on a
/// global option a future host could change.
///
/// <b>Casing is fixed here too.</b> <c>§3.2.3</c> fixes camelCase explicitly and
/// warns that a serializer default (PascalCase) must not be relied on, so every
/// member below is named with <see cref="JsonPropertyNameAttribute"/> rather
/// than left to the host's naming policy.
/// </summary>
/// <param name="Type">
/// The discriminator (<c>§3.2.2</c>): always present, one of the documented
/// event names tabulated there.
/// </param>
/// <param name="Shape">
/// <c>MatchCreated</c> only (<c>§3.2.6</c>): the Match's shape <b>identity</b>,
/// <c>Straight</c> or <c>Lt</c>.
/// </param>
/// <param name="Cells">
/// <c>MatchCreated</c> only (<c>§3.2.6</c>): the Match's cells, ascending §1.0
/// index, each once.
/// </param>
/// <param name="GemType">
/// <c>MatchCreated</c> and <c>GemMatched</c> (<c>§3.2.6</c>, <c>§3.2.9</c>):
/// the Gem type, spelled per <c>§3.2.4</c>. It is a different member for a
/// different event, which is why the one DTO cannot carry it as a single shared
/// member with one presence rule.
/// </param>
/// <param name="CascadeDepth">
/// <c>CascadeCreated</c> only (<c>§3.2.7</c>): the Cascade's depth index within
/// this Swap. It is <b>not</b> <c>MatchCreated</c>'s pass depth — the two
/// answer the same question with different documented values (<c>§3.2.6</c>
/// item 4, <c>§3.2.7</c> item 2), which is why this DTO keeps them apart rather
/// than sharing one member.
/// </param>
/// <param name="Combo">
/// <c>ComboChanged</c> only (<c>§3.2.8</c>): the <b>new</b> Combo value, as
/// produced by the accounting.
/// </param>
/// <param name="CellIndex">
/// <c>GemMatched</c> only (<c>§3.2.9</c>): the cleared cell's §1.0 index.
/// </param>
/// <param name="PassiveId">
/// <c>PassiveCharged</c> and <c>PassiveTriggered</c> (<c>§3.3</c>): the identity
/// of the Passive that charged or triggered, as the string
/// <c>GAME_STATE.md</c> §2.3's <c>PetState.PassiveId</c> holds it. It is the
/// sibling identity member <c>shape</c>/<c>cascadeDepth</c> are for their own
/// events — one member shared by two events that report the same fact.
/// </param>
/// <param name="Progress">
/// <c>PassiveCharged</c> and <c>PassiveTriggered</c> (<c>§3.3</c>): the progress
/// value the event reports — the value this increment produced
/// (<c>GAME_EVENTS.md</c> §2 item 2), or, on a trigger, the value at the moment
/// the threshold was crossed, before that trigger's own reset. <c>0</c> is a real
/// value where it occurs and is written as <c>0</c>, never omitted
/// (<c>§3.2.5</c>).
/// </param>
/// <param name="Threshold">
/// <c>PassiveCharged</c> and <c>PassiveTriggered</c> (<c>§3.3</c>): the Passive's
/// Threshold, reported alongside so a consumer renders the documented
/// <c>progress / threshold</c> pair without supplying either from elsewhere
/// (<c>GAME_EVENTS.md</c> §2 item 2, <c>PASSIVE_RULES.md</c> §6 item 1).
/// </param>
/// <param name="Base">
/// <c>DamageCalculated</c> only (<c>§3.2.13</c>): Step 1 — Base Damage.
/// </param>
/// <param name="ComboModifier">
/// <c>DamageCalculated</c> only (<c>§3.2.13</c>): Step 2 — Combo Modifier factor.
/// </param>
/// <param name="ElementModifier">
/// <c>DamageCalculated</c> only (<c>§3.2.13</c>): Step 3 — Element Modifier factor.
/// </param>
/// <param name="OtherModifiers">
/// <c>DamageCalculated</c> only (<c>§3.2.13</c>): Step 4 — Other Modifiers factor.
/// </param>
/// <param name="Defense">
/// <c>DamageCalculated</c> only (<c>§3.2.13</c>): Step 5 — damage after Defense Mitigation.
/// </param>
/// <param name="FinalDamage">
/// <c>DamageCalculated</c> only (<c>§3.2.13</c>): Step 6 — Final Damage (truncated, never negative).
/// </param>
/// <param name="Source">
/// <c>DamageDealt</c> and <c>DamageTaken</c> (<c>§3.2.14</c>, <c>§3.2.15</c>): the
/// party that dealt the damage.
///
/// <b>It is also the shared event's <c>source</c> member.</b>
/// <c>PassiveCharged</c> and <c>PassiveTriggered</c> (<c>§3.2.16</c> item 1,
/// <c>§3.2.17</c>) carry a member of the same wire name and the same string
/// vocabulary — <c>"pet"</c> or <c>"boss"</c> — for a different fact: which
/// entity's Passive it is (<c>BOSS_RULES.md</c> §7). The contract fixes one wire
/// member name per fact, and here the two facts share a name and a value set, so
/// they share this one slot: an item carries <c>source</c> and only one of the
/// events that has such a member produces it.
/// </param>
/// <param name="Target">
/// <c>DamageDealt</c> and <c>DamageTaken</c> (<c>§3.2.14</c>, <c>§3.2.15</c>): the party that received the damage.
/// </param>
/// <param name="Amount">
/// <c>DamageDealt</c> and <c>DamageTaken</c> (<c>§3.2.14</c>, <c>§3.2.15</c>): the Final Damage amount.
/// </param>
/// <param name="SourceId">
/// <c>PassiveCharged</c>, <c>PassiveTriggered</c>, and <c>BossSkillCast</c>
/// (<c>§3.2.16</c> item 2, <c>§3.2.17</c>, <c>§3.2.18</c>): the identity of the
/// owning entity — <c>PetState.PetId</c> or <c>BossState.BossId</c>. It is the
/// same fact — "which entity owns this event" — and the same wire name on all
/// three events, so it is one member, and it is always present on each of them.
/// For a Boss it is the canonical technical Identity <c>BossState.BossId</c>
/// (e.g. <c>"boss-hoa-long"</c>), never a display name
/// (<c>BOSS_RULES.md</c> §6.4). The damage events have no such member, so no
/// <c>DamageDealt</c>/<c>DamageTaken</c> item writes it.
/// </param>
/// <param name="SkillId">
/// <c>BossSkillCast</c> only (<c>§3.2.18</c>): the Boss Skill's identity
/// (<c>BOSS_RULES.md</c> §4, §6.4).
/// </param>
/// <param name="Outcome">
/// <c>BattleWon</c> and <c>BattleLost</c> only (<c>§3.2.19</c>): <c>"victory"</c>
/// or <c>"defeat"</c>. It is a string and not a boolean (<c>§3.2.19</c> item 1),
/// and it is the constant of whichever of the two events this is rather than a
/// value recomputed from the HP pair.
/// </param>
/// <param name="FinalBossHp">
/// <c>BattleWon</c> and <c>BattleLost</c> only (<c>§3.2.19</c>): the Boss's HP at
/// battle end (<c>GAME_STATE.md</c> §2.4).
/// </param>
/// <param name="FinalPlayerHp">
/// <c>BattleWon</c> and <c>BattleLost</c> only (<c>§3.2.19</c>): the active Pet's HP
/// at battle end (<c>GAME_STATE.md</c> §2.3, <c>ADR-011</c> items 3 and 5) — the
/// Pet is the Player side's combat character, and this member keeps the fixed
/// protocol label. It is <b>not</b> omitted when it is <c>0</c>: a defeat's
/// terminal player-side HP is a real value the client renders, and <c>§3.2.5</c>
/// omits only members that do not belong to the event.
/// </param>
public sealed record BattleEventWireDto(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("shape")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Shape = null,
    [property: JsonPropertyName("cells")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<int>? Cells = null,
    [property: JsonPropertyName("gemType")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? GemType = null,
    [property: JsonPropertyName("cascadeDepth")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? CascadeDepth = null,
    [property: JsonPropertyName("combo")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Combo = null,
    [property: JsonPropertyName("cellIndex")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? CellIndex = null,
    [property: JsonPropertyName("passiveId")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? PassiveId = null,
    [property: JsonPropertyName("progress")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Progress = null,
    [property: JsonPropertyName("threshold")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Threshold = null,
    [property: JsonPropertyName("base")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Base = null,
    [property: JsonPropertyName("comboModifier")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? ComboModifier = null,
    [property: JsonPropertyName("elementModifier")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? ElementModifier = null,
    [property: JsonPropertyName("otherModifiers")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? OtherModifiers = null,
    [property: JsonPropertyName("defense")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? Defense = null,
    [property: JsonPropertyName("finalDamage")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? FinalDamage = null,
    [property: JsonPropertyName("source")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Source = null,
    [property: JsonPropertyName("target")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Target = null,
    [property: JsonPropertyName("amount")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Amount = null,
    [property: JsonPropertyName("sourceId")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? SourceId = null,
    [property: JsonPropertyName("skillId")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? SkillId = null,
    [property: JsonPropertyName("outcome")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Outcome = null,
    [property: JsonPropertyName("finalBossHp")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? FinalBossHp = null,
    [property: JsonPropertyName("finalPlayerHp")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? FinalPlayerHp = null,
    [property: JsonPropertyName("cardId")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? CardId = null,
    [property: JsonPropertyName("relicId")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? RelicId = null,
    [property: JsonPropertyName("delta")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Delta = null,
    [property: JsonPropertyName("power")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Power = null,
    [property: JsonPropertyName("actor")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Actor = null,
    [property: JsonPropertyName("fromCell")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? FromCell = null,
    [property: JsonPropertyName("toCell")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? ToCell = null,
    [property: JsonPropertyName("passDepth")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? PassDepth = null,
    [property: JsonPropertyName("dropMoves")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<DropMoveWireDto>? DropMoves = null,
    [property: JsonPropertyName("spawnedGems")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<SpawnedGemWireDto>? SpawnedGems = null)
{
    /// <summary>
    /// The wire item for one <c>SwapStarted</c>
    /// (<c>SIGNALR_PROTOCOL.md</c> §3.2.26) — the acting side and the two §1.0
    /// cells the accepted action named.
    /// </summary>
    /// <param name="actor">
    /// <c>"player"</c> or <c>"boss"</c>, produced by
    /// <see cref="BattleEventWireProjection"/>, which is the one place the
    /// Domain <c>DamageParty</c> is projected to its lowercase contract name
    /// (<c>§3.2.4</c>, the rule <c>PowerChanged.source</c> already follows).
    /// </param>
    /// <param name="fromCell">The first cell the accepted action named (§1.0 index).</param>
    /// <param name="toCell">The second cell the accepted action named (§1.0 index).</param>
    public static BattleEventWireDto SwapStarted(string actor, int fromCell, int toCell) =>
        new(
            Type: "SwapStarted",
            Actor: actor,
            FromCell: fromCell,
            ToCell: toCell);

    /// <summary>
    /// The wire item for one <c>SwapResolved</c>
    /// (<c>SIGNALR_PROTOCOL.md</c> §3.2.27) — the delivered outcome and nothing
    /// else.
    /// </summary>
    /// <param name="outcome">
    /// <c>"committed"</c> or <c>"reverted"</c>, produced by
    /// <see cref="BattleEventWireProjection"/>. It reuses the <c>outcome</c> member
    /// name <c>BattleWon</c>/<c>BattleLost</c> already own (<c>§3.2.19</c> item 1,
    /// <c>§3.2.27</c> item 2) with this event's own two values.
    /// </param>
    public static BattleEventWireDto SwapResolved(string outcome) =>
        new(Type: "SwapResolved", Outcome: outcome);

    /// <summary>
    /// The wire item for one <c>MatchCreated</c>
    /// (<c>SIGNALR_PROTOCOL.md</c> §3.2.6).
    /// </summary>
    /// <param name="shape">
    /// The shape identity — produced by <see cref="BattleEventWireProjection"/>,
    /// which is the one place the <c>Straight</c>/<c>Lt</c> decision is made.
    /// </param>
    /// <param name="cells">The shape's cells, ascending §1.0 index, each once.</param>
    /// <param name="gemType">The Match's Gem type, as a §3.2.4 contract name.</param>
    /// <param name="cascadeDepth">The detection pass's own depth.</param>
    public static BattleEventWireDto MatchCreated(
        string shape,
        IReadOnlyList<int> cells,
        string gemType,
        int cascadeDepth) =>
        new(
            Type: "MatchCreated",
            Shape: shape,
            Cells: cells,
            GemType: gemType,
            CascadeDepth: cascadeDepth);

    /// <summary>
    /// The wire item for one <c>CascadeCreated</c>
    /// (<c>SIGNALR_PROTOCOL.md</c> §3.2.7) — the depth index and nothing else.
    /// </summary>
    public static BattleEventWireDto CascadeCreated(int cascadeDepth) =>
        new(Type: "CascadeCreated", CascadeDepth: cascadeDepth);

    /// <summary>
    /// The wire item for one <c>ComboChanged</c>
    /// (<c>SIGNALR_PROTOCOL.md</c> §3.2.8) — the new Combo value and nothing
    /// else.
    /// </summary>
    public static BattleEventWireDto ComboChanged(int combo) =>
        new(Type: "ComboChanged", Combo: combo);

    /// <summary>
    /// The wire item for one <c>GemMatched</c>
    /// (<c>SIGNALR_PROTOCOL.md</c> §3.2.9).
    /// </summary>
    /// <param name="cellIndex">The cleared cell's §1.0 index.</param>
    /// <param name="gemType">The cleared cell's Gem type, as a §3.2.4 name.</param>
    public static BattleEventWireDto GemMatched(
        int cellIndex,
        string gemType) =>
        new(
            Type: "GemMatched",
            GemType: gemType,
            CellIndex: cellIndex);

    /// <summary>
    /// The wire item for one <c>PassiveCharged</c> (<c>SIGNALR_PROTOCOL.md</c>
    /// §3.3).
    /// </summary>
    /// <param name="passiveId">
    /// The Passive's identity, carried as the string <c>PetState.PassiveId</c>
    /// holds it (<c>GAME_STATE.md</c> §2.3) — reported, never re-derived
    /// (<c>GAME_EVENTS.md</c> §2 item 1).
    /// </param>
    /// <param name="progress">
    /// The progress value this increment produced (<c>GAME_EVENTS.md</c> §2
    /// item 2). It is a plain integer: <c>0</c> is a value where it occurs and is
    /// written as <c>0</c>, not omitted.
    /// </param>
    /// <param name="threshold">The Passive's Threshold (§2 item 2).</param>
    /// <param name="source">
    /// <c>"pet"</c> or <c>"boss"</c> — which entity's Passive charged
    /// (<c>§3.2.16</c> item 1, <c>BOSS_RULES.md</c> §7). Always present.
    /// </param>
    /// <param name="sourceId">
    /// The identity of the owning entity (<c>§3.2.16</c> item 2) — the
    /// canonical technical Identity <c>BossState.BossId</c> for a Boss
    /// (<c>boss-&lt;ascii-kebab-case-name&gt;</c>, e.g. <c>"boss-hoa-long"</c>;
    /// never a display name — <c>BOSS_RULES.md</c> §6.4).
    /// </param>
    public static BattleEventWireDto PassiveCharged(
        string passiveId,
        int progress,
        int threshold,
        string source,
        string? sourceId) =>
        new(
            Type: "PassiveCharged",
            PassiveId: passiveId,
            Progress: progress,
            Threshold: threshold,
            Source: source,
            SourceId: sourceId);

    /// <summary>
    /// The wire item for one <c>PassiveTriggered</c> (<c>SIGNALR_PROTOCOL.md</c>
    /// §3.3).
    ///
    /// It carries no <c>effect summary</c>: <c>GAME_EVENTS.md</c> §2 item 3 records
    /// that member as <b>deferred</b> to the Combat stage, so omitting it here is
    /// the documented sequencing position and not an omission of a required member.
    /// A consumer must not read its absence as "no effect occurred".
    /// </summary>
    /// <param name="passiveId">The Passive's identity (§2 item 1).</param>
    /// <param name="progress">
    /// The progress at the moment the Threshold was crossed — <b>before</b> that
    /// trigger's own reset (<c>GAME_EVENTS.md</c> §2 item 2).
    /// </param>
    /// <param name="threshold">The Passive's Threshold (§2 item 2).</param>
    /// <param name="source">
    /// <c>"pet"</c> or <c>"boss"</c> — which entity's Passive triggered
    /// (<c>§3.2.17</c>, <c>BOSS_RULES.md</c> §7). Always present.
    /// </param>
    /// <param name="sourceId">
    /// The identity of the owning entity (<c>§3.2.17</c>) — the canonical
    /// technical Identity <c>BossState.BossId</c> for a Boss (e.g.
    /// <c>"boss-hoa-long"</c>; never a display name — <c>BOSS_RULES.md</c>
    /// §6.4).
    /// </param>
    public static BattleEventWireDto PassiveTriggered(
        string passiveId,
        int progress,
        int threshold,
        string source,
        string? sourceId) =>
        new(
            Type: "PassiveTriggered",
            PassiveId: passiveId,
            Progress: progress,
            Threshold: threshold,
            Source: source,
            SourceId: sourceId);

    /// <summary>
    /// The wire item for one <c>BossSkillCast</c>
    /// (<c>SIGNALR_PROTOCOL.md</c> §3.2.18) — the Skill's identity and the Boss's
    /// identity, and nothing else.
    /// </summary>
    /// <param name="skillId">
    /// The Boss Skill's identity (<c>BOSS_RULES.md</c> §4, §6.4) — the value the
    /// definition carries, reported rather than re-derived (§3.2.18 item 1).
    /// </param>
    /// <param name="sourceId">
    /// The Boss's identity — the canonical technical Identity
    /// <c>BossState.BossId</c> (e.g. <c>"boss-hoa-long"</c>), never a display
    /// name (<c>BOSS_RULES.md</c> §6.4, §3.2.18 item 2).
    /// </param>
    public static BattleEventWireDto BossSkillCast(string skillId, string sourceId) =>
        new(
            Type: "BossSkillCast",
            SkillId: skillId,
            SourceId: sourceId);

    /// <summary>
    /// The wire item for one <c>BattleWon</c> (<c>SIGNALR_PROTOCOL.md</c> §3.2.19).
    /// </summary>
    /// <param name="finalBossHp">
    /// The Boss's HP at battle end (<c>GAME_STATE.md</c> §2.4) — <c>0</c> here.
    /// </param>
    /// <param name="finalPlayerHp">
    /// The active Pet's HP at battle end (<c>GAME_STATE.md</c> §2.3,
    /// <c>ADR-011</c> items 3 and 5), carried under this fixed protocol label.
    /// </param>
    public static BattleEventWireDto BattleWon(int finalBossHp, int finalPlayerHp) =>
        new(
            Type: "BattleWon",
            // §3.2.19 item 1: the outcome is the string constant of this event's own
            // type. It is not read from the HP pair, which cannot distinguish an
            // outcome on its own.
            Outcome: "victory",
            FinalBossHp: finalBossHp,
            FinalPlayerHp: finalPlayerHp);

    /// <summary>
    /// The wire item for one <c>BattleLost</c> (<c>SIGNALR_PROTOCOL.md</c> §3.2.19).
    /// </summary>
    /// <param name="finalBossHp">
    /// The Boss's HP at battle end (<c>GAME_STATE.md</c> §2.4).
    /// </param>
    /// <param name="finalPlayerHp">
    /// The active Pet's HP at battle end (<c>GAME_STATE.md</c> §2.3,
    /// <c>ADR-011</c> items 3 and 5) — <c>0</c> here, carried under this fixed
    /// protocol label and written as <c>0</c> rather than omitted.
    /// </param>
    public static BattleEventWireDto BattleLost(int finalBossHp, int finalPlayerHp) =>
        new(
            Type: "BattleLost",
            Outcome: "defeat",
            FinalBossHp: finalBossHp,
            FinalPlayerHp: finalPlayerHp);

    /// <summary>
    /// The wire item for one <c>DamageCalculated</c>
    /// (<c>SIGNALR_PROTOCOL.md</c> §3.2.13) — the full pipeline breakdown.
    /// </summary>
    /// <param name="baseDamage">Step 1 — Base Damage.</param>
    /// <param name="comboModifier">Step 2 — Combo Modifier factor.</param>
    /// <param name="elementModifier">Step 3 — Element Modifier factor.</param>
    /// <param name="otherModifiers">Step 4 — Other Modifiers factor.</param>
    /// <param name="defense">Step 5 — damage after Defense Mitigation.</param>
    /// <param name="finalDamage">Step 6 — Final Damage (truncated, never negative).</param>
    public static BattleEventWireDto DamageCalculated(
        int baseDamage,
        double comboModifier,
        double elementModifier,
        double otherModifiers,
        double defense,
        int finalDamage) =>
        new(
            Type: "DamageCalculated",
            Base: baseDamage,
            ComboModifier: comboModifier,
            ElementModifier: elementModifier,
            OtherModifiers: otherModifiers,
            Defense: defense,
            FinalDamage: finalDamage);

    /// <summary>
    /// The wire item for one <c>DamageDealt</c>
    /// (<c>SIGNALR_PROTOCOL.md</c> §3.2.14) — source, target, amount.
    /// </summary>
    /// <param name="source">The party that dealt the damage.</param>
    /// <param name="target">The party that received the damage.</param>
    /// <param name="amount">The Final Damage applied.</param>
    public static BattleEventWireDto DamageDealt(
        string source,
        string target,
        int amount) =>
        new(
            Type: "DamageDealt",
            Source: source,
            Target: target,
            Amount: amount);

    /// <summary>
    /// The wire item for one <c>DamageTaken</c>
    /// (<c>SIGNALR_PROTOCOL.md</c> §3.2.15) — source, target, amount.
    /// </summary>
    /// <param name="source">The party that dealt the damage.</param>
    /// <param name="target">The party that received the damage.</param>
    /// <param name="amount">The Final Damage taken.</param>
    public static BattleEventWireDto DamageTaken(
        string source,
        string target,
        int amount) =>
        new(
            Type: "DamageTaken",
            Source: source,
            Target: target,
            Amount: amount);

    /// <summary>
    /// The wire item for one <c>CardCast</c> (<c>SIGNALR_PROTOCOL.md</c> §3.2.20).
    /// </summary>
    /// <param name="cardId">The cast Card's definition identity.</param>
    public static BattleEventWireDto CardCast(string cardId) =>
        new(
            Type: "CardCast",
            CardId: cardId);

    /// <summary>
    /// The wire item for one <c>PetSkillCast</c> (<c>SIGNALR_PROTOCOL.md</c> §3.2.21).
    /// </summary>
    /// <param name="cardId">The cast Signature Skill Card's definition identity.</param>
    public static BattleEventWireDto PetSkillCast(string cardId) =>
        new(
            Type: "PetSkillCast",
            CardId: cardId);

    /// <summary>
    /// The wire item for one <c>RelicTriggered</c>
    /// (<c>SIGNALR_PROTOCOL.md</c> §3.2.23) — the triggered Relic's identity and
    /// nothing else.
    /// </summary>
    /// <param name="relicId">
    /// The triggered Relic's <b>owned instance</b> identity (<c>RELIC_RULES.md</c>
    /// §2.2 item 3) — the same value <c>PetState.EquippedRelics[]</c> holds, read
    /// and reported rather than re-derived (§3.2.23 item 1). It is never a
    /// <c>RelicDefinitionId</c>.
    /// </param>
    public static BattleEventWireDto RelicTriggered(string relicId) =>
        new(
            Type: "RelicTriggered",
            RelicId: relicId);

    /// <summary>
    /// The wire item for one <c>PowerChanged</c>
    /// (<c>SIGNALR_PROTOCOL.md</c> §3.2.24) — the signed change, the resulting
    /// Power, and what changed it.
    /// </summary>
    /// <param name="delta">
    /// The signed change applied to <c>PetState.Power</c> (§3.2.24 item 1).
    /// <c>0</c> is a real value where it occurs and is written as <c>0</c>, never
    /// omitted.
    /// </param>
    /// <param name="power">
    /// <c>PetState.Power</c> after the change (§3.2.24 item 2). <c>0</c> is a real
    /// value here too and is written as <c>0</c>.
    /// </param>
    /// <param name="source">
    /// <c>"match"</c>, <c>"card"</c>, <c>"relic"</c>, or <c>"boss"</c> (§3.2.24
    /// item 3) — which stage owns the mutation, projected from the payload's own
    /// value set. It never states the direction of the change.
    /// </param>
    public static BattleEventWireDto PowerChanged(
        int delta,
        int power,
        string source) =>
        new(
            Type: "PowerChanged",
            Delta: delta,
            Power: power,
            // §3.2.24 item 3 / §3.2.7 item 2: `source` is a shared wire member name
            // carrying each event's own value set, so the shared slot carries this
            // event's projected value.
            Source: source);

    /// <summary>
    /// The wire item for one <c>CascadeStepResolved</c>
    /// (<c>SIGNALR_PROTOCOL.md</c> §3.2.28) — one detection pass's completed
    /// Gravity and Spawn transition: its own 1-based depth, the survivors it
    /// moved, and the Gems it spawned.
    ///
    /// <b>No board member and no actor member.</b> The board travels in the state
    /// push and the recovery snapshot alone (§3.1 item 3, §3.2.12 item 1), and the
    /// acting side is the sibling <c>SwapStarted</c>'s member (§3.2.26), which the
    /// client already carries forward — a second spelling of either would be a
    /// second source of truth for a fact the contract already delivers.
    /// </summary>
    /// <param name="passDepth">
    /// The pass's own 1-based detection depth (§3.2.28 item 2): identical in value
    /// to <c>MatchCreated.cascadeDepth</c> on the same pass. It is never written
    /// under the name <c>cascadeDepth</c>, which two existing members already
    /// carry with two different documented values (§3.2.7 item 2).
    /// </param>
    /// <param name="dropMoves">
    /// The survivors the pass's Gravity relocated, or <c>null</c> to omit the
    /// member entirely when the pass moved none. An empty array is never written:
    /// it is normalized to the omission the contract requires (§3.2.5 item 1).
    /// </param>
    /// <param name="spawnedGems">
    /// The Gems the pass's Spawn placed, or <c>null</c> to omit the member entirely
    /// when the pass spawned none (§3.2.5 item 1, §3.2.28 item 2).
    /// </param>
    public static BattleEventWireDto CascadeStepResolved(
        int passDepth,
        IReadOnlyList<DropMoveWireDto>? dropMoves,
        IReadOnlyList<SpawnedGemWireDto>? spawnedGems) =>
        new(
            Type: "CascadeStepResolved",
            PassDepth: passDepth,
            DropMoves: dropMoves is { Count: > 0 } ? dropMoves : null,
            SpawnedGems: spawnedGems is { Count: > 0 } ? spawnedGems : null);
}

/// <summary>
/// One <c>dropMoves</c> entry (<c>SIGNALR_PROTOCOL.md</c> §3.2.28).
///
/// Both members are always present: an entry exists only because a survivor moved,
/// so neither cell is ever omitted, and both are §1.0 indices in the same column.
/// It carries no Gem identity, order index, or distance — the cells are the whole
/// fact, and the movement's direction is implied by which one is the source.
/// </summary>
/// <param name="FromCell">The §1.0 cell the survivor occupied before Gravity.</param>
/// <param name="ToCell">The §1.0 cell it occupies after Gravity; never equal to <see cref="FromCell"/>.</param>
public sealed record DropMoveWireDto(
    [property: JsonPropertyName("fromCell")] int FromCell,
    [property: JsonPropertyName("toCell")] int ToCell);

/// <summary>
/// One <c>spawnedGems</c> entry (<c>SIGNALR_PROTOCOL.md</c> §3.2.28).
///
/// Both members are always present. It carries exactly the destination cell and
/// the drawn Gem type: Spawn places an ordinary Gem only
/// (<c>MATCH3_RULES.md</c> §4.5 item 7), so no further member belongs here
/// (§3.2.28 item 4).
/// </summary>
/// <param name="CellIndex">The destination §1.0 cell the Gem was placed in.</param>
/// <param name="GemType">The drawn Gem type, as a §3.2.4 contract name.</param>
public sealed record SpawnedGemWireDto(
    [property: JsonPropertyName("cellIndex")] int CellIndex,
    [property: JsonPropertyName("gemType")] string GemType);

/// <summary>
/// Projects Domain <c>BattleEvent</c> values onto the <c>ReceiveEvents.events[]</c>
/// wire schema (<c>SIGNALR_PROTOCOL.md</c> §3.2 — the single authoritative owner
/// of that schema).
///
/// <code>
/// BattleEvent (Domain)                        GameServer.Domain.Match3
///         ↓  this projection                  GameServer.Api — pure field mapping
/// BattleEventWireDto
///         ↓  ReceiveEventsPayload             §3 envelope
///         ↓  SignalR JSON
/// Frontend                                    opaque (ARCHITECTURE.md §2.2.1 rule 4)
/// </code>
///
/// <b>Why the projection exists.</b> <c>BattleEvent</c> is a Domain value, not
/// a wire DTO, and must never become one (<c>§3.2.1</c> item 1). Its
/// non-applicable payload accessors — <c>Match</c>, <c>CascadeDepth</c>,
/// <c>Combo</c>, <c>Gem</c>, <c>PassiveCharged</c>, <c>PassiveTriggered</c> —
/// throw rather than return a default, so a serializer that reflects over its
/// public members reads all of them and aborts the send. Direct serialization of
/// it is therefore invalid <b>by construction</b>, not merely discouraged: the
/// order here is "read <see cref="BattleEvent.Type"/> first, then only that
/// event's own payload" (<c>§3.2.1</c> item 2), which never touches an accessor
/// that does not apply. The Domain type is unchanged by this: it stays a Domain
/// type, and the transport layer owns serialization.
///
/// <b>Pure field mapping — the projection rules.</b>
/// <list type="bullet">
/// <item><b>One-to-one and order-preserving.</b> N Domain events produce N wire
/// events, in the same order. Nothing is filtered, reordered, merged, grouped,
/// or invented (<c>§3.2.1</c> item 3, <c>§3.2.12</c> item 4) — the array
/// <b>is</b> the executor's own list.</item>
/// <item><b>Side-effect free.</b> It is a pure function of its input: it reads
/// no <c>BattleState</c>, no board, and no RNG, and it writes none of them. It
/// runs no detection pass and consults no second resolution, so it cannot change
/// gameplay, event order, or any authoritative value
/// (<c>GAME_EVENTS.md</c> §3 item 6).</item>
/// <item><b>Derives no gameplay value.</b> <c>cascadeDepth</c> is read from the
/// payload that carries it; <c>combo</c> is read as the already-accounted value
/// and is never recomputed (<c>§3.2.8</c> item 1).</item>
/// <item><b>Exposes no Domain geometry.</b> <c>MatchShape</c> is not serialized
/// mechanically: its arms, arm lengths, <c>StartIndex</c>, <c>IntersectionIndex</c>,
/// and <c>Step</c> stay Domain-side (<c>§3.2.6</c> item 3). Every fact
/// <c>GAME_EVENTS.md</c> §2 requires is carried by <c>shape</c> + <c>cells</c> +
/// <c>gemType</c>.</item>
/// </list>
/// </summary>
public static class BattleEventWireProjection
{
    /// <summary>
    /// Projects the resolution's ordered Battle Events onto the §3.2 wire
    /// schema, one-to-one and in order.
    /// </summary>
    /// <param name="events">
    /// The resolution's own event list — <c>SwapExecutionResult.Events</c>,
    /// already in the <c>GAME_RULES.md</c> §17 / <c>GAME_EVENTS.md</c> §1.1 order.
    /// </param>
    /// <returns>
    /// The same number of wire items, in the same order, each carrying exactly
    /// the members §3.2 gives its event.
    /// </returns>
    public static IReadOnlyList<BattleEventWireDto> Project(IReadOnlyList<BattleEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        var wire = new BattleEventWireDto[events.Count];

        for (var i = 0; i < events.Count; i++)
        {
            // Position-for-position: the wire array is the executor's list, so no
            // sort, filter, merge, or re-group can occur here (§3.2.1 item 3).
            wire[i] = Project(events[i]);
        }

        return wire;
    }

    /// <summary>
    /// Projects one Battle Event onto its §3.2 wire item.
    ///
    /// The discriminator is read first, and only the matching event's own
    /// payload is then read, so no non-applicable accessor is ever touched
    /// (<c>§3.2.1</c> item 2).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The event's <see cref="BattleEventType"/> is not one of the documented
    /// events tabulated by <c>§3.2.2</c>. The set is closed (<c>§3.2.2</c>
    /// item 2), so an unknown kind is a defect rather than a further event — it
    /// is never silently dropped, because dropping one would break the
    /// one-to-one projection the batch's atomicity depends on.
    /// </exception>
    private static BattleEventWireDto Project(BattleEvent battleEvent) =>
        battleEvent.Type switch
        {
            BattleEventType.SwapStarted => BattleEventWireDto.SwapStarted(
                // §3.2.26 item 2 / §3.2.4: the two-sided identity projected to its
                // lowercase contract name, never the enum's ordinal or a
                // PascalCase variant. The values are the ones the damage events'
                // `source`/`target` and the Passive events' `source` already use.
                actor: battleEvent.SwapStarted.Actor.ToString().ToLowerInvariant(),
                // §3.2.26 items 3–4: the action's own two cells, reported as named.
                fromCell: battleEvent.SwapStarted.FromCell,
                toCell: battleEvent.SwapStarted.ToCell),
            BattleEventType.SwapResolved => ProjectSwapResolved(battleEvent.SwapResolved),
            BattleEventType.MatchCreated => ProjectMatch(battleEvent.Match),
            BattleEventType.CascadeCreated => BattleEventWireDto.CascadeCreated(battleEvent.CascadeDepth),
            BattleEventType.ComboChanged => BattleEventWireDto.ComboChanged(battleEvent.Combo),
            BattleEventType.GemMatched => ProjectGem(battleEvent.Gem),
            BattleEventType.PassiveCharged => ProjectPassiveCharged(battleEvent.PassiveCharged),
            BattleEventType.PassiveTriggered => ProjectPassiveTriggered(battleEvent.PassiveTriggered),
            BattleEventType.DamageCalculated => ProjectDamageCalculated(battleEvent.DamageCalculated),
            BattleEventType.DamageDealt => ProjectDamageDealt(battleEvent.DamageDealt),
            BattleEventType.DamageTaken => ProjectDamageTaken(battleEvent.DamageTaken),
            BattleEventType.BossSkillCast => ProjectBossSkillCast(battleEvent.BossSkillCast),
            BattleEventType.BattleWon => ProjectBattleWon(battleEvent.BattleWon),
            BattleEventType.BattleLost => ProjectBattleLost(battleEvent.BattleLost),
            BattleEventType.CardCast => ProjectCardCast(battleEvent.CardCast),
            BattleEventType.PetSkillCast => ProjectPetSkillCast(battleEvent.PetSkillCast),
            BattleEventType.RelicTriggered => ProjectRelicTriggered(battleEvent.RelicTriggered),
            BattleEventType.PowerChanged => ProjectPowerChanged(battleEvent.PowerChanged),
            BattleEventType.CascadeStepResolved => ProjectCascadeStepResolved(battleEvent.CascadeStep),
            _ => throw new ArgumentOutOfRangeException(
                nameof(battleEvent),
                battleEvent.Type,
                "Not one of the documented Battle Event types (SIGNALR_PROTOCOL.md §3.2.2, §3.2.13–§3.2.24, §3.2.26–§3.2.28, §3.3)."),
        };

    /// <summary>
    /// Projects the <c>SwapResolved</c> payload (<c>SIGNALR_PROTOCOL.md</c>
    /// §3.2.27) — the delivered outcome, as the contract word for the committed
    /// fact the payload carries.
    ///
    /// The Domain value is the fact ("was the exchange committed") and the wire
    /// member is the name §2 uses for it (<c>"committed"</c> / <c>"reverted"</c>,
    /// §3.2.27 item 2). This is the same value-name projection
    /// <see cref="ProjectMatch"/> performs for the shape identity, and it derives
    /// no gameplay value. On the delivered path the outcome is always
    /// <c>"committed"</c>: a rejected action emits no event at all
    /// (<c>§3.1</c> item 4, <c>GAME_EVENTS.md</c> §1.2), so the emitting stage
    /// only ever hands in the committed fact.
    /// </summary>
    private static BattleEventWireDto ProjectSwapResolved(SwapResolvedEvent resolved) =>
        BattleEventWireDto.SwapResolved(outcome: resolved.Committed ? "committed" : "reverted");

    /// <summary>
    /// Projects the <c>RelicTriggered</c> payload (<c>SIGNALR_PROTOCOL.md</c>
    /// §3.2.23) — the triggered Relic's owned instance identity, and nothing else.
    ///
    /// No <c>effect summary</c> is projected: §3.2.23 item 2 omits it under
    /// §3.2.25, and <c>RELIC_RULES.md</c> §7 fixes the shape as
    /// <c>{ type, relicId }</c>. No order index is projected either — §3.2.23
    /// item 3 reads the deterministic order from the batch position, which the
    /// one-to-one order-preserving projection preserves.
    /// </summary>
    private static BattleEventWireDto ProjectRelicTriggered(RelicTriggeredEvent triggered) =>
        BattleEventWireDto.RelicTriggered(triggered.RelicId);

    /// <summary>
    /// Projects the <c>PowerChanged</c> payload (<c>SIGNALR_PROTOCOL.md</c>
    /// §3.2.24) — the signed delta, the resulting value, and the projected source
    /// name.
    ///
    /// The source is projected to its documented lowercase contract name by the
    /// same value-projection rule §3.2.14 item 1 applies to <c>DamageParty</c>, so
    /// the Domain ordinal never reaches the wire (§3.2.24 item 3, §3.2.4).
    /// </summary>
    private static BattleEventWireDto ProjectPowerChanged(PowerChangedEvent changed) =>
        BattleEventWireDto.PowerChanged(
            delta: changed.Delta,
            power: changed.Power,
            source: changed.Source.ToString().ToLowerInvariant());

    /// <summary>
    /// Projects the <c>PetSkillCast</c> payload (<c>SIGNALR_PROTOCOL.md</c> §3.2.21).
    /// </summary>
    private static BattleEventWireDto ProjectPetSkillCast(PetSkillCastEvent petSkillCast) =>
        BattleEventWireDto.PetSkillCast(petSkillCast.CardId);

    /// <summary>
    /// Projects the <c>CardCast</c> payload (<c>SIGNALR_PROTOCOL.md</c> §3.2.20).
    /// </summary>
    private static BattleEventWireDto ProjectCardCast(CardCastEvent cardCast) =>
        BattleEventWireDto.CardCast(cardCast.CardId);

    /// <summary>
    /// Projects the <c>PassiveCharged</c> payload (<c>SIGNALR_PROTOCOL.md</c> §3.3,
    /// §3.2.16).
    /// </summary>
    private static BattleEventWireDto ProjectPassiveCharged(PassiveChargedEvent charged) =>
        BattleEventWireDto.PassiveCharged(
            // §2 item 1: the identity the tracker read off PetState or BossState —
            // reported, not re-derived, re-numbered, or invented here.
            passiveId: charged.PassiveId.Value,

            // §2 item 2: the progress this increment produced, passed through as the
            // tracker's own value. It is never recomputed from the Threshold or from
            // another charge.
            progress: charged.Progress,

            // §2 item 2: the Passive's Threshold, reported alongside so the client
            // renders `progress / threshold` without recomputing either.
            threshold: charged.Threshold,

            // §3.2.16 item 1: "pet" or "boss" — the shared event's discriminator
            // between the two Passive systems (BOSS_RULES.md §7). Carried through
            // from the payload, which the emitting stage set.
            source: charged.Source,

            // §3.2.16 item 2: the owning entity's identity — the canonical
            // technical Identity BossState.BossId for a Boss, never a display
            // name (BOSS_RULES.md §6.4). Omitted when the emitter had
            // none (the Pet Passive stage, whose PetState carries no PetId yet).
            sourceId: charged.SourceId);

    /// <summary>
    /// Projects the <c>PassiveTriggered</c> payload (<c>SIGNALR_PROTOCOL.md</c> §3.3,
    /// §3.2.17).
    /// </summary>
    private static BattleEventWireDto ProjectPassiveTriggered(PassiveTriggeredEvent triggered) =>
        BattleEventWireDto.PassiveTriggered(
            // §2 item 1: the same identity member the charge reports.
            passiveId: triggered.PassiveId.Value,

            // §2 item 2: the progress at the crossing — before that trigger's own
            // reset — which is the tracker's own reported value.
            progress: triggered.Progress,

            // §2 item 2: the Passive's Threshold.
            threshold: triggered.Threshold,

            // §3.2.17: the same two members the charge carries, with the same
            // semantics.
            source: triggered.Source,
            sourceId: triggered.SourceId);

    /// <summary>
    /// Projects the <c>BossSkillCast</c> payload (<c>SIGNALR_PROTOCOL.md</c>
    /// §3.2.18) — the Skill's identity and the Boss's identity.
    /// </summary>
    private static BattleEventWireDto ProjectBossSkillCast(BossSkillCastEvent skill) =>
        BattleEventWireDto.BossSkillCast(
            // §3.2.18 item 1 / §3.2.18 item 2: both members are identities the
            // resolution read off the Boss's definition and state. Neither is
            // re-derived here, and no effect detail is added — §3.2.18 item 3 leaves
            // the Skill's damage to the damage events that follow it in this batch.
            skillId: skill.SkillId,
            sourceId: skill.SourceId);

    /// <summary>
    /// Projects the <c>BattleWon</c> payload (<c>SIGNALR_PROTOCOL.md</c> §3.2.19).
    /// </summary>
    private static BattleEventWireDto ProjectBattleWon(BattleWonEvent won) =>
        BattleEventWireDto.BattleWon(
            // §3.2.19 item 2: the terminal HP pair, read from the resolution's own
            // final state. `outcome` is fixed by the event type, not computed here.
            // The reward summary is deferred (§3.2.19 item 3) and is not a member.
            finalBossHp: won.FinalBossHp,
            finalPlayerHp: won.FinalPlayerHp);

    /// <summary>
    /// Projects the <c>BattleLost</c> payload (<c>SIGNALR_PROTOCOL.md</c> §3.2.19).
    /// </summary>
    private static BattleEventWireDto ProjectBattleLost(BattleLostEvent lost) =>
        BattleEventWireDto.BattleLost(
            finalBossHp: lost.FinalBossHp,
            finalPlayerHp: lost.FinalPlayerHp);

    /// <summary>
    /// Projects the <c>DamageCalculated</c> payload (<c>SIGNALR_PROTOCOL.md</c>
    /// §3.2.13) — the full pipeline breakdown.
    /// </summary>
    private static BattleEventWireDto ProjectDamageCalculated(DamageCalculation calc) =>
        BattleEventWireDto.DamageCalculated(
            baseDamage: calc.Base,
            comboModifier: calc.ComboModifier,
            elementModifier: calc.ElementModifier,
            otherModifiers: calc.OtherModifiers,
            defense: calc.Defense,
            finalDamage: calc.FinalDamage);

    /// <summary>
    /// Projects the <c>DamageDealt</c> payload (<c>SIGNALR_PROTOCOL.md</c>
    /// §3.2.14) — source, target, amount.
    /// </summary>
    private static BattleEventWireDto ProjectDamageDealt(DamageDealtEvent dealt) =>
        BattleEventWireDto.DamageDealt(
            source: dealt.Source.ToString().ToLowerInvariant(),
            target: dealt.Target.ToString().ToLowerInvariant(),
            amount: dealt.Amount);

    /// <summary>
    /// Projects the <c>DamageTaken</c> payload (<c>SIGNALR_PROTOCOL.md</c>
    /// §3.2.15) — source, target, amount.
    /// </summary>
    private static BattleEventWireDto ProjectDamageTaken(DamageTakenEvent taken) =>
        BattleEventWireDto.DamageTaken(
            source: taken.Source.ToString().ToLowerInvariant(),
            target: taken.Target.ToString().ToLowerInvariant(),
            amount: taken.Amount);

    /// <summary>
    /// Projects the <c>MatchCreated</c> payload (<c>§3.2.6</c>).
    /// </summary>
    private static BattleEventWireDto ProjectMatch(MatchResolution match) =>
        BattleEventWireDto.MatchCreated(
            // §3.2.6 item 1: the shape IDENTITY — the two values the game rules
            // own. MatchShape's arms and geometry are Domain detail and are not
            // projected (item 3); the shape's own Cells list is already the union
            // of its arms, each cell once, ascending §1.0 index (item 2).
            shape: match.Shape.IsLt ? "Lt" : "Straight",

            // §3.2.6 item 2: the shape's cells, in the order MatchShape already
            // holds them — stated explicitly rather than implied by position.
            cells: match.Shape.Cells,

            // §3.2.6 item 5: the Match's own Gem type, which both arms share.
            gemType: GemTypes.ToContractName(match.Shape.GemType),

            // §3.2.6 item 4: the DETECTION PASS's own depth — the same depth the
            // Domain MatchResolution carries, and deliberately not the
            // CascadeCreated depth of §3.2.7.
            cascadeDepth: match.CascadeDepth);

    /// <summary>
    /// Projects the <c>GemMatched</c> payload (<c>§3.2.9</c>).
    /// </summary>
    private static BattleEventWireDto ProjectGem(GemMatchedEvent gem) =>
        BattleEventWireDto.GemMatched(
            // §3.2.9 item 1: the §1.0 cell index, stated explicitly.
            cellIndex: gem.CellIndex,

            // §3.2.9 item 2: the cell's Gem type, read from the pre-removal board —
            // the state in which the cell still held the Gem that was consumed
            // (GAME_EVENTS.md §2 item 2).
            gemType: GemTypes.ToContractName(gem.GemType));

    /// <summary>
    /// Projects the <c>CascadeStepResolved</c> payload (<c>§3.2.28</c>) — the
    /// pass's own depth, the survivors its Gravity moved, and the Gems its Spawn
    /// placed.
    ///
    /// <b>It reads the step's own results and re-derives nothing.</b> The two
    /// arrays are mapped in the order the steps produced them — Gravity's read
    /// order and Spawn's fill order (<c>MATCH3_RULES.md</c> §4.4 item 1,
    /// §4.5 item 2) — because that order is part of the contract
    /// (<c>§3.2.28</c> item 3) and the projection sorts nothing
    /// (<c>§3.2.1</c> item 3). Gem types are projected to their §3.2.4 contract
    /// names by the same helper the Match and Gem payloads use, so the value a
    /// consumer reads for a spawned Gem is the same vocabulary every other
    /// <c>gemType</c> member uses.
    /// </summary>
    private static BattleEventWireDto ProjectCascadeStepResolved(CascadeStepResolvedEvent step)
    {
        var dropMoves = new DropMoveWireDto[step.DropMoves.Count];

        for (var i = 0; i < step.DropMoves.Count; i++)
        {
            // §3.2.28 item 3: the two §1.0 cells, as Gravity reported them. The
            // projection checks no adjacency, column, or direction — the fact is
            // the step's, and a consumer is never asked to validate gameplay.
            dropMoves[i] = new DropMoveWireDto(
                step.DropMoves[i].FromCell,
                step.DropMoves[i].ToCell);
        }

        var spawnedGems = new SpawnedGemWireDto[step.SpawnedGems.Count];

        for (var i = 0; i < step.SpawnedGems.Count; i++)
        {
            // §3.2.28 item 2: the destination cell and the drawn type, and nothing
            // else — a spawned Gem is always an ordinary Gem (§4.5 item 7).
            spawnedGems[i] = new SpawnedGemWireDto(
                step.SpawnedGems[i].CellIndex,
                GemTypes.ToContractName(step.SpawnedGems[i].GemType));
        }

        // §3.2.5 item 1: an empty array is omitted rather than written as `[]`, so
        // the factory normalizes both cases.
        return BattleEventWireDto.CascadeStepResolved(
            passDepth: step.PassDepth,
            dropMoves: dropMoves,
            spawnedGems: spawnedGems);
    }
}