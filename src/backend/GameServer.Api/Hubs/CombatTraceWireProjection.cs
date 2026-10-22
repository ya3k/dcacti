namespace GameServer.Api.Hubs;

using System.Text.Json.Serialization;
using GameServer.Domain.Combat;
using GameServer.Domain.Match3;

/// <summary>
/// Projects a Domain <see cref="CombatTrace"/> onto the <c>SIGNALR_PROTOCOL.md</c>
/// §3.3 wire schema (TASK-321).
///
/// <b>A pure field mapping, exactly like <see cref="BattleEventWireProjection"/>.</b>
/// The Domain value is never serialized directly (`§3.2.1` item 1): the transport
/// owns the wire shape, the casing, and the omission rules. The projection adds
/// no gameplay value, re-derives none, reads no second state, and changes no
/// computation — every member is carried across from the values the resolution
/// already recorded.
///
/// <b>Casing and omission are fixed here.</b> `§3.2.3` fixes camelCase
/// explicitly, so every member is named; `§3.2.5` requires a not-applicable
/// member to be omitted rather than written as `null`, so the optional members
/// (a board cycle's `fromCell`/`toCell`, a card cycle's `cardId`, a status tick's
/// `cycleOrdinal`) use `WhenWritingNull`. A real zero — a zero DEF, a zero heal,
/// a zero absorption — is a value and is always written.
/// </summary>
internal static class CombatTraceWireProjection
{
    /// <summary>
    /// Projects the whole trace. Every list is projected position-for-position, in
    /// the order the resolution recorded it.
    /// </summary>
    public static CombatTraceWireDto Project(CombatTrace trace)
    {
        var cycles = new CombatTraceCycleWireDto[trace.Cycles.Count];
        for (var index = 0; index < trace.Cycles.Count; index++)
        {
            cycles[index] = ProjectCycle(trace.Cycles[index]);
        }

        var damage = new CombatTraceDamageWireDto[trace.Damage.Count];
        for (var index = 0; index < trace.Damage.Count; index++)
        {
            damage[index] = ProjectDamage(trace.Damage[index]);
        }

        var healing = new CombatTraceHealWireDto[trace.Healing.Count];
        for (var index = 0; index < trace.Healing.Count; index++)
        {
            healing[index] = ProjectHeal(trace.Healing[index]);
        }

        return new CombatTraceWireDto(
            BattleId: trace.BattleId,
            ServerSequence: trace.ServerSequence,
            Move: new CombatTraceMoveWireDto(
                Kind: MoveKind(trace.Move.Kind),
                FromCell: trace.Move.FromCell,
                ToCell: trace.Move.ToCell),
            Cycles: cycles,
            Damage: damage,
            Healing: healing,
            Pet: ProjectTarget(trace.Pet),
            Boss: ProjectTarget(trace.Boss));
    }

    private static CombatTraceCycleWireDto ProjectCycle(CombatTraceCycle cycle)
    {
        var gems = new CombatTraceGemWireDto[cycle.MatchedGems.Count];
        for (var index = 0; index < cycle.MatchedGems.Count; index++)
        {
            var gem = cycle.MatchedGems[index];
            gems[index] = new CombatTraceGemWireDto(
                GemType: GemTypes.ToContractName(gem.GemType),
                Count: gem.Count);
        }

        var cascades = new CombatTraceCascadeWireDto[cycle.Cascades.Count];
        for (var index = 0; index < cycle.Cascades.Count; index++)
        {
            cascades[index] = new CombatTraceCascadeWireDto(Depth: cycle.Cascades[index].Depth);
        }

        return new CombatTraceCycleWireDto(
            Ordinal: cycle.Ordinal,
            Actor: Party(cycle.Actor),
            Action: CycleAction(cycle.Action),
            FromCell: cycle.FromCell,
            ToCell: cycle.ToCell,
            CardId: cycle.CardId,
            MatchedGems: gems,
            Cascades: cascades);
    }

    private static CombatTraceDamageWireDto ProjectDamage(CombatTraceDamage damage) =>
        new(
            Ordinal: damage.Ordinal,
            Phase: DamagePhase(damage.Phase),
            CycleOrdinal: damage.CycleOrdinal,
            Source: Party(damage.Source),
            Target: Party(damage.Target),
            Base: damage.Base,
            ComboModifier: damage.ComboModifier,
            ElementModifier: damage.ElementModifier,
            OtherModifiers: damage.OtherModifiers,
            MitigatedDamage: damage.MitigatedDamage,
            FinalDamage: damage.FinalDamage,
            DefenderDefBase: damage.DefenderDefenseBase,
            DefenderDefEffective: damage.DefenderDefenseEffective,
            ShieldAbsorbed: damage.ShieldAbsorbed,
            HpBefore: damage.HpBefore,
            HpAfter: damage.HpAfter,
            HpLoss: damage.HpLoss);

    private static CombatTraceHealWireDto ProjectHeal(CombatTraceHeal heal) =>
        new(
            Ordinal: heal.Ordinal,
            CycleOrdinal: heal.CycleOrdinal,
            Target: Party(heal.Target),
            Amount: heal.Amount,
            RawAmount: heal.RawAmount,
            HpBefore: heal.HpBefore,
            HpAfter: heal.HpAfter);

    private static CombatTraceTargetWireDto ProjectTarget(CombatTraceTarget target) =>
        new(
            HpBefore: target.HpBefore,
            HpAfter: target.HpAfter,
            DefBase: target.DefBase,
            PowerBefore: target.PowerBefore,
            PowerAfter: target.PowerAfter);

    /// <summary>
    /// The two-sided party vocabulary `§3.2.14` item 1 / `§3.2.26` item 2 already
    /// fix: lowercase `"player"` or `"boss"`, never the enum's ordinal.
    /// </summary>
    private static string Party(DamageParty party) => party.ToString().ToLowerInvariant();

    private static string CycleAction(CombatTraceCycleAction action) => action switch
    {
        CombatTraceCycleAction.Board => "board",
        CombatTraceCycleAction.Card => "card",
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Not a documented combat-trace cycle action."),
    };

    private static string DamagePhase(CombatTraceDamagePhase phase) => phase switch
    {
        CombatTraceDamagePhase.Attack => "attack",
        CombatTraceDamagePhase.StatusTick => "statusTick",
        _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, "Not a documented combat-trace damage phase."),
    };

    private static string MoveKind(CombatTraceMoveKind kind) => kind switch
    {
        CombatTraceMoveKind.Swap => "swap",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Not a documented combat-trace move kind."),
    };
}

/// <summary>
/// The `ReceiveEvents.combatTrace` wire DTO (`SIGNALR_PROTOCOL.md` §3 item 5,
/// §3.3). Every member is named explicitly because `§3.2.3` fixes camelCase and
/// warns against relying on a serializer default.
/// </summary>
/// <param name="BattleId">The same battle id the envelope carries.</param>
/// <param name="ServerSequence">The same committed sequence the envelope carries.</param>
/// <param name="Move">The committed player move.</param>
/// <param name="Cycles">The round's actor cycles, in resolution order.</param>
/// <param name="Damage">Every damage instance, individually, in order.</param>
/// <param name="Healing">Every heal applied, in order.</param>
/// <param name="Pet">The Pet's before/after snapshot.</param>
/// <param name="Boss">The Boss's before/after snapshot.</param>
public record CombatTraceWireDto(
    [property: JsonPropertyName("battleId")] string BattleId,
    [property: JsonPropertyName("serverSequence")] int ServerSequence,
    [property: JsonPropertyName("move")] CombatTraceMoveWireDto Move,
    [property: JsonPropertyName("cycles")] IReadOnlyList<CombatTraceCycleWireDto> Cycles,
    [property: JsonPropertyName("damage")] IReadOnlyList<CombatTraceDamageWireDto> Damage,
    [property: JsonPropertyName("healing")] IReadOnlyList<CombatTraceHealWireDto> Healing,
    [property: JsonPropertyName("pet")] CombatTraceTargetWireDto Pet,
    [property: JsonPropertyName("boss")] CombatTraceTargetWireDto Boss);

/// <summary>The traced move's wire shape (`§3.3.1`).</summary>
/// <param name="Kind">`"swap"` — the only traced move kind.</param>
/// <param name="FromCell">The committed Swap's first cell.</param>
/// <param name="ToCell">The committed Swap's second cell.</param>
public record CombatTraceMoveWireDto(
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("fromCell")] int FromCell,
    [property: JsonPropertyName("toCell")] int ToCell);

/// <summary>One actor cycle's wire shape (`§3.3.1`).</summary>
/// <param name="Ordinal">1-based order within the round.</param>
/// <param name="Actor">`"player"` or `"boss"`.</param>
/// <param name="Action">`"board"` or `"card"`.</param>
/// <param name="FromCell">Board action's first cell; omitted for a card cycle.</param>
/// <param name="ToCell">Board action's second cell; omitted for a card cycle.</param>
/// <param name="CardId">Cast Card identity; omitted for a board cycle.</param>
/// <param name="MatchedGems">The cycle's cleared-Gem counts, always present.</param>
/// <param name="Cascades">One entry per detection pass, always present.</param>
public record CombatTraceCycleWireDto(
    [property: JsonPropertyName("ordinal")] int Ordinal,
    [property: JsonPropertyName("actor")] string Actor,
    [property: JsonPropertyName("action")] string Action,
    [property: JsonPropertyName("fromCell")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? FromCell,
    [property: JsonPropertyName("toCell")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? ToCell,
    [property: JsonPropertyName("cardId")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? CardId,
    [property: JsonPropertyName("matchedGems")] IReadOnlyList<CombatTraceGemWireDto> MatchedGems,
    [property: JsonPropertyName("cascades")] IReadOnlyList<CombatTraceCascadeWireDto> Cascades);

/// <summary>One Gem type's cleared count (`§3.3.1`).</summary>
/// <param name="GemType">`"ATK"`, `"DEF"`, `"HP"`, or `"POWER"` (`§3.2.4`).</param>
/// <param name="Count">The number of cleared cells of that type.</param>
public record CombatTraceGemWireDto(
    [property: JsonPropertyName("gemType")] string GemType,
    [property: JsonPropertyName("count")] int Count);

/// <summary>One detection pass of a cycle (`§3.3.1`).</summary>
/// <param name="Depth">The pass's 1-based depth within its own cycle.</param>
public record CombatTraceCascadeWireDto(
    [property: JsonPropertyName("depth")] int Depth);

/// <summary>One damage instance's wire shape (`§3.3.1`).</summary>
/// <param name="Ordinal">1-based order within the round.</param>
/// <param name="Phase">`"attack"` or `"statusTick"`.</param>
/// <param name="CycleOrdinal">Owning cycle's ordinal; omitted for a status tick.</param>
/// <param name="Source">`"player"` or `"boss"`.</param>
/// <param name="Target">`"player"` or `"boss"`.</param>
/// <param name="Base">Step 1 — ATK plus damage pool.</param>
/// <param name="ComboModifier">Step 2 factor.</param>
/// <param name="ElementModifier">Step 3 factor.</param>
/// <param name="OtherModifiers">Step 4 factor.</param>
/// <param name="MitigatedDamage">Step 5 — post-DEF, pre-truncation damage.</param>
/// <param name="FinalDamage">Step 6 — truncated Final Damage.</param>
/// <param name="DefenderDefBase">The defender's stored base DEF.</param>
/// <param name="DefenderDefEffective">The effective DEF step 5 actually used.</param>
/// <param name="ShieldAbsorbed">The part of the Final Damage the Shield absorbed.</param>
/// <param name="HpBefore">The target's HP before this instance.</param>
/// <param name="HpAfter">The target's HP after this instance.</param>
/// <param name="HpLoss">The HP this instance actually removed.</param>
public record CombatTraceDamageWireDto(
    [property: JsonPropertyName("ordinal")] int Ordinal,
    [property: JsonPropertyName("phase")] string Phase,
    [property: JsonPropertyName("cycleOrdinal")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? CycleOrdinal,
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("target")] string Target,
    [property: JsonPropertyName("base")] int Base,
    [property: JsonPropertyName("comboModifier")] double ComboModifier,
    [property: JsonPropertyName("elementModifier")] double ElementModifier,
    [property: JsonPropertyName("otherModifiers")] double OtherModifiers,
    [property: JsonPropertyName("mitigatedDamage")] double MitigatedDamage,
    [property: JsonPropertyName("finalDamage")] int FinalDamage,
    [property: JsonPropertyName("defenderDefBase")] int DefenderDefBase,
    [property: JsonPropertyName("defenderDefEffective")] int DefenderDefEffective,
    [property: JsonPropertyName("shieldAbsorbed")] int ShieldAbsorbed,
    [property: JsonPropertyName("hpBefore")] int HpBefore,
    [property: JsonPropertyName("hpAfter")] int HpAfter,
    [property: JsonPropertyName("hpLoss")] int HpLoss);

/// <summary>One heal's wire shape (`§3.3.1`).</summary>
/// <param name="Ordinal">1-based order within the round.</param>
/// <param name="CycleOrdinal">The owning cycle's ordinal.</param>
/// <param name="Target">`"player"` or `"boss"`.</param>
/// <param name="Amount">HP actually restored; 0 at full HP.</param>
/// <param name="RawAmount">The raw Heal Pool.</param>
/// <param name="HpBefore">The target's HP before the heal.</param>
/// <param name="HpAfter">The target's HP after the heal.</param>
public record CombatTraceHealWireDto(
    [property: JsonPropertyName("ordinal")] int Ordinal,
    [property: JsonPropertyName("cycleOrdinal")] int CycleOrdinal,
    [property: JsonPropertyName("target")] string Target,
    [property: JsonPropertyName("amount")] int Amount,
    [property: JsonPropertyName("rawAmount")] int RawAmount,
    [property: JsonPropertyName("hpBefore")] int HpBefore,
    [property: JsonPropertyName("hpAfter")] int HpAfter);

/// <summary>One side's before/after snapshot (`§3.3.1`).</summary>
/// <param name="HpBefore">HP before the move.</param>
/// <param name="HpAfter">HP after the committed write-back.</param>
/// <param name="DefBase">The stored base DEF.</param>
/// <param name="PowerBefore">Power before the move.</param>
/// <param name="PowerAfter">Power after the committed write-back.</param>
public record CombatTraceTargetWireDto(
    [property: JsonPropertyName("hpBefore")] int HpBefore,
    [property: JsonPropertyName("hpAfter")] int HpAfter,
    [property: JsonPropertyName("defBase")] int DefBase,
    [property: JsonPropertyName("powerBefore")] int PowerBefore,
    [property: JsonPropertyName("powerAfter")] int PowerAfter);
