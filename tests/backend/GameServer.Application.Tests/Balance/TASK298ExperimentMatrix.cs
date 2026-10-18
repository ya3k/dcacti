using GameServer.Domain.Bosses;
using GameServer.Domain.Cards;
using GameServer.Domain.Pets;
using GameServer.Domain.Players;

namespace GameServer.Application.Tests.Balance;

/// <summary>
/// The <c>TASK-298</c> experiment matrix — the four controlled parameter groups
/// of <c>TASK-298</c> §4 plus the combined candidate, each run over the
/// <b>unchanged</b> canonical 75-run cell set.
///
/// <code>
/// E0  baseline reproduction   zero deltas — must reproduce TASK-296 bit-for-bit
/// E1  starter card healing + Power economy
///     E1a  card-heal 20% → 10% MaxHP          (healing half only)
///     E1b  card-power-charge 0 → 10 cost,     (economy half only)
///                          +25 → +20 grant
///     E1   both halves together
/// E2  boss regeneration / defensive sustain
///     E2b  Mộc Yêu Passive Threshold 5 → 2 matches   (regen cadence)
/// E3  boss DEF and damage scaling
///     E3   Boss DEF 50 → 0 on the three shared-stat Bosses
///     E3b  Boss DEF 50 → 25 (half) — a smaller scaling step for comparison
/// E5  combined candidate — the smallest set that clears the acceptance targets
/// </code>
///
/// <b>It defines no gameplay rule and computes no gameplay value.</b> It names
/// parameter deltas and hands each cell to the existing
/// <see cref="BalanceSimulator"/>; the production pipeline decides everything,
/// exactly as the canonical matrix does. Nothing here is read by production code.
///
/// <b>The cell set is never varied.</b> Every experiment runs the identical
/// 5 Bosses × 3 policies × 5 seeds of <see cref="BalanceBaselineMatrix"/>, so
/// every outcome is comparable to its TASK-296 counterpart on the same seed.
/// </summary>
internal static class TASK298ExperimentMatrix
{
    /// <summary>
    /// The zero-delta control. Its purpose is to prove the harness applies no
    /// delta when none is named — i.e. that an experiment's difference is the
    /// experiment — and that the run is deterministic end to end.
    /// </summary>
    public static readonly TASK298ExperimentDefinition E0BaselineReproduction = new()
    {
        Id = "E0",
        ParameterGroup = "baseline reproduction (no delta)",
        Hypothesis = "The TASK-298 harness, driven with zero deltas, reproduces the unchanged "
            + "TASK-296 candidate's 75 outcomes exactly; therefore every later difference is the "
            + "named delta and not the harness.",
        IntendedPlayerFacingEffect = "None — a control run. No player-facing behavior is proposed.",
    };

    /// <summary>
    /// <b>Group 1 — starter card healing only.</b> Isolates the healing half of
    /// the sustain equation: the same Power loop stays intact, but the HP a
    /// <c>card-heal</c> returns per cast halves.
    /// </summary>
    public static readonly TASK298ExperimentDefinition E1aHealMagnitude = new()
    {
        Id = "E1a",
        ParameterGroup = "group 1 — starter card healing (card-heal magnitude only)",
        Hypothesis = "The Average policy's sustain loop is bounded by the HP each card-heal "
            + "returns, not by the Power that pays for it. Halving card-heal's magnitude while "
            + "leaving the Power economy at 7.5 HP/Power of headroom leaves Average unable to "
            + "out-heal the Boss, because the loop's per-Turn ceiling falls below the measured "
            + "26.1 HP/Power generation margin.",
        IntendedPlayerFacingEffect = "A Heal card is a meaningful response to a bad Turn rather "
            + "than a permanent shield: healing stays available, but it no longer makes a fight "
            + "unlosable.",
        CardHealPercentMaxHp = 10,
    };

    /// <summary>
    /// <b>Group 1 — Power economy only.</b> Isolates the economy half: the
    /// heal's magnitude is untouched, but it acquires a real price and
    /// <c>card-power-charge</c> stops being a free action.
    /// </summary>
    public static readonly TASK298ExperimentDefinition E1bPowerEconomy = new()
    {
        Id = "E1b",
        ParameterGroup = "group 1 — starter card Power economy (costs and grant only)",
        Hypothesis = "card-power-charge's zero Power cost is the sustain loop's engine: it makes "
            + "Power a strictly non-decreasing resource, so any heal is infinitely repeatable. "
            + "Giving it a non-zero cost and trimming its grant makes the loop a net-loss rate "
            + "that a policy cannot run forever, without touching what a Heal returns.",
        IntendedPlayerFacingEffect = "Power becomes a resource to spend rather than one to farm: "
            + "Power Charge is still the recovery option (never blocked by insufficient Power), "
            + "but farming it no longer pays for itself.",
        CardPowerChargePowerCost = 10,
        CardPowerChargeGrant = 20,
    };

    /// <summary>
    /// <b>Group 1 — both halves.</b> The group's own candidate: break the loop
    /// from both ends at once.
    /// </summary>
    public static readonly TASK298ExperimentDefinition E1HealAndEconomy = new()
    {
        Id = "E1",
        ParameterGroup = "group 1 — starter card healing and Power economy (both halves)",
        Hypothesis = "Neither half alone is sufficient: the magnitude bounds the heal, the "
            + "economy bounds how often it can be paid for. Together the loop's ceiling "
            + "(10% MaxHP per ~12 Turns of farming) is far below the Boss's throughput.",
        IntendedPlayerFacingEffect = "The starter kit is a sustain option with a cost, matching "
            + "the Boss's own DEF-gem and HP-gem sustain rather than exceeding it.",
        CardHealPercentMaxHp = 10,
        CardPowerChargePowerCost = 10,
        CardPowerChargeGrant = 20,
    };

    /// <summary>
    /// <b>Group 2 — Boss regeneration cadence.</b> Mộc Yêu's <c>5%</c> MaxHP per
    /// match-charged trigger is applied every
    /// <see cref="BalanceBaselineMatrix"/>'s historical Threshold of <c>5</c>
    /// Matches — the matrix deliberately pins that value for historical
    /// reproducibility, while <c>BOSS_RULES.md</c> §6.2.3 authors <c>8</c>.
    /// </summary>
    public static readonly TASK298ExperimentDefinition E2bMocYeuRegenCadence = new()
    {
        Id = "E2b",
        ParameterGroup = "group 2 — Boss regeneration cadence (Mộc Yêu Passive Threshold only)",
        Hypothesis = "Mộc Yêu's regeneration floor is set by its trigger cadence, not by the 5% "
            + "magnitude: at 5 Matches the Passive fires often enough inside one Turn's cascade "
            + "that its heal can exceed the Turn's entire player damage. The documented §6.2.3 "
            + "cadence of 8 spreads the same 5% over 60% more Matches.",
        IntendedPlayerFacingEffect = "Mộc Yêu's regeneration reads as a pressure clock the player "
            + "can out-pace by playing well, rather than a floor that rises with the player's "
            + "own match count.",
        MocYeuPassiveThreshold = 8,
    };

    /// <summary>
    /// <b>Group 3 — Boss DEF.</b> Removes the flat mitigation tax the three
    /// shared-stat Bosses carry, matching the two Bosses
    /// (<c>BOSS_RULES.md</c> §6.1: Sơn Thạch Vệ, Kim Lôi Vương) that already
    /// author <c>DEF = 0</c>.
    /// </summary>
    public static readonly TASK298ExperimentDefinition E3BossDefenseZero = new()
    {
        Id = "E3",
        ParameterGroup = "group 3 — Boss DEF (the §6.1 shared row's DEF 50 → 0)",
        Hypothesis = "DEF 50 is a flat ×0.667 tax on the player's only scalable damage source "
            + "(ATK-match damage) with no player-side counterplay, because the player has no "
            + "DEF-ignoring or DEF-reducing option and the Boss pays it trivially against the "
            + "Pet's DEF 25. Removing it raises player damage throughput 50% without changing "
            + "the Boss's own offense.",
        IntendedPlayerFacingEffect = "Matching ATK gems is worth what the rules say it is worth; "
            + "the fight is decided by board play and card choices rather than by an "
            + "uninteractable defensive tax.",
        DefensiveBossDefense = 0,
    };

    /// <summary>
    /// <b>Group 3 — half step.</b> The same manipulation at a smaller magnitude,
    /// so the DEF axis has a measured response curve rather than one point.
    /// </summary>
    public static readonly TASK298ExperimentDefinition E3bBossDefenseHalf = new()
    {
        Id = "E3b",
        ParameterGroup = "group 3 — Boss DEF half step (the shared row's DEF 50 → 25)",
        Hypothesis = "If the DEF axis is a dominant term, a half-sized delta should move the "
            + "outcome distribution materially but not resolve it. A near-identical distribution "
            + "would falsify the hypothesis and redirect tuning away from DEF.",
        IntendedPlayerFacingEffect = "A partial reduction of the defensive tax, for comparison only.",
        DefensiveBossDefense = 25,
    };

    /// <summary>
    /// <b>Group 3 — damage scaling on the Boss HP axis.</b> Group 3 of
    /// <c>TASK-298</c> §4 is "Boss DEF <b>and damage scaling</b>". The measured
    /// DEF axis is worth roughly a ×1.5 throughput (≈ +2 kills on Hỏa Long); the
    /// HP axis is the size of the health bar the same throughput must drain, so it
    /// is the second, independent lever on encounter length.
    ///
    /// <b>It is stated as its own experiment rather than folded into the DEF
    /// delta</b>, so the two terms remain separable in the evidence.
    /// </summary>
    public static readonly TASK298ExperimentDefinition E4BossHpScaling = new()
    {
        Id = "E4",
        ParameterGroup = "group 3 — Boss damage scaling (MaxHP on the §6.1 shared row)",
        Hypothesis = "Once sustain is bounded (group 1), the remaining term is throughput × "
            + "health: the shared row's 5000 MaxHP is the largest single health bar in the set "
            + "and permits no meaningful player counterplay, since match-only rules give the "
            + "player no HP-reduction option. Scaling it to the authored long-boss ratio "
            + "(the two authored Bosses carry 2800 and 3000 for ATK 140/120) shortens the "
            + "encounter without touching player or Boss sustain.",
        IntendedPlayerFacingEffect = "Hỏa Long, Thủy Ma, and Mộc Yêu are fights the player can "
            + "finish inside a reasonable session rather than bars the match-only economy "
            + "cannot drain.",
        SharedRowBossMaxHp = 3000,
    };

    /// <summary>
    /// <b>Combined candidate.</b> The union of the group candidates that the
    /// group experiments show to be necessary. It is derived from the experiment
    /// results, never from a target percentage — if the groups do not justify a
    /// member, it does not appear here.
    /// </summary>
    public static readonly TASK298ExperimentDefinition E5Combined = new()
    {
        Id = "E5",
        ParameterGroup = "combined candidate — group 1 + group 2 + group 3 (DEF and MaxHP)",
        Hypothesis = "The groups act on independent terms of the encounter — player sustain "
            + "rate (group 1), Boss sustain rate (group 2), and player damage throughput against "
            + "the Boss's health bar (group 3). The combined candidate should clear the "
            + "acceptance targets where no single group does.",
        IntendedPlayerFacingEffect = "Encounters resolve in tens of Turns, all five Bosses stay "
            + "defeatable, no policy is trapped in a repeatable sustain loop, and Passive / "
            + "Average / Skilled remain distinguishable.",
        CardHealPercentMaxHp = 10,
        CardPowerChargePowerCost = 10,
        CardPowerChargeGrant = 20,
        MocYeuPassiveThreshold = 8,
        DefensiveBossDefense = 0,
        SharedRowBossMaxHp = 3000,
    };

    /// <summary>
    /// Every experiment the task evaluates, in evaluation order. <c>E5</c> is
    /// listed last because it is only meaningful after the groups it combines.
    /// </summary>
    public static readonly IReadOnlyList<TASK298ExperimentDefinition> All =
    [
        E0BaselineReproduction,
        E1aHealMagnitude,
        E1bPowerEconomy,
        E1HealAndEconomy,
        E2bMocYeuRegenCadence,
        E3bBossDefenseHalf,
        E3BossDefenseZero,
        E4BossHpScaling,
        E5Combined,
    ];

    /// <summary>Resolves an experiment by its recorded identity.</summary>
    /// <exception cref="ArgumentOutOfRangeException">No such experiment.</exception>
    public static TASK298ExperimentDefinition ById(string id) =>
        All.FirstOrDefault(experiment => string.Equals(experiment.Id, id, StringComparison.Ordinal))
        ?? throw new ArgumentOutOfRangeException(
            nameof(id),
            id,
            $"TASK-298 defines no experiment '{id}'. Known ids: {string.Join(", ", All.Select(e => e.Id))}.");

    /// <summary>
    /// The complete input set for one experiment at one canonical matrix cell.
    ///
    /// <b>It is the canonical cell with only the experiment's deltas applied.</b>
    /// The seed, the battle-owner identity, the Pet configuration (identity,
    /// Element, Passive, passive threshold, and the equipped Card and Relic
    /// snapshots), the policy, and the Turn bound are taken from
    /// <see cref="BalanceBaselineMatrix.Configuration"/> unchanged; the Card
    /// content set and the Boss definition are the canonical ones with the
    /// experiment's deltas.
    /// </summary>
    public static BalanceSimulationConfiguration Configuration(
        TASK298ExperimentDefinition experiment,
        BossDefinition canonicalBoss,
        string policyName,
        ulong seed)
    {
        var canonical = BalanceBaselineMatrix.Configuration(canonicalBoss, policyName, seed);

        return canonical with
        {
            Boss = experiment.ApplyToBoss(canonicalBoss),
            CardDefinitions = experiment.ApplyToCards(),
        };
    }

    /// <summary>
    /// Runs one experiment at one canonical matrix cell through the existing
    /// TASK-193 harness (which drives the production pipeline) against an
    /// isolated in-process store.
    /// </summary>
    public static Task<BalanceSimulationResult> RunAsync(
        TASK298ExperimentDefinition experiment,
        BossDefinition canonicalBoss,
        string policyName,
        ulong seed) =>
        new BalanceSimulator(
            Configuration(experiment, canonicalBoss, policyName, seed),
            new InMemoryBattleStateRepository()).RunAsync();
}
