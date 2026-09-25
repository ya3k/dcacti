using System.Text.Json;
using GameServer.Domain.Bosses;
using GameServer.Domain.Cards;
using GameServer.Domain.Elements;
using GameServer.Domain.Match3;
using GameServer.Domain.Passives;
using GameServer.Domain.Relics;

namespace GameServer.Domain.Battle.Serialization;

/// <summary>
/// The runtime JSON serialization mapping for the authoritative
/// <c>BattleState</c> (<c>GAME_STATE.md</c> §2; <c>REDIS_STATE.md</c> §2).
///
/// <code>
/// BattleState
///     ↓  Serialize
/// JSON
///     ↓  Deserialize
/// BattleState
/// </code>
///
/// <b>What it is.</b> A lossless, deterministic mapping between the authoritative
/// runtime state and one JSON document. <c>GAME_STATE.md</c> §2.1.7 item 5 states
/// the obligation it satisfies: "Serializing a <c>BattleState</c> and
/// deserializing it must return a <c>BattleState</c> whose <c>Cells[64]</c> are
/// identical … and whose <c>Turn</c>, <c>Sequence</c>, <c>RngSeed</c>, and
/// <c>RngState</c> are unchanged", and <c>REDIS_STATE.md</c> §7 items 9–11 extend
/// that to the counters, the commit record, and the loadout snapshots.
///
/// <b>What it is not.</b> It is not a Redis client, a Redis key, a TTL, a lock, or
/// a concurrency mechanism. <c>REDIS_STATE.md</c> §1–§4 own those, and §7's
/// deferral remains in force: this type opens no connection, names no key, and
/// writes nothing. It is also not a wire projection — <c>SIGNALR_PROTOCOL.md</c>
/// owns what the client receives, and that is a different, protocol-fixed subset.
/// It is not an HTTP concern and is not reachable from any endpoint.
///
/// <b>It is a pure mapping.</b> Every method is a total, side-effect-free function
/// of its input:
/// <list type="bullet">
/// <item>it computes no gameplay value — no damage, HP, Power, Match, Combo,
/// Passive progress, or Boss state is calculated, clamped, or derived
/// (<c>GAME_RULES.md</c> §18, <c>ADR-001</c>),</item>
/// <item>it draws no randomness and never re-seeds: it writes the stored
/// <c>RngSeed</c>/<c>RngState</c> pair and reads it back unchanged, so a recovered
/// battle resumes the stream exactly (<c>GAME_STATE.md</c> §2.6.2 item 4),</item>
/// <item>it advances no counter: <c>Turn</c> and <c>Sequence</c> are written and
/// restored as state, never incremented or recomputed (<c>REDIS_STATE.md</c> §7
/// item 9),</item>
/// <item>it generates, repairs, or normalizes no board and no cell
/// (<c>GAME_STATE.md</c> §2.7.2 item 4),</item>
/// <item>it re-reads no loadout from Player inventory: the snapshots in
/// <c>PetState</c> are self-contained and are restored from the record itself
/// (<c>RELIC_RULES.md</c> §2.5, <c>CARD_RULES.md</c> §1).</item>
/// </list>
///
/// <b>Where it lives, and why.</b> <c>BattleState</c> is Domain runtime state, and
/// this mapping is a direct expression of the shape <c>GAME_STATE.md</c> §2 owns,
/// so it sits beside the type it maps. It adds no dependency to Domain:
/// <c>System.Text.Json</c> is part of the .NET base class library and is not one
/// of the concerns <c>ARCHITECTURE.md</c> §2.1 item 1 excludes from Domain
/// (ASP.NET Core, Redis, PostgreSQL, SignalR). No Redis, HTTP, EF Core, or
/// transport type is referenced anywhere in this file.
///
/// <b>Deserialization validates rather than trusts.</b> The Domain constructors it
/// rebuilds through enforce their own documented invariants — 64 cells
/// (<c>MATCH3_RULES.md</c> §1.0), one of the four Gem types (§1.1), orientation
/// present exactly for a Line Clear Gem (<c>GAME_STATE.md</c> §2.1.4 item 2), and
/// the canonical <c>MinCellIndex &lt; MaxCellIndex</c> commit record (§2.1.10
/// item 2). A malformed record is therefore rejected as the contract violation it
/// is, instead of being silently repaired into a state the battle never had.
/// </summary>
public static class BattleStateSerializer
{
    /// <summary>
    /// The serializer's options. Every member name is declared on the DTOs
    /// themselves (<see cref="BattleStateJsonNames"/>), so no naming policy is set
    /// here and none can rename a persisted member
    /// (<c>SIGNALR_PROTOCOL.md</c> §3.2.3's warning against relying on a default).
    ///
    /// Indentation is off: the document is a stored runtime record, not a
    /// human-facing artifact, and writing it compactly keeps the record's size a
    /// function of the state alone.
    /// </summary>
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = false,
    };

    /// <summary>
    /// Serializes the authoritative state to its runtime JSON representation
    /// (<c>REDIS_STATE.md</c> §2 item 1).
    ///
    /// The projection is one-to-one and order-preserving: the board's cells are
    /// written in ascending index order and the loadout arrays in the order the
    /// state holds them. Nothing is sorted, filtered, deduplicated, defaulted, or
    /// derived.
    /// </summary>
    /// <param name="state">The authoritative state to map.</param>
    /// <returns>The JSON document, matching <c>GAME_STATE.md</c> §2 exactly.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="state"/> is null.</exception>
    public static string Serialize(BattleState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return JsonSerializer.Serialize(ToJson(state), Options);
    }

    /// <summary>
    /// Deserializes a runtime JSON record back into an authoritative
    /// <c>BattleState</c>.
    ///
    /// The restored state is a value the rest of the system cannot distinguish
    /// from the one that was written: no field is defaulted, re-derived, or
    /// recomputed, and the RNG pair is restored as stored so the stream resumes
    /// exactly where it stopped (<c>GAME_STATE.md</c> §2.6.2 item 4,
    /// <c>ADR-008</c>).
    /// </summary>
    /// <param name="json">A document produced by <see cref="Serialize"/>.</param>
    /// <returns>The restored authoritative state.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="json"/> is null.</exception>
    /// <exception cref="JsonException">
    /// The document is not valid JSON, or a required member is missing. A missing
    /// member is an error rather than a default: silently substituting one would
    /// invent state the record never held.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The document is well-formed JSON but violates a documented state invariant
    /// — a board without exactly 64 valid cells, a malformed Special Gem, an
    /// unknown Gem type, Element, or Special Gem name, or a commit record that is
    /// not canonically ordered. Those are contract violations, so they are rejected
    /// rather than repaired.
    /// </exception>
    public static BattleState Deserialize(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        var dto = JsonSerializer.Deserialize<BattleStateJson>(json, Options)
            ?? throw new JsonException("The record is null; a BattleState record is required.");

        return FromJson(dto);
    }

    /// <summary>
    /// Projects the authoritative state onto the JSON document shape — the
    /// <c>BattleState → JSON</c> half of the mapping.
    ///
    /// Every statement below is a field copy. The comments name the contract each
    /// member comes from so a later member-set change is traceable to the document
    /// that owns it (<c>GAME_STATE.md</c> §2).
    /// </summary>
    private static BattleStateJson ToJson(BattleState state) =>
        new()
        {
            // §2 root, present from the start (§2.0).
            BattleId = state.BattleId,
            Turn = state.Turn,
            Sequence = state.Sequence,

            // §2.6: the seed and the state pair are written as they are held. The
            // pair stays nested as one logical field (§2.6.2 item 1), and neither
            // component is re-derived from the other.
            RngSeed = state.RngSeed,
            RngState = new RngStateJson
            {
                State = state.RngState.State,
                Increment = state.RngState.Increment,
            },

            // §2.1: BoardState is Cells[64] and nothing else. The array is written
            // in the state's own ascending index order (§2.1.1 item 6) — the array
            // position IS the cell index, so no index member is written.
            BoardState = new BoardStateJson
            {
                Cells = state.BoardState.Cells.Select(ToCellJson).ToArray(),
            },

            // §2.2: root Match/Combo accounting. Written even at 0 — zero is a real
            // publishable value, not an absence (§2.2.1 item 1).
            Combo = state.Combo,
            MatchCount = state.MatchCount,

            PetState = ToPetStateJson(state.PetState),
            BossState = ToBossStateJson(state.BossState),

            // §2.1.10: null (no Swap committed) leaves the member absent; a present
            // pair is written in its stored canonical order.
            LastCommittedSwapPair = state.LastCommittedSwapPair is { } pair
                ? new CommittedSwapPairJson
                {
                    MinCellIndex = pair.MinCellIndex,
                    MaxCellIndex = pair.MaxCellIndex,
                }
                : null,
        };

    /// <summary>
    /// Projects one cell entry (<c>GAME_STATE.md</c> §2.1.1) — a pure field copy.
    /// </summary>
    private static CellJson ToCellJson(Cell cell) =>
        new()
        {
            // §2.1.3 item 1: the Gem type is always written, including for a cell
            // holding a Special Gem. The documented contract name is used
            // (MATCH3_RULES.md §1.1), not the enum ordinal.
            GemType = GemTypes.ToContractName(cell.GemType),

            // §2.1.7 items 3–4: an absent Special Gem leaves the member absent; a
            // present one writes its type, and its orientation only when the type
            // is LineClear.
            SpecialGem = cell.SpecialGem is { } gem
                ? new SpecialGemJson
                {
                    Type = gem.Type.ToString(),
                    Orientation = gem.Orientation?.ToString(),
                }
                : null,
        };

    /// <summary>
    /// Projects <c>PetState</c> (<c>GAME_STATE.md</c> §2.3) — a pure field copy.
    ///
    /// The loadout arrays are carried across element for element, in the order
    /// held. Nothing is sorted, filtered, or deduplicated: the Relic order is the
    /// equip-slot order (<c>RELIC_RULES.md</c> §2.3) and a repeated Card id is the
    /// documented same-definition repetition, not a defect (§2.3).
    /// </summary>
    private static PetStateJson ToPetStateJson(PetState petState) =>
        new()
        {
            HP = petState.HP,
            MaxHP = petState.MaxHP,
            ATK = petState.ATK,
            DEF = petState.DEF,
            Crit = petState.Crit,
            Power = petState.Power,

            // Enum-valued members are written by name, so the record does not
            // depend on the enum's member ordering.
            Element = petState.Element.ToString(),
            PassiveId = petState.PassiveId.Value,
            PassiveProgress = ToPassiveProgressJson(petState.PassiveProgress),

            // §2.3: null means "the default reset behavior", and absence is how the
            // contract spells it. Nothing is written in its place.
            PassiveResetOverride = petState.PassiveResetOverride?.ToString(),

            EquippedRelics = petState.EquippedRelics?.Select(relic => relic.Value).ToArray(),
            EquippedCards = petState.EquippedCards?.Select(card => card.Value).ToArray(),
        };

    /// <summary>
    /// Projects <c>BossState</c> (<c>GAME_STATE.md</c> §2.4) — a pure field copy.
    ///
    /// No value is recalculated: the HP is the HP the state holds, not a value
    /// derived from damage, and <c>State</c> is carried as the Boss's own recorded
    /// enum without evaluating any Boss condition (§2.4.3–§2.4.5).
    /// </summary>
    private static BossStateJson ToBossStateJson(BossState bossState) =>
        new()
        {
            BossId = bossState.BossId.Value,
            Element = bossState.Element.ToString(),
            HP = bossState.HP,
            MaxHP = bossState.MaxHP,
            ATK = bossState.ATK,
            DEF = bossState.DEF,
            State = bossState.State.ToString(),
            PassiveId = bossState.PassiveId.Value,
            PassiveProgress = ToPassiveProgressJson(bossState.PassiveProgress),
            SkillCharge = bossState.SkillCharge,
            SkillCooldown = bossState.SkillCooldown,
        };

    /// <summary>
    /// Projects a <c>(Threshold, Current)</c> progress pair (<c>GAME_STATE.md</c>
    /// §2.3, §2.4) — a pure field copy.
    /// </summary>
    private static PassiveProgressJson ToPassiveProgressJson(PassiveProgress progress) =>
        new()
        {
            Threshold = progress.Threshold,
            Current = progress.Current,
        };

    /// <summary>
    /// Rebuilds the authoritative state from the JSON document shape — the
    /// <c>JSON → BattleState</c> half of the mapping.
    ///
    /// The state is reconstructed through <c>BattleState</c>'s own constructor
    /// rather than through <see cref="BattleState.Create(string, ulong, PetState, BossState)"/>:
    /// creation generates a board from a seed and resets the counters, which would
    /// discard exactly the state being recovered. Restoring is a reconstruction of
    /// stored values, not a new battle (<c>GAME_STATE.md</c> §2.6.2 item 4).
    /// </summary>
    private static BattleState FromJson(BattleStateJson dto) =>
        new(
            dto.BattleId,
            dto.Turn,
            dto.Sequence,
            dto.RngSeed,
            new RngState(dto.RngState.State, dto.RngState.Increment),

            // BoardState.FromCellEntries enforces the documented board contract —
            // exactly 64 entries, each holding one of the four Gem types, with
            // orientation present exactly for a Line Clear Gem (§2.1.1, §2.1.4
            // item 2) — so a malformed board is rejected here rather than admitted.
            BoardState.FromCellEntries(dto.BoardState.Cells.Select(FromCellJson)),

            dto.Combo,
            dto.MatchCount,
            FromPetStateJson(dto.PetState),
            FromBossStateJson(dto.BossState),

            // §2.1.10 item 3: an absent member is the documented representation of
            // "no Swap has been committed". No sentinel pair is synthesized for it
            // — the CommittedSwapPair constructor rejects any non-canonical value,
            // including the (0, 0) stand-in the contract explicitly rules out.
            dto.LastCommittedSwapPair is { } pair
                ? new CommittedSwapPair(pair.MinCellIndex, pair.MaxCellIndex)
                : null);

    /// <summary>
    /// Rebuilds one cell entry, parsing the documented contract names back into
    /// their domain values.
    /// </summary>
    private static Cell FromCellJson(CellJson cell) =>
        new(
            Enum.Parse<GemType>(cell.GemType, ignoreCase: true),
            cell.SpecialGem is { } gem
                ? new SpecialGem(
                    Enum.Parse<SpecialGemType>(gem.Type, ignoreCase: true),
                    gem.Orientation is null
                        ? null
                        : Enum.Parse<SpecialGemOrientation>(gem.Orientation, ignoreCase: true))
                : null);

    /// <summary>
    /// Rebuilds <c>PetState</c>, including both battle-scoped loadout snapshots
    /// read from the record itself rather than rebuilt from any inventory
    /// (<c>RELIC_RULES.md</c> §2.5, <c>CARD_RULES.md</c> §1).
    /// </summary>
    private static PetState FromPetStateJson(PetStateJson dto) =>
        new(
            dto.HP,
            dto.MaxHP,
            dto.ATK,
            dto.DEF,
            dto.Crit,
            dto.Power,
            Enum.Parse<Element>(dto.Element, ignoreCase: true),
            new PassiveId(dto.PassiveId),
            FromPassiveProgressJson(dto.PassiveProgress),

            // §2.3 / §4: an absent member IS the default behavior. Parsing only a
            // present value keeps absence meaning "Default" rather than becoming a
            // third, explicitly-stored spelling of it.
            dto.PassiveResetOverride is null
                ? null
                : Enum.Parse<PassiveResetBehavior>(dto.PassiveResetOverride, ignoreCase: true),

            dto.EquippedRelics?.Select(value => new EquippedRelicIdentity(value)).ToArray(),
            dto.EquippedCards?.Select(value => new EquippedCardIdentity(value)).ToArray());

    /// <summary>
    /// Rebuilds <c>BossState</c> from its stored values — carried, never
    /// recalculated (<c>GAME_STATE.md</c> §2.4).
    /// </summary>
    private static BossState FromBossStateJson(BossStateJson dto) =>
        new(
            new BossId(dto.BossId),
            Enum.Parse<Element>(dto.Element, ignoreCase: true),
            dto.HP,
            dto.MaxHP,
            dto.ATK,
            dto.DEF,
            Enum.Parse<BossStateKind>(dto.State, ignoreCase: true),
            new PassiveId(dto.PassiveId),
            FromPassiveProgressJson(dto.PassiveProgress),
            dto.SkillCharge,
            dto.SkillCooldown);

    /// <summary>
    /// Rebuilds a <c>(Threshold, Current)</c> progress pair.
    /// </summary>
    private static PassiveProgress FromPassiveProgressJson(PassiveProgressJson dto) =>
        new(dto.Threshold, dto.Current);
}
