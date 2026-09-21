using GameServer.Domain.Match3;
using Xunit;

namespace GameServer.Domain.Tests;

/// <summary>
/// PCG32 algorithm tests (ADR-009).
///
/// These are <b>reference-vector</b> tests, not "looks random" approximations:
/// ADR-009 requires the canonical PCG32 reference sequence to be implemented
/// exactly, and the negative consequence it names is that "the reference sequence
/// must be implemented exactly (not 'approximately') or determinism breaks".
/// A statistical or shape-based assertion would not detect a substituted
/// algorithm, so every expected value here is a fixed published constant.
///
/// Reference values are taken from O'Neill's canonical PCG32 C implementation
/// (<c>pcg_basic.c</c>: <c>pcg32_srandom_r</c>, <c>pcg32_random_r</c>,
/// <c>pcg32_boundedrand_r</c>), which ADR-009 names as the specification:
/// the <c>PCG32_INITIALIZER</c> state pair and the documented
/// <c>srandom(42, 54)</c> sample sequence.
/// </summary>
public class Pcg32Tests
{
    /// <summary>
    /// The canonical <c>PCG32_INITIALIZER</c> state pair from <c>pcg_basic.h</c>.
    /// </summary>
    private const ulong InitializerState = 0x853c49e6748fea9bUL;
    private const ulong InitializerIncrement = 0xda3e39cb94b95bdbUL;

    [Fact]
    public void NextUInt32_ShouldMatchTheReferenceSequence_FromTheInitializerState()
    {
        // pcg_basic.h defines PCG32_INITIALIZER = { 0x853c49e6748fea9bULL,
        // 0xda3e39cb94b95bdbULL }. The first output of that state pair is
        // 0x152ca78d in the canonical reference implementation.
        var rng = new Pcg32(InitializerState, InitializerIncrement);

        Assert.Equal(0x152ca78du, rng.NextUInt32());
    }

    [Fact]
    public void FromSeed_ShouldMatchTheReferenceSequence_ForSeed42Stream54()
    {
        // The canonical pcg-c-basic sample seeds pcg32_srandom_r(&rng, 42u, 54u)
        // and prints the following values. ADR-009 adopts this sequence as the
        // specification, so these are fixed expected constants.
        var rng = Pcg32.FromSeed(seed: 42UL, stream: 54UL);

        var expected = new uint[]
        {
            2707161783u,
            2068313097u,
            3122475824u,
            2211639955u,
            3215226955u,
            3421331566u,
        };

        foreach (var value in expected)
        {
            Assert.Equal(value, rng.NextUInt32());
        }
    }

    [Fact]
    public void FromSeed_ShouldProduceTheReferenceStatePair_ForSeed42Stream54()
    {
        // pcg32_srandom_r performs: state = 0; inc = (initseq << 1) | 1;
        // step(); state += initstate; step(). For (42, 54) the resulting state is
        // 1753877967969059832 with increment 109.
        var rng = Pcg32.FromSeed(seed: 42UL, stream: 54UL);

        Assert.Equal(1753877967969059832UL, rng.CurrentState.State);
        Assert.Equal(109UL, rng.CurrentState.Increment);
    }

    [Fact]
    public void FromSeed_ShouldDeriveAnOddStreamSelector()
    {
        // ADR-009 state row: the 64-bit stream selector "must *always* be odd"
        // for PCG-XSH-RR 64/32.
        foreach (var stream in new ulong[] { 0UL, 1UL, 2UL, 54UL, ulong.MaxValue - 1 })
        {
            var rng = Pcg32.FromSeed(seed: 42UL, stream: stream);

            Assert.Equal(1UL, rng.CurrentState.Increment & 1UL);
        }
    }

    [Fact]
    public void Multiplier_ShouldBeTheCanonicalPcg32Constant()
    {
        // The LCG multiplier is a fixed part of the algorithm identity
        // (ADR-009), not an implementation choice.
        Assert.Equal(6364136223846793005UL, Pcg32.Multiplier);
    }

    [Fact]
    public void NextUInt32_ShouldAdvanceTheStateByExactlyOneStep()
    {
        // GAME_STATE.md §2.6.2 item 3: RngState changes only when a value is
        // drawn. One draw is exactly one LCG step.
        var rng = new Pcg32(InitializerState, InitializerIncrement);
        var before = rng.CurrentState;

        rng.NextUInt32();

        var expectedState = unchecked((before.State * Pcg32.Multiplier) + before.Increment);
        Assert.Equal(expectedState, rng.CurrentState.State);

        // The stream selector is not affected by drawing (ADR-009 state row).
        Assert.Equal(before.Increment, rng.CurrentState.Increment);
    }

    [Fact]
    public void CurrentState_ShouldBeUnchangedWhenNothingIsDrawn()
    {
        // GAME_STATE.md §2.6.2 item 3: drawing nothing leaves the state
        // unchanged — it is not advanced on a timer or as a side effect.
        var rng = new Pcg32(InitializerState, InitializerIncrement);
        var before = rng.CurrentState;

        Assert.Equal(before, rng.CurrentState);
        Assert.Equal(before, rng.CurrentState);
    }

    [Fact]
    public void ResumingFromAStoredState_ShouldContinueTheStreamIdentically()
    {
        // GAME_STATE.md §2.6.2 item 4: recovering a battle from a snapshot
        // restores the stream exactly — subsequent draws continue identically to
        // the unrecovered battle.
        var uninterrupted = new Pcg32(InitializerState, InitializerIncrement);
        for (var i = 0; i < 10; i++)
        {
            uninterrupted.NextUInt32();
        }

        var resumed = new Pcg32(uninterrupted.CurrentState.State, uninterrupted.CurrentState.Increment);

        for (var i = 0; i < 20; i++)
        {
            Assert.Equal(uninterrupted.NextUInt32(), resumed.NextUInt32());
        }
    }

    [Fact]
    public void SameSeedAndStream_ShouldProduceTheSameStream()
    {
        var first = Pcg32.FromSeed(seed: 12345UL, stream: 0UL);
        var second = Pcg32.FromSeed(seed: 12345UL, stream: 0UL);

        for (var i = 0; i < 100; i++)
        {
            Assert.Equal(first.NextUInt32(), second.NextUInt32());
        }
    }

    [Fact]
    public void DifferentSeeds_ShouldProduceDifferentStreams()
    {
        var first = Pcg32.FromSeed(seed: 1UL);
        var second = Pcg32.FromSeed(seed: 2UL);

        var firstValues = Enumerable.Range(0, 32).Select(_ => first.NextUInt32()).ToArray();
        var secondValues = Enumerable.Range(0, 32).Select(_ => second.NextUInt32()).ToArray();

        Assert.NotEqual(firstValues, secondValues);
    }

    [Fact]
    public void NextBounded_ShouldMatchTheReferenceBoundedSequence()
    {
        // The canonical pcg32_boundedrand_r uses a rejection threshold of
        // -bound % bound. Seeded with (42, 54) and bound 4, the reference
        // implementation yields this sequence.
        var rng = Pcg32.FromSeed(seed: 42UL, stream: 54UL);

        var expected = new uint[] { 3u, 1u, 0u, 3u, 3u, 2u, 1u, 1u, 2u, 0u };

        foreach (var value in expected)
        {
            Assert.Equal(value, rng.NextBounded(4));
        }
    }

    [Fact]
    public void NextBounded_ShouldStayWithinTheBound()
    {
        // Uniform draw over the four Gem types (MATCH3_RULES.md §1.2.4).
        var rng = Pcg32.FromSeed(seed: 7UL);

        for (var i = 0; i < 10_000; i++)
        {
            Assert.InRange(rng.NextBounded(4), 0u, 3u);
        }
    }

    [Fact]
    public void NextBounded_ShouldRejectAZeroBound()
    {
        var rng = Pcg32.FromSeed(seed: 1UL);

        Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextBounded(0));
    }

    [Fact]
    public void Constructor_ShouldRejectAnEvenStreamSelector()
    {
        // ADR-009 state row: the increment "must *always* be odd". An even value
        // is not a valid PCG32 stream selector, so a stored RngState carrying one
        // is a corrupt state rather than a resumable one.
        Assert.Throws<ArgumentOutOfRangeException>(() => new Pcg32(InitializerState, 2UL));
    }
}