using GameServer.Application.Battle;
using GameServer.Domain.Battle;
using GameServer.Domain.Bosses;
using GameServer.Domain.Cards;
using GameServer.Domain.Players;
using GameServer.Domain.Relics;

namespace GameServer.Application.Tests.Balance;

/// <summary>
/// The complete, immutable input to one balance simulation run
/// (<c>TASK-193</c> §4.1).
///
/// <b>These inputs fully determine a run.</b> Given identical values for every
/// member below, the simulation produces an identical result — which is the
/// determinism contract <c>MATCH3_RULES.md</c> §7.1 and <c>GAME_STATE.md</c>
/// §2.6.2 item 4 already require of the production pipeline. Nothing here is a
/// balance value: every member is either an identity, a seed, or the caller's
/// own selection, and the harness computes none of them.
///
/// <b>Why the configuration is data and not code.</b> A scenario must vary only
/// what the scenario is about (<c>TASK-193</c> §5.2). Holding the inputs as one
/// value lets a comparison run be built by <c>with</c>-expression from a
/// baseline, so every member except the varied one is provably identical
/// (<c>H-07</c>) rather than merely intended to be.
/// </summary>
internal sealed record BalanceSimulationConfiguration
{
    /// <summary>
    /// The battle's PRNG seed (<c>GAME_STATE.md</c> §2.6.1). It selects the
    /// generated board and nothing else: no Gem value, damage number, or rule
    /// outcome is derived from it directly (<c>ADR-009</c> §2). It is a
    /// simulation input, not a gameplay value.
    /// </summary>
    public required ulong Seed { get; init; }

    /// <summary>
    /// The identity of the Player the simulated battle belongs to
    /// (<c>GAME_STATE.md</c> §2.8) — owner identity only, carrying no combat
    /// pool.
    /// </summary>
    public required PlayerId PlayerId { get; init; }

    /// <summary>
    /// The active Pet's configuration — identity, Element, Passive, and the
    /// battle-scoped Card and Relic loadout snapshots
    /// (<c>GAME_STATE.md</c> §2.3).
    /// </summary>
    public required BattleStateService.PetConfiguration Pet { get; init; }

    /// <summary>
    /// The Boss definition the battle is fought against
    /// (<c>BOSS_RULES.md</c> §6.1).
    /// </summary>
    public required BossDefinition Boss { get; init; }

    /// <summary>
    /// The Card definitions the run resolves casts against
    /// (<c>DATABASE.md</c> §1). The lookup is an identity map, exactly as a
    /// production card-definition lookup would return rows for the equipped ids.
    /// </summary>
    public required IReadOnlyList<CardDefinition> CardDefinitions { get; init; }

    /// <summary>
    /// The Relic definitions in equip-slot order, or <c>null</c> for a battle
    /// whose Relic content is not attached (<c>RELIC_RULES.md</c> §2.3). A
    /// <c>null</c> list is the documented staging position, never an invented
    /// empty loadout.
    /// </summary>
    public IReadOnlyList<RelicDefinition?>? RelicDefinitions { get; init; }

    /// <summary>
    /// The scripted player policy this run is driven by
    /// (<c>TASK-193</c> §3.2, <c>BalancePlayerPolicy</c>).
    /// </summary>
    public required BalancePlayerPolicy Policy { get; init; }

    /// <summary>
    /// The hard maximum number of Turns this run may resolve before it is
    /// reported as <see cref="BalanceSimulationOutcome.Stalemate"/>
    /// (<c>TASK-193</c> §5.3).
    ///
    /// <b>This is a technical safety bound, not a gameplay balance value.</b> It
    /// is chosen for termination — so a broken configuration cannot hang — and
    /// it approves nothing. A run that reaches it reports <c>STALEMATE</c>, which
    /// is evidence for B-07 and not a decision about it. The default is
    /// deliberately far above any plausible fight, so it never silently stands in
    /// for a duration target (Q-1 is QUALITATIVE: no target exists).
    /// </summary>
    public int MaxTurns { get; init; } = DefaultMaxTurns;

    /// <summary>
    /// The default hard Turn limit — see <see cref="MaxTurns"/>. It is a safety
    /// bound only and represents no approved fight duration.
    /// </summary>
    public const int DefaultMaxTurns = 1000;

    /// <summary>
    /// The battle id the simulated battle is created under. It is fixed so a run
    /// is reproducible from its record alone (<c>H-09</c>); it identifies the
    /// simulation's own isolated record and never a real battle.
    /// </summary>
    public string BattleId => "balance-simulation";
}
