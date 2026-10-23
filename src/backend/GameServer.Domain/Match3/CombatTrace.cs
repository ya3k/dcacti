namespace GameServer.Domain.Match3;

using GameServer.Domain.Combat;

/// <summary>
/// The ordered, authoritative combat telemetry of one committed Swap
/// (<c>SIGNALR_PROTOCOL.md</c> §3 item 5, §3.3 — TASK-321).
///
/// <code>
/// CombatTrace
/// ├── BattleId, ServerSequence     envelope correlation
/// ├── Move                         the committed player move
/// ├── Cycles[]                     the round's actor cycles, in resolution order
/// ├── Damage[]                     every damage instance, individually
/// ├── Healing[]                    every heal applied, including a 0 heal at full HP
/// ├── Pet                          before/after snapshot
/// └── Boss                         before/after snapshot
/// </code>
///
/// <b>It is a report of a resolution, not a second one.</b> Every value here is
/// read from the values the deterministic resolution already computed at the
/// point it computed them — the cascade passes that ran, the resources the
/// cleared Gems generated, the damage instances the pipeline priced, the heals
/// the heal stage applied, and the states the single write-back committed
/// (<c>GAME_STATE.md</c> §5.1). Capturing it re-runs no damage step, draws no
/// RNG, mutates no state, and changes no combat value. It never becomes a source
/// of state (<c>GAME_EVENTS.md</c> §3 item 6): it is transient delivery metadata
/// that is not persisted in <c>BattleState</c>, Redis, or PostgreSQL.
///
/// <b>It is a Domain value, not a wire type.</b> The transport layer projects it
/// onto its own DTOs (<c>ARCHITECTURE.md</c> §2.1 item 4,
/// <c>SIGNALR_PROTOCOL.md</c> §3.2.1); nothing here defines serialization.
/// </summary>
/// <param name="BattleId">
/// The battle the resolution belongs to. It is the same value the
/// <c>ReceiveEvents</c> envelope carries, so a consumer can reject a trace that
/// does not belong to the batch it arrived on.
/// </param>
/// <param name="ServerSequence">
/// The committed <c>BattleState.Sequence</c> after this resolution — the same
/// value the envelope carries. It is read from the committed state, never
/// computed here.
/// </param>
/// <param name="Move">The committed player move's identity.</param>
/// <param name="Cycles">
/// The round's actor cycles in resolution order. A committed Swap always has
/// the player's board cycle; the Boss's response cycle is present only when the
/// Boss actually acted (a board action or a Boss Card cast).
/// </param>
/// <param name="Damage">
/// Every damage instance of the round in event order — including a step-19a
/// damage-over-time tick. Instances are never collapsed into one record.
/// </param>
/// <param name="Healing">
/// Every heal the round applied, in application order. A heal whose effective
/// amount is 0 (a heal at full HP) is reported with its raw amount, so it is
/// distinguishable from "no heal occurred". A heal applied outside any actor
/// cycle's action is still reported, with no cycle ordinal — the not-applicable
/// omission <c>SIGNALR_PROTOCOL.md</c> §3.3.1/§3.3.2 item 7 states.
/// </param>
/// <param name="Pet">The active Pet's before/after snapshot.</param>
/// <param name="Boss">The Boss's before/after snapshot.</param>
public readonly record struct CombatTrace(
    string BattleId,
    int ServerSequence,
    CombatTraceMove Move,
    IReadOnlyList<CombatTraceCycle> Cycles,
    IReadOnlyList<CombatTraceDamage> Damage,
    IReadOnlyList<CombatTraceHeal> Healing,
    CombatTraceTarget Pet,
    CombatTraceTarget Boss);

/// <summary>
/// The move a <see cref="CombatTrace"/> reports (<c>SIGNALR_PROTOCOL.md</c>
/// §3.3.1).
///
/// <b>Only a committed Swap carries a trace.</b> Card and Pet-skill casts change
/// combat values but do not resolve a player board cycle, so they deliver no
/// trace rather than a half-populated one; their events remain the only report.
/// </summary>
/// <param name="Kind">The move kind. MVP's only traced kind is a Swap.</param>
/// <param name="FromCell">The committed Swap's first cell (§1.0 index).</param>
/// <param name="ToCell">The committed Swap's second cell (§1.0 index).</param>
public readonly record struct CombatTraceMove(
    CombatTraceMoveKind Kind,
    int FromCell,
    int ToCell);

/// <summary>The traced move kinds (<c>SIGNALR_PROTOCOL.md</c> §3.3.1).</summary>
public enum CombatTraceMoveKind
{
    /// <summary>A committed player Swap — the only kind that resolves a combat round.</summary>
    Swap = 0,
}

/// <summary>
/// One actor cycle of a combat round (<c>SIGNALR_PROTOCOL.md</c> §3.3.1).
///
/// <code>
/// Cycle
/// ├── Ordinal, Actor              identity — never inferred from array position
/// ├── Action                     board swap or Boss Card cast
/// ├── FromCell/ToCell  or CardId  the action's own identity
/// ├── MatchedGems[]              the cycle's MatchedGemSummary counts
/// └── Cascades[]                 one entry per detection pass, in depth order
/// </code>
///
/// <b>The actor is the cycle's own, not an inference.</b> The player's cycle and
/// the Boss's cycle are separate entries; a consumer reads <see cref="Actor"/>
/// rather than assuming the first entry is the player and the second the Boss
/// (<c>SIGNALR_PROTOCOL.md</c> §3.3.2 item 2).
/// </summary>
/// <param name="Ordinal">1-based order of this cycle within the round.</param>
/// <param name="Actor">The side that owns the cycle.</param>
/// <param name="Action">Whether the cycle resolved a board swap or a Boss Card cast.</param>
/// <param name="FromCell">The board action's first cell; <c>null</c> for a card cycle.</param>
/// <param name="ToCell">The board action's second cell; <c>null</c> for a card cycle.</param>
/// <param name="CardId">The cast Card's definition identity; <c>null</c> for a board cycle.</param>
/// <param name="MatchedGems">
/// The cycle's cleared-Gem counts, one entry per Gem type in the fixed
/// ATK, DEF, HP, POWER order (<c>MATCH3_RULES.md</c> §5.8.2). Always present;
/// a cycle that cleared nothing carries four zero counts, never a missing list.
/// </param>
/// <param name="Cascades">
/// One entry per detection pass of this cycle, in the cycle's own depth order.
/// Depth 1 is the pass run on the committed Swap's board
/// (<c>MATCH3_RULES.md</c> §4.2); depth ≥ 2 is a Cascade. The list is the
/// resolution's own pass list — no pass is synthesized or dropped.
/// </param>
public readonly record struct CombatTraceCycle(
    int Ordinal,
    DamageParty Actor,
    CombatTraceCycleAction Action,
    int? FromCell,
    int? ToCell,
    string? CardId,
    IReadOnlyList<CombatTraceGemCount> MatchedGems,
    IReadOnlyList<CombatTraceCascade> Cascades);

/// <summary>How an actor cycle acted (<c>SIGNALR_PROTOCOL.md</c> §3.3.1).</summary>
public enum CombatTraceCycleAction
{
    /// <summary>The actor committed a board swap (<c>SwapStarted</c>).</summary>
    Board = 0,

    /// <summary>The Boss cast its provisioned Boss Card (<c>BossCardCast</c>).</summary>
    Card = 1,
}

/// <summary>One Gem type's cleared count in a cycle (<c>MATCH3_RULES.md</c> §5.8.2).</summary>
/// <param name="GemType">The Gem type the count is for.</param>
/// <param name="Count">The number of cells of that type the cycle cleared.</param>
public readonly record struct CombatTraceGemCount(GemType GemType, int Count);

/// <summary>
/// One detection pass of an actor cycle — the cycle's cascade identity
/// (<c>MATCH3_RULES.md</c> §4.2, <c>SIGNALR_PROTOCOL.md</c> §3.2.28).
/// </summary>
/// <param name="Depth">
/// The pass's 1-based depth within its own cycle. Depth 1 is the pass run on the
/// committed Swap's board and is not a Cascade; depth ≥ 2 is. Depths restart per
/// actor cycle (<c>MATCH3_RULES.md</c> §4.2), which is why the cycle's
/// <see cref="CombatTraceCycle.Actor"/> — not a depth — is what attributes a
/// pass to a side.
/// </param>
public readonly record struct CombatTraceCascade(int Depth);

/// <summary>
/// One damage instance of a combat round, reported individually
/// (<c>SIGNALR_PROTOCOL.md</c> §3.3.1).
///
/// <code>
/// Damage
/// ├── Ordinal, Phase, CycleOrdinal   identity and attribution
/// ├── Source, Target                 the instance's two parties
/// ├── Base / Combo / Element / Other the pipeline's steps 1–4
/// ├── MitigatedDamage, FinalDamage   steps 5–6
/// ├── DefenderDefBase/Effective      the DEF stats step 5 actually read
/// └── ShieldAbsorbed, HpBefore/After/HpLoss  the applied outcome
/// </code>
///
/// <b>Every value is the pipeline's own.</b> The breakdown is the
/// <c>DamageCalculation</c> the pipeline returned; the DEF stats are the values
/// its step 5 read (never re-derived and never a buff prediction); the shield
/// absorption and HP pair are the <c>DamageResult</c>'s. A Shield that absorbed
/// the whole instance yields <see cref="HpLoss"/> 0 with a positive
/// <see cref="ShieldAbsorbed"/> — absorption is never reported as HP loss.
/// </summary>
/// <param name="Ordinal">1-based order of this instance within the round.</param>
/// <param name="Phase">Whether the instance is an ATK-gated attack or a step-19a DoT tick.</param>
/// <param name="CycleOrdinal">
/// The <see cref="CombatTraceCycle.Ordinal"/> whose action produced this
/// instance; <c>null</c> for a step-19a status tick, which belongs to no actor
/// cycle.
/// </param>
/// <param name="Source">The party that dealt the damage.</param>
/// <param name="Target">The party that received it.</param>
/// <param name="Base">Step 1 — ATK plus the generated damage pool.</param>
/// <param name="ComboModifier">Step 2 — the Combo factor applied.</param>
/// <param name="ElementModifier">Step 3 — the Element factor applied.</param>
/// <param name="OtherModifiers">Step 4 — the combined other factor applied.</param>
/// <param name="MitigatedDamage">
/// Step 5 — the post-DEF, pre-truncation damage. It is damage, never a DEF stat
/// (<c>COMBAT_RULES.md</c> §3.2).
/// </param>
/// <param name="FinalDamage">Step 6 — the truncated, never-negative Final Damage.</param>
/// <param name="DefenderDefenseBase">
/// The defender's stored base DEF at this instance's step 5. An explicit 0 is a
/// real zero DEF.
/// </param>
/// <param name="DefenderDefenseEffective">
/// The effective DEF step 5 actually used — base plus every active
/// <c>TargetStat = "DEF"</c> modifier (<c>COMBAT_RULES.md</c> §5.4.6, §5.5.6).
/// </param>
/// <param name="ShieldAbsorbed">The part of the Final Damage the Shield pool absorbed.</param>
/// <param name="HpBefore">The target's HP immediately before this instance.</param>
/// <param name="HpAfter">The target's HP after this instance's Final Damage was applied.</param>
/// <param name="HpLoss">The HP this instance actually removed; 0 when a Shield absorbed it all.</param>
public readonly record struct CombatTraceDamage(
    int Ordinal,
    CombatTraceDamagePhase Phase,
    int? CycleOrdinal,
    DamageParty Source,
    DamageParty Target,
    int Base,
    double ComboModifier,
    double ElementModifier,
    double OtherModifiers,
    double MitigatedDamage,
    int FinalDamage,
    int DefenderDefenseBase,
    int DefenderDefenseEffective,
    int ShieldAbsorbed,
    int HpBefore,
    int HpAfter,
    int HpLoss);

/// <summary>Which stage priced a damage instance (<c>SIGNALR_PROTOCOL.md</c> §3.3.1).</summary>
public enum CombatTraceDamagePhase
{
    /// <summary>An ATK-gated attack instance (steps 15–17 or 18d).</summary>
    Attack = 0,

    /// <summary>A step-19a damage-over-time tick.</summary>
    StatusTick = 1,
}

/// <summary>
/// One heal a combat round applied (<c>SIGNALR_PROTOCOL.md</c> §3.3.1).
///
/// <b>No Battle Event reports a heal</b> (<c>GAME_EVENTS.md</c> §2,
/// <c>COMBAT_RULES.md</c> §4), so the trace is the only delivery of this value.
/// The applied amount is the HP the heal actually restored after modifiers and
/// the MaxHP clamp; a heal at full HP is reported with its raw amount and a zero
/// applied amount rather than being omitted (<c>COMBAT_RULES.md</c> §4 item 7).
/// </summary>
/// <param name="Ordinal">1-based order of this heal within the round.</param>
/// <param name="CycleOrdinal">
/// The cycle whose action produced the heal; <c>null</c> for a heal no actor cycle
/// produced — the Boss Passive's regeneration, which <c>BOSS_RULES.md</c> §3.3 item 1
/// applies once per player action outside any cycle's action. <c>null</c> is the
/// *not-applicable* case, not an unknown: <c>SIGNALR_PROTOCOL.md</c> §3.3.1/§3.3.2
/// item 7 require it to be omitted from the JSON, so no sentinel stands in for it.
/// Every heal a cycle's action produces names that cycle
/// (<see cref="CombatTraceCycle.Ordinal"/>, always ≥ 1).
/// </param>
/// <param name="Target">The side that was healed.</param>
/// <param name="Amount">HP actually restored; 0 at full HP.</param>
/// <param name="RawAmount">The raw Heal Pool the heal was resolved from.</param>
/// <param name="HpBefore">The target's HP immediately before the heal.</param>
/// <param name="HpAfter">The target's HP after the heal.</param>
public readonly record struct CombatTraceHeal(
    int Ordinal,
    int? CycleOrdinal,
    DamageParty Target,
    int Amount,
    int RawAmount,
    int HpBefore,
    int HpAfter);

/// <summary>
/// One side's before/after combat snapshot for the move
/// (<c>SIGNALR_PROTOCOL.md</c> §3.3.1).
///
/// <b>DEF is the stored base stat.</b> A DEF-Gem buff never rewrites it
/// (<c>COMBAT_RULES.md</c> §5.4.4); the effective DEF actually used is carried
/// per damage instance (<see cref="CombatTraceDamage.DefenderDefenseEffective"/>),
/// which is where a consumer reads it — never by predicting it from a Gem count.
/// </summary>
/// <param name="HpBefore">HP before the move began.</param>
/// <param name="HpAfter">HP after the committed write-back.</param>
/// <param name="DefBase">The stored base DEF.</param>
/// <param name="PowerBefore">Power before the move began.</param>
/// <param name="PowerAfter">Power after the committed write-back.</param>
public readonly record struct CombatTraceTarget(
    int HpBefore,
    int HpAfter,
    int DefBase,
    int PowerBefore,
    int PowerAfter);
