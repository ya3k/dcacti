using System.Text.Json.Serialization;
using GameServer.Domain.Combat;
using GameServer.Domain.Match3;
using GameServer.Domain.Passives;

namespace GameServer.Api.Hubs;

/// <summary>
/// The transport projection of one Battle Event — the wire item of
/// <c>ReceiveEvents.events[]</c> (<c>SIGNALR_PROTOCOL.md</c> §3.2).
///
/// It is a <b>flat camelCase record</b> carrying exactly one discriminator
/// member plus that event's own payload members at the same level
/// (<c>§3.2.2</c>). The discriminator is the documented string
/// <c>MatchCreated</c> / <c>CascadeCreated</c> / <c>ComboChanged</c> /
/// <c>GemMatched</c> / <c>PassiveCharged</c> / <c>PassiveTriggered</c> /
/// <c>DamageCalculated</c> / <c>DamageDealt</c> / <c>DamageTaken</c> /
/// <c>BossSkillCast</c> / <c>BattleWon</c> / <c>BattleLost</c> — never
/// the Domain enum's ordinal, which is a Domain identity and not part of the
/// wire contract (<c>§3.2.2</c> item 3). The first four are the board
/// resolution's events (<c>§3.2.6–§3.2.9</c>); the next two are the Passive
/// stage's (<c>§3.3</c>, the shared Pet/Boss events; <c>§3.2.16–§3.2.17</c>);
/// the three after them are the Damage Pipeline's (<c>§3.2.13–§3.2.15</c>); and
/// the last three are the Boss Response's and the outcome's
/// (<c>§3.2.18–§3.2.19</c>). All travel in this same <c>events[]</c> array
/// on the same path (<c>§3.1</c>, <c>§4.3</c> item 10).
///
/// <b>One type, twelve shapes.</b> The schema defines a flat object per event, not a
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
/// The discriminator (<c>§3.2.2</c>): always present, one of the four
/// documented event names.
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
/// <param name="CreatedSpecialGems">
/// <c>MatchCreated</c> only (<c>§3.2.6</c>): the Special Gems the Match
/// created, in the §5.5.1 order. Omitted when empty; a present member is never
/// an empty array.
/// </param>
/// <param name="CellIndex">
/// <c>GemMatched</c> only (<c>§3.2.9</c>): the cleared cell's §1.0 index.
/// </param>
/// <param name="SpecialGem">
/// <c>GemMatched</c> only (<c>§3.2.9</c>): the Special Gem consumed at that
/// cell, present exactly when the cleared cell held one. It is the transport
/// representation of <c>§3.2.10</c>, never a <c>SpecialGemClaim</c>.
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
    [property: JsonPropertyName("createdSpecialGems")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<CreatedSpecialGemWireDto>? CreatedSpecialGems = null,
    [property: JsonPropertyName("cellIndex")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? CellIndex = null,
    [property: JsonPropertyName("specialGem")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] SpecialGemWireDto? SpecialGem = null,
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
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? FinalPlayerHp = null)
{
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
    /// <param name="createdSpecialGems">
    /// The committed creations, or <c>null</c> to omit the member entirely when
    /// the Match created none (§3.2.6 item 6). An empty array is never written:
    /// it is normalized to the omission the contract requires.
    /// </param>
    public static BattleEventWireDto MatchCreated(
        string shape,
        IReadOnlyList<int> cells,
        string gemType,
        int cascadeDepth,
        IReadOnlyList<CreatedSpecialGemWireDto>? createdSpecialGems) =>
        new(
            Type: "MatchCreated",
            Shape: shape,
            Cells: cells,
            GemType: gemType,
            CascadeDepth: cascadeDepth,
            // §3.2.6 item 6: the member is omitted when the array would be empty,
            // so a non-empty list is the only thing that can reach the wire.
            CreatedSpecialGems: createdSpecialGems is { Count: > 0 } ? createdSpecialGems : null);

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
    /// <param name="specialGem">
    /// The consumed Special Gem, or <c>null</c> for an ordinary Gem — omitted
    /// entirely, which is the only representation of "none was consumed"
    /// (§3.2.5, §3.2.9 item 4).
    /// </param>
    public static BattleEventWireDto GemMatched(
        int cellIndex,
        string gemType,
        SpecialGemWireDto? specialGem) =>
        new(
            Type: "GemMatched",
            GemType: gemType,
            CellIndex: cellIndex,
            SpecialGem: specialGem);

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
}

/// <summary>
/// One <c>createdSpecialGems</c> entry (<c>SIGNALR_PROTOCOL.md</c> §3.2.11).
///
/// <c>cellIndex</c> is always present here — unlike in <c>GemMatched</c>, where
/// it is a sibling of the event, a creation entry is not itself an event and
/// must state its cell (<c>§3.2.11</c> item 1) — and <c>specialGem</c> is
/// always present, because a creation entry exists only because a Special Gem
/// was created (<c>§3.2.11</c> item 2).
/// </summary>
public sealed record CreatedSpecialGemWireDto(
    [property: JsonPropertyName("cellIndex")] int CellIndex,
    [property: JsonPropertyName("specialGem")] SpecialGemWireDto SpecialGem);

/// <summary>
/// The transport representation of a created or consumed Special Gem
/// (<c>SIGNALR_PROTOCOL.md</c> §3.2.10).
///
/// <code>
/// specialGem
/// ├── type            "LineClear" | "Burst" | "Area"     always present
/// └── orientation     "Horizontal" | "Vertical"          present iff LineClear
/// </code>
///
/// It carries no <c>cellIndex</c> and no <c>GemType</c>: the sibling
/// <c>cellIndex</c> (or the event's own) already states the cell and the
/// event's own <c>gemType</c> already states the type, so a nested copy would
/// be a second spelling of one fact (<c>§3.2.10</c> items 3–4). It carries no
/// identity, order, depth, or age either (<c>item 5</c>) — a Special Gem's
/// behaviour is a pure function of its type, its orientation, and its cell.
///
/// <b>This is not <c>SpecialGemClaim</c>.</b> That type is Transient Resolution
/// State and is never serialized (<c>§3.2.10</c> item 1, <c>GAME_STATE.md</c>
/// §3): its <c>ShapeIndex</c>, <c>IntraShapeOrder</c>, and <c>Source</c> are the
/// collision keys its own contract states are never serialized. Only the two
/// facts any consumer needs are projected.
/// </summary>
/// <param name="Type">
/// <c>LineClear</c>, <c>Burst</c>, or <c>Area</c> (<c>§3.2.4</c>).
/// </param>
/// <param name="Orientation">
/// <c>Horizontal</c> or <c>Vertical</c>, present <b>if and only if</b>
/// <see cref="Type"/> is <c>LineClear</c> (<c>§3.2.10</c> item 6). A
/// <c>Burst</c> or <c>Area</c> entry omits it, because an orientation on those
/// types is a value no rule reads.
/// </param>
public sealed record SpecialGemWireDto(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("orientation")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Orientation = null);

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
/// <c>gemType</c> + <c>createdSpecialGems</c>.</item>
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
    /// The event's <see cref="BattleEventType"/> is not one of the four
    /// documented events. The set is closed (<c>§3.2.2</c> item 2), so an
    /// unknown kind is a defect rather than a fifth event — it is never silently
    /// dropped, because dropping one would break the one-to-one projection the
    /// batch's atomicity depends on.
    /// </exception>
    private static BattleEventWireDto Project(BattleEvent battleEvent) =>
        battleEvent.Type switch
        {
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
            _ => throw new ArgumentOutOfRangeException(
                nameof(battleEvent),
                battleEvent.Type,
                "Not one of the documented Battle Event types (SIGNALR_PROTOCOL.md §3.2.2, §3.2.13–§3.2.19, §3.3)."),
        };

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
            cascadeDepth: match.CascadeDepth,

            // §3.2.6 items 6–7 / §3.2.11: the committed creations, in the §5.5.1
            // order the resolution recorded. A collision-discarded claim is absent
            // from that list already, and an empty list is omitted entirely.
            createdSpecialGems: ProjectCreatedSpecialGems(match.CreatedSpecialGems));

    /// <summary>
    /// Projects the <c>GemMatched</c> payload (<c>§3.2.9</c>).
    /// </summary>
    private static BattleEventWireDto ProjectGem(GemMatchedEvent gem) =>
        BattleEventWireDto.GemMatched(
            // §3.2.9 item 1: the §1.0 cell index, stated explicitly.
            cellIndex: gem.CellIndex,

            // §3.2.9 item 2: always the cell's Gem type, including for a cell that
            // held a Special Gem — a Special Gem adds metadata to an occupant and
            // does not replace its type.
            gemType: GemTypes.ToContractName(gem.GemType),

            // §3.2.9 item 3: read from the pre-removal board, so a consumed Special
            // Gem is reported for the cell that held it. Absent for an ordinary Gem
            // (item 4), which omits the member rather than writing null.
            specialGem: ProjectSpecialGem(gem.ConsumedSpecialGem));

    /// <summary>
    /// Projects the created Special Gems of a Match onto §3.2.11 entries.
    ///
    /// The claims are mapped in the order the resolution committed them — the
    /// §5.5.1 creation order, which is the order the Domain list already holds.
    /// Nothing is re-sorted and no claim is dropped here; a claim discarded by
    /// collision resolution was never in the list
    /// (<c>MATCH3_RULES.md</c> §5.5.4 item 3).
    /// </summary>
    private static IReadOnlyList<CreatedSpecialGemWireDto>? ProjectCreatedSpecialGems(
        IReadOnlyList<SpecialGemClaim> claims)
    {
        if (claims.Count == 0)
        {
            // §3.2.6 item 6: omitted when the array would be empty, so the caller
            // never has to write an empty array.
            return null;
        }

        var entries = new CreatedSpecialGemWireDto[claims.Count];

        for (var i = 0; i < claims.Count; i++)
        {
            entries[i] = new CreatedSpecialGemWireDto(
                // §3.2.11 item 1: the cell the Special Gem was created at. Only the
                // claim's CellIndex and its SpecialGem are projected — the claim's
                // ShapeIndex, IntraShapeOrder, Source, and GemType are collision
                // bookkeeping that is never serialized (§3.2.10 items 1, 4).
                claims[i].CellIndex,
                ToWire(claims[i].SpecialGem));
        }

        return entries;
    }

    /// <summary>
    /// Projects a cell's optional Special Gem for the <c>GemMatched</c> payload,
    /// or <c>null</c> to omit the member for an ordinary Gem
    /// (<c>§3.2.9</c> item 4).
    /// </summary>
    private static SpecialGemWireDto? ProjectSpecialGem(SpecialGem? specialGem) =>
        specialGem is { } gem ? ToWire(gem) : null;

    /// <summary>
    /// Projects one <c>SpecialGem</c> onto the §3.2.10 transport representation.
    ///
    /// A pure field mapping: the type and, for a Line Clear Gem only, the
    /// orientation. The enum's documented name is used rather than its ordinal
    /// (<c>§3.2.4</c> item 2), and nothing else is added.
    /// </summary>
    private static SpecialGemWireDto ToWire(SpecialGem specialGem) =>
        // §3.2.10 item 3: no cellIndex — the sibling or the entry's own member
        // already states the cell. Item 4: no GemType — the event's own gemType
        // already states the type. Item 5: no identity, order, depth, or age.
        new(specialGem.Type.ToString(), specialGem.Orientation?.ToString());
}