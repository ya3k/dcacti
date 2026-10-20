namespace GameServer.Domain.Tests;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using GameServer.Domain.Bosses;
using GameServer.Domain.Match3;

/// <summary>
/// TASK-309 V-1 — the <b>reproducible 10,000-board Boss cast-priority baseline</b>.
///
/// <para>
/// <b>What this measures.</b> <c>BOSS_RULES.md</c> §4.3 / ADR-029 §2.7 select a Boss
/// Card only when <see cref="BossAiEngine"/>'s best board score is <i>strictly</i>
/// below the Boss definition's <c>CastPriorityScore</c> (a tie goes to the board
/// action). This harness generates the same deterministic stream of initial boards
/// the production initializer draws, scores each board's best legal Boss swap with
/// the production engine, and records the resulting board-score distribution. The
/// quantity <c>P(boardScore &lt; threshold)</c> is therefore the <b>board-only</b>
/// gate probability for a candidate cast-priority score at the moment a board is
/// generated — nothing else.
/// </para>
///
/// <para>
/// <b>How it is produced (methodology — must not change).</b>
/// </para>
/// <list type="number">
/// <item>
/// A single <see cref="Pcg32"/> stream is constructed from the explicit master
/// state/increment pair below. The stream is never re-seeded, restarted or
/// perturbed between samples; it is exactly the documented single gameplay PRNG
/// (ADR-009).
/// </item>
/// <item>
/// For each of <see cref="SampleCount"/> samples:
/// <c>BoardGenerator.Generate(rng)</c> draws the initial 8x8 board through the
/// production constrained row-major fill — 64 selections per attempt, retries
/// continuing the same stream, no fallback (MATCH3_RULES.md §1.2.1, §1.5).
/// </item>
/// <item>
/// <c>BossAiEngine.SelectMove(board, rng)</c> enumerates the 112 orthogonal
/// candidates, scores the legal ones with <see cref="BossMoveScoringWeights.Default"/>
/// (ATK 10, DEF 5, HP 20, POWER 10 — COMBAT_RULES.md §2 base outputs), applies the
/// deterministic lowest-index tie-breaker, and — only when the board has zero legal
/// swaps — consumes the same stream for its bounded deadlock reshuffle (ADR-026 §8).
/// The recorded sample is the selected move's <c>Score</c>.
/// </item>
/// <item>
/// The stream continues from <c>selection.RngState</c>: after each selection the
/// next generator is reconstructed from the returned state pair
/// (<c>new Pcg32(state, increment)</c>), exactly as production callers resume a
/// stored <c>RngState</c> (<c>BattleStateService</c> constructs the Boss selection
/// generator the same way). A deadlock reshuffle therefore advances the stream
/// for the following sample; an ordinary selection consumes no RNG and leaves it
/// unchanged.
/// </item>
/// </list>
///
/// <para>
/// <b>Exact stream (must be preserved verbatim).</b>
/// Master state <c>0x5EED030900000001</c>, increment/stream selector <c>0x0309</c>
/// (odd, as PCG-XSH-RR 64/32 requires). This is the <see cref="Pcg32"/> raw
/// state/increment pair — <b>not</b> <see cref="Pcg32.FromSeed"/>, whose canonical
/// <c>pcg32_srandom_r</c> seeding would define a different stream and therefore a
/// different baseline. It intentionally reproduces the TASK-309 operator-recorded
/// exploration run (same stream, same 10,000 samples).
/// </para>
///
/// <para>
/// <b>Recorded result (asserted below so the baseline cannot silently drift).</b>
/// samples = 10,000; min = 30; max = 180; mean = 88.6915; score sum = 886,915;
/// P(boardScore &lt; 25) = 0/10,000 = 0.00%; P(&lt; 85) = 3,697/10,000 = 36.97%;
/// P(&lt; 100) = 6,643/10,000 = 66.43%. Zero deadlock recoveries and zero generation
/// retries were observed on this stream. The deterministic digest is
/// <c>7b477ada5ed81d5f064a9ff752a3d62996a42531f7a465e64bdffab35488f940</c>.
/// The frozen artifact is
/// <c>tasks/artifacts/TASK-309-boss-cast-priority-baseline.json</c>.
/// </para>
///
/// <para>
/// <b>Digest definition (part of the reproducibility contract).</b> SHA-256 over the
/// UTF-8 encoding of the ordered score list encoded invariantly as decimal integers
/// separated by single commas — generation order, no leading definition of the
/// first comma, no trailing comma, no whitespace, no line break. The value is
/// rendered as lowercase hexadecimal.
/// </para>
///
/// <para>
/// <b>What this baseline does NOT claim (read before using the number).</b>
/// </para>
/// <list type="bullet">
/// <item>
/// It is a <b>board-only</b> probability, measured at board-generation time on a
/// fresh initial board. It is <b>not</b> an in-battle cast frequency and must not be
/// reported as one: in a real battle the board has been played (cascades, gravity
/// and spawns mutate it), the Boss response phase only sees that post-action board,
/// and a cast additionally requires Boss Power eligibility and a zero cooldown at
/// the response phase. Those are separate measurements (the in-battle analysis
/// drives real rounds and reads committed state/events).
/// </item>
/// <item>
/// <c>CastPriorityScore = 85</c> is the operator-approved <b>starting value</b> to
/// monitor and adjust from real gameplay data (TASK-309 §1 B-1). It is not globally
/// optimal, it was not tuned to this distribution, and it guarantees no in-battle
/// cast rate — in particular, <b>no ~38% in-battle rate is guaranteed</b> by the
/// 36.97% board-only figure.
/// </item>
/// <item>
/// No value in this distribution is a balance judgement; the baseline exists only so
/// a future change to board generation, scoring weights, tie-breaking, deadlock
/// recovery or the frozen stream is detected as an explicit, reviewed drift.
/// </item>
/// </list>
/// </summary>
public class TASK309BossCastPriorityBoardSimulationTests
{
    /// <summary>
    /// The explicit master PCG state word the baseline stream starts from
    /// (<c>0x5EED030900000001</c>). This is the raw state/increment constructor pair
    /// recorded by the TASK-309 V-1 methodology, not a <c>FromSeed</c> seed.
    /// </summary>
    internal const ulong MasterSeed = 0x5EED030900000001UL;

    /// <summary>
    /// The baseline stream selector/increment (<c>0x0309</c>, odd). Changing it
    /// changes the board stream and therefore the recorded distribution.
    /// </summary>
    internal const ulong StreamIncrement = 0x0309UL;

    /// <summary>The operator-recorded number of sampled boards (TASK-309 §1 V-1).</summary>
    internal const int SampleCount = 10_000;

    /// <summary>
    /// SHA-256 (lowercase hex) of the ordered decimal score list for the full
    /// <see cref="SampleCount"/> sample — the reproducibility anchor.
    /// </summary>
    internal const string ExpectedDigest = "7b477ada5ed81d5f064a9ff752a3d62996a42531f7a465e64bdffab35488f940";

    /// <summary>
    /// One reproducible baseline run: the ordered best-board scores plus the
    /// production-path counters that document the stream's behavior.
    /// </summary>
    /// <param name="Scores">Best Boss board score per sample, in generation order.</param>
    /// <param name="DeadlockRecoveries">
    /// Samples on which <see cref="BossAiEngine.SelectMove"/> found zero legal swaps and
    /// performed its bounded deterministic reshuffle (ADR-026 §8). Recorded, not assumed.
    /// </param>
    /// <param name="BoardsWithGenerationRetry">
    /// Samples whose initial board needed more than one <see cref="BoardGenerator"/>
    /// attempt (MATCH3_RULES.md §1.5). Recorded so a retry-path change is visible.
    /// </param>
    private sealed record BaselineRun(int[] Scores, int DeadlockRecoveries, int BoardsWithGenerationRetry);

    /// <summary>
    /// Runs the frozen baseline stream: production board generation then production
    /// Boss move selection per sample, continuing the one stream.
    /// </summary>
    private static BaselineRun RunBaseline(int sampleCount)
    {
        var scores = new int[sampleCount];
        var deadlockRecoveries = 0;
        var boardsWithGenerationRetry = 0;

        // The explicit state/increment pair (not FromSeed) is the frozen baseline stream.
        var rng = new Pcg32(MasterSeed, StreamIncrement);

        for (var i = 0; i < sampleCount; i++)
        {
            // MATCH3_RULES.md §1.2.1: production deterministic initial board fill.
            var generation = BoardGenerator.Generate(rng);

            // §1.5 has no fallback construction: a failed generation must fail the
            // harness rather than silently shrink the sample.
            if (!generation.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Sample {i}: BoardGenerator.Generate failed without a board; the baseline "
                    + "sample size and digest would be invalid.");
            }

            if (generation.Attempts > 1)
            {
                boardsWithGenerationRetry++;
            }

            // ADR-026 §8: production deterministic scoring, tie-break and (if needed)
            // deadlock reshuffle on the same continuing stream.
            var selection = BossAiEngine.SelectMove(generation.Board!, rng);

            if (selection.Reshuffled)
            {
                deadlockRecoveries++;
            }

            scores[i] = selection.Score;

            // Resume from the committed RngState exactly as production does: rebuild the
            // generator from the returned pair (BattleStateService constructs the Boss
            // selection generator from stored RngState the same way). If no reshuffle
            // occurred the pair is unchanged; if one occurred the advanced pair is carried
            // forward — the stream is never re-seeded.
            rng = new Pcg32(selection.RngState.State, selection.RngState.Increment);
        }

        return new BaselineRun(scores, deadlockRecoveries, boardsWithGenerationRetry);
    }

    /// <summary>
    /// Count of samples whose best board score is strictly below
    /// <paramref name="castPriorityScore"/> — the board-only cast gate.
    /// </summary>
    private static int CountBelow(IReadOnlyList<int> scores, int castPriorityScore)
    {
        var count = 0;
        for (var i = 0; i < scores.Count; i++)
        {
            if (scores[i] < castPriorityScore)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>Board-only gate probability <c>P(boardScore &lt; threshold)</c>.</summary>
    private static double FractionBelow(IReadOnlyList<int> scores, int castPriorityScore) =>
        (double)CountBelow(scores, castPriorityScore) / scores.Count;

    /// <summary>
    /// The documented digest: SHA-256 over UTF-8 of the ordered scores encoded
    /// invariantly as comma-separated decimal integers (generation order, no trailing
    /// comma, lowercase hex).
    /// </summary>
    private static string ComputeDigest(IReadOnlyList<int> scores)
    {
        var builder = new StringBuilder(scores.Count * 4);
        for (var i = 0; i < scores.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            builder.Append(scores[i].ToString(CultureInfo.InvariantCulture));
        }

        var bytes = Encoding.UTF8.GetBytes(builder.ToString());
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    [Fact]
    public void V1_TenThousandBoardBaseline_MatchesTheRecordedDistribution()
    {
        // When: the frozen stream is replayed through production generation + selection.
        var run = RunBaseline(SampleCount);
        var scores = run.Scores;

        // Then: the sample is complete and its aggregates are exactly the recorded ones.
        Assert.Equal(SampleCount, scores.Length);
        Assert.Equal(886_915, scores.Sum()); // mean 88.6915
        Assert.Equal(30, scores.Min());
        Assert.Equal(180, scores.Max());

        // And: the board-only gate counts are exact (strictly "below", per BOSS_RULES §4.3).
        Assert.Equal(0, CountBelow(scores, 25));
        Assert.Equal(3_697, CountBelow(scores, 85));
        Assert.Equal(6_643, CountBelow(scores, 100));

        // And: the corresponding board-only probabilities (0.00%, 36.97%, 66.43%).
        Assert.Equal(0.0000, FractionBelow(scores, 25), 4);
        Assert.Equal(0.3697, FractionBelow(scores, 85), 4);
        Assert.Equal(0.6643, FractionBelow(scores, 100), 4);

        // And: the full score histogram is exact. Scores are weight*cell-count sums
        // (weights are multiples of 5), so only these values occur on this stream.
        var expectedHistogram = new (int Score, int Count)[]
        {
            (30, 4), (40, 14), (45, 24), (50, 26), (55, 24), (60, 1650), (65, 89),
            (70, 176), (75, 646), (80, 1044), (85, 64), (90, 2735), (95, 147),
            (100, 1656), (105, 21), (110, 819), (115, 171), (120, 186), (125, 8),
            (130, 333), (135, 17), (140, 72), (145, 1), (150, 56), (155, 1),
            (160, 7), (170, 8), (180, 1),
        };

        var actualHistogram = scores
            .GroupBy(score => score)
            .OrderBy(group => group.Key)
            .Select(group => (Score: group.Key, Count: group.Count()))
            .ToArray();

        Assert.Equal(expectedHistogram, actualHistogram);

        // And: production-path behavior on the stream is recorded, not assumed. This
        // stream happened to produce no deadlock recovery and no generation retry, but
        // SelectMove still executed the documented deadlock path (zero legal swaps would
        // have reshuffled); the baseline includes that path by construction.
        Assert.Equal(0, run.DeadlockRecoveries);
        Assert.Equal(0, run.BoardsWithGenerationRetry);

        // And: the reproducibility digest matches the frozen artifact.
        Assert.Equal(ExpectedDigest, ComputeDigest(scores));
    }

    [Fact]
    public void V1_FixedStream_ReplaysIdentically()
    {
        // Determinism guard: two independent replays of the frozen stream must produce
        // the same ordered scores and the same digest. This proves the baseline is a
        // function of the documented state/increment pair only (no clock, no ambient
        // randomness, no hidden mutable static state).
        var first = RunBaseline(256);
        var second = RunBaseline(256);

        Assert.True(
            first.Scores.SequenceEqual(second.Scores),
            "Two replays of the frozen stream produced different ordered score lists.");
        Assert.Equal(ComputeDigest(first.Scores), ComputeDigest(second.Scores));
        Assert.Equal(0, first.DeadlockRecoveries);
        Assert.Equal(0, second.DeadlockRecoveries);
    }

    [Fact]
    public void V1_StreamIsDistinctFromAFromSeedStream()
    {
        // The digest anchors the exact stream. This guards the methodology detail that
        // the baseline uses the raw state/increment constructor pair
        // (0x5EED030900000001, 0x0309), not Pcg32.FromSeed (whose canonical
        // pcg32_srandom_r seeding defines a different stream and a different baseline):
        // the two must not be conflated.
        var baseline = RunBaseline(64);
        var fromSeed = Pcg32.FromSeed(MasterSeed, stream: StreamIncrement);

        var fromSeedScores = new int[64];
        for (var i = 0; i < fromSeedScores.Length; i++)
        {
            var generation = BoardGenerator.Generate(fromSeed);
            var selection = BossAiEngine.SelectMove(generation.Board!, fromSeed);
            fromSeedScores[i] = selection.Score;
            fromSeed = new Pcg32(selection.RngState.State, selection.RngState.Increment);
        }

        Assert.NotEqual(ComputeDigest(baseline.Scores), ComputeDigest(fromSeedScores));
    }
}
