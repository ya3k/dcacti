using GameServer.Domain.Battle;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GameServer.Api.Controllers;

/// <summary>
/// The <c>GET /api/battle/{battleId}/result</c> response body
/// (<c>API_CONTRACTS.md</c> §4).
///
/// <code>
/// {
///   "battleId":     "string",
///   "outcome":      "victory" | "defeat",
///   "rewards":      {},
///   "durationTurns": 0
/// }
/// </code>
///
/// <b>Exactly the four documented members.</b> §4 defines no other member, so
/// none is added: in particular the response carries no Redis key, no
/// <c>Sequence</c>, no <c>RngSeed</c>/<c>RngState</c>, no internal persistence
/// metadata, no Discord access token, no JWT signing key or <c>kid</c>, and no
/// authentication internals. §4's shape is also a <b>representation</b> of the
/// row, not the row itself: <c>DATABASE.md</c> §1's <c>PlayerId</c>,
/// <c>PetInstanceId</c>, <c>BossDefinitionId</c>, and <c>CompletedAt</c> are
/// persistence values the endpoint does not expose, which is why this type is
/// the documented response rather than a projection of the entity.
///
/// <b>The two members §4 documents as always present are always present.</b>
/// <c>rewards</c> is §4 note 1's "always present" member — for both outcomes,
/// never absent or optional — and <c>outcome</c> is one of exactly two values
/// (§4 note 4, owned by <c>GAME_EVENTS.md</c> §2). Neither is nullable and
/// neither is conditionally omitted, so the JSON never carries a member the
/// contract does not define and never drops one it does.
/// </summary>
/// <param name="BattleId">
/// The battle's id — the result row's own key (<c>API_CONTRACTS.md</c> §4 note 2:
/// "<c>battleId</c> is the result row's primary key — <c>BattleResultId</c> is
/// the battle's own <c>BattleId</c>, one row per battle").
/// </param>
/// <param name="Outcome">
/// The battle's outcome — <c>"victory"</c> or <c>"defeat"</c>
/// (<c>API_CONTRACTS.md</c> §4 note 4; value set owned by <c>GAME_EVENTS.md</c>
/// §2). The value is spelled by <c>BattleOutcomes</c>, the same mapping
/// persistence uses, so the stored row and this response can never drift to two
/// vocabularies for one battle.
/// </param>
/// <param name="Rewards">
/// The persisted <c>BattleResult.RewardSummary</c> (<c>API_CONTRACTS.md</c> §4
/// note 1) with no reward line items until TASK-033 owns the member list — the
/// documented staging value is the empty object <c>{}</c>, present for both
/// outcomes. Its members are <b>not</b> restated here because §4 defines none:
/// the value emitted is exactly the JSON document the row holds, so this
/// boundary adds no member, no value, and no default — inventing any of them
/// would be inventing reward semantics (<c>AGENTS.md</c> §7).
/// </param>
/// <param name="DurationTurns">
/// <c>BattleResult.DurationTurns</c> — the battle's Turn count at terminal
/// resolution (<c>API_CONTRACTS.md</c> §4 note 5, <c>DATABASE.md</c> §1
/// "Duration and completion sourcing" item 1).
/// </param>
public record BattleResultResponse(
    [property: JsonPropertyName("battleId")] string BattleId,
    [property: JsonPropertyName("outcome")] string Outcome,
    [property: JsonPropertyName("rewards")] JsonElement Rewards,
    [property: JsonPropertyName("durationTurns")] int DurationTurns)
{
    /// <summary>
    /// Builds the documented response from a stored result — a pure field
    /// mapping that decides nothing.
    ///
    /// The three exposed members are read from the row and the outcome is
    /// spelled through the contract's own mapping; the persisted
    /// <c>PlayerId</c>, <c>PetInstanceId</c>, <c>BossDefinitionId</c>, and
    /// <c>CompletedAt</c> are deliberately not projected, because
    /// <c>API_CONTRACTS.md</c> §4's shape does not include them.
    ///
    /// <b><c>rewards</c> is emitted as the stored JSON document, not as a
    /// string.</b> <c>API_CONTRACTS.md</c> §4 shows <c>"rewards": {}</c> — an
    /// object — and note 1 calls it "always present ... never absent". The row
    /// stores that document as JSON text (<c>DATABASE.md</c> §1), so it is parsed
    /// back into a value here; emitting the raw text would put a quoted string on
    /// the wire where the contract defines an object.
    /// </summary>
    /// <param name="result">The stored result row.</param>
    public static BattleResultResponse From(BattleResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new BattleResultResponse(
            result.BattleResultId,
            BattleOutcomes.ToContractValue(result.Outcome),
            ParseRewards(result.RewardSummary),
            result.DurationTurns);
    }

    /// <summary>
    /// The stored reward summary as the JSON value the contract's shape shows.
    ///
    /// A stored document is emitted exactly as written. Text that is not valid
    /// JSON cannot be emitted as the contract's object shape, so it is refused
    /// rather than passed through as a string — no default, placeholder, or
    /// substituted value stands in for what the row actually holds
    /// (<c>AGENTS.md</c> §7).
    /// </summary>
    internal static JsonElement ParseRewards(string rewardSummary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rewardSummary);

        using var document = JsonDocument.Parse(rewardSummary);

        return document.RootElement.Clone();
    }
}

/// <summary>
/// One <c>GET /api/battle/history</c> element (<c>API_CONTRACTS.md</c> §4.5).
///
/// <code>
/// {
///   "battleId":      "string",
///   "outcome":       "victory" | "defeat",
///   "rewards":       { ...the 8-member RewardSummary... },
///   "durationTurns": 0,
///   "completedAt":   "string"
/// }
/// </code>
///
/// <b>It is the §4 shape plus exactly one member.</b> §4.5 note 2: the four
/// members <c>battleId</c>, <c>outcome</c>, <c>rewards</c>, and
/// <c>durationTurns</c> "carry exactly the same meaning, source, type, and value
/// vocabulary as the <c>GET /api/battle/{battleId}/result</c> 200 response … A
/// history element is a §4 result object plus exactly one further member (note
/// 3)". That is why this type carries the §4 object and adds one member, rather
/// than restating or re-mapping the four: the §4 projection is applied once
/// (<see cref="BattleResultResponse.From"/>) so the two responses cannot drift
/// into two vocabularies for one battle, and §4's own contract is untouched by
/// this addition.
///
/// <b>It exposes no Boss or Pet identifier.</b> §4.5 note 12: neither
/// <c>bossDefinitionId</c>/<c>bossId</c> nor <c>petInstanceId</c>/<c>petId</c> is
/// a member of a history element, "even though <c>BattleResult</c> persists
/// both". The element member set is exactly the five members above and no more —
/// so the persisted <c>PetInstanceId</c>, <c>BossDefinitionId</c>, and
/// <c>PlayerId</c> stay internal, as they do in §4.
/// </summary>
/// <param name="Result">
/// The §4 result members (<c>API_CONTRACTS.md</c> §4.5 note 2) — the same
/// <c>battleId</c>, <c>outcome</c>, <c>rewards</c>, and <c>durationTurns</c> the
/// single-result endpoint returns for that battle.
/// </param>
/// <param name="CompletedAt">
/// <c>BattleResult.CompletedAt</c> — the server clock reading captured on the
/// battle-end path when the durable result was written
/// (<c>API_CONTRACTS.md</c> §4.5 note 3; <c>DATABASE.md</c> §1 "Duration and
/// completion sourcing" item 2). It is the row's own persisted instant: it is
/// never generated or re-read while serving history, never client-sourced, and
/// never modified here. §4.5 note 3 fixes its type and exact serialization as the
/// persisted column's, which <c>DATABASE.md</c> §1 leaves to implementation and
/// which asserts no timezone — so this boundary states no format of its own and
/// lets the application's established <c>System.Text.Json</c> convention
/// serialize the value (<c>DateTimeOffset</c>, round-trippable ISO 8601 with its
/// offset).
/// </param>
public sealed record BattleHistoryItemResponse(
    [property: JsonPropertyName("battleId")] string BattleId,
    [property: JsonPropertyName("outcome")] string Outcome,
    [property: JsonPropertyName("rewards")] JsonElement Rewards,
    [property: JsonPropertyName("durationTurns")] int DurationTurns,
    [property: JsonPropertyName("completedAt")] DateTimeOffset CompletedAt)
{
    /// <summary>
    /// Builds the documented history element from a stored result — a pure field
    /// mapping that decides nothing.
    ///
    /// The four §4 members come from <see cref="BattleResultResponse.From"/>, so
    /// the history element and the single-result response are the same projection
    /// rather than two implementations of §4. The one member this endpoint adds,
    /// <c>completedAt</c>, is read straight from the row
    /// (<c>API_CONTRACTS.md</c> §4.5 note 3).
    /// </summary>
    /// <param name="result">The stored result row.</param>
    public static BattleHistoryItemResponse From(BattleResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var section4 = BattleResultResponse.From(result);

        return new BattleHistoryItemResponse(
            section4.BattleId,
            section4.Outcome,
            section4.Rewards,
            section4.DurationTurns,
            result.CompletedAt);
    }
}
