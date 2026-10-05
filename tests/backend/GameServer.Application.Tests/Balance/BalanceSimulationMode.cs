namespace GameServer.Application.Tests.Balance;

/// <summary>
/// The simulation modes a balance run can use (<c>TASK-193</c> §5.2).
///
/// The two modes differ only in <b>what the caller varied</b>, never in the
/// rules the simulation executes: both run the same production pipeline
/// unmodified, and the harness contains no candidate balance value in either.
/// </summary>
internal enum BalanceSimulationMode
{
    /// <summary>
    /// Current production rules and current content values, unchanged — the
    /// reference every comparison is made against (<c>H-03</c>).
    /// </summary>
    Baseline = 0,

    /// <summary>
    /// A run whose configuration differs from a baseline in exactly one
    /// caller-supplied parameter (<c>H-07</c>).
    ///
    /// The varied value is <b>caller input</b>. It is never hard-coded here, so
    /// the mode cannot pre-approve or pre-judge any balance proposal: the harness
    /// measures whichever configuration it is handed.
    /// </summary>
    ControlledComparison = 1,
}
