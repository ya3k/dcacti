using GameServer.Domain.Battle;
using GameServer.Domain.Bosses;
using GameServer.Domain.Players;

namespace GameServer.Application.Tests.Balance;

/// <summary>
/// One balance simulation run's complete result (<c>TASK-193</c> §5.1's emit
/// stage, <c>H-09</c>).
///
/// <b>A result must be reproducible from its own record.</b> It therefore carries
/// the seed, the identifying half of the configuration, the policy, the mode,
/// the outcome, the Turn count, and the full metric set — so a reader can re-run
/// the identical scenario without consulting anything the result does not state.
///
/// It is immutable, matching the repository's record conventions, and is exposed
/// through no production API (<c>TASK-193</c> §3.3).
/// </summary>
internal sealed record BalanceSimulationResult
{
    /// <summary>The seed the run was created with (<c>GAME_STATE.md</c> §2.6.1).</summary>
    public required ulong Seed { get; init; }

    /// <summary>The simulation mode this run used (<c>TASK-193</c> §5.2).</summary>
    public required BalanceSimulationMode Mode { get; init; }

    /// <summary>The scripted player policy that drove the run (§3.2).</summary>
    public required string PolicyName { get; init; }

    /// <summary>How the run ended (§5.3, M-14).</summary>
    public required BalanceSimulationOutcome Outcome { get; init; }

    /// <summary>
    /// Why the run ended as it did, in words — the production reason for a
    /// terminal outcome, or the configuration fault for an invalid one.
    /// </summary>
    public required string OutcomeDetail { get; init; }

    /// <summary>The effective Turn limit this run was bounded by (§5.3, H-08).</summary>
    public required int MaxTurns { get; init; }

    /// <summary>Identifying half of the configuration — the Player the battle belongs to.</summary>
    public required PlayerId PlayerId { get; init; }

    /// <summary>Identifying half of the configuration — the active Pet.</summary>
    public required string PetId { get; init; }

    /// <summary>Identifying half of the configuration — the Boss's canonical identity.</summary>
    public required string BossId { get; init; }

    /// <summary>
    /// The Boss definition this run fought. Carried whole because a Boss's stats
    /// are what a reader compares across runs, and because it is the value
    /// <c>B-03</c>/<c>B-04</c> would vary.
    /// </summary>
    public required BossDefinition Boss { get; init; }

    /// <summary>The Card loadout ids the run cast against, in snapshot order.</summary>
    public required IReadOnlyList<string> EquippedCardIds { get; init; }

    /// <summary>
    /// The Relic instance ids equipped, in equip-slot order, or empty when the
    /// battle carries no Relic content.
    /// </summary>
    public required IReadOnlyList<string> EquippedRelicIds { get; init; }

    /// <summary>The full metric set (M-01…M-15).</summary>
    public required BalanceSimulationMetrics Metrics { get; init; }

    /// <summary>
    /// The terminal authoritative state, or <c>null</c> for an
    /// <see cref="BalanceSimulationOutcome.InvalidSimulation"/> run that never
    /// produced one. It is carried so a reader can inspect anything the metric
    /// set does not summarize, without the harness having to anticipate it.
    /// </summary>
    public required BattleState? FinalState { get; init; }
}
