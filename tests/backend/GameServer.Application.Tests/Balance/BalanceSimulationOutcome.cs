namespace GameServer.Application.Tests.Balance;

/// <summary>
/// How one balance simulation run ended (<c>TASK-193</c> §5.3).
///
/// The four values are exhaustive and mutually exclusive. <b>A stalemate is
/// never reinterpreted as victory or defeat</b>: the harness reports what the
/// production battle actually reached, and a non-terminal battle is reported as
/// non-terminal.
/// </summary>
internal enum BalanceSimulationOutcome
{
    /// <summary>
    /// The Boss reached 0 HP. This is the production pipeline's own
    /// determination — <c>BattleWon</c> (<c>GAME_EVENTS.md</c> §2,
    /// <c>BOSS_RULES.md</c> §5 item 4) — not a comparison the harness performs.
    /// </summary>
    Victory = 0,

    /// <summary>
    /// The active Pet reached 0 HP. This is likewise the production pipeline's
    /// own <c>BattleLost</c> determination (<c>GAME_EVENTS.md</c> §2).
    /// </summary>
    Defeat = 1,

    /// <summary>
    /// The configured <see cref="BalanceSimulationConfiguration.MaxTurns"/> was
    /// reached without either terminal event. The battle was still running: this
    /// is the documented non-termination case, reported as its own outcome rather
    /// than folded into victory or defeat.
    /// </summary>
    Stalemate = 2,

    /// <summary>
    /// The run could not proceed — an invalid configuration, an impossible
    /// setup, or an invariant violation.
    ///
    /// <b>It is not a generic error bucket.</b> A production rejection (an
    /// unplayable swap, an unaffordable cast) is ordinary gameplay and is
    /// recorded as such; only a configuration the simulation cannot legally run
    /// reaches this outcome.
    /// </summary>
    InvalidSimulation = 3,
}
